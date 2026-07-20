using System.Text;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace IronworkingExpanded.BlockStructures.Products.BlockEntities;

/// <summary>
/// Block entity for the solidified-metal blocks; tracks how many bits the block will drop. Metal-neutral -
/// which metal it is comes from the block's <c>metal</c> attribute, so one entity backs both the blast
/// furnaces' iron and the cupola's cast iron.
/// </summary>
[BlockEntityRegister]
public class BlockEntitySolidifiedIron : BlockEntity
{
  /// <summary>Number of metal bits stored, used to scale the break drop.</summary>
  public int MetalCount { get; set; } = 2;

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(Lang.Get("iwex:solidifiedmetal-info-count", MetalCount));
  }

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    tree.SetInt("ironCount", MetalCount);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    // Tree key deliberately still "ironCount" - the property rename must not orphan existing saves.
    MetalCount = tree.GetInt("ironCount", 2);
  }
}
