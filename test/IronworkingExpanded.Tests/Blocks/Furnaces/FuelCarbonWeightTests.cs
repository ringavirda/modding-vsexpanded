using System.Collections.Generic;
using ExpandedLib.Materials;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The <b>arithmetic</b> of a shaft that burns more than one fuel: what a column of mixed fuels reads as,
/// and how a single tick's carbon budget is spent across a raceway holding two of them.
/// <para>
/// <b>Charcoal was never rejected - it was mispriced.</b> It has been accepted at every gate the shaft
/// owns since the role registry arrived: it lit, burned, made gas, descended and yielded iron at
/// <b>coke's exact rate</b>. Nothing in any suite could tell that apart from a furnace that genuinely
/// priced it, because every fixture charged coke and every assertion counted <em>bands</em>. These two
/// cases are the ones that can: both would pass unchanged on the old aliasing model if they were stated
/// in bands, so both are stated in <b>carbon</b>.
/// </para>
/// <para>
/// <b>Every expectation here is computed from <see cref="MaterialRoleRegistry"/>, never from
/// <see cref="BlockEntityFurnaceCore.CarbonPerUnit"/></b> - see <see cref="CarbonOf"/>. The weight
/// function is the thing under test: an expectation derived from it moves with the defect, so a furnace
/// that had stopped pricing anything at all would still be green. The registry (and the reference the
/// values are quoted against) is the independent source.
/// </para>
/// <para>
/// Driven straight against the production members rather than through a scene, because what is at stake is
/// a conversion and not a campaign - <c>CombustionMix</c>/<c>ReadChargeMix</c> for the read and
/// <c>BurnCarbon</c> for the spend. The scenario counterparts (a whole furnace running cooler on charcoal,
/// and one that still melts on a richer course) live in <see cref="ColdBlastFurnaceScenarioTests"/>.
/// </para>
/// </summary>
// Joins the furnace-config collection as a *reader*: BfFuelCarbonReference and ChargeItemsPerBand are
// process-wide statics, and a class that retunes either while these run would silently move the answer.
[Collection(FurnaceConfigCollection.Name)]
public class FuelCarbonWeightTests
{
  #region Harness

  private const string Coke = "game:coke";
  private const string Charcoal = "game:charcoal";
  private const string BurdenCode = "iwex:burden";

  /// <summary>What the scenes lay their charge at - a single fixed value so <see cref="ChargeColumn.Push"/>
  /// merges as it would in the world rather than shattering into one-unit bands.</summary>
  private const float ChargeTemp = 20f;

  /// <summary>
  /// A hair of carbon over what the case actually means to spend.
  /// <para>
  /// <b>Not superstition - it is the integer truncation in <c>BurnCarbon</c>.</b> The spend is
  /// <c>(int)(left / carbon)</c>, so a budget built to buy <em>exactly</em> N units sits on the boundary,
  /// and any fuel value that is not a clean binary fraction (a retune to, say, 0.6 of coke) rounds the
  /// division a hair low and buys N-1. The nudge is smaller than the assertions' own precision, so it
  /// changes nothing that is being claimed and stops the case being a hostage to the shipped ratio being
  /// dyadic.
  /// </para>
  /// </summary>
  private const float Nudge = 0.0001f;

  /// <summary>
  /// The carbon one charge unit of <paramref name="code"/> carries, in coke units.
  /// <para>
  /// <b>Computed from the registry, deliberately, and never by calling
  /// <see cref="BlockEntityFurnaceCore.CarbonPerUnit"/>.</b> That method is the production weight function
  /// and it is what these cases exist to pin: an expectation that called it would move in lockstep with any
  /// defect in it - make it return a flat 1.0 and both sides of every assertion below would agree, on a
  /// furnace that had stopped telling coke from charcoal entirely.
  /// </para>
  /// <para>
  /// It restates production's own formula (role value over <c>BfFuelCarbonReference</c>) rather than
  /// hardcoding 1.0/0.5, so a retune of either the value or the reference moves the expectation with it -
  /// and, because the reference cancels, any assertion stated as a <em>ratio</em> is immune to it entirely.
  /// </para>
  /// </summary>
  private static float CarbonOf(string code) =>
    MaterialRoleRegistry.ValueOf(Roles.Fuel, new AssetLocation(code))
    / IwexValues.BfFuelCarbonReference;

