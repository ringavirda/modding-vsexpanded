using System.Text;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;

/// <summary>
/// Block entity for the bell hopper beneath the reinforced hopper. It no longer mixes anything: it pulls
/// the ready-made burden from the reinforced tank above into an internal magazine, then drips that burden
/// down into the furnace shaft while dropping is enabled. The burden's grade (its blast-mix proportions)
/// rides along, so the furnace core reads the same charge the burdenmaker stamped.
/// </summary>
[BlockEntityRegister]
public class BlockEntityHopperBell : BlockEntity
{
  private long _tickId;

  // The magazine is one burden stack (grade = its attributes), pulled from the tank above and dripped
  // below. Null when empty.
  private ItemStack? _magazine;

  // The furnace this bell charges, resolved by the same bounded multiblock scan every furnace part uses.
  //
  // The shaft geometry is the furnace core's: the core answers with its own columns, keyed
  // structure-local. The bell must not derive a plane scan of its own - that would be a second copy of
  // geometry the core already owns, right for exactly one layout at exactly one facing, and it would get
  // out of step with a redrawn shaft.
  private MultiblockAnchorLink<BlockEntityFurnaceCore>? _anchor;

  private MultiblockAnchorLink<BlockEntityFurnaceCore> Anchor =>
    _anchor ??= new MultiblockAnchorLink<BlockEntityFurnaceCore>(
      this,
      BlockEntityFurnaceCore.ComponentScanHorizontal,
      BlockEntityFurnaceCore.ComponentScanBelow,
      BlockEntityFurnaceCore.ComponentScanAbove
    );

  /// <summary>The burden stack buffered in the magazine (or null when empty), for the block's break
  /// drops. The caller must not mutate it - clone first.</summary>
  public ItemStack? MagazineContents => _magazine;

  // Dropping is on by default so a freshly built furnace feeds itself without the player having to
  // discover the Ctrl + right-click toggle first.
  private bool _isDropping = true;

  /// <summary>Burden units currently buffered in the magazine.</summary>
  public int BlastMixMagazine => _magazine?.StackSize ?? 0;

  /// <summary>Maximum burden the magazine can hold.</summary>
  public int MaxMagazineCapacity => SmexValues.HopperMaxMagazineCapacity;

