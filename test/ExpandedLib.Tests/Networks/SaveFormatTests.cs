using System.Linq;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using ExpandedLib.Testing.Doubles;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The on-disk format of a network node, against the shape a world saved before membership became a
/// behaviour. Vanilla fans behaviour persistence out over the block entity's own flat tree with no
/// subtree, so a behaviour that wrote anything would land in the namespace the block entity already
/// owns; the block entity therefore stays the only writer and nothing on disk moved. These guards pin
/// that: the exact key set, which keys are conditional, and which of the two writers that reach the
/// shared three runs last.
/// </summary>
public class SaveFormatTests {
  /// <summary>Reaches the pipe's protected persistence hook and the display fields it writes over the
  /// network state, so the two writers can be made to disagree - in production they agree, and
  /// agreement cannot show which of them ran last.</summary>
  private sealed class DivergentPipe : BlockEntityPipe {
    public void SetDisplay(float temperature, string medium, float pressure) {
      Temperature = temperature;
      Medium = medium;
      Pressure = pressure;
    }

    public void SetSavedState(object? state) => _savedNetworkState = state;

    public void WriteNetworkState(ITreeAttribute tree, object? state) =>
      SerializeNetworkState(tree, state);
  }

  /// <summary>Every key a pre-membership world wrote for a pipe holding a live run: the four vanilla
  /// <c>BlockEntity</c> keys, the three <c>BlockEntityNetworkNode</c> ones, and the eight the pipe's
  /// network state serialises to.</summary>
  private static readonly string[] LoadedRunKeys =
  [
    "posx",
    "posy",
    "posz",
    "blockCode",
    "networkType",
    "orientation",
    "possibleOrientations",
    "vol",
    "max",
    "temp",
    "medium",
    "openings",
    "flow",
    "pressure",
    "feedPressure",
  ];

  /// <summary>The keys <c>IsNetworkStateMeaningful</c> gates: absent for a run holding nothing.</summary>
  private static readonly string[] ConditionalKeys =
  [
    "vol",
    "max",
    "openings",
    "flow",
    "feedPressure",
  ];

  /// <summary>The three both writers reach, which is what makes their order observable at all.</summary>
  private static readonly string[] SharedKeys = ["temp", "medium", "pressure"];

  private static readonly PipeNetworkState SavedRun = new() {
    Volume = 123f,
    MaxVolume = 300f,
    Temperature = 88f,
    MediumType = "Steam",
    OpeningsCount = 2,
    FlowRate = 4.5f,
    Pressure = 1.7f,
    FeedPressure = 2.3f,
  };

  private static TestWorld NewPipeWorld() {
    var w = new TestWorld();
    w.RegisterNetwork("pipe", sys => new StubNetwork(sys, "pipe"));
    return w;
  }

  /// <summary>The tree a pre-membership world holds for a pipe carrying a run, written key by key
  /// rather than by round-tripping through today's code - a tree the current writer produced would
  /// agree with the current reader whatever the pair of them became.</summary>
  private static TreeAttribute OldFormatTree(BlockPos pos) {
    var tree = new TreeAttribute();
    tree.SetInt("posx", pos.X);
    tree.SetInt("posy", pos.InternalY);
    tree.SetInt("posz", pos.Z);
    tree.SetString("blockCode", "test:pipe-ns-950");

    tree.SetString("networkType", "pipe");
    tree.SetString("orientation", "ns");
    tree.SetString("possibleOrientations", "[\"ns\",\"we\"]");

    tree.SetFloat("vol", SavedRun.Volume);
    tree.SetFloat("max", SavedRun.MaxVolume);
    tree.SetFloat("temp", SavedRun.Temperature);
    tree.SetString("medium", SavedRun.MediumType);
    tree.SetInt("openings", SavedRun.OpeningsCount);
    tree.SetFloat("flow", SavedRun.FlowRate);
    tree.SetFloat("pressure", SavedRun.Pressure);
    tree.SetFloat("feedPressure", SavedRun.FeedPressure);
    return tree;
  }

  private static string[] KeysOf(ITreeAttribute tree) =>
    tree.Select(kv => kv.Key).OrderBy(k => k).ToArray();

  private static string[] Sorted(params string[][] sets) =>
    sets.SelectMany(s => s).OrderBy(k => k).ToArray();

  #region Loading a pre-membership world

  [Fact]
  public void A_tree_written_before_membership_moved_still_loads_the_node_and_its_run() {
    var w = NewPipeWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = new BlockEntityPipe();
    w.Place(pos, TestNetworkBlock.Create("pipe", "ns", id: 950), be);

    be.FromTreeAttributes(OldFormatTree(pos), w.World);
    w.Initialize(be);

    // The membership answers for the cell, so the walk finds a node here at all.
    Assert.IsAssignableFrom<BEBehaviorNetworkMember>(
      NetworkMembership.Resolve(w.Accessor, pos, "pipe")
    );

    // And the run it was carrying is back on the network, not merely back on the block entity.
    BlockNetwork net = Assert.IsType<StubNetwork>(w.NetworkAt(pos));
    var restored = Assert.IsType<PipeNetworkState>(net.State);
    Assert.Equal(SavedRun.Volume, restored.Volume, 3);
    Assert.Equal(SavedRun.MediumType, restored.MediumType);
    Assert.Equal(SavedRun.FeedPressure, restored.FeedPressure, 3);
  }

