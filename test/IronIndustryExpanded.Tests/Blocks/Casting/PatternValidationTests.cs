using System.Linq;
using IronIndustryExpanded.BlockStructures.Casting;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The load-time pattern sweep: at asset finalize every collectible carrying a <c>mold</c> attribute must
/// parse into a valid <see cref="MoldSpec"/>, so a malformed pattern surfaces as a load error rather than
/// a silent no-op when it is rammed into a cell. Operates on a plain collectible list.
/// </summary>
public class PatternValidationTests {
  private static Item Pattern(string code, string moldJson) =>
    new() {
      Code = new AssetLocation("iiex", code),
      Attributes = new JsonObject(
        JToken.Parse("{ \"mold\": " + moldJson + " }")
      ),
    };

  private const string ValidMold = """
    {
      "size": "cell",
      "shape": "iiex:casting/cell-filling-plate",
      "capacity": 136,
      "cavity": [ { "x1": 3, "y1": 12, "z1": 3, "x2": 13, "y2": 14, "z2": 13 } ],
      "output": { "type": "block", "code": "iiex:casting-mold-ingot" }
    }
    """;

  [Fact]
  public void All_valid_patterns_produce_no_errors() {
    var errors = PatternValidation.Validate([
      Pattern("pattern-plate-oak", ValidMold),
    ]);
    Assert.Empty(errors);
  }

  [Fact]
  public void A_malformed_pattern_is_reported_with_its_code_and_reason() {
    var bad = ValidMold.Replace("\"capacity\": 136", "\"capacity\": 0");
    var errors = PatternValidation.Validate([
      Pattern("pattern-plate-oak", bad),
    ]);

    Assert.Single(errors);
    Assert.Contains("iiex:pattern-plate-oak", errors[0]); // names the offending pattern
    Assert.Contains("capacity", errors[0]); // and the reason
  }

  [Fact]
  public void Non_pattern_collectibles_are_skipped() {
    // An item with no `mold` attribute is not a pattern and must not be flagged.
    var plain = new Item { Code = new AssetLocation("game", "stick") };
    Assert.Empty(PatternValidation.Validate([plain]));
  }

  [Fact]
  public void A_bad_pattern_among_good_ones_is_the_only_one_reported() {
    var errors = PatternValidation.Validate([
      Pattern("pattern-plate-oak", ValidMold),
      Pattern(
        "pattern-broken-oak",
        ValidMold.Replace(
          "\"shape\": \"iiex:casting/cell-filling-plate\"",
          "\"shape\": \"\""
        )
      ),
      new Item { Code = new AssetLocation("game", "plank-oak") },
    ]);

    Assert.Single(errors);
    Assert.Contains("pattern-broken-oak", errors.Single());
  }
}
