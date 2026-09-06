using System.Linq;
using ExpandedLib.Machines;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Industry.Heat;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.Items;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The crucible furnace's rhythm: the damper is thrown once, in the middle of a heat. Shut, it brings cold
/// pots up gently; open, it gives the full draught that melts them - and destroys any pot that has not
/// come up yet. Both directions are pinned, because getting the polarity backwards is a machine that
/// either never melts or never keeps a pot.
/// </summary>
public class CrucibleFurnaceTests {
  #region Harness

  private static readonly BlockPos Anchor = new(0, 16, 0);

  private static ExBlockDef Def =>
    BlockCrucibleFurnaceCore.Definitions("iiex").Single();

  /// <summary>A furnace stood at north, with a real hearth in its one firebox cell.</summary>
  private static (
    BlockEntityCrucibleFurnace Furnace,
    BlockEntityCrucibleHearth Hearth,
    StructureRig Rig
  ) Stood(int fuel = 12) {
    var furnace = new BlockEntityCrucibleFurnace();
    StructureRig rig = Stand(
      furnace,
      Def,
      Anchor,
      "iiex:furnace-cruciblecore-tier1",
      "north"
    );
    SeatCrucibleHearth(rig, furnace, fuel);
    rig.World.RegisterItem(BlisterBreaking.ChunkCode);
    rig.World.RegisterItem(BlisterBreaking.BitCode);
    rig.World.RegisterItem("iiex:ingot-cruciblesteel");
    rig.World.Register(
      TestBlocks.Configure(
        new BlockSteelCrucible(),
        "iiex:steelcrucible-burned",
        910,
        ("type", "burned")
      )
    );
    rig.World.Register(
      TestBlocks.Configure(
        new BlockSteelCruciblePour(),
        "iiex:steelcrucible-smelted",
        911,
        ("type", "smelted")
      )
    );
    return (furnace, furnace.Hearth!, rig);
  }

  /// <summary>Throws the damper, by putting a real cap in the cell the drawing marks for one.</summary>
  private static BlockEntityPuddlingChimneyCap Damper(
    StructureRig rig,
    BlockEntityCrucibleFurnace furnace,
    bool open
  ) {
    BlockPos cell = furnace.CellsWithRole(FurnaceCellRoles.Damper).Single();
    var cap = new BlockEntityPuddlingChimneyCap { Pos = cell.Copy() };
    rig.Occupy(
      cell,
      TestBlocks.Configure(
        new Block(),
        "iiex:furnace-puddlingchimneycap-s",
        906,
        ("side", "south")
      ),
      cap
    );
    if (open)
      cap.Toggle();
    return cap;
  }

  /// <summary>A pot in every hole, each charged full.</summary>
  private static void Load(
    BlockEntityCrucibleHearth hearth,
    StructureRig rig,
    int pots = 4
  ) {
    Block pot = rig.World.World.GetBlock(
      new AssetLocation("iiex:steelcrucible-burned")
    )!;
    Item chunk = rig.World.World.GetItem(
      new AssetLocation(BlisterBreaking.ChunkCode)
    )!;
    Item bit = rig.World.World.GetItem(
      new AssetLocation(BlisterBreaking.BitCode)
    )!;
    for (int i = 0; i < pots; i++) {
      hearth.Seat(new ItemStack(pot));
      hearth.Charge(new ItemStack(chunk, 4));
      hearth.Charge(new ItemStack(bit, 2));
    }
  }

  #endregion

  #region The temperature gate

  /// <summary>
  /// The one thing that decides whether the machine works at all: at the chimney it is rated for, with the
  /// damper open, a full hearth clears 1600 C. B15 and B2 are the same failure - a furnace that lights,
  /// holds and never crosses its own process line.
  /// </summary>
  [Fact]
  public void At_its_rated_chimney_the_furnace_clears_its_process_temperature() {
    (BlockEntityCrucibleFurnace furnace, _, _) = Stood();
    int capacity = (int)
      ReflectionHelpers.GetProperty(furnace, "ChargeCapacityUnits")!;

    float best = Settles(furnace, capacity, furnace.BestNaturalDraught);

    Assert.True(
      best >= IiexValues.CrucibleMeltingPointC,
      $"a rated stack settles at {best} C against a process of "
        + $"{IiexValues.CrucibleMeltingPointC} C"
    );
  }

