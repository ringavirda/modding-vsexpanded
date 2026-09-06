using ExpandedLib.Structures;
using ExpandedLib.Catalogues;
using ExpandedLib.Registries;
using HarmonyLib;
using System.ComponentModel;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib;

/// <summary>
/// Entry point for the shared Expanded Lib mod (<c>exlib</c>). Registers the library's own blocks, block
/// entities and behaviours (the invisible structure filler, the multiblock structure behaviour) and points
/// <see cref="StructureFillers"/> at this mod's filler block, so every dependent mod's mega-blocks reuse one
/// shared filler. On the client it owns the per-player display-preferences store
/// (<see cref="Registries.ExPreferences"/>, backed by <c>exmod_preferences.json</c>) and the metric/imperial
/// measure feature; dependent mods add further preferences and sub-commands from their own assemblies. The
/// block-network graph manager (<see cref="Networks.BlockNetworkModSystem"/>) and the block-code
/// migrator (<see cref="Migrations.BlockMigrationModSystem"/>) are separate auto-loaded ModSystems.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class ExpandedLibModSystem : ModSystem {
  // Client-side Harmony instance for the handbook unit patch (see StartClientSide).
  private Harmony? _harmony;

  public override void Start(ICoreAPI api) {
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
  /// Populates the shared liquid catalogue from every domain's <c>config/liquids</c>, after the
  /// asset-patch pipeline has merged all mods' JSON and before recipe and world finalize. Runs on
  /// both sides: the <c>config</c> category is Universal. Consumers fall back to convention values
  /// for any liquid not enriched here, so a partial load still yields a usable catalogue, and
  /// exlib's single dll means this fires once whatever is installed.
  /// </summary>
  public override void AssetsFinalize(ICoreAPI api) {
    // The content guards - dangling recipe codes, uncovered lang, pinned network nodes and the rest -
    // run here so a JSON-only mod gets them as a log line without ever opening the xUnit harness.
    // Also available on demand with /exmod verify; ExlibConfig.RunChecksOnLoad opts out of this pass.
    if (ExlibValues.RunChecksOnLoad)
      Checks.ExlibChecks.Log(api.Logger, Checks.ExlibChecks.All(api));

    LiquidCatalogueLoader.Load(api).Log(api.Logger);
    // The material-role catalogue (flux/fuel/ore/scrap/charge classification) and its mod-gated code
    // contributors. Must load after the metal and liquid registries - the domain layer's own IExModule.AssetsFinalize
    // (ExecuteOrder 0.03) loads the metal registry before this default-order pass runs; exlib ships
    // no role content itself.
    MaterialRoleLoader.Load(api).Log(api.Logger);

    // The merged process-stage catalogue. Read again here rather than only at inject time so the
    // registry the machines consult is the post-patch one; the emitter's earlier read cannot be.
    ProcessRouteLoader.Load(api).Log(api.Logger);

    // The terminal half of the same contract: every machine's job table.
    ProcessJobLoader.Load(api).Log(api.Logger);

    // What each store's items occupy. Also each store's whitelist: an item no rule names is one no rack
    // takes, so a missing file reads as an empty rack rather than as one that holds anything.
    BayOccupancyLoader.Load(api).Log(api.Logger);
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
    _harmony = ExHarmony.PatchOnce(Mod, GetType().Assembly);

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
    ExHarmony.UnpatchAll(Mod);
    _harmony = null;
    base.Dispose();
  }
}
