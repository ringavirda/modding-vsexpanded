using System.Runtime.CompilerServices;
using ExpandedLib.Testing;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Registers the Vintage Story assembly resolver before any test type (which references the game
/// assemblies, plus exlib/lpex/smex) is touched by the runner's reflection-based discovery.
/// </summary>
internal static class ModuleInit
{
  [ModuleInitializer]
  internal static void Init()
  {
    VsAssemblyResolver.Register();
    TestLang.Init();
    // The hopper slot accept-rules now read the shared material-role registry; seed iwex's roles (the
    // headless stand-in for materialroles.json) so those predicates resolve in unit tests.
    MaterialRoleSeeds.SeedIwexDefaults();
  }
}
