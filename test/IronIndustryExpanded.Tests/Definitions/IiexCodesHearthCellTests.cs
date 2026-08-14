using ExpandedLib.Definitions;
using Vintagestory.API.Util;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The shaft furnace's interior cell codes. They live in <c>IiexCodes</c> rather than exlib's
/// catalogue because the blocks that make the alternations correct - the charge pile, the frozen pool -
/// are iiex's own, and this test lives here because exlib's tests cannot reference iiex. smex's hot
/// furnace draws the same codes from downstream.
/// </summary>
public class IiexCodesHearthCellTests {
  [Theory]
  // The hearth cell is a superset of ChargeShaft: everything ChargeShaft admits, it admits too.
  [InlineData("air")]
  [InlineData("coalpile")]
  [InlineData("furnace-chargepile")]
  // ...plus the block the furnace stands its own metal in. A legend that refuses a block the machine
  // places there reads incomplete on the next monitor tick, and an incomplete shaft furnace extinguishes.
  [InlineData("hearthmetal-pigiron")]
  [InlineData("hearthmetal-castiron")]
  public void The_hearth_cell_admits_the_pool(string path) {
    Assert.True(
      WildcardUtil.Match(
        new Vintagestory.API.Common.AssetLocation(IiexCodes.HearthCell),
        new Vintagestory.API.Common.AssetLocation("iiex:" + path)
      )
    );
  }

  /// <summary>
  /// The pool member is written <c>hearthmetal-.*</c> because inside an alternation the body is a
  /// regex. A later metal needs no edit here, and a lookalike that only starts with the word is
  /// refused. Asserted through <see cref="WildcardUtil.Match"/> rather than against the string itself.
  /// </summary>
  [Theory]
  [InlineData("hearthmetal-pigiron", true)]
  [InlineData("hearthmetal-castiron", true)]
  // A metal that does not exist yet still matches.
  [InlineData("hearthmetal-somefuturealloy", true)]
  // ...but a bare-prefix lookalike must not.
  [InlineData("hearthmetalother", false)]
  [InlineData("hearthmetalslag", false)]
  // ...and neither must the bare word, which is not a code this block ever expands to.
  [InlineData("hearthmetal", false)]
  public void The_pool_member_matches_on_the_separator_not_on_the_prefix(
    string path,
    bool admitted
  ) =>
    Assert.Equal(
      admitted,
      WildcardUtil.Match(
        new Vintagestory.API.Common.AssetLocation(IiexCodes.HearthCell),
        new Vintagestory.API.Common.AssetLocation("iiex:" + path)
      )
    );

  /// <summary>
  /// Both spellings pinned in one table. Inside <c>@(…)</c> the body is a regular expression, so
  /// <c>hearthmetal-*</c> means "hearthmetal followed by zero or more dashes": it admits the bare word,
  /// which no definition produces, and refuses both metals that do exist. Outside an alternation the
  /// same <c>*</c> is an ordinary glob. The wrong spelling is asserted alongside the right one because
  /// it does not crash - a hearth legend refusing the block the furnace places reads incomplete on the
  /// next monitor tick, which extinguishes the furnace.
  /// </summary>
  [Theory]
  // The glob spelling, inside an alternation: inverted.
  [InlineData("*:@(air|hearthmetal-*)", "hearthmetal-pigiron", false)]
  [InlineData("*:@(air|hearthmetal-*)", "hearthmetal-castiron", false)]
  [InlineData("*:@(air|hearthmetal-*)", "hearthmetal", true)]
  // The regex spelling, inside an alternation: what the constant ships.
  [InlineData("*:@(air|hearthmetal-.*)", "hearthmetal-pigiron", true)]
  [InlineData("*:@(air|hearthmetal-.*)", "hearthmetal-castiron", true)]
  [InlineData("*:@(air|hearthmetal-.*)", "hearthmetal", false)]
  [InlineData("*:@(air|hearthmetal-.*)", "hearthmetalother", false)]
  // The same glob outside an alternation, where `*` is an ordinary glob.
  [InlineData("*:hearthmetal-*", "hearthmetal-pigiron", true)]
  [InlineData("*:hearthmetal-*", "hearthmetal", false)]
  public void A_star_inside_an_alternation_is_a_regex_and_a_star_outside_one_is_a_glob(
    string wildcard,
    string path,
    bool matches
  ) =>
    Assert.Equal(
      matches,
      WildcardUtil.Match(
        new Vintagestory.API.Common.AssetLocation(wildcard),
        new Vintagestory.API.Common.AssetLocation("iiex:" + path)
      )
    );

