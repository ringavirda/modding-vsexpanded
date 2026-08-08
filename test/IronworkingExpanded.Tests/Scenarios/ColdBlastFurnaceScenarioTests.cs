using System;
using ExpandedLib.Materials;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Products.BlockEntities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The <b>cold</b> blast furnace driven end to end (docs/design/iwex.md): a charged, lit, mechanically
/// blown shaft climbs past iron's melt line on the coke in its own burden, enters Melting, renders ore
/// burden into molten pig iron and slag, taps them into canals - and, when it goes out, freezes its pool
/// onto the hearth and hands the rest of the column back as salvage.
/// <para>
/// This is the machine the whole iron economy starts at, and until now it had only geometry, parts, tap
/// and HUD tests: nothing charged it, lit it, melted, tapped or extinguished it. The suite is meant to be
/// the safety net a later rework of the charge model, the heat model and the crucible is judged against,
/// so each case pins an <em>observable behaviour</em> and most carry their own control - the counterpart
/// scene that must come out differently - so a test cannot pass for a reason other than the one it names.
/// </para>
/// <para>
/// Everything runs on the clock through <see cref="ColdBlastFurnaceScenes"/>: the real 160-cell footprint
/// stands, the furnace completes itself, and its own registered production tick does the work. No state is
/// fast-forwarded by reflection.
/// </para>
/// </summary>
// Joins the furnace-config collection as a *reader*: these scenes drive real furnaces and read computed
// heat balances, so they must not run while HeatBalanceTests has BfCombustionBaseTemp or BfCokeSensitivity
// off their shipped values.
[Collection(FurnaceConfigCollection.Name)]
public class ColdBlastFurnaceScenarioTests
{
  /// <summary>
  /// Long enough for a cold furnace to blow in and reach the melt line, with slack.
  /// <para>
  /// <b>Not derivable from the config; that is the counter-current model.</b> The shaft branch has no
  /// chase and no soak: the flame follows the raceway within a tick and what takes time is the <b>charge warming
  /// through</b>, which depends on how much coke is at the raceway, how tall the column is and how much
  /// gas each band strips on the way past. There is no closed form for that, so this is a ceiling with
  /// room in it rather than a computed earliest.
  /// </para>
  /// <para>
  /// Caution: never wait a <em>fixed</em> duration here - it is wrong in both directions: the furnace
  /// can reach Melting well before it, and - because a campaign ends when the coke does rather than
  /// when a timer does - it can also have finished the whole shaft by then. Use
  /// <see cref="ColdBlastFurnaceRig.RunUntil"/> and assert on the state it was waited for.
  /// </para>
  /// </summary>
  private const int CampaignSeconds = 900;

  /// <summary>
  /// A ceiling for "run it until the fire dies", with room in it.
  /// <para>
  /// <b>A shaft furnace has no extinguish timer to wait out</b> - only the firebox branch owns a
  /// countdown. What ends a campaign is running out of carbon, so what is being waited for is
  /// <c>coke ÷ (BfRacewayCarbonPerSecond × AirFactor)</c> - a full cold shaft at 30 % coke is ~370 u of
  /// carbon against 0.35 u/s, so ~1 060 s blown and twice that on natural draught. Waited for with
  /// <see cref="ColdBlastFurnaceRig.RunUntil"/> and asserted on the state it was waited for, never sampled at
  /// a fixed offset.
  /// </para>
  /// </summary>
  private const int BurnoutSeconds = 2400;

  #region The whole process, emergent

  [Fact]
  public void Charge_light_melt_tap_yields_pig_and_slag_into_their_canals()
  {
    // A coke-rich burden is what makes a cold furnace work: with no cowper on the line every degree
    // above iron's melt line has to come out of the charge. 30 % coke clears the ~23.9 % break-even.
    var scene = ColdBlastFurnaceScenes.Complete();

    // It starts built, cold, idle and untouched - the structure completed itself.
    Assert.True(
      scene.Core.StructureComplete,
      "the furnace should have completed its own 160-cell structure"
    );
    Assert.Equal(FurnaceState.Idle, scene.State);

    // Charged, blown, and run until it has actually rendered metal rather than for a fixed stretch - see
    // CampaignSeconds. Waited on the product, not on the state: entering Melting and having melted
    // something are now different moments, because the burden still has to arrive at the raceway hot.
    Assert.True(
      scene.RunUntil(s => s.MoltenIron > 0f, CampaignSeconds) > 0,
      "a coke-rich cold furnace should render pig within a campaign"
    );

    Assert.Equal(FurnaceState.Melting, scene.State);
    Assert.True(
      scene.Temp > IwexValues.BfIronMeltingPoint,
      $"a coke-rich cold furnace should settle above the melt line, was {scene.Temp} C"
    );
    Assert.True(scene.MoltenIron > 0f, "the furnace should have made pig");
    Assert.True(scene.MoltenSlag > 0f, "and slag alongside it");
    // Cold really is cold: the hearth is hot because of the coke, not because of the blast.
    Assert.Equal(0f, scene.Heat.PreheatGain, 1);

    // Both taps are opened through the production right-click, not by poking the block entity.
    Assert.True(scene.OpenIronTap(), "the iron tap should open over its canal");
    Assert.True(scene.OpenSlagTap(), "the slag tap should open over its canal");

    scene.RunLive(30);

    Assert.True(scene.IronCanalUnits > 0, "pig should have reached the canal start");
    Assert.True(scene.SlagCanalUnits > 0, "slag should have reached its own canal start");
    // The blast furnace makes pig iron, not plain iron - plain iron is a Bessemer over-blow product.
    Assert.Contains("pigiron", scene.IronCanalMetal);
    Assert.DoesNotContain("ingot-iron", scene.IronCanalMetal);
    Assert.Contains("slag", scene.SlagCanalMetal);
  }

  #endregion

  #region Firing

  /// <summary>
  /// <b>There is no quantity threshold on ignition.</b>
  /// <para>
  /// A shaft derives its state from what is in front of its tuyeres - a complete raceway course, and
  /// <b>carbon</b> in it - so 319 units of perfectly good burden-and-coke laid across every column
  /// lights exactly as 320 does, and it should. A quantity gate would pin a tunable, not a behaviour.
  /// </para>
  /// <para>
  /// What genuinely keeps a full shaft dark is having <b>nothing that can burn</b> at the raceway. That
  /// is the honest half, it needs no number, and it is the half a stamped-coke reading would get wrong: this
  /// scene's burden carries a 30 % coke <em>stamp</em>, so a furnace that took its carbon from the stamp
  /// rather than from the bands would light here.
  /// </para>
  /// </summary>
  [Fact]
  public void A_full_shaft_with_no_coke_at_its_raceway_never_lights()
  {
    var noFuel = ColdBlastFurnaceScenes.NoCokeAtTheRaceway();
    Assert.True(noFuel.Core.StructureComplete);

    // The scene's own premises, so it cannot pass by being empty or under-built.
    Assert.Equal(noFuel.ShaftCapacityUnits, noFuel.ColumnUnits);
    Assert.Equal(0, noFuel.CokeUnits);
    Assert.True(
      noFuel.ChargedMix.FuelFrac > 0f,
      "the burden must still be STAMPED with coke, or this cannot tell a band read from a stamp read"
    );

    // Watched rather than sampled at the end: a furnace that lit and then went out would be Idle
    // again by the last tick, so "Idle at the end" alone would not say it never caught.
    bool everLit = false;
    noFuel.RunLive(120, s => everLit |= s.State != FurnaceState.Idle);

    Assert.False(everLit, "a shaft with no carbon in it should never catch at all");
    Assert.Equal(FurnaceState.Idle, noFuel.State);

    // The control, and it is the whole case: the same furnace charged with the same burden at the same
    // grade - but laid as real rounds, so the coke is its own bands - catches on its first tick. Without
    // it this passes just as well on a furnace that could never light at all.
    var lit = ColdBlastFurnaceScenes.Complete();
    Assert.True(lit.CokeUnits > 0);
    lit.RunLive(2);
    Assert.Equal(FurnaceState.Firing, lit.State);
  }

