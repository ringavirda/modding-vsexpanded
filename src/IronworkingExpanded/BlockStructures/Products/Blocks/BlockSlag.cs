using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Products.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Products.Blocks;

/// <summary>Solidified slag block left when blast mix finishes burning; drops slag items scaled to its stored count.</summary>
[BlockRegister]
public partial class BlockSlag : Block, IExBlockDefProvider
{
  /// <summary>The solidified-slag blocktype, authored in C# (migrated from blastfurnace/slag.json). Smeltable
  /// back into slag items via its combustibleProps.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "slag-block", "slag/block")
        .Class<BlockSlag>()
        .EntityClass("iwex.BlockEntitySlag")
        .Material(EnumBlockMaterial.Stone)
        .CreativeCommon("*")
        .Shape("game:block/basic/cube")
        .TextureAll(Items.SlagItemDefinitions.Texture)
        .Resistance(3.0f)
        .MaxStackSize(64)
        .MiningTier(2)
        .MineTool(EnumTool.Pickaxe)
        .Sounds(
          "game:block/stone",
          "game:block/stone",
          "game:block/stone",
          "game:walk/stone"
        )
        .CombustibleProps(
          new
          {
            meltingPoint = 720,
            meltingDuration = 30,
            smeltedRatio = 1,
            smeltedStack = new { type = "item", code = "iwex:slag" },
          }
        ),
    ];

  public override ItemStack[] GetDrops(
    IWorldAccessor worldMap,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    if (worldMap.BlockAccessor.GetBlockEntity(pos) is BlockEntitySlag be)
    {
      Item? slagItem = worldMap.GetItem(new AssetLocation("iwex", "slag"));
      if (slagItem != null && be.SlagCount > 0)
      {
        // Randomize the drop slightly (e.g. 80-100% of the original mix)
        int dropCount = (int)(
          be.SlagCount * (0.8f + (worldMap.Rand.NextDouble() * 0.2f))
        );
        if (dropCount <= 0)
          dropCount = 1;
        return [new ItemStack(slagItem, dropCount)];
      }
    }
    return base.GetDrops(worldMap, pos, byPlayer, dropQuantityMultiplier);
  }
}
