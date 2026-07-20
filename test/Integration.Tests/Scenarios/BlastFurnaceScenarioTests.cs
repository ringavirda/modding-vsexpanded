using ExpandedLib.Testing;
using SteelmakingExpanded;
using IronworkingExpanded;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.Items;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The blast furnace's primary process driven end to end (handbook blast-furnace + hot-blast): a
/// charged, lit hearth fed hot blast climbs past iron's melting point, enters the Melting phase,
/// renders blast mix into molten iron, and taps it into a canal - and loses the melt if the blast is
/// cut. Exercises the gated firing/melting tick with its real peripherals via <see cref="BlastFurnaceRig"/>.
/// </summary>
public class BlastFurnaceScenarioTests
{
  #region Heat + phase progression

  [Fact]
  public void Hot_blast_drives_the_furnace_past_irons_melting_point()
  {
    // A lit, firing furnace sat at where cold blast alone would hold it (1420 C, below iron's
    // 1482 C melt point).
    var rig = new BlastFurnaceRig()
      .FeedBlast(950f)
      .SetState(FurnaceState.Firing)
      .SetTemp(BlastFurnaceRig.ColdBlastCeiling);

    rig.Tick(30); // preheated blast lifts T_in well past the melt point

    Assert.True(
      rig.Temp > IwexValues.BfIronMeltingPoint,
      $"hot blast should drive the furnace past {IwexValues.BfIronMeltingPoint} C, was {rig.Temp}"
    );
  }

  [Fact]
  public void Sustained_heat_above_the_melt_point_transitions_to_melting()
  {
    var rig = new BlastFurnaceRig()
      .FeedBlast()
      .SetState(FurnaceState.Firing)
      .SetTemp(1600f)
      .SetSecondsAboveMelting(IwexValues.BfMeltStartDelay - 1f); // about to cross the soak time

    rig.Tick(1);

    Assert.Equal(FurnaceState.Melting, rig.State);
  }

  #endregion

  #region Melting → tapping

  [Fact]
  public void Melting_renders_blast_mix_into_molten_iron()
  {
    var rig = new BlastFurnaceRig()
      .FeedBlast()
      .SetState(FurnaceState.Melting)
      .SetTemp(1600f)
      .SetMeltSeconds(IwexValues.BfMeltIntervalSec - 1f); // a melt cycle completes this tick

    rig.Tick(1);

    Assert.True(rig.MoltenIron > 0f, "a melt cycle should produce molten iron");
  }

  [Fact]
  public void A_melting_furnace_taps_molten_iron_into_the_canal()
  {
    var rig = new BlastFurnaceRig()
      .FeedBlast()
      .WithIronTapAndCanal()
      .SetState(FurnaceState.Melting)
      .SetTemp(1600f)
      .SetMoltenIron(100f);

    rig.Tick(1);

    Assert.True(
      rig.CanalIron > 0,
      "the open tap should pour iron into the canal start"
    );
    Assert.True(
      rig.MoltenIron < 100f,
      "the furnace should give up the tapped iron"
    );
  }

  #endregion

  #region Losing the blast

  [Fact]
  public void Cutting_the_blast_lets_the_melt_fall_back_to_firing()
  {
    // Melting just below the melt point with the blast cut: with no air arriving the combustion
    // term falls to natural draught and the furnace's process temperature collapses to ~970 C, so
    // it cools out of Melting and reverts to Firing once it's been cold long enough.
    var rig = new BlastFurnaceRig()
      .SetState(FurnaceState.Melting)
      .SetTemp(IwexValues.BfIronMeltingPoint - 2f)
      .CutBlast();
    ReflectionHelpers.SetField(rig.Furnace, "_belowMeltingSeconds", 29f);

    rig.Tick(1);

    Assert.Equal(FurnaceState.Firing, rig.State);
  }

  #endregion

  #region Cold vs hot blast, emergent

  // The two claims the design docs make about the furnaces, which nothing used to test: a cold blast
  // furnace runs a high-coke burden (docs/design/iwex.md) and a hot blast furnace is what lets a
  // low-coke burden clear the melt line at all (docs/design/smex.md). Neither is a branch in the
  // code - both fall out of the heat balance, which is why they can be asserted on one furnace class
  // by changing only what is charged into it and whether the blast is preheated.

  private static readonly BurdenMix HighCoke = new(65f, 5f, 30f);
  private static readonly BurdenMix LowCoke = new(85f, 5f, 10f);

  [Fact]
  public void A_high_coke_burden_melts_on_cold_blast()
  {
    var rig = new BlastFurnaceRig(burden: HighCoke)
      .FeedBlast(20f) // pressurised, but straight off the blower - no cowper, no preheat
      .SetState(FurnaceState.Firing);

    rig.Tick(1);

    Assert.Equal(0f, rig.Heat.PreheatGain, 1); // cold blast really is cold
    Assert.True(
      rig.Heat.TProcess > IwexValues.BfIronMeltingPoint,
      $"a high-coke burden should melt on cold blast; settled at {rig.Heat.TProcess} C"
    );
  }

