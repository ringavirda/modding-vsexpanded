using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkPipe.Blocks;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The twin-tub blower: the iron tier's air source, and the only one that needs no steam. It is
/// <b>both</b> a gas-pipe node (it sits in the blast main and produces into it, hence the
/// <see cref="BlockPipe"/> base) <b>and</b> a mega-block that reserves a 1x2x3 footprint of invisible
/// fillers - the upper-rear cell of which hosts a mechanical-power port, so an axle on that face drives
/// the bellows. See <see cref="BlockEntityTwinTubMPBlower"/> for the simulation.
/// <para>
/// It cannot derive <see cref="BlockFilledMegastructure"/> (that base is a plain <c>Block</c> and the
/// blower must be a pipe), so it composes the same behaviour the way exlib's filler machinery is
/// factored for exactly this case: implement <see cref="IFillerHost"/> and drive the
/// <see cref="StructureFillers"/> statics from the placement triad below. Without that, the
/// <c>fillerOffsets</c> footprint is inert JSON - no fillers spawn, and the MP port cell never exists.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockTwinTubMPBlower : BlockPipe, IExBlockDefProvider, IFillerHost
{
  #region Code-first definition

  /// <summary>
  /// The mechanical-power intake hosted by the footprint's upper-rear cell: an axle on the
  /// (rotation-relative) west face drives the bellows. Same coupling the ore mixer uses.
  /// </summary>
  private static readonly FillerBehaviorSpec MpPortWest =
    new("exlib.BEBehaviorMPFillerPort", "west");

  /// <summary>
  /// The twin-tub blower blocktype. Orientation follows the pipe-fitting convention (a <c>type</c> +
  /// <c>orientation</c> variant pair, as the tuyere has), so the <see cref="BlockPipe"/> base derives
  /// its AllowedOrientations and connector faces from this def rather than a hand-written table.
  /// </summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "twintubmpblower", "furnaces/twintubmpblower")
        .Class<BlockTwinTubMPBlower>()
        .EntityClass<BlockEntityTwinTubMPBlower>()
        // Carries the shared build-outline behaviour so the projection gesture is wired at every functional
        // component uniformly. Unlike the tap/tuyere/hopper the blower is NOT a cell of the furnace layout -
        // it is a pipe-network machine linked to a tuyere by the blast main at unbounded distance - so the
        // layout-ownership resolver finds no owning anchor from it and the gesture does nothing (the "no
        // resolvable anchor -> does nothing" contract). It keeps its own MP/pipe HUD.
        .Behavior("MultiblockStructure")
        .Material(EnumBlockMaterial.Ceramic)
        .MaxStackSize(1)
        .VariantGroup("type", "twintubmpblower")
        .VariantGroup("orientation", "n", "e", "s", "w")
        // The footprint is authored in the NORTH frame, so north is the unrotated shape - the offsets in
        // FillerOffsets and StructureAngle below share that frame.
        .ShapeByType("*-n", "iwex:furnaces/twintubmpblower", rotateY: 0)
        .ShapeByType("*-e", "iwex:furnaces/twintubmpblower", rotateY: 90)
        .ShapeByType("*-s", "iwex:furnaces/twintubmpblower", rotateY: 180)
        .ShapeByType("*-w", "iwex:furnaces/twintubmpblower", rotateY: 270)
        .CreativeCommon("*-n")
        .FillerOffsets(
          StructureFootprint.Layout(f =>
            f.Host('M', MpPortWest)
              .Origin(0, 1)
              .Slice(
                0,
                // Rows are -Y (top row y=1), columns +Z (z=0..2); '0' is the principal, skipped.
                // 'M' (y=1,z=0) hosts the MP port an axle couples to; the remaining cells are the
                // bellows housing, whose far end (y=0,z=2) is what the blast main butts against.
                """
                M##
                0##
                """
              )
          )
        )
        .SolidNonOpaque(),
    ];

  #endregion

  #region Footprint

  /// <summary>The block's <c>fillerOffsets</c> attribute (from the injected code-first def).</summary>
  public JsonObject? FillerOffsets => Attributes?["fillerOffsets"];

  /// <summary>
  /// Rotation applied to the north-frame footprint to reach the placed orientation. Read from the
  /// pipe-fitting <c>orientation</c> variant (n 0, e 90, s 180, w 270), matching the per-orientation
  /// shape rotations in the definition above. The block entity rotates its MP-port lookup by the same
  /// angle, so the two can never disagree.
  /// </summary>
  public int StructureAngle =>
    Variant?["orientation"] switch
    {
      "e" => 90,
      "s" => 180,
      "w" => 270,
      _ => 0,
    };

  private List<FillerCell> FootprintCells(BlockPos pos) =>
    StructureFillers.FootprintCells(this, pos, StructureAngle);

  public override bool CanPlaceBlock(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel,
    ref string failureCode
  )
  {
    if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
      return false;

    // Refuse placement unless the whole volume is clear, else the fillers fail to spawn and the blower
    // would stand with no MP port cell to be driven through.
    if (!StructureFillers.CanPlace(world, FootprintCells(blockSel.Position)))
    {
      failureCode = "notenoughspace";
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
    StructureFillers.PlaceFillers(world, blockPos, FootprintCells(blockPos));
  }

  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    // Clear the reserved volume first so no invisible solid cells are left behind.
    StructureFillers.RemoveFillers(world, pos, FootprintCells(pos));
    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  #endregion
}
