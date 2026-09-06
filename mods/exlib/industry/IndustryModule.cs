using ExpandedLib.Catalogues;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Metals;
using ExpandedLib.Registries;
using Vintagestory.API.Common;

namespace ExpandedLib.Industry;

/// <summary>
/// Entry point for the family layer. This assembly ships inside the exlib mod folder beside
/// <c>exlib.dll</c>, and only one dll per mod folder may contain mod systems at all, so it
/// implements <see cref="IExModule"/> instead and exlib's module driver runs it through the same
/// phases. The pipe, molten and mechanical-power classes it carries are registered for it, under the
/// <c>exlib</c> domain its <c>[assembly: ExDomain]</c> names, so the keys a shipped blocktype and a
/// player's save already hold - <c>exlib.BlockPipe</c> and the rest - are unchanged.
/// </summary>
public sealed class IndustryModule : IExModule, IExDefinitionContributor {
  /// <summary>
  /// Refractory tier (cowper stove and smoke stack intakes) is the one variant group
  /// <see cref="ExBlockNames"/>' built-in material/rock/brick clause does not cover. Registered in
  /// the first phase so it is in place before any block's <c>GetHeldItemName</c> can decorate.
  /// </summary>
  public void StartPre(ICoreAPI api) =>
    ExBlockNames.AddVariantQualifier("refractory", "exlib:refractory-");

  /// <summary>
  /// Emits the generated metal resource item family (ingot/plate/rod/nails/bits per opted-in metal)
  /// and registers each with <see cref="ExDefinitions.RegisterItem(ExItemDef)"/>, so the definition
  /// system's injection at <c>AssetsLoaded</c> 0.04 builds them like any other code-first item. Read
  /// straight from the <c>config/metals/</c> assets rather than off <see cref="MetalRegistry"/>,
  /// which stays empty until <c>AssetsFinalize</c>. No side check: <see cref="ExDefinitions.RunContributors"/>
  /// only runs on the server, which is already <see cref="ExDefinitionModSystem"/>'s own guard.
  /// </summary>
  public void Contribute(ICoreAPI api) {
    foreach (
      ExItemDef def in MetalFamilyEmitter.Emit(
        AssetCatalogueLoader.GetMany<MetalDef>(api, "config/metals/")
      )
    )
      ExDefinitions.RegisterItem(def);
  }

  /// <summary>
  /// Populates <see cref="MetalRegistry"/> from the loaded metal worldproperties and every domain's
  /// <c>config/metals</c>. The driver runs below exlib's own mod system, so this lands before the
  /// liquid and material-role loads that read it.
  /// </summary>
  public void AssetsFinalize(ICoreAPI api) => MetalCatalogueLoader.Load(api).Log(api.Logger);
}
