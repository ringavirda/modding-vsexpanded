using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Forming;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The reheat furnace putting heat back into stock - the one thing it exists for, and the step that closes
/// the forming loop: the mill refuses a piece below rolling heat, so without a soak a schedule of round
/// trips dead-ends at the first piece that goes cold.
/// <para>
/// The soak is hosted on the furnace's melt cycle, so these cases drive the machine's own production tick
/// rather than calling <c>SoakTick</c> by hand wherever they can. A soak that works only when invoked
/// directly is the defect, not the feature.
/// </para>
/// </summary>
public class ReheatSoakTests {
  #region Harness

  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>
  /// The bed cell in the heating furnace's own drawing: layer 0, column 4, row 2, against its
  /// <c>Origin(-6, -2)</c>. A literal here rather than a read of <c>ShaftCentre</c>, which is the thing
  /// under test - an expectation computed from its subject agrees with any value of it.
  /// </summary>
  private static readonly Vec3i BedCell = new(-2, 0, 0);

  /// <summary>
  /// A reheat furnace stood at <paramref name="side"/> with a real hearth in its bed cell, placed before
  /// the rig raises its stand-ins so nothing has to be replaced afterwards. Its fireboxes carry
  /// <paramref name="unitsPerCell"/> of fuel; zero stands the machine up unlit.
  /// </summary>
  private static (
    BlockEntityHeatingFurnace Furnace,
    BlockEntityHeatingHearth Bed,
    StructureRig Rig
  ) Stood(string side = "north", int unitsPerCell = 0) {
    var furnace = new BlockEntityHeatingFurnace();
    var bed = new BlockEntityHeatingHearth();

    StructureRig rig = Stand(
      furnace,
      BlockHeatingFurnaceCore.Definitions("iiex").Single(),
      Anchor,
      "iiex:furnace-heatingcore-tier1",
      side,
      complete: false
    );

    // The legend is orientation-pinned, so a furnace facing south wants its hearth coded -s. The authored
    // letter satisfies north alone.
    string letter = ExOrientation.RotateOrientationToken(
      "n",
      ExOrientation.AngleFromSide(side)
    );
    BlockPos cell = rig.Cell(BedCell.X, BedCell.Y, BedCell.Z);
    bed.Pos = cell.Copy();
    rig.Occupy(
      cell,
      TestBlocks.Configure(
        new Block(),
        $"iiex:furnace-heatinghearth-{letter}",
        900,
        ("side", ExOrientation.SideFromAngle(ExOrientation.AngleFromSide(side)))
      ),
      bed
    );
    rig.Complete();

    if (unitsPerCell > 0)
      LoadFireboxes(rig, furnace, unitsPerCell);
    // The furnace resolved its parts when the structure completed, before the fuel beds existed.
    ReflectionHelpers.Invoke(furnace, "ScanForOutlets");
    return (furnace, bed, rig);
  }

  /// <summary>A furnace whose fireboxes are full, so a tick can light it.</summary>
  private static (
    BlockEntityHeatingFurnace Furnace,
    BlockEntityHeatingHearth Bed,
    StructureRig Rig
  ) Fuelled() => Stood(unitsPerCell: BEBehaviorFirebox.CellCapacity);

  /// <summary>Runs <paramref name="seconds"/> one-second production ticks through the furnace's own
  /// override, by reflection because <c>OnProductionTick</c> is <c>protected</c>.</summary>
  private static void Tick(BlockEntityFurnaceCore be, int seconds) {
    for (int i = 0; i < seconds; i++)
      ReflectionHelpers.Invoke(be, "OnProductionTick", 1f);
  }

  /// <summary>
  /// Ticks until the furnace is running its melt phase, so a soak assertion measures the soak rather than
  /// the climb to temperature. Asserts rather than gives up quietly: a fixture that ran zero cycles would
  /// make every case below vacuous.
  /// </summary>
  private static void BringToHeat(BlockEntityHeatingFurnace furnace) {
    for (int i = 0; i < 900 && furnace.State != FurnaceState.Melting; i++)
      Tick(furnace, 1);
    Assert.Equal(FurnaceState.Melting, furnace.State);
  }

  /// <summary>A piece of stock lying at <paramref name="tempC"/>, carrying its form on the item type the
  /// way a freshly crafted one does rather than on the stack.</summary>
  private static ItemStack Piece(
    StructureRig rig,
    StockForm form,
    float tempC = 20f
  ) {
    Item item = rig.World.RegisterItem($"iiex:stock-{form.Name}", 1500f);
    item.Attributes = new JsonObject(
      JToken.Parse($"{{ \"stockForm\": \"{form.Name}\" }}")
    );
    var stack = new ItemStack(item);
    stack.Collectible.SetTemperature(rig.World.World, stack, tempC);
    return stack;
  }

