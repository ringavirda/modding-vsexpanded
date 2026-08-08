using Vintagestory.API.MathTools;

namespace ExpandedLib.Definitions;

/// <summary>
/// The catalogue of <b>vanilla</b> block codes the mod family's multiblock layouts are drawn from -
/// authored once here instead of re-typed in every <see cref="MultiblockLayoutBuilder.Legend"/> line.
/// <para>
/// <b>A layout code is a magic string with nothing between the author and a typo.</b> A code that
/// resolves to no block does not throw at the keystroke, at compile, or at first placement - it
/// becomes a <c>blockNumbers</c> entry matching nothing, and the structure can never complete. That
/// is not hypothetical: the layout scratchpad carried a <c>FIX:</c> note for exactly this, where
/// <c>refractorygrating</c> had been written for <see cref="RefractoryGrating"/>'s
/// <c>refractorybrickgrating</c>. Referenced by name, a typo is a compile error.
/// </para>
/// <para>
/// <b>This is the one catalogue that can never be generated.</b> Every other code in the suite is
/// declared by an <c>ExBlockDef</c> somewhere and could be emitted from it; these belong to the game,
/// so they are hand-authored and hand-verified. That is exactly why the historical typo was here and
/// not in a mod's own codes - and why this file deserves the most scrutiny per line.
/// </para>
/// <para>
/// <b>Where the others live.</b> <see cref="ExCodes"/> holds exlib's own blocks;
/// <c>IwexCodes</c> / <c>LpexCodes</c> hold each mod's, so the dependency runs down the chain
/// exlib ← iwex ← lpex ← smex/hpex and never back up. A vanilla code used by exactly one layout for
/// something only that layout cares about may still stay inline - but the bar is low, because the
/// value here is the compiler, not the deduplication.
/// </para>
/// <para>
/// <b>Blocks, not items.</b> Vanilla <em>items</em> named for recipes live in
/// <see cref="ExIngredients"/>, which wraps them in <c>IngredientBuilder</c> factories rather than
/// handing back bare strings. The two do not overlap today (this file is masonry, air and doors;
/// that one is tools, plates and rods) - but they would the moment a recipe wants a brick, so a
/// shared string belongs here and <c>ExIngredients</c> should reference it.
/// </para>
/// </summary>
public static class VanillaCodes
{
  #region Air

  /// <summary>
  /// Air: <c>game:air</c>. The vent shaft above a stockline, a flue, a tap alcove - a cell the layout
  /// requires to stay <em>empty</em>.
  /// <para>
  /// There is deliberately no wildcarded sibling: vanilla ships no <c>air-*</c> variant, so
  /// <c>game:air*</c> would match exactly the same single block.
  /// </para>
  /// </summary>
  public const string Air = "game:air";

  #endregion

  #region Refractory and brick

  // A ladder OF permissiveness, strictest first. A layout picks the rung that matches what the
  // cell is actually for: a hearth wall that must survive the heat pins a tier; a shell that only
  // has to be masonry takes AnyBricks and lets the player build it out of whatever they have. The
  // rungs are named rather than spelled out per layout because the difference between them is a
  // gameplay decision - how much the furnace dictates the player's material - and it should be
  // legible on the drawing rather than buried in a wildcard.
  //
  //   RefractoryTier(n) ⊂ Refractory ⊂ RefractoryOrFire ⊂ AnyBricks
  //   ColouredBricks    ⊄ AnyBricks — see the clinker note on AnyBricks; the containment holds for
  //                                   seven of vanilla's eight brickcourse colours, not the eighth
  //
  // All of these are domainless or `game:`-prefixed on purpose: `@(…)` is a regex over the path
  // only, so a bare alternation is implicitly `game:` and cannot admit a modded brick. That is
  // wanted here - these name vanilla's own masonry - and is the opposite of the choice ChargeShaft
  // has to make. See CoalBed.

