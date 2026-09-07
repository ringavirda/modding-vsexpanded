using System.Collections.Generic;
using ExpandedLib.Catalogues;
using ExpandedLib.Industry.Materials;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Arithmetic of a shaft burning more than one fuel: what a column of mixed fuels reads as, and how one
/// tick's carbon budget is spent across a raceway holding two of them. Both cases are stated in carbon
/// rather than bands, since a band-counted assertion holds whether or not the furnace prices the two
/// fuels apart. Expectations come from <see cref="MaterialRoleRegistry"/> and never from
/// <see cref="BlockEntityFurnaceCore.CarbonPerUnit"/> (see <see cref="CarbonOf"/>), which is the weight
/// function under test. Driven against the production members directly: <c>CombustionMix</c> and
/// <c>ReadChargeMix</c> for the read, <c>BurnCarbon</c> for the spend. The whole-furnace counterparts
/// live in <see cref="ColdBlastFurnaceScenarioTests"/>.
/// </summary>
// Joins the furnace-config collection as a reader: BfFuelCarbonReference and ChargeItemsPerBand are
// process-wide statics, and a class that retunes either while these run would move the answer.
[Collection(FurnaceConfigCollection.Name)]
public class FuelCarbonWeightTests {
  #region Harness

  private const string Coke = "game:coke";
  private const string Charcoal = "game:charcoal";
  private const string BurdenCode = "iiex:burden";

  /// <summary>Temperature every charge is laid at, in °C. A single fixed value so
  /// <see cref="ChargeColumn.Push"/> merges bands rather than shattering into one-unit ones.</summary>
  private const float ChargeTemp = 20f;

  /// <summary>
  /// A hair of carbon over what a case means to spend, to clear the integer truncation in
  /// <c>BurnCarbon</c>: the spend is <c>(int)(left / carbon)</c>, so a budget built to buy exactly N
  /// units sits on the boundary and a fuel value that is not a clean binary fraction buys N-1. Smaller
  /// than the precision of the assertions it feeds.
  /// </summary>
  private const float Nudge = 0.0001f;

  /// <summary>
  /// The carbon one charge unit of <paramref name="code"/> carries, in coke units. Read from the
  /// registry rather than from <see cref="BlockEntityFurnaceCore.CarbonPerUnit"/>, the weight function
  /// under test. It restates production's formula (role value over <c>BfFuelCarbonReference</c>) rather
  /// than hardcoding 1.0/0.5, so a retune moves the expectation with it.
  /// </summary>
  private static float CarbonOf(string code) =>
    MaterialRoleRegistry.ValueOf(Roles.Fuel, new AssetLocation(code))
    / IiexValues.BfFuelCarbonReference;

  /// <summary>A column holding <paramref name="bands"/> bottom-first: index 0 is the raceway end, as
  /// <see cref="ChargeColumn"/> orders itself, so "coke over charcoal" is written in that order.</summary>
  private static ChargeColumn Column(
    params (string Material, int Units, BurdenMix Mix)[] bands
  ) {
    var column = new ChargeColumn();
    foreach (var (material, units, mix) in bands)
      column.Push(material, units, ChargeTemp, mix);
    return column;
  }

  /// <summary>How much of <paramref name="material"/> is left standing in <paramref name="column"/>, in
  /// units. A spend is asserted against this, since <c>BurnCarbon</c> reports carbon.</summary>
  private static int UnitsOf(ChargeColumn column, string material) {
    int units = 0;
    foreach (ChargeSegment segment in column.Segments)
      if (segment.Material == material)
        units += segment.Units;
    return units;
  }

