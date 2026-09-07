using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace ExpandedLib.Blocks;

/// <summary>
/// Block entity base that persists whatever it declares, on top of vanilla's own inventory - the
/// <see cref="ExBlockEntity"/> convenience for a block entity whose base slot is already spent on
/// <see cref="BlockEntityContainer"/>. Mark a field <see cref="PersistAttribute"/> and it needs no
/// <see cref="DeclareState"/> entry at all; the container's own <c>ToTreeAttributes</c>/
/// <c>FromTreeAttributes</c> pair still runs first, through <c>base</c>, so the inventory keeps
/// serializing exactly as it always has.
/// </summary>
public abstract class ExBlockEntityContainer : BlockEntityContainer {
  private ExBlockState? _state;

  /// <summary>This block entity's declared fields, built on first use.</summary>
  protected ExBlockState Persisted =>
    BlockEntityStateHost.GetOrCreate(this, ref _state, DeclareState);

  /// <summary>
  /// Declares the fields this block entity persists, beyond the inventory <see cref="BlockEntityContainer"/>
  /// already carries. Called once, lazily.
  /// </summary>
  protected abstract void DeclareState(ExBlockState state);

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    Persisted.ToTree(tree);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    Persisted.FromTree(tree, worldForResolving);
  }

  public override void OnStoreCollectibleMappings(
    Dictionary<int, AssetLocation> blockIdMapping,
    Dictionary<int, AssetLocation> itemIdMapping
  ) {
    base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
    Persisted.StoreCollectibleMappings(
      Api.World,
      blockIdMapping,
      itemIdMapping
    );
  }

  public override void OnLoadCollectibleMappings(
    IWorldAccessor worldForResolve,
    Dictionary<int, AssetLocation> oldBlockIdMapping,
    Dictionary<int, AssetLocation> oldItemIdMapping,
    int schematicSeed,
    bool resolveImports
  ) {
    base.OnLoadCollectibleMappings(
      worldForResolve,
      oldBlockIdMapping,
      oldItemIdMapping,
      schematicSeed,
      resolveImports
    );
    Persisted.LoadCollectibleMappings(
      worldForResolve,
      oldBlockIdMapping,
      oldItemIdMapping
    );
  }
}
