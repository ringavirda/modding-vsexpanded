using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Helpers;

/// <summary>
/// Config-gated "disable this content" toggle: hides registered blocks and items from the creative
/// inventory and handbook. The caller supplies the predicate selecting what to gate. Call after content
/// has resolved, from a mod system's <c>StartServerSide</c>/<c>StartClientSide</c>, not <c>Start</c>.
/// </summary>
public static class ExContentGate {
  /// <summary>
  /// Hides every collectible matching <paramref name="match"/> from the creative inventory and handbook
  /// by clearing its creative tabs and stacks; <c>CollectibleObject.GetHandBookStacks</c> lists nothing
  /// for a collectible with neither. Returns the number hidden. Both are client-built, so this has
  /// effect on the client and is a harmless no-op on the server.
  /// </summary>
  public static int HideFromCreativeAndHandbook(
    ICoreAPI api,
    System.Func<CollectibleObject, bool> match
  ) {
    int hidden = 0;
    foreach (var obj in AllCollectibles(api)) {
      if (obj?.Code == null || !match(obj))
        continue;
      // Empty, not null: vanilla reads the tab list without a null guard in places
      // (BehaviorAttachable.cs:646 does `CreativeInventoryTabs.Length == 0`), so nulling it throws
      // on anything that enumerates collectibles. Length 0 hides the item just as well.
      obj.CreativeInventoryTabs = [];
      obj.CreativeInventoryStacks = null;
      hidden++;
    }
    return hidden;
  }

  private static IEnumerable<CollectibleObject> AllCollectibles(ICoreAPI api) =>
    api
      .World.Blocks.Cast<CollectibleObject>()
      .Concat(api.World.Items.Cast<CollectibleObject>());
}
