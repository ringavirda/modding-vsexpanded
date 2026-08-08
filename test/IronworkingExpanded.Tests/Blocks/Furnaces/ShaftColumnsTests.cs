using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The furnace's ownership of its charge: one <see cref="ChargeColumn"/> per <c>(x, z)</c> of the shaft
/// box, keyed structure-local, persisted per column, and switched off entirely on a reverberatory hearth.
/// </summary>
/// <remarks>
/// The geometry cases do not rest on the shipped 3x3 shaft: a 3x3 set of columns is closed under 90 deg
/// rotation, so "the same keys at all four facings" holds just as well for a set derived from the world
/// box - the mistake available here, since <c>ShaftBounds()</c> sits beside <c>ShaftBox</c> and returns
/// rotated corners. An asymmetric 3x2 shaft makes that assertion bite, and the local-to-world cases pin a
/// named corner rather than a set.
/// </remarks>
public class ShaftColumnsTests {
  #region Harness

  private const string Coke = "game:coke";
  private const string Burden = "iwex:burden";
  private const string Charcoal = "game:charcoal";

  /// <summary>U+2212 MINUS SIGN - what sv-SE, fi-FI and lt-LT put in front of a negative number, and so
  /// what a save key must never be allowed to pick up. Escaped rather than pasted: it is
  /// indistinguishable from ASCII <c>-</c> in a source listing.</summary>
  private const string MinusSign = "\u2212";

  private static readonly BurdenMix Fluxed = new(70f, 10f, 20f);
  private static readonly BurdenMix Lean = new(60f, 20f, 20f);

  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>The nine columns of the shipped blast-furnace shaft, in the order <see cref="Keys"/>
  /// reports them. Stated once so a count and a key set are never asserted apart.</summary>
  private static readonly (int X, int Z)[] BlastFurnaceColumns =
  [
    (-1, -1),
    (-1, 0),
    (-1, 1),
    (0, -1),
    (0, 0),
    (0, 1),
    (1, -1),
    (1, 0),
    (1, 1),
  ];

  /// <summary>The 3x2 asymmetric set - see the class remarks for why a square one proves nothing.</summary>
  private static readonly (int X, int Z)[] AsymmetricColumns =
  [
    (-1, 0),
    (-1, 1),
    (0, 0),
    (0, 1),
    (1, 0),
    (1, 1),
  ];

  /// <summary>
  /// A furnace whose shaft box is deliberately not square: 3 cells on x, 2 on z, and off-centre on z.
  /// Every shipped shaft is square or a single cell, so this is the only shape in which a transposed
  /// derivation loop, or one keyed off the rotated world box, is visible at all. The box is the bounding
  /// box of the cells the layout marks <c>Chargeable</c>, so varying the drawing is the only way to vary
  /// it.
  /// </summary>
  private static ExBlockDef AsymmetricDef() =>
    ShaftBoxDef(new Vec3i(-1, 1, 0), new Vec3i(1, 5, 1));

  private static BlockEntityBlastFurnaceCold Cold(string side = "north") {
    var be = new BlockEntityBlastFurnaceCold { Pos = Anchor.Copy() };
    OrientWithLayout(
      be,
      BlockBlastFurnaceCoreCold.Definitions("iwex").Single(),
      $"iwex:furnace-blastcore-tier1-{side}",
      side
    );
    return be;
  }

  private static BlockEntityCupolaFurnace Cupola(string side = "north") {
    var be = new BlockEntityCupolaFurnace { Pos = Anchor.Copy() };
    OrientWithLayout(
      be,
      BlockCupolaFurnaceCore.Definitions("iwex").Single(),
      $"iwex:furnace-cupolacore-tier1-{side}",
      side
    );
    return be;
  }

  private static BlockEntityPuddlingFurnace Puddling() {
    var be = new BlockEntityPuddlingFurnace { Pos = Anchor.Copy() };
    OrientWithLayout(
      be,
      BlockPuddlingFurnaceCore.Definitions("iwex").Single(),
      "iwex:furnace-puddlingcore-tier1-n",
      "north"
    );
    return be;
  }

  private static BlockEntityHeatingFurnace Heating() {
    var be = new BlockEntityHeatingFurnace { Pos = Anchor.Copy() };
    OrientWithLayout(
      be,
      BlockHeatingFurnaceCore.Definitions("iwex").Single(),
      "iwex:furnace-heatingcore-tier1-n",
      "north"
    );
    return be;
  }

  private static BlockEntityBlastFurnaceCold Asymmetric(string side = "north") {
    var be = new BlockEntityBlastFurnaceCold { Pos = Anchor.Copy() };
    OrientWithLayout(
      be,
      AsymmetricDef(),
      $"iwex:furnace-blastcore-tier1-{side}",
      side
    );
    return be;
  }

  private static (int X, int Z)[] Keys(BlockEntityFurnaceCore be) =>
    be.ShaftColumns.Keys.OrderBy(k => k.X).ThenBy(k => k.Z).ToArray();

