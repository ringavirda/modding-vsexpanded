using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Boiler.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// A boiler is built in place (RightClickConstructable), not placed from a frame item, so breaking it
/// returns only its construction materials and never the boiler block itself. A JSON <c>"drops": []</c>
/// is not reliably honoured for a variant block, so the block overrides <c>GetDrops</c> to return an
/// empty list even when the registry populates a self-drop.
/// </summary>
public class BoilerDropTests {
  [Fact]
  public void A_boiler_never_drops_itself_even_if_registered_with_a_self_drop() {
    var block = TestBlocks.Configure(
      new BlockBoilerCornish(),
      "iiex:boilercornish-n",
      1,
      ("side", "north")
    );
    block.Drops = [new BlockDropItemStack(new ItemStack(block))];

    ItemStack[] drops = block.GetDrops(null!, new BlockPos(0, 0, 0), null);

    Assert.Empty(drops);
  }
}
