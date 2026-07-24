using System;
using ExpandedLib.Definitions;

namespace SteelmakingExpanded.Recipes;

/// <summary>
/// smex-specific ingredient factories shared by more than one recipe provider. The vanilla staples
/// (plate/nails/rod/gear/hammer) come from <see cref="ExIngredients"/> via <c>using static</c>; anything
/// used by a single provider stays private to that file.
/// </summary>
internal static class RecipeIngredients
{
  /// <summary>
  /// A plain pipe segment, using the trailing-star wildcard (no dash) carried over from the source JSON.
  /// Shared by the three blocks that are essentially a brick shell around a pipe - the cowper intake, the
  /// smoke-stack intake and the converter's gas intake - so all three keep asking for the same thing.
  /// </summary>
  internal static Func<IngredientBuilder, IngredientBuilder> PipeStar(int qty) =>
    i => i.Block("lpex:pipe-straight*").Quantity(qty);

  /// <summary>
  /// Refractory brick of any tier, capturing the tier as <c>{tier}</c> so the crafted block resolves to the
  /// matching variant. Used by the cowper and smoke-stack intakes; the hot blast furnace deliberately does
  /// <b>not</b> use this - it is tier-3 only.
  /// </summary>
  internal static Func<IngredientBuilder, IngredientBuilder> RefractoryTier(
    int qty
  ) =>
    i =>
      i.Item("game:refractorybrick-fired-*")
        .Named("tier", "tier1", "tier2", "tier3")
        .Quantity(qty);
}
