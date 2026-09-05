using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The build-outline projection (Ctrl+Shift+right-click "show missing blocks") resolves from every
/// functional component of the furnace multiblock, not only the core: the tap, the tuyere and the tall
/// hopper each scan up to the anchor whose layout owns their cell, through
/// <see cref="BlockBehaviorMultiblockStructure.ResolveIncompleteAnchor"/> over
/// <see cref="IMultiblockComponent"/>. A refractory brick or filler has no block-entity role, so it is
/// not a component and is not covered here.
/// </summary>
public class MultiblockProjectionTests {
  #region Standup

  private static BlockEntityBlastFurnaceCold Furnace(
    TestWorld world,
    bool complete
  ) {
    var block = TestBlocks.Configure(
      new Block(),
      "iiex:furnace-blastcore-tier1-n",
      1,
      ("side", "north")
    );
    // Carry the real multiblock layout so OwnsCell answers off the same transformed offsets the game builds.
    block.Attributes = new JsonObject(
      (JObject)
        BlockBlastFurnaceCoreCold.Definitions("iiex").Single().ToJson()[
          "attributes"
        ]!
    );

    var be = new BlockEntityBlastFurnaceCold();
    world.Place(new BlockPos(0, 16, 0), block, be);
    world.Attach(be);
    // Prime _structure, the layout OwnsCell walks. Completeness is set explicitly rather than built in
    // the world: the resolver gates on the StructureComplete flag, not a live recount.
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
    ReflectionHelpers.SetProperty(be, nameof(be.StructureComplete), complete);
    return be;
  }

  // `type` is the tap family member, irontap or slagtap. The HUD and the projection both key off the
  // tap's position, so it plays no part in these assertions; call sites name it anyway so a stand-in is
  // not coded as the wrong notch.
  private static BlockEntityFurnaceTap Tap(
    TestWorld world,
    BlockPos pos,
    string type = BlockFurnaceTap.IronType
  ) {
    var be = new BlockEntityFurnaceTap();
    world.Place(
      pos,
      TestBlocks.Configure(
        new Block(),
        $"iiex:furnace-{type}-n",
        2,
        ("type", type),
        ("side", "north")
      ),
      be
    );
    world.Attach(be);
    return be;
  }

  private static BlockEntityTuyere Tuyere(TestWorld world, BlockPos pos) {
    var be = new BlockEntityTuyere();
    world.Place(
      pos,
      TestBlocks.Configure(new Block(), "iiex:furnace-tuyere-n", 4),
      be
    );
    world.Attach(be);
    return be;
  }

  private static BlockEntityHopperTall Hopper(TestWorld world, BlockPos pos) {
    var be = new BlockEntityHopperTall();
    world.Place(
      pos,
      TestBlocks.Configure(new Block(), "iiex:hopper-tall", 5),
      be
    );
    world.Attach(be);
    return be;
  }

  // North furnace: structure-local == world-relative, so a layout offset lands at core.Pos + (x,y,z).
  private static BlockPos Global(BlockEntity be, int x, int y, int z) =>
    (BlockPos)ReflectionHelpers.Invoke(be, "GetGlobalPos", x, y, z)!;

  private static BlockEntityMultiblockStructure? Resolve(
    TestWorld world,
    BlockPos pos
  ) =>
    BlockBehaviorMultiblockStructure.ResolveIncompleteAnchor(world.World, pos);

  #endregion

  #region Incomplete: projects from every functional component

  [Fact]
  public void An_incomplete_furnace_projects_from_the_metal_tap() {
    var world = new TestWorld();
    var furnace = Furnace(world, complete: false);
    var tap = Tap(world, furnace.MetalTapPos!);

    Assert.Same(furnace, Resolve(world, tap.Pos));
  }

  [Fact]
  public void An_incomplete_furnace_projects_from_the_slag_tap() {
    var world = new TestWorld();
    var furnace = Furnace(world, complete: false);
    var tap = Tap(world, furnace.SlagTapPos!, BlockFurnaceTap.SlagType);

    Assert.Same(furnace, Resolve(world, tap.Pos));
  }

  [Fact]
  public void An_incomplete_furnace_projects_from_the_tuyere() {
    var world = new TestWorld();
    var furnace = Furnace(world, complete: false);
    // A tuyere cell of the blast layout (structure-local (0, 1, -1)).
    var tuyere = Tuyere(world, Global(furnace, 0, 1, -1));

    Assert.Same(furnace, Resolve(world, tuyere.Pos));
  }

  [Fact]
  public void An_incomplete_furnace_projects_from_the_tall_hopper() {
    var world = new TestWorld();
    var furnace = Furnace(world, complete: false);
    // The cold furnace's tall hopper sits at structure-local (-2, 6, 0).
    var hopper = Hopper(world, Global(furnace, -2, 6, 0));

    Assert.Same(furnace, Resolve(world, hopper.Pos));
  }

  #endregion

  #region The projection a component resolves is the core's

  [Fact]
  public void A_component_resolves_the_same_anchor_the_core_does() {
    var world = new TestWorld();
    var furnace = Furnace(world, complete: false);
    var tap = Tap(world, furnace.MetalTapPos!);

    // The gesture on the core and the gesture on the tap resolve the same instance, so both toggle one
    // outline.
    Assert.Same(Resolve(world, furnace.Pos), Resolve(world, tap.Pos));
    Assert.Same(furnace, Resolve(world, furnace.Pos));
  }

  #endregion

  #region Complete: projects from none

  [Fact]
  public void A_complete_furnace_does_not_project_from_a_component() {
    var world = new TestWorld();
    var furnace = Furnace(world, complete: true);
    var tap = Tap(world, furnace.MetalTapPos!);
    var tuyere = Tuyere(world, Global(furnace, 0, 1, -1));
    var hopper = Hopper(world, Global(furnace, -2, 6, 0));

    Assert.Null(Resolve(world, tap.Pos));
    Assert.Null(Resolve(world, tuyere.Pos));
    Assert.Null(Resolve(world, hopper.Pos));
    // Nor from the core.
    Assert.Null(Resolve(world, furnace.Pos));
  }

  #endregion

  #region No furnace: resolves nothing

  [Fact]
  public void A_component_with_no_furnace_resolves_no_anchor() {
    var world = new TestWorld();
    var tap = Tap(world, new BlockPos(40, 20, 40)); // nothing around it

    Assert.Null(Resolve(world, tap.Pos));
  }

  #endregion
}
