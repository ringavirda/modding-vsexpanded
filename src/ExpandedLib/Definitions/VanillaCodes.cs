using Vintagestory.API.MathTools;

namespace ExpandedLib.Definitions;

/// <summary>
/// The catalogue of vanilla block codes the mod family's multiblock layouts are drawn from, named once here
/// so that a typo in a <see cref="MultiblockLayoutBuilder.Legend"/> is a compile error rather than a
/// <c>blockNumbers</c> entry matching no block, which throws nowhere and leaves the structure unable to
/// complete. Unlike the mods' own codes, these cannot be generated from an <c>ExBlockDef</c>.
/// <see cref="ExCodes"/> holds exlib's own blocks and <c>IiexCodes</c> / <c>IiexCodes</c> each mod's;
/// vanilla items named by recipes live in <see cref="ExIngredients"/>, and a string both need lives here.
/// </summary>
public static class VanillaCodes {
  #region Air

  /// <summary>
  /// Air: <c>game:air</c>. A cell the layout requires to stay empty - the vent shaft above a stockline, a
  /// flue, a tap alcove. There is no wildcarded sibling; vanilla ships no <c>air-*</c> variant.
  /// </summary>
  public const string Air = "game:air";

  #endregion

  #region Refractory and brick

  // A route of permissiveness, strictest first; a layout picks the rung matching what the cell is for:
  //
  //   RefractoryTier(n) ⊂ Refractory ⊂ RefractoryOrFire ⊂ AnyBricks
  //
  // All of these are domainless or `game:`-prefixed: `@(…)` is a regex over the path only, so a bare
  // alternation is implicitly `game:` and cannot admit a modded brick. Correct here, since these name
  // vanilla's own masonry; see CoalBed and IiexCodes.ChargeShaft for where it is not.

  /// <summary>Refractory brick pinned to one tier: <c>game:refractorybricks-good-tier{tier}</c>. For a
  /// shell whose heat leaves no cheaper tier viable; the hot blast furnace demands tier 3
  /// throughout.</summary>
  public static string RefractoryTier(int tier) =>
    $"game:refractorybricks-good-tier{tier}";

  /// <summary>
  /// Any refractory brick, any tier: <c>game:refractorybricks-good-tier*</c>. The default shell material for
  /// every furnace in the family. <c>-good-</c> excludes vanilla's <c>damaged</c> state, which declares
  /// <c>sidesolid: false</c> and so cannot hold a structure; <c>tier*</c> spans the whole <c>type</c> group
  /// (tier1/2/3).
  /// </summary>
  public const string Refractory = "game:refractorybricks-good-tier*";

  /// <summary>Fire brick: <c>game:claybricks-good-fire</c>. The boilers' setting - the cheap
  /// heat-resisting masonry that is not refractory.</summary>
  public const string FireBricks = "game:claybricks-good-fire";

  /// <summary>
  /// Any refractory brick or fire brick: <c>@(refractorybricks-good-tier.*|claybricks-good-fire)</c>. The
  /// rung for a wall that resists a working heat but not a metallurgical one - between them, exactly the two
  /// masonries vanilla marks <c>cokeOvenViable</c>.
  /// </summary>
  public const string RefractoryOrFire =
    "@(refractorybricks-good-tier.*|claybricks-good-fire)";

  /// <summary>
  /// Any coloured brick course, in any bond and any colour: <c>game:brickcourse-*</c>. Vanilla's
  /// <c>brickcourse</c> spans <c>{four|eight} × {header|soldier|running|…} × {black…tan|clinker}</c> and this
  /// admits the lot - the rung for masonry that is structural but whose appearance is the player's.
  /// </summary>
  public const string ColouredBricks = "game:brickcourse-*";

  /// <summary>
  /// Any brick a wall can be built from - fire, clinker, refractory of any tier, or a coloured course. The
  /// loosest rung, for a cell that must be masonry and is otherwise the player's to choose. Clinker is
  /// covered in both places vanilla puts it, the eighth <c>brickcourse</c> colour and the standalone
  /// <c>claybricks-clinkerrough</c> block; the <c>claybrickchimney</c> palette omits the clinker course and
  /// is not a safe list to copy. Damaged refractory stays excluded - see <see cref="Refractory"/>.
  /// </summary>
  public const string AnyBricks =
    "@(claybricks-(good-fire|clinkerrough)|refractorybricks-good-.*|brickcourse-.*)";

  /// <summary>The refractory grate: <c>game:refractorybrickgrating-good-tier*</c>. Vanilla's block is
  /// <c>refractorybrickgrating</c>; the shortening <c>refractorygrating</c> is not a block and resolves to
  /// nothing.</summary>
  public const string RefractoryGrating =
    "game:refractorybrickgrating-good-tier*";

  #endregion

  #region Slabs

  // The same route one rung lower, for half-height courses. Every slab code takes a facing: a slab opens a
  // hearth mouth only when laid the right way round.
  //
  // `-free` is vanilla's no-snow cover state and its `-snow` sibling is a different block, so a slab cell
  // with sky above it stops satisfying the structure the first time it snows. Every rung here pins `-free`,
  // so an open-topped layout must roof its slabs.

