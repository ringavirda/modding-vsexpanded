using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using IronworkingExpanded.Items;

namespace IronworkingExpanded.BlockStructures.Casting;

/// <summary>
/// The iwex mold <b>patterns</b> - the wooden positives a sand mold is rammed around. Each is a variant of
/// one <c>pattern</c> itemtype, carrying its <see cref="MoldSpec"/> in a per-variant <c>mold</c> attribute
/// (via <c>attributesByType</c>), so the casting cell reads what to cast off the held pattern without any
/// code coupling. A pattern is crafted from its diagram + a knife + a log (a durable tool, worn by use).
/// <para>
/// iwex owns the patterns for the parts it casts: the iron ingot mold, the heavy cast plate, the cast
/// barrel, the wheel section and the long cell's stock ladder. lpex ships its own <c>pattern</c> itemtype
/// off <see cref="Itemtype"/> for its parts (the cast shell today; axle, gear blanks and cylinder later).
/// </para>
/// <para>
/// <b>Type names follow the drawn art</b>: <c>castheavyplate</c>,
/// <c>castingotmold</c>, <c>castbillets</c>, <c>castblooms</c>, <c>castslab</c>. <b>Plural where the
/// impression yields more than one piece</b> - three billets from one pour, two blooms, one slab - while
/// the items stay singular. The lang keys and the <c>diag-{type}</c> textures key off these, so a rename
/// here is a rename in three places.
/// </para>
/// </summary>
public class PatternItemDefinitions : IExItemDefProvider
{
  /// <summary>
  /// One mold spec, as the nested attribute a pattern variant carries. Kept in C# so the cavity numbers sit
  /// next to the shapes they were measured from.
  /// <para>
  /// <b>Public because other mods author molds too.</b> iwex owns the pattern <em>system</em> - this
  /// helper, <see cref="Itemtype"/>, <see cref="PatternWoods"/> and <see cref="MoldSpec"/> - while each mod
  /// owns its own <em>entries</em>. lpex's cast shell is the first outside caller; the alternative was iwex
  /// naming an <c>lpex:</c> output code, which is exactly the coupling the spec-on-the-pattern design exists
  /// to avoid.
  /// </para>
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

  /// <summary>
  /// A cavity box. float, not int: the cavity is a <c>Cuboidf</c> and the long cell's lanes genuinely land
  /// on half voxels - three 3-wide billet lanes plus two ribs do not partition 16 into whole numbers. A
  /// single-cell pattern still passes whole numbers and reads identically.
  /// </summary>
  public static object Box(
    float x1,
    float y1,
    float z1,
    float x2,
    float y2,
    float z2
  ) =>
    new
    {
      x1,
      y1,
      z1,
      x2,
      y2,
      z2,
    };

  /// <summary>
  /// The pattern's own item shape per type. Art rule: a pattern is the <b>cast item's shape</b> with the
  /// texture swapped to plain wood, so it reads as a wooden positive of the part it makes - which is why these
  /// point at the same shapes the cast items use, not at bespoke pattern art. Keyed by the same type as
  /// <see cref="Molds"/>; a type with no entry falls back to the generic plate.
  /// </summary>
  private static readonly Dictionary<string, string> PatternShapes = new()
  {
    ["castheavyplate"] = "iwex:item/heavyplate",
    ["castingotmold"] = "iwex:item/ingotmold",
    ["castbarrel"] = "iwex:item/cast-barrel",
    ["castwheelsection"] = "iwex:item/castwheelsection",
    ["castshell"] = "iwex:item/castshell",
    // The long-cell stock. Same art rule: the pattern wears the cast item's shape in plain wood.
    // Plural type names, singular shapes: the impression yields three billets, the item is one billet.
    ["castbillets"] = "iwex:item/castbillet",
    ["castblooms"] = "iwex:item/castbloom",
    ["castslab"] = "iwex:item/castslab",
  };

