using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockNetworkMolten.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockNetworkMolten.Blocks;

/// <summary>
/// Mold pedestal: a canal endpoint that holds a small tool mold and fills it from
/// the network's liquid metal. Sneak + right-click places/removes the mold;
/// Ctrl + right-click toggles pouring.
/// </summary>
[BlockRegister]
public partial class BlockMoltenCanalMoldPedestal : BlockMoltenCanalTap {
  #region Code-first definition

  // Fill geometry of the cast mold the pedestal holds, read at runtime from the block's own attributes.
  // The pedestal's own canal fill uses the base FillStart/FillHeight/FillQuadsByLevel accessors.
  public JsonObject? MoldFillQuadsByLevel =>
    Attributes?["moldFillQuadsByLevel"];
  public int MoldFillStart => Attributes?["moldFillStart"].AsInt(12) ?? 12;
  public int MoldFillHeight => Attributes?["moldFillHeight"].AsInt(1) ?? 1;

  /// <summary>The two mold-pedestal blocktypes (fire-brick and cobblestone skins), built off the shared
  /// canal-family surface and carrying the separate <c>moldFill*</c> geometry for the mold they
  /// hold.</summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain) {
    foreach (CanalSkin skin in CanalSkins)
      yield return CanalFamilyDef(
          domain,
          $"molten/{skin.Folder}/moldpedestal",
          skin,
          "moldpedestal",
          1,
          "*-moldpedestal-*-s",
          ["n", "w", "s", "e"],
          new[]
          {
            new
            {
              x1 = 7,
              z1 = 0,
              x2 = 9,
              z2 = 5,
            },
          },
          "iiex:molten/canal/moldpedestal",
          [
            ("*-moldpedestal-*-n", null),
            ("*-moldpedestal-*-w", 90),
            ("*-moldpedestal-*-s", 180),
            ("*-moldpedestal-*-e", 270),
          ]
        )
        .Attribute("moldFillStart", 12)
        .Attribute("moldFillHeight", 1)
        .Attribute(
          "moldFillQuadsByLevel",
          new[]
          {
            new
            {
              x1 = 2,
              z1 = 2,
              x2 = 14,
              z2 = 14,
            },
          }
        )
        .Class<BlockMoltenCanalMoldPedestal>()
        .EntityClass("iiex.BlockEntityMoltenCanalMoldPedestal");
  }

  #endregion

  private ItemStack[]? _acceptedMolds;

  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1f
  ) {
    // The placed mold and any cast metal live on the block entity, not as a separate block, so drop
    // them before the pedestal is removed or they are lost.
    if (
      world.Side == EnumAppSide.Server
      && byPlayer is not { WorldData.CurrentGameMode: EnumGameMode.Creative }
      && world.BlockAccessor.GetBlockEntity(pos)
        is BlockEntityMoltenCanalMoldPedestal be
      && be.IsMold
      && be.MoldStack != null
    ) {
      world.SpawnItemEntity(be.RemoveMold(), pos.ToVec3d().Add(0.5, 0.5, 0.5));
    }

    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);

    var moldList = new List<ItemStack>();
    foreach (var block in api.World.Blocks) {
      if (MoldKinds.FitsPedestal(block))
        moldList.Add(new ItemStack(block));
    }
    _acceptedMolds = moldList.ToArray();
  }

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is not BlockEntityMoltenCanalMoldPedestal be
    )
      return false;

    // Sneak (ShiftKey) + RMB places or removes the mold; CtrlKey + RMB toggles pouring. Plain RMB chips
    // out a clogged (solidified) cell with a chisel and hammer, like a canal or the start block.
    bool sneak = byPlayer.Entity.Controls.ShiftKey;
    bool opposite = byPlayer.Entity.Controls.CtrlKey;
    if (!sneak && !opposite) {
      if (!be.Solidified)
        return false;
      // Call the shared chisel handling directly. Deferring to base lands in
      // BlockMoltenCanalTap.OnBlockInteractStart, whose `is not BlockEntityMoltenCanalTap` guard rejects
      // this pedestal's block entity and drops the chisel-out: the pedestal block extends the tap block,
      // but their block entities are siblings.
      return MoltenChisel.TryChisel(
          world,
          byPlayer,
          blockSel.Position,
          be,
          ExSounds.StoneCrush
        ) != ChiselOutcome.NotChiseling;
    }

    if (world.Side == EnumAppSide.Client)
      return true;

    if (opposite) {
      be.TryTogglePouring();
      return true;
    }

    if (!be.IsMold) {
      var heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
      if (heldSlot?.Itemstack?.Block is not BlockToolMold)
        return false;

      if (!MoldKinds.FitsPedestal(heldSlot.Itemstack.Block)) {
        (byPlayer as IServerPlayer)?.SendIngameError("iiex-moldtoolarge");
        return false;
      }

      be.AddMold(heldSlot.Itemstack);
      heldSlot.TakeOut(1);
      heldSlot.MarkDirty();
    } else {
      // A mold full of still-liquid metal may only be taken into an empty
      // hand - anywhere else in the inventory it instantly spills.
      bool liquidMold = MoltenMoldSpill.IsLiquidContent(
        world,
        be.MoldMetalContent,
        be.MoldCurrentUnits
      );
      if (
        liquidMold
        && MoltenMoldSpill.DenyLiquidPickup(
          world,
          byPlayer,
          be.MoldMetalContent,
          be.MoldCurrentUnits
        )
      )
        return true;

      var moldStack = be.RemoveMold();
      MoltenMoldSpill.GiveMoldStack(
        world,
        byPlayer,
        moldStack,
        liquidMold,
        blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5)
      );
    }
    ExSounds.Play(world.Api, blockSel.Position, ExSounds.Ingot, 0.7f);
    be.MarkDirty(true);
    return true;
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) {
    bool isMold =
      world.BlockAccessor.GetBlockEntity(selection.Position)
      is BlockEntityMoltenCanalMoldPedestal { IsMold: true };

    var toggle = new WorldInteraction {
      ActionLangCode = "iiex:blockhelp-canal-togglepour",
      MouseButton = EnumMouseButton.Right,
      HotKeyCode = "sprint",
    };

    var result = new List<WorldInteraction>();
    if (!isMold)
      result.Add(
        new WorldInteraction {
          ActionLangCode = "iiex:blockhelp-pedestal-placemold",
          MouseButton = EnumMouseButton.Right,
          HotKeyCode = "sneak",
          Itemstacks = _acceptedMolds,
        }
      );
    else
      result.Add(
        new WorldInteraction {
          ActionLangCode = "iiex:blockhelp-pedestal-removemold",
          MouseButton = EnumMouseButton.Right,
          HotKeyCode = "sneak",
        }
      );
    result.Add(toggle);

    // A clogged (solidified) pedestal cell is chipped clear like a canal.
    WorldInteraction? chisel = ChiselClearInteraction(
      world,
      selection.Position
    );
    if (chisel != null)
      result.Add(chisel);

    return result.ToArray();
  }
}
