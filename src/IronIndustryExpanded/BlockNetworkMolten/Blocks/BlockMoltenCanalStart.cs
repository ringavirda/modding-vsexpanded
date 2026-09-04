using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockNetworkMolten.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace IronIndustryExpanded.BlockNetworkMolten.Blocks;

/// <summary>
/// The canal network's anchor block. Its entity (<see cref="BlockEntityMoltenCanalStart"/>) is the sink
/// liquid metal is poured into, either from a furnace or converter tap above or from a smelted crucible
/// held by the player.
/// </summary>
[BlockRegister]
public partial class BlockMoltenCanalStart : BlockMoltenCanal {
  // Smelted crucibles cached once on load, used only for the pour interaction help.
  private ItemStack[] _smeltedCrucibles = [];

  /// <summary>The two start blocktypes (fire-brick and cobblestone skins), built off the shared
  /// canal-family surface. <c>new</c> hides the base's canal-shape defs so discovery and the derived
  /// AllowedOrientations see only the start block's own defs.</summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain) {
    foreach (CanalSkin skin in CanalSkins)
      yield return CanalFamilyDef(
          domain,
          $"molten/{skin.Folder}/start",
          skin,
          "start",
          1,
          "*-start-*-s",
          ["n", "w", "s", "e"],
          new[]
          {
            new
            {
              x1 = 5,
              z1 = 5,
              x2 = 11,
              z2 = 11,
            },
            new
            {
              x1 = 7,
              z1 = 11,
              x2 = 9,
              z2 = 16,
            },
          },
          "iiex:molten/canal/start",
          [
            ("*-start-*-s", null),
            ("*-start-*-w", 270),
            ("*-start-*-n", 180),
            ("*-start-*-e", 90),
          ]
        )
        .Class<BlockMoltenCanalStart>()
        .EntityClass("iiex.BlockEntityMoltenCanalStart");
  }

  // AllowedOrientations is derived from the def's variant group (base BlockMoltenCanal). Only the fallback
  // is overridden: the start defaults to facing south (toward the pour), not to its first-listed north.
  protected override string GetFallbackOrientation(string? type) => "s";

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);

    _smeltedCrucibles = MoltenMetal.SmeltedCrucibleStacks(api.World);
  }

  // A solidified start clogs like any canal, so it goes to the base chisel-clear interaction. Otherwise
  // return false so the held item's own interaction runs: the block entity implements ILiquidMetalSink,
  // and vanilla BlockSmeltedContainer pours a smelted crucible into the canal network here.
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is BlockEntityMoltenCanal { Solidified: true }
    )
      return base.OnBlockInteractStart(world, byPlayer, blockSel);

    return false;
  }

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
        rotY = 180;
        break;
      case "s":
        rotY = 0;
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

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) {
    var baseHelp =
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [];

    // Only advertise pouring while the network here can still take metal.
    if (
      world.BlockAccessor.GetBlockEntity(selection.Position)
        is not BlockEntityMoltenCanalStart be
      || !be.CanReceiveAny
    )
      return baseHelp;

    return
    [
      .. baseHelp,
      new WorldInteraction
      {
        ActionLangCode = "iiex:blockhelp-canalstart-pour",
        MouseButton = EnumMouseButton.Right,
        Itemstacks = _smeltedCrucibles,
      },
    ];
  }
}
