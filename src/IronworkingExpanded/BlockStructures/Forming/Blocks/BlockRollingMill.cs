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

namespace IronworkingExpanded.BlockStructures.Forming.Blocks;

/// <summary>
/// The rolling mill: the iron tier's forming machine and the first <b>consumer</b> of the mechanical-energy
/// network (<c>docs/design/mp-energy-network.md</c>). A 3×2×3 megablock whose principal cell drives a horizontal
/// roll stand: stock fed onto one deck is walked through segmented roll gaps (widest → narrowest) and comes off
/// the far deck at whatever thickness the player stops at (plate 1.0, sheet 0.5). The simulation lives in
/// <see cref="BlockEntityRollingMill"/>.
/// <para>
/// Footprint (authored in the <c>we</c> = axle-along-X frame): the three axle cells at <c>y=0,z=0</c> are the
/// drive line. The principal <c>(0,0,0)</c> is the mpenergy consumer node; the two west axle cells are
/// invisible <see cref="BlockRollingMillAxle"/> pass-through nodes so the line is one connected bus - drivable
/// from either shaft end and chainable into a manual train. The roll stand <c>(y=1,z=0)</c> and the two feed
/// decks <c>(z=±1)</c> are invisible fillers; the middle deck cell of each is where the player works stock.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockRollingMill
  : BlockNetworkNode,
    IExBlockDefProvider,
    IFillerHost,
    IFillerInteractionTarget
{
  public override string NetworkType => "mpenergy";

  #region Code-first definition

  /// <summary>The mill blocktype: two horizontal orientations (<c>ns</c>/<c>we</c>), the axle running along
  /// the orientation axis so its two shaft faces carry the drive connectors. Authored in the <c>we</c> frame
  /// (<c>rotateY:0</c>); <c>ns</c> is the 90° rotation.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "rollingmill", "forming/rollingmill")
        .Class<BlockRollingMill>()
        .EntityClass<BlockEntityRollingMill>()
        .Material(EnumBlockMaterial.Metal)
        .Sound("walk", "game:walk/metal")
        .Sound("place", "game:block/anvil")
        .MaxStackSize(1)
        .Handbook("rollingmill-*")
        .VariantGroup("type", "rollingmill")
        .VariantGroup("orientation", "ns", "we")
        // we = axle along X (authored, rotateY 0); ns = the 90° rotation (axle along Z).
        .ShapeByType("*-we", "iwex:forming/rollingmill", rotateY: 0)
        .ShapeByType("*-ns", "iwex:forming/rollingmill", rotateY: 90)
        .CreativeCommon("*-we")
        .FillerOffsets(Footprint)
        // The stand overhangs its cell; the placed cell is solid but must not cull neighbour faces.
        .SolidNonOpaque(),
    ];

  #endregion

  #region Footprint (invisible fillers + the axle bus)

  /// <summary>The nine invisible fillers the mill reserves besides its principal and axle bus: the two feed
  /// decks (<c>z=±1</c>) and the roll stand above the axle (<c>y=1,z=0</c>). The axle row itself is left empty
  /// here (the <c>.</c> cells) - those two cells get <see cref="BlockRollingMillAxle"/> nodes, not fillers,
  /// since a filler can never be a graph node. Authored in the <c>we</c> frame, principal at <c>(0,0,0)</c>.</summary>
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

  /// <summary>Rotation applied to the <c>we</c>-frame footprint/axle to reach the placed orientation: <c>we</c>
  /// 0 (authored), <c>ns</c> 90 - the same angles the per-orientation shapes rotate by.</summary>
  public int StructureAngle => Variant?["orientation"] == "ns" ? 90 : 0;

  private List<FillerCell> FootprintCells(BlockPos pos) =>
    StructureFillers.FootprintCells(this, pos, StructureAngle);

  /// <summary>The world positions of the two axle-bus cells for a mill at <paramref name="pos"/>, rotated into
  /// the placed orientation. Exposed so placement and the footprint can be asserted.</summary>
  public IEnumerable<BlockPos> AxleCells(BlockPos pos)
  {
    foreach (Vec3i off in AxleOffsets)
    {
      Vec3i r = ExOrientation.RotateOffset(off, StructureAngle);
      yield return pos.AddCopy(r.X, r.Y, r.Z);
    }
  }

  public override bool CanPlaceBlock(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel,
    ref string failureCode
  )
  {
    if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
      return false;

    // The whole volume must be clear so both the fillers and the axle nodes always spawn.
    if (
      !StructureFillers.CanPlace(world, FootprintCells(blockSel.Position))
      || !AxleCellsClear(world, blockSel.Position)
    )
    {
      failureCode = "notenoughspace";
      return false;
    }
    return true;
  }

  private bool AxleCellsClear(IWorldAccessor world, BlockPos pos)
  {
    foreach (BlockPos cell in AxleCells(pos))
    {
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
  )
  {
    base.OnBlockPlaced(world, blockPos, byItemStack);
    if (world.Side != EnumAppSide.Server)
      return;

    StructureFillers.PlaceFillers(world, blockPos, FootprintCells(blockPos));
    PlaceAxleNodes(world, blockPos);
  }

  private void PlaceAxleNodes(IWorldAccessor world, BlockPos pos)
  {
    if (Orientation == null)
      return;
    // The axle runs along the mill's orientation axis, so the axle cells share the mill's orientation.
    Block? axle = world.GetBlock(
      CodeWithPath("rollingmillaxle-shaft-" + Orientation)
    );
    if (axle == null)
      return;

    foreach (BlockPos cell in AxleCells(pos))
    {
      world.BlockAccessor.SetBlock(axle.BlockId, cell);
      if (
        world.BlockAccessor.GetBlockEntity(cell)
        is BlockEntityRollingMillAxle be
      )
      {
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
  )
  {
    if (world.Side == EnumAppSide.Server)
    {
      RemoveAxleNodes(world, pos);
      StructureFillers.RemoveFillers(world, pos, FootprintCells(pos));
    }
    // base removes the principal from the mpenergy graph and drops the mill.
    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  private void RemoveAxleNodes(IWorldAccessor world, BlockPos pos)
  {
    foreach (BlockPos cell in AxleCells(pos))
      if (world.BlockAccessor.GetBlock(cell) is BlockRollingMillAxle)
        world.BlockAccessor.SetBlock(0, cell); // the axle BE's OnBlockRemoved runs RemoveNode
  }

  #endregion

  #region Deck interaction (Phase A stub)

  // The two feed-deck middle cells route the player's "work the stock" click here. Phase A proves the wiring
  // and does nothing; Phase C picks the live input deck from the drive-rotation direction and runs a pass.
  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => false;

  bool IFillerInteractionTarget.OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => false;

  void IFillerInteractionTarget.OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) { }

  WorldInteraction[] IFillerInteractionTarget.GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) => [];

  #endregion
}
