using System.Runtime.CompilerServices;
using ExpandedLib.Testing;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Registers the Vintage Story assembly resolver before any test type (which references the game
/// assemblies, plus exlib/iiex/siex) is touched by the runner's reflection-based discovery.
/// </summary>
internal static class ModuleInit {
  [ModuleInitializer]
  internal static void Init() {
    VsAssemblyResolver.Register();
    TestLang.Init();
    // Hopper slot accept-rules read the shared material-role registry; seeding iiex's roles stands in
    // for materialroles.json so those predicates resolve headless.
    MaterialRoleSeeds.SeedIiexDefaults();
  }
}
