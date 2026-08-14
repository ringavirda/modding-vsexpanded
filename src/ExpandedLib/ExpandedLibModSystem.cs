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
/// Entry point for the shared Expanded Lib mod (<c>exlib</c>). Registers the library's own blocks, block
/// entities and behaviours (the invisible structure filler, the multiblock structure behaviour) and points
/// <see cref="StructureFillers"/> at this mod's filler block, so every dependent mod's mega-blocks reuse one
/// shared filler. On the client it owns the per-player display-preferences store
/// (<see cref="Registries.Preferences.ExPreferences"/>, backed by <c>exmod_preferences.json</c>) and the metric/imperial
/// measure feature; dependent mods add further preferences and sub-commands from their own assemblies. The
/// block-network graph manager (<see cref="Blocks.Networks.BlockNetworkModSystem"/>) and the block-code
/// migrator (<see cref="Blocks.Migrations.BlockMigrationModSystem"/>) are separate auto-loaded ModSystems.
/// </summary>
public class ExpandedLibModSystem : ModSystem {
  // Client-side Harmony instance for the handbook unit patch (see StartClientSide).
  private Harmony? _harmony;

  public override void Start(ICoreAPI api) {
    // Load the library's own gameplay tunables, chiefly the block-network constants the concrete networks
    // in this assembly read. Before this runs the accessor holds the coded defaults, so reads are safe.
    ExlibValues.Load(api);

    // Auto-register the library's [BlockRegister]/[BlockEntityRegister]/[BlockBehaviorRegister] classes
    // (filler block + entity, the MultiblockStructure behaviour) under the exlib domain.
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // The shared filler block this lib ships; dependent mods' mega-blocks reserve their footprint cells with
    // it. Taken from the generated table rather than a hand-typed path, so the code cannot drift from the
    // definition.
    StructureFillers.FillerCode = new AssetLocation(
      ExlibBlocks.Structurefiller.Code
    );
  }

  /// <summary>
  /// Populates the shared metal catalogue from the loaded metal worldproperties and every domain's
  /// <c>config/metals</c>, after the asset-patch pipeline has merged all mods' JSON and before recipe and
  /// world finalize. Runs on both sides: the <c>config</c> and <c>worldproperties</c> categories are
  /// Universal. Consumers fall back to convention values for any metal not enriched here, so a partial load
  /// still yields a usable catalogue, and exlib's single dll means this fires once whatever is installed.
  /// </summary>
  public override void AssetsFinalize(ICoreAPI api) {
    MetalCatalogueLoader.Load(api);
    ExLiquids.Load(api);
    // The material-role catalogue (flux/fuel/ore/scrap/charge classification) and its mod-gated code
    // contributors. Must load after the metal and liquid registries; exlib ships no role content itself.
    MaterialRoleLoader.Load(api);

    // The merged process-stage catalogue. Read again here rather than only at inject time so the
    // registry the machines consult is the post-patch one; the emitter's earlier read cannot be.
    foreach (string error in Processes.ProcessRouteLoader.Load(api))
      api.Logger.Error("[exlib] invalid stage route - " + error);

    // The terminal half of the same contract: every machine's job table.
    foreach (string error in Processes.ProcessJobLoader.Load(api))
      api.Logger.Error("[exlib] invalid process job - " + error);
  }

  public override void StartClientSide(ICoreClientAPI api) {
    // The library's own display preferences, currently the metric/imperial unit system. Registered
    // before the .exmod sub-commands below, which resolve their preference once at registration time.
    PreferenceRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // Load the per-player display-preference store, writing the file on first run. Dependent mods contribute
    // further preferences in their own StartClientSide, which applying on LevelFinalize picks up.
    ExPreferences.LoadConfig(api);

    // The handbook unit-conversion patch that makes authored metric prose read in imperial. Client only,
    // and guarded so it is applied once however many dependent mods are installed.
    if (!Harmony.HasAnyPatches(Mod.Info.ModID)) {
      _harmony = new Harmony(Mod.Info.ModID);
      _harmony.PatchAll(GetType().Assembly);
    }

    // Apply the local player's saved choices once the world (and player) are ready.
    api.Event.LevelFinalize += () =>
      ExPreferences.ApplyForPlayer(api.World.Player.PlayerUID);

    // The library's own client commands: the shared .exmod root and its network-highlight sub-command.
    // Dependent mods attach their own sub-commands to the same root.
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // Apply every dependent mod's selected recipe-cost level, registered in their Start, to the live
    // recipes so the client handbook and grid agree with the server. Runs after all mods' Start.
    ExRecipeProfiles.ApplyAll(api);
  }

  public override void StartServerSide(ICoreServerAPI api) {
    // The server-side counterpart: the universal exmod root surfaces here as /exmod, plus the generic
    // /exmod recipes <mod> <level> switch over the recipe profiles dependent mods register.
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // Apply every registered mod's selected recipe-cost level to the live, host-authoritative recipes.
    ExRecipeProfiles.ApplyAll(api);
  }

  public override void Dispose() {
    _harmony?.UnpatchAll(Mod.Info.ModID);
    _harmony = null;
    base.Dispose();
  }
}
