using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Products.BlockEntities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The cupola furnace's re-melting process driven end to end (docs/design/iwex.md): a charged, lit,
/// blast-fed shaft climbs past cast iron's melt line, enters Melting, re-melts remelt burden into molten
/// cast iron, taps cast iron out the lower tap and slag out the upper, and - extinguished mid-heat -
/// freezes its pool onto the hearth as solid cast iron while the rest of the burden survives as
/// salvageable spent charge, never slag. Plus the family gate: ore burden burns in the cupola but never
/// converts. Exercises the gated firing/melting tick with its real peripherals via <see cref="CupolaRig"/>.
/// </summary>
public class CupolaScenarioTests
{
  private static readonly BurdenMix Remelt = new(60f, 5f, 35f);

  #region Ignition + phase progression

  [Fact]
  public void A_charged_lit_shaft_fed_blast_ignites()
  {
    var rig = new CupolaRig(burden: Remelt).FeedBlast();

    rig.Tick(1); // full shaft + blast + all piles lit -> catches

    Assert.Equal(FurnaceState.Firing, rig.State);
  }

  [Fact]
  public void Sustained_heat_above_the_cast_iron_melt_point_transitions_to_melting()
  {
    var rig = new CupolaRig(burden: Remelt)
      .FeedBlast()
      .SetState(FurnaceState.Firing)
      .SetTemp(IwexValues.CupolaCastIronMeltingPoint + 100f)
      .SetSecondsAboveMelting(IwexValues.CupolaMeltStartDelay - 1f); // about to cross the soak time

    rig.Tick(1);

    Assert.Equal(FurnaceState.Melting, rig.State);
  }

  #endregion

  #region Melting -> tapping

  [Fact]
  public void Melting_renders_remelt_burden_into_molten_cast_iron()
  {
    var rig = new CupolaRig(burden: Remelt)
      .FeedBlast()
      .SetState(FurnaceState.Melting)
      .SetTemp(IwexValues.CupolaCastIronMeltingPoint + 200f)
      .SetMeltSeconds(IwexValues.CupolaMeltIntervalSec - 1f); // a melt cycle completes this tick

    rig.Tick(1);

    Assert.True(
      rig.MoltenCastIron > 0f,
      "a melt cycle should render molten cast iron"
    );
  }

  [Fact]
  public void A_melting_cupola_taps_cast_iron_low_and_slag_high()
  {
    var rig = new CupolaRig(burden: Remelt)
      .FeedBlast()
      .WithCastIronTapAndCanal()
      .WithSlagTapAndCanal()
      .SetState(FurnaceState.Melting)
      .SetTemp(IwexValues.CupolaCastIronMeltingPoint + 200f)
      .SetMoltenCastIron(150f)
      .SetMoltenSlag(80f);

    rig.Tick(1);

    Assert.True(
      rig.CastIronCanalUnits > 0,
      "the lower tap should pour cast iron into its canal"
    );
    // The cupola is UNCHANGED by the blast furnace's pig-iron flip: it still drains cast iron
    // (iwex/game:ingot-castiron), the counterpart to the blast furnace's pig-iron drain.
    string metal = rig.CastIronCanalMetalType!;
    Assert.Contains("castiron", metal);
    Assert.True(
      rig.SlagCanalUnits > 0,
      "the upper tap should pour slag into its canal"
    );
    Assert.True(rig.MoltenCastIron < 150f, "the cupola gives up the tapped cast iron");
    Assert.True(rig.MoltenSlag < 80f, "the cupola gives up the tapped slag");
  }

  #endregion

  #region Extinguish residue

