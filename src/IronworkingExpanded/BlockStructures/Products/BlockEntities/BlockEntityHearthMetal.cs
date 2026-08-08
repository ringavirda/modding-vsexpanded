using System.Text;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace IronworkingExpanded.BlockStructures.Products.BlockEntities;

/// <summary>
/// Block entity for <c>iwex:hearthmetal-*</c>; tracks how many bits the block will drop. Metal-neutral -
/// which metal it is now comes from the block's <b>code</b> (the <c>metal</c> variant), so one entity backs
/// both the blast furnace's pig iron and the cupola's cast iron.
/// <para>
/// Deliberately still a plain <see cref="BlockEntity"/> and not a molten cell. Making the hearth a live
/// <c>BEBehaviorMoltenCell</c> pool is planned work; adding the behaviour here early would force a
/// second golden blessing for a block that has not changed shape yet.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityHearthMetal : BlockEntity
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
    // Tree key deliberately "ironCount": the key is the save contract and stays stable across property
    // and block renames alike; the field name is not. `HearthMetalMigrationTests`
    // asserts the key rather than the property for exactly that reason.
    MetalCount = tree.GetInt("ironCount", 2);
  }
}
