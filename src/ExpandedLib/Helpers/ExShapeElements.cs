using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Helpers;

/// <summary>
/// Prunes a loaded <see cref="Shape"/> to a chosen set of element paths - the mesh-side counterpart of a
/// blocktype's <c>selectiveElements</c>, for a block entity that decides which parts to draw at runtime
/// (a hearth showing only the pigs actually charged on it).
/// <para>
/// It exists because the engine's own selective-element matching is <b>prefix-based per path segment</b>
/// and has caught this codebase out twice: naming an ancestor keeps far more than intended, and naming an
/// element exactly drops all of its children. Filtering the element tree here makes the rule explicit and,
/// more importantly, <b>pure and testable</b> - these are string literals aimed at art, where a typo
/// renders nothing at all rather than raising anything.
/// </para>
/// </summary>
public static class ExShapeElements
{
  /// <summary>
  /// Whether the element at <paramref name="path"/> should be kept, given <paramref name="patterns"/>.
  /// Two directions both count, and both are needed:
  /// <list type="bullet">
  /// <item>the element is <b>on the way to</b> a wanted one (its path is a prefix of a pattern) - a
  /// parent group must survive or its children are unreachable;</item>
  /// <item>the element is <b>at or under</b> a wanted one (a pattern is a prefix of its path) - so
  /// naming a group keeps the whole subtree, and naming a leaf keeps the leaf.</item>
  /// </list>
  /// A trailing <c>/*</c> is accepted and ignored: it reads as "and everything under it", which is
  /// already what a pattern means here, so the two spellings cannot diverge.
  /// </summary>
  public static bool Matches(string path, IReadOnlyCollection<string> patterns)
  {
    foreach (string raw in patterns)
    {
      // "A/B/*" means the subtree under A/B; a lone "*" means the whole shape. Both reduce to a prefix.
      string p = raw == "*" ? "" : raw.EndsWith("/*") ? raw[..^2] : raw;
      if (p.Length == 0)
        return true;
      if (IsSegmentPrefix(path, p) || IsSegmentPrefix(p, path))
        return true;
    }
    return false;
  }

  // Prefix on whole segments only: "Items1" must not match "Items10", and "Pigs/Pig1" must not match
  // "Pigs/Pig11". That is the same class of bug as a bare-prefix block-code wildcard.
  private static bool IsSegmentPrefix(string prefix, string path) =>
    path.Length >= prefix.Length
    && path.StartsWith(prefix, System.StringComparison.Ordinal)
    && (path.Length == prefix.Length || path[prefix.Length] == '/');

  /// <summary>
  /// Returns a copy of <paramref name="shape"/> containing only the elements <paramref name="keep"/>
  /// selects. The original is not modified - shapes are shared assets, and mutating one would leak the
  /// pruning into every other block using it.
  /// </summary>
  public static Shape Pruned(Shape shape, IReadOnlyCollection<string> keep)
  {
    Shape copy = shape.Clone();
    copy.Elements = PruneLevel(copy.Elements, "", keep);
    return copy;
  }

  private static ShapeElement[] PruneLevel(
    ShapeElement[]? elements,
    string parentPath,
    IReadOnlyCollection<string> keep
  )
  {
    if (elements == null)
      return [];

    var kept = new List<ShapeElement>();
    foreach (ShapeElement el in elements)
    {
      string path = parentPath.Length == 0 ? el.Name ?? "" : parentPath + "/" + el.Name;
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
  /// wearing whichever of coke/bituminous/anthracite/charcoal was charged into it).
  /// <para>
  /// It rewrites the <b>shape</b>, not the block: the texture source a tesselation runs against resolves
  /// <c>#key</c> against the <em>blocktype's</em> texture map, so <paramref name="to"/> must be a key that
  /// blocktype declares. A key it does not declare draws the engine's missing-texture pink rather than
  /// raising, which is the failure this doc-comment exists to shorten the hunt for.
  /// </para>
  /// <para>
  /// The original is not modified, for the reason <see cref="Pruned"/> does not modify it: shapes are
  /// shared assets and a retexture leaking into another block using the same shape would be invisible
  /// until someone noticed the wrong block had changed colour.
  /// </para>
  /// </summary>
  public static Shape Retextured(Shape shape, string from, string to)
  {
    Shape copy = shape.Clone();
    RetextureLevel(copy.Elements, "#" + from, "#" + to);
    return copy;
  }

  private static void RetextureLevel(ShapeElement[]? elements, string from, string to)
  {
    foreach (ShapeElement el in elements ?? [])
    {
      foreach (ShapeElementFace face in (el.FacesResolved ?? []))
        if (face?.Texture == from)
          face.Texture = to;
      RetextureLevel(el.Children, from, to);
    }
  }

  /// <summary>Every element path in <paramref name="shape"/>, parent-first - what a test asserts a
  /// layout's literals against, so a re-export that renames a group fails headlessly instead of
  /// silently drawing nothing.</summary>
  public static IEnumerable<string> AllPaths(Shape shape) => Walk(shape.Elements, "");

  private static IEnumerable<string> Walk(ShapeElement[]? elements, string parentPath)
  {
    foreach (ShapeElement el in elements ?? [])
    {
      string path = parentPath.Length == 0 ? el.Name ?? "" : parentPath + "/" + el.Name;
      yield return path;
      foreach (string child in Walk(el.Children, path))
        yield return child;
    }
  }
}
