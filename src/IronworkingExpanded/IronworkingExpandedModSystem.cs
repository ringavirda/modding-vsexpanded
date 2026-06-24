using ExpandedLib.Blocks.Networks;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Commands;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkMolten;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace IronworkingExpanded;

/// <summary>
/// Main mod system for Ironworking Expanded (iwex). Auto-registers every block / block-entity /
/// item / behavior class in this assembly via <see cref="EntityRegistry"/> and adds the mod's
/// creative tab. Builds on the shared exlib framework (structure fillers, RightClickConstructable,
/// attribute-driven registration) and the ppex mechanical-power / pipe content.
/// </summary>
public class IronworkingExpandedModSystem : ModSystem
{
  public override void Start(ICoreAPI api)
  {
    // Load gameplay tunables from ModConfig/iwex_values.json (writes defaults on first run).
    // Done before any block entity is constructed so the molten-system values apply.
    IwexValues.Load(api);

    // Auto-register every [BlockRegister]/[ItemRegister]/[BlockEntityRegister]/etc. declared here.
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);

    // The molten-metal network. iwex loads before smex, so the network type exists before any
    // dependent mod (smex) needs it. The unified "pipe" network is registered by ppex.
    var netManager = api.ModLoader.GetModSystem<BlockNetworkModSystem>();
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