  private static string[] TreeKeys(BlockEntity be) {
    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);
    return tree.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
  }

  /// <summary>The save key the furnace writes per column, restated here rather than reached through the
  /// production helper, which is private and would agree with any answer. Invariant for the same reason
  /// the production one is: a Swedish or Finnish machine formats <c>-1</c> with U+2212 and this mirror
  /// would stop matching.</summary>
  private static string ColumnKeyOf((int X, int Z) column) =>
    string.Create(
      CultureInfo.InvariantCulture,
      $"chargeCol_{column.X}_{column.Z}"
    );

  private static string[] ColumnKeysOf(BlockEntityFurnaceCore be) =>
    be
      .ShaftColumns.Keys.Select(ColumnKeyOf)
      .OrderBy(k => k, StringComparer.Ordinal)
      .ToArray();

  /// <summary>
  /// Runs <paramref name="body"/> with <paramref name="culture"/> installed on the calling thread and
  /// restores the previous one whatever happens - xUnit hands threads back to the pool, so a leaked
  /// culture would land on an unrelated test.
  /// </summary>
  private static void InCulture(CultureInfo culture, Action body) {
    CultureInfo previous = CultureInfo.CurrentCulture;
    CultureInfo.CurrentCulture = culture;
    try {
      body();
    } finally {
      CultureInfo.CurrentCulture = previous;
    }
  }

  /// <summary>
  /// A culture that writes a negative sign the way sv-SE, fi-FI and lt-LT do - U+2212 MINUS SIGN, not
  /// ASCII hyphen. Synthesised rather than looked up: <c>GetCultureInfo("sv-SE")</c> answers whatever ICU
  /// the host ships, and the invariant culture outright in globalization-invariant mode, so a real-locale
  /// test would go green on some machines. The behaviour is what matters here, not the locale.
  /// </summary>
  private static CultureInfo Hostile() {
    var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
    var numbers = (NumberFormatInfo)culture.NumberFormat.Clone();
    numbers.NegativeSign = MinusSign;
    culture.NumberFormat = numbers;
    return culture;
  }

  /// <summary>
  /// The same tree after a trip through real attribute serialization - what a world save does to it. The
  /// columns ride as nested sub-trees, which an in-memory hand-back would not exercise.
  /// </summary>
  private static TreeAttribute Serialized(TreeAttribute tree) {
    using var stream = new MemoryStream();
    using (
      var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)
    )
      tree.ToBytes(writer);

    stream.Position = 0;
    using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
    var read = new TreeAttribute();
    read.FromBytes(reader);
    return read;
  }

  private static TreeAttribute Saved(BlockEntityFurnaceCore be) {
    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);
    return Serialized(tree);
  }

  #endregion

  #region Geometry

  [Fact]
  public void The_cold_furnace_owns_one_column_per_cell_of_its_shaft() {
    BlockEntityBlastFurnaceCold be = Cold();

    Assert.Equal(BlastFurnaceColumns, Keys(be));
  }

  /// <summary>
  /// The hot blast furnace's own count, asserted here because it is the same number from the same source:
  /// <c>BlockEntityBlastFurnaceHot</c> is an empty subclass of <c>BlockEntityShaftFurnace</c> and
  /// overrides neither shaft corner, so it inherits the core defaults this furnace also uses - 9 columns.
  /// That it holds them is smex's to pin, since the type is not in this assembly's reference chain.
  /// </summary>
  [Fact]
  public void The_shaft_box_the_hot_furnace_inherits_is_the_core_default_nine() {
    BlockEntityBlastFurnaceCold be = Cold();

    // The corners are stated as literals rather than derived, so the box is pinned against a value the
    // code under test does not supply. The hot furnace inherits the same drawing shape and so the same
    // box.
    Assert.Equal((new Vec3i(-1, 1, -1), new Vec3i(1, 5, 1)), ShaftBoxOf(be));
    Assert.Equal(9, be.ShaftColumns.Count);
  }

  [Fact]
  public void The_cupola_shaft_is_a_single_column() {
    Assert.Equal(new (int X, int Z)[] { (0, 0) }, Keys(Cupola()));
  }

  [Fact]
  public void Every_column_is_its_own_charge_stack() {
    BlockEntityBlastFurnaceCold be = Cold();

    be.ChargeColumnAt(-1, -1)!.Push(Coke, 5, 20f, default);

    // Nine keys pointing at one shared column would read in game as a shaft that fills everywhere at
    // once from a single drip.
    Assert.Equal(5, be.ShaftChargeUnits);
    foreach (var entry in be.ShaftColumns)
      Assert.Equal(entry.Key == (-1, -1) ? 5 : 0, entry.Value.TotalUnits);
  }

  [Fact]
  public void A_cell_that_is_not_a_shaft_column_has_no_column() {
    BlockEntityBlastFurnaceCold be = Cold();

    Assert.NotNull(be.ChargeColumnAt(1, 1));
    Assert.Null(be.ChargeColumnAt(2, 1));
    Assert.Null(be.ChargeColumnAt(0, -2));
  }

  [Fact]
  public void The_column_set_spans_the_shaft_box_on_each_axis_independently() {
    // A loop that walked one axis twice, or transposed x and z, gives 9 or 4 here, and is invisible on
    // every shaft the mod ships.
    Assert.Equal(AsymmetricColumns, Keys(Asymmetric()));
  }

  [Theory]
  [InlineData("north")]
  [InlineData("south")]
  [InlineData("east")]
  [InlineData("west")]
  public void Column_keys_are_in_the_furnace_frame_so_turning_it_does_not_move_them(
    string side
  ) {
    // On the shipped 3x3 shaft this assertion is vacuous: that set is closed under 90 deg rotation, so it
    // would hold for a set derived from ShaftBounds() - the world box - just as well. Rotated a quarter
    // turn the 3x2 world box is 2x3, so a world-derived set comes back transposed and fails here.
    Assert.Equal(AsymmetricColumns, Keys(Asymmetric(side)));
  }

  /// <summary>
  /// The shipped layout's own chargeable cells inside the shaft box - derived, not restated, so a layout
  /// change moves the expectation with it.
  /// </summary>
  private static List<Vec3i> ChargeCellsInShaft(BlockEntityFurnaceCore be) {
    var (min, max) = ShaftBoxOf(be)!.Value;
    return ChargeCells(
        LayoutOf(BlockBlastFurnaceCoreCold.Definitions("iwex").Single())
      )
      .Where(c =>
        c.X >= min.X
        && c.X <= max.X
        && c.Y >= min.Y
        && c.Y <= max.Y
        && c.Z >= min.Z
        && c.Z <= max.Z
      )
      .ToList();
  }

  [Fact]
  public void The_shaft_box_is_a_bounding_box_whose_lowest_level_is_mostly_not_chargeable() {
    BlockEntityBlastFurnaceCold be = Cold();
    List<Vec3i> charge = ChargeCellsInShaft(be);

    // The columns come off ShaftBox, which is a bounding box: at the hearth floor the layout marks only
    // three of its nine cells chargeable - the rest is the tuyere pair and brick - so six columns have no
    // cell at y=1 at all. Block placement therefore has to go per (column, y) against the layout rather
    // than fill the box.
    Assert.Equal(
      new (int X, int Z)[] { (-1, 0), (0, 0), (1, 0) },
      charge
        .Where(c => c.Y == 1)
        .Select(c => (c.X, c.Z))
        .OrderBy(k => k.X)
        .ThenBy(k => k.Z)
        .ToArray()
    );
    // Above the tuyeres the full set is open, four levels of it - which is the 36 the hopper's
    // "27 of 36" readout counts.
    for (int y = 2; y <= 5; y++)
      Assert.Equal(9, charge.Count(c => c.Y == y));

    // Every column still earns its place: each one is chargeable somewhere up the shaft.
    foreach (var (x, z) in Keys(be))
      Assert.Contains(charge, c => c.X == x && c.Z == z);
  }

  [Theory]
  [InlineData("north", "shipped")]
  [InlineData("south", "shipped")]
  [InlineData("east", "shipped")]
  [InlineData("west", "shipped")]
  [InlineData("north", "asymmetric")]
  [InlineData("south", "asymmetric")]
  [InlineData("east", "asymmetric")]
  [InlineData("west", "asymmetric")]
  public void Every_chargeable_cell_of_the_rotated_layout_belongs_to_a_column(
    string side,
    string shaft
  ) {
    // Both shafts. On the shipped 3x3 the column half of this test is vacuous: that key set is closed
    // under 90 deg rotation, so "the cell's (x, z) is a column key" holds for a set derived from the
    // world box too. The 3x2 case keeps the same layout and the same chargeable cells but is not
    // rotation-closed, so a world-derived key set is missing the very cells the layout charges. The
    // shipped case stays for AssertRotatedCell's half, which is real work on the geometry that ships.
    BlockEntityBlastFurnaceCold be =
      shaft == "asymmetric" ? Asymmetric(side) : Cold(side);
    int angle = AngleFromSide(side);
    Dictionary<Vec3i, string> layout = RotatedLayoutOf(
      BlockBlastFurnaceCoreCold.Definitions("iwex").Single(),
      angle
    );

    // Both of AssertRotatedCell's oracles, for every cell the layout charges: the furnace's own
    // GetGlobalPos against independently-rotated offsets, and the resulting world cell against the
    // structure vanilla assembles. A key held in world space would be rotated a second time here.
    //
    // The hearth-floor row (-1,1,0)/(0,1,0)/(1,1,0) is what makes this a rotation test rather than a
    // shape that survives any mapping: it is a line, so a quarter turn lays it along z instead of x,
    // unlike the 3x3 above it. Being centred, it separates north/south from east/west but not north from
    // south; the four-way statement lives on the taps, in
    // FurnaceRoleCellsTests.The_four_facings_are_four_different_tuyere_and_tap_footprints.
    List<Vec3i> cells = ChargeCellsInShaft(be);

    // Counted, so the loop below cannot pass by running zero times: 39 in the shipped box - the
    // hearth-floor row of three plus four full levels of 9 - and 27 when the box is narrowed to 3x2.
    Assert.Equal(shaft == "asymmetric" ? 27 : 39, cells.Count);

    foreach (Vec3i cell in cells) {
      Assert.Contains((cell.X, cell.Z), be.ShaftColumns.Keys);
      AssertRotatedCell(
        be,
        layout,
        be.Pos,
        angle,
        cell,
        // Both glyphs: the hearth course carries its own code - the same Chargeable role, but a string
        // that also admits solidified metal - so the three cells at y=1 are not ShaftGlyph.
        [ShaftGlyph, HearthGlyph],
        $"column ({cell.X}, {cell.Z}) at y={cell.Y}"
      );
    }
  }

  [Fact]
  public void One_named_corner_column_is_a_different_world_cell_at_every_facing() {
    // The set-level assertions above cannot see this: the 3x3 set is closed under rotation while this
    // cell is not. The key names a place in the furnace, and where that place is in the world is a
    // question only the facing can answer.
    var cells = new List<BlockPos>();
    foreach (string side in new[] { "north", "east", "south", "west" }) {
      BlockEntityBlastFurnaceCold be = Cold(side);
      Assert.Contains((-1, -1), be.ShaftColumns.Keys);

      Vec3i local = new(-1, 1, -1); // the shaft's north-west corner, on the hearth floor
      Vec3i rotated = ExOrientation.RotateOffset(local, AngleFromSide(side));
      BlockPos world = Global(be, local);

      Assert.Equal(
        new BlockPos(
          Anchor.X + rotated.X,
          Anchor.Y + rotated.Y,
          Anchor.Z + rotated.Z,
          Anchor.dimension
        ),
        world
      );
      cells.Add(world);
    }

    Assert.Equal(4, cells.Distinct().Count());
  }

  [Fact]
  public void Every_column_of_a_built_cold_furnace_stands_inside_the_structure_it_completed() {
    // Against the real 160-cell footprint the furnace completed by itself, rather than against the
    // authored layout: the columns have to land on cells this machine owns.
    ColdBlastFurnaceRig rig = ColdBlastFurnaceScenes.Complete();
    var (min, max) = ShaftBoxOf(rig.Core)!.Value;

    Assert.Equal(BlastFurnaceColumns, Keys(rig.Core));
    foreach (var (x, z) in Keys(rig.Core))
      for (int y = min.Y; y <= max.Y; y++)
        Assert.True(
          rig.Core.OwnsCell(Global(rig.Core, new Vec3i(x, y, z))),
          $"column ({x}, {z}) at y={y} is outside the footprint the furnace completed"
        );
  }

  #endregion

  #region The branch that carries the invariant

  /// <summary>
  /// "Does this furnace hold a layered charge?" is answered by the type: both reverberatory hearths hang
  /// off <c>BlockEntityFireboxFurnace</c>, which cannot be a shaft furnace. Reparent either one onto the
  /// shaft branch and it fails here rather than in a save file with <c>chargeCol_</c> keys in it.
  /// </summary>
  [Fact]
  public void The_reverberatory_hearths_are_on_the_firebox_branch_not_the_shaft_branch() {
    foreach (
      Type hearth in new[]
      {
        typeof(BlockEntityPuddlingFurnace),
        typeof(BlockEntityHeatingFurnace),
      }
    ) {
      Assert.True(
        typeof(BlockEntityFireboxFurnace).IsAssignableFrom(hearth),
        $"{hearth.Name} is a hearth and must extend BlockEntityFireboxFurnace"
      );
      Assert.False(
        typeof(BlockEntityShaftFurnace).IsAssignableFrom(hearth),
        $"{hearth.Name} is a hearth and must not be a shaft furnace"
      );
    }

    // The converse, so the pair cannot be satisfied by collapsing one branch into the other.
    Assert.True(
      typeof(BlockEntityShaftFurnace).IsAssignableFrom(
        typeof(BlockEntityBlastFurnaceCold)
      )
    );
    Assert.False(
      typeof(BlockEntityFireboxFurnace).IsAssignableFrom(
        typeof(BlockEntityShaftFurnace)
      )
    );
  }

  /// <summary>
  /// The flag belongs to a branch, never to a leaf: restated on a concrete furnace it drifts out of step
  /// with the class it was declared on. The scan walks the whole loaded closure rather than iwex alone,
  /// because smex ships furnace leaves too and runs the same assertion over its own host
  /// (<see cref="FurnaceBranchGuards"/>).
  /// </summary>
  [Fact]
  public void No_concrete_furnace_declares_the_layered_charge_flag_itself() =>
    FurnaceBranchGuards.TheBranchOwnsTheLayeredChargeFlag();

  /// <summary>
  /// The state label is a read and nothing may assign it - see
  /// <see cref="FurnaceBranchGuards.NoFurnaceExposesASettableState"/> for why a private setter is not
  /// good enough.
  /// </summary>
  [Fact]
  public void No_furnace_exposes_a_settable_state() =>
    FurnaceBranchGuards.NoFurnaceExposesASettableState();

  #endregion

  #region Opt-out - the reverberatory hearths

  [Fact]
  public void The_reverberatory_hearths_allocate_no_columns_at_all() {
    BlockEntityPuddlingFurnace puddling = Puddling();
    BlockEntityHeatingFurnace heating = Heating();

    foreach (
      BlockEntityFurnaceCore hearth in new BlockEntityFurnaceCore[]
      {
        puddling,
        heating,
      }
    ) {
      Assert.False(
        (bool)ReflectionHelpers.GetProperty(hearth, "ShaftHoldsLayeredCharge")!
      );
      Assert.Empty(hearth.ShaftColumns);
      Assert.Equal(0, hearth.ShaftChargeUnits);
    }

    // Not even at their own firebox cells, which is what the base's "shaft" walk points at for them.
    Assert.Null(puddling.ChargeColumnAt(-5, 0));
    Assert.Null(heating.ChargeColumnAt(-5, 0));
    Assert.Null(heating.ChargeColumnAt(-5, 1));
  }

  [Fact]
  public void A_puddling_hearth_writes_the_shaft_furnaces_tree_minus_the_columns_and_the_pools() {
    BlockEntityPuddlingFurnace hearth = Puddling();
    BlockEntityBlastFurnaceCold shaft = Cold();

    string[] hearthKeys = TreeKeys(hearth);
    string[] shaftKeys = TreeKeys(shaft);

    // A puddling furnace pours nothing and cannot reach Melting, so it writes neither molten-pool key.
    // Stated as a set difference rather than as a literal list of keys, so the furnace can grow: a key
    // added to the core lands in both trees and this still holds, while a hearth that started writing a
    // column key fails immediately.
    Assert.Empty(hearthKeys.Except(shaftKeys));
    Assert.Equal(
      ColumnKeysOf(shaft)
        .Concat(["moltenIron", "moltenSlag"])
        .OrderBy(k => k, StringComparer.Ordinal),
      shaftKeys.Except(hearthKeys).OrderBy(k => k, StringComparer.Ordinal)
    );

    // The two hearths write the same tree, which is what "adopt the firebox defaults" means at the save
    // file, and the assertion a pool re-added to either leaf would have to get past.
    Assert.Equal(TreeKeys(Heating()), hearthKeys);
  }

  [Fact]
  public void A_heating_hearth_writes_the_shaft_furnaces_tree_minus_the_columns_and_the_pools() {
    BlockEntityHeatingFurnace hearth = Heating();
    BlockEntityBlastFurnaceCold shaft = Cold();

    string[] hearthKeys = TreeKeys(hearth);
    string[] shaftKeys = TreeKeys(shaft);

    // A hearth melts nothing, so it overrides none of the core's molten-product members and writes
    // neither pool key - which makes the shaft furnace's two the only other difference there can be.
    Assert.Empty(hearthKeys.Except(shaftKeys));
    Assert.Equal(
      ColumnKeysOf(shaft)
        .Concat(["moltenIron", "moltenSlag"])
        .OrderBy(k => k, StringComparer.Ordinal),
      shaftKeys.Except(hearthKeys).OrderBy(k => k, StringComparer.Ordinal)
    );
  }

  [Fact]
  public void Reading_the_column_api_on_a_hearth_does_not_make_it_start_saving_columns() {
    BlockEntityHeatingFurnace hearth = Heating();
    string[] before = TreeKeys(hearth);

    _ = hearth.ShaftColumns;
    _ = hearth.ChargeColumnAt(-5, 0);
    _ = hearth.ShaftChargeUnits;

    // The set is built lazily, so "a hearth writes nothing" has to survive someone having asked it for
    // its columns first - otherwise the guarantee would depend on call order.
    Assert.Equal(before, TreeKeys(hearth));
    Assert.DoesNotContain(
      TreeKeys(hearth),
      k => k.StartsWith("chargeCol", StringComparison.Ordinal)
    );
  }

  #endregion

  #region Persistence

  [Fact]
  public void A_multi_column_multi_segment_shaft_round_trips_every_column_and_every_band() {
    BlockEntityBlastFurnaceCold saved = Cold();
    saved.ChargeColumnAt(-1, -1)!.Push(Coke, 3, 1180.5f, default);
    saved.ChargeColumnAt(-1, -1)!.Push(Burden, 9, 1140.25f, Fluxed);
    saved.ChargeColumnAt(0, 0)!.Push(Burden, 11, 940.125f, Lean);
    saved.ChargeColumnAt(1, 1)!.Push(Charcoal, 4, 980f, default);

    BlockEntityBlastFurnaceCold loaded = Cold();
    loaded.FromTreeAttributes(Saved(saved), loaded.Api.World);

    Assert.Equal(27, loaded.ShaftChargeUnits);
    Assert.Equal(
      saved.ChargeColumnAt(-1, -1)!.Segments,
      loaded.ChargeColumnAt(-1, -1)!.Segments
    );
    Assert.Equal(
      saved.ChargeColumnAt(0, 0)!.Segments,
      loaded.ChargeColumnAt(0, 0)!.Segments
    );
    Assert.Equal(
      saved.ChargeColumnAt(1, 1)!.Segments,
      loaded.ChargeColumnAt(1, 1)!.Segments
    );
    // Spot-check the fields a per-column write could transpose between columns.
    Assert.Equal(Lean, loaded.ChargeColumnAt(0, 0)!.Segments[0].Mix);
    Assert.Equal(
      1180.5f,
      loaded.ChargeColumnAt(-1, -1)!.Segments[0].Temperature
    );

    // ...and the six untouched columns come back empty rather than as copies of the last one written,
    // which is what one flat tree for nine columns would have produced.
    foreach (var entry in loaded.ShaftColumns)
      if (entry.Key != (-1, -1) && entry.Key != (0, 0) && entry.Key != (1, 1))
        Assert.Empty(entry.Value.Segments);
  }

  [Fact]
  public void A_saved_column_outside_the_current_shaft_box_is_dropped_without_disturbing_a_valid_one() {
    BlockEntityBlastFurnaceCold saved = Cold();
    saved.ChargeColumnAt(0, 0)!.Push(Burden, 11, 940f, Lean);

    var tree = new TreeAttribute();
    saved.ToTreeAttributes(tree);

    // A furnace rebuilt to a different shape, or a save written before the shaft box moved: the key is
    // not one this shaft has any more.
    var strayColumn = new ChargeColumn();
    strayColumn.Push(Charcoal, 99, 500f, default);
    var stray = new TreeAttribute();
    strayColumn.ToTree(stray);
    tree["chargeCol_7_-4"] = stray;

    BlockEntityBlastFurnaceCold loaded = Cold();
    loaded.FromTreeAttributes(Serialized(tree), loaded.Api.World); // must not throw

    Assert.Null(loaded.ChargeColumnAt(7, -4));
    // Dropped, not folded onto a valid key: 99 units of charcoal turning up in a column the player never
    // charged is worse than losing a column that no longer has anywhere to be.
    Assert.Equal(11, loaded.ShaftChargeUnits);
    Assert.Equal(Burden, loaded.ChargeColumnAt(0, 0)!.TopMaterial);
  }

  [Fact]
  public void A_column_the_save_does_not_describe_comes_back_empty() {
    BlockEntityBlastFurnaceCold saved = Cold();
    saved.ChargeColumnAt(0, 0)!.Push(Burden, 11, 940f, Lean);

    var tree = new TreeAttribute();
    saved.ToTreeAttributes(tree);
    tree.RemoveAttribute("chargeCol_-1_-1");

    BlockEntityBlastFurnaceCold loaded = Cold();
    loaded.ChargeColumnAt(-1, -1)!.Push(Coke, 5, 20f, default);
    loaded.FromTreeAttributes(Serialized(tree), loaded.Api.World);

    // The save wins over what the column happened to be holding, exactly as ChargeColumn.FromTree
    // replaces rather than appends - or a reload would leave last session's charge behind.
    Assert.Equal(0, loaded.ChargeColumnAt(-1, -1)!.TotalUnits);
    Assert.Equal(11, loaded.ShaftChargeUnits);
  }

  [Fact]
  public void A_tree_that_mentions_no_column_at_all_leaves_the_shaft_alone() {
    BlockEntityBlastFurnaceCold be = Cold();
    be.ChargeColumnAt(0, 0)!.Push(Burden, 11, 940f, Lean);

    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);
    foreach (string key in ColumnKeysOf(be))
      tree.RemoveAttribute(key);

    be.FromTreeAttributes(Serialized(tree), be.Api.World);

    // A save from before the columns existed, or a partial tree, is not the world saying the shaft is
    // empty - clearing on it would delete the charge of every furnace in an upgrading world.
    Assert.Equal(11, be.ShaftChargeUnits);
  }

  [Fact]
  public void The_save_is_keyed_in_the_furnace_frame_so_a_reload_facing_elsewhere_keeps_each_corner() {
    BlockEntityBlastFurnaceCold north = Cold("north");
    north.ChargeColumnAt(-1, -1)!.Push(Coke, 3, 1180f, default);
    north.ChargeColumnAt(1, 1)!.Push(Burden, 9, 1140f, Fluxed);

    BlockEntityBlastFurnaceCold west = Cold("west");
    west.FromTreeAttributes(Saved(north), west.Api.World);

    Assert.Equal(Coke, west.ChargeColumnAt(-1, -1)!.TopMaterial);
    Assert.Equal(Burden, west.ChargeColumnAt(1, 1)!.TopMaterial);

    // The same key names a different physical cell at the two facings, so a save keyed in world space
    // would have had to move the charge to stay true.
    Assert.NotEqual(
      Global(north, new Vec3i(-1, 1, -1)),
      Global(west, new Vec3i(-1, 1, -1))
    );
  }

  [Theory]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void An_asymmetric_shaft_reloads_both_its_far_corners_after_a_quarter_turn(
    string side
  ) {
    BlockEntityBlastFurnaceCold north = Asymmetric("north");
    north.ChargeColumnAt(-1, 0)!.Push(Coke, 30, 1180f, default);
    north.ChargeColumnAt(1, 1)!.Push(Burden, 40, 1140f, Fluxed);

    BlockEntityBlastFurnaceCold turned = Asymmetric(side);
    turned.FromTreeAttributes(Saved(north), turned.Api.World);

    // The second, independent catcher for columns derived from the world box, reached through ColumnKey,
    // the tree and the bytes rather than through the Geometry region's key-set equality. The two corners
    // are chosen so that a rotated 3x2 box is missing one of them at every turned facing - (1,1) at east
    // and south, (-1,0) at west - which loses the column rather than renaming it: the charge is dropped
    // on load and the shaft comes back 30 or 40 units light.
    Assert.NotNull(turned.ChargeColumnAt(-1, 0));
    Assert.NotNull(turned.ChargeColumnAt(1, 1));
    Assert.Equal(70, turned.ShaftChargeUnits);
    Assert.Equal(30, turned.ChargeColumnAt(-1, 0)!.TotalUnits);
    Assert.Equal(Coke, turned.ChargeColumnAt(-1, 0)!.TopMaterial);
    Assert.Equal(40, turned.ChargeColumnAt(1, 1)!.TotalUnits);
    Assert.Equal(Burden, turned.ChargeColumnAt(1, 1)!.TopMaterial);
  }

  [Fact]
  public void A_shaft_saved_where_minus_is_not_a_hyphen_reloads_whole_somewhere_it_is() {
    TreeAttribute? saved = null;

    InCulture(
      Hostile(),
      () => {
        // Eight of the nine column keys carry a sign and only (0, 0) does not, so a culture-formatted key
        // leaves the guard for "no column key at all" seeing a described shaft and clearing the other
        // eight instead of leaving them alone.
        BlockEntityBlastFurnaceCold be = Cold();
        be.ChargeColumnAt(-1, -1)!.Push(Coke, 30, 1180f, default);
        be.ChargeColumnAt(-1, 0)!.Push(Burden, 40, 1140f, Fluxed);
        be.ChargeColumnAt(0, -1)!.Push(Burden, 50, 1100f, Lean);
        be.ChargeColumnAt(0, 0)!.Push(Burden, 60, 1060f, Lean);
        Assert.Equal(180, be.ShaftChargeUnits);

        saved = Saved(be);

        // The culture really is hostile - otherwise the whole test would pass by not being a test.
        Assert.Equal(
          MinusSign + "1",
          (-1).ToString(CultureInfo.CurrentCulture)
        );
      }
    );

    InCulture(
      CultureInfo.InvariantCulture,
      () => {
        // A different machine, or the same one after the player changed their regional format. With a
        // culture-formatted key 180 u goes into the save and 60 u comes back out, because only the
        // sign-free column matches.
        BlockEntityBlastFurnaceCold loaded = Cold();
        loaded.FromTreeAttributes(saved!, loaded.Api.World);

        Assert.Equal(180, loaded.ShaftChargeUnits);
        Assert.Equal(30, loaded.ChargeColumnAt(-1, -1)!.TotalUnits);
        Assert.Equal(40, loaded.ChargeColumnAt(-1, 0)!.TotalUnits);
        Assert.Equal(50, loaded.ChargeColumnAt(0, -1)!.TotalUnits);
        Assert.Equal(60, loaded.ChargeColumnAt(0, 0)!.TotalUnits);
      }
    );

    // The mechanism behind the reload above: the key the hostile culture produced is ASCII throughout.
    Assert.Contains("chargeCol_-1_-1", saved!.Keys);
    Assert.DoesNotContain(
      saved.Keys,
      k => k.Contains(MinusSign, StringComparison.Ordinal)
    );
  }

  #endregion

  #region The shaft with no box

  // No shipped furnace can reach a box-less state - every drawing marks its fuel - so these degradations
  // are pinned only by fixtures written for them. Columns and their save path are this file's subject,
  // so their box-less answers belong here.

  /// <summary>
  /// A shaft furnace drawn with real shaft cells and no <c>Chargeable</c> role - the same construction
  /// <c>FurnaceRoleCellsTests.NoFuelRoleDef</c> uses, restated rather than shared so this region stands
  /// on its own.
  /// </summary>
  private static ExBlockDef NoFuelRoleDef() =>
    ExBlockDef
      .Create("iwex", "furnace")
      .MultiblockLayout(s =>
        s.Origin(0, 0)
          .Legend('C', "iwex:furnace-blastcore-*")
          .Legend('c', ShaftGlyph)
          .Layer(0, "C")
          .Layer(1, "c")
          .Layer(2, "c")
      );

  private static BlockEntityBlastFurnaceCold BoxLess(out StructureRig rig) {
    var be = new BlockEntityBlastFurnaceCold();
    rig = Stand(
      be,
      NoFuelRoleDef(),
      Anchor,
      "iwex:furnace-blastcore-tier1",
      "north"
    );
    return be;
  }

  [Fact]
  public void A_shaft_with_no_box_holds_no_charge_rather_than_charge_over_an_invented_box() {
    BlockEntityBlastFurnaceCold be = BoxLess(out StructureRig rig);

    // A cell the drawing draws as shaft and never marks. A box invented at the anchor - the one
    // degenerate pair two corners can express - covers exactly this cell, and no other assertion in the
    // suite separates that fallback from the guard.
    BlockPos cell = rig.Cell(0, 1, 0);

    Assert.Null(ShaftBoxOf(be));
    Assert.Empty(be.ShaftColumns);
    Assert.Null(be.ChargeColumnAt(cell, out int index));
    Assert.Equal(-1, index);

    // The positive control, without which the lines above pass for the wrong reason: the identical cell
    // of a furnace that does mark its shaft resolves to a real column at block index 0, so the emptiness
    // is the guard and not a fixture that stood nothing up.
    var marked = new BlockEntityBlastFurnaceCold();
    StructureRig markedRig = Stand(
      marked,
      BlockBlastFurnaceCoreCold.Definitions("iwex").Single(),
      Anchor,
      "iwex:furnace-blastcore-tier1",
      "north"
    );

    Assert.NotEmpty(marked.ShaftColumns);
    Assert.NotNull(
      marked.ChargeColumnAt(markedRig.Cell(0, 1, 0), out int markedIndex)
    );
    Assert.Equal(0, markedIndex);
  }

  [Fact]
  public void A_shaft_with_no_box_saves_and_loads_without_a_column_and_without_throwing() {
    BlockEntityBlastFurnaceCold be = BoxLess(out _);

    // EnsureShaftColumns is allowed to build nothing, and a throw out of ToTreeAttributes is a chunk that
    // will not save.
    Assert.Empty(be.ShaftColumns);
    Assert.DoesNotContain(
      TreeKeys(be),
      k => k.StartsWith("chargeCol", StringComparison.Ordinal)
    );

    // The load half, given a tree that does carry columns - a save written before the drawing lost its
    // role, which is the shape the read guard has to survive. The keys are left alone; this furnace has
    // nowhere to put them.
    BlockEntityBlastFurnaceCold charged = Cold();
    charged.ChargeColumnAt(0, 0)!.Push(Burden, 11, 940f, Lean);
    TreeAttribute saved = Saved(charged);

    be.FromTreeAttributes(saved, be.Api.World);

    Assert.Empty(be.ShaftColumns);
    Assert.Equal(0, be.ShaftChargeUnits);
  }

  [Fact]
  public void A_shaft_asked_for_its_columns_before_its_drawing_arrives_still_gets_them() {
    // EnsureShaftColumns runs out of FromTreeAttributes, which the engine calls before Initialize. A
    // furnace that memoised either the empty box or the empty column set there would own no columns for
    // the rest of its life and come back from every reload empty, so neither cache may be written from a
    // derivation that found nothing.
    var be = new BlockEntityBlastFurnaceCold();
    Orient(be, "iwex:furnace-blastcore-tier1-n", "north"); // a block carrying no attributes at all

    Assert.Null(ShaftBoxOf(be));
    Assert.Empty(be.ShaftColumns);
    Assert.Equal(0, be.ShaftChargeUnits); // the read that runs EnsureShaftColumns

    // The drawing arrives and the furnace recovers, charge included.
    Stand(
      be,
      BlockBlastFurnaceCoreCold.Definitions("iwex").Single(),
      Anchor,
      "iwex:furnace-blastcore-tier1",
      "north"
    );

    Assert.Equal(BlastFurnaceColumns, Keys(be));
    be.ChargeColumnAt(-1, -1)!.Push(Coke, 7, 1180f, default);
    Assert.Equal(7, be.ShaftChargeUnits);
  }

  #endregion
}
