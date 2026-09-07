using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The harness has no shipped-asset guard of its own to catch a rename or an addition drifting from
/// its wiki page (it isn't a mod, so <see cref="WikiParity"/>'s reflection-vs-doc-comment check
/// doesn't cover it), so this stands in: every public type
/// <c>ExpandedLib.Testing</c> declares must be named somewhere on
/// <c>Testing-API-Reference.md</c>, and every type name the page's own "Where things are" table
/// claims must actually exist.
/// </summary>
public class HarnessSurfaceTests {
  private static readonly string ReferencePath = Path.Combine(
    RepoPaths.Mod("exlib"),
    "wiki",
    "Testing-API-Reference.md"
  );

  private static string ReferenceText => File.ReadAllText(ReferencePath);

  /// <summary>Every public top-level type the harness assembly declares, by its bare name - a
  /// generic's backtick arity suffix (<c>ResourceInvariant`1</c>) is stripped, since the page spells
  /// it <c>ResourceInvariant&lt;TState&gt;</c>. Nested types (<c>WikiParity.Report</c> and the like)
  /// are implementation detail of the type that declares them, not a table entry of their own.</summary>
  private static IEnumerable<string> PublicTypeNames() =>
    typeof(TestWorld)
      .Assembly.GetExportedTypes()
      .Where(t => t.DeclaringType == null)
      .Select(t => t.Name)
      .Select(name => name.Contains('`') ? name[..name.IndexOf('`')] : name)
      .Distinct();

  public static IEnumerable<object[]> Types() =>
    PublicTypeNames()
      .OrderBy(n => n, StringComparer.Ordinal)
      .Select(n => new object[] { n });

  [Theory]
  [MemberData(nameof(Types))]
  public void Every_public_type_is_named_on_the_reference_page(string typeName) {
    Assert.True(
      Regex.IsMatch(ReferenceText, $@"\b{Regex.Escape(typeName)}\b"),
      $"{typeName} is public in ExpandedLib.Testing but is not mentioned anywhere in {ReferencePath}"
    );
  }

  [Fact]
  public void Every_type_named_in_the_where_things_are_table_exists() {
    var known = PublicTypeNames().ToHashSet(StringComparer.Ordinal);

    // The "Where things are" table's rows, up to the next blank line - the header row and its
    // markdown separator both carry no backtick token, so no explicit skip is needed.
    string[] lines = ReferenceText
      .Split('\n')
      .SkipWhile(l => !l.StartsWith("## Where things are"))
      .Skip(1)
      .SkipWhile(string.IsNullOrWhiteSpace)
      .TakeWhile(l => !string.IsNullOrWhiteSpace(l))
      .Where(l => l.StartsWith('|'))
      .ToArray();

    // A fully-backtick-enclosed token, optionally generic (`ResourceInvariant<TState>`) - excludes
    // the folder-path column's `World/`, `Rigs/`, ... which never closes its backtick before the
    // slash.
    var claimed = new List<string>();
    foreach (string line in lines)
      foreach (
        Match m in Regex.Matches(line, @"`([A-Za-z][A-Za-z0-9]*)(?:<[^>`]*>)?`")
      )
        claimed.Add(m.Groups[1].Value);

    Assert.NotEmpty(claimed);

    var unknown = claimed.Where(n => !known.Contains(n)).Distinct().ToList();
    Assert.True(
      unknown.Count == 0,
      "Testing-API-Reference.md's \"Where things are\" table names a type that does not exist: "
        + string.Join(", ", unknown)
    );
  }
}
