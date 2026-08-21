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
  /// The fasteners an ordinary structural joint accepts, in the order recipes are emitted for them. A
  /// merely-structural joint takes either - a nail and a rivet both hold plate together - so a shop that
  /// has built the riveter need not keep an anvil going for nails as well. A joint that must also be
  /// tight is a different rule and lives on the boiler, which takes rivets alone.
  /// See docs/design/items/fasteners.md and STATE.md.
  /// </summary>
  /// <remarks>
  /// A list rather than one ingredient because Vintage Story has no OR across item codes - not in a grid
  /// ingredient and not in an RCC <c>requireStacks</c> entry, which is an AND list. So substitution is one
  /// recipe per fastener, the way the two gear routes are already authored. Six sites carry it rather than
  /// every nail site: past that the duplication starts multiplying against the gear loop.
  /// </remarks>
  internal static string[] Fasteners(string domain) =>
    [
      "game:metalnailsandstrips-*",
      $"{domain}:{Items.FastenerItemDefinitions.RivetCode}",
    ];

  /// <summary>
  /// One fastener, by code. The metal capture rides along only where the code carries a variant to
  /// capture: nails have a metal axis and the rivet has none. No output at the six sites reads
  /// <c>{metal}</c>, so the two variants of a recipe differ in nothing but this ingredient - which is what
  /// keeps them from clashing on their shared pattern.
  /// </summary>
  internal static System.Func<IngredientBuilder, IngredientBuilder> Fastener(
    string code,
    int quantity
  ) =>
    code.Contains('*')
      ? i => i.Item(code).Metal().Quantity(quantity)
      : i => i.Item(code).Quantity(quantity);

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
