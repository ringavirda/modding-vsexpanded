using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Guards the handbook authoring pipeline: prose is written as HTML under <c>docs/{domain}/handbook/</c> and
/// ships as one long string under a lang key, and the copy across used to be a manual paste. It stopped
/// happening - by the time this guard was written every lpex and smex page had drifted a full rewrite behind
/// its source, silently, because nothing compared the two. Now the paste is a command
/// (<c>EXLIB_WRITE_HANDBOOK=1</c>) and forgetting it is a failing test.
/// <para>
/// Discovers domains from the source tree, so a new mod's handbook is covered the moment it ships a page.
/// See <see cref="HandbookSync"/> for the transform and what it deliberately leaves alone (titles,
/// translations).
/// </para>
/// </summary>
public class HandbookParityTests
{
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
  )
  {
    HandbookSync.Page page = HandbookSync
      .Pages(domain)
      .Single(p => p.Number == number);

    var (ok, message) = HandbookSync.Check(page);

    Assert.True(ok, message);
  }

  [Theory]
  [MemberData(nameof(AllDomains))]
  public void Every_handbook_page_is_wired_end_to_end(string domain)
  {
    // Not prose: the plumbing. A shipped page with no source can never be revised, a source that ships
    // nowhere is writing nobody reads, and a descriptor pointing at an undefined key renders the raw key
    // in game. None of those are visible from the parity check above, because that only runs on pages
    // where both halves already exist.
    IReadOnlyList<string> problems = HandbookSync.Problems(domain);

    Assert.True(problems.Count == 0, string.Join("\n", problems));
  }

  /// <summary>
  /// The paste, as a command. Set <c>EXLIB_WRITE_HANDBOOK=1</c> and run this suite to adopt every edited
  /// authoring source into its lang file, then run again without it to confirm parity - exactly the
  /// two-step the golden re-bless uses (test order inside a single run is not guaranteed, so the
  /// confirming run is a separate one).
  /// </summary>
  [Fact]
  public void Regenerate_handbook_text_when_requested()
  {
    if (!HandbookSync.WriteRequested)
      return;

    foreach (string domain in HandbookSync.Domains())
      HandbookSync.WriteAll(domain);
  }

  /// <summary>
  /// The reconciliation for the other kind of drift - prose edited straight into a lang file, which the
  /// import above would silently throw away. <c>EXLIB_EXPORT_HANDBOOK=1</c> rewrites the authoring sources
  /// from the shipped text instead, leaving what players read untouched. Which switch to reach for is an
  /// editorial call, so neither runs by default.
  /// </summary>
  [Fact]
  public void Export_handbook_text_to_sources_when_requested()
  {
    if (System.Environment.GetEnvironmentVariable("EXLIB_EXPORT_HANDBOOK") != "1")
      return;

    foreach (string domain in HandbookSync.Domains())
      HandbookSync.ExportAll(domain);
  }
}
