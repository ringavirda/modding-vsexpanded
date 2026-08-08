using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronworkingExpanded.Items;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the <b>forming</b> shop - currently the rolling mill stand. It had no recipe of
/// any kind, so the mill was creative-only even though its machinery is complete.
/// <para>
/// The <b>roll sets</b> are deliberately not here: they are cast, not fabricated, so their recipes belong with
/// the casting patterns once their blank pattern lands. Chilled cast iron is what a roll is made of, and a roll
/// takes steady compression rather than shock - the same compression/tension rule that makes the mill's frame
/// castable and the high-pressure hammer's frame not.
/// </para>
/// <para>
/// <b>The mill is buildable before it is useful.</b> It cannot yet roll anything - no stock item resolves a
/// work piece and no product comes off the rolls (see <c>docs/design/iwex-bringup.md</c> Stage 4). Shipping the
/// recipe now is what lets the machinery be exercised outside creative; it is not a claim that the process
/// works.
/// </para>
/// </summary>
public class FormingRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) => [RollingMill(domain)];

  /// <summary>
  /// The mill stand: a heavy cast-iron housing, journals for the two rolls, and a gear to couple it to the
  /// line shaft. Costed in <b>heavy cast plate</b> because a rolling stand is the heaviest thing in the shop -
  /// the housings have to take the full separating force of every pass without flexing, which is precisely why
  /// they were cast rather than fabricated.
  /// <para>
  /// It is the tier's first real capital purchase, and the cost should read that way: it needs a cupola, a
  /// casting cell and a pattern before a single plate exists. That is the design's whole thesis - build
  /// complexity bought, operating efficiency returned.
  /// </para>
  /// </summary>
  private static ExRecipeDef RollingMill(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "rollingmill")
      .Grid(r =>
        r.Name("Rolling Mill")
          .Pattern("PRP,PGP,PHP")
          .Size(3, 3)
          .Ingredient("P", i => i.Item($"{domain}:castplate-heavy").Quantity(1))
          .Ingredient("R", Rod(2))
          .Ingredient("G", i => i.Item($"{domain}:{SpurGearItemDefinitions.Code}").Quantity(1))
          .Ingredient("H", Hammer)
          // `we` is the authored (unrotated) orientation and the creative default.
          .OutputBlock($"{domain}:forming-rollingmill-we", 1)
      );
}
