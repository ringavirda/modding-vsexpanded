using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The golden-file parity oracle for every siex code-first def: the trivial blocks, the Bessemer
/// converter parts, the cowper-stove and smoke-stack anchors, the tool molds, and the
/// grid/clayforming/barrel recipe files. Each def must reproduce its committed
/// <c>goldens/siex/{Location.Path}</c> file, and the golden set must exactly cover the defs. Behavioural
/// checks live in <see cref="SiexDefinitionBehaviorTests"/>; the harness is
/// <see cref="DefinitionGoldens"/>.
/// </summary>
public class SiexDefinitionGoldenTests {
  private const string Domain = "siex";
  private static readonly Assembly Mod =
    typeof(Recipes.Grid.ConverterRecipeDefinitions).Assembly;
  private static readonly string GoldenRoot =
    DefinitionGoldens.SolutionRelative(
      "test/SteelIndustryExpanded.Tests/goldens"
    );

  public static IEnumerable<object[]> Defs() =>
    DefinitionGoldens.Cases(Domain, Mod);

  [Theory]
  [MemberData(nameof(Defs))]
  public void Def_reproduces_its_golden(string relativePath) {
    var (ok, message) = DefinitionGoldens.CheckGolden(
      Domain,
      Mod,
      relativePath,
      GoldenRoot
    );
    Assert.True(ok, message);
  }

  [Fact]
  public void Goldens_exactly_cover_the_defs() {
    var (missing, orphans) = DefinitionGoldens.CheckCompleteness(
      Domain,
      Mod,
      GoldenRoot
    );
    Assert.True(
      missing.Count == 0,
      "defs with no golden file (unmigrated?): " + string.Join(", ", missing)
    );
    Assert.True(
      orphans.Count == 0,
      "golden files with no def (deleted?): " + string.Join(", ", orphans)
    );
  }

  /// <summary>
  /// Every shape a def names must resolve to a shipped file. The goldens pin only what a def emits, so a
  /// stable but wrong path passes them and shows up in game as a block with no model.
  /// </summary>
  [Fact]
  public void Every_shape_reference_resolves_to_a_shipped_file() {
    IReadOnlyList<string> missing = DefinitionAssets.MissingShapes(Domain, Mod);

    Assert.True(
      missing.Count == 0,
      "definitions naming shapes that do not exist: "
        + string.Join("; ", missing)
    );
  }

  [Fact]
  public void Regenerate_goldens_when_requested() {
    if (DefinitionGoldens.WriteRequested)
      DefinitionGoldens.WriteAll(Domain, Mod, GoldenRoot);
  }
}
