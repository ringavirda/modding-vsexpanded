using ExpandedLib.Testing;
using LowPressureExpanded.BlockStructures.Boiler;
using LowPressureExpanded.BlockStructures.Boiler.BlockEntities;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// The boiler's internal steam bookkeeping that runs outside the engine draw: bleeding excess steam
/// out of the open lid (<c>VentExcessSteam</c>), condensing leftover steam back into water after a
/// shutdown (<c>CondenseInternal</c>), and the shutdown reset. Pure field math on a primed Cornish
/// boiler, so no Api or structure is required - this pins the safety behaviour (venting never
/// collapses working pressure, condensation can never run the pressure up toward a burst).
/// </summary>
public class BoilerSteamCycleTests
{
  private const float Capacity = 800f; // CornishBoilerCapacity
  private const float MaxBoilWater = 500f; // CornishBoilerMaxBoilWater
  private const float VentRate = 200f; // BoilerLidVentRate (L/s)
  private const float Expansion = 16f; // SteamExpansionFactor

  private static BlockEntityBoilerCornish Boiler(
    float water,
    float steam,
    BlockEntityBoiler.BoilerState state = BlockEntityBoiler.BoilerState.Idle
  )
  {
    var be = new BlockEntityBoilerCornish();
    ReflectionHelpers.SetField(be, "_waterVolume", water);
    ReflectionHelpers.SetField(be, "_steamVolume", steam);
    ReflectionHelpers.SetField(be, "_state", state);
    return be;
  }

  private static float Water(BlockEntityBoiler be) =>
    (float)ReflectionHelpers.GetField(be, "_waterVolume")!;

  private static float Steam(BlockEntityBoiler be) =>
    (float)ReflectionHelpers.GetField(be, "_steamVolume")!;

  #region Lid venting

  [Fact]
  public void Venting_idle_steam_bleeds_down_at_the_lid_rate()
  {
    var be = Boiler(water: 0f, steam: 1000f); // Idle -> floor 0
    ReflectionHelpers.Invoke(be, "VentExcessSteam", 1f);
    Assert.Equal(1000f - VentRate, Steam(be), 3); // 800 L left after one second
  }

  [Fact]
  public void Venting_while_running_stops_at_the_headspace_floor()
  {
    // Boiling: the floor is the steam that fills the head-space above the water
    // (Capacity - water), so venting never collapses the working pressure to zero.
    var be = Boiler(
      water: 500f,
      steam: 400f,
      state: BlockEntityBoiler.BoilerState.Boiling
    );
    // floor = 800 - 500 = 300; only 100 L sit above it, less than the 200 L/s cap.
    ReflectionHelpers.Invoke(be, "VentExcessSteam", 1f);
    Assert.Equal(300f, Steam(be), 3);
  }

  [Fact]
  public void Venting_does_nothing_below_the_floor()
  {
    var be = Boiler(
      water: 500f,
      steam: 250f,
      state: BlockEntityBoiler.BoilerState.Boiling
    );
    ReflectionHelpers.Invoke(be, "VentExcessSteam", 1f);
    Assert.Equal(250f, Steam(be), 3); // floor 300 already above the steam present
  }

  #endregion

  #region Internal condensation

  [Fact]
  public void Condensing_turns_steam_back_into_water_at_the_expansion_ratio()
  {
    var be = Boiler(water: 100f, steam: 200f);
    // pressure 200/700 = 0.29 < 16, water room 400 L; half a second condenses 100 L steam.
    ReflectionHelpers.Invoke(be, "CondenseInternal", 0.5f);
    Assert.Equal(100f, Steam(be), 3); // 200 - 100
    Assert.Equal(100f + 100f / Expansion, Water(be), 3); // +6.25 L water
  }

  [Fact]
  public void Condensing_refuses_above_the_expansion_pressure_so_it_cannot_drive_a_burst()
  {
    // pressure = steam / (Capacity - water); 13000 / 800 = 16.25 >= 16 -> refuse.
    var be = Boiler(water: 0f, steam: 13000f);
    ReflectionHelpers.Invoke(be, "CondenseInternal", 1f);
    Assert.Equal(13000f, Steam(be), 3);
    Assert.Equal(0f, Water(be), 3);
  }

