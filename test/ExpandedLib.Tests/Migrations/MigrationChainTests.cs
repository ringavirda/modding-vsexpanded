using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="BlockMigrationModSystem.FollowChain"/> - the walk that decides whether a save from
/// several renames ago survives.
/// <para>
/// <b>Why chaining had to exist.</b> The remap table used to resolve each declared pair against the
/// world as it collected it, so it could only ever honour <b>one hop</b>. Every multi-hop history in
/// this repo - <c>ppex → lpex → hpex</c>, <c>smex → iwex → renamed</c> - therefore lost its oldest
/// worlds silently, because a chain's intermediate code is by definition the one that no longer
/// registers, and <c>BuildRemapTable</c> drops an unresolvable pair with nothing but a
/// <c>Logger.Warning</c>.
/// </para>
/// </summary>
public class MigrationChainTests
{
  private static AssetLocation L(string code) => new(code);

  private static AssetLocation Walk(
    string from,
    Dictionary<string, string> hops,
    out bool purged,
    out bool overflowed,
    params string[] purges
  )
  {
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
  public void A_code_with_no_declared_hop_is_its_own_terminal()
  {
    var terminal = Walk("a:x", [], out bool purged, out bool overflowed);

    Assert.Equal(L("a:x"), terminal);
    Assert.False(purged);
    Assert.False(overflowed);
  }

  [Fact]
  public void A_single_hop_resolves_to_its_target()
  {
    var hops = new Dictionary<string, string> { ["ppex:x"] = "lpex:x" };

    Assert.Equal(L("lpex:x"), Walk("ppex:x", hops, out _, out _));
  }

  [Fact]
  public void A_multi_hop_chain_resolves_to_the_far_end()
  {
    // The real shape: ppex → lpex → hpex. The middle code is dead, which is exactly why resolving
    // per-hop against the world could never work.
    var hops = new Dictionary<string, string>
    {
      ["ppex:boilerlancashire-north"] = "lpex:boilerlancashire-n",
      ["lpex:boilerlancashire-n"] = "hpex:boilerlancashire-n",
    };

    Assert.Equal(
      L("hpex:boilerlancashire-n"),
      Walk("ppex:boilerlancashire-north", hops, out bool purged, out _)
    );
    Assert.False(purged);
  }

  [Fact]
  public void A_chain_entered_midway_still_reaches_the_far_end()
  {
    // A world that already ran the first migration holds the middle code, not the oldest one.
    var hops = new Dictionary<string, string>
    {
      ["ppex:x"] = "lpex:x",
      ["lpex:x"] = "hpex:x",
    };

    Assert.Equal(L("hpex:x"), Walk("lpex:x", hops, out _, out _));
  }

  #endregion

  #region Termination

  [Fact]
  public void A_chain_ending_on_a_purge_reports_purged()
  {
    var hops = new Dictionary<string, string>
    {
      ["smex:old"] = "iwex:interim",
      ["iwex:interim"] = "iwex:retired",
    };

    var terminal = Walk("smex:old", hops, out bool purged, out _, "iwex:retired");

    Assert.True(purged);
    Assert.Equal(L("iwex:retired"), terminal);
  }

  [Fact]
  public void A_purge_stops_the_walk_rather_than_following_past_it()
  {
    // Should a retired code later gain a hop, the purge still wins - the block is gone, and walking
    // on would resurrect it as something else.
    var hops = new Dictionary<string, string>
    {
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
  public void A_cycle_overflows_instead_of_spinning()
  {
    var hops = new Dictionary<string, string>
    {
      ["a:x"] = "a:y",
      ["a:y"] = "a:x",
    };

    Walk("a:x", hops, out bool purged, out bool overflowed);

    Assert.True(overflowed);
    Assert.False(purged);
  }

  [Fact]
  public void A_self_hop_overflows_rather_than_looping_forever()
  {
    var hops = new Dictionary<string, string> { ["a:x"] = "a:x" };

    Walk("a:x", hops, out _, out bool overflowed);

    Assert.True(overflowed);
  }

  [Fact]
  public void A_chain_at_the_hop_limit_still_resolves()
  {
    // Exactly MaxChainHops hops must succeed; one more must overflow. Pinning both sides keeps the
    // guard from being quietly tightened into rejecting a legitimate rename history.
    var atLimit = new Dictionary<string, string>();
    for (int i = 0; i < BlockMigrationModSystem.MaxChainHops; i++)
      atLimit[$"a:s{i}"] = $"a:s{i + 1}";

    var terminal = Walk("a:s0", atLimit, out _, out bool overflowed);

    Assert.False(overflowed);
    Assert.Equal(L($"a:s{BlockMigrationModSystem.MaxChainHops}"), terminal);

    var overLimit = new Dictionary<string, string>(atLimit)
    {
      [$"a:s{BlockMigrationModSystem.MaxChainHops}"] =
        $"a:s{BlockMigrationModSystem.MaxChainHops + 1}",
    };

    Walk("a:s0", overLimit, out _, out bool tooFar);
    Assert.True(tooFar);
  }

  #endregion
}
