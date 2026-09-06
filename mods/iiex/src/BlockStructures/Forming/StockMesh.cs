using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Forming;

/// <summary>
/// Draws a work piece at whatever gauge it is standing on. The mesh is composed rather than authored - every
/// state is the form's one base shape scaled - and the scale comes from the same values the simulation uses
/// (<see cref="WorkPiece.WidthAt"/> / <see cref="WorkPiece.LengthAt"/>), so the picture cannot drift from the
/// piece's behaviour.
/// <para>
/// What it covers is the half-step. A piece is one gauge across its whole width, and the gauges a route
/// declares are drawn art; the state between two of them is the half-step a round lands, and nothing draws
/// that. Under the per-side model this composed the lopsided piece that model allowed; a lopsided piece is
/// no longer reachable.
/// </para>
/// </summary>
public static class StockMesh {
  /// <summary>The base shapes are authored centred on x = 8 in the usual 16-unit block space.</summary>
  public const float CentreX = 8f;

  /// <summary>
  /// Per-axis scale of the form's base shape that draws <paramref name="piece"/> at its current gauge:
  /// width, thickness, length. The base is authored centred, so a scale about the centre is the whole
  /// placement.
  /// </summary>
  public static Vec3f ScaleOf(WorkPiece piece) {
    StockForm form = piece.Form;
    if (
      form.BaseWidth <= 0f
      || form.BaseThickness <= 0f
      || form.BaseLength <= 0f
    )
      return new Vec3f(1f, 1f, 1f);

    return new Vec3f(
      piece.Width / form.BaseWidth,
      piece.Thickness / form.BaseThickness,
      piece.Length / form.BaseLength
    );
  }

  /// <summary>
  /// Whether <paramref name="piece"/> is exactly the shape its form was authored at - still at the base
  /// gauge - so the default item mesh already draws it and nothing has to be composed.
  /// </summary>
  /// <remarks>
  /// The side count is not the question, and neither was evenness before it. A piece divided for a narrow
  /// barrel is still one gauge, and testing the division made a piece read as worked for having met a
  /// barrel it was never fed to.
  /// </remarks>
  public static bool IsBaseState(WorkPiece piece) =>
    ExpandedLib.Catalogues.ProcessRoute.SameThickness(
      piece.Thickness,
      piece.Form.BaseThickness
    );

  /// <summary>
  /// The shape element <paramref name="route"/> draws <paramref name="piece"/> at, or null when there is
  /// none and the composed mesh is the answer: no route, no shape file to hold the elements, a gauge the
  /// route does not draw, or a piece that does not know which branch worked it.
  /// </summary>
  /// <remarks>
  /// A piece with no family is never guessed at. At a fork two families draw one gauge differently, so a
  /// guess is visibly wrong half the time.
  /// </remarks>
  public static string? ElementFor(
    ExpandedLib.Catalogues.ProcessRoute? route,
    WorkPiece piece
  ) =>
    route?.Shape == null || piece.Family == null
      ? null
      : route.StageAt(piece.Thickness, piece.Family)?.Element;

  /// <summary>
  /// A key identifying the geometry of <paramref name="piece"/>, for caching composed meshes. The form, the
  /// gauge and the branch take part; the round in progress, the side count and the heat do not, since two
  /// pieces at the same gauge on the same branch look identical whatever they went through to get there.
  /// </summary>
  /// <remarks>
  /// Nothing per-stack may enter this key. The handbook clones the stack every frame, so a key carrying a
  /// stack's own identity would upload a fresh mesh per frame and leak every one of them - the trap vanilla
  /// documents in place on <c>ItemWorkItem</c>.
  /// </remarks>
  public static string CacheKey(WorkPiece piece) =>
    string.Concat(
      piece.Form.Name,
      "|",
      piece.Thickness.ToString(
        "0.###",
        System.Globalization.CultureInfo.InvariantCulture
      ),
      "|",
      piece.Family ?? "-"
    );
}
