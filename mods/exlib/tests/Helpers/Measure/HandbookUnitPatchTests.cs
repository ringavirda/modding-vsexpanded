using System.Reflection;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using Vintagestory.GameContent;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="HandbookUnitPatch"/>'s Harmony patch applies cleanly headlessly - it is exlib's only
/// uncategorised <c>[HarmonyPatch]</c> class, so <see cref="HarmonyFixture"/> touches nothing else.
/// Split from <see cref="HandbookUnitPatchConvertTextTests"/> below: a class joins exactly one xUnit
/// collection, and this one mutates process-wide Harmony state, not <c>ExMeasure.System</c>.
/// </summary>
[Collection(ExHarmonyCollection.Name)]
public class HandbookUnitPatchTests {
  [Fact]
  public void The_patch_applies_to_GuiHandbookTextPage_Init() {
    MethodBase original = typeof(GuiHandbookTextPage).GetMethod(
      "Init",
      BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
    )!;

    using var fixture = new HarmonyFixture(
      "exlibtest.handbookunitpatch",
      typeof(HandbookUnitPatch).Assembly
    );

    Assert.True(fixture.IsPatched(original));
  }
}

/// <summary>
/// The prose conversion <see cref="HandbookUnitPatch"/> drives, <see cref="ExMeasure.ConvertMetricText"/>
/// in imperial mode - the case <see cref="ExMeasureTests"/> does not cover, since it only proves the
/// metric-mode passthrough and the no-units passthrough. Joins <see cref="ExMeasureCollection"/>: it
/// mutates the process-wide <c>ExMeasure.System</c>.
/// </summary>
[Collection(ExMeasureCollection.Name)]
public class HandbookUnitPatchConvertTextTests {
  #region ConvertMetricText in imperial mode

  // TestLang echoes every lang key back rather than resolving it to a real symbol, so the "metric
  // symbol" ConvertMetricText matches against is the literal key "exlib:unit-litres", not "L" - the
  // conversion logic under test is exactly the same either way, only the printed unit text differs.
  [Fact]
  public void A_single_value_converts_litres_to_gallons() {
    ExMeasure.System = MeasurementSystem.Imperial;
    try {
      string result = ExMeasure.ConvertMetricText("Holds 30 exlib:unit-litres of water.");

      // 30 * 0.219969248 = 6.599... -> "6.6" at format "0.##"
      Assert.Contains("6.6 exlib:unit-gallons", result);
    } finally {
      ExMeasure.System = MeasurementSystem.Metric;
    }
  }

  [Fact]
  public void A_range_converts_both_ends_and_keeps_the_dash() {
    ExMeasure.System = MeasurementSystem.Imperial;
    try {
      string result = ExMeasure.ConvertMetricText("2-4 exlib:unit-atm");

      // 2 * 14.6959488 = 29.39..., 4 * 14.6959488 = 58.78... -> "29.39-58.78" at format "0.##"
      Assert.Equal("29.39-58.78 exlib:unit-psi", result);
    } finally {
      ExMeasure.System = MeasurementSystem.Metric;
    }
  }

  [Fact]
  public void The_longer_compound_symbol_wins_over_its_prefix() {
    ExMeasure.System = MeasurementSystem.Imperial;
    try {
      // "exlib:unit-litres" is a literal prefix of "exlib:unit-litres-per-second" under TestLang's
      // echo, the same collision "L" vs "L/s" is a stand-in for with the real symbols.
      string result = ExMeasure.ConvertMetricText("8 exlib:unit-litres-per-second");

      Assert.Contains("exlib:unit-gallons-per-second", result);
      Assert.DoesNotContain("exlib:unit-gallons ", result);
    } finally {
      ExMeasure.System = MeasurementSystem.Metric;
    }
  }

  #endregion
}
