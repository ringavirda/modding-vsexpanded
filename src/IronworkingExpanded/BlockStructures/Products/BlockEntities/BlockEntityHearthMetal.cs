using System.Text;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace IronworkingExpanded.BlockStructures.Products.BlockEntities;

/// <summary>
/// Block entity for <c>iwex:hearthmetal-*</c>; tracks how many bits the block will drop. Metal-neutral:
/// the metal comes from the block's code (the <c>metal</c> variant), so one entity backs both the blast
/// furnace's pig iron and the cupola's cast iron. A plain <see cref="BlockEntity"/>, not a molten cell.
/// </summary>
[BlockEntityRegister]
public class BlockEntityHearthMetal : BlockEntity {
  /// <summary>Number of metal bits stored, used to scale the break drop.</summary>
  public int MetalCount { get; set; } = 2;

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(Lang.Get("iwex:solidifiedmetal-info-count", MetalCount));
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetInt("ironCount", MetalCount);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    // Tree key stays "ironCount": the key is the save contract and must survive property and block
    // renames.
    MetalCount = tree.GetInt("ironCount", 2);
  }
}
