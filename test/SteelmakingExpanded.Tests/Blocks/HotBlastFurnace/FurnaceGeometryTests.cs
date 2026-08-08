using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronworkingExpanded.Tests;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The hot blast furnace's half of the furnace geometry oracle. The assertions themselves live in iwex's
/// <see cref="FurnaceLayoutRig"/> - a wrong tuyere or tap offset is the same bug on every furnace, and the
/// cells it checks are iwex types - so all that belongs here is the one fact that is genuinely smex's:
/// the hot furnace is the furnace that <b>vents</b>, and its gas outlets must land on the pipe outlets its
/// own layout declares. (iwex's cold blast furnace and cupola assert the opposite, that they have none.)
/// </summary>
public class FurnaceGeometryTests
{
  [Fact]
  public void Hot_furnace_offsets_line_up_with_its_layout()
  {
    var be = new BlockEntityBlastFurnaceHot { Pos = new BlockPos(0, 16, 0) };
    OrientWithLayout(
      be,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single(),
      "smex:blastfurnacecore-n",
      "north"
    );

    List<Vec3i> outlets = AssertFurnaceGeometry(
      be,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single(),
      "smex:blastfurnacecore-*",
      "hot furnace",
      TapGlyphs.ShaftFurnace,
      NorthTuyereGlyph,
      SouthTuyereGlyph
    );

    // The rig already checked that every declared outlet lands on "lpex:pipe-outlet*". What is specific
    // to this furnace is that there are any at all - it is the only one of the three with a stack.
    Assert.NotEmpty(outlets);
  }

