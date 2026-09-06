using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.Tests;
using SteelIndustryExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using SteelIndustryExpanded.BlockStructures.HotBlastFurnace.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Geometry checks for the hot blast furnace. The shared assertions live in iiex's
/// <see cref="FurnaceLayoutRig"/>, since a wrong tuyere or tap offset is the same bug on every furnace.
/// What is specific here is that this furnace vents: its gas outlets must land on the pipe outlets its
/// own layout declares, where the cold blast furnace and the cupola assert they have none.
/// </summary>
public class FurnaceGeometryTests {
  [Fact]
  public void Hot_furnace_offsets_line_up_with_its_layout() {
    var be = new BlockEntityBlastFurnaceHot { Pos = new BlockPos(0, 16, 0) };
    OrientWithLayout(
      be,
      BlockBlastFurnaceCoreHot.Definitions("siex").Single(),
      "siex:blastfurnacecore-n",
      "north"
    );

    List<Vec3i> outlets = AssertFurnaceGeometry(
      be,
      BlockBlastFurnaceCoreHot.Definitions("siex").Single(),
      "siex:blastfurnacecore-*",
      "hot furnace",
      TapGlyphs.ShaftFurnace,
      // Both glyphs, where the two iwex furnaces charge the shaft alone: this drawing still marks its
      // crucible course Chargeable beside Pool. Deferred to the smex remake, and pinned by
      // HotFurnaceCrucibleOverlapTests - not a set to copy onto a new drawing.
      [ShaftGlyph, HearthGlyph],
      "n",
      "s"
    );

    // The rig already checked that every declared outlet lands on "iiex:pipe-outlet*". This furnace is
    // the only one of the three with a stack, so it is the only one that declares any.
    Assert.NotEmpty(outlets);
  }

  /// <summary>
  /// <c>ScanForOutlets</c> fills <c>_gasOutlets</c>, the field <c>OnProductionTick</c> walks, and not only
  /// the <c>CellsWithRole</c> accessor. Both fields are blanked first so a non-empty read afterwards is
  /// that call's work rather than a leftover from completion. Driven reflectively: the scan runs from
  /// <c>OnStructureCompleted</c> and two lit-tick paths, none of which a test furnace reaches.
  /// </summary>
  [Fact]
  public void The_hot_furnaces_vent_cells_reach_the_field_the_lit_tick_walks() {
    var furnace = new BlockEntityBlastFurnaceHot();
    Stand(
      furnace,
      BlockBlastFurnaceCoreHot.Definitions("siex").Single(),
      Anchor,
      "siex:blastfurnacecore",
      "north"
    );

    // Both fields blanked, so nothing below can pass on a value completion already wrote.
    ReflectionHelpers.SetField(
      furnace,
      "_gasOutlets",
      (IReadOnlyList<BlockPos>)new List<BlockPos>()
    );
    ReflectionHelpers.SetField(
      furnace,
      "_tuyeres",
      (IReadOnlyList<BlockPos>)new List<BlockPos>()
    );

    ReflectionHelpers.Invoke(furnace, "ScanForOutlets");

    var gasOutlets =
      (IReadOnlyList<BlockPos>)
        ReflectionHelpers.GetField(furnace, "_gasOutlets")!;

    Assert.NotEmpty(gasOutlets);
    // The cells the layout marks, not merely some non-empty set.
    Assert.Equal(
      Render(furnace.CellsWithRole(FurnaceCellRoles.GasOutlet)),
      Render(gasOutlets)
    );

    // The same scan fills the blast side, so a filled outlet list reflects this furnace's layout rather
    // than ScanForOutlets happening to touch one field.
    Assert.NotEmpty(
      (IReadOnlyList<BlockPos>)ReflectionHelpers.GetField(furnace, "_tuyeres")!
    );
  }

  /// <summary>
  /// The hot furnace holds a layered charge. The flag is sealed onto <c>BlockEntityShaftFurnace</c> rather
  /// than opted into, so what this pins is that the branch's answer arrives across the assembly boundary.
  /// </summary>
  [Fact]
  public void Hot_furnace_holds_its_charge_in_the_nine_columns_of_its_shaft() {
    var be = new BlockEntityBlastFurnaceHot { Pos = new BlockPos(0, 16, 0) };
    OrientWithLayout(
      be,
      BlockBlastFurnaceCoreHot.Definitions("siex").Single(),
      "siex:blastfurnacecore-n",
      "north"
    );

    // The two literals are the values `BlockEntityFurnaceCore.ShaftMin`/`ShaftMax` held, which this
    // furnace never overrode: a 3x3x5 box rather than the 4x4 its wider shell suggests. The box still
    // reaches down to the crucible course here, where the cold furnace's now starts a course above it.
    Assert.Equal((LocalShaftMin, LocalShaftMax), ShaftBoxOf(be));
    Assert.Equal(9, be.ShaftColumns.Count);
    Assert.NotNull(be.ChargeColumnAt(-1, -1));
  }

