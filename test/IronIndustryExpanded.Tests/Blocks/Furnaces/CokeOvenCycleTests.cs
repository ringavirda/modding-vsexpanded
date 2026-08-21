using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The bake: a sealed chamber of bituminous coal becomes coke on a timer, never on a temperature. The
/// oven rides vanilla's coking process rather than the heat balance, so what these cases pin is the clock,
/// the ledger and the per-chamber independence - not a heat curve.
/// <para>
/// The yield is deliberately compared against vanilla's own number (0.75 for bituminous, from
/// <c>itemtypes/resource/ore-ungraded.json</c>): the whole case for building this oven is that it beats
/// the 3x3x3 chamber a player already has, so a change that quietly drops below that has to fail here.
/// </para>
/// </summary>
public class CokeOvenCycleTests {
  #region Harness

  private static readonly BlockPos Anchor = new(0, 16, 0);

  private const string Bituminous = "game:ore-bituminouscoal";
  private const string Coke = "game:coke";

  /// <summary>Vanilla's own bituminous conversion, which this oven must beat rather than match.</summary>
  private const float VanillaRate = 0.75f;

  /// <summary>
  /// An oven stood facing north with every chamber cell holding <paramref name="unitsPerCell"/> of
  /// <paramref name="fuel"/>. <paramref name="where"/> narrows the charge to some cells, leaving the rest
  /// standing but empty, which is how one chamber is charged and the other is not.
  /// </summary>
  private static (BlockEntityCokeOven Oven, StructureRig Rig) Charged(
    int unitsPerCell,
    string fuel = Bituminous,
    System.Func<BlockPos, bool>? where = null
  ) {
    var oven = new BlockEntityCokeOven();
    StructureRig rig = Stand(
      oven,
      BlockCokeOvenCore.Definitions("iiex").Single(),
      Anchor,
      "iiex:furnace-cokeovencore",
      "north",
      complete: false
    );
    // Shut, or nothing bakes: the seal gate reads a missing lid as an open one. These cases are about the
    // clock and the ledger; the gate itself is CokeOvenLidGateTests'.
    CloseCokeOven(rig);
    rig.Complete();
    rig.World.RegisterItem(Coke);
    LoadFireboxes(rig, oven, unitsPerCell, fuel, where);
    return (oven, rig);
  }

  /// <summary>Drives the bake for <paramref name="seconds"/>, in the interval-sized steps the core would
  /// hand it, so nothing passes only because it was given one enormous <c>dt</c>.</summary>
  private static void Bake(BlockEntityCokeOven oven, float seconds) {
    const float Step = 60f;
    for (float t = 0; t < seconds; t += Step)
      ReflectionHelpers.Invoke(
        oven,
        "SmeltCycle",
        [new object(), System.Math.Min(Step, seconds - t)]
      );
  }

  private static IReadOnlyList<BEBehaviorFirebox> BedsOf(
    BlockEntityCokeOven oven,
    IReadOnlyList<BlockPos> chamber
  ) =>
    [
      .. chamber.Select(c =>
        oven.Api.World.BlockAccessor.GetBlockEntity(c)
          .GetBehavior<BEBehaviorFirebox>()
      ),
    ];

  private static float Cycle => IiexValues.CokeOvenCycleSec;

  #endregion

  #region The bake

  /// <summary>
  /// A full oven left for one cycle comes out coke, at the configured yield truncated exactly as vanilla
  /// truncates its own. Both chambers, because a charge fills the whole oven at one interaction.
  /// </summary>
  [Fact]
  public void A_full_charge_bakes_to_coke_at_the_stated_yield() {
    int capacity = BEBehaviorFirebox.CellCapacity;
    (BlockEntityCokeOven oven, _) = Charged(capacity);

    Bake(oven, Cycle);

    int expected = (int)(capacity * IiexValues.CokeOvenYieldFrac);
    foreach (var chamber in oven.Chambers)
      foreach (BEBehaviorFirebox bed in BedsOf(oven, chamber)) {
        Assert.Equal(Coke, bed.FuelCode);
        Assert.Equal(expected, bed.Units);
      }
  }

