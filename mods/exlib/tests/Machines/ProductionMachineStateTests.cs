using ExpandedLib.Blocks;
using ExpandedLib.Machines;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="BlockEntityProductionMachine"/> gets the same <c>Persisted</c>/<c>DeclareState</c> convenience
/// as <see cref="ExBlockEntity"/>, layered on top of the hand-written <c>pm_lastHours</c> stamp it
/// already writes - a subclass that adds nothing must not disturb that stamp, and one that overrides the
/// pair itself must not be shadowed by it.
/// </summary>
public class ProductionMachineStateTests {
  private sealed class StatefulMachine : BlockEntityProductionMachine {
    public float Temp;

    protected override bool CanRunProduction => true;

    protected override void OnProductionTick(float dt) { }

    protected override void DeclareState(ExBlockState state) =>
      state.Float("temp", () => Temp, v => Temp = v);
  }

  private sealed class PlainMachine : BlockEntityProductionMachine {
    protected override bool CanRunProduction => true;

    protected override void OnProductionTick(float dt) { }

    public void StartTicking() => StartProductionTick();
  }

  private sealed class OverridingMachine : BlockEntityProductionMachine {
    public int Custom;

    protected override bool CanRunProduction => true;

    protected override void OnProductionTick(float dt) { }

    public override void ToTreeAttributes(ITreeAttribute tree) {
      base.ToTreeAttributes(tree);
      tree.SetInt("custom", Custom);
    }

    public override void FromTreeAttributes(
      ITreeAttribute tree,
      IWorldAccessor worldForResolving
    ) {
      base.FromTreeAttributes(tree, worldForResolving);
      Custom = tree.GetInt("custom");
    }
  }

  // ToTreeAttributes reads Block.IsMissing before it ever reaches Persisted, so every instance under test
  // needs a real block even though nothing here cares which one.
  private static void Place(BlockEntity be) {
    be.Pos = new BlockPos(0, 0, 0);
    be.Block = TestBlocks.Configure(new Block(), "test:machine", 1);
  }

  [Fact]
  public void A_field_declared_through_State_round_trips() {
    var source = new StatefulMachine { Temp = 42.5f };
    Place(source);
    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);

    var target = new StatefulMachine();
    Place(target);
    target.FromTreeAttributes(tree, new TestWorld().World);

    Assert.Equal(42.5f, target.Temp);
  }

  [Fact]
  public void A_subclass_that_declares_nothing_still_round_trips_its_base_fields() {
    var world = new TestWorld();
    var source = new PlainMachine();
    Place(source);
    world.Attach(source);
    source.StartTicking();
    world.FireBlockEntityTicks(); // stamps pm_lastHours off its -1 default

    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);
    double savedHours = tree.GetDouble("pm_lastHours", -1);
    Assert.True(savedHours >= 0);

    var target = new PlainMachine();
    Place(target);
    target.FromTreeAttributes(tree, world.World);

    var reread = new TreeAttribute();
    target.ToTreeAttributes(reread);
    Assert.Equal(savedHours, reread.GetDouble("pm_lastHours", -1));
  }

  [Fact]
  public void A_subclass_overriding_the_pair_and_calling_base_keeps_its_own_keys() {
    var source = new OverridingMachine { Custom = 7 };
    Place(source);
    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);

    var target = new OverridingMachine();
    Place(target);
    target.FromTreeAttributes(tree, new TestWorld().World);

    Assert.Equal(7, target.Custom);
  }
}
