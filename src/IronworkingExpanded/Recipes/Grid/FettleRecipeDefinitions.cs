using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronworkingExpanded.Items;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Preparing <b>puddling fettle</b>: three parts iron oxide, three fettle out.
/// <para>
/// The three slots each take <b>any</b> <see cref="FettleItemDefinitions.StockTag"/> material - crushed iron
/// ore, tap cinder off the puddling hearth, or mill scale off the rolls - and that interchangeability is the
/// mechanic, not a convenience. A player's first batches are three parts bought ore, because that is all they
/// have. As the works starts running, each part gets displaced by oxide the machines gave back, until
/// fettling costs no ore at all. **A shop that is already working feeds itself; a new one pays in ore.**
/// </para>
/// <para>
/// Nothing discriminates between the three, and nothing needs to: vanilla already funnels magnetite,
/// hematite and limonite into one <c>crushed-iron</c>, and all three fettle materials are the same thing
/// chemically - iron oxide with the iron still locked up in it.
/// </para>
/// <para>
/// <b>Interim.</b> Real fettle was <em>roasted</em> oxide, which is what a reverberatory furnace is for (and
/// why "bull dog" - roasted tap cinder - was the standard British fettling). Once the heating furnace can
/// roast, roasting should become the better route, leaving this hand-prepared craft as the fallback.
/// </para>
/// </summary>
public class FettleRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [
      ExRecipeDef
        .Create(domain, "grid", "puddlingfettle")
        .Grid(r =>
          r.Name("Puddling Fettle")
            .Pattern("FFF")
            .Size(3, 1)
            .Ingredient(
              "F",
              i => i.Tagged("item", FettleItemDefinitions.StockTag).Quantity(1)
            )
            .OutputItem($"{domain}:{FettleItemDefinitions.Code}", 3)
        ),
    ];
}
