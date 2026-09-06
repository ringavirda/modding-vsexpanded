using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// The hot blast furnace's crucible is the last drawing where <see cref="FurnaceCellRoles.Pool"/> and
/// <see cref="FurnaceCellRoles.Chargeable"/> share cells, and this file pins the behaviour that follows from it -
/// which is known-wrong, not intended. The two iwex shafts separated the roles when their crucible became
/// live molten cells; this one holds because dropping <c>Chargeable</c> alone would leave a two-cell
/// crucible under a 3x3 shaft, a bosh no furnace has, and because the hearth's real defect is a cinder
/// notch a course too high. Crucible, notch and tuyeres move together in the smex remake, in one layout
/// change and one blessing.
/// <para>
/// So these cases are a countdown, not a contract: the day that remake starts they fail, and the failure
/// names exactly what changed. Do not "fix" them by editing the expectations - the fix is the layout, and
/// then this file goes. Nothing here touches a golden.
/// </para>
/// </summary>
public class CrucibleOverlapTests {
  /// <summary>Seconds allowed for a charged, blown furnace to warm through and melt, as
  /// <c>BlastFurnaceScenarioTests</c> allows.</summary>
  private const int SoakSeconds = 900;

  /// <summary>The furnace's own quantum, <c>protected</c> on the core.</summary>
  private static int UnitsPerBlock(BlockEntityFurnaceCore be) =>
    (int)ReflectionHelpers.GetProperty(be, "ChargeUnitsPerBlock")!;

  private static int FloorY(BlockEntityFurnaceCore be, int x, int z) =>
    (int)ReflectionHelpers.Invoke(be, "ColumnFloorY", x, z)!;

  [Fact]
  public void The_hot_furnaces_crucible_is_still_burden_and_pool_at_once() {
    var rig = new BlastFurnaceRig();

    List<BlockPos> pool = [.. rig.Furnace.PoolCells];
    Assert.Equal(2, pool.Count);
    Assert.All(pool, c => Assert.Contains(c, rig.Furnace.ChargeableCells));

    // The two cells, named: the crucible course is y=1 and the shaft starts there rather than above it.
    Assert.Equal(
      new[] { new Vec3i(0, 1, 0), new Vec3i(1, 1, 0) },
      pool.Select(rig.Furnace.LocalOf).OrderBy(c => c.X).ToArray()
    );

    // ...and so two of its nine columns floor on a cell the pool owns, where the other seven start at 2.
    Assert.Equal(1, FloorY(rig.Furnace, 0, 0));
    Assert.Equal(1, FloorY(rig.Furnace, 1, 0));
    Assert.Equal(2, FloorY(rig.Furnace, -1, -1));
  }

  /// <summary>
  /// The first consequence, driven rather than reasoned about: once the bath stands, the guard in
  /// <c>SyncChargeBlocks</c> refuses the cell for the whole campaign, so those two columns draw one block
  /// fewer than the charge they hold calls for. Every other column draws its full height from the same
  /// charge, which is what makes this the overlap and not a shaft that is simply short.
  /// </summary>
  [Fact]
  public void While_the_bath_stands_the_two_crucible_columns_draw_a_block_short() {
    // Charged to capacity and blown, with no tap: the bath has to stand in the crucible for the whole
    // case, and a tap would drain it out from under the assertion.
    var rig = new BlastFurnaceRig(blastMix: 0).FeedBlast();
    Assert.True(
      rig.RunUntil(s => s.MoltenIron > 0f, SoakSeconds) > 0,
      "the furnace should have melted something"
    );

    // The bath is standing in both crucible cells - the premise, without which the counts below say
    // nothing.
    foreach (Vec3i local in new[] { new Vec3i(0, 1, 0), new Vec3i(1, 1, 0) })
      Assert.Equal(
        "iiex:hearthmetal-pigiron",
        rig.World.GetBlock(rig.Structure.Cell(local.X, local.Y, local.Z))
          .Code?.ToString()
      );

    int perBlock = UnitsPerBlock(rig.Furnace);
    int checkedCrucible = 0;
    int checkedOther = 0;
    foreach (var (x, z) in rig.Furnace.ShaftColumns.Keys) {
      ChargeColumn column = rig.Furnace.ChargeColumnAt(x, z)!;
      if (column.TotalUnits == 0)
        continue;

      int want = ChargeColumn.BlocksTall(column.TotalUnits, perBlock);
      int drawn = PilesIn(rig, x, z);
      bool crucible = z == 0 && (x == 0 || x == 1);
      if (crucible)
        checkedCrucible++;
      else
        checkedOther++;

      Assert.Equal(
        $"({x},{z}): {(crucible ? want - 1 : want)} drawn",
        $"({x},{z}): {drawn} drawn"
      );
    }

    // Both halves of the comparison really ran: the loop skips empty columns, so without this a spent
    // shaft passes by checking nothing.
    Assert.Equal(2, checkedCrucible);
    Assert.True(
      checkedOther > 0,
      "at least one column off the crucible must have been checked, or there is nothing to contrast with"
    );
  }

  /// <summary>
  /// The second consequence: the block the column cannot draw is not a block it does not hold. The units
  /// stay in the column and go on counting toward <c>ShaftChargeUnits</c>, so the hopper readout and the
  /// shaft a player can see disagree by up to one block per crucible column for the whole campaign.
  /// </summary>
  [Fact]
  public void The_units_the_crucible_hides_still_count_toward_the_readout() {
    var rig = new BlastFurnaceRig(blastMix: 0).FeedBlast();
    Assert.True(rig.RunUntil(s => s.MoltenIron > 0f, SoakSeconds) > 0);

    int perBlock = UnitsPerBlock(rig.Furnace);
    int drawn = 0;
    int want = 0;
    int held = 0;
    foreach (var (x, z) in rig.Furnace.ShaftColumns.Keys) {
      ChargeColumn column = rig.Furnace.ChargeColumnAt(x, z)!;
      held += column.TotalUnits;
      want += ChargeColumn.BlocksTall(column.TotalUnits, perBlock);
      drawn += PilesIn(rig, x, z);
    }

    Assert.True(held > 0, "the shaft should still be holding charge");
    // The readout counts every unit, including the ones no block can draw.
    Assert.Equal(held, rig.Furnace.ShaftChargeUnits);
    // Two blocks' worth of it stands in no block at all - one per crucible column.
    Assert.Equal(2, want - drawn);
  }

  /// <summary>Charge piles standing in one column, counted off the world rather than off the machine.</summary>
  private static int PilesIn(BlastFurnaceRig rig, int x, int z) {
    int count = 0;
    foreach (BlockPos cell in rig.Furnace.ChargeableCells) {
      Vec3i local = rig.Furnace.LocalOf(cell);
      if (local.X != x || local.Z != z)
        continue;
      if (
        rig.World.GetBlock(cell)?.Code?.ToShortString()
        == BlockChargePile.PileCode.ToShortString()
      )
        count++;
    }
    return count;
  }
}