  /// <summary>
  /// How hot the piece lying in <paramref name="row"/> is. Read off the bed rather than off the stack the
  /// caller laid: <c>ItemSlot.TakeOut</c> hands over a clone, so the caller's copy stays at the temperature
  /// it was made with however hot the furnace gets - and every assertion against it would pass vacuously.
  /// </summary>
  private static float TempIn(
    StructureRig rig,
    BlockEntityHeatingHearth bed,
    HearthRows.Row row
  ) =>
    bed.StockIn(row) is { } stack
      ? stack.Collectible.GetTemperature(rig.World.World, stack)
      : float.NaN;

  private static void Lay(
    BlockEntityHeatingHearth bed,
    HearthRows.Row row,
    ItemStack stack
  ) =>
    Assert.True(
      bed.TryLoad(row, new DummySlot(stack)),
      $"the bed refused a piece in {row}"
    );

  private static float Internal(BlockEntityFurnaceCore be) =>
    (float)ReflectionHelpers.GetField(be, "_internalTemp")!;

  #endregion

  #region The core names its bed

  /// <summary>
  /// The link the whole unit hangs off. <c>ShaftCentrePos</c> is <c>GlobalOf(ShaftCentre)</c>, so it
  /// already carries the structure's angle; checked at all four facings because a second rotation anywhere
  /// in the lookup passes at north and finds brick at the other three.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_core_resolves_the_hearth_standing_in_its_bed_cell(string side) {
    var (furnace, bed, _) = Stood(side);

