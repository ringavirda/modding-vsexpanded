using IronworkingExpanded;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// <b>The guard-rail the design specified and nobody built.</b>
/// <c>docs/design/processes/roasting.md</c> § 7 names exactly one invariant worth pinning — <i>"the iwex chain
/// must never yield less iron per ore than a vanilla bloomery"</i> — and calls it <i>"the single test that
/// stops a recovery rebalance quietly turning the mod into a downgrade"</i>. It did not exist.
/// <para>
/// It matters most right now: the charge model is being recalibrated from coal-pile <em>items</em> onto the
/// metal unit scale, and a recovery number that drifts during that has no other alarm. Every constant it
/// touches is live-editable config, so a mistuned server is a downgrade with no symptom but a slow player.
/// </para>
/// <para>
/// <b>Recovery is not stored anywhere as a rate.</b> It is implied by three numbers that were tuned
/// independently, which is exactly why it could drift unnoticed - this file is where it becomes one derived
/// figure with a floor under it.
/// </para>
/// </summary>
public class OreRecoveryGuardRailTests
{
  #region The chain, as one number

  /// <summary>Vanilla's anchor: a bloomery recovers ~50 % of an iron nugget, which is <b>5 u</b>
  /// (<c>roasting.md:144</c>, sourced from <c>conventions.md</c>). Stated as a literal because it is a fact
  /// about the base game, not about this mod - if vanilla ever moves, this constant is what has to be
  /// re-checked rather than silently inherited.</summary>
  private const float BloomeryUnitsPerNugget = 5f;

  /// <summary>
  /// Iron units a blast furnace recovers per <b>crushed-ore item</b>, derived from the three shipped
  /// constants rather than declared.
  /// <para>
  /// The chain: <c>1 nugget → 1 crushed iron item → 1 burden item</c> (the burdenmaker is 1:1 - its gate moves
  /// <c>ore + flux</c> units into the basin as that many units of burden), and a burden item is only partly
  /// ore - the rest is flux, and only the ore becomes iron. So the per-nugget figure is the per-cycle yield
  /// divided by the charge it consumed, divided again by the ore share of that charge.
  /// <para>
  /// The 1:1 claim used to cite <c>BlockEntityOreMixer._burdenCount = TotalRaw</c>, deleted with the
  /// mixer. The burdenmaker keeps the same ratio, so the derivation below is unchanged - but the citation
  /// had to move, because a derivation resting on a deleted line is one nobody can check.
  /// </para>
  /// </para>
  /// </summary>
  private static float BlastFurnaceUnitsPerNugget()
  {
    float ironShare =
      1f - IwexValues.BfDefaultFluxFrac - IwexValues.BfReferenceFuelFrac;
    float perBurdenItem =
      IwexValues.BfIronPerMeltCycle / IwexValues.BfBlastMixPerMeltCycle;
    return perBurdenItem / ironShare;
  }

  [Fact]
  public void The_iwex_chain_never_yields_less_iron_per_ore_than_a_vanilla_bloomery()
  {
    // The invariant. A player who builds the whole ironmaking chain must never be worse off than one who
    // kept a bloomery. Everything else in this suite is a balance opinion; this is the floor, and it is the
    // one number a recovery rebalance can cross without any other test noticing.
    float bf = BlastFurnaceUnitsPerNugget();

    Assert.True(
      bf >= BloomeryUnitsPerNugget,
      $"the blast furnace recovers {bf:0.###} u/nugget against the bloomery's "
        + $"{BloomeryUnitsPerNugget:0.###} - the iwex chain has become a DOWNGRADE. "
        + "See docs/design/processes/roasting.md section 7."
    );
  }

  [Fact]
  public void The_derivation_is_not_vacuous()
  {
    // Without this the invariant above passes on a chain that yields infinity, or on one whose constants were
    // all zeroed - both of which are "not less than the bloomery".
    Assert.True(IwexValues.BfIronPerMeltCycle > 0f);
    Assert.True(IwexValues.BfBlastMixPerMeltCycle > 0);
    Assert.InRange(
      1f - IwexValues.BfDefaultFluxFrac - IwexValues.BfReferenceFuelFrac,
      0.01f,
      1f
    );
    Assert.InRange(BlastFurnaceUnitsPerNugget(), 1f, 100f);
  }

  #endregion

  #region What the advantage actually is

  [Fact]
  public void The_shipped_blast_furnace_is_at_bloomery_PARITY_not_the_designed_advantage()
  {
    // A finding, not an endorsement. The design fixes the blast furnace's recovery at ~85 % on
    // raw ore and ~92 % on roasted - 8.5 and 9.2 u/nugget against the bloomery's 5 (roasting.md:144-146).
    // The shipped constants derive to exactly 5.0: 60 u per cycle ÷ 16 charge ÷ 0.75 ore share. So the whole
    // ironmaking chain currently buys the player nothing per ore over the bloomery it replaces - the advantage
    // is entirely in throughput and in what else the furnace enables, never in recovery.
    //
    // This is precisely the failure roasting.md predicted the missing guard-rail would hide, except one
    // notch subtler: the chain is not a downgrade, so the specified invariant passes. It is a no-op.
    //
    // This test pins the number as it stands so the rebalance is a deliberate edit with a visible diff, not a
    // drift. Hitting the designed 8.5 means BfIronPerMeltCycle 60 → 102 (8.5 × 0.75 × 16). When that lands,
    // change the expectation here and delete this remark - do not loosen the assertion.
    Assert.Equal(5.0f, BlastFurnaceUnitsPerNugget(), 3);
    Assert.Equal(60f, IwexValues.BfIronPerMeltCycle);

    // ...and the designed target, stated so the gap is legible rather than needing the doc open.
    const float DesignedRawRecovery = 8.5f;
    Assert.True(
      BlastFurnaceUnitsPerNugget() < DesignedRawRecovery,
      "the recovery rebalance appears to have landed - update this test and its remark"
    );
  }

  #endregion
}
