using System;
using System.Linq;
using ExpandedLib.Structures;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Networks;

/// <summary>
/// One network membership held by a block entity: which network it joins and which faces it couples
/// on. A block entity carries one per network it is on, which is what lets a converter sit on the
/// pipe, molten and mechanical networks at once. The membership registers its own cell as a graph
/// node and drops it again when the block is removed.
/// See docs/design/mechanics/framework-composition.md.
/// </summary>
/// <remarks>Registered as a class so a mega-block footprint cell can declare one in its
/// <c>fillerOffsets</c> and become a node of its own (<see cref="ConfigureFromFiller"/>).</remarks>
[BlockEntityBehaviorRegister]
public class BEBehaviorNetworkMember(BlockEntity blockentity)
  : BlockEntityBehavior(blockentity),
    INetworkMember,
    IFillerHostedBehavior {
  /// <summary>
  /// The graph manager this membership registers with, resolved on <see cref="Initialize"/>. Settable
  /// so a host that comes by it another way can point the membership at the same manager.
  /// </summary>
  public BlockNetworkModSystem? NetworkSystem { get; set; }

  /// <inheritdoc/>
  /// <remarks>Virtual because a hosted membership does not own its answer - it reads the block entity
  /// it belongs to, whose type a save tree can reassign long after this behaviour initialised. A copy
  /// taken once drifts, and the graph walk keys on this.</remarks>
  public virtual string NetworkType { get; protected set; } = "";

  /// <inheritdoc/>
  /// <remarks>Declared here as a virtual rather than left to the interface default, so a membership
  /// whose type varies by cell can override it and be reached through the interface. A fresh member
  /// on a subclass would not be, since this class is the one that lists the interface.</remarks>
  public virtual string NetworkTypeAt(IBlockAccessor world, BlockPos pos) =>
    NetworkType;

  /// <summary>
  /// The faces this membership couples on, or empty to leave the answer to the block. Settable so a
  /// host that knows which face the cell exposes without a block variant to carry it - a structure
  /// filler reads one off the footprint declaration, already rotated into the placed orientation -
  /// can state it directly.
  /// </summary>
  /// <remarks>Virtual for the same reason the interface members are: a subclass computing its faces
  /// by shadowing a non-virtual property would be read straight past by the connector test below.</remarks>
  public virtual BlockFacing[] Connectors { get; set; } = [];

  /// <summary>
  /// Takes <paramref name="orientation"/> - single-letter side codes, as everywhere else in the repo:
  /// "ns", "we", "nsewud" - as this membership's connector faces. A letter naming no side is dropped,
  /// and a string naming none at all leaves the block to answer.
  /// </summary>
  public void DeclareConnectors(string? orientation) =>
    Connectors = BlockNetworkModSystem.SidesToFaces(orientation);

  /// <inheritdoc/>
  /// <remarks>Answers from <see cref="Connectors"/> when this membership states any, and otherwise
  /// from the block's own set, so a membership added to an existing network block inherits its
  /// orientation without restating it and one on a block that answers for no cell can still couple.</remarks>
  public virtual bool HasConnectorAt(
    IBlockAccessor world,
    BlockPos pos,
    BlockFacing face
  ) =>
    Connectors.Length > 0
      ? Connectors.Contains(face)
      : (Blockentity.Block as INetworkConnector)?.HasConnectorAt(
        world,
        pos,
        face
      ) ?? false;

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

  #region Hosted on a footprint cell

  /// <summary>
  /// Takes the cell's connector face - handed over already rotated into the placed orientation - as
  /// this membership's own, so the graph reads the face the cell exposes rather than one derived from
  /// the single always-north filler block every footprint cell in the world shares. A cell a run
  /// passes straight through declares <c>passThrough</c> and couples on the opposite face as well,
  /// the same axis rule an axle follows.
  /// </summary>
  /// <remarks>A declaration carrying no face leaves the answer to the cell's block, which for a
  /// structure filler is the fixed port recorded on its block entity, so a membership can turn an
  /// existing port cell into a node without restating where it couples. A <c>connectors</c> string is
  /// written in the unrotated frame and does not turn with the structure, so a footprint that rotates
  /// states its face here instead.</remarks>
  public void ConfigureFromFiller(
    BlockPos? principal,
    BlockFacing? connectorFace,
    JsonObject? properties
  ) {
    if (connectorFace == null)
      return;

    Connectors =
      properties?["passThrough"].AsBool(false) == true
        ? [connectorFace, connectorFace.Opposite]
        : [connectorFace];
  }

  #endregion

  #region Graph registration

  /// <summary>
  /// Network state this membership holds ready to push back into its run on load; <c>null</c> for a
  /// membership that persists nothing. Read once, before the cell registers.
  /// </summary>
  protected virtual object? SavedNetworkState => null;

  /// <summary>
  /// Takes <paramref name="declared"/> as this membership's network type, but only when it has none
  /// of its own. A membership whose block entity already names a network keeps that answer and the
  /// disagreement is reported: the block entity's is a compiled contract, and every node family but
  /// the fluid intake implements the setter as a no-op, so a declaration that won would vanish on the
  /// way through and register under the constant anyway.
  /// </summary>
  private void ApplyDeclaredNetworkType(ICoreAPI api, string? declared) {
    if (string.IsNullOrEmpty(declared))
      return;

    if (string.IsNullOrEmpty(NetworkType)) {
      NetworkType = declared;
      return;
    }

    if (
      api.Side == EnumAppSide.Server
      && !string.Equals(declared, NetworkType, StringComparison.Ordinal)
    )
      api.Logger.Error(
        "Network membership on {0} at {1} declares network type \"{2}\" but its block entity already "
          + "names \"{3}\", which wins. Drop the declaration.",
        Blockentity.Block?.Code,
        Pos,
        declared,
        NetworkType
      );
  }

  /// <summary>
  /// Takes <paramref name="declared"/> as this membership's connector faces, but only when it has
  /// none of its own, and reports a disagreement. A set configured in code names faces this cell
  /// actually exposes - a filler's port arrives already rotated into the placed orientation - while a
  /// declaration is written in the unrotated frame, so letting it win would move the port.
  /// </summary>
  private void ApplyDeclaredConnectors(ICoreAPI api, string? declared) {
    if (string.IsNullOrEmpty(declared))
      return;

    BlockFacing[] parsed = BlockNetworkModSystem.SidesToFaces(declared);
    if (Connectors.Length == 0) {
      Connectors = parsed;
      return;
    }

    if (
      api.Side == EnumAppSide.Server
      && !Connectors.ToHashSet().SetEquals(parsed)
    )
      api.Logger.Error(
        "Network membership on {0} at {1} declares connectors \"{2}\" but its host already set "
          + "\"{3}\", which wins. Drop the declaration.",
        Blockentity.Block?.Code,
        Pos,
        declared,
        string.Concat(Connectors.Select(f => f.Code[0]))
      );
  }

  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);

    // A behaviour added in code carries no properties at all - only CreateBehaviors assigns them - so
    // every read here is optional. A JSON declaration names its network and its faces through these
    // two keys, and either may be left out.
    ApplyDeclaredNetworkType(api, properties?["networkType"].AsString());
    ApplyDeclaredConnectors(api, properties?["connectors"].AsString());

    NetworkSystem = api.ModLoader.GetModSystem<BlockNetworkModSystem>();

    if (api.Side != EnumAppSide.Server)
      return;

    // Registering a blank type throws out of the factory lookup, and this runs inside a chunk load, so
    // one bad declaration would take a world down. Logged as an error rather than a warning: the cell
    // reads to a player as a run that stopped working, with nothing else to go on.
    if (string.IsNullOrEmpty(NetworkType)) {
      api.Logger.Error(
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
  /// removal-only teardown: a chunk unload is not a removal, and deregistering there would fracture a
  /// live network every time a player walked away from one. The vanilla unload path already drops the
  /// block entity's tick listeners, and <see cref="Initialize"/> re-adopts the position when the chunk
  /// comes back.
  /// <para>
  /// Keeping the node is only half of it: an unload hides the block as well as the block entity, so
  /// the cell reads to the walk as empty space in the meantime. The graph answers that by suspending
  /// its fracture check rather than acting on it - see <see cref="BlockNetworkModSystem"/>.
  /// </para>
  /// </summary>
  public override void OnBlockRemoved() {
    base.OnBlockRemoved();
    // The block entity's api rather than this behaviour's: a behaviour is handed one only by its own
    // Initialize, while a block entity can be given one without being initialised - the shape the
    // fixtures use - and it is assigned before the behaviour fan-out, so it is set whenever the
    // behaviour's is.
    ICoreAPI? api = Blockentity.Api;
    if (api?.Side == EnumAppSide.Server)
      NetworkSystem?.RemoveNode(api.World.BlockAccessor, Pos);
  }

  #endregion
}
