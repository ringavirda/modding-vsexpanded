using ExpandedLib.Blocks.Structures;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Casting;
using IronIndustryExpanded.BlockStructures.Casting.BlockEntities;
using IronIndustryExpanded.Items;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The sand casting bed's two mass-critical pieces as pure logic (the megablock wiring is verified
/// in-game): harvest denomination, which splits hardened units into pigs/chunks/bits without creating
/// matter, and the internal flow edge, where metal moves basin-outward, a mold keeps its charge, and
/// unlike metals do not mix. Iron melts at 1500 C.
/// </summary>
public class SandCastingBedTests {
  private const string Iron = "game:ingot-iron";
  private const string Steel = "game:ingot-steel";

  #region Denomination (mass conservation)

  [Theory]
  [InlineData(750, 2, 0, 0)] // a full end-row mold: two pigs
  [InlineData(1125, 3, 0, 0)] // a full middle-row mold: three pigs - the rows are not the same size
  [InlineData(375, 1, 0, 0)] // one pig
  [InlineData(400, 1, 1, 0)] // pig + chunk
  [InlineData(380, 1, 0, 1)] // pig + bit
  [InlineData(50, 0, 2, 0)] // partial: two chunks
  [InlineData(20, 0, 0, 4)] // runner residue: four bits
  [InlineData(3, 0, 0, 0)] // sub-bit crumb: nothing collectable
  [InlineData(4500, 12, 0, 0)] // a whole bed's worth
  public void Denominate_splits_units_into_pigs_chunks_bits(
    int units,
    int pigs,
    int chunks,
    int bits
  ) {
    Assert.Equal(
      (pigs, chunks, bits),
      BlockEntitySandCastingBed.Denominate(units)
    );
  }

  // Values either side of a pig boundary, so a change to the unit masses cannot make the cases vacuous.
  [Theory]
  [InlineData(0)]
  [InlineData(7)]
  [InlineData(374)] // one unit short of a pig
  [InlineData(376)] // one unit over
  [InlineData(749)] // one short of two
  [InlineData(452)]
  public void Denominate_never_creates_matter(int units) {
    (int pigs, int chunks, int bits) = BlockEntitySandCastingBed.Denominate(
      units
    );
    int recovered =
      pigs * ItemPig.PigUnits
      + chunks * ItemPig.ChunkUnits
      + bits * ItemPig.BitUnits;
    Assert.True(recovered <= units); // never more than was poured
    Assert.True(units - recovered < 5); // only a sub-bit crumb is ever lost
  }

  #endregion

  #region What a cell yields

  [Fact]
  public void Only_a_carved_mold_yields_a_casting() {
    // One mold shape casts both products, so the yield depends only on whether a cavity was cut there,
    // not on which metal reached the cell.
    Assert.True(BlockEntitySandCastingBed.YieldsCasting(BedSlotState.Mold));
  }

  [Fact]
  public void A_runners_stranded_charge_is_scrap_however_good_the_metal_was() {
    // A runner is a conduit, not a cavity, so its stranded charge denominates as recovered bits.
    Assert.False(BlockEntitySandCastingBed.YieldsCasting(BedSlotState.Runner));
  }

  [Fact]
  public void Uncarved_sand_casts_nothing_whatever_is_in_it() {
    // A charge in a slot that was never cut, or has been shaken out, comes back only as bits.
    Assert.False(BlockEntitySandCastingBed.YieldsCasting(BedSlotState.Sand));
  }

  #endregion

  #region Flow edge

  [Fact]
  public void FlowEdge_moves_metal_from_the_fuller_cell_toward_the_emptier() {
    var w = NewWorld();
    var runner = Cell(w, "{ \"capacity\": 50 }", 50);
    var mold = Cell(w, "{ \"capacity\": 750, \"drainFitting\": true }", 0);

    BlockEntitySandCastingBed.FlowEdge(runner, mold, w.World);

    Assert.True(mold.CellAmount > 0); // charge advanced into the mold
    Assert.Equal(50, mold.CellAmount + runner.CellAmount); // mass conserved across the edge
  }

  [Fact]
  public void A_mold_hoards_its_charge_and_never_drains_back_out() {
    var w = NewWorld();
    var mold = Cell(w, "{ \"capacity\": 750, \"drainFitting\": true }", 100);
    var runner = Cell(w, "{ \"capacity\": 50 }", 0);

    BlockEntitySandCastingBed.FlowEdge(mold, runner, w.World);

    Assert.Equal(100, mold.CellAmount); // the mold gave nothing back
    Assert.Equal(0, runner.CellAmount);
  }

  [Fact]
  public void Unlike_metals_do_not_mix_across_the_edge() {
    var w = NewWorld();
    var a = Cell(w, "{ \"capacity\": 100 }", 60, Iron);
    var b = Cell(w, "{ \"capacity\": 100, \"drainFitting\": true }", 20, Steel);

    BlockEntitySandCastingBed.FlowEdge(a, b, w.World);

    Assert.Equal(60, a.CellAmount); // refused - different metal already present
    Assert.Equal(20, b.CellAmount);
  }

  #endregion

  #region Fixture

  private static TestWorld NewWorld() {
    var w = new TestWorld();
    w.RegisterItem(Iron, 1500f);
    w.RegisterItem(Steel, 1500f);
    return w;
  }

  private static BEBehaviorMoltenCell Cell(
    TestWorld world,
    string props,
    int amount,
    string metal = Iron
  ) {
    var filler = TestBlocks.Configure(
      new BlockStructureFiller(),
      "exlib:structurefiller",
      70
    );
    var be = new BlockEntityStructureFiller();
    world.Place(new BlockPos(0, 0, 0), filler, be);
    world.Attach(be);
    var cell = new BEBehaviorMoltenCell(be);
    be.Behaviors.Add(cell);
    cell.ConfigureFromFiller(null, null, new JsonObject(JToken.Parse(props)));
    if (amount > 0)
      cell.PushMetalRaw(amount, metal, 1500f, world.World);
    return cell;
  }

  #endregion
}
