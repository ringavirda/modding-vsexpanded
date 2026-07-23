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

  /// <summary>Structure-local candidate columns the drip searches, own column first, then the four
  /// horizontal neighbours - so a hopper sitting directly over the shaft AND one sitting beside-and-above
  /// it (the cupola's side-charging layout) both find the shaft to feed, orientation-blind.</summary>
  private static readonly (int dx, int dz)[] Columns =
  [
    (0, 0),
    (0, -1),
    (0, 1),
    (-1, 0),
    (1, 0),
  ];

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
  /// Whether <paramref name="stack"/> can enter the tank right now: it must be prepared burden of either
  /// family, and - once the tank holds something - must match what is already in it (same item and grade),
  /// because one stack cannot carry two grades. An empty tank accepts any single burden.
  /// </summary>
  public bool Accepts(ItemStack? stack) =>
    Burden.IsAny(stack) && (_tank == null || IsMergeable(stack));

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

  private void OnServerTick(float dt)
  {
    if (_tank == null || _tank.StackSize <= 0)
      return;

    (BlockPos pos, bool seed)? target = FindDropTarget();
    if (target == null)
      return; // shaft full / none below: hold the burden until there is room

    int perTick = Math.Max(1, IwexValues.HopperTallDropPerSecond);
    int room = target.Value.seed
      ? IwexValues.HopperTallPileCap
      : IwexValues.HopperTallPileCap - PileStackSize(target.Value.pos);
    int amount = Math.Min(Math.Min(perTick, _tank.StackSize), room);
    if (amount <= 0)
      return;

    if (target.Value.seed)
      SeedPile(target.Value.pos, amount);
    else
      TopUpPile(target.Value.pos, amount);

    _tank.StackSize -= amount;
    if (_tank.StackSize <= 0)
      _tank = null;

    ExParticles.FallingDust(Api.World, target.Value.pos);
    ExSounds.Play(Api, Pos, ExSounds.StoneCrush, 0.4f, 16f);
    MarkDirty(true);
  }

  /// <summary>
  /// Finds the cell to drip into: walking each candidate column downward, top up the first burden pile
  /// that has room, else seed a new pile in the gap just above the first full pile or the first solid
  /// floor - so a column fills from the bottom up and never higher than the hopper's own level. Returns
  /// the cell and whether it must be seeded (a fresh coal pile) versus topped up (an existing one).
  /// </summary>
  private (BlockPos pos, bool seed)? FindDropTarget()
  {
    int depth = Math.Max(1, IwexValues.HopperTallDropDepth);
    foreach (var (dx, dz) in Columns)
    {
      for (int d = 1; d <= depth; d++)
      {
        var pos = new BlockPos(
          Pos.X + dx,
          Pos.Y - d,
          Pos.Z + dz,
          Pos.dimension
        );
        Block block = Api.World.BlockAccessor.GetBlock(pos);

        if (IsCoalPile(block))
        {
          if (PileHasRoom(pos))
            return (pos, false);
          // A full pile: the next unit lands in the empty cell just on top of it (bottom-up growth).
          if (CanSeedAt(pos.UpCopy()))
            return (pos.UpCopy(), true);
          break; // column blocked here
        }

        if (!IsReplaceable(block))
        {
          // A solid floor: seed a pile in the empty cell resting on it.
          if (CanSeedAt(pos.UpCopy()))
            return (pos.UpCopy(), true);
          break;
        }
        // Otherwise air/replaceable: keep falling toward the floor or the top of a pile.
      }
    }
    return null;
  }

  // A cell can be seeded when it is empty (replaceable), at or below the hopper's own level (burden does
  // not pile up past where it is charged), and is not the hopper's own base cell.
  private bool CanSeedAt(BlockPos pos) =>
    pos.Y <= Pos.Y
    && !pos.Equals(Pos)
    && IsReplaceable(Api.World.BlockAccessor.GetBlock(pos));

  private bool PileHasRoom(BlockPos pos)
  {
    if (Pile(pos) is not { inventory: { Count: > 0 } } pile)
      return false;
    ItemSlot slot = pile.inventory[0];
    if (slot.Empty)
      return true;
    return IsMergeable(slot.Itemstack)
      && slot.StackSize < IwexValues.HopperTallPileCap;
  }

  private int PileStackSize(BlockPos pos) =>
    Pile(pos)?.inventory is { Count: > 0 } inv && !inv[0].Empty
      ? inv[0].StackSize
      : 0;

  private void TopUpPile(BlockPos pos, int amount)
  {
    if (Pile(pos) is not { inventory: { Count: > 0 } } pile)
      return;
    ItemSlot slot = pile.inventory[0];
    if (slot.Empty)
      slot.Itemstack = BurdenStack(amount);
    else
      slot.Itemstack.StackSize += amount;
    slot.MarkDirty();
    pile.MarkDirty(true);
    Api.World.BlockAccessor.MarkBlockDirty(pos);
  }

  private void SeedPile(BlockPos pos, int amount)
  {
    Block? coalpile = Api.World.GetBlock(new AssetLocation("game", "coalpile"));
    if (coalpile == null)
      return;
    Api.World.BlockAccessor.SetBlock(coalpile.BlockId, pos);
    if (Pile(pos) is { inventory: { Count: > 0 } } pile)
    {
      pile.inventory[0].Itemstack = BurdenStack(amount);
      pile.inventory[0].MarkDirty();
      pile.MarkDirty(true);
    }
  }

  // A stack of the tank's exact burden (item + grade), sized for this drop - so the pile below carries
  // the same family and mix the furnace reads.
  private ItemStack BurdenStack(int amount)
  {
    ItemStack stack = _tank!.Clone();
    stack.StackSize = amount;
    return stack;
  }

  private BlockEntityCoalPile? Pile(BlockPos pos) =>
    Api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCoalPile;

  private static bool IsCoalPile(Block block) =>
    block.Code?.Path.StartsWith("coalpile") == true;

  // Air/water/other replaceable cells read >= 6000; a solid block or a coal pile reads below it.
  private static bool IsReplaceable(Block block) => block.Replaceable >= 6000;

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
      // Which furnace this burden is for, so a mis-loaded hopper reads its mistake before the furnace stalls.
      dsc.AppendLine(Lang.Get("iwex:burden-for-" + Burden.FamilyOf(_tank)));
    }

    // The shaft may hold burden even with the tank empty (the hopper dripped it all down), so this runs
    // regardless of the tank lines above. Silent until an anchor resolves and its structure is complete.
    Anchor.Resolve()?.AppendShaftChargeInfo(dsc);
  }

  #endregion
}
