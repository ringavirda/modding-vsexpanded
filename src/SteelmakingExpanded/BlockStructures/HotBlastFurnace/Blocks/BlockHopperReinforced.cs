using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SteelmakingExpanded.BlockStructures.HotBlastFurnace.Blocks;

/// <summary>
/// The reinforced hopper that feeds the blast furnace. A plain charge tank: right-click with charge
/// fills it, empty-handed right-click empties it, and Ctrl plus empty-handed right-click toggles the
/// dropping of the bell hopper below. The burden itself is made by iwex's burdenmaker.
/// </summary>
[BlockRegister]
public partial class BlockHopperReinforced : Block, IExBlockDefProvider {
  /// <summary>The reinforced hopper blocktype. Not a container: it holds a single burden tank, so it
  /// carries no inventory slots and no dialog.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "hopperreinforced", "blastfurnace/hopperreinforced")
        .Class<BlockHopperReinforced>()
        .EntityClass("smex.BlockEntityHopperReinforced")
        .CreativeCommon("*")
        .Material(EnumBlockMaterial.Metal)
        .MaxStackSize(1)
        .LightAbsorption(0)
        .Shape("smex:blastfurnace/hopper-reinforced")
        .NonSolid()
        .Resistance(1.75f)
        .MetalSounds(),
    ];

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is not BlockEntityHopperReinforced be
    )
      return base.OnBlockInteractStart(world, byPlayer, blockSel);

    // All mutation is server-authoritative; the click is swallowed on both sides so nothing is placed
    // against the hopper face.
    if (world.Side == EnumAppSide.Server) {
      ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;
      bool ctrl = byPlayer.Entity.Controls.CtrlKey;

      if (active?.Empty != false) {
        // Empty-handed: Ctrl toggles the bell's dropping, a plain click draws the tank out.
        if (ctrl)
          be.ToggleBellDropping();
        else
          Withdraw(world, byPlayer, be, blockSel.Position);
      } else if (be.IsChargeItem(active.Itemstack)) {
        // Charge in hand: fill the tank (Ctrl deposits the whole held stack). A mismatched grade is
        // reported; a full tank is silent, since the block info already shows it.
        //
        // The gate must stay `be.IsChargeItem` - an identity test over everything the furnace
        // charges. A burden-only or grade-aware gate (Burden.IsAny, be.Accepts) drops coke and
        // mismatched grades into the Ctrl branch below, where they toggle the bell instead of
        // loading, and makes the wronggrade error unreachable. Grade is TryDeposit's question.
        if (!be.TryDeposit(active, ctrl) && !be.IsFull)
          (byPlayer as IServerPlayer)?.SendIngameError(
            "smex-hopper-wronggrade"
          );
      } else if (ctrl) {
        // Holding something the furnace does not charge: Ctrl still toggles the bell.
        be.ToggleBellDropping();
      }
    }

    return true;
  }

  private static void Withdraw(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntityHopperReinforced be,
    BlockPos pos
  ) {
    ItemStack? taken = be.TryWithdraw();
    if (
      taken != null
      && byPlayer.InventoryManager?.TryGiveItemstack(taken) != true
    )
      world.SpawnItemEntity(taken, pos.ToVec3d().Add(0.5, 0.5, 0.5));
  }

  // A broken hopper drops itself plus whatever the tank still held, so the charge is never lost.
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) {
    var drops = new List<ItemStack>(
      base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier)
    );
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityHopperReinforced be
      && be.TankContents is { } burden
    )
      drops.Add(burden.Clone());
    return [.. drops];
  }

  #region Interaction help

  // Resolved once (the block is a singleton): a burden stack shown in the "add" hints.
  private ItemStack[]? _burdenStack;

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) {
    var baseHelp =
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [];

    ItemStack[] burden = _burdenStack ??= ResolveBurdenStack(world);
    var help = new List<WorldInteraction>
    {
      new()
      {
        ActionLangCode = "smex:blockhelp-hopper-add",
        MouseButton = EnumMouseButton.Right,
        Itemstacks = burden,
      },
      new()
      {
        ActionLangCode = "smex:blockhelp-hopper-addstack",
        MouseButton = EnumMouseButton.Right,
        HotKeyCode = "ctrl",
        Itemstacks = burden,
      },
    };

    if (
      world.BlockAccessor.GetBlockEntity(selection.Position)
        is BlockEntityHopperReinforced be
      && be.TankCount > 0
    )
      help.Add(
        new WorldInteraction {
          ActionLangCode = "smex:blockhelp-hopper-take",
          MouseButton = EnumMouseButton.Right,
        }
      );

    if (
      world.BlockAccessor.GetBlockEntity(selection.Position.DownCopy())
      is BlockEntityHopperBell
    )
      help.Add(
        new WorldInteraction {
          ActionLangCode = "smex:blockhelp-hopper-toggle",
          HotKeyCodes = ["ctrl"],
          MouseButton = EnumMouseButton.Right,
        }
      );

    return baseHelp.Concat(help).ToArray();
  }

  private static ItemStack[] ResolveBurdenStack(IWorldAccessor world) {
    Item? burden = world.GetItem(new AssetLocation("iwex", "burden"));
    return burden == null ? [] : [new ItemStack(burden)];
  }

  #endregion
}
