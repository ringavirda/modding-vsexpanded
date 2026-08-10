using System.Collections.Generic;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Forming.BlockEntities;
using IronworkingExpanded.BlockStructures.Forming.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// One round trip behind the repo-wide collectible-mapping guard (see
/// <c>ExpandedLib.Tests.CollectibleMappingGuardTests</c>): a stack stored in a block entity records its
/// collectible's code through <c>OnStoreCollectibleMappings</c>, the call <c>BlockSchematic</c> makes
/// while pasting a structure into another world, before <c>ItemStack.FixMapping</c> resolves the pasted
/// stack against the destination world's ids. The rolling mill's piece under the rolls is the simplest of
/// the eight block entities the guard caught: a single loose <c>ItemStack</c> field, set through the
/// public <c>BeginPass</c> without any multiblock or network setup.
/// </summary>
public class CollectibleMappingTests {
  [Fact]
  public void The_rolling_mills_piece_under_the_rolls_maps_its_collectible() {
    var world = new TestWorld();
    var block = TestBlocks.Configure(
      new BlockRollingMill(),
      "iwex:forming-rollingmill-we",
      1,
      ("type", "rollingmill"),
      ("orientation", "we")
    );
    var pos = new BlockPos(0, 0, 0);
    var mill = new BlockEntityRollingMill();
    world.Place(pos, block, mill);
    world.Attach(mill);

    Item bloom = world.RegisterItem("iwex:stock-bloom");
    var piece = new ItemStack(bloom);

    Assert.True(
      mill.BeginPass(
        draft: 0.5f,
        width: 4f,
        length: 40f,
        tempC: 1100f,
        piece: piece
      )
    );

    var blockIdMapping = new Dictionary<int, AssetLocation>();
    var itemIdMapping = new Dictionary<int, AssetLocation>();
    mill.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);

    Assert.True(itemIdMapping.TryGetValue(bloom.Id, out AssetLocation? code));
    Assert.Equal(bloom.Code, code);
  }
}
