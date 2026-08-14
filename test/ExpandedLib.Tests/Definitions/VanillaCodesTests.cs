using ExpandedLib.Definitions;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The vanilla layout block-code catalogue: codes the game declares, which no definition can generate.
/// Covers what the goldens do not reach - the containment order between brick-route rungs no layout
/// draws yet, and the facing helpers, whose interpolated side segment is what makes a cell
/// orientation-checked. How a constant used by a shipped layout expands is recorded in that layout's
/// blocktype golden.
/// </summary>
public class VanillaCodesTests {
  // Real vanilla codes, read off survival/blocktypes/clay/*.json, so a rung cannot pass by matching a
  // code the game does not ship.
  private const string RefractoryTier1 = "refractorybricks-good-tier1";
  private const string RefractoryTier3 = "refractorybricks-good-tier3";
  private const string RefractoryDamaged = "refractorybricks-damaged-tier1";
  private const string FireBrick = "claybricks-good-fire";
  private const string CourseRed = "brickcourse-four-running-red";
  private const string CourseClinker = "brickcourse-four-running-clinker";
  private const string ClinkerRough = "claybricks-clinkerrough";

  private static bool Matches(string code, string blockCode) =>
    WildcardUtil.Match(
      new Vintagestory.API.Common.AssetLocation(code),
      new Vintagestory.API.Common.AssetLocation("game:" + blockCode)
    );

  #region The brick route is really a route

  [Theory]
  // Refractory: every tier, and only refractory.
  [InlineData(VanillaCodes.Refractory, RefractoryTier1, true)]
  [InlineData(VanillaCodes.Refractory, RefractoryTier3, true)]
  [InlineData(VanillaCodes.Refractory, FireBrick, false)]
  [InlineData(VanillaCodes.Refractory, CourseRed, false)]
  // Damaged refractory is not sidesolid, so it must not satisfy a structural cell; the `-good-`
  // segment is what excludes it.
  [InlineData(VanillaCodes.Refractory, RefractoryDamaged, false)]
  // Refractory or fire: the next rung up, and it contains the one below.
  [InlineData(VanillaCodes.RefractoryOrFire, RefractoryTier1, true)]
  [InlineData(VanillaCodes.RefractoryOrFire, RefractoryTier3, true)]
  [InlineData(VanillaCodes.RefractoryOrFire, FireBrick, true)]
  [InlineData(VanillaCodes.RefractoryOrFire, CourseRed, false)]
  [InlineData(VanillaCodes.RefractoryOrFire, RefractoryDamaged, false)]
  // Coloured courses - a sibling rung, not a parent: it admits no refractory and no fire brick.
  [InlineData(VanillaCodes.ColouredBricks, CourseRed, true)]
  [InlineData(VanillaCodes.ColouredBricks, CourseClinker, true)]
  [InlineData(VanillaCodes.ColouredBricks, RefractoryTier1, false)]
  [InlineData(VanillaCodes.ColouredBricks, FireBrick, false)]
  // The top rung contains every branch below it.
  [InlineData(VanillaCodes.AnyBricks, RefractoryTier1, true)]
  [InlineData(VanillaCodes.AnyBricks, RefractoryTier3, true)]
  [InlineData(VanillaCodes.AnyBricks, FireBrick, true)]
  [InlineData(VanillaCodes.AnyBricks, CourseRed, true)]
  // Damaged refractory stays excluded on every rung.
  [InlineData(VanillaCodes.AnyBricks, RefractoryDamaged, false)]
  public void Each_rung_admits_exactly_what_it_claims(
    string rung,
    string blockCode,
    bool expected
  ) => Assert.Equal(expected, Matches(rung, blockCode));

  [Fact]
  public void Clinker_is_admitted_in_both_of_the_places_vanilla_puts_it() {
    // Clinker belongs on every rung that says "any". Vanilla files it twice: as the eighth
    // `brickcourse` colour, and as a standalone `claybricks` variant with no `state` group at all.
    // AnyBricks therefore takes the colour group whole instead of enumerating colours by name.
    Assert.True(Matches(VanillaCodes.ColouredBricks, CourseClinker));
    Assert.True(Matches(VanillaCodes.AnyBricks, CourseClinker));
    Assert.True(Matches(VanillaCodes.AnyBricks, ClinkerRough));

    // Containment over the whole colour group.
    Assert.True(Matches(VanillaCodes.AnyBricks, CourseRed));
  }

  #endregion

  #region The facing has to reach the string

  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void A_cardinal_slab_is_orientation_checked(string side) {
    // The oriented-parts feature decides a cell is facing-checked by reading the code, so a helper
    // that stopped interpolating the facing would still compile and still match a block, while
    // letting the slab be laid any way round.
    BlockFacing facing = BlockFacing.FromCode(side);

    Assert.Contains($"-{side}-", VanillaCodes.FireSlab(facing));
    Assert.Contains($"-{side}-", VanillaCodes.AnySlab(facing));
    Assert.NotEmpty(
      MultiblockLayoutBuilder.FindOrientationSegments(
        VanillaCodes.FireSlab(facing)
      )
    );
    Assert.NotEmpty(
      MultiblockLayoutBuilder.FindOrientationSegments(
        VanillaCodes.AnySlab(facing)
      )
    );
  }

