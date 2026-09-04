using System;

namespace ExpandedLib.Registries.Config;

/// <summary>
/// Marks a config POCO implementing <see cref="IExVersionedConfig"/> for which the
/// <c>ExConfigGenerator</c> source generator emits a static accessor class: <c>const ConfigFileName</c>,
/// the backing <see cref="ExConfigRegister{TConfig}"/>, <c>Load(ICoreAPI)</c> and one read-only
/// <c>public static</c> property per config value. It is <c>static partial</c> in the config's
/// namespace; a <c>public static Migrations</c> member on the config is forwarded to the store.
/// </summary>
/// <example>
/// <code>[ExConfigRegister("ex_values.json", "iiex")] on IiexConfig generates IiexValues.Load(api), IiexValues.BoilingPoint, ...</code>
/// </example>
[AttributeUsage(
  AttributeTargets.Class,
  AllowMultiple = false,
  Inherited = false
)]
public sealed class ExConfigRegisterAttribute : Attribute {
  /// <param name="fileName">Config file name under the game's <c>ModConfig</c> folder (e.g. <c>"ex_values.json"</c>).</param>
  /// <param name="modId">Owning mod id; resolves the running version and tags log lines.</param>
  public ExConfigRegisterAttribute(string fileName, string modId) {
    FileName = fileName;
    ModId = modId;
  }

  /// <summary>Config file name under the game's <c>ModConfig</c> folder.</summary>
  public string FileName { get; }

  /// <summary>The owning mod id.</summary>
  public string ModId { get; }

  /// <summary>Name of the generated accessor class. Defaults to the config type name with a trailing
  /// <c>Config</c> swapped for <c>Values</c> (<c>IiexConfig</c> gives <c>IiexValues</c>), or the type
  /// name plus <c>Values</c> when it has no <c>Config</c> suffix.</summary>
  public string? AccessorName { get; set; }

  /// <summary>Former names this config file used under <c>ModConfig</c>. On load, if the current
  /// <see cref="FileName"/> is absent but one of these still exists, it is renamed to the current
  /// name (first match wins), carrying existing tuning over a rename.</summary>
  public string[]? LegacyFileNames { get; set; }

  /// <summary>Mod ids whose section of <see cref="FileName"/> this config now owns - the mods this
  /// one was renamed from or absorbed. A section is keyed by mod id, so without these a rename
  /// silently discards the player's whole tuning for this mod; the values revert to their coded
  /// defaults with no error. Distinct from <see cref="LegacyFileNames"/>, which carries a legacy
  /// FILE into a section rather than one section into another.</summary>
  public string[]? LegacySectionIds { get; set; }

  /// <summary>When <c>true</c>, the generated accessor registers this store with
  /// <see cref="ExpandedLib.Registries.Config.ExConfigProfiles"/> at load, exposing its simple-typed
  /// values to the generic <c>/exmod config &lt;mod&gt; &lt;value&gt; [&lt;new&gt;]</c> command. Leave
  /// it off for catalogue configs such as recipe costs, which have their own command.</summary>
  public bool Manageable { get; set; }
}
