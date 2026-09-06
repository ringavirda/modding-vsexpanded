using System;
using ExpandedLib.Industry.Materials;
using ExpandedLib.Catalogues;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Products.BlockEntities;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The cold blast furnace driven end to end: a charged, lit, mechanically blown shaft climbs past iron's
/// melt line on the coke in its own burden, melts ore burden into pig iron and slag, taps both into canals
/// and, when it goes out, freezes its pool onto the hearth and hands the column back as salvage
/// (<c>docs/design/layered-charge.md</c>). Everything runs on the clock through
/// <see cref="ColdBlastFurnaceScenes"/>: the real 160-cell footprint stands, the furnace completes itself
/// and its own production tick does the work, with no state fast-forwarded by reflection.
/// </summary>
// Joins the furnace-config collection as a reader: these scenes read computed heat balances, so they must
// not run while HeatBalanceTests has BfCombustionBaseTemp or BfCokeSensitivity off their shipped values.
[Collection(FurnaceConfigCollection.Name)]
public class ColdBlastFurnaceScenarioTests {
  /// <summary>
  /// Ceiling in seconds for a cold furnace to blow in and reach the melt line, with slack. Not derivable
  /// from the config: what takes time is the charge warming through. Never wait this out as a fixed
  /// duration - a campaign ends when the coke does, not when a timer does. Use
  /// <see cref="ColdBlastFurnaceRig.RunUntil"/> and assert on the state it was waited for.
  /// </summary>
  private const int CampaignSeconds = 900;

  /// <summary>
  /// Ceiling in seconds for "run it until the fire dies", with slack. A shaft furnace has no extinguish
  /// timer, so what is waited for is <c>coke ÷ (BfRacewayCarbonPerSecond × AirFactor)</c>: a full cold
  /// shaft at 30 % coke is ~370 u of carbon against 0.35 u/s, so ~1 060 s blown and twice that on natural
  /// draught. Waited for with <see cref="ColdBlastFurnaceRig.RunUntil"/>, never sampled at a fixed offset.
  /// </summary>
  private const int BurnoutSeconds = 2400;

  #region The whole process, emergent

  [Fact]
  public void Charge_light_melt_tap_yields_pig_and_slag_into_their_canals() {
    // A coke-rich burden is what makes a cold furnace work: with no cowper on the line every degree
    // above iron's melt line has to come out of the charge. 30 % coke clears the ~23.9 % break-even.
    var scene = ColdBlastFurnaceScenes.Complete();

    // It starts built, cold, idle and untouched - the structure completed itself.
    Assert.True(
      scene.Core.StructureComplete,
      "the furnace should have completed its own 160-cell structure"
    );
    Assert.Equal(FurnaceState.Idle, scene.State);

    // Waited on the product, not on the state: entering Melting and having melted something are different
    // moments, because the burden still has to arrive at the raceway hot.
    Assert.True(
      scene.RunUntil(s => s.MoltenIron > 0f, CampaignSeconds) > 0,
      "a coke-rich cold furnace should render pig within a campaign"
    );

    Assert.Equal(FurnaceState.Melting, scene.State);
    Assert.True(
      scene.Temp > IiexValues.BfIronMeltingPoint,
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

    Assert.True(
      scene.IronCanalUnits > 0,
      "pig should have reached the canal start"
    );
    Assert.True(
      scene.SlagCanalUnits > 0,
      "slag should have reached its own canal start"
    );
    // The blast furnace makes pig iron, not plain iron - plain iron is a Bessemer over-blow product.
    Assert.Contains("pigiron", scene.IronCanalMetal);
    Assert.DoesNotContain("ingot-iron", scene.IronCanalMetal);
    Assert.Contains("slag", scene.SlagCanalMetal);
  }

  #endregion

  #region Firing

  /// <summary>
  /// Ignition has no quantity threshold: a shaft derives its state from what is in front of its tuyeres - a
  /// complete raceway course with carbon in it. What keeps a full shaft dark is having nothing that can
  /// burn there. The scene's burden still carries a 30 % coke stamp, so a furnace reading its carbon off
  /// the stamp rather than off the bands would light here.
  /// </summary>
  [Fact]
  public void A_full_shaft_with_no_coke_at_its_raceway_never_lights() {
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

    Assert.False(
      everLit,
      "a shaft with no carbon in it should never catch at all"
    );
    Assert.Equal(FurnaceState.Idle, noFuel.State);

    // The control: the same burden at the same grade, laid as real rounds so the coke is its own bands,
    // catches on its first tick. Without it this passes on a furnace that could never light at all.
    var lit = ColdBlastFurnaceScenes.Complete();
    Assert.True(lit.CokeUnits > 0);
    lit.RunLive(2);
    Assert.Equal(FurnaceState.Firing, lit.State);
  }

  /// <summary>
  /// The ignition gate is positional, not a quantity (<c>docs/design/layered-charge.md</c>, section "The
  /// ignition sequence, fully specified"): a charge pile must stand on every column. This scene piles three
  /// times the fire threshold into one, leaving eight of the nine tuyeres facing empty air. The requirement
  /// is the furnace's own column count, so a redrawn layout updates it and there is no constant to tune.
  /// </summary>
  [Fact]
  public void A_shaft_piled_into_ONE_column_never_lights_however_much_is_in_it() {
    var tower = ColdBlastFurnaceScenes.OneTallColumn();

    Assert.True(tower.Core.StructureComplete);
    // The premise: the shaft is genuinely well charged, so failing to light cannot be read as an empty
    // furnace. Stated against the shaft's own capacity.
    Assert.True(
      tower.ColumnUnits > tower.ShaftCapacityUnits / 4,
      $"the scene must stand a substantial charge, or it proves nothing; "
        + $"{tower.ColumnUnits} of {tower.ShaftCapacityUnits}"
    );

    // Watched rather than sampled: a furnace that lit and went out would read Idle at the end anyway.
    bool everLit = false;
    tower.RunLive(120, s => everLit |= s.State != FurnaceState.Idle);

    Assert.False(
      everLit,
      "a shaft with eight empty columns should never catch"
    );

    // The control: the same total, spread into a complete course, lights at once.
    var course = ColdBlastFurnaceScenes.Complete(charge: tower.ColumnUnits);
    course.RunLive(2);
    Assert.Equal(FurnaceState.Firing, course.State);
  }

  /// <summary>
  /// A stopped blower throttles the fire rather than snuffing it. Air is the reagent, so the blast sets the
  /// rate: cut it and the furnace falls back to natural draught at <c>BfNaturalDraughtFactor</c> of the
  /// blown rate, settles below iron's melt line, renders nothing and goes on eating its campaign. See
  /// <c>docs/design/layered-charge.md</c>, section "What sets the rate: the blast".
  /// </summary>
  [Fact]
  public void Cutting_the_blast_takes_a_melting_furnace_back_out_of_melting() {
    var scene = ColdBlastFurnaceScenes.Complete();
    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Melting, CampaignSeconds) > 0,
      "the furnace should have reached Melting on blast"
    );

