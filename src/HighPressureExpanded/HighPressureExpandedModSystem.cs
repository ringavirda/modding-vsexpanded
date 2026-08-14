using ExpandedLib.Blocks.Construction;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Commands;
using ExpandedLib.Registries.Entities;
using ExpandedLib.Registries.Recipes;
using HighPressureExpanded.BlockStructures.Boiler.BlockEntities;
using HighPressureExpanded.BlockStructures.Engine.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace HighPressureExpanded;

/// <summary>
/// Main mod system for High Pressure Expanded (hpex), the top of the steam chain: the Lancashire
/// boiler, the Cornish engine and the rolled pipe tier. Loads the gameplay tunables and the
/// recipe-cost catalogue, registers this domain's RCC salvage ratio, pipe ratings and recipe profile,
/// adds the creative tab, and auto-registers every <c>[BlockRegister]</c>/<c>[BlockEntityRegister]</c>
/// class plus the co-located code-first definitions.
/// <para>
/// Registers no network type: the unified "pipe" network is owned by iiex, the lowest mod that ships
/// pipes, and the HP machines ride it as their iiex bases do.
/// </para>
/// </summary>
public class HighPressureExpandedModSystem : ModSystem {
  public override void Start(ICoreAPI api) {
    // Gameplay tunables from the shared ModConfig file; writes defaults on first run.
    HpexValues.Load(api);

    // RCC salvage ratio for the boiler and engine, read live from this mod's config. The lookup is
    // keyed by the broken block's Code.Domain, so hpex must register its own even though iiex
    // registers an identical default for its domain.
    ExRccSettings.RegisterBrokenDropsRatio(
      Mod.Info.ModID,
      () => HpexValues.RccBrokenDropsRatio
    );

    // Burst rating and throughput of the rolled (Hadfield steel) pipe tier, read live from this mod's
    // config. Keyed by the block's `tier` variant in BlockPipe, as iiex registers plated and cast.
    BlockPipe.RegisterBurst(
      BlockPipe.RolledTier,
      () => HpexValues.RolledPipeBurstPressure
    );
    BlockPipe.RegisterThroughput(
      BlockPipe.RolledTier,
      () => HpexValues.RolledPipeThroughput
    );
    // Rolled pipe is octagonal and welded, with no flange to bolt a lower tier onto, so an HP run
    // does not join a plated or cast one.
    BlockPipe.RegisterJoint(BlockPipe.RolledTier, BlockPipe.WeldedJoint);

    // The HP-machine recipe cost catalogue.
    HpexRecipeValues.Load(api);
    // The recipe-cost profile, so exlib's shared apply pass and the generic
    // /exmod recipes hpex <level> command can drive it. See ExRecipeProfiles.
    ExRecipeProfiles.Register(
      new RecipeProfile {
        Code = Mod.Info.ModID,
        Catalogue = () => HpexRecipeValues.Recipes,
        Defaults = HpexRecipeConfig.DefaultCatalogue,
        GetLevel = () => HpexValues.RecipeLevel,
        SetLevel = level => HpexValues.Edit(c => c.RecipeLevel = level),
        SaveCatalogue = HpexRecipeValues.Save,
      }
    );

    // Registers every [BlockRegister]/[BlockEntityRegister] class declared here and discovers the
    // co-located code-first block/recipe definitions (IExBlockDefProvider, IExRecipeDefProvider) for
    // injection. The structure-filler block and the network/structure framework come from exlib; the
    // boiler and engine bases and the "pipe" network come from iiex.
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // Both machines shipped in ppex 0.6.8 before the hpex extraction, so a released save holds their
    // block entities under ppex class keys. A class string lives in the save rather than in any
    // definition, so HpexExtractionMigration - which remaps block codes - never reaches it, and an
    // unresolved key drops the block entity: the machine keeps its blocks and loses its state.
    api.RegisterBlockEntityClass(
      "ppex.BlockEntityBoilerLancashire",
      typeof(BlockEntityBoilerLancashire)
    );
    api.RegisterBlockEntityClass(
      "ppex.BlockEntityEngineCornish",
      typeof(BlockEntityEngineCornish)
    );
  }

  public override void StartClientSide(ICoreClientAPI api) {
    ExCreativeTabs.EnsureTab(Mod.Info.ModID);
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }

  public override void StartServerSide(ICoreServerAPI api) {
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }
}
