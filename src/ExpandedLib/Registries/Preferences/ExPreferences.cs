using System;
using System.Collections.Generic;
using ExpandedLib.Registries.Config;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries.Preferences;

/// <summary>
/// On-disk shape of the per-player preferences file: for each player UID, a map of preference key to
/// chosen value. Keyed by UID so every player on a server keeps their own choices.
/// </summary>
public class ExPreferencesConfig {
  /// <summary>playerUID -&gt; (preferenceKey -&gt; value).</summary>
  public Dictionary<string, Dictionary<string, string>> Players { get; set; } =
  [];
}

/// <summary>
/// Store for per-player display preferences shared by every Expanded mod (e.g. iiex's
/// metric/imperial unit system). Definitions register through <see cref="PreferenceRegistry"/>; this
/// class persists each player's choices in <see cref="ConfigFileName"/> and applies them to live
/// client state via <see cref="IExPreference.Apply"/>.
/// <para>
/// Client-side only: the look-at HUD and handbook render on the client. A client loads the config on
/// startup, applies the local player's saved choices on join, and updates them through the
/// <c>.exmod</c> command.
/// </para>
/// </summary>
public static class ExPreferences {
  /// <summary>Config file name, written under the game's <c>ModConfig</c> folder. One file for all
  /// preferences: one entry per player, each with one value per preference.</summary>
  public const string ConfigFileName = "exmod_preferences.json";

  /// <summary>Former names of <see cref="ConfigFileName"/>, renamed on load so saved preferences
  /// carry over instead of regenerating empty.</summary>
  private static readonly string[] _legacyFileNames = ["exmod.json"];

  private static readonly Dictionary<string, IExPreference> _registered = new();
  private static ExPreferencesConfig _config = new();
  private static ICoreAPI? _api;

  /// <summary>Every registered preference definition, in registration order.</summary>
  public static IEnumerable<IExPreference> All => _registered.Values;

  /// <summary>Adds a preference definition. Called by <see cref="PreferenceRegistry"/>; a later
  /// registration with the same <see cref="IExPreference.Key"/> replaces the earlier one.</summary>
  public static void Register(IExPreference preference) =>
    _registered[preference.Key] = preference;

  /// <summary>The registered preference for <paramref name="key"/>, or <c>null</c> if none.</summary>
  public static IExPreference? Find(string key) =>
    _registered.TryGetValue(key, out var p) ? p : null;

  /// <summary>Loads <see cref="ConfigFileName"/> (falling back to defaults) and writes it back so
  /// the file is created on first run. Call once on the client during startup, before applying any
  /// player's choices.</summary>
  public static void LoadConfig(ICoreAPI api) {
    _api = api;
    ExConfigFiles.RenameLegacy(api, "exlib", ConfigFileName, _legacyFileNames);
    try {
      _config =
        api.LoadModConfig<ExPreferencesConfig>(ConfigFileName)
        ?? new ExPreferencesConfig();
    } catch (Exception e) {
      api.Logger.Warning(
        "[exlib] Failed to read {0}; using defaults. {1}",
        ConfigFileName,
        e
      );
      _config = new ExPreferencesConfig();
    }
    Save();
  }

  /// <summary>The saved value of a preference for a player, or the preference's
  /// <see cref="IExPreference.Default"/> when none is stored (or the key is unknown).</summary>
  public static string GetForPlayer(string playerUid, string key) {
    if (
      _config.Players.TryGetValue(playerUid, out var prefs)
      && prefs.TryGetValue(key, out var value)
    )
      return value;
    return Find(key)?.Default ?? string.Empty;
  }

  /// <summary>Stores and persists a player's choice and applies it to live client state. Call
  /// client-side for the local player (the <c>.exmod</c> command does this).</summary>
  public static void SetForPlayer(string playerUid, string key, string value) {
    if (!_config.Players.TryGetValue(playerUid, out var prefs))
      _config.Players[playerUid] = prefs = new Dictionary<string, string>();
    prefs[key] = value;
    Find(key)?.Apply(value);
    Save();
  }

  /// <summary>Applies every registered preference's saved value for a player to live client state.
  /// Call client-side for the local player once the world is ready (e.g. on join).</summary>
  public static void ApplyForPlayer(string playerUid) {
    foreach (var pref in _registered.Values)
      pref.Apply(GetForPlayer(playerUid, pref.Key));
  }

  private static void Save() {
    try {
      _api?.StoreModConfig(_config, ConfigFileName);
    } catch (Exception e) {
      _api?.Logger.Warning(
        "[exlib] Could not write {0}. {1}",
        ConfigFileName,
        e
      );
    }
  }
}
