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
/// The furnace HUD is split across the functional component blocks: the lower (metal) tap shows the molten
/// metal pool it drains, the upper (slag) tap the slag pool, the tall hopper the burden loaded in the shaft,
/// and the core keeps only the temperature and heat ledger. Each component scans up to the furnace core it
/// belongs to - the anchor pushes to its components by offset and nothing points back - and reads the core's
/// synced state. Both taps are the same block, so which pool a tap shows is decided by the tap cell it sits
/// on. Covers the cold blast furnace and the cupola, which share this path with the hot blast furnace.
/// </summary>
// Joins the furnace-config collection as a reader: it drives real furnaces, so it must not run while
// HeatBalanceTests has BfCombustionBaseTemp or BfCokeSensitivity moved off their shipped values.
[Collection(FurnaceConfigCollection.Name)]
public class FurnaceHudDistributionTests {
  // The lang service echoes the key back (TestLang), so a slice is present exactly when its key appears.
  private const string MetalKey = "iwex:bf-info-molteniron";
  private const string SlagKey = "iwex:bf-info-moltenslag";
  private const string CastIronKey = "iwex:cupola-info-moltencastiron";

  // The shaft-charge line: the loaded count against the furnace's capacity.
  private const string MixKey = "iwex:bf-info-shaftfull";
  private const string WrongBurdenKey = "iwex:bf-info-wrongburden";
  private const string TapStateKey = "iwex:tap-state";
  private const string NeedsMixKey = "iwex:bf-info-needsmix";

  private static TestWorld NewWorld() {
    var world = new TestWorld();
    world.RegisterItem("game:ingot-iron", 1500f);
    world.RegisterItem("iwex:slag");
    return world;
  }

  #region Furnace + component standup

  /// <summary>
  /// Builds a complete, north-facing furnace core at <paramref name="pos"/> carrying its real multiblock
  /// layout, so <see cref="BlockEntityMultiblockStructure.OwnsCell"/> answers off the same transformed
  /// offsets the game builds from the shipped definition. North makes structure-local offsets equal
  /// world-relative ones, so callers place components at the plain offset. The caller primes the state.
  /// </summary>
  private static T Furnace<T>(
    TestWorld world,
    BlockPos pos,
    string code,
    JObject attributes
  )
    where T : BlockEntityFurnaceCore, new() {
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
      "iwex:furnace-blastcore-tier1-n",
      (JObject)
        BlockBlastFurnaceCoreCold.Definitions("iwex").Single().ToJson()[
          "attributes"
        ]!
    );

  private static BlockEntityCupolaFurnace CupolaFurnace(TestWorld world) =>
    Furnace<BlockEntityCupolaFurnace>(
      world,
      new BlockPos(0, 16, 0),
      "iwex:furnace-cupolacore-tier1-n",
      (JObject)
        BlockCupolaFurnaceCore.Definitions("iwex").Single().ToJson()[
          "attributes"
        ]!
    );

