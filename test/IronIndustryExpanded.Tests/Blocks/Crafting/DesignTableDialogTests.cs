using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Crafting.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The design table's dialog teardown: <c>GuiDialogDesignTable</c> needs a real
/// <c>ICoreClientAPI</c>, which the headless <see cref="TestWorld"/> does not provide (it wires a
/// server-side API only), so the window itself is never opened here. What is reachable headlessly, and
/// what regresses the bug this guards against, is covered instead: both teardown paths actually call
/// <c>CloseDialog</c> (not merely declare an override that forgets to), and calling them in the
/// overwhelmingly common case - no window ever opened - does not throw and leaves the dialog field
/// cleared.
/// </summary>
public class DesignTableDialogTests {
  private sealed class SpyDesignTable : BlockEntityDesignTable {
    public int CloseDialogCalls { get; private set; }

    // The window teardown now lives on exlib's BlockEntityMachineStation, so this guards every
    // machine station, not only the design table.
    protected override void CloseWindow() {
      CloseDialogCalls++;
      base.CloseWindow();
    }
  }

  private static T Table<T>(TestWorld world, BlockPos pos)
    where T : BlockEntityDesignTable, new() {
    var be = new T {
      Pos = pos,
      Block = TestBlocks.Configure(new Block(), "iiex:designtable", 100),
    };
    world.Place(pos, be.Block, be);
    world.Initialize(be);
    return be;
  }

  private static BlockEntityDesignTable Table(TestWorld world, BlockPos pos) =>
    Table<BlockEntityDesignTable>(world, pos);

  [Fact]
  public void OnBlockRemoved_and_OnBlockUnloaded_both_call_CloseDialog() {
    var world = new TestWorld();
    var be = Table<SpyDesignTable>(world, new BlockPos(0, 1, 0));

    // A deleted CloseDialog(); call leaves the override signature intact (it still compiles as
    // `public override void OnBlockRemoved() { base.OnBlockRemoved(); }`), so nothing short of
    // observing the call itself catches that regression - the dead-override shape Task 3 forbids.
    be.OnBlockRemoved();
    Assert.Equal(1, be.CloseDialogCalls);

    be.OnBlockUnloaded();
    Assert.Equal(2, be.CloseDialogCalls);
  }

  [Fact]
  public void Removal_and_unload_leave_the_dialog_field_cleared_when_none_was_open() {
    var world = new TestWorld();
    var table = Table(world, new BlockPos(0, 1, 0));

    // The dialog field starts null here (no client API to open one against). CloseDialog reads it
    // through null-conditional operators; a regression that drops the "?" would NRE on exactly this
    // path, which is the normal case for a block broken or unloaded with its window closed.
    table.OnBlockRemoved();
    Assert.Null(ReflectionHelpers.GetField(table, "_dialog"));

    table.OnBlockUnloaded();
    Assert.Null(ReflectionHelpers.GetField(table, "_dialog"));
  }
}
