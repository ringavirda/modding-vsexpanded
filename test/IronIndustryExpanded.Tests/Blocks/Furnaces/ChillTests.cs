using System;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The chill: under-coked burden reaches a raceway too cold to melt, so it does not leave, no space
/// appears beneath the column and nothing above it descends. Hanging is a per-column property derived
/// from the counter-current model; see <c>docs/design/layered-charge.md</c>, section "The chill".
/// Scenes lay burden temperature directly rather than driving a campaign into a hang, since a hung
/// column brings no fresh carbon to its own raceway and the shaft goes Idle within about two minutes
/// (<see cref="ColdBlastFurnaceScenarioTests"/> owns that). They chill one column among rich ones and
/// never the reverse, because <c>RacewayMix</c> is unit-weighted across every column, so a lone rich
/// column burns at the furnace's poor average.
/// </summary>
[Collection(FurnaceConfigCollection.Name)]
public class ChillTests {
  #region Scenes

  /// <summary>Comfortably over the 1482 C melt line; burden this hot renders.</summary>
  private const float Molten = 1600f;

  /// <summary>Comfortably under it, but not ambient: a chill is burden that warmed and still missed the
  /// melt line, rather than a furnace that never lit.</summary>
  private const float Chilled = 900f;

  /// <summary>
  /// A complete, blown furnace laid course by course: column 0 at <paramref name="firstTemp"/>, the
  /// rest at <paramref name="restTemp"/>. Every column gets a real fuel course, since one carbon-free
  /// raceway makes the whole furnace Idle. Bands are thin and lean (2 in 16) so they repeat inside the
  /// 32-unit raceway slice, the mix reads under <c>BfReferenceFuelFrac</c> (about 0.17) so the demanded
  /// pressure clears the <c>BfBlastPressureMin</c> floor, and the charge still burns.
  /// </summary>
  private static ColdBlastFurnaceRig Scene(float firstTemp, float restTemp) {
    var scene = ColdBlastFurnaceScenes.Complete(charge: -1);
    var keys = scene.ColumnKeys;

    scene.LayCourseInto(
      keys[0].X,
      keys[0].Z,
      2,
      14,
      rounds: 8,
      burdenTemp: firstTemp
    );
    for (int i = 1; i < keys.Count; i++)
      scene.LayCourseInto(
        keys[i].X,
        keys[i].Z,
        2,
        14,
        rounds: 8,
        burdenTemp: restTemp
      );

    // One tick: every read under test is derived, so the furnace only has to decide its state once.
    return scene.PressuriseBlast().RunLive(1);
  }

  #endregion

  #region A hang is per column

  /// <summary>
  /// The column whose raceway burden is under the melt line hangs, and its neighbours carrying the same
  /// burden over the line do not. Both halves are asserted: a predicate answering "hung" for everything
  /// satisfies the first, one answering "no" for everything the second.
  /// </summary>
  [Fact]
  public void The_column_whose_raceway_burden_is_below_the_melt_line_is_the_one_that_hangs() {
    var scene = Scene(firstTemp: Chilled, restTemp: Molten);
    var keys = scene.ColumnKeys;

    Assert.True(
      scene.IsHung(keys[0].X, keys[0].Z),
      "the chilled column should hang"
    );
    for (int i = 1; i < keys.Count; i++)
      Assert.False(
        scene.IsHung(keys[i].X, keys[i].Z),
        $"column {i} carries burden over the melt line and must not hang"
      );
    Assert.Equal(1, scene.HungColumns);
  }

  /// <summary>
  /// A raceway holding no burden at all is not hung: it is a fuel course waiting for its burden, which
  /// is what the bottom of a freshly-charged shaft looks like. A predicate written as "no meltable
  /// burden down there" would report every correctly-charged furnace as hanging on its first course.
  /// </summary>
  [Fact]
  public void A_raceway_with_no_burden_in_it_at_all_is_not_hung() {
    var scene = ColdBlastFurnaceScenes.Complete(charge: -1);
    var keys = scene.ColumnKeys;

    // A full block of pure fuel under the burden - the raceway slice sees nothing but carbon.
    foreach (var (x, z) in keys)
      scene.LayCourseInto(x, z, 32, 0);
    foreach (var (x, z) in keys)
      scene.LayCourseInto(x, z, 8, 24, rounds: 3, burdenTemp: Chilled);

    scene.PressuriseBlast().RunLive(1);

    Assert.NotEqual(FurnaceState.Idle, scene.State);
    Assert.Equal(0, scene.HungColumns);
  }

