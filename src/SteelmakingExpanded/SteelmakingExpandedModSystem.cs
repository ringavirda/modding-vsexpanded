using ExpandedLib.Helpers;
using ExpandedLib.Registries.Commands;
using ExpandedLib.Registries.Entities;
using ExpandedLib.Registries.Recipes;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SteelmakingExpanded;

/// <summary>
/// Main mod system for Steelmaking Expanded. Auto-registers every block, block-entity, item and
/// behavior class via <see cref="EntityRegistry"/>, adds the mod's creative tab, and registers the
/// mod's recipe-cost profile and the bessemer converter's RCC salvage ratio. The molten-metal network
/// belongs to iwex and the pipe network to lpex; this mod only consumes them.
/// </summary>
public class SteelmakingExpandedModSystem : ModSystem {
  private Harmony? _harmony;

  public override void Dispose() {
    _harmony?.UnpatchAll(Mod.Info.ModID);
    _harmony = null;
    base.Dispose();
  }

  #region Creative category
  public override void StartClientSide(ICoreClientAPI api) {
    ExCreativeTabs.EnsureTab(Mod.Info.ModID);
    // exlib (ExRecipeProfiles) mirrors the recipe cost level on the client.
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly); // client-side sub-commands
  }
  #endregion

  #region Server-side registration
  public override void StartServerSide(ICoreServerAPI api) {
    // exlib (ExRecipeProfiles) applies the recipe cost level; /exmod recipes smex <level> is the
    // generic switch.
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }
  #endregion

  #region Registration
  public override void Start(ICoreAPI api) {
    // Load gameplay tunables from the mod config, writing defaults on first run. Must precede the
    // construction of any block entity for the values to apply.
    SmexValues.Load(api);
    // Drive the exlib RCC salvage ratio for the bessemer converter from the live config.
    ExpandedLib.Blocks.Construction.ExRccSettings.RegisterBrokenDropsRatio(
      Mod.Info.ModID,
      () => SmexValues.RccBrokenDropsRatio
    );
    // The steelmaking recipe cost catalogue.
    SmexRecipeValues.Load(api);
    // Register this mod's recipe-cost profile so exlib's shared apply pass and the generic
    // /exmod recipes smex <level> command can drive it (see ExRecipeProfiles).
    ExRecipeProfiles.Register(
      new RecipeProfile {
        Code = Mod.Info.ModID,
        Catalogue = () => SmexRecipeValues.Recipes,
        Defaults = SmexRecipeConfig.DefaultCatalogue,
        GetLevel = () => SmexValues.RecipeLevel,
        SetLevel = level => SmexValues.Edit(c => c.RecipeLevel = level),
        SaveCatalogue = SmexRecipeValues.Save,
      }
    );

    // Harmony bootstrap. This assembly declares no patches; the call is kept so that one added later
    // applies without further wiring.
    if (!Harmony.HasAnyPatches(Mod.Info.ModID)) {
      _harmony = new Harmony(Mod.Info.ModID);
      _harmony.PatchAll(GetType().Assembly);
    }

    // The shared structure-filler block lives in exlib, a hard dependency, which points the
    // StructureFillers helper at exlib:structurefiller for this mod's mega-blocks to reuse.

    // Auto-register every [BlockRegister] / [ItemRegister] / [BlockEntityRegister] block, item and
    // behavior declared in this assembly.
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }

  #endregion
}
