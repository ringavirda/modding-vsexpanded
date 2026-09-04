using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkPipe;
using IronIndustryExpanded.BlockNetworkPipe.BlockEntities;
using IronIndustryExpanded.BlockNetworkPipe.Blocks;
using IronIndustryExpanded.Tests;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The pressure-relief valve's player-set gate: stepping it up or down is clamped to
/// [<see cref="BlockEntityPressureValve.MinGatePressure"/>, the valve's burst rating], reports
/// whether it actually moved, and persists across a save/reload, defaulting to 1 atm when the saved
/// tree carries no gate value.
/// </summary>
public class PressureValveBeTests {
  // The shipped code and variant shape. The `tier` variant is what BlockPipe.BurstPressure reads, so a
  // fixture that omits it takes the 5 atm default - numerically identical to the cast tier's rating,
  // which is why the omission would not show up in any assertion below.
  private static BlockPressureValve ValveBlock() {
    var block = TestBlocks.Configure(
      new BlockPressureValve(),
      "iiex:pipe-cast-pressurevalve-ns",
      40,
      ("tier", "cast"),
      ("type", "pressurevalve"),
      ("orientation", "ns")
    );
    ReflectionHelpers.SetProperty(block, "Type", "pressurevalve");
    ReflectionHelpers.SetProperty(block, "Orientation", "ns");
    return block;
  }

  private static BlockEntityPressureValve Valve() {
    var world = new TestWorld();
    var be = new BlockEntityPressureValve {
      Pos = new BlockPos(0, 0, 0),
      Block = ValveBlock(),
    };
    world.Attach(be);
    return be;
  }

  #region Rating

  [Fact]
  public void MaxGatePressure_is_the_blocks_burst_rating() {
    var be = Valve();
    Assert.Equal(
      ((BlockPressureValve)be.Block).BurstPressure,
      be.MaxGatePressure
    );
  }

  [Fact]
  public void The_ceiling_is_the_cast_tier_s_rating_and_it_comes_from_the_variant() {
    var be = Valve();
    Assert.Equal("cast", ((BlockPressureValve)be.Block).Tier);
    Assert.Equal(PipeTestWorld.CastTierBurst, be.MaxGatePressure, 3);

    // Proves the number is resolved through the `tier` variant rather than the domain or the default,
    // both of which answer 5 for this block. A rating registered against a tier no shipped block wears
    // must reach a valve wearing it - and must not disturb the three shipped tiers.
    const string probeTier = "castvalveprobe";
    BlockPipe.RegisterBurst(probeTier, () => 9.5f);
    var probe = TestBlocks.Configure(
      new BlockPressureValve(),
      $"iiex:pipe-{probeTier}-pressurevalve-ns",
      41,
      ("tier", probeTier),
      ("type", "pressurevalve"),
      ("orientation", "ns")
    );
    Assert.Equal(9.5f, probe.BurstPressure, 3);
    Assert.Equal(PipeTestWorld.CastTierBurst, be.MaxGatePressure, 3);
  }

  #endregion

  #region Adjusting the gate

  [Fact]
  public void Increase_raises_the_gate_by_one_step() {
    var be = Valve();
    float before = be.GatePressure;

    Assert.True(be.AdjustGatePressure(increase: true));
    Assert.Equal(
      before + BlockEntityPressureValve.GatePressureStep,
      be.GatePressure,
      3
    );
  }

  [Fact]
  public void Decrease_lowers_the_gate_by_one_step() {
    var be = Valve();
    float before = be.GatePressure;

    Assert.True(be.AdjustGatePressure(increase: false));
    Assert.Equal(
      before - BlockEntityPressureValve.GatePressureStep,
      be.GatePressure,
      3
    );
  }

  [Fact]
  public void Raising_clamps_at_the_tier_ceiling_and_then_reports_no_change() {
    var be = Valve();

    // Step up until it pins at the ceiling.
    for (int i = 0; i < 100 && be.AdjustGatePressure(true); i++) { }

    Assert.Equal(be.MaxGatePressure, be.GatePressure, 3);
    Assert.False(be.AdjustGatePressure(true)); // already at the rail
  }

  [Fact]
  public void Lowering_clamps_at_the_floor_and_then_reports_no_change() {
    var be = Valve();

    for (int i = 0; i < 100 && be.AdjustGatePressure(false); i++) { }

    Assert.Equal(BlockEntityPressureValve.MinGatePressure, be.GatePressure, 3);
    Assert.False(be.AdjustGatePressure(false));
  }