  // `type` names the tap's family member, irontap or slagtap. The HUD and the projection both key off the
  // tap's position rather than its type; call sites still pass it so the stand-in matches its cell.
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
        $"iwex:furnace-{type}-n",
        2,
        ("type", type),
        ("side", "north")
      ),
      be
    );
    world.Attach(be);
    return be;
  }

  private static BlockEntityHopperTall Hopper(TestWorld world, BlockPos pos) {
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

  private static string Info(BlockEntity be) {
    var sb = new StringBuilder();
    be.GetBlockInfo(null!, sb);
    return sb.ToString();
  }

  #endregion

  #region Metal tap

  [Fact]
  public void The_metal_tap_surfaces_the_molten_metal_pool() {
    var world = NewWorld();
    var furnace = ColdFurnace(world);
    // Both pools loaded: the metal tap must show the metal pool and not the slag one.
    ReflectionHelpers.SetField(furnace, "_moltenIron", 80f);
    ReflectionHelpers.SetField(furnace, "_moltenSlag", 40f);

    var tap = Tap(world, furnace.MetalTapPos!);
    string info = Info(tap);

    Assert.Contains(MetalKey, info);
    Assert.DoesNotContain(SlagKey, info);
    Assert.Contains(TapStateKey, info); // still shows its own open/closed line
  }

  [Fact]
  public void An_empty_metal_tap_shows_only_its_open_closed_line() {
    var world = NewWorld();
    var furnace = ColdFurnace(world);
    // Idle furnace, empty pool: the metal readout appears only when there is metal to report.
    var tap = Tap(world, furnace.MetalTapPos!);
    string info = Info(tap);

    Assert.DoesNotContain(MetalKey, info);
    Assert.Contains(TapStateKey, info);
  }

  #endregion

  #region Slag tap

  [Fact]
  public void The_slag_tap_surfaces_the_molten_slag_pool() {
    var world = NewWorld();
    var furnace = ColdFurnace(world);
    ReflectionHelpers.SetField(furnace, "_moltenIron", 80f);
    ReflectionHelpers.SetField(furnace, "_moltenSlag", 40f);

    var tap = Tap(world, furnace.SlagTapPos!, BlockFurnaceTap.SlagType);
    string info = Info(tap);

    Assert.Contains(SlagKey, info);
    Assert.DoesNotContain(MetalKey, info);
  }

  #endregion

  #region Tall hopper

  [Fact]
  public void The_hopper_surfaces_the_shaft_charge_and_the_wrong_burden_warning() {
    var world = NewWorld();
    var furnace = ColdFurnace(world);
    ReflectionHelpers.SetField(furnace, "_cachedMixCount", 220);
    ReflectionHelpers.SetField(furnace, "_cachedRejectedCount", 50);

    // The cold furnace's tall hopper sits at structure-local (-2, 6, 0), six cells above the hearth.
    var hopper = Hopper(world, Global(furnace, -2, 6, 0));
    string info = Info(hopper);

    Assert.Contains(MixKey, info);
    Assert.Contains(WrongBurdenKey, info);
  }

  #endregion

  #region Core keeps only its slice

  [Fact]
  public void The_core_drops_the_moved_lines_and_keeps_its_own() {
    var world = NewWorld();
    // The core's GetBlockInfo is throttled on world-elapsed ms; push past the interval so it rebuilds.
    world.World.ElapsedMilliseconds.Returns(10000L);
    var furnace = ColdFurnace(world);
    // Load the pools and shaft so the moved lines would print if they were still on the core.
    ReflectionHelpers.SetField(furnace, "_moltenIron", 80f);
    ReflectionHelpers.SetField(furnace, "_cachedMixCount", 100);
    // A furnace that has never ticked is Idle. `State` has no setter, so this states the premise rather
    // than arranging it.
    Assert.Equal(FurnaceState.Idle, furnace.State);

    string info = Info(furnace);

    Assert.DoesNotContain(MetalKey, info);
    Assert.DoesNotContain(MixKey, info);
    Assert.DoesNotContain(WrongBurdenKey, info);
    // The core keeps its own readiness slice: the shaft is not full.
    Assert.Contains(NeedsMixKey, info);
  }

  #endregion

  #region No furnace / cupola

  [Fact]
  public void A_tap_with_no_furnace_shows_only_its_own_line() {
    var world = NewWorld();
    var tap = Tap(world, new BlockPos(40, 20, 40)); // nothing around it
    string info = Info(tap);

    Assert.Contains(TapStateKey, info);
    Assert.DoesNotContain(MetalKey, info);
    Assert.DoesNotContain(SlagKey, info);
  }

  [Fact]
  public void The_cupola_metal_tap_shows_cast_iron_not_pig_iron() {
    var world = NewWorld();
    var furnace = CupolaFurnace(world);
    ReflectionHelpers.SetField(furnace, "_moltenIron", 60f);

    var tap = Tap(world, furnace.MetalTapPos!);
    string info = Info(tap);

    Assert.Contains(CastIronKey, info); // the cupola relabels its molten product
    Assert.DoesNotContain(MetalKey, info);
  }

  #endregion
}