  /// <summary>
  /// The oven beats the chamber the player already has. Vanilla converts bituminous at 0.75; anything at
  /// or below that makes the build pointless, and the arched crown is what the difference is attributed
  /// to.
  /// </summary>
  [Fact]
  public void The_yield_beats_vanillas_own_oven() {
    int capacity = BEBehaviorFirebox.CellCapacity;

    Assert.True(
      IiexValues.CokeOvenYieldFrac > VanillaRate,
      "the stated yield must beat vanilla's 0.75"
    );
    // And after truncation too, which is the number a player actually receives: a fraction that beats
    // vanilla on paper and loses to it at this cell size would be a downgrade wearing a better figure.
    Assert.True(
      (int)(capacity * IiexValues.CokeOvenYieldFrac)
        > (int)(capacity * VanillaRate),
      "the truncated yield must beat vanilla's too"
    );
  }

  /// <summary>
  /// Scale is the other half of the case. A vanilla chamber cokes one 16-coal pile; this oven holds every
  /// chamber cell at once, and if that ever stopped being much larger the machine would be a bigger box at
  /// the same rate - the failure the design rules out by name.
  /// </summary>
  [Fact]
  public void The_oven_holds_far_more_than_a_vanilla_pile() {
    const int VanillaPileUnits = 16;
    (BlockEntityCokeOven oven, _) = Charged(BEBehaviorFirebox.CellCapacity);

    int held = oven
      .Chambers.SelectMany(c => BedsOf(oven, c))
      .Sum(bed => bed.Units);

    Assert.Equal(12 * BEBehaviorFirebox.CellCapacity, held);
    Assert.True(
      held >= 4 * VanillaPileUnits,
      $"the oven holds {held} units against a vanilla pile's {VanillaPileUnits}"
    );
  }

  /// <summary>
  /// Short of the cycle, nothing has happened. Without this the conversion could fire on the first tick
  /// and every other case here would still pass.
  /// </summary>
  [Fact]
  public void Nothing_converts_before_the_cycle_is_up() {
    (BlockEntityCokeOven oven, _) = Charged(BEBehaviorFirebox.CellCapacity);

    Bake(oven, Cycle * 0.9f);

    foreach (var chamber in oven.Chambers)
      foreach (BEBehaviorFirebox bed in BedsOf(oven, chamber))
        Assert.Equal(Bituminous, bed.FuelCode);
  }

  /// <summary>
  /// The yield applies once per charge, not once per cycle. Two consecutive cycles on one charge is the
  /// dirty-precondition form of this: a single cycle cannot distinguish "converted" from "converted
  /// repeatedly", and a bake that kept re-applying its fraction would silently eat the coke it made.
  /// </summary>
  [Fact]
  public void A_second_cycle_does_not_bake_the_coke_again() {
    int capacity = BEBehaviorFirebox.CellCapacity;
    (BlockEntityCokeOven oven, _) = Charged(capacity);

    Bake(oven, Cycle);
    int after = BedsOf(oven, oven.Chambers[0])[0].Units;
    Bake(oven, Cycle * 3f);

    Assert.Equal(after, BedsOf(oven, oven.Chambers[0])[0].Units);
    Assert.Equal(Coke, BedsOf(oven, oven.Chambers[0])[0].FuelCode);
  }

  #endregion

  #region What it will not bake

  /// <summary>
  /// Coke does not re-coke. It is already the product, and a chamber that kept converting it would apply
  /// the yield again on every cycle - a fuel shredder rather than an oven.
  /// </summary>
  [Fact]
  public void A_chamber_of_coke_is_left_alone() {
    int capacity = BEBehaviorFirebox.CellCapacity;
    (BlockEntityCokeOven oven, _) = Charged(capacity, Coke);

    Bake(oven, Cycle * 2f);

    foreach (BEBehaviorFirebox bed in BedsOf(oven, oven.Chambers[0])) {
      Assert.Equal(Coke, bed.FuelCode);
      Assert.Equal(capacity, bed.Units);
    }
  }

