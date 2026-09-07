using System.Collections.Generic;
using ExpandedLib.Catalogues;
using ExpandedLib.Helpers;
using ExpandedLib.Registries;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Forming.Items;

/// <summary>
/// A piece of rolling stock, drawn at the gauge it has been worked to, so its state is legible in the hand
/// between passes. Two routes: the stage the family's shape file draws, and - for the half-step a round
/// lands, which no route declares - the form's base shape scaled from the same numbers the simulation uses
/// (<see cref="StockMesh.ScaleOf"/>), which costs one shape per form instead of one per state. Meshes are
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
      || StockMesh.IsBaseState(piece)
    )
      return;

    // Cached on geometry alone - never on anything belonging to the stack. The handbook clones the stack
    // every frame, so a per-stack key would upload a mesh per frame and leak every one.
    string key = StockMesh.CacheKey(piece);
    if (!_meshes.TryGetValue(key, out MultiTextureMeshRef? mesh)) {
      mesh = Build(capi, piece);
      if (mesh == null)
        return;
      _meshes[key] = mesh;
    }
    renderinfo.ModelRef = mesh;
  }

  /// <summary>
  /// The mesh for a worked piece: the stage the family's shape file draws it at when the route has one,
  /// and the composed mesh otherwise. Drawn art wins because it carries detail the scale cannot - a groove
  /// is not a thinner rectangle - while the composition covers every gauge nobody has drawn.
  /// </summary>
  private MultiTextureMeshRef? Build(ICoreClientAPI capi, WorkPiece piece) {
    ProcessRoute? route = ProcessRouteRegistry.Shared.Route(piece.Form.Name);
    return StockMesh.ElementFor(route, piece) is { } element
      ? Drawn(capi, route!.Shape!, element)
      : Compose(capi, piece);
  }

  /// <summary>Tesselates one element of the family's shape file - the stage as it was drawn.</summary>
  // The texture source is the item's own rather than the shape file's: `ShapeTextureSource` does not
  // exist on 1.20, and this is the route every other tesselation site here already takes.
  private MultiTextureMeshRef? Drawn(
    ICoreClientAPI capi,
    string shape,
    string element
  ) {
    Shape? loaded = ExMeshCache.LoadShape(
      capi,
      new AssetLocation(shape)
        .WithPathPrefixOnce("shapes/")
        .WithPathAppendixOnce(".json")
    );
    if (loaded == null)
      return null;

    // The shape holds the whole family, so only this stage's element is kept. Prefix matching is the
    // engine's own rule here, which is why the stages are authored as flat, distinctly-named elements.
    capi.Tesselator.TesselateShape(
      "stock stage " + element,
      loaded,
      out MeshData? mesh,
      capi.Tesselator.GetTextureSource(this),
      null,
      0,
      0,
      0,
      null,
      [element]
    );
    return mesh == null ? null : capi.Render.UploadMultiTextureMesh(mesh);
  }

  /// <summary>Tesselates the base shape and scales it to the piece's own section and length.</summary>
  private MultiTextureMeshRef? Compose(ICoreClientAPI capi, WorkPiece piece) {
    capi.Tesselator.TesselateItem(this, out MeshData? baseMesh);
    if (baseMesh == null)
      return null;

    // Cloned before scaling. Nothing promises the tesselator hands back a mesh nobody else holds, and
    // scaling one that is shared would resize every other piece of stock drawn from the same shape.
    MeshData mesh = baseMesh.Clone();
    Vec3f scale = StockMesh.ScaleOf(piece);
    mesh.Scale(
      new Vec3f(StockMesh.CentreX / 16f, 0f, 0.5f),
      scale.X,
      scale.Y,
      scale.Z
    );
    return capi.Render.UploadMultiTextureMesh(mesh);
  }

  public override void OnUnloaded(ICoreAPI api) {
    foreach (MultiTextureMeshRef mesh in _meshes.Values)
      mesh.Dispose();
    _meshes.Clear();
    base.OnUnloaded(api);
  }
}