  /// <summary>Refractory brick pinned to one tier: <c>game:refractorybricks-good-tier{tier}</c>.
  /// For a shell whose heat leaves no cheaper tier viable - the hot blast furnace demands tier 3
  /// throughout.</summary>
  public static string RefractoryTier(int tier) =>
    $"game:refractorybricks-good-tier{tier}";

  /// <summary>
  /// Any refractory brick, any tier: <c>game:refractorybricks-good-tier*</c>. The default shell
  /// material for every furnace in the family, and what lets a core wear the tier it was built from.
  /// <para>
  /// <c>-good-</c> excludes vanilla's <c>damaged</c> state, which is not merely cosmetic: damaged
  /// refractory declares <c>sidesolid: false</c>, so a structure built from it would not hold.
  /// <c>tier*</c> is the whole of the <c>type</c> group (tier1/2/3), so this really is "any".
  /// </para>
  /// </summary>
  public const string Refractory = "game:refractorybricks-good-tier*";

  /// <summary>Fire brick: <c>game:claybricks-good-fire</c>. The boilers' setting - the cheap
  /// heat-resisting masonry that is not refractory.</summary>
  public const string FireBricks = "game:claybricks-good-fire";

  /// <summary>
  /// Any refractory brick <em>or</em> fire brick:
  /// <c>@(refractorybricks-good-tier.*|claybricks-good-fire)</c>. The rung for a wall that has to
  /// resist a working heat but not a metallurgical one - between them, exactly the two masonries
  /// vanilla marks <c>cokeOvenViable</c> - so the player may reach for whichever they have without
  /// the furnace insisting on the more expensive one.
  /// </summary>
  public const string RefractoryOrFire =
    "@(refractorybricks-good-tier.*|claybricks-good-fire)";

  /// <summary>
  /// Any coloured brick course, in any bond and any colour: <c>game:brickcourse-*</c>. Vanilla's
  /// <c>brickcourse</c> spans <c>{four|eight} × {header|soldier|running|…} × {black…tan|clinker}</c>,
  /// and this admits the lot - the rung for decorative masonry, where the block is structural but
  /// its appearance is the player's.
  /// </summary>
  public const string ColouredBricks = "game:brickcourse-*";

  /// <summary>
  /// Any brick at all - fire, clinker, refractory of any tier, or a coloured course. The loosest
  /// rung: a cell that must be masonry and is otherwise the player's to choose. Drawn by the smoke
  /// stack, and the natural code for a chimney or an outer shell.
  /// <para>
  /// <b>Clinker is included in both of the places vanilla puts it</b> - as the eighth
  /// <c>brickcourse</c> colour and as the standalone <c>claybricks-clinkerrough</c> block. Clinker
  /// brick is solid masonry like any other, so the whole colour group is admitted rather than an
  /// enumeration of colours; the <c>claybrickchimney</c> palette omits the clinker course and is
  /// not a safe list to copy.
  /// </para>
  /// <para>
  /// Still excludes <c>damaged</c> refractory, which is not <c>sidesolid</c> - see
  /// <see cref="Refractory"/>. "Any brick" means any brick a wall can be built from.
  /// </para>
  /// </summary>
  public const string AnyBricks =
    "@(claybricks-(good-fire|clinkerrough)|refractorybricks-good-.*|brickcourse-.*)";

  /// <summary>
  /// The refractory grate: <c>game:refractorybrickgrating-good-tier*</c>.
  /// <para>
  /// Vanilla's block is <c>refractorybrickgrating</c>. The intuitive shortening
  /// <c>refractorygrating</c> is not a block, resolves to nothing, and is the typo this whole table
  /// exists for - see the class remarks.
  /// </para>
  /// </summary>
  public const string RefractoryGrating =
    "game:refractorybrickgrating-good-tier*";

  #endregion

  #region Slabs

  // The same ladder one rung lower, for half-height courses. every slab code takes a facing,
  // because a slab is the archetypal oriented part: its whole purpose in these layouts is to open a
  // hearth mouth wide enough to reach through, which it only does laid the right way round.
  //
  // `-free` is vanilla's no-snow cover state, and its `-snow` sibling is a different block. A slab
  // cell with sky above it therefore stops satisfying the structure the first time it snows. Every
  // slab drawn today sits under a course, so this is latent - but an open-topped draft must either
  // roof its slabs or say `-*` for the cover, and there is no rung here that does the latter,
  // deliberately: it should be a visible choice.