  /// <summary>
  /// The hang is derived, so a furnace that is out reports none however cold its columns are. The
  /// charge is unchanged from a tick ago and only the fire has changed, so a stored flag would survive
  /// this and a dead furnace would go on reporting a hang.
  /// </summary>
  [Fact]
  public void A_furnace_that_is_out_reports_no_hang_however_cold_its_columns_are() {
    var scene = Scene(firstTemp: Chilled, restTemp: Chilled);
    Assert.True(
      scene.HungColumns > 0,
      "a wholly chilled shaft should hang while it is lit"
    );

    scene.CutBlast();
    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Idle, 1200) > 0,
      "cutting the blast should put it out"
    );

    Assert.Equal(0, scene.HungColumns);
    foreach (var (x, z) in scene.ColumnKeys)
      Assert.False(scene.IsHung(x, z));
  }

  #endregion

  #region The melt walk and the state label read the same band

  /// <summary>
  /// <c>MeltBurden</c> stops at the first burden band it cannot melt, because the column above rests on
  /// it, and <c>AtMeltingTemperature</c> must read that same band. The scene puts hot burden above cold
  /// burden inside one raceway slice, so a scan of the slice for any burden over the line would report
  /// <c>Melting</c> while nothing renders.
  /// </summary>
  [Fact]
  public void Hot_burden_stranded_above_a_chilled_band_does_not_make_the_furnace_Melting() {
    var scene = ColdBlastFurnaceScenes.Complete(charge: -1);

    // [fuel 8][burden 4 cold][burden 20 hot], all inside one 32-unit raceway slice. Laid rich (8 of 32)
    // unlike the other scenes here, because the claim needs the flame comfortably over the melt line.
    foreach (var (x, z) in scene.ColumnKeys) {
      scene.LayCourseInto(x, z, 8, 4, burdenTemp: Chilled);
      scene.LayCourseInto(x, z, 0, 20, burdenTemp: Molten);
      scene.LayCourseInto(x, z, 8, 24, rounds: 3, burdenTemp: Molten);
    }

    scene.PressuriseBlast().RunLive(10);

    // Lit, so this is not passing because the furnace is dead.
    Assert.NotEqual(FurnaceState.Idle, scene.State);

    // The control on the other conjunct: `AtMeltingTemperature` is an AND of flame over the line and
    // burden at a raceway over it, so a lagging flame answers "not melting" for another reason.
    Assert.True(
      scene.Heat.TProcess > IiexValues.BfIronMeltingPoint,
      $"the flame must be over the melt line or the claim below is vacuous; T_process was {scene.Heat.TProcess} C"
    );

    // The cold band decides, even with 24 units of molten-hot burden sitting on top of it.
    Assert.NotEqual(FurnaceState.Melting, scene.State);
    // The column reports it as a hang.
    Assert.Equal(scene.ColumnKeys.Count, scene.HungColumns);

    // It renders nothing, which the label alone cannot prove.
    float ironBefore = scene.MoltenIron;
    scene.RunLive(30);
    Assert.Equal(ironBefore, scene.MoltenIron, 3);
  }

  #endregion

  #region The whole stockline

  /// <summary>
  /// When every column hangs the furnace stops melting by arithmetic rather than by a threshold: a hung
  /// column offers the melt walk nothing, so the derived state falls to <c>Firing</c> while the shaft
  /// stays lit and goes on burning its charge. See <c>docs/design/layered-charge.md</c>.
  /// </summary>
  [Fact]
  public void A_wholly_hung_shaft_stops_melting_but_goes_on_burning_its_fuel() {
    var scene = Scene(firstTemp: Chilled, restTemp: Chilled);

    Assert.Equal(scene.ColumnKeys.Count, scene.HungColumns);
    Assert.NotEqual(FurnaceState.Idle, scene.State);
    Assert.NotEqual(FurnaceState.Melting, scene.State);

    float ironBefore = scene.MoltenIron;
    float carbonBefore = scene.CarbonUnits;
    scene.RunLive(30);

    Assert.Equal(ironBefore, scene.MoltenIron, 3); // it renders nothing
    Assert.True(
      scene.CarbonUnits < carbonBefore,
      $"and it goes on burning its carbon; {carbonBefore} -> {scene.CarbonUnits}"
    );
  }

  /// <summary>
  /// The self-reinforcement is arithmetic rather than a rule: a hung column keeps burning fuel while its
  /// burden refuses to melt, so its raceway slice goes leaner and <c>RequiredBlastPressureFor</c>'s
  /// coke-shortfall term raises the demanded pressure. There is no hang branch in the blast code. Read
  /// off <c>RacewayMix</c>, what the furnace read this tick, never off a mix built here.
  /// </summary>
  [Fact]
  public void A_hang_stiffens_the_blast_demand_with_no_hang_branch_in_the_blast_code() {
    var scene = Scene(firstTemp: Chilled, restTemp: Chilled);

    float leanBefore = scene.RacewayMix.FuelFrac;
    float demandBefore = scene.Core.RequiredBlastPressureFor(scene.RacewayMix);

    scene.RunLive(60);

    float leanAfter = scene.RacewayMix.FuelFrac;
    float demandAfter = scene.Core.RequiredBlastPressureFor(scene.RacewayMix);

    Assert.True(
      leanAfter < leanBefore,
      $"a hung raceway burns its fuel and melts nothing, so it must go leaner; {leanBefore} -> {leanAfter}"
    );
    Assert.True(
      demandAfter > demandBefore,
      $"and a leaner raceway must demand more pressure; {demandBefore} -> {demandAfter} atm"
    );
  }

  #endregion

  #region Getting a chilled charge back out

  /// <summary>
  /// Recoverability end to end: a chilled column is dug out through the shaft wall and the burden comes
  /// back with its grade intact. It has to be the bottom block, because a chill sits at the raceway and
  /// <c>TryTakeTop</c> only reaches the stockline.
  /// </summary>
  [Fact]
  public void A_chilled_column_can_be_dug_out_through_the_wall_and_the_burden_keeps_its_grade() {
    var scene = Scene(firstTemp: Chilled, restTemp: Chilled);
    Assert.Equal(scene.ColumnKeys.Count, scene.HungColumns); // the premise: it really is stuck

    BurdenMix charged = scene.ChargedMix;
    var (x, z) = scene.ColumnKeys[0];

    // The lowest charge block of that column, where the chilled band is. Searched rather than named,
    // so the case does not encode this furnace's floor height.
    BlockEntityChargePile? pile = null;
    for (int y = 0; y < 8 && pile == null; y++)
      pile = scene.PileAtLocal(x, y, z);
    Assert.NotNull(pile);
    int before = scene.ColumnUnitsAt(x, z);
    scene.World.Drops.Clear();

    pile.Block.OnBlockBroken(scene.World.World, pile.Pos, null!);

    // It came back as burden of the grade that went in.
    Assert.NotEmpty(scene.World.Drops);
    ItemStack? burden = scene.World.Drops.Find(Burden.Is);
    Assert.NotNull(burden);
    Assert.Equal(charged, Burden.Read(burden));

    // The column is shorter, so the drops are recovery rather than duplication.
    Assert.True(
      scene.ColumnUnitsAt(x, z) < before,
      $"the column should have lost the window it gave back; {before} -> {scene.ColumnUnitsAt(x, z)}"
    );
  }

  #endregion

  #region The readout

  /// <summary>
  /// A hang has no other symptom the player can attribute: the furnace stays lit, stays hot, eats fuel
  /// and stops making iron. The readout is the only diagnosis; see <c>docs/design/layered-charge.md</c>.
  /// </summary>
  [Fact]
  public void The_readout_names_the_hang_and_says_when_the_whole_stockline_has_gone() {
    var partial = Scene(firstTemp: Chilled, restTemp: Molten);
    Assert.Contains("bf-info-hung", partial.CoreInfo());
    Assert.DoesNotContain("bf-info-hungall", partial.CoreInfo());

    var whole = Scene(firstTemp: Chilled, restTemp: Chilled);
    Assert.Contains("bf-info-hungall", whole.CoreInfo());

    // A furnace with nothing wrong says nothing: the line is a diagnosis, not a status field.
    var sound = Scene(firstTemp: Molten, restTemp: Molten);
    Assert.DoesNotContain("bf-info-hung", sound.CoreInfo());
  }

  #endregion
}