  /// <summary>
  /// And it is the chimney that carries it there, not the losses on their own. A furnace with no stack
  /// built is well short, which is what makes height the machine's temperature dial rather than decoration.
  /// </summary>
  [Fact]
  public void With_no_chimney_built_the_furnace_falls_short() {
    (BlockEntityCrucibleFurnace furnace, _, _) = Stood();
    int capacity = (int)
      ReflectionHelpers.GetProperty(furnace, "ChargeCapacityUnits")!;

    float bare = Settles(
      furnace,
      capacity,
      StackDraught.NaturalDraughtFor(2, damperOpen: true)
    );

    Assert.True(bare < IiexValues.CrucibleMeltingPointC);
  }

  /// <summary>
  /// A shut damper is a cooler fire, which is what makes the preheat gentle. The polarity was ruled the
  /// other way once and inverted: a crucible furnace melts at full draught, so shutting the damper has to
  /// cost temperature rather than gain it.
  /// </summary>
  [Fact]
  public void A_shut_damper_is_a_cooler_fire() {
    (BlockEntityCrucibleFurnace furnace, _, _) = Stood();
    int capacity = (int)
      ReflectionHelpers.GetProperty(furnace, "ChargeCapacityUnits")!;
    int rated = furnace.RatedStackCourses;

    float open = Settles(
      furnace,
      capacity,
      StackDraught.NaturalDraughtFor(rated, damperOpen: true)
    );
    float shut = Settles(
      furnace,
      capacity,
      StackDraught.NaturalDraughtFor(rated, damperOpen: false)
    );

    Assert.True(shut < open);
    Assert.True(shut < IiexValues.CrucibleMeltingPointC);
  }

  private static float Settles(
    BlockEntityCrucibleFurnace furnace,
    int units,
    float draught
  ) =>
    (
      (HeatBalance)
        ReflectionHelpers.Invoke(
          furnace,
          "ComputeHeatBalanceAt",
          new BurdenMix(0f, 0f, units),
          0f,
          20f,
          units,
          draught
        )!
    ).TProcess;

  #endregion

  #region The damper

  /// <summary>
  /// A damper reads shut until a player throws it, and the furnace reads it off the cell the drawing marks
  /// rather than off the branch's cap - which sits over the highest flue course, where this furnace has
  /// only the player's chimney.
  /// </summary>
  [Fact]
  public void The_furnace_reads_the_damper_the_drawing_marks() {
    (BlockEntityCrucibleFurnace furnace, _, StructureRig rig) = Stood();

    Assert.Null(furnace.Damper);
    Assert.False((bool)ReflectionHelpers.GetProperty(furnace, "DamperOpen")!);

    BlockEntityPuddlingChimneyCap cap = Damper(rig, furnace, open: true);

    Assert.Equal(cap, furnace.Damper);
    Assert.True((bool)ReflectionHelpers.GetProperty(furnace, "DamperOpen")!);
  }

  #endregion

  #region Preheat and the crack

  /// <summary>
  /// With the damper shut the pots come up, and nothing breaks. This is the phase the whole rhythm exists
  /// for: one deliberate act in the middle of a heat rather than a setting left alone.
  /// </summary>
  [Fact]
  public void A_shut_damper_brings_the_pots_up_and_breaks_nothing() {
    (_, BlockEntityCrucibleHearth hearth, StructureRig rig) = Stood();
    Load(hearth, rig);

    hearth.FireTick(IiexValues.CruciblePreheatSec, damperOpen: false);

    Assert.All(hearth.Holes, h => Assert.True(h.Occupied));
    Assert.All(hearth.Holes, h => Assert.True(h.Preheated));
  }

