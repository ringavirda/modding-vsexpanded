using System;
using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Networks;

/// <summary>
/// Concrete <see cref="BlockNetwork"/> for the molten-canal system. Each node (an
/// <see cref="IMoltenCell"/>) owns its own metal, so merge and split need no redistribution; the
/// network supplies connectivity plus the per-tick driver that flows metal cell-to-cell by level
/// equalisation and runs each cell's cooling. Cells are resolved from
/// <see cref="BlockNetwork.Nodes"/>. See docs/design/mechanics/molten-network.md.
/// </summary>
public class MoltenNetwork(BlockNetworkModSystem system) : BlockNetwork(system) {
  public override string NetworkType => "molten";

  // Resolved once from the first loaded node's BE (a network never moves between worlds).
  private IWorldAccessor? _world;

  private IWorldAccessor? GetWorld(IBlockAccessor blockAccessor) {
    if (_world != null)
      return _world;
    foreach (var pos in Nodes) {
      if (
        blockAccessor.GetBlockEntity(pos) is BlockEntity be
        && be.Api?.World != null
      )
        return _world = be.Api.World;
    }
    return null;
  }

  private static int ComparePos(BlockPos a, BlockPos b) {
    int c = a.X.CompareTo(b.X);
    if (c != 0)
      return c;
    c = a.Y.CompareTo(b.Y);
    return c != 0 ? c : a.Z.CompareTo(b.Z);
  }

  // Distance-from-start is purely topological, so the map is cached and the BFS re-run only when a
  // cheap topology signature changes rather than every tick.
  private Dictionary<BlockPos, int>? _cachedDistFromStart;
  private (int Count, long PosHash, long StartHash) _cachedTopoSig;

  /// <summary>
  /// The cached distance-from-start map, rebuilt via <see cref="BuildDistanceFromStart"/> only when
  /// the topology signature has changed since the last computation.
  /// </summary>
  private Dictionary<BlockPos, int> GetDistanceFromStart(
    IBlockAccessor blockAccessor,
    List<(BlockPos Pos, IMoltenCell Cell)> cells
  ) {
    var sig = ComputeTopologySignature(cells);
    if (_cachedDistFromStart == null || sig != _cachedTopoSig) {
      _cachedDistFromStart = BuildDistanceFromStart(blockAccessor, cells);
      _cachedTopoSig = sig;
    }
    return _cachedDistFromStart;
  }

  /// <summary>
  /// Order-independent fingerprint of the cells that drive the distance map: cell count plus
  /// XOR-folded hashes of all cell positions and of the flow-source positions. Any add, removal or
  /// swap between source and plain cell changes at least one term.
  /// </summary>
  private static (int, long, long) ComputeTopologySignature(
    List<(BlockPos Pos, IMoltenCell Cell)> cells
  ) {
    long posHash = 0;
    long startHash = 0;
    foreach (var c in cells) {
      long h = unchecked(
        (long)((uint)c.Pos.GetHashCode() * 0x9E3779B97F4A7C15UL)
      );
      posHash ^= h;
      if (c.Cell.IsFlowSource)
        startHash ^= h;
    }
    return (cells.Count, posHash, startHash);
  }

  /// <summary>
  /// Multi-source BFS over the canal graph mapping each cell to its hop distance from the nearest
  /// flow source. Cells unreachable from any source (a sourceless run) are absent from the map.
  /// </summary>
  private Dictionary<BlockPos, int> BuildDistanceFromStart(
    IBlockAccessor blockAccessor,
    List<(BlockPos Pos, IMoltenCell Cell)> cells
  ) {
    var dist = new Dictionary<BlockPos, int>(cells.Count);
    var queue = new Queue<BlockPos>();
    foreach (var c in cells)
      if (c.Cell.IsFlowSource) {
        dist[c.Pos] = 0;
        queue.Enqueue(c.Pos);
      }

    while (queue.Count > 0) {
      BlockPos cur = queue.Dequeue();
      int next = dist[cur] + 1;
      if (blockAccessor.GetBlock(cur) is not BlockNetworkNode node)
        continue;

      foreach (var face in BlockFacing.HORIZONTALS) {
        if (!node.HasConnectorAt(face))
          continue;
        BlockPos npos = cur.AddCopy(face);
        if (!Nodes.Contains(npos) || dist.ContainsKey(npos))
          continue;
        if (blockAccessor.GetBlockEntity(npos) is not IMoltenCell)
          continue;
        dist[npos] = next;
        queue.Enqueue(npos);
      }
    }
    return dist;
  }

  /// <summary>
  /// Orders cells for the flow pass, greater distance from the source first, so metal is driven from
  /// the farthest cells back toward the source. Position breaks ties; a cell unreachable from any
  /// source counts as farthest.
  /// </summary>
  private static int CompareFlowOrder(
    (BlockPos Pos, IMoltenCell Cell) x,
    (BlockPos Pos, IMoltenCell Cell) y,
    Dictionary<BlockPos, int> dist
  ) {
    int dx = dist.TryGetValue(x.Pos, out int vx) ? vx : int.MaxValue;
    int dy = dist.TryGetValue(y.Pos, out int vy) ? vy : int.MaxValue;
    int c = dy.CompareTo(dx); // descending distance: farthest processed first
    return c != 0 ? c : ComparePos(x.Pos, y.Pos);
  }

