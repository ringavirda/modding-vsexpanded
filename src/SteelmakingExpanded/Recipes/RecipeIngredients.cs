using System;
using ExpandedLib.Definitions;

namespace SteelmakingExpanded.Recipes;

/// <summary>
/// smex-specific ingredient factories shared by more than one recipe provider. The vanilla staples
/// (plate/nails/rod/gear/hammer) come from <see cref="ExIngredients"/> via <c>using static</c>; anything
/// used by a single provider stays private to that file.
/// </summary>
internal static class RecipeIngredients {
  /// <summary>
  /// A plain pipe segment of any variant, matched with a trailing star and no dash. Used by the
  /// cowper, smoke-stack and converter gas intakes.
  /// </summary>
  internal static Func<IngredientBuilder, IngredientBuilder> PipeStar(
    int qty
  ) => i => i.Block("lpex:pipe-cast-straight*").Quantity(qty);

  /// <summary>
  /// Refractory brick of any tier, capturing the tier as <c>{tier}</c> so the crafted block resolves
  /// to the matching variant. Used by the cowper and smoke-stack intakes. The hot blast furnace does
  /// not use this: it takes tier 3 only.
  /// </summary>
  internal static Func<IngredientBuilder, IngredientBuilder> RefractoryTier(
    int qty
  ) =>
    i =>
      i.Item("game:refractorybrick-fired-*")
        .Named("tier", "tier1", "tier2", "tier3")
        .Quantity(qty);
}
