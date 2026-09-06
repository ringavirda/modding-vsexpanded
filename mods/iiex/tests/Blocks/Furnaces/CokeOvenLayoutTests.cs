using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.MathTools;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The coke oven's drawing: two chambers either side of a shared wall, and the first furnace in the family
/// whose fuel cells are not one solid cuboid. Everything downstream reads the chambers off this layout, so
/// the cases here pin the shape rather than any behaviour on top of it.
/// </summary>
public class CokeOvenLayoutTests {
  #region Harness

  private static readonly BlockPos Anchor = new(0, 16, 0);

  private static ExBlockDef Def =>
    BlockCokeOvenCore.Definitions("iiex").Single();

  /// <summary>
  /// The oven stood at <paramref name="side"/>, completing itself from a raised footprint. Nothing forces
  /// <c>StructureComplete</c>: a drawing whose parts cannot be satisfied at some facing shows up here as a
  /// machine that never completes, which is the failure the four-sided theory is for.
  /// </summary>
  private static (BlockEntityCokeOven Oven, StructureRig Rig) Stood(string side) {
    var oven = new BlockEntityCokeOven();
    StructureRig rig = Stand(
      oven,
      Def,
      Anchor,
      "iiex:furnace-cokeovencore",
      side
    );
    return (oven, rig);
  }

  /// <summary>The chamber cells in the authored frame, read off the drawing rather than off the def, so an
  /// edit to the layout has to be made here too rather than silently agreeing with itself.</summary>
  private static IEnumerable<Vec3i> AuthoredChambers() {
    foreach (int x in new[] { -3, -2, -1, 1, 2, 3 })
      foreach (int z in new[] { -1, 0 })
        yield return new Vec3i(x, 1, z);
  }

  #endregion

  #region The drawing

  /// <summary>
  /// Twelve chamber cells, six a side, all marked <c>Firebox</c> so the branch's charge walk finds them.
  /// The count is the whole scale argument: vanilla cokes one block at a time and this is twelve.
  /// </summary>
  [Fact]
  public void The_drawing_marks_twelve_chamber_cells_six_to_a_chamber() {
    List<Vec3i> marked = RoleCellsOf(Def, FurnaceCellRoles.Firebox);

    Assert.Equal(12, marked.Count);
    Assert.Equal(6, marked.Count(c => c.X < 0));
    Assert.Equal(6, marked.Count(c => c.X > 0));
    Assert.Equal(
      AuthoredChambers().OrderBy(c => c.X).ThenBy(c => c.Z).ToList(),
      marked.OrderBy(c => c.X).ThenBy(c => c.Z).ToList()
    );
  }

  /// <summary>
  /// The chambers are separated, not merely wide. Nothing marked sits on x = 0, which is the shared wall
  /// the core itself stands in - and it is that gap which makes the bounding box a lie about this
  /// furnace's size.
  /// </summary>
  [Fact]
  public void The_shared_wall_carries_no_chamber_cell() {
    Assert.DoesNotContain(RoleCellsOf(Def, FurnaceCellRoles.Firebox), c => c.X == 0);
  }

  /// <summary>
  /// A sealed retort has no stack. Marking a flue would hand the oven a natural draught it must not have,
  /// because <c>StackCourses</c> counts exactly these cells - so the absence is the design, not an
  /// omission.
  /// </summary>
  [Fact]
  public void The_drawing_marks_no_flue_and_no_other_role() {
    Assert.Empty(RoleCellsOf(Def, FurnaceCellRoles.Flue));
    Assert.Equal([nameof(FurnaceCellRoles.Firebox)], RoleNamesOf(Def));
  }

  /// <summary>
  /// The oven carries no <c>tier</c> group: it is fire brick throughout, which is vanilla's own coke-oven
  /// masonry, and there is no tiered brick in the drawing for a tier variant to match. Four blocks, one
  /// per facing.
  /// </summary>
  [Fact]
  public void The_core_is_tierless_and_varies_only_by_side() {
    var groups = Def.ToJson()["variantgroups"]!
      .Select(g => (string)g["code"]!)
      .ToList();

    Assert.Equal(["type", "side"], groups);
  }

  #endregion

  #region Standing it up

  /// <summary>
  /// The oven completes at every facing. Its parts are orientation-checked - the drawing doors, their slab
  /// shoulders and the crown lids all name a cardinal - so a facing whose required codes cannot be
  /// satisfied leaves the structure permanently unfinished rather than reporting anything.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_oven_completes_at_every_facing(string side) {
    (BlockEntityCokeOven oven, _) = Stood(side);

    Assert.True(oven.StructureComplete);
  }

  /// <summary>
  /// The chambers come back as twelve world cells at every facing, which is what the charge walk iterates.
  /// A drawing that completed while the role cells rotated wrong would still pass the completion case
  /// above and hand the oven a chamber made of brick.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void Twelve_chamber_cells_resolve_at_every_facing(string side) {
    (BlockEntityCokeOven oven, StructureRig rig) = Stood(side);

    IReadOnlyList<BlockPos> cells = oven.FireboxCells;
    Assert.Equal(12, cells.Count);
    Assert.Equal(12, cells.Distinct().Count());
    // Rotation moves the chambers, so the axis they straddle changes; what cannot change is that they lie
    // in one course, one cell above the core.
    Assert.All(cells, c => Assert.Equal(rig.Cell(0, 1, 0).Y, c.Y));
  }

  #endregion
}
