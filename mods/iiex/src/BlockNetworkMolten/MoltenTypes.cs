using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockNetworkMolten;

/// <summary>
/// Defines a horizontal fill footprint (x/z extents) for the molten-surface
/// renderer.
/// </summary>
public struct FillQuadDef {
  public float x1,
    z1,
    x2,
    z2;
}

/// <summary>
/// Reads the molten-surface fill geometry attributes (<c>fillQuadsByLevel</c>, <c>fillStart</c>,
/// <c>fillHeight</c>, or a prefixed variant such as the pedestal's <c>moldFill*</c>) that the canal, tap,
/// pedestal and barrel declare for their <see cref="MoltenRenderer"/>.
/// </summary>
public static class FillQuads {
  /// <summary>
  /// Builds the renderer footprint boxes from a resolved <c>fillQuadsByLevel</c> node, such as a block's
  /// <c>FillQuadsByLevel</c> accessor. Each quad def becomes a full-height x/z box; a missing or empty
  /// node yields a single <paramref name="fallback"/> box. Fill start and height levels are not read here:
  /// call sites take them from the block's own <c>FillStart</c>/<c>FillHeight</c>, in pixels.
  /// </summary>
  public static Cuboidf[] BoxesFrom(JsonObject? quadsNode, Cuboidf fallback) {
    var quadDefs = quadsNode?.AsObject<FillQuadDef[]>();
    return quadDefs is { Length: > 0 }
      ? [.. quadDefs.Select(q => new Cuboidf(q.x1, 0f, q.z1, q.x2, 16f, q.z2))]
      : [fallback];
  }
}