  /// <summary>
  /// And the other direction, which is the whole cost of getting it wrong: full draught taken on a cold pot
  /// destroys it. Deterministic, because the player controls the one input that decides it.
  /// </summary>
  [Fact]
  public void Full_draught_on_a_cold_pot_breaks_it() {
    (_, BlockEntityCrucibleHearth hearth, StructureRig rig) = Stood();
    Load(hearth, rig);

    hearth.FireTick(1f, damperOpen: true);

    Assert.All(hearth.Holes, h => Assert.False(h.Occupied));
  }

  /// <summary>
  /// A pot that has come up takes the full fire without complaint - otherwise the machine could never be
  /// run at all.
  /// </summary>
  [Fact]
  public void A_preheated_pot_takes_the_full_draught() {
    (_, BlockEntityCrucibleHearth hearth, StructureRig rig) = Stood();
    Load(hearth, rig);

    hearth.FireTick(IiexValues.CruciblePreheatSec, damperOpen: false);
    hearth.FireTick(1f, damperOpen: true);

    Assert.All(hearth.Holes, h => Assert.True(h.Occupied));
  }

  /// <summary>
  /// The charge survives the pot. A cracked pot spills what it was holding one block down into the ash pit
  /// as items the player can pick up - a silent void here is the puddling hearth's own mistake repeated at
  /// four times the scale.
  /// </summary>
  [Fact]
  public void A_cracked_pot_spills_its_charge_into_the_pit() {
    (_, BlockEntityCrucibleHearth hearth, StructureRig rig) = Stood();
    Load(hearth, rig, pots: 1);

    hearth.FireTick(1f, damperOpen: true);

    int units = rig.World.Drops.Sum(s =>
      s.Collectible.Code?.ToString() == BlisterBreaking.ChunkCode
        ? s.StackSize * BlisterBreaking.ChunkUnits
        : s.StackSize * BlisterBreaking.BitUnits
    );

    Assert.Equal(IiexValues.CruciblePotChargeUnits, units);
    // Into the pit, not onto the floor beside it: the ash pit is the cellar the design gives it.
    Assert.All(SpawnPositions(rig), p => Assert.True(p.Y < hearth.Pos.Y));
  }

  #endregion

  #region The heat

  /// <summary>
  /// 110 u of blister steel in, 100 u of crucible steel out, and the loss is paid once. A melt loss applied
  /// per cycle rather than per heat would eat the charge to nothing over a long melt.
  /// </summary>
  [Fact]
  public void A_heat_pays_its_melt_loss_once() {
    (_, BlockEntityCrucibleHearth hearth, StructureRig rig) = Stood();
    Load(hearth, rig, pots: 1);
    hearth.FireTick(IiexValues.CruciblePreheatSec, damperOpen: false);

    for (
      float t = 0;
      t < IiexValues.CrucibleMeltSec * 2;
      t += IiexValues.CrucibleMeltIntervalSec
    )
      hearth.MeltStep(IiexValues.CrucibleMeltIntervalSec);

    Assert.Equal(IiexValues.CruciblePotYieldUnits, hearth.Holes[0].Metal);
    Assert.Equal(0, hearth.Holes[0].Charge);
  }

  /// <summary>
  /// Four pots are four heats. A pot seated after the others finishes after them rather than riding their
  /// progress, which is what makes the bank a throughput axis instead of a free multiplier.
  /// </summary>
  [Fact]
  public void Four_pots_are_four_independent_heats() {
    (_, BlockEntityCrucibleHearth hearth, StructureRig rig) = Stood();
    Load(hearth, rig, pots: 3);
    hearth.FireTick(IiexValues.CruciblePreheatSec, damperOpen: false);

    // The three run most of the way through, then a fourth is seated and charged late.
    hearth.MeltStep(IiexValues.CrucibleMeltSec - 1f);
    Load(hearth, rig, pots: 1);
    hearth.FireTick(IiexValues.CruciblePreheatSec, damperOpen: false);
    hearth.MeltStep(1f);

    Assert.Equal(3, hearth.Holes.Count(h => h.Molten));
    Assert.False(hearth.Holes[3].Molten);
    Assert.True(hearth.Holes[3].Charged);
  }

