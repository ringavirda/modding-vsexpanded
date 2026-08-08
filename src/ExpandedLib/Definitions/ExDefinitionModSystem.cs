using ExpandedLib.Metals;
using ExpandedLib.Registries;
using Vintagestory.API.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// Injects every registered code-first <see cref="ExBlockDef"/> into the server's asset manager as a
/// synthetic <c>blocktypes/</c> asset, so the vanilla object loader builds them exactly like file
/// assets (variant expansion, atlas, block-ID assignment, client packet sync all unchanged).
/// <para>
/// <b>Timing (verified against the engine):</b> the loader (<c>ModRegistryObjectTypeLoader</c>) is
/// server-only and reads <c>blocktypes/</c> in the <c>AssetsLoaded</c> phase at <c>ExecuteOrder 0.2</c>;
/// JSON patches run first at <c>0.05</c>. Injecting in <c>AssetsLoaded</c> at <c>0.04</c> (below 0.05)
/// places the assets in the index <i>before</i> patches, so other mods can still patch them, and well
/// before the loader consumes them. By this phase <c>assetsByCategory["blocktypes"]</c> is already
/// populated (built in <c>AddExternalAssets</c> before the phase), so <c>Add</c> is safe.
/// </para>
/// </summary>
public class ExDefinitionModSystem : ModSystem
{
  // Server-only: blocktypes is a server-side category and the object loader is server-only; the client
  // receives the resolved block types over the network, so nothing to inject client-side.
  public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

  // Below the JSON patch loader (0.05) so injected assets are patch targets, above 0 so base assets
  // are already indexed.
  public override double ExecuteOrder() => 0.04;

  public override void AssetsLoaded(ICoreAPI api)
  {
    if (api.Side != EnumAppSide.Server)
      return;

    var origin = new ExDefinitionOrigin();

    // Register the generated metal-family resource items (ingot/plate/rod/nails/bits per opted-in metal)
    // so they inject through the same item path below. The catalogue is read directly here, not off
    // MetalRegistry: the registry is populated at AssetsFinalize, after this phase, so at injection time
    // it is still empty - the emitter must read config/metals itself.
    foreach (
      ExItemDef def in MetalFamilyEmitter.Emit(
        AssetCatalogueLoader.GetMany<MetalDef>(api, "config/metals/")
      )
    )
      ExDefinitions.RegisterItem(def);

    int blocks = 0;
    foreach (var (location, asset) in ExDefinitions.BuildBlockAssets(origin))
    {
      api.Assets.Add(location, asset);
      blocks++;
    }

    // itemtypes is a server-side category read by the same object loader (AssetsLoaded 0.2), so items
    // inject identically to blocks - one synthetic itemtypes/ asset each, before the loader consumes them.
    int items = 0;
    foreach (var (location, asset) in ExDefinitions.BuildItemAssets(origin))
    {
      api.Assets.Add(location, asset);
      items++;
    }

    // recipes/{category}/ are server-side categories read by the survival recipe loaders, which run after the
    // object loader (they resolve block/item codes the object loader has just built) - so injecting here at
    // 0.04 places the recipe files in the index well before any recipe loader consumes them.
    int recipes = 0;
    foreach (var (location, asset) in ExDefinitions.BuildRecipeAssets(origin))
    {
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
