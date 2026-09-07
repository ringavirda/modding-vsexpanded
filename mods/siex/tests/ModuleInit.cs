using System.IO;
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
    // exlib's own assets/ tree is not under mods/ any more - see IronIndustryExpanded.Tests'
    // ModuleInit for why this points RepoPaths at the sibling checkout directly.
    string exlibRoot = Path.GetFullPath(
      Path.Combine(RepoPaths.Root, "..", "exlib")
    );
    if (Directory.Exists(exlibRoot))
      RepoPaths.Register("exlib", exlibRoot);
    // Hopper slot accept-rules read the shared material-role registry; seeding iiex's roles stands in
    // for materialroles.json so those predicates resolve headless.
    MaterialRoleSeeds.SeedIiexDefaults();
    // ReleasedCodeCoverageTests/ReleasedEntityClassTests/ModinfoTests are the only suite that checks
    // all three mods' shipped history together, so exlib's registration is called explicitly rather
    // than left to depend on its assembly happening to load.
    ExlibReleasedHistorySeed.Register();
    ReleasedHistorySeed.Register();
  }
}
