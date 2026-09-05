using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Repo-wide block entity teardown rule: whatever cleans up in <c>OnBlockRemoved</c> must also clean
/// up in <c>OnBlockUnloaded</c>, or state in the file why it does not. Removal means the block is gone
/// for good; unload means the chunk left memory while the block stays placed, so the two paths share
/// the client-side releases (listeners, renderers, dialogs, highlights) but not the permanent ones
/// (dropping contents, deregistering a network node, putting a fire out).
/// <para>
/// Behaviours are covered by the same signatures, because a block entity fans both calls out to them.
/// </para>
/// <para>
/// Checked per file rather than per class tree: a subclass's own teardown is its own, and a corrected
/// base class does not run it. A file whose asymmetry is deliberate opts out with <c>removal-only
/// teardown:</c> followed by the reason.
/// </para>
/// </summary>
public class TeardownSymmetryTests {
  #region Corpus

  // The block entity signatures take no arguments, which separates them from the Block overloads of the
  // same names (those take a world and a position and are not part of this rule).
  private static readonly Regex RemovedOverride = new(
    @"override\s+void\s+OnBlockRemoved\s*\(\s*\)",
    RegexOptions.Compiled
  );
  private static readonly Regex UnloadedOverride = new(
    @"override\s+void\s+OnBlockUnloaded\s*\(\s*\)",
    RegexOptions.Compiled
  );

  private const string OptOut = "removal-only teardown:";

  // Every mod's own source tree - mods/*/src - the whole corpus this rule scans.
  private static IEnumerable<string> SourceFiles() {
    string modsRoot = Path.Combine(RepoRoot(), "mods");
    foreach (string mod in Directory.EnumerateDirectories(modsRoot)) {
      string full = Path.Combine(mod, "src");
      if (!Directory.Exists(full))
        continue;
      foreach (
        string path in Directory.EnumerateFiles(
          full,
          "*.cs",
          SearchOption.AllDirectories
        )
      ) {
        string rel = Rel(path);
        if (
          rel.Contains("/bin/", StringComparison.Ordinal)
          || rel.Contains("/obj/", StringComparison.Ordinal)
          || path.EndsWith(".g.cs", StringComparison.Ordinal)
        )
          continue;
        yield return path;
      }
    }
  }

  private static string Rel(string path) =>
    Path.GetRelativePath(RepoRoot(), path).Replace('\\', '/');

  private static string RepoRoot() {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (
      dir != null
      && !File.Exists(Path.Combine(dir.FullName, "VintageStory.sln"))
    )
      dir = dir.Parent;
    return dir?.FullName
      ?? throw new InvalidOperationException(
        "Could not locate the repo root (VintageStory.sln) from "
          + AppContext.BaseDirectory
      );
  }

  #endregion

  #region Rules

  [Fact]
  public void A_block_entity_that_tears_down_on_removal_also_tears_down_on_unload() {
    var offenders = new List<string>();
    int seen = 0;
    foreach (string f in SourceFiles()) {
      string text = File.ReadAllText(f);
      if (!RemovedOverride.IsMatch(text))
        continue;
      seen++;
      if (UnloadedOverride.IsMatch(text))
        continue;
      if (text.Contains(OptOut, StringComparison.Ordinal))
        continue;
      offenders.Add(Rel(f));
    }

    Assert.True(
      seen > 0,
      "Found no OnBlockRemoved overrides at all - the source walk is wrong."
    );
    Assert.True(
      offenders.Count == 0,
      "These clean up on removal but not on chunk unload. Add an OnBlockUnloaded doing the "
        + "client-side share of the teardown, or mark the file with \""
        + OptOut
        + " <reason>\":\n    "
        + string.Join("\n    ", offenders)
    );
  }

  [Fact]
  public void A_removal_only_opt_out_sits_where_the_removal_teardown_does() {
    // A marker outlives the method it explains when the teardown moves to a behaviour or a subclass,
    // and the rule above reads any file carrying one as exempt - so a stale marker quietly exempts
    // whatever offender lands in that file next.
    var offenders = new List<string>();
    int marked = 0;
    foreach (string f in SourceFiles()) {
      string text = File.ReadAllText(f);
      if (!text.Contains(OptOut, StringComparison.Ordinal))
        continue;
      marked++;
      if (!RemovedOverride.IsMatch(text))
        offenders.Add(Rel(f));
    }

    Assert.True(
      marked > 0,
      "Found no opt-out markers at all - the source walk is wrong."
    );
    Assert.True(
      offenders.Count == 0,
      "These carry the \""
        + OptOut
        + "\" marker but override no OnBlockRemoved(), so it explains nothing and exempts the "
        + "file from the rule above. Move it to whatever does the removal-only teardown now:\n    "
        + string.Join("\n    ", offenders)
    );
  }

  [Fact]
  public void A_removal_only_opt_out_states_its_reason() {
    var offenders = new List<string>();
    foreach (string f in SourceFiles()) {
      foreach (string line in File.ReadAllLines(f)) {
        int at = line.IndexOf(OptOut, StringComparison.Ordinal);
        if (at < 0)
          continue;
        if (line[(at + OptOut.Length)..].Trim().Length < 10)
          offenders.Add(Rel(f));
      }
    }

    Assert.True(
      offenders.Count == 0,
      "The opt-out marker must be followed by the reason on the same line:\n    "
        + string.Join("\n    ", offenders)
    );
  }

  #endregion
}
