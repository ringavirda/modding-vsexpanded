using ExpandedLib.Config;

namespace HelloModule;

/// <summary>
/// The module's one tunable, generated into a typed <c>HelloModuleValues</c> accessor by
/// <c>ExConfigGenerator</c>. Loaded from and written to the <c>hellomodule</c> section of
/// <c>ModConfig/hellomodule.json</c>.
/// </summary>
[ExConfigRegister("hellomodule.json", "hellomodule", Manageable = true)]
public class HelloModuleConfig : IExVersionedConfig {
  /// <summary>Mod version that last wrote this file. Managed by the config store - do not set by hand.</summary>
  public string? ConfigVersion { get; set; }

  /// <summary>How many greetings <see cref="BlockBehaviorGreeter"/> sends per interact.</summary>
  [ExConfigRange(1, 10)]
  public int GreetingsPerClick { get; set; } = 1;
}