  /// <summary>
  /// A second heat runs in the same pot: the melt clock resets with the charge, so a re-charged pot is not
  /// already most of the way through its next heat.
  /// </summary>
  [Fact]
  public void A_recharged_pot_starts_its_next_heat_from_nothing() {
    (_, BlockEntityCrucibleHearth hearth, StructureRig rig) = Stood();
    Load(hearth, rig, pots: 1);
    hearth.FireTick(IiexValues.CruciblePreheatSec, damperOpen: false);
    hearth.MeltStep(IiexValues.CrucibleMeltSec);

    Assert.True(hearth.Holes[0].Molten);
    hearth.Pull();
    Load(hearth, rig, pots: 1);

    hearth.MeltStep(1f);

    Assert.False(hearth.Holes[0].Molten);
  }

  #endregion

  #region The chimney

  /// <summary>
  /// The stack is counted off the world, course by course, starting above the last one the drawing itself
  /// declares. Height is the machine's temperature dial and the layout cannot express it, so the walk is
  /// the mechanism.
  /// </summary>
  [Fact]
  public void The_chimney_counts_the_courses_the_player_built() {
    (BlockEntityCrucibleFurnace furnace, _, StructureRig rig) = Stood();

    Assert.Equal(0, furnace.WalkChimney());

    BuildChimney(rig, furnace, 4);

    Assert.Equal(4, furnace.WalkChimney());
  }

  /// <summary>
  /// A gap breaks the count rather than being stepped over. A leaking flue has no draught, and a walk that
  /// jumped the hole would credit a chimney the player has not finished.
  /// </summary>
  [Fact]
  public void A_gap_in_the_chimney_ends_the_count() {
    (BlockEntityCrucibleFurnace furnace, _, StructureRig rig) = Stood();
    BuildChimney(rig, furnace, 4);

    // Knock one brick out of the third course.
    BlockPos top = furnace
      .CellsWithRole(FurnaceCellRoles.Flue)
      .Aggregate((a, b) => b.Y > a.Y ? b : a);
    rig.World.Place(
      top.UpCopy().UpCopy().UpCopy().AddCopy(BlockFacing.NORTH),
      rig.World.Air
    );

    Assert.Equal(2, furnace.WalkChimney());
  }

