using System.Runtime.CompilerServices;
using ExpandedLib.Testing;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Registers the Vintage Story assembly resolver before any test type (which references the game
/// assemblies, plus exlib/iiex/iiex) is touched by the runner's reflection-based discovery.
/// </summary>
internal static class ModuleInit {
  [ModuleInitializer]
  internal static void Init() {
    VsAssemblyResolver.Register();
    TestLang.Init();
    // Mixer and furnace-core classification read the shared material-role registry; seed iiex's roles
    // (the headless stand-in for materialroles.json) so those predicates resolve.
    MaterialRoleSeeds.SeedIiexDefaults();
    // The mill reads its stopping points off the shared stage catalogue, which is populated from the loaded
    // collectibles in game and from the emitted stock defs here.
    ProcessRouteSeeds.SeedIiexRoutes();
    ReleasedHistorySeed.Register();
  }
}
