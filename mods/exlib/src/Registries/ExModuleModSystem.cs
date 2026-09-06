using System.ComponentModel;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib.Registries;

/// <summary>
/// Drives exlib's own framework modules - the <see cref="IExModule"/>s of any dll shipped beside
/// <c>exlib.dll</c> in the mod folder, which is how the family's domain layer
/// (<c>exlib.industry.dll</c>) takes part in the lifecycle without being a second dll that contains
/// mod systems, which the game refuses. Also boots exlib's own definition and entity-registry
/// loggers and the library's tunables, ahead of anything that might log or register.
/// </summary>
/// <remarks>
/// The execute order is what makes a module's phases usable rather than merely called. At 0.03 it
/// sits below <c>ExDefinitionModSystem</c>'s 0.04, so definitions a module registers in
/// <see cref="IExModule.AssetsLoaded"/> exist before that system injects them as synthetic assets;
/// and below <c>ExpandedLibModSystem</c>'s default 0.1, so a catalogue a module loads in
/// <see cref="IExModule.AssetsFinalize"/> is populated before the framework's own loads read it.
/// A mod of its own drives its modules from <see cref="ExModSystem"/> instead, at whatever order it
/// declares.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public class ExModuleModSystem : ModSystem {
  private ExModuleHost? _host;

  private ExModuleHost Host => _host ??= new ExModuleHost(Mod);

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

    api.Logger.Notification(
      "[exlib] modules hosted by {0}: {1}",
      Mod.Info.ModID,
      Host.Modules.Count > 0
        ? string.Join(", ", Host.Modules.Select(m => m.Id))
        : "none"
    );
    Host.StartPre(api);
  }

  public override void Start(ICoreAPI api) {
    SetFlags(api);
    Host.Start(api);
  }

  public override void StartServerSide(ICoreServerAPI api) => Host.StartServerSide(api);

  public override void StartClientSide(ICoreClientAPI api) => Host.StartClientSide(api);

  public override void AssetsLoaded(ICoreAPI api) => Host.AssetsLoaded(api);

  public override void AssetsFinalize(ICoreAPI api) => Host.AssetsFinalize(api);

  public override void Dispose() {
    _host?.Dispose();
    _host = null;
    base.Dispose();
  }

  // Sets exlib:module:<id> for every module discovered anywhere in the process, not only this
  // host's own, so a JSON patch condition can gate on a module regardless of which mod hosts it.
  // Idempotent, so calling from both StartPre and Start (see ExModsModSystem.SetFlags for why) costs
  // nothing extra.
  private static void SetFlags(ICoreAPI api) {
    var config = api.World?.Config;
    if (config == null)
      return;
    foreach (ExModuleInfo module in ExModules.All)
      config.SetBool(ExModules.FlagKey(module.Id), true);
  }
}
