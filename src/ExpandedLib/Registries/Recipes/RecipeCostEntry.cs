using System.Collections.Generic;

namespace ExpandedLib.Registries.Recipes;

/// <summary>
/// One managed recipe in a mod's cost catalogue: its kind, the wildcard code that locates it, and one
/// self-contained <see cref="RecipeProfileCost"/> per cost profile (<c>normal</c>, <c>cheap</c>, ...).
/// The <c>normal</c> profile is auto-filled from the live recipe on first run (see
/// <see cref="ExRecipeCosts.EnsureNormalExtracted"/>); a mod ships only what an alternate profile
/// pins, and the rest is scale-filled. Every number is editable in the file afterwards.
/// </summary>
public class RecipeCostEntry {
  /// <summary>How to locate and edit the recipe: <c>"grid"</c> (a crafting recipe, matched by output
  /// code) or <c>"rcc"</c> (a right-click-construction block, matched by block code).</summary>
  public string Type { get; set; } = "grid";

  /// <summary>Wildcard code matched against the grid output / RCC block code (e.g.
  /// <c>"iiex:enginewatt-*"</c>). Kept separate from the catalogue key so the same block can have
  /// both a grid entry and an rcc entry under distinct keys.</summary>
  public string Match { get; set; } = "";

  /// <summary>Profile name (<c>"normal"</c>, <c>"cheap"</c>, ...) to everything that profile changes
  /// for this recipe. Each entry holds its own ingredient costs, RCC stage costs and output quantity,
  /// so switching profile is picking one entry here.</summary>
  public Dictionary<string, RecipeProfileCost> Profiles { get; set; } = new();
}
