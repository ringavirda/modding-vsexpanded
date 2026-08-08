using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using IronworkingExpanded.BlockStructures.Casting;

namespace IronworkingExpanded.Items;

/// <summary>
/// Code-first itemtype for the iwex crafting diagrams - Phase 1 of the diagram-crafting system (see
/// <c>docs/design/diagram-crafting.md</c>). One <c>iwex:diagram</c> item with a <c>type</c> variant over
/// the plans iwex owns: pipes, molten canals, the tuyere, the casting bed/cell, and the cold-blast and
/// cupola cores. Each is a drawn parchment sheet - the shared <c>exlib:item/diag-base</c> folded-sheet
/// shape, the parchment base texture, and the per-type <c>diag-{type}</c> drawing overlaid on the sheet's
/// face so the plan reads on top.
/// <para>
/// A diagram is a <b>plain item</b>: it carries no logic. Crafting is Model A - the diagram is an
/// ingredient in ordinary grid recipes, reusable via <c>isTool</c> for parts and consumed for structure
/// cores (added in a later phase). lpex (passthrough / pipe-outlet) and smex (tool molds, the hot-blast
/// core) contribute their own diagram variants when those phases land. For now it is creative-only until
/// the design table can draft it.
/// </para>
/// <para>
/// Held two-handed and tilted so the drawing faces the player. The held/gui/ground transforms below are
/// <b>seed values only</b> - model transforms can only be judged in-game, so expect to tune them.
/// </para>
/// </summary>
public class DiagramItemDefinitions : IExItemDefProvider
{
  // Each name is BOTH the variant state and the diag-{name} texture (assets/iwex/textures/item/diagram/).
  // STRUCTURE diagrams - the plans for placeable blocks (pipes, molten canals, the tuyere, the casting
  // bed/cell, the cold-blast and cupola cores).
  private static readonly string[] StructureTypes =
  [
    "pipe-straight",
    "pipe-bend",
    "pipe-tjunction",
    "pipe-xjunction",
    "molten-straight",
    "molten-bend",
    "molten-tjunction",
    "molten-xjunction",
    "molten-start",
    "molten-tap",
    "molten-furnacetap",
    "molten-moldpedestal",
    // No `molten-barrel` entry: the barrel's cast route is planned by the pattern diagram
    // `item-castbarrel`, which makes the blank the `molten-barrel-cast` variant is lined
    // from - a second structure diagram for the finished vessel would plan the same thing twice.
    "molten-sandbed",
    "molten-sandcell",
    "tuyere",
    // `furnace-coldblast` / `furnace-cupola`, not initialisms. The type name is the
    // texture name, and the drawn set spells them out.
    "furnace-coldblast",
    "furnace-cupola",
    // The two flywheel plans. Each grid consumes its own (reusable, `isTool`), which is what makes them
    // read as diagram-crafting rather than as a picture of a wheel drawn in the grid: per
    // docs/design/diagram-crafting.md a diagram-led recipe is diagram + material list, with quantities on
    // one cell each, never the same ingredient scattered across cells to suggest a shape.
    //
    // Two diagrams, not one shared. A 5×5×2 wheel is a different drawing from a 3×3×1 one even though the
    // build is an upgrade of it, and the art says so: `diag-mpenergy-flywheel` and
    // `diag-mpenergy-flywheellarge` are both drawn. The type names follow the art, which follows the block
    // codes (`mpenergy-flywheel-normal` / `-large`).
    "mpenergy-flywheel",
    "mpenergy-flywheellarge",
  ];

  // PATTERN/ITEM diagrams - one per sand-casting pattern, drawn as diag-item-{pattern}. Derived from the
  // pattern list (single source), so a new castable part gets its diagram for free; the "item-" prefix
  // keeps the two families apart and resolves the diag-item-{pattern} texture through the diag-{type} rule.
  private static readonly string[] PatternDiagramTypes =
    [.. PatternItemDefinitions.PatternTypes.Select(t => "item-" + t)];

  private static readonly string[] Types = [.. StructureTypes, .. PatternDiagramTypes];

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Itemtype(domain, Types)];

  /// <summary>
  /// Builds a mod's whole <c>diagram</c> itemtype over <paramref name="types"/>. Shared for the same
  /// reason <see cref="BlockStructures.Casting.PatternItemDefinitions.Itemtype"/> is: a mod that owns a cast
  /// part owns the diagram that plans it, and duplicating sixty lines of seed transforms per mod is how the
  /// four sheets come to sit at four different angles in the hand.
  /// <para>
  /// Each type needs a <c>{domain}:item/diagram/diag-{type}</c> texture. A missing one is not an error -
  /// the sheet just renders untextured - so the texture set has no guard but the eye.
  /// </para>
  /// </summary>
  public static ExItemDef Itemtype(string domain, IEnumerable<string> types) =>
      ExItemDef
        .Create(domain, "diagram")
        // The folded-sheet shape (shared, in exlib): parchment base + the per-type drawing overlaid on the
        // sheet's "paper" face, so the plan reads on the top surface.
        .Shape("exlib:item/diag-base")
        .Texture(
          "paper",
          "exlib:item/diagram/paper",
          $"{domain}:item/diagram/diag-{{type}}"
        )
        .VariantGroup("type", [.. types])
        .MaxStackSize(64)
        .MaterialDensity(100)
        // Held two-handed and inclined so the drawing on the top face angles toward the camera. SEED
        // transforms only - there is no headless way to judge these; tune them in-game.
        .HeldTpIdleAnimation("holdbothhands")
        .Raw(
          "fpHandTransform",
          new
          {
            translation = new { x = 0.0, y = -0.1, z = -0.3 },
            rotation = new { x = -40, y = 0, z = 0 },
            scale = 1.3,
          }
        )
        .TpHandTransform(
          new
          {
            translation = new { x = -0.5, y = -0.2, z = -0.3 },
            rotation = new { x = 0, y = 0, z = 0 },
            scale = 1.0,
          }
        )
        .GuiTransform(new { rotation = new { x = -63, y = 0, z = 0 }, scale = 2.2 })
        .GroundTransform(
          new
          {
            translation = new { x = 0, y = 0, z = 0 },
            rotation = new { x = 0, y = 0, z = 0 },
            scale = 2.5,
          }
        )
        .CreativeCommon("*");
}
