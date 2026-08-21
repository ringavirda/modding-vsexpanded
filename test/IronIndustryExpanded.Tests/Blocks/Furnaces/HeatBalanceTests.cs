using System.Linq;
using ExpandedLib.Heat;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The furnace's dynamic heat balance, evaluated directly. <c>T_process = T_in - T_loss</c> carries no
/// maximum temperature, so this table is what holds its numbers in place: a cold blast furnace is one
/// running a high-coke burden on unheated air, a hot blast furnace the same machine with a cowper on the
/// line. It runs on the cold furnace's block entity because the model is iiex's
/// (<c>BlockEntityFurnaceCore.ComputeHeatBalance</c>) and takes the blast temperature as an argument, so
/// hot-blast rows need no hot furnace. Parity with the smex furnace is asserted from the smex suite, where
/// both types are in scope.
/// </summary>
[Collection(FurnaceConfigCollection.Name)]
public class HeatBalanceTests {
  #region Harness

  /// <summary>
  /// A cold blast furnace carrying its real drawing. The furnace has to own cells: the heat balance's
  /// denominator is its own geometric capacity, and <c>ComputeHeatBalance</c> clamps a 0 capacity up to 1,
  /// so a furnace stood on a layout-less block saturates the clamp on every input and the calibration
  /// table below would pass while measuring nothing. The denominator cases assert the capacity is real.
  /// </summary>
  private static BlockEntityBlastFurnaceCold Furnace() {
    var world = new TestWorld();
    var be = new BlockEntityBlastFurnaceCold { Pos = new BlockPos(0, 16, 0) };
    world.Attach(be);
    FurnaceLayoutRig.OrientWithLayout(
      be,
      BlockBlastFurnaceCoreCold.Definitions("iiex").First(),
      "iiex:furnace-blastcore-tier1-n",
      "north"
    );
    ReflectionHelpers.Invoke(be, "CacheAttributes");
    return be;
  }

  /// <summary>A burden of a given coke fraction, carrying enough flux to grade as a real burden.</summary>
  private static BurdenMix Burden(float fuelFrac) =>
    new(1f - 0.05f - fuelFrac, 0.05f, fuelFrac);

  private static HeatBalance Balance(
    object be,
    float fuelFrac,
    float blastSupplyFrac,
    float blastTemp,
    int mixCount
  ) =>
    (HeatBalance)
      ReflectionHelpers.Invoke(
        be,
        "ComputeHeatBalance",
        Burden(fuelFrac),
        blastSupplyFrac,
        blastTemp,
        mixCount
      )!;

  /// <summary>
  /// A cold blast furnace loaded to capacity - the calibration reference, and what every row below means
  /// by "a full hearth". 36 chargeable cells × 32 units a block.
  /// <para>
  /// It must stay a literal. The heat balance divides <c>mixCount / capacity</c>, so reading the reference
  /// off the same source as the denominator moves numerator and denominator together and the six expected
  /// temperatures survive any change to it. The tie to the furnace's own capacity is asserted below rather
  /// than assigned here.
  /// </para>
  /// </summary>
  private const int FullHearth = 1152;

  /// <summary>
  /// Ties the hand-written reference above to the furnace's own geometry, so the table cannot stop
  /// describing a full hearth. A layout change that adds or removes a chargeable cell fails here, by name,
  /// instead of shifting six temperatures that still look plausible.
  /// </summary>
  [Fact]
  public void The_calibration_reference_really_is_the_furnaces_capacity() {
    Assert.Equal(FullHearth, Denominator(Furnace()));
  }

  /// <summary>The furnace's own cold-charge denominator - what it counts as "full".</summary>
  private static int Denominator(BlockEntityBlastFurnaceCold be) =>
    (int)ReflectionHelpers.GetProperty(be, "ChargeCapacityUnits")!;

  #endregion

  #region The cold-charge denominator

  // No row of the calibration table below can observe this number: `chargeLoss = BfChargeLossFull *
  // clamp(mixCount / requiredMix, 0, 1)`, and every row passes `FullHearth` as the mixCount, so the clamp
  // saturates at 1 and the six temperatures are the full-charge case whatever the denominator is. The
  // denominator rests on the shaft's geometric capacity, and these two cases are the only ones that see it.

