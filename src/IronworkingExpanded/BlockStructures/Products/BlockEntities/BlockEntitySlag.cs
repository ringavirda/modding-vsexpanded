using System.Text;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace IronworkingExpanded.BlockStructures.Products.BlockEntities;

/// <summary>
/// Block entity for the solidified-slag block; tracks how many slag units it
/// will drop.
/// <para>
/// <b>Nothing in the mod writes <see cref="SlagCount"/>, and that is deliberate rather than
/// an oversight - written down here so a later cleanup does not read this block as dead and delete it.</b>
/// Its producer was the unattended burn of a blast-mix coal pile outside a furnace, and the layered
/// charge retired that whole idea: the only thing that burns is coke, and only inside a furnace that
/// owns it, so there is no unmanaged fire left to slag anything.
/// </para>
/// <para>
/// The block is <b>kept</b> on purpose. It is a live <b>migration target</b> - existing worlds have
/// <c>iwex:slag-block</c> standing in them with a saved <c>slagCount</c>, and it reads, drops and
/// persists correctly. Nothing on the extinguish path has ever made slag either: a furnace that goes
/// out is a setback, not the loss of the charge, so burn-out hands the burden back as salvage. A future
/// producer (a slag runner off the cinder notch) is a design question, not a repair.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntitySlag : BlockEntity
{
  /// <summary>Number of slag units stored, used to scale the break drop. Write-free in the shipped
  /// mod - see the class remarks before concluding this block is dead.</summary>
  public int SlagCount { get; set; } = 0;

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(Lang.Get("iwex:slag-info-count", SlagCount));
  }

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    tree.SetInt("slagCount", SlagCount);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    SlagCount = tree.GetInt("slagCount", 0);
  }
}
