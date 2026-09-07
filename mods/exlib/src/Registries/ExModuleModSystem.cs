using System.ComponentModel;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib.Registries;

/// <summary>
/// Drives exlib's own framework modules - the <see cref="IExModule"/>s of any loaded assembly
/// carrying <c>[assembly: ExModule]</c> with <c>Host = "exlib"</c>, shipped inside exlib's own mod
/// folder (the domain layer, <c>exlib.industry.dll</c>) or as its own mod, which is how the domain
/// layer joins the lifecycle without a second dll with mod systems, which the game refuses. Also
/// boots exlib's own definition and entity-registry loggers and the library's tunables, ahead of
/// anything that might log or register.
/// </summary>
/// <remarks>
/// The execute order is what makes a module's phases usable rather than merely called. At 0.03 it
/// sits below <c>ExDefinitionModSystem</c>'s 0.04, so a definition a module registers in
/// <see cref="IExModule.AssetsLoaded"/> exists before that system injects it, and below
/// <c>ExpandedLibModSystem</c>'s default 0.1, so a catalogue a module loads in
/// <see cref="IExModule.AssetsFinalize"/> is populated before the framework's own loads read it. A
/// mod of its own drives its modules from <see cref="ExModSystem"/> instead, at its own order.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public class ExModuleModSystem : ModSystem {
  private ExModuleHost? _host;

  private ExModuleHost Host(ICoreAPI api) =>
    _host ??= new ExModuleHost(Mod, api);

  public override double ExecuteOrder() => 0.03;

  public override void StartPre(ICoreAPI api) {
    // Wired before anything registers a def or a class, so the re-registration notification and the
    // cross-mod Class<T>() fallback warning are live for every mod's own Start - including a
    // framework module's, driven a few lines below.
    Definitions.ExDefinitions.Logger = api.Logger;
    EntityRegistry.Logger = api.Logger;

    // Load the library's own gameplay tunables before any module reads them.
    ExlibValues.Load(api);

    SetFlags(api);

    ExModuleHost host = Host(api);
    api.Logger.Notification(
      "[exlib] modules hosted by {0}: {1}",
      Mod.Info.ModID,
      host.Modules.Count > 0
        ? string.Join(", ", host.Modules.Select(m => m.Id))
        : "none"
    );
    host.StartPre(api);
  }

  public override void Start(ICoreAPI api) {
    SetFlags(api);
    Host(api).Start(api);
  }

  public override void StartServerSide(ICoreServerAPI api) =>
    Host(api).StartServerSide(api);

  public override void StartClientSide(ICoreClientAPI api) =>
    Host(api).StartClientSide(api);

  public override void AssetsLoaded(ICoreAPI api) =>
    Host(api).AssetsLoaded(api);

  public override void AssetsFinalize(ICoreAPI api) =>
    Host(api).AssetsFinalize(api);

  public override void Dispose() {
    _host?.Dispose();
    _host = null;
    base.Dispose();
  }

  // Sets exlib:module:<id> for every enabled module discovered anywhere in the process, not only
  // this host's own, so a JSON patch condition can gate on a module regardless of which mod hosts
  // it. Idempotent, so calling from both StartPre and Start (see ExModsModSystem.SetFlags for why)
  // costs nothing extra.
  private static void SetFlags(ICoreAPI api) {
    var config = api.World?.Config;
    if (config == null)
      return;
    foreach (ExModuleInfo module in ExModules.Enabled(api))
      config.SetBool(ExModules.FlagKey(module.Id), true);
  }
}