  /// <summary>
  /// Charcoal is fuel and is not coal, so it bakes into nothing. The oven refuses it at the door for the
  /// same reason - see below - and this pins the cycle's own half of that, so removing one guard cannot
  /// hide behind the other.
  /// </summary>
  [Fact]
  public void A_chamber_of_charcoal_is_left_alone() {
    int capacity = BEBehaviorFirebox.CellCapacity;
    (BlockEntityCokeOven oven, _) = Charged(capacity, "game:charcoal");

    Bake(oven, Cycle * 2f);

    foreach (BEBehaviorFirebox bed in BedsOf(oven, oven.Chambers[0])) {
      Assert.Equal("game:charcoal", bed.FuelCode);
      Assert.Equal(capacity, bed.Units);
    }
  }

  /// <summary>
  /// The accept list at the door: bituminous only, by ruling, which makes coal type a prospecting
  /// constraint on entering the iron tier. Vanilla is looser and cokes lignite at 0.5 as well - the
  /// departure is deliberate, and this is where it is stated.
  /// </summary>
  [Theory]
  [InlineData(Bituminous, true)]
  [InlineData("game:ore-lignite", false)]
  [InlineData("game:ore-anthracite", false)]
  [InlineData("game:charcoal", false)]
  [InlineData(Coke, false)]
  public void The_oven_takes_coking_coal_alone(string code, bool accepted) {
    (BlockEntityCokeOven oven, StructureRig rig) = Charged(0);
    var stack = new ItemStack(rig.World.RegisterItem(code), 1);

    Assert.Equal(accepted, oven.AcceptsFireboxFuel(stack));
  }

  /// <summary>
  /// An ordinary firebox is unchanged by the seam the oven needed. The reheat furnace still burns
  /// everything it did, or the accept-list override has quietly narrowed every machine in the family.
  /// </summary>
  [Theory]
  [InlineData(Coke, true)]
  [InlineData("game:charcoal", true)]
  [InlineData("game:ore-anthracite", true)]
  [InlineData("game:ore-lignite", false)]
  public void A_reheat_furnace_still_burns_what_it_always_did(
    string code,
    bool accepted
  ) {
    var furnace = new BlockEntityHeatingFurnace();
    StructureRig rig = Stand(
      furnace,
      BlockHeatingFurnaceCore.Definitions("iiex").Single(),
      Anchor,
      "iiex:furnace-heatingcore-tier1",
      "north"
    );
    var stack = new ItemStack(rig.World.RegisterItem(code), 1);

    Assert.Equal(accepted, furnace.AcceptsFireboxFuel(stack));
  }

  #endregion

  #region Two chambers, two clocks

  /// <summary>
  /// The drawing gives two chambers and the oven finds them without being told: connected groups of the
  /// cells the layout marks, with the shared wall between them carrying none.
  /// </summary>
  [Fact]
  public void The_oven_finds_its_two_chambers() {
    (BlockEntityCokeOven oven, _) = Charged(0);

    Assert.Equal(2, oven.Chambers.Count);
    Assert.All(oven.Chambers, c => Assert.Equal(6, c.Count));
  }