  [Fact]
  public void A_low_coke_burden_needs_hot_blast()
  {
    var cold = new BlastFurnaceRig(burden: LowCoke)
      .FeedBlast(20f)
      .SetState(FurnaceState.Firing);
    cold.Tick(1);

    var hot = new BlastFurnaceRig(burden: LowCoke)
      .FeedBlast(950f) // the same furnace, now with a charged cowper on the line
      .SetState(FurnaceState.Firing);
    hot.Tick(1);

    Assert.True(
      cold.Heat.TProcess < IwexValues.BfIronMeltingPoint,
      $"a low-coke burden should stall on cold blast; settled at {cold.Heat.TProcess} C"
    );
    Assert.True(
      hot.Heat.TProcess > IwexValues.BfIronMeltingPoint,
      $"the same burden should melt once the blast is preheated; settled at {hot.Heat.TProcess} C"
    );
  }

  [Fact]
  public void An_under_pressure_blast_line_runs_the_furnace_as_natural_draught()
  {
    // Blast pressure is the tier gate (docs/design/smex.md): under the threshold the line stops
    // counting as blast at all - preheated or not - and the furnace falls back to what its own stack
    // can pull. No branch does this; it comes out of the air factor.
    var blown = new BlastFurnaceRig()
      .FeedBlast(950f, pressure: IwexValues.BlastPressureThreshold * 2f)
      .SetState(FurnaceState.Firing);
    var starved = new BlastFurnaceRig()
      .FeedBlast(950f, pressure: IwexValues.BlastPressureThreshold / 2f)
      .SetState(FurnaceState.Firing);
    blown.Tick(1);
    starved.Tick(1);

    Assert.Equal(1f, blown.Heat.AirFactor, 3);
    Assert.Equal(IwexValues.BfNaturalDraughtFactor, starved.Heat.AirFactor, 3);
    Assert.False(starved.Heat.BlastSupplied);
    Assert.True(
      starved.Heat.TProcess < blown.Heat.TProcess,
      $"an under-pressure line should run cooler; {starved.Heat.TProcess} C vs {blown.Heat.TProcess} C"
    );
  }

  [Fact]
  public void A_brim_full_hearth_runs_cooler_than_a_thin_charge()
  {
    // Cold charge mass is a real heat sink, so topping a furnace up costs temperature. Counter-
    // intuitive enough that the block info has to show it, but it is the historically correct trade.
    var full = new BlastFurnaceRig(blastMix: 640)
      .FeedBlast(20f)
      .SetState(FurnaceState.Firing);
    var thin = new BlastFurnaceRig(blastMix: 160)
      .FeedBlast(20f)
      .SetState(FurnaceState.Firing);
    full.Tick(1);
    thin.Tick(1);

    Assert.True(
      thin.Heat.TProcess > full.Heat.TProcess,
      $"a thin charge should run hotter; thin {thin.Heat.TProcess} C vs full {full.Heat.TProcess} C"
    );
    Assert.True(full.Heat.ChargeLoss > thin.Heat.ChargeLoss);
  }

  [Fact]
  public void Melt_rate_scales_with_how_far_past_the_melt_line_the_furnace_runs()
  {
    // Where the docs' "cold ~30 u/s, hot ~45 u/s" comes from: one yield constant, two temperatures.
    var rig = new BlastFurnaceRig().FeedBlast().SetState(FurnaceState.Melting);

    rig.SetTemp(IwexValues.BfIronMeltingPoint);
    float atLine = rig.MeltSpeed;
    rig.SetTemp(
      IwexValues.BfIronMeltingPoint + IwexValues.BfMeltMarginReference
    );
    float wellPast = rig.MeltSpeed;

    Assert.Equal(1f, atLine, 2); // no margin, nominal rate
    Assert.Equal(1f + IwexValues.BfMeltMarginGain, wellPast, 2);
  }

  #endregion

  #region Live config

  // Regression (player-reported): /exmod config smex bfmeltstartdelay 30 used to take effect only
  // after a relog, because the furnace cached its tunables once at load. The production tick now
  // re-reads them, so an admin change applies on the next tick without a reload.
  [Fact]
  public void A_live_config_change_to_the_melt_delay_applies_without_a_reload()
  {
    float original = IwexValues.BfMeltStartDelay;
    try
    {
      var rig = new BlastFurnaceRig()
        .FeedBlast()
        .SetState(FurnaceState.Firing);

      // Admin shortens the soak time mid-session.
      IwexValues.Edit(c => c.BfMeltStartDelay = 30f);
      rig.Tick(1);

      Assert.Equal(
        30f,
        (float)ReflectionHelpers.GetField(rig.Furnace, "_meltStartDelay")!,
        3
      );
    }
    finally
    {
      IwexValues.Edit(c => c.BfMeltStartDelay = original);
    }
  }

  #endregion
}
