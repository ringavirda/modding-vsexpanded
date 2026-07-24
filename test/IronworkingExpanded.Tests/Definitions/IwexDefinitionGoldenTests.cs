using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The single golden-file parity oracle for every iwex code-first def - the solidified-iron / slag blocks, the
/// blast-furnace fittings + door, the ore bunker + mixer, the molten canals + endpoints, the slag paths, the
/// slag/burden items, and the grid recipe files. Each def must reproduce its committed
/// <c>goldens/iwex/{Location.Path}</c> file, and the golden set must exactly cover the defs. Replaces the
/// per-family iwex parity classes. Non-parity behavioural checks (orientation derivation, multiblock cell
/// counts, filler ports) live in <see cref="IwexDefinitionBehaviorTests"/>; the harness is
/// <see cref="DefinitionGoldens"/>.
/// </summary>
public class IwexDefinitionGoldenTests
{
  private const string Domain = "iwex";
  private static readonly Assembly Mod =
    typeof(Recipes.Grid.FurnaceRecipeDefinitions).Assembly;
  private static readonly string GoldenRoot =
    DefinitionGoldens.SolutionRelative("test/IronworkingExpanded.Tests/goldens");

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
    if (DefinitionGoldens.WriteRequested)
      DefinitionGoldens.WriteAll(Domain, Mod, GoldenRoot);
  }
}
