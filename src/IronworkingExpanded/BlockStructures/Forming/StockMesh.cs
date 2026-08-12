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
  /// Whether <paramref name="piece"/> is exactly the shape its form was authored at - one undivided side,
  /// still at the base gauge - so the default item mesh already draws it and nothing has to be composed.
  /// </summary>
  /// <remarks>
  /// Evenness is not the question. Every single-sided piece is even, so testing that alone made a bloom
  /// taken from 3.0 down to 2.0 read as unworked and render as if it had never been rolled.
  /// </remarks>
  public static bool IsBaseState(WorkPiece piece) =>
    piece.Sides <= 1
    && ExpandedLib.Processes.StageLadder.SameThickness(
      piece.Thickest,
      piece.Form.BaseThickness
    );

  /// <summary>
  /// The shape element <paramref name="ladder"/> draws <paramref name="piece"/> at, or null when there is
  /// none and the composed mesh is the answer: no ladder, no shape file to hold the elements, a gauge the
  /// ladder does not draw, or a piece that does not know which branch worked it.
  /// </summary>
  /// <remarks>
  /// A piece with no family is never guessed at. At a fork two families draw one gauge differently, so a
  /// guess is visibly wrong half the time.
  /// </remarks>
  public static string? ElementFor(
    ExpandedLib.Processes.StageLadder? ladder,
    WorkPiece piece
  ) =>
    ladder?.Shape == null || piece.Family == null
      ? null
      : ladder.StageAt(piece.Thickest, piece.Family)?.Element;

  /// <summary>
  /// A key identifying the geometry of <paramref name="piece"/>, for caching composed meshes. The form, the
  /// strip thicknesses and the branch take part; turn-over state and heat do not, since two pieces at the
  /// same gauge on the same branch look identical whatever they went through to get there.
  /// </summary>
  /// <remarks>
  /// Nothing per-stack may enter this key. The handbook clones the stack every frame, so a key carrying a
  /// stack's own identity would upload a fresh mesh per frame and leak every one of them - the trap vanilla
  /// documents in place on <c>ItemWorkItem</c>.
  /// </remarks>
  public static string CacheKey(WorkPiece piece) {
    var sb = new System.Text.StringBuilder(piece.Form.Name);
    foreach (float t in piece.Strips)
      sb.Append('|')
        .Append(
          t.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
        );
    return sb.Append('|').Append(piece.Family ?? "-").ToString();
  }
}
