using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockMigrations;
using IronIndustryExpanded.BlockStructures.Products.BlockEntities;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The frozen-melt block merge: <c>iwex:solidifiediron</c> and <c>iwex:solidifiedcastiron</c> were two
/// definitions over one class distinguished by a <c>metal</c> block attribute, and become one
/// variant-grouped <c>iiex:hearthmetal-{pigiron|castiron}</c> with the metal in the code.
/// <para>
/// Neither source code ever shipped (<see cref="ReleasedCodes"/> has no iwex rows), so this migration
/// covers dev and playtest worlds only. The released code in this family is <c>smex:solidifiediron</c>,
/// which reaches the same place through <see cref="SmexToIiexMigration"/> and is covered by
/// <c>ReleasedCodeCoverageTests</c>.
/// </para>
/// </summary>
public class HearthMetalMigrationTests {
  private static Dictionary<AssetLocation, AssetLocation> Remaps(
    TestWorld world
  ) =>
    new HearthMetalMigration()
      .GetRemaps(world.Api)
      .ToDictionary(r => r.oldCode, r => r.newCode);

  /// <summary>
  /// <c>CodeRelocation.Remap</c> walks <c>api.World.Blocks</c>, which the substitute does not populate
  /// from <see cref="TestWorld.Register"/>; it must be set separately or the migration returns no remaps
  /// at all.
  /// </summary>
  private static TestWorld WorldWith(params string[] codes) {
    var world = new TestWorld();
    Block[] blocks =
    [
      .. codes.Select(
        (code, i) => TestBlocks.Configure(new Block(), code, 100 + i)
      ),
    ];
    foreach (Block block in blocks)
      world.Register(block);
    world.World.Blocks.Returns(new List<Block>(blocks));
    return world;
  }

  private static TestWorld WorldWithBothVariants() =>
    WorldWith("iiex:hearthmetal-pigiron", "iiex:hearthmetal-castiron");

  #region The remap

  [Fact]
  public void Each_old_code_maps_to_its_own_metal_variant() {
    var remaps = Remaps(WorldWithBothVariants());

    Assert.Equal(2, remaps.Count);
    Assert.Equal(
      new AssetLocation("iiex", "hearthmetal-pigiron"),
      remaps[new AssetLocation("iwex", "solidifiediron")]
    );
    Assert.Equal(
      new AssetLocation("iiex", "hearthmetal-castiron"),
      remaps[new AssetLocation("iwex", "solidifiedcastiron")]
    );
  }

  /// <summary>
  /// The two rows must not cross. Both blocks are the same class with the same entity and differ only in
  /// texture and drop metal, so a swapped pair is invisible at every other seam.
  /// </summary>
  [Fact]
  public void The_two_rows_do_not_cross() {
    var remaps = Remaps(WorldWithBothVariants());

    Assert.DoesNotContain(
      remaps,
      r => r.Key.Path == "solidifiediron" && r.Value.Path.EndsWith("castiron")
    );
    Assert.DoesNotContain(
      remaps,
      r =>
        r.Key.Path == "solidifiedcastiron" && r.Value.Path.EndsWith("pigiron")
    );
  }

  /// <summary>
  /// Each row is guarded by its own variant. Remapping a metal whose target is not registered points a
  /// saved block at a code with no live block behind it, and <c>BlockMigrationModSystem</c> drops an
  /// unresolvable pair with a warning rather than failing, so the block stops loading.
  /// </summary>
  [Fact]
  public void A_missing_variant_removes_only_its_own_row() {
    var remaps = Remaps(WorldWith("iiex:hearthmetal-pigiron"));

    Assert.Single(remaps);
    Assert.Equal(
      new AssetLocation("iiex", "hearthmetal-pigiron"),
      remaps[new AssetLocation("iwex", "solidifiediron")]
    );
  }

  [Fact]
  public void Nothing_is_remapped_when_neither_variant_is_registered() {
    Assert.Empty(Remaps(new TestWorld()));
  }

  #endregion

  #region The saved state

  /// <summary>
  /// The stored count scales the break drop, so it has to survive the rename. The tree key stays
  /// <c>ironCount</c>, and this asserts the key rather than the property: renaming the field is free,
  /// renaming the key orphans saves.
  /// </summary>
  [Fact]
  public void A_saved_count_survives_the_rename() {
    var world = new TestWorld();
    var saved = new TreeAttribute();
    saved.SetInt("ironCount", 7);

    var migrated = new BlockEntityHearthMetal();
    new HearthMetalMigration().MigrateBlockEntity(
      new AssetLocation("iwex", "solidifiediron"),
      new AssetLocation("iiex", "hearthmetal-pigiron"),
      saved,
      migrated,
      world.World
    );

    Assert.Equal(7, migrated.MetalCount);
  }

  /// <summary>
  /// The write side of the same contract: the new entity must write the old key, or a world saved after
  /// migrating reads the default on reload.
  /// </summary>
  [Fact]
  public void The_new_entity_still_writes_the_old_tree_key() {
    // BlockEntity.ToTreeAttributes writes the position and the block's code through the accessor, so the
    // entity has to be genuinely placed - a Pos alone is not enough, and neither is an Api. The read side
    // above can stay bare because FromTreeAttributes takes the world as an argument.
    var world = new TestWorld();
    Block block = TestBlocks.Configure(
      new Block(),
      "iiex:hearthmetal-pigiron",
      100
    );
    var be = new BlockEntityHearthMetal { MetalCount = 5 };
    world.Place(new BlockPos(0, 0, 0), block, be);
    world.Attach(be);

    var written = new TreeAttribute();
    be.ToTreeAttributes(written);

    Assert.Equal(5, written.GetInt("ironCount"));
  }

  /// <summary>A block entity with no saved tree must not throw - it keeps its own default.</summary>
  [Fact]
  public void A_missing_tree_leaves_the_entity_at_its_default() {
    var world = new TestWorld();
    var migrated = new BlockEntityHearthMetal();

    new HearthMetalMigration().MigrateBlockEntity(
      new AssetLocation("iwex", "solidifiediron"),
      new AssetLocation("iiex", "hearthmetal-pigiron"),
      null,
      migrated,
      world.World
    );

    Assert.Equal(2, migrated.MetalCount);
  }

  #endregion
}
