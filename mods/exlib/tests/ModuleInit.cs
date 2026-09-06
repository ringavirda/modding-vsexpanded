using System.Runtime.CompilerServices;
using ExpandedLib.Registries;
using ExpandedLib.Testing;

// A module of a fake host of its own, never exlib: RegistrationKeyTests relies on this assembly
// declaring no [assembly: ExDomain].
[assembly: ExModule("exlibtests", Host = "exlibtest.host")]

namespace ExpandedLib.Tests;

/// <summary>
/// Registers the Vintage Story assembly resolver before any test type (which references the game
/// assemblies) is touched by the runner's reflection-based discovery.
/// </summary>
internal static class ModuleInit {
  [ModuleInitializer]
  internal static void Init() {
    VsAssemblyResolver.Register();
    TestLang.Init();
    ReleasedHistorySeed.Register();
  }
}
