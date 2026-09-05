using ExpandedLib.Blocks.Behaviors;
using ExpandedLib.Helpers;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="BlockBehaviorExOrientable"/>, the family's replacement for vanilla's
/// <c>HorizontalOrientable</c>: horizontal and omni placement, the canonical drop and pick stack,
/// and <c>ApplyOrientation</c>.
/// </summary>
public class ExOrientableTests {
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
  ) {
    var rig = ExOrientableRig.WithVariants("exlib:probe", "side", FourSides);

    Assert.True(rig.PlaceLooking(look));

    Assert.Equal($"exlib:probe-{token}", rig.PlacedCode);
  }

  [Fact]
  public void A_multi_segment_code_keeps_every_segment_before_the_side() {
    // Vanilla builds the rotated code with CodeWithParts(facing), which keeps only the first
    // dash-segment and replaces everything after it, so a code with a group before the side resolves
    // to no block. Resolving by variant name is indifferent to how many groups precede it.
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
  public void A_missing_variant_state_is_refused_and_logged_rather_than_crashing() {
    // The block declares only `n` while the player looks west. A missing variant state resolves to
    // no block, so the placement must be refused and logged rather than dereferencing the null.
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
  public void Every_facing_drops_and_picks_the_same_canonical_stack() {
    // A canonical stack is what lets four facings merge into one inventory slot and satisfy one grid
    // recipe, so GetDrops and OnPickBlock must both answer the scheme's first token.
    var rig = ExOrientableRig.WithVariants("exlib:probe", "side", FourSides);
    rig.PlaceLooking("west");

    Assert.Equal("exlib:probe-n", rig.DropCode);
    Assert.Equal("exlib:probe-n", rig.PickCode);
  }

  [Fact]
  public void The_canonical_stack_is_the_schemes_first_token_not_a_hard_coded_north() {
    // A network block's scheme does not start at `n`, so the canonical stack reads the scheme rather
    // than assuming a compass direction exists at all.
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
  public void A_network_oriented_block_leaves_placement_to_its_own_connector_scan() {
    // A network block goes down wearing whatever the stack carried, and the node re-orients it on
    // the next neighbour notification, so taking placement over here would be overwritten anyway.
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
  public void An_omni_block_clicked_on_a_wall_still_takes_the_horizontal_look() {
    var rig = OmniProbe();

    // A horizontal selected face - the player clicked the side of a neighbour - so omni falls
    // through to the same look math a horizontal block uses.
    Assert.True(rig.PlaceLooking("south", selectedFace: "north"));

    Assert.Equal("exlib:probe-s", rig.PlacedCode);
  }

  [Theory]
  [InlineData("up", "u")]
  [InlineData("down", "d")]
  public void An_omni_block_clicked_on_a_floor_or_ceiling_points_that_way(
    string selectedFace,
    string token
  ) {
    // No block in either mod declares `mode: "omni"`, so TokenFor's vertical arm has no production
    // caller and this is its only coverage.
    var rig = OmniProbe();

    Assert.True(rig.PlaceLooking("south", selectedFace));

    Assert.Equal($"exlib:probe-{token}", rig.PlacedCode);
  }

  [Fact]
  public void A_network_block_that_names_no_scheme_falls_back_silently() {
    // The fallback is deliberate - one misspelling in a JSON asset must not take a world down - but it
    // is indistinguishable at runtime from a correct Axis block, so what reached it is recorded.
    var rig = ExOrientableRig.WithVariants(
      "exlib:axle",
      "orientation",
      ["ns", "we", "ud"],
      mode: "network"
    );

    Assert.Equal(ExOrientations.Axis.Name, rig.Scheme.Name);
    Assert.Null(rig.UnresolvedScheme);
  }

  [Fact]
  public void A_network_block_that_names_a_scheme_wrongly_records_the_name() {
    // Axis holds none of a bend's twelve tokens, so this block would refuse every orientation its own
    // neighbours ask for and simply stop moving. A code-first def cannot reach here - NetworkOriented
    // writes the name it resolved off the block's own states - so this is the JSON-authored case.
    var rig = ExOrientableRig.WithVariants(
      "exlib:bend",
      "orientation",
      ["nw", "se", "en", "ws"],
      mode: "network",
      scheme: "CanalBnd"
    );

    Assert.Equal(ExOrientations.Axis.Name, rig.Scheme.Name);
    Assert.Equal("CanalBnd", rig.UnresolvedScheme);
    Assert.False(rig.ApplyOrientation("se"));
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
  public void ApplyOrientation_swaps_a_placed_block_to_the_requested_token() {
    // The mechanism both routes share: the player route reaches it through TryPlaceBlock, the
    // network route calls it directly.
    var rig = ExOrientableRig.WithVariants("exlib:probe", "side", FourSides);
    rig.PlaceLooking("north");

    Assert.True(rig.ApplyOrientation("e"));

    Assert.Equal("exlib:probe-e", rig.PlacedCode);
  }

  [Fact]
  public void ApplyOrientation_refuses_a_token_outside_the_declared_scheme() {
    var rig = ExOrientableRig.WithVariants("exlib:probe", "side", FourSides);
    rig.PlaceLooking("north");

    // `u` is in FaceAll but not in Face, so a horizontal block must refuse it even when a block of
    // that code would exist.
    Assert.False(rig.ApplyOrientation("u"));

    Assert.Equal("exlib:probe-n", rig.PlacedCode);
  }

  [Fact]
  public void ApplyOrientation_is_a_no_op_when_the_block_already_wears_the_token() {
    // Returning false is load-bearing: the network route calls this on every neighbour notification,
    // and true would signal a change, re-triggering the walk indefinitely.
    var rig = ExOrientableRig.WithVariants("exlib:probe", "side", FourSides);
    rig.PlaceLooking("north");

    Assert.False(rig.ApplyOrientation("n"));

    Assert.Equal("exlib:probe-n", rig.PlacedCode);
  }

  #endregion
}
