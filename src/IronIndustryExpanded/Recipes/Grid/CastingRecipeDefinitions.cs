using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronIndustryExpanded.BlockStructures.Casting;
using static ExpandedLib.Definitions.ExIngredients;
using static IronIndustryExpanded.Recipes.RecipeIngredients;

namespace IronIndustryExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for iiex's sand-casting stations; the parts they cast are authored with their
/// patterns. The cells are fired-brick shells hammered and chiselled over fire clay, the same brick-tinted
/// idiom as the molten canal, so they share the canal's <see cref="RecipeIngredients.Fhk"/> /
/// <see cref="RecipeIngredients.RunningBrick"/> / <see cref="RecipeIngredients.FireBrick"/> captures.
/// A cell's 8-state <c>brick</c> variant (fire plus seven colours) needs two recipes on one pattern: a
/// coloured route capturing the running-brick colour into <c>{brick}</c>, and a fire-brick route outputting
/// the <c>fire</c> default. The two cannot conflict - <c>brickcourse-four-running-*</c> and
/// <c>claybricks-good-fire</c> do not overlap.
/// </summary>
public class CastingRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      SandCastingCell(domain),
      SandCastingLongCell(domain),
      SandCastingBed(domain),
      GreenSand(domain),
    ];

  /// <summary>
  /// The 3×1×4 pig bed's base course, which turns molten pig iron into pigs. Right-click constructed, so
  /// this recipe covers only the first brick course; the two remaining courses (16 bricks each) and the
  /// sand fill are charged by the construction stages and carry the bulk of the cost. The output is
  /// <c>casting-sandbed-{brick}-n</c> with no <c>sand</c> variant group: green sand is the one moulding
  /// material, so the rock type that filled the bed is cosmetic and is not recorded.
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
          .OutputBlock("iiex:casting-sandbed-{brick}-n", 1)
      );

  /// <summary>
  /// Mixing green sand: eight sand around one blue clay. The clay is the binder that lets the mix hold an
  /// impression, at about 11 %, close to the real proportion. The sand ingredient is a <c>game:sand-*</c>
  /// wildcard with no capture: sand's rock type is cosmetic in vanilla, so every type mixes and the same
  /// prepared item comes out whatever went in.
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
          .OutputBlock("iiex:casting-sandcell-{brick}-n", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Sand Casting Cell (Fire Brick)")
              .Pattern("BHB,BFB,BKB")
              .Size(3, 3)
              .Ingredient("B", FireBrick),
            2
          )
          .OutputBlock("iiex:casting-sandcell-fire-n", 1)
      );

  /// <summary>
  /// The 1×2 long cell: the 1×1 cell's shell at double the length, so double the brick and double the
  /// core. The cost is doubled through quantity rather than a taller pattern because the vanilla crafting
  /// grid is 3×3 and a 3×4 pattern cannot be crafted at all - same shape as the 1×1 cell, 12 bricks and
  /// 4 fire clay against its 6 and 2. Two routes for the same reason as the 1×1 cell: the 8-state
  /// <c>brick</c> variant needs a coloured route capturing <c>{brick}</c> and a fire-brick route
  /// outputting the <c>fire</c> default.
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
          .OutputBlock("iiex:casting-sandlongcell-{brick}-n", 1)
      )
      .Grid(r =>
        Fhk(
            r.Name("Sand Casting Long Cell (Fire Brick)")
              .Pattern("BHB,BFB,BKB")
              .Size(3, 3)
              .Ingredient("B", i => FireBrick(i).Quantity(2)),
            4
          )
          .OutputBlock("iiex:casting-sandlongcell-fire-n", 1)
      );
}
