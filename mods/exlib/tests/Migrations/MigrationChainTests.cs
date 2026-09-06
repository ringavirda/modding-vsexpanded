using System.Collections.Generic;
using ExpandedLib.Migrations;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="BlockMigrationModSystem.FollowChain"/>, the walk that resolves a saved block code through
/// a multi-hop rename history (<c>ppex</c> to <c>iiex</c> to <c>hpex</c>, <c>smex</c> to <c>iiex</c>) to
/// its terminal code. Hops resolve against the declared remap table rather than against the world,
/// because a chain's intermediate code is one that no longer registers.
/// </summary>
public class MigrationChainTests {
  private static AssetLocation L(string code) => new(code);

  private static AssetLocation Walk(
    string from,
    Dictionary<string, string> hops,
    out bool purged,
    out bool overflowed,
    params string[] purges
  ) {
    var removals = new HashSet<string>(purges);
    return BlockMigrationModSystem.FollowChain(
      L(from),
      c => hops.TryGetValue(c.ToString(), out string? n) ? L(n) : null,
      c => removals.Contains(c.ToString()),
      out purged,
      out overflowed
    );
  }

  #region Following a chain

  [Fact]
  public void A_code_with_no_declared_hop_is_its_own_terminal() {
    var terminal = Walk("a:x", [], out bool purged, out bool overflowed);

    Assert.Equal(L("a:x"), terminal);
    Assert.False(purged);
    Assert.False(overflowed);
  }

  [Fact]
  public void A_single_hop_resolves_to_its_target() {
    var hops = new Dictionary<string, string> { ["ppex:x"] = "iiex:x" };

    Assert.Equal(L("iiex:x"), Walk("ppex:x", hops, out _, out _));
  }

  [Fact]
  public void A_multi_hop_chain_resolves_to_the_far_end() {
    // The shipped shape: ppex, then iiex, then hpex. The middle code no longer registers, so it can
    // only be resolved from the table.
    var hops = new Dictionary<string, string> {
      ["ppex:boilerlancashire-north"] = "iiex:boilerlancashire-n",
      ["iiex:boilerlancashire-n"] = "hpex:boilerlancashire-n",
    };

    Assert.Equal(
      L("hpex:boilerlancashire-n"),
      Walk("ppex:boilerlancashire-north", hops, out bool purged, out _)
    );
    Assert.False(purged);
  }

  [Fact]
  public void A_chain_entered_midway_still_reaches_the_far_end() {
    // A world that already ran the first migration holds the middle code, not the oldest one.
    var hops = new Dictionary<string, string> {
      ["ppex:x"] = "iiex:x",
      ["iiex:x"] = "hpex:x",
    };

    Assert.Equal(L("hpex:x"), Walk("iiex:x", hops, out _, out _));
  }

  #endregion

  #region Termination

  [Fact]
  public void A_chain_ending_on_a_purge_reports_purged() {
    var hops = new Dictionary<string, string> {
      ["smex:old"] = "iiex:interim",
      ["iiex:interim"] = "iiex:retired",
    };

    var terminal = Walk(
      "smex:old",
      hops,
      out bool purged,
      out _,
      "iiex:retired"
    );

    Assert.True(purged);
    Assert.Equal(L("iiex:retired"), terminal);
  }

  [Fact]
  public void A_purge_stops_the_walk_rather_than_following_past_it() {
    // A purge wins even where the retired code later gains a hop; walking on would resurrect the
    // block as something else.
    var hops = new Dictionary<string, string> {
      ["a:one"] = "a:two",
      ["a:two"] = "a:three",
    };

    var terminal = Walk("a:one", hops, out bool purged, out _, "a:two");

    Assert.True(purged);
    Assert.Equal(L("a:two"), terminal);
  }

  #endregion

  #region The cycle guard

  [Fact]
  public void A_cycle_overflows_instead_of_spinning() {
    var hops = new Dictionary<string, string> {
      ["a:x"] = "a:y",
      ["a:y"] = "a:x",
    };

    Walk("a:x", hops, out bool purged, out bool overflowed);

    Assert.True(overflowed);
    Assert.False(purged);
  }

  [Fact]
  public void A_self_hop_overflows_rather_than_looping_forever() {
    var hops = new Dictionary<string, string> { ["a:x"] = "a:x" };

    Walk("a:x", hops, out _, out bool overflowed);

    Assert.True(overflowed);
  }

  [Fact]
  public void A_chain_at_the_hop_limit_still_resolves() {
    // Exactly MaxChainHops hops resolve and one more overflows. Both sides are pinned so the guard
    // cannot tighten into rejecting a legitimate rename history.
    var atLimit = new Dictionary<string, string>();
    for (int i = 0; i < BlockMigrationModSystem.MaxChainHops; i++)
      atLimit[$"a:s{i}"] = $"a:s{i + 1}";

    var terminal = Walk("a:s0", atLimit, out _, out bool overflowed);

    Assert.False(overflowed);
    Assert.Equal(L($"a:s{BlockMigrationModSystem.MaxChainHops}"), terminal);

    var overLimit = new Dictionary<string, string>(atLimit) {
      [$"a:s{BlockMigrationModSystem.MaxChainHops}"] =
        $"a:s{BlockMigrationModSystem.MaxChainHops + 1}",
    };

    Walk("a:s0", overLimit, out _, out bool tooFar);
    Assert.True(tooFar);
  }

  #endregion
}
