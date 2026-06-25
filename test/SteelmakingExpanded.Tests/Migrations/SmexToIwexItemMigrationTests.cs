using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockMigrations;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The smex→iwex domain migration for the relocated <em>items</em> (blast mix, slag, powdered slag):
/// it pairs every registered <c>iwex</c> item whose base code is relocated with its old <c>smex</c>
/// code, leaves the new iwex-only <c>burden</c> item and non-iwex items untouched, and stays
/// independent of the same-named <c>slag</c> block migration.
/// </summary>
public class SmexToIwexItemMigrationTests
{
  private static Item Item(string code, int id) =>
    new() { Code = new AssetLocation(code), ItemId = id };

  private static TestWorld WorldWith(params Item[] items)
  {
    var world = new TestWorld();
    world.World.Items.Returns(new List<Item>(items));
    return world;
  }

  private static Dictionary<AssetLocation, AssetLocation> Remaps(TestWorld world) =>
    new SmexToIwexItemMigration()
      .GetRemaps(world.Api)
      .ToDictionary(r => r.oldCode, r => r.newCode);

  [Fact]
  public void Relocated_items_remap_from_smex_to_iwex_preserving_path()
  {
    var world = WorldWith(
      Item("iwex:blastmix", 100),
      Item("iwex:slag", 101),
      Item("iwex:powderedslag", 102)
    );
    var remaps = Remaps(world);

    Assert.Equal(3, remaps.Count);
    foreach (string path in new[] { "blastmix", "slag", "powderedslag" })
      Assert.Equal(
        new AssetLocation("iwex", path),
        remaps[new AssetLocation("smex", path)]
      );
  }

  [Fact]
  public void The_new_burden_item_is_not_remapped()
  {
    var world = WorldWith(
      Item("iwex:burden", 100),
      Item("iwex:slag", 101) // a relocated one, for contrast
    );
    var remaps = Remaps(world);

    Assert.Single(remaps);
    Assert.Contains(new AssetLocation("smex", "slag"), remaps.Keys);
    Assert.DoesNotContain(new AssetLocation("smex", "burden"), remaps.Keys);
  }

  [Fact]
  public void Items_outside_the_iwex_domain_are_ignored()
  {
    var world = WorldWith(
      Item("smex:blastmix", 100),
      Item("game:powderedslag", 101)
    );
    Assert.Empty(Remaps(world));
  }
}
