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
    // Keyed on the block code alone: the body mesh is a pure function of the blocktype's shape and its
    // rotateY, and the engine snapping a generator onto another axis does it with ExchangeBlock, so the
    // new orientation arrives as a different code and misses this entry rather than reusing it.
    if (
      Api is ICoreClientAPI capi
      && ExMeshCache.GetOrCreate(
        capi,
        Block,
        "body",
        () => BuildBody(tesselator)
      )
        is { } body
    )
      mesher.AddMeshData(body);

    base.OnTesselation(mesher, tesselator);
    return true;
  }

  private MeshData? BuildBody(ITesselatorAPI tesselator) {
    if (
      ExMeshCache.LoadShape(Api, ExMeshCache.ShapePathOf(Block))
      is not { } shape
    )
      return null;

    // Render the whole body except the Axle* elements; vanilla's MP renderer spins those.
    Shape body = shape.Clone();
    body.Elements = body
      .Elements.Where(e => !e.Name?.StartsWith("Axle") ?? true)
      .ToArray();
    tesselator.TesselateShape(Block, body, out MeshData mesh);
    // Inside the factory, not on the way out: the cached mesh is shared by every instance of this
    // blocktype and Rotate mutates in place, so rotating a returned one turns the body a further
    // rotateY on every tesselation.
    ExMesh.RotateByShape(mesh, Block);
    return mesh;
  }
}