  /// <summary>Where every item this world has spawned was dropped.</summary>
  private static System.Collections.Generic.List<Vec3d> SpawnPositions(
    StructureRig rig
  ) =>
    [
      .. rig
        .World.World.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == "SpawnItemEntity")
        .Select(c => (Vec3d)c.GetArguments()[1]!),
    ];

  /// <summary>
  /// A course whose middle is bricked up is not a course either - a chimney is a hole with a wall round
  /// it, and a solid one carries nothing.
  /// </summary>
  [Fact]
  public void A_blocked_flue_ends_the_count() {
    (BlockEntityCrucibleFurnace furnace, _, StructureRig rig) = Stood();
    BuildChimney(rig, furnace, 4);

    BlockPos top = furnace
      .CellsWithRole(FurnaceCellRoles.Flue)
      .Aggregate((a, b) => b.Y > a.Y ? b : a);
    rig.World.Place(
      top.UpCopy().UpCopy().UpCopy(),
      TestBlocks.Configure(
        new Block(),
        "game:brickcourse-four-running-black",
        907
      )
    );

    Assert.Equal(2, furnace.WalkChimney());
  }

  /// <summary>
  /// And the count actually reaches the draught. The walk is only worth having if what it counts is what
  /// the heat balance reads, and the two are wired by one line that nothing else exercises.
  /// </summary>
  [Fact]
  public void The_counted_chimney_is_what_the_draught_reads() {
    (BlockEntityCrucibleFurnace furnace, _, StructureRig rig) = Stood();
    int drawn = (int)ReflectionHelpers.GetProperty(furnace, "StackCourses")!;
    BuildChimney(rig, furnace, 4);

    furnace.GetBehavior<BEBehaviorProductionMachine>().DriveProductionTick(1f);

    Assert.Equal(
      drawn + 4,
      (int)ReflectionHelpers.GetProperty(furnace, "StackCourses")!
    );
  }

  private static void BuildChimney(
    StructureRig rig,
    BlockEntityCrucibleFurnace furnace,
    int courses
  ) {
    var brick = TestBlocks.Configure(
      new Block(),
      "game:brickcourse-four-running-black",
      907
    );
    BlockPos above = furnace
      .CellsWithRole(FurnaceCellRoles.Flue)
      .Aggregate((a, b) => b.Y > a.Y ? b : a)
      .UpCopy();

    for (int i = 0; i < courses; i++) {
      foreach (BlockFacing side in BlockFacing.HORIZONTALS)
        rig.World.Place(above.AddCopy(side), brick);
      above = above.UpCopy();
    }
  }

  #endregion

  #region The readout

  /// <summary>
  /// R7, and the one line the machine cannot be run without: the chimney's course count, what it is
  /// pulling, and which way a taller one would move it. A draught that declines past the peak is
  /// indistinguishable from a bug unless the peak is named.
  /// </summary>
  [Theory]
  [InlineData(0, "cruciblefurnace-stack-rising")]
  [InlineData(7, "cruciblefurnace-stack-peak")]
  [InlineData(12, "cruciblefurnace-stack-past")]
  public void The_readout_names_the_peak_and_the_direction(
    int built,
    string key
  ) {
    TestLang.Init();
    (BlockEntityCrucibleFurnace furnace, _, StructureRig rig) = Stood();
    BuildChimney(rig, furnace, built);
    ReflectionHelpers.SetField(furnace, "_builtCourses", furnace.WalkChimney());

    var sb = new System.Text.StringBuilder();
    ReflectionHelpers.Invoke(furnace, "AppendHeatExtras", sb);

    Assert.Contains(key, sb.ToString());
  }

  /// <summary>
  /// And the damper, which is invisible from outside the furnace and is the difference between a heat and
  /// four broken pots.
  /// </summary>
  [Fact]
  public void The_readout_says_which_way_the_damper_stands() {
    TestLang.Init();
    (BlockEntityCrucibleFurnace furnace, _, StructureRig rig) = Stood();

    var shut = new System.Text.StringBuilder();
    ReflectionHelpers.Invoke(furnace, "AppendHeatExtras", shut);
    Assert.Contains("cruciblefurnace-damper-preheat", shut.ToString());

    Damper(rig, furnace, open: true);
    var open = new System.Text.StringBuilder();
    ReflectionHelpers.Invoke(furnace, "AppendHeatExtras", open);
    Assert.Contains("cruciblefurnace-damper-melt", open.ToString());
  }

  #endregion

  #region Absences

  /// <summary>
  /// A crucible melt makes essentially no slag, and nothing here quietly accumulates any: the molten-slag
  /// path belongs to the shaft branch, which this furnace is not on, so its slag readout is the empty base.
  /// </summary>
  /// <remarks>
  /// Stated structurally rather than by reading a counter, because there is no counter to read - the field
  /// the design worried about lives on <c>BlockEntityShaftFurnace</c>. Re-parenting this furnace onto that
  /// branch would fail here, which is the only way the worry could come back.
  /// </remarks>
  [Fact]
  public void The_furnace_is_not_on_the_branch_that_accumulates_slag() {
    (
      BlockEntityCrucibleFurnace furnace,
      BlockEntityCrucibleHearth hearth,
      StructureRig rig
    ) = Stood();
    Load(hearth, rig, pots: 1);
    hearth.FireTick(IiexValues.CruciblePreheatSec, damperOpen: false);
    hearth.MeltStep(IiexValues.CrucibleMeltSec);

    Assert.True(hearth.Holes[0].Molten);
    Assert.IsNotAssignableFrom<BlockEntityShaftFurnace>(furnace);

    var sb = new System.Text.StringBuilder();
    furnace.AppendMoltenSlagInfo(sb);
    Assert.Equal(string.Empty, sb.ToString());
  }

  #endregion
}
