using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockMigrations;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The smex to iiex domain migration for the relocated molten-network and blast-furnace blocks. The
/// fixture codes are the ones the definitions register, and each rename row is asserted individually so
/// a dropped row cannot pass.
/// <para>
/// The cross-mod contract - that every code which shipped still reaches a live block - is asserted
/// against the real definitions in <c>ReleasedCodeCoverageTests</c>, the only suite that sees all five
/// mods at once.
/// </para>
/// </summary>
public class SmexToIiexMigrationTests {
  // smex code as it shipped in 0.9.4 -> the iiex code that block lives under today. The last three are
  // renames as well as domain moves.
  private static readonly (string Old, string New)[] Relocated =
  [
    ("smex:moltencanal-straight-fire-ns", "iiex:molten-canal-straight-fire-ns"),
    ("smex:moltencanal-tap-n", "iiex:molten-canal-tap-n"),
    ("smex:slag", "iiex:slag-block"),
    ("smex:solidifiediron", "iiex:hearthmetal-pigiron"),
    ("smex:blastfurnace-tuyere-n", "iiex:furnace-tuyere-n"),
    ("smex:blastfurnacetap-n", "iiex:furnace-irontap-n"),
    ("smex:blastfurnacedoor", "iiex:furnace-chargedoor-n"),
  ];

  private static Block Block(string code, int id) =>
    TestBlocks.Configure(new Block(), code, id);

  private static TestWorld WorldWith(params Block[] blocks) {
    var world = new TestWorld();
    foreach (Block b in blocks)
      world.Register(b);
    world.World.Blocks.Returns(new List<Block>(blocks));
    return world;
  }

  private static TestWorld WorldWithLiveTargets() =>
    WorldWith([.. Relocated.Select((r, i) => Block(r.New, 100 + i))]);

  private static Dictionary<AssetLocation, AssetLocation> Remaps(
    TestWorld world
  ) =>
    new SmexToIiexMigration()
      .GetRemaps(world.Api)
      .ToDictionary(r => r.oldCode, r => r.newCode);

  #region Relocated blocks

  [Theory]
  [InlineData(
    "smex:moltencanal-straight-fire-ns",
    "iiex:molten-canal-straight-fire-ns"
  )]
  [InlineData("smex:moltencanal-tap-n", "iiex:molten-canal-tap-n")]
  [InlineData("smex:slag", "iiex:slag-block")]
  [InlineData("smex:solidifiediron", "iiex:hearthmetal-pigiron")]
  // Renames as well as domain moves, one per case so a dropped row names itself instead of moving a count.
  [InlineData("smex:blastfurnace-tuyere-n", "iiex:furnace-tuyere-n")]
  [InlineData("smex:blastfurnacetap-n", "iiex:furnace-irontap-n")]
  [InlineData("smex:blastfurnacedoor", "iiex:furnace-chargedoor-n")]
  public void Each_relocated_block_remaps_from_its_shipped_smex_code(
    string oldCode,
    string newCode
  ) {
    var remaps = Remaps(WorldWithLiveTargets());

    Assert.True(
      remaps.TryGetValue(new AssetLocation(oldCode), out AssetLocation? actual),
      $"{oldCode} shipped in smex 0.9.4 and has no remap - a released world loses it."
    );
    Assert.Equal(new AssetLocation(newCode), actual);
  }

  [Fact]
  public void Each_variant_is_remapped_individually() {
    // Variants are derived from the live registry, so a new orientation needs no edit here.
    var world = WorthTwoCanalBends();
    var remaps = Remaps(world);

    Assert.Equal(2, remaps.Count);
    Assert.Contains(
      new AssetLocation("smex", "moltencanal-bend-fire-nw"),
      remaps.Keys
    );
    Assert.Contains(
      new AssetLocation("smex", "moltencanal-bend-fire-se"),
      remaps.Keys
    );

    static TestWorld WorthTwoCanalBends() =>
      WorldWith(
        Block("iiex:molten-canal-bend-fire-nw", 100),
        Block("iiex:molten-canal-bend-fire-se", 101)
      );
  }

