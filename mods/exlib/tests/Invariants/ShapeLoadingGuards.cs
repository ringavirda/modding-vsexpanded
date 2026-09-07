using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// How shape assets may be loaded, enforced across every mod. Both rules here were live defects the
/// vendored game source exposed, and neither shows up at runtime until something goes wrong on the
/// tesselation thread, so they are pinned mechanically rather than left to review.
/// <para>
/// This is a source-text scan, not a behavioural one: it stops the pattern coming back, it cannot
/// tell whether a given load is correct.
/// </para>
/// </summary>
public class ShapeLoadingGuards {
  #region Corpus

  private sealed record SourceFile(string Relative, string Text);

  private static IReadOnlyList<SourceFile> Production() {
    string root = RepoRoot();
    var files = new List<SourceFile>();

    foreach (
      string mod in Directory.EnumerateDirectories(Path.Combine(root, "mods"))
    ) {
      string src = Path.Combine(mod, "src");
      if (!Directory.Exists(src))
        continue;

      foreach (
        string path in Directory.EnumerateFiles(
          src,
          "*.cs",
          SearchOption.AllDirectories
        )
      ) {
        string rel = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (
          rel.Contains("/bin/", StringComparison.Ordinal)
          || rel.Contains("/obj/", StringComparison.Ordinal)
          || path.EndsWith(".g.cs", StringComparison.Ordinal)
        )
          continue;
        files.Add(new SourceFile(rel, File.ReadAllText(path)));
      }
    }

    Assert.True(
      files.Count > 0,
      "Found no C# sources under mods/*/src - the repo-root walk is wrong, and every rule below would "
        + "pass by scanning nothing."
    );
    return files;
  }

  // Whole-file matching rather than line-by-line: the formatter breaks a fluent chain across lines, so a
  // per-line scan would miss exactly the spellings csharpier produces.
  private static string[] Offenders(Regex pattern) =>
    [
      .. Production()
        .SelectMany(f =>
          pattern
            .Matches(f.Text)
            .Select(m => $"{f.Relative}:{LineOf(f.Text, m.Index)}")
        ),
    ];

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
  public void No_shape_is_deserialized_straight_off_the_asset() {
    // Assets.TryGet(loc).ToObject<Shape>() throws on a malformed shape file, and the calls that matter
    // run on the tesselation thread, where the exception surfaces detached from the block that caused
    // it. Shape.TryGet wraps the same call: it catches, names the offending file in the log, and sets
    // ShapeElement.locationForLogging first so a per-element warning names it too. ExMeshCache.LoadShape
    // is the way in.
    string[] offenders = Offenders(new Regex(@"ToObject\s*<\s*Shape\s*>\s*\("));

    Assert.True(
      offenders.Length == 0,
      "Load shapes through ExMeshCache.LoadShape (Shape.TryGet), not a raw ToObject<Shape>():\n  "
        + string.Join("\n  ", offenders)
    );
  }

  [Fact]
  public void A_blocktypes_own_shape_path_is_never_rewritten_in_place() {
    // WithPathPrefixOnce and WithPathAppendixOnce mutate the receiver and return it, so calling either
    // on Block.Shape.Base permanently rewrites the path for every instance of that blocktype. For an MP
    // machine that also changes its CompositeShape's hash, which is what MechNetworkRenderer pools its
    // renderers on, so the device silently migrates to a second bucket with its own uploaded mesh.
    // Cloning first is what breaks the chain, and ExMeshCache.ShapePathOf does it.
    string[] offenders = Offenders(
      new Regex(@"Shape\s*\.\s*Base\s*\.\s*WithPath")
    );

    Assert.True(
      offenders.Length == 0,
      "Clone the location before prefixing it (ExMeshCache.ShapePathOf):\n  "
        + string.Join("\n  ", offenders)
    );
  }

  #endregion
}