  [Fact]
  public void A_wildcard_earlier_in_the_path_does_not_shadow_the_facing() {
    // AnySlab puts a `*` before the facing, spanning one variant group on fire slabs (`fire`) and two
    // on coloured ones (`four-red`). The facing lands on the same segment index either way, because a
    // star is not an orientation token and is skipped.
    Assert.Equal(
      [2],
      MultiblockLayoutBuilder.FindOrientationSegments(
        VanillaCodes.AnySlab(BlockFacing.SOUTH)
      )
    );
    Assert.Equal(
      [2],
      MultiblockLayoutBuilder.FindOrientationSegments(
        VanillaCodes.FireSlab(BlockFacing.SOUTH)
      )
    );

    // Rotation keeps the star intact, so the rotated code still admits both slab families. The
    // convention is north 0, west 90, south 180, east 270 (ExOrientation), so a +90 step runs
    // south, east, north, west.
    Assert.Equal(
      "brickslabs-*-east-free",
      Blocks.Structures.MultiblockFacings.RotateSegments(
        "brickslabs-*-south-free",
        [2],
        90
      )
    );
  }

  [Theory]
  // Vertical never rotates under a Y turn, so an up-facing slab is not an oriented part.
  [InlineData("up")]
  [InlineData("down")]
  public void A_vertical_slab_is_not_orientation_checked(string side) =>
    Assert.Empty(
      MultiblockLayoutBuilder.FindOrientationSegments(
        VanillaCodes.FireSlab(BlockFacing.FromCode(side))
      )
    );

  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_coke_oven_door_pins_its_side_and_leaves_its_state_wild(
    string side
  ) {
    // Vanilla spells the code `cokeovendoor-{closed|opened}-{side}`. A trailing `*` would swallow the
    // side along with the state and admit a door hung any way round; pinning the state instead would
    // break the structure the moment the player opened the door.
    string code = VanillaCodes.CokeOvenDoor(BlockFacing.FromCode(side));

    Assert.EndsWith($"-{side}", code);
    Assert.Contains("-*-", code);
    Assert.Equal([2], MultiblockLayoutBuilder.FindOrientationSegments(code));
  }

  [Theory]
  // Vanilla's variant is the opposite of the wall face the door closes, the reverse of this family's
  // own doors: `iiex:furnace-puddlingchargedoor-south` sits in the south wall, while the coke door in
  // that same wall is spelled `north`. Derived from `liquidBarrierOnSidesByType`, which gives
  // `cokeovendoor-closed-north` a barrier at face index 2, with `BlockFacing.ALLFACES` running
  // N, E, S, W. `Sealing` is the only place the inversion is applied; layouts name the wall.
  [InlineData("south", "north")]
  [InlineData("north", "south")]
  [InlineData("west", "east")]
  [InlineData("east", "west")]
  public void Sealing_a_wall_asks_for_the_opposite_variant(
    string wall,
    string variant
  ) =>
    Assert.Equal(
      VanillaCodes.CokeOvenDoor(BlockFacing.FromCode(variant)),
      VanillaCodes.Sealing(BlockFacing.FromCode(wall))
    );

  #endregion

  #region Stairs carry two orientation groups

  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void A_stair_is_checked_on_its_cardinal_not_its_half(string side) {
    // A stair code carries `up`/`down` then a cardinal, which is the case the "last whole side
    // segment wins" rule exists for. Scanning from the front would find the half, rotate it to a
    // cardinal, and produce a code matching no block.
    BlockFacing facing = BlockFacing.FromCode(side);

    foreach (BlockFacing half in new[] { BlockFacing.UP, BlockFacing.DOWN }) {
      string fire = VanillaCodes.FireStairs(half, facing);
      string any = VanillaCodes.AnyStairs(half, facing);

      Assert.Contains($"-{half.Code}-{side}-", fire);
      Assert.Contains($"-{half.Code}-{side}-", any);

      // Only segment 3, the cardinal. The half at index 2 is `up`/`down`, which no Y rotation moves
      // and which the code already matches literally; it must never be picked instead.
      Assert.Equal([3], MultiblockLayoutBuilder.FindOrientationSegments(fire));
      Assert.Equal([3], MultiblockLayoutBuilder.FindOrientationSegments(any));
    }
  }

  [Fact]
  public void A_stair_rotates_its_cardinal_and_leaves_its_half_alone() {
    string code = VanillaCodes.FireStairs(BlockFacing.UP, BlockFacing.NORTH);
    int[] segments = [.. MultiblockLayoutBuilder.FindOrientationSegments(code)];

    // north + 90 is west under the family's convention (north 0, west 90, south 180, east 270).
    Assert.Equal(
      "brickstairs-fire-up-west-free",
      Blocks.Structures.MultiblockFacings.RotateSegments(
        new Vintagestory.API.Common.AssetLocation(code).Path,
        segments,
        90
      )
    );
  }

  [Theory]
  // The two groups passed the wrong way round. Both orderings compile and both produce a code
  // matching no block, so the helper throws instead.
  [InlineData("north", "up")]
  [InlineData("south", "down")]
  public void A_stair_with_its_two_groups_swapped_is_refused(
    string half,
    string facing
  ) =>
    Assert.Throws<System.ArgumentException>(() =>
      VanillaCodes.FireStairs(
        BlockFacing.FromCode(half),
        BlockFacing.FromCode(facing)
      )
    );

  #endregion

  #region Tier pinning

  [Fact]
  public void A_pinned_tier_admits_that_tier_only() {
    // The hot blast furnace is the one shell that pins its material, so RefractoryTier must be
    // narrower than Refractory rather than a synonym for it.
    Assert.True(Matches(VanillaCodes.RefractoryTier(3), RefractoryTier3));
    Assert.False(Matches(VanillaCodes.RefractoryTier(3), RefractoryTier1));
    Assert.True(Matches(VanillaCodes.Refractory, RefractoryTier1));
  }

  #endregion
}
