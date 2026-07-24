using System.Linq;
using System.Text;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The furnace HUD is spread across its functional component blocks: the lower (metal) tap shows the
/// molten metal pool it drains, the upper (slag) tap the slag pool, the tall hopper the burden loaded in
/// the shaft, and the core keeps the temperature/heat ledger only. Each component scans up to the furnace
/// core it belongs to (the anchor pushes to its components by offset and nothing points back) and reads
/// the core's synced state, showing only its own slice. This pins that split: each component surfaces the
/// right slice from a driven furnace, the moved lines are gone from the core, and a component with no
/// furnace stays silent. Both taps are the same block, so which pool a tap shows is decided purely by
/// which tap cell it sits on - that disambiguation is pinned too. Covers the cold blast furnace and the
/// cupola (same base, cast-iron label), which share the whole path with the hot blast furnace.
/// </summary>
// Joins the furnace-config collection as a *reader*: it drives real furnaces, so it must not run while
// HeatBalanceTests has BfCombustionBaseTemp or BfCokeSensitivity turned off their shipped values.
[Collection(FurnaceConfigCollection.Name)]
public class FurnaceHudDistributionTests
{
  // The lang service echoes the key back (TestLang), so a slice is present exactly when its key appears.
  private const string MetalKey = "iwex:bf-info-molteniron";
  private const string SlagKey = "iwex:bf-info-moltenslag";
  private const string CastIronKey = "iwex:cupola-info-moltencastiron";
  private const string MixKey = "iwex:bf-info-mixloaded";
  private const string WrongBurdenKey = "iwex:bf-info-wrongburden";
  private const string TapStateKey = "iwex:tap-state";
  private const string NeedsMixKey = "iwex:bf-info-needsmix";

  private static TestWorld NewWorld()
  {
    var world = new TestWorld();
    world.RegisterItem("game:ingot-iron", 1500f);
    world.RegisterItem("iwex:slag");
    return world;
  }

  #region Furnace + component standup

  /// <summary>
  /// Builds a complete, north-facing furnace core at <paramref name="pos"/> carrying its real multiblock
  /// layout - so the anchor resolver's <see cref="BlockEntityMultiblockStructure.OwnsCell"/> answers off the
  /// same transformed offsets the game builds from the shipped definition. North means structure-local ==
  /// world-relative, so callers place components at the plain offset. State is primed by the caller.
  /// </summary>
  private static T Furnace<T>(
    TestWorld world,
    BlockPos pos,
    string code,
    JObject attributes
  )
    where T : BlockEntityFurnaceCore, new()
  {
    var block = TestBlocks.Configure(new Block(), code, 1, ("side", "north"));
    block.Attributes = new JsonObject(attributes);

    var be = new T();
    world.Place(pos, block, be);
    world.Attach(be);
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
    ReflectionHelpers.Invoke(be, "CacheAttributes");
    ReflectionHelpers.SetProperty(be, nameof(be.StructureComplete), true);
    return be;
  }

  private static BlockEntityBlastFurnaceCold ColdFurnace(TestWorld world) =>
    Furnace<BlockEntityBlastFurnaceCold>(
      world,
      new BlockPos(0, 16, 0),
      "iwex:blastfurnacecore-north",
      (JObject)
        BlockBlastFurnaceCoreCold.Definitions("iwex").Single().ToJson()[
          "attributes"
        ]!
    );

  private static BlockEntityCupolaFurnace CupolaFurnace(TestWorld world) =>
    Furnace<BlockEntityCupolaFurnace>(
      world,
      new BlockPos(0, 16, 0),
      "iwex:cupolafurnacecore-north",
      (JObject)
        BlockCupolaFurnaceCore.Definitions("iwex").Single().ToJson()[
          "attributes"
        ]!
    );

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

  private static BlockEntityHopperTall Hopper(TestWorld world, BlockPos pos)
  {
    var be = new BlockEntityHopperTall();
    world.Place(
      pos,
      TestBlocks.Configure(new Block(), "iwex:hopper-tall", 3),
      be
    );
    world.Attach(be);
    return be;
  }

  private static BlockPos Global(BlockEntity be, int x, int y, int z) =>
    (BlockPos)ReflectionHelpers.Invoke(be, "GetGlobalPos", x, y, z)!;

  private static string Info(BlockEntity be)
  {
    var sb = new StringBuilder();
    be.GetBlockInfo(null!, sb);
    return sb.ToString();
  }

  #endregion

  #region Metal tap

