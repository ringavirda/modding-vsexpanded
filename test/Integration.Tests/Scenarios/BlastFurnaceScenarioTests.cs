using ExpandedLib.Testing;
using SteelmakingExpanded;
using IronworkingExpanded;
using IronworkingExpanded.BlockStructures.Furnace;
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
    // A lit, firing furnace at its natural ceiling (1420 C, below iron's 1482 C melt point).
    var rig = new BlastFurnaceRig()
      .FeedBlast(950f)
      .SetState(FurnaceState.Firing)
      .SetTemp(IwexValues.BfNaturalMaxTemp);

    rig.Tick(30); // hot blast pushes the boosted ceiling well above the melt point

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
    // Melting just below the melt point with the blast cut: it can only reach the natural ceiling
    // (1420 C), so it cools out of Melting and reverts to Firing once it's been cold long enough.
    var rig = new BlastFurnaceRig()
      .SetState(FurnaceState.Melting)
      .SetTemp(IwexValues.BfIronMeltingPoint - 2f)
      .CutBlast();
    ReflectionHelpers.SetField(rig.Furnace, "_belowMeltingSeconds", 29f);

    rig.Tick(1);

    Assert.Equal(FurnaceState.Firing, rig.State);
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
