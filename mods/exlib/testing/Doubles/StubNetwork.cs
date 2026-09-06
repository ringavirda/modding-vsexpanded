using ExpandedLib.Networks;
using Vintagestory.API.Common;

namespace ExpandedLib.Testing;

/// <summary>
/// Medium-less concrete <see cref="BlockNetwork"/> for exercising the graph engine
/// (<see cref="BlockNetworkModSystem"/>) in isolation - add, remove, merge, fracture, rebuild - with no
/// pipe or molten state semantics. <see cref="Tag"/> is carried through merge, split and inherit so a
/// test can assert which state survived a topology change.
/// </summary>
public sealed class StubNetwork(
  BlockNetworkModSystem system,
  string networkType = "test"
) : BlockNetwork(system) {
  public override string NetworkType => networkType;

  /// <summary>Arbitrary marker used to verify state propagation across topology changes.</summary>
  public string? Tag { get; set; }

  public override void OnMerge(BlockNetwork other, IBlockAccessor world) {
    if (other is StubNetwork s && Tag == null)
      Tag = s.Tag;
  }

  public override void OnSplitFragment(
    BlockNetwork original,
    IBlockAccessor world
  ) {
    if (original is StubNetwork s)
      Tag = s.Tag;
  }

  public override void InheritStateFrom(BlockNetwork source) {
    if (source is StubNetwork s)
      Tag = s.Tag;
  }

  public override void OnTick(
    IBlockAccessor world,
    float dt,
    BlockNetworkModSystem manager
  ) { }
}
