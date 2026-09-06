using ExpandedLib.Blocks;
using ExpandedLib.Machines;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="BlockEntityMachineStation"/> had no <c>ToTreeAttributes</c>/<c>FromTreeAttributes</c> pair
/// of its own before this - vanilla's <c>BlockEntityContainer</c> already saves the inventory - so this
/// covers the new pair carrying a declared field alongside it.
/// </summary>
public class MachineStationStateTests {
  private sealed class StatefulStation : BlockEntityMachineStation {
    public int Setting;

    protected override MachineSlotSpec[] SlotSpecs => [];

    public override string InventoryClassName => "test-station";

    protected override void DeclareState(ExBlockState state) =>
      state.Int("setting", () => Setting, v => Setting = v);
  }

  [Fact]
  public void A_field_declared_through_State_round_trips() {
    // ToTreeAttributes reads Block.IsMissing before it ever reaches Persisted, so the station needs a
    // real block even though nothing here cares which one.
    var source = new StatefulStation {
      Setting = 3,
      Pos = new BlockPos(0, 0, 0),
      Block = TestBlocks.Configure(new Block(), "test:station", 1),
    };
    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);

    var target = new StatefulStation();
    target.FromTreeAttributes(tree, new TestWorld().World);

    Assert.Equal(3, target.Setting);
  }
}
