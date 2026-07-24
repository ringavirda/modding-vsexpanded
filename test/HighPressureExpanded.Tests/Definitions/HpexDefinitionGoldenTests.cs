using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace HighPressureExpanded.Tests;

/// <summary>
/// The single golden-file parity oracle for every hpex code-first def - the Lancashire boiler and the
/// Cornish engine blocktypes plus their grid recipe file. Each def must reproduce its committed
/// <c>goldens/hpex/{Location.Path}</c> file (the byte-faithful record of the JSON it injects), and the
/// golden set must exactly cover the defs. Mirrors the per-mod oracles in the other test projects; the
/// shared harness is <see cref="DefinitionGoldens"/>.
/// </summary>
public class HpexDefinitionGoldenTests
{
  private const string Domain = "hpex";
  private static readonly Assembly Mod =
    typeof(Recipes.Grid.MachineRecipeDefinitions).Assembly;
  private static readonly string GoldenRoot =
    DefinitionGoldens.SolutionRelative("test/HighPressureExpanded.Tests/goldens");

  public static IEnumerable<object[]> Defs() => DefinitionGoldens.Cases(Domain, Mod);

  [Theory]
  [MemberData(nameof(Defs))]
  public void Def_reproduces_its_golden(string relativePath)
  {
    var (ok, message) = DefinitionGoldens.CheckGolden(Domain, Mod, relativePath, GoldenRoot);
    Assert.True(ok, message);
  }

  [Fact]
  public void Goldens_exactly_cover_the_defs()
  {
    var (missing, orphans) = DefinitionGoldens.CheckCompleteness(Domain, Mod, GoldenRoot);
    Assert.True(missing.Count == 0, "defs with no golden file (unmigrated?): " + string.Join(", ", missing));
    Assert.True(orphans.Count == 0, "golden files with no def (deleted?): " + string.Join(", ", orphans));
  }


  /// <summary>
  /// Every shape a def names must exist. The goldens above pin what a def <em>emits</em>, which cannot
  /// catch a stable-but-wrong path: a shape that does not resolve is invisible everywhere except in
  /// game, where the block simply has no model.
  /// </summary>
  [Fact]
  public void Every_shape_reference_resolves_to_a_shipped_file()
  {
    IReadOnlyList<string> missing = DefinitionAssets.MissingShapes(Domain, Mod);

    Assert.True(
      missing.Count == 0,
      "definitions naming shapes that do not exist: " + string.Join("; ", missing)
    );
  }

  [Fact]
  public void Regenerate_goldens_when_requested()
  {
    // Dormant unless EXLIB_WRITE_GOLDENS=1 - re-blesses the golden files from the current def output after an
    // intended def change (then commit the diff). A normal run does nothing.
    if (DefinitionGoldens.WriteRequested)
      DefinitionGoldens.WriteAll(Domain, Mod, GoldenRoot);
  }
}
