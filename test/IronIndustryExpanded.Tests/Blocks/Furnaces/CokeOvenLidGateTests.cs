using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Helpers;
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
/// The seal. Coking is destructive distillation, so a chamber open to the air bakes nothing - and this is
/// where <c>BlockEntityChargeDoor.IsVenting</c> gets its first consumer anywhere in <c>src/</c>.
/// <para>
/// Both halves are pinned on purpose. A suite that only asserts "a shut chamber cokes" leaves the gate
/// free to be deleted; a suite that only asserts "an open one does not" would pass on an oven that never
/// cokes at all.
/// </para>
/// </summary>
public class CokeOvenLidGateTests {
  #region Harness

  private static readonly BlockPos Anchor = new(0, 16, 0);

  private const string Bituminous = "game:ore-bituminouscoal";
  private const string Coke = "game:coke";

  /// <summary>The lids in the drawing's own frame, from the shared rig rather than from
  /// <c>BlockEntityCokeOven</c>: an expectation computed from its subject agrees with any value of
  /// it.</summary>
  private static Vec3i[] Lids => CokeOvenLids;

  /// <summary>
  /// A charged oven with real charge doors in all four closure cells, so the seal has something to read.
  /// The rig's stand-ins satisfy the layout but carry no block entity, and a chamber whose lid resolves to
  /// nothing is treated as open - which is correct, and would make every case here pass for the wrong
  /// reason.
  /// </summary>
  private static (
    BlockEntityCokeOven Oven,
    StructureRig Rig,
    List<BlockEntityChargeDoor> Closures
  ) Stood(
    int unitsPerCell = 12,
    string side = "north",
    System.Func<Vec3i, bool>? place = null
  ) {
    var oven = new BlockEntityCokeOven();
    StructureRig rig = Stand(
      oven,
      BlockCokeOvenCore.Definitions("iiex").Single(),
      Anchor,
      "iiex:furnace-cokeovencore",
      side,
      complete: false
    );

    List<BlockEntityChargeDoor> closures = CloseCokeOven(rig, side, place);

    rig.Complete();
    rig.World.RegisterItem(Coke);
    LoadFireboxes(rig, oven, unitsPerCell, Bituminous);
    return (oven, rig, closures);
  }

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

  #region The gate

  /// <summary>
  /// A shut chamber cokes. The other half of the pair below, and the premise of every case here: the
  /// closures resolve and the seal reads true to begin with.
  /// </summary>
  [Fact]
  public void A_shut_chamber_bakes() {
    (BlockEntityCokeOven oven, _, _) = Stood();

    Assert.All(oven.Chambers, c => Assert.True(oven.Sealed(c)));

    Bake(oven, Cycle);

    Assert.All(
      oven.Chambers.SelectMany(c => BedsOf(oven, c)),
      bed => Assert.Equal(Coke, bed.FuelCode)
    );
  }

  /// <summary>
  /// A chamber with its crown lid open bakes nothing, however long it stands. Coal needs heating out of
  /// contact with air, and the lid is the one part of the crown that opens.
  /// </summary>
  [Fact]
  public void An_open_lid_stops_the_bake() {
    (BlockEntityCokeOven oven, StructureRig rig, _) = Stood();
    var west = oven.Chambers[0];

    LidOf(oven, rig, west).ToggleMain();
    Assert.False(oven.Sealed(west));

    Bake(oven, Cycle * 2f);

    Assert.All(
      BedsOf(oven, west),
      bed => Assert.Equal(Bituminous, bed.FuelCode)
    );
  }

  /// <summary>
  /// The drawing door counts too. A chamber sealed above and open at the front is no more sealed than one
  /// open above, and a gate that read only the lid would let a player bake with the door standing wide.
  /// </summary>
  [Fact]
  public void An_open_drawing_door_stops_the_bake() {
    (BlockEntityCokeOven oven, StructureRig rig, _) = Stood();
    var west = oven.Chambers[0];

    DoorOf(oven, rig, west).ToggleMain();
    Assert.False(oven.Sealed(west));

    Bake(oven, Cycle * 2f);

    Assert.All(
      BedsOf(oven, west),
      bed => Assert.Equal(Bituminous, bed.FuelCode)
    );
  }

  /// <summary>
  /// Shutting the lid again resumes the bake rather than restarting it. The clock is held while a chamber
  /// stands open, not lost: opening the crown to look is a mistake a player should be able to correct.
  /// </summary>
  [Fact]
  public void Shutting_the_lid_resumes_the_bake_where_it_stopped() {
    (BlockEntityCokeOven oven, StructureRig rig, _) = Stood();
    var west = oven.Chambers[0];
    BlockEntityChargeDoor lid = LidOf(oven, rig, west);

    Bake(oven, Cycle * 0.8f);
    lid.ToggleMain();
    Bake(oven, Cycle * 2f);

    // Still raw after two further cycles, and the time already put in has not been thrown away.
    Assert.Equal(Bituminous, BedsOf(oven, west)[0].FuelCode);
    float banked = oven.BakedSeconds(0);
    Assert.True(
      banked > 0f,
      "an open chamber holds its clock rather than losing it"
    );

    lid.ToggleMain();
    Bake(oven, Cycle * 0.3f);

    Assert.Equal(Coke, BedsOf(oven, west)[0].FuelCode);
  }

