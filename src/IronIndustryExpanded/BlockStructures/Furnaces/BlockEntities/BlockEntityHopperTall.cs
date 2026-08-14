using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The tall hopper's block entity: a one-stack burden tank that drips into the furnace shaft below,
/// continuously and with no on/off toggle. It drops either burden family and gates nothing itself; the
/// furnace core it feeds decides whether that family melts. The tank is a single <see cref="ItemStack"/>,
/// so it holds one grade at a time and a mismatched deposit is refused. All player interaction arrives from
/// the hopper's top filler cell through
/// <see cref="IronIndustryExpanded.BlockStructures.Furnaces.Blocks.BlockHopperTall"/>
/// (<see cref="ExpandedLib.Blocks.Structures.IFillerInteractionTarget"/>) rather than the base block.
/// See docs/design/machines/tall-hopper.md.
/// </summary>
[BlockEntityRegister]
public class BlockEntityHopperTall : BlockEntity, IMultiblockComponent {
  // The whole tank is one burden stack (item identity = family, attributes = grade). Null when empty.
  private ItemStack? _tank;

  // The hopper feeds the shaft below, so it also surfaces the furnace's burden slice: how much charge is
  // loaded, and any wrong-family warning. Scans down for the core it drips into (six cells below in the cold
  // furnace, four in the cupola); silent when the hopper is not over a furnace. The same link answers the
  // build-outline projection (ResolveOwningAnchor).
  private MultiblockAnchorLink<BlockEntityFurnaceCore>? _anchor;

  /// <summary>The furnace this hopper charges, resolved by scanning down to the core whose layout owns the
  /// hopper's cell (cached and throttled by the link). Drives the shaft-charge HUD and the build outline.</summary>
  private MultiblockAnchorLink<BlockEntityFurnaceCore> Anchor =>
    _anchor ??= new MultiblockAnchorLink<BlockEntityFurnaceCore>(
      this,
      BlockEntityFurnaceCore.ComponentScanHorizontal,
      BlockEntityFurnaceCore.ComponentScanBelow,
      BlockEntityFurnaceCore.ComponentScanAbove
    );

  /// <inheritdoc/>
  public BlockEntityMultiblockStructure? ResolveOwningAnchor() =>
    Anchor.Resolve();

  // No offset table and no drip rotation here: columns are keyed structure-local on the core, so there is
  // nothing to rotate, and the anchor link resolves the core by the multiblock scan every furnace part uses.

  /// <summary>Units currently held in the tank (0 when empty). Serialized, so the client HUD reads it.</summary>
  public int TankCount => _tank?.StackSize ?? 0;

  /// <summary>The burden stack the tank holds (or null when empty), for the block's break drops. The
  /// caller must not mutate it - clone first.</summary>
  public ItemStack? TankContents => _tank;

  /// <summary>Maximum units the tank holds (one burden stack). Live config.</summary>
  public int Capacity => IiexValues.HopperTallCapacity;

  /// <summary>Whether the tank is at capacity.</summary>
  public bool IsFull => TankCount >= Capacity;

