using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Forming.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockStructures.Forming.Blocks;

/// <summary>
/// The rolling mill: a 3x2x3 megablock and an mpenergy consumer whose principal cell drives a horizontal roll
/// stand. Stock fed onto one deck is walked through segmented roll gaps, widest to narrowest, and comes off the
/// far deck at whatever thickness the player stops at (plate 1.0, sheet 0.5). The simulation lives in
/// <see cref="BlockEntityRollingMill"/>. See docs/design/machines/rolling-mill.md.
/// <para>
/// Footprint, authored in the <c>we</c> (axle-along-X) frame: the three cells at <c>y=0,z=0</c> are the drive
/// line, with the principal <c>(0,0,0)</c> as the consumer node and the two west cells as invisible
/// <see cref="BlockRollingMillAxle"/> pass-through nodes, so the line is one bus drivable from either shaft
/// end. The roll stand <c>(y=1,z=0)</c> and the two feed decks <c>(z=±1)</c> are invisible fillers.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockRollingMill
  : BlockNetworkNode,
    IExBlockDefProvider,
    IFillerHost,
    IFillerInteractionTarget {
  public override string NetworkType => "mpenergy";

  #region Code-first definition

  /// <summary>The mill blocktype: two horizontal orientations (<c>ns</c>/<c>we</c>), the axle running along the
  /// orientation axis so its two shaft faces carry the drive connectors. Authored in the <c>we</c> frame
  /// (<c>rotateY:0</c>); <c>ns</c> is the 90 degree rotation.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "forming", "forming/rollingmill")
        .Class<BlockRollingMill>()
        .EntityClass<BlockEntityRollingMill>()
        .Material(EnumBlockMaterial.Metal)
        .Sound("walk", "game:walk/metal")
        .Sound("place", "game:block/anvil")
        .MaxStackSize(1)
        .Handbook("forming-rollingmill-*")
        .VariantGroup("type", "rollingmill")
        .VariantGroup("orientation", "ns", "we")
        // we = axle along X (authored, rotateY 0); ns = the 90 degree rotation (axle along Z).
        .ShapeByType("*-we", "iwex:forming/rollingmill", rotateY: 0)
        .ShapeByType("*-ns", "iwex:forming/rollingmill", rotateY: 90)
        .CreativeCommon("*-we")
        .FillerOffsets(Footprint)
        // The stand overhangs its cell; the placed cell is solid but must not cull neighbour faces.
        .SolidNonOpaque(),
    ];

  #endregion

  #region Footprint (invisible fillers + the axle bus)

  /// <summary>The invisible fillers the mill reserves besides its principal and axle bus: the two feed decks
  /// (<c>z=±1</c>) and the roll stand above the axle (<c>y=1,z=0</c>). The axle row is left empty here (the
  /// <c>.</c> cells) because those cells take <see cref="BlockRollingMillAxle"/> nodes, and a filler cannot be
  /// a graph node. Authored in the <c>we</c> frame, principal at <c>(0,0,0)</c>.</summary>
  private static readonly IReadOnlyList<FillerCellSpec> Footprint =
    StructureFootprint.Layout(f =>
      f.Solid('-')
        .Origin(-2, -1)
        .Layer(
          0,
          """
          - - -
          . . O
          - - -
          """
        )
        .Layer(
          1,
          """
          . . .
          # # #
          . . .
          """
        )
    );

  /// <summary>Axle-bus offsets (west of the principal) in the <c>we</c> frame - the two cells that become
  /// <see cref="BlockRollingMillAxle"/> pass-through nodes. Rotated into the placed orientation by
  /// <see cref="StructureAngle"/>.</summary>
  private static readonly Vec3i[] AxleOffsets = [new(-1, 0, 0), new(-2, 0, 0)];

  /// <summary>The <c>fillerOffsets</c> attribute (from the code-first def), read by the shared helper.</summary>
  public JsonObject? FillerOffsets => Attributes?["fillerOffsets"];

  /// <summary>Rotation in degrees applied to the <c>we</c>-frame footprint and axle to reach the placed
  /// orientation: <c>we</c> 0 (authored), <c>ns</c> 90, matching the per-orientation shape rotations.</summary>
  public int StructureAngle => Variant?["orientation"] == "ns" ? 90 : 0;

  private List<FillerCell> FootprintCells(BlockPos pos) =>
    StructureFillers.FootprintCells(this, pos, StructureAngle);

  /// <summary>The world positions of the two axle-bus cells for a mill at <paramref name="pos"/>, rotated into
  /// the placed orientation.</summary>
  public IEnumerable<BlockPos> AxleCells(BlockPos pos) {
    foreach (Vec3i off in AxleOffsets) {
      Vec3i r = ExOrientation.RotateOffset(off, StructureAngle);
      yield return pos.AddCopy(r.X, r.Y, r.Z);
    }
  }

  public override bool CanPlaceBlock(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel,
    ref string failureCode
  ) {
    if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
      return false;

    // The whole volume must be clear so both the fillers and the axle nodes always spawn.
    if (
      !StructureFillers.CanPlace(world, FootprintCells(blockSel.Position))
      || !AxleCellsClear(world, blockSel.Position)
    ) {
      failureCode = "notenoughspace";
      return false;
    }
    return true;
  }

  private bool AxleCellsClear(IWorldAccessor world, BlockPos pos) {
    foreach (BlockPos cell in AxleCells(pos)) {
      Block existing = world.BlockAccessor.GetBlock(cell);
      if (existing.Id != 0 && !existing.IsReplacableBy(this))
        return false;
    }
    return true;
  }

  public override void OnBlockPlaced(
    IWorldAccessor world,
    BlockPos blockPos,
    ItemStack? byItemStack = null
  ) {
    base.OnBlockPlaced(world, blockPos, byItemStack);
    if (world.Side != EnumAppSide.Server)
      return;

    StructureFillers.PlaceFillers(world, blockPos, FootprintCells(blockPos));
    PlaceAxleNodes(world, blockPos);
  }

  private void PlaceAxleNodes(IWorldAccessor world, BlockPos pos) {
    if (Orientation == null)
      return;
    // The axle runs along the mill's orientation axis, so the axle cells share the mill's orientation.
    Block? axle = world.GetBlock(
      CodeWithPath("forming-millaxle-" + Orientation)
    );
    if (axle == null)
      return;

    foreach (BlockPos cell in AxleCells(pos)) {
      world.BlockAccessor.SetBlock(axle.BlockId, cell);
      if (
        world.BlockAccessor.GetBlockEntity(cell)
        is BlockEntityRollingMillAxle be
      ) {
        be.Principal = pos.Copy();
        be.MarkDirty(true);
      }
    }
  }

  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1f
  ) {
    if (world.Side == EnumAppSide.Server) {
      RemoveAxleNodes(world, pos);
      StructureFillers.RemoveFillers(world, pos, FootprintCells(pos));
    }
    // The base call removes the principal from the mpenergy graph and drops the mill.
    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  private void RemoveAxleNodes(IWorldAccessor world, BlockPos pos) {
    foreach (BlockPos cell in AxleCells(pos))
      if (world.BlockAccessor.GetBlock(cell) is BlockRollingMillAxle)
        world.BlockAccessor.SetBlock(0, cell); // the axle BE's OnBlockRemoved runs RemoveNode
  }

  #endregion

  #region Deck interaction

  /// <summary>
  /// Routes a click on the footprint.
  /// <list type="bullet">
  ///   <item>Holding a roll set, anywhere on the machine: fit it to the stand, swapping out any set already
  ///   there.</item>
  ///   <item>Holding stock on the input deck: feed it. The position along the deck picks the gap and sneaking
  ///   picks which strip goes through; the piece rides out onto the far deck.</item>
  /// </list>
  /// Clicking the output deck does nothing, since a two-high stand cannot be fed backwards.
  /// </summary>
  public bool OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => HandleInteract(world, byPlayer, principalSel, clickedCell);

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    HandleInteract(world, byPlayer, blockSel, blockSel.Position)
    || base.OnBlockInteractStart(world, byPlayer, blockSel);

  private bool HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection sel,
    BlockPos clickedCell
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(sel.Position)
      is not BlockEntities.BlockEntityRollingMill mill
    )
      return false;

    ItemSlot? slot = byPlayer.InventoryManager?.ActiveHotbarSlot;
    ItemStack? held = slot?.Itemstack;

    // A wrench frees a piece stuck in the rolls, whatever stopped the pass. The piece comes back untouched.
    if (IsWrench(held) && mill.IsRolling)
      return FreeStuckPiece(world, byPlayer, mill);
    if (IsRollSet(held))
      return FitRollSet(world, byPlayer, mill, slot!);
    if (held == null || !mill.IsInputDeck(clickedCell))
      return false;

    return Feed(world, byPlayer, mill, sel, clickedCell, held);
  }

  private static bool IsWrench(ItemStack? stack) =>
    stack?.Collectible?.Code?.FirstCodePart() == "wrench";

  private static bool FreeStuckPiece(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntities.BlockEntityRollingMill mill
  ) {
    if (world.Side != EnumAppSide.Server)
      return true;

    ItemStack? freed = mill.ReleaseStuckPiece();
    if (freed != null && !byPlayer.InventoryManager.TryGiveItemstack(freed))
      world.SpawnItemEntity(freed, byPlayer.Entity.Pos.XYZ);
    return true;
  }

  private static bool IsRollSet(ItemStack? stack) =>
    stack?.Collectible?.Attributes?[RollSetSpec.AttributeKey]
      is { Exists: true };

  private static bool FitRollSet(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntities.BlockEntityRollingMill mill,
    ItemSlot slot
  ) {
    if (world.Side != EnumAppSide.Server)
      return true;

    ItemStack fitting = slot.TakeOut(1);
    if (!mill.TryFitRollSet(fitting, out ItemStack? previous)) {
      // Refused because a pass is running: put the set back in the slot rather than losing it.
      slot.Itemstack = fitting;
      slot.MarkDirty();
      (byPlayer as IServerPlayer)?.SendIngameError("iwex-rollingmill-busy");
      return true;
    }

    if (
      previous != null
      && !byPlayer.InventoryManager.TryGiveItemstack(previous)
    )
      world.SpawnItemEntity(previous, byPlayer.Entity.Pos.XYZ);
    slot.MarkDirty();
    return true;
  }

  private bool Feed(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntities.BlockEntityRollingMill mill,
    BlockSelection sel,
    BlockPos clickedCell,
    ItemStack held
  ) {
    if (world.Side != EnumAppSide.Server)
      return true;

    int gapCount = mill.RollSet?.Gaps.Length ?? 1;
    int gap = MillFeed.GapZone(
      AlongBarrel(mill.Pos, clickedCell, sel),
      gapCount
    );
    int sides =
      WorkPiece.FromStack(held) is { } wp && mill.RollSet != null
        ? WorkPiece.SidesFor(wp.Width, mill.RollSet.BarrelWidth)
        : 1;
    int strip = MillFeed.StripIndex(byPlayer.Entity.Controls.Sneak, sides);

    FeedDecision decision = mill.TryFeed(held, gap, strip);
    if (decision.Accepted) {
      // The mill holds the piece until it drops on the far deck, so it leaves the player's hand here.
      byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
      byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
      return true;
    }

    (byPlayer as IServerPlayer)?.SendIngameError(
      decision.Verdict switch {
        FeedVerdict.NoRollSet => "iwex-rollingmill-norollset",
        FeedVerdict.WrongForm => "iwex-rollingmill-wrongform",
        FeedVerdict.NoReduction => "iwex-rollingmill-noreduction",
        FeedVerdict.TooCold => "iwex-rollingmill-toocold",
        _ => "iwex-rollingmill-wontbite",
      }
    );
    return true;
  }

  /// <summary>
  /// Where along the barrel a click landed, 0..1. The hit point is taken into the mill's own frame first, so
  /// the band arithmetic in <see cref="MillFeed.AlongBarrel"/> never has to know the orientation.
  /// </summary>
  private float AlongBarrel(
    BlockPos principal,
    BlockPos clickedCell,
    BlockSelection sel
  ) {
    // World-space hit point relative to the principal, then rotated back into the authored frame.
    double dx = clickedCell.X - principal.X + (sel.HitPosition?.X ?? 0.5);
    double dz = clickedCell.Z - principal.Z + (sel.HitPosition?.Z ?? 0.5);
    double localX = StructureAngle == 90 ? dz : dx;
    return MillFeed.AlongBarrel(localX);
  }

  public bool OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => false;

  public void OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) { }

  public WorldInteraction[] GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(principalSel.Position)
        is not BlockEntities.BlockEntityRollingMill mill
      || !mill.IsInputDeck(clickedCell)
    )
      return [];

    return
    [
      new WorldInteraction
      {
        ActionLangCode = "iwex:rollingmill-help-feed",
        MouseButton = EnumMouseButton.Right,
      },
      new WorldInteraction
      {
        ActionLangCode = "iwex:rollingmill-help-feed-far",
        MouseButton = EnumMouseButton.Right,
        HotKeyCode = "sneak",
      },
      new WorldInteraction
      {
        ActionLangCode = "iwex:rollingmill-help-free",
        MouseButton = EnumMouseButton.Right,
        Itemstacks = [],
      },
    ];
  }

  #endregion
}
