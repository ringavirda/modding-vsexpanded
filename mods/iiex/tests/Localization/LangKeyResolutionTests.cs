using System.IO;
using ExpandedLib.Testing;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The literal lang-key rule (<see cref="LangKeys"/>) over iiex's own source.
/// </summary>
public class LangKeyResolutionTests {
  private static readonly string[] SourceRoots = [RepoPaths.Src("iiex")];

  [Fact]
  public void Every_literal_lang_key_resolves_in_english() {
    var missing = LangKeys.Check(
      SourceRoots,
      Path.Combine(RepoPaths.Assets("iiex"), "lang")
    );

    Assert.True(
      missing.Count == 0,
      $"{missing.Count} lang key(s) render as their own code in game: "
        + string.Join("; ", missing)
    );
  }

  [Fact]
  public void The_scan_finds_the_calls_it_is_meant_to_guard() {
    // A regex that stops matching turns the guard above into an unconditional pass.
    Assert.NotEmpty(LangKeys.Literals(SourceRoots));
  }
}
