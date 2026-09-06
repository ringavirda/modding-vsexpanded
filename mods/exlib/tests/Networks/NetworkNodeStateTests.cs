using ExpandedLib.Blocks;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="BlockEntityNetworkNode"/> gets the same <c>Persisted</c>/<c>DeclareState</c> convenience as
/// <see cref="ExBlockEntity"/>, on top of the network-membership keys it already writes by hand.
/// </summary>
public class NetworkNodeStateTests {
  private sealed class StatefulNode : BlockEntityNetworkNode {
    public override string NetworkType { get; set; } = "test";
    public int Amount;

    protected override void DeclareState(ExBlockState state) =>
      state.Int("amount", () => Amount, v => Amount = v);
  }

  [Fact]
  public void A_field_declared_through_State_round_trips() {
    // ToTreeAttributes reads Block.IsMissing before it ever reaches Persisted, so the node needs a real
    // block even though nothing here cares which one.
    var source = new StatefulNode {
      Amount = 5,
      Pos = new BlockPos(0, 0, 0),
      Block = TestBlocks.Configure(new Block(), "test:node", 1),
    };
    var tree = new TreeAttribute();
    source.ToTreeAttributes(tree);

    var target = new StatefulNode();
    target.FromTreeAttributes(tree, new TestWorld().World);

    Assert.Equal(5, target.Amount);
  }
}
