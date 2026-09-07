using System;
using System.Text;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using NSubstitute;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The <c>GetBlockInfo</c> line-builders: a plain <c>Lang.Get</c> line, the same guarded by a
/// condition, and a measured value folded through <see cref="ExMeasure"/>.
/// </summary>
[Collection(ExMeasureCollection.Name)] // Measure reads the global ExMeasure.System
public class ExInfoTests : IDisposable {
  private readonly MeasurementSystem _original = ExMeasure.System;

  public ExInfoTests() => TestLang.Init();

  public void Dispose() => ExMeasure.System = _original;

  [Fact]
  public void Lang_appends_the_keys_text_as_one_line() {
    var dsc = new StringBuilder();

    dsc.Lang("iiex:some-key");

    Assert.Equal("iiex:some-key" + Environment.NewLine, dsc.ToString());
  }

  [Fact]
  public void LangIf_appends_the_line_when_the_condition_holds() {
    var dsc = new StringBuilder();

    dsc.LangIf(true, "iiex:some-key");

    Assert.Equal("iiex:some-key" + Environment.NewLine, dsc.ToString());
  }

  [Fact]
  public void LangIf_appends_nothing_when_the_condition_fails() {
    var dsc = new StringBuilder();

    dsc.LangIf(false, "iiex:some-key");

    Assert.Equal("", dsc.ToString());
  }

  [Fact]
  public void Measure_formats_the_value_in_metric() {
    ExMeasure.System = MeasurementSystem.Metric;
    var dsc = new StringBuilder();

    dsc.Measure("iiex:water-volume", 800f, "volume");

    TestLang
      .Service.Received()
      .Get(
        "iiex:water-volume",
        Arg.Is<object[]>(a =>
          a.Length == 1 && (string)a[0] == ExMeasure.Volume(800f)
        )
      );
  }

  [Fact]
  public void Measure_formats_the_value_in_imperial() {
    ExMeasure.System = MeasurementSystem.Imperial;
    var dsc = new StringBuilder();

    dsc.Measure("iiex:water-volume", 100f, "pressure");

    TestLang
      .Service.Received()
      .Get(
        "iiex:water-volume",
        Arg.Is<object[]>(a =>
          a.Length == 1 && (string)a[0] == ExMeasure.Pressure(100f)
        )
      );
  }

  [Theory]
  [InlineData("volume")]
  [InlineData("PRESSURE")]
  [InlineData("Temperature")]
  [InlineData("power")]
  [InlineData("speed")]
  [InlineData("flowrate")]
  [InlineData("energy")]
  public void Measure_accepts_every_known_unit_case_insensitively(string unit) {
    var dsc = new StringBuilder();

    dsc.Measure("iiex:some-key", 10f, unit);

    Assert.Equal("iiex:some-key" + Environment.NewLine, dsc.ToString());
  }

  [Fact]
  public void Measure_throws_naming_an_unrecognised_unit() {
    var dsc = new StringBuilder();

    var ex = Assert.Throws<ArgumentException>(() =>
      dsc.Measure("iiex:some-key", 10f, "bogus")
    );
    Assert.Contains("bogus", ex.Message);
  }
}
