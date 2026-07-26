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
/// Main mod system for Steelmaking Expanded. Auto-registers every block, block-entity, item and behavior
/// class via <see cref="EntityRegistry"/>; adds the mod's creative tab; and registers the mod's
/// recipe-cost profile plus the bessemer converter's RCC salvage ratio. It <b>consumes</b> the
/// molten-metal network the foundational iwex mod registers rather than owning it; the in-hand mold
/// spill/burn safety and the mold-rack spill patch also live in iwex now (it owns the cast-iron molds),
/// so smex currently registers no Harmony patches of its own - the bootstrap is kept so any added to this
/// assembly later auto-apply. The pipe network and all pipe/steam-power content live in the Low Pressure
/// Expanded mod (lpex).
/// </summary>
public class SteelmakingExpandedModSystem : ModSystem
{
  private Harmony? _harmony;

  public override void Dispose()
  {
    _harmony?.UnpatchAll(Mod.Info.ModID);
    _harmony = null;
    base.Dispose();
  }

  #region Creative category
  public override void StartClientSide(ICoreClientAPI api)
  {
    ExCreativeTabs.EnsureTab(Mod.Info.ModID);
    // The recipe cost level is mirrored on the client centrally by exlib (ExRecipeProfiles).
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly); // client-side sub-commands (none yet)
  }
  #endregion

  #region Server-side registration
  public override void StartServerSide(ICoreServerAPI api)
  {
    // The recipe cost level is applied centrally by exlib (ExRecipeProfiles); /exmod recipes smex
    // <level> is the generic switch. (The in-hand mold spill/burn safety and the mold-rack spill patch
    // moved to the foundational iwex mod, which owns the cast-iron molds, so they work without smex.)
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }
  #endregion

  #region Registration
  public override void Start(ICoreAPI api)
  {
    // Load gameplay tunables from ModConfig/smex_values.json (writes defaults on first
    // run). Done before any block entity is constructed so the values apply.
    SmexValues.Load(api);
    // Drive the exlib RCC salvage ratio for the bessemer converter from the (live) config.
    ExpandedLib.Blocks.Construction.ExRccSettings.RegisterBrokenDropsRatio(
      Mod.Info.ModID,
      () => SmexValues.RccBrokenDropsRatio
    );
    // The steelmaking recipe cost catalogue (smex_recipes.json).
    SmexRecipeValues.Load(api);
    // Register this mod's recipe-cost profile so exlib's shared apply pass and the generic
    // /exmod recipes smex <level> command can drive it (see ExRecipeProfiles).
    ExRecipeProfiles.Register(
      new RecipeProfile
      {
        Code = Mod.Info.ModID,
        Catalogue = () => SmexRecipeValues.Recipes,
        Defaults = SmexRecipeConfig.DefaultCatalogue,
        GetLevel = () => SmexValues.RecipeLevel,
        SetLevel = level => SmexValues.Edit(c => c.RecipeLevel = level),
        SaveCatalogue = SmexRecipeValues.Save,
      }
    );

    // Apply this mod's own Harmony patches - currently none: the tool-mold / mold-rack filled-mold
    // handling and the coal-pile blast-mix patch moved to the foundational iwex mod, and the ceramic-mold
    // patches were retired with the ceramic molds. The bootstrap is kept so a patch added to this
    // assembly later auto-applies without re-wiring Harmony.
    if (!Harmony.HasAnyPatches(Mod.Info.ModID))
    {
      _harmony = new Harmony(Mod.Info.ModID);
      _harmony.PatchAll(GetType().Assembly);
    }

    // The shared structure-filler block lives in exlib (a hard dependency); exlib points the
    // StructureFillers helper at exlib:structurefiller, which this mod's mega-blocks reuse.

    // Auto-register every [BlockRegister]/[ItemRegister]/[BlockEntityRegister]/etc. block / item / behavior
    // declared in this assembly.
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // The molten-metal network now lives in (and is registered by) the foundational iwex mod, which
    // loads first; smex only consumes it. The unified "pipe" network is registered by lpex.
  }

  #endregion
}
