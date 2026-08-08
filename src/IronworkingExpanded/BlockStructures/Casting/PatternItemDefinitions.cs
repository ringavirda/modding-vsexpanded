using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using IronworkingExpanded.Items;

namespace IronworkingExpanded.BlockStructures.Casting;

/// <summary>
/// The iwex mold patterns: the wooden positives a sand mold is rammed around. Each is a variant of one
/// <c>pattern</c> itemtype carrying its <see cref="MoldSpec"/> in a per-variant <c>mold</c> attribute (via
/// <c>attributesByType</c>), so the casting cell reads what to cast off the held pattern. A pattern is
/// crafted from its diagram, a knife and a log, and wears with use. lpex ships its own <c>pattern</c>
/// itemtype off <see cref="Itemtype"/> for its own parts. Type names follow the drawn art and are plural
/// where the impression yields more than one piece while the item stays singular; the lang keys and the
/// <c>diag-{type}</c> textures key off the type name, so a rename here is a rename in three places.
/// </summary>
public class PatternItemDefinitions : IExItemDefProvider {
  /// <summary>
  /// One mold spec, as the nested attribute a pattern variant carries. Kept in C# so the cavity numbers sit
  /// next to the shapes they were measured from. Public because iwex owns the pattern system - this helper,
  /// <see cref="Itemtype"/>, <see cref="PatternWoods"/> and <see cref="MoldSpec"/> - while each mod
  /// contributes its own entries.
  /// </summary>
  public static object Mold(
    string shape,
    int capacity,
    object[] cavity,
    string outputCode,
    string outputType = "item",
    float minPourTemp = 1150f,
    string size = "cell"
  ) =>
    new {
      mold = new {
        size,
        shape,
        capacity,
        cavity,
        output = new { type = outputType, code = outputCode },
        minPourTemp,
      },
    };

  /// <summary>
  /// A cavity box, in voxels. Float rather than int because the cavity is a <c>Cuboidf</c> and the long
  /// cell's lanes land on half voxels: three 3-wide billet lanes plus two ribs do not partition 16 evenly.
  /// </summary>
  public static object Box(
    float x1,
    float y1,
    float z1,
    float x2,
    float y2,
    float z2
  ) =>
    new {
      x1,
      y1,
      z1,
      x2,
      y2,
      z2,
    };

  /// <summary>
  /// The pattern's own item shape per type. A pattern reuses the cast item's shape with the texture swapped
  /// to plain wood, so these point at the cast items' shapes rather than at bespoke pattern art. Keyed by
  /// the same type as <see cref="Molds"/>; a type with no entry falls back to the generic plate.
  /// </summary>
  private static readonly Dictionary<string, string> PatternShapes = new() {
    ["castheavyplate"] = "iwex:item/heavyplate",
    ["castingotmold"] = "iwex:item/ingotmold",
    ["castbarrel"] = "iwex:item/cast-barrel",
    ["castwheelsection"] = "iwex:item/castwheelsection",
    ["castshell"] = "iwex:item/castshell",
    // The long-cell stock. Plural type names, singular shapes: the impression yields three billets, the
    // item is one billet.
    ["castbillets"] = "iwex:item/castbillet",
    ["castblooms"] = "iwex:item/castbloom",
    ["castslab"] = "iwex:item/castslab",
  };