  #endregion

  #region Persistence

  [Fact]
  public void Gate_pressure_round_trips_through_the_tree() {
    var src = Valve();
    src.AdjustGatePressure(true);
    src.AdjustGatePressure(true); // 1 -> 1.5
    float expected = src.GatePressure;

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);

    var world = new TestWorld();
    var restored = new BlockEntityPressureValve {
      Pos = new BlockPos(0, 0, 0),
      Block = ValveBlock(),
    };
    world.Attach(restored);
    restored.FromTreeAttributes(tree, world.World);

    Assert.Equal(expected, restored.GatePressure, 3);
  }

  [Fact]
  public void A_save_without_a_gate_value_defaults_to_one_atm() {
    var world = new TestWorld();
    var be = new BlockEntityPressureValve {
      Pos = new BlockPos(0, 0, 0),
      Block = ValveBlock(),
    };
    world.Attach(be);

    // A tree with the base pipe keys but no "gatePressure".
    var tree = new TreeAttribute();
    tree.SetString("networkType", "pipe");
    tree.SetString("orientation", "ns");
    tree.SetString("possibleOrientations", "[]");
    be.FromTreeAttributes(tree, world.World);

    Assert.Equal(1f, be.GatePressure, 3);
  }

  #endregion

  #region Overflow venting (network-backed)

  /// <summary>
  /// A valve at (0,0,1) facing "ns": its input face (north) butts against a sealed 2-cell pipe run at
  /// (0,0,0)/(0,0,-1), capped by rock to the north and by the valve block to the south, and its output
  /// face (south) is open air, so overflow vents to atmosphere. Returns the live input network so the
  /// test can pressurise it.
  /// </summary>
  private static (
    TestWorld world,
    BlockEntityPressureValve valve,
    PipeNetwork inNet
  ) VentRig() {
    var world = new TestWorld();
    world.RegisterNetwork("pipe", sys => new PipeNetwork(sys));

    var pipe = PipeTestWorld.MakePipe(orientation: "ns");
    world.Place(new BlockPos(0, 0, -1), pipe);
    world.Place(new BlockPos(0, 0, 0), pipe);

    var valveBlock = ValveBlock();
    var valve = new BlockEntityPressureValve {
      Pos = new BlockPos(0, 0, 1),
      Block = valveBlock,
    };
    world.Place(valve.Pos, valveBlock); // caps the run's south end (non-air)
    world.Attach(valve);
    ReflectionHelpers.SetProperty(
      valve,
      nameof(valve.NetworkSystem),
      world.Networks
    );

    var rock = TestBlocks.Configure(new Block(), "game:rock", 99);
    world.Place(new BlockPos(0, 0, -2), rock); // caps the north end

    world.AddNode(new BlockPos(0, 0, -1), "pipe");
    world.AddNode(new BlockPos(0, 0, 0), "pipe");

    var inNet = (PipeNetwork)world.NetworkAt(new BlockPos(0, 0, 0))!;
    return (world, valve, inNet);
  }

  private static float Tick(BlockEntityPressureValve valve) =>
    (float)ReflectionHelpers.GetField(valve, "_lastVentVolume")!;

  private static void RunTick(BlockEntityPressureValve valve) =>
    ReflectionHelpers.Invoke(valve, "OnTick", 1f);

  [Fact]
  public void Above_the_gate_an_open_face_vents_gas_to_atmosphere() {
    var (world, valve, inNet) = VentRig();

    // MaxVolume = 2 pipes * 30 L = 60; gate is 1 atm, so >60 L is overflow.
    PipeTestWorld.Saturate(
      inNet,
      150f,
      "Steam",
      world.Accessor,
      maxOutputPressure: 10f
    );
    float before = inNet.State!.Volume;

    RunTick(valve);

    Assert.True(Tick(valve) > 0f); // something vented
    Assert.True(inNet.State!.Volume < before); // drawn out of the run
  }

  [Fact]
  public void At_or_below_the_gate_nothing_vents() {
    var (world, valve, inNet) = VentRig();

    // 30 L is below the 60 L gate allowance, so the valve stays shut.
    inNet.TryProduceGas(
      30f,
      150f,
      "Steam",
      world.Accessor,
      maxOutputPressure: 10f
    );

    RunTick(valve);

    Assert.Equal(0f, Tick(valve), 3);
  }

  #endregion
}