    // The control comes first, on the same furnace: with the blowers still on it holds a long window
    // without ever leaving Melting.
    const int window = 120;
    scene.RunLive(window);
    Assert.Equal(FurnaceState.Melting, scene.State);
    Assert.False(scene.AirStarved);

    // Now cut the blast. The air factor collapses to natural draught and the process temperature falls
    // far below the melt line.
    int coke = scene.CokeUnits;
    scene.CutBlast();

    // Watched second by second: "not Melting at the end" would also hold for a furnace that dipped out and
    // melted again. The watch arms once it has left Melting and banks the pool at that instant - the tuyere
    // mains still hold the last second of blast, so the first tick or two after the cut legitimately melt.
    bool left = false;
    bool meltedAgain = false;
    float bankedAtCutoff = 0f;
    float grewAfter = 0f;
    scene.RunLive(
      window,
      s => {
        if (s.State != FurnaceState.Melting) {
          if (!left)
            bankedAtCutoff = s.MoltenIron;
          left = true;
          grewAfter = Math.Max(grewAfter, s.MoltenIron - bankedAtCutoff);
        } else if (left)
          meltedAgain = true;
      }
    );

    Assert.True(left, "an unblown furnace should fall out of Melting");
    Assert.False(meltedAgain, "and it should not melt again");
    Assert.NotEqual(FurnaceState.Melting, scene.State);
    Assert.True(
      scene.Temp < IiexValues.BfIronMeltingPoint,
      $"natural draught should sit below the melt line; was {scene.Temp} C"
    );
    Assert.True(
      bankedAtCutoff > 0f,
      "it should have banked real pig before the blast went"
    );
    Assert.Equal(0f, grewAfter, 3); // and rendered nothing at all once it had fallen out

