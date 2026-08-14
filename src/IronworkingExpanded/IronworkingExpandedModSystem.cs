using ExpandedLib.Blocks.Networks;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Commands;
using ExpandedLib.Registries.Entities;
using ExpandedLib.Registries.Recipes;
using HarmonyLib;
using IronworkingExpanded.BlockNetworkMolten;
using IronworkingExpanded.BlockNetworkMolten.Blocks;
using IronworkingExpanded.Compat;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace IronworkingExpanded;

/// <summary>
/// Main mod system for Ironworking Expanded (iwex). Auto-registers every block / block-entity /
/// item / behavior class in this assembly via <see cref="EntityRegistry"/> and adds the mod's
/// creative tab; registers the pipe, molten-metal and mechanical-energy network types; registers
/// other mods' iron-ore types for the blast furnace hoppers; and applies the Harmony patch that
/// wires blast-mix into the vanilla coal pile.
/// </summary>
public class IronworkingExpandedModSystem : ModSystem {
  private Harmony? _harmony;

  public override void Dispose() {
    _harmony?.UnpatchAll(Mod.Info.ModID);
    _harmony = null;
    base.Dispose();
  }

  public override void Start(ICoreAPI api) {
    // Gameplay tunables from ModConfig/iwex_values.json (writes defaults on first run). Must run
    // before any block entity is constructed so the molten-system values apply.
    IwexValues.Load(api);

    // Recipe-cost catalogue plus its profile, so exlib's shared apply pass and the generic
    // /exmod recipes iwex <level> command can drive it (see ExRecipeProfiles).
    IwexRecipeValues.Load(api);
    ExRecipeProfiles.Register(
      new RecipeProfile {
        Code = Mod.Info.ModID,
        Catalogue = () => IwexRecipeValues.Recipes,
        Defaults = IwexRecipeConfig.DefaultCatalogue,
        GetLevel = () => IwexValues.RecipeLevel,
        SetLevel = level => IwexValues.Edit(c => c.RecipeLevel = level),
        SaveCatalogue = IwexRecipeValues.Save,
      }
    );

    // Other mods' iron ore types, used by the blast furnace's reinforced hopper.
    IronOreCompat.Init(api);

    // Blast-mix burn-to-slag is patched into the vanilla coal pile rather than replacing its
    // registered class, so other mods touching the coal pile can coexist.
    if (!Harmony.HasAnyPatches(Mod.Info.ModID)) {
      _harmony = new Harmony(Mod.Info.ModID);
      _harmony.PatchAll(GetType().Assembly);
    }

    // The plated pipe segment's ratings, read live from this mod's config. Higher pipe tiers register
    // their own (lpex cast, hpex rolled), keyed by the block's `tier` variant in BlockPipe.
    BlockPipe.RegisterBurst(
      BlockPipe.PlatedTier,
      () => IwexValues.PlatedPipeBurstPressure
    );
    BlockPipe.RegisterThroughput(
      BlockPipe.PlatedTier,
      () => IwexValues.PlatedPipeThroughput
    );
    BlockPipe.RegisterJoint(BlockPipe.PlatedTier, BlockPipe.FlangedJoint);

    // Registers every [BlockRegister]/[ItemRegister]/[BlockEntityRegister]/etc. declared here, and
    // discovers any co-located code-first block definitions (IExBlockDefProvider) for injection.
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // Save compatibility for pipes placed while the pipe bases lived in iwex. Those worlds carry the
    // old iwex.* class strings, which the scan above cannot re-claim now the classes live in exlib.
    api.RegisterBlockEntityClass(
      "iwex.BlockEntityPipe",
      typeof(BlockEntityPipe)
    );
    api.RegisterBlockEntityClass(
      "iwex.BlockEntityPipePassthrough",
      typeof(BlockEntityPipePassthrough)
    );

    // iwex owns the networks its pipes and canals ride, and registers them before any dependent mod
    // (lpex, smex) needs them. The unified "pipe" network (gas + liquid pools) carries a chimney-vent
    // strategy that draws gas through a chimney-ventable fitting's top connector, at a rate read live
    // from iwex's config.
    var netManager = api.ModLoader.GetModSystem<BlockNetworkModSystem>();
    netManager.RegisterNetworkType(
      "pipe",
      () =>
        new PipeNetwork(
          netManager,
          new ChimneyVent(() => IwexValues.ChimneyGasDrawRate)
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
      api.Logger.Error("[iwex] invalid mold pattern - " + error);

    // Same for the mill's tooling: a bad gap sequence fails by doing nothing when stock is fed in,
    // so it is caught at load rather than at the rolls.
    foreach (
      string error in BlockStructures.Forming.RollSetValidation.Validate(
        api.World.Collectibles
      )
    )
      api.Logger.Error("[iwex] invalid roll set - " + error);
  }

  public override void StartClientSide(ICoreClientAPI api) {
    ExCreativeTabs.EnsureTab(Mod.Info.ModID);
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }

  public override void StartServerSide(ICoreServerAPI api) {
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // In-hand mold safety: spill a filled mold moved out of the active hand, burn a bare hand holding
    // a hot one. Applies to iwex cast molds always, and to vanilla clay molds when EnhanceVanillaMolds
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
    if (temp < IwexValues.MoldBurnMinTemperature || HasHandProtection(player))
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
