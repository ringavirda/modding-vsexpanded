using System.Collections.Generic;
using System.Text;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace SteelIndustryExpanded.BlockStructures.HotBlastFurnace.BlockEntities;

/// <summary>
/// Bell hopper, sitting beneath the reinforced hopper. Pulls ready-made burden from the tank above into
/// an internal magazine and drips it into the furnace shaft while dropping is enabled. The burden's grade
/// (its blast-mix proportions) rides along, so the furnace core reads the charge the burdenmaker stamped.
/// </summary>
[BlockEntityRegister]
public class BlockEntityHopperBell : BlockEntity {
  private long _tickId;

  // The magazine is one burden stack (grade carried in its attributes), pulled from the tank above and
  // dripped below. Null when empty.
  private ItemStack? _magazine;

  // The furnace this bell charges, resolved by the bounded multiblock scan every furnace part uses. The
  // core owns the shaft geometry and answers with its own structure-local columns.
  private MultiblockAnchorLink<BlockEntityFurnaceCore>? _anchor;

  private MultiblockAnchorLink<BlockEntityFurnaceCore> Anchor =>
    _anchor ??= new MultiblockAnchorLink<BlockEntityFurnaceCore>(
      this,
      BlockEntityFurnaceCore.ComponentScanHorizontal,
      BlockEntityFurnaceCore.ComponentScanBelow,
      BlockEntityFurnaceCore.ComponentScanAbove
    );

  /// <summary>The burden stack buffered in the magazine, or null when empty, for the block's break
  /// drops. The caller must not mutate it - clone first.</summary>
  public ItemStack? MagazineContents => _magazine;

  // Defaults to on, so a freshly built furnace feeds itself before the player finds the
  // Ctrl + right-click toggle.
  private bool _isDropping = true;

  /// <summary>Burden units currently buffered in the magazine.</summary>
  public int BlastMixMagazine => _magazine?.StackSize ?? 0;

  /// <summary>Maximum burden the magazine can hold.</summary>
  public int MaxMagazineCapacity => SiexValues.HopperMaxMagazineCapacity;

