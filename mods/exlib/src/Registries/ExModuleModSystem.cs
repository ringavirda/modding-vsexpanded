using System.ComponentModel;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries;

/// <summary>
/// Drives exlib's own companion assemblies - the <see cref="IExModule"/>s of any dll shipped beside
/// <c>exlib.dll</c> in the mod folder, which is how the family's domain layer
/// (<c>exlib.industry.dll</c>) takes part in the lifecycle without being a second dll that contains
/// mod systems, which the game refuses.
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
  public override double ExecuteOrder() => 0.03;

  public override void StartPre(ICoreAPI api) => ExModules.Drive(Mod, m => m.StartPre(api));

  public override void Start(ICoreAPI api) => ExModules.Start(Mod, api);

  public override void AssetsLoaded(ICoreAPI api) =>
    ExModules.Drive(Mod, m => m.AssetsLoaded(api));

  public override void AssetsFinalize(ICoreAPI api) =>
    ExModules.Drive(Mod, m => m.AssetsFinalize(api));
}
