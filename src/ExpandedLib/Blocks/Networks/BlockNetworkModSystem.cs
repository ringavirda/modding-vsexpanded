using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ExpandedLib.Blocks.Networks;

/// <summary>
/// Graph manager for all block networks: node add/remove, BFS fracture detection, and per-tick
/// dispatch. All type-specific state and logic live in the concrete <see cref="BlockNetwork"/>
/// subclasses.
/// </summary>
public class BlockNetworkModSystem : ModSystem {
  #region Graph storage
  private readonly Dictionary<Guid, BlockNetwork> _networks = [];
  private readonly Dictionary<BlockPos, Guid> _posToNetwork = [];
  private readonly Dictionary<string, Func<BlockNetwork>> _factories = [];

  /// <summary>
  /// Networks whose connectivity could not be decided, each mapped to the node positions the walk
  /// could not read. Non-empty means a fracture check is deferred rather than answered wrongly; it is
  /// taken again once one of those cells is back. Empty in the ordinary case, which is what keeps
  /// <see cref="ResumeSuspendedReviews"/> free on the tick.
  /// </summary>
  private readonly Dictionary<Guid, HashSet<BlockPos>> _unreadableNodes = [];

  /// <summary>
  /// Registers a factory that creates a new typed network instance for the
  /// given <paramref name="networkType"/> (e.g. "gas", "molten").
  /// Call once during <c>ModSystem.Start</c>.
  /// </summary>
  public void RegisterNetworkType(
    string networkType,
    Func<BlockNetwork> factory
  ) => _factories[networkType] = factory;

  /// <summary>
  /// Server world accessor, available to network instances during their tick (e.g.
  /// to break a burst/melted node and drop its items). <c>null</c> on the client.
  /// </summary>
  public IServerWorldAccessor? ServerWorld { get; private set; }

  public override void StartServerSide(ICoreServerAPI api) {
    ServerWorld = api.World;
    api.Event.RegisterGameTickListener(
      dt => ServerTick(api.World.BlockAccessor, dt),
      1000
    );
  }

  /// <summary>Every live network instance. Server-side only: <see cref="AddNode"/> and
  /// <see cref="RemoveNode"/> never run on the client, so the client graph is empty.</summary>
  public IEnumerable<BlockNetwork> AllNetworks => _networks.Values;

  /// <summary>Returns the network that owns <paramref name="pos"/>, or <c>null</c>.</summary>
  public BlockNetwork? GetNetworkAt(BlockPos pos) =>
    _posToNetwork.TryGetValue(pos, out Guid id)
    && _networks.TryGetValue(id, out var net)
      ? net
      : null;

  /// <summary>
  /// Returns the network across <paramref name="connectorFace"/> from <paramref name="connectorPos"/>,
  /// but only when the cell there exposes a connector back toward it. A pipe merely occupying the
  /// adjacent cell without a facing connector is not plumbed in. Returns <c>null</c> when there is no
  /// reciprocating connector or no network there.
  /// </summary>
  /// <remarks>The far cell answers through whatever speaks for it, a membership or the block, the way
  /// the walk resolves one. Reading only the block would leave a machine sitting against a footprint
  /// cell that is a node finding no network across that face.</remarks>
  public BlockNetwork? GetConnectedNetworkAcross(
    IBlockAccessor world,
    BlockPos connectorPos,
    BlockFacing connectorFace
  ) {
    BlockPos neighbourPos = connectorPos.AddCopy(connectorFace);
    return NetworkMembership.CouplesAt(
      world,
      neighbourPos,
      connectorFace.Opposite
    )
      ? GetNetworkAt(neighbourPos)
      : null;
  }

  /// <summary>
  /// The network instance for <paramref name="networkType"/>, or <c>null</c> when no factory is
  /// registered for it. Returning null rather than throwing is deliberate: the only caller runs inside
  /// chunk load, so a mistyped or unregistered type would take the world down over one bad block
  /// declaration. The caller logs the position and the registered types and adds no node.
  /// </summary>
  private BlockNetwork? TryCreateNetwork(string networkType) =>
    _factories.TryGetValue(networkType, out var factory) ? factory() : null;

