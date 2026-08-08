using ExpandedLib.Definitions;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The <b>vanilla</b> layout block-code catalogue. Two quite different things need pinning here, and only
/// one of them is covered elsewhere.
/// <para>
/// These are the codes that can never be generated from a definition, because the game declares them and
/// we do not - which is exactly why the historical typo was among them, and why they earn a test at all.
/// </para>
/// <para>
/// <b>That the constants still expand to what the layouts shipped</b> is already proven, and proven
/// better than a test could: every layout's blocktype golden records the <em>expanded</em> code, so a
/// mistyped constant moves a golden and the parity suites fail. Nothing here duplicates that.
/// </para>
/// <para>
/// What is <b>not</b> covered elsewhere is everything the catalogue newly <em>claims</em>. Three rungs of
/// the brick ladder are drawn by no layout yet, so no golden touches them, and the class remarks assert a
/// containment order between them that nothing checks. The facing helpers carry a "do not clean this
/// up into a parameter that stops being interpolated" warning, which is precisely the kind of warning a
/// later reader overrules unless something fails. Both are pinned below.
/// </para>
/// </summary>
public class VanillaCodesTests
{
  // Real vanilla codes, read off survival/blocktypes/clay/*.json rather than invented, so a rung that
  // matches nothing in the game cannot pass by matching a code the game does not ship either.
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

  #region The brick ladder is really a ladder

  [Theory]
  // Refractory: every tier, and only refractory.
  [InlineData(VanillaCodes.Refractory, RefractoryTier1, true)]
  [InlineData(VanillaCodes.Refractory, RefractoryTier3, true)]
  [InlineData(VanillaCodes.Refractory, FireBrick, false)]
  [InlineData(VanillaCodes.Refractory, CourseRed, false)]
  // Damaged refractory is not sidesolid, so it must not satisfy a structural cell. The `-good-`
  // segment is what excludes it, and it is easy to lose while "simplifying" the wildcard.
  [InlineData(VanillaCodes.Refractory, RefractoryDamaged, false)]
  // Refractory or fire - the next rung up, and it must genuinely contain the one below.
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
  // …still excluding the one brick a wall cannot be built from.
  [InlineData(VanillaCodes.AnyBricks, RefractoryDamaged, false)]
  public void Each_rung_admits_exactly_what_it_claims(
    string rung,
    string blockCode,
    bool expected
  ) => Assert.Equal(expected, Matches(rung, blockCode));

  [Fact]
  public void Clinker_is_admitted_in_both_of_the_places_vanilla_puts_it()
  {
    // Clinker is a brick like any other, so it belongs on every rung that says "any". Vanilla files
    // it twice - as the eighth `brickcourse` colour, and as a standalone `claybricks` variant with no
    // `state` group at all - and it is easy to catch one and miss the other.
    //
    // This replaces an earlier gap: AnyBricks used to enumerate seven colours by name, apparently
    // copied from the `claybrickchimney` palette (which really has no clinker course). Enumerating the
    // colours is what let one go missing, so the colour group is now taken whole.
    Assert.True(Matches(VanillaCodes.ColouredBricks, CourseClinker));
    Assert.True(Matches(VanillaCodes.AnyBricks, CourseClinker));
    Assert.True(Matches(VanillaCodes.AnyBricks, ClinkerRough));

    // …and the containment that was broken by the gap now genuinely holds.
    Assert.True(Matches(VanillaCodes.AnyBricks, CourseRed));
  }

  #endregion

  #region The facing has to reach the string

  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void A_cardinal_slab_is_orientation_checked(string side)
  {
    // The invariant the helpers' doc-comments warn about. The oriented-parts feature decides a cell
    // is facing-checked by reading the code, so a helper that stopped interpolating the facing would
    // still compile, still produce a code that matches a block, and silently let a slab be laid any way
    // round - the exact defect that let a "complete" furnace show gaps in its walls.
    BlockFacing facing = BlockFacing.FromCode(side);

    Assert.Contains($"-{side}-", VanillaCodes.FireSlab(facing));
    Assert.Contains($"-{side}-", VanillaCodes.AnySlab(facing));
    Assert.NotEmpty(MultiblockLayoutBuilder.FindOrientationSegments(VanillaCodes.FireSlab(facing)));
    Assert.NotEmpty(MultiblockLayoutBuilder.FindOrientationSegments(VanillaCodes.AnySlab(facing)));
  }

  [Fact]
  public void A_wildcard_earlier_in_the_path_does_not_shadow_the_facing()
  {
    // AnySlab puts a `*` before the facing, spanning one variant group on fire slabs (`fire`) and two
    // on coloured ones (`four-red`). The facing is still found - and lands on the same segment index
    // either way - because the star is not an orientation token, so it is simply skipped.
    Assert.Equal([2], MultiblockLayoutBuilder.FindOrientationSegments(VanillaCodes.AnySlab(BlockFacing.SOUTH)));
    Assert.Equal([2], MultiblockLayoutBuilder.FindOrientationSegments(VanillaCodes.FireSlab(BlockFacing.SOUTH)));

    // …and rotating it keeps the star intact, so the rotated code still admits both slab families.
    // south + 90° is east, not west: the family's convention is north 0°, west 90°, south 180°,
    // east 270° (ExOrientation), so a +90 step runs south → east → north → west.
    Assert.Equal(
      "brickslabs-*-east-free",
      Blocks.Structures.MultiblockFacings.RotateSegments("brickslabs-*-south-free", [2], 90)
    );
  }

