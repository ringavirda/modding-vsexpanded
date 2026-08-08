using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockMigrations;
using IronworkingExpanded.BlockStructures.Products.BlockEntities;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The frozen-melt block's merge: <c>iwex:solidifiediron</c> and <c>iwex:solidifiedcastiron</c> were two
/// definitions over one class, distinguished by a <c>metal</c> block attribute. They become one
/// variant-grouped <c>iwex:hearthmetal-{pigiron|castiron}</c>, so the metal moves into the code.
/// <para>
/// <b>Neither source code ever shipped</b> - <see cref="ReleasedCodes"/> has no iwex rows at all - so
/// this migration carries no released debt and exists as a courtesy to dev and playtest worlds. The
/// released code in this family is <c>smex:solidifiediron</c>, which reaches the same place through
/// <see cref="SmexToIwexMigration"/> and is covered by <c>ReleasedCodeCoverageTests</c>.
/// </para>
/// </summary>
public class HearthMetalMigrationTests
{
  private static Dictionary<AssetLocation, AssetLocation> Remaps(TestWorld world) =>
    new HearthMetalMigration()
      .GetRemaps(world.Api)
      .ToDictionary(r => r.oldCode, r => r.newCode);

  /// <summary>
  /// <c>CodeRelocation.Remap</c> walks <c>api.World.Blocks</c>, which the substitute does not populate
  /// from <see cref="TestWorld.Register"/> - it has to be told separately, exactly as
  /// <c>SmexToIwexMigrationTests</c> does. A fixture that only registers would return <b>no remaps at
  /// all</b> and every assertion here would read as "the migration is empty" rather than "the fixture is".
  /// </summary>
  private static TestWorld WorldWith(params string[] codes)
  {
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
    WorldWith("iwex:hearthmetal-pigiron", "iwex:hearthmetal-castiron");

  #region The remap

  [Fact]
  public void Each_old_code_maps_to_its_own_metal_variant()
  {
    var remaps = Remaps(WorldWithBothVariants());

    Assert.Equal(2, remaps.Count);
    Assert.Equal(
      new AssetLocation("iwex", "hearthmetal-pigiron"),
      remaps[new AssetLocation("iwex", "solidifiediron")]
    );
    Assert.Equal(
      new AssetLocation("iwex", "hearthmetal-castiron"),
      remaps[new AssetLocation("iwex", "solidifiedcastiron")]
    );
  }

  /// <summary>
  /// The two rows must not cross. Both blocks are the same class with the same entity and differ only
  /// in texture and drop metal, so a swapped pair is invisible at every seam except this one - a dead
  /// cupola would come back wearing the blast furnace's bright sheet, and vice versa.
  /// </summary>
  [Fact]
  public void The_two_rows_do_not_cross()
  {
    var remaps = Remaps(WorldWithBothVariants());

    Assert.DoesNotContain(
      remaps,
      r =>
        r.Key.Path == "solidifiediron" && r.Value.Path.EndsWith("castiron")
    );
    Assert.DoesNotContain(
      remaps,
      r => r.Key.Path == "solidifiedcastiron" && r.Value.Path.EndsWith("pigiron")
    );
  }

  /// <summary>
  /// Half a registry is worse than none: remapping only the metal that happens to be registered would
  /// send a saved cast-iron block at a code with no live block behind it, and
  /// <c>BlockMigrationModSystem</c> drops an unresolvable pair with a warning rather than failing - so
  /// the block simply stops loading. Each row is guarded by its own variant.
  /// </summary>
  [Fact]
  public void A_missing_variant_removes_only_its_own_row()
  {
    var remaps = Remaps(WorldWith("iwex:hearthmetal-pigiron"));

    Assert.Single(remaps);
    Assert.Equal(
      new AssetLocation("iwex", "hearthmetal-pigiron"),
      remaps[new AssetLocation("iwex", "solidifiediron")]
    );
  }

  [Fact]
  public void Nothing_is_remapped_when_neither_variant_is_registered()
  {
    Assert.Empty(Remaps(new TestWorld()));
  }

  #endregion

  #region The saved state

  /// <summary>
  /// The stored count is the whole value of the block - it is what the break drop scales by - so a
  /// rename that dropped the tree would silently turn every frozen salamander in a world into two bits.
  /// The tree key stays <c>ironCount</c> deliberately, and this asserts the key rather than the
  /// property: renaming the field is free, renaming the key orphans saves.
  /// </summary>
  [Fact]
  public void A_saved_count_survives_the_rename()
  {
    var world = new TestWorld();
    var saved = new TreeAttribute();
    saved.SetInt("ironCount", 7);

    var migrated = new BlockEntityHearthMetal();
    new HearthMetalMigration().MigrateBlockEntity(
      new AssetLocation("iwex", "solidifiediron"),
      new AssetLocation("iwex", "hearthmetal-pigiron"),
      saved,
      migrated,
      world.World
    );

    Assert.Equal(7, migrated.MetalCount);
  }

  /// <summary>
  /// The other half of the same contract: what the new entity <em>writes</em> must be readable as the
  /// old key too, or a world saved after migrating and reloaded reads the default.
  /// </summary>
  [Fact]
  public void The_new_entity_still_writes_the_old_tree_key()
  {
    // A bare entity cannot serialise: BlockEntity.ToTreeAttributes writes its position and its block's
    // code, so the entity has to be genuinely placed - a Pos alone is not enough, and neither is an Api.
    // The read side above can stay bare because FromTreeAttributes takes the world it needs as an
    // argument; only the write side reaches back through the accessor.
    var world = new TestWorld();
    Block block = TestBlocks.Configure(
      new Block(),
      "iwex:hearthmetal-pigiron",
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
  public void A_missing_tree_leaves_the_entity_at_its_default()
  {
    var world = new TestWorld();
    var migrated = new BlockEntityHearthMetal();

    new HearthMetalMigration().MigrateBlockEntity(
      new AssetLocation("iwex", "solidifiediron"),
      new AssetLocation("iwex", "hearthmetal-pigiron"),
      null,
      migrated,
      world.World
    );

    Assert.Equal(2, migrated.MetalCount);
  }

  #endregion
}
