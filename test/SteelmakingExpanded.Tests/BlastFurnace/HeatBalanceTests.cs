using ExpandedLib.Process;
using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.Items;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The furnace's dynamic heat balance, evaluated directly. <c>T_process = T_in - T_loss</c> has no
/// maximum temperature in it, so the only thing holding its numbers in place is this table - and the
/// table is what the design docs' claims rest on: a cold blast furnace is one running a high-coke
/// burden on unheated air, a hot blast furnace is the same machine with a cowper on the line.
/// </summary>
public class HeatBalanceTests
{
  #region Harness

  private static BlockEntityBlastFurnaceHot Furnace()
  {
    var world = new TestWorld();
    var be = new BlockEntityBlastFurnaceHot
    {
      Pos = new BlockPos(0, 16, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "smex:blastfurnacecore-north",
        1,
        ("side", "north")
      ),
    };
    world.Attach(be);
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
    ReflectionHelpers.Invoke(be, "CacheAttributes");
    return be;
  }

  private static BlockEntityBlastFurnaceCold ColdFurnace()
  {
    var world = new TestWorld();
    var be = new BlockEntityBlastFurnaceCold
    {
      Pos = new BlockPos(0, 16, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "iwex:blastfurnacecore-north",
        1,
        ("side", "north")
      ),
    };
    world.Attach(be);
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
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

  /// <summary>A hearth loaded exactly to the fire threshold - the calibration reference.</summary>
  private static int FullHearth => IwexValues.BlastMixRequiredToFire;

  #endregion

  #region Calibration

  // The anchor table. The first two rows reproduce the fixed ceilings the furnace used to carry as
  // config constants (1420 C natural, 1740 C boosted), so this change is behaviour-preserving where
  // it was already tuned; the rest are the cases those constants could not express at all.
  [Theory]
  // fuelFrac, blastSupplyFrac, blastTemp, expected T_process
  [InlineData(0.20f, 1f, 20f, 1420f)] // standard burden, cold blast - the old natural ceiling
  [InlineData(0.20f, 1f, 950f, 1745.5f)] // standard burden, hot blast - the old boosted ceiling
  [InlineData(0.30f, 1f, 20f, 1577.5f)] // high coke, cold blast - melts (iwex.md)
  [InlineData(0.10f, 1f, 20f, 1262.5f)] // low coke, cold blast - stalls
  [InlineData(0.10f, 1f, 950f, 1588f)] // low coke, hot blast - melts (smex.md)
  [InlineData(0.20f, 0f, 20f, 970f)] // blowers off, natural draught only - stalls
  public void The_heat_balance_settles_where_the_calibration_says(
    float fuelFrac,
    float blastSupplyFrac,
    float blastTemp,
    float expected
  )
  {
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
  )
  {
    HeatBalance hb = Balance(Furnace(), fuelFrac, 1f, blastTemp, FullHearth);

    Assert.Equal(shouldMelt, hb.TProcess > IwexValues.BfIronMeltingPoint);
  }

  #endregion

  #region The cold/hot split is not in the code

  [Fact]
  public void Both_furnaces_compute_the_same_balance_from_the_same_conditions()
  {
    // The whole point of the merge: there is one blast furnace. Feed the cold anchor's block entity
    // and the hot anchor's the same charge and the same blast and they must agree to the degree -
    // what differs in game is the layout they sit in and whether a cowper is on the line, never the
    // model. Two independent copies of these formulas is what this replaces.
    HeatBalance cold = Balance(ColdFurnace(), 0.20f, 1f, 950f, FullHearth);
    HeatBalance hot = Balance(Furnace(), 0.20f, 1f, 950f, FullHearth);

    Assert.Equal(cold, hot);
  }

  #endregion

  #region Contributors

  [Fact]
  public void Preheat_is_the_only_thing_hot_blast_changes()
  {
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
  public void Piling_in_coke_hits_the_ceiling_rather_than_running_away()
  {
    Assert.Equal(
      IwexValues.BfMaxFuelFactor,
      Balance(Furnace(), 0.95f, 1f, 20f, FullHearth).FuelFactor,
      3
    );
  }

  [Fact]
  public void The_coke_factor_floors_rather_than_going_negative()
  {
    // At the shipped sensitivity a coke-free burden only drops the factor to 0.65, so the floor is
    // slack - it exists for retuning. Turn the sensitivity up far enough to drive the raw factor
    // negative and the clamp has to catch it, or a badly tuned config could make a furnace produce
    // cold.
    float original = IwexValues.BfCokeSensitivity;
    try
    {
      IwexValues.Edit(c => c.BfCokeSensitivity = 4f);
      Assert.Equal(
        IwexValues.BfMinFuelFactor,
        Balance(Furnace(), 0f, 1f, 20f, FullHearth).FuelFactor,
        3
      );
    }
    finally
    {
      IwexValues.Edit(c => c.BfCokeSensitivity = original);
    }
  }

  [Fact]
  public void An_empty_hearth_carries_no_charge_loss()
  {
    HeatBalance empty = Balance(Furnace(), 0.20f, 1f, 20f, 0);
    HeatBalance full = Balance(Furnace(), 0.20f, 1f, 20f, FullHearth);

    Assert.Equal(0f, empty.ChargeLoss, 3);
    Assert.Equal(IwexValues.BfChargeLossFull, full.ChargeLoss, 3);
    Assert.Equal(IwexValues.BfRadiationLossBase, empty.TLoss, 3);
  }

  [Fact]
  public void Overfilling_the_hearth_does_not_keep_costing_heat()
  {
    HeatBalance full = Balance(Furnace(), 0.20f, 1f, 20f, FullHearth);
    HeatBalance overfull = Balance(Furnace(), 0.20f, 1f, 20f, FullHearth * 4);

    Assert.Equal(full.ChargeLoss, overfull.ChargeLoss, 3);
  }

  [Fact]
  public void Unstamped_charge_burns_at_the_reference_coke_ratio()
  {
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

    Assert.Equal(IwexValues.BfDefaultFuelFrac, legacy.FuelFrac, 3);
    Assert.Equal(1f, legacy.FuelFactor, 3);
  }

  [Fact]
  public void A_cold_day_costs_heat_but_a_hot_one_is_not_a_gift()
  {
    var be = Furnace();
    HeatBalance mild = Balance(be, 0.20f, 1f, 20f, FullHearth);

    ReflectionHelpers.SetField(be, "_ambientTemp", -10f);
    HeatBalance freezing = Balance(be, 0.20f, 1f, 20f, FullHearth);

    ReflectionHelpers.SetField(be, "_ambientTemp", 35f);
    HeatBalance summer = Balance(be, 0.20f, 1f, 20f, FullHearth);

    Assert.Equal(0f, mild.AmbientLoss, 3);
    Assert.Equal(
      30f * IwexValues.BfAmbientLossPerDegree,
      freezing.AmbientLoss,
      3
    );
    Assert.Equal(0f, summer.AmbientLoss, 3); // no free heat for smelting in July
  }

  [Fact]
  public void The_process_temperature_never_falls_below_ambient()
  {
    var be = Furnace();
    ReflectionHelpers.SetField(be, "_ambientTemp", 30f);
    // Nothing burning worth the name, and a hearth packed with cold mass.
    IwexValues.Edit(c => c.BfCombustionBaseTemp = 0f);
    try
    {
      HeatBalance hb = Balance(be, 0f, 0f, 30f, FullHearth);
      Assert.Equal(30f, hb.TProcess, 3);
    }
    finally
    {
      IwexValues.Edit(c => c.BfCombustionBaseTemp = 950f);
    }
  }

  #endregion
}
