using System.Linq;
using ExpandedLib.Industry.Heat;
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

  /// <summary>
  /// A reheat furnace carrying its real drawing - the reverberatory side of the table. Same reason the
  /// blast furnace above needs its layout: the charge-loss denominator is the machine's own firebox, and
  /// a hearth stood on no layout counts zero cells and saturates the clamp on nothing.
  /// </summary>
  private static BlockEntityHeatingFurnace Hearth() {
    var world = new TestWorld();
    var be = new BlockEntityHeatingFurnace { Pos = new BlockPos(0, 16, 0) };
    world.Attach(be);
    FurnaceLayoutRig.OrientWithLayout(
      be,
      BlockHeatingFurnaceCore.Definitions("iiex").First(),
      "iiex:furnace-heatingcore-tier1-n",
      "north"
    );
    ReflectionHelpers.Invoke(be, "CacheAttributes");
    return be;
  }

  /// <summary>
  /// The heat balance of a firebox: pure fuel, no blast and no preheat, which is what
  /// <c>BlockEntityFireboxFurnace.ReadChargeMix</c> hands the model.
  /// </summary>
  private static HeatBalance FireboxBalance(object be, int units) =>
    (HeatBalance)
      ReflectionHelpers.Invoke(
        be,
        "ComputeHeatBalance",
        new BurdenMix(0f, 0f, units),
        0f,
        20f,
        units
      )!;

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

  #endregion

  #region Calibration - the reverberatory branch

  // Derived by hand from the same formula, not read off a run. A firebox burns pure fuel, so `FuelFrac`
  // is 1.0 and `fuelFactor` clamps at BfMaxFuelFactor; it has no tuyeres, so `airFactor` is the natural
  // draught its own stack pulls. The reheat furnace's drawing declares two flue courses:
  //
  //   natural   = 0.5 + 0.11 x sqrt(2) - 0.00102 x 4           = 0.65148
  //   T_in      = 950 + 900 x 1.25 x 0.65148                   = 1682.9
  //   T_loss    = 120 radiation + 100 charge + 0 ambient + 100 transfer = 320
  //   T_process                                                = 1362.9
  //
  // Before the losses became per-machine a hearth paid the blast furnace's 310 C column penalty for a bed
  // and no bridge loss at all. The transfer term is what a reverberatory furnace is for, and it is why
  // one cannot melt iron at any height its drawing gives it.

  /// <summary>
  /// A reverberatory hearth's settled temperature at a full firebox, from the arithmetic above.
  /// </summary>
  [Fact]
  public void A_full_firebox_settles_where_the_reverberatory_arithmetic_says() {
    BlockEntityHeatingFurnace hearth = Hearth();
    int capacity = (int)
      ReflectionHelpers.GetProperty(hearth, "ChargeCapacityUnits")!;
    Assert.True(
      capacity > 0,
      "the hearth must own a firebox, or the charge-loss clamp saturates on nothing"
    );
    Assert.Equal(
      2,
      (int)ReflectionHelpers.GetProperty(hearth, "StackCourses")!
    );

    HeatBalance hb = FireboxBalance(hearth, capacity);

    Assert.Equal(1682.9f, hb.TIn, 1);
    Assert.Equal(320f, hb.TLoss, 1);
    Assert.Equal(1362.9f, hb.TProcess, 1);
  }

  /// <summary>
  /// The two loss terms are per-machine, and the difference is the whole point: the same hearth carrying
  /// the shaft's column penalty and no bridge loss reads hotter than it should by exactly
  /// <c>BfChargeLossFull - FireboxChargeLossFull - ReverberatoryTransferLoss</c>.
  /// </summary>
  [Fact]
  public void The_reverberatory_hearth_pays_a_bed_penalty_and_a_bridge_loss() {
    BlockEntityHeatingFurnace hearth = Hearth();
    int capacity = (int)
      ReflectionHelpers.GetProperty(hearth, "ChargeCapacityUnits")!;

    HeatBalance hb = FireboxBalance(hearth, capacity);

    Assert.Equal(IiexValues.FireboxChargeLossFull, hb.ChargeLoss, 1);
    Assert.Equal(IiexValues.ReverberatoryTransferLoss, hb.TransferLoss, 1);
    // The shaft pays neither of the reverberatory's terms; nothing here changed for it.
    Assert.Equal(
      0f,
      Balance(Furnace(), 0.20f, 1f, 20f, FullHearth).TransferLoss,
      1
    );
    Assert.Equal(
      IiexValues.BfChargeLossFull,
      Balance(Furnace(), 0.20f, 1f, 20f, FullHearth).ChargeLoss,
      1
    );
  }

  /// <summary>
  /// A reverberatory furnace cannot melt iron, and no constant says so - the bridge loss does. Stated
  /// against the natural-draught ceiling, which is what the stack raises; the reheat furnace works stock
  /// far below it and puddling is a pasty-state process for the same reason.
  /// </summary>
  [Fact]
  public void No_reverberatory_hearth_reaches_irons_melting_point_on_a_bare_flue() {
    BlockEntityHeatingFurnace hearth = Hearth();
    int capacity = (int)
      ReflectionHelpers.GetProperty(hearth, "ChargeCapacityUnits")!;

    HeatBalance hb = FireboxBalance(hearth, capacity);

    Assert.True(
      hb.TProcess < IiexValues.BfIronMeltingPoint,
      $"a bare-flue hearth settles at {hb.TProcess} C, at or above iron's "
        + $"{IiexValues.BfIronMeltingPoint} C - the bridge loss has stopped doing its job"
    );
    Assert.True(
      hb.TProcess > IiexValues.RollingTempC,
      $"a bare-flue hearth settles at {hb.TProcess} C, below the {IiexValues.RollingTempC} C the reheat "
        + "furnace works stock at - it would have nothing to do"
    );
  }

  #endregion

  #region The puddling furnace's chimney is load-bearing

  // Its own arithmetic, hand-derived like the rows above. Four flue courses:
  //
  //   natural   = 0.5 + 0.11 x sqrt(4) - 0.00102 x 16          = 0.70368
  //   T_in      = 950 + 900 x 1.25 x 0.70368                   = 1741.6
  //   T_process = 1741.6 - 320                                 = 1421.6
  //
  // against a process temperature of 1400. Twenty-one degrees of headroom, and every way of losing the
  // draught takes it away: no stack at all is 1192.5, a shut damper 907.1, an open main door 1105.0.

  [Fact]
  public void A_puddling_furnace_reaches_its_process_temperature_only_with_the_stack_pulling() {
    Assert.Equal(1421.6f, PuddlingSettles(courses: 4), 1);
    Assert.True(
      PuddlingSettles(courses: 4) > IiexValues.PuddlingProcessTempC,
      "the drawn chimney must carry the furnace over its process temperature"
    );
    Assert.True(
      PuddlingSettles(courses: 0) < IiexValues.PuddlingProcessTempC,
      "without its stack the furnace must fall short, or the chimney is decoration"
    );
  }

  [Fact]
  public void A_shut_damper_or_an_open_door_puts_the_process_out_of_reach() {
    Assert.True(
      PuddlingSettles(courses: 4, damperOpen: false)
        < IiexValues.PuddlingProcessTempC,
      "a shut damper must stall the process - it is the furnace's one air control"
    );
    Assert.True(
      PuddlingSettles(courses: 4, venting: true)
        < IiexValues.PuddlingProcessTempC,
      "the main door standing open must stall it too, or closing up costs the player nothing"
    );
  }

  /// <summary>
  /// The process temperature is a window, not a ceiling: too cool and the pig never melts down, too hot
  /// and the decarburised iron stays liquid instead of coming to nature. Both ends are this mod's own
  /// melting points.
  /// </summary>
  [Fact]
  public void The_process_temperature_sits_between_pigs_melting_point_and_irons() {
    Assert.InRange(
      IiexValues.PuddlingProcessTempC,
      IiexValues.CupolaCastIronMeltingPoint,
      IiexValues.BfIronMeltingPoint
    );
    Assert.True(
      PuddlingSettles(courses: 4) < IiexValues.BfIronMeltingPoint,
      "a reverberatory furnace must not reach iron's melting point - the ball would melt"
    );
  }

  /// <summary>
  /// The puddling furnace's settled temperature at a full firebox, computed from the shared model rather
  /// than restated: the same formula the heat balance runs, at the stack and damper this case names.
  /// </summary>
  private static float PuddlingSettles(
    int courses,
    bool damperOpen = true,
    bool venting = false
  ) {
    float natural = StackDraught.NaturalDraughtFor(
      courses,
      damperOpen,
      venting
    );
    float tIn =
      IiexValues.BfCombustionBaseTemp
      + IiexValues.BfCombustionCokeGain * IiexValues.BfMaxFuelFactor * natural;
    float tLoss =
      IiexValues.BfRadiationLossBase
      + IiexValues.FireboxChargeLossFull
      + IiexValues.ReverberatoryTransferLoss;
    return tIn - tLoss;
  }

  /// <summary>
  /// The premise of every row above: the puddling furnace's drawing really does declare four flue
  /// courses, so the arithmetic describes the shipped machine rather than a hypothetical one.
  /// </summary>
  [Fact]
  public void The_puddling_furnaces_drawing_declares_the_stack_the_arithmetic_assumes() {
    var world = new TestWorld();
    var be = new BlockEntityPuddlingFurnace { Pos = new BlockPos(0, 16, 0) };
    world.Attach(be);
    FurnaceLayoutRig.OrientWithLayout(
      be,
      BlockPuddlingFurnaceCore.Definitions("iiex").First(),
      "iiex:furnace-puddlingcore-tier1-n",
      "north"
    );

    Assert.Equal(4, (int)ReflectionHelpers.GetProperty(be, "StackCourses")!);
    Assert.Equal(
      IiexValues.PuddlingProcessTempC,
      (float)ReflectionHelpers.GetProperty(be, "MeltingPoint")!
    );
  }

  #endregion

  #region Whether a burden melts

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