  /// <summary>Registered network types, for a diagnostic naming what the caller could have meant.</summary>
  private string RegisteredTypes() =>
    _factories.Count == 0
      ? "(none)"
      : string.Join(", ", _factories.Keys.Order());

  /// <summary>
  /// Creates a network of a type already proven registered, because the caller took it off a live
  /// network instance (a fracture split, a root rebuild). Throwing here is correct: a miss is an exlib
  /// invariant violation rather than a mod declaring a bad type, and the content-facing path
  /// (<see cref="AddNode"/>) uses <see cref="TryCreateNetwork"/> instead.
  /// </summary>
  private BlockNetwork CreateNetwork(string networkType) =>
    TryCreateNetwork(networkType)
    ?? throw new InvalidOperationException(
      $"No factory registered for network type '{networkType}', reached from an existing network of "
        + $"that type. Registered: {RegisteredTypes()}."
    );
  #endregion

  #region Graph node manipulation
  /// <summary>
  /// Adds <paramref name="pos"/> to the network graph, merging adjacent networks
  /// of the same type as needed.
  /// </summary>
  /// <param name="broadcast">
  /// When <c>true</c> (default), immediately broadcasts state so clients reflect
  /// the new connectivity.  Pass <c>false</c> during batch operations.
  /// </param>
  public virtual void AddNode(
    IBlockAccessor world,
    BlockPos pos,
    string networkType,
    bool broadcast = true
  ) {
    var connectedNeighbors = GetConnectedNeighbors(world, pos, networkType)
      .ToList();

    var adjacentNetworks = connectedNeighbors
      .Where(_posToNetwork.ContainsKey)
      .Select(p => _networks[_posToNetwork[p]])
      .Distinct()
      .ToList();

    if (adjacentNetworks.Count == 0) {
      // Isolated new node - standalone network, no broadcast needed.
      BlockNetwork? net = TryCreateNetwork(networkType);
      if (net == null) {
        // Only an isolated node reaches the factory - one placed against an existing run joins that
        // network instead - so an unregistered type surfaces intermittently and by position. Naming
        // the block and the registered types is what turns that into something a modder can act on.
        ServerWorld?.Logger.Error(
          "[exlib] Block network: '{0}' at {1} declares network type '{2}', which no mod registered. "
            + "The block is placed but joins no network. Registered types: {3}. "
            + "Call BlockNetworkModSystem.RegisterNetworkType from ModSystem.Start.",
          world.GetBlock(pos)?.Code?.ToString() ?? "unknown block",
          pos,
          networkType,
          RegisteredTypes()
        );
        return;
      }

      net.Nodes.Add(pos);
      _networks[net.Id] = net;
      _posToNetwork[pos] = net.Id;
      net.OnTopologyChanged();
    } else {
      // Join the first adjacent network and merge any others into it.
      var primaryNet = adjacentNetworks[0];
      primaryNet.Nodes.Add(pos);
      _posToNetwork[pos] = primaryNet.Id;

      for (int i = 1; i < adjacentNetworks.Count; i++) {
        var netToMerge = adjacentNetworks[i];
        if (!primaryNet.CanMerge(netToMerge, world))
          continue;

        foreach (var nPos in netToMerge.Nodes) {
          primaryNet.Nodes.Add(nPos);
          _posToNetwork[nPos] = primaryNet.Id;
        }

        // A suspended review carries over rather than being dropped: the cells the walk could not read
        // are now this network's, so it inherits the doubt about whether they still hang together.
        if (_unreadableNodes.TryGetValue(netToMerge.Id, out var pending)) {
          if (_unreadableNodes.TryGetValue(primaryNet.Id, out var carried))
            carried.UnionWith(pending);
          else
            _unreadableNodes[primaryNet.Id] = pending;
        }

        primaryNet.OnMerge(netToMerge, world);
        DissolveNetwork(netToMerge.Id);
      }

      primaryNet.OnTopologyChanged();
      if (broadcast)
        primaryNet.BroadcastUpdate(world);
    }
  }

