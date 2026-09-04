using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Testing;

/// <summary>
/// Relates a mega-block's drawn mesh to the volume it reserves. A mega-block states its shape twice -
/// once as art the tesselator spins by the blocktype's own <c>rotateYByType</c>, once as a filler
/// footprint the runtime spins by <c>StructureAngle</c> - and those are separate numbers on separate
/// objects. A shape authored along one axis and a grid authored along the other read as consistent in
/// every file while the placed machine draws its body where its collision is not: interaction cells
/// answer on air, port cells sit under nothing, and a surface renderer draws outside the shell.
/// </summary>
public static class MegablockFrames {
  /// <summary>
  /// Blocks of overhang a drawn mesh may have past its reserved footprint. Art hangs decorative
  /// hardware over the edge - an outlet neck rising into the pipe cell above a port, a hatch's furniture
  /// standing proud of the face it is set in. Small on purpose: one cell is exactly the error this
  /// comparison exists to catch, so a tolerance of a whole cell would accept it.
  /// </summary>
  public const float DefaultOverhang = 0.25f;

  /// <summary>
  /// A description of how <paramref name="shapePath"/>'s drawn box falls outside
  /// <paramref name="principal"/>'s footprint once each is turned by its own angle, or <c>null</c> when
  /// it fits. Both boxes are measured in blocks about the principal cell's centre, which is the point
  /// both rotations turn about.
  /// </summary>
  public static string? Misfit(
    string shapePath,
    int spinDegrees,
    IFillerHost principal,
    int structureAngle,
    float overhang = DefaultOverhang
  ) {
    Box mesh = Rotate(MeshBox(shapePath), spinDegrees);
    Box footprint = Rotate(FootprintBox(principal), structureAngle);

    for (int axis = 0; axis < 3; axis++)
      if (
        mesh.Min[axis] < footprint.Min[axis] - overhang
        || mesh.Max[axis] > footprint.Max[axis] + overhang
      )
        return $"draws {Axis(axis)} over "
          + $"[{mesh.Min[axis]:0.###}, {mesh.Max[axis]:0.###}] but reserves "
          + $"[{footprint.Min[axis]:0.###}, {footprint.Max[axis]:0.###}] "
          + $"(blocks from the principal's centre, {overhang} of overhang "
          + $"allowed). The shape spins by {spinDegrees}deg and the footprint "
          + $"by {structureAngle}deg; unless those two agree the mesh and the "
          + "fillers are placed in different frames.";

    return null;
  }

  /// <summary>The repo-relative shape file a <c>domain:path</c> shape reference names.</summary>
  public static string ShapeFile(string shapeBase) {
    string[] parts = shapeBase.Split(':', 2);
    return DefinitionGoldens.SolutionRelative(
      $"assets/{parts[0]}/shapes/{parts[1]}.json"
    );
  }

  /// <summary>An axis-aligned box in blocks, measured from the principal cell's centre.</summary>
  private readonly record struct Box(float[] Min, float[] Max);

  private static string Axis(int i) =>
    i == 0 ? "x"
    : i == 1 ? "y"
    : "z";

  /// <summary>
  /// The shape's own drawn bounding box. Shape voxels run 0..16 across the principal cell, so a voxel is
  /// a sixteenth of a block and the cell's centre sits at 8.
  /// </summary>
  private static Box MeshBox(string shapePath) {
    (float[] min, float[] max) = ShapeExtents.Bounds(shapePath);
    return new Box(
      [.. min.Select(v => v / 16f - 0.5f)],
      [.. max.Select(v => v / 16f - 0.5f)]
    );
  }

  /// <summary>
  /// The declared footprint's bounding box, the principal's own cell included - it is drawn as the
  /// layout's <c>O</c> and never becomes a filler, but the machine occupies it. Each cell spans half a
  /// block either side of its centre.
  /// </summary>
  private static Box FootprintBox(IFillerHost principal) {
    var cells = new List<Vec3i> { new(0, 0, 0) };
    cells.AddRange(
      StructureFillers
        .ReadOffsets(principal.FillerOffsets)
        .Select(o => o.Offset)
    );
    return new Box(
      [
        cells.Min(c => c.X) - 0.5f,
        cells.Min(c => c.Y) - 0.5f,
        cells.Min(c => c.Z) - 0.5f,
      ],
      [
        cells.Max(c => c.X) + 0.5f,
        cells.Max(c => c.Y) + 0.5f,
        cells.Max(c => c.Z) + 0.5f,
      ]
    );
  }

  /// <summary>
  /// <paramref name="box"/> turned by <paramref name="angle"/> about the principal cell's centre, as the
  /// axis-aligned box of the result. Exact for the four quarter turns, which are the only angles either
  /// frame uses.
  /// </summary>
  private static Box Rotate(Box box, int angle) {
    var xs = new List<float>();
    var zs = new List<float>();
    foreach (float x in new[] { box.Min[0], box.Max[0] })
      foreach (float z in new[] { box.Min[2], box.Max[2] }) {
        (double rx, double rz) = ExOrientation.RotateXZ(x, z, angle);
        xs.Add((float)rx);
        zs.Add((float)rz);
      }
    return new Box(
      [xs.Min(), box.Min[1], zs.Min()],
      [xs.Max(), box.Max[1], zs.Max()]
    );
  }
}
