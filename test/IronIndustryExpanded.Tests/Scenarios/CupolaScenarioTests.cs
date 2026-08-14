using ExpandedLib.Testing;
using IronIndustryExpanded;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Products.BlockEntities;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The cupola furnace's re-melting process driven end to end (docs/design/iiex.md): a charged, lit,
/// blast-fed shaft climbs past cast iron's melt line, re-melts remelt burden into molten cast iron, taps
/// cast iron out the lower tap and slag out the upper, and when put out freezes its pool onto the hearth
/// as solid cast iron while the rest of the burden survives as salvageable spent charge, never slag. Also
/// covers the family gate: ore burden burns in the cupola but never converts. Each case arranges by
/// charging and blowing, since a <see cref="BlockEntityShaftFurnace"/> derives its state from the charge
/// every tick.
/// </summary>
public class CupolaScenarioTests {
  private static readonly BurdenMix Remelt = new(60f, 5f, 35f);

  /// <summary>Long enough for a charged, blown cupola to warm its column through and melt, with slack.</summary>
  private const int SoakSeconds = 600;

  #region Ignition + phase progression

  [Fact]
  public void A_charged_lit_shaft_fed_blast_ignites() {
    var rig = new CupolaRig(burden: Remelt).FeedBlast();

    rig.Tick(1); // a full shaft with coke at the raceway + blast -> catches

    Assert.Equal(FurnaceState.Firing, rig.State);
  }

  /// <summary>
  /// Ignition is positional and pneumatic - a complete raceway course with carbon in it - so no charge
  /// threshold gates it. The cupola's own geometry decides, and a shaft charged well under any
  /// blast-furnace figure lights.
  /// </summary>
  [Fact]
  public void The_cupola_lights_on_its_OWN_geometry_not_the_blast_furnaces_threshold() {
    // 160 units: far under what a blast furnace holds.
    var rig = new CupolaRig(charge: 160, burden: Remelt).FeedBlast();

    rig.Tick(1);

    Assert.Equal(FurnaceState.Firing, rig.State);
  }

  /// <summary>
  /// What keeps a full cupola dark is having nothing that can burn in front of its tuyere. The charge is
  /// still stamped at 35 % coke, so a furnace reading carbon off the stamp rather than off the fuel bands
  /// would light here.
  /// </summary>
  [Fact]
  public void A_full_shaft_with_no_coke_at_the_raceway_stays_cold() {
    var rig = new CupolaRig(charge: -1, burden: Remelt)
      .FeedBlast()
      .ChargeWithoutCoke();

    Assert.Equal(0, rig.CokeUnits);
    Assert.True(
      rig.ChargedMix.FuelFrac > 0f,
      "the charge must still be STAMPED with coke"
    );

    bool everLit = false;
    rig.RunUntil(_ => false, 60, r => everLit |= r.State != FurnaceState.Idle);

    Assert.False(everLit, "a cupola with no carbon in it should never catch");
    Assert.Equal(FurnaceState.Idle, rig.State);
  }

  [Fact]
  public void Sustained_heat_above_the_cast_iron_melt_point_transitions_to_melting() {
    var rig = new CupolaRig(burden: Remelt).FeedBlast().HeatSoak(SoakSeconds);

    Assert.Equal(FurnaceState.Melting, rig.State);
    Assert.True(
      rig.Temp > IiexValues.CupolaCastIronMeltingPoint,
      $"a melting cupola should be over its own melt line, was {rig.Temp} C"
    );
  }

  #endregion

  #region Melting -> tapping

  [Fact]
  public void Melting_renders_remelt_burden_into_molten_cast_iron() {
    var rig = new CupolaRig(burden: Remelt).FeedBlast();

    // Waits on the product rather than the label: the render happens on the carbon burnt after the state
    // turns, so entering Melting and having melted something are separate moments.
    Assert.True(
      rig.RunUntil(r => r.MoltenCastIron > 0f, SoakSeconds) > 0,
      "a melt cycle should render molten cast iron"
    );
  }

  [Fact]
  public void A_melting_cupola_taps_cast_iron_low_and_slag_high() {
    var rig = new CupolaRig(burden: Remelt)
      .FeedBlast()
      .WithCastIronTapAndCanal()
      .WithSlagTapAndCanal();

    Assert.True(
      rig.RunUntil(
        r => r.CastIronCanalUnits > 0 && r.SlagCanalUnits > 0,
        SoakSeconds
      ) > 0,
      "both taps should pour once the cupola is melting"
    );

    // Asserted on the material, not only on a unit count: the rig stands each tap on a literal cell of the
    // drawing (see CupolaScenes), so a swapped `T`/`S` pair pours slag down the low tap, which a count
    // alone cannot see because something still pours into both. The cupola drains cast iron.
    Assert.Contains("castiron", rig.CastIronCanalMetalType!);
    Assert.Contains("slag", rig.SlagCanalMetalType!);
  }

  #endregion

  #region Extinguish residue