  /// <summary>A column holding <paramref name="bands"/> bottom-first - index 0 is the raceway end, exactly
  /// as <see cref="ChargeColumn"/> orders itself, so "coke over charcoal" is written in that order.</summary>
  private static ChargeColumn Column(
    params (string Material, int Units, BurdenMix Mix)[] bands
  )
  {
    var column = new ChargeColumn();
    foreach (var (material, units, mix) in bands)
      column.Push(material, units, ChargeTemp, mix);
    return column;
  }

  /// <summary>How much of <paramref name="material"/> is left standing in <paramref name="column"/> - what
  /// a spend has to be asserted against, since <c>BurnCarbon</c> reports carbon and the column holds units.</summary>
  private static int UnitsOf(ChargeColumn column, string material)
  {
    int units = 0;
    foreach (ChargeSegment segment in column.Segments)
      if (segment.Material == material)
        units += segment.Units;
    return units;
  }

  /// <summary>The shaft's own per-column raceway spend, invoked directly: it needs no world, no structure
  /// and no clock, which is what lets a single budget be watched across a two-fuel boundary exactly.</summary>
  private static float Burn(
    BlockEntityShaftFurnace furnace,
    ChargeColumn column,
    float want
  ) => (float)ReflectionHelpers.Invoke(furnace, "BurnCarbon", column, want)!;

  private static Dictionary<(int X, int Z), ChargeColumn> Shaft(
    ChargeColumn column
  ) => new() { [(0, 0)] = column };

  #endregion

  #region What a mixed column reads as

  /// <summary>
  /// <b>A column holding two fuels reads its carbon, not its bands.</b> Six units of coke and eight of
  /// charcoal are fourteen units of <em>volume</em> and ten units of <em>carbon</em>, and it is the ten that
  /// the flame temperature, the blast demand and the melt rate are all computed from.
  /// <para>
  /// Asserted through <b>both</b> callers of <c>Accumulate</c> - <c>CombustionMix</c> (the raceway round,
  /// which sets the flame) and <c>ReadChargeMix</c> (the whole shaft, which sets the blast demand and the
  /// grade readout). They are two walks over the same helper and they must not come apart: a furnace whose
  /// raceway priced charcoal while its blast demand counted bands would ask for the wrong air on a charge it
  /// was burning correctly, and nothing observable would name either half.
  /// </para>
  /// <para>
  /// The band-counted answer is asserted against explicitly rather than left implicit. The two differ by
  /// about nine points of coke fraction here, which is well inside the range the heat balance is sensitive
  /// over - so "close enough" is exactly the failure mode this case is for.
  /// </para>
  /// </summary>
  [Fact]
  public void A_column_of_MIXED_fuels_reads_the_carbon_weighted_average()
  {
    float coke = CarbonOf(Coke);
    float charcoal = CarbonOf(Charcoal);
    Assert.True(
      coke > charcoal,
      "the registry must price the two fuels differently, or this case claims nothing"
    );

    // A round's worth exactly - 32 units, which is one whole raceway slice - so CombustionMix sees all of
    // it and the two reads below are answering about the same charge.
    var stamp = new BurdenMix(0.65f, 0.05f, 0.30f);
    ChargeColumn column = Column(
      (Coke, 6, default),
      (Charcoal, 8, default),
      (BurdenCode, 18, stamp)
    );
    var handle = Shaft(column);
    var furnace = new BlockEntityBlastFurnaceCold();

    // The burden's own stamped 30 % fuel contributes nothing - carbon comes from fuel bands and nowhere
    // else - so the denominator is the ore and flux it actually carries plus the two fuels' carbon.
    float carbon = (6 * coke) + (8 * charcoal);
    float ore = 18 * stamp.IronFrac;
    float flux = 18 * stamp.FluxFrac;
    float expected = carbon / (carbon + ore + flux);

    var raceway = (BurdenMix)
      ReflectionHelpers.Invoke(furnace, "CombustionMix", handle, default(BurdenMix))!;
    Assert.Equal(expected, raceway.FuelFrac, 4);

    object?[] args = [handle, false, default(BurdenMix), 0];
    int total = (int)ReflectionHelpers.Invoke(furnace, "ReadChargeMix", args)!;
    var shaft = (BurdenMix)args[2]!;
    Assert.Equal(32, total); // the whole column counted, fuel included - it is charge on a shaft
    Assert.Equal(expected, shaft.FuelFrac, 4);

    // And it is not the volume. Stated as its own assertion because the difference is the whole case:
    // fourteen bands of fuel against ten units of carbon is ~9 points of coke fraction, which is more than
    // the gap between the standard grade and the cold furnace's break-even.
    float bandCounted = (6 + 8) / (float)(6 + 8 + ore + flux);
    Assert.NotEqual(bandCounted, raceway.FuelFrac, 4);
  }

