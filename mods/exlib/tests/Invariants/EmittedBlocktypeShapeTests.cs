using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Vintagestory.API.Util;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The two selector families anchored on an emitted block code, both of which fail silently when the
/// code moves out from under them: <c>shapeByType</c>, which leaves a variant with no shape, and the
/// handbook's <c>groupBy</c>, which leaves an entry ungrouped. Inserting a variant group ahead of an
/// existing one is how a code moves, and neither an unmatched shape pattern nor an unmatched group
/// selector is an error.
/// <para>
/// The goldens are the emitted blocktypes, so checking them covers the code-first definitions
/// without needing a running game.
/// </para>
/// </summary>
public class EmittedBlocktypeShapeTests {
  public static TheoryData<string> EveryGoldenBlocktype() {
    var data = new TheoryData<string>();
    foreach (string path in GoldenBlocktypes())
      data.Add(path);
    return data;
  }

  [Theory]
  [MemberData(nameof(EveryGoldenBlocktype))]
  public void Every_block_variant_resolves_a_shape(string repoRelativePath) {
    using JsonDocument doc = Parse(repoRelativePath);
    JsonElement root = doc.RootElement;
    if (root.ValueKind != JsonValueKind.Object)
      return;

    JsonElement shapes = default;
    foreach (JsonProperty p in root.EnumerateObject())
      if (p.NameEquals("shapebytype") || p.NameEquals("shapeByType"))
        shapes = p.Value;
    if (shapes.ValueKind != JsonValueKind.Object)
      return;

    var patterns = shapes.EnumerateObject().Select(p => p.Name).ToList();
    var unmatched = BlockCodes(root)
      .Where(code => !patterns.Any(pat => WildcardUtil.Match(pat, code)))
      .ToList();

    Assert.True(
      unmatched.Count == 0,
      $"{repoRelativePath}: {unmatched.Count} variant(s) match no shape pattern "
        + $"[{string.Join(", ", patterns)}]: {string.Join(", ", unmatched.Take(6))}"
    );
  }

  /// <summary>
  /// A handbook group selector is matched against a whole collectible code, and one carrying no domain
  /// is qualified with the grouping block's own before matching
  /// (<c>SlideshowItemstackTextComponent</c>). It selects across blocktypes - the four pipe segments
  /// each list all four of their tier's shapes - so the corpus is the whole domain, not one def.
  /// </summary>
  [Theory]
  [MemberData(nameof(EveryGoldenBlocktype))]
  public void Every_handbook_group_selector_matches_a_shipped_code(
    string repoRelativePath
  ) {
    using JsonDocument doc = Parse(repoRelativePath);
    var selectors = HandbookGroups(doc.RootElement);
    if (selectors.Count == 0)
      return;

    string domain = DomainOf(repoRelativePath);
    IReadOnlyList<string> corpus = _qualifiedCodes.Value;
    var unmatched = selectors
      .Where(sel => {
        string qualified = sel.Contains(':') ? sel : $"{domain}:{sel}";
        return !corpus.Any(code => WildcardUtil.Match(qualified, code));
      })
      .ToList();

    Assert.True(
      unmatched.Count == 0,
      $"{repoRelativePath}: {unmatched.Count} handbook groupBy selector(s) match no shipped "
        + $"block code (resolved against domain {domain}): {string.Join(", ", unmatched)}"
    );
  }

  [Fact]
  public void The_corpus_reaches_definitions_that_group_the_handbook() {
    // Same reason as the shape guard below: with no groupBy in the corpus the theory returns early on
    // every case and a clean run is indistinguishable from an inert one.
    int grouping = GoldenBlocktypes()
      .Count(p => {
        using JsonDocument doc = Parse(p);
        return HandbookGroups(doc.RootElement).Count > 0;
      });

    Assert.True(
      grouping > 0,
      "no golden blocktype carries a handbook groupBy - the guard is inert"
    );
  }

  [Fact]
  public void The_corpus_reaches_definitions_that_select_a_shape_by_type() {
    // Without at least one shapeByType golden the theory above passes by returning early on every
    // case, which reads identical to a clean run.
    int selecting = GoldenBlocktypes()
      .Count(p => {
        using JsonDocument doc = Parse(p);
        return doc.RootElement.ValueKind == JsonValueKind.Object
          && doc.RootElement.EnumerateObject()
            .Any(x =>
              x.NameEquals("shapebytype") || x.NameEquals("shapeByType")
            );
      });

    Assert.True(
      selecting > 0,
      "no golden blocktype carries a shapeByType map - the guard is inert"
    );
  }

