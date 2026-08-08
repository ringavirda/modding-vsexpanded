using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The <b>facing overload</b> on the generated code table - <c>WithSide(BlockFacing)</c> /
/// <c>WithOrientation(BlockFacing)</c> - and the one thing a blocktype golden cannot see: that a layout
/// built from it really is orientation-checked.
/// <para>
/// A part drawn on a shaft furnace's charging face has to be placed the right way <em>round</em>, not
/// merely be present, and the facing only reaches the completion check by being <b>in the code string</b>.
/// So "simplify the accessor" is a silent behaviour change: a code that stops carrying its facing emits no
/// <c>multiblockFacings</c> table at all, the cell stops being checked, and every furnace still completes.
/// Asserted through the emitted attribute rather than the builder's internals, because that is the form a
/// furnace actually consumes.
/// </para>
/// <para>
/// This used to pin a hand-written <c>IwexCodes.HopperTall(BlockFacing)</c> wrapper. The generator emits
/// that overload itself now, so the wrapper is gone and what is pinned is the generated one.
/// </para>
/// </summary>
public class IwexCodesTests
{
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
  )
  {
    // This asserted the full word until the `side` group was respelled to single
    // letters - so a `side` and an `orientation` group are now the same spelling and the N4 split has
    // collapsed to one vocabulary. It is kept as a separate case anyway, because it is a different
    // group reaching the same answer, and a regression that respelled only one of the two would
    // otherwise be invisible in the surviving test.
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
  )
  {
    // The same BlockFacing, the other spelling. A tuyere is a network node, so its facing is an
    // `orientation` group carrying letter tokens - and that is the point of the overload: which spelling a
    // part uses is a fact about the block, not about the cell, and a layout author should not have to
    // know it. Before this the caller had to hand-type "n" here and "north" above.
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
  )
  {
    // The one word-spelled facing left in the suite, and the reason the overload still earns its
    // keep. The slag stairs load their orientation from vanilla's `abstract/horizontalorientation`
    // worldproperty, so the states are the game's full words - our respelling does not reach them and
    // must not. Hand-typing the letter every other block now uses names no registered block; two
    // recipe outputs did exactly that and shipped uncraftable stairs.
    string code = IwexBlocks.SlagBrickstairs.WithHorizontalorientation(
      BlockFacing.FromCode(side)
    );

    Assert.Equal($"iwex:slag-brickstairs-up-{side}-*", code);
  }

  #endregion

  #region The negative control

  [Fact]
  public void A_wildcarded_code_is_not_orientation_checked()
  {
    // The control the two theories need: `multiblockFacings` is emitted only for a code that actually
    // pins a facing, so its presence above means something. A layout built from `Any` accepts a part
    // fitted any way round - which is the shipped behaviour for the taps and is why they emit none.
    Assert.Null(FacingsFor(IwexBlocks.HopperTall.Any));
    Assert.Null(FacingsFor(IwexBlocks.FurnaceIrontap.Any));
  }

  #endregion
}
