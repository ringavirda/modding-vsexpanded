using System;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The ASCII layout DSL's own origin check: <c>Core(symbol)</c> marks the glyph the game places (the
/// anchor), and <see cref="MultiblockLayoutBuilder.Build"/> then requires the declared <c>Origin</c> to
/// land it on the layout's own <c>(0,0,0)</c>. Unmarked layouts (no <c>Core</c> call) are unchecked, so
/// every layout shipped before this method existed keeps building.
/// </summary>
public class MultiblockLayoutBuilderTests {
  private static void Build(Action<MultiblockLayoutBuilder> configure) =>
    ExBlockDef.Create("d", "c").MultiblockLayout(configure);

  [Fact]
  public void Build_rejects_an_origin_that_does_not_match_the_anchor() {
    // 'C' sits at column 1, row 0 of the grid, so the matching Origin would be (-1, 0); (0, 0) is wrong.
    var ex = Assert.Throws<InvalidOperationException>(() =>
      Build(s =>
        s.Origin(0, 0)
          .Core('C')
          .Legend('#', "game:refractorybricks-good-tier*")
          .Legend('C', "mod:core")
          .Layer(0, "# C")
      )
    );
    Assert.Contains("'C'", ex.Message);
    Assert.Contains("Origin(0,0)", ex.Message);
    Assert.Contains("(1,0,0)", ex.Message);
  }

  [Fact]
  public void Build_accepts_the_matching_origin() {
    var json = ExBlockDef
      .Create("d", "c")
      .MultiblockLayout(s =>
        s.Origin(-1, 0)
          .Core('C')
          .Legend('#', "game:refractorybricks-good-tier*")
          .Legend('C', "mod:core")
          .Layer(0, "# C")
      )
      .ToJson();

    Assert.NotNull(json["attributes"]!["multiblockStructure"]);
  }

  [Fact]
  public void Build_rejects_a_core_symbol_with_no_legend_entry() {
    var ex = Assert.Throws<InvalidOperationException>(() =>
      Build(s =>
        s.Origin(0, 0)
          .Core('C')
          .Legend('#', "game:refractorybricks-good-tier*")
          .Layer(0, "#")
      )
    );
    Assert.Contains("'C'", ex.Message);
    Assert.Contains("Legend", ex.Message);
  }

  [Fact]
  public void Build_rejects_a_core_symbol_never_drawn() {
    var ex = Assert.Throws<InvalidOperationException>(() =>
      Build(s =>
        s.Origin(0, 0)
          .Core('C')
          .Legend('#', "game:refractorybricks-good-tier*")
          .Legend('C', "mod:core")
          .Layer(0, "#")
      )
    );
    Assert.Contains("'C'", ex.Message);
    Assert.Contains("never draws", ex.Message);
  }

  [Fact]
  public void A_layout_that_never_calls_Core_is_not_checked() {
    // The pre-existing behaviour: an unmarked layout builds whatever its Origin says, right or wrong.
    var json = ExBlockDef
      .Create("d", "c")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('#', "game:refractorybricks-good-tier*")
          .Legend('C', "mod:core")
          .Layer(0, "# C")
      )
      .ToJson();

    Assert.NotNull(json["attributes"]!["multiblockStructure"]);
  }

  [Fact]
  public void Slice_draws_a_fixed_x_elevation() {
    // A front elevation at x=0: 'C' at column 0 (z=0), '#' at column 1 (z=1); both at y=0 (originB).
    JObject structure = (JObject)
      ExBlockDef
        .Create("d", "c")
        .MultiblockLayout(s =>
          s.Origin(0, 0)
            .Core('C')
            .Legend('#', "game:refractorybricks-good-tier*")
            .Legend('C', "mod:core")
            .Slice(0, "C #")
        )
        .ToJson()["attributes"]!["multiblockStructure"]!;

    Assert.Equal(2, ((JArray)structure["offsets"]!).Count);
  }

  [Fact]
  public void Face_draws_a_fixed_z_elevation() {
    // A front elevation at z=0: 'C' at column 0 (x=0), '#' at column 1 (x=1); both at y=0 (originB).
    JObject structure = (JObject)
      ExBlockDef
        .Create("d", "c")
        .MultiblockLayout(s =>
          s.Origin(0, 0)
            .Core('C')
            .Legend('#', "game:refractorybricks-good-tier*")
            .Legend('C', "mod:core")
            .Face(0, "C #")
        )
        .ToJson()["attributes"]!["multiblockStructure"]!;

    Assert.Equal(2, ((JArray)structure["offsets"]!).Count);
  }
}