  /// <summary>
  /// Removes <paramref name="pos"/> from the network graph, running BFS fracture
  /// detection and splitting the network if it disconnects.
  /// </summary>
  /// <param name="broadcast">
  /// When <c>true</c> (default), broadcasts the updated state to all surviving
  /// fragment nodes.
  /// </param>
  public virtual void RemoveNode(
    IBlockAccessor world,
    BlockPos pos,
    bool broadcast = true
  ) {
    if (!_posToNetwork.TryGetValue(pos, out Guid netId))
      return;
    if (!_networks.TryGetValue(netId, out BlockNetwork? network))
      return;

    network.Nodes.Remove(pos);
    _posToNetwork.Remove(pos);

    if (network.Nodes.Count == 0) {
      DissolveNetwork(netId);
      return;
    }

    ReviewConnectivity(world, netId, network, broadcast);
  }
  #endregion

  #region Connectivity review
  /// <summary>
  /// Decides whether <paramref name="network"/> is still one run and acts on the answer: splits it
  /// into its connected components, or - when part of it is behind an unloaded chunk - defers that
  /// decision and leaves it whole until the missing cells come back.
  /// </summary>
  /// <remarks>An unreadable cell and an absent one are indistinguishable to the walk, because
  /// <see cref="IBlockAccessor.GetBlock"/> answers the air block for an unloaded chunk rather than
  /// null. Splitting on "cannot see" would fracture a run around a player who walked away from it,
  /// and it would stay fractured: the returning cell finds its position already in a network and
  /// never re-joins. See docs/design/mechanics/pipe-network.md.</remarks>
  private void ReviewConnectivity(
    IBlockAccessor world,
    Guid netId,
    BlockNetwork network,
    bool broadcast
  ) {
    HashSet<BlockPos> visited = WalkFromAnyNode(world, network);

    if (visited.Count < network.Nodes.Count) {
      var unreadable = network
        .Nodes.Where(p => world.GetChunkAtBlockPos(p) == null)
        .ToHashSet();

      if (unreadable.Count > 0) {
        // Suspended, not answered. Every node position comes from a placed block, so an unreadable
        // one is an unloaded chunk rather than a coordinate off the map, and no bounds test is owed.
        _unreadableNodes[netId] = unreadable;
        Settle(world, network, broadcast);
        return;
      }

      _unreadableNodes.Remove(netId);
      Fracture(world, netId, network, broadcast);
      return;
    }

    _unreadableNodes.Remove(netId);
    Settle(world, network, broadcast);
  }

  /// <summary>Walks the graph from one of <paramref name="network"/>'s nodes and returns everything it
  /// reached. Only positions already in the node set are followed, so connectivity is decided by the
  /// nodes alone.</summary>
  private HashSet<BlockPos> WalkFromAnyNode(
    IBlockAccessor world,
    BlockNetwork network
  ) {
    BlockPos startNode = network.Nodes.First();
    var visited = new HashSet<BlockPos> { startNode };
    var queue = new Queue<BlockPos>();
    queue.Enqueue(startNode);

    while (queue.Count > 0) {
      BlockPos curr = queue.Dequeue();
      foreach (
        var adj in GetConnectedNeighbors(world, curr, network.NetworkType)
      ) {
        if (network.Nodes.Contains(adj) && visited.Add(adj))
          queue.Enqueue(adj);
      }
    }

    return visited;
  }

  /// <summary>Rebuilds each connected component of <paramref name="network"/> as a network of its own,
  /// each inheriting its proportional share of the original state.</summary>
  private void Fracture(
    IBlockAccessor world,
    Guid netId,
    BlockNetwork network,
    bool broadcast
  ) {
    var unassigned = new HashSet<BlockPos>(network.Nodes);
    DissolveNetwork(netId);

    while (unassigned.Count > 0) {
      BlockPos newStart = unassigned.First();
      BlockNetwork newNet = CreateNetwork(network.NetworkType);
      _networks[newNet.Id] = newNet;

      var bfsQueue = new Queue<BlockPos>();
      bfsQueue.Enqueue(newStart);
      unassigned.Remove(newStart);
      newNet.Nodes.Add(newStart);
      _posToNetwork[newStart] = newNet.Id;

      while (bfsQueue.Count > 0) {
        BlockPos curr = bfsQueue.Dequeue();
        foreach (
          var adj in GetConnectedNeighbors(world, curr, network.NetworkType)
        ) {
          if (unassigned.Contains(adj)) {
            unassigned.Remove(adj);
            newNet.Nodes.Add(adj);
            _posToNetwork[adj] = newNet.Id;
            bfsQueue.Enqueue(adj);
          }
        }
      }

      newNet.OnSplitFragment(network, world);
      newNet.OnTopologyChanged();

      if (broadcast)
        newNet.BroadcastUpdate(world);
    }
  }

