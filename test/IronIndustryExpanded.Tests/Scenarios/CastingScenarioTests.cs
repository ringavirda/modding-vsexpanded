using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkMolten;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Whole-process casting scenarios (handbook casting article): molten iron poured into a canal run flows
/// down the network and is cast at the far end, into a tool mold on a pedestal or into a parked barrel on
/// a canal tap. Exercises the molten network and the draining fittings together, asserting that the metal
/// travels the run and ends up in the casting.
/// </summary>
public class CastingScenarioTests {
  #region Mold pedestal casting

  [Fact]
  public void Molten_iron_flows_down_the_canal_and_casts_into_a_mold_pedestal() {
    var scene = new Scene().Network("molten", s => new MoltenNetwork(s));
    var line = new CastingLine(
      scene,
      new BlockPos(0, 0, 0),
      length: 3,
      endsInPedestal: true
    );
    scene.Build();
    line.SetMold();

    line.PourIn(40); // furnace tap charges the head cell
    line.Run(20); // the network carries it to the pedestal, which casts it

    Assert.True(
      line.Pedestal!.MoldCurrentUnits > 0,
      "the mold should have taken a cast"
    );
    // Conservation: every unit is either still in the run or cast into the mold (nothing vanished).
    Assert.Equal(40, line.TotalInRun + line.Pedestal.MoldCurrentUnits);
  }

  #endregion

  #region Barrel tapping

  [Fact]
  public void A_canal_tap_drains_the_run_into_a_parked_barrel() {
    var scene = new Scene().Network("molten", s => new MoltenNetwork(s));
    var line = new CastingLine(
      scene,
      new BlockPos(0, 0, 0),
      length: 3,
      endsInPedestal: false
    );
    scene.Build();
    line.ParkBarrel(drainSpeed: 8f);

    line.PourIn(40);
    line.Run(20);

    Assert.True(
      line.Tap!.BarrelCurrentUnits > 0,
      "the barrel should have filled from the run"
    );
    Assert.Equal(40, line.TotalInRun + line.Tap.BarrelCurrentUnits);
  }

  [Fact]
  public void A_closed_tap_keeps_the_metal_in_the_run() {
    var scene = new Scene().Network("molten", s => new MoltenNetwork(s));
    var line = new CastingLine(
      scene,
      new BlockPos(0, 0, 0),
      length: 3,
      endsInPedestal: false
    );
    scene.Build();
    line.ParkBarrel(drainSpeed: 8f);
    line.Tap!.TryTogglePouring(); // close it again

    line.PourIn(40);
    line.Run(10);

    Assert.Equal(0, line.Tap.BarrelCurrentUnits); // nothing poured through the shut tap
  }

  #endregion

  #region A forked yard (one heat, two stations)

  /// <summary>
  /// A single tap feeding a canal that forks, so one heat serves a mold pedestal and a barrel at the same
  /// time. Laid out with <see cref="SceneDiagram"/> because the topology is the subject here. Both
  /// stations must draw, and the metal must balance across the whole yard.
  /// </summary>
  [Fact]
  public void One_tap_feeds_two_casting_stations_through_a_forked_canal() {
    var scene = new Scene().Network("molten", s => new MoltenNetwork(s));
    var yard = new CastingYard(scene, new BlockPos(0, 0, 0));
    scene.Build();
    yard.OpenBothStations();

    // One cell holds 50 u, so a heat is poured in over several taps rather than in one shot: the head
    // cell is a canal cell, not a ladle.
    for (int i = 0; i < 4; i++)
      yard.PourIn(40).Run(10);

    Assert.True(
      yard.Pedestal.MoldCurrentUnits > 0,
      "the mold station should have taken a cast"
    );
    Assert.True(
      yard.Tap.BarrelCurrentUnits > 0,
      "the barrel station should have filled"
    );
    // Conservation across a branching run, not just a straight one: whatever the head accepted is
    // either standing in a cell or cast at one of the two stations.
    Assert.Equal(
      yard.TotalPoured,
      yard.TotalInRun
        + yard.Pedestal.MoldCurrentUnits
        + yard.Tap.BarrelCurrentUnits
    );
  }

  #endregion
}
