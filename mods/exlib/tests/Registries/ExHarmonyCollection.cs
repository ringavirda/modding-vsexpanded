using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Serializes every test class that applies or removes a real Harmony patch. Harmony patches are
/// process-global (keyed by owner id and target method, not by test instance), so two classes
/// patching in parallel would see each other's prefixes; members: <see cref="ExHarmonyTests"/>.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class ExHarmonyCollection {
  public const string Name = "ExHarmony";
}
