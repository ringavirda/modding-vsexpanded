using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using IronIndustryExpanded.BlockNetworkMolten.Blocks;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The molten canal ships two defs per shape, one per rock skin, and both must render the same
/// <see cref="ExBlockDef.Any"/> wildcard so that naming either one means both.
/// <para>
/// The two defs declare different variant groups (<c>brick(fire|black|…)</c> against
/// <c>rock(from block/rockwithdeposit)</c>), but <see cref="ExBlockDef.Any"/> wildcards a multi-state
/// group whatever it is called, so both render <c>iiex:molten-canal-start-*-*</c>. The equality holds
/// only while the skins render the same number of segments: give one an extra group and the converter's
/// footprint (BlockConverterControl) narrows to whichever skin it names, while both codes still resolve
/// and every golden stays green.
/// </para>
/// </summary>
public class MoltenCanalSkinsAgreeTests {
  /// <summary>Every canal def, keyed by the shape it draws (<c>start</c>, <c>straight</c>, …); the two
  /// skins of one shape land in the same bucket.</summary>
  private static Dictionary<string, List<ExBlockDef>> ByShape() {
    var byShape = new Dictionary<string, List<ExBlockDef>>();
    foreach (ExBlockDef def in BlockMoltenCanal.Definitions("iiex")) {
      // The shape is the `type` group's single state: `molten/canal/brick/start` and
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
  public void Both_skins_of_a_canal_shape_render_the_same_wildcard() {
    var byShape = ByShape();
    Assert.NotEmpty(byShape);

    var disagreeing = new List<string>();
    foreach ((string shape, List<ExBlockDef> defs) in byShape) {
      // A shape with a single def has no second skin to agree with.
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
  public void At_least_one_shape_really_does_ship_two_skins() {
    // Positive control: the test above passes vacuously if the canal ever stops shipping a second skin.
    Assert.Contains(ByShape(), kv => kv.Value.Count > 1);
  }
}
