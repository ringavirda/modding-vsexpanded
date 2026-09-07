using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Boiler;
using IronIndustryExpanded.BlockStructures.Boiler.Blocks;
using IronIndustryExpanded.BlockStructures.Furnaces;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;
using ModBoiler = IronIndustryExpanded.BlockStructures.Boiler.BlockEntityBoiler;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Wiring that makes a boiler's gated production tick run headlessly. The vessel is self-contained -
/// its footprint is its own and nothing around it is verified - so the only gate left is the
/// right-click construction, which consumes items from a player's hotbar and has no headless
/// equivalent. That is faked; the fuel bed is not. Shared by the unit-level <see cref="BoilerRig"/> and
/// the integration-level <c>BoilerFixture</c>.
/// </summary>
internal static class BoilerFakes {
  /// <summary>The Cornish boiler's shipped definition, the source of its footprint and its bed.</summary>
  public static ExBlockDef CornishDef =>
    BlockBoilerCornish.Definitions("iiex").Single();

  /// <summary>The registered class code the shipped definition names its fuel bed by.</summary>
  private const string FireboxClass = "iiex.BEBehaviorFirebox";

  /// <summary>
  /// Attaches <paramref name="def"/>'s shipped attributes to <paramref name="be"/>'s block, so the
  /// production code reads the real geometry offsets and the real footprint rather than falling back to
  /// the origin, then hosts the bed that definition declares and marks the construction finished.
  /// Call after the block entity is placed and before it is used.
  /// </summary>
  public static void Commission(TestWorld world, ModBoiler be, ExBlockDef def) {
    JObject json = def.ToJson();
    Assert.True(
      json["attributes"] is JObject,
      $"Block definition '{def.Code}' ships no attributes, so the fixture would "
        + "stand a boiler with no footprint and no geometry."
    );
    be.Block.Attributes = new JsonObject(json["attributes"]!);

    RaiseFootprint(world, be);
    world.Initialize(be);
    // Initialize re-reads _rcc off the (absent) behaviors and would clear a construction faked before
    // it, so the fake goes in afterwards.
    ForceConstructed(be);
    HostDeclaredBed(world, be, json);
  }

  /// <summary>
  /// Places the boiler's own footprint cells, the way <c>OnBlockPlaced</c> does in the world: every
  /// declared cell gets a filler carrying its attachment flag, its partial-fill boxes and its network
  /// port, all already rotated into the placed orientation. The ports are the vessel's steam and
  /// exhaust couplings, and the cells are what seals a pipe run laid against it, so a scene without
  /// them is a boiler with an open neck.
  /// </summary>
  private static void RaiseFootprint(TestWorld world, ModBoiler be) {
    if (be.Block is not BlockBoiler block)
      return;
    foreach (
      FillerCell cell in StructureFillers.FootprintCells(
        block,
        be.Pos,
        block.StructureAngle
      )
    ) {
      var filler = new BlockEntityStructureFiller {
        Principal = be.Pos.Copy(),
        AllowAttach = cell.AllowAttach,
        CollisionBoxes = cell.CollisionBoxes,
        PortFace = cell.PortFace,
        PortNetworkType = cell.PortNetworkType,
        HostedBehaviors = cell.Behaviors,
      };
      world.Place(cell.Pos, world.Filler, filler);
      world.Initialize(filler);
    }
  }

  /// <summary>
  /// Marks <paramref name="be"/>'s right-click construction finished. A real <c>Initialize</c> re-reads
  /// <c>_rcc</c> from the (absent) behaviors and clears it, so callers that Initialize must call this
  /// again afterwards.
  /// </summary>
  public static void ForceConstructed(ModBoiler be) => RccFake.Complete(be);

  /// <summary>
  /// Stands up the fuel bed the shipped definition declares, initialised with the properties it
  /// declares. Asserting the declaration is the fixture's premise: a boiler whose blocktype quietly
  /// stopped hosting a <see cref="BEBehaviorFirebox"/> would otherwise leave every test that fires it
  /// green while the shipped machine could never be lit.
  /// </summary>
  private static void HostDeclaredBed(
    TestWorld world,
    ModBoiler be,
    JObject json
  ) {
    JObject? declared = (json["entityBehaviors"] as JArray)
      ?.OfType<JObject>()
      .FirstOrDefault(b => (string?)b["name"] == FireboxClass);
    Assert.True(
      declared != null,
      $"Block definition '{json["code"]}' declares no '{FireboxClass}' entity "
        + "behavior, so the shipped boiler has no fuel bed to light."
    );

    var bed = new BEBehaviorFirebox(be);
    be.Behaviors.Add(bed);
    bed.Initialize(
      world.Api,
      new JsonObject(declared!["properties"] ?? new JObject())
    );
  }
}
