using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Parity guard over the handbook authoring pipeline: prose is written as HTML under
/// <c>docs/{domain}/handbook/</c> and ships as one string under a lang key, and the two must agree.
/// Setting <c>EXLIB_WRITE_HANDBOOK=1</c> adopts the sources into the lang files. Domains are discovered
/// from the source tree, so a new mod's handbook is covered as soon as it ships a page. See
/// <see cref="HandbookSync"/> for the transform and what it leaves alone (titles, translations).
/// </summary>
public class HandbookParityTests {
  /// <summary>One case per handbook page across every domain that ships one.</summary>
  public static IEnumerable<object[]> AllPages() =>
    HandbookSync
      .Domains()
      .SelectMany(d => HandbookSync.Pages(d))
      .Select(p => new object[] { p.Domain, p.Number });

  /// <summary>One case per domain that ships handbook pages.</summary>
  public static IEnumerable<object[]> AllDomains() =>
    HandbookSync.Domains().Select(d => new object[] { d });

  [Theory]
  [MemberData(nameof(AllPages))]
  public void Every_shipped_handbook_page_matches_its_authoring_source(
    string domain,
    string number
  ) {
    HandbookSync.Page page = HandbookSync
      .Pages(domain)
      .Single(p => p.Number == number);

    var (ok, message) = HandbookSync.Check(page);

    Assert.True(ok, message);
  }

  [Theory]
  [MemberData(nameof(AllDomains))]
  public void Every_handbook_page_is_wired_end_to_end(string domain) {
    // Checks the wiring rather than the prose: a shipped page with no source, a source that ships
    // nowhere, and a descriptor pointing at an undefined key (which renders the raw key in game). The
    // parity check above sees none of these, since it only runs where both halves exist.
    IReadOnlyList<string> problems = HandbookSync.Problems(domain);

    Assert.True(problems.Count == 0, string.Join("\n", problems));
  }

  /// <summary>
  /// Adopts every edited authoring source into its lang file when <c>EXLIB_WRITE_HANDBOOK=1</c> is set.
  /// Confirming parity needs a second run with the variable unset, because test order within one run is
  /// not guaranteed.
  /// </summary>
  [Fact]
  public void Regenerate_handbook_text_when_requested() {
    if (!HandbookSync.WriteRequested)
      return;

    foreach (string domain in HandbookSync.Domains())
      HandbookSync.WriteAll(domain);
  }

  /// <summary>
  /// The reverse direction, for prose edited straight into a lang file, which the import above would
  /// otherwise overwrite. <c>EXLIB_EXPORT_HANDBOOK=1</c> rewrites the authoring sources from the shipped
  /// text and leaves the lang files untouched. Neither direction runs by default.
  /// </summary>
  [Fact]
  public void Export_handbook_text_to_sources_when_requested() {
    if (
      System.Environment.GetEnvironmentVariable("EXLIB_EXPORT_HANDBOOK") != "1"
    )
      return;

    foreach (string domain in HandbookSync.Domains())
      HandbookSync.ExportAll(domain);
  }
}