  /// <summary>
  /// The charge volume: the cells the layout marks <see cref="FurnaceCellRoles.Chargeable"/>. 38 of the shaft
  /// box's 45, because at the hearth floor only <c>(0,1,0)</c> and <c>(1,1,0)</c> are open and the rest of
  /// that level is the tuyere pair and brick. Two more than the cold furnace's 36, those two being the
  /// crucible cells this drawing still charges. Run here because the iiex host never loads this assembly,
  /// so a siex layout that forgot the role, or hung it on the wrong glyph, would be invisible there.
  /// </summary>
  [Fact]
  public void The_hot_furnaces_charge_volume_is_the_thirty_eight_cells_its_layout_marks() {
    var rig = new BlastFurnaceRig();
    var authored = ChargeCells(
      LayoutOf(BlockBlastFurnaceCoreHot.Definitions("siex").Single()),
      ShaftGlyph,
      // The crucible course counts here and does not on either iwex furnace - see the overlap note in
      // the geometry case above.
      HearthGlyph
    );

    Assert.Equal(38, authored.Count);
    Assert.Equal(authored.Count, rig.Furnace.ChargeableCells.Count);

    foreach (Vec3i local in authored)
      // Built at north, so local and world differ only by the anchor - the rotated cases are below.
      Assert.Contains(
        rig.Furnace.Pos.AddCopy(local.X, local.Y, local.Z),
        rig.Furnace.ChargeableCells
      );

    // Tightness of the shaft box, against a min/max computed here over the authored cells rather than
    // against the machine's own derivation. Containment is a tautology: the box is this set's bounds.
    Assert.Equal(
      (
        new Vec3i(
          authored.Min(c => c.X),
          authored.Min(c => c.Y),
          authored.Min(c => c.Z)
        ),
        new Vec3i(
          authored.Max(c => c.X),
          authored.Max(c => c.Y),
          authored.Max(c => c.Z)
        )
      ),
      ShaftBoxOf(rig.Furnace)
    );
  }

