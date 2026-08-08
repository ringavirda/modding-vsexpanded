using SteelmakingExpanded;
using SteelmakingExpanded.BlockStructures.Converter;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// End-to-end steelmaking scenarios (handbook bessemer article): molten pig iron is charged from a
/// canal into the Bessemer converter, blown with blast drawn off a live gas network until carbon falls
/// to the steel target (or past it, to soft iron), then poured out with its slag byproduct through the
/// shared output cell. Covers the molten input/output cells, the blast gas network and the carbon model.
/// </summary>
public class BessemerScenarioTests {
  #region The machine stands (emergent)

  /// <summary>
  /// Drives a converter raised from its layout through the same public production tick the game calls,
  /// so commissioning and the block codes of its service blocks are covered. The other scenarios here
  /// set one state at a time and bypass that tick.
  /// </summary>
  [Fact]
  public void A_built_converter_commissions_and_refines_through_its_own_production_tick() {
    var rig = new ConverterRig();

    // All four commissioning gates come from the built structure, not from a set flag.
    Assert.True(
      rig.IsCommissioned,
      "shell, vessel and both service ports should all check out"
    );

    rig.PourPigToInput(50).SetMechPower(1f).ChargeBlast(5f);

    // Switching to Filling is gated: it is refused on an unbuilt or unpowered machine. The public
    // tick then draws the charge in and starts the blow.
    rig.SetState(ConverterOpState.Filling);
    rig.ProductionTick();
    rig.SetState(ConverterOpState.Normal);
    rig.ProductionTick(2);

    Assert.Equal(50, rig.ContentUnits);
    Assert.Contains("pigiron", rig.ContentCode);
    Assert.True(
      rig.Carbon < SmexValues.BessemerPigCarbonStart,
      $"the blow should have started oxidising carbon, was {rig.Carbon}"
    );
  }

  [Fact]
  public void A_converter_whose_shell_is_breached_stops_at_the_first_gate() {
    // Removing one filler from the body makes the control refuse on its own next tick.
    var rig = new ConverterRig();
    rig.PourPigToInput(50).SetMechPower(1f).ChargeBlast(5f);

    rig.World.Place(
      rig.Structure.Cell(0, 1, 1),
      ExpandedLib.Testing.TestBlocks.Configure(
        new Vintagestory.API.Common.Block(),
        "game:air",
        0
      )
    );
    rig.World.AdvanceBlockEntityTime(3000);
    Assert.False(rig.Control.StructureComplete);

    rig.ProductionTick();

    Assert.Equal(0, rig.ContentUnits); // never drew the charge in
  }

  #endregion

  #region Full charge → blow → pour cycle

  [Fact]
  public void Molten_pig_is_charged_blown_into_steel_and_poured_to_the_output_canal() {
    var rig = new ConverterRig();

    // 1. Fill: the furnace tap pours molten pig iron into the input canal, the converter draws it in.
    rig.PourPigToInput(50);
    rig.Fill();
    Assert.Equal(50, rig.ContentUnits);
    Assert.True(
      rig.Input.IsCellEmpty,
      "the input cell should have drained into the vessel"
    );
    Assert.Contains("pigiron", rig.ContentCode);

    // 2. Blow: with blast flowing the converter oxidises carbon out, drawing blast off the gas network.
    rig.ChargeBlast(3f);
    float carbonBefore = rig.Carbon;
    float blastBefore = rig.BlastVolume;
    rig.Refine();
    Assert.True(rig.Carbon < carbonBefore, "the blow should oxidise carbon");
    Assert.True(
      rig.BlastVolume < blastBefore,
      "the blow should consume blast from the network"
    );

    // 3. Finish the blow (fast-forwarded through the carbon math): the charge becomes Bessemer steel.
    rig.BlowToSteel();
    Assert.Contains("bessemersteel", rig.ContentCode);

    // 4. Pour: the finished steel drains into the output canal, ready to travel the molten network.
    rig.DrainSteel();
    Assert.True(
      rig.Output.CellAmount > 0,
      "the output canal should receive the steel"
    );
    Assert.Contains("bessemersteel", rig.Output.CellMetalType);
  }

