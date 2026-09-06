using ExpandedLib.Config;

namespace ExpandedLib.Tests;

/// <summary>
/// A minimal <c>[ExConfigRegister]</c> config, top-level so <c>ExConfigGenerator</c> emits a proper
/// accessor for it (a nested class's unqualified name would not resolve). Its only purpose is to give
/// <see cref="ExModSystemTests"/> a real generated accessor - carrying <c>[ExConfigAccessor]</c> - to
/// find in this assembly.
/// </summary>
[ExConfigRegister("exmodsystemtest.json", "exlib-modsystem-test")]
public class ExModSystemTestConfig : IExVersionedConfig {
  public string? ConfigVersion { get; set; }

  public int Tunable { get; set; } = 42;
}