    // Still alight, and still spending. A frozen furnace and an extinguished one look identical from
    // outside, so consumption is the only observable that separates "throttled" from "out".
    Assert.NotEqual(FurnaceState.Idle, scene.State);
    Assert.True(
      scene.CokeUnits < coke,
      $"an unblown furnace should still be burning its carbon; {scene.CokeUnits} vs {coke}"
    );
  }

  #endregion

  #region Melting

  /// <summary>
  /// The crucible and the burden no longer share a cell, and the two writers no longer contend. While the
  /// furnace melts, the hearth-metal blocks it stands its bath in occupy the whole y=1 course and the
  /// burden stands from y=2 up; nothing of the shaft is drawn into the crucible and nothing of the bath is
  /// drawn over by <c>SyncChargeBlocks</c>.
  /// <para>
  /// Run live rather than asserted off the layout, because the disjointness of the two roles is a static
  /// fact that says nothing about which cells the two walks actually write to at runtime.
  /// </para>
  /// </summary>
  [Fact]
  public void A_melting_furnace_stands_its_bath_under_its_burden_not_in_it() {
    var scene = ColdBlastFurnaceScenes.Complete();
    Assert.True(
      scene.RunUntil(s => s.MoltenIron > 0f, CampaignSeconds) > 0,
      "the furnace should have melted something"
    );

    // The bath: the whole crucible course, and none of it a charge pile.
    foreach (int x in new[] { -1, 0, 1 }) {
      Assert.Equal(
        "iiex:hearthmetal-pigiron",
        scene.BlockAtLocal(x, 1, 0).Code?.ToString()
      );
      Assert.Null(scene.PileAtLocal(x, 1, 0));
    }

    // The burden: still standing, and its lowest block is the shaft's own floor rather than the crucible.
    Assert.True(
      scene.ColumnUnits > 0,
      "a melting furnace should still have burden standing in its shaft"
    );
    Assert.NotNull(scene.PileAtLocal(0, 2, 0));

    // The column reaches no further down than that. Asked of the machine rather than of the drawing: a
    // descent that walked into the crucible would answer a column here.
    Assert.Null(
      scene.Core.ChargeColumnAt(scene.Structure.Cell(0, 1, 0), out int index)
    );
    Assert.Equal(-1, index);
  }

  /// <summary>
  /// The cold/hot trade: the <c>standard</c> grade (20 % coke) is the heat balance's calibration point and
  /// on cold blast settles at ~1420 C against a 1482 C melt line, reporting <c>bf-info-heatstall</c>. A
  /// burden that never melts makes no room for the column to descend, so the fire eats what carbon it can
  /// reach and chills. See <c>docs/design/layered-charge.md</c>, section "The chill".
  /// </summary>
  [Fact]
  public void A_standard_burden_lights_holds_and_never_melts() {
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
      s => {
        hottest = Math.Max(hottest, s.Temp);
        everMelted |= s.State == FurnaceState.Melting;
      }
    );

    Assert.False(
      everMelted,
      "a standard burden must never reach Melting on cold blast"
    );
    Assert.True(
      hottest < IiexValues.BfIronMeltingPoint,
      $"a standard burden should hold below the melt line, peaked at {hottest} C"
    );
    Assert.Equal(0f, stalled.MoltenIron, 3);
    Assert.Equal(0f, stalled.MoltenSlag, 3);

    // ...and it is a chill, not a spent campaign: the fire stopped with burden still standing in the shaft.
    // A furnace that had simply burnt its charge would end empty.
    Assert.True(lived > 0, "a chilled furnace should eventually go out");
    Assert.True(
      stalled.ColumnUnits > 0,
      "the chilled column should still be standing there, which is what makes it salvageable"
    );

    // The player is told why. Read on a fresh scene still in the stall, because the one above has gone out.
    var naming = ColdBlastFurnaceScenes.StandardBurden();
    Assert.True(naming.RunUntil(s => s.State == FurnaceState.Firing, 30) > 0);
    naming.RunLive(30);
    Assert.Equal(naming.Heat.TProcess, naming.Temp, 1); // arrived at the balance, not still climbing
    Assert.Contains(IiexLang.BfInfoHeatstall, naming.CoreInfo());

    // The control: the same rig charged with coke-rich burden crosses into Melting inside the same window,
    // so what holds the stalled furnace below the line is the burden and nothing about the scene.
    var melting = ColdBlastFurnaceScenes.Complete();
    Assert.True(
      melting.RunUntil(s => s.State == FurnaceState.Melting, CampaignSeconds)
        > 0
    );
    Assert.Equal(FurnaceState.Melting, melting.State);
  }

  /// <summary>
  /// Campaign length is not bounded by one shaft-full: a furnace is charged continuously while it runs.
  /// Two identical furnaces, blown the same way; one is recharged when its carbon is about half gone, so
  /// the divergence at the end is the recharge and nothing else. A lit shaft runs until its carbon is gone,
  /// so carbon is what the scene halves, what the recharge restores and what the control dies of.
  /// </summary>
  [Fact]
  public void Refilling_a_firing_furnace_extends_the_campaign() {
    var refilled = ColdBlastFurnaceScenes.Complete();
    var control = ColdBlastFurnaceScenes.Complete();

    Assert.True(
      refilled.RunUntil(s => s.State == FurnaceState.Melting, CampaignSeconds)
        > 0
    );
    Assert.True(
      control.RunUntil(s => s.State == FurnaceState.Melting, CampaignSeconds)
        > 0
    );

    int charged = refilled.CokeUnits;
    int half = charged / 2;
    Assert.True(
      half > 0,
      "the scene must carry real carbon, or there is nothing to halve"
    );

    // Burn until roughly half the carbon is gone.
    Assert.True(
      refilled.RunUntil(s => s.CokeUnits <= half, CampaignSeconds) > 0
    );
    Assert.True(
      control.RunUntil(s => s.CokeUnits <= half, CampaignSeconds) > 0
    );
    Assert.Equal(FurnaceState.Melting, refilled.State);
    Assert.Equal(FurnaceState.Melting, control.State);

    // The one difference: the player charges again.
    refilled.Recharge(refilled.ShaftCapacityUnits);

    // The recharge has to be visible to the furnace, not merely present in the world: a machine that
    // latched its charge at ignition would coast through the window below on a stale number.
    refilled.RunLive(1);
    Assert.True(
      refilled.CokeUnits > half,
      $"the recharge should show up in the carbon the furnace can reach; still {refilled.CokeUnits}"
    );

    // The window is the control's own life, not a constant, so "still melting" is measured against the
    // thing it is compared with and cannot go stale when a rate is retuned.
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
    Assert.True(
      refilled.MoltenIron > 0f,
      "and it kept making pig the whole way"
    );
  }

  [Fact]
  public void A_tuyere_fitted_backwards_never_completes_the_structure() {
    // The one mistake nothing in the engine catches: a node whose connector points into the structure it
    // is embedded in. ComputeValidOrientations relaxes to connectsAny for a single-axis shape and the
    // solid-brick branch re-adds the current letter as required, so a tuyere already facing south stays
    // in its own valid set and the neighbour scan never turns it round.
    var wrong = ColdBlastFurnaceScenes.BackwardsTuyere();

    Assert.False(
      wrong.Core.StructureComplete,
      "a tuyere blowing into the hearth must not complete the furnace"
    );
    // ...and it is the tuyere cell that is short, not some unrelated one.
    Assert.Equal(1, wrong.Structure.Missing);
    Assert.Contains(
      wrong.Structure.Cell(0, 2, -2).ToString(),
      wrong.Structure.MissingReport
    );
  }

  #endregion

  #region Tapping

  [Fact]
  public void A_tap_installed_backwards_no_longer_completes_the_structure() {
    // The tap cell's legend pins the facing (`iiex:furnace-irontap-west`), so a backwards tap does not
    // satisfy the cell and the structure stays incomplete. A wildcarded facing would defer the failure to
    // the first tap, where an east-facing tap in the east wall aims its spout into the furnace's own base.
    var wrong = ColdBlastFurnaceScenes.BackwardsIronTap();
    Assert.False(
      wrong.Core.StructureComplete,
      "a backwards tap must no longer satisfy the facing-pinned tap cell"
    );

    // ...and it is the tap that is unsatisfied, not some unrelated cell: exactly one cell short, and the
    // breakdown names the iron notch's own position.
    Assert.Equal(1, wrong.Structure.Missing);
    Assert.Contains(
      wrong.IronTap.Pos.ToString(),
      wrong.Structure.MissingReport
    );

    // The control: the identical scene with the tap the right way round completes and opens.
    var right = ColdBlastFurnaceScenes.Complete();
    Assert.True(right.Core.StructureComplete);
    Assert.True(right.OpenIronTap());
    Assert.True(right.IronTap.IsPouring);
  }

  [Fact]
  public void A_full_canal_backs_the_pour_up_and_the_pool_stops_draining() {
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

    // With the runout brim-full the pour is refused. The pool must be held, not silently swallowed: the
    // furnace only gives up metal the canal actually accepted.
    float pooled = scene.MoltenIron;
    scene.RunLive(20);

    Assert.Equal(scene.IronCanalCapacity, scene.IronCanalUnits);
    Assert.True(
      scene.MoltenIron > pooled,
      $"a backed-up tap must not eat the pool; {scene.MoltenIron} vs {pooled}"
    );

    // The control: clear the runout and the very next tick pours again, so the stall was the full canal.
    scene.IronCanal.DrainMetal(scene.IronCanalCapacity);
    Assert.Equal(0, scene.IronCanalUnits);
    scene.RunLive(1);
    Assert.True(
      scene.IronCanalUnits > 0,
      "with the runout clear the same tap should pour again"
    );
  }

  /// <summary>
  /// Pins the tap's per-tick rate rather than its direction. One tick on a freshly opened tap over an empty
  /// canal must drain <c>Ceiling(min(TapDrainPerTick, MoltenIron) * TapIronStackFactor)</c> - 31 at the
  /// shipped 50 and 0.6f, not 30, since <c>Math.Ceiling</c> rounds up.
  /// </summary>
  [Fact]
  public void A_freshly_opened_tap_drains_exactly_one_ticks_worth_not_a_hidden_ceiling() {
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
  /// A lit shaft burns <c>BfRacewayCarbonPerTuyerePerSecond x tuyeres</c> of carbon a second at full blast
  /// and runs until that carbon is gone, so campaign length is charge over blast with no timer in it. The
  /// tuyere count comes from the furnace's own scanned list, not a literal. Measured over a window rather
  /// than a tick, since a fuel band burns in whole units and a single second usually spends none.
  /// </summary>
  [Fact]
  public void The_blast_meters_the_carbon_at_the_rate_per_tuyere_it_advertises() {
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
      IiexValues.BfRacewayCarbonPerTuyerePerSecond * scene.TuyereCount * window;

    // ±5 %: the spend is carried between ticks in whole units, so a window can end mid-unit.
    Assert.True(
      Math.Abs(burned - expected) <= expected * 0.05f,
      $"a furnace on full blast should burn {expected} u of carbon in {window} s; it burned {burned}"
    );
  }

  #endregion

  #region Breach

  /// <summary>
  /// A furnace that loses its walls keeps burning: opened to the air it draws harder and burns its coke off
  /// at open-air temperature. A furnace whose blast fails is the one that dies, because a packed shaft has
  /// almost no natural draught of its own. Breach and choke are opposite failure modes, so
  /// <c>OnStructureLost</c> must not take the choke path through <c>Extinguish()</c>.
  /// </summary>
  [Fact]
  public void A_breached_lit_furnace_keeps_burning_its_coke_and_makes_nothing() {
    var scene = ColdBlastFurnaceScenes.Complete();
    Assert.True(
      scene.RunUntil(s => s.MoltenIron > 0f, CampaignSeconds) > 0,
      "the furnace should be melting before its wall comes out"
    );

    scene.Breach();
    scene.RunLive(5);
    Assert.False(
      scene.Core.StructureComplete,
      "the breach should be visible to the furnace"
    );

    int coke = scene.ColumnUnits;
    float made = scene.MoltenIron;
    scene.RunLive(180);

    // Still alight. A frozen furnace and an extinguished one look identical from the outside, so
    // consumption is the only observable that tells burning from both.
    Assert.NotEqual(FurnaceState.Idle, scene.State);
    Assert.True(
      scene.ColumnUnits < coke,
      $"a breached furnace must still be burning its charge; {scene.ColumnUnits} vs {coke}"
    );

    // ...and running on natural draught alone, so it is well below the melt line and renders nothing.
    Assert.True(
      scene.Temp < IiexValues.BfIronMeltingPoint,
      $"a breach is open to the air, so it should fall to natural draught; was {scene.Temp} C"
    );
    Assert.Equal(made, scene.MoltenIron, 3);
  }

  [Fact]
  public void A_breached_IDLE_furnace_stays_idle_and_cannot_be_lit_through_the_hole() {
    // The clause that keeps breach and choke distinguishable: without it a wall could be knocked out of a
    // cold furnace and the furnace lit through the gap. Staged on the permanently-incomplete scene rather
    // than by breaching a complete one, because the structure monitor only re-runs every few seconds.
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
  public void The_pool_freezes_onto_the_hearth_rather_than_vanishing() {
    var scene = ColdBlastFurnaceScenes.Complete();
    Assert.True(
      scene.RunUntil(s => s.MoltenIron > 0f, CampaignSeconds) > 0,
      "the furnace should have made pig before it is put out"
    );

    // Let the campaign run itself out, remembering the pool as of the last tick it was still lit - that is
    // the metal the freeze has to account for. A campaign ends when the carbon runs out, so the scene waits
    // for it with the taps shut and the pool still standing in the hearth when the fire dies.
    float pooled = 0f;
    Assert.True(
      scene.RunUntil(
        s => s.State == FurnaceState.Idle,
        BurnoutSeconds,
        s => {
          if (s.State != FurnaceState.Idle)
            pooled = s.MoltenIron;
        }
      ) > 0,
      "a furnace should go out when its carbon is gone"
    );
    Assert.True(pooled > 0f);

    // The pool froze across the crucible course, under the shaft, as solid iron - not slag, not nothing.
    // Three cells, because the crucible runs the full width of the hearth course.
    var crucible = new[] { (-1, 1, 0), (0, 1, 0), (1, 1, 0) };
    foreach (var (x, y, z) in crucible)
      Assert.Equal(
        "iiex:hearthmetal-pigiron",
        scene.BlockAtLocal(x, y, z).Code?.ToString()
      );

    // ...and it carries the metal rather than being a decorative block over a deleted pool. The pool is
    // the cells' own contents now, so the conservation claim is exact: every unit that was liquid when
    // the furnace died is still standing on the crucible floor. Under the old stamp-at-shutdown model
    // this could only be asserted through a lossy units-per-nugget conversion.
    Assert.Equal(pooled, scene.MoltenIron, 3);
  }

  [Fact]
  public void Burnt_out_charge_comes_back_as_salvage_richer_in_coke_at_the_top() {
    // The column is rewritten in place as spent charge: ore and flux survive verbatim and only the coke is
    // burned out, by height, because the blast burned hardest at the tuyeres. Run on the standard burden
    // because it never melts: with no pool there is no freeze, so both piles survive to be read.
    var scene = ColdBlastFurnaceScenes.StandardBurden();
    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Firing, 30) > 0,
      "the furnace should have lit"
    );

    // Put out by the chill, with the blowers left running: that leaves the whole shaft standing to be
    // salvaged, which is the situation burn-out exists for. Cutting the blast would only throttle it.
    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Idle, BurnoutSeconds) > 0,
      "a chilled furnace should go out"
    );

    // Both piles are still charge piles - burned out, not destroyed and not slagged. Stated as "is still a
    // charge pile" rather than "is not slag", which an empty cell would also satisfy. The block is
    // `iiex:furnace-chargepile`: the shaft's charge is the furnace's own, not a vanilla container.
    Assert.Equal(
      "iiex:furnace-chargepile",
      scene.BlockAtLocal(0, 2, 0).Code?.ToString()
    );
    Assert.Equal(
      "iiex:furnace-chargepile",
      scene.BlockAtLocal(0, 5, 0).Code?.ToString()
    );
    Assert.NotNull(scene.PileAtLocal(0, 2, 0));
    Assert.NotNull(scene.PileAtLocal(0, 5, 0));

    BurdenMix charged = scene.ChargedMix;
    BurdenMix bottom = scene.SalvageAtLocal(0, 2, 0); // level with the tuyeres
    BurdenMix top = scene.SalvageAtLocal(0, 5, 0); // top of the charge column

    // The salvage is real: ore and flux come back untouched at both ends of the column.
    Assert.Equal(charged.Iron, bottom.Iron, 3);
    Assert.Equal(charged.Iron, top.Iron, 3);
    Assert.Equal(charged.Flux, bottom.Flux, 3);
    Assert.Equal(charged.Flux, top.Flux, 3);

    // The gradient. Asserted as a relationship plus a tie to the config keys rather than to today's
    // numbers, so retuning the retention pair moves the test with it.
    Assert.True(
      top.Fuel > bottom.Fuel,
      $"the top of the shaft should keep more coke than the bottom; "
        + $"top {top.Fuel} vs bottom {bottom.Fuel}"
    );
    Assert.True(
      top.Fuel < charged.Fuel,
      "even the top of the column loses coke"
    );
    Assert.Equal(
      charged.Fuel * IiexValues.BfBurnoutFuelRetainedTop,
      top.Fuel,
      4
    );
    Assert.Equal(
      charged.Fuel * IiexValues.BfBurnoutFuelRetainedBottom,
      bottom.Fuel,
      4
    );
  }

  /// <summary>
  /// Burn-out is applied once, so the salvage does not depend on how fast the player came back for it.
  /// <c>Extinguish</c> leaves the shaft full, so <c>BlastmixPiles.ReleaseFromFurnace</c>
  /// (<c>CoalPileBlastmixPatches.cs</c>) has to call <c>pile.Extinguish()</c> on a released pile that is
  /// still burning: otherwise an already-lit shaft re-ignites on the next tick and each
  /// <c>Extinguish → ExtinguishResidue → BurnOutCharge</c> cycle multiplies the remaining coke by
  /// <c>retained</c> again, decaying the burden as <c>fuel × retained^n</c>. Runs on the standard burden: a
  /// furnace that made a pool freezes it, which breaks the structure and stops the tick, while one that
  /// never melted can oscillate indefinitely.
  /// </summary>
  [Fact]
  public void A_furnace_left_alone_after_it_goes_out_keeps_burning_its_own_salvage() {
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
    // And the coke bands are untouched too, which the stamp alone cannot say. Burn-out zeroes the carbon at
    // the raceway (BfBurnoutFuelRetainedBottom is 0) so a dead furnace cannot re-derive itself alight; this
    // asserts the gate holds for the whole idle stretch, not only on the tick it was applied.
    Assert.Equal(cokeLeft, scene.CokeUnits);
  }

  #endregion

  #region Charcoal salvage

  // Everything above this line lays coke, and every case above passes identically against a furnace whose
  // fuel test is the literal `game:coke`. Charcoal is priced at half the carbon a unit (`CarbonPerUnit`),
  // and the cases here are the ones that cannot pass on a coke-only machine.

  /// <summary>
  /// The geometric twin of <see cref="ColdBlastFurnaceScenes.StandardBurden"/>: a full shaft of rounds at
  /// <c>BfReferenceFuelFrac</c> by volume laid in charcoal, so the two compare band for band. 20 % by
  /// volume is 10 % by carbon, under the ~23.9 % cold break-even, so the shaft lights and chills with its
  /// column left standing. <c>charge: -1</c> lays nothing; the constructor would otherwise charge coke.
  /// </summary>
  private static ColdBlastFurnaceRig CharcoalShaft() =>
    ColdBlastFurnaceScenes
      .Complete(charge: -1, fuelFrac: IiexValues.BfReferenceFuelFrac)
      .ChargeWithFuel(ColdBlastFurnaceRig.CharcoalCode);

  /// <summary>
  /// A dead furnace cannot re-derive itself alight, stated through the second fuel. A shaft has no state
  /// machine: what stops the relight is that burn-out leaves no carbon at its own raceway
  /// (<c>BfBurnoutFuelRetainedBottom</c> is 0). <c>BurnOutCharge</c> recognises a fuel band through
  /// <c>IsFuelCode</c>; a coke literal would send a charcoal band down the legacy-burden branch, which
  /// keeps every unit and leaves the raceway readable as carbon-bearing.
  /// <para>
  /// Watched second by second rather than sampled at the end: an oscillating furnace spends most of its
  /// time <c>Idle</c>, so what separates the two is that nothing was consumed.
  /// </para>
  /// </summary>
  [Fact]
  public void A_charcoal_furnace_that_went_out_cannot_relight_off_its_own_salvage() {
    var scene = CharcoalShaft();

    // The scene's own premises, so it cannot pass by holding no fuel or by holding coke after all.
    Assert.True(
      scene.FuelBandUnits > 0,
      "the shaft must actually hold fuel bands"
    );
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
    // ...and none of it is left in front of a tuyere, on any column - which is the gate.
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

    Assert.False(
      everLit,
      "a dead charcoal furnace must never re-derive itself alight"
    );
    Assert.Equal(FurnaceState.Idle, scene.State);
    Assert.Equal(fuel, scene.FuelBandUnits); // the bands are untouched, which the stamp alone cannot say
    Assert.Equal(carbon, scene.CarbonUnits, 3);
  }

  /// <summary>
  /// The charcoal twin of <c>Burnt_out_charge_comes_back_as_salvage_richer_in_coke_at_the_top</c>: a dead
  /// furnace owes the player its ore and flux verbatim on the same height gradient, whatever was burned in
  /// it. Asserted against the two config keys rather than numbers. <c>SalvageAtLocal</c> steps over fuel
  /// bands through the production predicate, which is why this reads burden at all on charcoal.
  /// </summary>
  [Fact]
  public void Charcoal_salvage_keeps_its_ore_and_flux_on_the_same_gradient_as_cokes() {
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
    Assert.NotNull(scene.PileAtLocal(0, 2, 0));
    Assert.NotNull(scene.PileAtLocal(0, 5, 0));

    BurdenMix charged = scene.ChargedMix;
    BurdenMix bottom = scene.SalvageAtLocal(0, 2, 0); // level with the tuyeres
    BurdenMix top = scene.SalvageAtLocal(0, 5, 0); // top of the charge column

    Assert.Equal(charged.Iron, bottom.Iron, 3);
    Assert.Equal(charged.Iron, top.Iron, 3);
    Assert.Equal(charged.Flux, bottom.Flux, 3);
    Assert.Equal(charged.Flux, top.Flux, 3);

    Assert.Equal(
      charged.Fuel * IiexValues.BfBurnoutFuelRetainedTop,
      top.Fuel,
      4
    );
    Assert.Equal(
      charged.Fuel * IiexValues.BfBurnoutFuelRetainedBottom,
      bottom.Fuel,
      4
    );
  }

  #endregion

  #region Charcoal is a trade, not an alias

  // The cases above prove charcoal survives the shaft's machinery; these prove it is priced there. Each
  // goes red on aliasing charcoal to coke and on ignoring it. The ratio is read off the registry, never
  // off `CarbonPerUnit` - see `CarbonOf` - since `CarbonPerUnit` is the thing under test.

  /// <summary>
  /// The carbon one charge unit of <paramref name="code"/> carries, in coke units: the <c>fuel</c>-role
  /// value over <see cref="IiexValues.BfFuelCarbonReference"/>, recomputed from the registry so a defect in
  /// <c>CarbonPerUnit</c> does not move the expectation.
  /// </summary>
  private static float CarbonOf(string code) =>
    MaterialRoleRegistry.ValueOf(Roles.Fuel, new AssetLocation(code))
    / IiexValues.BfFuelCarbonReference;

  /// <summary>
  /// A complete, blown cold furnace charged to the brim with real rounds laid in <paramref name="fuel"/> at
  /// <paramref name="fuelFrac"/> by volume. <c>charge: -1</c> lays nothing: the constructor would otherwise
  /// charge coke, and a shaft holding both fuels would prove neither.
  /// </summary>
  private static ColdBlastFurnaceRig Rounds(
    string fuel,
    float fuelFrac = 0.30f
  ) =>
    ColdBlastFurnaceScenes
      .Complete(charge: -1, fuelFrac: fuelFrac)
      .ChargeWithFuel(fuel);

  /// <summary>
  /// The blast meters carbon, so two fuels of different worth burn away at different volumes. Two furnaces
  /// identical to the cell, differing only in which fuel the rounds are laid with: over the same window
  /// they spend the same carbon, and the charcoal one loses fuel-band volume in the registry's ratio. Equal
  /// carbon says the blast is still the only throttle; the volume ratio says charcoal is priced at all.
  /// <para>
  /// Measured over a window rather than a tick, with both furnaces asserted still alight at the end: a fuel
  /// band burns in whole units, and a charcoal shaft at this fraction chills a few minutes in.
  /// </para>
  /// </summary>
  [Fact]
  public void Charcoal_burns_at_its_own_carbon_value_not_cokes() {
    var coke = Rounds(ColdBlastFurnaceRig.CokeCode);
    var charcoal = Rounds(ColdBlastFurnaceRig.CharcoalCode);

    float ratio =
      CarbonOf(ColdBlastFurnaceRig.CokeCode)
      / CarbonOf(ColdBlastFurnaceRig.CharcoalCode);
    Assert.True(
      ratio > 1f,
      "the registry must price coke above charcoal, or this claims nothing"
    );

    // The premise: the same volume of fuel stands in both shafts, carrying different carbon. Without it the
    // case could pass on two furnaces charged differently.
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
    Assert.True(
      cokeBands > 0,
      "the coke furnace should have burned something in the window"
    );

    // Same carbon: the blast is the throttle and it does not know which fuel it is burning.
    Assert.True(
      Math.Abs(cokeCarbon - charcoalCarbon) <= cokeCarbon * 0.05f,
      $"the blast meters carbon, so both should spend the same; coke {cokeCarbon} vs charcoal {charcoalCarbon}"
    );

    // Different volume, in the registry's own ratio - the assertion aliasing fails.
    float expectedBands = cokeBands * ratio;
    Assert.True(
      Math.Abs(charcoalBands - expectedBands) <= expectedBands * 0.05f,
      $"charcoal should burn {ratio}x the band volume for the same carbon; "
        + $"expected ~{expectedBands} u, burned {charcoalBands} against coke's {cokeBands}"
    );
  }

  /// <summary>
  /// Same geometry, same 30 % fuel course by volume, same cold blast, differing only in the fuel: the
  /// raceway reads ~0.36 coke fraction on a coke round and ~0.22 on a charcoal one, so the coke twin clears
  /// iron's melt line and the charcoal twin settles roughly 200 °C under it. Asserted on each furnace's
  /// peak over its run, since a chilled furnace stops recomputing its balance.
  /// </summary>
  [Fact]
  public void A_charcoal_charge_at_the_same_band_fraction_runs_COOLER_than_a_coke_one() {
    var coke = Rounds(ColdBlastFurnaceRig.CokeCode);
    var charcoal = Rounds(ColdBlastFurnaceRig.CharcoalCode);

    float cokeFactor = 0f;
    float cokeTemp = 0f;
    bool cokeMelted = false;
    int lit = coke.RunUntil(
      s => s.State == FurnaceState.Melting,
      CampaignSeconds,
      s => {
        cokeFactor = Math.Max(cokeFactor, s.Heat.FuelFactor);
        cokeTemp = Math.Max(cokeTemp, s.Temp);
        cokeMelted |= s.State == FurnaceState.Melting;
      }
    );
    Assert.True(
      lit > 0,
      "the coke twin must reach Melting, or there is nothing to be cooler than"
    );

    // The charcoal twin gets the whole campaign window - strictly more time than the coke one needed - so
    // "it never got there" cannot be an artefact of a shorter run.
    float charcoalFactor = 0f;
    float charcoalTemp = 0f;
    bool charcoalMelted = false;
    charcoal.RunLive(
      CampaignSeconds,
      s => {
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
      cokeTemp > IiexValues.BfIronMeltingPoint,
      $"the coke twin should cross the melt line; peaked at {cokeTemp} C"
    );
    Assert.True(
      charcoalTemp < IiexValues.BfIronMeltingPoint,
      $"the charcoal twin should never reach it; peaked at {charcoalTemp} C"
    );
    Assert.True(cokeMelted);
    Assert.False(
      charcoalMelted,
      "a 30 % charcoal course must not melt on cold blast"
    );
    Assert.Equal(0f, charcoal.MoltenIron, 3);
  }

  /// <summary>
  /// The mirror of <c>A_full_shaft_with_no_coke_at_its_raceway_never_lights</c>: a shaft needs carbon in
  /// front of its tuyeres, not coke, so a course laid entirely in charcoal catches on its first tick. It
  /// carries its own arithmetic premise - fuel volume times charcoal's registry weight - so it cannot pass
  /// on a machine that lit thinking the bands were coke.
  /// </summary>
  [Fact]
  public void A_shaft_whose_only_raceway_fuel_is_CHARCOAL_still_lights() {
    var scene = Rounds(ColdBlastFurnaceRig.CharcoalCode);
    Assert.True(scene.Core.StructureComplete);

    Assert.True(
      scene.FuelBandUnits > 0,
      "the shaft must actually hold fuel bands"
    );
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
  /// A shaft charged in rounds of raw anthracite - a vanilla item that is neither burden nor fuel, since
  /// raw coal crushes to dust under a burden column (<c>docs/design/processes/coking.md</c>) - never
  /// catches, so <c>RacewayIsLightable</c> cannot degrade to "anything that is not burden". Its control is
  /// the charcoal shaft, not the coke one, so the pair asks about the role alone.
  /// </summary>
  [Fact]
  public void A_raceway_of_a_non_fuel_that_is_not_burden_never_lights() {
    const string anthracite = "game:ore-anthracite";
    Assert.False(
      BlockEntityFurnaceCore.IsFuelCode(anthracite),
      "raw coal must not carry the fuel role, or this scene is testing nothing"
    );
    Assert.False(Burden.IsCode(anthracite), "...and it is not burden either");

    var scene = ColdBlastFurnaceScenes
      .Complete(charge: -1)
      .ChargeWithFuel(anthracite);
    Assert.True(scene.Core.StructureComplete);
    Assert.True(scene.ColumnUnits > 0, "the shaft must actually be charged");
    Assert.Equal(0, scene.FuelBandUnits); // nothing in it is fuel, on the production predicate
    Assert.Equal(0f, scene.CarbonUnits, 3);

    // Watched rather than sampled at the end: a furnace that lit and went out would read Idle anyway.
    bool everLit = false;
    scene.RunLive(120, s => everLit |= s.State != FurnaceState.Idle);

    Assert.False(
      everLit,
      "a raceway of a role-less material should never catch"
    );
    Assert.Equal(FurnaceState.Idle, scene.State);

    // The control: the identical scene laid with charcoal - also not coke - catches on its first tick, so
    // what the shaft reads is the role and not a literal.
    var lit = Rounds(ColdBlastFurnaceRig.CharcoalCode);
    lit.RunLive(2);
    Assert.Equal(FurnaceState.Firing, lit.State);
  }

  /// <summary>
  /// The positive half: charcoal buys a working campaign at a richer course. Cold, full and blown the
  /// balance is <c>T = 520 + 900·f</c>, so the 1482 °C melt line needs <c>f ≥ 1.069</c> - a raceway coke
  /// fraction of 0.239, where <c>f = 1 + 0.35·(F-0.2)/0.2</c>. A round of 32 units at volume fraction φ is
  /// <c>u = ⌊32φ⌋</c> of fuel over <c>32-u</c> of burden, so charcoal reads
  /// <c>F = 0.5u / (0.5u + (1-φ)(32-u))</c>: 0.218 at φ = 0.30, break-even near 0.32, and 0.333 at
  /// φ = 0.40 for <c>f = 1.233</c> and ~1630 °C. The furnace's own <c>Heat.TProcess</c> is asserted too, so
  /// retuning any of those five keys fails here rather than downstream.
  /// </summary>
  [Fact]
  public void Charcoal_still_makes_a_viable_campaign_on_a_richer_course() {
    // 0.40 by volume - worked out above, and roughly eight points over the charcoal break-even.
    var scene = Rounds(ColdBlastFurnaceRig.CharcoalCode, fuelFrac: 0.40f);

    Assert.True(
      scene.RunUntil(s => s.MoltenIron > 0f, CampaignSeconds) > 0,
      "a charcoal furnace on a rich enough course should render pig within a campaign"
    );

    Assert.Equal(FurnaceState.Melting, scene.State);
    Assert.True(
      scene.Heat.TProcess > IiexValues.BfIronMeltingPoint,
      $"the balance itself should clear the melt line; T_process was {scene.Heat.TProcess} C"
    );
    Assert.True(scene.MoltenIron > 0f, "it should have made pig");
    Assert.True(scene.MoltenSlag > 0f, "and slag alongside it");
    Assert.Equal(0f, scene.Heat.PreheatGain, 1); // still cold blast - the charge did all of it

    // And it melted on charcoal, priced as charcoal: without this the case passes on a furnace that had
    // aliased the fuel to coke.
    Assert.True(
      scene.CarbonUnits < scene.FuelBandUnits,
      $"the fuel standing in this shaft must be worth less than coke per unit; "
        + $"{scene.CarbonUnits} carbon in {scene.FuelBandUnits} u of bands"
    );

    // It really does drain, so "viable" means a campaign and not a puddle.
    Assert.True(scene.OpenIronTap(), "the iron tap should open over its canal");
    scene.RunLive(30);
    Assert.True(
      scene.IronCanalUnits > 0,
      "pig should have reached the canal start"
    );
    Assert.Contains("pigiron", scene.IronCanalMetal);
  }

  #endregion
}
