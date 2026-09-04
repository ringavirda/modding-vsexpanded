using System;

namespace ExpandedLib.Registries.Preferences;

/// <summary>
/// Marks an <see cref="IExPreference"/> class for automatic registration by
/// <see cref="PreferenceRegistry.RegisterAll"/>, so a mod system needs no hand-written wiring.
/// Mirrors <see cref="Commands.CommandRegisterAttribute"/>. Preferences are per-player client-side
/// display settings, so the registry runs on the client only and there is no side to configure.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PreferenceRegisterAttribute : Attribute { }
