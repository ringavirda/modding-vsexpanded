using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Parses every JSON asset the mods ship. The game reports a syntax error only to the server log and
/// then carries on with the file's whole contents missing, so a corrupt recipe or shape file removes
/// everything in it while the build and the rest of the suite stay green.
/// <para>
/// The corpus is derived from the same MSBuild properties that pack the mods -
/// <c>AssetDomain</c> and <c>ShipGameLangOverride</c> in each <c>src/*/*.csproj</c> - so a new mod is
/// covered on the day it is added and the source-only <c>assets/editable/</c> tree stays out.
/// </para>
/// </summary>
public class ShippedAssetJsonTests {
  public static TheoryData<string> EveryShippedJsonAsset() {
    var data = new TheoryData<string>();
    foreach (string path in AssetFiles())
      data.Add(path);
    return data;
  }

  [Theory]
  [MemberData(nameof(EveryShippedJsonAsset))]
  public void Every_shipped_json_asset_parses(string repoRelativePath) {
    var ex = Record.Exception(() => Parse(repoRelativePath).Dispose());

    Assert.True(
      ex == null,
      $"{repoRelativePath} is not valid JSON: {ex?.Message}"
    );
  }

  [Theory]
  [MemberData(nameof(EveryShippedJsonAsset))]
  public void No_shipped_json_asset_carries_a_control_character(
    string repoRelativePath
  ) {
    // Tab, LF and CR are the only control characters legal in JSON text. Anything else is invisible
    // in an editor, survives copy/paste, and breaks the parse at a column the error message cannot
    // show - so name the offending offsets explicitly.
    byte[] bytes = File.ReadAllBytes(
      Path.Combine(RepoRoot(), repoRelativePath)
    );
    var bad = new List<string>();
    for (int i = 0; i < bytes.Length; i++) {
      byte b = bytes[i];
      if (b < 0x20 && b != 0x09 && b != 0x0a && b != 0x0d)
        bad.Add($"0x{b:x2} at offset {i}");
    }

    Assert.True(
      bad.Count == 0,
      $"{repoRelativePath} contains {bad.Count} control character(s): "
        + string.Join(", ", bad.Take(8))
    );
  }

  [Fact]
  public void The_corpus_covers_every_shipped_domain() {
    // A path filter that silently matches nothing turns both theories above into no-ops, so the
    // premise is asserted rather than assumed.
    var covered = AssetFiles()
      .Select(p => p.Split('/')[1])
      .Distinct()
      .ToHashSet(StringComparer.Ordinal);

    Assert.Equal(ShippedDomains().OrderBy(d => d), covered.OrderBy(d => d));
  }

  #region Patch declarations

  [Fact]
  public void Every_patch_entry_declares_the_side_it_runs_on() {
    // JsonPatch.Side defaults to Universal, not to the target file's category, so an entry with no
    // "side" is evaluated on the client too - where blocktypes, itemtypes and recipes do not exist.
    // The patch is then counted as not-found and logs a miss per entry on every client start. The
    // engine's own loader comments on exactly this case as the reason it does not warn about it.
    // Never write "side": null either: that takes the branch which skips the patch on BOTH sides,
    // silently and with no log line at all.
    var offenders = new List<string>();

    foreach (string path in PatchFiles()) {
      using JsonDocument doc = Parse(path);
      int index = 0;
      foreach (JsonElement entry in doc.RootElement.EnumerateArray()) {
        if (
          !entry.TryGetProperty("side", out JsonElement side)
          || side.ValueKind != JsonValueKind.String
        )
          offenders.Add($"{path} [{index}] declares no side");
        else if (ServerOnlyCategory(entry) && side.GetString() != "Server")
          offenders.Add(
            $"{path} [{index}] targets a server-only category but declares "
              + $"\"{side.GetString()}\""
          );
        index++;
      }
    }

    Assert.True(
      offenders.Count == 0,
      "Patch entries must declare their side:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  [Fact]
  public void The_patch_corpus_is_not_empty() {
    // The rule above passes trivially if the path filter stops matching - a patches folder renamed or
    // moved would read as "every entry is correct".
    Assert.NotEmpty(PatchFiles());
  }

  // blocktypes, itemtypes and recipes are all EnumAppSide.Server asset categories. Anything else
  // (shapes, textures, lang) is legitimately client-side or universal, so only the side's presence is
  // required there.
  private static readonly string[] ServerOnlyCategories =
  [
    "blocktypes",
    "itemtypes",
    "recipes",
  ];

  private static bool ServerOnlyCategory(JsonElement entry) =>
    entry.TryGetProperty("file", out JsonElement file)
    && file.GetString() is { } target
    && ServerOnlyCategories.Contains(
      target.Split(':').Last().Split('/').First()
    );

  private static IReadOnlyList<string> PatchFiles() =>
    [
      .. AssetFiles()
        .Where(p => p.Contains("/patches/", StringComparison.Ordinal)),
    ];

  #endregion

  #region Corpus

  /// <summary>
  /// The asset domains the build packs: one per mod, plus <c>game</c> when a mod opts into shipping
  /// the shared vanilla lang override.
  /// </summary>
  private static IEnumerable<string> ShippedDomains() {
    var domains = new List<string>();
    foreach (
      string csproj in Directory.EnumerateFiles(
        Path.Combine(RepoRoot(), "src"),
        "*.csproj",
        SearchOption.AllDirectories
      )
    ) {
      string text = File.ReadAllText(csproj);
      Match domain = Regex.Match(
        text,
        @"<AssetDomain>\s*([^<\s]+)\s*</AssetDomain>"
      );
      if (domain.Success)
        domains.Add(domain.Groups[1].Value);
      if (
        Regex.IsMatch(
          text,
          @"<ShipGameLangOverride>\s*true\s*</ShipGameLangOverride>",
          RegexOptions.IgnoreCase
        )
      )
        domains.Add("game");
    }

    Assert.NotEmpty(domains);
    return domains.Distinct();
  }

  /// <summary>
  /// Every JSON under a shipped domain, repo-relative and forward-slashed so the theory labels read
  /// the same on any platform.
  /// </summary>
  private static IEnumerable<string> AssetFiles() {
    string root = RepoRoot();
    foreach (string domain in ShippedDomains()) {
      string dir = Path.Combine(root, "assets", domain);
      if (!Directory.Exists(dir))
        continue;
      foreach (
        string file in Directory.EnumerateFiles(
          dir,
          "*.json",
          SearchOption.AllDirectories
        )
      )
        yield return Path.GetRelativePath(root, file).Replace('\\', '/');
    }
  }

  private static JsonDocument Parse(string repoRelativePath) =>
    JsonDocument.Parse(
      File.ReadAllText(Path.Combine(RepoRoot(), repoRelativePath)),
      new JsonDocumentOptions {
        // The game's loader tolerates both, so the guard must too - otherwise it would fail files
        // that ship and work.
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
