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
/// chimney look-at info, auto-registers every <c>[BlockRegister]</c>/<c>[ItemRegister]</c>/etc.
/// decorated class, adds the creative tab, and registers the unified "pipe" network type (gases + liquids).
/// </summary>
public class LowPressureExpandedModSystem : ModSystem
{
  private Harmony? _harmony;

  public override void Start(ICoreAPI api)
  {
    // Load gameplay tunables from ModConfig/lpex_values.json (writes defaults on first run).
    LpexValues.Load(api);
    // Drive the exlib RCC salvage ratio for our engines/boilers from the (live) config.
    ExpandedLib.Blocks.Construction.ExRccSettings.RegisterBrokenDropsRatio(
      Mod.Info.ModID,
      () => LpexValues.RccBrokenDropsRatio
    );
    // The steam-machine recipe cost catalogue (lpex_recipes.json).
    LpexRecipeValues.Load(api);
    // Register this mod's recipe-cost profile so exlib's shared apply pass and the generic
    // /exmod recipes lpex <level> command can drive it (see ExRecipeProfiles).
    ExRecipeProfiles.Register(
      new RecipeProfile
      {
        Code = Mod.Info.ModID,
        Catalogue = () => LpexRecipeValues.Recipes,
        Defaults = LpexRecipeConfig.DefaultCatalogue,
        GetLevel = () => LpexValues.RecipeLevel,
        SetLevel = level => LpexValues.Edit(c => c.RecipeLevel = level),
        SaveCatalogue = LpexRecipeValues.Save,
      }
    );

    // Patch the vanilla chimney's look-at info so a chimney venting one of our pipes
    // reports it (the gas draw itself runs in PipeNetwork's tick).
    if (!Harmony.HasAnyPatches(Mod.Info.ModID))
    {
      _harmony = new Harmony(Mod.Info.ModID);
      _harmony.PatchAll(GetType().Assembly);
    }

    // The plain cast (lpex) pipe segment's burst rating, read live from this mod's config. The base
    // pipe block + the "pipe" network itself are registered by iwex; lpex just contributes its tier's
    // strength and the cast segments/fittings via its code-first definitions.
    BlockPipe.RegisterBurst(Mod.Info.ModID, () => LpexValues.CastPipeBurstPressure);
    BlockPipe.RegisterThroughput(Mod.Info.ModID, () => LpexValues.CastPipeThroughput);
    // Cast pipe is square and plated like the plated tier, so the two runs interconnect.
    BlockPipe.RegisterJoint(Mod.Info.ModID, BlockPipe.FlangedJoint);

    // The shared structure-filler block and network/structure framework live in the exlib
    // mod (a hard dependency); exlib points StructureFillers at exlib:structurefiller and
    // registers its own classes. Here we only register lpex's own content.
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }

  public override void Dispose()
  {
    _harmony?.UnpatchAll(Mod.Info.ModID);
    _harmony = null;
    base.Dispose();
  }

  public override void StartServerSide(ICoreServerAPI api)
  {
    // Server-side sub-commands. The recipe-cost level is applied centrally by exlib (ExRecipeProfiles);
    // /exmod recipes lpex <level> is the generic switch.
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }

  #region Creative category
  public override void StartClientSide(ICoreClientAPI api)
  {
    ExCreativeTabs.EnsureTab(Mod.Info.ModID);

    // Register lpex's display preferences (the metric/imperial unit system) into the library's
    // shared store, then build their .exmod sub-commands. exlib loads/persists/applies the values.
    PreferenceRegistry.RegisterAll(api, Mod, GetType().Assembly);
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // The whole measure feature (the metric/imperial preference, its .exmod measure sub-command and
    // the handbook unit patch) moved down to exlib: iwex displays litres and atmospheres too, so the
    // unit system cannot belong to any one consumer. lpex registers nothing for it.
    // The recipe cost level is applied centrally by exlib (ExRecipeProfiles) on both sides.
  }
  #endregion
}
