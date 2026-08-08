using System;
using ExpandedLib.Metals;
using ExpandedLib.Process;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockNetworkMolten;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using SteelmakingExpanded;
using SteelmakingExpanded.BlockStructures.Converter;
using SteelmakingExpanded.BlockStructures.Converter.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The Bessemer converter's dynamic charge process, exercised directly (the full
/// <c>OnProductionTick</c> is gated on four aligned peripherals + a constructed vessel, but the per-
/// state steps and the carbon model are reachable on their own): filling molten pig and seeding its
/// carbon, blowing carbon down to retype pig → Bessemer steel (and past it, over-blow → soft iron),
/// the mass-conserving slag/steel split, the cold-scrap temperature gate + its emergent freeze cap,
/// and the split slag/steel pour out the shared output cell.
/// </summary>
public class ConverterControlProcessTests
{
  // Resolved the same way the control resolves them, so a headless registry (game: convention) and a
  // populated one (iwex:/smex:) both line up between what we push and what the control reads.
  private static string Pig => MetalRegistry.MoltenItemOf("pigiron").ToString();
  private static string Steel =>
    MetalRegistry.MoltenItemOf("bessemersteel").ToString();
  private static string Iron => MetalRegistry.MoltenItemOf("iron").ToString();
  private static string Slag => MetalRegistry.MoltenItemOf("slag").ToString();

  private const float PigMelt = 1150f;

  // Structure-local peripheral offsets (mirrors the private constants in the control).
  private static readonly (int x, int y, int z) InputTapLocal = (1, 1, 2);
  private static readonly (int x, int y, int z) OutputStartLocal = (1, -2, 2);

  private static TestWorld NewWorld()
  {
    var world = new TestWorld();
    // Register both the convention (game:) and the shipped (iwex:/smex:) codes so the resolved token
    // resolves to a real item whatever the static MetalRegistry state is under the test runner.
    foreach (
      var (code, melt) in new[]
      {
        ("game:ingot-pigiron", PigMelt),
        ("iwex:ingot-pigiron", PigMelt),
        ("game:ingot-bessemersteel", 1500f),
        ("smex:ingot-bessemersteel", 1500f),
        ("game:ingot-iron", 1500f),
        ("game:ingot-slag", 1200f),
        ("iwex:slag", 1200f),
      }
    )
      world.RegisterItem(code, melt);
    world.RegisterItem("game:metalbit-iron");
    world.RegisterItem("game:metalbit-steel");
    return world;
  }

  private static BlockEntityConverterControl Control(TestWorld world)
  {
    var be = new BlockEntityConverterControl
    {
      Pos = new BlockPos(0, 8, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "smex:converterbessemercontrol-n",
        1,
        ("side", "north")
      ),
    };
    world.Attach(be);
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
    return be;
  }

  private static ItemStack Metal(TestWorld world, string code, float temp) =>
    MoltenMetal.CreateStack(world.World, code, temp)!;

  private static BlockEntityMoltenCanal PlaceCell(
    TestWorld world,
    BlockEntityConverterControl control,
    (int x, int y, int z) local
  )
  {
    var pos = (BlockPos)
      ReflectionHelpers.Invoke(
        control,
        "GetGlobalPos",
        local.x,
        local.y,
        local.z
      )!;
    var cell = new BlockEntityMoltenCanal
    {
      Block = TestBlocks.Configure(
        new Block(),
        "smex:moltencanal-straight-ns",
        9,
        ("type", "straight"),
        ("orientation", "ns")
      ),
    };
    world.Place(pos, cell.Block, cell);
    world.Attach(cell);
    return cell;
  }

  private static MoltenCharge? Charge(BlockEntityConverterControl be) =>
    ReflectionHelpers.GetField(be, "_charge") as MoltenCharge;

  private static int ChargeUnits(BlockEntityConverterControl be) =>
    Charge(be)?.Units ?? 0;

  private static string ChargeCode(BlockEntityConverterControl be) =>
    Charge(be)?.MetalCode.ToString() ?? "";

  private static float CarbonOf(BlockEntityConverterControl be) =>
    (float)ReflectionHelpers.GetField(be, "_carbon")!;

  private static float SlagOf(BlockEntityConverterControl be) =>
    (float)ReflectionHelpers.GetField(be, "_moltenSlag")!;

  // Loads a pig charge (units + carbon + the pig-mass yield basis) directly, as a fill would have.
  private static void GivePig(
    TestWorld world,
    BlockEntityConverterControl be,
    int units,
    float temp = 1600f
  )
  {
    ReflectionHelpers.SetField(
      be,
      "_charge",
      MoltenCharge.Of(Metal(world, Pig, temp), units)
    );
    ReflectionHelpers.SetField(be, "_carbon", SmexValues.BessemerPigCarbonStart);
    ReflectionHelpers.SetField(be, "_pigCharged", units);
  }

