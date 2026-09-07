using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Helpers;
using ExpandedLib.Registries;
using ExpandedLib.Structures;
using IronIndustryExpanded.BlockNetworkMolten.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockNetworkMolten.Blocks;

/// <summary>
/// Canal tap: pours the network's liquid metal downward into a molten barrel or a
/// large tool mold placed beneath it. Sneak + right-click adds/removes the
/// barrel/mold; Ctrl + right-click toggles pouring on and off.
/// </summary>
[BlockRegister]
public partial class BlockMoltenCanalTap : BlockMoltenCanal {
  // Barrel + large molds (anvil, helve hammer) that can be cast in the tap.
  private ItemStack[]? _acceptedContents;

  #region Code-first definition

  /// <summary>The canal tap blocktype. A single-skin endpoint with its own burned-clay and metal-sheet
  /// textures rather than the brick and cobble skins, so it is declared directly instead of through the
  /// shared canal-family surface.</summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "molten-canal", "molten/canal/tap")
        .Class<BlockMoltenCanalTap>()
        .EntityClass("iiex.BlockEntityMoltenCanalTap")
        .Material(EnumBlockMaterial.Stone)
        .Sound("walk", "game:walk/stone")
        .SoundByTool(
          EnumTool.Pickaxe,
          "game:block/rock-hit-pickaxe",
          "game:block/rock-break-pickaxe"
        )
        .MaxStackSize(1)
        .CreativeCommon("*-tap-s")
        .Attribute("fillHeight", 1)
        .Attribute("fillStart", 14)
        .Attribute(
          "fillQuadsByLevel",
          new[]
          {
            new
            {
              x1 = 7,
              z1 = 0,
              x2 = 9,
              z2 = 5,
            },
          }
        )
        .Handbook("molten-canal-tap-*")
        .Texture("burned", "game:block/clay/vessel/sides/burned")
        .Texture("steel3", "game:block/metal/riveted/steel3")
        .Texture("iron3", "game:block/metal/sheet-plain/iron3")
        .Texture("steel32", "game:block/metal/sheet-plain/steel3")
        .Behavior("Lockable")
        .VariantGroup("type", "tap")
        .VariantGroup("orientation", "n", "w", "s", "e")
        .NetworkOriented()
        .ShapeByType("*-tap-n", "iiex:molten/canal/tap")
        .ShapeByType("*-tap-w", "iiex:molten/canal/tap", rotateY: 90)
        .ShapeByType("*-tap-s", "iiex:molten/canal/tap", rotateY: 180)
        .ShapeByType("*-tap-e", "iiex:molten/canal/tap", rotateY: 270)
        .CollisionBox(0.0625f, 0.0625f, 0f, 0.9375f, 0.9375f, 0.9375f)
        .SelectionBox(0.0625f, 0.0625f, 0f, 0.9375f, 0.9375f, 0.9375f)
        .NonSolid(),
    ];

  #endregion

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);

    var list = new List<ItemStack>();
    // Collect every construction variant by class: the bare code `iiex:molten-barrel` does not resolve
    // now that the barrel carries construction(plated|cast), and a lookup by code would drop the barrel
    // from the accepted-contents list without any visible failure.
    foreach (var block in api.World.Blocks)
      if (block is BlockMoltenBarrel)
        list.Add(new ItemStack(block));
    foreach (var block in api.World.Blocks)
      if (MoldKinds.IsLarge(block))
        list.Add(new ItemStack(block));
    _acceptedContents = list.ToArray();
  }

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is not BlockEntityMoltenCanalTap be
    )
      return false;

    // Sneak (ShiftKey) + RMB places or removes the barrel; CtrlKey + RMB toggles pouring. Plain RMB chips
    // out a clogged (solidified) cell with a chisel and hammer, like a canal or the start block.
    bool sneak = byPlayer.Entity.Controls.ShiftKey;
    bool opposite = byPlayer.Entity.Controls.CtrlKey;
    if (!sneak && !opposite)
      return be.Solidified
        ? base.OnBlockInteractStart(world, byPlayer, blockSel)
        : false;

    if (world.Side == EnumAppSide.Client)
      return true;

    if (sneak) {
      if (!HasSolidSupportBelow(world, blockSel.Position))
        return false;

      if (be.HasContent) {
        // A mold full of still-liquid metal may only be taken into an empty
        // hand - anywhere else in the inventory it instantly spills.
        bool liquidMold =
          !be.IsBarrel
          && MoltenMoldSpill.IsLiquidContent(
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

        var stack = be.IsBarrel ? be.RemoveBarrel() : be.RemoveMold();
        MoltenMoldSpill.GiveMoldStack(
          world,
          byPlayer,
          stack,
          liquidMold,
          blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5)
        );
      } else {
        var heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (heldSlot?.Itemstack is not { } heldStack)
          return false;

        if (heldStack.Block is BlockMoltenBarrel) {
          be.AddBarrel(heldStack);
        } else if (MoldKinds.IsLarge(heldStack.Block)) {
          be.AddMold(heldStack);
        } else if (heldStack.Block is BlockToolMold) {
          (byPlayer as IServerPlayer)?.SendIngameError("iiex-moldtoosmall");
          return false;
        } else {
          return false;
        }

        heldSlot.TakeOut(1);
        heldSlot.MarkDirty();
      }
      ExSounds.Play(world.Api, blockSel.Position, ExSounds.Ingot, 0.7f);
      be.MarkDirty(true);
    } else {
      be.TryTogglePouring();
    }

    return true;
  }

  /// <summary>
  /// True when the cell directly below the tap is a solid surface a barrel or mold can rest on. Invisible
  /// megablock fillers are excluded: they are solid for collision but are internal parts of another
  /// structure.
  /// </summary>
  private static bool HasSolidSupportBelow(
    IWorldAccessor world,
    BlockPos tapPos
  ) {
    Block below = world.BlockAccessor.GetBlock(tapPos.DownCopy());
    return below.SideSolid[BlockFacing.UP.Index]
      && below is not BlockStructureFiller;
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) {
    var interactions =
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [];

    bool hasContent =
      world.BlockAccessor.GetBlockEntity(selection.Position)
        is BlockEntityMoltenCanalTap be
      && be.HasContent;

    var result = new List<WorldInteraction>(interactions)
    {
      new()
      {
        ActionLangCode = "iiex:blockhelp-canal-togglepour",
        MouseButton = EnumMouseButton.Right,
        HotKeyCode = "sprint",
      },
    };

    if (!HasSolidSupportBelow(world, selection.Position))
      return result.ToArray();

    if (!hasContent) {
      result.Add(
        new WorldInteraction {
          ActionLangCode = "iiex:blockhelp-tap-placecontent",
          MouseButton = EnumMouseButton.Right,
          HotKeyCode = "sneak",
          Itemstacks = _acceptedContents,
        }
      );
    } else {
      result.Add(
        new WorldInteraction {
          ActionLangCode = "iiex:blockhelp-tap-removecontent",
          MouseButton = EnumMouseButton.Right,
          HotKeyCode = "sneak",
        }
      );
    }

    return result.ToArray();
  }

  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1f
  ) {
    // A parked barrel or mold is stored on the block entity, not as a separate block, and the player's
    // item was consumed on placement, so drop it with its contents before the tap is removed.
    if (
      world.Side == EnumAppSide.Server
      && byPlayer is not { WorldData.CurrentGameMode: EnumGameMode.Creative }
      && world.BlockAccessor.GetBlockEntity(pos) is BlockEntityMoltenCanalTap be
      && be.HasContent
    ) {
      ItemStack? parked = be.IsBarrel
        ? be.RemoveBarrel()
        : (be.MoldStack != null ? be.RemoveMold() : null);
      if (parked != null)
        world.SpawnItemEntity(parked, pos.ToVec3d().Add(0.5, 0.5, 0.5));
    }

    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  // AllowedOrientations and GetFallbackOrientation come from the def's variant group (base
  // BlockMoltenCanal); the tap's first-listed orientation "n" is also its fallback, so neither is
  // overridden here.

  protected override void GetRotations(
    string orientation,
    out float rotX,
    out float rotY,
    out float rotZ
  ) {
    rotX = 0;
    rotY = 0;
    rotZ = 0;

    switch (orientation) {
      case "n":
        rotY = 0;
        break;
      case "s":
        rotY = 180;
        break;
      case "w":
        rotY = 90;
        break;
      case "e":
        rotY = 270;
        break;
      default:
        base.GetRotations(orientation, out rotX, out rotY, out rotZ);
        break;
    }
  }
}
