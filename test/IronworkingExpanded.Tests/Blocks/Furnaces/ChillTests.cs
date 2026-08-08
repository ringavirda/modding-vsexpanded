using System;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// <b>The chill</b> — under-coked burden reaches a raceway too cold to melt, so it does not leave, so no
/// space appears beneath the column and nothing above it descends. Hanging (scaffolding) is the most famous
/// way a real blast furnace goes wrong, and <c>docs/design/layered-charge.md</c> § <i>The chill</i> rules it
/// as a <b>per-column</b> property that falls out of the counter-current model rather than a rule laid on
/// top of it.
/// <para>
/// <b>These cases lay the burden's temperature directly rather than waiting for a campaign to produce
/// one, and that is a deliberate choice with a reason.</b> Driving a real furnace into a hang works — and
/// then the furnace <b>dies within about two minutes</b>, because a column that does not descend never
/// brings fresh carbon down to its own raceway, so <c>RacewayHoldsCarbon</c> fails and the whole shaft goes
/// Idle. That death is correct and <see cref="ColdBlastFurnaceScenarioTests"/> owns it as the *consequence*.
/// It is useless as an *instrument*: every assertion about a hung column would be racing the furnace's own
/// funeral. Stating the premise makes each rule below observable on its own.
/// </para>
/// <para>
/// <b>One lean column among rich ones, never one rich among lean.</b> The flame temperature is a property
/// of the whole raceway — <c>RacewayMix</c> is unit-weighted across every column — so a single rich column
/// standing among starved ones burns at the furnace's poor average and chills along with them. What is
/// genuinely per-column is how much hot gas rises through it.
/// </para>
/// </summary>
[Collection(FurnaceConfigCollection.Name)]
public class ChillTests
{
  #region Scenes

  /// <summary>Comfortably over the 1482 C melt line — burden this hot is going to be rendered.</summary>
  private const float Molten = 1600f;

  /// <summary>Comfortably under it. Not ambient: a chill is burden that got *warm* and still missed, which
  /// is what makes it a charging mistake rather than a furnace that never lit.</summary>
  private const float Chilled = 900f;

  /// <summary>
  /// A complete, blown furnace whose columns are laid course by course at chosen temperatures: the column
  /// at index 0 at <paramref name="firstTemp"/> and every other at <paramref name="restTemp"/>.
  /// <para>
  /// Every column gets a real fuel course, because <c>RacewayHoldsCarbon</c> asks <b>every</b> column and
  /// a furnace with one carbon-free raceway is Idle — which would make every case below vacuous.
  /// </para>
  /// <para>
  /// <b>Thin bands, many of them, at a deliberately lean 2-in-16.</b> Three separate constraints pick
  /// that number and it is not free to round off:
  /// <list type="number">
  /// <item>Bands must repeat <em>inside</em> the 32-unit raceway slice. A hung column never descends, so its
  /// slice never refreshes — lay the same fuel as one thick band and it burns out, <c>RacewayHoldsCarbon</c>
  /// fails and the whole furnace goes Idle mid-case.</item>
  /// <item>The raceway must read <b>leaner than <c>BfReferenceFuelFrac</c></b>, or
  /// <c>RequiredBlastPressureFor</c> sits pinned on its <c>BfBlastPressureMin</c> floor and the
  /// self-reinforcement case can observe nothing. At 2/16 the raceway reads ≈0.17 and demands ≈2.2 atm,
  /// with room to climb.</item>
  /// <item>It must still be a charge that <em>burns</em>. This is an under-coked furnace, which is what a
  /// chill is — not an unlit one.</item>
  /// </list>
  /// </para>
  /// </summary>
  private static ColdBlastFurnaceRig Scene(float firstTemp, float restTemp)
  {
    var scene = ColdBlastFurnaceScenes.Complete(charge: -1);
    var keys = scene.ColumnKeys;

    scene.LayCourseInto(keys[0].X, keys[0].Z, 2, 14, rounds: 8, burdenTemp: firstTemp);
    for (int i = 1; i < keys.Count; i++)
      scene.LayCourseInto(keys[i].X, keys[i].Z, 2, 14, rounds: 8, burdenTemp: restTemp);

    // One tick is all it takes: every read under test is derived, so the furnace only has to have
    // decided its state once.
    return scene.PressuriseBlast().RunLive(1);
  }

  #endregion

  #region A hang is per column

  /// <summary>
  /// The rule itself: the column whose raceway burden is under the melt line is hung, and its neighbours
  /// carrying the same burden over the line are not.
  /// <para>
  /// Both halves are asserted. A predicate that answered "hung" for everything would satisfy the first
  /// and a predicate that answered "no" for everything would satisfy the second, and each alone reads as a
  /// working rule.
  /// </para>
  /// </summary>
  [Fact]
  public void The_column_whose_raceway_burden_is_below_the_melt_line_is_the_one_that_hangs()
  {
    var scene = Scene(firstTemp: Chilled, restTemp: Molten);
    var keys = scene.ColumnKeys;

    Assert.True(scene.IsHung(keys[0].X, keys[0].Z), "the chilled column should hang");
    for (int i = 1; i < keys.Count; i++)
      Assert.False(
        scene.IsHung(keys[i].X, keys[i].Z),
        $"column {i} carries burden over the melt line and must not hang"
      );
    Assert.Equal(1, scene.HungColumns);
  }

