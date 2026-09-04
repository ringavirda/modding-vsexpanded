using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Networks;

/// <summary>
/// Base block entity for any block that is a node in a <see cref="BlockNetwork"/> (gas pipes, molten
/// canals). Graph membership is held by a <see cref="BEBehaviorNetworkMember"/> this class hosts, which
/// registers and unregisters the node server-side; the block entity itself persists orientation and
/// network state and forwards network updates to the concrete block entity.
/// </summary>
public abstract class BlockEntityNetworkNode : BlockEntity, INetworkNode {
  private readonly HostMembership _membership;

  protected BlockEntityNetworkNode() {
    // Added here because BlockEntity fans both FromTreeAttributes and Initialize out over Behaviors,
    // and a membership added any later misses whichever of the two has already run.
    _membership = new HostMembership(this);
    Behaviors.Add(_membership);
  }

  /// <summary>The network manager this node is registered with, resolved when the block entity
  /// initialises. Held by the membership, which is what registers with it.</summary>
  public BlockNetworkModSystem? NetworkSystem {
    get => _membership.NetworkSystem;
    protected set => _membership.NetworkSystem = value;
  }

  /// <summary>
  /// This node's graph membership. It holds no copy of the network type or the saved state and reads
  /// both off the block entity as it needs them: <see cref="FromTreeAttributes"/> reaches the
  /// behaviours before it assigns either, and runs again on every client sync.
  /// </summary>
  private sealed class HostMembership(BlockEntityNetworkNode owner)
    : BEBehaviorNetworkMember(owner) {
    public override string NetworkType {
      get => owner.NetworkType;
      protected set => owner.NetworkType = value;
    }

    protected override object? SavedNetworkState => owner._savedNetworkState;
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetString("networkType", NetworkType);
    tree.SetString("orientation", Orientation);
    tree.SetStrings("possibleOrientations", PossibleOrientations);
    SerializeNetworkState(tree, _savedNetworkState);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    NetworkType = tree.GetString("networkType", null);
    Orientation = tree.GetString("orientation");
    // Saves written before the string-array cutover hold this key as JSON text under the same name. The
    // array read returns null on one of those rather than throwing, so the old encoding is simply the
    // second thing tried; a node loaded that way is rewritten in the new shape on its next save.
    PossibleOrientations =
      tree.GetStrings("possibleOrientations")
      ?? ExTree.SafeDeserialize<string[]>(
        tree.GetString("possibleOrientations"),
        []
      );
    _savedNetworkState = DeserializeNetworkState(tree);
  }

  #region Persistence hooks - override in concrete BEs

  /// <summary>Returns <c>true</c> when <paramref name="state"/> is worth caching and restoring.
  /// Default: any non-null state. Override to require non-empty content (e.g. amount > 0).</summary>
  protected virtual bool IsNetworkStateMeaningful(object? state) =>
    state != null;

  /// <summary>Deserializes the network state written by <see cref="SerializeNetworkState"/>; returns
  /// <c>null</c> when none was saved. Called from <see cref="FromTreeAttributes"/>.</summary>
  protected virtual object? DeserializeNetworkState(ITreeAttribute tree) =>
    null;

  /// <summary>Serializes <paramref name="state"/> into <paramref name="tree"/> for save/reload.
  /// Called from <see cref="ToTreeAttributes"/>.</summary>
  protected virtual void SerializeNetworkState(
    ITreeAttribute tree,
    object? state
  ) { }

  #endregion

  #region INetworkNode
  /// <inheritdoc/>
  public string[] PossibleOrientations { get; set; } = [];

  /// <inheritdoc/>
  public string? Orientation { get; set; }

  /// <summary>Network state cached for the restore-on-load path.</summary>
  protected object? _savedNetworkState;
  protected object? _networkState;

  /// <inheritdoc/>
  public virtual bool HasConnectorAt(BlockFacing face) =>
    (Block as BlockNetworkNode)?.HasConnectorAt(face) ?? false;

  /// <inheritdoc/>
  public virtual void OnNetworkUpdate(object? state) {
    _networkState = state;
    if (IsNetworkStateMeaningful(state))
      _savedNetworkState = state;
    else
      _savedNetworkState = null;
  }

  /// <summary>
  /// Whether this node currently severs the network at its position (e.g. a closed
  /// valve). Default <c>false</c>; override to break connectivity dynamically.
  /// </summary>
  public virtual bool IsConnectionBroken() => false;

  /// <inheritdoc/>
  public virtual void OnOpenConnectorsChanged(BlockFacing[] openFaces) { }

  /// <inheritdoc/>
  public virtual void OnLeak(
    BlockFacing[] leakingFaces,
    bool isLiquid,
    float intensity
  ) { }

  /// <inheritdoc/>
  public abstract string NetworkType { get; set; }
  #endregion
}