  /// <summary>Leaves <paramref name="network"/> as it stands - one run - after a review that did not
  /// split it, telling it its node set may have moved.</summary>
  private static void Settle(
    IBlockAccessor world,
    BlockNetwork network,
    bool broadcast
  ) {
    network.OnTopologyChanged();
    if (broadcast)
      network.BroadcastUpdate(world);
  }

  /// <summary>Drops <paramref name="netId"/> and anything the graph remembered about it. Every path
  /// that retires a network goes through here, so a suspended review cannot outlive its network.</summary>
  private void DissolveNetwork(Guid netId) {
    _networks.Remove(netId);
    _unreadableNodes.Remove(netId);
  }
  #endregion

  /// <summary>
  /// Completely rebuilds the network rooted at <paramref name="rootPos"/> via BFS,
  /// replacing all existing network entries that overlap with the reachable subgraph.
  /// Preserves state from the old root network so temperature/fill survive rebuilds.
  /// </summary>
  public BlockNetwork? RebuildFromRoot(
    IBlockAccessor world,
    BlockPos rootPos,
    string networkType,
    bool broadcast = true
  ) {
    if (NetworkMembership.Resolve(world, rootPos, networkType) == null)
      return null;

    // BFS-discover all reachable positions.
    var reachable = new HashSet<BlockPos>();
    var bfsQueue = new Queue<BlockPos>();
    reachable.Add(rootPos);
    bfsQueue.Enqueue(rootPos);

    while (bfsQueue.Count > 0) {
      var curr = bfsQueue.Dequeue();
      foreach (var neighbor in GetConnectedNeighbors(world, curr, networkType)) {
        if (reachable.Add(neighbor))
          bfsQueue.Enqueue(neighbor);
      }
    }

    // Collect old network IDs that overlap with the reachable set.
    var oldNetIds = new HashSet<Guid>();
    foreach (var pos in reachable) {
      if (_posToNetwork.TryGetValue(pos, out Guid id))
        oldNetIds.Add(id);
    }

    // Preserve state of the current root network (temperature, fill level, …).
    _posToNetwork.TryGetValue(rootPos, out Guid rootOldId);
    BlockNetwork? rootOldNet =
      rootOldId != default && _networks.TryGetValue(rootOldId, out var ron)
        ? ron
        : null;

    // Tear down all overlapping old networks.
    foreach (var id in oldNetIds) {
      if (_networks.TryGetValue(id, out var oldNet)) {
        foreach (var p in oldNet.Nodes)
          _posToNetwork.Remove(p);
        DissolveNetwork(id);
      }
    }

    // Build the new root-anchored network.
    var newNet = CreateNetwork(networkType);
    newNet.RootPos = rootPos.Copy();

    if (rootOldNet != null)
      newNet.InheritStateFrom(rootOldNet);

    foreach (var pos in reachable) {
      newNet.Nodes.Add(pos);
      _posToNetwork[pos] = newNet.Id;
    }

    _networks[newNet.Id] = newNet;
    newNet.OnTopologyChanged();

    if (broadcast)
      newNet.BroadcastUpdate(world);

    return newNet;
  }

  #region Tick
  /// <summary>
  /// One second of graph work: resumes any connectivity review an unloaded chunk suspended, then
  /// dispatches <see cref="BlockNetwork.OnTick"/> for every live network. Registered in
  /// <see cref="StartServerSide"/>; public so the headless harness drives the same path.
  /// </summary>
  public void ServerTick(IBlockAccessor blockAccessor, float dt) {
    // The network tick is a server-global listener that survives chunk unload, so a rejoin can
    // deliver one huge dt that an over-pressure grace timer would cross in a single step. Capped at
    // 2x the 1000ms interval, matching BlockEntityProductionMachine.
    dt = GameMath.Min(dt, 2f);
    ResumeSuspendedReviews(blockAccessor);
    foreach (var network in _networks.Values.ToList())
      network.OnTick(blockAccessor, dt, this);
  }