  /// <summary>
  /// A free-standing fire-brick slab facing <paramref name="facing"/>:
  /// <c>game:brickslabs-fire-{facing}-free</c>. The shoulders round a hearth doorway -
  /// <c>BlockFacing.UP</c> for the flat course, a cardinal for the upright cheeks.
  /// <para>
  /// <b>The facing must reach the string, and this helper only saves you typing it.</b> The
  /// oriented-parts feature decides a cell is facing-checked by <em>reading the code</em>
  /// (<see cref="MultiblockLayoutBuilder.FindSideSegment"/>), so <c>FireSlab(BlockFacing.SOUTH)</c>
  /// must still emit <c>…-south-…</c>. Do not "clean this up" into a parameter that stops being
  /// interpolated: the cell would quietly go back to accepting a slab laid any way round - which is
  /// how a completed furnace could once still have visible gaps in its walls.
  /// <c>BlockFacing.UP</c> emits <c>up</c>, which is not a horizontal side word and so is correctly
  /// left unrotated.
  /// </para>
  /// </summary>
  public static string FireSlab(BlockFacing facing) =>
    $"game:brickslabs-fire-{facing.Code}-free";

  /// <summary>
  /// A free-standing slab of <em>any</em> brick, facing <paramref name="facing"/>:
  /// <c>game:brickslabs-*-{facing}-free</c>. The loose rung, matching
  /// <see cref="AnyBricks"/> - for a shoulder or a sill whose job is the shape, not the material.
  /// <para>
  /// The star spans a whole variant group on the fire slabs (<c>fire</c>) and two on the coloured
  /// ones (<c>four-red</c>), which is fine: vanilla expands <c>*</c> to a regex that crosses dashes.
  /// The facing still sits in the <b>last</b> side-word position either way, which is exactly why
  /// <see cref="MultiblockLayoutBuilder.FindSideSegment"/> scans from the end - so this stays
  /// orientation-checked and rotates correctly.
  /// </para>
  /// <para>
  /// <b>There is deliberately no coloured-slabs-only rung.</b> Vanilla files fire and coloured
  /// slabs under one block code distinguished only by their variant groups, so "coloured but not
  /// fire" cannot be said with a <c>*</c> at all - it would need a regex spliced into the middle of a
  /// path, a form nothing in the family uses or tests. If a layout ever needs it, that is a
  /// deliberate piece of work, not a constant.
  /// </para>
  /// </summary>
  public static string AnySlab(BlockFacing facing) =>
    $"game:brickslabs-*-{facing.Code}-free";

  #endregion

  #region Stairs

  // Stairs carry two orientation groups - which way the step faces, and whether it is inverted -
  // so their codes read `brickstairs-fire-{up|down}-{cardinal}-free`. Only the cardinal rotates with
  // the structure; `up`/`down` is invariant under a Y turn.
  //
  // This is the case FindSideSegment's "the last whole side segment wins" rule exists for. Scanning
  // from the front would find `up` and orientation-check the wrong group, which would then rotate to
  // nonsense and the cell could never be satisfied. Scanning from the end lands on the cardinal.

  /// <summary>
  /// A free-standing fire-brick stair: <c>game:brickstairs-fire-{half}-{facing}-free</c>.
  /// <paramref name="half"/> is <c>BlockFacing.UP</c> for an inverted stair or
  /// <c>BlockFacing.DOWN</c> for a normal one; <paramref name="facing"/> is the cardinal the step
  /// faces, and is the part that turns with the structure.
  /// </summary>
  public static string FireStairs(BlockFacing half, BlockFacing facing) =>
    $"game:brickstairs-fire-{Half(half)}-{Cardinal(facing)}-free";

