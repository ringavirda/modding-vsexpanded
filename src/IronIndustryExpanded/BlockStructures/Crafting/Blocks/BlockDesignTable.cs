using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Crafting.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace IronIndustryExpanded.BlockStructures.Crafting.Blocks;

/// <summary>
/// The design table: a candle-lit, two-wide draughting desk where diagrams are drawn and builds are
/// planned. Right-click opens the drafting dialog (<see cref="BlockEntityDesignTable"/>) to pick a
/// diagram and draw it onto parchment. Currently a single cell whose model overhangs into the next;
/// the second cell has no filler yet. See <c>docs/design/diagram-crafting.md</c>.
/// </summary>
[BlockRegister]
public partial class BlockDesignTable : Block, IExBlockDefProvider {
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "crafting-designtable", "crafting/designtable")
        .Class<BlockDesignTable>()
        .EntityClass<BlockEntityDesignTable>()
        .Behavior("ExOrientable")
        .SideVariant()
        .CreativeCommon("*-n")
        .Shape("iiex:crafting/designtable")
        .Material(EnumBlockMaterial.Wood)
        // Always-lit candles: a warm, dim glow.
        .Raw("lightHsv", new[] { 5, 7, 12 })
        .MaxStackSize(1)
        .Resistance(2f)
        .Sound("walk", "game:walk/wood")
        .Sound("place", "game:block/planks")
        .MaterialDensity(600),
    ];

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is BlockEntityDesignTable be
    ) {
      be.OnInteract(byPlayer);
      return true;
    }

    return base.OnBlockInteractStart(world, byPlayer, blockSel);
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) {
    var baseHelp =
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [];

    return baseHelp
      .Append(
        new WorldInteraction {
          ActionLangCode = "iiex:blockhelp-designtable-open",
          MouseButton = EnumMouseButton.Right,
        }
      )
      .ToArray();
  }
}
