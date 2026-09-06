using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Migrations;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="IBlockRemoval"/>: a real implementation's declared codes drive
/// <see cref="BlockMigrationModSystem.VisitCell"/> (via <see cref="ReflectionHelpers.Invoke"/>, since
/// it is protected) to delete a placed block, and <see cref="CodeRelocation"/>'s helpers, which build
/// the <c>(oldCode, newCode)</c> pairs a mod's own <see cref="IBlockCodeMigration"/> declares.
/// </summary>
public class BlockRemovalTests {
  private sealed class PurgeMigration : IBlockRemoval {
    public string Name => "purge test";

    public IEnumerable<AssetLocation> GetRemovals(ICoreServerAPI api) => [new("stub:gone")];
  }

  private static Block Block(string code, int id) => TestBlocks.Configure(new Block(), code, id);

  private static BlockMigrationModSystem System(TestWorld world) {
    var sys = new BlockMigrationModSystem();
    ReflectionHelpers.SetField(sys, "_sapi", world.Api);
    return sys;
  }

  #region IBlockRemoval

  [Fact]
  public void GetRemovals_declares_the_named_code() {
    var removal = new PurgeMigration();

    Assert.Equal([new AssetLocation("stub:gone")], removal.GetRemovals(new TestWorld().Api).ToList());
  }

  [Fact]
  public void A_declared_removal_deletes_the_block_when_the_cell_is_visited() {
    var world = new TestWorld();
    var pos = new BlockPos(1, 1, 1, 0);
    var gone = Block("stub:gone", 90);
    world.Place(pos, gone);

    var sys = System(world);
    foreach (AssetLocation code in new PurgeMigration().GetRemovals(world.Api))
      sys._remap[code] = new BlockMigrationModSystem.RemapEntry(null, code, null, null);

    int changed = (int)ReflectionHelpers.Invoke(
      sys,
      "VisitCell",
      world.Accessor,
      pos,
      gone.BlockId
    )!;

    Assert.Equal(1, changed);
    Assert.Same(world.Air, world.GetBlock(pos));
  }

  [Fact]
  public void A_code_the_removal_never_names_is_left_in_place() {
    var world = new TestWorld();
    var pos = new BlockPos(2, 2, 2, 0);
    var kept = Block("stub:kept", 91);
    world.Place(pos, kept);
    var sys = System(world);
    // Nothing registered in _remap: this removal names only "stub:gone".

    int changed = (int)ReflectionHelpers.Invoke(sys, "VisitCell", world.Accessor, pos, kept.BlockId)!;

    Assert.Equal(0, changed);
    Assert.Same(kept, world.GetBlock(pos));
  }

  #endregion

  #region Migration discovery order

  private sealed class ZDiscoveryOrderMigration : IBlockCodeMigration {
    public string Name => "z discovery order";

    public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(ICoreServerAPI api) =>
      [(new AssetLocation("discoverytest:z-old"), new AssetLocation("discoverytest:z-new"))];
  }

  private sealed class ADiscoveryOrderMigration : IBlockCodeMigration {
    public string Name => "a discovery order";

    public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(ICoreServerAPI api) =>
      [(new AssetLocation("discoverytest:a-old"), new AssetLocation("discoverytest:a-new"))];
  }

  // BlockMigrationModSystem.Discover<T> walks ReflectionScan.GetCandidateTypes, ordered by assembly
  // full name then type full name - not declaration order, which is why the "Z..." class is declared
  // first above and still sorts after "A...".
  [Fact]
  public void Discovery_order_is_by_type_full_name_not_declaration_order() {
    var names = BlockMigrationModSystem
      .DeclaredBlockRemaps(new TestWorld().Api)
      .Where(r => r.OldCode.Domain == "discoverytest")
      .Select(r => r.Migration)
      .ToList();

    Assert.Equal(["a discovery order", "z discovery order"], names);
  }

  [Fact]
  public void Discovery_order_is_stable_across_repeated_calls() {
    var api = new TestWorld().Api;
    var first = BlockMigrationModSystem.DeclaredBlockRemaps(api).Select(r => r.Migration).ToList();
    var second = BlockMigrationModSystem.DeclaredBlockRemaps(api).Select(r => r.Migration).ToList();

    Assert.Equal(first, second);
  }

  #endregion

  #region CodeRelocation

  [Fact]
  public void Remap_pairs_every_matching_live_block_with_its_historical_code() {
    var world = new TestWorld();
    world.Register(Block("newdomain:widget", 100));
    world.Register(Block("newdomain:widget-big", 101));
    world.Register(Block("newdomain:widgetry", 102)); // no literal "-" boundary: must not match

    var pairs = CodeRelocation
      .Remap(world.Api, "olddomain", "widget", "newdomain", "widget")
      .OrderBy(p => p.newCode.Path)
      .ToList();

    Assert.Equal(2, pairs.Count);
    Assert.Equal(new AssetLocation("olddomain:widget"), pairs[0].oldCode);
    Assert.Equal(new AssetLocation("newdomain:widget"), pairs[0].newCode);
    Assert.Equal(new AssetLocation("olddomain:widget-big"), pairs[1].oldCode);
    Assert.Equal(new AssetLocation("newdomain:widget-big"), pairs[1].newCode);
  }

  [Fact]
  public void Remap_with_a_shared_base_uses_the_same_name_on_both_sides() {
    var world = new TestWorld();
    world.Register(Block("newdomain:shared-x", 110));

    var pairs = CodeRelocation.Remap(world.Api, "olddomain", "newdomain", "shared").ToList();

    Assert.Equal(new AssetLocation("olddomain:shared-x"), pairs[0].oldCode);
    Assert.Equal(new AssetLocation("newdomain:shared-x"), pairs[0].newCode);
  }

  [Fact]
  public void RemapToDefault_maps_onto_a_registered_target_only() {
    var world = new TestWorld();
    world.Register(Block("newdomain:target-a", 120));

    var hit = CodeRelocation
      .RemapToDefault(world.Api, "olddomain", "bare", "newdomain:target-a")
      .ToList();
    var miss = CodeRelocation
      .RemapToDefault(world.Api, "olddomain", "bare", "newdomain:missing")
      .ToList();

    Assert.Equal(new AssetLocation("olddomain:bare"), hit[0].oldCode);
    Assert.Equal(new AssetLocation("newdomain:target-a"), hit[0].newCode);
    Assert.Empty(miss);
  }

  #endregion
}