  // Each entry: the pattern's type variant -> its mold spec. Adding a castable part is one line here plus
  // the output item and the filling shape.
  private static readonly Dictionary<string, object> Molds = new() {
    ["castheavyplate"] = Mold(
      "iwex:casting/cell-filling-heavyplate",
      CastPartItemDefinitions.HeavyPlateUnits,
      [Box(7, 4, 4, 9, 14, 12)],
      "iwex:castplate-heavy"
    ),
    // The cast output is the placeable mold block itself, so `capacity` here is the metal needed to make
    // the mold, not what it later holds.
    ["castingotmold"] = Mold(
      "iwex:casting/cell-filling-ingotmold",
      152,
      [Box(3, 12, 3, 13, 14, 13)],
      "iwex:casting-mold-ingot",
      outputType: "block"
    ),
    // The cored cast-barrel blank (unlined); lined with fire clay it becomes the molten-barrel-cast block,
    // and it is the biggest single-cell cast. The cavity box is the vessel-wall region the molten iron
    // fills, used for the cell's fill glow.
    ["castbarrel"] = Mold(
      "iwex:casting/cell-filling-moltenbarrel",
      CastPartItemDefinitions.CastBarrelUnits,
      [Box(4, 4, 4, 12, 12, 12)],
      "iwex:cast-barrel"
    ),
    // The cast shell: iwex owns it because the ladle is built from it, and lpex consumes it for the water
    // tank, ore crusher and engines. The cavity box is the walled void of the drawn rammed sand, a shallow
    // tray - the sand bed tops out at y 10 and the walls at y 14. Illustrative only; `capacity` alone
    // carries the mass.
    ["castshell"] = Mold(
      "iwex:casting/cell-filling-castshell",
      CastPartItemDefinitions.CastShellUnits,
      [Box(4, 4, 4, 12, 14, 12)],
      "iwex:castshell"
    ),
    // The rim segment, cast at 600 u to match its rolled/riveted equivalent - see
    // CastPartItemDefinitions.CastWheelSectionUnits.
    ["castwheelsection"] = Mold(
      // The drawn filling asset is still named `flywheelpart`; the pattern type uses the current name.
      "iwex:casting/cell-filling-flywheelpart",
      CastPartItemDefinitions.CastWheelSectionUnits,
      [Box(4, 10, 4, 12, 14, 12)],
      "iwex:castwheelsection"
    ),
    // The long-cell stock ladder: one cell, three fillings, three products - three narrow lanes give
    // billets, two give blooms, one a slab, so swapping the pattern changes what the station makes.
    //
    // Capacity is the whole impression, not one lane: a 3-lane billet pattern holds 3 x 600 u and yields
    // 3 billets, so a partial pour fills lanes progressively and a short pour is scrap.
    //
    // The cavity boxes are principal-local and span negative z, because the long cell's body extends -Z
    // from the principal and a lane runs either side of the origin. They are illustrative: they drive the
    // rammed-sand mesh and the molten fill glow while `capacity` alone carries the mass, so a lane whose
    // drawn volume does not multiply out to its capacity is not a defect. The one hard dimension is length:
    // a lane cannot exceed 24 voxels, since the station's interior runs z -14..14 with 2-thick end dams.
    // See docs/design/machines/long-cell.md.
    ["castbillets"] = Mold(
      "iwex:casting/longcell-filling-billets",
      3 * CastStockItemDefinitions.BilletUnits,
      // 3 lanes, 2.5 / 3 / 2.5 wide (unequal in the shipped art), ribs at x 5.5-6.5 and x 9.5-10.5.
      [
        Box(3, 11, -12, 5.5f, 14, 12),
        Box(6.5f, 11, -12, 9.5f, 14, 12),
        Box(10.5f, 11, -12, 13, 14, 12),
      ],
      "iwex:caststock-billet",
      size: LongCell
    ),
    ["castblooms"] = Mold(
      "iwex:casting/longcell-filling-blooms",
      2 * CastStockItemDefinitions.BloomUnits,
      // 2 lanes, 3 wide x 4 deep x 24 long, rib at x 7-9 and 2-thick outer walls.
      [Box(4, 10, -12, 7, 14, 12), Box(9, 10, -12, 12, 14, 12)],
      "iwex:caststock-bloom",
      size: LongCell
    ),
    ["castslab"] = Mold(
      "iwex:casting/longcell-filling-castslab",
      CastStockItemDefinitions.SlabUnits,
      // 1 lane, 8 wide x 4 deep x 24 long - the full width between the 2-thick walls.
      [Box(4, 10, -12, 12, 14, 12)],
      "iwex:caststock-slab",
      size: LongCell
    ),
  };

