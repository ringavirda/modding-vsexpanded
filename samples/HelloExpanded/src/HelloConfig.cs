using ExpandedLib.Config;

namespace HelloExpanded;

/// <summary>
/// The whole config walk: one live-editable tunable, generated into a typed <c>HelloValues</c>
/// accessor by <c>ExConfigGenerator</c>. Loaded from and written to the <c>helloexpanded</c> section
/// of <c>ModConfig/helloexpanded.json</c>.
/// </summary>
[ExConfigRegister("helloexpanded.json", "helloexpanded", Manageable = true)]
public class HelloConfig : IExVersionedConfig {
  /// <summary>Mod version that last wrote this file. Managed by the config store - do not set by hand.</summary>
  public string? ConfigVersion { get; set; }

  /// <summary>How often <see cref="BlockEntityHello.OnProductionTick"/> fires, in milliseconds.</summary>
  [ExConfigRange(100, 10000)]
  public int TickIntervalMs { get; set; } = 1000;
}
