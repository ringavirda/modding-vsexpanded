using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockMigrations;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The smex→iwex domain migration for the relocated molten-network and blast-furnace blocks.
/// <para>
/// <b>This suite used to fabricate its own world and prove nothing.</b> Its fixture registered
/// <c>iwex:blastfurnace-tuyere-n</c> and <c>iwex:blastfurnacetap-north</c> - codes <b>no definition has
/// ever produced</b> - so it stayed green while the real tuyere (<c>iwex:furnace-tuyere-*</c>), tap
/// (now <c>iwex:furnace-irontap-*</c>) and furnace door (<c>iwex:furnace-chargedoor-*</c>) had no migration path at
/// all and every released world lost them. The codes below are now the ones the definitions actually
/// register, and the rename rows are asserted individually so dropping one cannot pass.
/// </para>
/// <para>
/// The cross-mod contract - that every code which ever <i>shipped</i> still reaches a live block - is
/// asserted against the real definitions in <c>ReleasedCodeCoverageTests</c>, which is the only suite
/// that can see all five mods at once.
/// </para>
/// </summary>
public class SmexToIwexMigrationTests
{
  // smex code as it shipped in 0.9.4  ->  the iwex code that block lives under today. The last
  // three are renames as well as domain moves, which is precisely what the old FirstCodePart-matching
  // form went blind to.
  private static readonly (string Old, string New)[] Relocated =
  [
    ("smex:moltencanal-straight-fire-ns", "iwex:molten-canal-straight-fire-ns"),
    ("smex:moltencanal-tap-n", "iwex:molten-canal-tap-n"),
    ("smex:slag", "iwex:slag-block"),
    ("smex:solidifiediron", "iwex:hearthmetal-pigiron"),
    ("smex:blastfurnace-tuyere-n", "iwex:furnace-tuyere-n"),
    ("smex:blastfurnacetap-n", "iwex:furnace-irontap-n"),
    ("smex:blastfurnacedoor", "iwex:furnace-chargedoor-n"),
  ];

  private static Block Block(string code, int id) =>
    TestBlocks.Configure(new Block(), code, id);

  private static TestWorld WorldWith(params Block[] blocks)
  {
    var world = new TestWorld();
    foreach (Block b in blocks)
      world.Register(b);
    world.World.Blocks.Returns(new List<Block>(blocks));
    return world;
  }

  private static TestWorld WorldWithLiveTargets() =>
    WorldWith(
      [.. Relocated.Select((r, i) => Block(r.New, 100 + i))]
    );

  private static Dictionary<AssetLocation, AssetLocation> Remaps(TestWorld world) =>
    new SmexToIwexMigration()
      .GetRemaps(world.Api)
      .ToDictionary(r => r.oldCode, r => r.newCode);

  #region Relocated blocks

  [Theory]
  [InlineData("smex:moltencanal-straight-fire-ns", "iwex:molten-canal-straight-fire-ns")]
  [InlineData("smex:moltencanal-tap-n", "iwex:molten-canal-tap-n")]
  [InlineData("smex:slag", "iwex:slag-block")]
  [InlineData("smex:solidifiediron", "iwex:hearthmetal-pigiron")]
  // The three that were silently uncovered until 2026-08-03. Asserted one per case so a dropped row
  // names itself instead of moving a count.
  [InlineData("smex:blastfurnace-tuyere-n", "iwex:furnace-tuyere-n")]
  [InlineData("smex:blastfurnacetap-n", "iwex:furnace-irontap-n")]
  [InlineData("smex:blastfurnacedoor", "iwex:furnace-chargedoor-n")]
  public void Each_relocated_block_remaps_from_its_shipped_smex_code(
    string oldCode,
    string newCode
  )
  {
    var remaps = Remaps(WorldWithLiveTargets());

    Assert.True(
      remaps.TryGetValue(new AssetLocation(oldCode), out AssetLocation? actual),
      $"{oldCode} shipped in smex 0.9.4 and has no remap - a released world loses it."
    );
    Assert.Equal(new AssetLocation(newCode), actual);
  }

  [Fact]
  public void Each_variant_is_remapped_individually()
  {
    // Variants are still derived from the live registry, so a new orientation needs no edit here.
    var world = WorthTwoCanalBends();
    var remaps = Remaps(world);

    Assert.Equal(2, remaps.Count);
    Assert.Contains(new AssetLocation("smex", "moltencanal-bend-fire-nw"), remaps.Keys);
    Assert.Contains(new AssetLocation("smex", "moltencanal-bend-fire-se"), remaps.Keys);

    static TestWorld WorthTwoCanalBends() =>
      WorldWith(
        Block("iwex:molten-canal-bend-fire-nw", 100),
        Block("iwex:molten-canal-bend-fire-se", 101)
      );
  }