  /// <summary>The <c>size</c> token a long-cell pattern declares. The casting cell refuses any
  /// non-<c>cell</c> spec and the long cell any non-<c>longcell</c> one.</summary>
  private const string LongCell = "longcell";

  /// <summary>
  /// The pattern types belonging to the long cell rather than the 1x1 casting cell, the cast stock ladder.
  /// Derived from <see cref="CastStockItemDefinitions.Forms"/> so the two cannot drift.
  /// </summary>
  public static readonly string[] LongCellPatternTypes =
  [
    .. CastStockItemDefinitions.Forms.Select(f => f.Pattern),
  ];

  /// <summary>Impressions a wooden pattern survives before it wears out, as item durability. A worn pattern
  /// is re-carved from its diagram.</summary>
  public const int WoodenPatternDurability = 24;

  /// <summary>The pattern <c>type</c> variants, in emit order. The pattern diagrams
  /// (<see cref="Items.DiagramItemDefinitions"/>) and the diagram-to-pattern recipes
  /// (<see cref="Recipes.Grid.PatternRecipeDefinitions"/>) both derive from this, so one
  /// <see cref="Molds"/> entry carries its diagram and its craft.</summary>
  public static readonly string[] PatternTypes = [.. Molds.Keys];

  /// <summary>The wood a pattern is carved from: a cosmetic variant taken from the plank used in the craft.
  /// The vanilla <c>block/wood</c> set, shared with
  /// <see cref="Recipes.Grid.PatternRecipeDefinitions"/> so the recipe capture and the item variants stay
  /// aligned. The mold spec is per type, not per wood, so every wood of a type casts the same part.</summary>
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
    [Itemtype(domain, Molds, PatternShapes)];

  /// <summary>
  /// Builds a mod's whole <c>pattern</c> itemtype from its own mold table, so another mod can own its cast
  /// parts without either mod naming the other's codes; the casting cell reads the spec off whatever
  /// pattern is held. <paramref name="shapes"/> may omit a type, which then falls back to
  /// <c>game:item/plate</c> - a mistyped key looks the same as missing art.
  /// </summary>
  public static ExItemDef Itemtype(
    string domain,
    IReadOnlyDictionary<string, object> molds,
    IReadOnlyDictionary<string, string> shapes
  ) {
    // attributesByType maps "*-{type}-*" -> { mold: {...} }, the trailing -* absorbing the wood variant,
    // and resolves per variant into each pattern's own Attributes where the cell reads it. The spec is
    // wood-agnostic, so all woods of a type share one.
    var byType = new Dictionary<string, object>();
    foreach ((string type, object mold) in molds)
      byType["*-" + type + "-*"] = mold;

    // shapeByType mirrors it: the pattern wears the shape of the part it casts (see PatternShapes), with
    // the trailing `-*` absorbing the wood variant as the mold wildcard does.
    var shapeByType = new Dictionary<string, object>();
    foreach ((string type, string shape) in shapes)
      shapeByType["*-" + type + "-*"] = new { @base = shape };

    return ExItemDef
      .Create(domain, "pattern")
      // `shapeByType` picks the cast part's shape and the `all` texture wildcard overrides whatever that
      // shape declares (cast iron) with the plank's debarked wood, giving a wooden positive of the part.
      // `Shape` is the fallback for a type with no dedicated art.
      .Shape("game:item/plate")
      .Raw("shapeByType", shapeByType)
      .TextureAll("game:block/wood/debarked/{wood}")
      // type first, wood last: the mold-spec wildcard (*-{type}-*) and the lang key (item-pattern-{type}-*)
      // both key off the type with the wood as a trailing variant.
      .VariantGroup("type", [.. molds.Keys])
      .VariantGroup("wood", PatternWoods)
      .MaxStackSize(1)
      // Durable tool: each ram-up (Imprint) damages it.
      .Raw("durability", WoodenPatternDurability)
      .Raw("attributesByType", byType)
      .CreativeCommon("*");
  }
}
