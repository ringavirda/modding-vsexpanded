using ExpandedLib.Blocks.Structures;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Casting.BlockEntities;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The sand casting bed's two mass-critical pieces, tested as pure logic (the mega-block wiring is left
/// to in-game verification): the harvest denomination (hardened units -&gt; pigs/chunks/bits, never
/// creating matter) and the internal flow edge (metal moves basin-outward, a mold hoards its charge,
/// and unlike metals never mix). Iron melts at 1500 C.
/// </summary>
public class SandCastingBedTests
{
  private const string Iron = "game:ingot-iron";
  private const string Steel = "game:ingot-steel";

  #region Denomination (mass conservation)

  [Theory]
  [InlineData(300, 2, 0, 0)] // a full double-mold: two pigs
  [InlineData(150, 1, 0, 0)] // one pig
  [InlineData(175, 1, 1, 0)] // pig + chunk
  [InlineData(155, 1, 0, 1)] // pig + bit
  [InlineData(50, 0, 2, 0)] // partial: two chunks
  [InlineData(20, 0, 0, 4)] // runner residue: four bits
  [InlineData(3, 0, 0, 0)] // sub-bit crumb: nothing collectable
  [InlineData(1800, 12, 0, 0)] // a whole bed's worth
  public void Denominate_splits_units_into_pigs_chunks_bits(
    int units,
    int pigs,
    int chunks,
    int bits
  )
  {
    Assert.Equal((pigs, chunks, bits), BlockEntitySandCastingBed.Denominate(units));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(7)]
  [InlineData(149)]
  [InlineData(151)]
  [InlineData(299)]
  [InlineData(452)]
  public void Denominate_never_creates_matter(int units)
  {
    (int pigs, int chunks, int bits) = BlockEntitySandCastingBed.Denominate(units);
    int recovered = pigs * 150 + chunks * 25 + bits * 5;
    Assert.True(recovered <= units); // never more than was poured
    Assert.True(units - recovered < 5); // only a sub-bit crumb is ever lost
  }

  #endregion

  #region Flow edge

  [Fact]
  public void FlowEdge_moves_metal_from_the_fuller_cell_toward_the_emptier()
  {
    var w = NewWorld();
    var runner = Cell(w, "{ \"capacity\": 50 }", 50);
    var mold = Cell(w, "{ \"capacity\": 300, \"drainFitting\": true }", 0);

    BlockEntitySandCastingBed.FlowEdge(runner, mold, w.World);

    Assert.True(mold.CellAmount > 0); // charge advanced into the mold
    Assert.Equal(50, mold.CellAmount + runner.CellAmount); // mass conserved across the edge
  }

  [Fact]
  public void A_mold_hoards_its_charge_and_never_drains_back_out()
  {
    var w = NewWorld();
    var mold = Cell(w, "{ \"capacity\": 300, \"drainFitting\": true }", 100);
    var runner = Cell(w, "{ \"capacity\": 50 }", 0);

    BlockEntitySandCastingBed.FlowEdge(mold, runner, w.World);

    Assert.Equal(100, mold.CellAmount); // the mold gave nothing back
    Assert.Equal(0, runner.CellAmount);
  }

  [Fact]
  public void Unlike_metals_do_not_mix_across_the_edge()
  {
    var w = NewWorld();
    var a = Cell(w, "{ \"capacity\": 100 }", 60, Iron);
    var b = Cell(w, "{ \"capacity\": 100, \"drainFitting\": true }", 20, Steel);

    BlockEntitySandCastingBed.FlowEdge(a, b, w.World);

    Assert.Equal(60, a.CellAmount); // refused - different metal already present
    Assert.Equal(20, b.CellAmount);
  }

  #endregion

  #region Fixture

  private static TestWorld NewWorld()
  {
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
  )
  {
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
