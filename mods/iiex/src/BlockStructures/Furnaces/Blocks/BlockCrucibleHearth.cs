using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;
using ExpandedLib.Structures;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The crucible furnace's hearth: four melting holes in one cell, each a clay stand carrying one pot, with
/// the coke packed around them. Its firebars are part of the block, as the firebox's are, so no separate
/// grating cell sits under it - the ash pit below is left open.
/// </summary>
/// <remarks>
/// A <see cref="BlockFirebox"/>, deliberately: the coke round the pots is the same fuel bed every other
/// firebox machine burns, charged and drawn down by the same gestures, so the hearth adds pots to a fuel
/// bed rather than reimplementing one. What is not built is the pot seating itself - the holes are drawn
/// and mapped by <see cref="CrucibleHearthLayout"/> but nothing puts a pot in one yet.
/// See docs/design/machines/crucible-furnace.md.
/// </remarks>
[BlockRegister]
public partial class BlockCrucibleHearth : BlockFirebox, IExBlockDefProvider {
  #region Code-first definition

  public static new IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(
          domain,
          BlockFurnaceCoreBase.FurnaceCode,
          "furnace/cruciblehearth"
        )
        .Class<BlockCrucibleHearth>()
        .EntityClass<BlockEntityCrucibleHearth>()
        .EntityBehavior<BEBehaviorFirebox>()
        .Material(EnumBlockMaterial.Ceramic)
        .MaxStackSize(4)
        // Build outline: a hearth is a functional cell of a furnace layout, so a player standing at it can
        // preview and complete an unfinished furnace, as at a firebox or a door.
        .Behavior("MultiblockStructure")
        .Behavior("ExOrientable")
        // `type` names the family member; every furnace part shares the code `iiex:furnace`
        // (see BlockFurnaceCoreBase.FurnaceCode and N7).
        .VariantGroup("type", "cruciblehearth")
        .VariantGroup("tier", "tier1", "tier2", "tier3")
        .SideVariant()
        .ShapeByTypePerOrientation("iiex:furnace/cruciblehearth", 0)
        // The drawn shape names tier3 outright. Every furnace part in the mod wears `{tier}` instead, so
        // the block is built from whatever refractory the player laid, and any tier is allowed here.
        .Texture("front1", "game:block/clay/refractory/{tier}/front1")
        .Texture("burned", "game:block/clay/vessel/sides/burned")
        .Texture("iron5", "game:block/metal/sheet-plain/iron5")
        .Texture("blistersteel", "game:block/metal/ingot/blistersteel")
        // All four fuels are declared, as the firebox declares them, so a bed of charcoal does not look
        // like a bed of coke once the block entity repoints the bed's faces.
        .Texture(FuelTexture, "game:block/coal/coke")
        .Texture("bituminous", "game:block/coal/bituminous")
        .Texture("anthracite", "game:block/coal/anthracite")
        .Texture("charcoal", "game:block/coal/charcoal")
        .CreativeCommon("*-tier1-n")
        .Replaceable(400)
        .Resistance(4f)
        // Loose fuel over an open grate and pots standing in it: the furnace's own light has to reach
        // through the bed rather than being absorbed by it.
        .LightAbsorption(0)
        .SolidNonOpaque()
        .Sound("walk", "walk/stone")
        .Sound("place", "block/ceramicplace")
        .SoundByTool(
          EnumTool.Pickaxe,
          "block/rock-hit-pickaxe",
          "block/rock-break-pickaxe"
        ),
    ];

  #endregion

  #region Interaction

  /// <summary>
  /// The pot gestures, over the fuel bed's own. A burned pot in hand is seated, crushed blister steel is
  /// charged into the pots, and an empty hand pulls whichever pot has most to give. Everything else falls
  /// through to <see cref="BlockFirebox"/>, so the bed is still charged and drawn down exactly as any
  /// other firebox is.
  /// </summary>
  /// <remarks>
  /// An empty hand only reaches the fuel bed once every hole is empty. That is the cost of putting two
  /// jobs on one block: the pots are what a player is at the hearth for, and taking a course of coke back
  /// out is the rarer act.
  /// </remarks>
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is not BlockEntityCrucibleHearth hearth
    )
      return base.OnBlockInteractStart(world, byPlayer, blockSel);

    // The build-outline gesture first, so ctrl+shift+right-click previews an unfinished furnace instead of
    // seating a pot in it.
    if (
      BlockBehaviorMultiblockStructure.TryToggleProjection(
        world,
        byPlayer,
        blockSel.Position
      )
    )
      return true;
    if (world.Side != EnumAppSide.Server)
      return true;

    ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;
    var player = byPlayer as IServerPlayer;

    if (active is { Empty: false }) {
      if (
        active.Itemstack.Collectible.Code?.Path == BlockSteelCrucible.BurnedCode
      ) {
        if (!hearth.Seat(active.Itemstack))
          player?.SendIngameError("iiex-crucible-holesfull");
        else {
          active.TakeOut(1);
          active.MarkDirty();
        }
        return true;
      }

      if (BlockEntityCrucibleHearth.IsBlister(active.Itemstack)) {
        int taken = hearth.Charge(active.Itemstack);
        if (taken == 0)
          player?.SendIngameError("iiex-crucible-nopot");
        else {
          active.TakeOut(taken);
          active.MarkDirty();
        }
        return true;
      }
    } else if (hearth.Pull() is { } pulled) {
      if (byPlayer.InventoryManager?.TryGiveItemstack(pulled) != true)
        world.SpawnItemEntity(
          pulled,
          blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5)
        );
      return true;
    }

    return base.OnBlockInteractStart(world, byPlayer, blockSel);
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) =>
    [
      new()
      {
        ActionLangCode = IiexLang.CrucibleHelpSeat,
        MouseButton = EnumMouseButton.Right,
      },
      new()
      {
        ActionLangCode = IiexLang.CrucibleHelpPull,
        MouseButton = EnumMouseButton.Right,
      },
      .. base.GetPlacedBlockInteractionHelp(world, selection, forPlayer),
    ];

  #endregion

  #region Break safety

  /// <summary>
  /// The pots and their charge, on top of the fuel the bed drops. A hearth broken mid-heat gives back four
  /// pots and whatever was in them rather than swallowing them.
  /// </summary>
  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1
  ) {
    if (
      world.Side == EnumAppSide.Server
      && world.BlockAccessor.GetBlockEntity(pos)
        is BlockEntityCrucibleHearth hearth
    )
      foreach (ItemStack stack in hearth.HoleDrops().ToList())
        world.SpawnItemEntity(stack, pos.ToVec3d().Add(0.5, 0.5, 0.5));

    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  #endregion
}
