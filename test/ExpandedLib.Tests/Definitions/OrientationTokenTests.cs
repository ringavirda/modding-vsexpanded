using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Orientation tokens on network node blocks: the multi-direction codes a pipe, passthrough, axle,
/// junction or valve carries instead of a facing (<c>ns</c>, <c>we</c>, <c>ud</c>, <c>nswe</c>,
/// <c>nsud</c>, <c>weud</c>, and the valves' reversed <c>sn</c> / <c>ew</c> / <c>du</c>). None of them
/// is a side word, so the oriented-parts check recognises them through a separate grammar.
/// </summary>
public class OrientationTokenTests {
  #region The grammar is exact, not "a run of direction letters"

  [Theory]
  // Every token a block in the suite declares.
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
  // The grammar is whole axis pairs rather than any run of letters from nsewud: direction letters
  // spell ordinary words, and a loose match would rotate a material segment into a code matching no
  // block. Every case below is a plausible code segment.
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
  public void Word_like_segments_are_not_mistaken_for_orientations(
    string token
  ) => Assert.False(ExOrientation.IsOrientationToken(token));

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
  // A half-vertical junction does move.
  [InlineData("nsud", 90, "weud")]
  [InlineData("weud", 90, "nsud")]
  [InlineData("nsud", 180, "nsud")]
  // Single directions follow the side-word path.
  [InlineData("n", 90, "w")]
  [InlineData("north", 90, "west")]
  public void Tokens_rotate_to_the_variant_the_block_declares(
    string token,
    int angle,
    string expected
  ) =>
    Assert.Equal(expected, ExOrientation.RotateOrientationToken(token, angle));

  [Fact]
  public void Rotation_lands_on_a_declared_variant_after_four_quarter_turns() {
    // A pipe declares only `ns`, `we`, `ud`, so every intermediate turn must land on one of the three
    // and not just the fourth: a token like `sn` would demand a block that does not exist.
    string[] declared = ["ns", "we", "ud"];
    foreach (string start in declared) {
      string t = start;
      for (int i = 0; i < 4; i++) {
        t = ExOrientation.RotateOrientationToken(t, 90);
        Assert.Contains(t, declared);
      }
      Assert.Equal(start, t);
    }
  }

  [Fact]
  public void Only_tokens_that_actually_move_are_treated_as_oriented() {
    // An invariant token is already required exactly by the code matching literally, so recording it
    // as oriented would only add a golden entry.
    Assert.True(ExOrientation.RotatesUnderY("ns"));
    Assert.True(ExOrientation.RotatesUnderY("nsud"));
    Assert.False(ExOrientation.RotatesUnderY("ud"));
    Assert.False(ExOrientation.RotatesUnderY("up"));
    // A four-way junction is rotation-symmetric, so it is invariant too.
    Assert.False(ExOrientation.RotatesUnderY("nswe"));
  }

  [Fact]
  public void The_valves_directed_spelling_canonicalises_and_that_is_the_known_limit() {
    // `BlockValve` and `BlockPressureValve` declare both `ns` and `sn`, where the order encodes input
    // to output; every other node declares only the canonical spelling of an undirected pair. The two
    // grammars are indistinguishable from the token alone, so rotation canonicalises and a valve loses
    // its direction. A layout pinning a directed valve uses LegendAnyFacing and checks it itself.
    Assert.Equal("we", ExOrientation.RotateOrientationToken("sn", 90));
    Assert.Equal("ns", ExOrientation.RotateOrientationToken("ew", 90));
  }

  #endregion

  #region A node code in a layout

  [Theory]
  [InlineData("lpex:pipe-straight-fire-ns", 3)]
  [InlineData("iwex:mpenergy-shaft-we", 2)]
  [InlineData("lpex:pipe-junction-iron-nsud", 3)]
  public void A_node_orientation_is_found_in_the_code(
    string code,
    int segment
  ) =>
    Assert.Equal(
      [segment],
      MultiblockLayoutBuilder.FindOrientationSegments(code)
    );

  [Fact]
  public void A_node_cell_rotates_with_the_structure() {
    var attrs = new Vintagestory.API.Datastructures.JsonObject(
      (Newtonsoft.Json.Linq.JObject)
        ExBlockDef
          .Create("d", "c")
          .MultiblockLayout(s =>
            s.Legend('p', "lpex:pipe-straight-fire-ns").Layer(0, "p")
          )
          .ToJson()["attributes"]!
    );
    MultiblockFacings facings = MultiblockFacings.FromAttributes(attrs);

    var authored = new Vintagestory.API.Common.AssetLocation(
      "lpex:pipe-straight-fire-ns"
    );
    Assert.Equal(
      "lpex:pipe-straight-fire-we",
      facings.Rotate(authored, 90).ToString()
    );
    Assert.Equal(
      "lpex:pipe-straight-fire-ns",
      facings.Rotate(authored, 180).ToString()
    );
  }

  [Fact]
  public void A_vertical_node_emits_no_facings_at_all() {
    // A vertical token does not move under a Y rotation, so no multiblockFacings entry is emitted.
    var attributes = (Newtonsoft.Json.Linq.JObject)
      ExBlockDef
        .Create("d", "c")
        .MultiblockLayout(s =>
          s.Legend('o', "lpex:pipe-outlet-fire-u").Layer(0, "o")
        )
        .ToJson()["attributes"]!;

    Assert.Null(attributes["multiblockFacings"]);
  }

  #endregion
}
