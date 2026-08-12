using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The rule the whole extension layer exists to make real: a machine reads its tooling and names no
/// product. Every hard-coded product code is a place a third party has to fork instead of declare, and
/// nothing about it fails at runtime - the machine simply makes the one thing its author thought of.
/// <para>
/// A source-text scan, in the shape of <see cref="ShapeLoadingGuards"/>: it stops the pattern coming back,
/// it cannot tell whether a given lookup is correct.
/// See docs/design/mechanics/process-extension.md.
/// </para>
/// </summary>
public class ProcessExtensionGuards {
  #region Corpus

  private sealed record SourceFile(string Relative, string Text);

  // What makes a file a process machine is that it reads a process registry. Deriving the corpus that way
  // rather than from a hand-kept list means a machine adopting a registry joins this guard by doing so,
  // and cannot be added to the codebase outside its reach.
  private static readonly Regex ReadsARegistry = new(
    @"StageLadderRegistry|ProcessJobRegistry|ProcessExtensions|MillSchedule|RollSetSpec|MoldSpec",
    RegexOptions.Compiled
  );

  // A literal code is banned; an art path is not. Shapes and textures are the machine's own appearance,
  // which no registry owns and no modder extends by declaring a stage.
  private static readonly Regex LiteralCode = new(
    """new\s+AssetLocation\s*\(\s*"(?<code>[a-z][a-z0-9]*:[^"]*)"\s*\)""",
    RegexOptions.Compiled
  );

  private static IReadOnlyList<SourceFile> ProcessMachines() {
    string root = RepoRoot();
    var files = new List<SourceFile>();

    foreach (
      string path in Directory.EnumerateFiles(
        Path.Combine(root, "src"),
        "*.cs",
        SearchOption.AllDirectories
      )
    ) {
      string rel = Path.GetRelativePath(root, path).Replace('\\', '/');
      if (
        rel.Contains("/bin/", StringComparison.Ordinal)
        || rel.Contains("/obj/", StringComparison.Ordinal)
        || path.EndsWith(".g.cs", StringComparison.Ordinal)
        // The registries and the spec types name the contract itself, which is the one place a code may
        // legitimately be written down - and the emitter's whole job is to build items from declared ones.
        || rel.Contains("/Processes/", StringComparison.Ordinal)
      )
        continue;

      string text = File.ReadAllText(path);
      if (ReadsARegistry.IsMatch(text))
        files.Add(new SourceFile(rel, text));
    }
    return files;
  }

  private static bool IsArtPath(string code) =>
    code.Contains("shapes/", StringComparison.OrdinalIgnoreCase)
    || code.Contains("textures/", StringComparison.OrdinalIgnoreCase)
    || code.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

  private static int LineOf(string text, int index) =>
    text.Take(index).Count(c => c == '\n') + 1;

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

  #region Rules

  [Fact]
  public void The_corpus_is_not_empty() {
    // Without this the rule below passes by scanning nothing, which is how a guard reads green for a year
    // after the pattern it watched for moved to a folder it no longer walks.
    IReadOnlyList<SourceFile> machines = ProcessMachines();

    Assert.True(
      machines.Count > 0,
      "Found no process-machine sources under src/ - the corpus rule is wrong and the guard below is "
        + "checking nothing."
    );
    Assert.Contains(
      machines,
      f =>
        f.Relative.EndsWith(
          "BlockEntityRollingMill.cs",
          StringComparison.Ordinal
        )
    );
  }

  [Fact]
  public void No_process_machine_names_a_product_in_code() {
    string[] offenders =
    [
      .. ProcessMachines()
        .SelectMany(f =>
          LiteralCode
            .Matches(f.Text)
            .Where(m => !IsArtPath(m.Groups["code"].Value))
            .Select(m =>
              $"{f.Relative}:{LineOf(f.Text, m.Index)} names \"{m.Groups["code"].Value}\""
            )
        ),
    ];

    Assert.True(
      offenders.Length == 0,
      "A machine reads its tooling and names no product. Declare it in the machine's registry instead:\n  "
        + string.Join("\n  ", offenders)
    );
  }

  #endregion
}