  /// <summary>
  /// The denominator's whole observable effect, stated without needing <c>T_in</c>: a hearth at half its
  /// denominator runs exactly <c>BfChargeLossFull / 2</c> hotter than one at full, because half as much
  /// cold charge is there to soak the heat. A difference rather than an absolute, so it pins the rule and
  /// survives a retune of the combustion terms, which the six absolute rows below do not.
  /// </summary>
  [Fact]
  public void A_half_charged_hearth_pays_half_the_cold_charge_penalty() {
    BlockEntityBlastFurnaceCold be = Furnace();
    int full = Denominator(be);

    // The premise: `ComputeHeatBalance` clamps the denominator up to 1, so a furnace whose capacity read
    // 0 would saturate on every input and pass both assertions below while measuring nothing.
    Assert.True(
      full >= 2,
      $"the denominator must be a real capacity for this case to mean anything; it was {full}"
    );

    float atFull = Balance(be, 0.20f, 1f, 20f, full).TProcess;
    float atHalf = Balance(be, 0.20f, 1f, 20f, full / 2).TProcess;

    Assert.Equal(IiexValues.BfChargeLossFull / 2f, atHalf - atFull, 1);
  }

  /// <summary>
  /// The charge loss clamps: an over-charged hearth pays the full penalty and no more. Without the clamp a
  /// shaft charged past its own capacity would go on getting colder.
  /// </summary>
  [Fact]
  public void An_over_charged_hearth_pays_the_full_penalty_and_no_more() {
    BlockEntityBlastFurnaceCold be = Furnace();
    int full = Denominator(be);

    Assert.Equal(
      Balance(be, 0.20f, 1f, 20f, full).TProcess,
      Balance(be, 0.20f, 1f, 20f, full * 4).TProcess,
      1
    );

    // An empty hearth pays none of it, the other end of the same clamp.
    Assert.Equal(
      IiexValues.BfChargeLossFull,
      Balance(be, 0.20f, 1f, 20f, 0).TProcess
        - Balance(be, 0.20f, 1f, 20f, full).TProcess,
      1
    );
  }

  #endregion

  #region Calibration

  // The anchor table. The first two rows are the fixed ceilings the furnace once carried as config
  // constants (1420 C natural, 1740 C boosted); the rest are cases those constants could not express.
  [Theory]
  // fuelFrac, blastSupplyFrac, blastTemp, expected T_process
  [InlineData(0.20f, 1f, 20f, 1420f)] // standard burden, cold blast - the old natural ceiling
  [InlineData(0.20f, 1f, 950f, 1745.5f)] // standard burden, hot blast - the old boosted ceiling
  [InlineData(0.30f, 1f, 20f, 1577.5f)] // high coke, cold blast - melts (iiex.md)
  [InlineData(0.10f, 1f, 20f, 1262.5f)] // low coke, cold blast - stalls
  [InlineData(0.10f, 1f, 950f, 1588f)] // low coke, hot blast - melts (smex.md)
  [InlineData(0.20f, 0f, 20f, 970f)] // blowers off, natural draught only - stalls
  public void The_heat_balance_settles_where_the_calibration_says(
    float fuelFrac,
    float blastSupplyFrac,
    float blastTemp,
    float expected
  ) {
    HeatBalance hb = Balance(
      Furnace(),
      fuelFrac,
      blastSupplyFrac,
      blastTemp,
      FullHearth
    );

    Assert.Equal(expected, hb.TProcess, 1);
  }

  [Theory]
  [InlineData(0.30f, 20f, true)] // high coke on cold blast clears the melt line
  [InlineData(0.10f, 20f, false)] // low coke on cold blast does not
  [InlineData(0.10f, 950f, true)] // ... until a cowper preheats the same blast
  public void Whether_a_burden_melts_follows_from_coke_and_preheat(
    float fuelFrac,
    float blastTemp,
    bool shouldMelt
  ) {
    HeatBalance hb = Balance(Furnace(), fuelFrac, 1f, blastTemp, FullHearth);

    Assert.Equal(shouldMelt, hb.TProcess > IiexValues.BfIronMeltingPoint);
  }

  #endregion

  #region Contributors

  [Fact]
  public void Preheat_is_the_only_thing_hot_blast_changes() {
    HeatBalance cold = Balance(Furnace(), 0.20f, 1f, 20f, FullHearth);
    HeatBalance hot = Balance(Furnace(), 0.20f, 1f, 950f, FullHearth);

    Assert.Equal(0f, cold.PreheatGain, 3);
    Assert.False(cold.IsHotBlast);
    Assert.True(hot.IsHotBlast);
    // Same fuel, same air, same losses: the entire difference is the preheat term.
    Assert.Equal(cold.FuelFactor, hot.FuelFactor, 4);
    Assert.Equal(cold.AirFactor, hot.AirFactor, 4);
    Assert.Equal(cold.TLoss, hot.TLoss, 4);
    Assert.Equal(hot.TProcess - cold.TProcess, hot.PreheatGain, 2);
  }