  // Blows exactly the blast that takes the bath's carbon down to `to`, so a test picks the phase
  // (still pig / at steel / over-blown) precisely rather than juggling litres.
  private static void BlowToCarbon(BlockEntityConverterControl be, float to)
  {
    float carbon = CarbonOf(be);
    float blast = Math.Max(
      0f,
      (carbon - to) / SmexValues.BessemerCarbonPerBlastLitre
    );
    ReflectionHelpers.Invoke(be, "BlowStep", blast);
  }

  private static HeatBalance HeatBalance(
    BlockEntityConverterControl be,
    float airFactor
  ) =>
    (HeatBalance)
      ReflectionHelpers.Invoke(be, "ComputeHeatBalance", airFactor)!;

  #region Filling

  [Fact]
  public void TickFilling_draws_molten_pig_and_seeds_the_carbon()
  {
    var world = NewWorld();
    var be = Control(world);
    var input = PlaceCell(world, be, InputTapLocal);
    input.PushMetal(50, Metal(world, Pig, 1600f), world.World);

    ReflectionHelpers.Invoke(be, "TickFilling", 1f);

    Assert.Equal(50, ChargeUnits(be));
    Assert.True(input.IsCellEmpty); // drained into the vessel
    // Fresh pig seeds the blow's carbon and records the pig-mass yield basis.
    Assert.Equal(SmexValues.BessemerPigCarbonStart, CarbonOf(be), 4);
    Assert.Equal(50, (int)ReflectionHelpers.GetField(be, "_pigCharged")!);
  }

  #endregion

  #region The dynamic carbon model

  [Fact]
  public void Blowing_to_the_carbon_target_retypes_pig_to_bessemer_steel()
  {
    var world = NewWorld();
    var be = Control(world);
    GivePig(world, be, 1000);

    // Blow down into the steel window (below the target, still above the over-blow floor).
    BlowToCarbon(
      be,
      (SmexValues.BessemerSteelCarbonTarget + SmexValues.BessemerOverblowCarbon)
        / 2f
    );

    Assert.Contains("bessemersteel", ChargeCode(be));
  }

  [Fact]
  public void Stopping_the_blow_above_the_target_leaves_it_as_pig()
  {
    var world = NewWorld();
    var be = Control(world);
    GivePig(world, be, 1000);

    // A short blow that does not reach the target: still pig, not yet steel.
    BlowToCarbon(be, SmexValues.BessemerSteelCarbonTarget * 2f);

    Assert.Contains("pigiron", ChargeCode(be));
  }

  [Fact]
  public void Blowing_past_the_target_over_blows_the_steel_to_ingot_iron()
  {
    var world = NewWorld();
    var be = Control(world);
    GivePig(world, be, 1000);

    // First reach steel, then keep blowing past the over-blow floor.
    BlowToCarbon(
      be,
      (SmexValues.BessemerSteelCarbonTarget + SmexValues.BessemerOverblowCarbon)
        / 2f
    );
    Assert.Contains("bessemersteel", ChargeCode(be));

    BlowToCarbon(be, SmexValues.BessemerOverblowCarbon / 2f);

    Assert.Equal(Iron, ChargeCode(be)); // soft ingot iron, the deliberate plain-iron path
  }

  [Fact]
  public void A_full_blow_conserves_mass_steel_plus_slag_never_exceeding_the_pig()
  {
    var world = NewWorld();
    var be = Control(world);
    GivePig(world, be, 1000);

    // Blow the whole heat down to steel: mass sheds off the charge into slag (+ gas) as it goes.
    BlowToCarbon(be, SmexValues.BessemerSteelCarbonTarget / 2f);

    int steel = ChargeUnits(be);
    float slag = SlagOf(be);
    Assert.Contains("bessemersteel", ChargeCode(be));
    // R2: never create matter - the gas share is the only thing that left.
    Assert.True(steel + slag <= 1000, $"steel {steel} + slag {slag} > 1000");
    // ~90u steel + ~6u slag per 100u pig (the shipped yields), leaving the ~4u gas.
    Assert.Equal(1000 * SmexValues.BessemerSteelYield, steel, 0f);
    Assert.Equal(1000 * SmexValues.BessemerSlagYield, slag, 1f);
  }

  #endregion

  #region Cold steel scrap - the temperature gate

  [Fact]
  public void Cold_scrap_lowers_the_process_temperature()
  {
    var world = NewWorld();
    var be = Control(world);
    GivePig(world, be, 2000);

    float peakNoScrap = HeatBalance(be, 1f).TProcess;

    ReflectionHelpers.SetField(be, "_scrapUnits", 400);
    float peakWithScrap = HeatBalance(be, 1f).TProcess;

    Assert.True(
      peakWithScrap < peakNoScrap,
      $"scrap should lower the peak: {peakWithScrap} !< {peakNoScrap}"
    );
  }

