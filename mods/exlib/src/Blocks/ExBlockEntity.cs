using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Blocks;

/// <summary>
/// Block entity base that persists whatever it declares. Mark a field <see cref="PersistAttribute"/> and
/// it needs no <see cref="DeclareState"/> entry at all; anything else is named once there, and the save,
/// the load, the client sync and the collectible id mappings all follow from that declaration.
/// <para>
/// A block entity whose base slot is already spent - a container, a multiblock, a network node - gets
/// the same thing by owning an <see cref="ExBlockState"/> directly and calling it from its own two
/// overrides. This class is the convenience, not the mechanism.
/// </para>
/// </summary>
public abstract class ExBlockEntity : BlockEntity {
  private ExBlockState? _state;

  /// <summary>This block entity's declared fields, built on first use.</summary>
  protected ExBlockState Persisted =>
    BlockEntityStateHost.GetOrCreate(this, ref _state, DeclareState);

  /// <summary>
  /// Declares the fields this block entity persists. Called once, lazily.
  /// <code>
  /// protected override void DeclareState(ExBlockState s) {
  ///   s.Float("temp", () =&gt; _tempC, v =&gt; _tempC = v)
  ///    .Stack("piece", () =&gt; _piece, v =&gt; _piece = v);
  /// }
  /// </code>
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
    Persisted.StoreCollectibleMappings(Api.World, blockIdMapping, itemIdMapping);
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
