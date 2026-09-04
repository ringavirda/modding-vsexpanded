using ExpandedLib.Blocks.Networks;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Commands;
using ExpandedLib.Registries.Entities;
using ExpandedLib.Registries.Preferences;
using ExpandedLib.Registries.Recipes;
using HarmonyLib;
using IronIndustryExpanded.BlockNetworkMolten;
using IronIndustryExpanded.BlockNetworkMolten.Blocks;
using IronIndustryExpanded.BlockNetworkPipe.BlockEntities;
using IronIndustryExpanded.BlockStructures.Boiler.BlockEntities;
using IronIndustryExpanded.BlockStructures.Engine.BlockEntities;
using IronIndustryExpanded.BlockStructures.ManualPump.BlockEntities;
using IronIndustryExpanded.Compat;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace IronIndustryExpanded;

/// <summary>
/// Main mod system for Iron Industry Expanded (iiex). Auto-registers every block / block-entity /
/// item / behavior class in this assembly via <see cref="EntityRegistry"/> and adds the mod's
/// creative tab; registers the pipe, molten-metal and mechanical-energy network types and the two
/// pipe tiers' ratings; registers other mods' iron-ore types for the blast furnace hoppers; and
/// applies the Harmony patches that wire blast-mix into the vanilla coal pile and report a chimney
/// venting a pipe.
/// <para>
/// One <see cref="ModSystem"/> for the whole assembly, not one per merged mod. A second would call
/// <see cref="EntityRegistry.RegisterAll"/> over the same assembly and register every class twice;
/// definition registration is idempotent and both compute the same <c>Mod.Info.ModID</c>, so the
/// goldens still match and the Harmony guard still skips - it would work by coincidence.
/// </para>
/// </summary>
public class IronIndustryExpandedModSystem : ModSystem {
  private Harmony? _harmony;

  public override void Dispose() {
    _harmony?.UnpatchAll(Mod.Info.ModID);
    _harmony = null;
    base.Dispose();
  }

  public override void Start(ICoreAPI api) {
    // Gameplay tunables from ModConfig/ex_values.json (writes defaults on first run). Must run
    // before any block entity is constructed so the molten-system values apply.
    IiexValues.Load(api);
    // Drives the exlib RCC salvage ratio for the engines and boilers from the live config.
    ExpandedLib.Blocks.Construction.ExRccSettings.RegisterBrokenDropsRatio(
      Mod.Info.ModID,
      () => IiexValues.RccBrokenDropsRatio
    );

    // Recipe-cost catalogue plus its profile, so exlib's shared apply pass and the generic
    // /exmod recipes iiex <level> command can drive it (see ExRecipeProfiles).
    IiexRecipeValues.Load(api);
    ExRecipeProfiles.Register(
      new RecipeProfile {
        Code = Mod.Info.ModID,
        Catalogue = () => IiexRecipeValues.Recipes,
        Defaults = IiexRecipeConfig.DefaultCatalogue,
        GetLevel = () => IiexValues.RecipeLevel,
        SetLevel = level => IiexValues.Edit(c => c.RecipeLevel = level),
        SaveCatalogue = IiexRecipeValues.Save,
      }
    );

    // No ore-compat registration here any more: other mods' iron ore types are rows in
    // assets/iiex/config/materialroles.json, each gated by `requiresMod`, and the loader applies them.

    // Blast-mix burn-to-slag is patched into the vanilla coal pile, and the vanilla chimney's
    // look-at info is patched to report a chimney venting a pipe (the gas draw itself runs in
    // PipeNetwork's tick). Both are patches rather than replaced registered classes, so other mods
    // touching the same vanilla blocks can coexist.
    if (!Harmony.HasAnyPatches(Mod.Info.ModID)) {
      _harmony = new Harmony(Mod.Info.ModID);
      _harmony.PatchAll(GetType().Assembly);
    }

    // Both pipe tiers' ratings, read live from this mod's config, keyed by the block's `tier` variant
    // in BlockPipe; siex registers the rolled tier's own. Cast is square and plated like the plated
    // tier, so the two runs interconnect - which is the point of keeping both: plated is the
    // bootstrap rung a player plumbs the works with before steam exists.
    BlockPipe.RegisterBurst(
      BlockPipe.PlatedTier,
      () => IiexValues.PlatedPipeBurstPressure
    );
    BlockPipe.RegisterThroughput(
      BlockPipe.PlatedTier,
      () => IiexValues.PlatedPipeThroughput
    );
    BlockPipe.RegisterJoint(BlockPipe.PlatedTier, BlockPipe.FlangedJoint);
    BlockPipe.RegisterBurst(
      BlockPipe.CastTier,
      () => IiexValues.CastPipeBurstPressure
    );
    BlockPipe.RegisterThroughput(
      BlockPipe.CastTier,
      () => IiexValues.CastPipeThroughput
    );
    BlockPipe.RegisterJoint(BlockPipe.CastTier, BlockPipe.FlangedJoint);

    // Registers every [BlockRegister]/[ItemRegister]/[BlockEntityRegister]/etc. declared here, and
    // discovers any co-located code-first block definitions (IExBlockDefProvider) for injection.
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);