  [Fact]
  public void The_blow_makes_slag_that_pours_off_the_shallow_tilt_out_the_shared_cell() {
    var rig = new ConverterRig();
    rig.ChargePig(100);
    rig.BlowToSteel();

    // A full blow leaves steel in the charge and a floating slag pool beside it (mass-conserving).
    Assert.Contains("bessemersteel", rig.ContentCode);
    Assert.True(rig.SlagUnits > 0f, "the blow should accumulate slag");
    Assert.True(
      rig.ContentUnits + rig.SlagUnits <= 100,
      "steel + slag must never exceed the pig charged"
    );

    // Shallow tilt (SlagPouring) skims the slag off through the same output cell the steel uses.
    rig.DrainSlag();
    Assert.True(rig.Output.CellAmount > 0);
    Assert.Contains("slag", rig.Output.CellMetalType);
    Assert.True(rig.SlagUnits < 1f, "the slag should have drained");
  }

  [Fact]
  public void Over_blowing_past_the_steel_target_yields_soft_ingot_iron() {
    var rig = new ConverterRig();
    rig.PourPigToInput(50);
    rig.Fill();
    rig.BlowToSteel();
    Assert.Contains("bessemersteel", rig.ContentCode);

    // Blowing on past the over-blow floor is the route to plain iron.
    rig.OverBlowToIron();
    Assert.Contains("ingot-iron", rig.ContentCode);
  }

  // A converter serves many heats: once poured out it must accept and refine a fresh pig charge, with
  // no leftover steel latching the fill guard on a type mismatch.
  [Fact]
  public void A_second_pig_heat_can_be_charged_and_refined_after_pouring_the_first() {
    var rig = new ConverterRig();

    // First heat: pig → steel → poured out, emptying the vessel.
    rig.PourPigToInput(50);
    rig.Fill();
    rig.BlowToSteel();
    Assert.Contains("bessemersteel", rig.ContentCode);
    rig.DrainSteel();
    Assert.Equal(0, rig.ContentUnits); // vessel emptied - no leftover steel

    // Second heat: a fresh pig charge must fill cleanly and refine to steel again.
    rig.PourPigToInput(50);
    rig.Fill();
    Assert.Equal(50, rig.ContentUnits);
    Assert.Contains("pigiron", rig.ContentCode);

    rig.BlowToSteel();
    Assert.Contains("bessemersteel", rig.ContentCode);
  }

  #endregion

  #region Cold-scrap temperature gate

  [Fact]
  public void A_modest_scrap_charge_refines_and_yields_more_steel_than_pig_alone() {
    var rig = new ConverterRig();
    rig.ChargePig(100);
    rig.ChargeScrap(40); // cold steel bits, melted in at the target
    rig.BlowToSteel();

    Assert.Contains("bessemersteel", rig.ContentCode);
    // Pig alone would yield ~90 steel (100 × yield); the melted scrap adds to it.
    int pigOnly = (int)(100 * SmexValues.BessemerSteelYield);
    Assert.True(
      rig.ContentUnits > pigOnly,
      $"scrap should yield more steel than pig alone: {rig.ContentUnits} !> {pigOnly}"
    );
  }

  #endregion

  #region Blast dependency

  [Fact]
  public void Without_blast_the_charge_does_not_refine() {
    var rig = new ConverterRig();
    rig.PourPigToInput(50);
    rig.Fill();

    float before = rig.Carbon;
    rig.Refine(); // gas network is empty - no blast to draw

    Assert.Equal(before, rig.Carbon, 4); // carbon did not fall
    Assert.Contains("pigiron", rig.ContentCode); // still raw pig, not steel
  }

  #endregion

  #region Mechanical-power gate (engine→generator→transmission→converter)

  [Fact]
  public void A_turning_transmission_gives_the_converter_power() {
    var rig = new ConverterRig();
    rig.SetMechPower(speed: 1f); // the engine's MP generator spins the transmission axle

    Assert.True(
      rig.HasPower,
      "a turning transmission should power the converter"
    );
  }

  [Fact]
  public void A_stalled_transmission_leaves_the_converter_unpowered() {
    var rig = new ConverterRig();
    rig.SetMechPower(speed: 0f); // axle present but not turning (engine off / overstressed)

    Assert.False(rig.HasPower);
  }

  [Fact]
  public void With_no_mechanical_network_the_converter_has_no_power() {
    var rig = new ConverterRig();
    // The transmission block is placed but was never spun up (no MP network behind it).
    Assert.False(rig.HasPower);
  }

  #endregion
}
