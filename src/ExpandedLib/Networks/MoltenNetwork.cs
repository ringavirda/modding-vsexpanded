using System;
using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Networks;

/// <summary>
/// Concrete <see cref="BlockNetwork"/> for the molten-canal system. Each canal block
/// (an <see cref="IMoltenCell"/>) owns its own metal; the network only provides connectivity plus the
/// per-tick driver that flows metal cell-to-cell (level-equalisation) and runs each cell's cooling.
/// Because cells own their metal, merge/split need no redistribution.
/// <para>
/// The driver works over the <see cref="IMoltenCell"/> contract and the block-network graph, so the
/// concrete canal / start / tap / pedestal block entities live in their content mod (iwex) while this
/// framework code lives in exlib. Positions come from the network's own <see cref="BlockNetwork.Nodes"/>
/// set, paired with each resolved cell.
/// </para>
/// </summary>
public class MoltenNetwork(BlockNetworkModSystem system) : BlockNetwork(system)
{
  public override string NetworkType => "molten";

  // Resolved once from the first loaded node's BE (a network never moves between worlds).
  private IWorldAccessor? _world;

  private IWorldAccessor? GetWorld(IBlockAccessor blockAccessor)
  {
    if (_world != null)
      return _world;
    foreach (var pos in Nodes)
    {
      if (
        blockAccessor.GetBlockEntity(pos) is BlockEntity be
        && be.Api?.World != null
      )
        return _world = be.Api.World;
    }
    return null;
  }

  private static int ComparePos(BlockPos a, BlockPos b)
  {
    int c = a.X.CompareTo(b.X);
    if (c != 0)
      return c;
    c = a.Y.CompareTo(b.Y);
    return c != 0 ? c : a.Z.CompareTo(b.Z);
  }

  // Distance-from-start is purely topological, so cache the map and recompute (BFS) only when a
  // cheap topology signature changes, instead of every tick.
  private Dictionary<BlockPos, int>? _cachedDistFromStart;
  private (int Count, long PosHash, long StartHash) _cachedTopoSig;

  /// <summary>
  /// Returns the cached distance-from-start map, rebuilding it (via
  /// <see cref="BuildDistanceFromStart"/>) only when the network's topology
  /// signature has changed since the last computation.
  /// </summary>
  private Dictionary<BlockPos, int> GetDistanceFromStart(
    IBlockAccessor blockAccessor,
    List<(BlockPos Pos, IMoltenCell Cell)> cells
  )
  {
    var sig = ComputeTopologySignature(cells);
    if (_cachedDistFromStart == null || sig != _cachedTopoSig)
    {
      _cachedDistFromStart = BuildDistanceFromStart(blockAccessor, cells);
      _cachedTopoSig = sig;
    }
    return _cachedDistFromStart;
  }

