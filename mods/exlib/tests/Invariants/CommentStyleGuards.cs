using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Repo-wide comment style, enforced across every mod rather than trusted to review. The rules here
/// are only the mechanically unambiguous ones: prose tests on words like "we" or "used to" produce
/// false positives on legitimate vocabulary (<c>we</c> is the west-east orientation token, and
/// "used to swap in a renderer" is ordinary English), so judgment-based rules stay in
/// <c>CONTRIBUTING.md</c> for a reviewer.
/// <para>
/// The size limits are regression backstops, not targets. CONTRIBUTING asks for a 6-line class doc;
/// these fail only at the point where a doc has clearly become an essay again.
/// </para>
/// </summary>
public class CommentStyleGuards {
  #region Corpus

  private const int MaxDocBlockLines = 16;
  private const int MaxParaPerDocBlock = 3;

  private static readonly Regex CommentLine = new(
    @"^\s*(///|//)",
    RegexOptions.Compiled
  );
  private static readonly Regex XmlDocLine = new(
    @"^\s*///",
    RegexOptions.Compiled
  );

  private sealed record SourceFile(
    string Path,
    string Relative,
    string[] Lines
  );

  private static IReadOnlyList<SourceFile> Sources() {
    string root = RepoRoot();
    var files = new List<SourceFile>();
    string mods = Path.Combine(root, "mods");
    foreach (
      string path in Directory.EnumerateFiles(
        mods,
        "*.cs",
        SearchOption.AllDirectories
      )
    ) {
      // Generated sources are not hand-authored, so the style rules do not apply to them.
      if (path.EndsWith(".g.cs", StringComparison.Ordinal))
        continue;
      string rel = Path.GetRelativePath(root, path).Replace('\\', '/');
      if (
        rel.Contains("/bin/", StringComparison.Ordinal)
        || rel.Contains("/obj/", StringComparison.Ordinal)
      )
        continue;
      files.Add(new SourceFile(path, rel, File.ReadAllLines(path)));
    }
    Assert.True(
      files.Count > 0,
      "Found no C# sources to check - the repo-root walk is wrong."
    );
    return files;
  }

  // Every comment line in the repo, as "<relative path>:<1-based line>" plus its text.
  private static IEnumerable<(string Where, string Text)> CommentLines() =>
    from f in Sources()
    from i in Enumerable.Range(0, f.Lines.Length)
    where CommentLine.IsMatch(f.Lines[i])
    select ($"{f.Relative}:{i + 1}", f.Lines[i]);

  private static string Report(string rule, IEnumerable<string> hits) {
    var list = hits.ToList();
    return $"{rule}\n  {list.Count} violation(s):\n"
      + string.Join("\n", list.Take(25).Select(h => "    " + h))
      + (list.Count > 25 ? $"\n    ... and {list.Count - 25} more" : "");
  }

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

  #region Typography

  [Fact]
  public void Comments_use_a_hyphen_not_an_em_dash() {
    var hits = CommentLines()
      .Where(c => c.Text.Contains('—'))
      .Select(c => c.Where)
      .ToList();
    Assert.True(
      hits.Count == 0,
      Report("Use '-' instead of an em dash in comments.", hits)
    );
  }

  [Fact]
  public void Xml_docs_do_not_use_html_emphasis() {
    var tag = new Regex(@"</?(b|i|em|strong)>", RegexOptions.IgnoreCase);
    var hits = CommentLines()
      .Where(c => tag.IsMatch(c.Text))
      .Select(c => c.Where)
      .ToList();
    Assert.True(
      hits.Count == 0,
      Report(
        "Drop <b>/<i>/<em>/<strong> from doc comments - state the fact plainly instead.",
        hits
      )
    );
  }

  [Fact]
  public void Comments_carry_no_marker_glyphs() {
    // The house style that grew during bring-up used these to rank remarks. They read as generated
    // and carry no information a sentence cannot.
    var markers = new[] { '★', '⛔', '⚠', '✅', 'ⓘ', '❗', '⭐' };
    var hits = CommentLines()
      .Where(c => c.Text.IndexOfAny(markers) >= 0)
      .Select(c => c.Where)
      .ToList();
    Assert.True(
      hits.Count == 0,
      Report(
        "Remove marker glyphs (star, no-entry, warning) from comments.",
        hits
      )
    );
  }

  [Fact]
  public void Comments_do_not_open_with_filler() {
    var filler = new Regex(
      @"(^|\s)(Note that|It is worth noting|Importantly|Crucially|Remember that)\b",
      RegexOptions.IgnoreCase
    );
    var hits = CommentLines()
      .Where(c => filler.IsMatch(c.Text))
      .Select(c => c.Where)
      .ToList();
    Assert.True(
      hits.Count == 0,
      Report("Drop the filler opener and state the fact directly.", hits)
    );
  }

  #endregion

  #region Size

  // Consecutive /// lines form one doc block.
  private static IEnumerable<(string Where, int Lines, int Paras)> DocBlocks() {
    foreach (var f in Sources()) {
      int run = 0,
        start = 0,
        paras = 0;
      for (int i = 0; i <= f.Lines.Length; i++) {
        bool isDoc = i < f.Lines.Length && XmlDocLine.IsMatch(f.Lines[i]);
        if (isDoc) {
          if (run == 0)
            start = i + 1;
          run++;
          paras += Regex.Matches(f.Lines[i], "<para>").Count;
        } else if (run > 0) {
          yield return ($"{f.Relative}:{start}", run, paras);
          run = 0;
          paras = 0;
        }
      }
    }
  }

  [Fact]
  public void No_doc_comment_has_grown_back_into_an_essay() {
    var hits = DocBlocks()
      .Where(b => b.Lines > MaxDocBlockLines)
      .Select(b => $"{b.Where} ({b.Lines} lines)")
      .ToList();
    Assert.True(
      hits.Count == 0,
      Report(
        $"A doc comment over {MaxDocBlockLines} lines is an essay. Keep the constraint, move the "
          + "rationale to docs/design and cite it. CONTRIBUTING.md asks for 6 lines on a class.",
        hits
      )
    );
  }

  [Fact]
  public void No_doc_comment_stacks_more_than_three_paragraphs() {
    var hits = DocBlocks()
      .Where(b => b.Paras > MaxParaPerDocBlock)
      .Select(b => $"{b.Where} ({b.Paras} <para>)")
      .ToList();
    Assert.True(
      hits.Count == 0,
      Report(
        $"More than {MaxParaPerDocBlock} <para> blocks means the doc is arguing rather than "
          + "describing. Each <para> should state a separate constraint.",
        hits
      )
    );
  }

  #endregion
}