  /// <summary>
  /// Re-decides the connectivity of every network a missing chunk left suspended, once at least one of
  /// the cells it could not read is readable again. Costs a dictionary count in the ordinary case.
  /// </summary>
  /// <remarks>Driven from the tick rather than from a chunk event, so the review always sees a chunk
  /// that has finished bringing its block entities back - a footprint cell answers through nothing
  /// else - and so a run cannot stay suspended for good because one event went missing.</remarks>
  private void ResumeSuspendedReviews(IBlockAccessor world) {
    if (_unreadableNodes.Count == 0)
      return;

    // Snapshot: a review splits its network, which rewrites both dictionaries underneath us.
    var ready = _unreadableNodes
      .Where(e => e.Value.Any(p => world.GetChunkAtBlockPos(p) != null))
      .Select(e => e.Key)
      .ToList();

    foreach (Guid netId in ready) {
      if (_networks.TryGetValue(netId, out BlockNetwork? network))
        ReviewConnectivity(world, netId, network, broadcast: true);
      else
        _unreadableNodes.Remove(netId);
    }
  }
  #endregion

  #region Public utilities
  /// <summary>
  /// Returns the connector faces on <paramref name="pos"/> that have no valid network neighbour
  /// (open ends / leaks), as seen by <paramref name="member"/> - the cell's own participation in its
  /// network, from <see cref="NetworkMembership.Resolve"/> or from the block the caller already holds.
  /// </summary>
  /// <remarks>An open face is a physical fact, not a graph one, so <see cref="CouplesFrom"/> is
  /// deliberately not asked of the source here: a severed or endpoint cell contributes no graph edge
  /// while still meeting the pipe it touches, and capping or venting that face would be visible to
  /// the player. See docs/design/mechanics/pipe-network.md.</remarks>
  public BlockFacing[] GetOpenConnectorFaces(
    IBlockAccessor world,
    BlockPos pos,
    INetworkMember member
  ) {
    var open = new List<BlockFacing>();
    foreach (var face in BlockFacing.ALLFACES) {
      if (!member.HasConnectorAt(world, pos, face))
        continue;

      BlockPos nPos = pos.AddCopy(face);
      Block nBlock = world.GetBlock(nPos);

      bool connected =
        IsValidNetworkNeighbour(world, pos, member, nBlock, nPos, face)
        || SealsAgainst(world, pos, nBlock, face);
      if (!connected)
        open.Add(face);
    }
    return open.Count == 0 ? [] : open.ToArray();
  }

  /// <summary>
  /// Returns all positions that are graph-connected to <paramref name="pos"/> on
  /// <paramref name="networkType"/> (matching connector on the touching face, same network type, not
  /// broken). A cell joins the graph through whatever answers for it there - a membership behaviour
  /// or the block - so a block that spent its base class elsewhere still walks.
  /// </summary>
  public IEnumerable<BlockPos> GetConnectedNeighbors(
    IBlockAccessor world,
    BlockPos pos,
    string networkType
  ) {
    INetworkMember? source = NetworkMembership.Resolve(world, pos, networkType);
    if (source == null || !CouplesFrom(world, pos, source))
      yield break;

    foreach (var face in BlockFacing.ALLFACES) {
      if (source.HasConnectorAt(world, pos, face)) {
        BlockPos neighborPos = pos.AddCopy(face);
        if (
          IsValidNetworkNeighbour(
            world,
            pos,
            source,
            world.GetBlock(neighborPos),
            neighborPos,
            face
          )
        )
          yield return neighborPos;
      }
    }
  }

