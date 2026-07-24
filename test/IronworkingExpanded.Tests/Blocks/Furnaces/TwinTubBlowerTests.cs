using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using IronworkingExpanded.Items;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The twin-tub blower: the iron tier's only air source, a mechanically driven bellows that produces
/// into the blast main it stands in.
/// <para>
/// The tests that matter here are the <b>ordering</b> ones. The iron tier's progression gate is not a
/// rule anywhere in the code - it is the relationship between four numbers: what a coke-rich burden
/// demands, what a lean one demands, what bellows can raise, and what bolted pipe can hold. Order them
/// wrong and either the iron tier cannot blow its own furnace, or a cheap mechanical blower runs the
/// fuel-efficient burden the steam tier is supposed to be for. The rest pins the speed response, the
/// production path and the footprint that hosts the axle.
/// </para>
/// </summary>
public class TwinTubBlowerTests
{
  #region Tier balance

  // The iron tier's whole gate lives in four numbers: what a rich burden demands, what a lean one
  // demands, what the bellows can raise, and what bolted pipe can hold. Get their ORDER wrong and
  // either the iron tier cannot be blown at all, or a mechanical blower runs the fuel-efficient
  // burden that is supposed to require steam.

  private static readonly BurdenMix RichBurden = new(65f, 5f, 30f);
  private static readonly BurdenMix LeanBurden = new(85f, 5f, 10f);

  /// <summary>A cold blast furnace, purely to evaluate its burden-derived blast demand.</summary>
  private static BlockEntityBlastFurnaceCold Furnace()
  {
    var world = new TestWorld();
    var be = new BlockEntityBlastFurnaceCold
    {
      Pos = new BlockPos(0, 16, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "iwex:blastfurnacecore-north",
        1,
        ("side", "north")
      ),
    };
    world.Attach(be);
    // The live draw scales the cached reference rate, which Initialize would prime; cache it directly
    // rather than standing up the whole furnace just to read one number.
    ReflectionHelpers.Invoke(be, "CacheAttributes");
    return be;
  }

  [Fact]
  public void The_bellows_can_blow_a_coke_rich_burden_but_not_a_lean_one()
  {
    var furnace = Furnace();
    float rich = furnace.RequiredBlastPressureFor(RichBurden);
    float lean = furnace.RequiredBlastPressureFor(LeanBurden);
    float ceiling = IwexValues.TwinTubBlowerMaxPressure;

    // Brute force is allowed: a coke-hungry charge is permeable and blows on bellows.
    Assert.True(
      ceiling >= rich,
      $"the iron tier cannot blow its own furnace: bellows reach {ceiling} atm, a rich burden needs {rich}"
    );
    // Fuel efficiency is not: the lean charge is what the steam tier is for.
    Assert.True(
      ceiling < lean,
      $"bellows at {ceiling} atm run a lean burden needing {lean} atm - the steam gate is open"
    );
  }

  [Fact]
  public void The_bellows_never_burst_their_own_tier_of_pipe()
  {
    Assert.True(
      IwexValues.TwinTubBlowerMaxPressure < IwexValues.BoltedPipeBurstPressure,
      $"a blower at {IwexValues.TwinTubBlowerMaxPressure} atm bursts bolted pipe "
        + $"(burst {IwexValues.BoltedPipeBurstPressure} atm)"
    );
  }

  [Fact]
  public void A_lean_burden_needs_more_pressure_than_bolted_pipe_can_hold()
  {
    // The second half of the gate, and the one that makes it visible in world: even if a player found
    // a stronger iron-tier blower, the bolted main itself would burst before the lean burden lit.
    Assert.True(
      Furnace().RequiredBlastPressureFor(LeanBurden)
        > IwexValues.BoltedPipeBurstPressure,
      "bolted pipe can hold a lean burden's blast, so the pipe tier is not a gate"
    );
  }