  [Fact]
  public void Every_shipped_tap_orientation_lands_on_the_iron_tap()
  {
    // The tap split in two on 2026-08-03 and the migration cannot tell which half a shipped block was.
    // smex had one tap blocktype for both notches, so its code carries no record of whether the player
    // built an iron notch or a cinder notch - there is nothing to branch on. Every one therefore lands on
    // `irontap`, and this test is where that is *stated* rather than left to be discovered in a save.
    //
    // All four orientations, not just the default: the target's variant grammar changed shape (one
    // group became two), and a mapping that happened to work for `north` while dropping the rest is
    // exactly the failure the shipped-code contract exists to catch.
    var world = WorldWith(
        Block("iwex:furnace-irontap-n", 200),
        Block("iwex:furnace-irontap-e", 201),
        Block("iwex:furnace-irontap-s", 202),
        Block("iwex:furnace-irontap-w", 203),
        Block("iwex:furnace-slagtap-n", 204)
      );

    var remaps = Remaps(world);

    // Letters on both sides, and that is a fact about what shipped rather than a style choice:
    // `ReleasedCodes.Smex` records `smex:blastfurnacetap-{e,n,s,w}`, because smex spelled every facing
    // as a letter already. So the 2026-08-04 side respelling left this row alone - the released code
    // and the live one simply agree again. The ppex rows are the opposite case and need
    // `legacySideWords`; see CodeRelocation.
    foreach (string side in new[] { "n", "e", "s", "w" })
      Assert.Equal(
        new AssetLocation("iwex", "furnace-irontap-" + side),
        remaps[new AssetLocation("smex", "blastfurnacetap-" + side)]
      );

    // ...and nothing is invented for the notch smex never had. A `smex:blastfurnaceslagtap` source would
    // name a block that never shipped, which is the dead weight that reads like coverage.
    Assert.DoesNotContain(remaps.Values, v => v.Path.StartsWith("furnace-slagtap"));
  }

  #endregion

  #region Exclusions

  [Fact]
  public void Blocks_that_never_shipped_under_smex_are_not_remapped()
  {
    // The ore line arrived with iwex; the slag paving line was added after the split. Rows for
    // blocks that never escaped read like coverage and are not.
    // These were `iwex:ore-bunker-red-n` / `iwex:ore-mixer-n` until both machines were deleted. The
    // fixture has to name blocks that really exist, or it stops being a statement about what shipped and
    // becomes a statement about two strings.
    var world = WorldWith(
      Block("iwex:burdenmaker-red-n", 100),
      Block("iwex:crafting-designtable-n", 101),
      Block("iwex:slag-path-free", 102),
      Block("iwex:slag-block", 103) // a genuinely relocated one, for contrast
    );
    var remaps = Remaps(world);

    Assert.Single(remaps);
    Assert.Contains(new AssetLocation("smex", "slag"), remaps.Keys);
  }

  [Fact]
  public void The_slag_base_does_not_swallow_the_slag_paving_family()
  {
    // `slag` and `slagpath*` differ by one character at the boundary. CodeRelocation requires a
    // literal '-' after the base for exactly this reason; losing it would migrate the whole family.
    var world = WorldWith(
      Block("iwex:slag-block", 100),
      Block("iwex:slag-path-free", 101),
      Block("iwex:slag-pathslab-free", 102),
      Block("iwex:slag-bricks", 103)
    );

    Assert.Equal([new AssetLocation("smex", "slag")], Remaps(world).Keys);
  }

  [Fact]
  public void Blocks_outside_the_iwex_domain_are_ignored()
  {
    var world = WorldWith(
      Block("smex:moltencanal-straight-fire-ns", 100),
      Block("game:slag", 101)
    );
    Assert.Empty(Remaps(world));
  }

  [Fact]
  public void The_hoppers_stayed_in_smex_and_are_not_claimed()
  {
    // hopperbell/hopperreinforced were in the old relocated set but never left smex. Claiming a
    // block that did not move is how a migration starts rewriting live content.
    var world = WorldWith(
      Block("smex:hopperbell", 100),
      Block("smex:hopperreinforced", 101)
    );
    Assert.Empty(Remaps(world));
  }

  #endregion
}