  [Fact]
  public void The_hearth_cell_keeps_the_domain_wildcard_that_makes_it_work() {
    // `@(…)` is a regex over the path only, so without the leading `*:` the alternation is implicitly
    // `game:` and admits none of iiex's own blocks - neither the charge pile nor the frozen pool.
    Assert.StartsWith("*:", IiexCodes.HearthCell);
    Assert.StartsWith("*:", IiexCodes.ChargeShaft);
  }

  #region The rig's copies of these strings

  /// <summary>
  /// <see cref="FurnaceLayoutRig.ShaftGlyph"/> and <see cref="FurnaceLayoutRig.HearthGlyph"/> are
  /// string literals equal to <see cref="IiexCodes.ChargeShaft"/> and
  /// <see cref="IiexCodes.HearthCell"/>. Being literals rather than references, a change to either
  /// constant compiles cleanly and leaves the rig asserting the old spelling, so the equality is pinned
  /// here. The duplication is intentional: glyphs derived from the constants would move with them and
  /// could not catch a wrong constant.
  /// </summary>
  [Fact]
  public void The_layout_rigs_glyphs_still_match_the_shipped_constants() {
    Assert.Equal(IiexCodes.ChargeShaft, FurnaceLayoutRig.ShaftGlyph);
    Assert.Equal(IiexCodes.HearthCell, FurnaceLayoutRig.HearthGlyph);
  }

  #endregion

  #region The domain half of an alternation

  /// <summary>
  /// The behaviour both constants are built on. <see cref="WildcardUtil.Match"/> compares domain and
  /// path separately: an <c>@(…)</c> alternation is a regex over the path and says nothing about the
  /// domain, so the domain is whatever the <see cref="Vintagestory.API.Common.AssetLocation"/>
  /// constructor supplied, which is <c>game</c> when the string carries no <c>:</c>. A bare alternation
  /// is therefore <c>game:@(…)</c>, matches every vanilla member and no modded one, and in a multiblock
  /// legend reads as a structure that never completes.
  /// </summary>
  [Theory]
  // A domain-wildcarded alternation matches the same path in any domain.
  [InlineData(
    "*:@(air|coalpile|furnace-chargepile)",
    "iiex:furnace-chargepile",
    true
  )]
  [InlineData("*:@(air|coalpile|furnace-chargepile)", "game:air", true)]
  [InlineData("*:@(air|coalpile|furnace-chargepile)", "game:coalpile", true)]
  // ...and a domainless one is `game:`, so the modded member of its own alternation never matches.
  [InlineData(
    "@(air|coalpile|furnace-chargepile)",
    "iiex:furnace-chargepile",
    false
  )]
  [InlineData("@(air|coalpile|furnace-chargepile)", "game:air", true)]
  // The same statement without an alternation: the domain decides, not the syntax.
  [InlineData("game:coalpile", "iiex:coalpile", false)]
  [InlineData("*:coalpile", "iiex:coalpile", true)]
  public void A_bare_alternation_is_game_domained_and_cannot_admit_a_modded_block(
    string wildcard,
    string code,
    bool matches
  ) =>
    Assert.Equal(
      matches,
      WildcardUtil.Match(
        new Vintagestory.API.Common.AssetLocation(wildcard),
        new Vintagestory.API.Common.AssetLocation(code)
      )
    );

  /// <summary>
  /// <c>*:</c> admits the path from any mod, which is accepted for these cells. <c>VanillaCodes</c>'
  /// masonry ladder stays domainless for the opposite reason: those alternations name vanilla's own
  /// bricks and must not take a modded lookalike.
  /// </summary>
  [Fact]
  public void The_domain_wildcard_admits_any_mods_matching_path_and_that_is_the_trade() {
    Assert.True(
      WildcardUtil.Match(
        new Vintagestory.API.Common.AssetLocation(IiexCodes.ChargeShaft),
        new Vintagestory.API.Common.AssetLocation("someothermod:coalpile")
      )
    );
    Assert.False(
      WildcardUtil.Match(
        new Vintagestory.API.Common.AssetLocation(VanillaCodes.CoalBed),
        new Vintagestory.API.Common.AssetLocation("someothermod:coalpile")
      )
    );
  }

  #endregion
}
