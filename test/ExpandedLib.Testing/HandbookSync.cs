using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// The handbook authoring pipeline: <c>docs/{domain}/handbook/NN-*.html</c> is the hand-edited source for a
/// handbook page's body, and the shipped copy is one long string under a lang key in
/// <c>assets/{domain}/lang/en.json</c>. Editing prose as HTML is far easier than editing it inside a JSON
/// string literal, so the HTML is where the writing happens - but the copy across was manual, which means it
/// silently stopped happening and the shipped handbook drifted a full rewrite behind the source.
/// <para>
/// This turns that copy into a checked step, deliberately shaped like <see cref="DefinitionGoldens"/>: a
/// parity test fails when a page's shipped text no longer matches its source, and
/// <c>EXLIB_WRITE_HANDBOOK=1</c> re-blesses the lang files from the HTML. Same ergonomics, same source-tree
/// anchor, no build-output copy step.
/// </para>
/// <para>
/// <b>Only the body is synced.</b> A page's <c>title</c> key is a handful of words with no HTML source, so it
/// stays hand-authored in the lang file. Translations are untouched: this writes <c>en.json</c> only, and the
/// existing lang-parity guard still holds because syncing changes values, never the key set.
/// </para>
/// </summary>
public static class HandbookSync
{
  /// <summary>
  /// One handbook page: its authoring HTML, the shipped page descriptor that names its lang key, and the
  /// key itself (already stripped of its <c>domain:</c> prefix).
  /// </summary>
  public sealed record Page(
    string Domain,
    string Number,
    string HtmlPath,
    string DescriptorPath,
    string LangKey
  )
  {
    /// <summary>A stable, serializable id that doubles as the xUnit theory case name.</summary>
    public override string ToString() => $"{Domain}/{Number} [{LangKey}]";
  }

  private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
  private static readonly Regex LeadingNumber = new(@"^\d+", RegexOptions.Compiled);

  /// <summary>True when <c>EXLIB_WRITE_HANDBOOK=1</c> - the opt-in switch the sync test guards on.</summary>
  public static bool WriteRequested =>
    Environment.GetEnvironmentVariable("EXLIB_WRITE_HANDBOOK") == "1";

  #region The transform

  /// <summary>
  /// The authoring HTML as it must appear in the lang file. Two rules, both forced by how the files are
  /// written rather than by choice:
  /// <list type="bullet">
  /// <item>The sources are <b>pre-escaped for pasting into a JSON string</b> - their attribute quotes are
  /// written <c>\"</c> - so the backslashes come back out here and the JSON writer puts its own in.</item>
  /// <item>Line breaks in the source are wrapping, not content (VTML, like HTML, treats runs of whitespace
  /// as one space), so every run collapses to a single space. This is what makes the check stable against a
  /// re-wrap of the same prose.</item>
  /// </list>
  /// </summary>
  public static string Normalize(string html) =>
    Whitespace.Replace(html.Replace("\\\"", "\""), " ").Trim();

  #endregion

  #region Discovery

  /// <summary>Every domain under <c>assets/</c> that ships handbook pages, in order.</summary>
  public static IReadOnlyList<string> Domains()
  {
    string assets = DefinitionGoldens.SolutionRelative("assets");
    if (!Directory.Exists(assets))
      return [];

    return Directory
      .EnumerateDirectories(assets)
      .Where(d => Directory.Exists(Path.Combine(d, "config", "handbook")))
      .Select(d => new DirectoryInfo(d).Name)
      .OrderBy(d => d, StringComparer.Ordinal)
      .ToList();
  }

  /// <summary>
  /// The pages of <paramref name="domain"/> that have both an authoring source and a shipped descriptor,
  /// joined on the <c>NN-</c> ordering prefix the two trees share. The prefix is the join rather than the
  /// whole file name because the slugs already disagree in places (<c>00-advances.html</c> ships as
  /// <c>00-advancedsteelmaking.json</c>), and the ordering number is the one thing both sides must agree on
  /// anyway - it is what decides the page order in game.
  /// </summary>
  public static IReadOnlyList<Page> Pages(string domain)
  {
    var sources = SourcesByNumber(domain);
    var pages = new List<Page>();

    foreach ((string number, string descriptor) in DescriptorsByNumber(domain))
    {
      if (!sources.TryGetValue(number, out string? html))
        continue;
      string? key = LangKeyOf(descriptor);
      if (key != null)
        pages.Add(new Page(domain, number, html, descriptor, key));
    }
    return pages;
  }