  [Fact]
  public void A_modest_scrap_charge_still_refines_and_yields_extra_steel()
  {
    var world = NewWorld();
    var be = Control(world);
    GivePig(world, be, 1000);
    ReflectionHelpers.SetField(be, "_scrapUnits", 200);

    // Modest scrap keeps the bath above the refine floor, so the blow completes.
    Assert.True(HeatBalance(be, 1f).TProcess >= SmexValues.BessemerRefineTemperature);

    BlowToCarbon(be, SmexValues.BessemerSteelCarbonTarget / 2f);

    // The scrap melts into the steel at the target: pig-steel (~900) + scrap-steel (~200 × yield).
    int expected =
      (int)(1000 * SmexValues.BessemerSteelYield)
      + (int)Math.Round(200 * SmexValues.BessemerScrapSteelYield);
    Assert.Contains("bessemersteel", ChargeCode(be));
    Assert.Equal(expected, ChargeUnits(be));
    Assert.True(
      ChargeUnits(be) > 1000 * SmexValues.BessemerSteelYield,
      "scrap should yield more steel than pig alone"
    );
  }

  [Fact]
  public void An_excessive_scrap_charge_freezes_the_bath_before_it_refines()
  {
    var world = NewWorld();
    var be = Control(world);
    GivePig(world, be, 1000, temp: 1600f);
    // Far past the practical ceiling: the blast cannot keep this much cold mass molten.
    ReflectionHelpers.SetField(be, "_scrapUnits", 3000);

    HeatBalance hb = HeatBalance(be, 1f);
    Assert.True(
      hb.TProcess < PigMelt,
      $"an excessive scrap charge should drag T_process ({hb.TProcess}) below the melt point"
    );

    // The blow drives the bath to that frozen equilibrium; the solidify latch then catches it - the
    // emergent scrap cap reusing the existing solidified/chisel path, no hardcoded limit.
    ReflectionHelpers.Invoke(be, "HoldBathTemperature", hb.TProcess);
    ReflectionHelpers.Invoke(be, "UpdateSolidified");
    Assert.True((bool)ReflectionHelpers.GetField(be, "_solidified")!);
  }

  #endregion

  #region Split-pour out the shared output cell

  [Fact]
  public void TickSteelPouring_pushes_the_steel_into_the_output_cell()
  {
    var world = NewWorld();
    var be = Control(world);
    var output = PlaceCell(world, be, OutputStartLocal);
    ReflectionHelpers.SetField(
      be,
      "_charge",
      MoltenCharge.Of(Metal(world, Steel, 1600f), 300)
    );

    ReflectionHelpers.Invoke(be, "TickSteelPouring", 1f);

    Assert.True(output.CellAmount > 0);
    Assert.Contains("bessemersteel", output.CellMetalType);
    Assert.True(ChargeUnits(be) < 300); // drained toward the canal
  }

  [Fact]
  public void TickSlagPouring_pushes_slag_out_of_the_same_output_cell()
  {
    var world = NewWorld();
    var be = Control(world);
    var output = PlaceCell(world, be, OutputStartLocal);
    // A steel charge is present (its temperature feeds the slag), with slag floating on top.
    ReflectionHelpers.SetField(
      be,
      "_charge",
      MoltenCharge.Of(Metal(world, Steel, 1600f), 300)
    );
    ReflectionHelpers.SetField(be, "_moltenSlag", 40f);

    ReflectionHelpers.Invoke(be, "TickSlagPouring", 1f);

    Assert.True(output.CellAmount > 0);
    Assert.Contains("slag", output.CellMetalType);
    Assert.True(SlagOf(be) < 40f); // slag drained, steel untouched
    Assert.Equal(300, ChargeUnits(be));
  }

  #endregion

  #region Solidify latch

  [Theory]
  [InlineData(1600f, false)] // above the 1150 pig melt point -> stays liquid
  [InlineData(300f, true)] // below melt -> latches solid
  public void UpdateSolidified_latches_against_the_melting_point(
    float temp,
    bool expected
  )
  {
    var world = NewWorld();
    var be = Control(world);
    ReflectionHelpers.SetField(
      be,
      "_charge",
      MoltenCharge.Of(Metal(world, Pig, temp), 30)
    );

    ReflectionHelpers.Invoke(be, "UpdateSolidified");

    Assert.Equal(
      expected,
      (bool)ReflectionHelpers.GetField(be, "_solidified")!
    );
  }

  #endregion

  #region Operability gate

  [Fact]
  public void CanOperate_refuses_an_incomplete_structure_with_a_reason()
  {
    var world = NewWorld();
    var be = Control(world);

    bool ok = be.CanOperate(out string error);

    Assert.False(ok);
    Assert.NotEqual("", error);
  }

  [Fact]
  public void IsConverterPresent_is_false_with_no_vessel_placed()
  {
    var world = NewWorld();
    Assert.False(Control(world).IsConverterPresent());
  }

  #endregion
}
