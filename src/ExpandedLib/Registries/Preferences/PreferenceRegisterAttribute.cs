using System;

namespace ExpandedLib.Registries.Preferences;

/// <summary>
/// Marks an <see cref="IExPreference"/> class for automatic registration by
/// <see cref="PreferenceRegistry.RegisterAll"/>. Mirrors
/// <see cref="Commands.CommandRegisterAttribute"/>: a preference only needs this one attribute and
/// an <see cref="IExPreference"/> implementation - no hand-written wiring in the mod system.
/// <para>
/// Preferences are per-player, client-side display settings (the HUD/handbook render on the
/// client), so the registry only runs them on the client; there is no side to configure.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PreferenceRegisterAttribute : Attribute { }
