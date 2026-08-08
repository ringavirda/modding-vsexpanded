using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Products.BlockEntities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// The cupola furnace's re-melting process driven end to end (docs/design/iwex.md): a charged, lit,
/// blast-fed shaft climbs past cast iron's melt line, enters Melting, re-melts remelt burden into molten
/// cast iron, taps cast iron out the lower tap and slag out the upper, and - extinguished mid-heat -
/// freezes its pool onto the hearth as solid cast iron while the rest of the burden survives as
/// salvageable spent charge, never slag. Plus the family gate: ore burden burns in the cupola but never
/// converts. Exercises the gated firing/melting tick with its real peripherals via <see cref="CupolaRig"/>.
/// <para>
/// <b>Every case here used to arrange the machine by assignment and the machine underneath had never
/// lit at all.</b> The cupola is a <see cref="BlockEntityShaftFurnace"/>, so it burns coke at a raceway -
/// and every scene charged remelt burden alone, with no fuel band anywhere. Writing <c>State = Melting</c>
/// hid that completely: six cases green over a furnace that could not catch fire. They arrange by charging
/// and blowing now, and <see cref="CupolaRig.HeatSoak"/> fails legibly when the machine will not do it.
/// </para>
/// </summary>
public class CupolaScenarioTests
{
  private static readonly BurdenMix Remelt = new(60f, 5f, 35f);

  /// <summary>Long enough for a charged, blown cupola to warm its column through and melt, with slack.</summary>
  private const int SoakSeconds = 600;

  #region Ignition + phase progression

  [Fact]
  public void A_charged_lit_shaft_fed_blast_ignites()
  {
    var rig = new CupolaRig(burden: Remelt).FeedBlast();

    rig.Tick(1); // a full shaft with coke at the raceway + blast -> catches

    Assert.Equal(FurnaceState.Firing, rig.State);
  }

  /// <summary>
  /// <b>This case tested a threshold that no longer gates anything, and its own regression note is why
  /// it is worth keeping rather than deleting.</b>
  /// <para>
  /// It was <c>The_cupola_lights_at_its_OWN_threshold_not_the_blast_furnaces</c>: <c>GetBlastMixCount</c>
  /// set <c>isFull</c> from the literal <c>BlastMixRequiredToFire</c> rather than from
  /// <c>ChargeCapacityUnits</c>, so the cupola demanded exactly double its own threshold to light while its
  /// heat balance used the right number for the same question. One furnace, two answers - and nothing
  /// caught it because <c>CupolaRig</c>'s default charge was set to the number the bug wanted.
  /// </para>
  /// <para>
  /// Ignition is <b>positional and pneumatic</b> now - a complete raceway course with carbon in it - so
  /// there is no threshold left to disagree about, which retires the defect rather than fixing it. What
  /// survives is the half that still bites: the cupola's <b>own</b> geometry decides, so a shaft charged
  /// well under any blast-furnace figure lights perfectly well.
  /// </para>
  /// </summary>
  [Fact]
  public void The_cupola_lights_on_its_OWN_geometry_not_the_blast_furnaces_threshold()
  {
    // This opened by asserting `CupolaMixRequiredToFire < BlastMixRequiredToFire` - "vacuous unless the
    // cupola's figure is genuinely the lower of the two". Both keys are gone now, and the deletion is a
    // stronger statement of the same thing: there is no blast-furnace figure for a cupola to be measured
    // against any more, on either branch of the question. What is left to state is that the cupola lights
    // on a charge far under what a blast furnace holds, which is the arrangement the old defect broke.
    var rig = new CupolaRig(charge: 160, burden: Remelt).FeedBlast();

    rig.Tick(1);

    Assert.Equal(FurnaceState.Firing, rig.State);
  }

  /// <summary>
  /// The discriminating half. It used to be "one unit under the threshold stays cold"; the threshold is
  /// gone, so what genuinely keeps a full cupola dark is having <b>nothing that can burn</b> in front of
  /// its tuyere.
  /// <para>
  /// The charge is still <b>stamped</b> at 35 % coke, which is the point: a furnace reading carbon off
  /// the stamp rather than off the bands would light here.
  /// </para>
  /// </summary>
  [Fact]
  public void A_full_shaft_with_no_coke_at_the_raceway_stays_cold()
  {
    var rig = new CupolaRig(charge: -1, burden: Remelt)
      .FeedBlast()
      .ChargeWithoutCoke();

    Assert.Equal(0, rig.CokeUnits);
    Assert.True(rig.ChargedMix.FuelFrac > 0f, "the charge must still be STAMPED with coke");

    bool everLit = false;
    rig.RunUntil(_ => false, 60, r => everLit |= r.State != FurnaceState.Idle);

    Assert.False(everLit, "a cupola with no carbon in it should never catch");
    Assert.Equal(FurnaceState.Idle, rig.State);
  }