  #region Tick - flow + cooling
  public override void OnTick(
    IBlockAccessor blockAccessor,
    float dt,
    BlockNetworkModSystem manager
  ) {
    var world = GetWorld(blockAccessor);
    if (world == null)
      return;

    var cells = new List<(BlockPos Pos, IMoltenCell Cell)>(Nodes.Count);
    foreach (var pos in Nodes)
      if (blockAccessor.GetBlockEntity(pos) is IMoltenCell c)
        cells.Add((pos, c));
    if (cells.Count == 0)
      return;

    // Farthest-first order drains the run toward the source one wavefront per tick rather than in
    // arbitrary positional order.
    var distFromStart = GetDistanceFromStart(blockAccessor, cells);
    cells.Sort((x, y) => CompareFlowOrder(x, y, distFromStart));
    foreach (var c in cells)
      c.Cell.EnsureMetalStack(world);

    int maxFlow = ExlibValues.MoltenFlowRate;
    foreach (var a in cells) {
      if (
        a.Cell.Sealed
        || a.Cell.Solidified
        || blockAccessor.GetBlock(a.Pos) is not BlockNetworkNode aNode
      )
        continue;

      // ALLFACES, not HORIZONTALS: the graph joins vertical neighbours, so a run that steps down a level
      // has to exchange metal across that edge too. HasConnectorAt still gates every face, so flow only
      // happens where a connector exists.
      foreach (var face in BlockFacing.ALLFACES) {
        if (!aNode.HasConnectorAt(face))
          continue;
        BlockPos npos = a.Pos.AddCopy(face);
        if (!Nodes.Contains(npos))
          continue;
        if (blockAccessor.GetBlockEntity(npos) is not IMoltenCell bCell)
          continue;
        var b = (Pos: npos, Cell: bCell);
        if (b.Cell.Sealed || b.Cell.Solidified)
          continue;

        // A vertical edge is downhill only: it is driven from the upper cell (face DOWN) and the reverse
        // face is skipped outright, bypassing the distance ordering that decides horizontal edges, which
        // would pump metal uphill. A full lower cell backs the upper one up, the back-pressure the rest
        // of the canal model relies on.
        if (face.Axis == EnumAxis.Y) {
          if (face != BlockFacing.DOWN)
            continue;
          FlowEdge(a.Cell, b.Cell, maxFlow, world, downhillOnly: true);
          continue;
        }

        // Drive each undirected edge exactly once, from the cell farther from the source (ties broken
        // by position).
        if (CompareFlowOrder(a, b, distFromStart) >= 0)
          continue;

        FlowEdge(a.Cell, b.Cell, maxFlow, world);
      }
    }

    // Thermal pass: cool / solidify each cell.
    foreach (var c in cells)
      c.Cell.UpdateThermal(world);
  }

  /// <summary>
  /// Moves metal across one connection toward an equal amount (not an equal fill ratio), capped at
  /// <paramref name="maxFlow"/> units. <paramref name="downhillOnly"/> makes the edge one-way from
  /// <paramref name="aNode"/>, used for vertical edges where levelling both ways would pump metal uphill.
  /// </summary>
  private static void FlowEdge(
    IMoltenCell aNode,
    IMoltenCell bNode,
    int maxFlow,
    IWorldAccessor world,
    bool downhillOnly = false
  ) {
    var aCap = aNode.MaxUnitCapacity;
    var bCap = bNode.MaxUnitCapacity;
    if (aCap <= 0 || bCap <= 0)
      return;

    var diff = Math.Abs(aNode.CellAmount - bNode.CellAmount);
    if (diff == 0)
      return;

    bool aIsGiver = aNode.CellAmount > bNode.CellAmount;
    // A downhill edge runs one way only: nothing moves when the lower cell is the fuller one.
    if (downhillOnly && !aIsGiver)
      return;
    IMoltenCell giver = aIsGiver ? aNode : bNode;
    IMoltenCell receiver = aIsGiver ? bNode : aNode;
    if (giver.CellAmount <= 0f)
      return;

    // Different metals sit side by side without mixing.
    if (
      receiver.CellAmount > 0f
      && receiver.CellMetalType != giver.CellMetalType
    )
      return;

    // A levelling pair moves half the difference: the whole difference overshoots the midpoint and swaps
    // the two levels (100/80 becomes 80/100), so the pair oscillates instead of settling. Integer division
    // floors, so the step decays 20 -> 10 -> 5 -> 2 -> 1 -> 0 in whole units and the edge goes quiet on its
    // own. The two exemptions are one-way sinks, not levelling pairs - a drain fitting (pedestal/tap)
    // consuming metal and a downhill edge pouring into the cell below - and take the whole difference so no
    // dregs are stranded.
    var step = receiver.AcceptsSubMinimumFlow || downhillOnly ? diff : diff / 2;
    var transfer = step > maxFlow ? maxFlow : step;

    // A levelling edge applies no MoltenMinFlowAmount floor: combined with halving the floor would act as a
    // deadband of twice its size, so at a floor of 10 a pair 19 units apart would never level. Halving is
    // proportional and decays to nothing on its own.
    if (transfer <= 0)
      return;

    var accepted = receiver.PushMetalRaw(
      transfer,
      giver.CellMetalType,
      giver.CellTemperature,
      world
    );
    if (accepted > 0f)
      giver.DrainMetal(accepted);
  }
  #endregion

  #region Graph lifecycle (cells own their metal, so these are trivial)
  public override bool CanMerge(BlockNetwork other, IBlockAccessor world) =>
    other is MoltenNetwork;

  public override void OnMerge(BlockNetwork other, IBlockAccessor world) { }

  public override void OnSplitFragment(
    BlockNetwork original,
    IBlockAccessor world
  ) { }
  #endregion
}
