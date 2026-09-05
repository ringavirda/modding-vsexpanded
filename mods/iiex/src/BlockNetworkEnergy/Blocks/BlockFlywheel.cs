using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockNetworkEnergy.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockNetworkEnergy.Blocks;

/// <summary>
/// Self-orienting storage node of the <c>"mpenergy"</c> run: a vertical disc on a horizontal shaft holding
/// energy as spin (<c>E = ½Iω²</c>), its rotational inertia setting the run's reservoir capacity and
/// spin-up time. Connectors sit on the two shaft-axis faces, using the linear <c>ns</c>/<c>we</c>
/// orientation model. Two sizes share this class: <c>normal</c> (3×3×1) and <c>large</c> (5×5×2), disc
/// inertia scaling with <c>R⁴·t</c> so the large wheel holds about 15× the energy. Per-size values are
/// content (<see cref="IiexValues"/>); the simulation lives in <see cref="BlockEntityFlywheel"/> and
/// <c>ExpandedLib.Networks.MpEnergyNetwork</c>. See docs/design/mechanics/mp-energy.md.
/// </summary>
[BlockRegister]
public partial class BlockFlywheel
  : BlockNetworkNode,
    IExBlockDefProvider,
    IFillerHost {
  public override string NetworkType => "mpenergy";

  // AllowedOrientations is inherited from BlockNetworkNode, which derives it from this block's own
  // code-first defs (type → {ns, we}).

  #region Code-first definition

  // The disc is thin in Z (face in the X-Y plane, axle along Z), so a coupled axle meets it on the north
  // or south shaft face and the hub cell hosts a port for each side. StructureFillers.FootprintCells
  // rotates these into the placed orientation, so the we variant's ports become east/west.
  private static readonly FillerBehaviorSpec MpNorth =
    FillerBehaviorSpec.Of<BEBehaviorMPFillerPort>("north");
  private static readonly FillerBehaviorSpec MpSouth =
    FillerBehaviorSpec.Of<BEBehaviorMPFillerPort>("south");

  /// <summary>Normal wheel footprint: a thin-in-Z 3×3 disc in the X-Y plane, principal at bottom-centre,
  /// its hub one cell up at <c>(0,1,0)</c> hosting the two shaft ports.</summary>
  private static readonly IReadOnlyList<FillerCellSpec> NormalFootprint =
    StructureFootprint.Layout(f =>
      f.Origin(-1, 2)
        .Host('M', MpNorth, MpSouth)
        .Face(
          0,
          """
          # # #
          # M #
          # O #
          """
        )
    );

  /// <summary>Large wheel footprint: a 5×5×2 disc (two thin-in-Z faces), hubs at <c>(0,2,0)</c> and
  /// <c>(0,2,1)</c> each hosting the two shaft ports so an axle couples from either side.</summary>
  private static readonly IReadOnlyList<FillerCellSpec> LargeFootprint =
    StructureFootprint.Layout(f =>
      f.Origin(-2, 4)
        .Host('M', MpNorth, MpSouth)
        .Face(
          0,
          """
          # # # # #
          # # # # #
          # # M # #
          # # # # #
          # # O # #
          """
        )
        .Face(
          1,
          """
          # # # # #
          # # # # #
          # # M # #
          # # # # #
          # # # # #
          """
        )
    );

  /// <summary>The two flywheel blocktypes (normal / large), each a vertical disc connecting on its
  /// shaft-axis faces (ns / we). Authored in the north (shaft-along-Z) frame; <c>we</c> is the same shape
  /// rotated 90°.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "mpenergy", "mpenergy/flywheel")
        .Class<BlockFlywheel>()
        .EntityClass<BlockEntityFlywheel>()
        .Material(EnumBlockMaterial.Metal)
        .Sound("walk", "game:walk/metal")
        .Sound("place", "game:block/anvil")
        .MaxStackSize(1)
        .Handbook("mpenergy-flywheel-*")
        // The disc spins at the run's speed, so the spin is the charge gauge (docs/design/conventions.md
        // R7). The animator renders the wheel only while a clip is active, so the block entity must always
        // hold one (idle at rest, cycle while turning) or the mesh falls back to the static shape.
        .EntityBehavior("Animatable")
        // `type` names the family member, not the size: every mpenergy block shares the code
        // `iiex:mpenergy`, so no block's code is a prefix of another's and a wildcard built from Code
        // cannot stray outside its member (see CodePrefixCollision). The size is its own group.
        .VariantGroup("type", "flywheel")
        .VariantGroup("size", "normal", "large")
        .VariantGroup("orientation", "ns", "we")
        .NetworkOriented()
        // North frame = shaft along Z (ns); we is the 90° rotation. Each size has its own disc shape.
        .ShapeByType("*-normal-ns", "iiex:mpenergy/flywheel", rotateY: 0)
        .ShapeByType("*-normal-we", "iiex:mpenergy/flywheel", rotateY: 90)
        .ShapeByType("*-large-ns", "iiex:mpenergy/flywheel-large", rotateY: 0)
        .ShapeByType("*-large-we", "iiex:mpenergy/flywheel-large", rotateY: 90)
        .CreativeCommon("*-normal-ns", "*-large-ns")
        // The reserved volume differs by size, so each size declares its own footprint (attributesByType).
        .FillerOffsetsByType("*-normal-*", NormalFootprint)
        .FillerOffsetsByType("*-large-*", LargeFootprint)
        // The disc overhangs its cell (the shape is authored larger than 16px); the placed cell is solid
        // but must not cull neighbour faces around the overhang.
        .SolidNonOpaque(),
    ];

  #endregion

  #region Footprint (invisible fillers reserve the disc's volume)

  // The wheel renders across a whole slab but occupies one grid cell, so the rest of the volume is
  // reserved with invisible solid fillers that reroute interaction and break to this principal. The hub
  // cells carry the MP ports the bridge reads.

  /// <summary>The <c>fillerOffsets</c> attribute for the placed size variant (from the code-first def).</summary>
  public JsonObject? FillerOffsets => Attributes?["fillerOffsets"];

  /// <summary>Degrees applied to the north-frame (ns, shaft-along-Z) footprint to reach the placed
  /// orientation: <c>ns</c> 0, <c>we</c> 90. Matches the per-orientation shape rotation, and the block
  /// entity rotates its hub-port lookup by the same angle.</summary>
  public int StructureAngle => Variant?["orientation"] == "we" ? 90 : 0;

  private List<FillerCell> FootprintCells(BlockPos pos) =>
    StructureFillers.FootprintCells(this, pos, StructureAngle);

  public override bool CanPlaceBlock(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel,
    ref string failureCode
  ) {
    if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
      return false;

    // Refuse unless the whole disc volume is clear, so the fillers always spawn: an unfilled cell would
    // let blocks be placed inside the wheel and leave a gap in its collision.
    if (!StructureFillers.CanPlace(world, FootprintCells(blockSel.Position))) {
      failureCode = "notenoughspace";
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
    StructureFillers.PlaceFillers(world, blockPos, FootprintCells(blockPos));
  }

  public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos) {
    // Runs on every removal path (a player break, an explosion, a worldedit delete), unlike
    // OnBlockBroken, so the reserved volume is never left behind as orphan solid cells. Mirrors
    // ExpandedLib.Blocks.Structures.BlockFilledMegastructure.OnBlockRemoved.
    StructureFillers.RemoveFillers(world, pos, FootprintCells(pos));
    base.OnBlockRemoved(world, pos);
  }

  #endregion
}
