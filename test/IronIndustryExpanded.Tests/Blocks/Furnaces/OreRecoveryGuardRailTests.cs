using IronIndustryExpanded;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The recovery floor from <c>docs/design/processes/roasting.md</c> section 7: the iiex chain must never
/// yield less iron per ore than a vanilla bloomery. Recovery is not stored anywhere as a rate - it is
/// implied by three independently tuned, live-editable config constants - so this file derives it as one
/// figure and puts a floor under it.
/// </summary>
public class OreRecoveryGuardRailTests {
  #region The chain, as one number

  /// <summary>Vanilla's anchor: a bloomery recovers about 50 % of an iron nugget, which is 5 u
  /// (<c>roasting.md:144</c>, sourced from <c>conventions.md</c>). A literal because it is a fact about the
  /// base game: if vanilla moves, this constant has to be re-checked rather than inherited.</summary>
  private const float BloomeryUnitsPerNugget = 5f;

  /// <summary>
  /// Iron units a blast furnace recovers per crushed-ore item, derived from the three shipped constants
  /// rather than declared. The chain is <c>1 nugget -> 1 crushed iron item -> 1 burden item</c> (the
  /// burdenmaker is 1:1: its gate moves <c>ore + flux</c> units into the basin as that many units of
  /// burden), and a burden item is only partly ore, the rest flux, of which only the ore becomes iron. So
  /// the per-nugget figure is the per-cycle yield divided by the charge it consumed, divided again by the
  /// ore share of that charge.
  /// </summary>
  private static float BlastFurnaceUnitsPerNugget() {
    float ironShare =
      1f - IiexValues.BfDefaultFluxFrac - IiexValues.BfReferenceFuelFrac;
    float perBurdenItem =
      IiexValues.BfIronPerMeltCycle / IiexValues.BfBlastMixPerMeltCycle;
    return perBurdenItem / ironShare;
  }

  [Fact]
  public void The_iwex_chain_never_yields_less_iron_per_ore_than_a_vanilla_bloomery() {
    // The floor: a player who builds the whole ironmaking chain must never be worse off per ore than one
    // who kept a bloomery.
    float bf = BlastFurnaceUnitsPerNugget();

    Assert.True(
      bf >= BloomeryUnitsPerNugget,
      $"the blast furnace recovers {bf:0.###} u/nugget against the bloomery's "
        + $"{BloomeryUnitsPerNugget:0.###} - the iiex chain has become a DOWNGRADE. "
        + "See docs/design/processes/roasting.md section 7."
    );
  }

  [Fact]
  public void The_derivation_is_not_vacuous() {
    // Without this the invariant above passes on a chain that yields infinity, or on one whose constants
    // are all zeroed: both are "not less than the bloomery".
    Assert.True(IiexValues.BfIronPerMeltCycle > 0f);
    Assert.True(IiexValues.BfBlastMixPerMeltCycle > 0);
    Assert.InRange(
      1f - IiexValues.BfDefaultFluxFrac - IiexValues.BfReferenceFuelFrac,
      0.01f,
      1f
    );
    Assert.InRange(BlastFurnaceUnitsPerNugget(), 1f, 100f);
  }

  #endregion

  #region What the advantage actually is

  [Fact]
  public void The_shipped_blast_furnace_is_at_bloomery_PARITY_not_the_designed_advantage() {
    // The design fixes the blast furnace's recovery at ~85 % on raw ore and ~92 % on roasted - 8.5 and
    // 9.2 u/nugget against the bloomery's 5 (roasting.md:144-146). The shipped constants derive to exactly
    // 5.0: 60 u per cycle / 16 charge / 0.75 ore share, so the chain is at bloomery parity and its
    // advantage is in throughput rather than in recovery.
    //
    // The number is pinned here so a rebalance is a deliberate edit with a visible diff rather than a
    // drift. Reaching the designed 8.5 means BfIronPerMeltCycle 60 -> 102 (8.5 × 0.75 × 16); when that
    // lands the expectation changes rather than the assertion loosening.
    Assert.Equal(5.0f, BlastFurnaceUnitsPerNugget(), 3);
    Assert.Equal(60f, IiexValues.BfIronPerMeltCycle);

    // The designed target, so the gap is legible without opening the doc.
    const float DesignedRawRecovery = 8.5f;
    Assert.True(
      BlastFurnaceUnitsPerNugget() < DesignedRawRecovery,
      "the recovery rebalance appears to have landed - update this test and its remark"
    );
  }

  #endregion
}
