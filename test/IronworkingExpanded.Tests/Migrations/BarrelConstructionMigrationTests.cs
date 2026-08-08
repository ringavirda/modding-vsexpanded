using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockMigrations;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The molten barrel's construction-variant remap: the barrel's code gained a plated/cast variant group,
/// so the old single-code barrel is rewritten to the plated variant (plated = the original fabricated
/// barrel). Covers both the current <c>iwex</c> code and the pre-split <c>smex</c> code, and that it
/// no-ops when the new plated block is not registered.
/// </summary>
public class BarrelConstructionMigrationTests {
  private static Dictionary<AssetLocation, AssetLocation> Remaps(
    TestWorld world
  ) =>
    new BarrelConstructionMigration()
      .GetRemaps(world.Api)
      .ToDictionary(r => r.oldCode, r => r.newCode);

  [Fact]
  public void Both_old_barrel_codes_map_to_the_plated_variant() {
    var world = new TestWorld();
    world.Register(
      TestBlocks.Configure(new Block(), "iwex:molten-barrel-plated", 100)
    );

    var remaps = Remaps(world);
    var plated = new AssetLocation("iwex", "molten-barrel-plated");

    Assert.Equal(2, remaps.Count);
    Assert.Equal(plated, remaps[new AssetLocation("iwex", "moltenbarrel")]);
    Assert.Equal(plated, remaps[new AssetLocation("smex", "moltenbarrel")]);
  }

  [Fact]
  public void Nothing_is_remapped_when_the_plated_block_is_absent() {
    // No molten-barrel-plated registered - the migration has nothing safe to remap to.
    Assert.Empty(Remaps(new TestWorld()));
  }
}