  /// <summary>
  /// Everything wrong with <paramref name="domain"/>'s handbook wiring that is not a prose mismatch: a
  /// shipped page with no authoring source (its prose can never be revised), an authoring source that ships
  /// nowhere (writing nobody reads), a descriptor with no <c>text</c> key, and a key absent from
  /// <c>en.json</c> (which renders in game as the raw key). Empty means the two trees line up.
  /// </summary>
  public static IReadOnlyList<string> Problems(string domain)
  {
    var sources = SourcesByNumber(domain);
    var descriptors = DescriptorsByNumber(domain);
    JObject lang = Lang(domain);
    var problems = new List<string>();

    foreach ((string number, string descriptor) in descriptors)
    {
      string name = Path.GetFileName(descriptor);
      if (!sources.ContainsKey(number))
        problems.Add(
          $"{domain}: shipped page {name} has no authoring source docs/{domain}/handbook/{number}-*.html"
        );

      string? key = LangKeyOf(descriptor);
      if (key == null)
        problems.Add($"{domain}: page descriptor {name} declares no 'text' lang key");
      else if (lang[key] == null)
        problems.Add(
          $"{domain}: page {name} points at '{key}', which assets/{domain}/lang/en.json does not define"
        );
    }

    foreach ((string number, string html) in sources)
      if (!descriptors.ContainsKey(number))
        problems.Add(
          $"{domain}: authoring source {Path.GetFileName(html)} ships nowhere - no assets/{domain}/config/handbook/{number}-*.json"
        );

    return problems;
  }

  #endregion

  #region Check + write

  /// <summary>
  /// Checks one page's shipped text against its authoring source. Returns <c>(true, "")</c> on match, else a
  /// message naming the page, both lengths and the first character position that differs with the text
  /// around it - enough to see at a glance whether this is a real revision or a stray edit.
  /// </summary>
  public static (bool ok, string message) Check(Page page)
  {
    string want = Normalize(File.ReadAllText(page.HtmlPath));
    string? have = (string?)Lang(page.Domain)[page.LangKey];

    if (have == want)
      return (true, "");

    string relative = $"docs/{page.Domain}/handbook/{Path.GetFileName(page.HtmlPath)}";
    if (have == null)
      return (false, $"{page}: lang key is missing entirely; {relative} has {want.Length} chars of source");

    return (
      false,
      $"{page}: shipped text ({have.Length} chars) differs from {relative} ({want.Length} chars)\n"
        + $"  first difference at char {FirstDifference(have, want)}:\n"
        + $"    shipped: …{Excerpt(have, FirstDifference(have, want))}…\n"
        + $"    source : …{Excerpt(want, FirstDifference(have, want))}…\n"
        + "  Re-run with EXLIB_WRITE_HANDBOOK=1 to adopt the authoring source."
    );
  }

  /// <summary>
  /// Re-blesses <paramref name="domain"/>'s lang file from its authoring sources, and returns the keys that
  /// actually changed. Writes through Newtonsoft's <see cref="JObject"/>, which preserves key order, so the
  /// diff is confined to the handbook values. Opt-in (call only when <see cref="WriteRequested"/>).
  /// </summary>
  public static IReadOnlyList<string> WriteAll(string domain)
  {
    string path = LangPath(domain);
    if (!File.Exists(path))
      return [];

    string original = File.ReadAllText(path);
    JObject lang = JObject.Parse(original);
    var changed = new List<string>();

    foreach (Page page in Pages(domain))
    {
      string want = Normalize(File.ReadAllText(page.HtmlPath));
      if ((string?)lang[page.LangKey] == want)
        continue;
      lang[page.LangKey] = want;
      changed.Add(page.LangKey);
    }

    if (changed.Count > 0)
      File.WriteAllText(
        path,
        Serialize(lang)
          + (original.EndsWith("\n", StringComparison.Ordinal) ? "\n" : "")
      );
    return changed;
  }

  // The converter array is passed EXPLICITLY, and that is the whole point of this helper. The suite
  // compiles against one Newtonsoft and loads the GAME's at runtime; where the two disagree, the
  // compiler binds the convenience overload (`ToString(Formatting)`, `WriteTo(JsonWriter)`) and the
  // runtime has only the `params JsonConverter[]` form, so the call throws MissingMethodException.
  // Nothing catches that until the write path actually runs - which, for a one-shot re-bless switch,
  // can be a long time. Naming the array binds to the signature both versions really have.
  private static string Serialize(JObject lang) =>
    lang.ToString(Formatting.Indented, Array.Empty<JsonConverter>());

