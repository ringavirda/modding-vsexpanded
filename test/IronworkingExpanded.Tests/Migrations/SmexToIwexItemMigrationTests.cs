using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockMigrations;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The smex to iwex domain migration for the relocated items (blast mix, slag, powdered slag): it pairs
/// every registered <c>iwex</c> item whose base code is relocated with its old <c>smex</c> code, leaves
/// the iwex-only <c>burden</c> item and non-iwex items untouched, and stays independent of the same-named
/// <c>slag</c> block migration.
/// </summary>
public class SmexToIwexItemMigrationTests {
  private static Item Item(string code, int id) =>
    new() { Code = new AssetLocation(code), ItemId = id };

  private static TestWorld WorldWith(params Item[] items) {
    var world = new TestWorld();
    world.World.Items.Returns(new List<Item>(items));
    return world;
  }

  private static Dictionary<AssetLocation, AssetLocation> Remaps(
    TestWorld world
  ) =>
    new SmexToIwexItemMigration()
      .GetRemaps(world.Api)
      .ToDictionary(r => r.oldCode, r => r.newCode);

  [Fact]
  public void Relocated_items_remap_from_smex_to_iwex_preserving_path() {
    var world = WorldWith(
      Item("iwex:slag", 101),
      Item("iwex:powderedslag", 102)
    );
    var remaps = Remaps(world);

    Assert.Equal(2, remaps.Count);
    foreach (string path in new[] { "slag", "powderedslag" })
      Assert.Equal(
        new AssetLocation("iwex", path),
        remaps[new AssetLocation("smex", path)]
      );
  }

  /// <summary>
  /// <c>blastmix</c> is not remapped: the <c>iwex:blastmix</c> item was deleted with the coal-pile
  /// charge path, so there is nothing to remap onto. The walk enumerates registered <c>iwex</c> items,
  /// so a stale entry in the relocated set yields no pair; both spellings become unresolvable and the
  /// stack is dropped on load.
  /// <para>
  /// It is not pointed at <c>iwex:burden</c> either: burden carries a stamped iron/flux/coke mix and
  /// blast mix carried none, so a remap would produce a burden of an invented grade.
  /// </para>
  /// </summary>
  [Fact]
  public void Blast_mix_is_not_remapped_because_the_item_no_longer_exists() {
    // A world that still knows the item: the only way this regresses is `blastmix` returning to the
    // relocated set and remapping again.
    var world = WorldWith(Item("iwex:blastmix", 100), Item("iwex:slag", 101));
    var remaps = Remaps(world);

    Assert.Single(remaps);
    Assert.DoesNotContain(new AssetLocation("smex", "blastmix"), remaps.Keys);
  }

  [Fact]
  public void The_new_burden_item_is_not_remapped() {
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
  public void Items_outside_the_iwex_domain_are_ignored() {
    var world = WorldWith(
      Item("smex:blastmix", 100),
      Item("game:powderedslag", 101)
    );
    Assert.Empty(Remaps(world));
  }
}
