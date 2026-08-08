using System.Collections.Generic;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Forming.Items;

/// <summary>
/// A piece of rolling stock, drawn to match how far each side has been worked, so its state is legible in
/// the hand between passes. The mesh is composed rather than authored: the form's base shape is
/// tesselated once and then scaled per side from the same numbers the simulation uses
/// (<see cref="StockMesh.SideOf"/>), which costs one shape per form instead of one per state. Meshes are
/// cached by geometry alone (<see cref="StockMesh.CacheKey"/>), a handful of entries per form, not per stack.
/// </summary>
[ItemRegister]
public class ItemStockPiece : Item {
  private readonly Dictionary<string, MultiTextureMeshRef> _meshes = [];

  public override void OnBeforeRender(
    ICoreClientAPI capi,
    ItemStack itemstack,
    EnumItemRenderTarget target,
    ref ItemRenderInfo renderinfo
  ) {
    base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

    // An unworked piece is exactly its base shape, so leave the default mesh alone.
    if (
      WorkPiece.FromStack(itemstack) is not { } piece
      || piece.Sides <= 1 && piece.IsEven
    )
      return;

    string key = StockMesh.CacheKey(piece);
    if (!_meshes.TryGetValue(key, out MultiTextureMeshRef? mesh)) {
      mesh = Compose(capi, piece);
      if (mesh == null)
        return;
      _meshes[key] = mesh;
    }
    renderinfo.ModelRef = mesh;
  }

  /// <summary>Tesselates the base shape once and lays a scaled copy down for each side of the piece.</summary>
  private MultiTextureMeshRef? Compose(ICoreClientAPI capi, WorkPiece piece) {
    capi.Tesselator.TesselateItem(this, out MeshData? baseMesh);
    if (baseMesh == null)
      return null;

    MeshData composed = new(4, 3);
    var origin = new Vec3f(StockMesh.CentreX / 16f, 0f, 0.5f);
    for (int side = 0; side < piece.Sides; side++) {
      SidePlacement placement = StockMesh.SideOf(piece, side);
      MeshData part = baseMesh.Clone();
      part.Scale(
        origin,
        placement.Scale.X,
        placement.Scale.Y,
        placement.Scale.Z
      );
      part.Translate(placement.OffsetX / 16f, 0f, 0f);
      composed.AddMeshData(part);
    }
    return capi.Render.UploadMultiTextureMesh(composed);
  }

  public override void OnUnloaded(ICoreAPI api) {
    foreach (MultiTextureMeshRef mesh in _meshes.Values)
      mesh.Dispose();
    _meshes.Clear();
    base.OnUnloaded(api);
  }
}
