using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Blocks;
using ExpandedLib.Catalogues;
using ExpandedLib.Industry.Materials;
using ExpandedLib.Registries;
using IronIndustryExpanded.Compat;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockStructures.OreProcessing.BlockEntities;

/// <summary>
/// The burdenmaker's block entity: two hoppers over a shared basin with one sliding gate between them.
/// Materials go in and come out freely, one or a stack at a time, with no batch state. The
/// <c>RightClickConstructable</c> behaviour suppresses the default mesh, so the machine renders through a
/// permanent animation re-tessellated to the currently-built elements; the resting clip <c>closed</c> must
/// keep running or the mesh goes with it, and its sibling <c>open</c> is a held pose cleared by
/// <c>StopAnimation</c>; both endings are set at export in <c>scripts/convert-shape.py</c>. Storage is a
/// real multi-slot inventory, so <see cref="BlockEntityContainer"/>'s break-spill covers drops. See
/// <c>docs/design/machines/burdenmaker.md</c>.
/// </summary>
[BlockEntityRegister]
public class BlockEntityBurdenmaker : ExBlockEntityContainer {
  // Slot layout. Fixed ranges, so "which tank" is a property of the index and never derived from a slot's
  // contents. Counts are sized against a worst-case stack of 64 so the configured unit capacity always
  // binds before the slots do; `MaxStackSize` belongs to the loaded item, so this cannot be derived at
  // compile time. A pinning test keeps the counts in step and must move whenever a capacity does.
  private const int OreSlots = 8; // 512 u
  private const int FluxSlots = 4; // 205 u
  private const int BunkerSlots = 18; // 1152 u

  private const int OreFirst = 0;
  private const int FluxFirst = OreFirst + OreSlots;
  private const int BunkerFirst = FluxFirst + FluxSlots;
  private const int TotalSlots = BunkerFirst + BunkerSlots;

  private readonly InventoryBurdenmaker _inventory;

  private ConstructedAnimator? _animator;

  [Persist]
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

  public BlockEntityBurdenmaker() {
    _inventory = new InventoryBurdenmaker(TotalSlots) { Machine = this };
  }

  #region Lifecycle

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    _inventory.LateInitialize(
      InventoryClassName + "-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z,
      api
    );