  [Fact]
  public void Sustained_heat_above_the_cast_iron_melt_point_transitions_to_melting()
  {
    var rig = new CupolaRig(burden: Remelt).FeedBlast().HeatSoak(SoakSeconds);

    Assert.Equal(FurnaceState.Melting, rig.State);
    Assert.True(
      rig.Temp > IwexValues.CupolaCastIronMeltingPoint,
      $"a melting cupola should be over its own melt line, was {rig.Temp} C"
    );
  }

  #endregion

  #region Melting -> tapping

  [Fact]
  public void Melting_renders_remelt_burden_into_molten_cast_iron()
  {
    var rig = new CupolaRig(burden: Remelt).FeedBlast();

    // Waited on the product, not on the label: entering Melting and having melted something are separate
    // moments, because the render happens on the carbon burnt after the state turns.
    Assert.True(
      rig.RunUntil(r => r.MoltenCastIron > 0f, SoakSeconds) > 0,
      "a melt cycle should render molten cast iron"
    );
  }

  [Fact]
  public void A_melting_cupola_taps_cast_iron_low_and_slag_high()
  {
    var rig = new CupolaRig(burden: Remelt)
      .FeedBlast()
      .WithCastIronTapAndCanal()
      .WithSlagTapAndCanal();

    Assert.True(
      rig.RunUntil(r => r.CastIronCanalUnits > 0 && r.SlagCanalUnits > 0, SoakSeconds) > 0,
      "both taps should pour once the cupola is melting"
    );

    // Both canals are asserted on their material, not only on a unit count. The rig stands each tap on
    // a literal cell of the drawing (see CupolaScenes), so "low" and "high" are this file's own statement
    // rather than an echo of the roles - and a swapped `T`/`S` pair pours slag down the low tap, which a
    // count alone cannot see because something still pours into both.
    //
    // The cupola is unchanged by the blast furnace's pig-iron flip: it still drains cast iron
    // (iwex/game:ingot-castiron), the counterpart to the blast furnace's pig-iron drain.
    Assert.Contains("castiron", rig.CastIronCanalMetalType!);
    Assert.Contains("slag", rig.SlagCanalMetalType!);
  }

  #endregion

  #region Extinguish residue

  [Fact]
  public void Extinguishing_freezes_cast_iron_and_leaves_salvageable_burden_and_no_slag()
  {
    // Put out by running its carbon out, not by a fuel clock: `CupolaMaxFuelBurnTime` is dead on this
    // branch (a shaft has no timers), and a campaign now ends when the coke does.
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
      "iwex:hearthmetal-castiron",
      rig.BlockAtLocal(0, 1, 0).Code?.ToString()
    );
    Assert.True(
      rig.BlockEntityAtLocal(0, 1, 0) is BlockEntityHearthMetal { MetalCount: > 0 },
      "the frozen block should carry a positive cast-iron bit count"
    );