  #endregion

  #region What one budget buys across a fuel boundary

  /// <summary>
  /// <b>The only genuinely new bookkeeping in the model, and the case that pins it.</b> A tick's carbon
  /// budget is one number, and a raceway slice routinely spans a coke course and a charcoal one - so the
  /// unit price has to be resolved <b>per segment, inside the walk</b>. Resolve it once before the walk and
  /// the second fuel is spent at the first one's price, in whichever direction the player happened to charge.
  /// <para>
  /// <b>The residue is what discriminates, not the carbon returned.</b> A budget spent entirely comes back
  /// as the same number whichever price it was spent at - the defect hides perfectly in the return value and
  /// shows only in what is left standing in the column. So both halves below assert the surviving units, and
  /// both orders are run: bottom-up is the walk's direction, so coke-under-charcoal and charcoal-under-coke
  /// fail a resolve-once implementation in opposite directions and neither one alone can catch both.
  /// </para>
  /// <para>
  /// The two budgets are deliberately <b>different numbers</b> for the same six units of fuel volume, which
  /// is the fact the whole feature rests on: what a raceway spends is carbon, and volume is not carbon.
  /// </para>
  /// </summary>
  [Fact]
  public void One_budget_over_two_fuels_spends_each_at_its_own_value()
  {
    float coke = CarbonOf(Coke);
    float charcoal = CarbonOf(Charcoal);
    Assert.True(
      coke > charcoal,
      "the registry must price the two fuels differently, or this case claims nothing"
    );

    var furnace = new BlockEntityBlastFurnaceCold();

    // ── Coke standing over charcoal: the walk meets the cheap fuel first.
    // A budget that buys all four charcoal units and then exactly two of the coke above them.
    ChargeColumn cokeOnTop = Column((Charcoal, 4, default), (Coke, 4, default));
    float wantCokeOnTop = (4 * charcoal) + (2 * coke);
    float spentCokeOnTop = Burn(furnace, cokeOnTop, wantCokeOnTop + Nudge);

    Assert.Equal(wantCokeOnTop, spentCokeOnTop, 3);
    Assert.Equal(0, UnitsOf(cokeOnTop, Charcoal));
    // The assertion. A weight resolved once from the bottom band spends the coke at charcoal's price and
    // clears all four units; one resolved at coke's price never finishes the charcoal. Only a per-segment
    // conversion leaves exactly two.
    Assert.Equal(2, UnitsOf(cokeOnTop, Coke));

    // ── Charcoal standing over coke: the same budget shape, mirrored, so the defect cannot hide in the
    // order the player charged. Note the number itself differs - six units of fuel, less carbon.
    ChargeColumn charcoalOnTop = Column((Coke, 4, default), (Charcoal, 4, default));
    float wantCharcoalOnTop = (4 * coke) + (2 * charcoal);
    float spentCharcoalOnTop = Burn(furnace, charcoalOnTop, wantCharcoalOnTop + Nudge);

    Assert.Equal(wantCharcoalOnTop, spentCharcoalOnTop, 3);
    Assert.Equal(0, UnitsOf(charcoalOnTop, Coke));
    Assert.Equal(2, UnitsOf(charcoalOnTop, Charcoal));

    // The same six units of fuel volume, two different budgets - which is the feature, stated as a fact
    // about the numbers this case was built from rather than as a further behaviour.
    Assert.NotEqual(wantCokeOnTop, wantCharcoalOnTop, 3);
  }

  #endregion
}
