using ExpandedLib.Catalogues;
using ExpandedLib.Industry.Metals;
using ExpandedLib.Registries;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// Injects every registered code-first <see cref="ExBlockDef"/> into the server's asset manager as a
/// synthetic <c>blocktypes/</c> asset, so the vanilla object loader builds them like file assets
/// (variant expansion, atlas, block-ID assignment and client sync unchanged). Item and recipe
/// definitions inject through the same path.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
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

    // The stopping points of every stage route. Read from the catalogue assets rather than from
    // ProcessRouteRegistry, which is only populated at AssetsFinalize - and read here rather than there
    // because these items must exist before the object loader builds them. That ordering is why routes
    // are config assets and not item attributes.
    var routes = ProcessRouteLoader.Parse(
      ProcessRouteLoader.Read(api),
      out var routeErrors
    );
    var generated = ProcessItemEmitter.Emit(routes, out var skipped);
    foreach (ExItemDef def in generated)
      ExDefinitions.RegisterItem(def);

    // Reported here as well as at AssetsFinalize: a route that fails to parse generates no item, and the
    // finalize pass would only say the route was missing.
    foreach (string error in routeErrors)
      api.Logger.Error("[exlib] invalid stage route - " + error);
    foreach (string note in skipped)
      api.Logger.Notification(
        "[exlib] stage names a code it does not build - " + note
      );

    // A root key the typed API has no method for goes through RootKey/RootKeyByType, the escape
    // hatch; a real blocktype/itemtype key is read by the object loader, anything else is written into
    // the JSON and never read by anything. Checked here, once per def, rather than inside the builder,
    // since the builder does not know the installed game version's key set until KnownRootKeys does.
    foreach (ExBlockDef def in ExDefinitions.Blocks)
      foreach (string key in Audit(def))
        api.Logger.Warning(
          "[exlib] {0}: root key '{1}' is not a blocktype key the game reads",
          def.QualifiedCode,
          key
        );
    foreach (ExItemDef def in ExDefinitions.Items)
      foreach (string key in Audit(def))
        api.Logger.Warning(
          "[exlib] {0}: root key '{1}' is not an itemtype key the game reads",
          def.Domain + ":" + def.Code,
          key
        );

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

  /// <summary>The root keys of <paramref name="def"/>'s emitted JSON that are not a key the block
  /// loader reads (<see cref="KnownRootKeys.IsKnownBlockKey"/>). Empty when every key is known.
  /// Exposed so a test can exercise the check without a running <see cref="ExDefinitionModSystem"/>.
  /// </summary>
  internal static IReadOnlyList<string> Audit(ExBlockDef def) =>
    UnknownRootKeys(def.ToJson(), KnownRootKeys.IsKnownBlockKey);

  /// <summary>Item-side sibling of <see cref="Audit(ExBlockDef)"/>.</summary>
  internal static IReadOnlyList<string> Audit(ExItemDef def) =>
    UnknownRootKeys(def.ToJson(), KnownRootKeys.IsKnownItemKey);

  private static IReadOnlyList<string> UnknownRootKeys(
    JObject json,
    System.Func<string, bool> isKnown
  ) =>
    json.Properties()
      .Select(p => p.Name)
      .Where(key => !isKnown(key) && !IsByTypeSelector(key))
      .ToList();

  // RegistryObjectType.solveByType resolves any key ending "byType" (case-insensitive) generically,
  // substituting the wildcard-matched value under the key with the suffix stripped, before the loader
  // ever binds a field - so the un-suffixed key need not itself be one KnownRootKeys can see (some,
  // like collisionSelectionBoxesByType, bundle several fields into one). Not a real root key on its
  // own account; never flagged as unknown.
  private static bool IsByTypeSelector(string key) =>
    key.EndsWith("byType", System.StringComparison.OrdinalIgnoreCase);
}