  [Fact]
  public void Full_output_covers_a_two_tuyere_furnace_on_its_thirstiest_burden()
  {
    // A rich burden burns the most coke and so draws the most air - the highest demand the iron tier
    // ever has to meet. One blower must run one furnace, or the tier needs stacked blowers to work.
    float demand = 2 * Furnace().TuyereDrawFor(RichBurden);
    Assert.True(
      IwexValues.TwinTubBlowerOutputPerSecond >= demand,
      $"blower delivers {IwexValues.TwinTubBlowerOutputPerSecond} L/s but a rich-charged furnace "
        + $"draws {demand} L/s"
    );
  }

  #endregion

  #region Burden-derived demand

  [Fact]
  public void A_leaner_burden_demands_more_pressure_and_less_air()
  {
    var furnace = Furnace();

    // Coke is the permeable skeleton of the charge: less of it packs denser and resists the blast more,
    // while burning less fuel needs less oxygen. Both directions come from the one number.
    Assert.True(
      furnace.RequiredBlastPressureFor(LeanBurden)
        > furnace.RequiredBlastPressureFor(RichBurden),
      "a leaner (denser) burden should be harder to blow through"
    );
    Assert.True(
      furnace.TuyereDrawFor(LeanBurden) < furnace.TuyereDrawFor(RichBurden),
      "a leaner burden burns less coke and so should draw less air"
    );
  }

  [Fact]
  public void An_unstamped_charge_is_treated_as_the_default_grade()
  {
    var furnace = Furnace();
    var standard = new BurdenMix(
      100f - 100f * IwexValues.BfDefaultFuelFrac - 5f,
      5f,
      100f * IwexValues.BfDefaultFuelFrac
    );

    // Legacy count-only blast mix carries no composition; the heat balance reads it as standard grade
    // and the blast demand must agree, or old charges would silently become unblowable.
    Assert.Equal(
      furnace.RequiredBlastPressureFor(standard),
      furnace.RequiredBlastPressureFor(default),
      2
    );
    Assert.Equal(
      furnace.TuyereDrawFor(standard),
      furnace.TuyereDrawFor(default),
      2
    );
  }

  // (The ascending pipe-tier ordering is asserted in the lpex suite - iwex cannot see lpex's config.)

  #endregion

  #region Speed response

  [Theory]
  [InlineData(0f, 0f)]
  [InlineData(0.5f, 0f)] // at the minimum the bellows barely move
  [InlineData(1.0f, 0.5f)] // halfway between min and max
  [InlineData(1.5f, 1f)] // rated speed
  [InlineData(4f, 1f)] // over-driven: capped, never more than rated
  public void Output_scales_linearly_between_the_min_and_max_axle_speed(
    float speed,
    float expected
  )
  {
    Assert.Equal(
      expected,
      BlockEntityTwinTubMPBlower.SpeedFraction(speed),
      3
    );
  }

  [Fact]
  public void A_retuned_speed_band_moves_the_response_with_it()
  {
    float minOriginal = IwexValues.TwinTubBlowerMinSpeed;
    try
    {
      IwexValues.Edit(c => c.TwinTubBlowerMinSpeed = 1.0f);
      // What used to be half output is now nothing: the band starts where the config says it does.
      Assert.Equal(0f, BlockEntityTwinTubMPBlower.SpeedFraction(1.0f), 3);
    }
    finally
    {
      IwexValues.Edit(c => c.TwinTubBlowerMinSpeed = minOriginal);
    }
  }

  #endregion

  #region Production

  /// <summary>
  /// A blower standing as a node in its own (single-cell) blast main. <c>ProduceAir</c> is driven
  /// directly with an axle speed: the live tick reads its speed from a hosted MP filler port, which
  /// needs a real filler block entity the headless world does not build.
  /// </summary>
  private static (TestWorld world, PipeNetwork net, BlockEntityTwinTubMPBlower blower) Rig()
  {
    var world = new TestWorld();
    world.RegisterNetwork("pipe", sys => new PipeNetwork(sys));

    var blowerBlock = TestBlocks.Configure(
      new BlockTwinTubMPBlower(),
      "iwex:twintubmpblower-twintubmpblower-n",
      120,
      ("type", "twintubmpblower"),
      ("orientation", "n")
    );
    ReflectionHelpers.SetProperty(blowerBlock, "Type", "twintubmpblower");
    ReflectionHelpers.SetProperty(blowerBlock, "Orientation", "n");

    var blower = new BlockEntityTwinTubMPBlower();
    var pos = new BlockPos(0, 0, 0);
    world.Place(pos, blowerBlock, blower);
    world.Attach(blower);
    world.AddNode(pos, "pipe");
    ReflectionHelpers.SetProperty(
      blower,
      nameof(blower.NetworkSystem),
      world.Networks
    );

    return (world, (PipeNetwork)world.NetworkAt(pos)!, blower);
  }

