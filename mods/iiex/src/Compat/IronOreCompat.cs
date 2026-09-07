using ExpandedLib.Catalogues;
using ExpandedLib.Industry.Materials;
using Vintagestory.API.Common;

namespace IronIndustryExpanded.Compat;

/// <summary>
/// The blast furnace's iron-ore feed test. Every ore code - the base <c>crushed-iron</c> prefix and the
/// per-mod ones alike - now ships as data in <c>assets/iiex/config/materialroles.json</c>, each gated by
/// <c>requiresMod</c>, so supporting a further mod's ore is a JSON row rather than a branch in here.
/// <para>
/// The code contributor this class used to register is gone with them
/// (<see cref="MaterialRoleRegistry.RegisterContributor"/> remains, for a registration that genuinely
/// cannot be expressed as data).
/// </para>
/// </summary>
public static class IronOreCompat {
  /// <summary>
  /// True if <paramref name="path"/> is a recognised crushed-iron-ore item path for the blast furnace
  /// feed, resolved through <see cref="MaterialRoleRegistry"/>. Callers pass the item's
  /// <see cref="AssetLocation.Path"/>; matching is domain-blind.
  /// </summary>
  public static bool IsCrushedIronOre(string path) =>
    MaterialRoleRegistry.IsRole(Roles.IronOre, new AssetLocation(path));
}
