using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Blocks;

/// <summary>
/// Block-entity behaviour base that persists whatever it declares - the <see cref="ExBlockEntity"/>
/// convenience for a block entity whose base slot is already spent. Mark a field <see cref="PersistAttribute"/>
/// and it needs no <see cref="DeclareState"/> entry at all; anything else is named once there.
/// <para>
/// Vanilla fans a block entity's <c>ToTreeAttributes</c>/<c>FromTreeAttributes</c> out over its
/// behaviours against the same flat tree the host itself writes into, so a behaviour's keys and its
/// host's - or another behaviour's on the same host - share one key space. <see cref="ExBlockState"/>
/// only catches a duplicate declared twice inside one state; a key this behaviour declares that its
/// host or a sibling behaviour also happens to write is not caught here and collides silently.
/// </para>
/// </summary>
public abstract class ExBlockEntityBehavior(BlockEntity blockentity)
  : BlockEntityBehavior(blockentity) {
  private ExBlockState? _state;

  /// <summary>This behaviour's declared fields, built on first use.</summary>
  protected ExBlockState Persisted =>
    BlockEntityStateHost.GetOrCreate(this, ref _state, DeclareState);

  /// <summary>
  /// Declares the fields this behaviour persists. Called once, lazily. Default: nothing - a behaviour
  /// whose whole state is <c>[Persist]</c> fields needs no override.
  /// </summary>
  protected virtual void DeclareState(ExBlockState state) { }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    Persisted.ToTree(tree);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldAccessForResolve
  ) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    Persisted.FromTree(tree, worldAccessForResolve);
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
    IWorldAccessor worldForNewMappings,
    Dictionary<int, AssetLocation> oldBlockIdMapping,
    Dictionary<int, AssetLocation> oldItemIdMapping,
    int schematicSeed,
    bool resolveImports
  ) {
    base.OnLoadCollectibleMappings(
      worldForNewMappings,
      oldBlockIdMapping,
      oldItemIdMapping,
      schematicSeed,
      resolveImports
    );
    Persisted.LoadCollectibleMappings(
      worldForNewMappings,
      oldBlockIdMapping,
      oldItemIdMapping
    );
  }
}
