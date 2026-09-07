using System;
using System.Reflection;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries;

/// <summary>
/// Reflection-driven preference registration for mods built on ExpandedLib, the preference-side
/// counterpart to <see cref="CommandRegistry"/>. Scans an assembly for
/// <see cref="IExPreference"/> classes carrying <see cref="PreferenceRegisterAttribute"/> and adds
/// each one to <see cref="ExPreferences"/>.
/// </summary>
public static class PreferenceRegistry {
  /// <summary>
  /// Registers every <see cref="PreferenceRegisterAttribute"/>-decorated
  /// <see cref="IExPreference"/> in <paramref name="asm"/> (default: the calling assembly) with
  /// <see cref="ExPreferences"/>. Call from <c>ModSystem.StartClientSide</c>, before the mod's own
  /// <see cref="CommandRegistry"/> call: a preference sub-command resolves its definition
  /// once at registration time, so one built first holds no preference. Order against
  /// <see cref="ExPreferences.LoadConfig"/> does not matter - neither reads the other's state, and
  /// exlib applies saved choices on <c>LevelFinalize</c>, after every mod's StartClientSide.
  /// </summary>
  public static void RegisterAll(ICoreAPI api, Mod mod, Assembly? asm = null) {
    asm ??= Assembly.GetCallingAssembly();
    string modId = mod.Info.ModID;

    ReflectionScan.ForEachAttributed<
      PreferenceRegisterAttribute,
      IExPreference
    >(api, modId, asm, (attr, pref) => ExPreferences.Register(pref));
  }
}
