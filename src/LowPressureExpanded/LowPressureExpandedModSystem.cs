using ExpandedLib.Blocks.Networks;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Commands;
using ExpandedLib.Registries.Entities;
using ExpandedLib.Registries.Preferences;
using ExpandedLib.Registries.Recipes;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace LowPressureExpanded;

/// <summary>
/// Main mod system for Low Pressure Expanded. Loads the gameplay tunables, patches the vanilla
/// chimney look-at info, auto-registers every <c>[BlockRegister]</c>/<c>[ItemRegister]</c> decorated
/// class, adds the creative tab and registers the unified "pipe" network type (gases and liquids).
/// </summary>
public class LowPressureExpandedModSystem : ModSystem {
  private Harmony? _harmony;

  public override void Start(ICoreAPI api) {
    // Load gameplay tunables from ModConfig/lpex_values.json (writes defaults on first run).
    LpexValues.Load(api);
    // Drives the exlib RCC salvage ratio for the engines and boilers from the live config.
    ExpandedLib.Blocks.Construction.ExRccSettings.RegisterBrokenDropsRatio(
      Mod.Info.ModID,
      () => LpexValues.RccBrokenDropsRatio
    );
    // The steam-machine recipe cost catalogue (lpex_recipes.json).
    LpexRecipeValues.Load(api);
    // Registers this mod's recipe-cost profile so exlib's shared apply pass and the generic
    // `/exmod recipes lpex <level>` command can drive it (see ExRecipeProfiles).
    ExRecipeProfiles.Register(
      new RecipeProfile {
        Code = Mod.Info.ModID,
        Catalogue = () => LpexRecipeValues.Recipes,
        Defaults = LpexRecipeConfig.DefaultCatalogue,
        GetLevel = () => LpexValues.RecipeLevel,
        SetLevel = level => LpexValues.Edit(c => c.RecipeLevel = level),
        SaveCatalogue = LpexRecipeValues.Save,
      }
    );

    // Patches the vanilla chimney's look-at info so a chimney venting a pipe reports it. The gas
    // draw itself runs in PipeNetwork's tick.
    if (!Harmony.HasAnyPatches(Mod.Info.ModID)) {
      _harmony = new Harmony(Mod.Info.ModID);
      _harmony.PatchAll(GetType().Assembly);
    }

    // Burst rating and throughput of the plain cast (lpex) pipe segment, read live from this mod's
    // config. The base pipe block and the "pipe" network itself are registered by iwex; lpex
    // contributes its tier's strength and the cast segments and fittings.
    BlockPipe.RegisterBurst(
      BlockPipe.CastTier,
      () => LpexValues.CastPipeBurstPressure
    );
    BlockPipe.RegisterThroughput(
      BlockPipe.CastTier,
      () => LpexValues.CastPipeThroughput
    );
    // Cast pipe is square and plated like the plated tier, so the two runs interconnect.
    BlockPipe.RegisterJoint(BlockPipe.CastTier, BlockPipe.FlangedJoint);

    // The shared structure-filler block and the network/structure framework live in the exlib mod
    // (a hard dependency), which points StructureFillers at exlib:structurefiller and registers its
    // own classes. Only lpex content is registered here.
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }

  public override void Dispose() {
    _harmony?.UnpatchAll(Mod.Info.ModID);
    _harmony = null;
    base.Dispose();
  }

  public override void StartServerSide(ICoreServerAPI api) {
    // Server-side sub-commands. The recipe-cost level itself is applied centrally by exlib
    // (ExRecipeProfiles); `/exmod recipes lpex <level>` is the generic switch.
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }

  #region Creative category
  public override void StartClientSide(ICoreClientAPI api) {
    ExCreativeTabs.EnsureTab(Mod.Info.ModID);

    // Registers any lpex display preferences into exlib's shared store and builds their .exmod
    // sub-commands. exlib loads, persists and applies the values. The measure feature (the
    // metric/imperial preference and the handbook unit patch) belongs to exlib, not to lpex.
    PreferenceRegistry.RegisterAll(api, Mod, GetType().Assembly);
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }
  #endregion
}
