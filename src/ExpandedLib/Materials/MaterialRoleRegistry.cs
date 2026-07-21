using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ExpandedLib.Materials;

/// <summary>
/// Process-wide catalogue of material-role assignments (<see cref="MaterialRoleDef"/>) - the single
/// surface the mixer, both hopper families and the furnace core consult instead of hardcoding
/// <c>Collectible.Code.Path</c> equality to tell flux / fuel / ore / scrap / charge apart. Mirrors
/// <see cref="Metals.MetalRegistry"/>: populated at <c>AssetsFinalize</c> by
/// <see cref="MaterialRoleLoader"/> from every domain's <c>config/materialroles.json</c>, plus any
/// registered code contributor (for the mod-gating JSON cannot express - see
/// <see cref="RegisterContributor"/>). Kept world-free so it is unit-testable headless.
/// <para>
/// Matching mirrors <see cref="MaterialRoleDef"/>: an item has a role if any def under that role matches
/// it by exact domain-normalised <see cref="MaterialRoleDef.Code"/> or by domain-blind
/// <see cref="MaterialRoleDef.PathPrefix"/>. Empty registry ⇒ nothing has a role, so a machine that
/// reads the registry before it loads (or a mis-ordered load) simply classifies nothing rather than
/// throwing.
/// </para>
/// </summary>
public static class MaterialRoleRegistry
{
  // role token (case-insensitive) -> the defs granting it. A role can be granted by several defs
  // (e.g. fuel = coke + charcoal; ironore = the crushed-iron prefix + per-mod ore codes).
  private static readonly Dictionary<string, List<MaterialRoleDef>> _byRole =
    new(StringComparer.OrdinalIgnoreCase);

  // Code contributors run after the JSON overlay on every load, so a mod-gated registration
  // (IronOreCompat's IsModEnabled branches) survives the loader's Clear and re-applies each world load.
  private static readonly List<Action<ICoreAPI>> _contributors = new();

  #region Registration
  /// <summary>Registers a role assignment. A def with no role, or with neither code nor path prefix,
  /// is ignored (it could never match).</summary>
  public static void Register(MaterialRoleDef def)
  {
    if (
      def == null
      || string.IsNullOrEmpty(def.Role)
      || (string.IsNullOrEmpty(def.Code) && string.IsNullOrEmpty(def.PathPrefix))
    )
      return;
    if (!_byRole.TryGetValue(def.Role, out List<MaterialRoleDef>? list))
      _byRole[def.Role] = list = new List<MaterialRoleDef>();
    list.Add(def);
  }

  /// <summary>Drops every registered role assignment. The loader clears before repopulating on each
  /// world load (contributors are kept - they are re-invoked after the clear).</summary>
  public static void Clear() => _byRole.Clear();

  /// <summary>Registers a code contributor invoked (with the api) after the JSON overlay on every
  /// <see cref="MaterialRoleLoader.Load"/> - the seam a mod uses for role registrations JSON cannot
  /// express, e.g. gating on <c>ModLoader.IsModEnabled</c>. Call once from the mod's <c>Start</c>.</summary>
  public static void RegisterContributor(Action<ICoreAPI> contributor)
  {
    if (contributor != null)
      _contributors.Add(contributor);
  }

  /// <summary>Drops every registered contributor (test isolation only - the game registers once per process).</summary>
  public static void ClearContributors() => _contributors.Clear();

  /// <summary>Runs every registered contributor against <paramref name="api"/> (called by the loader
  /// after clearing + overlaying JSON).</summary>
  internal static void InvokeContributors(ICoreAPI api)
  {
    foreach (Action<ICoreAPI> contributor in _contributors)
      contributor(api);
  }
  #endregion

  #region Lookup
  /// <summary>True when <paramref name="loc"/> has role <paramref name="role"/> (by exact code or path prefix).</summary>
  public static bool IsRole(string role, AssetLocation loc)
  {
    if (loc == null || !_byRole.TryGetValue(role, out List<MaterialRoleDef>? list))
      return false;
    string norm = Normalize(loc);
    foreach (MaterialRoleDef def in list)
      if (Matches(def, norm, loc.Path))
        return true;
    return false;
  }

  /// <summary>True when a stack's collectible has role <paramref name="role"/>. Null-safe.</summary>
  public static bool IsRole(string role, ItemStack? stack) =>
    stack?.Collectible?.Code != null && IsRole(role, stack.Collectible.Code);

  /// <summary>The <see cref="MaterialRoleDef.Value"/> of the first def of <paramref name="role"/> that
  /// matches <paramref name="loc"/> and carries a value; <paramref name="fallback"/> when none does.</summary>
  public static float ValueOf(string role, AssetLocation loc, float fallback = 1f)
  {
    if (loc != null && _byRole.TryGetValue(role, out List<MaterialRoleDef>? list))
    {
      string norm = Normalize(loc);
      foreach (MaterialRoleDef def in list)
        if (Matches(def, norm, loc.Path) && def.Value.HasValue)
          return def.Value.Value;
    }
    return fallback;
  }

  /// <summary>The value of the role a stack's collectible carries. Null-safe.</summary>
  public static float ValueOf(string role, ItemStack? stack, float fallback = 1f) =>
    stack?.Collectible?.Code != null
      ? ValueOf(role, stack.Collectible.Code, fallback)
      : fallback;

  /// <summary>Every def registered under <paramref name="role"/> (empty when none).</summary>
  public static IEnumerable<MaterialRoleDef> OfRole(string role) =>
    _byRole.TryGetValue(role, out List<MaterialRoleDef>? list)
      ? list
      : Array.Empty<MaterialRoleDef>();
  #endregion

  #region Matching + normalisation (mirrors MetalRegistry)
  // Code is exact + domain-normalised; PathPrefix is a domain-blind StartsWith on the path segment.
  private static bool Matches(MaterialRoleDef def, string normLoc, string path) =>
    (def.Code != null && Normalize(def.Code) == normLoc)
    || (
      def.PathPrefix != null
      && path.StartsWith(def.PathPrefix, StringComparison.Ordinal)
    );

  // Domain-normalise so "lime" (defaulting to game) and "game:lime" key alike.
  private static string Normalize(string code) => new AssetLocation(code).ToString();

  private static string Normalize(AssetLocation loc) => loc.ToString();
  #endregion
}
