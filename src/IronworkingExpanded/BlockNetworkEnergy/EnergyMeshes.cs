using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockNetworkEnergy;

/// <summary>
/// Shared mesh work for the cast-iron mpenergy transmission. A bevel is drawn by composition, not from one baked
/// shape: the straight shaft body (rotated to the run's <c>ns</c>/<c>we</c> axis) plus one bevel-gear mesh per
/// geared face, each rotated from the shape's south-authored reference onto its world face - the same
/// "authored facing south, rotated onto the face" trick the molten end-cap uses. Keeping the gear rotation in a
/// tiny, testable table (<see cref="GearRotation"/>) is what lets the geometry be pinned without the renderer.
/// </summary>
public static class EnergyMeshes
{
  private static readonly AssetLocation ShaftShapeLoc = new(
    "iwex:shapes/mpenergy/shaft.json"
  );
  private static readonly AssetLocation GearShapeLoc = new(
    "iwex:shapes/mpenergy/bevelgear.json"
  );

  private static readonly Vec3f CellCentre = new(0.5f, 0.5f, 0.5f);

  /// <summary>
  /// The rotation (radians, X/Y/Z about the cell centre) that carries the south-authored bevel-gear mesh onto
  /// <paramref name="worldFace"/>. South is the authoring reference (identity); the four horizontals turn about
  /// Y exactly as the end-cap does, and up/down tip about X. Pure so it can be pinned in a unit test.
  /// </summary>
  public static Vec3f GearRotation(BlockFacing worldFace) =>
    worldFace.Index switch
    {
      BlockFacing.indexSOUTH => new Vec3f(0f, 0f, 0f),
      BlockFacing.indexNORTH => new Vec3f(0f, GameMath.PI, 0f),
      BlockFacing.indexEAST => new Vec3f(0f, GameMath.PIHALF, 0f),
      BlockFacing.indexWEST => new Vec3f(0f, GameMath.PI + GameMath.PIHALF, 0f),
      BlockFacing.indexUP => new Vec3f(-GameMath.PIHALF, 0f, 0f),
      BlockFacing.indexDOWN => new Vec3f(GameMath.PIHALF, 0f, 0f),
      _ => new Vec3f(0f, 0f, 0f),
    };

  /// <summary>Tesselates the straight shaft body for <paramref name="block"/>, rotated to the block's shape
  /// orientation (the <c>ns</c>/<c>we</c> the chunk tesselator would otherwise bake in). Null if the shape is missing.</summary>
  public static MeshData? TesselateShaft(
    ICoreAPI api,
    ITesselatorAPI tesselator,
    Block block
  )
  {
    Shape? shape = api.Assets.Get<Shape>(ShaftShapeLoc);
    if (shape == null)
      return null;

    tesselator.TesselateShape(block, shape, out MeshData mesh);
    ExpandedLib.Helpers.ExMesh.RotateByShape(mesh, block);
    return mesh;
  }

  /// <summary>Tesselates one bevel gear rotated onto <paramref name="worldFace"/>. The face rotation is applied
  /// in the world frame (independent of the shaft axis), so callers must NOT also apply the shape rotation to it.
  /// Null if the shape is missing. Callers cache per face.</summary>
  public static MeshData? TesselateGear(
    ICoreAPI api,
    ITesselatorAPI tesselator,
    Block block,
    BlockFacing worldFace
  )
  {
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
