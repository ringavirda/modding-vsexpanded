using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Repo-wide rule for a block entity that hosts a production process: it must round-trip the process's
/// last-tick stamp, or opt out with <c>no away-catch-up:</c> and a reason. The stamp is the only thing
/// the catch-up measures an absence against, and the behaviour cannot save it - vanilla fans a
/// behaviour's tree into the block entity's own flat tree, so exactly one of the two may write the key
/// and it is the host.
/// <para>
/// The failure is silent both ways: a host that saves nothing has <c>MaxAwayCatchupSteps</c> honoured
/// and replays nothing, and one that saves it twice loses a writer without a word. Neither shows up in
/// a machine's own tests, which is why this is a corpus rule rather than an assertion on one class.
/// </para>
/// </summary>
public class AwayCatchupStampTests {
  #region Corpus

  // The base-list form: every host declares its process as a nested subclass, so the file that defines
  // a process is the file that must decide what happens to its stamp.
  private static readonly Regex HostsAProcess = new(
    @":\s*BEBehaviorProductionMachine\s*\(",
    RegexOptions.Compiled
  );

  private const string StampKey = "pm_lastHours";
  private const string OptOut = "no away-catch-up:";

  private static IEnumerable<string> SourceFiles(string dir) {
    string full = Path.Combine(RepoRoot(), dir);
    if (!Directory.Exists(full))
      yield break;
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
  public void A_process_host_saves_the_stamp_its_catch_up_measures_from() {
    var offenders = new List<string>();
    int seen = 0;
    foreach (string f in SourceFiles("src")) {
      string text = File.ReadAllText(f);
      if (!HostsAProcess.IsMatch(text))
        continue;
      seen++;
      if (text.Contains(StampKey, StringComparison.Ordinal))
        continue;
      if (text.Contains(OptOut, StringComparison.Ordinal))
        continue;
      offenders.Add(Rel(f));
    }

    Assert.True(
      seen > 0,
      "Found no production-process hosts at all - the source walk is wrong."
    );
    Assert.True(
      offenders.Count == 0,
      "These host a production process but neither save nor restore its \""
        + StampKey
        + "\" stamp, so away-catch-up is dead on them however many steps they allow. Round-trip it "
        + "in ToTreeAttributes/FromTreeAttributes, or mark the file with \""
        + OptOut
        + " <reason>\":\n    "
        + string.Join("\n    ", offenders)
    );
  }

  [Fact]
  public void A_no_catch_up_opt_out_sits_on_a_host_and_states_its_reason() {
    // A marker outlives the process it explains when the host moves or the class is split, and the rule
    // above reads any file carrying one as exempt - so a stale marker quietly exempts whatever host
    // lands in that file next.
    var stranded = new List<string>();
    var unexplained = new List<string>();
    int marked = 0;
    foreach (string f in SourceFiles("src")) {
      string text = File.ReadAllText(f);
      if (!text.Contains(OptOut, StringComparison.Ordinal))
        continue;
      marked++;
      if (!HostsAProcess.IsMatch(text))
        stranded.Add(Rel(f));
      foreach (string line in File.ReadAllLines(f)) {
        int at = line.IndexOf(OptOut, StringComparison.Ordinal);
        if (at >= 0 && line[(at + OptOut.Length)..].Trim().Length < 10)
          unexplained.Add(Rel(f));
      }
    }

    Assert.True(
      marked > 0,
      "Found no opt-out markers at all - the source walk is wrong."
    );
    Assert.True(
      stranded.Count == 0,
      "These carry the \""
        + OptOut
        + "\" marker but host no production process, so it explains nothing and exempts the file "
        + "from the rule above. Move it to whatever hosts the process now:\n    "
        + string.Join("\n    ", stranded)
    );
    Assert.True(
      unexplained.Count == 0,
      "The opt-out marker must be followed by the reason on the same line:\n    "
        + string.Join("\n    ", unexplained)
    );
  }

  #endregion
}
