using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The release metadata every mod ships. Packaging rewrites the <c>game</c> dependency from the
/// current version to each legacy target's by a literal string replace, and a replace that matches
/// nothing reports success - so a mod declaring any other version silently ships the wrong floor in
/// all three zips.
/// <para>
/// The expected version is read from <c>src/Directory.Build.props</c>, the manifest the build itself
/// uses, so promoting a new primary game version does not need this file edited too.
/// </para>
/// </summary>
public class ModinfoTests {
  private sealed record Mod(
    string Folder,
    string ModId,
    string Version,
    JsonElement Dependencies
  );

  [Fact]
  public void Every_mod_declares_the_current_game_version() {
    string expected = CurrentGameVer();
    var wrong = Mods()
      .Where(m => Dependency(m, "game") != expected)
      .Select(m =>
        $"{m.Folder} declares game {Dependency(m, "game") ?? "(none)"}"
      )
      .ToList();

    Assert.True(
      wrong.Count == 0,
      $"every src/*/modinfo.json must declare \"game\": \"{expected}\" (CurrentGameVer in "
        + $"src/Directory.Build.props): {string.Join("; ", wrong)}"
    );
  }

  [Fact]
  public void A_dependency_on_a_sibling_mod_is_not_below_that_mod_s_version() {
    // A modinfo dependency is a MINIMUM, not a pin. A floor left at an old version lets the game load
    // a sibling too old to carry the APIs the dependent compiles against, which surfaces as a
    // MissingMethodException at runtime rather than as the unmet dependency it is.
    var mods = Mods().ToList();
    var byId = mods.ToDictionary(
      m => m.ModId,
      m => m.Version,
      StringComparer.Ordinal
    );
    var stale = new List<string>();

    foreach (Mod m in mods)
      foreach (JsonProperty dep in m.Dependencies.EnumerateObject()) {
        if (!byId.TryGetValue(dep.Name, out string? actual))
          continue; // game, or a third-party mod this repo does not build
        string declared = dep.Value.GetString() ?? "";
        if (Compare(declared, actual) < 0)
          stale.Add(
            $"{m.Folder} requires {dep.Name} >= {declared}, but {dep.Name} is {actual}"
          );
      }

    Assert.True(stale.Count == 0, string.Join("; ", stale));
  }

  [Fact]
  public void No_mod_s_source_version_is_at_or_below_a_version_it_has_already_released() {
    // The game compares by modid, and a config file carries the version that wrote it. Cutting a
    // release from a tree stamped at or below what is already published means the player's installed
    // build is newer than the update: the update never supersedes it, and every ExConfigMigration
    // whose ToVersion falls in the gap has already been passed, so it can never fire again. That
    // combination has shipped from this repo once.
    var released = ReleasedVersions();
    var behind = new List<string>();

    foreach (Mod m in Mods()) {
      if (!released.TryGetValue(m.ModId, out string? shipped))
        continue; // never published under this id; nothing to regress against
      if (Compare(m.Version, shipped) <= 0)
        behind.Add(
          $"{m.Folder} is {m.Version} but {m.ModId} {shipped} is already in dist/Releases"
        );
    }

    Assert.True(
      behind.Count == 0,
      "a source version must be strictly above every version already published under the same "
        + $"modid: {string.Join("; ", behind)}"
    );
  }

  #region Corpus

  /// <summary>
  /// The highest version published per modid, read from the zip names under <c>dist/Releases/</c>
  /// rather than restated here, so recording a release needs no edit to this file. Keyed by the modid
  /// the artifact becomes today: <c>ppex</c> was renamed to <c>iiex</c>, and a rename does not reset
  /// the version line, because a pre-rename config file is folded into the renamed mod's section and
  /// still carries the version that wrote it.
  /// </summary>
  private static Dictionary<string, string> ReleasedVersions() {
    string root = Path.Combine(RepoRoot(), "dist", "Releases");
    var highest = new Dictionary<string, string>(StringComparer.Ordinal);
    if (!Directory.Exists(root))
      return highest;

    // {modid}_{version}.zip, optionally with the legacy game version appended: exlib_0.7.2_1.21.0.zip
    var name = new Regex(
      @"^(?<id>[a-z][a-z-]*)_(?<ver>\d+(?:\.\d+)*)(?:_\d+(?:\.\d+)*)?\.zip$"
    );

    foreach (
      string zip in Directory.EnumerateFiles(
        root,
        "*.zip",
        SearchOption.AllDirectories
      )
    ) {
      Match m = name.Match(Path.GetFileName(zip));
      if (!m.Success)
        continue;

      string id = m.Groups["id"].Value;
      id = id switch {
        "ppex" => "iiex", // renamed 2026-07; IiexRenameMigration carries the codes across
        _ => id,
      };

      string ver = m.Groups["ver"].Value;
      if (!highest.TryGetValue(id, out string? seen) || Compare(ver, seen) > 0)
        highest[id] = ver;
    }

    // Three mods have shipped. An empty map would make the assertion above vacuous, which is the one
    // way this guard could pass while the regression it exists for is live.
    Assert.NotEmpty(highest);
    return highest;
  }

  private static string? Dependency(Mod mod, string name) =>
    mod.Dependencies.TryGetProperty(name, out JsonElement v)
      ? v.GetString()
      : null;

  /// <summary>Numeric component-wise compare, so "0.10.0" sorts above "0.9.0".</summary>
  private static int Compare(string a, string b) {
    int[] Parts(string s) =>
      [.. s.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0)];

    int[] x = Parts(a),
      y = Parts(b);
    for (int i = 0; i < Math.Max(x.Length, y.Length); i++) {
      int cmp = (i < x.Length ? x[i] : 0).CompareTo(i < y.Length ? y[i] : 0);
      if (cmp != 0)
        return cmp;
    }
    return 0;
  }

  private static string CurrentGameVer() {
    string props = File.ReadAllText(
      Path.Combine(RepoRoot(), "src", "Directory.Build.props")
    );
    Match m = Regex.Match(
      props,
      @"<CurrentGameVer>\s*([^<\s]+)\s*</CurrentGameVer>"
    );
    Assert.True(
      m.Success,
      "src/Directory.Build.props declares no <CurrentGameVer>"
    );
    return m.Groups[1].Value;
  }

  private static IEnumerable<Mod> Mods() {
    var mods = new List<Mod>();
    foreach (
      string path in Directory.EnumerateFiles(
        Path.Combine(RepoRoot(), "src"),
        "modinfo.json",
        SearchOption.AllDirectories
      )
    ) {
      string rel = path.Replace('\\', '/');
      if (rel.Contains("/bin/") || rel.Contains("/obj/"))
        continue;
      using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
      JsonElement root = doc.RootElement;
      mods.Add(
        new Mod(
          new DirectoryInfo(Path.GetDirectoryName(path)!).Name,
          root.GetProperty("modid").GetString() ?? "",
          root.GetProperty("version").GetString() ?? "",
          root.TryGetProperty("dependencies", out JsonElement d)
            ? d.Clone()
            : default
        )
      );
    }

    // Every mod project ships one, so an empty corpus means the discovery broke, not that the repo
    // has no mods.
    Assert.NotEmpty(mods);
    return mods;
  }

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
