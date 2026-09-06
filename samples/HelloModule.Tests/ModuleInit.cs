using System.Runtime.CompilerServices;
using ExpandedLib.Testing;

namespace HelloModule.Tests;

/// <summary>
/// Registers the Vintage Story assembly resolver before any test type (which references the game
/// assemblies plus exlib/hellomodule) is touched by the runner's reflection-based discovery, and
/// touches <see cref="HelloModule"/> so its <c>[assembly: ExModule]</c> is loaded before
/// <c>ExModules</c> discovery runs.
/// </summary>
internal static class ModuleInit {
  [ModuleInitializer]
  internal static void Init() {
    VsAssemblyResolver.Register();
    TestLang.Init();
    _ = typeof(global::HelloModule.HelloModule);
  }
}
