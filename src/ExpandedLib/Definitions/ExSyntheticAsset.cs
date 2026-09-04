using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// Builds the in-memory asset a code-first definition is injected as. The object loader
/// (<c>ModRegistryObjectTypeLoader</c>) iterates blocktypes as the concrete <see cref="Asset"/> type, so
/// an injected asset must be a real <see cref="Asset"/>: a custom <see cref="IAsset"/> throws
/// <see cref="System.InvalidCastException"/> and aborts the <c>AssetsLoaded</c> phase. That contract is
/// why exlib references <c>VintagestoryLib</c>. The asset is built already-loaded (<c>Data</c> set), so
/// its origin is never asked to load it.
/// </summary>
internal static class ExSyntheticAsset {
  /// <summary>Constructs a real engine <see cref="Asset"/> carrying <paramref name="data"/> at
  /// <paramref name="location"/>, stamped with <paramref name="origin"/>.</summary>
  public static IAsset Create(
    AssetLocation location,
    byte[] data,
    IAssetOrigin origin
  ) => new Asset(data, location, origin);
}

/// <summary>
/// The <see cref="IAssetOrigin"/> stamped on injected assets. Injection goes through
/// <c>AssetManager.Add</c> rather than origin enumeration and the asset is constructed already-loaded,
/// so the load hooks are never called and <c>GetAssets</c> returns nothing; the origin exists only so
/// <see cref="IAsset.Origin"/> is non-null and gameplay-allowed.
/// </summary>
internal sealed class ExDefinitionOrigin : IAssetOrigin {
  public string OriginPath => "exlib:code-first-definitions";

  public void LoadAsset(IAsset asset) { }

  public bool TryLoadAsset(IAsset asset) => true;

  public List<IAsset> GetAssets(
    AssetCategory category,
    bool shouldLoad = true
  ) => [];

  public List<IAsset> GetAssets(
    AssetLocation baseLocation,
    bool shouldLoad = true
  ) => [];

  // Blocktypes and itemtypes are gameplay-affecting categories: an origin returning false is skipped
  // for them. Only reached if this origin is ever enumerated rather than used via Add.
  public bool IsAllowedToAffectGameplay() => true;
}