  /// <summary>
  /// <b>The ignition gate is positional, not a quantity</b> (<c>docs/design/layered-charge.md</c> § <i>The ignition
  /// sequence, fully specified</i>): the raceway course must be complete - a charge pile standing on
  /// <b>every</b> column - or a tuyere has nothing to blow into.
  /// <para>
  /// It is a fraction of capacity numerically and must never be <em>expressed</em> as one. "Charge ≥ N
  /// units" and "every column has charge at the raceway" agree on a full course and disagree everywhere
  /// else, and this scene is the everywhere-else: three times the fire threshold, piled into one column,
  /// with eight of the nine tuyeres facing empty air.
  /// </para>
  /// <para>
  /// The requirement is the furnace's own column count - nine here, one on the cupola - so a redrawn
  /// layout updates it for free and there is no constant to tune or to drift.
  /// </para>
  /// </summary>
  [Fact]
  public void A_shaft_piled_into_ONE_column_never_lights_however_much_is_in_it()
  {
    var tower = ColdBlastFurnaceScenes.OneTallColumn();

    Assert.True(tower.Core.StructureComplete);
    // This compared against `BlastMixRequiredToFire` (320) until that key was deleted - "the scene must
    // clear the threshold, or it would be testing the quantity gate". There is no quantity gate left to
    // clear; ignition is positional and pneumatic. The premise the case still needs is that the shaft is
    // genuinely well charged, so a failure to light cannot be read as an empty furnace - stated against the shaft's
    // own capacity, which is the only "how much" number left.
    Assert.True(
      tower.ColumnUnits > tower.ShaftCapacityUnits / 4,
      $"the scene must stand a substantial charge, or it proves nothing; "
        + $"{tower.ColumnUnits} of {tower.ShaftCapacityUnits}"
    );

    // Watched rather than sampled: a furnace that lit and went out would read Idle at the end anyway.
    bool everLit = false;
    tower.RunLive(120, s => everLit |= s.State != FurnaceState.Idle);

    Assert.False(everLit, "a shaft with eight empty columns should never catch");

    // The control, and it is the whole case: the same total, spread into a complete course, lights at
    // once. Without it this passes just as well on a furnace that can never light at all.
    var course = ColdBlastFurnaceScenes.Complete(charge: tower.ColumnUnits);
    course.RunLive(2);
    Assert.Equal(FurnaceState.Firing, course.State);
  }

