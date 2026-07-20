using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace SteelmakingExpanded.BlockStructures.HotBlastFurnace.Blocks;

/// <summary>
/// The reinforced hopper that feeds the blast furnace. Right-click opens its
/// iron/coke/flux inventory; Ctrl + right-click toggles blast-mix dropping on the
/// bell hopper below.
/// </summary>
[BlockRegister]
public partial class BlockHopperReinforced : Block, IExBlockDefProvider
{
  /// <summary>The reinforced hopper blocktype, authored in C# (migrated from blastfurnace/hopperreinforced.json).
  /// Its vanilla Container attributes (inventory class, faces, slots, open/tumble sounds) are authored as one
  /// merged attributes object.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "hopperreinforced", "blastfurnace/hopperreinforced")
        .Class<BlockHopperReinforced>()
        .EntityClass("smex.BlockEntityHopperReinforced")
        .Behavior("Lockable")
        .Behavior("Container")
        .Attributes(
          new
          {
            inventoryClassName = "hopperreinforced",
            pullFaces = new string[] { },
            acceptFromFaces = new[] { "up" },
            pushFaces = new[] { "down" },
            quantitySlots = 8,
            openSound = new
            {
              path = "block/hopperopen",
              pitch = new { avg = 1.0, @var = 0.25 },
            },
            tumbleSound = new
            {
              path = "block/hoppertumble",
              pitch = new { avg = 1.0, @var = 0.25 },
              range = 8,
              volume = new { avg = 0.5, @var = 0.0 },
            },
          }
        )
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
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is BlockEntityHopperReinforced be
    )
    {
      be.OnInteract(byPlayer);
      return true;
    }

    return base.OnBlockInteractStart(world, byPlayer, blockSel);
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  )
  {
    var baseHelp =
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [];

    // Plain RMB always opens the hopper inventory.
    var openHelp = new WorldInteraction
    {
      ActionLangCode = "smex:blockhelp-hopper-open",
      MouseButton = EnumMouseButton.Right,
    };

    if (
      world.BlockAccessor.GetBlockEntity(selection.Position.DownCopy())
      is BlockEntityHopperBell
    )
    {
      var toggleHelp = new WorldInteraction
      {
        ActionLangCode = "smex:blockhelp-hopper-toggle",
        HotKeyCodes = ["ctrl"],
        MouseButton = EnumMouseButton.Right,
      };
      return baseHelp.Append(openHelp).Append(toggleHelp).ToArray();
    }

    return baseHelp.Append(openHelp).ToArray();
  }
}
