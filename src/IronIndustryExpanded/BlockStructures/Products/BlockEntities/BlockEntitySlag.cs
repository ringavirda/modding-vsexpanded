using System.Text;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace IronIndustryExpanded.BlockStructures.Products.BlockEntities;

/// <summary>
/// Block entity for the solidified-slag block; tracks how many slag units it will drop.
/// </summary>
/// <remarks>
/// No code path currently writes <see cref="SlagCount"/>. The type is retained as a migration target:
/// existing worlds contain <c>iiex:slag-block</c> with a saved <c>slagCount</c>, which this reads, drops
/// and persists correctly. It is not dead code.
/// </remarks>
[BlockEntityRegister]
public class BlockEntitySlag : BlockEntity {
  /// <summary>Number of slag units stored, used to scale the break drop. Read from the save tree only;
  /// see the type's remarks.</summary>
  public int SlagCount { get; set; } = 0;

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(Lang.Get("iiex:slag-info-count", SlagCount));
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetInt("slagCount", SlagCount);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    SlagCount = tree.GetInt("slagCount", 0);
  }
}