  [Fact]
  public void The_metal_tap_surfaces_the_molten_metal_pool()
  {
    var world = NewWorld();
    var furnace = ColdFurnace(world);
    // Both pools loaded: the metal tap must show its own (metal) and not the slag tap's.
    ReflectionHelpers.SetField(furnace, "_moltenIron", 80f);
    ReflectionHelpers.SetField(furnace, "_moltenSlag", 40f);

    var tap = Tap(world, furnace.MetalTapPos);
    string info = Info(tap);

    Assert.Contains(MetalKey, info);
    Assert.DoesNotContain(SlagKey, info);
    Assert.Contains(TapStateKey, info); // still shows its own open/closed line
  }

  [Fact]
  public void An_empty_metal_tap_shows_only_its_open_closed_line()
  {
    var world = NewWorld();
    var furnace = ColdFurnace(world);
    // Idle furnace, empty pool: the gauge stays quiet - the readout appears only when there is metal.
    var tap = Tap(world, furnace.MetalTapPos);
    string info = Info(tap);

    Assert.DoesNotContain(MetalKey, info);
    Assert.Contains(TapStateKey, info);
  }

  #endregion

  #region Slag tap

  [Fact]
  public void The_slag_tap_surfaces_the_molten_slag_pool()
  {
    var world = NewWorld();
    var furnace = ColdFurnace(world);
    ReflectionHelpers.SetField(furnace, "_moltenIron", 80f);
    ReflectionHelpers.SetField(furnace, "_moltenSlag", 40f);

    var tap = Tap(world, furnace.SlagTapPos);
    string info = Info(tap);

    Assert.Contains(SlagKey, info);
    Assert.DoesNotContain(MetalKey, info);
  }

  #endregion

  #region Tall hopper

  [Fact]
  public void The_hopper_surfaces_the_shaft_charge_and_the_wrong_burden_warning()
  {
    var world = NewWorld();
    var furnace = ColdFurnace(world);
    ReflectionHelpers.SetField(furnace, "_cachedMixCount", 220);
    ReflectionHelpers.SetField(furnace, "_cachedRejectedCount", 50);
    ReflectionHelpers.SetField(furnace, "_cachedRejectedFamily", "remelt");

    // The cold furnace's tall hopper sits at structure-local (-2, 6, 0) - six cells above the hearth.
    var hopper = Hopper(world, Global(furnace, -2, 6, 0));
    string info = Info(hopper);

    Assert.Contains(MixKey, info);
    Assert.Contains(WrongBurdenKey, info);
  }

  #endregion

  #region Core keeps only its slice

  [Fact]
  public void The_core_drops_the_moved_lines_and_keeps_its_own()
  {
    var world = NewWorld();
    // The core's GetBlockInfo is throttled on world-elapsed ms; push past the interval so it rebuilds.
    world.World.ElapsedMilliseconds.Returns(10000L);
    var furnace = ColdFurnace(world);
    // Load the pools and shaft so, had the lines stayed, they would print - proving their absence is the
    // split, not an empty furnace. Idle so the (unchanged) lit heat ledger is not exercised here.
    ReflectionHelpers.SetField(furnace, "_moltenIron", 80f);
    ReflectionHelpers.SetField(furnace, "_cachedMixCount", 100);
    ReflectionHelpers.SetProperty(
      furnace,
      nameof(furnace.State),
      FurnaceState.Idle
    );

    string info = Info(furnace);

    Assert.DoesNotContain(MetalKey, info);
    Assert.DoesNotContain(MixKey, info);
    Assert.DoesNotContain(WrongBurdenKey, info);
    // The core still shows its own not-lit readiness slice (the shaft is not full).
    Assert.Contains(NeedsMixKey, info);
  }

  #endregion

  #region No furnace / cupola

  [Fact]
  public void A_tap_with_no_furnace_shows_only_its_own_line()
  {
    var world = NewWorld();
    var tap = Tap(world, new BlockPos(40, 20, 40)); // nothing around it
    string info = Info(tap);

    Assert.Contains(TapStateKey, info);
    Assert.DoesNotContain(MetalKey, info);
    Assert.DoesNotContain(SlagKey, info);
  }

  [Fact]
  public void The_cupola_metal_tap_shows_cast_iron_not_pig_iron()
  {
    var world = NewWorld();
    var furnace = CupolaFurnace(world);
    ReflectionHelpers.SetField(furnace, "_moltenIron", 60f);

    var tap = Tap(world, furnace.MetalTapPos);
    string info = Info(tap);

    Assert.Contains(CastIronKey, info); // the cupola relabels its molten product
    Assert.DoesNotContain(MetalKey, info);
  }

  #endregion
}
