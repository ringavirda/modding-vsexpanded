using ExpandedLib.Definitions;
using Vintagestory.API.Util;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The shaft furnace's interior cell codes. These live in <c>IwexCodes</c> rather than exlib's
/// catalogue because what they describe is a <b>shaft furnace's</b> interior: the alternations are
/// mostly vanilla paths, but the blocks that make them correct - the charge pile, the frozen pool -
/// are iwex's own. smex's hot furnace draws them too, which is fine: smex is downstream.
/// <para>
/// This test moving here from the exlib suite is the layering working: the codes went to iwex, and
/// exlib's tests cannot reference iwex, so the test had to follow.
/// </para>
/// </summary>
public class IwexCodesHearthCellTests
{
  [Theory]
  // Everything ChargeShaft admits, the hearth cell admits too - it is a superset, not a variant.
  [InlineData("air")]
  [InlineData("coalpile")]
  [InlineData("furnace-chargepile")]
  // …plus the block the furnace stands its own metal in. A cell whose legend does not admit a block
  // the machine itself places there reads incomplete on the next monitor tick, and for a shaft furnace
  // incomplete means extinguish. This is the same shape of defect that made ChargeShaft need the charge
  // pile, one cell lower.
  [InlineData("hearthmetal-pigiron")]
  [InlineData("hearthmetal-castiron")]
  public void The_hearth_cell_admits_the_pool(string path)
  {
    Assert.True(
      WildcardUtil.Match(
        new Vintagestory.API.Common.AssetLocation(IwexCodes.HearthCell),
        new Vintagestory.API.Common.AssetLocation("iwex:" + path)
      )
    );
  }

