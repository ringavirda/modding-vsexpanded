using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Forming;

/// <summary>How one side of a work piece is placed relative to the form's authored base shape.</summary>
/// <param name="Scale">Per-axis scale of the base mesh: width, thickness, length.</param>
/// <param name="OffsetX">Shift along the barrel axis that puts this side in its place across the piece.</param>
public readonly record struct SidePlacement(Vec3f Scale, float OffsetX);

/// <summary>
/// Places the sides of a part-rolled piece so a half-worked bloom looks half-worked: thin and wide down the
/// side that has been through the rolls, still thick and narrow down the side that has not. The mesh is
/// composed rather than authored - every state is the form's one base shape scaled per side - and the scale
/// comes from the same values the simulation uses (<see cref="WorkPiece.StripWidth"/> /
/// <see cref="WorkPiece.StripLength"/>), so the picture cannot drift from the piece's behaviour.
/// </summary>
public static class StockMesh {
  /// <summary>The base shapes are authored centred on x = 8 in the usual 16-unit block space.</summary>
  public const float CentreX = 8f;

  /// <summary>
  /// Where side <paramref name="index"/> of <paramref name="piece"/> sits, as a scale and shift of the
  /// form's base shape. Sides abut across the piece and the whole stays centred, so an unevenly rolled
  /// piece is lopsided, the wider and thinner side taking up more of the width.
  /// </summary>
  public static SidePlacement SideOf(
    WorkPiece piece,
    int index,
    float centreX = CentreX
  ) {
    StockForm form = piece.Form;
    if (index < 0 || index >= piece.Strips.Length || form.BaseWidth <= 0f)
      return new SidePlacement(new Vec3f(1f, 1f, 1f), 0f);

    float thickness = piece.Strips[index];
    float width = piece.StripWidth(thickness);

    // Left edge of the whole piece, then walk across the sides before this one.
    float left = centreX - piece.Width / 2f;
    for (int i = 0; i < index; i++)
      left += piece.StripWidth(piece.Strips[i]);

    return new SidePlacement(
      new Vec3f(
        width / form.BaseWidth,
        thickness / form.BaseThickness,
        piece.StripLength(thickness) / form.BaseLength
      ),
      // The base scales about the centre, so shift from there to where this side actually belongs.
      left + width / 2f - centreX
    );
  }

  /// <summary>
  /// A key identifying the geometry of <paramref name="piece"/>, for caching composed meshes. Only the form
  /// and the strip thicknesses take part: two pieces at the same thicknesses look identical, so turn-over
  /// state and heat are excluded and the cache holds a handful of states per form rather than one per stack.
  /// </summary>
  public static string CacheKey(WorkPiece piece) {
    var sb = new System.Text.StringBuilder(piece.Form.Name);
    foreach (float t in piece.Strips)
      sb.Append('|')
        .Append(
          t.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
        );
    return sb.ToString();
  }
}
