using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Crafting.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace IronworkingExpanded.BlockStructures.Crafting.Blocks;

/// <summary>
/// The design table - the draughting station where diagrams are drawn and builds are planned (Phase 3 of
/// the diagram-crafting system, see <c>docs/design/diagram-crafting.md</c>). A candle-lit, two-wide
/// draughting desk (paper, charcoal, ruler, candles).
/// <para>
/// Right-click opens the drafting window (<see cref="BlockEntityDesignTable"/> / its dialog): pick a diagram
/// and Draw it onto parchment. Still to come: the setup-guide viewer, candle particles, and the 2-wide
/// megablock collision (a filler for the second cell); for now the block is a single cell whose model
/// overhangs into the next.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockDesignTable : Block, IExBlockDefProvider
{
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "designtable", "design-table")
        .Class<BlockDesignTable>()
        .EntityClass<BlockEntityDesignTable>()
        .Behavior("HorizontalOrientable")
        .VariantGroupFromProperties("side", "abstract/horizontalorientation")
        .CreativeCommon("*-north")
        .Shape("iwex:design-table")
        .Material(EnumBlockMaterial.Wood)
        // Always-lit candles: a warm, dim glow (tune the value in-game).
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
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is BlockEntityDesignTable be
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

    return baseHelp
      .Append(
        new WorldInteraction
        {
          ActionLangCode = "iwex:blockhelp-designtable-open",
          MouseButton = EnumMouseButton.Right,
        }
      )
      .ToArray();
  }
}
