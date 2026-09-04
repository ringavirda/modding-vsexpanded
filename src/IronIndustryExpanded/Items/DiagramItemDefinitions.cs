using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using IronIndustryExpanded.BlockStructures.Casting;

namespace IronIndustryExpanded.Items;

/// <summary>
/// Code-first itemtype for the iiex crafting diagrams; Phase 1 of the diagram-crafting system, see
/// <c>docs/design/mechanics/diagram-crafting.md</c>. One <c>iiex:diagram</c> item with a <c>type</c>
/// variant over the plans iiex owns: pipes, molten canals, the tuyere, the casting bed and cell, the
/// cold-blast and cupola cores, the flywheels, and one per sand-casting pattern. Each renders as the shared
/// <c>exlib:item/diag-base</c> folded sheet with the per-type <c>diag-{type}</c> drawing over the parchment
/// base texture. A diagram carries no logic: it is an ingredient in ordinary grid recipes, reusable via
/// <c>isTool</c> for parts and consumed for structure cores. Creative-only until the design table can
/// draft it.
/// </summary>
public class DiagramItemDefinitions : IExItemDefProvider {
  // Each name is both the variant state and the diag-{name} texture (assets/iiex/textures/item/diagram/).
  // Structure diagrams: the plans for placeable blocks.
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
    // `item-castbarrel`, which makes the blank the `molten-barrel-cast` variant is lined from.
    "molten-sandbed",
    "molten-sandcell",
    "tuyere",
    // Spelled out rather than generated: the type name is the texture name.
    "furnace-coldblast",
    "furnace-cupola",
    // Two flywheel plans rather than one shared: a 5x5x2 wheel is a different drawing from a 3x3x1 one,
    // and both textures are drawn. The type names follow the block codes
    // (`mpenergy-flywheel-normal` / `-large`).
    "mpenergy-flywheel",
    "mpenergy-flywheellarge",
  ];

  // Pattern diagrams: one per sand-casting pattern, drawn as diag-item-{pattern}. Derived from the pattern
  // list, so a new castable part gets its diagram for free; the "item-" prefix keeps the two families
  // apart and resolves through the diag-{type} rule.
  private static readonly string[] PatternDiagramTypes =
  [
    .. PatternItemDefinitions.PatternTypes.Select(t => "item-" + t),
  ];

  private static readonly string[] Types =
  [
    .. StructureTypes,
    .. PatternDiagramTypes,
  ];

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Itemtype(domain, Types)];

  /// <summary>
  /// Builds a mod's whole <c>diagram</c> itemtype over <paramref name="types"/>, shared for the reason
  /// <see cref="BlockStructures.Casting.PatternItemDefinitions.Itemtype"/> is: a mod that owns a cast part
  /// owns the diagram that plans it, and the transforms are declared once. Each type needs a
  /// <c>{domain}:item/diagram/diag-{type}</c> texture; a missing one renders untextured, not an error.
  /// </summary>
  public static ExItemDef Itemtype(string domain, IEnumerable<string> types) =>
    ExItemDef
      .Create(domain, "diagram")
      // The shared folded-sheet shape in exlib: parchment base plus the per-type drawing overlaid on the
      // sheet's paper face, so the plan reads on the top surface.
      .Shape("exlib:item/diag-base")
      .Texture(
        "paper",
        "exlib:item/diagram/paper",
        $"{domain}:item/diagram/diag-{{type}}"
      )
      .VariantGroup("type", [.. types])
      .MaxStackSize(64)
      .MaterialDensity(100)
      // Held two-handed and inclined so the drawing on the top face angles toward the camera. The
      // transforms below are seed values: there is no headless way to judge them.
      .HeldTpIdleAnimation("holdbothhands")
      .Raw(
        "fpHandTransform",
        new {
          translation = new {
            x = 0.0,
            y = -0.1,
            z = -0.3,
          },
          rotation = new {
            x = -40,
            y = 0,
            z = 0,
          },
          scale = 1.3,
        }
      )
      .TpHandTransform(
        new {
          translation = new {
            x = -0.5,
            y = -0.2,
            z = -0.3,
          },
          rotation = new {
            x = 0,
            y = 0,
            z = 0,
          },
          scale = 1.0,
        }
      )
      .GuiTransform(
        new {
          rotation = new {
            x = -63,
            y = 0,
            z = 0,
          },
          scale = 2.2,
        }
      )
      .GroundTransform(
        new {
          translation = new {
            x = 0,
            y = 0,
            z = 0,
          },
          rotation = new {
            x = 0,
            y = 0,
            z = 0,
          },
          scale = 2.5,
        }
      )
      .CreativeCommon("*");
}
