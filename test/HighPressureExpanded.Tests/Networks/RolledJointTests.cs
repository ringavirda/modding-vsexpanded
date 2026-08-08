using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.Tests;
using LowPressureExpanded.BlockNetworkPipe;
using LowPressureExpanded.Tests;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace HighPressureExpanded.Tests;

/// <summary>
/// The rolled (HP) pipe tier does not couple to the tiers below it. The rule is about shape rather than
/// pressure: plated and cast pipe are square in section and bolted through flanges, so they mate with
/// each other, while rolled pipe is octagonal and welded with no flange. Pinned from both directions,
/// because the graph is walked from whichever node it reaches first and a direction-dependent joint
/// check yields a network that exists one way and not the other. This suite is the only one that sees
/// all three tiers.
/// </summary>
public class RolledJointTests {
  private static BlockPipe Pipe(string domain, int id) =>
    PipeTestWorld.MakePipe(
      material: domain switch {
        "hpex" => "hadfield",
        "lpex" => "steel",
        _ => "iron",
      },
      id: id,
      orientation: "ns"
    );

  /// <summary>Two pipe cells butted together along +Z, each registered as a node.</summary>
  private static TestWorld Butted(string domainA, string domainB) {
    var world = new TestWorld();
    world.RegisterNetwork("pipe", s => new PipeNetwork(s));
    world.Place(new BlockPos(0, 0, 0), Pipe(domainA, 1));
    world.Place(new BlockPos(0, 0, 1), Pipe(domainB, 2));
    world.AddNode(new BlockPos(0, 0, 0), "pipe");
    world.AddNode(new BlockPos(0, 0, 1), "pipe");
    return world;
  }

  private static bool OneNetwork(TestWorld world) =>
    ReferenceEquals(
      world.NetworkAt(new BlockPos(0, 0, 0)),
      world.NetworkAt(new BlockPos(0, 0, 1))
    );

  #region The tiers that do couple

  [Theory]
  [InlineData("iwex", "iwex")]
  [InlineData("lpex", "lpex")]
  [InlineData("iwex", "lpex")] // plated -> cast: both square and flanged
  [InlineData("lpex", "iwex")]
  [InlineData("hpex", "hpex")]
  public void Pipes_sharing_a_joint_form_one_network(string a, string b) {
    Assert.True(
      OneNetwork(Butted(a, b)),
      $"{a} and {b} pipe share a joint and should have merged"
    );
  }

  #endregion

  #region The joint that does not

  [Theory]
  [InlineData("hpex", "iwex")]
  [InlineData("iwex", "hpex")]
  [InlineData("hpex", "lpex")]
  [InlineData("lpex", "hpex")]
  public void A_rolled_pipe_never_joins_a_lower_tier(string a, string b) {
    // Both orderings: AcceptsNeighbour has to answer symmetrically.
    Assert.False(
      OneNetwork(Butted(a, b)),
      $"{a} and {b} pipe must not couple - welded octagonal against plated square"
    );
  }

  [Fact]
  public void The_refused_joint_reads_as_an_open_end_not_a_seal() {
    // A refused joint is not a cap: the rolled run's face stays unmated, so it vents rather than
    // holding pressure.
    var world = Butted("hpex", "iwex");
    var net = (PipeNetwork)world.NetworkAt(new BlockPos(0, 0, 0))!;
    net.TryProduceGas(
      60f,
      200f,
      "Steam",
      world.Accessor,
      maxOutputPressure: 10f
    );

    world.Tick();

    Assert.True(
      net.State!.OpeningsCount > 0,
      "the unmated face should be detected as an open end"
    );
  }

  #endregion

  #region Joint families

  [Fact]
  public void The_three_tiers_declare_the_joints_the_models_show() {
    Assert.Equal(BlockPipe.FlangedJoint, Pipe("iwex", 1).JointFamily);
    Assert.Equal(BlockPipe.FlangedJoint, Pipe("lpex", 2).JointFamily);
    Assert.Equal(BlockPipe.WeldedJoint, Pipe("hpex", 3).JointFamily);
  }

  [Fact]
  public void A_machine_port_is_not_a_pipe_and_is_unaffected() {
    // The rule covers pipe-to-pipe couplings only; an HP run must still reach machine ports.
    BlockPipe rolled = Pipe("hpex", 1);
    var port = TestBlocks.Configure(new Block(), "smex:converter-intake-n", 2);

    Assert.True(rolled.AcceptsNeighbour(port));
  }

  #endregion
}
