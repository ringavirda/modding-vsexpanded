using ExpandedLib.Blocks.Structures;
using ExpandedLib.Fluids;
using ExpandedLib.Materials;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Commands;
using ExpandedLib.Registries.Entities;
using ExpandedLib.Registries.Preferences;
using ExpandedLib.Registries.Recipes;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib;

/// <summary>
/// Entry point for the shared Expanded Lib mod (<c>exlib</c>). Registers the library's own
/// blocks / block entities / behaviours (the invisible structure filler and the multiblock
/// structure behaviour) and points the <see cref="StructureFillers"/> helper at this mod's
/// filler block, so every dependent mod's mega-blocks reuse a single shared filler.
/// <para>
/// On the client it owns the generic per-player display-preferences store
/// (<see cref="Registries.Preferences.ExPreferences"/>): it loads the per-player <c>exmod.json</c>
/// and applies each player's saved choices on join. It also owns the <b>measure</b> feature - the
/// metric/imperial display-unit preference, its <c>.exmod measure</c> sub-command and the handbook
/// unit-conversion patch - because every mod from iwex up displays litres, atmospheres and
/// temperatures, so it cannot sensibly belong to any one of them. Dependent mods contribute further
/// preferences and sub-commands from their own assemblies.
/// </para>
/// <para>
/// The block-network graph manager and the world block-code migrator are separate
/// <c>ModSystem</c>s in this assembly (<see cref="Blocks.Networks.BlockNetworkModSystem"/>,
/// <see cref="Blocks.Migrations.BlockMigrationModSystem"/>); the game auto-loads them too.
/// </para>
/// </summary>
public class ExpandedLibModSystem : ModSystem
{
  // Client-side Harmony instance for the handbook unit patch (see StartClientSide).
  private Harmony? _harmony;

  public override void Start(ICoreAPI api)
  {
    // Load the library's own gameplay tunables (chiefly the block-network constants the concrete
    // networks that live in this assembly read). Before this runs the accessor already holds the
    // coded defaults, so reads are always safe.
    ExlibValues.Load(api);

    // Auto-register the library's [BlockRegister]/[BlockEntityRegister]/[BlockBehaviorRegister] classes (filler block + entity, the
    // MultiblockStructure behaviour) under the exlib domain.
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // The shared filler block this lib ships; dependent mods' mega-blocks reserve their
    // footprint cells with it (see StructureFillers).
    StructureFillers.FillerCode = new AssetLocation(
      Mod.Info.ModID,
      "structurefiller"
    );
  }

  /// <summary>
  /// After the asset-patch pipeline has merged all mods' JSON (and before recipe/world finalize),
  /// populate the shared metal catalogue from the loaded metal worldproperties + every domain's
  /// <c>config/metals</c>. Runs on both sides (the <c>config</c>/<c>worldproperties</c> categories are
  /// Universal); consumers read back convention values for any metal not enriched here, so a partial
  /// load never regresses. Fires exactly once - exlib ships as its own dll, so this ModSystem loads
  /// singly regardless of how many dependent mods are installed.
  /// </summary>
  public override void AssetsFinalize(ICoreAPI api)
  {
    MetalCatalogueLoader.Load(api);
    ExLiquids.Load(api);
    // The material-role catalogue (flux/fuel/ore/scrap/charge classification) + its mod-gated code
    // contributors. Loaded after the metal/liquid registries; exlib ships no role content itself.
    MaterialRoleLoader.Load(api);
  }

  public override void StartClientSide(ICoreClientAPI api)
  {
    // The library's own display preferences - currently the metric/imperial unit system, which lives
    // here rather than in a consumer because every mod from iwex up displays litres, atmospheres and
    // temperatures. Registered before the store loads so a saved choice has something to apply to.
    PreferenceRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // Load the per-player display-preference store (writes the file on first run). Dependent mods
    // contribute further preferences in their own StartClientSide; applying on LevelFinalize (after
    // every mod has started) picks up whatever they registered.
    ExPreferences.LoadConfig(api);

    // The handbook unit-conversion patch that makes authored metric prose read in imperial. Client
    // only, and guarded so it is applied once however many dependent mods are installed.
    if (!Harmony.HasAnyPatches(Mod.Info.ModID))
    {
      _harmony = new Harmony(Mod.Info.ModID);
      _harmony.PatchAll(GetType().Assembly);
    }

    // Apply the local player's saved choices once the world (and player) are ready.
    api.Event.LevelFinalize += () =>
      ExPreferences.ApplyForPlayer(api.World.Player.PlayerUID);

    // Register the library's own client commands: the shared .exmod root and its network-highlight
    // sub-command. Dependent mods attach their own sub-commands to the same root.
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // Apply every dependent mod's selected recipe-cost level (registered in their Start) to the live
    // recipes, so the client handbook/grid agree with the server. Runs after all mods' Start.
    ExRecipeProfiles.ApplyAll(api);
  }

  public override void StartServerSide(ICoreServerAPI api)
  {
    // Register the server-side counterpart: the universal exmod root surfaces here as /exmod, plus the
    // generic /exmod recipes <mod> <level> switch over the recipe profiles dependent mods register.
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // Apply every registered mod's selected recipe-cost level to the live (host-authoritative) recipes.
    ExRecipeProfiles.ApplyAll(api);
  }

  public override void Dispose()
  {
    _harmony?.UnpatchAll(Mod.Info.ModID);
    _harmony = null;
    base.Dispose();
  }
}
