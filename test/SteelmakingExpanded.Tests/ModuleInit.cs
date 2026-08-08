using System.Runtime.CompilerServices;
using ExpandedLib.Testing;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Registers the Vintage Story assembly resolver before any test type (which references the game
/// assemblies, plus exlib/lpex/smex) is touched by the runner's reflection-based discovery.
/// </summary>
internal static class ModuleInit {
  [ModuleInitializer]
  internal static void Init() {
    VsAssemblyResolver.Register();
    TestLang.Init();
    // Hopper slot accept-rules read the shared material-role registry; seeding iwex's roles stands in
    // for materialroles.json so those predicates resolve headless.
    MaterialRoleSeeds.SeedIwexDefaults();
  }
}
