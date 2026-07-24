using ExpandedLib.Blocks.Construction;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Commands;
using ExpandedLib.Registries.Entities;
using ExpandedLib.Registries.Recipes;
using IronworkingExpanded.BlockNetworkPipe.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace HighPressureExpanded;

/// <summary>
/// Main mod system for High Pressure Expanded (hpex) - the top of the steam chain, holding the two
/// high-pressure leaves (Lancashire boiler, Cornish engine). Loads the gameplay tunables and the
/// recipe-cost catalogue, registers this domain's RCC salvage ratio and recipe profile, adds the
/// creative tab, and auto-registers every <c>[BlockRegister]</c>/<c>[BlockEntityRegister]</c>
/// decorated class plus the co-located code-first definitions.
/// <para>
/// It deliberately registers <b>no network type</b>: the unified "pipe" network is owned by iwex
/// (the lowest mod that ships pipes) and the HP machines just ride it, exactly like their lpex
/// bases. There is likewise no Harmony patching here - the leaves add no vanilla behaviour.
/// </para>
/// </summary>
public class HighPressureExpandedModSystem : ModSystem
{
  public override void Start(ICoreAPI api)
  {
    // Load gameplay tunables from the shared ModConfig config (writes defaults on first run).
    HpexValues.Load(api);

    // Drive the exlib RCC salvage ratio for our boiler/engine from the (live) config. The lookup is
    // keyed by the broken block's Code.Domain, so hpex must register its own even though lpex
    // already registered an identical default for its domain.
    ExRccSettings.RegisterBrokenDropsRatio(
      Mod.Info.ModID,
      () => HpexValues.RccBrokenDropsRatio
    );

    // The rolled (Hadfield steel) pipe tier's burst rating, read live from this mod's config. Keyed by
    // domain in BlockPipe, exactly as iwex registers bolted and lpex registers cast.
    BlockPipe.RegisterBurst(Mod.Info.ModID, () => HpexValues.RolledPipeBurstPressure);
    // Rolled pipe is octagonal and welded - no flange to bolt a lower tier onto, so an HP run
    // will not join a bolted or cast one at all.
    BlockPipe.RegisterJoint(Mod.Info.ModID, BlockPipe.WeldedJoint);

    // The HP-machine recipe cost catalogue.
    HpexRecipeValues.Load(api);
    // Register this mod's recipe-cost profile so exlib's shared apply pass and the generic
    // /exmod recipes hpex <level> command can drive it (see ExRecipeProfiles).
    ExRecipeProfiles.Register(
      new RecipeProfile
      {
        Code = Mod.Info.ModID,
        Catalogue = () => HpexRecipeValues.Recipes,
        Defaults = HpexRecipeConfig.DefaultCatalogue,
        GetLevel = () => HpexValues.RecipeLevel,
        SetLevel = level => HpexValues.Edit(c => c.RecipeLevel = level),
        SaveCatalogue = HpexRecipeValues.Save,
      }
    );

    // Auto-register every [BlockRegister]/[BlockEntityRegister]/etc. declared here, and discover the
    // co-located code-first block/recipe definitions (IExBlockDefProvider/IExRecipeDefProvider) for
    // injection. The shared structure-filler block and the network/structure framework come from
    // exlib; the boiler/engine bases from lpex; the "pipe" network from iwex.
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }

  public override void StartClientSide(ICoreClientAPI api)
  {
    ExCreativeTabs.EnsureTab(Mod.Info.ModID);
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }

  public override void StartServerSide(ICoreServerAPI api)
  {
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }
}
