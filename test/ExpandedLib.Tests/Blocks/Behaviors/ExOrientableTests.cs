using ExpandedLib.Blocks.Behaviors;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="BlockBehaviorExOrientable"/> - the family's replacement for vanilla's
/// <c>HorizontalOrientable</c>, and the reason placing a two-group block no longer crashes the client.
/// <para>
/// Until this file existed the behaviour had <b>no direct coverage at all</b>: the only thing pinning it
/// was a golden asserting the string <c>"ExOrientable"</c> appears in a <c>behaviors</c> array, which
/// would still pass if every method here returned garbage.
/// </para>
/// </summary>
public class ExOrientableTests
{
  private static readonly string[] FourSides = ["n", "e", "s", "w"];

  #region Horizontal placement

  [Theory]
  [InlineData("north", "n")]
  [InlineData("east", "e")]
  [InlineData("south", "s")]
  [InlineData("west", "w")]
  public void A_placed_block_wears_the_side_the_player_looked_towards(
    string look,
    string token
  )
  {
    var rig = ExOrientableRig.WithVariants("exlib:probe", "side", FourSides);

    Assert.True(rig.PlaceLooking(look));

    Assert.Equal($"exlib:probe-{token}", rig.PlacedCode);
  }

  [Fact]
  public void A_multi_segment_code_keeps_every_segment_before_the_side()
  {
    // The regression. Vanilla builds the rotated code with CodeWithParts(facing), which keeps only
    // the first dash-segment and replaces everything after it - so `iwex:crafting-designtable-n` became
    // `iwex:crafting-w`, which matches no block, and vanilla then dereferenced the null. Placing the
    // block took the client down, and it hit every furnace part, both hoppers, the mixer, the bunker
    // and both casting blocks. Resolving by variant name is indifferent to how many groups precede it.
    var rig = ExOrientableRig.WithVariants(
      "exlib:crafting",
      "side",
      FourSides,
      fixedGroups: ("kind", "designtable")
    );

    Assert.True(rig.PlaceLooking("west"));

    Assert.Equal("exlib:crafting-designtable-w", rig.PlacedCode);
  }

  [Fact]
  public void A_missing_variant_state_is_refused_and_logged_rather_than_crashing()
  {
    // The block declares only `n`; the player looks west. This is the exact cell where vanilla
    // dereferenced the null - an authoring mistake should cost a refusal and a log line, not a crash
    // report from a player who did nothing wrong.
    var rig = ExOrientableRig.WithVariants("exlib:probe", "side", ["n"]);

    bool placed = rig.PlaceLooking("west");

    Assert.False(placed);
    Assert.Equal("cantplace", rig.FailureCode);
    Assert.Contains("no 'side' state 'w'", Assert.Single(rig.LoggedErrors));
    Assert.Null(rig.PlacedCode);
  }

  #endregion

  #region Fungibility

  [Fact]
  public void Every_facing_drops_and_picks_the_same_canonical_stack()
  {
    // Fungibility is the whole point of a canonical stack: four facings must merge into one inventory
    // slot and satisfy one grid recipe. GetDrops already answered the scheme's first token; OnPickBlock
    // did not, so a middle-clicked west-facing block would not stack with a broken north-facing one.
    var rig = ExOrientableRig.WithVariants("exlib:probe", "side", FourSides);
    rig.PlaceLooking("west");

    Assert.Equal("exlib:probe-n", rig.DropCode);
    Assert.Equal("exlib:probe-n", rig.PickCode);
  }

  [Fact]
  public void The_canonical_stack_is_the_schemes_first_token_not_a_hard_coded_north()
  {
    // An omni block's vocabulary starts at the same `n`, but a network block's does not - so the
    // canonical stack has to read the scheme rather than assume a compass direction exists at all.
    var rig = ExOrientableRig.WithVariants(
      "exlib:axle",
      "orientation",
      ["ns", "we"],
      mode: "network",
      scheme: "Axis"
    );

    Assert.Equal("exlib:axle-ns", rig.DropCode);
    Assert.Equal("exlib:axle-ns", rig.PickCode);
  }

