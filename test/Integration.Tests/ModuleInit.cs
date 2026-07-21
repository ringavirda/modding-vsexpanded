using System.Runtime.CompilerServices;
using ExpandedLib.Testing;

namespace Integration.Tests;

/// <summary>
/// Registers the Vintage Story assembly resolver and the headless Lang before any test type (which
/// references the game assemblies plus exlib/ppex/smex) is touched by the runner's discovery.
/// </summary>
internal static class ModuleInit
{
  [ModuleInitializer]
  internal static void Init()
  {
    VsAssemblyResolver.Register();
    TestLang.Init();
    // The blast-furnace charge classification now reads the shared material-role registry; seed iwex's
    // roles (the headless stand-in for materialroles.json) so the scenarios' blast-mix charge resolves.
    MaterialRoleSeeds.SeedIwexDefaults();
  }
}