  /// <summary>The shaft's own per-column raceway spend, invoked directly: it needs no world, structure
  /// or clock, so one budget can be watched exactly across a two-fuel boundary.</summary>
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
  /// A column holding two fuels reads its carbon, not its bands: six units of coke and eight of
  /// charcoal are fourteen units of volume and ten of carbon, and flame temperature, blast demand and
  /// melt rate are computed from the ten. Asserted through both callers of <c>Accumulate</c>, which
  /// must not come apart: <c>CombustionMix</c> for the flame, <c>ReadChargeMix</c> for the blast demand.
  /// </summary>
  [Fact]
  public void A_column_of_MIXED_fuels_reads_the_carbon_weighted_average() {
    float coke = CarbonOf(Coke);
    float charcoal = CarbonOf(Charcoal);
    Assert.True(
      coke > charcoal,
      "the registry must price the two fuels differently, or this case claims nothing"
    );

    // Exactly one raceway slice, 32 units, so CombustionMix sees all of it and the two reads below
    // answer about the same charge.
    var stamp = new BurdenMix(0.65f, 0.05f, 0.30f);
    ChargeColumn column = Column(
      (Coke, 6, default),
      (Charcoal, 8, default),
      (BurdenCode, 18, stamp)
    );
    var handle = Shaft(column);
    var furnace = new BlockEntityBlastFurnaceCold();

    // The burden's stamped 30 % fuel contributes nothing: carbon comes from fuel bands only, so the
    // denominator is the ore and flux the burden carries plus the two fuels' carbon.
    float carbon = (6 * coke) + (8 * charcoal);
    float ore = 18 * stamp.IronFrac;
    float flux = 18 * stamp.FluxFrac;
    float expected = carbon / (carbon + ore + flux);

    var raceway = (BurdenMix)
      ReflectionHelpers.Invoke(
        furnace,
        "CombustionMix",
        handle,
        default(BurdenMix)
      )!;
    Assert.Equal(expected, raceway.FuelFrac, 4);

    object?[] args = [handle, false, default(BurdenMix), 0];
    int total = (int)ReflectionHelpers.Invoke(furnace, "ReadChargeMix", args)!;
    var shaft = (BurdenMix)args[2]!;
    Assert.Equal(32, total); // the whole column, fuel included: on a shaft, fuel is charge
    Assert.Equal(expected, shaft.FuelFrac, 4);

    // Not the volume: fourteen bands of fuel against ten units of carbon is about 9 points of fuel
    // fraction, more than the gap between the standard grade and the cold furnace's break-even.
    float bandCounted = (6 + 8) / (float)(6 + 8 + ore + flux);
    Assert.NotEqual(bandCounted, raceway.FuelFrac, 4);
  }

  #endregion

  #region What one budget buys across a fuel boundary

  /// <summary>
  /// A tick's carbon budget is one number and a raceway slice can span a coke course and a charcoal
  /// one, so the unit price is resolved per segment inside the walk. The surviving units discriminate,
  /// not the carbon returned: a budget spent in full comes back as the same number at either price.
  /// Both charge orders are run, since they fail a resolve-once walk in opposite directions.
  /// </summary>
  [Fact]
  public void One_budget_over_two_fuels_spends_each_at_its_own_value() {
    float coke = CarbonOf(Coke);
    float charcoal = CarbonOf(Charcoal);
    Assert.True(
      coke > charcoal,
      "the registry must price the two fuels differently, or this case claims nothing"
    );

    var furnace = new BlockEntityBlastFurnaceCold();

    // Coke standing over charcoal: the walk meets the cheap fuel first.
    // A budget that buys all four charcoal units and then exactly two of the coke above them.
    ChargeColumn cokeOnTop = Column((Charcoal, 4, default), (Coke, 4, default));
    float wantCokeOnTop = (4 * charcoal) + (2 * coke);
    float spentCokeOnTop = Burn(furnace, cokeOnTop, wantCokeOnTop + Nudge);

    Assert.Equal(wantCokeOnTop, spentCokeOnTop, 3);
    Assert.Equal(0, UnitsOf(cokeOnTop, Charcoal));
    // A weight resolved once from the bottom band spends the coke at charcoal's price and clears all
    // four units; only a per-segment conversion leaves exactly two.
    Assert.Equal(2, UnitsOf(cokeOnTop, Coke));

    // Charcoal standing over coke: the same budget shape mirrored, so a defect cannot hide in the order
    // the player charged. The number itself differs - six units of fuel, less carbon.
    ChargeColumn charcoalOnTop = Column(
      (Coke, 4, default),
      (Charcoal, 4, default)
    );
    float wantCharcoalOnTop = (4 * coke) + (2 * charcoal);
    float spentCharcoalOnTop = Burn(
      furnace,
      charcoalOnTop,
      wantCharcoalOnTop + Nudge
    );

    Assert.Equal(wantCharcoalOnTop, spentCharcoalOnTop, 3);
    Assert.Equal(0, UnitsOf(charcoalOnTop, Coke));
    Assert.Equal(2, UnitsOf(charcoalOnTop, Charcoal));

    // The same six units of fuel volume buy two different budgets: what a raceway spends is carbon, and
    // volume is not carbon.
    Assert.NotEqual(wantCokeOnTop, wantCharcoalOnTop, 3);
  }

  #endregion
}