  [Fact]
  public void Every_shipped_tap_orientation_lands_on_the_iron_tap() {
    // smex had one tap blocktype for both notches, so a shipped code carries no record of whether the
    // player built an iron notch or a cinder notch. Every orientation therefore lands on `irontap`.
    // All four are checked because the target's variant grammar changed shape (one group became two),
    // so a mapping can work for one facing and drop the rest.
    var world = WorldWith(
      Block("iiex:furnace-irontap-n", 200),
      Block("iiex:furnace-irontap-e", 201),
      Block("iiex:furnace-irontap-s", 202),
      Block("iiex:furnace-irontap-w", 203),
      Block("iiex:furnace-slagtap-n", 204)
    );

    var remaps = Remaps(world);

    // Letters on both sides: `ReleasedCodes.Smex` records `smex:blastfurnacetap-{e,n,s,w}` because smex
    // spelled every facing as a letter, so the released code and the live one agree. The ppex rows spell
    // sides as words and need `legacySideWords`; see CodeRelocation.
    foreach (string side in new[] { "n", "e", "s", "w" })
      Assert.Equal(
        new AssetLocation("iiex", "furnace-irontap-" + side),
        remaps[new AssetLocation("smex", "blastfurnacetap-" + side)]
      );

    // Nothing is invented for the notch smex never had: a `smex:blastfurnaceslagtap` source would name a
    // block that never shipped.
    Assert.DoesNotContain(
      remaps.Values,
      v => v.Path.StartsWith("furnace-slagtap")
    );
  }

  #endregion

  #region Exclusions

  [Fact]
  public void Blocks_that_never_shipped_under_smex_are_not_remapped() {
    // The ore line arrived with iiex, so it never shipped under smex and may not be claimed. The slag
    // paving line is claimed now: it shipped in smex 0.9.8, which the manifest only started recording
    // once ReleasedCodes was refreshed off 0.6.8/0.9.8.
    var world = WorldWith(
      Block("iiex:burdenmaker-red-n", 100),
      Block("iiex:crafting-designtable-n", 101),
      Block("iiex:slag-path-free", 102),
      Block("iiex:slag-block", 103)
    );
    var remaps = Remaps(world);

    Assert.Equal(2, remaps.Count);
    Assert.Contains(new AssetLocation("smex", "slag"), remaps.Keys);
    Assert.Contains(new AssetLocation("smex", "slagpath-free"), remaps.Keys);
  }

  [Fact]
  public void The_slag_base_does_not_swallow_the_slag_paving_family() {
    // `slag` and `slagpath*` differ by one character at the boundary, and each now has its own row.
    // CodeRelocation requires a literal '-' after the base for exactly this reason: without it the
    // `slag` row would claim the whole paving family and every one of them would land on slag-block.
    var world = WorldWith(
      Block("iiex:slag-block", 100),
      Block("iiex:slag-path-free", 101),
      Block("iiex:slag-pathslab-free", 102),
      Block("iiex:slag-bricks", 103)
    );
    var remaps = Remaps(world);

    // Each source resolves to its own target, never to slag-block by prefix accident.
    Assert.Equal(
      new AssetLocation("iiex", "slag-block"),
      remaps[new AssetLocation("smex", "slag")]
    );
    Assert.Equal(
      new AssetLocation("iiex", "slag-path-free"),
      remaps[new AssetLocation("smex", "slagpath-free")]
    );
    Assert.Equal(
      new AssetLocation("iiex", "slag-pathslab-free"),
      remaps[new AssetLocation("smex", "slagpathslab-free")]
    );
    // The brick line is iiex's own and never shipped under smex, so nothing may claim it.
    Assert.DoesNotContain(remaps.Values, v => v.Path.StartsWith("slag-brick"));
  }

  [Fact]
  public void Blocks_outside_the_iwex_domain_are_ignored() {
    var world = WorldWith(
      Block("smex:moltencanal-straight-fire-ns", 100),
      Block("game:slag", 101)
    );
    Assert.Empty(Remaps(world));
  }

  [Fact]
  public void The_hoppers_stayed_in_smex_and_are_not_claimed() {
    // hopperbell and hopperreinforced stayed in smex. Claiming a block that did not move rewrites live
    // content.
    var world = WorldWith(
      Block("smex:hopperbell", 100),
      Block("smex:hopperreinforced", 101)
    );
    Assert.Empty(Remaps(world));
  }

  #endregion
}