  #endregion

  #region Modes

  [Fact]
  public void A_network_oriented_block_leaves_placement_to_its_own_connector_scan()
  {
    // Not a no-op by accident. A network block goes down wearing whatever the stack carried and the
    // node re-orients it on the next neighbour notification; taking over here would fight that and
    // produce a placement the network immediately overwrites.
    var rig = ExOrientableRig.WithVariants(
      "exlib:axle",
      "orientation",
      ["ns", "we"],
      mode: "network",
      scheme: "Axis"
    );

    Assert.True(rig.PlaceLooking("west"));

    // Handled by the engine's default path, so the rig's own store never saw a SetBlock.
    Assert.Null(rig.PlacedCode);
    Assert.Equal("orientation", rig.VariantKey);
  }

  [Fact]
  public void An_omni_block_clicked_on_a_wall_still_takes_the_horizontal_look()
  {
    var rig = OmniProbe();

    // A horizontal selected face - the player clicked the side of a neighbour - so omni falls through
    // to exactly the same look math a horizontal block uses.
    Assert.True(rig.PlaceLooking("south", selectedFace: "north"));

    Assert.Equal("exlib:probe-s", rig.PlacedCode);
  }

  [Theory]
  [InlineData("up", "u")]
  [InlineData("down", "d")]
  public void An_omni_block_clicked_on_a_floor_or_ceiling_points_that_way(
    string selectedFace,
    string token
  )
  {
    // This branch had never run. `mode: "omni"` is declared by no block in any of the five mods, so
    // TokenFor's vertical arm was unreachable in production and untested here - the state the U0 audit
    // called "declared and unreached". Pinning it now means the first block to opt in gets a route that
    // has at least been executed once.
    var rig = OmniProbe();

    Assert.True(rig.PlaceLooking("south", selectedFace));

    Assert.Equal($"exlib:probe-{token}", rig.PlacedCode);
  }

  private static ExOrientableRig OmniProbe() =>
    ExOrientableRig.WithVariants(
      "exlib:probe",
      "side",
      ["n", "e", "s", "w", "u", "d"],
      mode: "omni"
    );

  #endregion

  #region The shared mechanism

  [Fact]
  public void ApplyOrientation_swaps_a_placed_block_to_the_requested_token()
  {
    // The one mechanism both routes are meant to share. The player route reaches it through
    // TryPlaceBlock; the network route is supposed to call it directly instead of rewriting codes
    // itself - see U10, which is where BlockNetworkNode's four hand-rolled sites go.
    var rig = ExOrientableRig.WithVariants("exlib:probe", "side", FourSides);
    rig.PlaceLooking("north");

    Assert.True(rig.ApplyOrientation("e"));

    Assert.Equal("exlib:probe-e", rig.PlacedCode);
  }

  [Fact]
  public void ApplyOrientation_refuses_a_token_outside_the_declared_scheme()
  {
    var rig = ExOrientableRig.WithVariants("exlib:probe", "side", FourSides);
    rig.PlaceLooking("north");

    // `u` is in FaceAll but not in Face, so a horizontal block must refuse it even though the caller
    // could name a real block if the variant happened to exist.
    Assert.False(rig.ApplyOrientation("u"));

    Assert.Equal("exlib:probe-n", rig.PlacedCode);
  }

  [Fact]
  public void ApplyOrientation_is_a_no_op_when_the_block_already_wears_the_token()
  {
    // Returning false here is load-bearing: the network route calls this on every neighbour
    // notification, and a true would mean "I changed something", re-triggering the walk forever.
    var rig = ExOrientableRig.WithVariants("exlib:probe", "side", FourSides);
    rig.PlaceLooking("north");

    Assert.False(rig.ApplyOrientation("n"));

    Assert.Equal("exlib:probe-n", rig.PlacedCode);
  }

  #endregion
}