    foreach (var (legacyKey, type) in LegacyEntityClasses)
      api.RegisterBlockEntityClass(legacyKey, type);

    // iiex owns the networks its pipes and canals ride, and registers them before any dependent mod
    // (siex, siex) needs them. The unified "pipe" network (gas + liquid pools) carries a chimney-vent
    // strategy that draws gas through a chimney-ventable fitting's top connector, at a rate read live
    // from this mod's config.
    var netManager = api.ModLoader.GetModSystem<BlockNetworkModSystem>();
    netManager.RegisterNetworkType(
      "pipe",
      () =>
        new PipeNetwork(
          netManager,
          new ChimneyVent(() => IiexValues.ChimneyGasDrawRate)
        )
    );
    netManager.RegisterNetworkType(
      "molten",
      () => new MoltenNetwork(netManager)
    );
    // The mechanical-energy network the flywheel buffers and the forming machines (rolling mill,
    // puddling) draw from. Registered here because the iron tier is where it first appears: a
    // water-wheel or windmill bridges vanilla MP into it via the flywheel. Friction and burst
    // constants are read live from ExlibValues, so the network takes no per-instance config.
    netManager.RegisterNetworkType(
      "mpenergy",
      () => new MpEnergyNetwork(netManager)
    );
  }

  #region Legacy block-entity class keys

  /// <summary>
  /// Block-entity class strings from earlier domains, each pointed at the type that answers for it now.
  /// A class string lives in the SAVE, not in any definition: <c>CodeRelocation</c> remaps block
  /// codes and never touches it, no golden covers it, and an unresolved one drops the block entity -
  /// a boiler keeps its block and loses its water. So it needs its own table, and this is it.
  /// <para>
  /// Bounded by what shipped, read out of the release zips rather than out of source: the
  /// <c>entityClass</c> values in <c>ppex_0.6.8.zip</c> (fifteen) and <c>smex_0.9.8.zip</c> (nineteen)
  /// are the ground truth. <c>iwex</c>/<c>lpex</c> never shipped, so their two entries cover
  /// development worlds only. Rows are omitted where the block itself has no migration - the eight
  /// converter/cowper/tiered-part families in <c>ReleasedCodeDebt</c> - since an alias cannot save
  /// state for a block that does not arrive. Two of ppex's belong to siex and are registered there;
  /// <c>ppex.BlockEntityMpFluidPump</c> has no live type until that pump is ported back from the
  /// <c>0.9-support</c> branch.
  /// </para>
  /// </summary>
  public static readonly (string Key, System.Type Type)[] LegacyEntityClasses =
  [
    // The pipe bases moved iwex -> exlib, so neither domain's key resolves by scan any more.
    ("ppex.BlockEntityPipe", typeof(BlockEntityPipe)),
    ("ppex.BlockEntityPipePassthrough", typeof(BlockEntityPipePassthrough)),
    ("iwex.BlockEntityPipe", typeof(BlockEntityPipe)),
    ("iwex.BlockEntityPipePassthrough", typeof(BlockEntityPipePassthrough)),
    // Fittings and machines the merge moved to iiex.*. `Mp` -> `MP` in the generator's type name is
    // the one spelling that changed as well as the domain.
    ("ppex.BlockEntityFluidIntake", typeof(BlockEntityFluidIntake)),
    ("ppex.BlockEntityPipeOutlet", typeof(BlockEntityPipeOutlet)),
    ("ppex.BlockEntityPressureValve", typeof(BlockEntityPressureValve)),
    ("ppex.BlockEntityValve", typeof(BlockEntityValve)),
    ("ppex.BlockEntitySteamCondenser", typeof(BlockEntitySteamCondenser)),
    ("ppex.BlockEntityBoilerCornish", typeof(BlockEntityBoilerCornish)),
    ("ppex.BlockEntityEngineWatt", typeof(BlockEntityEngineWatt)),
    ("ppex.BlockEntityEngineFluidPump", typeof(BlockEntityEngineFluidPump)),
    ("ppex.BlockEntityEngineMpGenerator", typeof(BlockEntityEngineMPGenerator)),
    ("ppex.BlockEntityManualFluidPump", typeof(BlockEntityManualFluidPump)),
    // The ironmaking and molten content that shipped in siex 0.9.8 and relocated here. These are the
    // ones with real player worlds behind them: SmexToIiexMigration moves the block codes and copies
    // the old block entity's tree, but that tree is read from a LIVE block entity - so without the
    // class key the game never constructs it, `oldState` arrives null, and every canal, barrel and
    // frozen pool migrates empty.
    (
      "smex.BlockEntityMoltenCanal",
      typeof(BlockNetworkMolten.BlockEntities.BlockEntityMoltenCanal)
    ),
    (
      "smex.BlockEntityMoltenCanalStart",
      typeof(BlockNetworkMolten.BlockEntities.BlockEntityMoltenCanalStart)
    ),
    (
      "smex.BlockEntityMoltenCanalTap",
      typeof(BlockNetworkMolten.BlockEntities.BlockEntityMoltenCanalTap)
    ),
    (
      "smex.BlockEntityMoltenCanalMoldPedestal",
      typeof(BlockNetworkMolten.BlockEntities.BlockEntityMoltenCanalMoldPedestal)
    ),
    (
      "smex.BlockEntityMoltenBarrel",
      typeof(BlockNetworkMolten.BlockEntities.BlockEntityMoltenBarrel)
    ),
    (
      "smex.BlockEntitySlag",
      typeof(BlockStructures.Products.BlockEntities.BlockEntitySlag)
    ),
    // Renamed as well as relocated: the two frozen-melt blocks merged into one variant-grouped
    // hearthmetal, the MP blower became the twin-tub, and siex's one BlockEntityBlastFurnace class
    // backed the charge DOOR (blastfurnace/door.json), not the core - the core shipped with none.
    (
      "smex.BlockEntitySolidifiedIron",
      typeof(BlockStructures.Products.BlockEntities.BlockEntityHearthMetal)
    ),
    (
      "smex.BlockEntityMpBlower",
      typeof(BlockStructures.Furnaces.BlockEntities.BlockEntityTwinTubMPBlower)
    ),
    (
      "smex.BlockEntityBlastFurnace",
      typeof(BlockStructures.Furnaces.BlockEntities.BlockEntityChargeDoor)
    ),
    (
      "smex.BlockEntityBlastFurnaceTap",
      typeof(BlockStructures.Furnaces.BlockEntities.BlockEntityFurnaceTap)
    ),
    (
      "smex.BlockEntityTuyere",
      typeof(BlockStructures.Furnaces.BlockEntities.BlockEntityTuyere)
    ),
  ];

  #endregion

  public override void AssetsFinalize(ICoreAPI api) {
    base.AssetsFinalize(api);
    // A malformed mold pattern is reported at load rather than failing as a silent no-op when the
    // pattern is rammed into a casting cell. Server-side only; the same defs load on the client.
    if (api.Side != EnumAppSide.Server)
      return;

    foreach (
      string error in BlockStructures.Casting.PatternValidation.Validate(
        api.World.Collectibles
      )
    )
      api.Logger.Error("[iiex] invalid mold pattern - " + error);

    // Same for the mill's tooling: a bad gap sequence fails by doing nothing when stock is fed in,
    // so it is caught at load rather than at the rolls.
    foreach (
      string error in BlockStructures.Forming.RollSetValidation.Validate(
        api.World.Collectibles
      )
    )
      api.Logger.Error("[iiex] invalid roll set - " + error);
  }

  public override void StartClientSide(ICoreClientAPI api) {
    ExCreativeTabs.EnsureTab(Mod.Info.ModID);

    // Registers any iiex display preferences into exlib's shared store and builds their .exmod
    // sub-commands. exlib loads, persists and applies the values. The measure feature (the
    // metric/imperial preference and the handbook unit patch) belongs to exlib, not here.
    PreferenceRegistry.RegisterAll(api, Mod, GetType().Assembly);
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }

  public override void StartServerSide(ICoreServerAPI api) {
    // Server-side sub-commands. The recipe-cost level itself is applied centrally by exlib
    // (ExRecipeProfiles); `/exmod recipes iiex <level>` is the generic switch.
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // In-hand mold safety: spill a filled mold moved out of the active hand, burn a bare hand holding
    // a hot one. Applies to iiex cast molds always, and to vanilla clay molds when EnhanceVanillaMolds
    // is on (see MoltenMoldSpill.IsHandledMold).
    api.Event.AfterActiveSlotChanged += (player, ev) =>
      OnAfterActiveSlotChanged(api, player, ev);
    api.Event.RegisterGameTickListener(_ => OnMoldServerTick(api), 1000);
  }

  #region Mold safety (spill / burn)

  private static void OnMoldServerTick(ICoreServerAPI api) {
    foreach (var p in api.World.AllOnlinePlayers) {
      if (p is not IServerPlayer player || player.Entity?.Alive != true)
        continue;

      var invManager = player.InventoryManager;
      if (invManager == null)
        continue;

      ItemSlot? activeSlot = invManager.ActiveHotbarSlot;

      BurnIfHoldingHotMold(api, player, activeSlot);

      foreach (var inv in invManager.InventoriesOrdered)
        SpillMoltenMolds(inv, activeSlot, api, player);

      foreach (var inv in invManager.OpenedInventories)
        SpillMoltenMolds(inv, activeSlot, api, player);
    }
  }

  private static void SpillMoltenMolds(
    IInventory inv,
    ItemSlot? activeSlot,
    ICoreServerAPI api,
    IServerPlayer player
  ) {
    if (inv == null || inv.ClassName == GlobalConstants.creativeInvClassName)
      return;

    foreach (var slot in inv)
      if (slot != activeSlot)
        MoltenMoldSpill.SpillIfMolten(slot, api.World, player);
  }

  private static void BurnIfHoldingHotMold(
    ICoreServerAPI api,
    IServerPlayer player,
    ItemSlot? activeSlot
  ) {
    var stack = activeSlot?.Itemstack;
    if (stack == null || !MoltenMoldSpill.IsHandledMold(stack.Block))
      return;

    var (contents, fill) = MoltenContents.Read(
      stack,
      MoltenContents.MoldUnitsKey,
      api.World
    );
    if (contents?.Collectible == null || fill <= 0)
      return;

    float temp = contents.Collectible.GetTemperature(api.World, contents);
    if (temp < IiexValues.MoldBurnMinTemperature || HasHandProtection(player))
      return;

    player.Entity.ReceiveDamage(
      new DamageSource {
        Source = EnumDamageSource.Block,
        Type = EnumDamageType.Fire,
      },
      1f
    );
  }

  private static bool HasHandProtection(IServerPlayer player) {
    var charInv = player.InventoryManager?.GetOwnInventory(
      GlobalConstants.characterInvClassName
    );
    int handSlot = (int)EnumCharacterDressType.Hand;
    if (charInv == null || handSlot >= charInv.Count)
      return false;

    string? path = charInv[handSlot]?.Itemstack?.Collectible?.Code?.Path;
    return path
      is "clothes-hand-heavy-leather-gloves"
        or "clothes-nadiya-hand-blacksmith";
  }

  private static void OnAfterActiveSlotChanged(
    ICoreServerAPI api,
    IServerPlayer player,
    ActiveSlotChangeEventArgs ev
  ) {
    var hotbar = player.InventoryManager?.GetHotbarInventory();
    if (hotbar == null || ev.FromSlot < 0 || ev.FromSlot >= hotbar.Count)
      return;

    MoltenMoldSpill.SpillIfMolten(hotbar[ev.FromSlot], api.World, player);
  }

  #endregion
}