  /// <summary>
  /// A free-standing stair of <em>any</em> brick:
  /// <c>game:brickstairs-*-{half}-{facing}-free</c>. The loose rung, matching
  /// <see cref="AnySlab"/> - the star spans one variant group on the fire stairs and two on the
  /// coloured ones, and the cardinal still sits last, so the cell stays orientation-checked.
  /// </summary>
  public static string AnyStairs(BlockFacing half, BlockFacing facing) =>
    $"game:brickstairs-*-{Half(half)}-{Cardinal(facing)}-free";

  /// <summary>
  /// Guards the vertical half of a stair code. A stair whose two orientation groups are passed the
  /// wrong way round still <em>compiles</em> and still produces a plausible-looking code - it just
  /// matches no block, and the structure then can never complete, with no error anywhere. Since that
  /// is the exact silent failure this whole table exists to prevent, the mistake fails the load
  /// instead.
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
  /// A coke-oven door facing <paramref name="facing"/>, in any state:
  /// <c>game:cokeovendoor-*-{facing}</c>. The firing door on every hearth and boiler in the family.
  /// <para>
  /// <b>The state stays wild and the side is pinned</b>, which is the only combination that works.
  /// Vanilla spells the code <c>cokeovendoor-{closed|opened}-{side}</c>, so a trailing <c>*</c>
  /// swallows the <em>side</em> along with the state, and a door hung any
  /// way round would satisfy the cell. Pinning the state instead would be worse: the structure would come
  /// apart the moment the player opened the door.
  /// </para>
  /// <para>
  /// The door carries its facing in a <c>side</c> variant and a <c>HorizontalOrientable</c>
  /// behaviour, so this really is checkable - unlike vanilla's trapdoor, which keeps both facing and
  /// open state in the <b>block entity</b> where no code match can see them.
  /// </para>
  /// <para>
  /// <b><paramref name="facing"/> is the opposite of the wall face the door closes</b>, and this
  /// is the reverse of our own doors' convention. Vanilla's
  /// <c>liquidBarrierOnSidesByType</c> gives <c>cokeovendoor-closed-north</c> a barrier at face index
  /// 2 - and <c>BlockFacing.ALLFACES</c> runs N, E, S, W - so a <c>north</c> door seals its
  /// <b>south</b> side. Meanwhile <c>iwex:furnace-puddlingchargedoor-south</c> sits in the south wall. Two
  /// doors, one wall, opposite spellings. Use <see cref="Sealing"/> rather than reasoning about it at
  /// each call site.
  /// </para>
  /// </summary>
  public static string CokeOvenDoor(BlockFacing facing) =>
    $"game:cokeovendoor-*-{facing.Code}";

  /// <summary>
  /// The coke-oven door that closes the <paramref name="wall"/> face of its cell - the way an author
  /// actually thinks about it ("the door in the south wall"). Converts to vanilla's inverted spelling
  /// via <see cref="CokeOvenDoor"/>, so the above is reasoned about once, here, instead of at five
  /// call sites.
  /// </summary>
  public static string Sealing(BlockFacing wall) => CokeOvenDoor(wall.Opposite);

  #endregion

  #region Fuel beds

  /// <summary>
  /// A plain fuel bed - vanilla coal pile or nothing: <c>@(air|coalpile)</c>.
  /// <para>
  /// The alternation is <b>domainless on purpose</b>: vanilla's <c>WildcardUtil</c> compares
  /// domain and path separately and <c>@(…)</c> is a regex over the path only, so this is implicitly
  /// <c>game:</c> and can never admit a mod's own charge block. That is correct for a firebox and
  /// wrong for a shaft - see <see cref="ChargeShaft"/>.
  /// </para>
  /// <para>
  /// Air is an accepted occupant, so a layout drawn with this <b>completes with no fuel cell
  /// built at all</b>. That is a known weakness of the vanilla-pile firebox and the reason the
  /// design replaces it with a required block; when that lands, every use of this constant moves
  /// with it, from here.
  /// </para>
  /// </summary>
  public const string CoalBed = "@(air|coalpile)";

  #endregion
}
