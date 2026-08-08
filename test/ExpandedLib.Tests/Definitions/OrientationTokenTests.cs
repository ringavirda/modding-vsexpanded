using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Orientation tokens on <b>network node</b> blocks - the multi-direction codes a pipe, passthrough,
/// axle, junction or valve carries instead of a facing (<c>ns</c>, <c>we</c>, <c>ud</c>, <c>nswe</c>,
/// <c>nsud</c>, <c>weud</c>, and the valves' reversed <c>sn</c> / <c>ew</c> / <c>du</c>).
/// <para>
/// <b>None of these is a side word</b>, so before this existed a layout pinning one was invisible to
/// the oriented-parts check: it would silently not rotate, and be wrong two times out of four. Nothing
/// shipped hits that today - every pipe legend is a wildcard or the vertical <c>-u</c> - which is
/// exactly why it needs a test rather than a bug report.
/// </para>
/// </summary>
public class OrientationTokenTests
{
  #region The grammar is exact, not "a run of direction letters"

  [Theory]
  // Every token any block in the suite actually declares.
  [InlineData("n")]
  [InlineData("s")]
  [InlineData("e")]
  [InlineData("w")]
  [InlineData("u")]
  [InlineData("d")]
  [InlineData("north")]
  [InlineData("down")]
  [InlineData("ns")]
  [InlineData("we")]
  [InlineData("ud")]
  [InlineData("sn")] // BlockValve / BlockPressureValve: the reversed, directed spellings
  [InlineData("ew")]
  [InlineData("du")]
  [InlineData("nswe")]
  [InlineData("nsud")]
  [InlineData("weud")]
  public void Declared_tokens_are_recognised(string token) =>
    Assert.True(ExOrientation.IsOrientationToken(token));

  [Theory]
  // The reason the grammar is whole-axis-pairs rather than "letters from nsewud": direction
  // letters spell ordinary words, and a loose test would orientation-check a material segment,
  // rotate it, and produce a code matching no block - a cell that can never be satisfied, with no
  // error anywhere. Every one of these is a plausible code segment.
  [InlineData("sun")]
  [InlineData("wend")]
  [InlineData("used")]
  [InlineData("news")]
  [InlineData("dune")]
  [InlineData("den")]
  // An axis may not repeat: `nsns` is not a connector set.
  [InlineData("nsns")]
  // Real segments from this repo's own codes.
  [InlineData("fire")]
  [InlineData("good")]
  [InlineData("tier3")]
  [InlineData("straight")]
  [InlineData("")]
  public void Word_like_segments_are_not_mistaken_for_orientations(string token) =>
    Assert.False(ExOrientation.IsOrientationToken(token));

  #endregion

  #region Rotation

  [Theory]
  // A single axis swaps with the other horizontal one.
  [InlineData("ns", 90, "we")]
  [InlineData("ns", 180, "ns")]
  [InlineData("ns", 270, "we")]
  [InlineData("we", 90, "ns")]
  // Vertical is untouched by a Y rotation.
  [InlineData("ud", 90, "ud")]
  [InlineData("up", 90, "up")]
  [InlineData("u", 90, "u")]
  // A cross junction is symmetric, so it maps to itself.
  [InlineData("nswe", 90, "nswe")]
  // …but a half-vertical junction does move, which is the case worth having.
  [InlineData("nsud", 90, "weud")]
  [InlineData("weud", 90, "nsud")]
  [InlineData("nsud", 180, "nsud")]
  // Single directions behave exactly as the side-word path always did.
  [InlineData("n", 90, "w")]
  [InlineData("north", 90, "west")]
  public void Tokens_rotate_to_the_variant_the_block_declares(
    string token,
    int angle,
    string expected
  ) => Assert.Equal(expected, ExOrientation.RotateOrientationToken(token, angle));

