using ExpandedLib.Metals;
using ExpandedLib.Registries;
using Vintagestory.API.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// Injects every registered code-first <see cref="ExBlockDef"/> into the server's asset manager as a
/// synthetic <c>blocktypes/</c> asset, so the vanilla object loader builds them like file assets
/// (variant expansion, atlas, block-ID assignment and client sync unchanged). Item and recipe
/// definitions inject through the same path.
/// </summary>
public class ExDefinitionModSystem : ModSystem {
  // blocktypes is a server-side category and the object loader is server-only; the client receives the
  // resolved block types over the network.
  public override bool ShouldLoad(EnumAppSide side) =>
    side == EnumAppSide.Server;

  // Below the JSON patch loader (0.05) so injected assets stay patchable by other mods, well below the
  // object loader (0.2) that consumes them, and above 0 so base assets are already indexed.
  public override double ExecuteOrder() => 0.04;

  public override void AssetsLoaded(ICoreAPI api) {
    if (api.Side != EnumAppSide.Server)
      return;

    var origin = new ExDefinitionOrigin();

    // Register the generated metal-family resource items (ingot/plate/rod/nails/bits per opted-in metal)
    // so they inject through the same item path below. The catalogue is read directly rather than off
    // MetalRegistry, which is only populated at AssetsFinalize and so is still empty in this phase.
    foreach (
      ExItemDef def in MetalFamilyEmitter.Emit(
        AssetCatalogueLoader.GetMany<MetalDef>(api, "config/metals/")
      )
    )
      ExDefinitions.RegisterItem(def);

    int blocks = 0;
    foreach (var (location, asset) in ExDefinitions.BuildBlockAssets(origin)) {
      api.Assets.Add(location, asset);
      blocks++;
    }

    // itemtypes is read by the same object loader at 0.2, so items inject identically to blocks: one
    // synthetic itemtypes/ asset each.
    int items = 0;
    foreach (var (location, asset) in ExDefinitions.BuildItemAssets(origin)) {
      api.Assets.Add(location, asset);
      items++;
    }

    // recipes/{category}/ are read by the survival recipe loaders, which run after the object loader
    // because they resolve the block and item codes it builds, so injection here precedes them.
    int recipes = 0;
    foreach (var (location, asset) in ExDefinitions.BuildRecipeAssets(origin)) {
      api.Assets.Add(location, asset);
      recipes++;
    }

    if (blocks > 0)
      api.Logger.Notification(
        "[exlib] Injected {0} code-first block definition(s).",
        blocks
      );
    if (items > 0)
      api.Logger.Notification(
        "[exlib] Injected {0} code-first item definition(s).",
        items
      );
    if (recipes > 0)
      api.Logger.Notification(
        "[exlib] Injected {0} code-first recipe file(s).",
        recipes
      );
  }
}