  /// <summary>Whether the hopper is dripping burden into the furnace.</summary>
  public bool IsDropping {
    get => _isDropping;
    set {
      if (_isDropping == value)
        return;
      _isDropping = value;
      if (Api?.Side == EnumAppSide.Server) {
        if (_isDropping)
          StartTicking();
        else
          StopTicking();
      }
    }
  }

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);

    if (api.Side == EnumAppSide.Server && _isDropping)
      StartTicking();
  }

  private void StartTicking() {
    if (_tickId == 0 && Api != null)
      _tickId = RegisterGameTickListener(OnServerTick, 1000);
  }

  private void StopTicking() {
    if (_tickId != 0 && Api != null) {
      UnregisterGameTickListener(_tickId);
      _tickId = 0;
    }
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    _magazine = tree.GetItemstack("magazine");
    _magazine?.ResolveBlockOrItem(worldForResolving);
    if (_magazine?.Collectible == null || _magazine.StackSize <= 0)
      _magazine = null;
    IsDropping = tree.GetBool("isDropping", true);
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    if (_magazine != null)
      tree.SetItemstack("magazine", _magazine);
    tree.SetBool("isDropping", IsDropping);
  }

  /// <summary>Maps the magazine's burden stack, so a bell hopper pasted into another world resolves
  /// its buffered charge against that world's item ids rather than this one's.</summary>
  public override void OnStoreCollectibleMappings(
    Dictionary<int, AssetLocation> blockIdMapping,
    Dictionary<int, AssetLocation> itemIdMapping
  ) =>
    _magazine?.Collectible?.OnStoreCollectibleMappings(
      Api.World,
      new DummySlot(_magazine),
      blockIdMapping,
      itemIdMapping
    );

  public override void OnLoadCollectibleMappings(
    IWorldAccessor worldForResolve,
    Dictionary<int, AssetLocation> oldBlockIdMapping,
    Dictionary<int, AssetLocation> oldItemIdMapping,
    int schematicSeed,
    bool resolveImports
  ) {
    // A false return means the destination world has no such item/block; FixMapping leaves Id at the
    // source world's value, which would resolve to whatever owns that id there. Null the stack instead
    // of keeping a mis-resolved one, matching vanilla's BEIngotMold.cs:806-809.
    if (
      _magazine?.FixMapping(
        oldBlockIdMapping,
        oldItemIdMapping,
        worldForResolve
      ) == false
    )
      _magazine = null;
  }

  private void OnServerTick(float dt) {
    PullFromTankAbove();
    DripIntoShaft();
  }

  // Draws ready-made burden from the reinforced tank above into the magazine. One grade at a time: a
  // different grade waits until the magazine drains.
  private void PullFromTankAbove() {
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
  /// One drop: lays <c>HopperDropAmount</c> of the magazine onto the column the furnace nominates
  /// (<see cref="BlockEntityFurnaceCore.NextChargeColumn"/>: lowest column first, fuel only onto burden).
  /// <para>
  /// <c>BlastMixMagazine</c> is a stack size and <c>room</c> is column units; on this furnace they are the
  /// same currency (<c>ChargeUnitsPerBlock = ChargeItemsPerBand × BandsPerBlock</c>). A fuel's carbon
  /// weight (<c>CarbonPerUnit</c>: 1.0 coke, 0.5 charcoal) is applied where the raceway reads the band,
  /// not where it is laid, so charging is volumetric and identical for both fuels.
  /// </para>
  /// </summary>
  private void DripIntoShaft() {
    int dropAmount = SiexValues.HopperDropAmount;
    if (_magazine == null || BlastMixMagazine < dropAmount)
      return;
    if (Anchor.Resolve() is not { } core)
      return;

    // A stack whose item no longer resolves reads null here. Reachable on a live tick path, so it holds
    // rather than throws.
    string? material = _magazine.Collectible?.Code?.ToShortString();
    if (string.IsNullOrEmpty(material) || !core.IsChargeCode(material))
      return;

    ChargeColumn? column = core.NextChargeColumn(material, out int room);
    if (column == null || room < dropAmount)
      return; // shaft full, or the band order refuses this material anywhere - hold the magazine

    // The grade (the burden's flux ratio) is read off the magazine and pushed with the charge, so it
    // reaches the raceway intact.
    column.Push(
      material,
      dropAmount,
      core.ChargeTemperature,
      Burden.Read(_magazine)
    );
    core.SyncChargeBlocks();
    core.MarkDirty(true); // the hoppers' readouts read the core's shaft total

    _magazine.StackSize -= dropAmount;
    if (_magazine.StackSize <= 0)
      _magazine = null;

    ExParticles.FallingDust(Api.World, Pos);
    Api.World.PlaySoundAt(ExSounds.StoneCrush, Pos.X, Pos.Y, Pos.Z);
    MarkDirty(true);
  }

  /// <summary>
  /// Whether every column of the furnace below is at its own capacity. Tested per column rather than
  /// against one shaft total, because a hearth well gives some columns a cell more than the rest.
  /// False when there is no furnace under the bell.
  /// </summary>
  public bool IsFurnaceFull() {
    if (Api == null || Anchor.Resolve() is not { } core)
      return false;

    bool any = false;
    foreach (var ((x, z), column) in core.ShaftColumns) {
      any = true;
      if (column.TotalUnits < core.ColumnCapacity(x, z))
        return false;
    }
    return any;
  }

  // Same item and same stamped grade: the magazine holds one grade at a time, as the tank above does.
  private bool IsMergeable(ItemStack stack) =>
    _magazine != null
    && stack.Collectible == _magazine.Collectible
    && Burden.Read(stack).Equals(Burden.Read(_magazine));

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(
      Lang.Get(
        "siex:hopper-info-bell",
        IsDropping
          ? Lang.Get("siex:hopper-state-dropping")
          : Lang.Get("siex:hopper-state-stopped")
      )
    );
    dsc.AppendLine(
      Lang.Get(
        "siex:hopper-info-magazine",
        BlastMixMagazine,
        MaxMagazineCapacity
      )
    );
  }
}