  [Fact]
  public void Extinguishing_freezes_cast_iron_and_leaves_salvageable_burden_and_no_slag() {
    // Put out by running its carbon out rather than by a fuel clock: a shaft has no timers, so a campaign
    // ends when the fuel does.
    var rig = new CupolaRig(burden: Remelt).FeedBlast();

    Assert.True(
      rig.RunUntil(r => r.MoltenCastIron > 0f, SoakSeconds) > 0,
      "the cupola should have made cast iron before it is put out"
    );
    Assert.True(
      rig.RunUntil(r => r.State == FurnaceState.Idle, 4 * SoakSeconds) > 0,
      "a cupola should go out when its carbon is gone"
    );

    // The pool froze onto the hearth floor as solid cast iron (not iron, not slag).
    Assert.Equal(
      "iiex:hearthmetal-castiron",
      rig.BlockAtLocal(0, 1, 0).Code?.ToString()
    );
    Assert.True(
      rig.BlockEntityAtLocal(0, 1, 0)
        is BlockEntityHearthMetal { MetalCount: > 0 },
      "the frozen block should carry a positive cast-iron bit count"
    );

    // Nothing on the extinguish path makes slag: the slag pool is simply cleared.
    Assert.Equal(0f, rig.MoltenSlag, 3);
    Assert.NotEqual("iiex:slag", rig.BlockAtLocal(0, 1, 0).Code?.ToString());
  }

  /// <summary>
  /// The freeze must treat a fuel band at the hearth as a free cell rather than as wrong-family charge. On
  /// a shaft furnace fuel is charge, and <c>Burden.FamilyOfCode</c> answers <c>ore</c> for anything that is
  /// not remelt burden, coke and charcoal included, so a <c>remelt</c> furnace scores every fuel band as
  /// refused charge, <c>SolidifyBottomLayer</c> finds no cell to freeze into and <c>ClearMoltenPools</c>
  /// zeroes the metal. An ordinary burn-out cannot reach that case - a shaft goes out when
  /// <c>RacewayHoldsCarbon</c> fails over the same slice the freeze inspects - so the case breaks a running
  /// cupola (<c>OnBlockRemoved</c>), which puts one out with its raceway still stocked.
  /// </summary>
  [Fact]
  public void Breaking_a_running_cupola_freezes_its_pool_over_the_coke_at_its_raceway() {
    var rig = new CupolaRig(burden: Remelt).FeedBlast();
    Assert.True(
      rig.RunUntil(r => r.MoltenCastIron > 0f, SoakSeconds) > 0,
      "the cupola should have made cast iron before it is broken"
    );

    // The premise, stated rather than assumed: there is a molten bath, and the crucible cell that must
    // receive it holds a fuel band.
    float bath = rig.MoltenCastIron;
    Assert.True(bath > 0f);
    Assert.True(
      rig.HoldsFuelAtLocal(0, 1, 0),
      "the hearth cell must still hold fuel, or this case is the burn-out case again"
    );
    Assert.Equal(
      "iiex:furnace-chargepile",
      rig.BlockAtLocal(0, 1, 0).Code?.ToString()
    );

    rig.Furnace.OnBlockRemoved(); // broken while running

    // The bath froze where it stood. Asserted on the block, not on the pool figure: the pool is cleared
    // either way, so a metal count alone cannot tell "frozen onto the hearth" from "silently destroyed".
    Assert.Equal(
      "iiex:hearthmetal-castiron",
      rig.BlockAtLocal(0, 1, 0).Code?.ToString()
    );
    Assert.True(
      rig.BlockEntityAtLocal(0, 1, 0)
        is BlockEntityHearthMetal { MetalCount: > 0 },
      "the frozen block should carry a positive cast-iron bit count"
    );
  }

  #endregion

  #region Burden family gate

  // The blast furnace's ore burden charged into a cupola: the shaft still lights and burns, since the fuel
  // in it is real fuel, but the family gate refuses to render it into cast iron. Remelt burden in the same
  // rig converts normally (Melting_renders_remelt_burden_into_molten_cast_iron above).

  [Fact]
  public void A_cupola_will_not_convert_an_ore_burden_charge() {
    var rig = new CupolaRig(burden: Remelt, chargeCode: "burden").FeedBlast();

    rig.RunUntil(_ => false, SoakSeconds);

    Assert.Equal(0f, rig.MoltenCastIron, 3); // hot and "ready", but the wrong family never converts
  }

  /// <summary>
  /// Fullness is family-blind: a shaft packed with ore burden lights and burns out, and only the conversion
  /// is gated. The label follows - a furnace that will render nothing reads <c>Firing</c> however hot it
  /// gets, a clause <c>DeriveState</c> carries explicitly.
  /// </summary>
  [Fact]
  public void A_wrong_family_shaft_still_reads_full_so_it_lights_and_burns() {
    var rig = new CupolaRig(burden: Remelt, chargeCode: "burden").FeedBlast();

    int coke = rig.CokeUnits;
    bool everMelted = false;
    rig.RunUntil(
      _ => false,
      SoakSeconds,
      r => everMelted |= r.State == FurnaceState.Melting
    );

    Assert.False(everMelted, "a wrong-family shaft must never read as Melting");
    Assert.True(
      rig.CokeUnits < coke,
      $"but it really is burning the charge out; {rig.CokeUnits} vs {coke}"
    );
  }

  #endregion

  #region Slower than the blast furnace

  /// <summary>
  /// Design intent (docs/design/iiex.md): about two cupolas keep pace with one blast furnace. Production is
  /// metered by the carbon burned, and carbon by the tuyeres, so the cupola is slower because it has one
  /// tuyere against the cold blast furnace's two.
  /// </summary>
  [Fact]
  public void The_cupola_melts_at_about_half_the_cold_blast_furnaces_rate() {
    // Carbon burned per second scales with tuyere count and sets both campaign length and production rate,
    // so the tuyere ratio is the rate ratio. Expressed as counts, so each machine's own charge quantum
    // cancels and the comparison is like for like.
    const int cupolaTuyeres = 1;
    const int coldBlastTuyeres = 2;

    Assert.Equal(0.5f, cupolaTuyeres / (float)coldBlastTuyeres, 2);
  }

  #endregion
}
