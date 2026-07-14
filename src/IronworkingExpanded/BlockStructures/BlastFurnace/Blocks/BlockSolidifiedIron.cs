using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.BlastFurnace.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.BlastFurnace.Blocks;

/// <summary>
/// Solidified iron block left when a lit furnace is extinguished; drops iron
/// bits scaled to its stored count.
/// </summary>
[BlockRegister]
public partial class BlockSolidifiedIron : Block, IExBlockDefProvider
{
  /// <summary>
  /// Code-first blocktype definition (migrated verbatim from the former
  /// <c>assets/iwex/blocktypes/blastfurnace/solidifiediron.json</c>, 2026-07-14). Discovered and
  /// injected by the shared definition system; kept next to the class it configures.
  /// </summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "solidifiediron")
        .Class<BlockSolidifiedIron>()
        .EntityClass<BlockEntitySolidifiedIron>()
        .Material(EnumBlockMaterial.Metal)
        .CreativeTab("general", "*")
        .CreativeTab("iwex", "*")
        .Shape("game:block/basic/cube")
        .TextureAll("game:block/metal/sheet-plain/iron5")
        .Resistance(45f)
        .MaxStackSize(8)
        .MiningTier(5)
        .MineTool(EnumTool.Pickaxe)
        .MetalSounds(),
    ];

  public override ItemStack[] GetDrops(
    IWorldAccessor worldMap,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    if (
      worldMap.BlockAccessor.GetBlockEntity(pos) is BlockEntitySolidifiedIron be
    )
    {
      Item? bit = worldMap.GetItem(new AssetLocation("game", "metalbit-iron"));
      if (bit != null && be.IronCount > 0)
      {
        return [new ItemStack(bit, be.IronCount)];
      }
    }
    return base.GetDrops(worldMap, pos, byPlayer, dropQuantityMultiplier);
  }
}
