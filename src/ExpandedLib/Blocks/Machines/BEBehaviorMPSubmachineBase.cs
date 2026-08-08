using System.Linq;
using ExpandedLib.Helpers;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace ExpandedLib.Blocks.Machines;

/// <summary>
/// Shared base for a mechanical-power node whose block renders a static body plus a vanilla-spun axle
/// (the Bessemer transmission, the engine MP generator). Vanilla's MP renderer draws only the rotating
/// <c>Axle*</c> elements selected by <see cref="GetShape"/>, so this base tesselates and caches the rest
/// of the shape itself and sets the per-axis <see cref="BEBehaviorMPBase.AxisSign"/>. A subclass supplies
/// its discovery-face mapping (<see cref="ResolveDiscoveryFace"/>) and its producer/consumer specifics.
/// </summary>
public abstract class BEBehaviorMPSubmachineBase(BlockEntity blockentity)
  : BEBehaviorMPBase(blockentity) {
  private MeshData? _baseMesh;

  /// <summary>
  /// The network-discovery face in the placed orientation. The base seeds
  /// <see cref="BEBehaviorMPBase.OutFacingForNetworkDiscovery"/> from it and derives the axle's rotation
  /// sense per axis. The generator seeds from the back of the axis so vanilla's
  /// <c>IsRotationReversed</c> matches the engine's beam linkage.
  /// </summary>
  protected abstract BlockFacing ResolveDiscoveryFace();

  public override void SetOrientations() {
    OutFacingForNetworkDiscovery = ResolveDiscoveryFace();

    // One sign per axis, not per facing: opposite facings on an axis (north/south, east/west) share an
    // axle line and must not counter-rotate. A signed facing normal would flip the rendered sense for
    // half the orientations.
    AxisSign =
      OutFacingForNetworkDiscovery.Axis == EnumAxis.X ? [-1, 0, 0] : [0, 0, -1];
  }

  /// <summary>Drops the cached static body mesh so the next tesselation rebuilds it. Call after an
  /// in-place orientation change, as the generator does when the engine snaps it.</summary>
  protected void ResetBaseMesh() => _baseMesh = null;

  protected override CompositeShape GetShape() =>
    new() {
      Base = Block.Shape.Base.Clone(),
      SelectiveElements = ["Axle*"],
      rotateY = Block.Shape.rotateY,
      InsertBakedTextures = true,
    };

  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  ) {
    if (_baseMesh == null) {
      AssetLocation shapeLoc = Block
        .Shape.Base.WithPathPrefixOnce("shapes/")
        .WithPathAppendixOnce(".json");
      Shape? shape = Api.Assets.TryGet(shapeLoc)?.ToObject<Shape>();
      if (shape != null) {
        // Render the whole body except the Axle* elements; vanilla's MP renderer spins those.
        Shape baseShape = shape.Clone();
        baseShape.Elements = baseShape
          .Elements.Where(e => !e.Name?.StartsWith("Axle") ?? true)
          .ToArray();
        tesselator.TesselateShape(Block, baseShape, out _baseMesh);
        ExMesh.RotateByShape(_baseMesh, Block);
      }
    }

    if (_baseMesh != null)
      mesher.AddMeshData(_baseMesh);

    base.OnTesselation(mesher, tesselator);
    return true;
  }
}