  /// <summary>Whether the hopper is dripping burden into the furnace.</summary>
  public bool IsDropping
  {
    get => _isDropping;
    set
    {
      if (_isDropping == value)
        return;
      _isDropping = value;
      if (Api?.Side == EnumAppSide.Server)
      {
        if (_isDropping)
          StartTicking();
        else
          StopTicking();
      }
    }
  }

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);

    if (api.Side == EnumAppSide.Server && _isDropping)
      StartTicking();
  }

  private void StartTicking()
  {
    if (_tickId == 0 && Api != null)
      _tickId = RegisterGameTickListener(OnServerTick, 1000);
  }

  private void StopTicking()
  {
    if (_tickId != 0 && Api != null)
    {
      UnregisterGameTickListener(_tickId);
      _tickId = 0;
    }
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    _magazine = tree.GetItemstack("magazine");
    _magazine?.ResolveBlockOrItem(worldForResolving);
    if (_magazine?.Collectible == null || _magazine.StackSize <= 0)
      _magazine = null;
    IsDropping = tree.GetBool("isDropping", true);
  }

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    if (_magazine != null)
      tree.SetItemstack("magazine", _magazine);
    tree.SetBool("isDropping", IsDropping);
  }

  private void OnServerTick(float dt)
  {
    PullFromTankAbove();
    DripIntoShaft();
  }

  // Draw ready-made burden from the reinforced tank above into the magazine, respecting the single-grade
  // rule (a different grade waits until the magazine drains).
  private void PullFromTankAbove()
  {
    if (
      Api.World.BlockAccessor.GetBlockEntity(Pos.UpCopy())
      is not BlockEntityHopperReinforced top
    )
      return;

    int space = MaxMagazineCapacity - BlastMixMagazine;
    if (space <= 0)
      return;

    ItemStack? peek = top.PeekTank();
    if (peek == null || (_magazine != null && !IsMergeable(peek)))
      return;

    ItemStack? drawn = top.DrawBurden(space);
    if (drawn == null)
      return;

    if (_magazine == null)
      _magazine = drawn;
    else
      _magazine.StackSize += drawn.StackSize;
    MarkDirty(true);
  }

  /// <summary>
  /// One drop: lays <c>HopperDropAmount</c> of the magazine onto the column the furnace nominates.
  /// <para>
  /// The selection rule is the furnace's, not this block's
  /// (<see cref="BlockEntityFurnaceCore.NextChargeColumn"/> - lowest column first, fuel only onto burden).
  /// The bell keeps only what is genuinely its own: the magazine, the pull from the tank above, and the
  /// drop cadence with its stop toggle. Duplicating the rule here is how the two hoppers would come to
  /// charge the same shaft differently.
  /// </para>
  /// <para>
  /// The item/unit comparisons below stay honest with fuel coming through here too. <c>BlastMixMagazine</c>
  /// is a stack size and <c>room</c> is column units, and on this furnace the two are the same currency by
  /// construction: an ore-scale charge is counted in items
  /// (<c>ChargeUnitsPerBlock = ChargeItemsPerBand × BandsPerBlock</c>), so one item of coke is one unit of
  /// coke exactly as one item of burden is one unit of burden. A fuel's carbon <em>weight</em>
  /// (<c>CarbonPerUnit</c>: 1.0 coke, 0.5 charcoal) is applied where the raceway reads the band, never
  /// where it is laid - so charging is volumetric and identical for both fuels, which is what makes a
  /// charcoal campaign the same number of loads for less iron rather than fewer loads.
  /// <para>
  /// The one furnace where item ≠ unit is the cupola (3 000 metal units a block), and a bell can
  /// never charge one: the anchor link demands <c>OwnsCell</c>, and no cupola drawing carries a bell cell.
  /// If that ever changes, this arithmetic is the first thing that breaks.
  /// </para>
  /// </para>
  /// </summary>
  private void DripIntoShaft()
  {
    int dropAmount = SmexValues.HopperDropAmount;
    if (_magazine == null || BlastMixMagazine < dropAmount)
      return;
    if (Anchor.Resolve() is not { } core)
      return;

    // A stack whose item no longer resolves reads null here - a live world state on a tick path, so it
    // holds rather than throws.
    string? material = _magazine.Collectible?.Code?.ToShortString();
    if (string.IsNullOrEmpty(material) || !core.IsChargeCode(material))
      return;

    ChargeColumn? column = core.NextChargeColumn(material, out int room);
    if (column == null || room < dropAmount)
      return; // shaft full, or the band order refuses this material anywhere - hold the magazine

    // The grade rides along, read off the magazine: burden's one surviving quality is its flux ratio
    // and it has to reach the raceway intact.
    column.Push(material, dropAmount, core.ChargeTemperature, Burden.Read(_magazine));
    core.SyncChargeBlocks();
    core.MarkDirty(true); // the shaft total is the core's, and the hoppers' readouts read it

    _magazine.StackSize -= dropAmount;
    if (_magazine.StackSize <= 0)
      _magazine = null;

    ExParticles.FallingDust(Api.World, Pos);
    Api.World.PlaySoundAt(ExSounds.StoneCrush, Pos.X, Pos.Y, Pos.Z);
    MarkDirty(true);
  }

  /// <summary>
  /// Whether the furnace below has no room left - <b>every</b> column at its own capacity.
  /// <para>
  /// Per column, not one figure for the furnace: a hearth with a well in it gives some columns a cell
  /// more than the rest, so "the shaft holds N" would read full while three columns still had room.
  /// </para>
  /// <para>
  /// A bell over no furnace answers <b>false</b>, which is the honest reading of "is the shaft full" -
  /// there is no shaft. The drip refuses separately, on the same missing anchor.
  /// </para>
  /// </summary>
  public bool IsFurnaceFull()
  {
    if (Api == null || Anchor.Resolve() is not { } core)
      return false;

    bool any = false;
    foreach (var ((x, z), column) in core.ShaftColumns)
    {
      any = true;
      if (column.TotalUnits < core.ColumnCapacity(x, z))
        return false;
    }
    return any;
  }

  // Same item and same stamped grade - the magazine holds one grade at a time, mirroring the tank above.
  private bool IsMergeable(ItemStack stack) =>
    _magazine != null
    && stack.Collectible == _magazine.Collectible
    && Burden.Read(stack).Equals(Burden.Read(_magazine));

  public override void OnBlockRemoved()
  {
    base.OnBlockRemoved();
    StopTicking();
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(
      Lang.Get(
        "smex:hopper-info-bell",
        IsDropping
          ? Lang.Get("smex:hopper-state-dropping")
          : Lang.Get("smex:hopper-state-stopped")
      )
    );
    dsc.AppendLine(
      Lang.Get(
        "smex:hopper-info-magazine",
        BlastMixMagazine,
        MaxMagazineCapacity
      )
    );
  }
}