  /// <summary>
  /// Order-independent fingerprint of the cells that drive the distance map: cell
  /// count plus XOR-folded hashes of all cell positions and of the flow-source
  /// positions. Any add, removal, or source↔plain swap changes at least one term.
  /// </summary>
  private static (int, long, long) ComputeTopologySignature(
    List<(BlockPos Pos, IMoltenCell Cell)> cells
  )
  {
    long posHash = 0;
    long startHash = 0;
    foreach (var c in cells)
    {
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
  /// Multi-source BFS over the canal graph that maps each cell to its hop
  /// distance from the nearest flow source. Cells unreachable from any source
  /// (e.g. a sourceless run) are simply absent.
  /// </summary>
  private Dictionary<BlockPos, int> BuildDistanceFromStart(
    IBlockAccessor blockAccessor,
    List<(BlockPos Pos, IMoltenCell Cell)> cells
  )
  {
    var dist = new Dictionary<BlockPos, int>(cells.Count);
    var queue = new Queue<BlockPos>();
    foreach (var c in cells)
      if (c.Cell.IsFlowSource)
      {
        dist[c.Pos] = 0;
        queue.Enqueue(c.Pos);
      }

    while (queue.Count > 0)
    {
      BlockPos cur = queue.Dequeue();
      int next = dist[cur] + 1;
      if (blockAccessor.GetBlock(cur) is not BlockNetworkNode node)
        continue;

      foreach (var face in BlockFacing.HORIZONTALS)
      {
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
  /// Orders cells for the flow pass: greater distance from the source first, so
  /// metal is driven from the farthest cells back toward the source. Position
  /// breaks ties (including cells unreachable from a source, treated as farthest).
  /// </summary>
  private static int CompareFlowOrder(
    (BlockPos Pos, IMoltenCell Cell) x,
    (BlockPos Pos, IMoltenCell Cell) y,
    Dictionary<BlockPos, int> dist
  )
  {
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
  )
  {
    var world = GetWorld(blockAccessor);
    if (world == null)
      return;

    var cells = new List<(BlockPos Pos, IMoltenCell Cell)>(Nodes.Count);
    foreach (var pos in Nodes)
      if (blockAccessor.GetBlockEntity(pos) is IMoltenCell c)
        cells.Add((pos, c));
    if (cells.Count == 0)
      return;

    // Order cells by graph distance from the source, farthest first, so metal drains toward the
    // source a wavefront at a time rather than in arbitrary positional order.
    var distFromStart = GetDistanceFromStart(blockAccessor, cells);
    cells.Sort((x, y) => CompareFlowOrder(x, y, distFromStart));
    foreach (var c in cells)
      c.Cell.EnsureMetalStack(world);

    int maxFlow = ExlibValues.MoltenFlowRate;
    foreach (var a in cells)
    {
      if (
        a.Cell.Sealed
        || a.Cell.Solidified
        || blockAccessor.GetBlock(a.Pos) is not BlockNetworkNode aNode
      )
        continue;

      // ALLFACES, not HORIZONTALS: the graph joins vertical neighbours, so a run that steps down a level was
      // in one network yet never exchanged any metal - the two halves just sat there. HasConnectorAt still
      // gates every face, so this only enables flow where a connector actually exists.
      foreach (var face in BlockFacing.ALLFACES)
      {
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

        // A vertical edge is downhill only - molten metal runs down a launder, it never climbs. So it is
        // driven from the upper cell (face DOWN) and the reverse face is skipped outright, rather than going
        // through the distance ordering that decides horizontal edges: a level-seeking vertical edge would
        // pump metal uphill. A full lower cell simply backs the upper one up, which is exactly the
        // back-pressure the rest of the canal model already relies on.
        if (face.Axis == EnumAxis.Y)
        {
          if (face != BlockFacing.DOWN)
            continue;
          FlowEdge(a.Cell, b.Cell, maxFlow, world, downhillOnly: true);
          continue;
        }

        // Drive each undirected edge exactly once, from the cell farther from
        // the source (ties broken by position).
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
  /// Moves metal across one connection toward an equal <b>amount</b>, capped at <paramref name="maxFlow"/>
  /// units. Deliberately amount, not fill <em>ratio</em>. Whether differently-sized cells (canal vs
  /// bed vs long cell) should level by ratio instead is a live tuning question, not a bug.
  /// <para>
  /// <paramref name="downhillOnly"/> makes the edge one-way from <paramref name="aNode"/>: used for vertical
  /// edges, where levelling in both directions would pump metal uphill.
  /// </para>
  /// </summary>
  private static void FlowEdge(
    IMoltenCell aNode,
    IMoltenCell bNode,
    int maxFlow,
    IWorldAccessor world,
    bool downhillOnly = false
  )
  {
    var aCap = aNode.MaxUnitCapacity;
    var bCap = bNode.MaxUnitCapacity;
    if (aCap <= 0 || bCap <= 0)
      return;

    var diff = Math.Abs(aNode.CellAmount - bNode.CellAmount);
    if (diff == 0)
      return;

    bool aIsGiver = aNode.CellAmount > bNode.CellAmount;
    // A downhill edge only ever runs one way. If the lower cell is the fuller one there is nothing to do -
    // metal does not climb back up the launder.
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

    // Move half the difference, not all of it. Moving the whole difference overshoots the midpoint and
    // swaps the two cells' levels outright - 100/80 becomes 80/100 - so a pair never settles and instead
    // oscillates for as long as the run is alive. Half converges monotonically, which is what "flows toward
    // level" is supposed to mean.
    //
    // Two exemptions keep their whole-difference behaviour, and both are one-way sinks rather than pairs
    // levelling with each other: a drain fitting (pedestal/tap) is consuming the metal, and a downhill edge
    // is pouring into the cell below. Halving those would strand dregs that have nowhere else to go.
    //
    // Whole units only, never less than the minimum per tick - except into a drain fitting, which takes the
    // final sub-minimum dregs so a run can empty completely.
    // Integer division floors, which is what keeps this to whole units and makes it terminate: the step
    // decays 20 -> 10 -> 5 -> 2 -> 1 -> 0 and the edge goes quiet on its own once the pair is level.
    var step = receiver.AcceptsSubMinimumFlow || downhillOnly ? diff : diff / 2;
    var transfer = step > maxFlow ? maxFlow : step;

    // No MoltenMinFlowAmount floor on a levelling edge, and that is deliberate. The floor exists to "stop
    // sub-unit dribbles", but combined with halving it becomes a deadband of twice its own size: at a floor of
    // 10 a pair 19 units apart would move nothing at all, forever, and a canal would quietly stop delivering
    // partway along. Halving is the better dribble control because it is proportional - it decays to nothing
    // by itself - so the floor has no work left to do here. It still applies to nothing else: the only other
    // callers are one-way sinks, which were already exempt from it.
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
