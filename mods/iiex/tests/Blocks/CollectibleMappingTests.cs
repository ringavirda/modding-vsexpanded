using System.Collections.Generic;
using ExpandedLib.Industry.MechanicalPower;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Forming.BlockEntities;
using IronIndustryExpanded.BlockStructures.Forming.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

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
      "iiex:forming-rollingmill-we",
      1,
      ("type", "rollingmill"),
      ("orientation", "we")
    );
    var pos = new BlockPos(0, 0, 0);
    var mill = new BlockEntityRollingMill();
    world.Place(pos, block, mill);
    // Initialize, not Attach: the mapping now runs through the container base, whose in-world
    // container resolves the world off the API its own Initialize hands it. Attach sets the block
    // entity's Api but never runs Initialize, so the container would still be unwired - and
    // initialising the mill also joins its membership to the graph, which needs the factory.
    world.RegisterNetwork("mpenergy", n => new MpEnergyNetwork(n));
    world.Initialize(mill);

    Item bloom = world.RegisterItem("iiex:stock-shingledbar");
    var piece = new ItemStack(bloom);

    Assert.True(
      mill.BeginPass(draft: 0.25f, length: 40f, tempC: 1100f, piece: piece)
    );

    var blockIdMapping = new Dictionary<int, AssetLocation>();
    var itemIdMapping = new Dictionary<int, AssetLocation>();
    mill.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);

    Assert.True(itemIdMapping.TryGetValue(bloom.Id, out AssetLocation? code));
    Assert.Equal(bloom.Code, code);
  }
}
