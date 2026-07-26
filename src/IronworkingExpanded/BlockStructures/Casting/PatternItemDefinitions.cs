using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronworkingExpanded.Items;

namespace IronworkingExpanded.BlockStructures.Casting;

/// <summary>
/// The iwex mold <b>patterns</b> - the wooden positives a sand mold is rammed around. Each is a variant of
/// one <c>pattern</c> itemtype, carrying its <see cref="MoldSpec"/> in a per-variant <c>mold</c> attribute
/// (via <c>attributesByType</c>), so the casting cell reads what to cast off the held pattern without any
/// code coupling. A pattern is crafted from its diagram + a knife + a log (a durable tool, worn by use).
/// <para>
/// iwex owns the patterns for the parts it casts: the iron molds that replace the ceramic tool molds, the
/// heavy cast plate, and the cast barrel. lpex adds its own <c>pattern</c> itemtype for the machine parts
/// (axle, gear blanks, cylinder, frame) the same way.
/// </para>
/// </summary>
public class PatternItemDefinitions : IExItemDefProvider
{
  // One mold spec, as the nested attribute a pattern variant carries. Kept in C# so the cavity numbers
  // sit next to the shapes they were measured from.
  private static object Mold(
    string shape,
    int capacity,
    object[] cavity,
    string outputCode,
    string outputType = "item",
    float minPourTemp = 1150f,
    string size = "cell"
  ) =>
    new
    {
      mold = new
      {
        size,
        shape,
        capacity,
        cavity,
        output = new { type = outputType, code = outputCode },
        minPourTemp,
      },
    };

  private static object Box(int x1, int y1, int z1, int x2, int y2, int z2) =>
    new
    {
      x1,
      y1,
      z1,
      x2,
      y2,
      z2,
    };

  // Each entry: the pattern's type variant -> its mold spec. Adding a castable part is one line here plus
  // the output item and the filling shape.
  private static readonly Dictionary<string, object> Molds = new()
  {
    ["heavyplate"] = Mold(
      "iwex:casting/cell-filling-heavyplate",
      CastPartItemDefinitions.HeavyPlateUnits,
      [Box(7, 4, 4, 9, 14, 12)],
      "iwex:castplate-heavy"
    ),
    // The iron molds themselves are cast in the cell (they replace the ceramic tool molds); the cast is
    // the placeable mold block.
    ["moldplate"] = Mold(
      "iwex:casting/cell-filling-plate",
      136,
      [Box(3, 12, 3, 13, 14, 13)],
      "iwex:castmold-plate",
      outputType: "block"
    ),
    ["molddoubleingot"] = Mold(
      "iwex:casting/cell-filling-doubleingot",
      152,
      [Box(3, 12, 3, 13, 14, 13)],
      "iwex:castmold-doubleingot",
      outputType: "block"
    ),
    // The cored cast-barrel blank (unlined); lined with fire clay it becomes the moltenbarrel-cast block.
    // A high-volume consumable - the biggest single-cell cast. Cavity box = the vessel-wall region the
    // molten iron fills, for the cell's fill glow.
    ["castbarrel"] = Mold(
      "iwex:casting/cell-filling-moltenbarrel",
      CastPartItemDefinitions.CastBarrelUnits,
      [Box(4, 4, 4, 12, 12, 12)],
      "iwex:cast-barrel"
    ),
  };

  /// <summary>Impressions a wooden pattern survives before it wears out (durability). A modest wooden-tool
  /// figure - the cell casts capital goods a handful at a time, and a worn pattern is re-carved from its
  /// diagram. A metal-pattern tier for long runs is a later addition (see the design's Quality section).</summary>
  public const int WoodenPatternDurability = 24;

  /// <summary>The pattern <c>type</c> variants, in emit order - the single source the pattern diagrams
  /// (<see cref="Items.DiagramItemDefinitions"/>) and the diagram→pattern recipes
  /// (<see cref="Recipes.Grid.PatternRecipeDefinitions"/>) both derive from, so adding a castable part
  /// (one <see cref="Molds"/> entry) carries its diagram and its craft automatically.</summary>
  public static readonly string[] PatternTypes = [.. Molds.Keys];

  /// <summary>The wood the pattern is carved from - a cosmetic variant taken from the plank used in the
  /// craft (see the recipe). The vanilla <c>block/wood</c> set; shared with
  /// <see cref="Recipes.Grid.PatternRecipeDefinitions"/> so the recipe capture and the item variants stay
  /// aligned. The mold spec is per <em>type</em>, not per wood, so every wood of a type casts the same part.</summary>
  public static readonly string[] PatternWoods =
  [
    "birch",
    "oak",
    "maple",
    "pine",
    "acacia",
    "kapok",
    "baldcypress",
    "larch",
    "redwood",
    "ebony",
    "walnut",
    "purpleheart",
  ];

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Pattern(domain)];

  private static ExItemDef Pattern(string domain)
  {
    // attributesByType maps "*-{type}-*" -> { mold: {...} } (the trailing -* absorbs the wood variant),
    // resolved per variant into each pattern's own Attributes, where the cell reads it. The mold spec is
    // wood-agnostic, so all woods of a type share one spec.
    var byType = new Dictionary<string, object>();
    foreach ((string type, object mold) in Molds)
      byType["*-" + type + "-*"] = mold;

    return ExItemDef
      .Create(domain, "pattern")
      // Art rule (maintainer): a pattern reuses the CAST ITEM'S shape with the texture swapped to plain
      // wood - it reads as a wooden positive of the part. Wired per type via shapeByType when the full
      // pattern set lands; the placeholder uses the plate shape. The wood variant textures the positive
      // with the plank's debarked wood.
      .Shape("game:item/plate")
      .TextureAll("game:block/wood/debarked/{wood}")
      // type first, wood last: the mold-spec wildcard (*-{type}-*) and the lang key (item-pattern-{type}-*)
      // both key off the type with the wood as a trailing variant.
      .VariantGroup("type", PatternTypes)
      .VariantGroup("wood", PatternWoods)
      .MaxStackSize(1)
      // A durable tool: each ram-up (Imprint) damages it, so a wooden pattern wears out over its runs.
      .Raw("durability", WoodenPatternDurability)
      .Raw("attributesByType", byType)
      .CreativeCommon("*");
  }
}
