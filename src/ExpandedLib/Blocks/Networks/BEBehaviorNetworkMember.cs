using ExpandedLib.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Networks;

/// <summary>
/// One network membership held by a block entity: which network it joins and which faces it couples
/// on. A block entity carries one per network it is on, which is what lets a converter sit on the
/// pipe, molten and mechanical networks at once. The membership registers its own cell as a graph
/// node and drops it again when the block is removed.
/// See docs/design/mechanics/framework-composition.md.
/// </summary>
public class BEBehaviorNetworkMember(BlockEntity blockentity)
  : BlockEntityBehavior(blockentity),
    INetworkMember {
  /// <summary>
  /// The graph manager this membership registers with, resolved on <see cref="Initialize"/>. Settable
  /// so a host that comes by it another way can point the membership at the same manager.
  /// </summary>
  public BlockNetworkModSystem? NetworkSystem { get; set; }

  /// <inheritdoc/>
  public string NetworkType { get; protected set; } = "";

  /// <inheritdoc/>
  /// <remarks>Declared here as a virtual rather than left to the interface default, so a membership
  /// whose type varies by cell can override it and be reached through the interface. A fresh member
  /// on a subclass would not be, since this class is the one that lists the interface.</remarks>
  public virtual string NetworkTypeAt(IBlockAccessor world, BlockPos pos) =>
    NetworkType;

  /// <inheritdoc/>
  /// <remarks>Defaults to the block's own connector set, so a membership added to an existing
  /// network block inherits its orientation without restating it.</remarks>
  public virtual bool HasConnectorAt(
    IBlockAccessor world,
    BlockPos pos,
    BlockFacing face
  ) =>
    (Blockentity.Block as INetworkConnector)?.HasConnectorAt(world, pos, face)
    ?? false;

  /// <inheritdoc/>
  public virtual bool IsConnectionBroken(IBlockAccessor world, BlockPos pos) =>
    (Blockentity as INetworkNode)?.IsConnectionBroken() ?? false;

  /// <inheritdoc/>
  public virtual bool IsNetworkEndPoint =>
    (Blockentity.Block as INetworkConnector)?.IsNetworkEndPoint ?? false;

  /// <inheritdoc/>
  public virtual bool AcceptsNeighbour(Block neighbour) =>
    (Blockentity.Block as INetworkConnector)?.AcceptsNeighbour(neighbour)
    ?? true;

  #region Graph registration

  /// <summary>
  /// Network state this membership holds ready to push back into its run on load; <c>null</c> for a
  /// membership that persists nothing. Read once, before the cell registers.
  /// </summary>
  protected virtual object? SavedNetworkState => null;

  /// <summary>
  /// Called once the graph manager is resolved and before the cell registers, on both sides. Override
  /// to bring the membership into step with state its host settles no earlier than this, such as a
  /// network type that arrives in the save tree.
  /// </summary>
  protected virtual void OnBeforeRegister() { }

  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);

    // A behaviour added in code carries no properties at all - only CreateBehaviors assigns them - so
    // every read here is optional. A JSON declaration names its network type through this key.
    string? declared = properties?["networkType"].AsString();
    if (!string.IsNullOrEmpty(declared))
      NetworkType = declared;

    NetworkSystem = api.ModLoader.GetModSystem<BlockNetworkModSystem>();
    OnBeforeRegister();

    if (api.Side != EnumAppSide.Server)
      return;

    if (string.IsNullOrEmpty(NetworkType)) {
      api.Logger.Warning(
        "Network membership on {0} at {1} names no network type and so joins no graph. Declare "
          + "\"networkType\" on the behaviour, or set it from the block entity.",
        Blockentity.Block?.Code,
        Pos
      );
      return;
    }

    // Read before registering: AddNode broadcasts, the broadcast reaches OnNetworkUpdate, and that
    // clears the very state this restores.
    object? pendingRestore = SavedNetworkState;

    if (NetworkSystem.GetNetworkAt(Pos) == null)
      NetworkSystem.AddNode(api.World.BlockAccessor, Pos, NetworkType);

    if (
      pendingRestore != null
      && NetworkSystem.GetNetworkAt(Pos) is BlockNetwork network
    ) {
      network.RestoreState(pendingRestore);
      network.BroadcastUpdate(api.World.BlockAccessor);
    }
  }

  /// <summary>
  /// Drops this position out of the graph, which splits or shrinks the network it belonged to.
  /// removal-only teardown: a chunk unload leaves the block placed, so deregistering there would
  /// fracture a live network every time a player walked away from it. The vanilla unload path already
  /// drops the block entity's tick listeners, and <see cref="Initialize"/> re-adopts the position when
  /// the chunk comes back.
  /// </summary>
  public override void OnBlockRemoved() {
    base.OnBlockRemoved();
    if (Api?.Side == EnumAppSide.Server)
      NetworkSystem?.RemoveNode(Api.World.BlockAccessor, Pos);
  }

  #endregion
}
