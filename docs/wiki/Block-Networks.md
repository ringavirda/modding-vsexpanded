# Block Networks

A generic connected-graph framework: self-orienting node blocks, live network instances with
merge/fracture handling, and a single manager `ModSystem`. It also ships
the two concrete networks the mods use - the `PipeNetwork` (gas + water, registered as `"pipe"` by
`lpex`) and the `MoltenNetwork` (metal canals, registered as `"molten"` by `iwex`) - so all three
mods share one implementation. Each mod just registers the type and supplies its own
content-specific pieces through the seams below. You register your own network type the same way.

The code sits in two namespaces, and you will usually import both: **`ExpandedLib.Networks`** holds the
graph model (`BlockNetwork` and its subclasses, `PipeNetworkState`, and every `I*Node`/`I*Connector`
contract) - no Vintage Story block types involved - while **`ExpandedLib.Blocks.Networks`** holds the
engine-facing shell (`BlockNetworkNode`, `BlockEntityNetworkNode`, `BlockNetworkModSystem`).

Network tunables (litres per pipe, leak/evaporation rates, over-pressure grace, molten flow rate
and minimum) live in exlib's own config, `ExlibValues` (`ModConfig/exlib_values.json`,
`/exmod config exlib …`), since the network code that reads them lives here now. Content-specific
numbers (pipe burst pressures, chimney draw rate, molten cooldown) stay in each mod's own config.

## The pieces

| Piece | Type | Role |
| --- | --- | --- |
| `BlockNetworkNode` | abstract `Block` | A node block that auto-orients to its neighbours and reports its connector faces. |
| `BlockEntityNetworkNode` | abstract `BlockEntity` | The node's block entity: registers/unregisters with the graph, persists state, forwards network updates. |
| `BlockNetwork` | abstract class | One live network instance: owns typed `State`, merges/splits/ticks. |
| `BlockNetworkModSystem` | `ModSystem` | The graph manager: add/remove nodes, BFS fracture detection, per-tick dispatch. |
| `INetworkNode` / `INetworkConnector` | interfaces | Contracts a block entity / block implement to participate. |

The mental model: **blocks** expose connector faces, **block entities** are graph nodes, the
**manager** maintains the graph and hands each connected component a **`BlockNetwork`** instance
that carries your gameplay state (pressure, fluid, temperature...).

## Registering a network type

Once, during `ModSystem.Start`, give the manager a factory for your network type:

```csharp
public override void Start(ICoreAPI api)
{
    var networks = api.ModLoader.GetModSystem<BlockNetworkModSystem>();
    // lpex's pipe: the vent strategy is optional content (the vanilla-chimney gas draw).
    networks.RegisterNetworkType("pipe", () => new PipeNetwork(networks, new LpexChimneyVent()));
    // iwex's molten canals need no extra pieces beyond the IMoltenCell block entities.
    networks.RegisterNetworkType("molten", () => new MoltenNetwork(networks));
}
```