  [Theory]
  // Vertical never rotates under a Y turn, so an up-facing slab is deliberately not an oriented part.
  [InlineData("up")]
  [InlineData("down")]
  public void A_vertical_slab_is_not_orientation_checked(string side) =>
    Assert.Empty(
      MultiblockLayoutBuilder.FindOrientationSegments(VanillaCodes.FireSlab(BlockFacing.FromCode(side)))
    );

  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_coke_oven_door_pins_its_side_and_leaves_its_state_wild(string side)
  {
    // The only combination that works. Vanilla spells the code
    // `cokeovendoor-{closed|opened}-{side}`, so the trailing `*` these layouts used to say swallowed
    // the side along with the state - a door hung any way round satisfied the cell. Pinning the state
    // instead would be worse: the structure would come apart the moment the player opened the door.
    string code = VanillaCodes.CokeOvenDoor(BlockFacing.FromCode(side));

    Assert.EndsWith($"-{side}", code);
    Assert.Contains("-*-", code);
    Assert.Equal([2], MultiblockLayoutBuilder.FindOrientationSegments(code));
  }

  [Theory]
  // Vanilla's variant is the opposite of the wall face the door closes, and this is the reverse of
  // our own doors' convention: `iwex:furnace-puddlingchargedoor-south` sits in the south wall, while the coke
  // door beside it in that same wall must be spelled `north`.
  //
  // Derived, not guessed: `liquidBarrierOnSidesByType` gives `cokeovendoor-closed-north` a barrier at
  // face index 2, and `BlockFacing.ALLFACES` runs N, E, S, W - so a `north` door seals its south side.
  // `Sealing` is the only place that inversion is reasoned about; every layout says which wall it is
  // filling, which is how an author actually thinks about it.
  [InlineData("south", "north")]
  [InlineData("north", "south")]
  [InlineData("west", "east")]
  [InlineData("east", "west")]
  public void Sealing_a_wall_asks_for_the_opposite_variant(string wall, string variant) =>
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
  public void A_stair_is_checked_on_its_cardinal_not_its_half(string side)
  {
    // The case "the last whole side segment wins" exists for. A stair code carries `up`/`down` and
    // a cardinal, in that order. Scanning from the front would find the half, orientation-check it,
    // rotate it to a cardinal, and produce a code matching no block - a cell that can never be
    // satisfied, with no error anywhere.
    BlockFacing facing = BlockFacing.FromCode(side);

    foreach (BlockFacing half in new[] { BlockFacing.UP, BlockFacing.DOWN })
    {
      string fire = VanillaCodes.FireStairs(half, facing);
      string any = VanillaCodes.AnyStairs(half, facing);

      Assert.Contains($"-{half.Code}-{side}-", fire);
      Assert.Contains($"-{half.Code}-{side}-", any);

      // Only segment 3 - the cardinal. The half at index 2 is `up`/`down`, which no Y rotation
      // moves, so recording it would buy nothing: it is already required exactly by the code
      // matching literally. What must never happen is the half being picked instead of the cardinal.
      Assert.Equal([3], MultiblockLayoutBuilder.FindOrientationSegments(fire));
      Assert.Equal([3], MultiblockLayoutBuilder.FindOrientationSegments(any));
    }
  }

  [Fact]
  public void A_stair_rotates_its_cardinal_and_leaves_its_half_alone()
  {
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
  // The two halves passed the wrong way round. Both orderings still compile and both produce a
  // plausible-looking code that matches no block - so the load fails loudly instead.
  [InlineData("north", "up")]
  [InlineData("south", "down")]
  public void A_stair_with_its_two_groups_swapped_is_refused(
    string half,
    string facing
  ) =>
    Assert.Throws<System.ArgumentException>(() =>
      VanillaCodes.FireStairs(BlockFacing.FromCode(half), BlockFacing.FromCode(facing))
    );

  #endregion

  #region Tier pinning

  [Fact]
  public void A_pinned_tier_admits_that_tier_only()
  {
    // The hot blast furnace is the one shell in the family that pins its material, so the difference
    // between this and Refractory is load-bearing rather than stylistic.
    Assert.True(Matches(VanillaCodes.RefractoryTier(3), RefractoryTier3));
    Assert.False(Matches(VanillaCodes.RefractoryTier(3), RefractoryTier1));
    Assert.True(Matches(VanillaCodes.Refractory, RefractoryTier1));
  }

  #endregion
}