  /// <summary>
  /// Whether the cell at <paramref name="pos"/> passes the run on: a fixed endpoint terminates it
  /// rather than continuing through, and a severed cell (a closed valve, a solidified canal) breaks
  /// it. Asked of the source and of every candidate neighbour, so a severed cell drops off the graph
  /// from both sides at once.
  /// </summary>
  private static bool CouplesFrom(
    IBlockAccessor world,
    BlockPos pos,
    INetworkMember member
  ) => !member.IsNetworkEndPoint && !member.IsConnectionBroken(world, pos);

  /// <summary>
  /// Whether the block at <paramref name="pos"/> counts a non-network neighbour on
  /// <paramref name="face"/> as sealed rather than open, suppressing a false leak against a machine
  /// housing. Read from the block because the hook is a block-side one.
  /// </summary>
  private static bool SealsAgainst(
    IBlockAccessor world,
    BlockPos pos,
    Block neighbour,
    BlockFacing face
  ) =>
    world.GetBlock(pos) is BlockNetworkNode node
    && node.IsValidNonNetworkConnection(neighbour, face);

  /// <summary>
  /// Whether the cell across <paramref name="facing"/> joins <paramref name="source"/>. The neighbour
  /// is resolved exactly as the source is, so a block carrying a membership can be walked to as well
  /// as from; resolving only the block would leave the graph one-directional.
  /// </summary>
  private static bool IsValidNetworkNeighbour(
    IBlockAccessor world,
    BlockPos sourcePos,
    INetworkMember source,
    Block neighbourBlock,
    BlockPos neighbourPos,
    BlockFacing facing
  ) {
    INetworkMember? neighbour = NetworkMembership.Resolve(
      world,
      neighbourPos,
      source.NetworkTypeAt(world, sourcePos)
    );
    if (
      neighbour == null
      || !neighbour.HasConnectorAt(world, neighbourPos, facing.Opposite)
    )
      return false;

    // Matching connectors and network type do not imply the two physically couple (see
    // INetworkMember.AcceptsNeighbour). Tested here because this is the one path shared by the
    // traversal and the open-end scan, so a refused joint reads the same way to both.
    if (!source.AcceptsNeighbour(neighbourBlock))
      return false;

    return CouplesFrom(world, neighbourPos, neighbour);
  }

  /// <summary>
  /// Maps an orientation string of single-letter side codes ("ns", "we", "nsewud") to the faces it
  /// names, dropping any letter that names no side and any repeat. The one walk shared by everything
  /// that turns an orientation into a connector set - a node block's variant, a membership's declared
  /// faces - so the two cannot drift on which letters count.
  /// </summary>
  public static BlockFacing[] SidesToFaces(string? orientation) =>
    [
      .. (orientation ?? "")
        .Select(c => SideToFace(c.ToString()))
        .OfType<BlockFacing>()
        .Distinct(),
    ];

  /// <summary>Maps a single-char side code ("n","s","e","w","u","d") to its <see cref="BlockFacing"/>.</summary>
  public static BlockFacing? SideToFace(string? side) =>
    side switch {
      "n" => BlockFacing.NORTH,
      "s" => BlockFacing.SOUTH,
      "e" => BlockFacing.EAST,
      "w" => BlockFacing.WEST,
      "u" => BlockFacing.UP,
      "d" => BlockFacing.DOWN,
      _ => null,
    };

  /// <summary>Returns <c>true</c> when <paramref name="neighbour"/> is a network block of type <paramref name="id"/>.</summary>
  public static bool IsCompatibleNetworkBlock(Block neighbour, string id) =>
    neighbour is INetworkConnector connector && connector.NetworkType == id;

  /// <summary>
  /// Position-aware compatibility - like <see cref="IsCompatibleNetworkBlock"/> but consults
  /// the connector's per-cell network type, so a structure filler that exposes a port on one
  /// footprint cell reads as compatible only on that cell.
  /// </summary>
  public static bool IsCompatibleNetworkBlockAt(
    IBlockAccessor world,
    BlockPos pos,
    Block neighbour,
    string id
  ) =>
    neighbour is INetworkConnector connector
    && connector.NetworkTypeAt(world, pos) == id;

  public override void Dispose() {
    _networks.Clear();
    _posToNetwork.Clear();
    _unreadableNodes.Clear();
    base.Dispose();
  }
  #endregion
}
