using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The facing overloads on the generated code table - <c>WithSide(BlockFacing)</c> and
/// <c>WithOrientation(BlockFacing)</c> - and the one thing a blocktype golden cannot see: that a layout
/// built from such a code really is orientation-checked. A facing reaches the completion check only by
/// being in the code string, so a code that stops carrying it emits no <c>multiblockFacings</c> table,
/// the cell stops being checked, and every furnace still completes. Asserted through the emitted
/// attribute rather than the builder's internals, because that is the form a furnace consumes.
/// </summary>
public class IwexCodesTests {
  /// <summary>The emitted <c>multiblockFacings</c> for a one-cell layout whose only legend is
  /// <paramref name="code"/> - null when the layout marks nothing orientation-checked.</summary>
  private static JToken? FacingsFor(string code) =>
    (
      (JObject)
        ExBlockDef
          .Create("iwex", "codeprobe")
          .MultiblockLayout(s => s.Legend('H', code).Layer(0, "H"))
          .ToJson()["attributes"]!
    )["multiblockFacings"];

  #region The two spellings of a facing

  [Theory]
  [InlineData("north", "n")] // the cold blast furnace
  [InlineData("west", "w")] // the cupola
  [InlineData("east", "e")]
  [InlineData("south", "s")]
  public void A_side_group_takes_a_facing_and_keeps_the_letter(
    string side,
    string letter
  ) {
    // A `side` group spells facings as single letters, the same as an `orientation` group. Kept as a
    // separate case because a regression respelling only one of the two would otherwise be invisible.
    string code = IwexBlocks.HopperTall.WithSide(BlockFacing.FromCode(side));

    Assert.Equal($"iwex:hopper-tall-{letter}", code);
    Assert.NotNull(FacingsFor(code));
  }

  [Theory]
  [InlineData("north", "n")]
  [InlineData("south", "s")]
  [InlineData("east", "e")]
  [InlineData("west", "w")]
  public void An_orientation_group_takes_the_same_facing_and_keeps_the_letter(
    string side,
    string letter
  ) {
    // The same BlockFacing, the other spelling: a tuyere is a network node, so its facing lives in an
    // `orientation` group carrying letter tokens. Which spelling a part uses is a fact about the block,
    // not about the cell, so the overload keeps it off the layout author.
    string code = IwexBlocks.FurnaceTuyere.WithOrientation(
      BlockFacing.FromCode(side)
    );

    Assert.Equal($"iwex:furnace-tuyere-{letter}", code);
    Assert.NotNull(FacingsFor(code));
  }

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void A_vanilla_sourced_group_takes_the_same_facing_and_keeps_the_full_word(
    string side
  ) {
    // The one word-spelled facing left in the suite. The slag stairs load their orientation from
    // vanilla's `abstract/horizontalorientation` worldproperty, so their states are the game's full
    // words; a hand-typed single letter here names no registered block.
    string code = IwexBlocks.SlagBrickstairs.WithHorizontalorientation(
      BlockFacing.FromCode(side)
    );

    Assert.Equal($"iwex:slag-brickstairs-up-{side}-*", code);
  }

  #endregion

  #region The negative control

  [Fact]
  public void A_wildcarded_code_is_not_orientation_checked() {
    // The control the two theories need: `multiblockFacings` is emitted only for a code that pins a
    // facing. A layout built from `Any` accepts a part fitted any way round, which is what the taps do.
    Assert.Null(FacingsFor(IwexBlocks.HopperTall.Any));
    Assert.Null(FacingsFor(IwexBlocks.FurnaceIrontap.Any));
  }

  #endregion
}