  [Fact]
  public void The_wrench_rotation_choices_come_back_from_the_json_string_they_were_saved_as() {
    // Worlds saved before the string-array cutover hold possibleOrientations as one string of
    // serialised JSON. Reading that with the array accessor alone answers empty for every world
    // already on disk, losing the player's rotation choices with nothing to show for it.
    var w = NewPipeWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = new BlockEntityPipe();
    w.Place(pos, TestNetworkBlock.Create("pipe", "ns", id: 951), be);

    be.FromTreeAttributes(OldFormatTree(pos), w.World);

    Assert.Equal(new[] { "ns", "we" }, be.PossibleOrientations);
    Assert.Equal("ns", be.Orientation);
  }

  [Fact]
  public void A_node_loaded_from_the_old_string_is_written_back_as_a_string_array() {
    // The migration is one-way and silent: nothing rewrites a world wholesale, so a pipe converts on
    // its own next save. If it wrote the string back the tree would never leave the old shape and the
    // fallback above would be load-bearing forever.
    var w = NewPipeWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = new BlockEntityPipe();
    w.Place(pos, TestNetworkBlock.Create("pipe", "ns", id: 955), be);
    be.FromTreeAttributes(OldFormatTree(pos), w.World);

    var written = new TreeAttribute();
    be.ToTreeAttributes(written);

    Assert.IsType<StringArrayAttribute>(written["possibleOrientations"]);
    Assert.Equal(
      new[] { "ns", "we" },
      ((StringArrayAttribute)written["possibleOrientations"]).value
    );
  }

  [Fact]
  public void The_rotation_choices_round_trip_through_the_new_shape() {
    var w = NewPipeWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = new BlockEntityPipe();
    w.Place(pos, TestNetworkBlock.Create("pipe", "ns", id: 956), be);
    be.PossibleOrientations = ["ns", "we", "ud"];

    var written = new TreeAttribute();
    be.ToTreeAttributes(written);

    var reloaded = new BlockEntityPipe();
    w.Place(
      new BlockPos(1, 0, 0),
      TestNetworkBlock.Create("pipe", "ns", id: 957),
      reloaded
    );
    reloaded.FromTreeAttributes(written, w.World);

    Assert.Equal(new[] { "ns", "we", "ud" }, reloaded.PossibleOrientations);
  }

  #endregion

  #region The key set

  [Fact]
  public void A_loaded_node_writes_back_exactly_the_keys_it_read() {
    var w = NewPipeWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = new BlockEntityPipe();
    w.Place(pos, TestNetworkBlock.Create("pipe", "ns", id: 952), be);
    be.FromTreeAttributes(OldFormatTree(pos), w.World);
    w.Initialize(be);

    // The premise: the node really does carry a membership behaviour, and vanilla really does fan
    // ToTreeAttributes out over its behaviours. Without both, this asserts nothing about behaviour
    // persistence staying silent.
    Assert.NotEmpty(NetworkMembership.MembersOf(be));

    var written = new TreeAttribute();
    be.ToTreeAttributes(written);

    Assert.Equal(Sorted(LoadedRunKeys), KeysOf(written));
  }

  [Fact]
  public void An_empty_run_writes_neither_more_nor_fewer_of_the_conditional_keys() {
    // IsNetworkStateMeaningful gates on Volume > 0 || FlowRate > 0, so five of the eight state keys
    // are absent for a run holding nothing. temp, medium and pressure survive it because the pipe
    // writes them itself for the client display - which is the other half of the order guard below.
    var w = NewPipeWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = new BlockEntityPipe();
    w.Place(pos, TestNetworkBlock.Create("pipe", "ns", id: 953), be);
    w.Initialize(be);

    be.OnNetworkUpdate(new PipeNetworkState { Volume = 0f, FlowRate = 0f });

    var written = new TreeAttribute();
    be.ToTreeAttributes(written);

    Assert.Equal(
      Sorted(LoadedRunKeys.Except(ConditionalKeys).ToArray()),
      KeysOf(written)
    );
  }

  #endregion

  #region The write order

  [Fact]
  public void The_network_state_writes_the_three_keys_the_pipe_writes_again() {
    // The premise the order guard rests on. Should the pipe's display keys ever stop colliding with
    // the serialised state's, order would no longer be observable and the guard below would pass on
    // nothing.
    var be = new DivergentPipe();
    var fromState = new TreeAttribute();

    be.WriteNetworkState(fromState, SavedRun);

    foreach (string key in SharedKeys)
      Assert.Contains(key, KeysOf(fromState));
  }

  [Fact]
  public void The_pipes_display_fields_are_written_after_the_network_state() {
    // BlockEntityPipe.ToTreeAttributes rewrites temp, medium and pressure after base has already run
    // SerializeNetworkState, so the display values are what reaches disk. Swapping the two writers
    // changes what is persisted with nothing to show for it, and a client reads its gauge off these.
    var w = NewPipeWorld();
    var pos = new BlockPos(0, 0, 0);
    var be = new DivergentPipe();
    w.Place(pos, TestNetworkBlock.Create("pipe", "ns", id: 954), be);
    be.SetSavedState(SavedRun);
    be.SetDisplay(11f, "Water", 9f);

    var written = new TreeAttribute();
    be.ToTreeAttributes(written);

    Assert.Equal(11f, written.GetFloat("temp"), 3);
    Assert.Equal("Water", written.GetString("medium"));
    Assert.Equal(9f, written.GetFloat("pressure"), 3);

    // The state's own five keys are untouched by the second writer, so this is an overwrite of three
    // rather than a wholesale rewrite.
    Assert.Equal(SavedRun.Volume, written.GetFloat("vol"), 3);
    Assert.Equal(SavedRun.FeedPressure, written.GetFloat("feedPressure"), 3);
  }

  #endregion
}
