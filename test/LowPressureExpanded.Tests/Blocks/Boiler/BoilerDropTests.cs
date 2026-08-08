using ExpandedLib.Testing;
using LowPressureExpanded.BlockStructures.Boiler.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// A boiler is built in place (RightClickConstructable), not placed from a frame item, so breaking it
/// must return only its construction materials - never the boiler block itself. The JSON "drops": []
/// isn't reliably honoured for a variant block, so the block overrides GetDrops to guarantee an empty
/// list; this pins that the override strips even a registry-populated self-drop. (Engines keep their
/// craftable-frame self-drop and don't derive from BlockBoiler.)
/// </summary>
public class BoilerDropTests
{
  [Fact]
  public void A_boiler_never_drops_itself_even_if_registered_with_a_self_drop()
  {
    var block = TestBlocks.Configure(
      new BlockBoilerCornish(),
      "lpex:boilercornish-n",
      1,
      ("side", "north")
    );
    block.Drops = [new BlockDropItemStack(new ItemStack(block))];

    ItemStack[] drops = block.GetDrops(null!, new BlockPos(0, 0, 0), null);

    Assert.Empty(drops);
  }
}
