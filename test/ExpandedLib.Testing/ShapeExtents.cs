using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Measures a shape file - the bounding box of everything it draws, in voxels. Shared because getting it
/// wrong is quiet: an art guard that measures the wrong box passes on art that is the wrong size, and both
/// of this repo's stock-art guards would otherwise carry their own copy of the composition rule.
/// </summary>
public static class ShapeExtents {
  /// <summary>
  /// The composed extents of every element in <paramref name="path"/>, as (width, thickness, length) on
  /// x / y / z.
  /// </summary>
  /// <remarks>
  /// A child element's <c>from</c> / <c>to</c> are offsets from its PARENT's <c>from</c>, not absolute
  /// coordinates, so every element has to be lifted into absolute space before it is measured. Read as
  /// absolute, a piece drawn as two halves end to end measures as lanes side by side - which is how three
  /// cast stock shapes were once reported as needing a redraw when only one did. Measuring the top-level
  /// elements alone has the milder version of the same fault: it sees one half and calls it the piece.
  /// </remarks>
  /// <param name="element">One element to measure, children included - a family shape file holds every
  /// stage of a route, so measuring the whole file would measure thirteen drawn states at once. Null
  /// measures the file.</param>
  public static (float Width, float Thickness, float Length) Of(
    string path,
    string? element = null
  ) {
    var corners = new List<float[]>();
    JObject shape = JObject.Parse(File.ReadAllText(path));
    JToken? root = shape["elements"];

    if (element != null) {
      JToken? found = Find(root, element);
      if (found == null)
        throw new KeyNotFoundException($"{path} has no element '{element}'");
      // Measured in its own frame: a stage's absolute placement in the file is layout, not geometry.
      Walk(found, [0f, 0f, 0f], corners);
    } else {
      foreach (JToken el in root ?? new JArray())
        Walk(el, [0f, 0f, 0f], corners);
    }

    return (Span(corners, 0), Span(corners, 1), Span(corners, 2));
  }

  private static JToken? Find(JToken? elements, string name) {
    foreach (JToken el in elements ?? new JArray()) {
      if ((string?)el["name"] == name)
        return el;
      if (Find(el["children"], name) is { } nested)
        return nested;
    }
    return null;
  }

  private static void Walk(
    JToken element,
    float[] origin,
    List<float[]> corners
  ) {
    float[] from = Corner(element["from"], origin);
    corners.Add(from);
    corners.Add(Corner(element["to"], origin));

    // The element's own `from` is the origin its children hang off, absolute by the time we are here.
    foreach (JToken child in element["children"] ?? new JArray())
      Walk(child, from, corners);
  }

  private static float[] Corner(JToken? point, float[] origin) =>
    [.. Enumerable.Range(0, 3).Select(i => origin[i] + (float)point![i]!)];

  private static float Span(List<float[]> corners, int axis) =>
    corners.Max(c => c[axis]) - corners.Min(c => c[axis]);
}
