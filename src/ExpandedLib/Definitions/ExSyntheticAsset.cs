using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// Builds the in-memory asset a code-first definition is injected as. The engine's object loader
/// (<c>ModRegistryObjectTypeLoader</c>) reads blocktypes via <c>GetMany&lt;JObject&gt;</c>, whose loop
/// variable is the <b>concrete</b> <see cref="Asset"/> (<c>foreach (Asset item in …)</c>) - so an
/// injected asset MUST be a real <see cref="Asset"/>; a custom <see cref="IAsset"/> throws
/// <see cref="System.InvalidCastException"/> and aborts the whole <c>AssetsLoaded</c> phase. That is
/// why exlib references <c>VintagestoryLib</c> here (the API alone can't express the loader's contract).
/// The asset is built already-loaded (its <c>Data</c> is set), so its origin is never asked to load it.
/// </summary>
internal static class ExSyntheticAsset
{
  /// <summary>Constructs a real engine <see cref="Asset"/> carrying <paramref name="data"/> at
  /// <paramref name="location"/>, stamped with <paramref name="origin"/>.</summary>
  public static IAsset Create(
    AssetLocation location,
    byte[] data,
    IAssetOrigin origin
  ) => new Asset(data, location, origin);
}

/// <summary>
/// The <see cref="IAssetOrigin"/> back-reference stamped on injected assets. Injection goes through
/// <c>AssetManager.Add</c>, not origin enumeration, and the asset is constructed already-loaded, so the
/// origin's load hooks are never called - it exists only so <see cref="IAsset.Origin"/> is non-null and
/// gameplay-allowed. Its <c>GetAssets</c> returns nothing.
/// </summary>
internal sealed class ExDefinitionOrigin : IAssetOrigin
{
  public string OriginPath => "exlib:code-first-definitions";

  public void LoadAsset(IAsset asset) { }

  public bool TryLoadAsset(IAsset asset) => true;

  public List<IAsset> GetAssets(AssetCategory category, bool shouldLoad = true) =>
    [];

  public List<IAsset> GetAssets(
    AssetLocation baseLocation,
    bool shouldLoad = true
  ) => [];

  // Blocktypes/itemtypes are gameplay-affecting categories; an origin that returns false here would be
  // skipped for them (relevant only if this origin is ever enumerated rather than used via Add).
  public bool IsAllowedToAffectGameplay() => true;
}
