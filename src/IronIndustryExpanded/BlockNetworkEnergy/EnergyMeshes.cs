using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockNetworkEnergy;

/// <summary>
/// Shared mesh work for the mpenergy transmission. A bevel is composed rather than baked from a single
/// shape: the straight shaft body rotated to the run's <c>ns</c>/<c>we</c> axis, plus one bevel-gear mesh
/// per geared face, each rotated from the shape's south-authored reference onto its world face.
/// </summary>
public static class EnergyMeshes {
  private static readonly AssetLocation ShaftShapeLoc = new(
    "iiex:shapes/mpenergy/shaft.json"
  );
  private static readonly AssetLocation GearShapeLoc = new(
    "iiex:shapes/mpenergy/bevelgear.json"
  );

  private static readonly Vec3f CellCentre = new(0.5f, 0.5f, 0.5f);

  /// <summary>
  /// Rotation (radians, X/Y/Z about the cell centre) that carries the south-authored bevel-gear mesh onto
  /// <paramref name="worldFace"/>. South is the authoring reference and yields identity; the horizontals
  /// turn about Y, up and down tip about X.
  /// </summary>
  public static Vec3f GearRotation(BlockFacing worldFace) =>
    worldFace.Index switch {
      BlockFacing.indexSOUTH => new Vec3f(0f, 0f, 0f),
      BlockFacing.indexNORTH => new Vec3f(0f, GameMath.PI, 0f),
      BlockFacing.indexEAST => new Vec3f(0f, GameMath.PIHALF, 0f),
      BlockFacing.indexWEST => new Vec3f(0f, GameMath.PI + GameMath.PIHALF, 0f),
      BlockFacing.indexUP => new Vec3f(-GameMath.PIHALF, 0f, 0f),
      BlockFacing.indexDOWN => new Vec3f(GameMath.PIHALF, 0f, 0f),
      _ => new Vec3f(0f, 0f, 0f),
    };

  /// <summary>Tesselates the straight shaft body for <paramref name="block"/>, rotated to the block's shape
  /// orientation (<c>ns</c>/<c>we</c>). Returns null if the shape is missing.</summary>
  public static MeshData? TesselateShaft(
    ICoreAPI api,
    ITesselatorAPI tesselator,
    Block block
  ) {
    Shape? shape = api.Assets.Get<Shape>(ShaftShapeLoc);
    if (shape == null)
      return null;

    tesselator.TesselateShape(block, shape, out MeshData mesh);
    ExpandedLib.Helpers.ExMesh.RotateByShape(mesh, block);
    return mesh;
  }

  /// <summary>Tesselates one bevel gear rotated onto <paramref name="worldFace"/>. The face rotation is applied
  /// in the world frame, independent of the shaft axis, so callers must not also apply the shape rotation.
  /// Returns null if the shape is missing. Callers cache per face.</summary>
  public static MeshData? TesselateGear(
    ICoreAPI api,
    ITesselatorAPI tesselator,
    Block block,
    BlockFacing worldFace
  ) {
    Shape? shape = api.Assets.Get<Shape>(GearShapeLoc);
    if (shape == null)
      return null;

    tesselator.TesselateShape(block, shape, out MeshData mesh);
    Vec3f rot = GearRotation(worldFace);
    if (rot.X != 0f || rot.Y != 0f || rot.Z != 0f)
      mesh.Rotate(CellCentre, rot.X, rot.Y, rot.Z);
    return mesh;
  }
}
