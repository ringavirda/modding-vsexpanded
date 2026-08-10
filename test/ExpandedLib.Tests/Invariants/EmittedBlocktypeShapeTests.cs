using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Vintagestory.API.Util;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Every variant a definition emits has to match one of its own <c>shapeByType</c> patterns. A
/// pattern is tested against the whole block code, so inserting a variant group ahead of an existing
/// one moves the code out from under every selector anchored on it and leaves those variants with no
/// shape at all - silently, since an unmatched selector is not an error.
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
      string file in Directory.EnumerateFiles(
        Path.Combine(root, "test"),
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