The manager creates one network per connected component and calls into it as the topology
and clock advance. The factory runs once **per** network instance, so anything the network needs
per-run (e.g. the chimney-vent's sound-throttle state) can be created fresh in the factory.

### Concrete-network seams

Because `PipeNetwork` / `MoltenNetwork` live in exlib, they reach content-specific behaviour
through small interfaces the mods implement, never by naming a mod's block type:

| Seam | Implemented by | Role |
| --- | --- | --- |
| `IMoltenCell` | the canal block entities | Per-cell metal state + capability flags (`IsFlowSource`, `AcceptsSubMinimumFlow`) the molten flow driver reads. |
| `IBurstablePipe` | `BlockPipe` | `{ CanBurst, BurstPressure }` - the pipe network walks nodes for the weakest burst rating. |
| `IPipeVentStrategy` | `LpexChimneyVent` | Optional gas-vent (chimney) classification + draw, injected at `RegisterNetworkType`. |
| `INetworkNode.OnLeak` | `BlockEntityPipe` (override) | Leak feedback (particles/sound) for a node on a leaking run; default no-op. |

## Defining a node block

```csharp
[BlockRegister]
public class BlockPipe : BlockNetworkNode
{
    public override string NetworkType => "pipe";

    // Shape "type" (from the variant map) -> valid orientation strings for that shape.
    public override Dictionary<string, string[]> AllowedOrientations { get; } = new()
    {
        ["straight"] = ["ns", "we", "ud"],
        ["bend"]     = ["ne", "es", "sw", "wn"],
    };

    public override string GetFallbackOrientation(string? type) => "ns";
}
```

`BlockNetworkNode` does the heavy lifting: at placement it computes the best orientation from
surrounding network blocks (`TryPlaceBlock`), recomputes on neighbour change
(`OnNeighbourBlockChange`), supplies rotated collision/selection boxes, drops a canonical
(fallback-orientation) item, and supports wrench rotation (`IWrenchOrientable`).

### Orientation convention

`Orientation` is the concatenation of single-character face codes the node connects on -
`"ns"` (north+south), `"we"`, `"ud"` (up+down), `"nswe"` (all four horizontals). The variant
group that drives placement is **`orientation`** (singular); a mis-named variant group silently
breaks placement and wrenching, so keep it exactly that.

### Key `BlockNetworkNode` members to know

```csharp
public abstract string NetworkType { get; }
public abstract Dictionary<string, string[]> AllowedOrientations { get; }
public abstract string GetFallbackOrientation(string? type);

public virtual bool IsNetworkEndPoint { get; }          // fixed endpoint, excluded from neighbour discovery
protected virtual bool IsFullCube { get; }              // full-cell blocks cycle the full topology on wrench
protected virtual bool CanWrenchRotate(IWorldAccessor world, BlockPos pos);

public virtual bool HasConnectorAt(BlockFacing face);   // true if Orientation contains the face code
public virtual BlockFacing[]? GetConnectorFaces();      // all connector faces, or null if unorientated
public virtual bool IsValidNonNetworkConnection(Block neighborBlock, BlockFacing face);  // suppress false leaks

protected virtual void GetRotations(string orientation, out float rotX, out float rotY, out float rotZ);
public virtual void RecalculateAndSyncOrientations(IWorldAccessor world, BlockPos pos);
```

Override `GetRotations` only for custom orientation alphabets; override
`IsValidNonNetworkConnection` to tell `GetOpenConnectorFaces` that an adjacent non-network block (a
machine housing, say) seals the face rather than leaving it open. Note that nothing in the suite
overrides it: an open face only becomes a leak when the neighbour is air, so a face against a solid
block is already quiet. Overriding it changes which faces are open for every consumer of that set,
not just the leak count.

## Defining the node block entity

```csharp
[BlockEntityRegister]
public class BlockEntityPipe : BlockEntityNetworkNode
{
    public override string NetworkType { get; set; } = "pipe";
}
```

`BlockEntityNetworkNode` registers the node in `Initialize`, unregisters in `OnBlockRemoved`,
and persists `networkType` / `orientation` / `possibleOrientations` / network state. You usually
override only the state-serialization hooks and the dynamic-connectivity hook:

```csharp
public override string NetworkType { get; set; }
public virtual bool HasConnectorAt(BlockFacing face);
public virtual void OnNetworkUpdate(object? state);     // network pushes its latest state here (client display)
public virtual bool IsConnectionBroken();               // return true to dynamically sever the graph here
public virtual void OnOpenConnectorsChanged(BlockFacing[] openFaces);  // open faces (leaks) changed

// State persistence hooks - override these to round-trip your typed network state:
protected virtual bool IsNetworkStateMeaningful(object? state);
protected virtual object? DeserializeNetworkState(ITreeAttribute tree);
protected virtual void SerializeNetworkState(ITreeAttribute tree, object? state);
```

> **Dynamic severing.** A node that can cut the network (a closed valve) overrides
> `IsConnectionBroken()` to return `true` while closed. The graph re-walks on every state change,
> so toggling it merges or fractures the network live. Restore the broken/closed flag in
> `FromTreeAttributes` **before** `Initialize` runs so `AddNode` sees the right state.

## Writing a `BlockNetwork`

Your network subclass owns the gameplay simulation. The manager calls these:

```csharp
public abstract string NetworkType { get; }
public HashSet<BlockPos> Nodes { get; }                 // every position in this network
public BlockPos? RootPos { get; set; }                  // for root-anchored networks; else null
public object? State { get; protected set; }            // your typed state object

public virtual void RestoreState(object? state);        // injected on world load - cast to your type
public void BroadcastUpdate(IBlockAccessor world);      // push State to every node's OnNetworkUpdate

public virtual bool CanMerge(BlockNetwork other, IBlockAccessor world);   // veto a merge (default: allow)
public abstract void OnMerge(BlockNetwork other, IBlockAccessor world);   // combine state on merge
public abstract void OnSplitFragment(BlockNetwork original, IBlockAccessor world);  // split state after fracture
public abstract void OnTick(IBlockAccessor world, float dt, BlockNetworkModSystem manager);  // per-tick sim
public virtual void InheritStateFrom(BlockNetwork source);   // preserve state across rebuilds
public virtual void OnTopologyChanged();                // Nodes changed - drop caches here

protected virtual void OnBeforeBroadcast(IBlockAccessor world);  // update derived state before broadcast
protected virtual object? GetStatePayload();            // the object actually sent in a broadcast
```

The contract you must satisfy when state is conserved (fluid, gas, charge):

- **`OnMerge`** - fold `other`'s state into this network (sum volumes, average temperature...).
- **`OnSplitFragment`** - when a fracture produces a new fragment, distribute the original's
  state into it (usually proportional to node count). Clamp to physical ceilings: over-pressure
  is legitimate up to a burst limit, so cap at the burst ceiling, not at nominal capacity, or
  every re-walk dumps the run.
- **`OnTopologyChanged`** - invalidate any cached aggregates after the node set changes.

## Manager API (`BlockNetworkModSystem`)

```csharp
public IServerWorldAccessor? ServerWorld { get; }       // non-null on server during tick
public IEnumerable<BlockNetwork> AllNetworks { get; }   // every live network (server-side)

public void RegisterNetworkType(string networkType, Func<BlockNetwork> factory);
public BlockNetwork? GetNetworkAt(BlockPos pos);
public BlockNetwork? GetConnectedNetworkAcross(IBlockAccessor world, BlockPos connectorPos, BlockFacing connectorFace);

public virtual void AddNode(IBlockAccessor world, BlockPos pos, string networkType, bool broadcast = true);
public virtual void RemoveNode(IBlockAccessor world, BlockPos pos, bool broadcast = true);
public BlockNetwork? RebuildFromRoot(IBlockAccessor world, BlockPos rootPos, string networkType, bool broadcast = true);

public BlockFacing[] GetOpenConnectorFaces(IBlockAccessor world, BlockPos pos, INetworkMember member);
public IEnumerable<BlockPos> GetConnectedNeighbors(IBlockAccessor world, BlockPos pos, string networkType);

public static BlockFacing? SideToFace(string? side);
public static bool IsCompatibleNetworkBlock(Block neighbour, string id);
public static bool IsCompatibleNetworkBlockAt(IBlockAccessor world, BlockPos pos, Block neighbour, string id);
```

`BlockEntityNetworkNode` calls `AddNode`/`RemoveNode` for you. **A block entity that is *not* a
`BlockEntityNetworkNode`** (e.g. a multiblock structure that also acts as a node) must call
`AddNode`/`RemoveNode` itself - only the dedicated base does it automatically.

## Connectors vs. nodes

Two interfaces, two roles:

```csharp
public interface INetworkNode            // implemented by the block ENTITY (a graph node)
{
    string? Orientation { get; }
    string[] PossibleOrientations { get; }
    string NetworkType { get; }
    bool HasConnectorAt(BlockFacing face);
    void OnOpenConnectorsChanged(BlockFacing[] openFaces);
    void OnLeak(BlockFacing[] leakingFaces, bool isLiquid, float intensity);  // leak feedback; default no-op
    void OnNetworkUpdate(object? state);
}

public interface INetworkConnector       // implemented by the BLOCK (anything a pipe can touch)
{
    string NetworkType { get; }
    bool HasConnectorAt(BlockFacing face);
    string NetworkTypeAt(IBlockAccessor world, BlockPos pos);
    bool HasConnectorAt(IBlockAccessor world, BlockPos pos, BlockFacing face);
}
```

A **node** is a full graph participant. A **connector** is anything a pipe will connect to,
including fixed machine ports that are *not* nodes - a boiler's steam outlet, an engine's intake.
Such ports read/write the network in the cell on the **far side** of their connector face; see
[Production Machines](Production-Machines) for the `MachinePorts` helpers that do exactly that.

## Visualising networks

`NetworkHighlightModSystem` renders every live network in its own transparent colour, toggled
per player with `.exmod network hi` / `.exmod network unhi`. The graph is server-only, so this is
a client->server request plus server-side `HighlightBlocks`, polled every 250 ms for live updates.
See [Commands](Commands).

## Related pages

- [Production Machines](Production-Machines) - fixed machines that tap a network through a port.
- [Multiblock Structures](Multiblock-Structures) - big machines that are also nodes.
- [Testing Harness](Testing-Harness) - `StubNetwork` / `TestNetworkBlock` exercise the graph headlessly.
