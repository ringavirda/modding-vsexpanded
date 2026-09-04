using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Helpers;

/// <summary>
/// Prunes a loaded <see cref="Shape"/> to a chosen set of element paths - the mesh-side counterpart of a
/// blocktype's <c>selectiveElements</c>, for a block entity that decides which parts to draw at runtime
/// (a hearth showing only the pigs actually charged on it). Matching here is explicit rather than the
/// engine's per-segment prefix rule, where naming an ancestor keeps more than intended and naming an
/// element exactly drops its children. Patterns are string literals aimed at art: one that matches
/// nothing renders nothing rather than raising.
/// </summary>
public static class ExShapeElements {
  /// <summary>
  /// Whether the element at <paramref name="path"/> should be kept, given <paramref name="patterns"/>.
  /// Prefix matching runs in both directions, and both are needed:
  /// <list type="bullet">
  /// <item>the element is on the way to a wanted one (its path is a prefix of a pattern) - a parent
  /// group must survive or its children are unreachable;</item>
  /// <item>the element is at or under a wanted one (a pattern is a prefix of its path) - so naming a
  /// group keeps the whole subtree, and naming a leaf keeps the leaf.</item>
  /// </list>
  /// A trailing <c>/*</c> is accepted and ignored, since "and everything under it" is already what a
  /// pattern means here.
  /// </summary>
  public static bool Matches(string path, IReadOnlyCollection<string> patterns) {
    foreach (string raw in patterns) {
      // "A/B/*" means the subtree under A/B; a lone "*" means the whole shape. Both reduce to a prefix.
      string p =
        raw == "*" ? ""
        : raw.EndsWith("/*") ? raw[..^2]
        : raw;
      if (p.Length == 0)
        return true;
      if (IsSegmentPrefix(path, p) || IsSegmentPrefix(p, path))
        return true;
    }
    return false;
  }

  // Prefix on whole segments only: "Items1" must not match "Items10", and "Pigs/Pig1" must not match
  // "Pigs/Pig11".
  private static bool IsSegmentPrefix(string prefix, string path) =>
    path.Length >= prefix.Length
    && path.StartsWith(prefix, System.StringComparison.Ordinal)
    && (path.Length == prefix.Length || path[prefix.Length] == '/');

  /// <summary>
  /// Returns a copy of <paramref name="shape"/> containing only the elements <paramref name="keep"/>
  /// selects. The original is not modified: shapes are shared assets, and mutating one would leak the
  /// pruning into every other block using it.
  /// </summary>
  public static Shape Pruned(Shape shape, IReadOnlyCollection<string> keep) {
    Shape copy = shape.Clone();
    copy.Elements = PruneLevel(copy.Elements, "", keep);
    return copy;
  }

  private static ShapeElement[] PruneLevel(
    ShapeElement[]? elements,
    string parentPath,
    IReadOnlyCollection<string> keep
  ) {
    if (elements == null)
      return [];

    var kept = new List<ShapeElement>();
    foreach (ShapeElement el in elements) {
      string path =
        parentPath.Length == 0 ? el.Name ?? "" : parentPath + "/" + el.Name;
      if (!Matches(path, keep))
        continue;
      el.Children = PruneLevel(el.Children, path, keep);
      kept.Add(el);
    }
    return [.. kept];
  }

  /// <summary>
  /// Returns a copy of <paramref name="shape"/> with every face pointing at texture key
  /// <paramref name="from"/> repointed at <paramref name="to"/> - the runtime half of a blocktype that
  /// declares several textures for one drawn part and picks between them per block entity (a firebox bed
  /// wearing whichever of coke/bituminous/anthracite/charcoal was charged into it). Only the shape is
  /// rewritten, so <paramref name="to"/> must be a key the blocktype declares: tesselation resolves
  /// <c>#key</c> against the blocktype's texture map, and an undeclared key draws the missing-texture
  /// pink rather than raising. The original is not modified, as in <see cref="Pruned"/>.
  /// </summary>
  public static Shape Retextured(Shape shape, string from, string to) {
    Shape copy = shape.Clone();
    RetextureLevel(copy.Elements, "#" + from, "#" + to);
    return copy;
  }

  private static void RetextureLevel(
    ShapeElement[]? elements,
    string from,
    string to
  ) {
    foreach (ShapeElement el in elements ?? []) {
      ShapeElementFace[] faces = el.FacesResolved ?? [];
      for (int i = 0; i < faces.Length; i++)
        if (faces[i]?.Texture == from)
          faces[i] = Repointed(faces[i], to);
      RetextureLevel(el.Children, from, to);
    }
  }

  // Shape.Clone deep-copies the element tree, but ShapeElement.Clone copies FacesResolved with a plain
  // array clone, so the copy's faces are the same objects as the source's. Writing face.Texture there
  // repoints the asset itself for every other block using it. The array slot is ours to overwrite; the
  // face behind it is not, so a repointed face is a new one.
  private static ShapeElementFace Repointed(ShapeElementFace face, string to) =>
    new() {
      Texture = to,
      Uv = (float[]?)face.Uv?.Clone(),
      Rotation = face.Rotation,
      Glow = face.Glow,
      Enabled = face.Enabled,
      ReflectiveMode = face.ReflectiveMode,
      WindMode = (sbyte[]?)face.WindMode?.Clone(),
      WindData = (sbyte[]?)face.WindData?.Clone(),
    };

  /// <summary>Every element path in <paramref name="shape"/>, parent-first - the set pattern literals
  /// can be checked against after a re-export renames a group.</summary>
  public static IEnumerable<string> AllPaths(Shape shape) =>
    Walk(shape.Elements, "");

  private static IEnumerable<string> Walk(
    ShapeElement[]? elements,
    string parentPath
  ) {
    foreach (ShapeElement el in elements ?? []) {
      string path =
        parentPath.Length == 0 ? el.Name ?? "" : parentPath + "/" + el.Name;
      yield return path;
      foreach (string child in Walk(el.Children, path))
        yield return child;
    }
  }
}
