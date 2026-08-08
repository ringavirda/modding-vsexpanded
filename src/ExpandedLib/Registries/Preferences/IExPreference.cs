using System.Collections.Generic;

namespace ExpandedLib.Registries.Preferences;

/// <summary>
/// A single per-player, client-side display preference (e.g. the metric/imperial unit system). Each
/// preference is a class carrying a <see cref="PreferenceRegisterAttribute"/>, discovered by
/// <see cref="PreferenceRegistry.RegisterAll"/>; <see cref="ExPreferences"/> persists it and the
/// <c>.exmod</c> command exposes it. The value is stored as a plain string, one of
/// <see cref="Options"/>.
/// <para>
/// The registry instantiates implementations through their parameterless constructor, so a
/// preference must hold no constructor state.
/// </para>
/// </summary>
public interface IExPreference {
  /// <summary>Stable key for this preference, used as the config key, the <c>.exmod</c>
  /// sub-command name and the lang-key stem (<c>command-{Key}-desc</c>,
  /// <c>pref-{Key}-label</c>, <c>pref-{Key}-{value}</c>). Lower-case, no spaces.</summary>
  string Key { get; }

  /// <summary>The allowed values, lower-case. The first entry is shown first; any of these is a
  /// valid argument to <c>.exmod {Key} {value}</c>.</summary>
  IReadOnlyList<string> Options { get; }

  /// <summary>The value used when the player has made no choice yet. Must be one of
  /// <see cref="Options"/>.</summary>
  string Default { get; }

  /// <summary>Applies a stored value to the live client state (e.g. the owning mod's active display
  /// unit system). Called for the local player on join and whenever the player changes the setting.
  /// <paramref name="value"/> is always one of <see cref="Options"/>.</summary>
  void Apply(string value);
}
