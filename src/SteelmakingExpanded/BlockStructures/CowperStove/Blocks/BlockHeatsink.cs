using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Metals;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkMolten;
using SteelmakingExpanded.BlockStructures.CowperStove.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SteelmakingExpanded.BlockStructures.CowperStove.Blocks;

/// <summary>
/// Heat sink block on the cowper stove: glows with the stored regenerator heat.
/// Always drops/picks as the canonical north variant.
/// </summary>
[BlockRegister]
public partial class BlockHeatSink : Block, IExBlockDefProvider
{
  /// <summary>The cowper-stove heat-sink blocktype, authored in C# (migrated from cowperstove/heatsink.json).</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "cowperstoveheatsink", "cowperstove/heatsink")
        .Class<BlockHeatSink>()
        .EntityClass("smex.BlockEntityHeatSink")
        .Shape("smex:cowperstove/heatsink")
        .Material(EnumBlockMaterial.Metal)
        .MetalSounds()
        .MaxStackSize(4)
        .LightAbsorption(99)
        .CreativeCommon("*-n")
        .Behavior("ExOrientable")
        .SideVariant()
        .ShapeByTypePerOrientation("smex:cowperstove/heatsink")
        .NonSolid(),
    ];

  public override byte[] GetLightHsv(
    IBlockAccessor blockAccessor,
    BlockPos pos,
    ItemStack? stack = null
  )
  {
    if (
      pos != null
      && blockAccessor.GetBlockEntity(pos) is BlockEntityHeatSink hs
    )
    {
      byte val = MoltenMetal.GlowLevel(hs.Temperature);
      if (val > 0)
        return [8, 7, val];
    }
    return base.GetLightHsv(blockAccessor, pos, stack);
  }

  public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
  {
    return new ItemStack(
      world.GetBlock(CodeWithVariant("side", "north")) ?? this
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
        worldMap.GetBlock(CodeWithVariant("side", "north")) ?? this
      ),
    ];
  }
}