  /// <summary>
  /// The reverse direction, for reconciling the other way: rewrites <paramref name="domain"/>'s authoring
  /// sources from the shipped lang values, and returns the files it changed. Needed because drift has two
  /// causes - prose edited in the HTML and never pasted, or prose edited straight into the lang file - and
  /// only the author knows which side is the good copy. Exporting keeps the in-game text byte-identical and
  /// brings the sources up to date, so the HTML-is-the-source workflow works from then on.
  /// <para>
  /// The result is re-wrapped at <see cref="WrapColumn"/> and re-escaped to the authoring convention, then
  /// checked to <see cref="Normalize"/> back to exactly the value it came from - so an export can never
  /// change what ships.
  /// </para>
  /// </summary>
  public static IReadOnlyList<string> ExportAll(string domain)
  {
    JObject lang = Lang(domain);
    var changed = new List<string>();

    foreach (Page page in Pages(domain))
    {
      var value = (string?)lang[page.LangKey];
      if (value == null)
        continue;

      string html = ToAuthoringHtml(value);
      if (Normalize(html) != value)
        throw new InvalidOperationException(
          $"{page}: export would change the shipped text - refusing to write {page.HtmlPath}"
        );

      if (File.Exists(page.HtmlPath) && Normalize(File.ReadAllText(page.HtmlPath)) == value)
        continue;

      File.WriteAllText(page.HtmlPath, html);
      changed.Add(page.HtmlPath);
    }
    return changed;
  }

  /// <summary>Column the exporter wraps at - narrow enough to diff and edit comfortably.</summary>
  private const int WrapColumn = 78;

  // A lang value as an authoring file: quotes escaped the way the hand-written sources escape them, and
  // greedy word wrap. Breaking only at existing spaces is what makes this round-trip - Normalize collapses
  // any whitespace run back to one space, so a line break inside a tag or an attribute is harmless (the
  // hand-written sources already do it).
  private static string ToAuthoringHtml(string value)
  {
    var lines = new List<string>();
    var line = new System.Text.StringBuilder();

    foreach (string word in value.Replace("\"", "\\\"").Split(' '))
    {
      if (line.Length > 0 && line.Length + 1 + word.Length > WrapColumn)
      {
        lines.Add(line.ToString());
        line.Clear();
      }
      if (line.Length > 0)
        line.Append(' ');
      line.Append(word);
    }
    if (line.Length > 0)
      lines.Add(line.ToString());

    return string.Join("\n", lines) + "\n";
  }

  #endregion

  #region Paths

  private static string LangPath(string domain) =>
    DefinitionGoldens.SolutionRelative($"assets/{domain}/lang/en.json");

  private static JObject Lang(string domain)
  {
    string path = LangPath(domain);
    return File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : [];
  }

  private static SortedDictionary<string, string> SourcesByNumber(string domain) =>
    ByNumber(DefinitionGoldens.SolutionRelative($"docs/{domain}/handbook"), "*.html");

  private static SortedDictionary<string, string> DescriptorsByNumber(string domain) =>
    ByNumber(
      DefinitionGoldens.SolutionRelative($"assets/{domain}/config/handbook"),
      "*.json"
    );

  // Files in `dir` keyed by their leading ordering number. A file without one is skipped rather than
  // guessed at - the number is the whole join, so a page that lacks it cannot be paired either way.
  private static SortedDictionary<string, string> ByNumber(string dir, string pattern)
  {
    var byNumber = new SortedDictionary<string, string>(StringComparer.Ordinal);
    if (!Directory.Exists(dir))
      return byNumber;

    foreach (string file in Directory.EnumerateFiles(dir, pattern))
    {
      Match m = LeadingNumber.Match(Path.GetFileNameWithoutExtension(file));
      if (m.Success)
        byNumber[m.Value] = file;
    }
    return byNumber;
  }

  // The lang key a page descriptor's "text" field names, with its domain prefix stripped.
  private static string? LangKeyOf(string descriptorPath)
  {
    var text = (string?)JObject.Parse(File.ReadAllText(descriptorPath))["text"];
    if (string.IsNullOrEmpty(text))
      return null;
    int colon = text!.IndexOf(':');
    return colon >= 0 ? text[(colon + 1)..] : text;
  }

  #endregion

  #region Diff reporting

  private static int FirstDifference(string a, string b)
  {
    int i = 0;
    while (i < a.Length && i < b.Length && a[i] == b[i])
      i++;
    return i;
  }

  private static string Excerpt(string s, int at, int radius = 60)
  {
    int start = Math.Max(0, at - radius / 2);
    return s.Substring(start, Math.Min(radius, s.Length - start));
  }

  #endregion
}