  /// <summary>
  /// <b>The pool member is a prefix pattern, and it is written <c>hearthmetal-.*</c> because inside an
  /// alternation the body is a regex.</b> A later metal needs no edit here; a lookalike that merely starts
  /// with the word must still be refused.
  /// <para>
  /// Written as a Theory over <see cref="WildcardUtil.Match"/> rather than as a claim about the string,
  /// because what matters is what the matcher does with it, not how it reads.
  /// </para>
  /// </summary>
  [Theory]
  [InlineData("hearthmetal-pigiron", true)]
  [InlineData("hearthmetal-castiron", true)]
  // A metal that does not exist yet still matches - that is the point of the pattern.
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
        new Vintagestory.API.Common.AssetLocation(IwexCodes.HearthCell),
        new Vintagestory.API.Common.AssetLocation("iwex:" + path)
      )
    );

  /// <summary>
  /// <b>The two syntaxes in one string, pinned - because the obvious spelling is silently inverted.</b>
  /// Inside <c>@(…)</c> the body is a regular expression, so <c>hearthmetal-*</c> reads as "hearthmetal
  /// followed by zero or more dashes": it admits the bare word (a code no definition produces) and refuses
  /// <b>both</b> metals that do exist. Outside an alternation the same <c>*</c> is an ordinary glob and
  /// behaves as expected.
  /// <para>
  /// Nothing about the string's appearance says which world it is in, and the failure is not a crash:
  /// a hearth legend that refuses the block the furnace itself places there reads <em>incomplete</em> on
  /// the next monitor tick, and for a shaft furnace incomplete means <b>extinguish</b>. So the wrong
  /// spelling is asserted here explicitly, rather than only the right one - a test that pinned the shipped
  /// constant alone would pass just as happily after someone "simplified" the <c>.</c> away.
  /// </para>
  /// </summary>
  [Theory]
  // The naive glob spelling, inside an alternation: exactly backwards.
  [InlineData("*:@(air|hearthmetal-*)", "hearthmetal-pigiron", false)]
  [InlineData("*:@(air|hearthmetal-*)", "hearthmetal-castiron", false)]
  [InlineData("*:@(air|hearthmetal-*)", "hearthmetal", true)]
  // The regex spelling, inside an alternation: what the constant ships.
  [InlineData("*:@(air|hearthmetal-.*)", "hearthmetal-pigiron", true)]
  [InlineData("*:@(air|hearthmetal-.*)", "hearthmetal-castiron", true)]
  [InlineData("*:@(air|hearthmetal-.*)", "hearthmetal", false)]
  [InlineData("*:@(air|hearthmetal-.*)", "hearthmetalother", false)]
  // The same glob outside an alternation, which is where `*` means what it looks like.
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
        new Vintagestory.API.Common.AssetLocation("iwex:" + path)
      )
    );

  [Fact]
  public void The_hearth_cell_keeps_the_domain_wildcard_that_makes_it_work()
  {
    // Load-bearing, exactly as on ChargeShaft: `@(…)` is a regex over the path only, so without the
    // leading `*:` the alternation is implicitly `game:` and could admit none of iwex's own blocks -
    // not the charge pile, and not the frozen pool either.
    Assert.StartsWith("*:", IwexCodes.HearthCell);
    Assert.StartsWith("*:", IwexCodes.ChargeShaft);
  }

  #region The rig's copies of these strings

  /// <summary>
  /// <b>The test rig hand-copies both alternations, and nothing made the copies follow the originals.</b>
  /// <see cref="FurnaceLayoutRig.ShaftGlyph"/> and <see cref="FurnaceLayoutRig.HearthGlyph"/> are string
  /// literals identical to <see cref="IwexCodes.ChargeShaft"/> and <see cref="IwexCodes.HearthCell"/> - and
  /// because they are literals rather than references, changing a constant here <b>compiles cleanly</b> and
  /// leaves the rig asserting against the old spelling.
  /// <para>
  /// It came due when <c>solidifiediron</c> / <c>solidifiedcastiron</c> were renamed to
  /// <c>hearthmetal-{metal}</c>: the rig's literal compiles cleanly against the new constant. Without
  /// this equality assertion every furnace layout test would keep passing against blocks that no longer
  /// exist - a green suite asserting nothing about the shipped legend, which is the exact failure mode
  /// such renames keep producing. Both spellings were changed in the same edit, so this pin
  /// was never observed red; what it protects is the *next* change, when only one side moves.
  /// </para>
  /// <para>
  /// The duplication itself is deliberate: a rig that derived its glyphs from the production constants
  /// could not catch a wrong constant, because both sides would move together. Two independent spellings
  /// plus one equality assertion is the shape that catches it - so keep the literals and keep this test.
  /// </para>
  /// </summary>
  [Fact]
  public void The_layout_rigs_glyphs_still_match_the_shipped_constants()
  {
    Assert.Equal(IwexCodes.ChargeShaft, FurnaceLayoutRig.ShaftGlyph);
    Assert.Equal(IwexCodes.HearthCell, FurnaceLayoutRig.HearthGlyph);
  }

  #endregion

  #region The domain half of an alternation

  /// <summary>
  /// <b>The behaviour both constants are built on, pinned against the real
  /// <see cref="WildcardUtil.Match"/> rather than described in a comment.</b>
  /// <para>
  /// The rule is that <c>Match</c> compares <b>domain and path separately</b>. An <c>@(…)</c> alternation
  /// is a regex over the <em>path</em> and says nothing about the domain, so the domain half is whatever
  /// the <see cref="Vintagestory.API.Common.AssetLocation"/> constructor put there - and with no <c>:</c>
  /// in the string that default is <c>game</c>. A bare alternation is therefore silently
  /// <c>game:@(…)</c> and <b>cannot match a modded block however well its path fits</b>.
  /// </para>
  /// <para>
  /// This is the trap, and it is quiet: the alternation still <em>looks</em> right, still matches every
  /// vanilla block in it, and only fails on the one member that is ours. In a multiblock legend that reads
  /// as a structure which will not complete once the player charges it.
  /// </para>
  /// </summary>
  [Theory]
  // A domain-wildcarded alternation matches the same path in any domain - which is the point.
  [InlineData("*:@(air|coalpile|furnace-chargepile)", "iwex:furnace-chargepile", true)]
  [InlineData("*:@(air|coalpile|furnace-chargepile)", "game:air", true)]
  [InlineData("*:@(air|coalpile|furnace-chargepile)", "game:coalpile", true)]
  // ...and a domainless one is `game:`, so the modded member of its own alternation never matches.
  [InlineData("@(air|coalpile|furnace-chargepile)", "iwex:furnace-chargepile", false)]
  [InlineData("@(air|coalpile|furnace-chargepile)", "game:air", true)]
  // The same statement without an alternation in the way: it is the domain that decides, not the syntax.
  [InlineData("game:coalpile", "iwex:coalpile", false)]
  [InlineData("*:coalpile", "iwex:coalpile", true)]
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
  /// The cost of the domain wildcard, stated so it is a choice rather than an oversight: <c>*:</c> admits
  /// the path from <b>any</b> mod, not only ours. That is accepted here - a third mod registering a block
  /// called <c>coalpile</c> or <c>furnace-chargepile</c> would be claiming to be one - but it is the reason
  /// <c>VanillaCodes</c>' masonry ladder stays domainless: those alternations name vanilla's own bricks and
  /// should <em>not</em> silently take a modded lookalike.
  /// </summary>
  [Fact]
  public void The_domain_wildcard_admits_any_mods_matching_path_and_that_is_the_trade()
  {
    Assert.True(
      WildcardUtil.Match(
        new Vintagestory.API.Common.AssetLocation(IwexCodes.ChargeShaft),
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
