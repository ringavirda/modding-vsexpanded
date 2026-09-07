using System.Collections.Generic;
using ExpandedLib.Testing;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The handbook authoring pipeline (<see cref="HandbookSync"/>) over iiex's own domain: prose written
/// as HTML under <c>docs/handbook/</c> must match what ships under the lang key, and the shipped
/// pages and their authoring sources must wire up end to end.
/// </summary>
public class HandbookParityTests {
  [Fact]
  public void Every_shipped_handbook_page_matches_its_authoring_source() {
    var failures = new List<string>();
    foreach (HandbookSync.Page page in HandbookSync.Pages("iiex")) {
      var (ok, message) = HandbookSync.Check(page);
      if (!ok)
        failures.Add(message);
    }

    Assert.True(failures.Count == 0, string.Join("\n", failures));
  }

  [Fact]
  public void Every_handbook_page_is_wired_end_to_end() {
    var problems = HandbookSync.Problems("iiex");
    Assert.True(problems.Count == 0, string.Join("\n", problems));
  }

  [Fact]
  public void Iiex_ships_at_least_one_handbook_page() {
    // The two rules above pass trivially if the domain stops shipping a handbook - a renamed
    // docs/handbook/ or config/handbook/ folder would otherwise read as "every page is in sync".
    Assert.NotEmpty(HandbookSync.Pages("iiex"));
  }
}
