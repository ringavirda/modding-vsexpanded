using ExpandedLib.Testing;
using LowPressureExpanded.BlockNetworkPipe.BlockEntities;
using LowPressureExpanded.BlockNetworkPipe.Blocks;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// The pressure valve's vent readout (<c>_lastVentVolume</c>) is set only by the server-side tick and
/// read by <c>GetBlockInfo</c>, which also runs client-side. Unless the value is written to the tree,
/// the client copy never sees anything but its zero default.
/// </summary>
public class PressureValveReadoutTests {
  private static BlockPressureValve ValveBlock() {
    var block = TestBlocks.Configure(
      new BlockPressureValve(),
      "lpex:pressurevalve-iron-ns",
      40,
      ("material", "iron"),
      ("type", "pressurevalve"),
      ("orientation", "ns")
    );
    ReflectionHelpers.SetProperty(block, "Type", "pressurevalve");
    ReflectionHelpers.SetProperty(block, "Orientation", "ns");
    return block;
  }

  private static BlockEntityPressureValve NewValve(TestWorld world) {
    var be = new BlockEntityPressureValve {
      Pos = new BlockPos(0, 0, 0),
      Block = ValveBlock(),
    };
    world.Attach(be);
    return be;
  }

  private static float LastVentVolumeOf(BlockEntityPressureValve be) =>
    (float)ReflectionHelpers.GetField(be, "_lastVentVolume")!;

  [Fact]
  public void The_vent_volume_survives_a_tree_round_trip() {
    // GetBlockInfo runs client-side, where only what ToTreeAttributes wrote exists. A field the
    // server tick sets and the tree omits reads zero on the client for ever.
    var world = new TestWorld();
    var src = NewValve(world);
    ReflectionHelpers.SetField(src, "_lastVentVolume", 12.5f);

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);

    var restored = NewValve(world);
    restored.FromTreeAttributes(tree, world.World);

    Assert.Equal(12.5f, LastVentVolumeOf(restored), 3);
  }
}
