using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Repo-wide filler-cleanup rule: a block that calls one of the <see cref="CleanupCalls"/> helpers from
/// <c>OnBlockBroken</c> must also call that same helper from the <c>Block</c> overload of
/// <c>OnBlockRemoved(IWorldAccessor, BlockPos)</c> - not merely declare the override - or the cleanup
/// survives any removal path that is not a player break. <c>Block.OnBlockExploded</c> sets the block to
/// air through the bulk accessor and never calls <c>OnBlockBroken</c>, so a call reachable only from
/// there leaves its cells behind after an explosion or a worldedit delete. The check reads each
/// method's own body (brace-matched), so a file that clears its footprint on removal but still leaves a
/// second helper - an axle bus, a network node - break-only cannot satisfy it by merely having an
/// <c>OnBlockRemoved</c> override elsewhere. Matches
/// <c>BlockFilledMegastructure.OnBlockRemoved</c>, the shape every filler host should converge on once
/// each can spare a base class for it.
/// </summary>
public class FillerCleanupHookTests {
  #region Corpus

  /// <summary>Cleanup calls known to leave orphan cells behind if they run only on a player break.
  /// Add to this list, not to the check itself, when a new break-only cleanup helper turns up.</summary>
  private static readonly string[] CleanupCalls =
  [
    "RemoveFillers(",
    "RemoveAxleNodes(",
  ];

  private static readonly Regex BrokenSignature = new(
    @"override\s+void\s+OnBlockBroken\s*\(",
    RegexOptions.Compiled
  );

  // The Block overload specifically: OnBlockRemoved(IWorldAccessor, BlockPos). The BlockEntity overload
  // takes no arguments and is a different method entirely (see TeardownSymmetryTests) - matching it here
  // would let a file satisfy this guard with the wrong OnBlockRemoved.
  private static readonly Regex RemovedBlockSignature = new(
    @"override\s+void\s+OnBlockRemoved\s*\(\s*IWorldAccessor",
    RegexOptions.Compiled
  );

  /// <summary>The brace-matched body of the first method whose signature matches
  /// <paramref name="signature"/>, or null if the signature or its opening brace is not found.</summary>
  private static string? MethodBody(string text, Regex signature) {
    Match m = signature.Match(text);
    if (!m.Success)
      return null;
    int start = text.IndexOf('{', m.Index);
    if (start < 0)
      return null;
    int depth = 0;
    for (int i = start; i < text.Length; i++) {
      if (text[i] == '{')
        depth++;
      else if (text[i] == '}' && --depth == 0)
        return text[start..(i + 1)];
    }
    return null;
  }

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

  [Fact]
  public void Filler_cleanup_hangs_off_removal_not_breaking() {
    // Block.OnBlockExploded sets the block to air through the bulk accessor and never calls
    // OnBlockBroken, so a cleanup call reachable only from OnBlockBroken's own body leaves its cells
    // behind. Checking method bodies, not file-wide substrings, is what catches a file that fixed one
    // cleanup helper (RemoveFillers, in OnBlockRemoved) while leaving a second one (RemoveAxleNodes)
    // stranded in OnBlockBroken: the earlier file-wide check saw the OnBlockRemoved override and
    // stopped looking, certifying the file clean.
    var offenders = new List<string>();
    foreach (string f in SourceFiles("src")) {
      string text = File.ReadAllText(f);
      // The call syntax, not a bare mention: BlockStructureFiller's OnBlockBroken names
      // RemoveFillers only in a comment describing the principal's cleanup, never calls it.
      bool mentionsAny = false;
      foreach (string call in CleanupCalls) {
        if (text.Contains(call)) {
          mentionsAny = true;
          break;
        }
      }
      if (!mentionsAny)
        continue;

      string? brokenBody = MethodBody(text, BrokenSignature);
      if (brokenBody == null)
        continue;

      string? removedBody = MethodBody(text, RemovedBlockSignature);
      foreach (string call in CleanupCalls) {
        if (!brokenBody.Contains(call))
          continue;
        if (removedBody != null && removedBody.Contains(call))
          continue;
        offenders.Add(Rel(f) + " (" + call.TrimEnd('(') + ")");
        break;
      }
    }

    Assert.True(
      offenders.Count == 0,
      "these clear their footprint only on a player break: "
        + string.Join(", ", offenders)
    );
  }
}
