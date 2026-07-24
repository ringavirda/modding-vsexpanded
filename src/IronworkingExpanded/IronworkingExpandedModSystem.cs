using ExpandedLib.Blocks.Networks;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Commands;
using ExpandedLib.Registries.Entities;
using ExpandedLib.Registries.Recipes;
using HarmonyLib;
using IronworkingExpanded.BlockNetworkMolten;
using IronworkingExpanded.BlockNetworkPipe;
using IronworkingExpanded.BlockNetworkPipe.Blocks;
using IronworkingExpanded.Compat;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace IronworkingExpanded;

/// <summary>
/// Main mod system for Ironworking Expanded (iwex). Auto-registers every block / block-entity /
/// item / behavior class in this assembly via <see cref="EntityRegistry"/> and adds the mod's
/// creative tab; registers the molten-metal network type; registers other mods' iron-ore types for
/// the blast furnace hoppers; and applies the Harmony patch that wires blast-mix into the vanilla
/// coal pile. Builds on the shared exlib framework (structure fillers, RightClickConstructable,
/// attribute-driven registration) and the lpex mechanical-power / pipe content.
/// </summary>
public class IronworkingExpandedModSystem : ModSystem
{
  private Harmony? _harmony;

  public override void Dispose()
  {
    _harmony?.UnpatchAll(Mod.Info.ModID);
    _harmony = null;
    base.Dispose();
  }

  public override void Start(ICoreAPI api)
  {
    // Load gameplay tunables from ModConfig/iwex_values.json (writes defaults on first run).
    // Done before any block entity is constructed so the molten-system values apply.
    IwexValues.Load(api);

    // Load this mod's recipe-cost catalogue and register its profile, so exlib's shared apply pass
    // and the generic /exmod recipes iwex <level> command can drive it (see ExRecipeProfiles). The
    // iron tier used to have no switch of its own - its costs rode under /exmod recipes smex.
    IwexRecipeValues.Load(api);
    ExRecipeProfiles.Register(
      new RecipeProfile
      {
        Code = Mod.Info.ModID,
        Catalogue = () => IwexRecipeValues.Recipes,
        Defaults = IwexRecipeConfig.DefaultCatalogue,
        GetLevel = () => IwexValues.RecipeLevel,
        SetLevel = level => IwexValues.Edit(c => c.RecipeLevel = level),
        SaveCatalogue = IwexRecipeValues.Save,
      }
    );

    // Register other mods' iron ore types (used by the blast furnace's reinforced hopper).
    IronOreCompat.Init(api);

    // Harmony patch that wires blast-mix burn-to-slag into the vanilla coal pile without replacing
    // its registered class, so other mods touching the coal pile can coexist.
    if (!Harmony.HasAnyPatches(Mod.Info.ModID))
    {
      _harmony = new Harmony(Mod.Info.ModID);
      _harmony.PatchAll(GetType().Assembly);
    }

    // The plain (bolted) pipe segment's burst rating, read live from this mod's config. Higher pipe
    // tiers register their own (lpex cast, hpex rolled), keyed by domain in BlockPipe.
    BlockPipe.RegisterBurst(Mod.Info.ModID, () => IwexValues.BoltedPipeBurstPressure);
    BlockPipe.RegisterJoint(Mod.Info.ModID, BlockPipe.FlangedJoint);

    // Auto-register every [BlockRegister]/[ItemRegister]/[BlockEntityRegister]/etc. declared here,
    // and discover any co-located code-first block definitions (IExBlockDefProvider) for injection.
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // iwex owns the two networks its pipes/canals ride and registers them before any dependent mod
    // (lpex, smex) needs them. The unified "pipe" network (gas + liquid pools) carries a chimney-vent
    // strategy that draws gas through a chimney-ventable fitting's top connector; the draw rate is read
    // live from iwex's config. The molten-metal network is iwex's own.
    var netManager = api.ModLoader.GetModSystem<BlockNetworkModSystem>();
    netManager.RegisterNetworkType(
      "pipe",
      () => new PipeNetwork(netManager, new ChimneyVent(() => IwexValues.ChimneyGasDrawRate))
    );
    netManager.RegisterNetworkType(
      "molten",
      () => new MoltenNetwork(netManager)
    );
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