  // Each entry: the pattern's type variant -> its mold spec. Adding a castable part is one line here plus
  // the output item and the filling shape.
  private static readonly Dictionary<string, object> Molds = new()
  {
    ["castheavyplate"] = Mold(
      "iwex:casting/cell-filling-heavyplate",
      CastPartItemDefinitions.HeavyPlateUnits,
      [Box(7, 4, 4, 9, 14, 12)],
      "iwex:castplate-heavy"
    ),
    // The one iron mold. The cast is the placeable mold block itself, so
    // `capacity` here is the metal to make the mold, not what it later holds.
    //
    // There is deliberately no plate mold: `game:metalplate-*` is a rolled product, so casting plates in
    // a tray would be a second route to something the mill already makes. The ingot mold stays because
    // crucible steel has to be poured into something.
    ["castingotmold"] = Mold(
      "iwex:casting/cell-filling-ingotmold",
      152,
      [Box(3, 12, 3, 13, 14, 13)],
      "iwex:casting-mold-ingot",
      outputType: "block"
    ),
    // The cored cast-barrel blank (unlined); lined with fire clay it becomes the molten-barrel-cast block.
    // A high-volume consumable - the biggest single-cell cast. Cavity box = the vessel-wall region the
    // molten iron fills, for the cell's fill glow.
    ["castbarrel"] = Mold(
      "iwex:casting/cell-filling-moltenbarrel",
      CastPartItemDefinitions.CastBarrelUnits,
      [Box(4, 4, 4, 12, 12, 12)],
      "iwex:cast-barrel"
    ),
    // ── The rim segment ───────────────────────────────────────────────────────────────────────────────
    // A cell cast at 600 u, with a rolled/riveted equivalent at the same 600 u, and that equality is the
    // point - see CastPartItemDefinitions.CastWheelSectionUnits for why the number is 600.
    //
    // The cavity box is the walled void of the drawn rammed sand, measured off the filling shape: a
    // shallow tray, since the sand bed tops out at y 10 and the walls at y 14. Illustrative, exactly as the
    // long cell's are - `capacity` alone is mass.
    //
    // Its sibling `castshell` is iwex's because the ladle is built from it and the ladle is iwex's - the
    // static canal merger that gates every alloy in the mod. The shape is `iwex owns it, lpex consumes
    // it` (water tank, ore crusher, engines), which is the normal dependency direction and needs no
    // cross-mod pattern indirection at all: the pattern and the part are in one mod.
    ["castshell"] = Mold(
      "iwex:casting/cell-filling-castshell",
      CastPartItemDefinitions.CastShellUnits,
      [Box(4, 4, 4, 12, 14, 12)],
      "iwex:castshell"
    ),
    ["castwheelsection"] = Mold(
      // The drawn filling is still named `flywheelpart` - the shape predates the current item name and
      // renaming the asset is a separate, art-side change. The pattern type is the current name.
      "iwex:casting/cell-filling-flywheelpart",
      CastPartItemDefinitions.CastWheelSectionUnits,
      [Box(4, 10, 4, 12, 14, 12)],
      "iwex:castwheelsection"
    ),
    // ── The long-cell stock ladder ────────────────────────────────────────────────────────────────────
    // One cell, three fillings, three products: three narrow lanes give billets, two give blooms, one
    // gives a slab. The lane count is the ladder - the whole point of the long cell is that swapping a
    // pattern changes what the same station makes, with no re-plumbing.
    //
    // Capacity here is the whole impression, not one lane: a 3-lane billet pattern holds 3 x 600 u and
    // yields 3 billets. A partial pour therefore fills lanes progressively and a short pour is scrap, the
    // same as any other under-filled cast.
    //
    // The cavity boxes are principal-local and span negative z: the long cell's body extends -Z from
    // the principal, so a lane runs either side of the origin. See long-cell.md § Structure.
    //
    // The cavity boxes are illustrative, deliberately. They drive the rammed-sand mesh and the molten
    // fill glow; the mass is `capacity` alone. A lane whose drawn volume does not multiply out to its
    // capacity is not a bug - the sand art is free to be adjusted for how it reads, with no mass
    // consequence, which is exactly why the two are separate fields. The boxes below are the shipped art
    // as measured.
    //
    // The one dimension that is not free: a lane cannot exceed 24 voxels in length. The station's
    // interior runs z -14..14 with 2-thick end dams. Cheaper to know here than to discover in Blockbench.
    //
    // The drawn billet lanes are 2.5 / 3 / 2.5 wide. That one is worth fixing whenever the fillings are
    // next touched: unequal lanes read as three different billets coming out of one pour, and they are
    // the same item.
    ["castbillets"] = Mold(
      "iwex:casting/longcell-filling-billets",
      3 * CastStockItemDefinitions.BilletUnits,
      // The drawn lanes are 2.5 / 3 / 2.5 wide - unequal - so one pour casts a heavier centre billet
      // than its neighbours: one station yielding two different items depending on which groove the metal
      // reached. The redraw cuts them equal. Ribs sit at x 5.5-6.5 and x 9.5-10.5.
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

  /// <summary>The <c>size</c> token a long-cell pattern declares. Spelt once: the casting cell refuses
  /// any non-<c>cell</c> spec and the long cell refuses any non-<c>longcell</c> one, so a typo here would
  /// make a pattern castable at neither station.</summary>
  private const string LongCell = "longcell";

  /// <summary>
  /// The pattern types that belong to the <b>long cell</b> rather than the 1×1 casting cell - the cast
  /// stock ladder. Derived from <see cref="CastStockItemDefinitions.Forms"/> so the two cannot drift.
  /// <para>
  /// <c>castframe</c> is <b>not</b> here. It is an lpex machine part, lpex ships its own <c>pattern</c>
  /// itemtype, and whether it stays at all is still open (see <c>long-cell.md</c> § Open questions). iwex
  /// owning a pattern for another mod's product is the coupling the whole spec-on-the-pattern design
  /// exists to avoid.
  /// </para>
  /// </summary>
  public static readonly string[] LongCellPatternTypes =
  [
    .. CastStockItemDefinitions.Forms.Select(f => f.Pattern),
  ];

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
    [Itemtype(domain, Molds, PatternShapes)];

  /// <summary>
  /// Builds a mod's whole <c>pattern</c> itemtype from its own mold table. <b>This is the seam that lets
  /// another mod own its cast parts without either mod naming the other's codes:</b> iwex owns the pattern
  /// system, each mod contributes entries, and the casting cell reads the spec off whatever pattern is held.
  /// lpex's <c>castshell</c> is the first outside caller.
  /// <para>
  /// <paramref name="shapes"/> may omit a type - it falls back to <c>game:item/plate</c>, so a part with
  /// no art yet reads as a flat plate rather than failing. That is forgiving by design and it is also a trap:
  /// a typo'd key looks exactly like missing art.
  /// </para>
  /// </summary>
  public static ExItemDef Itemtype(
    string domain,
    IReadOnlyDictionary<string, object> molds,
    IReadOnlyDictionary<string, string> shapes
  )
  {
    // attributesByType maps "*-{type}-*" -> { mold: {...} } (the trailing -* absorbs the wood variant),
    // resolved per variant into each pattern's own Attributes, where the cell reads it. The mold spec is
    // wood-agnostic, so all woods of a type share one spec.
    var byType = new Dictionary<string, object>();
    foreach ((string type, object mold) in molds)
      byType["*-" + type + "-*"] = mold;

    // shapeByType mirrors it: the pattern wears the shape of the part it casts (see PatternShapes). The
    // trailing `-*` absorbs the wood variant exactly as the mold wildcard does.
    var shapeByType = new Dictionary<string, object>();
    foreach ((string type, string shape) in shapes)
      shapeByType["*-" + type + "-*"] = new { @base = shape };

    return ExItemDef
      .Create(domain, "pattern")
      // Art rule (maintainer): a pattern reuses the CAST ITEM'S shape with the texture swapped to plain
      // wood - it reads as a wooden positive of the part. `shapeByType` picks the part's shape; the `all`
      // texture wildcard overrides whatever that shape declares (cast iron) with the plank's debarked wood,
      // which is what turns the cast part into its wooden positive. `Shape` stays as the fallback for a type
      // with no dedicated art yet.
      .Shape("game:item/plate")
      .Raw("shapeByType", shapeByType)
      .TextureAll("game:block/wood/debarked/{wood}")
      // type first, wood last: the mold-spec wildcard (*-{type}-*) and the lang key (item-pattern-{type}-*)
      // both key off the type with the wood as a trailing variant.
      .VariantGroup("type", [.. molds.Keys])
      .VariantGroup("wood", PatternWoods)
      .MaxStackSize(1)
      // A durable tool: each ram-up (Imprint) damages it, so a wooden pattern wears out over its runs.
      .Raw("durability", WoodenPatternDurability)
      .Raw("attributesByType", byType)
      .CreativeCommon("*");
  }
}
