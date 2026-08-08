using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
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

  /// <summary>
  /// Every locale under <c>assets/iwex/lang/</c> must carry either no name key for a variant-grouped block,
  /// or a wildcarded one (<c>block-{code}*</c> / <c>block-{code}-{state}*</c>, etc). A bare <c>block-{code}</c>
  /// key is a defect the moment the block has any variant group: the block's own <c>code</c> is never itself a
  /// placeable block - every real instance carries at least one variant suffix on top of it - so VS's
  /// <c>Lang.GetMatching("{domain}:block-" + Code.Path)</c> (no dash-stripping fallback) can never hit that
  /// bare key, and the raw key renders in-game instead of a name.
  /// <para>
  /// This is exactly the F1 defect (the tall hopper gained a <c>side</c> variant group and orphaned its bare
  /// lang key) plus its three pre-existing siblings (blastfurnacecore/cupolafurnacecore/twintubmpblower) - all
  /// four had this literal shape: a variant-grouped block whose only lang entry was the un-suffixed code.
  /// </para>
  /// <para>
  /// Deliberately narrower than "every blocktype has a resolving name key" - some iwex blocks (e.g. the
  /// heating/puddling furnace cores) currently ship no name key at all, which is a different, pre-existing gap
  /// this fix wave was not scoped to touch. Asserting full resolution here would fail on those too.
  /// </para>
  /// </summary>
  [Fact]
  public void Variant_grouped_blocks_never_carry_a_bare_name_key()
  {
    string langDir = DefinitionGoldens.SolutionRelative("assets/iwex/lang");
    var variantBlockCodes = DefinitionGoldens
      .Collect(Domain, Mod)
      .OfType<ExBlockDef>()
      .Where(d => d.ToJson()["variantgroups"] is JArray groups && groups.Count > 0)
      .Select(d => d.Code)
      .Distinct()
      .OrderBy(c => c)
      .ToList();

    var failures = new List<string>();
    foreach (string langFile in Directory.EnumerateFiles(langDir, "*.json"))
    {
      var lang = JObject.Parse(File.ReadAllText(langFile));
      string locale = Path.GetFileName(langFile);
      foreach (string code in variantBlockCodes)
      {
        string bareKey = "block-" + code;
        if (lang.ContainsKey(bareKey))
          failures.Add($"{locale}: '{bareKey}' is bare but '{code}' has variant groups - use '{bareKey}*'");
      }
    }

    Assert.True(failures.Count == 0, string.Join("\n", failures));
  }

  [Fact]
  public void Regenerate_goldens_when_requested()
  {
    if (DefinitionGoldens.WriteRequested)
      DefinitionGoldens.WriteAll(Domain, Mod, GoldenRoot);
  }
}
