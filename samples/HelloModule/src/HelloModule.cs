using ExpandedLib.Catalogues;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;
using Vintagestory.API.Common;

namespace HelloModule;

/// <summary>
/// Entry point for the sample module. Ships as its own mod folder, next to <c>hellomodule.dll</c>,
/// carrying no <see cref="ModSystem"/> of its own - exlib's <c>ExModuleModSystem</c> discovers this
/// class through the assembly's <c>[assembly: ExModule]</c> and drives it through the same phases a
/// <see cref="ModSystem"/> would get, proving a Code mod needs none to extend exlib.
/// </summary>
public sealed class HelloModule : IExModule, IExDefinitionContributor {
  /// <summary>
  /// Emits one generated item per <c>config/greetings/</c> entry and registers each with
  /// <see cref="ExDefinitions.RegisterItem(ExItemDef)"/>, so the definition system's injection at
  /// <c>AssetsLoaded</c> 0.04 builds them like any other code-first item.
  /// </summary>
  public void Contribute(ICoreAPI api) {
    foreach (
      ExItemDef def in GreetingItems.Emit(
        "hellomodule",
        AssetCatalogueLoader.GetMany<GreetingDef>(api, "config/greetings/")
      )
    )
      ExDefinitions.RegisterItem(def);
  }

  /// <summary>Populates <see cref="Greetings"/> from every domain's <c>config/greetings</c>, so
  /// <see cref="BlockBehaviorGreeter"/> has something to send once the world is up.</summary>
  public void AssetsFinalize(ICoreAPI api) => Greetings.Load(api);
}
