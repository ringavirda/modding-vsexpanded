using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Helpers;

/// <summary>
/// Generic mechanism for a config-gated "disable this content" toggle: hide registered blocks/items
/// from the creative inventory and handbook. The caller (a mod deciding a feature is off) supplies
/// the predicate that selects what to gate; this owns the how. Call after content has resolved -
/// i.e. from a mod system's <c>StartServerSide</c>/<c>StartClientSide</c>, not <c>Start</c>.
/// </summary>
public static class ExContentGate
{
  /// <summary>
  /// Hides every collectible matching <paramref name="match"/> from the creative inventory and the
  /// handbook by clearing its creative tabs and stacks - the handbook lists nothing for a collectible
  /// that has neither (see <c>CollectibleObject.GetHandBookStacks</c>). Returns the number hidden.
  /// The creative inventory and handbook are client-built, so this matters on the client; it is a
  /// harmless no-op effect on the server.
  /// </summary>
  public static int HideFromCreativeAndHandbook(
    ICoreAPI api,
    System.Func<CollectibleObject, bool> match
  )
  {
    int hidden = 0;
    foreach (var obj in AllCollectibles(api))
    {
      if (obj?.Code == null || !match(obj))
        continue;
      obj.CreativeInventoryTabs = null;
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
