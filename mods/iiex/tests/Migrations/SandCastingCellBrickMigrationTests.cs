using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockMigrations;
using Vintagestory.API.Common;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The sand casting cell's brick+facing remap: the cell used to ship variantless
/// (<c>iwex:sandcastingcell</c>) and gained a <c>brick</c> + <c>side</c> variant, so the old cells
/// (fire brick, north facing) rewrite to <c>iiex:casting-sandcell-fire-n</c>. Covers the single remap
/// and the no-op when the new block is not registered.
/// </summary>
public class SandCastingCellBrickMigrationTests {
  private static Dictionary<AssetLocation, AssetLocation> Remaps(
    TestWorld world
  ) =>
    new SandCastingCellBrickMigration()
      .GetRemaps(world.Api)
      .ToDictionary(r => r.oldCode, r => r.newCode);

  [Fact]
  public void The_old_codeless_cell_maps_to_the_fire_north_variant() {
    var world = new TestWorld();
    world.Register(
      TestBlocks.Configure(new Block(), "iiex:casting-sandcell-fire-n", 100)
    );

    var remaps = Remaps(world);
    // `-n`, not `-north`: the target must track the live code, while the source string is historical
    // and does not follow later respellings of the side suffix.
    var fireNorth = new AssetLocation("iiex", "casting-sandcell-fire-n");

    Assert.Single(remaps);
    Assert.Equal(
      fireNorth,
      remaps[new AssetLocation("iwex", "sandcastingcell")]
    );
  }

  [Fact]
  public void Nothing_is_remapped_when_the_new_block_is_absent() {
    // No casting-sandcell-fire-n registered - the migration has nothing safe to remap to.
    Assert.Empty(Remaps(new TestWorld()));
  }
}
