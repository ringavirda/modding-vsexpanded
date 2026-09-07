using System.IO;
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
    // exlib's own assets/ tree is not under mods/ any more - exmod.json here names no "exlib" mod,
    // so RepoPaths.Assets("exlib") would otherwise fall back to a mods/exlib folder that does not
    // exist. Point the domain at the sibling checkout directly: Path.Combine treats a rooted final
    // argument as the whole result, so Mod()'s own mods/<id> fallback resolves to this path
    // unchanged. Guarded on existence - a standalone clone (package mode) has no such sibling, and
    // this suite's asset-resolution checks are source-mode only.
    string exlibRoot = Path.GetFullPath(
      Path.Combine(RepoPaths.Root, "..", "exlib")
    );
    if (Directory.Exists(exlibRoot))
      RepoPaths.Register("exlib", exlibRoot);
    // Mixer and furnace-core classification read the shared material-role registry; seed iiex's roles
    // (the headless stand-in for materialroles.json) so those predicates resolve.
    MaterialRoleSeeds.SeedIiexDefaults();
    // The mill reads its stopping points off the shared stage catalogue, which is populated from the loaded
    // collectibles in game and from the emitted stock defs here.
    ProcessRouteSeeds.SeedIiexRoutes();
    ReleasedHistorySeed.Register();
  }
}
