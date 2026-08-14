using ExpandedLib.Definitions;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronIndustryExpanded.Recipes;

/// <summary>
/// iiex-specific ingredient factories shared by more than one recipe provider - the captures two
/// providers must agree on, where a drifting wildcard would give two recipes different ingredient sets.
/// The vanilla staples (plate/nails/rod/fire clay/gear/hammer/chisel) come from
/// <see cref="ExIngredients"/> via <c>using static</c>; anything used by a single provider stays
/// private to that file.
/// </summary>
internal static class RecipeIngredients {
  /// <summary>
  /// The vanilla rock types a molten canal can be cut from. Shared so the diagram-crafted canals
  /// (<see cref="Grid.DiagramRecipeDefinitions"/>) capture the same <c>{rock}</c> set as the
  /// hand-patterned ones (<see cref="Grid.MoltenRecipeDefinitions"/>); otherwise the two paths yield
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

  /// <summary>
  /// The seven coloured fired-brick states a running-brick capture resolves to, matching the
  /// <c>{brick}</c> variant on the brick-tinted molten blocks (canal, casting cell). Fire brick is
  /// absent: it has its own uncaptured recipe.
  /// </summary>
  internal static readonly string[] Bricks =
  [
    "black",
    "brown",
    "cream",
    "gray",
    "orange",
    "red",
    "tan",
  ];

  /// <summary>A coloured running-bond brick course, capturing its colour as <c>{brick}</c>.</summary>
  internal static IngredientBuilder RunningBrick(IngredientBuilder i) =>
    i.Block("game:brickcourse-four-running-*")
      .Named("brick", Bricks)
      .Quantity(1);

  /// <summary>Plain fire bricks: the uncaptured <c>fire</c> half of every coloured/fire recipe pair.</summary>
  internal static IngredientBuilder FireBrick(IngredientBuilder i) =>
    i.Block("game:claybricks-good-fire").Quantity(1);

  /// <summary>
  /// The fire-clay + hammer + chisel trio the brick-tinted molten crafts share (canal, casting cell).
  /// The clay quantity is a parameter because it varies by recipe: 4 for the canal tap, otherwise 2.
  /// </summary>
  internal static GridRecipeBuilder Fhk(GridRecipeBuilder r, int clayQty) =>
    r.Ingredient("F", FireClay(clayQty))
      .Ingredient("H", Hammer)
      .Ingredient("K", Chisel);
}