  /// <summary>
  /// The gas outlets reach the <b>field the lit tick walks</b>, not just the accessor.
  /// <para>
  /// Why this is here at all. <c>_gasOutlets</c> was asserted <b>empty</b> in three places and non-empty
  /// in none, so deleting <c>_gasOutlets = CellsWithRole(CellRole.GasOutlet)</c> from
  /// <c>ScanForOutlets</c> left every suite green - while the consumer in <c>OnProductionTick</c>
  /// (<c>foreach (var pos in _gasOutlets)</c> -> <c>TryProduce</c> -> the <c>IsChoked</c> latch) silently
  /// never ran. iwex could not close that: the hot blast furnace is the only machine in the repo that
  /// vents, so the only host that can state this is one that loads smex. The iwex side asserts the
  /// opposite - that a hearth's fields stay empty - and its own drive-is-not-a-no-op control covers only
  /// <c>_tuyeres</c>.
  /// </para>
  /// <para>
  /// The field is <b>cleared first</b>, which is this test's positive control: <c>ScanForOutlets</c> is
  /// the only thing that can refill it, so a non-empty read afterwards is that call's work and not a
  /// leftover from completion. Driven reflectively for the same reason the iwex sibling is - the scan runs
  /// from <c>OnStructureCompleted</c> and two lit-tick paths, none of which a test furnace reaches.
  /// </para>
  /// </summary>
  [Fact]
  public void The_hot_furnaces_vent_cells_reach_the_field_the_lit_tick_walks()
  {
    var furnace = new BlockEntityBlastFurnaceHot();
    Stand(
      furnace,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single(),
      Anchor,
      "smex:blastfurnacecore",
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
    // ...and they are the cells the drawing marks, not merely some non-empty set.
    Assert.Equal(
      Render(furnace.CellsWithRole(CellRole.GasOutlet)),
      Render(gasOutlets)
    );

    // The same scan fills the blast side too, so "the outlets arrived" is a statement about this furnace's
    // layout rather than about ScanForOutlets happening to touch one field.
    Assert.NotEmpty(
      (IReadOnlyList<BlockPos>)ReflectionHelpers.GetField(furnace, "_tuyeres")!
    );
  }

  /// <summary>
  /// The hot furnace holds a layered charge. It no longer opts in - the flag is sealed onto
  /// <c>BlockEntityShaftFurnace</c>, and this furnace is one by being one - so what is worth asserting
  /// here is that the branch's answer actually arrives across the assembly boundary.
  /// </summary>
  [Fact]
  public void Hot_furnace_holds_its_charge_in_the_nine_columns_of_its_shaft()
  {
    var be = new BlockEntityBlastFurnaceHot { Pos = new BlockPos(0, 16, 0) };
    OrientWithLayout(
      be,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single(),
      "smex:blastfurnacecore-n",
      "north"
    );

    // The two literals are what the removed `BlockEntityFurnaceCore.ShaftMin`/`ShaftMax` held, restated
    // so the derivation is pinned against what it replaced. This furnace never overrode either, and its
    // drawing marks the same 38 chargeable cells the cold one does - so its box is the same 3x3x5, not the
    // 4x4 its wider shell suggests.
    Assert.Equal((LocalShaftMin, LocalShaftMax), ShaftBoxOf(be));
    Assert.Equal(9, be.ShaftColumns.Count);
    Assert.NotNull(be.ChargeColumnAt(-1, -1));
  }

  /// <summary>
  /// The hot furnace's <b>charge volume</b> - the cells its layout marks <see cref="CellRole.Chargeable"/>,
  /// and the set Phase 3 will place against.
  /// <para>
  /// It is not the shaft box. The box above is 3x3x5 = 45 cells; the layout marks <b>38</b> of them
  /// chargeable, because at the hearth floor only <c>(0,1,0)</c> and <c>(1,1,0)</c> are open - the rest of
  /// that level is the tuyere pair and brick. Identical to the cold furnace's, which is worth stating
  /// rather than assuming: the two shells differ (this one has pipe outlets and a bell hopper) but their
  /// shafts are drawn the same.
  /// </para>
  /// <para>
  /// Run here rather than only in iwex because the accessor comes down the chain
  /// (exlib -> iwex core -> this leaf) and the iwex host never loads this assembly, so a smex layout that
  /// forgot the role - or hung it on the wrong glyph - would be invisible there.
  /// </para>
  /// </summary>
  [Fact]
  public void The_hot_furnaces_charge_volume_is_the_thirty_eight_cells_its_layout_marks()
  {
    var rig = new BlastFurnaceRig();
    var authored = ChargeCells(
      LayoutOf(BlockBlastFurnaceCoreHot.Definitions("smex").Single())
    );

    Assert.Equal(38, authored.Count);
    Assert.Equal(authored.Count, rig.Furnace.ChargeableCells.Count);

    foreach (Vec3i local in authored)
      // Built at north, so local and world differ only by the anchor - the rotated cases are below.
      Assert.Contains(
        rig.Furnace.Pos.AddCopy(local.X, local.Y, local.Z),
        rig.Furnace.ChargeableCells
      );

    // Containment inside the shaft box used to be asserted here and is now a tautology - the box is the
    // bounding box of this set. What survives is tightness, stated against a min/max this file computes
    // itself over the authored cells rather than against the machine's own derivation.
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
  /// The hot furnace's own copy of the migration proof. The role and the block code answer the
  /// <b>same</b> cells, element-wise, at every facing - and every one of those cells is inside the
  /// footprint the furnace completed.
  /// <para>
  /// Nothing in the layout DSL relates a role to the code its glyph carries, so a <c>Role()</c> hung on
  /// this drawing's brick or outlet glyph would build cleanly and answer a plausible set. This comparison
  /// is the only thing that would catch it, and it has to run in a host that loads smex.
  /// </para>
  /// <para>
  /// <b>Ownership is not what catches a reversed rotation</b>, and an earlier version of this comment
  /// said it was. A role cell is owned by construction: <c>CellsWithRole</c> emits
  /// <c>Pos.AddCopy(transformed[i])</c> off <c>MultiblockStructure.TransformedOffsets</c> and
  /// <c>OwnsCell</c> scans that same list, so the assertion cannot fail however the mapping is broken
  /// (verified by mutation - scrambling the index mapping fails 61 iwex tests and no ownership assertion
  /// anywhere). The <c>Assert.Equal</c> against <c>CellsAccepting</c> is the real coverage here, because
  /// it rotates by the other route; do not drop it as redundant with the ownership loop.
  /// </para>
  /// <para>
  /// The ownership loop is kept as a cheap cross-check that <c>OwnsCell</c> reads the <b>transformed</b>
  /// footprint rather than the authored one - point it at <c>Offsets</c> and this test fails at three
  /// facings of four, which is the one thing it genuinely pins.
  /// </para>
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void The_hot_furnaces_role_cells_are_the_cells_that_take_a_charge_pile(
    string side
  )
  {
    var furnace = new BlockEntityBlastFurnaceHot();
    Stand(
      furnace,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single(),
      new BlockPos(0, 16, 0),
      "smex:blastfurnacecore",
      side
    );

    Assert.Equal(38, furnace.ChargeableCells.Count);
    Assert.Equal(
      Render(furnace.CellsAccepting(new AssetLocation("iwex:furnace-chargepile"))),
      Render(furnace.CellsWithRole(CellRole.Chargeable))
    );
    foreach (BlockPos cell in furnace.ChargeableCells)
      Assert.True(
        furnace.OwnsCell(cell),
        $"role cell {furnace.LocalOf(cell)} at {side} is outside the footprint it completed"
      );
  }

  /// <summary>
  /// The hot furnace's own copy of the Tuyere / GasOutlet / Pool migration proof, element-wise against
  /// the values the deleted C# arrays held, at every facing.
  /// <para>
  /// It is the only furnace in the suite that <b>vents</b>, so it is the only one where
  /// <c>CellRole.GasOutlet</c> is non-empty at all - the cold furnace and the cupola state the absence and
  /// the two hearths never had outlets. If this file did not exist, the whole GasOutlet migration would be
  /// pinned only by two "is empty" assertions in a host that cannot see the furnace that fills it.
  /// </para>
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void The_hot_furnaces_migrated_cells_are_what_its_deleted_arrays_held(
    string side
  )
  {
    var furnace = new BlockEntityBlastFurnaceHot();
    Stand(
      furnace,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single(),
      Anchor,
      "smex:blastfurnacecore",
      side
    );
    int angle = AngleFromSide(side);

    // TuyereCells / GasOutletCells / SolidifyCells / MetalTapCell / SlagTapCell as this furnace inherited
    // them from BlockEntityFurnaceCore before the migration - it overrode none of the five.
    (CellRole Role, Vec3i[] Before)[] migrated =
    [
      (CellRole.Tuyere, [new(0, 1, -1), new(0, 1, 1)]),
      (CellRole.GasOutlet, [new(0, 6, -1), new(0, 6, 1)]),
      (CellRole.Pool, [new(0, 1, 0), new(1, 1, 0)]),
      (CellRole.MetalTap, [new(2, 1, 0)]),
      (CellRole.SlagTap, [new(-2, 2, 0)]),
    ];

    foreach (var (role, before) in migrated)
    {
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

    // The two roles that do have a distinguishing block code, played off CellsAccepting - the only check
    // anywhere that a Role() landed on the glyph its author meant. Pool has no such oracle: its glyph is a
    // deliberate duplicate of the shaft glyph, so it is pinned by the relation instead.
    // Two tuyere codes since iwex pinned their orientation - `n` out of the north wall, `s` out of the
    // south. The union is still the role's cells, and each code additionally answers its own single cell,
    // so a drawing that used one letter for both inlets fails here rather than shipping a furnace with an
    // inlet facing into its own hearth.
    // The letters are rotated by the structure angle: the tuyere legends are orientation-pinned, so a
    // west-facing furnace wants `iwex:furnace-tuyere-e` in the cell drawn `n`. Asking with the authored
    // letter at a rotated facing would find nothing and pass vacuously.
    AssetLocation Tuyere(string wall) =>
      new(
        "iwex:furnace-tuyere-"
          + ExOrientation.RotateOrientationToken(
            wall,
            ExOrientation.AngleFromSide(side)
          )
      );

    Assert.Equal(
      Render(
        furnace
          .CellsAccepting(Tuyere("n"))
          .Concat(furnace.CellsAccepting(Tuyere("s")))
      ),
      Render(furnace.CellsWithRole(CellRole.Tuyere))
    );
    Assert.Single(furnace.CellsAccepting(Tuyere("n")));
    Assert.Single(furnace.CellsAccepting(Tuyere("s")));
    Assert.Equal(
      Render(
        furnace.CellsAccepting(new AssetLocation("lpex:pipe-outlet-fire-u"))
      ),
      Render(furnace.CellsWithRole(CellRole.GasOutlet))
    );

    // A whole code oracle since iwex typed the taps. It used to be half of one - both taps were
    // `iwex:furnace-tap-*`, so CellsAccepting could be played off their union but could not say which was
    // which, and the drawing swapping them was invisible here. Each notch is now its own block, so each
    // role is pinned on its own. The Before() comparison above and the "slag floats" relation below stay:
    // they say something the codes do not.
    // Rotated, exactly as the tuyere letters above are, and the drawn facings are west and east rather
    // than north. Both read backwards - `TryPourMetal` spouts at `facing.Opposite`, so the iron notch in
    // the east wall is declared west. Hard-coding `-north` here found nothing at any facing and passed
    // vacuously once the legends were pinned.
    AssetLocation Tap(string type, string drawnSide) =>
      new(
        $"iwex:furnace-{type}-"
          + ExOrientation.SideFromAngle(
            ExOrientation.AngleFromSide(drawnSide)
              + ExOrientation.AngleFromSide(side),
            // A letter since the 2026-08-04 respelling. Same trap as the `-north` note above, one
            // spelling further on: a word here names no block, so every CellsAccepting came back empty
            // and the set-equality assertions compared two empties.
            asLetter: true
          )
      );

    Assert.Equal(
      Render(furnace.CellsAccepting(Tap("irontap", "west"))),
      Render(furnace.CellsWithRole(CellRole.MetalTap))
    );
    Assert.Equal(
      Render(furnace.CellsAccepting(Tap("slagtap", "east"))),
      Render(furnace.CellsWithRole(CellRole.SlagTap))
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

  // The structure-local corners the deleted ShaftMin/ShaftMax held on the core, which this furnace
  // inherited and never overrode. Stated once because two copies of one literal in one file is the drift
  // this whole migration exists to remove.
  //
  // Two plain fields rather than one `(Vec3i, Vec3i)` field. A static field whose type is a value type
  // instantiated over a game type - a ValueTuple of Vec3i - has to be resolved when the runtime loads this
  // class, which xUnit does during discovery, before the module initializer registers VsAssemblyResolver.
  // The whole assembly then skips with "could not find dependent assembly VintagestoryAPI". A field of a
  // game type is fine (it is a reference); it is the value-type instantiation that forces the load. Same
  // family of trap as the generic-constraint rule in FurnaceLayoutRig's remarks.
  private static readonly Vec3i LocalShaftMin = new(-1, 1, -1);
  private static readonly Vec3i LocalShaftMax = new(1, 5, 1);

  /// <summary>Where structure-local cells land at <paramref name="angle"/>, computed through the shared
  /// rotation helper rather than off the furnace - an independent second route to the same world cells.</summary>
  private static string ExpectedAt(IEnumerable<Vec3i> local, int angle) =>
    Render(
      local.Select(c =>
      {
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
  /// The hot furnace <b>turning</b> its own shaft box - the only fact in this file about rotation rather
  /// than about the drawing.
  /// <para>
  /// <b>Why it belongs here and not only in iwex.</b> Every other box assertion in smex reads
  /// <c>ShaftBox</c>, the structure-local box, which is facing-invariant - so nothing in this mod ever
  /// turned a box at all. <c>ShaftBounds()</c> is the single place the box meets rotation, and its
  /// per-component re-sort is load-bearing: a quarter turn can swap either horizontal axis, so "whatever
  /// the low corner rotated into" is not a minimum, and <c>CollectChargePiles</c>' walk over an inverted
  /// box visits nothing - a lit furnace that reads its shaft as empty.
  /// </para>
  /// <para>
  /// This furnace's box is the cold furnace's exactly, so it has always been <em>able</em> to witness the
  /// re-sort; it did not, because no smex assertion ever asked for the world box. Until this existed the
  /// re-sort had one test reader in the whole tree, and deleting that one iwex theory would have returned
  /// it to none without this furnace noticing.
  /// </para>
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void The_hot_furnaces_world_box_is_its_local_corners_rotated_and_re_sorted(
    string side
  )
  {
    var furnace = new BlockEntityBlastFurnaceHot();
    Stand(
      furnace,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single(),
      Anchor,
      "smex:blastfurnacecore",
      side
    );

    // Built from the pre-migration literals through the shared rotation helper and a component-wise
    // min/max - the same statement as the method, by a route that shares no code with it.
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

    // ...and never inverted on any axis, which is what a box walk needs. This box's corners differ on x
    // and on z, so a dropped re-sort inverts one of them at every facing but north.
    Assert.True(
      min.X <= max.X && min.Y <= max.Y && min.Z <= max.Z,
      $"the hot furnace's world box at {side} is inverted - ({min.X},{min.Y},{min.Z}) is not below "
        + $"({max.X},{max.Y},{max.Z})"
    );
  }

  /// <summary>
  /// The furnace laws, run in a host where <b>smex is loaded</b>. The iwex suite runs the same three
  /// assertions, but the iwex test host never loads this assembly, so until this existed a downstream
  /// furnace leaf could break any of them and no suite anywhere would notice. This furnace is the
  /// concrete exposure today; the scan is general, so anything smex adds later is covered by the same
  /// three lines.
  /// </summary>
  [Fact]
  public void Every_furnace_this_host_can_see_obeys_the_branch_laws()
  {
    FurnaceBranchGuards.TheBranchOwnsTheLayeredChargeFlag();
    FurnaceBranchGuards.NoFireboxAsksForMoreThanItsCellsCanHold();
    FurnaceBranchGuards.NoFurnaceExposesASettableState();
  }
}
