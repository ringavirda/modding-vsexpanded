using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The tall hopper's block entity: a one-stack burden tank that drips its contents into the furnace
/// shaft below, continuously and with no on/off toggle (unlike the smex bell hopper, which manages a
/// drop cadence and a stop state). It is a dumb tank - it drops <em>either</em> burden family and never
/// gates acceptance; the furnace core it feeds reads the shaft and decides whether that family melts.
/// <para>
/// All player interaction is routed here from the hopper's <b>top filler cell</b> by
/// <see cref="IronworkingExpanded.BlockStructures.Furnaces.Blocks.BlockHopperTall"/>
/// (<see cref="ExpandedLib.Blocks.Structures.IFillerInteractionTarget"/>), not from the base block: a
/// right-click with burden fills the tank, an empty-handed right-click empties it. The tank is a single
/// <see cref="ItemStack"/> so it can hold only one grade at a time; a mismatched deposit is refused
/// (the block raises the in-game error).
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityHopperTall : BlockEntity, IMultiblockComponent
{
  // The whole tank is one burden stack (item identity = family, attributes = grade). Null when empty.
  private ItemStack? _tank;

  private long _tickId;

  // The hopper feeds the shaft below, so it also surfaces the furnace's burden slice - how much charge
  // is loaded, and any wrong-family warning. Scans down for the core it drips into (six cells below in
  // the cold furnace, four in the cupola); silent when the hopper is not over a furnace. The same link
  // answers the build-outline projection (ResolveOwningAnchor) so the hopper previews an incomplete furnace.
  private MultiblockAnchorLink<BlockEntityFurnaceCore>? _anchor;

  /// <summary>The furnace this hopper charges, resolved by scanning down to the core whose layout owns the
  /// hopper's cell (cached + throttled by the link). Drives both the shaft-charge HUD and the build outline.</summary>
  private MultiblockAnchorLink<BlockEntityFurnaceCore> Anchor =>
    _anchor ??= new MultiblockAnchorLink<BlockEntityFurnaceCore>(
      this,
      BlockEntityFurnaceCore.ComponentScanHorizontal,
      BlockEntityFurnaceCore.ComponentScanBelow,
      BlockEntityFurnaceCore.ComponentScanAbove
    );

  /// <inheritdoc/>
  public BlockEntityMultiblockStructure? ResolveOwningAnchor() => Anchor.Resolve();

  // The hopper carries no offset table and no drip rotation of its own: columns are keyed
  // structure-local on the core, so there is nothing to rotate - a second, rotated copy of geometry the
  // furnace already owns is how a south- or west-facing cupola comes to charge outside itself. The
  // hopper asks the furnace which column to lay on, at any facing, and the anchor link (below) resolves
  // the core by the multiblock scan every furnace part uses, so "how many cells down to look" is not
  // this block's question either.

  /// <summary>Units currently held in the tank (0 when empty). Serialized, so the client HUD reads it.</summary>
  public int TankCount => _tank?.StackSize ?? 0;

  /// <summary>The burden stack the tank holds (or null when empty), for the block's break drops. The
  /// caller must not mutate it - clone first.</summary>
  public ItemStack? TankContents => _tank;

  /// <summary>Maximum units the tank holds (one burden stack). Live config.</summary>
  public int Capacity => IwexValues.HopperTallCapacity;

  /// <summary>Whether the tank is at capacity.</summary>
  public bool IsFull => TankCount >= Capacity;

  #region Lifecycle

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    // The drip is autonomous and always on: a placed hopper feeds whatever furnace is below it without
    // the player toggling anything. Server-side only - the client never moves burden.
    if (api.Side == EnumAppSide.Server)
      _tickId = RegisterGameTickListener(OnServerTick, 1000);
  }

  public override void OnBlockRemoved()
  {
    if (_tickId != 0)
      UnregisterGameTickListener(_tickId);
    base.OnBlockRemoved();
  }

  #endregion

  #region Deposit / withdraw (driven from the filler)

  /// <summary>
  /// Whether <paramref name="stack"/> can enter the tank right now: it must be something the furnace below
  /// actually charges, and - once the tank holds something - must match what is already in it (same item
  /// and grade), because one stack cannot carry two grades. An empty tank accepts any single charge.
  /// <para>
  /// <b>The hopper has no opinion of its own about what is chargeable.</b> Answering
  /// <c>Burden.IsAny</c> itself would be a fourth copy of a predicate that already exists on the cores,
  /// and one hopper block cannot serve four machines that way: the blast furnace, the cupola, the heating
  /// furnace and the beehive coke oven all take a different charge, and each of them burns its own fuel as
  /// well. Asking the anchored core makes the tank machine-agnostic and the machine the only authority.
  /// </para>
  /// <para>
  /// A hopper standing over nothing accepts nothing. That is the honest answer - there is no machine to
  /// declare a charge - and it is also what stops a hopper being filled and then walled into a furnace that
  /// refuses what is already in it.
  /// </para>
  /// <para>
  /// The single-stack tank rule below carries weight it did not before: it is what guarantees <b>one load
  /// lays one band type</b>, which is the whole of band-order charging. A tank that could hold coke and
  /// burden at once would drip them interleaved and no round could ever be laid.
  /// </para>
  /// </summary>
  public bool Accepts(ItemStack? stack) =>
    Anchor.Resolve() is { } core
    && core.IsChargeItem(stack)
    && (_tank == null || IsMergeable(stack));

  /// <summary>
  /// Moves burden from <paramref name="fromSlot"/> into the tank. With <paramref name="wholeStack"/> it
  /// takes the whole held stack (ctrl+right-click), otherwise a single unit (plain right-click), each
  /// capped by the remaining room. Server-side. Returns true when anything moved; false for a foreign or
  /// mismatched stack, or a full tank - the block turns the mismatch into an in-game error.
  /// </summary>
  public bool TryDeposit(ItemSlot fromSlot, bool wholeStack)
  {
    ItemStack? incoming = fromSlot.Itemstack;
    if (!Accepts(incoming))
      return false;

    int room = Capacity - TankCount;
    int take = Math.Min(wholeStack ? fromSlot.StackSize : 1, room);
    if (take <= 0)
      return false;

    if (_tank == null)
    {
      _tank = incoming!.Clone();
      _tank.StackSize = take;
    }
    else
    {
      _tank.StackSize += take;
    }

    fromSlot.TakeOut(take);
    fromSlot.MarkDirty();
    MarkDirty(true);
    return true;
  }

  /// <summary>Empties the tank, handing back the whole burden stack (or null when already empty).</summary>
  public ItemStack? TryWithdraw()
  {
    if (_tank == null || _tank.StackSize <= 0)
      return null;
    ItemStack taken = _tank;
    _tank = null;
    MarkDirty(true);
    return taken;
  }

  // Same item and same stamped grade - the vanilla stacking rule the bunker uses, so a slightly
  // different mix (or the other family) is kept apart instead of silently pooling into one stack.
  private bool IsMergeable(ItemStack? stack) =>
    _tank != null
    && stack?.Collectible == _tank.Collectible
    && Burden.Read(stack).Equals(Burden.Read(_tank));

  #endregion

  #region Continuous drip

  /// <summary>
  /// One drip: lay up to <c>HopperTallDropPerSecond</c> of the tank onto the shaft column the furnace
  /// nominates, and reconcile the blocks so the new material becomes something the player can see.
  /// <para>
  /// <b>No world scan, no <c>game:coalpile</c>, no per-cell cap.</b> The whole of "which cell does this
  /// land in" is <see cref="BlockEntityFurnaceCore.NextChargeColumn"/> - lowest column first, fuel only
  /// onto burden - so the hopper contributes the tank, the cadence and nothing else. The cap the old path
  /// clamped against was vanilla's pile mesh (a hard 16 layers); a column's ceiling is its own cell count
  /// times the furnace's block quantum, which is a number this mod chose.
  /// </para>
  /// <para>
  /// A full shaft, or a column set that will not take what is in the tank, simply holds the load - the
  /// hopper is a tank with a valve, not a machine that can fail.
  /// </para>
  /// </summary>
  private void OnServerTick(float dt)
  {
    if (_tank == null || _tank.StackSize <= 0)
      return;
    if (Anchor.Resolve() is not { } core)
      return;

    // The column stores a code string, not a stack: it holds units of a substance. A stack whose item no
    // longer resolves reads null here, which is a live world state on a tick path, so it holds rather
    // than throws - the same reason ChargeColumn.Push refuses an empty material instead of throwing.
    string? material = _tank.Collectible?.Code?.ToShortString();
    if (string.IsNullOrEmpty(material) || !core.IsChargeCode(material))
      return;

    ChargeColumn? column = core.NextChargeColumn(material, out int room);
    if (column == null || room <= 0)
      return; // shaft full, or the band order refuses this material anywhere

    int amount = Math.Min(
      Math.Min(Math.Max(1, IwexValues.HopperTallDropPerSecond), _tank.StackSize),
      room
    );
    if (amount <= 0)
      return;

    // The mix rides along, and it is read off the tank rather than recomputed: burden's one surviving
    // quality is its flux ratio, and it has to reach the raceway intact. Fuel carries `default`, which is
    // exactly what Burden.Read answers for a stack with no stamp.
    column.Push(material, amount, core.ChargeTemperature, Burden.Read(_tank));

    _tank.StackSize -= amount;
    if (_tank.StackSize <= 0)
      _tank = null;

    // The columns moved, so the world has to catch up - blocks appear as a column crosses a boundary, and
    // every surviving pile in it republishes the snapshot its mesh reads.
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
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    _tank = tree.GetItemstack("tank");
    _tank?.ResolveBlockOrItem(worldForResolving);
    // A resolved-away stack (the item no longer exists) or a zero stack reads as empty.
    if (_tank?.Collectible == null || _tank.StackSize <= 0)
      _tank = null;
  }

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    if (_tank != null)
      tree.SetItemstack("tank", _tank);
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    base.GetBlockInfo(forPlayer, dsc);
    if (_tank == null || _tank.StackSize <= 0)
    {
      dsc.AppendLine(Lang.Get("iwex:hoppertall-empty"));
    }
    else
    {
      dsc.AppendLine(
        Lang.Get(
          "iwex:hoppertall-holds",
          _tank.StackSize,
          Capacity,
          _tank.GetName()
        )
      );
      // No "which furnace this burden is for" line: a hopper cannot be mis-loaded with the wrong
      // burden, because there is only one burden item.
    }

    // The shaft may hold burden even with the tank empty (the hopper dripped it all down), so this runs
    // regardless of the tank lines above. Silent until an anchor resolves and its structure is complete.
    Anchor.Resolve()?.AppendShaftChargeInfo(dsc);
  }

  #endregion
}