  [Fact]
  public void A_driven_blower_puts_air_into_its_own_network()
  {
    var (_, net, blower) = Rig();
    Assert.Equal(0f, net.State?.Volume ?? 0f);

    float produced = blower.ProduceAir(IwexValues.TwinTubBlowerMaxSpeed, 1f);

    Assert.True(produced > 0f, "a driven blower should produce air");
    Assert.Equal("Air", net.State!.MediumType);
    Assert.True(net.State!.Volume > 0f);
  }

  [Fact]
  public void An_undriven_blower_leaves_the_main_alone()
  {
    var (world, net, blower) = Rig();
    net.TryProduceGas(20f, 20f, "Air", world.Accessor, maxOutputPressure: 2f);
    float before = net.State!.Volume;

    Assert.Equal(0f, blower.ProduceAir(0f, 1f));

    Assert.Equal(before, net.State!.Volume, 3);
  }

  [Fact]
  public void A_half_speed_axle_delivers_half_the_air_of_a_rated_one()
  {
    var (_, fastNet, fast) = Rig();
    var (_, slowNet, slow) = Rig();
    float mid =
      (IwexValues.TwinTubBlowerMinSpeed + IwexValues.TwinTubBlowerMaxSpeed) / 2f;

    float fullOutput = fast.ProduceAir(IwexValues.TwinTubBlowerMaxSpeed, 1f);
    float halfOutput = slow.ProduceAir(mid, 1f);

    Assert.True(fullOutput > 0f && halfOutput > 0f);
    Assert.Equal(fullOutput / 2f, halfOutput, 1);
  }

  [Fact]
  public void The_blower_never_pushes_its_line_past_the_pressure_ceiling()
  {
    var (_, net, blower) = Rig();

    // Blow far longer than the single cell can hold, so the ceiling - not the volume - is what stops it.
    for (int i = 0; i < 60; i++)
      blower.ProduceAir(IwexValues.TwinTubBlowerMaxSpeed, 1f);

    Assert.True(
      net.State!.Pressure <= IwexValues.TwinTubBlowerMaxPressure + 0.001f,
      $"line reached {net.State!.Pressure} atm, ceiling is {IwexValues.TwinTubBlowerMaxPressure}"
    );
  }

  #endregion

  #region Footprint

  [Fact]
  public void The_footprint_hosts_a_mechanical_port_on_its_upper_rear_cell()
  {
    ExBlockDef def = BlockTwinTubMPBlower.Definitions("iwex").Single();
    var offsets = (JArray)def.ToJson()["attributes"]!["fillerOffsets"]!;

    // 1x2x3 minus the principal = 5 filler cells.
    Assert.Equal(5, offsets.Count);

    JToken port = offsets.Single(o =>
      (int)o["y"]! == 1 && (int)o["z"]! == 0 && (int)o["x"]! == 0
    );
    var behaviors = (JArray)port["behaviors"]!;
    Assert.Equal("exlib.BEBehaviorMPFillerPort", (string)behaviors[0]!["code"]!);
    Assert.Equal("west", (string)behaviors[0]!["face"]!);
    Assert.True((bool)port["allowAttach"]!);
  }

  [Theory]
  [InlineData("n", 0)]
  [InlineData("e", 90)]
  [InlineData("s", 180)]
  [InlineData("w", 270)]
  public void The_structure_angle_follows_the_orientation_variant(
    string orientation,
    int expected
  )
  {
    var block = TestBlocks.Configure(
      new BlockTwinTubMPBlower(),
      $"iwex:twintubmpblower-twintubmpblower-{orientation}",
      121,
      ("type", "twintubmpblower"),
      ("orientation", orientation)
    );
    Assert.Equal(expected, block.StructureAngle);
  }

  #endregion
}