  [Fact]
  public void Piling_in_coke_hits_the_ceiling_rather_than_running_away() {
    Assert.Equal(
      IiexValues.BfMaxFuelFactor,
      Balance(Furnace(), 0.95f, 1f, 20f, FullHearth).FuelFactor,
      3
    );
  }

  [Fact]
  public void The_coke_factor_floors_rather_than_going_negative() {
    // At the shipped sensitivity a coke-free burden only drops the factor to 0.65, so the floor is slack
    // and exists for retuning. The sensitivity is raised here to drive the raw factor negative, where a
    // missing clamp would let a mistuned config make the furnace produce cold.
    float original = IiexValues.BfCokeSensitivity;
    try {
      IiexValues.Edit(c => c.BfCokeSensitivity = 4f);
      Assert.Equal(
        IiexValues.BfMinFuelFactor,
        Balance(Furnace(), 0f, 1f, 20f, FullHearth).FuelFactor,
        3
      );
    } finally {
      IiexValues.Edit(c => c.BfCokeSensitivity = original);
    }
  }

  [Fact]
  public void An_empty_hearth_carries_no_charge_loss() {
    HeatBalance empty = Balance(Furnace(), 0.20f, 1f, 20f, 0);
    HeatBalance full = Balance(Furnace(), 0.20f, 1f, 20f, FullHearth);

    Assert.Equal(0f, empty.ChargeLoss, 3);
    Assert.Equal(IiexValues.BfChargeLossFull, full.ChargeLoss, 3);
    Assert.Equal(IiexValues.BfRadiationLossBase, empty.TLoss, 3);
  }

  [Fact]
  public void Overfilling_the_hearth_does_not_keep_costing_heat() {
    HeatBalance full = Balance(Furnace(), 0.20f, 1f, 20f, FullHearth);
    HeatBalance overfull = Balance(Furnace(), 0.20f, 1f, 20f, FullHearth * 4);

    Assert.Equal(full.ChargeLoss, overfull.ChargeLoss, 3);
  }

  [Fact]
  public void Unstamped_charge_burns_at_the_reference_coke_ratio() {
    // A charge with no composition (legacy blast mix) must read as the standard grade, or every
    // existing world's furnace would drop to the fuel-starved end of the balance on load.
    HeatBalance legacy = (HeatBalance)
      ReflectionHelpers.Invoke(
        Furnace(),
        "ComputeHeatBalance",
        default(BurdenMix),
        1f,
        20f,
        FullHearth
      )!;

    Assert.Equal(IiexValues.BfDefaultFuelFrac, legacy.FuelFrac, 3);
    Assert.Equal(1f, legacy.FuelFactor, 3);
  }

  [Fact]
  public void A_cold_day_costs_heat_but_a_hot_one_is_not_a_gift() {
    var be = Furnace();
    HeatBalance mild = Balance(be, 0.20f, 1f, 20f, FullHearth);

    ReflectionHelpers.SetField(be, "_ambientTemp", -10f);
    HeatBalance freezing = Balance(be, 0.20f, 1f, 20f, FullHearth);

    ReflectionHelpers.SetField(be, "_ambientTemp", 35f);
    HeatBalance summer = Balance(be, 0.20f, 1f, 20f, FullHearth);

    Assert.Equal(0f, mild.AmbientLoss, 3);
    Assert.Equal(
      30f * IiexValues.BfAmbientLossPerDegree,
      freezing.AmbientLoss,
      3
    );
    Assert.Equal(0f, summer.AmbientLoss, 3); // a warmer-than-reference day is not a gain
  }

  [Fact]
  public void The_process_temperature_never_falls_below_ambient() {
    var be = Furnace();
    ReflectionHelpers.SetField(be, "_ambientTemp", 30f);
    // No combustion, and a hearth packed with cold mass.
    IiexValues.Edit(c => c.BfCombustionBaseTemp = 0f);
    try {
      HeatBalance hb = Balance(be, 0f, 0f, 30f, FullHearth);
      Assert.Equal(30f, hb.TProcess, 3);
    } finally {
      IiexValues.Edit(c => c.BfCombustionBaseTemp = 950f);
    }
  }

  #endregion
}