  [Fact]
  public void Extinguishing_freezes_cast_iron_and_leaves_salvageable_burden_and_no_slag()
  {
    var rig = new CupolaRig(burden: Remelt)
      .FeedBlast()
      .SetState(FurnaceState.Firing)
      .SetTemp(IwexValues.CupolaCastIronMeltingPoint + 100f)
      .SetMoltenCastIron(200f)
      .SetMoltenSlag(120f)
      // The fuel is spent this tick, so the furnace goes out with metal still in the hearth.
      .SetFuelBurnSeconds(IwexValues.CupolaMaxFuelBurnTime);

    rig.Tick(1);

    Assert.Equal(FurnaceState.Idle, rig.State);

    // The pool froze onto the hearth floor as solid CAST IRON (not iron, not slag).
    Assert.Equal(
      "iwex:solidifiedcastiron",
      rig.BlockAtLocal(0, 1, 0).Code?.ToString()
    );
    Assert.True(
      rig.BlockEntityAtLocal(0, 1, 0) is BlockEntitySolidifiedIron { MetalCount: > 0 },
      "the frozen block should carry a positive cast-iron bit count"
    );

    // The rest of the burden is burned out, not destroyed: still remelt burden, its scrap/flux intact
    // (salvageable), only its coke stripped down.
    var salvage = rig.PileAtLocal(0, 2, 0);
    Assert.NotNull(salvage);
    ItemStack? left = salvage!.inventory[0].Itemstack;
    Assert.NotNull(left);
    BurdenMix leftMix = Burden.Read(left);
    Assert.True(leftMix.Iron > 0f, "the scrap survives as salvage");
    Assert.True(leftMix.Fuel < Remelt.Fuel, "the coke is burned out");

    // Nothing on the extinguish path makes slag: the slag pool is simply cleared.
    Assert.Equal(0f, rig.MoltenSlag, 3);
    Assert.NotEqual("iwex:slag", rig.BlockAtLocal(0, 1, 0).Code?.ToString());
  }

  #endregion

  #region Burden family gate

  // The blast furnace's ore burden charged into a cupola: the shaft still lights and burns (it is real
  // fuel), but the family gate refuses to render it into cast iron. Remelt burden in the same rig
  // converts normally (Melting_renders_remelt_burden_into_molten_cast_iron above), so this is the
  // wrong-family half of "right family melts, wrong family burns but never converts".

  [Fact]
  public void A_cupola_will_not_convert_an_ore_burden_charge()
  {
    var rig = new CupolaRig(burden: Remelt, chargeCode: "burden")
      .FeedBlast()
      .SetState(FurnaceState.Melting)
      .SetTemp(IwexValues.CupolaCastIronMeltingPoint + 200f)
      .SetMeltSeconds(IwexValues.CupolaMeltIntervalSec - 1f); // a melt cycle WOULD complete this tick

    rig.Tick(1);

    Assert.Equal(0f, rig.MoltenCastIron, 3); // hot and "ready", but the wrong family never converts
  }

  [Fact]
  public void A_wrong_family_shaft_still_reads_full_so_it_lights_and_burns()
  {
    // Family-blind fullness: the cupola does not refuse to light a shaft packed with ore burden - it
    // lights and burns it out. Only the conversion is gated, so it holds at heat without ever melting.
    var rig = new CupolaRig(burden: Remelt, chargeCode: "burden")
      .FeedBlast()
      .SetState(FurnaceState.Firing)
      .SetTemp(IwexValues.CupolaCastIronMeltingPoint + 100f)
      .SetSecondsAboveMelting(IwexValues.CupolaMeltStartDelay - 1f); // would cross into Melting this tick

    rig.Tick(1);

    Assert.Equal(FurnaceState.Firing, rig.State); // soak completes, but the transition stays blocked
  }

  #endregion

  #region Slower than the blast furnace

  [Fact]
  public void The_cupola_renders_at_about_half_the_blast_furnaces_nominal_rate()
  {
    // Design intent (docs/design/iwex.md): ~2 cupolas keep pace with one blast furnace. The nominal
    // render rate is per-cycle yield / melt interval; the cupola's is ~half the blast furnace's, which
    // is what the Cupola* config keys encode.
    float bfRate = IwexValues.BfIronPerMeltCycle / IwexValues.BfMeltIntervalSec;
    float cupolaRate =
      IwexValues.CupolaCastIronPerMeltCycle / IwexValues.CupolaMeltIntervalSec;

    Assert.True(
      cupolaRate < bfRate,
      $"the cupola should melt slower; cupola {cupolaRate} u/s vs blast furnace {bfRate} u/s"
    );
    Assert.Equal(0.5f, cupolaRate / bfRate, 1); // ~2 cupolas per blast furnace
  }

  #endregion
}