  /// <summary>
  /// A raceway holding <b>no burden at all</b> is not hung — it is a fuel course waiting for its burden,
  /// which is exactly what the bottom of a freshly-charged shaft looks like. A predicate written as "no
  /// meltable burden down there" rather than "the burden down there is cold" answers yes here, and would
  /// report every correctly-charged furnace as hanging on its first course.
  /// </summary>
  [Fact]
  public void A_raceway_with_no_burden_in_it_at_all_is_not_hung()
  {
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
  /// <b>Derived, so a furnace that is out reports no hang however cold its columns are.</b> The charge is
  /// bit-for-bit what it was a tick ago; only the fire has changed. A stored flag would survive this, and a
  /// dead furnace would go on complaining about a hang the player cannot do anything about.
  /// </summary>
  [Fact]
  public void A_furnace_that_is_out_reports_no_hang_however_cold_its_columns_are()
  {
    var scene = Scene(firstTemp: Chilled, restTemp: Chilled);
    Assert.True(scene.HungColumns > 0, "a wholly chilled shaft should hang while it is lit");

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
  /// <b>The defect this unit found, pinned.</b> <c>MeltBurden</c> stops dead at the <b>first</b> burden
  /// band it cannot melt, because the whole column above is physically resting on it.
  /// <c>AtMeltingTemperature</c> used to scan the raceway slice for <b>any</b> burden over the line — so a
  /// column laid <c>[cold burden][hot burden]</c> reported <c>Melting</c>, with the label, the sound and the
  /// HUD line, while rendering precisely nothing. That is the exact failure
  /// <c>AtMeltingTemperature</c>'s own doc-comment says it exists to prevent.
  /// <para>
  /// The scene is built so the two readings <b>disagree</b>: hot burden sits above cold burden inside one
  /// raceway slice. A furnace whose two halves read the same band answers Firing; one whose halves disagree
  /// answers Melting and makes no iron.
  /// </para>
  /// </summary>
  [Fact]
  public void Hot_burden_stranded_above_a_chilled_band_does_not_make_the_furnace_Melting()
  {
    var scene = ColdBlastFurnaceScenes.Complete(charge: -1);

    // [fuel 8][burden 4 cold][burden 20 hot] - all inside one 32-unit raceway slice.
    // Laid rich (8 of 32) on purpose, unlike the other scenes here: this is the one case that needs the
    // flame comfortably over the line, because the whole claim is that a hot flame plus hot burden still
    // does not melt when a cold band is in the way.
    foreach (var (x, z) in scene.ColumnKeys)
    {
      scene.LayCourseInto(x, z, 8, 4, burdenTemp: Chilled);
      scene.LayCourseInto(x, z, 0, 20, burdenTemp: Molten);
      scene.LayCourseInto(x, z, 8, 24, rounds: 3, burdenTemp: Molten);
    }

    scene.PressuriseBlast().RunLive(10);

    // Lit - so this is not passing because the furnace is simply dead.
    Assert.NotEqual(FurnaceState.Idle, scene.State);

    // The control, and without it this case is worthless. `AtMeltingTemperature` is an AND: the flame
    // over the line, and burden at a raceway over it. A furnace whose flame has not caught up yet answers
    // "not melting" for a reason that has nothing to do with band order - so the old, defective scan passes
    // this case too. Reverting the fix leaves the whole suite green without this line, which is why it
    // is here.
    Assert.True(
      scene.Heat.TProcess > IwexValues.BfIronMeltingPoint,
      $"the flame must be over the melt line or the claim below is vacuous; T_process was {scene.Heat.TProcess} C"
    );

    // The claim: the cold band decides, even with 24 units of molten-hot burden sitting on top of it.
    Assert.NotEqual(FurnaceState.Melting, scene.State);
    // And the column says so in the one word the player can act on.
    Assert.Equal(scene.ColumnKeys.Count, scene.HungColumns);

    // It really does render nothing, which is the half the label alone cannot prove.
    float ironBefore = scene.MoltenIron;
    scene.RunLive(30);
    Assert.Equal(ironBefore, scene.MoltenIron, 3);
  }

  #endregion

  #region The whole stockline

  /// <summary>
  /// <b>When every column hangs the furnace halts — by arithmetic, not by a threshold.</b> There is no
  /// "N of 9 columns" constant anywhere and there must not be one: a hung column offers the melt walk
  /// nothing, so a shaft in which every column hangs renders nothing, and the derived state falls to
  /// <c>Firing</c> because no column has anything to offer. A constant could only disagree with that.
  /// <para>
  /// And it is still <b>lit and burning</b>. <c>layered-charge.md</c>: <i>"a furnace sitting hot and
  /// melting nothing is still burning its charge away, and that is a real and correct way to waste a
  /// campaign."</i>
  /// </para>
  /// </summary>
  [Fact]
  public void A_wholly_hung_shaft_stops_melting_but_goes_on_burning_its_fuel()
  {
    var scene = Scene(firstTemp: Chilled, restTemp: Chilled);

    Assert.Equal(scene.ColumnKeys.Count, scene.HungColumns);
    Assert.NotEqual(FurnaceState.Idle, scene.State);
    Assert.NotEqual(FurnaceState.Melting, scene.State);

    float ironBefore = scene.MoltenIron;
    float carbonBefore = scene.CarbonUnits;
    scene.RunLive(30);

    Assert.Equal(ironBefore, scene.MoltenIron, 3); // it renders nothing at all
    Assert.True(
      scene.CarbonUnits < carbonBefore,
      $"and it goes on burning its carbon; {carbonBefore} -> {scene.CarbonUnits}"
    );
  }

  /// <summary>
  /// <b>The self-reinforcement, and it is arithmetic rather than a rule.</b> A hung column keeps burning
  /// its fuel while its burden refuses to melt, so its raceway slice goes leaner, so
  /// <c>RequiredBlastPressureFor</c>'s coke-shortfall term raises the pressure the furnace demands — with no
  /// hang branch anywhere in the blast code. Denser charge, harder to blow through, dies faster, which is
  /// what a real hang does.
  /// <para>
  /// Read off <c>RacewayMix</c> — what the furnace itself read this tick — never off a mix built here.
  /// Handing <c>RequiredBlastPressureFor</c> an invented mix asserts its own arithmetic and says nothing
  /// whatever about a hang.
  /// </para>
  /// </summary>
  [Fact]
  public void A_hang_stiffens_the_blast_demand_with_no_hang_branch_in_the_blast_code()
  {
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
  /// <b>The R2 recoverability promise, end to end: a chilled column is dug out through the shaft wall and
  /// the burden comes back with its grade intact.</b> Without this a chill is a permanently bricked furnace,
  /// and the whole mechanic stops being a risk the player took and becomes a machine that ate their ore.
  /// <para>
  /// <b>It has to be the bottom block, and that is the entire point.</b> A chill sits at the raceway by
  /// definition, so recovering "from the top" — which is all <c>TryTakeTop</c> can do, and all the code
  /// once allowed — leaves the one failure recoverability exists for unrecoverable, with
  /// the gate reading green. This case digs at the very cell the chilled band stands in.
  /// </para>
  /// </summary>
  [Fact]
  public void A_chilled_column_can_be_dug_out_through_the_wall_and_the_burden_keeps_its_grade()
  {
    var scene = Scene(firstTemp: Chilled, restTemp: Chilled);
    Assert.Equal(scene.ColumnKeys.Count, scene.HungColumns); // the premise: it really is stuck

    BurdenMix charged = scene.ChargedMix;
    var (x, z) = scene.ColumnKeys[0];

    // The lowest charge block of that column - where the chilled band is. Searched rather than named, so
    // the case does not quietly encode this furnace's floor height and start passing at the wrong cell if
    // the layout moves.
    BlockEntityChargePile? pile = null;
    for (int y = 0; y < 8 && pile == null; y++)
      pile = scene.PileAtLocal(x, y, z);
    Assert.NotNull(pile);
    int before = scene.ColumnUnitsAt(x, z);
    scene.World.Drops.Clear();

    pile.Block.OnBlockBroken(scene.World.World, pile.Pos, null!);

    // It came back, and it came back as burden of the grade that went in - not as a generic lump and not
    // as nothing.
    Assert.NotEmpty(scene.World.Drops);
    ItemStack? burden = scene.World.Drops.Find(Burden.Is);
    Assert.NotNull(burden);
    Assert.Equal(charged, Burden.Read(burden));

    // And the column really is shorter - the drops are recovery, not duplication.
    Assert.True(
      scene.ColumnUnitsAt(x, z) < before,
      $"the column should have lost the window it gave back; {before} -> {scene.ColumnUnitsAt(x, z)}"
    );
  }

  #endregion

  #region The readout

  /// <summary>
  /// <b>A hang has no other symptom the player can attribute</b> — the furnace stays lit, stays hot, eats
  /// fuel and stops making iron — so the readout is the whole difference <c>layered-charge.md</c> draws
  /// between "a chill being a mystery and a chill being a risk the player took knowingly".
  /// </summary>
  [Fact]
  public void The_readout_names_the_hang_and_says_when_the_whole_stockline_has_gone()
  {
    var partial = Scene(firstTemp: Chilled, restTemp: Molten);
    Assert.Contains("bf-info-hung", partial.CoreInfo());
    Assert.DoesNotContain("bf-info-hungall", partial.CoreInfo());

    var whole = Scene(firstTemp: Chilled, restTemp: Chilled);
    Assert.Contains("bf-info-hungall", whole.CoreInfo());

    // A furnace with nothing wrong says nothing at all - the line is a diagnosis, not a status field.
    var sound = Scene(firstTemp: Molten, restTemp: Molten);
    Assert.DoesNotContain("bf-info-hung", sound.CoreInfo());
  }

  #endregion
}