  [Fact]
  public void Condensing_stops_when_the_vessel_is_full_to_the_boil_ceiling()
  {
    var be = Boiler(water: MaxBoilWater, steam: 100f); // no water room left
    ReflectionHelpers.Invoke(be, "CondenseInternal", 1f);
    Assert.Equal(100f, Steam(be), 3);
    Assert.Equal(MaxBoilWater, Water(be), 3);
  }

  #endregion

  #region Boil step

  [Fact]
  public void BoilStep_expands_water_into_steam_at_the_taxonomy_resolved_ratio()
  {
    // The boiler now reads its water→steam expansion from the medium taxonomy (Water's vaporisation
    // factor), falling back to the lpex steam-expansion constant when the medium leaves it unset -
    // which the built-in Water does, so the ratio stays 16:1. Pins that the taxonomy-driven factor
    // resolves to exactly the conversion the hardcoded constant used to produce (byte-parity).
    var be = Boiler(
      water: 100f,
      steam: 0f,
      state: BlockEntityBoiler.BoilerState.Boiling
    );
    ReflectionHelpers.Invoke(be, "BoilStep", 1f);

    float waterUsed = 100f - Water(be);
    Assert.True(waterUsed > 0f); // it actually boiled some water
    Assert.Equal(waterUsed * Expansion, Steam(be), 3); // 16 L steam per 1 L water
  }

  #endregion

  #region Shutdown reset

  [Fact]
  public void Shutting_down_returns_to_idle_and_clears_the_timers()
  {
    var be = Boiler(
      water: 300f,
      steam: 200f,
      state: BlockEntityBoiler.BoilerState.Boiling
    );
    ReflectionHelpers.SetField(be, "_heatingSeconds", 120f);
    ReflectionHelpers.SetField(be, "_shutdownSeconds", 30f);
    ReflectionHelpers.SetField(be, "_burning", true);

    ReflectionHelpers.Invoke(be, "ShutDown");

    Assert.Equal(
      BlockEntityBoiler.BoilerState.Idle,
      (BlockEntityBoiler.BoilerState)ReflectionHelpers.GetField(be, "_state")!
    );
    Assert.Equal(
      0f,
      (float)ReflectionHelpers.GetField(be, "_heatingSeconds")!,
      3
    );
    Assert.Equal(
      0f,
      (float)ReflectionHelpers.GetField(be, "_shutdownSeconds")!,
      3
    );
    Assert.False((bool)ReflectionHelpers.GetField(be, "_burning")!);
  }

  #endregion

  #region Steam pressure cap (over-pressure readout guard)

  // Every production tick ends with CapSteamToCeiling, which discards steam above the choke ceiling so
  // InternalPressure can never read past MaxOutputPressure - the "hundreds of atm" bug. The cap sits AT
  // the ceiling so a burning, closed boiler still trips the burst grace (fires at >= ceiling).
  private const float ChokePressure = 5f; // CornishBoilerMaxOutputPressure

  [Fact]
  public void Capping_discards_steam_above_the_choke_ceiling()
  {
    // 5000 L steam over 800-300 = 500 L free space would read 10 atm, double the 5 atm ceiling.
    var be = Boiler(water: 300f, steam: 5000f);

    ReflectionHelpers.Invoke(be, "CapSteamToCeiling");

    Assert.Equal(ChokePressure, be.InternalPressure, 3);
    Assert.Equal(ChokePressure * (Capacity - 300f), Steam(be), 3);
  }

  [Fact]
  public void Capping_leaves_a_below_ceiling_boiler_untouched()
  {
    // 1000 L over 500 L free = 2 atm, well under the 5 atm ceiling - nothing is discarded.
    var be = Boiler(water: 300f, steam: 1000f);

    ReflectionHelpers.Invoke(be, "CapSteamToCeiling");

    Assert.Equal(1000f, Steam(be), 3);
  }

  #endregion
}
