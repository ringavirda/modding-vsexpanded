using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Registries;

/// <summary>
/// The three rungs a mod needs to react to another mod being installed: a one-liner check
/// (<see cref="IsLoaded"/>, <see cref="AtLeast"/>), a run-now callback (<see cref="WhenLoaded"/>) and
/// a JSON patch condition (<see cref="FlagKey"/>), backed by the world-config flags
/// <see cref="ExModsModSystem"/> sets for every enabled mod.
/// </summary>
public static class ExMods {
  /// <summary>
  /// True when <paramref name="modId"/> is loaded and enabled on <paramref name="api"/>'s side. Never
  /// throws: false for a null or blank id, and false for an id no installed mod carries.
  /// </summary>
  public static bool IsLoaded(ICoreAPI api, string modId) =>
    !string.IsNullOrWhiteSpace(modId) && api.ModLoader.IsModEnabled(modId);

  /// <summary>The loaded mod's version string (e.g. <c>"1.9.0-rc.1"</c>), or null when
  /// <paramref name="modId"/> is not loaded.</summary>
  public static string? Version(ICoreAPI api, string modId) =>
    IsLoaded(api, modId) ? api.ModLoader.GetMod(modId).Info.Version : null;

  /// <summary>
  /// True when <paramref name="modId"/> is loaded and its version is at least
  /// <paramref name="minimumVersion"/>; false when the mod is absent or its version is older.
  /// Compares the way the game itself does (<see cref="GameVersion.IsAtLeastVersion(string, string)"/>),
  /// so a pre-release tag such as <c>"1.9.0-rc.1"</c> sorts below its release <c>"1.9.0"</c>.
  /// </summary>
  public static bool AtLeast(ICoreAPI api, string modId, string minimumVersion) {
    string? version = Version(api, modId);
    return version != null
      && GameVersion.IsAtLeastVersion(version, minimumVersion);
  }

  /// <summary>Runs <paramref name="action"/> immediately when <paramref name="modId"/> is loaded,
  /// otherwise does nothing. Returns whether it ran.</summary>
  public static bool WhenLoaded(ICoreAPI api, string modId, Action action) {
    if (!IsLoaded(api, modId))
      return false;
    action();
    return true;
  }

  /// <summary>
  /// The world-config key <see cref="ExModsModSystem"/> sets to <c>true</c> for every enabled mod:
  /// <c>"exlib:mod:&lt;modid&gt;"</c>. Usable in a JSON patch condition without any C# code:
  /// <c>{ "when": "exlib:mod:toolsmith", "isValue": "true" }</c>.
  /// </summary>
  public static string FlagKey(string modId) => "exlib:mod:" + modId;
}