  #region Corpus

  /// <summary>The <c>attributes.handbook.groupBy</c> selectors a blocktype declares, or empty.</summary>
  private static List<string> HandbookGroups(JsonElement root) {
    if (
      root.ValueKind != JsonValueKind.Object
      || !root.TryGetProperty("attributes", out JsonElement attrs)
      || !attrs.TryGetProperty("handbook", out JsonElement handbook)
      || !handbook.TryGetProperty("groupBy", out JsonElement groups)
      || groups.ValueKind != JsonValueKind.Array
    )
      return [];

    return [.. groups.EnumerateArray().Select(g => g.GetString() ?? "")];
  }

  // Built once: the theory runs per golden and each case needs the whole corpus.
  private static readonly Lazy<IReadOnlyList<string>> _qualifiedCodes = new(
    () =>
      [.. QualifiedCodes()]
  );

  /// <summary>Every block code every golden emits, domain-qualified.</summary>
  private static IEnumerable<string> QualifiedCodes() {
    foreach (string path in GoldenBlocktypes()) {
      string domain = DomainOf(path);
      using JsonDocument doc = Parse(path);
      foreach (string code in BlockCodes(doc.RootElement))
        yield return $"{domain}:{code}";
    }
  }

  // goldens/<domain>/blocktypes/... - the golden JSON carries the bare code, not the domain.
  private static string DomainOf(string repoRelativePath) {
    string[] parts = repoRelativePath.Split('/');
    int i = Array.IndexOf(parts, "goldens");
    return i >= 0 && i + 1 < parts.Length ? parts[i + 1] : "";
  }

  /// <summary>Every full block code the definition's variant groups produce.</summary>
  private static IEnumerable<string> BlockCodes(JsonElement root) {
    string code = root.TryGetProperty("code", out JsonElement c)
      ? c.GetString() ?? ""
      : "";
    if (code.Length == 0)
      yield break;

    var axes = new List<string[]>();
    if (root.TryGetProperty("variantgroups", out JsonElement groups))
      foreach (JsonElement g in groups.EnumerateArray()) {
        if (g.TryGetProperty("states", out JsonElement states))
          axes.Add([
            .. states.EnumerateArray().Select(s => s.GetString() ?? ""),
          ]);
        else
          // The only loadFromProperties in use is the horizontal orientation.
          axes.Add(["north", "south", "east", "west"]);
      }

    IEnumerable<string> codes = [code];
    foreach (string[] axis in axes)
      codes = codes.SelectMany(prefix => axis.Select(v => prefix + "-" + v));
    foreach (string full in codes)
      yield return full;
  }

  private static List<string> GoldenBlocktypes() {
    string root = RepoRoot();
    var files = new List<string>();
    foreach (
      string mod in Directory.EnumerateDirectories(Path.Combine(root, "mods"))
    ) {
      string tests = Path.Combine(mod, "tests");
      if (!Directory.Exists(tests))
        continue;

      foreach (
        string file in Directory.EnumerateFiles(
          tests,
          "*.json",
          SearchOption.AllDirectories
        )
      ) {
        string rel = Path.GetRelativePath(root, file).Replace('\\', '/');
        if (rel.Contains("/bin/") || rel.Contains("/obj/"))
          continue;
        if (
          !rel.Contains("/goldens/", StringComparison.Ordinal)
          || !rel.Contains("/blocktypes/", StringComparison.Ordinal)
        )
          continue;
        files.Add(rel);
      }
    }
    return files;
  }

  private static JsonDocument Parse(string repoRelativePath) =>
    JsonDocument.Parse(
      File.ReadAllText(Path.Combine(RepoRoot(), repoRelativePath)),
      new JsonDocumentOptions {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
      }
    );

  private static string RepoRoot() {
    DirectoryInfo? dir = new(AppContext.BaseDirectory);
    while (
      dir != null
      && !File.Exists(Path.Combine(dir.FullName, "VintageStory.sln"))
    )
      dir = dir.Parent;
    Assert.True(dir != null, "could not locate repo root (VintageStory.sln)");
    return dir!.FullName;
  }

  #endregion
}
