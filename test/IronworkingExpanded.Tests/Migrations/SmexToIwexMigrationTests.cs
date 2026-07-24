using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockMigrations;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The smex→iwex domain migration for the relocated molten-network and blast-furnace blocks: it pairs
/// every registered <c>iwex</c> block whose base code is one of the relocated set with its old
/// <c>smex</c> code (variant-for-variant, path unchanged), and leaves the new iwex-only blocks
/// (bunker, mixer) and any non-iwex blocks untouched.
/// </summary>
public class SmexToIwexMigrationTests
{
  // One registered iwex block per relocated base code, plus a representative multi-variant canal.
  private static readonly string[] RelocatedSamples =
  [
    "moltencanal-straight-fire-ns",
    "moltencanal-tap-n",
    "moltenbarrel",
    "blastfurnace-tuyere-n",
    "blastfurnacetap-north",
    "hopperbell",
    "hopperreinforced",
    "slag",
    "solidifiediron",
  ];

  private static Block Block(string code, int id) =>
    TestBlocks.Configure(new Block(), code, id);

  private static TestWorld WorldWith(params Block[] blocks)
  {
    var world = new TestWorld();
    world.World.Blocks.Returns(new List<Block>(blocks));
    return world;
  }

  private static Dictionary<AssetLocation, AssetLocation> Remaps(TestWorld world) =>
    new SmexToIwexMigration()
      .GetRemaps(world.Api)
      .ToDictionary(r => r.oldCode, r => r.newCode);

  #region Relocated blocks

  [Fact]
  public void Every_relocated_iwex_block_remaps_from_its_old_smex_code()
  {
    var blocks = RelocatedSamples
      .Select((p, i) => Block("iwex:" + p, 100 + i))
      .ToArray();
    var remaps = Remaps(WorldWith(blocks));

    Assert.Equal(RelocatedSamples.Length, remaps.Count);
    foreach (string path in RelocatedSamples)
      Assert.Equal(
        new AssetLocation("iwex", path),
        remaps[new AssetLocation("smex", path)]
      );
  }

  [Fact]
  public void Each_variant_is_remapped_individually()
  {
    // Two canal variants both relocate; the migration covers every variant, not just the base code.
    var world = WorldWith(
      Block("iwex:moltencanal-bend-fire-nw", 100),
      Block("iwex:moltencanal-bend-fire-se", 101)
    );
    var remaps = Remaps(world);

    Assert.Equal(2, remaps.Count);
    Assert.Contains(new AssetLocation("smex", "moltencanal-bend-fire-nw"), remaps.Keys);
    Assert.Contains(new AssetLocation("smex", "moltencanal-bend-fire-se"), remaps.Keys);
  }

  #endregion

  #region Exclusions

  [Fact]
  public void New_iwex_only_blocks_are_not_remapped()
  {
    var world = WorldWith(
      Block("iwex:bunker-red-north", 100),
      Block("iwex:mixer-north", 101),
      Block("iwex:moltenbarrel", 102) // a relocated one, for contrast
    );
    var remaps = Remaps(world);

    Assert.Single(remaps); // only the barrel
    Assert.Contains(new AssetLocation("smex", "moltenbarrel"), remaps.Keys);
    Assert.DoesNotContain(new AssetLocation("smex", "bunker-red-north"), remaps.Keys);
    Assert.DoesNotContain(new AssetLocation("smex", "mixer-north"), remaps.Keys);
  }

  [Fact]
  public void Blocks_outside_the_iwex_domain_are_ignored()
  {
    // A leftover smex/game block whose path happens to match a relocated base must not be touched -
    // the migration only rewrites blocks that now live under iwex.
    var world = WorldWith(
      Block("smex:moltencanal-straight-fire-ns", 100),
      Block("game:slag", 101)
    );
    Assert.Empty(Remaps(world));
  }

  #endregion
}