  /// <summary>
  /// The <see cref="FurnaceCellRoles.Chargeable"/> role and the charge-pile block code answer the same cells,
  /// element-wise, at every facing, and every one is inside the completed footprint. Nothing in the layout
  /// DSL relates a role to the code its glyph carries, so the <c>CellsAccepting</c> comparison, which
  /// rotates by a different route than <c>CellsWithRole</c>, is the only check that a <c>Role()</c> landed
  /// on the intended glyph. The ownership loop pins only that <c>OwnsCell</c> reads the transformed
  /// footprint rather than the authored one - pointed at <c>Offsets</c> it fails at three facings of four.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void The_hot_furnaces_role_cells_are_the_cells_that_take_a_charge_pile(
    string side
  ) {
    var furnace = new BlockEntityBlastFurnaceHot();
    Stand(
      furnace,
      BlockBlastFurnaceCoreHot.Definitions("siex").Single(),
      new BlockPos(0, 16, 0),
      "siex:blastfurnacecore",
      side
    );

    Assert.Equal(38, furnace.ChargeableCells.Count);
    Assert.Equal(
      Render(
        furnace.CellsAccepting(new AssetLocation("iiex:furnace-chargepile"))
      ),
      Render(furnace.CellsWithRole(FurnaceCellRoles.Chargeable))
    );
    foreach (BlockPos cell in furnace.ChargeableCells)
      Assert.True(
        furnace.OwnsCell(cell),
        $"role cell {furnace.LocalOf(cell)} at {side} is outside the footprint it completed"
      );
  }

  /// <summary>
  /// Tuyere, GasOutlet, Pool, MetalTap and SlagTap cells match the values the deleted C# arrays held,
  /// element-wise, at every facing. This is the only furnace that vents, so it is the only one where
  /// <see cref="FurnaceCellRoles.GasOutlet"/> is non-empty; every other suite pins that role by asserting absence.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void The_hot_furnaces_migrated_cells_are_what_its_deleted_arrays_held(
    string side
  ) {
    var furnace = new BlockEntityBlastFurnaceHot();
    Stand(
      furnace,
      BlockBlastFurnaceCoreHot.Definitions("siex").Single(),
      Anchor,
      "siex:blastfurnacecore",
      side
    );
    int angle = AngleFromSide(side);

    // TuyereCells / GasOutletCells / SolidifyCells / MetalTapCell / SlagTapCell as inherited from
    // BlockEntityFurnaceCore; this furnace overrode none of the five.
    (CellRole Role, Vec3i[] Before)[] migrated =
    [
      (FurnaceCellRoles.Tuyere, [new(0, 1, -1), new(0, 1, 1)]),
      (FurnaceCellRoles.GasOutlet, [new(0, 6, -1), new(0, 6, 1)]),
      (FurnaceCellRoles.Pool, [new(0, 1, 0), new(1, 1, 0)]),
      (FurnaceCellRoles.MetalTap, [new(2, 1, 0)]),
      (FurnaceCellRoles.SlagTap, [new(-2, 2, 0)]),
    ];

    foreach (var (role, before) in migrated) {
      Assert.Equal(
        $"{role}: {ExpectedAt(before, angle)}",
        $"{role}: {Render(furnace.CellsWithRole(role))}"
      );
      foreach (BlockPos cell in furnace.CellsWithRole(role))
        Assert.True(
          furnace.OwnsCell(cell),
          $"{role} cell {furnace.LocalOf(cell)} at {side} is outside the footprint it completed"
        );
    }

    // The roles with a distinguishing block code, played off CellsAccepting - the only check that a Role()
    // landed on the glyph its author meant. Pool has no such oracle: its glyph duplicates the shaft glyph,
    // so it is pinned by the relation at the end of this method instead.
    //
    // One wildcarded tuyere code for both inlets: the layout states which way each must open through its
    // Connector mark rather than by pinning the variant, because a network node re-picks its orientation
    // from its neighbours and is free to contradict a pin. So the code answers which cells, and the
    // demanded faces answer which way - a drawing marking both inlets outward-north fails on the second.
    // The faces rotate with the structure: a west-facing furnace wants `e` in the cell drawn `n`.
    string Face(string wall) =>
      ExOrientation.SideFromAngle(
        ExOrientation.AngleFromSide(wall) + ExOrientation.AngleFromSide(side),
        asLetter: true
      );

    Assert.Equal(
      Render(
        furnace.CellsAccepting(new AssetLocation("iiex:furnace-tuyere-*"))
      ),
      Render(furnace.CellsWithRole(FurnaceCellRoles.Tuyere))
    );
    Assert.Equal(
      new[] { Face("n"), Face("s") }.OrderBy(f => f),
      furnace
        .CellsWithRole(FurnaceCellRoles.Tuyere)
        .SelectMany(furnace.ConnectorFacesAt)
        .Select(f => ExOrientation.TokenOf(f, asLetter: true))
        .OrderBy(f => f)
    );
    Assert.Equal(
      Render(
        furnace.CellsAccepting(new AssetLocation("iiex:pipe-outlet-fire-u"))
      ),
      Render(furnace.CellsWithRole(FurnaceCellRoles.GasOutlet))
    );

    // Each tap notch carries its own block code, so each tap role is pinned on its own; the array
    // comparison above and the "slag floats" relation below say things the codes do not.
    //
    // Rotated like the tuyere letters above, off drawn facings of west and east. Both read backwards:
    // `TryPourMetal` spouts at `facing.Opposite`, so the iron notch in the east wall is declared west.
    AssetLocation Tap(string type, string drawnSide) =>
      new(
        $"iiex:furnace-{type}-"
          + ExOrientation.SideFromAngle(
            ExOrientation.AngleFromSide(drawnSide)
              + ExOrientation.AngleFromSide(side),
            // A letter, not a word: a side variant renders one letter, and a word here names no block,
            // so every CellsAccepting would come back empty and the set comparisons would compare empties.
            asLetter: true
          )
      );

    Assert.Equal(
      Render(furnace.CellsAccepting(Tap("irontap", "west"))),
      Render(furnace.CellsWithRole(FurnaceCellRoles.MetalTap))
    );
    Assert.Equal(
      Render(furnace.CellsAccepting(Tap("slagtap", "east"))),
      Render(furnace.CellsWithRole(FurnaceCellRoles.SlagTap))
    );
    Assert.Single(furnace.CellsAccepting(Tap("irontap", "west")));
    Assert.Single(furnace.CellsAccepting(Tap("slagtap", "east")));
    Assert.Equal(furnace.MetalTapPos!.Y + 1, furnace.SlagTapPos!.Y);

    int floorY = furnace.ChargeableCells.Min(c => c.Y);
    Assert.Equal(
      Render(furnace.ChargeableCells.Where(c => c.Y == floorY)),
      Render(furnace.PoolCells)
    );
  }

  private static readonly BlockPos Anchor = new(0, 16, 0);

  // The structure-local corners ShaftMin/ShaftMax held on the core, which this furnace never overrode.
  //
  // Two plain fields rather than one `(Vec3i, Vec3i)` field: a static field whose type is a value type
  // instantiated over a game type - a ValueTuple of Vec3i - has to be resolved when the runtime loads this
  // class, which xUnit does during discovery, before the module initializer registers VsAssemblyResolver.
  // The whole assembly then skips with "could not find dependent assembly VintagestoryAPI". A field of a
  // game type is fine because it is a reference; only the value-type instantiation forces the load.
  private static readonly Vec3i LocalShaftMin = new(-1, 1, -1);
  private static readonly Vec3i LocalShaftMax = new(1, 5, 1);

  /// <summary>Where structure-local cells land at <paramref name="angle"/>, computed through the shared
  /// rotation helper rather than off the furnace - an independent route to the same world cells.</summary>
  private static string ExpectedAt(IEnumerable<Vec3i> local, int angle) =>
    Render(
      local.Select(c => {
        Vec3i r = ExOrientation.RotateOffset(c, angle);
        return Anchor.AddCopy(r.X, r.Y, r.Z);
      })
    );

  private static string Render(IEnumerable<BlockPos> cells) =>
    string.Join(
      ", ",
      cells
        .Select(p => $"({p.X},{p.Y},{p.Z})")
        .OrderBy(s => s, System.StringComparer.Ordinal)
    );

  /// <summary>
  /// The furnace's world shaft box at each facing: the local corners rotated, then re-sorted per
  /// component. <c>ShaftBounds()</c> is the one place the box meets rotation, since every other box
  /// assertion in siex reads the facing-invariant <c>ShaftBox</c>. The re-sort is load-bearing: a quarter
  /// turn can swap either horizontal axis, so the rotated low corner is not a minimum, and
  /// <c>CollectChargePiles</c> walking an inverted box visits nothing.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void The_hot_furnaces_world_box_is_its_local_corners_rotated_and_re_sorted(
    string side
  ) {
    var furnace = new BlockEntityBlastFurnaceHot();
    Stand(
      furnace,
      BlockBlastFurnaceCoreHot.Definitions("siex").Single(),
      Anchor,
      "siex:blastfurnacecore",
      side
    );

    // Built from the local literals through the shared rotation helper and a component-wise min/max -
    // the same statement as the method, by a route that shares no code with it.
    int angle = AngleFromSide(side);
    Vec3i a = ExOrientation.RotateOffset(LocalShaftMin, angle);
    Vec3i b = ExOrientation.RotateOffset(LocalShaftMax, angle);

    var (min, max) = WorldBoxOf(furnace)!.Value;

    Assert.Equal(
      $"{side}: ({Anchor.X + Math.Min(a.X, b.X)},{Anchor.Y + Math.Min(a.Y, b.Y)},"
        + $"{Anchor.Z + Math.Min(a.Z, b.Z)})..({Anchor.X + Math.Max(a.X, b.X)},"
        + $"{Anchor.Y + Math.Max(a.Y, b.Y)},{Anchor.Z + Math.Max(a.Z, b.Z)})",
      $"{side}: ({min.X},{min.Y},{min.Z})..({max.X},{max.Y},{max.Z})"
    );

    // Never inverted on any axis, which is what a box walk needs. This box's corners differ on x and on
    // z, so a dropped re-sort inverts one of them at every facing but north.
    Assert.True(
      min.X <= max.X && min.Y <= max.Y && min.Z <= max.Z,
      $"the hot furnace's world box at {side} is inverted - ({min.X},{min.Y},{min.Z}) is not below "
        + $"({max.X},{max.Y},{max.Z})"
    );
  }

  /// <summary>
  /// The furnace branch laws, run in a host that loads siex. The iiex suite runs the same five
  /// assertions, but its host never loads this assembly, so a downstream furnace leaf breaking one would
  /// go unnoticed there. The scan is general: furnaces siex adds later are covered by the same lines.
  /// </summary>
  [Fact]
  public void Every_furnace_this_host_can_see_obeys_the_branch_laws() {
    FurnaceBranchGuards.TheBranchOwnsTheLayeredChargeFlag();
    FurnaceBranchGuards.NoFireboxAsksForMoreThanItsCellsCanHold();
    FurnaceBranchGuards.NoFireboxCarriesAFloorAboveItsOwnCapacity();
    FurnaceBranchGuards.EveryFireboxReachesItsOwnProcessTemperature();
    FurnaceBranchGuards.NoFurnaceExposesASettableState();
  }
}