  /// <summary>
  /// Charging one chamber cokes that chamber alone. This is what makes the bank two ovens rather than one
  /// large one: a shop can draw the west side and recharge it while the east is still baking.
  /// </summary>
  [Fact]
  public void Only_the_charged_chamber_bakes() {
    int capacity = BEBehaviorFirebox.CellCapacity;
    var oven = new BlockEntityCokeOven();
    StructureRig rig = Stand(
      oven,
      BlockCokeOvenCore.Definitions("iiex").Single(),
      Anchor,
      "iiex:furnace-cokeovencore",
      "north",
      complete: false
    );
    CloseCokeOven(rig);
    rig.Complete();
    rig.World.RegisterItem(Coke);

    // The premise: the two chambers really do lie either side of the anchor.
    var west = oven.Chambers[0];
    var east = oven.Chambers[1];
    Assert.True(west.All(c => c.X < Anchor.X));
    Assert.True(east.All(c => c.X > Anchor.X));

    LoadFireboxes(rig, oven, capacity, Bituminous, cell => cell.X < Anchor.X);
    Bake(oven, Cycle);

    Assert.All(BedsOf(oven, west), bed => Assert.Equal(Coke, bed.FuelCode));
    Assert.All(BedsOf(oven, east), bed => Assert.Null(bed.FuelCode));
  }

  /// <summary>
  /// The clocks are genuinely separate, which neither of the cases above proves on its own: an oven with
  /// one shared clock passes both, because a chamber with no coal converts nothing either way. Charge the
  /// west, bake most of a cycle, then charge the east and finish the west's - the west must come out coke
  /// and the east must still be raw, which only holds if the east started its own clock when it was
  /// charged.
  /// </summary>
  [Fact]
  public void A_chamber_charged_late_starts_its_own_clock() {
    int capacity = BEBehaviorFirebox.CellCapacity;
    (BlockEntityCokeOven oven, StructureRig rig) = Charged(
      capacity,
      Bituminous,
      cell => cell.X < Anchor.X
    );
    var west = oven.Chambers[0];
    var east = oven.Chambers[1];

    Bake(oven, Cycle * 0.8f);

    Item coal = rig.World.RegisterItem(Bituminous);
    foreach (BEBehaviorFirebox bed in BedsOf(oven, east))
      bed.TryAdd(new ItemStack(coal, capacity), capacity);

    Bake(oven, Cycle * 0.4f);

    Assert.All(BedsOf(oven, west), bed => Assert.Equal(Coke, bed.FuelCode));
    Assert.All(
      BedsOf(oven, east),
      bed => Assert.Equal(Bituminous, bed.FuelCode)
    );
  }

  /// <summary>
  /// A chamber's clock does not carry across charges. Bake the west most of the way, empty it, and the
  /// next charge starts from zero - otherwise a player could bank time by loading and drawing repeatedly.
  /// </summary>
  [Fact]
  public void An_emptied_chamber_loses_the_time_it_had_banked() {
    int capacity = BEBehaviorFirebox.CellCapacity;
    (BlockEntityCokeOven oven, _) = Charged(
      capacity,
      Bituminous,
      cell => cell.X < Anchor.X
    );
    var west = oven.Chambers[0];

    Bake(oven, Cycle * 0.9f);
    Assert.True(oven.BakedSeconds(0) > 0f);

    foreach (BEBehaviorFirebox bed in BedsOf(oven, west))
      bed.Clear();
    Bake(oven, 60f);

    Assert.Equal(0f, oven.BakedSeconds(0));
  }

  #endregion

  #region Going out

  /// <summary>
  /// A sealed chamber does not burn its charge out. The branch's burn-out keeps only a fraction of the bed
  /// as salvage, which is right where the bed is fuel and destroys the work here - and it is reachable,
  /// because an oven that never reaches its light temperature extinguishes on the fuel clock.
  /// </summary>
  [Fact]
  public void Going_out_leaves_the_charge_where_it_was() {
    int capacity = BEBehaviorFirebox.CellCapacity;
    (BlockEntityCokeOven oven, _) = Charged(capacity);

    ReflectionHelpers.Invoke(oven, "BurnOutCharge", []);

    foreach (BEBehaviorFirebox bed in BedsOf(oven, oven.Chambers[0])) {
      Assert.Equal(Bituminous, bed.FuelCode);
      Assert.Equal(capacity, bed.Units);
    }
  }

  #endregion
}