  /// <summary>
  /// One chamber's lid is not the other's. Opening the west leaves the east baking, which is the whole
  /// point of a bank of two: draw one side while the other works.
  /// </summary>
  [Fact]
  public void Opening_one_chamber_leaves_the_other_baking() {
    (BlockEntityCokeOven oven, StructureRig rig, _) = Stood();
    var west = oven.Chambers[0];
    var east = oven.Chambers[1];

    LidOf(oven, rig, west).ToggleMain();
    Bake(oven, Cycle);

    Assert.All(
      BedsOf(oven, west),
      bed => Assert.Equal(Bituminous, bed.FuelCode)
    );
    Assert.All(BedsOf(oven, east), bed => Assert.Equal(Coke, bed.FuelCode));
  }

  /// <summary>
  /// A chamber whose lid has been broken out is open, not sealed. A hole in the crown seals no better than
  /// an open lid, and treating a missing closure as shut would let a player skip the gate by demolishing
  /// it.
  /// </summary>
  [Fact]
  public void A_chamber_with_no_lid_is_not_sealed() {
    // The east chamber's closures are placed; the west's cells carry the block the layout wants but no
    // block entity, which is what a broken and replaced lid looks like from the core's side.
    (BlockEntityCokeOven oven, _, _) = Stood(place: local => local.X > 0);

    Assert.False(oven.Sealed(oven.Chambers[0]));
    Assert.True(oven.Sealed(oven.Chambers[1]));

    Bake(oven, Cycle);

    Assert.All(
      BedsOf(oven, oven.Chambers[0]),
      bed => Assert.Equal(Bituminous, bed.FuelCode)
    );
    Assert.All(
      BedsOf(oven, oven.Chambers[1]),
      bed => Assert.Equal(Coke, bed.FuelCode)
    );
  }

  #endregion

  #region Which closure belongs to which chamber

  /// <summary>
  /// The core finds its lids, never the reverse. A lid stands three courses above the anchor and
  /// <c>ComponentScanAbove</c> reaches one, so a lid asked to resolve its own core finds nothing - the
  /// same trap the reheat furnace's hearth link fell into. Pinned so a later refactor cannot quietly
  /// invert the direction.
  /// </summary>
  [Fact]
  public void A_lid_stands_further_above_the_core_than_a_part_can_scan() {
    Assert.True(
      Lids[0].Y > BlockEntityFurnaceCore.ComponentScanAbove,
      "if this ever stops being true the direction of resolution is a free choice again"
    );
  }

  /// <summary>
  /// Each chamber gets the closures on its own side, at every facing. The pairing is by proximity rather
  /// than by a table per facing, so a rotation that crossed the wires would show up here as a west chamber
  /// gated by the east lid.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void Each_chamber_gets_its_own_lid_and_door(string side) {
    (BlockEntityCokeOven oven, _, _) = Stood(side: side);

    var pairs = oven.Chambers.Select(c => oven.ClosuresOf(c)).ToList();

    Assert.All(pairs, p => Assert.NotNull(p.Lid));
    Assert.All(pairs, p => Assert.NotNull(p.Door));
    // Two chambers, four distinct closures: no lid or door serves both.
    Assert.Equal(2, pairs.Select(p => p.Lid!.Pos).Distinct().Count());
    Assert.Equal(2, pairs.Select(p => p.Door!.Pos).Distinct().Count());
  }

  #endregion

  #region Drops

  /// <summary>
  /// Breaking the core does not eat what is in the chambers. The chamber cells hold real firebox blocks
  /// rather than fillers, so they drop themselves - but nothing said so, and the puddling and reheat
  /// hearths' silent-destruction hole is exactly this shape.
  /// </summary>
  [Fact]
  public void Breaking_the_core_leaves_the_chambers_where_they_stand() {
    (BlockEntityCokeOven oven, StructureRig rig, _) = Stood();
    var cells = oven.Chambers.SelectMany(c => c).ToList();

    oven.OnBlockBroken();

    foreach (BlockPos cell in cells) {
      BEBehaviorFirebox? bed = rig
        .World.World.BlockAccessor.GetBlockEntity(cell)
        ?.GetBehavior<BEBehaviorFirebox>();
      Assert.NotNull(bed);
      Assert.Equal(Bituminous, bed!.FuelCode);
      Assert.Equal(BEBehaviorFirebox.CellCapacity, bed.Units);
    }
  }

  #endregion

  #region Helpers

  private static BlockEntityChargeDoor LidOf(
    BlockEntityCokeOven oven,
    StructureRig rig,
    IReadOnlyList<BlockPos> chamber
  ) => oven.ClosuresOf(chamber).Lid!;

  private static BlockEntityChargeDoor DoorOf(
    BlockEntityCokeOven oven,
    StructureRig rig,
    IReadOnlyList<BlockPos> chamber
  ) => oven.ClosuresOf(chamber).Door!;

  #endregion
}