  #region Lifecycle

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    // The drip is autonomous and always on. Server side only - the client never moves burden.
    // The handle is not kept: the base drops every listener on both removal and chunk unload, and
    // nothing else here starts or stops the drip.
    if (api.Side == EnumAppSide.Server)
      RegisterGameTickListener(OnServerTick, 1000);
  }

  #endregion

  #region Deposit / withdraw (driven from the filler)

  /// <summary>
  /// Whether <paramref name="stack"/> can enter the tank right now: it must be something the furnace below
  /// charges, and - once the tank holds something - must match it in item and grade, one stack carrying one
  /// grade. An empty tank accepts any single charge; a hopper over nothing accepts nothing. The chargeable
  /// predicate is the anchored core's, not the hopper's, so one hopper block serves the blast furnace, the
  /// cupola, the heating furnace and the beehive coke oven. The single-stack rule is what makes one load
  /// lay one band type, a tank of coke and burden at once dripping them interleaved.
  /// </summary>
  public bool Accepts(ItemStack? stack) =>
    Anchor.Resolve() is { } core
    && core.IsChargeItem(stack)
    && (_tank == null || IsMergeable(stack));

  /// <summary>
  /// Moves burden from <paramref name="fromSlot"/> into the tank: the whole held stack with
  /// <paramref name="wholeStack"/>, otherwise a single unit, each capped by the remaining room. Server side.
  /// Returns true when anything moved, false for a foreign or mismatched stack or a full tank; the block
  /// turns the mismatch into an in-game error.
  /// </summary>
  public bool TryDeposit(ItemSlot fromSlot, bool wholeStack) {
    ItemStack? incoming = fromSlot.Itemstack;
    if (!Accepts(incoming))
      return false;

    int room = Capacity - TankCount;
    int take = Math.Min(wholeStack ? fromSlot.StackSize : 1, room);
    if (take <= 0)
      return false;

    if (_tank == null) {
      _tank = incoming!.Clone();
      _tank.StackSize = take;
    } else {
      _tank.StackSize += take;
    }

    fromSlot.TakeOut(take);
    fromSlot.MarkDirty();
    MarkDirty(true);
    return true;
  }

  /// <summary>Empties the tank, handing back the whole burden stack (or null when already empty).</summary>
  public ItemStack? TryWithdraw() {
    if (_tank == null || _tank.StackSize <= 0)
      return null;
    ItemStack taken = _tank;
    _tank = null;
    MarkDirty(true);
    return taken;
  }

  // Same item and same stamped grade, so a different mix or the other family is kept apart instead of
  // pooling into one stack.
  private bool IsMergeable(ItemStack? stack) =>
    _tank != null
    && stack?.Collectible == _tank.Collectible
    && Burden.Read(stack).Equals(Burden.Read(_tank));

  #endregion

  #region Continuous drip

  /// <summary>
  /// One drip: lay up to <c>HopperTallDropPerSecond</c> of the tank onto the shaft column the furnace
  /// nominates, then reconcile the blocks so the new material becomes visible. Which cell the load lands in
  /// is <see cref="BlockEntityFurnaceCore.NextChargeColumn"/>'s decision (lowest column first, fuel only
  /// onto burden). A full shaft, or a column set that will not take what is in the tank, holds the load
  /// rather than failing.
  /// </summary>
  private void OnServerTick(float dt) {
    if (_tank == null || _tank.StackSize <= 0)
      return;
    if (Anchor.Resolve() is not { } core)
      return;

    // The column stores a code string, not a stack: it holds units of a substance. A stack whose item no
    // longer resolves reads null here, a live world state on a tick path, so the drip holds rather than
    // throws.
    string? material = _tank.Collectible?.Code?.ToShortString();
    if (string.IsNullOrEmpty(material) || !core.IsChargeCode(material))
      return;

    ChargeColumn? column = core.NextChargeColumn(material, out int room);
    if (column == null || room <= 0)
      return; // shaft full, or the band order refuses this material anywhere

    int amount = Math.Min(
      Math.Min(
        Math.Max(1, IiexValues.HopperTallDropPerSecond),
        _tank.StackSize
      ),
      room
    );
    if (amount <= 0)
      return;

    // The mix rides along, read off the tank rather than recomputed: burden's flux ratio has to reach the
    // raceway intact. Fuel carries `default`, which is what Burden.Read answers for an unstamped stack.
    column.Push(material, amount, core.ChargeTemperature, Burden.Read(_tank));

    _tank.StackSize -= amount;
    if (_tank.StackSize <= 0)
      _tank = null;

    // The columns moved: blocks appear as a column crosses a boundary, and every surviving pile in it
    // republishes the snapshot its mesh reads.
    core.SyncChargeBlocks();
    core.MarkDirty(true); // the shaft total is the core's, and this hopper's HUD reads it

    ExParticles.FallingDust(Api.World, Pos.DownCopy());
    ExSounds.Play(Api, Pos, ExSounds.StoneCrush, 0.4f, 16f);
    MarkDirty(true);
  }

  #endregion

  #region Serialization

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    _tank = tree.GetItemstack("tank");
    _tank?.ResolveBlockOrItem(worldForResolving);
    // A resolved-away stack (the item no longer exists) or a zero stack reads as empty.
    if (_tank?.Collectible == null || _tank.StackSize <= 0)
      _tank = null;
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    if (_tank != null)
      tree.SetItemstack("tank", _tank);
  }

  /// <summary>Maps the tank's burden stack, so a hopper pasted into another world resolves its
  /// contents against that world's item ids rather than this one's.</summary>
  public override void OnStoreCollectibleMappings(
    Dictionary<int, AssetLocation> blockIdMapping,
    Dictionary<int, AssetLocation> itemIdMapping
  ) =>
    _tank?.Collectible.OnStoreCollectibleMappings(
      Api.World,
      new DummySlot(_tank),
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
      _tank?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve)
      == false
    )
      _tank = null;
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    if (_tank == null || _tank.StackSize <= 0) {
      dsc.AppendLine(Lang.Get("iiex:hoppertall-empty"));
    } else {
      dsc.AppendLine(
        Lang.Get(
          "iiex:hoppertall-holds",
          _tank.StackSize,
          Capacity,
          _tank.GetName()
        )
      );
      // No "which furnace this burden is for" line: there is one burden item, so a hopper cannot be
      // mis-loaded.
    }

    // The shaft may hold burden with the tank empty (the hopper dripped it all down), so this runs
    // regardless of the tank lines above. Silent until an anchor resolves and its structure is complete.
    Anchor.Resolve()?.AppendShaftChargeInfo(dsc);
  }

  #endregion
}
