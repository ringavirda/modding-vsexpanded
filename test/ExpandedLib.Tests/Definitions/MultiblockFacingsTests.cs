using System;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Orientation-checked multiblock parts. Vanilla rotates a structure's offsets but never its codes, so a
/// layout could only ever demand "a slab", not "an <em>up</em>-facing slab turned with the building" - which
/// is how a completed furnace could still show gaps in its walls. These pin the two halves of the fix: the
/// layout builder recognising which legends carry a facing, and the runtime rotating that facing.
/// <para>
/// The consequence worth pinning hardest is the <b>negative</b> one: layouts that declare no oriented part
/// must emit nothing at all and behave exactly as before, because every structure shipped so far is one of
/// those and none of them may change behaviour.
/// </para>
/// </summary>
public class MultiblockFacingsTests
{
  private static JObject Def(Action<MultiblockLayoutBuilder> configure) =>
    (JObject)ExBlockDef.Create("d", "c").MultiblockLayout(configure).ToJson()["attributes"]!;

  private static JToken? Facings(Action<MultiblockLayoutBuilder> configure) =>
    Def(configure)["multiblockFacings"];

  #region Which legends are oriented

  [Theory]
  // The two forms real block codes use: trailing orientation, and orientation mid-code.
  [InlineData("game:cokeovendoor-closed-north", 2)]
  [InlineData("game:brickslabs-fire-south-free", 2)]
  [InlineData("game:brickslabs-fire-east-free", 2)]
  [InlineData("mod:thing-w", 1)]
  public void A_whole_side_segment_is_recognised(string code, int segment) =>
    Assert.Equal([segment], MultiblockLayoutBuilder.FindOrientationSegments(code));

  [Theory]
  // Vertical facings do not move under a Y rotation, so they are deliberately not oriented parts.
  [InlineData("game:brickslabs-fire-up-free")]
  [InlineData("game:brickslabs-fire-down-free")]
  [InlineData("lpex:pipe-outlet-fire-u")]
  // No facing at all - the overwhelming majority of legends.
  [InlineData("exlib:structurefiller")]
  [InlineData("game:refractorybricks-good-tier*")]
  [InlineData("iwex:furnace-puddlingcore-*")]
  // A side word must be a whole segment: these must not be mistaken for facings.
  [InlineData("mod:westward-thing")]
  [InlineData("mod:pipe-northgate")]
  public void Non_facing_codes_are_not_oriented(string code) =>
    Assert.Empty(MultiblockLayoutBuilder.FindOrientationSegments(code));

  [Fact]
  public void A_layout_with_no_oriented_part_emits_no_facings_attribute()
  {
    // Every structure shipped before this feature is of this shape. If it started emitting the
    // attribute, every one of their goldens would churn and their completion checks would change.
    Assert.Null(
      Facings(s =>
        s.Legend('#', "game:refractorybricks-good-tier*")
          .Legend('u', "game:brickslabs-fire-up-free")
          .Layer(0, "# u")
      )
    );
  }

  [Fact]
  public void LegendAnyFacing_opts_a_code_out_of_rotation()
  {
    Assert.Null(
      Facings(s => s.LegendAnyFacing('d', "game:cokeovendoor-closed-north").Layer(0, "d"))
    );
  }

  [Fact]
  public void An_oriented_legend_records_its_facing_segment()
  {
    JToken? f = Facings(s =>
      s.Legend('#', "game:claybricks-good-fire")
        .Legend('i', "game:brickslabs-fire-south-free")
        .Layer(0, "# i")
    );
    Assert.NotNull(f);
    // Keyed by code, not block number, so it reads against blockNumbers and survives renumbering.
    // An array of segment indices - one code can carry several orientation groups (stairs spell a
    // half and a facing), and a one-element array is the common case rather than the only shape.
    Assert.Equal([2], f!["game:brickslabs-fire-south-free"]!.Values<int>());
    Assert.Null(f["game:claybricks-good-fire"]);
  }

  #endregion

  #region Rotation

