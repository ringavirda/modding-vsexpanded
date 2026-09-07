using ExpandedLib.Blocks;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using HarmonyLib;
using SteelIndustryExpanded.BlockStructures.Boiler.BlockEntities;
using SteelIndustryExpanded.BlockStructures.Converter.BlockEntities;
using SteelIndustryExpanded.BlockStructures.CowperStove.BlockEntities;
using SteelIndustryExpanded.BlockStructures.Engine.BlockEntities;
using SteelIndustryExpanded.BlockStructures.Forming;
using SteelIndustryExpanded.BlockStructures.SmokeStack.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SteelIndustryExpanded;

/// <summary>
/// Main mod system for Steel Industry Expanded: the hot blast furnace, the Bessemer converter and the
/// high-pressure steam leaves that drive them. Auto-registers every block, block-entity, item and
/// behavior class via <see cref="EntityRegistry"/>, loads the gameplay tunables and the recipe-cost
/// catalogue, registers this domain's RCC salvage ratio, the rolled pipe tier's ratings and the recipe
/// profile, and adds the creative tab.
/// <para>
/// Registers no network type: the molten-metal network and the unified "pipe" network are both owned
/// by iiex, the lowest mod that ships them, and this mod's machines ride them as their iiex bases do.
/// </para>
/// </summary>
public class SteelIndustryExpandedModSystem : ModSystem {
  private Harmony? _harmony;

  public override void Dispose() {
    ExHarmony.UnpatchAll(Mod);
    _harmony = null;
    base.Dispose();
  }

  #region Registration
  public override void Start(ICoreAPI api) {
    // Load gameplay tunables from the mod config, writing defaults on first run. Must precede the
    // construction of any block entity for the values to apply.
    SiexValues.Load(api);

    // RCC salvage ratio for the converter vessel and the HP machines, read live from the config. The
    // lookup is keyed by the broken block's Code.Domain, so the one registration covers all of them
    // now that they share a domain.
    ExRccSettings.RegisterBrokenDropsRatio(
      Mod.Info.ModID,
      () => SiexValues.RccBrokenDropsRatio
    );

    // Burst rating and throughput of the rolled (Hadfield steel) pipe tier, read live from the
    // config. Keyed by the block's `tier` variant in BlockPipe, as iiex registers plated and cast.
    BlockPipe.RegisterBurst(
      BlockPipe.RolledTier,
      () => SiexValues.RolledPipeBurstPressure
    );
    BlockPipe.RegisterThroughput(
      BlockPipe.RolledTier,
      () => SiexValues.RolledPipeThroughput
    );
    // Rolled pipe is octagonal and welded, with no flange to bolt a lower tier onto, so an HP run
    // does not join a plated or cast one.
    BlockPipe.RegisterJoint(BlockPipe.RolledTier, BlockPipe.WeldedJoint);

    // The cast stock forms. iiex pours billet, bloom and slab and owns the mill; registering the forms
    // is what lets that mill bite them, so an iiex-only player holds cast stock the rolls refuse. Must
    // precede any feed decision, and the mill makes none before a world is joined.
    CastStockForms.Register();

    // The recipe cost catalogue, and the profile that lets exlib's shared apply pass and the generic
    // /exmod recipes siex <level> command drive it. See ExRecipeProfiles.
    SiexRecipeValues.Load(api);
    ExRecipeProfiles.Register(
      new RecipeProfile {
        Code = Mod.Info.ModID,
        Catalogue = () => SiexRecipeValues.Recipes,
        Defaults = SiexRecipeConfig.DefaultCatalogue,
        GetLevel = () => SiexValues.RecipeLevel,
        SetLevel = level => SiexValues.Edit(c => c.RecipeLevel = level),
        SaveCatalogue = SiexRecipeValues.Save,
      }
    );

    // Harmony bootstrap. This assembly declares no patches; the call is kept so that one added later
    // applies without further wiring.
    _harmony = ExHarmony.PatchOnce(Mod, GetType().Assembly);

    // Auto-register every [BlockRegister] / [ItemRegister] / [BlockEntityRegister] block, item and
    // behavior declared in this assembly, and discover the co-located code-first block/recipe
    // definitions (IExBlockDefProvider, IExRecipeDefProvider) for injection. The structure-filler
    // block and the network/structure framework come from exlib; the boiler and engine bases, the
    // "pipe" network and the molten-metal network come from iiex.
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);

    foreach (var (legacyKey, type) in LegacyEntityClasses)
      api.RegisterBlockEntityClass(legacyKey, type);
  }
  #endregion

  /// <summary>
  /// Block-entity class strings from earlier domains, each pointed at the type answering for it now.
  /// The string lives in the SAVE, so no code migration reaches it and an unresolved one drops the
  /// block entity - the machine keeps its blocks and arrives without its state. Bounded by the
  /// <c>entityClass</c> values in the shipped zips; <c>ReleasedEntityClassTests</c> holds this list
  /// to that, and will name any key the merge left stranded.
  /// </summary>
  public static readonly (string Key, System.Type Type)[] LegacyEntityClasses =
  [
    // Both HP machines shipped in ppex 0.6.8, before the hpex extraction that preceded this merge.
    ("ppex.BlockEntityBoilerLancashire", typeof(BlockEntityBoilerLancashire)),
    ("ppex.BlockEntityEngineCornish", typeof(BlockEntityEngineCornish)),
    // smex 0.9.8 wrote every class under its own domain, and the eleven that moved to iiex are
    // aliased there. These six stayed, so the domain rename is the only thing that moved them; each
    // still backs blocks that migrate, which is what bounds the list - ReleasedEntityClassTests
    // names any that is missing and rejects one whose blocktype is wholly recorded debt.
    (
      "smex.BlockEntityConverterBessemer",
      typeof(BlockEntityConverterBessemer)
    ),
    ("smex.BlockEntityConverterControl", typeof(BlockEntityConverterControl)),
    (
      "smex.BlockEntityConverterTransmission",
      typeof(BlockEntityConverterTransmission)
    ),
    ("smex.BlockEntityEngineAirBlower", typeof(BlockEntityEngineAirBlower)),
    ("smex.BlockEntityHeatSink", typeof(BlockEntityHeatSink)),
    ("smex.BlockEntitySmokeStack", typeof(BlockEntitySmokeStack)),
  ];

  #region Creative category
  public override void StartClientSide(ICoreClientAPI api) {
    ExCreativeTabs.EnsureTab(Mod.Info.ModID);
    // exlib (ExRecipeProfiles) mirrors the recipe cost level on the client.
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }
  #endregion

  #region Server-side registration
  public override void StartServerSide(ICoreServerAPI api) {
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }
  #endregion
}
