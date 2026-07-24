using ExpandedLib.Definitions;

namespace IronworkingExpanded.Recipes;

/// <summary>
/// iwex-specific ingredient factories shared by more than one recipe provider. The vanilla staples
/// (plate/nails/rod/fire clay/gear/hammer/chisel) come from <see cref="ExIngredients"/> via
/// <c>using static</c>; anything used by a single provider stays private to that file. What lands here is
/// the narrow middle: captures two providers must agree on, where a drifting wildcard would silently give
/// two recipes different ingredient sets.
/// </summary>
internal static class RecipeIngredients
{
  /// <summary>
  /// The vanilla rock types a molten canal can be cut from. Shared because the diagram-crafted canals
  /// (<see cref="Grid.DiagramRecipeDefinitions"/>) must capture the same <c>{rock}</c> set as the
  /// hand-patterned ones (<see cref="Grid.MoltenRecipeDefinitions"/>) or the two paths would produce
  /// different variants from the same stone.
  /// </summary>
  internal static readonly string[] Rocks =
  [
    "andesite",
    "chalk",
    "chert",
    "conglomerate",
    "limestone",
    "claystone",
    "granite",
    "sandstone",
    "shale",
    "basalt",
    "peridotite",
    "phyllite",
    "slate",
    "obsidian",
    "kimberlite",
    "bauxite",
    "suevite",
    "whitemarble",
    "redmarble",
    "greenmarble",
  ];

  /// <summary>Cobblestone of any of the <see cref="Rocks"/>, capturing the rock as <c>{rock}</c>.</summary>
  internal static IngredientBuilder Cobble(IngredientBuilder i) =>
    i.Block("game:cobblestone-*").Named("rock", Rocks);
}
