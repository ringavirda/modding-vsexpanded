using System.Collections.Generic;
using ExpandedLib.Checks;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="MultiblockCodesCheck.AnyProvides"/> against the wildcard shapes real layouts actually
/// write: a `*` segment standing in for one whole variant ("any tier", "any side") and a glued `*`
/// suffix standing in for an open prefix within one segment.
/// </summary>
public class MultiblockCodesCheckTests {
  [Fact]
  public void A_star_segment_matches_any_single_segment_in_that_position() {
    var registered = new HashSet<string>
    {
      "furnace-blastcore-tier1-n",
      "furnace-blastcore-tier1-e",
    };

    Assert.True(
      MultiblockCodesCheck.AnyProvides(registered, "furnace-blastcore-*-n")
    );
    Assert.False(
      MultiblockCodesCheck.AnyProvides(registered, "furnace-blastcore-*-s")
    );
  }

  [Fact]
  public void Two_star_segments_each_match_independently() {
    var registered = new HashSet<string> { "furnace-firebox-tier2-w" };

    Assert.True(
      MultiblockCodesCheck.AnyProvides(registered, "furnace-firebox-*-*")
    );
  }

  [Fact]
  public void A_glued_star_matches_by_prefix_within_one_segment() {
    var registered = new HashSet<string> { "convertercontrol-e" };

    Assert.True(
      MultiblockCodesCheck.AnyProvides(registered, "convertercontrol*")
    );
    Assert.False(
      MultiblockCodesCheck.AnyProvides(registered, "convertertransmission*")
    );
  }

  [Fact]
  public void A_trailing_wildcard_segment_still_matches_a_bare_registered_code() {
    var registered = new HashSet<string> { "furnace-tuyere-n" };

    Assert.True(
      MultiblockCodesCheck.AnyProvides(registered, "furnace-tuyere-*")
    );
  }
}