    // Resolved on both sides (IsConstructed gates server-side logic); it only builds and poses on the client.
    _animator = new ConstructedAnimator(this, () => AnimCacheKey);
    _animator.Initialize(ApplyPose);
  }

  // Must stay lazy: a wrench rotation changes the variant, and a key captured at Initialize would keep
  // handing back the old orientation's cached mesh.
  private string AnimCacheKey =>
    "burdenmaker-" + Block.Variant["side"] + (_gateOpen ? "-open" : "-closed");

  public override void OnBlockRemoved() {
    _animator?.Dispose();
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded() {
    _animator?.Dispose();
    base.OnBlockUnloaded();
  }

  /// <summary>
  /// Holds the machine visible via a permanent pose (RCC draws no mesh of its own): <c>closed</c> at rest,
  /// <c>open</c> while the lid is drawn back.
  /// </summary>
  private void ApplyPose() {
    string clip = _gateOpen ? "open" : "closed";
    _animator?.Pose(util => {
      util.StopAnimation(_gateOpen ? "closed" : "open");
      util.StartAnimation(
        new AnimationMetaData {
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
  /// Whether <paramref name="stack"/> belongs in the wide hopper: crushed or roasted iron ore. Resolved
  /// through <see cref="IronOreCompat"/> rather than by code prefix, so ores other mods register are
  /// accepted here exactly as the furnace accepts them.
  /// </summary>
  public static bool IsOre(ItemStack? stack) =>
    stack?.Collectible?.Code is { } code
    && IronOreCompat.IsCrushedIronOre(code.Path);

  /// <summary>Whether <paramref name="stack"/> belongs in the narrow hopper: lime and the like.</summary>
  public static bool IsFlux(ItemStack? stack) =>
    stack?.Collectible?.Code is { } code
    && MaterialRoleRegistry.IsRole(Roles.Flux, code);

  #endregion

  #region Loading and taking

  /// <summary>Moves ore from <paramref name="from"/> into the wide hopper. Returns false if nothing moved.</summary>
  public bool TryLoadOre(ItemSlot from, bool wholeStack = false) =>
    TryLoad(
      from,
      wholeStack,
      IsOre,
      OreFirst,
      OreSlots,
      IiexValues.BurdenmakerOreCapacity
    );

  /// <summary>Moves flux from <paramref name="from"/> into the narrow hopper. Returns false if nothing moved.</summary>
  public bool TryLoadFlux(ItemSlot from, bool wholeStack = false) =>
    TryLoad(
      from,
      wholeStack,
      IsFlux,
      FluxFirst,
      FluxSlots,
      IiexValues.BurdenmakerFluxCapacity
    );

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
  ) {
    if (from?.Itemstack is not { } held || !accepts(held))
      return false;

    int room = capacity - UnitsIn(first, count);
    if (room <= 0)
      return false;

    int wanted = Math.Min(wholeStack ? held.StackSize : 1, room);
    int moved = 0;
    for (int i = first; i < first + count && moved < wanted; i++) {
      ItemSlot slot = _inventory[i];
      // Only pool onto the same material: two ore types in one hopper would leave the burden's stamp
      // disagreeing with what went in, with no per-slot record to recover it from. Compared by code
      // rather than through ItemStack.Satisfies, which throws on a bare Item.
      if (
        !slot.Empty
        && !slot.Itemstack.Collectible.Code.Equals(held.Collectible.Code)
      )
        continue;

      int fits = slot.Empty
        ? held.Collectible.MaxStackSize
        : held.Collectible.MaxStackSize - slot.Itemstack.StackSize;
      if (fits <= 0)
        continue;

      int take = Math.Min(fits, wanted - moved);
      if (slot.Empty) {
        ItemStack one = held.Clone();
        one.StackSize = take;
        slot.Itemstack = one;
      } else {
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

  private ItemStack? TakeFrom(int first, int count) {
    // Highest slot first, so repeated takes empty the tank from the top and a partial stack does not
    // linger between two full ones.
    for (int i = first + count - 1; i >= first; i--) {
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

  private int UnitsIn(int first, int count) {
    int total = 0;
    for (int i = first; i < first + count; i++)
      if (!_inventory[i].Empty)
        total += _inventory[i].Itemstack?.StackSize ?? 0;
    return total;
  }

  #endregion

  #region The gate

  /// <summary>
  /// Opens the lid: both hoppers drain instantaneously into the basin as one stamped burden batch. The
  /// basin must be empty first, which is what marks batch boundaries without a batch state machine - one
  /// gate-open is one batch, and pouring onto a previous batch would average two stamps.
  /// </summary>
  public bool ToggleGate(out string? errorCode) {
    errorCode = null;

    if (_gateOpen) {
      _gateOpen = false;
      ApplyPose();
      MarkDirty(true);
      return true;
    }

    int ore = OreUnits;
    int flux = FluxUnits;
    if (ore + flux == 0) {
      errorCode = "iiex-burdenmaker-nothingloaded";
      return false;
    }
    if (BurdenUnits > 0) {
      errorCode = "iiex-burdenmaker-emptybunker";
      return false;
    }

    ItemStack? batch = MakeBurden(ore, flux);
    if (batch == null) {
      errorCode = "iiex-burdenmaker-nothingloaded";
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
  /// One batch of <c>iiex:burden</c>, stamped with the proportions actually loaded. The fuel fraction is
  /// always 0: the furnace reads carbon from the charge column's fuel bands and nowhere else.
  /// </summary>
  private ItemStack? MakeBurden(int ore, int flux) {
    Item? item = Api?.World.GetItem(new AssetLocation("iiex", "burden"));
    if (item == null)
      return null;

    int units = ore + flux;
    float total = Math.Max(1, units);
    var stack = new ItemStack(item, units);
    Burden.Write(stack, new BurdenMix(ore / total, flux / total, 0f));
    return stack;
  }

  private void StoreBurden(ItemStack batch) {
    int max = Math.Max(1, batch.Collectible.MaxStackSize);
    int left = batch.StackSize;
    for (int i = BunkerFirst; i < BunkerFirst + BunkerSlots && left > 0; i++) {
      ItemStack part = batch.Clone();
      part.StackSize = Math.Min(max, left);
      _inventory[i].Itemstack = part;
      _inventory[i].MarkDirty();
      left -= part.StackSize;
    }
  }

  private void ClearRange(int first, int count) {
    for (int i = first; i < first + count; i++)
      if (!_inventory[i].Empty) {
        _inventory[i].Itemstack = null;
        _inventory[i].MarkDirty();
      }
  }

  #endregion

  #region Readout

  /// <summary>
  /// Reports both hopper contents and the flux fraction the current pair would produce, named through the
  /// same <see cref="Burden.ProfileLangKey"/> the furnace grades a charge with. The preview is computed
  /// from the hoppers rather than the basin, so it answers while the mix can still be changed.
  /// </summary>
  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb) {
    base.GetBlockInfo(forPlayer, sb);
    if (!IsConstructed)
      return;

    int ore = OreUnits;
    int flux = FluxUnits;
    sb.AppendLine(
      Lang.Get(
        "iiex:burdenmaker-loaded",
        ore,
        IiexValues.BurdenmakerOreCapacity,
        flux,
        IiexValues.BurdenmakerFluxCapacity
      )
    );

    if (ore + flux > 0) {
      float total = ore + flux;
      var preview = new BurdenMix(ore / total, flux / total, 0f);
      sb.AppendLine(
        Lang.Get(
          "iiex:burdenmaker-willmake",
          (int)(preview.FluxFrac * 100f),
          Lang.Get(Burden.ProfileLangKey(preview))
        )
      );
    }

    int stored = BurdenUnits;
    sb.AppendLine(
      stored > 0
        ? Lang.Get("iiex:burdenmaker-stored", stored)
        : Lang.Get("iiex:burdenmaker-empty")
    );
  }

  #endregion

  #region Persistence

  #endregion

  /// <summary>
  /// The inventory. Beyond storage its only job is the per-range acceptance gate, so an automated feed
  /// cannot put lime in the ore hopper.
  /// </summary>
  private class InventoryBurdenmaker(int size)
    : InventoryGeneric(size, null, null) {
    public BlockEntityBurdenmaker? Machine { get; set; }

    public override bool CanContain(ItemSlot sink, ItemSlot from) {
      int index = GetSlotId(sink);
      ItemStack? stack = from?.Itemstack;
      return index switch {
        >= OreFirst and < FluxFirst => IsOre(stack),
        >= FluxFirst and < BunkerFirst => IsFlux(stack),
        // The basin is filled by the gate, never by hand or by a chute.
        _ => false,
      };
    }
  }
}
