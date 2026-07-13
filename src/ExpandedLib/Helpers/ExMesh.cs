using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Helpers;

/// <summary>Small shared helpers for hand-tesselated block meshes.</summary>
public static class ExMesh
{
  /// <summary>
  /// Rotates <paramref name="mesh"/> in place about the cell centre (0.5, 0.5, 0.5) by the block
  /// shape's Y rotation, matching how the chunk tesselator orients a placed block. A no-op when the
  /// mesh is null or the rotation is 0°. Used where a behaviour tesselates part of the shape itself
  /// (e.g. an MP node drawing its static body while vanilla renders the spinning axle) and so has to
  /// re-apply the orientation the tesselator would otherwise have baked in.
  /// </summary>
  public static void RotateByShape(MeshData? mesh, Block block)
  {
    if (mesh == null)
      return;
    float rotY = block.Shape.rotateY * GameMath.DEG2RAD;
    if (rotY != 0f)
      mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, rotY, 0);
  }
}