  [Theory]
  // Authored north reads as the side whose AngleFromSide equals the structure angle - the same
  // convention RotateFacing and RotateOffset already share.
  [InlineData("north", 0, "north")]
  [InlineData("north", 90, "west")]
  [InlineData("north", 180, "south")]
  [InlineData("north", 270, "east")]
  // ...and a part authored some other way round carries its offset from that.
  [InlineData("south", 90, "east")]
  [InlineData("south", 270, "west")]
  [InlineData("east", 90, "north")]
  [InlineData("west", 180, "east")]
  public void Side_words_rotate_with_the_structure(string side, int angle, string want) =>
    Assert.Equal(want, ExOrientation.RotateSideWord(side, angle));

  [Fact]
  public void Letter_form_survives_rotation_as_a_letter()
  {
    // `orientation` variants use letters and `side` variants use words; swapping the form would
    // build a code no block has, which fails as "this structure never completes".
    Assert.Equal("w", ExOrientation.RotateSideWord("n", 90));
    Assert.Equal("west", ExOrientation.RotateSideWord("north", 90));
  }

  [Theory]
  [InlineData("up")]
  [InlineData("down")]
  [InlineData("fire")]
  public void Non_horizontal_words_are_returned_unchanged(string word) =>
    Assert.Equal(word, ExOrientation.RotateSideWord(word, 90));

  [Fact]
  public void Rotate_swaps_only_the_facing_segment()
  {
    MultiblockFacings f = FacingsFor(s =>
      s.Legend('i', "game:brickslabs-fire-south-free").Layer(0, "i")
    );

    var code = new AssetLocation("game", "brickslabs-fire-south-free");
    Assert.Equal("game:brickslabs-fire-east-free", f.Rotate(code, 90).ToString());
    Assert.Equal("game:brickslabs-fire-south-free", f.Rotate(code, 0).ToString());
    Assert.Equal("game:brickslabs-fire-north-free", f.Rotate(code, 180).ToString());
  }

  [Fact]
  public void Rotate_leaves_codes_the_layout_did_not_mark_alone()
  {
    MultiblockFacings f = FacingsFor(s =>
      s.Legend('i', "game:brickslabs-fire-south-free").Layer(0, "i")
    );

    // A brick has no facing; an up-facing slab has one that cannot turn. Both must pass through, so
    // the completion walk can route every cell through Rotate without branching.
    var brick = new AssetLocation("game", "claybricks-good-fire");
    var upSlab = new AssetLocation("game", "brickslabs-fire-up-free");
    Assert.Equal(brick.ToString(), f.Rotate(brick, 90).ToString());
    Assert.Equal(upSlab.ToString(), f.Rotate(upSlab, 90).ToString());
  }

  [Fact]
  public void An_empty_facing_table_is_the_identity()
  {
    var code = new AssetLocation("game", "brickslabs-fire-south-free");
    Assert.True(MultiblockFacings.None.IsEmpty);
    Assert.Equal(code.ToString(), MultiblockFacings.None.Rotate(code, 90).ToString());
  }

  [Fact]
  public void A_full_turn_returns_the_authored_code()
  {
    MultiblockFacings f = FacingsFor(s =>
      s.Legend('d', "game:cokeovendoor-closed-north").Layer(0, "d")
    );
    var code = new AssetLocation("game", "cokeovendoor-closed-north");
    Assert.Equal(code.ToString(), f.Rotate(code, 360).ToString());
  }

  [Fact]
  public void A_segment_index_past_the_end_falls_back_to_the_authored_code()
  {
    // Only reachable via a hand-edited attribute. Building a nonsense code there would present as
    // "the structure never completes", which is far harder to diagnose than simply not rotating.
    Assert.Null(MultiblockFacings.RotateSegments("brickslabs-fire-south-free", [9], 90));
    Assert.Null(MultiblockFacings.RotateSegments("brickslabs-fire-south-free", [1], 90));
    // All-or-nothing: one bad index discards the whole rotation rather than half-applying it.
    Assert.Null(MultiblockFacings.RotateSegments("brickslabs-fire-south-free", [2, 9], 90));
  }

  #endregion

  private static MultiblockFacings FacingsFor(Action<MultiblockLayoutBuilder> configure)
  {
    var attrs = new Vintagestory.API.Datastructures.JsonObject(Def(configure));
    return MultiblockFacings.FromAttributes(attrs);
  }
}
