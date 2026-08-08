using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronworkingExpanded.BlockStructures.Casting;
using static ExpandedLib.Definitions.ExIngredients;
using static IronworkingExpanded.Recipes.RecipeIngredients;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for iwex's sand-casting <b>stations</b> (as opposed to the parts they cast, whose
/// recipes live with the patterns). The 1×1 casting cell is a fired-brick shell hammered and chiselled over
/// fire clay - the same brick-tinted idiom as the molten canal, so it shares the canal's
/// <see cref="RecipeIngredients.Fhk"/> / <see cref="RecipeIngredients.RunningBrick"/> /
/// <see cref="RecipeIngredients.FireBrick"/> captures.
/// <para>
/// The cell carries an 8-state <c>brick</c> variant (fire + seven colours), so it takes two recipes sharing
/// one pattern: a coloured route that captures the running-brick colour into <c>{brick}</c>, and a fire-brick
/// route that outputs the <c>fire</c> default. The two never conflict - the coloured brick course
/// (<c>brickcourse-four-running-*</c>) and the fire bricks (<c>claybricks-good-fire</c>) do not overlap -
/// exactly as the canal's coloured/fire pairs.
/// </para>
/// </summary>
public class CastingRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      SandCastingCell(domain),
      SandCastingLongCell(domain),
      SandCastingBed(domain),
      GreenSand(domain),
    ];

  /// <summary>
  /// The 3×1×4 pig-bed's base course. The recipe is essential: the bed is what turns molten pig iron
  /// into pigs, so without it the blast furnace has nowhere to pour in survival.
  /// <para>
  /// Right-click constructed, so this is only the first brick course you place. The two remaining courses
  /// (16 bricks each) and the sand fill are charged by its stages, which is where the bulk of the cost lives.
  /// </para>
  /// <para>
  /// <b>There is no <c>sand</c> variant group.</b> Recording the rock type of whatever
  /// sand filled the bed, over vanilla's 20-state <c>block/rock</c> property, would be twenty times the
  /// block codes for a purely cosmetic fact. Green sand is the one moulding material, so there is
  /// nothing to record and the output is <c>casting-sandbed-{brick}-n</c>.
  /// </para>
  /// </summary>
  private static ExRecipeDef SandCastingBed(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "sandcastingbed")
      .Grid(r =>
        r.Name("Sand Casting Bed")
          .Pattern("BBB,_H_")
          .Size(3, 2)
          .Ingredient(
            "B",
            i =>
              i.Item("game:burnedbrick-*")
                .Named("brick", RecipeIngredients.Bricks)
                .Quantity(4)
          )
          .Ingredient("H", Hammer)
          .OutputBlock("iwex:casting-sandbed-{brick}-n", 1)
      );

  /// <summary>
  /// Mixing <b>green sand</b>: eight sand around one blue clay. The clay is the binder that makes the mix
  /// hold an impression at all (~11 % here, which is about the real proportion), and it is the only reason
  /// this is a recipe rather than the cell simply taking raw sand.
  /// <para>
  /// The sand ingredient is a deliberate <c>game:sand-*</c> wildcard with <b>no capture</b>: every rock type
  /// mixes, and none of them survives into the output. That is the whole point - the rock type of sand is
  /// cosmetic in vanilla, so gating on it would be a rule a player could only look up. One prepared item
  /// comes out regardless of what went in.
  /// </para>
  /// </summary>
  private static ExRecipeDef GreenSand(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "greensand")
      .Grid(r =>
        r.Name("Green Sand")
          .Pattern("SSS,SCS,SSS")
          .Size(3, 3)
          .Ingredient("S", i => i.Block("game:sand-*").Quantity(1))
          .Ingredient("C", i => i.Item("game:clay-blue").Quantity(1))
          .OutputItem($"{domain}:{GreenSandItemDefinitions.Code}", 8)
      );

  private static ExRecipeDef SandCastingCell(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "sandcastingcell")
      // Six bricks walled around a fire-clay core, hammered and chiselled into the launder shell.
      .Grid(r =>
        Fhk(
            r.Name("Sand Casting Cell (Colored Brick)")
              .Pattern("BHB,BFB,BKB")
              .Size(3, 3)
              .Ingredient("B", RunningBrick),
            2
          )
          .OutputBlock("iwex:casting-sandcell-{brick}-n", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Sand Casting Cell (Fire Brick)")
              .Pattern("BHB,BFB,BKB")
              .Size(3, 3)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iwex:casting-sandcell-fire-n", 1)
      );

  /// <summary>
  /// The 1×2 long cell: the 1×1 cell's shell at double the length, so double the brick and double the core.
  /// It ships with its recipe rather than after it - a station with no recipe is not a partial
  /// feature, it is an absent one.
  /// <para>
  /// <b>The cost is doubled through quantity, not through a taller pattern.</b> The vanilla crafting
  /// grid is 3×3, so the 3×4 grid this obviously wants cannot be crafted at all - it would have passed
  /// every test in the suite and been uncraftable in the world. Same shape as the 1×1 cell, twice the
  /// brick per slot and twice the clay: 12 bricks and 4 fire clay against the cell's 6 and 2.
  /// </para>
  /// <para>
  /// Same two-route split as the 1×1 cell for the same reason: an 8-state <c>brick</c> variant needs a
  /// coloured route that captures <c>{brick}</c> and a fire-brick route that outputs the <c>fire</c>
  /// default. The two cannot conflict - <c>brickcourse-four-running-*</c> and <c>claybricks-good-fire</c>
  /// do not overlap.
  /// </para>
  /// </summary>
  private static ExRecipeDef SandCastingLongCell(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "sandcastinglongcell")
      .Grid(r =>
        Fhk(
            r.Name("Sand Casting Long Cell (Colored Brick)")
              .Pattern("BHB,BFB,BKB")
              .Size(3, 3)
              .Ingredient("B", i => RunningBrick(i).Quantity(2)),
            4
          )
          .OutputBlock("iwex:casting-sandlongcell-{brick}-n", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Sand Casting Long Cell (Fire Brick)")
              .Pattern("BHB,BFB,BKB")
              .Size(3, 3)
              .Ingredient("B", i => FireBrick(i).Quantity(2)),
            4
          )
          .OutputBlock("iwex:casting-sandlongcell-fire-n", 1)
      );
}