  /// <summary>
  /// A free-standing fire-brick slab facing <paramref name="facing"/>:
  /// <c>game:brickslabs-fire-{facing}-free</c>. The shoulders round a hearth doorway - <c>BlockFacing.UP</c>
  /// for the flat course (emitting <c>up</c>, which no Y rotation moves), a cardinal for the upright cheeks.
  /// The facing has to reach the emitted string: the layout builder facing-checks a cell only by reading the
  /// orientation out of the code.
  /// </summary>
  public static string FireSlab(BlockFacing facing) =>
    $"game:brickslabs-fire-{facing.Code}-free";

  /// <summary>
  /// A free-standing slab of any brick facing <paramref name="facing"/>:
  /// <c>game:brickslabs-*-{facing}-free</c>. The loose rung, matching <see cref="AnyBricks"/>; vanilla's
  /// <c>*</c> is a regex that crosses dashes, so it spans one variant group on the fire slabs and two on the
  /// coloured ones while the facing stays a whole segment and the cell remains orientation-checked. Fire and
  /// coloured slabs share one block code, so "coloured but not fire" cannot be expressed.
  /// </summary>
  public static string AnySlab(BlockFacing facing) =>
    $"game:brickslabs-*-{facing.Code}-free";

  #endregion

  #region Stairs

  // Stairs carry two orientation groups - which way the step faces, and whether it is inverted - so their
  // codes read `brickstairs-fire-{up|down}-{cardinal}-free`. Only the cardinal rotates with the structure;
  // `up`/`down` is invariant under a Y turn, and the layout builder records only what a Y rotation moves.

  /// <summary>
  /// A free-standing fire-brick stair: <c>game:brickstairs-fire-{half}-{facing}-free</c>.
  /// <paramref name="half"/> is <c>BlockFacing.UP</c> for an inverted stair or <c>BlockFacing.DOWN</c> for a
  /// normal one; <paramref name="facing"/> is the cardinal the step faces, and turns with the structure.
  /// </summary>
  public static string FireStairs(BlockFacing half, BlockFacing facing) =>
    $"game:brickstairs-fire-{Half(half)}-{Cardinal(facing)}-free";

  /// <summary>
  /// A free-standing stair of any brick: <c>game:brickstairs-*-{half}-{facing}-free</c>. The loose rung,
  /// matching <see cref="AnySlab"/>: the star spans one variant group on the fire stairs and two on the
  /// coloured ones, and the cardinal stays a whole segment, so the cell remains orientation-checked.
  /// </summary>
  public static string AnyStairs(BlockFacing half, BlockFacing facing) =>
    $"game:brickstairs-*-{Half(half)}-{Cardinal(facing)}-free";

  /// <summary>
  /// Guards the vertical half of a stair code: the two orientation groups passed the wrong way round compile
  /// into a plausible code that matches no block, so this throws at load instead.
  /// </summary>
  private static string Half(BlockFacing half) =>
    half == BlockFacing.UP || half == BlockFacing.DOWN
      ? half.Code
      : throw new System.ArgumentException(
        $"A stair's half must be UP (inverted) or DOWN (normal), not '{half.Code}'. The cardinal the "
          + "step faces is the second argument.",
        nameof(half)
      );

  /// <summary>Guards the horizontal half of a stair code - see <see cref="Half"/>.</summary>
  private static string Cardinal(BlockFacing facing) =>
    facing != BlockFacing.UP && facing != BlockFacing.DOWN
      ? facing.Code
      : throw new System.ArgumentException(
        $"A stair's step must face a cardinal, not '{facing.Code}'. Whether the stair is inverted is "
          + "the first argument.",
        nameof(facing)
      );

  #endregion

  #region Doors and openings

  /// <summary>
  /// A coke-oven door facing <paramref name="facing"/>, in any state: <c>game:cokeovendoor-*-{facing}</c>.
  /// The state stays wild and the side is pinned: over <c>cokeovendoor-{closed|opened}-{side}</c> a trailing
  /// <c>*</c> would swallow the side along with the state, while pinning the state would break the structure
  /// the moment the player opened the door. <paramref name="facing"/> is the opposite of the wall face the
  /// door closes, the reverse of this family's own door convention; prefer <see cref="Sealing"/>.
  /// </summary>
  public static string CokeOvenDoor(BlockFacing facing) =>
    $"game:cokeovendoor-*-{facing.Code}";

  /// <summary>
  /// The coke-oven door that closes the <paramref name="wall"/> face of its cell ("the door in the south
  /// wall"). Converts to vanilla's inverted spelling via <see cref="CokeOvenDoor"/>.
  /// </summary>
  public static string Sealing(BlockFacing wall) => CokeOvenDoor(wall.Opposite);

  #endregion

  #region Fuel beds

  /// <summary>
  /// A plain fuel bed - vanilla coal pile or nothing: <c>@(air|coalpile)</c>. The alternation is domainless:
  /// vanilla's <c>WildcardUtil</c> compares domain and path separately and <c>@(…)</c> is a regex over the
  /// path only, so this is implicitly <c>game:</c> and can never admit a mod's own charge block - correct
  /// for a firebox, wrong for a shaft (see <c>IiexCodes.ChargeShaft</c>). Air is an accepted occupant, so a
  /// layout drawn with this completes with no fuel cell built at all.
  /// </summary>
  public const string CoalBed = "@(air|coalpile)";

  #endregion
}