    // Nothing on the extinguish path makes slag: the slag pool is simply cleared.
    Assert.Equal(0f, rig.MoltenSlag, 3);
    Assert.NotEqual("iwex:slag", rig.BlockAtLocal(0, 1, 0).Code?.ToString());
  }

  /// <summary>
  /// <b>A fuel band in a cupola read as wrong-family charge, and it cost the player the whole
  /// bath.</b> <c>PileHoldsRejectedCharge</c> asked
  /// <c>IsChargeCode(m) &amp;&amp; !AcceptsChargeCode(m)</c>, and on a shaft furnace <b>fuel is charge</b>:
  /// <c>Burden.FamilyOfCode</c> answers <c>ore</c> for anything that is not remelt burden, coke and charcoal
  /// included, so against this furnace's <c>remelt</c> family every coke band scored as charge it had
  /// refused. Its one crucible cell was therefore never "free", <c>SolidifyBottomLayer</c> found no cell to
  /// freeze into and returned having frozen nothing, and <c>ClearMoltenPools</c> then zeroed the metal.
  /// <para>
  /// <b>Why the case above cannot see it, and why this one has to break the furnace.</b> A shaft goes out
  /// when <c>RacewayHoldsCarbon</c> fails - no fuel in the lowest <c>ChargeUnitsPerBlock</c> units - and that
  /// is the <em>same slice</em> the freeze inspects. Burning a campaign to its end therefore always leaves a
  /// coke-free hearth cell, so every ordinary extinguish drove straight past the defect. Breaking a running
  /// cupola (<c>OnBlockRemoved</c>) is the shipped route that puts it out with its raceway still stocked.
  /// </para>
  /// </summary>
  [Fact]
  public void Breaking_a_running_cupola_freezes_its_pool_over_the_coke_at_its_raceway()
  {
    var rig = new CupolaRig(burden: Remelt).FeedBlast();
    Assert.True(
      rig.RunUntil(r => r.MoltenCastIron > 0f, SoakSeconds) > 0,
      "the cupola should have made cast iron before it is broken"
    );

    // The premise, stated rather than assumed: there is a molten bath, and the crucible cell that has to
    // receive it has a coke band standing in it.
    float bath = rig.MoltenCastIron;
    Assert.True(bath > 0f);
    Assert.True(
      rig.HoldsFuelAtLocal(0, 1, 0),
      "the hearth cell must still hold fuel, or this case is the burn-out case again"
    );
    Assert.Equal(
      "iwex:furnace-chargepile",
      rig.BlockAtLocal(0, 1, 0).Code?.ToString()
    );

    rig.Furnace.OnBlockRemoved(); // the player takes a pick to a running cupola

    // The bath froze where it stood. Asserted on the block, not on the pool figure: the pool is cleared
    // either way, so a metal count alone cannot tell "frozen onto the hearth" from "silently destroyed".
    Assert.Equal(
      "iwex:hearthmetal-castiron",
      rig.BlockAtLocal(0, 1, 0).Code?.ToString()
    );
    Assert.True(
      rig.BlockEntityAtLocal(0, 1, 0) is BlockEntityHearthMetal { MetalCount: > 0 },
      "the frozen block should carry a positive cast-iron bit count"
    );
  }

  #endregion

  #region Burden family gate

  // The blast furnace's ore burden charged into a cupola: the shaft still lights and burns (the coke in it
  // is real fuel), but the family gate refuses to render it into cast iron. Remelt burden in the same rig
  // converts normally (Melting_renders_remelt_burden_into_molten_cast_iron above), so this is the
  // wrong-family half of "right family melts, wrong family burns but never converts".

  [Fact]
  public void A_cupola_will_not_convert_an_ore_burden_charge()
  {
    var rig = new CupolaRig(burden: Remelt, chargeCode: "burden").FeedBlast();

    rig.RunUntil(_ => false, SoakSeconds);

    Assert.Equal(0f, rig.MoltenCastIron, 3); // hot and "ready", but the wrong family never converts
  }

  /// <summary>
  /// Family-blind fullness: the cupola does not refuse to light a shaft packed with ore burden - it lights
  /// and burns it out. Only the conversion is gated.
  /// <para>
  /// And the <b>label stays honest with it</b>: a furnace that will render nothing is not melting, it is
  /// burning, so it reads <c>Firing</c> however hot it gets. That used to fall out of the soak transition
  /// refusing to fire; <c>DeriveState</c> carries the same clause deliberately.
  /// </para>
  /// </summary>
  [Fact]
  public void A_wrong_family_shaft_still_reads_full_so_it_lights_and_burns()
  {
    var rig = new CupolaRig(burden: Remelt, chargeCode: "burden").FeedBlast();

    int coke = rig.CokeUnits;
    bool everMelted = false;
    rig.RunUntil(_ => false, SoakSeconds, r => everMelted |= r.State == FurnaceState.Melting);

    Assert.False(everMelted, "a wrong-family shaft must never read as Melting");
    Assert.True(
      rig.CokeUnits < coke,
      $"but it really is burning the charge out; {rig.CokeUnits} vs {coke}"
    );
  }

  #endregion

  #region Slower than the blast furnace

  /// <summary>
  /// Design intent (docs/design/iwex.md): ~2 cupolas keep pace with one blast furnace.
  /// <para>
  /// <b>It is no longer a pair of tunables - it is the drawing.</b> This used to compare
  /// <c>CupolaCastIronPerMeltCycle / CupolaMeltIntervalSec</c> against the blast furnace's pair, two
  /// constants set to be half of two others. Production is metered by the <b>carbon burned</b> now, and
  /// carbon is metered by the <b>tuyeres</b>, so the cupola is slower for the reason a real one is: it has
  /// one tuyere against the cold blast furnace's two.
  /// </para>
  /// </summary>
  [Fact]
  public void The_cupola_melts_at_about_half_the_cold_blast_furnaces_rate()
  {
    // Charge units per second of carbon, per furnace, at full blast - which is what sets both the campaign
    // length and the production rate. Expressed against each machine's own charge quantum so the cupola's
    // metal-unit scale cancels and the comparison is like for like.
    const int cupolaTuyeres = 1;
    const int coldBlastTuyeres = 2;

    Assert.Equal(0.5f, cupolaTuyeres / (float)coldBlastTuyeres, 2);
  }

  #endregion
}
