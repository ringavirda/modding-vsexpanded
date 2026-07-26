using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkEnergy.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockNetworkEnergy.Blocks;

/// <summary>
/// The flywheel: the signature block of the mechanical-<b>energy</b> network
/// (<c>docs/design/mp-energy-network.md</c>). It is a self-orienting node of the <c>"mpenergy"</c> run
/// that stores energy as spin (<c>E = ½Iω²</c>); its rotational inertia sets the run's reservoir
/// capacity and spin-up time. A vertical disc on a horizontal shaft, so its connectors sit on the two
/// shaft-axis faces - the same linear <c>ns</c>/<c>we</c> orientation model a straight canal or pipe uses.
/// <para>
/// Two sizes share this class: <c>normal</c> (a 3×3×1 disc) and <c>large</c> (5×5×2). A disc's inertia
/// scales with <c>R⁴·t</c>, so the large wheel holds ~15× the energy and takes ~15× as long to charge -
/// the heavy-industry buffer. The per-size inertia is content (<see cref="IwexValues"/>); the simulation
/// lives in <see cref="BlockEntityFlywheel"/> and <c>ExpandedLib.Networks.MpEnergyNetwork</c>.
/// </para>
/// <para>
/// The wheel is a placeable, network-registered storage node: it reserves its disc volume with invisible
/// fillers (below), the hub cell(s) of which host mechanical-power ports so a coupled axle charges the
/// reservoir - the vanilla-MP bridge, driven by <see cref="BlockEntityFlywheel"/>. The spin animation and
/// any producers that spend the stored energy are follow-ups; until one lands the run simply sits idle.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockFlywheel : BlockNetworkNode, IExBlockDefProvider, IFillerHost
{
  public override string NetworkType => "mpenergy";

  // AllowedOrientations is inherited from BlockNetworkNode, which derives it from this block's own
  // code-first defs (type → {ns, we}) - no hand-kept table.

  #region Code-first definition

  // The disc is thin in Z (its face lies in the X-Y plane, its axle runs along Z), so a coupled axle
  // meets it on the north or south shaft face; the hub cell hosts a port for each side. Rotated into the
  // placed orientation by StructureFillers.FootprintCells, so the we variant's ports become east/west.
  private static readonly FillerBehaviorSpec MpNorth =
    new("exlib.BEBehaviorMPFillerPort", "north");
  private static readonly FillerBehaviorSpec MpSouth =
    new("exlib.BEBehaviorMPFillerPort", "south");

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

  /// <summary>The two flywheel blocktypes (normal / large), each a vertical disc that connects on its
  /// shaft-axis faces (ns / we). Authored in the north (shaft-along-Z) frame; the <c>we</c> variant is the
  /// same shape rotated 90°.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "flywheel", "mpenergy/flywheel")
        .Class<BlockFlywheel>()
        .EntityClass<BlockEntityFlywheel>()
        .Material(EnumBlockMaterial.Metal)
        .Sound("walk", "game:walk/metal")
        .Sound("place", "game:block/anvil")
        .MaxStackSize(1)
        .Handbook("flywheel-*")
        .VariantGroup("type", "normal", "large")
        .VariantGroup("orientation", "ns", "we")
        // North frame = shaft along Z (ns); we is the 90° rotation. Each size has its own disc shape.
        .ShapeByType("*-normal-ns", "iwex:mpenergy/flywheel", rotateY: 0)
        .ShapeByType("*-normal-we", "iwex:mpenergy/flywheel", rotateY: 90)
        .ShapeByType("*-large-ns", "iwex:mpenergy/flywheel-large", rotateY: 0)
        .ShapeByType("*-large-we", "iwex:mpenergy/flywheel-large", rotateY: 90)
        .CreativeCommon("*-normal-ns", "*-large-ns")
        // The reserved volume differs by SIZE, so each size declares its own footprint (attributesByType);
        // the placement triad drives the shared StructureFillers statics, exactly like the twin-tub blower.
        .FillerOffsetsByType("*-normal-*", NormalFootprint)
        .FillerOffsetsByType("*-large-*", LargeFootprint)
        // The disc overhangs its cell (the shape is authored larger than 16px); the placed cell is solid
        // but must not cull neighbour faces around the overhang.
        .SolidNonOpaque(),
    ];

  #endregion

  #region Footprint (invisible fillers reserve the disc's volume)

  // The wheel renders across a whole slab but occupies one grid cell, so - like the twin-tub blower and
  // ore bunker - the rest of the volume is reserved with invisible solid fillers that reroute interaction
  // and break to this principal. Each size's footprint is declared per-type in the def above and rotated
  // into the placed orientation by the shared helper; the hub cell(s) carry the MP ports the bridge reads.

  /// <summary>The <c>fillerOffsets</c> attribute for the placed size variant (from the code-first def).</summary>
  public JsonObject? FillerOffsets => Attributes?["fillerOffsets"];

  /// <summary>Rotation applied to the north-frame (ns, shaft-along-Z) footprint to reach the placed
  /// orientation: <c>ns</c> 0, <c>we</c> 90 - the same angles the per-orientation shapes rotate by, so
  /// footprint and mesh never disagree. The block entity rotates its hub-port lookup by the same angle.</summary>
  public int StructureAngle => Variant?["orientation"] == "we" ? 90 : 0;

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

    // Refuse unless the whole disc volume is clear, so the fillers always spawn (a wheel standing with an
    // unfilled cell would let blocks be placed inside it and leave a gap in its collision).
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
    // Clear the reserved volume first so no invisible solid cells are orphaned, then let the base run
    // (which removes this node from the mpenergy graph and drops the wheel).
    StructureFillers.RemoveFillers(world, pos, FootprintCells(pos));
    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  #endregion
}