    Assert.True(
      furnace.StructureComplete,
      $"the furnace did not complete at {side}"
    );
    Assert.Same(bed, furnace.Hearth);
  }

  /// <summary>
  /// The bed cell is the one the drawing puts an <c>H</c> in. Stated separately because the case above
  /// would also pass if the lookup scanned a neighbourhood rather than reading one cell.
  /// </summary>
  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_bed_cell_is_the_rotated_shaft_centre(string side) {
    var (furnace, bed, rig) = Stood(side);

    Assert.Equal(rig.Cell(BedCell.X, BedCell.Y, BedCell.Z), bed.Pos);
    Assert.Equal(
      bed.Pos,
      (BlockPos)ReflectionHelpers.GetProperty(furnace, "ShaftCentrePos")!
    );
  }

  /// <summary>The control: a bed cell holding no hearth must read null rather than picking up whatever
  /// stand-in the rig raised there.</summary>
  [Fact]
  public void A_furnace_with_no_hearth_in_its_bed_cell_reads_null() {
    var furnace = new BlockEntityHeatingFurnace();
    Stand(
      furnace,
      BlockHeatingFurnaceCore.Definitions("iiex").Single(),
      Anchor,
      "iiex:furnace-heatingcore-tier1",
      "north"
    );

    Assert.Null(furnace.Hearth);
  }

  #endregion

  #region A lit furnace brings its stock back to heat

  [Fact]
  public void A_cold_piece_on_a_lit_hearth_rises() {
    var (furnace, bed, rig) = Fuelled();
    Lay(bed, HearthRows.Row.Left, Piece(rig, StockForm.ShingledBar));

    BringToHeat(furnace);
    Tick(furnace, 120);

    Assert.True(
      TempIn(rig, bed, HearthRows.Row.Left) > IiexValues.RollingTempC,
      $"the bar came up to only {TempIn(rig, bed, HearthRows.Row.Left):F0} C"
    );
  }

  /// <summary>
  /// The control, and the one that says the soak is the fire's rather than the clock's. An unlit furnace
  /// never crosses into its melt phase, so the cycle carrying the soak never runs.
  /// </summary>
  [Fact]
  public void An_unlit_furnace_soaks_nothing() {
    var (furnace, bed, rig) = Stood();
    Lay(bed, HearthRows.Row.Left, Piece(rig, StockForm.ShingledBar));

    Tick(furnace, 600);

    Assert.NotEqual(FurnaceState.Melting, furnace.State);
    Assert.True(
      TempIn(rig, bed, HearthRows.Row.Left) <= 20f,
      $"an unlit furnace warmed the bar to {TempIn(rig, bed, HearthRows.Row.Left):F0} C"
    );
  }

  /// <summary>The bed soaks three pieces at once for one fire, which is the whole of this machine's
  /// advantage over a forge.</summary>
  [Fact]
  public void Every_loaded_row_soaks() {
    var (furnace, bed, rig) = Fuelled();
    // Flanks before centre: a loaded centre blocks both flanks on the way in.
    Lay(bed, HearthRows.Row.Left, Piece(rig, StockForm.ShingledBar));
    Lay(bed, HearthRows.Row.Right, Piece(rig, StockForm.ShingledBar));
    Lay(bed, HearthRows.Row.Centre, Piece(rig, StockForm.ShingledBar));

    BringToHeat(furnace);
    Tick(furnace, 120);

    Assert.Equal(3, bed.LoadedRows);
    Assert.Equal(3, bed.RowsAtRollingHeat);
  }

  /// <summary>
  /// The area law, measured on the machine rather than on the formula: a slab presents less surface for its
  /// bulk than a bar, so it comes up slower - and it does so carrying three times the metal, which is
  /// exactly what a mass law would have got wrong in the other direction.
  /// </summary>
  [Fact]
  public void A_slab_comes_up_slower_than_a_bar() {
    var (furnace, bed, rig) = Fuelled();
    Lay(bed, HearthRows.Row.Left, Piece(rig, StockForm.ShingledBar));
    Lay(bed, HearthRows.Row.Right, Piece(rig, StockForm.ShingledSlab));

    BringToHeat(furnace);
    Tick(furnace, 30);

    float barC = TempIn(rig, bed, HearthRows.Row.Left);
    float slabC = TempIn(rig, bed, HearthRows.Row.Right);
    // The premise: both moved. Two pieces that never warmed at all would satisfy neither ordering nor
    // this, which is what makes the comparison a measurement rather than a coincidence.
    Assert.True(slabC > 20f, "neither piece soaked at all");
    Assert.True(
      barC > slabC,
      $"bar {barC:F0} C is no hotter than slab {slabC:F0} C"
    );
  }

  /// <summary>
  /// A piece can never come out hotter than the chamber holding it, however long it lies there: the
  /// approach is asymptotic, so no soak time overshoots.
  /// </summary>
  [Fact]
  public void A_piece_never_passes_the_chamber_it_lies_in() {
    var (furnace, bed, rig) = Fuelled();
    Lay(bed, HearthRows.Row.Left, Piece(rig, StockForm.ShingledBar));

    BringToHeat(furnace);
    Tick(furnace, 1_800);

    float reached = TempIn(rig, bed, HearthRows.Row.Left);
    // The premise again: a piece that never soaked is trivially under the chamber.
    Assert.True(reached > IiexValues.RollingTempC, "the bar never soaked");
    Assert.True(
      reached <= Internal(furnace) + 0.5f,
      $"the bar reached {reached:F0} C in a {Internal(furnace):F0} C chamber"
    );
  }

  /// <summary>
  /// A chamber run hotter than the stock's own melting point must not cook it on the bed. Nothing shipped
  /// reaches that today - the drawn stack tops this furnace out well under iron's 1500 - so the clamp is
  /// stated against a hand-run soak rather than against a retuned config.
  /// </summary>
  [Fact]
  public void A_piece_stops_at_its_own_melting_point() {
    var (_, bed, rig) = Fuelled();
    Lay(bed, HearthRows.Row.Left, Piece(rig, StockForm.ShingledBar));

    // Past anything the heat balance can produce; the clamp is the only thing holding it.
    for (int i = 0; i < 600; i++)
      bed.SoakTick(3000f, 1f);

    float reached = TempIn(rig, bed, HearthRows.Row.Left);
    // The premise: it climbed most of the way there, so the clamp is what stopped it rather than a soak
    // that never ran.
    Assert.True(reached > 1400f, $"the bar only reached {reached:F0} C");
    Assert.True(
      reached <= 1500f,
      $"the bar reached {reached:F0} C, past its 1500 C melting point"
    );
  }

  #endregion

  #region Break safety

  /// <summary>
  /// Every piece on the bed is unique - its own gauge, its own crop tally, its own heat - so a hearth that
  /// swallowed its contents on being broken would destroy work no recipe can make again.
  /// </summary>
  [Fact]
  public void Breaking_a_loaded_hearth_drops_its_pieces() {
    var (_, bed, rig) = Fuelled();
    Lay(bed, HearthRows.Row.Left, Piece(rig, StockForm.ShingledBar));
    Lay(bed, HearthRows.Row.Right, Piece(rig, StockForm.ShingledBar));
    rig.World.Drops.Clear();

    bed.OnBlockBroken();

    Assert.Equal(0, bed.LoadedRows);
    Assert.Equal(2, rig.World.Drops.Count);
  }

  #endregion
}