  /// <summary>
  /// <b>A stopped blower does not snuff the fire - it throttles it.</b> This case once asserted the
  /// opposite ("an unblown furnace should be snuffed by the starvation disruption"), and the
  /// inversion is the point.
  /// <para>
  /// Air is the reagent, so what the blast decides is the <b>rate</b>: cut it and the furnace falls back to
  /// natural draught, which burns its carbon at <c>BfNaturalDraughtFactor</c> of the blown rate and settles
  /// well below iron's melt line. It leaves Melting, it renders nothing, and it goes on quietly eating its
  /// own campaign - which is a far worse outcome for the player than going out, and the correct one.
  /// <c>docs/design/layered-charge.md</c> § <i>What sets the rate: the blast</i>.
  /// </para>
  /// <para>
  /// The two failure modes it must not be confused with are elsewhere: a <b>choke</b> (exhaust sealed) is
  /// instant death, and a <b>breach</b> is this same natural-draught burn with the walls gone. A stopped
  /// blower is neither - the shaft is intact and the fire has all the fuel it started with.
  /// </para>
  /// </summary>
  [Fact]
  public void Cutting_the_blast_takes_a_melting_furnace_back_out_of_melting()
  {
    var scene = ColdBlastFurnaceScenes.Complete();
    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Melting, CampaignSeconds) > 0,
      "the furnace should have reached Melting on blast"
    );

    // The control comes first, on the same furnace: with the blowers still on it sails through a long
    // window without ever leaving Melting.
    const int window = 120;
    scene.RunLive(window);
    Assert.Equal(FurnaceState.Melting, scene.State);
    Assert.False(scene.AirStarved);

    // Now cut the blast. The air factor collapses to natural draught and the process temperature falls
    // far below the melt line.
    int coke = scene.CokeUnits;
    scene.CutBlast();

    // Watched second by second rather than read at the end: "not Melting at the end" would be satisfied by
    // a furnace that dipped out and melted again, which is the thing being denied.
    // The watch is armed only once it has actually left Melting, and the pool is banked at that same
    // instant. The tuyere mains still hold the last second of blast when the blowers stop, so the first
    // tick or two after the cut legitimately still melt - a furnace does not lose its air the instant a
    // crank stops turning, and pinning the pool from before the cut would be asserting that it does.
    bool left = false;
    bool meltedAgain = false;
    float bankedAtCutoff = 0f;
    float grewAfter = 0f;
    scene.RunLive(
      window,
      s =>
      {
        if (s.State != FurnaceState.Melting)
        {
          if (!left)
            bankedAtCutoff = s.MoltenIron;
          left = true;
          grewAfter = Math.Max(grewAfter, s.MoltenIron - bankedAtCutoff);
        }
        else if (left)
          meltedAgain = true;
      }
    );

    Assert.True(left, "an unblown furnace should fall out of Melting");
    Assert.False(meltedAgain, "and it should not melt again");
    Assert.NotEqual(FurnaceState.Melting, scene.State);
    Assert.True(
      scene.Temp < IwexValues.BfIronMeltingPoint,
      $"natural draught should sit below the melt line; was {scene.Temp} C"
    );
    Assert.True(bankedAtCutoff > 0f, "it should have banked real pig before the blast went");
    Assert.Equal(0f, grewAfter, 3); // and rendered nothing at all once it had fallen out

    // Still alight, and still spending. A frozen furnace and an extinguished one look identical from
    // outside, so consumption is the only observable that separates "throttled" from "out" - the same
    // assertion the breach case turns, for the same reason.
    Assert.NotEqual(FurnaceState.Idle, scene.State);
    Assert.True(
      scene.CokeUnits < coke,
      $"an unblown furnace should still be burning its carbon; {scene.CokeUnits} vs {coke}"
    );
  }

  #endregion

  #region Melting

  /// <summary>
  /// The taught cold/hot trade, not a bug. The <c>standard</c> grade (20 % coke) is the reference the whole
  /// heat balance is calibrated at; on cold blast it settles at ~1420 C against a 1482 C melt line and never
  /// crosses it. The furnace announces exactly that through <c>bf-info-heatstall</c> ("Needs {0} - add coke
  /// or hot blast") and shows T_process against the melt line every tick.
  /// <para>
  /// <b>And now it ends, which the flat model could not express.</b> A furnace whose burden never melts
  /// never makes room for the column to descend, so the fire eats the carbon it can <em>reach</em> and then
  /// finds burden where the next coke course should be. That is the <b>chill</b> - hanging, the most famous
  /// way a real blast furnace goes wrong - and it arrives here as an outcome rather than as a rule: nothing
  /// in the code names it, it is what "descent is a consequence, never a rate" produces.
  /// <c>docs/design/layered-charge.md</c> § <i>The chill</i>.
  /// </para>
  /// <para>
  /// The window is therefore <b>waited for</b>, never fixed. This case used to run a flat 600 s and assert
  /// <c>Firing</c> at the end, which is a claim about a duration - and the duration moved the moment
  /// campaign length became "how much carbon is charged" instead of a timer.
  /// </para>
  /// </summary>
  [Fact]
  public void A_standard_burden_lights_holds_and_never_melts()
  {
    var stalled = ColdBlastFurnaceScenes.StandardBurden();

    Assert.True(
      stalled.RunUntil(s => s.State == FurnaceState.Firing, 30) > 0,
      "a standard burden should still LIGHT - it is the melt it never reaches"
    );

    // Held below the line for as long as it burns, watched every second rather than sampled: a furnace
    // that crossed the melt line and fell back would read Firing at the end anyway.
    float hottest = 0f;
    bool everMelted = false;
    int lived = stalled.RunUntil(
      s => s.State == FurnaceState.Idle,
      BurnoutSeconds,
      s =>
      {
        hottest = Math.Max(hottest, s.Temp);
        everMelted |= s.State == FurnaceState.Melting;
      }
    );

    Assert.False(everMelted, "a standard burden must never reach Melting on cold blast");
    Assert.True(
      hottest < IwexValues.BfIronMeltingPoint,
      $"a standard burden should hold below the melt line, peaked at {hottest} C"
    );
    Assert.Equal(0f, stalled.MoltenIron, 3);
    Assert.Equal(0f, stalled.MoltenSlag, 3);

    // ...and it is a chill, not a spent campaign: the fire stopped with burden still standing in the shaft
    // and coke it could no longer reach. A furnace that had simply burnt its charge would end empty.
    Assert.True(lived > 0, "a chilled furnace should eventually go out");
    Assert.True(
      stalled.ColumnUnits > 0,
      "the chilled column should still be standing there, which is what makes it salvageable"
    );

    // The player is told why, rather than being left with a furnace that silently does nothing. Read on a
    // fresh scene still in the stall, because the one above has gone out.
    var naming = ColdBlastFurnaceScenes.StandardBurden();
    Assert.True(naming.RunUntil(s => s.State == FurnaceState.Firing, 30) > 0);
    naming.RunLive(30);
    Assert.Equal(naming.Heat.TProcess, naming.Temp, 1); // arrived at the balance, not still climbing
    Assert.Contains(IwexLang.BfInfoHeatstall, naming.CoreInfo());

    // The control: nothing about the scene stops a furnace melting - only the burden does. The same
    // rig charged with coke-rich burden crosses into Melting inside the same window.
    var melting = ColdBlastFurnaceScenes.Complete();
    Assert.True(
      melting.RunUntil(s => s.State == FurnaceState.Melting, CampaignSeconds) > 0
    );
    Assert.Equal(FurnaceState.Melting, melting.State);
  }

  /// <summary>
  /// Campaign length is <b>not</b> bounded by one shaft-full: a furnace is charged continuously while it
  /// runs. Two identical furnaces, blown the same way; one is recharged when its carbon is about half gone,
  /// the other is not. Nothing else differs, so the divergence at the end is the recharge and only the
  /// recharge.
  /// <para>
  /// <b>What "the campaign" is measured in changed, and this case is where it shows.</b> It used to be
  /// burden units against <c>DisruptionMixFloor</c> and windows counted in <c>ExtinguishThresholdDefault</c>
  /// - a shaft that ran thin tripped a disruption and a countdown put it out. There is no countdown and no
  /// floor on this branch: a lit shaft runs until its <b>carbon</b> is gone, so that is what the scene
  /// halves, what the recharge has to restore, and what the control dies of.
  /// </para>
  /// </summary>
  [Fact]
  public void Refilling_a_firing_furnace_extends_the_campaign()
  {
    var refilled = ColdBlastFurnaceScenes.Complete();
    var control = ColdBlastFurnaceScenes.Complete();

    Assert.True(
      refilled.RunUntil(s => s.State == FurnaceState.Melting, CampaignSeconds) > 0
    );
    Assert.True(
      control.RunUntil(s => s.State == FurnaceState.Melting, CampaignSeconds) > 0
    );

    int charged = refilled.CokeUnits;
    int half = charged / 2;
    Assert.True(half > 0, "the scene must carry real carbon, or there is nothing to halve");

    // Burn until roughly half the carbon is gone.
    Assert.True(refilled.RunUntil(s => s.CokeUnits <= half, CampaignSeconds) > 0);
    Assert.True(control.RunUntil(s => s.CokeUnits <= half, CampaignSeconds) > 0);
    Assert.Equal(FurnaceState.Melting, refilled.State);
    Assert.Equal(FurnaceState.Melting, control.State);

    // The one difference: the player charges again.
    refilled.Recharge(refilled.ShaftCapacityUnits);

    // The recharge has to be visible to the furnace, not merely present in the world. A machine that
    // latched its charge at ignition - or that only ever let it fall - would coast through the window
    // below on a stale number and pass everything after this line without ever crediting the new burden.
    // One tick, one assertion, and that hole is shut.
    refilled.RunLive(1);
    Assert.True(
      refilled.CokeUnits > half,
      $"the recharge should show up in the carbon the furnace can reach; still {refilled.CokeUnits}"
    );

    // The window is the control's own life, not a constant: run the un-refilled furnace until it dies,
    // then hold the refilled one for exactly that long. That makes "still melting" a claim measured against
    // the very thing it is being compared with, and it cannot go stale when a rate is retuned.
    int controlLived = control.RunUntil(
      s => s.State == FurnaceState.Idle,
      BurnoutSeconds
    );
    Assert.True(
      controlLived > 0,
      "the un-refilled furnace should run out of carbon and go out"
    );
    Assert.Equal(0, control.CokeUnits);

    bool everLeftMelting = false;
    refilled.RunLive(
      controlLived,
      s => everLeftMelting |= s.State != FurnaceState.Melting
    );

    Assert.False(
      everLeftMelting,
      "a recharged furnace should never drop out of Melting while the control burned out"
    );
    Assert.Equal(FurnaceState.Melting, refilled.State);
    Assert.True(refilled.CokeUnits > 0, "and it still has carbon left to burn");
    Assert.True(refilled.MoltenIron > 0f, "and it kept making pig the whole way");
  }

  #endregion

  #region Tapping

  [Fact]
  public void A_tap_installed_backwards_no_longer_completes_the_structure()
  {
    // This test once asserted the opposite, and the inversion is the point.
    //
    // The legend used to be the wildcard `iwex:furnace-tap-*`, so a tap fitted the wrong way round
    // satisfied its cell and the furnace completed - the mistake was invisible at build time and only
    // showed up at the tap, where the spout of an east-facing tap in the east wall aims one cell into
    // the furnace's own base and the block refuses to open over nothing. A player got a furnace that
    // built, lit, melted, and then silently would not drain.
    //
    // The redraw pinned the facing (`iwex:furnace-irontap-west`), so the wrong tap no longer satisfies
    // the cell at all: the structure stays incomplete and the build outline keeps pointing at the one
    // cell that is wrong. Failing loudly at build time is strictly better than failing quietly at the
    // first tap, so the scene now proves the refusal rather than the workaround.
    //
    // The runtime half did not go uncovered - it moved down to where it belongs and is stated on the
    // block itself, off any structure: BlastFurnaceTapTests.An_open_tap_over_nothing_pours_nothing and
    // .An_open_tap_pours_into_the_cell_its_side_faces_away_from.
    var wrong = ColdBlastFurnaceScenes.BackwardsIronTap();
    Assert.False(
      wrong.Core.StructureComplete,
      "a backwards tap must no longer satisfy the facing-pinned tap cell"
    );

    // ...and it is the tap that is unsatisfied, not some unrelated cell the scene got wrong. Exactly
    // one cell short, and the breakdown names the iron notch's own position - without this the test
    // would pass just as well on a scene that had gone wrong somewhere else entirely.
    Assert.Equal(1, wrong.Structure.Missing);
    Assert.Contains(wrong.IronTap.Pos.ToString(), wrong.Structure.MissingReport);

    // The control: the identical scene with the tap the right way round completes and opens on the
    // gesture the backwards one is never offered, so the difference is the facing and nothing else.
    var right = ColdBlastFurnaceScenes.Complete();
    Assert.True(right.Core.StructureComplete);
    Assert.True(right.OpenIronTap());
    Assert.True(right.IronTap.IsPouring);
  }

  [Fact]
  public void A_full_canal_backs_the_pour_up_and_the_pool_stops_draining()
  {
    var scene = ColdBlastFurnaceScenes.Complete();
    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Melting, CampaignSeconds) > 0
    );
    Assert.True(scene.OpenIronTap());

    // The runout fills to its own capacity and stops accepting.
    Assert.True(
      scene.RunUntil(s => s.IronCanalUnits >= s.IronCanalCapacity, 60) > 0,
      "the open tap should fill the canal start to capacity"
    );
    Assert.Equal(scene.IronCanalCapacity, scene.IronCanalUnits);

    // With the runout brim-full the pour is refused. The pool must be *held*, not silently swallowed:
    // the furnace only gives up metal the canal actually accepted.
    float pooled = scene.MoltenIron;
    scene.RunLive(20);

    Assert.Equal(scene.IronCanalCapacity, scene.IronCanalUnits);
    Assert.True(
      scene.MoltenIron > pooled,
      $"a backed-up tap must not eat the pool; {scene.MoltenIron} vs {pooled}"
    );

    // The control: clear the runout and the very next tick pours again, so the stall really was the
    // full canal and not a tap that had quietly stopped working.
    scene.IronCanal.DrainMetal(scene.IronCanalCapacity);
    Assert.Equal(0, scene.IronCanalUnits);
    scene.RunLive(1);
    Assert.True(
      scene.IronCanalUnits > 0,
      "with the runout clear the same tap should pour again"
    );
  }

  /// <summary>
  /// F5: every other tap assertion in this suite is direction-only (<c>IronCanalUnits &gt; 0</c>,
  /// <c>== 0</c>, <c>&gt;= IronCanalCapacity</c>) against a 50-unit canal that a multi-second run always
  /// saturates regardless of the tap's actual per-tick rate - reachable in 5 ticks even at the pre-fix
  /// hard-coded ceiling (<c>Ceiling(20 * TapIronStackFactor) = 12</c>/tick). Proven empirically:
  /// setting <c>TapDrainPerTick</c> back to 20 left all nine existing cases in this
  /// suite green.
  /// <para>
  /// This pins the rate itself, not just its direction: bank the pool well past a single tick's cap,
  /// open the tap fresh (so the canal starts at exactly 0), then run <b>exactly one</b> production tick.
  /// The canal must then read exactly <c>Ceiling(min(TapDrainPerTick, MoltenIron) * TapIronStackFactor)</c>
  /// - at the shipped defaults (50, 0.6f) that is <b>31</b>, not the mathematically-clean 30 (see
  /// <c>BlastFurnaceTapTests.Retuning_the_tap_rate_moves_the_drain_with_it</c> for why 0.6f rounds up
  /// through <c>Math.Ceiling</c>) and nowhere near the pre-fix ceiling's 12. A future rework that
  /// silently reintroduces a hard 20-unit cap fails this at 12, not 31.
  /// </para>
  /// </summary>
  [Fact]
  public void A_freshly_opened_tap_drains_exactly_one_ticks_worth_not_a_hidden_ceiling()
  {
    var scene = ColdBlastFurnaceScenes.Complete();
    Assert.True(
      scene.RunUntil(s => s.MoltenIron > 200f, CampaignSeconds) > 0,
      "need a pool well past a single tick's cap before measuring one tick's drain"
    );

    Assert.True(scene.OpenIronTap(), "the iron tap should open over its canal");
    Assert.Equal(0, scene.IronCanalUnits); // nothing has drained yet - the tap was closed until now

    scene.RunLive(1);

    Assert.Equal(31, scene.IronCanalUnits);
  }

  #endregion

  #region What the blast meters

  /// <summary>
  /// <b>Campaign length is arithmetic, and this is the case that pins the arithmetic.</b> A lit shaft
  /// burns <c>BfRacewayCarbonPerTuyerePerSecond x tuyeres</c> of carbon a second at full blast, and it runs
  /// until that carbon is gone - so how long a furnace lasts is what the player charged divided by how hard
  /// it is being blown, with no timer in it anywhere.
  /// <para>
  /// <b>The tuyere count is the half nothing else covers</b>, and it was proved missing rather than
  /// assumed: with the rate keyed per furnace instead of per tuyere, all five suites stayed green. A bigger
  /// furnace being a <em>faster</em> furnace is the whole reason to build one, and it is exactly the kind of
  /// claim that quietly stops being true - so it is asserted against the furnace's own scanned tuyere list
  /// rather than against a literal, and a machine that stopped multiplying by it halves this number.
  /// </para>
  /// <para>
  /// Measured over a window rather than a tick: coke is items, so a fuel band burns in whole units and a
  /// single second usually spends none. <c>ColdBlastFurnaceRig</c> lays real rounds, so what is being
  /// measured is a real charge burning down.
  /// </para>
  /// </summary>
  [Fact]
  public void The_blast_meters_the_carbon_at_the_rate_per_tuyere_it_advertises()
  {
    var scene = ColdBlastFurnaceScenes.Complete(charge: 0);
    Assert.Equal(2, scene.TuyereCount); // the cold furnace's drawing, so the expectation below is real

    // Let it light and settle, so the window measures a steady burn rather than the first tick.
    Assert.True(scene.RunUntil(s => s.State != FurnaceState.Idle, 30) > 0);
    scene.RunLive(10);

    const int window = 240;
    int before = scene.CokeUnits;
    scene.RunLive(window);
    int burned = before - scene.CokeUnits;

    float expected =
      IwexValues.BfRacewayCarbonPerTuyerePerSecond * scene.TuyereCount * window;

    // ±5 %: the spend is carried between ticks in whole units, so a window can end mid-unit.
    Assert.True(
      Math.Abs(burned - expected) <= expected * 0.05f,
      $"a furnace on full blast should burn {expected} u of carbon in {window} s; it burned {burned}"
    );
  }

  #endregion

  #region Breach

  /// <summary>
  /// <b>A furnace that loses its walls keeps burning.</b> <c>OnStructureLost</c> once called
  /// <c>Extinguish()</c> - applying the <b>choke</b> behaviour (sealed and
  /// starved, so it goes out) to its exact physical opposite. A furnace that loses its stack becomes a
  /// bonfire in a brick ruin: opened to the air it draws harder and burns its coke off at open-air
  /// temperature. A furnace whose <em>blast</em> fails is the one that dies, because a packed shaft has
  /// almost no natural draught of its own. Two failure modes, and they are opposites.
  /// <para>
  /// <b>Nothing in any of the five suites asserted what a breached lit furnace does</b> before this.
  /// </para>
  /// </summary>
  [Fact]
  public void A_breached_lit_furnace_keeps_burning_its_coke_and_makes_nothing()
  {
    var scene = ColdBlastFurnaceScenes.Complete();
    Assert.True(
      scene.RunUntil(s => s.MoltenIron > 0f, CampaignSeconds) > 0,
      "the furnace should be melting before its wall comes out"
    );

    scene.Breach();
    scene.RunLive(5);
    Assert.False(scene.Core.StructureComplete, "the breach should be visible to the furnace");

    int coke = scene.ColumnUnits;
    float made = scene.MoltenIron;
    scene.RunLive(180);

    // Still alight. A frozen furnace and an extinguished one look identical from the outside, so the
    // assertion is that it is still consuming - that is the only observable that tells burning from both.
    Assert.NotEqual(FurnaceState.Idle, scene.State);
    Assert.True(
      scene.ColumnUnits < coke,
      $"a breached furnace must still be burning its charge; {scene.ColumnUnits} vs {coke}"
    );

    // ...and running on natural draught alone, so it is well below the melt line and renders nothing.
    Assert.True(
      scene.Temp < IwexValues.BfIronMeltingPoint,
      $"a breach is open to the air, so it should fall to natural draught; was {scene.Temp} C"
    );
    Assert.Equal(made, scene.MoltenIron, 3);
  }

  [Fact]
  public void A_breached_IDLE_furnace_stays_idle_and_cannot_be_lit_through_the_hole()
  {
    // The clause that keeps breach and choke distinguishable. Without it a player could knock a wall
    // out of a cold furnace and light it through the gap, making "opened to the air" a strictly better
    // way to run one.
    // Staged on the permanently-incomplete scene rather than by breaching a complete one: the structure
    // monitor only re-runs every few seconds, so a complete furnace lights on its first tick and is
    // already burning by the time a breach could be noticed - which is correct, and untestable that way.
    var scene = ColdBlastFurnaceScenes.BackwardsIronTap();
    Assert.False(scene.Core.StructureComplete);
    Assert.Equal(FurnaceState.Idle, scene.State);

    scene.RunLive(120);

    Assert.Equal(FurnaceState.Idle, scene.State);
    Assert.Equal(0f, scene.MoltenIron, 3);
  }

  #endregion

  #region Extinguish

  [Fact]
  public void The_pool_freezes_onto_the_hearth_rather_than_vanishing()
  {
    var scene = ColdBlastFurnaceScenes.Complete();
    Assert.True(
      scene.RunUntil(s => s.MoltenIron > 0f, CampaignSeconds) > 0,
      "the furnace should have made pig before it is put out"
    );

    // Let the campaign run itself out, remembering the pool as of the last tick it was still lit - that is
    // the metal the freeze has to account for.
    //
    // It used to be put out by cutting the blast, and that no longer works: a stopped blower throttles the
    // fire to natural draught rather than snuffing it (see
    // Cutting_the_blast_takes_a_melting_furnace_back_out_of_melting). What ends a campaign is the carbon
    // running out, so the scene simply waits for it - with the taps shut, so the pool it made is still
    // standing in the hearth when the fire dies, which is the situation the freeze exists for.
    float pooled = 0f;
    Assert.True(
      scene.RunUntil(
        s => s.State == FurnaceState.Idle,
        BurnoutSeconds,
        s =>
        {
          if (s.State != FurnaceState.Idle)
            pooled = s.MoltenIron;
        }
      ) > 0,
      "a furnace should go out when its carbon is gone"
    );
    Assert.True(pooled > 0f);

    // The pool froze across the bottom layer of the shaft as solid iron - not slag, not nothing.
    // Three cells since the furnace redraw, not two: the crucible runs the full width of the
    // hearth course now. Counting only the old pair left a third of the metal unaccounted for and the
    // nugget total short - a wrong expectation that read exactly like a leaking freeze.
    var crucible = new[] { (-1, 1, 0), (0, 1, 0), (1, 1, 0) };
    foreach (var (x, y, z) in crucible)
      Assert.Equal(
        "iwex:hearthmetal-pigiron",
        scene.BlockAtLocal(x, y, z).Code?.ToString()
      );

    // ...and it carries the metal, rather than being a decorative block over a deleted pool. The
    // cells together hold the whole pool at the configured units-per-nugget.
    int nuggets = 0;
    foreach (var (x, y, z) in crucible)
      nuggets += Count(scene.BlockEntityAtLocal(x, y, z));
    Assert.Equal(
      Math.Max(1, (int)Math.Floor(pooled / IwexValues.BfUnitsPerSolidNugget)),
      nuggets
    );

    // Nothing on the extinguish path makes slag: the slag pool is simply cleared.
    Assert.Equal(0f, scene.MoltenIron, 3);
    Assert.Equal(0f, scene.MoltenSlag, 3);

    static int Count(Vintagestory.API.Common.BlockEntity? be) =>
      be is BlockEntityHearthMetal solid ? solid.MetalCount : 0;
  }

  [Fact]
  public void Burnt_out_charge_comes_back_as_salvage_richer_in_coke_at_the_top()
  {
    // A furnace that goes out is a setback, not a total loss. The column is rewritten in place as
    // spent charge: the ore and flux survive verbatim, and only the coke is burned out - by height,
    // because the blast burned hardest at the tuyeres and never reached the top of the stack. The
    // player digs the column out, adds fresh coke bands and charges it again.
    //
    // Run on the standard burden precisely because it never melts: with no pool there is no freeze,
    // so both piles - including the one on the hearth floor - survive to be read.
    var scene = ColdBlastFurnaceScenes.StandardBurden();
    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Firing, 30) > 0,
      "the furnace should have lit"
    );

    // It is put out by the chill, with the blowers left running. A standard burden never melts, so its
    // column never makes room to descend, so the fire eats the carbon it can reach and then finds burden
    // where the next coke course should be - which leaves the whole shaft standing there to be salvaged,
    // and is exactly the situation burn-out exists for. (Cutting the blast would only throttle it.)
    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Idle, BurnoutSeconds) > 0,
      "a chilled furnace should go out"
    );

    // Both piles are still there as charge piles - burned out, not destroyed and not slagged. (Stated
    // as "is still a charge pile" rather than "is not slag": the latter is satisfied by an empty cell,
    // so it would hold even if the whole column had been deleted.)
    // `iwex:furnace-chargepile`, not `game:coalpile`, since the column cutover: the shaft's charge is
    // the furnace's own and the block over it is a window onto it rather than a vanilla container.
    Assert.Equal(
      "iwex:furnace-chargepile",
      scene.BlockAtLocal(0, 1, 0).Code?.ToString()
    );
    Assert.Equal(
      "iwex:furnace-chargepile",
      scene.BlockAtLocal(0, 5, 0).Code?.ToString()
    );
    Assert.NotNull(scene.PileAtLocal(0, 1, 0));
    Assert.NotNull(scene.PileAtLocal(0, 5, 0));

    BurdenMix charged = scene.ChargedMix;
    BurdenMix bottom = scene.SalvageAtLocal(0, 1, 0); // sitting on the tuyeres
    BurdenMix top = scene.SalvageAtLocal(0, 5, 0); // top of the charge column

    // The salvage is real: ore and flux come back untouched at both ends of the column.
    Assert.Equal(charged.Iron, bottom.Iron, 3);
    Assert.Equal(charged.Iron, top.Iron, 3);
    Assert.Equal(charged.Flux, bottom.Flux, 3);
    Assert.Equal(charged.Flux, top.Flux, 3);

    // The gradient - the point of the case. Asserted as a relationship, plus a tie to the config keys
    // themselves rather than to today's numbers, so retuning the retention pair moves the test with it.
    Assert.True(
      top.Fuel > bottom.Fuel,
      $"the top of the shaft should keep more coke than the bottom; "
        + $"top {top.Fuel} vs bottom {bottom.Fuel}"
    );
    Assert.True(top.Fuel < charged.Fuel, "even the top of the column loses coke");
    Assert.Equal(
      charged.Fuel * IwexValues.BfBurnoutFuelRetainedTop,
      top.Fuel,
      4
    );
    Assert.Equal(
      charged.Fuel * IwexValues.BfBurnoutFuelRetainedBottom,
      bottom.Fuel,
      4
    );
  }

  /// <summary>
  /// Fixed production defect (Phase 1 fix wave, F2) - the salvage a dead furnace owes the player used to
  /// be destroyed geometrically if nobody came back for it immediately.
  /// <para>
  /// <c>Extinguish</c> leaves the shaft exactly as it was: full, and (before the fix) with every pile
  /// still alight. <c>BlastmixPiles.ReleaseFromFurnace</c> (<c>CoalPileBlastmixPatches.cs</c>) reset the
  /// pile's burn timer but never cleared <c>burning</c>, and the ignition gate in
  /// <c>BlockEntityFurnaceCore.OnProductionTick</c> asks only for
  /// <c>Idle &amp;&amp; StructureComplete &amp;&amp; _cachedIsFull &amp;&amp; !IsChoked</c> plus an
  /// all-lit shaft - so the furnace re-ignited on the <b>very next tick</b>. With no blast it starved
  /// out again one extinguish grace later, and <c>Extinguish → ExtinguishResidue → BurnOutCharge</c>
  /// multiplied the remaining coke by <c>retained</c> a second time. And a third.
  /// </para>
  /// <para>
  /// The burden used to decay as <c>fuel × retained^n</c>, one power per relight cycle: a stalled standard
  /// burden left for five minutes returned essentially no coke at all - while <c>bf-info-burnedout</c>
  /// was still telling the player to dig the column out and re-coke it. The loop was normally invisible
  /// because a furnace that had a molten pool freezes it onto the hearth, which breaks the structure
  /// and stops the production tick; a furnace that never melted (this one) had nothing to freeze and
  /// oscillated indefinitely.
  /// </para>
  /// <para>
  /// Fix: <c>ReleaseFromFurnace</c> now also calls <c>pile.Extinguish()</c> when the pile is still
  /// burning, so a released pile's own fire goes out with the furnace and the ignition gate cannot
  /// find an already-lit shaft on the next tick. The invariant asserted here is the honest one -
  /// <b>burn-out is applied once</b>, so the salvage a player finds does not depend on how fast they ran
  /// back to the furnace.
  /// </para>
  /// </summary>
  [Fact]
  public void A_furnace_left_alone_after_it_goes_out_keeps_burning_its_own_salvage()
  {
    var scene = ColdBlastFurnaceScenes.StandardBurden();
    Assert.True(scene.RunUntil(s => s.State == FurnaceState.Firing, 30) > 0);
    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Idle, BurnoutSeconds) > 0
    );

    // The salvage the player is owed, read on the tick the fire died.
    BurdenMix owed = scene.SalvageAtLocal(0, 5, 0);
    Assert.True(owed.Fuel > 0f, "the top of the column should keep some coke");
    int cokeLeft = scene.CokeUnits;

    // Nobody comes back for the column for another twenty minutes.
    scene.RunLive(1200);

    Assert.Equal(FurnaceState.Idle, scene.State);
    Assert.Equal(owed.Fuel, scene.SalvageAtLocal(0, 5, 0).Fuel, 5);
    // And the coke bands are untouched too, which the stamp alone cannot say. Burn-out zeroes the carbon
    // at the raceway (BfBurnoutFuelRetainedBottom is 0) precisely so a dead furnace cannot re-derive itself
    // alight off its own salvage - and this is the assertion that the gate holds for the whole idle stretch
    // rather than only on the tick it was applied.
    Assert.Equal(cokeLeft, scene.CokeUnits);
  }

  #endregion

  #region Charcoal salvage

  // Everything above this line lays coke. Charcoal has been accepted by the shaft all along - it lit,
  // burned, made gas, descended, yielded iron at coke's exact rate and rendered pixel-for-pixel the same -
  // so every one of those cases passes identically against a furnace whose fuel test is the literal
  // `game:coke`. Now charcoal is priced (half the carbon a unit, `CarbonPerUnit`), and the
  // cases here are the ones that cannot pass on a coke-only machine.
  //
  // The rig charges rounds in whatever fuel it is given (`ChargeWithFuel`), so a charcoal counterpart of
  // any scene costs one call and no second fixture. `charge: -1` means "stand the furnace up but lay
  // nothing" - the constructor charges in coke otherwise, and a shaft holding both would prove neither.

  /// <summary>
  /// The <b>exact geometric twin</b> of <see cref="ColdBlastFurnaceScenes.StandardBurden"/>: a full shaft of
  /// rounds at <c>BfReferenceFuelFrac</c> by volume, laid in charcoal instead of coke. Same courses, same
  /// band sizes, same column heights - the only difference in the whole scene is which item the fuel course
  /// is made of, which is what lets the two be compared band for band.
  /// <para>
  /// 20 % by volume is <b>10 % by carbon</b> here, half of what the same rounds in coke are worth, so it
  /// lights, holds well under the ~23.9 % cold break-even and chills. That is the honest charcoal campaign,
  /// and it is what makes these cases readable: a furnace that never melts freezes no pool, so the whole
  /// column is still standing to be dug out when the fire dies.
  /// </para>
  /// <para>
  /// <c>charge: -1</c> means "stand the furnace up but lay nothing" - the constructor charges in coke
  /// otherwise, and a shaft holding both fuels would prove neither.
  /// </para>
  /// </summary>
  private static ColdBlastFurnaceRig CharcoalShaft() =>
    ColdBlastFurnaceScenes
      .Complete(charge: -1, fuelFrac: IwexValues.BfReferenceFuelFrac)
      .ChargeWithFuel(ColdBlastFurnaceRig.CharcoalCode);

  /// <summary>
  /// <b>The no-was-lit-bit invariant, proven through the second fuel.</b> A shaft has no state machine:
  /// what stops a dead furnace re-deriving itself alight on the very next tick is that burn-out leaves no
  /// carbon at its own raceway (<c>BfBurnoutFuelRetainedBottom</c> is <b>0</b>) - not a flag, not a timer.
  /// <c>A_furnace_left_alone_after_it_goes_out_keeps_burning_its_own_salvage</c> pins that, and pins it for
  /// coke alone.
  /// <para>
  /// <b>What that leaves open.</b> <c>BurnOutCharge</c> recognises a fuel band through
  /// <c>IsFuelCode</c>; make that a coke literal and a charcoal band takes the <em>legacy burden</em> branch
  /// instead - it carries <c>default</c> mix, so it is stamped with the standard-grade shares and keeps
  /// <b>every unit</b>. The raceway still reads carbon-bearing, the furnace re-ignites on the next tick,
  /// starves one grace later, and burn-out multiplies the retention again. And again. The relight
  /// oscillation the fuel-pricing work closed, returning intact through the fuel nothing tested.
  /// </para>
  /// <para>
  /// Watched second by second over the idle stretch rather than sampled at the end: an oscillating
  /// furnace spends most of its time <c>Idle</c>, so "Idle twenty minutes later" is exactly what it looks
  /// like. What separates the two is that nothing was <b>consumed</b> - the same observable the breach and
  /// cut-blast cases turn, for the same reason.
  /// </para>
  /// </summary>
  [Fact]
  public void A_charcoal_furnace_that_went_out_cannot_relight_off_its_own_salvage()
  {
    var scene = CharcoalShaft();

    // The scene's own premises, so it cannot pass by holding no fuel or by holding coke after all.
    Assert.True(scene.FuelBandUnits > 0, "the shaft must actually hold fuel bands");
    Assert.Equal(scene.FuelBandUnits * 0.5f, scene.CarbonUnits, 3); // priced at half of coke, per unit

    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Firing, 30) > 0,
      "a complete raceway course of charcoal should LIGHT - it is the melt it never reaches"
    );
    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Idle, BurnoutSeconds) > 0,
      "a charcoal shaft under the cold break-even should chill and go out"
    );

    // The salvage is real: charcoal survives up the shaft, where the blast never reached.
    Assert.True(
      scene.FuelBandUnits > 0,
      "the top of the column should keep some charcoal, or there is no salvage to speak of"
    );
    // ...and none of it is left in front of a tuyere, on any column - which is the whole gate.
    foreach (ChargeColumn column in scene.Core.ShaftColumns.Values)
      Assert.False(
        column.Segments.Count > 0
          && BlockEntityFurnaceCore.IsFuelCode(column.Segments[0].Material),
        "burnt-out charge must leave no carbon at the raceway of any column"
      );

    int fuel = scene.FuelBandUnits;
    float carbon = scene.CarbonUnits;

    // Nobody comes back for the column for another twenty minutes.
    bool everLit = false;
    scene.RunLive(1200, s => everLit |= s.State != FurnaceState.Idle);

    Assert.False(everLit, "a dead charcoal furnace must never re-derive itself alight");
    Assert.Equal(FurnaceState.Idle, scene.State);
    Assert.Equal(fuel, scene.FuelBandUnits); // the bands are untouched, which the stamp alone cannot say
    Assert.Equal(carbon, scene.CarbonUnits, 3);
  }

  /// <summary>
  /// The charcoal twin of <c>Burnt_out_charge_comes_back_as_salvage_richer_in_coke_at_the_top</c>: what a
  /// dead furnace owes the player is the <b>burden's</b> ore and flux, verbatim, on the same height
  /// gradient - and that promise is about the shaft, not about what was burned in it.
  /// <para>
  /// It is the <em>identical</em> gradient, asserted against the same two config keys rather than
  /// against numbers, so a retention that ever grew a per-fuel branch fails here. <c>SalvageAtLocal</c>
  /// steps over fuel bands through the production predicate, which is the reason this reads burden at all
  /// on a charcoal furnace - a code literal there would answer <c>default</c> and every line below would be
  /// comparing an empty struct against a burden, silently and in the direction that looks like lost salvage.
  /// </para>
  /// </summary>
  [Fact]
  public void Charcoal_salvage_keeps_its_ore_and_flux_on_the_same_gradient_as_cokes()
  {
    var scene = CharcoalShaft();
    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Firing, 30) > 0,
      "the furnace should have lit"
    );
    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Idle, BurnoutSeconds) > 0,
      "a chilled charcoal furnace should go out"
    );

    // Still charge piles, at both ends of the column - burned out, not destroyed and not slagged.
    Assert.NotNull(scene.PileAtLocal(0, 1, 0));
    Assert.NotNull(scene.PileAtLocal(0, 5, 0));

    BurdenMix charged = scene.ChargedMix;
    BurdenMix bottom = scene.SalvageAtLocal(0, 1, 0); // sitting on the tuyeres
    BurdenMix top = scene.SalvageAtLocal(0, 5, 0); // top of the charge column

    Assert.Equal(charged.Iron, bottom.Iron, 3);
    Assert.Equal(charged.Iron, top.Iron, 3);
    Assert.Equal(charged.Flux, bottom.Flux, 3);
    Assert.Equal(charged.Flux, top.Flux, 3);

    Assert.Equal(charged.Fuel * IwexValues.BfBurnoutFuelRetainedTop, top.Fuel, 4);
    Assert.Equal(
      charged.Fuel * IwexValues.BfBurnoutFuelRetainedBottom,
      bottom.Fuel,
      4
    );
  }

  #endregion

  #region Charcoal is a trade, not an alias

  // The cases above prove charcoal survives the shaft's machinery. These prove it is priced there.
  // Every one of them is built to go red on two mutations and to say which: aliasing charcoal to coke
  // (`CarbonPerUnit` returning a flat 1.0, or `Accumulate` counting bands), and ignoring it (the fuel
  // predicate falling back to a `game:coke` literal). A case that survives both is testing the fixture.
  //
  // The ratio is read off the registry, never off `CarbonPerUnit` and never as a literal - see
  // `CarbonOf`. `CarbonPerUnit` is the thing under test; an expectation that called it would move with the
  // defect and the whole region would stay green on a furnace that had stopped telling the two apart.

  /// <summary>
  /// The carbon one charge unit of <paramref name="code"/> carries, in coke units - production's own
  /// formula (the <c>fuel</c>-role value over <see cref="IwexValues.BfFuelCarbonReference"/>) recomputed
  /// here from the registry, so a retune of either moves the expectation and a defect in
  /// <c>CarbonPerUnit</c> does not.
  /// </summary>
  private static float CarbonOf(string code) =>
    MaterialRoleRegistry.ValueOf(Roles.Fuel, new AssetLocation(code))
    / IwexValues.BfFuelCarbonReference;

  /// <summary>
  /// A complete, blown cold furnace charged to the brim with real rounds laid in <paramref name="fuel"/> at
  /// <paramref name="fuelFrac"/> <b>by volume</b>. <c>charge: -1</c> stands the furnace up and lays
  /// nothing; the constructor would otherwise charge it in coke, and a shaft holding both fuels would prove
  /// neither.
  /// </summary>
  private static ColdBlastFurnaceRig Rounds(string fuel, float fuelFrac = 0.30f) =>
    ColdBlastFurnaceScenes
      .Complete(charge: -1, fuelFrac: fuelFrac)
      .ChargeWithFuel(fuel);

  /// <summary>
  /// <b>The blast meters carbon, so two fuels of different worth burn away at different volumes.</b> Two
  /// furnaces identical to the cell - same layout, same blast, same burden stamp, same fuel-band volume
  /// charged - differing only in which fuel the rounds are laid with. Over the same window they spend the
  /// same carbon, and the charcoal one loses fuel-band volume in exactly the ratio the registry declares.
  /// <para>
  /// Both halves are needed and neither alone is enough. The equal <b>carbon</b> is what says the blast is
  /// still the only throttle - a furnace that burned charcoal "faster" to compensate would have quietly
  /// grown a second one. The volume <b>ratio</b> is what says charcoal is priced at all: alias it to coke and
  /// the two shafts empty at the same rate, which is the old unpriced model exactly.
  /// </para>
  /// <para>
  /// Measured over a window rather than a tick, and with both furnaces asserted still alight at the end of
  /// it. A fuel band burns in whole units - a single second usually spends none - and a charcoal shaft at
  /// this fraction chills a few minutes in, so a window that outran it would be comparing a burning furnace
  /// against a dead one.
  /// </para>
  /// </summary>
  [Fact]
  public void Charcoal_burns_at_its_own_carbon_value_not_cokes()
  {
    var coke = Rounds(ColdBlastFurnaceRig.CokeCode);
    var charcoal = Rounds(ColdBlastFurnaceRig.CharcoalCode);

    float ratio =
      CarbonOf(ColdBlastFurnaceRig.CokeCode)
      / CarbonOf(ColdBlastFurnaceRig.CharcoalCode);
    Assert.True(ratio > 1f, "the registry must price coke above charcoal, or this claims nothing");

    // The premise the whole comparison rests on: the same volume of fuel stands in both shafts, carrying
    // different carbon. Without this the case could pass on two furnaces charged differently.
    Assert.True(coke.FuelBandUnits > 0, "the coke shaft must hold fuel bands");
    Assert.Equal(coke.FuelBandUnits, charcoal.FuelBandUnits);
    Assert.Equal(coke.CarbonUnits / ratio, charcoal.CarbonUnits, 2);

    // Let both settle out of their first tick, then measure a steady burn.
    const int settle = 20;
    const int window = 120;
    coke.RunLive(settle);
    charcoal.RunLive(settle);

    int cokeBandsBefore = coke.FuelBandUnits;
    int charcoalBandsBefore = charcoal.FuelBandUnits;
    float cokeCarbonBefore = coke.CarbonUnits;
    float charcoalCarbonBefore = charcoal.CarbonUnits;

    coke.RunLive(window);
    charcoal.RunLive(window);

    // Both still burning, or the window has outrun one of them and the deltas are not comparable.
    Assert.NotEqual(FurnaceState.Idle, coke.State);
    Assert.NotEqual(FurnaceState.Idle, charcoal.State);

    int cokeBands = cokeBandsBefore - coke.FuelBandUnits;
    int charcoalBands = charcoalBandsBefore - charcoal.FuelBandUnits;
    float cokeCarbon = cokeCarbonBefore - coke.CarbonUnits;
    float charcoalCarbon = charcoalCarbonBefore - charcoal.CarbonUnits;
    Assert.True(cokeBands > 0, "the coke furnace should have burned something in the window");

    // Same carbon: the blast is the throttle and it does not know which fuel it is burning.
    Assert.True(
      Math.Abs(cokeCarbon - charcoalCarbon) <= cokeCarbon * 0.05f,
      $"the blast meters carbon, so both should spend the same; coke {cokeCarbon} vs charcoal {charcoalCarbon}"
    );

    // Different volume, in the registry's own ratio. This is the assertion aliasing fails.
    float expectedBands = cokeBands * ratio;
    Assert.True(
      Math.Abs(charcoalBands - expectedBands) <= expectedBands * 0.05f,
      $"charcoal should burn {ratio}x the band volume for the same carbon; "
        + $"expected ~{expectedBands} u, burned {charcoalBands} against coke's {cokeBands}"
    );
  }

  /// <summary>
  /// <b>The case that proves the feature.</b> These two furnaces' temperatures were once
  /// <b>bit-identical</b>: charcoal was accepted, burned, made gas and yielded iron at coke's exact rate, so
  /// a charge laid in it was a pure speed buff - identical flame, identical iron, half the campaign.
  /// <para>
  /// Same geometry, same 30 % fuel course by volume, same cold blast. The raceway reads 30 % of a coke round
  /// as ~0.36 coke fraction and 30 % of a charcoal one as ~0.22, so the coke twin clears iron's melt line
  /// and the charcoal twin settles roughly 200 °C under it. That is the trade, and it is a wall only at this
  /// fraction - see <see cref="Charcoal_still_makes_a_viable_campaign_on_a_richer_course"/>.
  /// </para>
  /// <para>
  /// Asserted on each furnace's <b>peak</b> over its run rather than on an end-of-window sample. The
  /// raceway composition moves as courses arrive and a chilled furnace stops recomputing its balance
  /// entirely, so a single sample would be reading whichever moment the window happened to end on - and
  /// "never crossed the melt line" is a claim about the whole run, not about one second of it.
  /// </para>
  /// </summary>
  [Fact]
  public void A_charcoal_charge_at_the_same_band_fraction_runs_COOLER_than_a_coke_one()
  {
    var coke = Rounds(ColdBlastFurnaceRig.CokeCode);
    var charcoal = Rounds(ColdBlastFurnaceRig.CharcoalCode);

    float cokeFactor = 0f;
    float cokeTemp = 0f;
    bool cokeMelted = false;
    int lit = coke.RunUntil(
      s => s.State == FurnaceState.Melting,
      CampaignSeconds,
      s =>
      {
        cokeFactor = Math.Max(cokeFactor, s.Heat.FuelFactor);
        cokeTemp = Math.Max(cokeTemp, s.Temp);
        cokeMelted |= s.State == FurnaceState.Melting;
      }
    );
    Assert.True(lit > 0, "the coke twin must reach Melting, or there is nothing to be cooler than");

    // The charcoal twin gets the whole campaign window - strictly more time than the coke one needed - so
    // "it never got there" cannot be an artefact of a shorter run.
    float charcoalFactor = 0f;
    float charcoalTemp = 0f;
    bool charcoalMelted = false;
    charcoal.RunLive(
      CampaignSeconds,
      s =>
      {
        charcoalFactor = Math.Max(charcoalFactor, s.Heat.FuelFactor);
        charcoalTemp = Math.Max(charcoalTemp, s.Temp);
        charcoalMelted |= s.State == FurnaceState.Melting;
      }
    );

    Assert.True(
      charcoalFactor < cokeFactor,
      $"a charcoal round must burn leaner than a coke one at the same volume; "
        + $"charcoal {charcoalFactor} vs coke {cokeFactor}"
    );
    Assert.True(
      charcoalTemp < cokeTemp,
      $"and therefore cooler; charcoal peaked at {charcoalTemp} C, coke at {cokeTemp} C"
    );

    // ...and the difference straddles the melt line, which is what makes it a trade rather than a nuance.
    Assert.True(
      cokeTemp > IwexValues.BfIronMeltingPoint,
      $"the coke twin should cross the melt line; peaked at {cokeTemp} C"
    );
    Assert.True(
      charcoalTemp < IwexValues.BfIronMeltingPoint,
      $"the charcoal twin should never reach it; peaked at {charcoalTemp} C"
    );
    Assert.True(cokeMelted);
    Assert.False(charcoalMelted, "a 30 % charcoal course must not melt on cold blast");
    Assert.Equal(0f, charcoal.MoltenIron, 3);
  }

  /// <summary>
  /// The mirror of <c>A_full_shaft_with_no_coke_at_its_raceway_never_lights</c>: what a shaft needs in front
  /// of its tuyeres is <b>carbon</b>, not coke, so a course laid entirely in charcoal catches on its first
  /// tick with not a lump of coke anywhere in the furnace.
  /// <para>
  /// It carries its own arithmetic premise - the shaft's carbon is its fuel volume times <em>charcoal's</em>
  /// weight, read off the registry - so it cannot pass on a machine that lit because it thought the bands
  /// were coke. Lighting alone is the half that survives aliasing; the carbon is the half that does not.
  /// </para>
  /// <para>
  /// Its control is <see cref="A_raceway_of_a_non_fuel_that_is_not_burden_never_lights"/>, built the same
  /// way out of a material that carries no fuel role: the pair is what separates "the shaft reads the role"
  /// from "the shaft lights on anything that is not burden".
  /// </para>
  /// </summary>
  [Fact]
  public void A_shaft_whose_only_raceway_fuel_is_CHARCOAL_still_lights()
  {
    var scene = Rounds(ColdBlastFurnaceRig.CharcoalCode);
    Assert.True(scene.Core.StructureComplete);

    Assert.True(scene.FuelBandUnits > 0, "the shaft must actually hold fuel bands");
    Assert.Equal(
      scene.FuelBandUnits * CarbonOf(ColdBlastFurnaceRig.CharcoalCode),
      scene.CarbonUnits,
      2
    );

    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Firing, 5) > 0,
      "a complete raceway course of charcoal should light at once"
    );
    // ...and it is genuinely burning it, not sitting lit on a stale reading.
    float carbon = scene.CarbonUnits;
    scene.RunLive(60);
    Assert.True(
      scene.CarbonUnits < carbon,
      $"a lit charcoal shaft should be spending its carbon; {scene.CarbonUnits} vs {carbon}"
    );
  }

  /// <summary>
  /// <b><c>RacewayIsLightable</c> was once literally "anything that is not burden"</b>, and
  /// this is the case that keeps it from drifting back. A shaft charged in rounds of <b>raw anthracite</b> -
  /// a real vanilla item, not burden, and deliberately <em>not</em> granted <c>Roles.Fuel</c> because raw
  /// coal crushes to dust under a burden column (<c>docs/design/processes/coking.md</c>) - never catches.
  /// <para>
  /// It is the discriminator the no-coke scene cannot be. That one charges <b>burden</b>, so a furnace
  /// whose fuel test had degraded to <c>!IsBurden</c> would still refuse it and still look correct. This one
  /// is the everywhere-else: a full shaft, a complete raceway course, and something in front of every tuyere
  /// that is neither burden nor fuel.
  /// </para>
  /// <para>
  /// The control is the <b>charcoal</b> shaft rather than the coke one, deliberately: both are "not coke",
  /// so the pair asks about the role and nothing else.
  /// </para>
  /// </summary>
  [Fact]
  public void A_raceway_of_a_non_fuel_that_is_not_burden_never_lights()
  {
    const string anthracite = "game:ore-anthracite";
    Assert.False(
      BlockEntityFurnaceCore.IsFuelCode(anthracite),
      "raw coal must not carry the fuel role, or this scene is testing nothing"
    );
    Assert.False(Burden.IsCode(anthracite), "...and it is not burden either");

    var scene = ColdBlastFurnaceScenes.Complete(charge: -1).ChargeWithFuel(anthracite);
    Assert.True(scene.Core.StructureComplete);
    Assert.True(scene.ColumnUnits > 0, "the shaft must actually be charged");
    Assert.Equal(0, scene.FuelBandUnits); // nothing in it is fuel, on the production predicate
    Assert.Equal(0f, scene.CarbonUnits, 3);

    // Watched rather than sampled at the end: a furnace that lit and went out would read Idle anyway.
    bool everLit = false;
    scene.RunLive(120, s => everLit |= s.State != FurnaceState.Idle);

    Assert.False(everLit, "a raceway of a role-less material should never catch");
    Assert.Equal(FurnaceState.Idle, scene.State);

    // The control, and it is the whole case: the identical scene laid with charcoal - also not coke -
    // catches on its first tick, so what the shaft is reading is the role and not a literal.
    var lit = Rounds(ColdBlastFurnaceRig.CharcoalCode);
    lit.RunLive(2);
    Assert.Equal(FurnaceState.Firing, lit.State);
  }

  /// <summary>
  /// The positive half, so the trade is a <b>trade</b> and not a wall: charcoal buys a working campaign at a
  /// richer course.
  /// <para>
  /// <b>The fraction is arithmetic, not a guess.</b> Cold, full and blown, the balance is
  /// <c>T = BfCombustionBaseTemp + BfCombustionCokeGain·f - (BfRadiationLossBase + BfChargeLossFull)</c> =
  /// <c>520 + 900·f</c>, so crossing the 1482 °C melt line needs <c>f ≥ 1.069</c>, i.e. a raceway coke
  /// fraction of <b>0.239</b> (<c>f = 1 + 0.35·(F-0.2)/0.2</c>). A round of 32 units laid at volume fraction
  /// φ is <c>u = ⌊32φ⌋</c> of fuel over <c>32-u</c> of burden, and the burden contributes only its ore and
  /// flux, so the raceway reads <c>F = 0.5u / (0.5u + (1-φ)(32-u))</c> on charcoal. At φ = 0.30 that is
  /// <b>0.218</b> - short, which is the cooler case above. Break-even lands near φ = 0.32; at
  /// <b>φ = 0.40</b> (u = 12) it is <c>6/18 = 0.333</c>, giving <c>f = 1.233</c> and ~1630 °C, comfortably
  /// clear. So charcoal costs about ten points of course richness, not the melt.
  /// </para>
  /// <para>
  /// The arithmetic is then <b>checked live</b> rather than only stated: the furnace's own
  /// <c>Heat.TProcess</c> has to clear the melt line, so a retune that moved any of those five keys fails
  /// here with the reason visible instead of somewhere downstream.
  /// </para>
  /// </summary>
  [Fact]
  public void Charcoal_still_makes_a_viable_campaign_on_a_richer_course()
  {
    // 0.40 by volume - worked out above, and roughly eight points over the charcoal break-even.
    var scene = Rounds(ColdBlastFurnaceRig.CharcoalCode, fuelFrac: 0.40f);

    Assert.True(
      scene.RunUntil(s => s.MoltenIron > 0f, CampaignSeconds) > 0,
      "a charcoal furnace on a rich enough course should render pig within a campaign"
    );

    Assert.Equal(FurnaceState.Melting, scene.State);
    Assert.True(
      scene.Heat.TProcess > IwexValues.BfIronMeltingPoint,
      $"the balance itself should clear the melt line; T_process was {scene.Heat.TProcess} C"
    );
    Assert.True(scene.MoltenIron > 0f, "it should have made pig");
    Assert.True(scene.MoltenSlag > 0f, "and slag alongside it");
    Assert.Equal(0f, scene.Heat.PreheatGain, 1); // still cold blast - the charge did all of it

    // And it melted on charcoal, priced as charcoal. Without this the case passes just as well on a
    // furnace that had aliased the fuel to coke - which is precisely the machine that made 30 % melt too.
    Assert.True(
      scene.CarbonUnits < scene.FuelBandUnits,
      $"the fuel standing in this shaft must be worth less than coke per unit; "
        + $"{scene.CarbonUnits} carbon in {scene.FuelBandUnits} u of bands"
    );

    // It really does drain, so "viable" means a campaign and not a puddle.
    Assert.True(scene.OpenIronTap(), "the iron tap should open over its canal");
    scene.RunLive(30);
    Assert.True(scene.IronCanalUnits > 0, "pig should have reached the canal start");
    Assert.Contains("pigiron", scene.IronCanalMetal);
  }

  #endregion

}
