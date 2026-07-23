using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The build-outline projection (Ctrl+Shift+right-click "show missing blocks") is reachable from every
/// functional component of the furnace multiblock, not only the core: the tap, the tuyere and the tall
/// hopper each scan up to the anchor whose layout owns their cell and forward to the core's outline through
/// one shared seam - <see cref="BlockBehaviorMultiblockStructure.ResolveIncompleteAnchor"/> over
/// <see cref="IMultiblockComponent"/>. This pins that: an INCOMPLETE furnace projects from any of the three
/// (and the projection they resolve is the very same core the gesture on the core resolves), a COMPLETE one
/// projects from none of them, and a lone component with no furnace resolves nothing. A refractory brick /
/// filler is not a component (no BE role), so it is silent by construction and not exercised here.
/// </summary>
public class MultiblockProjectionTests
{
  #region Standup

  private static BlockEntityBlastFurnaceCold Furnace(TestWorld world, bool complete)
  {
    var block = TestBlocks.Configure(
      new Block(),
      "iwex:blastfurnacecore-north",
      1,
      ("side", "north")
    );
    // Carry the real multiblock layout so OwnsCell answers off the same transformed offsets the game builds.
    block.Attributes = new JsonObject(
      (JObject)
        BlockBlastFurnaceCoreCold.Definitions("iwex").Single().ToJson()[
          "attributes"
        ]!
    );

    var be = new BlockEntityBlastFurnaceCold();
    world.Place(new BlockPos(0, 16, 0), block, be);
    world.Attach(be);
    // Prime _structure (the layout OwnsCell walks); completeness is set explicitly, not derived from a
    // (deliberately un-built) world - the resolver gates on the StructureComplete flag, not a live recount.
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
    ReflectionHelpers.SetProperty(be, nameof(be.StructureComplete), complete);
    return be;
  }

  private static BlockEntityMoltenMetalTap Tap(TestWorld world, BlockPos pos)
  {
    var be = new BlockEntityMoltenMetalTap();
    world.Place(
      pos,
      TestBlocks.Configure(
        new Block(),
        "iwex:moltenmetaltap-north",
        2,
        ("side", "north")
      ),
      be
    );
    world.Attach(be);
    return be;
  }

  private static BlockEntityTuyere Tuyere(TestWorld world, BlockPos pos)
  {
    var be = new BlockEntityTuyere();
    world.Place(pos, TestBlocks.Configure(new Block(), "iwex:tuyere-n", 4), be);
    world.Attach(be);
    return be;
  }

  private static BlockEntityHopperTall Hopper(TestWorld world, BlockPos pos)
  {
    var be = new BlockEntityHopperTall();
    world.Place(
      pos,
      TestBlocks.Configure(new Block(), "iwex:hopper-tall", 5),
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
  ) => BlockBehaviorMultiblockStructure.ResolveIncompleteAnchor(world.World, pos);

  #endregion

  #region Incomplete: projects from every functional component

  [Fact]
  public void An_incomplete_furnace_projects_from_the_metal_tap()
  {
    var world = new TestWorld();
    var furnace = Furnace(world, complete: false);
    var tap = Tap(world, furnace.MetalTapPos);

    Assert.Same(furnace, Resolve(world, tap.Pos));
  }

  [Fact]
  public void An_incomplete_furnace_projects_from_the_slag_tap()
  {
    var world = new TestWorld();
    var furnace = Furnace(world, complete: false);
    var tap = Tap(world, furnace.SlagTapPos);

    Assert.Same(furnace, Resolve(world, tap.Pos));
  }

  [Fact]
  public void An_incomplete_furnace_projects_from_the_tuyere()
  {
    var world = new TestWorld();
    var furnace = Furnace(world, complete: false);
    // A tuyere cell of the blast layout (structure-local (0, 1, -1)).
    var tuyere = Tuyere(world, Global(furnace, 0, 1, -1));

    Assert.Same(furnace, Resolve(world, tuyere.Pos));
  }

  [Fact]
  public void An_incomplete_furnace_projects_from_the_tall_hopper()
  {
    var world = new TestWorld();
    var furnace = Furnace(world, complete: false);
    // The cold furnace's tall hopper sits at structure-local (-2, 6, 0).
    var hopper = Hopper(world, Global(furnace, -2, 6, 0));

    Assert.Same(furnace, Resolve(world, hopper.Pos));
  }

  #endregion

  #region The projection a component resolves is the core's

  [Fact]
  public void A_component_resolves_the_same_anchor_the_core_does()
  {
    var world = new TestWorld();
    var furnace = Furnace(world, complete: false);
    var tap = Tap(world, furnace.MetalTapPos);

    // The gesture on the core resolves the core itself; the gesture on the tap resolves the same instance,
    // so both toggle the one outline - "the projection matches the core's".
    Assert.Same(Resolve(world, furnace.Pos), Resolve(world, tap.Pos));
    Assert.Same(furnace, Resolve(world, furnace.Pos));
  }

  #endregion

  #region Complete: projects from none

  [Fact]
  public void A_complete_furnace_does_not_project_from_a_component()
  {
    var world = new TestWorld();
    var furnace = Furnace(world, complete: true);
    var tap = Tap(world, furnace.MetalTapPos);
    var tuyere = Tuyere(world, Global(furnace, 0, 1, -1));
    var hopper = Hopper(world, Global(furnace, -2, 6, 0));

    Assert.Null(Resolve(world, tap.Pos));
    Assert.Null(Resolve(world, tuyere.Pos));
    Assert.Null(Resolve(world, hopper.Pos));
    // ...and not from the core either.
    Assert.Null(Resolve(world, furnace.Pos));
  }

  #endregion

  #region No furnace: resolves nothing

  [Fact]
  public void A_component_with_no_furnace_resolves_no_anchor()
  {
    var world = new TestWorld();
    var tap = Tap(world, new BlockPos(40, 20, 40)); // nothing around it

    Assert.Null(Resolve(world, tap.Pos));
  }

  #endregion
}
