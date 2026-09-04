using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Blocks;

/// <summary>
/// Block entity base that persists whatever it declares. Override <see cref="DeclareState"/>, name each
/// field once, and the save, the load, the client sync and the collectible id mappings all follow from
/// that declaration.
/// <para>
/// A block entity whose base slot is already spent - a container, a multiblock, a network node - gets
/// the same thing by owning an <see cref="ExBlockState"/> directly and calling it from its own two
/// overrides. This class is the convenience, not the mechanism.
/// </para>
/// </summary>
public abstract class ExBlockEntity : BlockEntity {
  private ExBlockState? _state;

  /// <summary>This block entity's declared fields, built on first use.</summary>
  protected ExBlockState State {
    get {
      if (_state != null)
        return _state;
      // Assigned before DeclareState runs: a subclass that declares a field whose accessor reads State
      // would otherwise recurse forever rather than fail with something readable.
      _state = new ExBlockState();
      DeclareState(_state);
      return _state;
    }
  }

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
    State.ToTree(tree);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    State.FromTree(tree, worldForResolving);
  }

  public override void OnStoreCollectibleMappings(
    Dictionary<int, AssetLocation> blockIdMapping,
    Dictionary<int, AssetLocation> itemIdMapping
  ) {
    base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
    State.StoreCollectibleMappings(Api.World, blockIdMapping, itemIdMapping);
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
    State.LoadCollectibleMappings(
      worldForResolve,
      oldBlockIdMapping,
      oldItemIdMapping
    );
  }
}
