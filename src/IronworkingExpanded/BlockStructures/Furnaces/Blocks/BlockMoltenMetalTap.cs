using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkMolten.Blocks;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// Molten metal tap on the blast furnace. Right-clicking with an empty hand toggles
/// pouring; the lower tap drains iron and the upper drains slag into a canal start
/// beneath the spout.
/// </summary>
[BlockRegister]
public partial class BlockMoltenMetalTap : Block, IExBlockDefProvider
{
  /// <summary>The blast-furnace molten tap blocktype, authored in C# (migrated from blastfurnace/tap.json).
  /// An Animatable, horizontally orientable ceramic spout.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "moltenmetaltap", "furnaces/moltenmetaltap")
        .Class<BlockMoltenMetalTap>()
        .EntityClass("iwex.BlockEntityMoltenMetalTap")
        .EntityBehavior("Animatable")
        .Material(EnumBlockMaterial.Ceramic)
        .MaxStackSize(1)
        .Behavior("HorizontalOrientable")
        .VariantGroupFromProperties("side", "abstract/horizontalorientation")
        .ShapeByTypePerOrientation("iwex:furnaces/moltenmetaltap", 0)
        .CreativeCommon("*-south")
        .Replaceable(400)
        .Resistance(3.5f)
        .LightAbsorption(3)
        .Sound("walk", "walk/stone")
        .Sound("place", "block/ceramicplace")
        .SoundByTool(
          EnumTool.Pickaxe,
          "block/rock-hit-pickaxe",
          "block/rock-break-pickaxe"
        )
        .NonSolid(),
    ];

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is BlockEntityMoltenMetalTap tap
    )
    {
      // Prevent toggling if the player is holding an item/block
      if (!byPlayer.Entity.RightHandItemSlot.Empty)
        return false;

      // Opening requires a canal start directly below the tap's spout.
      bool isOpening = !tap.IsPouring;
      if (isOpening)
      {
        BlockFacing facing = BlockFacing.FromCode(Variant["side"]);
        BlockPos startPos = blockSel
          .Position.AddCopy(facing.Opposite)
          .DownCopy();
        if (world.BlockAccessor.GetBlock(startPos) is not BlockMoltenCanalStart)
        {
          (world.Api as ICoreClientAPI)?.TriggerIngameError(
            this,
            "nocanal",
            Lang.Get("iwex:tap-err-nocanal")
          );
          return true;
        }
      }

      // The tap no longer swaps to a separate opened/closed block - its pouring
      // state lives on the block entity and is shown by holding the "open"
      // animation pose (see BlockEntityBlastFurnaceTap.ApplyPourPose).
      if (world.Side == EnumAppSide.Server)
        tap.TogglePouring();

      world.PlaySoundAt(
        ExSounds.CokeOvenDoorOpen,
        blockSel.Position.X,
        blockSel.Position.Y,
        blockSel.Position.Z,
        byPlayer
      );

      return true;
    }
    return true;
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  )
  {
    var baseHelp =
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [];

    var toggleHelp = new WorldInteraction
    {
      ActionLangCode = "iwex:blockhelp-tap-toggle",
      MouseButton = EnumMouseButton.Right,
      // Toggling needs an empty hand (a held item is placed instead). Gate the
      // hint on that rather than RequireFreeHand, which would draw an empty slot.
      ShouldApply = (wi, bs, es) => forPlayer.Entity.RightHandItemSlot.Empty,
    };

    return baseHelp.Append(toggleHelp).ToArray();
  }

  public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
  {
    return new ItemStack(
      world.GetBlock(new AssetLocation("iwex", "moltenmetaltap-north")) ?? this
    );
  }

  public override ItemStack[] GetDrops(
    IWorldAccessor worldMap,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    return
    [
      new ItemStack(
        worldMap.GetBlock(new AssetLocation("iwex", "moltenmetaltap-north"))
          ?? this
      ),
    ];
  }
}
