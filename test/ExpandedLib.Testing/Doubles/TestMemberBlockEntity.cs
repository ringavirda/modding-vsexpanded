using ExpandedLib.Blocks.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Testing.Doubles;

/// <summary>
/// A bare block entity carrying a chosen set of network memberships, for testing the accessor and
/// the graph walk without a concrete machine.
/// </summary>
public sealed class TestMemberBlockEntity : BlockEntity {
  /// <summary>Adds one membership per network type and places the block entity at <paramref name="pos"/>
  /// on a plain block. Pass a block to <see cref="TestWorld.Place"/> afterwards to override it.</summary>
  public static TestMemberBlockEntity With(
    TestWorld world,
    BlockPos pos,
    params string[] networkTypes
  ) {
    var be = new TestMemberBlockEntity();
    foreach (string type in networkTypes)
      be.Behaviors.Add(new TestNetworkMember(be, type));
    world.Place(pos, TestBlocks.Configure(new Block(), "test:member", 899), be);
    return be;
  }

  /// <summary>Places a block entity carrying one membership whose declared network type and
  /// per-cell answer differ, for the position-aware side of the resolver. A filler cell is the real
  /// case: its type comes from the port recorded on the cell, not from the behaviour.</summary>
  public static TestMemberBlockEntity WithPerCellType(
    TestWorld world,
    BlockPos pos,
    string declared,
    string atCell
  ) {
    var be = new TestMemberBlockEntity();
    be.Behaviors.Add(new PerCellNetworkMember(be, declared, atCell));
    world.Place(pos, TestBlocks.Configure(new Block(), "test:member", 899), be);
    return be;
  }

  private sealed class TestNetworkMember : BEBehaviorNetworkMember {
    public TestNetworkMember(BlockEntity be, string networkType)
      : base(be) => NetworkType = networkType;
  }

  private sealed class PerCellNetworkMember : BEBehaviorNetworkMember {
    private readonly string _atCell;

    public PerCellNetworkMember(BlockEntity be, string declared, string atCell)
      : base(be) {
      NetworkType = declared;
      _atCell = atCell;
    }

    public override string NetworkTypeAt(IBlockAccessor world, BlockPos pos) =>
      _atCell;
  }
}
