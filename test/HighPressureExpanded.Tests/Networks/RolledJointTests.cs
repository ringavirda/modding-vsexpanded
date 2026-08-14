using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.Tests;
using IronIndustryExpanded.BlockNetworkPipe;
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
/// <para>
/// Parameterised by tier, not by the domain that ships it. Domain was a usable proxy while each tier
/// had its own mod; once plated and cast landed in one, every plated-vs-cast row collapsed into a
/// duplicate that asserted nothing about the pairing it was named for.
/// </para>
/// </summary>
public class RolledJointTests {
  private static BlockPipe Pipe(string tier, int id) =>
    PipeTestWorld.MakePipe(
      material: tier switch {
        BlockPipe.RolledTier => "hadfield",
        BlockPipe.CastTier => "steel",
        _ => "iron",
      },
      id: id,
      orientation: "ns"
    );

  /// <summary>Two pipe cells butted together along +Z, each registered as a node.</summary>
  private static TestWorld Butted(string tierA, string tierB) {
    var world = new TestWorld();
    world.RegisterNetwork("pipe", s => new PipeNetwork(s));
    world.Place(new BlockPos(0, 0, 0), Pipe(tierA, 1));
    world.Place(new BlockPos(0, 0, 1), Pipe(tierB, 2));
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
  [InlineData(BlockPipe.PlatedTier, BlockPipe.PlatedTier)]
  [InlineData(BlockPipe.CastTier, BlockPipe.CastTier)]
  [InlineData(BlockPipe.PlatedTier, BlockPipe.CastTier)] // both square and flanged
  [InlineData(BlockPipe.CastTier, BlockPipe.PlatedTier)]
  [InlineData(BlockPipe.RolledTier, BlockPipe.RolledTier)]
  public void Pipes_sharing_a_joint_form_one_network(string a, string b) {
    Assert.True(
      OneNetwork(Butted(a, b)),
      $"{a} and {b} pipe share a joint and should have merged"
    );
  }

  #endregion

  #region The joint that does not

  [Theory]
  [InlineData(BlockPipe.RolledTier, BlockPipe.PlatedTier)]
  [InlineData(BlockPipe.PlatedTier, BlockPipe.RolledTier)]
  [InlineData(BlockPipe.RolledTier, BlockPipe.CastTier)]
  [InlineData(BlockPipe.CastTier, BlockPipe.RolledTier)]
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
    var world = Butted(BlockPipe.RolledTier, BlockPipe.PlatedTier);
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
    Assert.Equal(
      BlockPipe.FlangedJoint,
      Pipe(BlockPipe.PlatedTier, 1).JointFamily
    );
    Assert.Equal(
      BlockPipe.FlangedJoint,
      Pipe(BlockPipe.CastTier, 2).JointFamily
    );
    Assert.Equal(
      BlockPipe.WeldedJoint,
      Pipe(BlockPipe.RolledTier, 3).JointFamily
    );
  }

  [Fact]
  public void A_machine_port_is_not_a_pipe_and_is_unaffected() {
    // The rule covers pipe-to-pipe couplings only; an HP run must still reach machine ports.
    BlockPipe rolled = Pipe(BlockPipe.RolledTier, 1);
    var port = TestBlocks.Configure(new Block(), "smex:converter-intake-n", 2);

    Assert.True(rolled.AcceptsNeighbour(port));
  }

  #endregion
}
