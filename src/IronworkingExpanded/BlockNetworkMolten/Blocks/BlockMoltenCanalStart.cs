using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace IronworkingExpanded.BlockNetworkMolten.Blocks;

/// <summary>
/// The canal network's anchor block. Acts as an <see cref="BlockEntityMoltenCanalStart"/>
/// sink that liquid metal is poured into - from a furnace/converter tap above, or
/// directly from a smelted crucible held by the player.
/// </summary>
[BlockRegister]
public partial class BlockMoltenCanalStart : BlockMoltenCanal
{
  // Smelted crucibles cached once on load, used only for the pour interaction help.
  private ItemStack[] _smeltedCrucibles = [];

  /// <summary>The two start blocktypes (fire-brick + cobblestone skin), authored in C# (migrated from
  /// molten/canalbrick/start.json + molten/canalcobblestone/start.json) off the shared canal-family
  /// surface. <c>new</c> hides the base's canal-shape defs so discovery + the derived AllowedOrientations
  /// see only the start block's own defs.</summary>
  public static new IEnumerable<ExBlockDef> Definitions(string domain)
  {
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
            new { x1 = 5, z1 = 5, x2 = 11, z2 = 11 },
            new { x1 = 7, z1 = 11, x2 = 9, z2 = 16 },
          },
          "iwex:molten/canal/start",
          [
            ("*-start-*-s", null),
            ("*-start-*-w", 270),
            ("*-start-*-n", 180),
            ("*-start-*-e", 90),
          ]
        )
        .Class<BlockMoltenCanalStart>()
        .EntityClass("iwex.BlockEntityMoltenCanalStart");
  }

  // AllowedOrientations is derived from the def's variant group (base BlockMoltenCanal). Only the fallback
  // is overridden: the start defaults to facing south (toward the pour), not to its first-listed north.
  protected override string GetFallbackOrientation(string? type) => "s";

  public override void OnLoaded(ICoreAPI api)
  {
    base.OnLoaded(api);

    _smeltedCrucibles = MoltenMetal.SmeltedCrucibleStacks(api.World);
  }

  // A solidified start clogs like any canal - hand it to the base chisel-clear interaction so the
  // player can chip it out for the next pour. Otherwise return false so the held item's interaction
  // runs instead of being swallowed: the block entity implements ILiquidMetalSink, so vanilla
  // BlockSmeltedContainer (a smelted crucible) pours its molten metal into the canal network here.
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  )
  {
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
  )
  {
    rotX = 0;
    rotY = 0;
    rotZ = 0;

    switch (orientation)
    {
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
  )
  {
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
        ActionLangCode = "iwex:blockhelp-canalstart-pour",
        MouseButton = EnumMouseButton.Right,
        Itemstacks = _smeltedCrucibles,
      },
    ];
  }
}
