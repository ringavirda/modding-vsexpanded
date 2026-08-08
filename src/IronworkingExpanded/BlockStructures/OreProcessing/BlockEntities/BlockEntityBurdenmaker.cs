using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Blocks.Construction;
using ExpandedLib.Materials;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.Compat;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.OreProcessing.BlockEntities;

/// <summary>
/// The burdenmaker's block entity: two hoppers over a shared basin, with one sliding gate between them.
/// Construction is handled by the <c>RightClickConstructable</c> behaviour, which suppresses the default
/// mesh, so the machine renders through a permanent <c>closed</c> animation re-tessellated to the
/// currently-built elements.
/// <para>
/// <b>Crate semantics, and they are the design rather than a simplification.</b> Materials go in and come
/// out freely, one or a stack at a time, with no batch state to get stuck in. The ore mixer's
/// <c>drainfirst</c> / <c>nothingmixed</c> / <c>wrongfamily</c> refusals all disappear because there is no
/// batch, no lock and no cycle to interrupt - only "is there room" and "is the basin clear".
/// </para>
/// <para>
/// <b>Storage is a real multi-slot inventory on purpose.</b> Holding each tank as a bare unit count would
/// have been less code, but <see cref="BlockEntityContainer"/>'s break-spill is what makes
/// <c>docs/design/machines/burdenmaker.md</c> § Drops true - <b>everything comes back</b> - with no custom
/// <c>GetDrops</c> to forget. The ore mixer returned <em>nothing</em> and could silently destroy up to 512
/// units of raw charge on break; under R2 that was always wrong, and with no batch state there is not even
/// an excuse for it.
/// </para>
/// <para>
/// <b>The idle clip is <c>closed</c>, not <c>idle</c></b> - the gate's shut pose doubles as the machine's
/// resting one - and it must keep <b>running</b>: RCC draws no mesh of its own, so a clip that eases out
/// takes the whole machine's mesh with it. Its sibling <c>open</c> is a held pose cleared by
/// <c>StopAnimation</c>. Both endings are set in <c>scripts/convert-shape.py</c>, the only place that can see
/// them - <b>no C# test in this repo can</b>, because the rewrite happens at export.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityBurdenmaker : BlockEntityContainer
{
  // Slot layout. Fixed ranges rather than a free-for-all, so "which tank" is a property of the index and
  // never has to be re-derived from a slot's contents (which are empty exactly when you need to know).
  //
  // Slot counts are sized so the unit cap always binds first, never the slots. Too few slots (say 4
  // ore slots x a 64-stack item = 256 units against a configured capacity of 512) makes the config key
  // promise twice what the machine can hold - a number that means one thing and does another - and it
  // is invisible until someone fills the hopper. `A_full_hopper_takes_no_more` is the case that sees it.
  //
  // The count cannot be derived at compile time, because `MaxStackSize` belongs to whatever item the
  // player loads and varies per ore. So the divisor here is a deliberately conservative worst case (64);
  // a 128-stack ore simply leaves slots spare, which costs nothing. The pinning test is what keeps the two
  // numbers honest with each other, and it must move whenever a capacity does.
  private const int OreSlots = 8; // 512 u
  private const int FluxSlots = 4; // 205 u
  private const int BunkerSlots = 18; // 1152 u

  private const int OreFirst = 0;
  private const int FluxFirst = OreFirst + OreSlots;
  private const int BunkerFirst = FluxFirst + FluxSlots;
  private const int TotalSlots = BunkerFirst + BunkerSlots;

  private readonly InventoryBurdenmaker _inventory;

  private ConstructedAnimator? _animator;

  private bool _gateOpen;

  public override InventoryBase Inventory => _inventory;

  public override string InventoryClassName => "burdenmaker";

  /// <summary>True once the player has finished all five construction stages.</summary>
  public bool IsConstructed => _animator?.IsConstructed ?? false;

  /// <summary>Whether the sliding lid is drawn back.</summary>
  public bool GateOpen => _gateOpen;

  public int OreUnits => UnitsIn(OreFirst, OreSlots);

  public int FluxUnits => UnitsIn(FluxFirst, FluxSlots);

  public int BurdenUnits => UnitsIn(BunkerFirst, BunkerSlots);

  public BlockEntityBurdenmaker()
  {
    _inventory = new InventoryBurdenmaker(TotalSlots) { Machine = this };
  }

  #region Lifecycle

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    _inventory.LateInitialize(
      InventoryClassName + "-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z,
      api
    );

    // Resolved on both sides (IsConstructed gates server-side logic); it only builds and poses on the client.
    _animator = new ConstructedAnimator(this, () => AnimCacheKey);
    _animator.Initialize(ApplyPose);
  }

  // Lazy, and it must stay lazy: a wrench rotation changes the variant, and a key captured at Initialize
  // would keep handing back the old orientation's cached mesh.
  private string AnimCacheKey =>
    "burdenmaker-" + Block.Variant["side"] + (_gateOpen ? "-open" : "-closed");

  public override void OnBlockRemoved()
  {
    _animator?.Dispose();
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded()
  {
    _animator?.Dispose();
    base.OnBlockUnloaded();
  }

  /// <summary>
  /// Holds the machine visible via a permanent pose (RCC draws no mesh of its own): <c>closed</c> at rest,
  /// <c>open</c> while the lid is drawn back.
  /// </summary>
  private void ApplyPose()
  {
    string clip = _gateOpen ? "open" : "closed";
    _animator?.Pose(util =>
    {
      util.StopAnimation(_gateOpen ? "closed" : "open");
      util.StartAnimation(
        new AnimationMetaData
        {
          Animation = clip,
          Code = clip,
          AnimationSpeed = 1f,
          EaseInSpeed = 3f,
          EaseOutSpeed = 3f,
        }.Init()
      );
    });
  }

  #endregion

  #region What each hopper takes

  /// <summary>
  /// Whether <paramref name="stack"/> belongs in the wide hopper: crushed or roasted iron ore.
  /// <para>
  /// Resolved through <see cref="IronOreCompat"/> rather than by code prefix, so the modded ores other
  /// mods contribute to the registry are accepted here exactly as the furnace accepts them.
  /// </para>
  /// </summary>
  public static bool IsOre(ItemStack? stack) =>
    stack?.Collectible?.Code is { } code && IronOreCompat.IsCrushedIronOre(code.Path);

  /// <summary>Whether <paramref name="stack"/> belongs in the narrow hopper: lime and the like.</summary>
  public static bool IsFlux(ItemStack? stack) =>
    stack?.Collectible?.Code is { } code
    && MaterialRoleRegistry.IsRole(Roles.Flux, code);

  #endregion

  #region Loading and taking

  /// <summary>Moves ore from <paramref name="from"/> into the wide hopper. Returns false if nothing moved.</summary>
  public bool TryLoadOre(ItemSlot from, bool wholeStack = false) =>
    TryLoad(from, wholeStack, IsOre, OreFirst, OreSlots, IwexValues.BurdenmakerOreCapacity);

  /// <summary>Moves flux from <paramref name="from"/> into the narrow hopper. Returns false if nothing moved.</summary>
  public bool TryLoadFlux(ItemSlot from, bool wholeStack = false) =>
    TryLoad(from, wholeStack, IsFlux, FluxFirst, FluxSlots, IwexValues.BurdenmakerFluxCapacity);

  /// <summary>Takes one stack back out of the wide hopper, or null when it is empty.</summary>
  public ItemStack? TryTakeOre() => TakeFrom(OreFirst, OreSlots);

  /// <summary>Takes one stack back out of the narrow hopper, or null when it is empty.</summary>
  public ItemStack? TryTakeFlux() => TakeFrom(FluxFirst, FluxSlots);

  /// <summary>Takes one stack of finished burden out of the basin, or null when it is empty.</summary>
  public ItemStack? TryWithdrawBurden() => TakeFrom(BunkerFirst, BunkerSlots);

  private bool TryLoad(
    ItemSlot from,
    bool wholeStack,
    System.Func<ItemStack?, bool> accepts,
    int first,
    int count,
    int capacity
  )
  {
    if (from?.Itemstack is not { } held || !accepts(held))
      return false;

    int room = capacity - UnitsIn(first, count);
    if (room <= 0)
      return false;

    int wanted = Math.Min(wholeStack ? held.StackSize : 1, room);
    int moved = 0;
    for (int i = first; i < first + count && moved < wanted; i++)
    {
      ItemSlot slot = _inventory[i];
      // Only pool onto the same material: two ore types in one hopper would make the burden's stamp a
      // lie about what went into it, and there is no per-slot record to recover it from.
      //
      // Compared by code rather than through ItemStack.Satisfies. Satisfies walks attributes and the
      // collectible's own equality, which throws on a bare Item - and "is this the same material" is
      // exactly the code, not the stack's incidental state.
      if (
        !slot.Empty
        && !slot.Itemstack.Collectible.Code.Equals(held.Collectible.Code)
      )
        continue;

      int fits =
        slot.Empty
          ? held.Collectible.MaxStackSize
          : held.Collectible.MaxStackSize - slot.Itemstack.StackSize;
      if (fits <= 0)
        continue;

      int take = Math.Min(fits, wanted - moved);
      if (slot.Empty)
      {
        ItemStack one = held.Clone();
        one.StackSize = take;
        slot.Itemstack = one;
      }
      else
      {
        slot.Itemstack.StackSize += take;
      }
      slot.MarkDirty();
      moved += take;
    }

    if (moved == 0)
      return false;

    from.TakeOut(moved);
    from.MarkDirty();
    MarkDirty(true);
    return true;
  }

  private ItemStack? TakeFrom(int first, int count)
  {
    // Highest slot first, so repeated takes empty the tank from the top and a partial stack does not
    // linger between two full ones.
    for (int i = first + count - 1; i >= first; i--)
    {
      ItemSlot slot = _inventory[i];
      if (slot.Empty)
        continue;
      ItemStack taken = slot.TakeOutWhole();
      slot.MarkDirty();
      MarkDirty(true);
      return taken;
    }
    return null;
  }

  private int UnitsIn(int first, int count)
  {
    int total = 0;
    for (int i = first; i < first + count; i++)
      if (!_inventory[i].Empty)
        total += _inventory[i].Itemstack.StackSize;
    return total;
  }

  #endregion

  #region The gate

  /// <summary>
  /// Opens the lid: both hoppers drain together into the basin as one stamped burden batch. There is one
  /// lid, so there is one decision.
  /// <para>
  /// <b>The drain is instantaneous</b>, which settles <c>burdenmaker.md</c> § Open 4. The ore mixer
  /// drained at 8 u/s and that rate - not the mixing - was its throughput limit; but this machine has no
  /// mechanism and therefore no throughput lever to attach a rate to, so a rate here would be friction
  /// with nothing behind it.
  /// </para>
  /// <para>
  /// <b>The basin must be empty first</b>, and that is what keeps the machine honest about batch
  /// boundaries without a batch state machine: one gate-open is one batch, and the player takes it out
  /// before making the next. Pouring onto a previous batch would silently average two stamps.
  /// </para>
  /// </summary>
  public bool ToggleGate(out string? errorCode)
  {
    errorCode = null;

    if (_gateOpen)
    {
      _gateOpen = false;
      ApplyPose();
      MarkDirty(true);
      return true;
    }

    int ore = OreUnits;
    int flux = FluxUnits;
    if (ore + flux == 0)
    {
      errorCode = "iwex-burdenmaker-nothingloaded";
      return false;
    }
    if (BurdenUnits > 0)
    {
      errorCode = "iwex-burdenmaker-emptybunker";
      return false;
    }

    ItemStack? batch = MakeBurden(ore, flux);
    if (batch == null)
    {
      errorCode = "iwex-burdenmaker-nothingloaded";
      return false;
    }

    ClearRange(OreFirst, OreSlots);
    ClearRange(FluxFirst, FluxSlots);
    StoreBurden(batch);

    _gateOpen = true;
    ApplyPose();
    MarkDirty(true);
    return true;
  }

  /// <summary>
  /// One batch of <c>iwex:burden</c>, stamped with the proportions actually loaded.
  /// <para>
  /// <b>Fuel is 0 and must stay 0.</b> Coke left the burden when charging became layered; a burden
  /// carrying a fuel fraction is a second, disagreeing answer to "how much carbon is at the raceway", and
  /// the furnace has read carbon from fuel bands and nowhere else since U3.
  /// </para>
  /// </summary>
  private ItemStack? MakeBurden(int ore, int flux)
  {
    Item? item = Api?.World.GetItem(new AssetLocation("iwex", "burden"));
    if (item == null)
      return null;

    int units = ore + flux;
    float total = Math.Max(1, units);
    var stack = new ItemStack(item, units);
    Burden.Write(stack, new BurdenMix(ore / total, flux / total, 0f));
    return stack;
  }

  private void StoreBurden(ItemStack batch)
  {
    int max = Math.Max(1, batch.Collectible.MaxStackSize);
    int left = batch.StackSize;
    for (int i = BunkerFirst; i < BunkerFirst + BunkerSlots && left > 0; i++)
    {
      ItemStack part = batch.Clone();
      part.StackSize = Math.Min(max, left);
      _inventory[i].Itemstack = part;
      _inventory[i].MarkDirty();
      left -= part.StackSize;
    }
  }

  private void ClearRange(int first, int count)
  {
    for (int i = first; i < first + count; i++)
      if (!_inventory[i].Empty)
      {
        _inventory[i].Itemstack = null;
        _inventory[i].MarkDirty();
      }
  }

  #endregion

  #region Readout

  /// <summary>
  /// <b>The player is told the ratio, never asked to compute it.</b> Both hopper contents, plus the flux
  /// fraction the current pair <em>would</em> produce, named through the same
  /// <see cref="Burden.ProfileLangKey"/> the furnace grades a charge with - so "right" here and "right" at
  /// the furnace are one answer, not two.
  /// <para>
  /// The preview is computed from the hoppers, not from the basin: it has to answer *before* the gate is
  /// pulled, which is the only moment the player can still change it.
  /// </para>
  /// </summary>
  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
  {
    base.GetBlockInfo(forPlayer, sb);
    if (!IsConstructed)
      return;

    int ore = OreUnits;
    int flux = FluxUnits;
    sb.AppendLine(
      Lang.Get(
        "iwex:burdenmaker-loaded",
        ore,
        IwexValues.BurdenmakerOreCapacity,
        flux,
        IwexValues.BurdenmakerFluxCapacity
      )
    );

    if (ore + flux > 0)
    {
      float total = ore + flux;
      var preview = new BurdenMix(ore / total, flux / total, 0f);
      sb.AppendLine(
        Lang.Get(
          "iwex:burdenmaker-willmake",
          (int)(preview.FluxFrac * 100f),
          Lang.Get(Burden.ProfileLangKey(preview))
        )
      );
    }

    int stored = BurdenUnits;
    sb.AppendLine(
      stored > 0
        ? Lang.Get("iwex:burdenmaker-stored", stored)
        : Lang.Get("iwex:burdenmaker-empty")
    );
  }

  #endregion

  #region Persistence

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    tree.SetBool("gateOpen", _gateOpen);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    _gateOpen = tree.GetBool("gateOpen");
  }

  #endregion

  /// <summary>
  /// The inventory. Its only job beyond storage is the per-range acceptance gate, so an automated feed
  /// cannot put lime in the ore hopper - the same seam the ore bunker uses.
  /// </summary>
  private class InventoryBurdenmaker(int size)
    : InventoryGeneric(size, null, null)
  {
    public BlockEntityBurdenmaker? Machine { get; set; }

    public override bool CanContain(ItemSlot sink, ItemSlot from)
    {
      int index = GetSlotId(sink);
      ItemStack? stack = from?.Itemstack;
      return index switch
      {
        >= OreFirst and < FluxFirst => IsOre(stack),
        >= FluxFirst and < BunkerFirst => IsFlux(stack),
        // The basin is filled by the gate, never by hand or by a chute.
        _ => false,
      };
    }
  }
}
