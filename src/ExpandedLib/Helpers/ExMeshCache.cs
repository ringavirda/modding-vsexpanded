using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace ExpandedLib.Helpers;

/// <summary>
/// Shared cache for tesselated block-entity meshes, keyed by everything that changes the mesh.
/// <para>
/// <see cref="ITesselatorAPI.TesselateShape(string, Shape, out MeshData, ITexPositionSource, Vec3f, int,
/// byte, byte, int?, string[])"/>'s first argument is <c>typeForLogging</c>, used for error logging only
/// - vanilla's coal pile passes the literal <c>"coalpile"</c> for eight differently-sized shapes. A
/// descriptive string handed to it caches nothing, so a block entity that builds one and stops there
/// re-reads its shape JSON, deep-clones it and re-tesselates on **every** chunk re-tesselation, on the
/// tesselation thread. That string is the right cache key; this is what turns it into one.
/// </para>
/// </summary>
public static class ExMeshCache {
  // Everything lives under one prefix in the API's shared object cache, so a key collision with the game
  // or another mod is not possible without deliberately colliding here first.
  private const string Prefix = "exlib:mesh:";

  /// <summary>
  /// The mesh for <paramref name="key"/>, tesselating it through <paramref name="build"/> the first time
  /// it is asked for. The key must name every input the mesh depends on and nothing else: too little and
  /// two different meshes share an entry, too much and the cache never hits.
  /// <para>
  /// A null from <paramref name="build"/> is not remembered - the underlying cache treats a null entry as
  /// absent, so a shape that failed to load is retried on the next tesselation rather than leaving the
  /// block invisible until the game restarts.
  /// </para>
  /// <para>
  /// The returned mesh is shared by every caller of that key. Hand it straight to the mesher; the
  /// in-place transforms (<c>Rotate</c>, <c>Scale</c>, <c>Translate</c>) belong inside
  /// <paramref name="build"/>, or on a <c>Clone</c> if they vary per block entity.
  /// </para>
  /// </summary>
  public static MeshData? GetOrCreate(
    ICoreClientAPI capi,
    string key,
    Func<MeshData?> build
  ) =>
    ObjectCacheUtil.GetOrCreate<MeshData?>(capi, Prefix + key, () => build());

  /// <summary>
  /// The mesh for one variant of <paramref name="block"/>, with the block's own code folded into the key.
  /// Prefer this over the raw overload: two blocktypes whose variant keys happen to read the same would
  /// otherwise share a mesh, and the symptom - one block rendering as another - looks nothing like a
  /// caching bug.
  /// </summary>
  public static MeshData? GetOrCreate(
    ICoreClientAPI capi,
    Block block,
    string variantKey,
    Func<MeshData?> build
  ) => GetOrCreate(capi, $"{block.Code}|{variantKey}", build);

  /// <summary>
  /// Drops one cached mesh, so the next request rebuilds it. Only needed when something the key does not
  /// name has changed - a reloaded asset, say. A mesh that varies with block state belongs behind a key
  /// that says so, not behind an invalidation call.
  /// </summary>
  public static void Invalidate(ICoreAPI api, string key) =>
    ObjectCacheUtil.Delete(api, Prefix + key);

  /// <summary>
  /// Loads a shape asset the way the game does: through <see cref="Shape.TryGet(ICoreAPI, AssetLocation)"/>,
  /// which catches and logs a deserialisation failure instead of throwing it on the tesselation thread
  /// where it is much harder to attribute, and sets the element-level logging path so a malformed element
  /// names its file. Returns null when the asset is missing or will not parse.
  /// </summary>
  public static Shape? LoadShape(ICoreAPI api, AssetLocation shapePath) =>
    Shape.TryGet(api, shapePath);

  /// <summary>The full asset path of a block's own shape, prefixed and suffixed as the asset system wants
  /// it. The <c>Clone</c> matters: <c>WithPathPrefixOnce</c> mutates in place, and the block's shape is
  /// shared by every instance of that blocktype.</summary>
  public static AssetLocation ShapePathOf(Block block) =>
    block
      .Shape.Base.Clone()
      .WithPathPrefixOnce("shapes/")
      .WithPathAppendixOnce(".json");
}
