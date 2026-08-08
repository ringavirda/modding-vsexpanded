using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using IronworkingExpanded.BlockNetworkMolten.Blocks;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The molten canal ships <b>two defs per shape</b> - one per rock skin - and this pins the property that
/// lets a caller name either one and mean both.
///
/// <para>
/// <b>Why the property exists at all.</b> The two defs declare <em>different</em> variant groups:
/// <c>brick(fire|black|…)</c> against <c>rock(from block/rockwithdeposit)</c>. They are two defs precisely
/// because of that, since an asset path must be unique per def. But <see cref="ExBlockDef.Any"/> wildcards
/// a multi-state group whatever it is called, so both render the identical string
/// <c>iwex:molten-canal-start-*-*</c> - and a caller who writes the brick entry's <c>Any</c> gets the
/// cobblestone canals for free.
/// </para>
///
/// <para>
/// <b>This was believed to be impossible, and the belief cost a hand-written wildcard.</b>
/// <c>IwexCodes</c> carried <c>MoltenCanalStart</c>/<c>Straight</c>/<c>Tap</c> for years on the reasoning
/// that "any canal start is not any single entry's <c>Any</c>". It is. The wildcards are gone and the
/// converter's footprint reaches the generated table directly.
/// </para>
///
/// <para>
/// <b>So this test is what makes that safe.</b> The equality holds only while the two skins render the
/// same number of segments. Give one an extra group - a tier, a finish - and their <c>Any</c> values
/// diverge, the converter's footprint silently narrows to whichever skin it names, and a player who built
/// their runout from the other one finds the structure will not complete. Nothing else in the suite would
/// notice: both codes are real, both resolve, and every golden stays green.
/// </para>
/// </summary>
public class MoltenCanalSkinsAgreeTests
{
  /// <summary>Every canal def, keyed by the shape it draws (<c>start</c>, <c>straight</c>, …) - the two
  /// skins of one shape land in the same bucket.</summary>
  private static Dictionary<string, List<ExBlockDef>> ByShape()
  {
    var byShape = new Dictionary<string, List<ExBlockDef>>();
    foreach (ExBlockDef def in BlockMoltenCanal.Definitions("iwex"))
    {
      // The shape is the `type` group's single state - `molten/canal/brick/start` and
      // `molten/canal/cobblestone/start` are one shape drawn two ways.
      string? shape = def
        .VariantGroups.FirstOrDefault(g => g.Name == "type")
        ?.States.FirstOrDefault();
      if (shape == null)
        continue;
      if (!byShape.TryGetValue(shape, out var list))
        byShape[shape] = list = [];
      list.Add(def);
    }
    return byShape;
  }

  [Fact]
  public void Both_skins_of_a_canal_shape_render_the_same_wildcard()
  {
    var byShape = ByShape();
    Assert.NotEmpty(byShape);

    var disagreeing = new List<string>();
    foreach ((string shape, List<ExBlockDef> defs) in byShape)
    {
      // A shape with one def is fine - it simply has no second skin to agree with.
      var distinct = defs.Select(d => d.Any).Distinct().ToList();
      if (distinct.Count > 1)
        disagreeing.Add(
          $"{shape}: {string.Join(" vs ", distinct)} "
            + $"(from {string.Join(", ", defs.Select(d => d.Location.Path))})"
        );
    }

    Assert.True(
      disagreeing.Count == 0,
      "These canal shapes' skins no longer render the same wildcard, so naming one no longer means both "
        + "- and the converter's footprint (BlockConverterControl) has silently narrowed to whichever it "
        + "names:\n  "
        + string.Join("\n  ", disagreeing)
    );
  }

  [Fact]
  public void At_least_one_shape_really_does_ship_two_skins()
  {
    // The positive control. The theory above passes vacuously if the canal ever stops shipping a second
    // skin, and "both skins agree" would then be a statement about nothing.
    Assert.Contains(ByShape(), kv => kv.Value.Count > 1);
  }
}
