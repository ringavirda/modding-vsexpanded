using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Golden-file parity for every iiex code-first def: each def must reproduce its committed
/// <c>goldens/iiex/{Location.Path}</c> file, and the golden set must exactly cover the defs. Non-parity
/// behavioural checks (orientation derivation, multiblock cell counts, filler ports) live in
/// <see cref="IiexDefinitionBehaviorTests"/>; the harness is <see cref="DefinitionGoldens"/>.
/// </summary>
public class IiexDefinitionGoldenTests {
  private const string Domain = "iiex";
  private static readonly Assembly Mod =
    typeof(Recipes.Grid.FurnaceRecipeDefinitions).Assembly;
  private static readonly string GoldenRoot =
    DefinitionGoldens.SolutionRelative(
      "test/IronIndustryExpanded.Tests/goldens"
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
  /// Every shape a def names must exist. The goldens pin what a def emits, so a stable but unresolvable
  /// shape path passes them and shows up only in game, as a block with no model.
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

  /// <summary>
  /// Every locale under <c>assets/iiex/lang/</c> must carry either no name key for a variant-grouped block
  /// or a wildcarded one (<c>block-{code}*</c>, <c>block-{code}-{state}*</c>, etc). Once a block has a
  /// variant group its bare <c>code</c> is never itself placeable, so VS's
  /// <c>Lang.GetMatching("{domain}:block-" + Code.Path)</c> (which has no dash-stripping fallback) can
  /// never hit a bare <c>block-{code}</c> key and the raw key renders in place of a name.
  /// <para>
  /// Narrower than "every blocktype has a resolving name key": some iiex blocks ship no name key at all,
  /// which this check does not cover.
  /// </para>
  /// </summary>
  [Fact]
  public void Variant_grouped_blocks_never_carry_a_bare_name_key() {
    string langDir = DefinitionGoldens.SolutionRelative("assets/iiex/lang");
    var variantBlockCodes = DefinitionGoldens
      .Collect(Domain, Mod)
      .OfType<ExBlockDef>()
      .Where(d =>
        d.ToJson()["variantgroups"] is JArray groups && groups.Count > 0
      )
      .Select(d => d.Code)
      .Distinct()
      .OrderBy(c => c)
      .ToList();

    var failures = new List<string>();
    foreach (string langFile in Directory.EnumerateFiles(langDir, "*.json")) {
      var lang = JObject.Parse(File.ReadAllText(langFile));
      string locale = Path.GetFileName(langFile);
      foreach (string code in variantBlockCodes) {
        string bareKey = "block-" + code;
        if (lang.ContainsKey(bareKey))
          failures.Add(
            $"{locale}: '{bareKey}' is bare but '{code}' has variant groups - use '{bareKey}*'"
          );
      }
    }

    Assert.True(failures.Count == 0, string.Join("\n", failures));
  }

  [Fact]
  public void Regenerate_goldens_when_requested() {
    if (DefinitionGoldens.WriteRequested)
      DefinitionGoldens.WriteAll(Domain, Mod, GoldenRoot);
  }
}