  [Fact]
  public void Rotation_lands_on_a_declared_variant_after_four_quarter_turns()
  {
    // A pipe declares only `ns`, `we`, `ud`. If rotation could produce `sn`, a turned structure would
    // demand a block that does not exist - so every intermediate must be one of the three, not merely
    // the fourth turn.
    string[] declared = ["ns", "we", "ud"];
    foreach (string start in declared)
    {
      string t = start;
      for (int i = 0; i < 4; i++)
      {
        t = ExOrientation.RotateOrientationToken(t, 90);
        Assert.Contains(t, declared);
      }
      Assert.Equal(start, t);
    }
  }

  [Fact]
  public void Only_tokens_that_actually_move_are_treated_as_oriented()
  {
    // Recording an invariant token would buy nothing - it is already required exactly, by the code
    // matching literally - and would put a pointless entry in every golden.
    Assert.True(ExOrientation.RotatesUnderY("ns"));
    Assert.True(ExOrientation.RotatesUnderY("nsud"));
    Assert.False(ExOrientation.RotatesUnderY("ud"));
    Assert.False(ExOrientation.RotatesUnderY("up"));
    // A four-way junction is genuinely rotation-symmetric, so it is invariant too.
    Assert.False(ExOrientation.RotatesUnderY("nswe"));
  }

  [Fact]
  public void The_valves_directed_spelling_canonicalises_and_that_is_the_known_limit()
  {
    // `BlockValve` and `BlockPressureValve` declare both `ns` and `sn`, where the order encodes
    // input → output. Every other node treats a pair as an undirected axis and declares only the
    // canonical spelling. The two grammars are indistinguishable from the string alone - `ns` is a
    // legal member of both - so rotation canonicalises, which is right for pipes, passthroughs, axles,
    // junctions, flywheels and mills, and loses a valve's direction.
    //
    // Pinned rather than fixed: a layout that must pin a directed valve has to use LegendAnyFacing and
    // check the direction itself. If this ever starts preserving direction, the doc-comment on
    // RotateOrientationToken has to change with it.
    Assert.Equal("we", ExOrientation.RotateOrientationToken("sn", 90));
    Assert.Equal("ns", ExOrientation.RotateOrientationToken("ew", 90));
  }

  #endregion

  #region A node code in a layout

  [Theory]
  [InlineData("lpex:pipe-straight-fire-ns", 3)]
  [InlineData("iwex:mpenergy-shaft-we", 2)]
  [InlineData("lpex:pipe-junction-iron-nsud", 3)]
  public void A_node_orientation_is_found_in_the_code(string code, int segment) =>
    Assert.Equal(
      [segment],
      MultiblockLayoutBuilder.FindOrientationSegments(code)
    );

  [Fact]
  public void A_node_cell_rotates_with_the_structure()
  {
    var attrs = new Vintagestory.API.Datastructures.JsonObject(
      (Newtonsoft.Json.Linq.JObject)
        ExBlockDef
          .Create("d", "c")
          .MultiblockLayout(s => s.Legend('p', "lpex:pipe-straight-fire-ns").Layer(0, "p"))
          .ToJson()["attributes"]!
    );
    MultiblockFacings facings = MultiblockFacings.FromAttributes(attrs);

    var authored = new Vintagestory.API.Common.AssetLocation("lpex:pipe-straight-fire-ns");
    Assert.Equal("lpex:pipe-straight-fire-we", facings.Rotate(authored, 90).ToString());
    Assert.Equal("lpex:pipe-straight-fire-ns", facings.Rotate(authored, 180).ToString());
  }

  [Fact]
  public void A_vertical_node_emits_no_facings_at_all()
  {
    // `lpex:pipe-outlet-fire-u` is the one node code a shipped layout actually pins, and it must stay
    // un-oriented: emitting an entry for it would churn two boiler goldens for no behaviour change.
    var attributes = (Newtonsoft.Json.Linq.JObject)
      ExBlockDef
        .Create("d", "c")
        .MultiblockLayout(s => s.Legend('o', "lpex:pipe-outlet-fire-u").Layer(0, "o"))
        .ToJson()["attributes"]!;

    Assert.Null(attributes["multiblockFacings"]);
  }

  #endregion
}
