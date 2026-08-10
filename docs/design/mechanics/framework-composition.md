# Framework Composition — form, process, membership
**Status** settled 2026-08-10. This is a decision record: nothing here is built. The current code is
described only where it shows why the target differs.
**Mod** exlib (every seam) · every mod (every consumer)
**Owns** the three axes a machine is composed from, the two seams between them, and the rule that
decides whether a capability is a base class or a behaviour. Supersedes the implicit hierarchy in
which `BlockEntityMultiblockStructure` extends `BlockEntityProductionMachine`.
**Depends on** [multiblock](multiblock.md) (the form vocabulary and the filler mechanism, whose
"a filler can NEVER be a graph node" section this page retires) · [pipe-network](pipe-network.md) ·
[mp-energy](mp-energy.md) · [conventions](../conventions.md) (the block-size vocabulary, the network
families) · [the vanilla source map](../../vanilla/README.md)

---

## Role

A machine in these mods is three independent things at once: a **shape in the world**, a **process
that runs over time**, and a **member of one or more networks**. The framework currently expresses
two of those three as inheritance, and C# gives a class one base. Everything awkward in the block
layer follows from that single fact.

This page fixes the axes and the seams between them so that a capability is added by composition,
and states the rule for when something is a base class at all.

---

## How it works

### The three axes

**Form** — how the machine occupies space. The vocabulary is already
[conventions](../conventions.md)': `block`, `megablock` (more than one cell, via invisible fillers),
`multiblock` (a pattern the player builds by hand, guided by a projection). It is already
compositional in the design and partly so in the code: a boiler is an RCC megablock whose
construction is gated by a multiblock projection, and a footprint cell can host real block-entity
behaviours on the principal's behalf ([multiblock](multiblock.md) § Behaviour-capable fillers).

**Process** — what the machine does over time: a periodic tick and the gate that decides whether it
runs. A process is indifferent to form. The same smelting process is equally at home in a
single-cell block, a megablock, or a hand-built multiblock, and nothing about a tick interval or a
production step depends on how many cells the machine covers.

**Membership** — which networks the machine belongs to. This is a **set, not a scalar**: the
converter is on the pipe network for its blast, the molten network for its tap, and the vanilla
mechanical network for its transmission. Membership is per network type, and within a type it is
per face and per cell.

### The rule

> A capability that a block **has** is a behaviour. A capability that a block **is** is a base class.

Membership is had, not been: a pipe is not a kind of network, it is a block that belongs to one. The
same is true of a process. Form is the one axis that genuinely describes what a block *is*, because
it determines placement, collision and breaking, which the engine resolves through the block class
itself.

Vanilla applies the same rule to the same problem. Its mechanical power system puts all network
state and the whole graph walk in a `BlockEntityBehavior`, `BEBehaviorMPBase`, and asks the block
only three questions through `IMechanicalPowerBlock` — `GetNetwork`, `HasMechPowerConnectorAt`,
`DidConnectAt` (`vssurvivalmod/Systems/MechanicalPower/Network/IMechanicalPowerBlock.cs`).
`BlockMPBase` exists but is a convenience; the contract is the interface.

### Seam 1 — form publishes readiness, process consumes it

A multiblock knows whether its pattern is complete. A process needs a gate. Today that is an
inheritance edge: `BlockEntityMultiblockStructure` extends `BlockEntityProductionMachine` purely so
it can write `CanRunProduction => StructureComplete`
(`ExpandedLib/Blocks/Structures/BlockEntityMultiblockStructure.cs:46`, `:50`) and start and stop the
tick across completion transitions (`:69`, `:82`, `:101`, `:349`).

The coupling is real and must survive; only its expression changes. Form publishes a readiness
signal, process reads it. A plain block publishes "always ready"; a megablock publishes whether its
footprint is intact; a multiblock publishes pattern completion. No process needs to know which kind
of form answered.

Every concrete multiblock in the tree today does run a process, so the edge is currently harmless in
behaviour. It is not harmless in structure: it spends the single block-entity base slot for
everything beneath it, and it loses invariants across the levels — `BlockEntityMultiblockStructure`
overrides `OnBlockRemoved` without `OnBlockUnloaded`, although the parent it inherits from overrides
both (`BlockEntityProductionMachine.cs:151`, `:156`).

### Seam 2 — a cell carries membership, not a block class

The block-network walk resolves a node by block type:

```csharp
if (world.GetBlock(pos) is not BlockNetworkNode node)
  yield break;                       // BlockNetworkModSystem.cs:351, in GetConnectedNeighbors
```

The fracture walk tests the same way at `:242`, so both the discovery and the split path resolve a
node by block type. ([multiblock](multiblock.md) cites `:386-387` for this; the file has drifted
since.)

Three consequences, all currently documented as if they were rules of the domain:

- A filler is a plain `Block`, so it can never be a graph node, however many ports it hosts
  ([multiblock](multiblock.md) § A filler can NEVER be a graph node).
- A block that spent its base on something else reaches a network through `INetworkConnector`
  instead — ten implementors, nine of which are not network nodes at all and read the far-side cell.
- No megastructure is a network node, because none can extend two bases.

The same filler cell can already join the **vanilla** mechanical network, because vanilla resolves
its nodes by behaviour. One cell, two networks, one of which is reachable and one of which is not,
and the only difference is how the walk finds a node.

The target: the walk resolves a node by behaviour, and the block answers only a small interface.
A cell — principal or filler — carries one membership behaviour per network it joins.

### Membership is addressed by network type, never by CLR type

`BlockEntity.Behaviors` is a plain `List<BlockEntityBehavior>`, so several membership behaviours can
coexist on one block entity. `GetBehavior<T>()` returns the **first** match
(`vsapi/Common/Collectible/Block/BlockEntity.cs:52-58`), which is why exlib must supply its own
accessor keyed on network type and face rather than reuse vanilla's.

This is the one place the framework deliberately goes past vanilla, which never needed it: a vanilla
block entity carries at most one `BEBehaviorMPBase`.

### What stays a base class

Little. Once membership, process and structure-completion are behaviours, a shared block-entity root
holds only what every block entity needs and none can be trusted to repeat by hand: teardown that
runs identically whether the block was removed or its chunk unloaded, disposal of renderers, dialogs
and looping sounds, and declared fields that round-trip through the attribute tree.

That last one is a class of bug, not an inconvenience. A field written only by a server tick, read
in `GetBlockInfo`, and absent from `ToTreeAttributes` reads zero on the client for ever;
`BlockEntityPressureValve._lastVentVolume` does exactly this, and calls `MarkDirty(true)` when it
changes, paying for a chunk re-tesselation to synchronise a value it never sends.

---

## Consequences

Retired by this design:

- [multiblock](multiblock.md) § "A filler can NEVER be a graph node", and with it the rolling mill's
  dedicated `BlockRollingMillAxle` cells, which exist only because the two drive cells could not be
  fillers and nodes at once.
- `INetworkConnector` as a parallel mechanism **for membership**. A block that is *on* a network has
  a membership behaviour, and there is no second way. The interface itself survives, narrowed to
  what it was named for: a **port** — a face another network may couple to, on a block that is not
  itself a graph member. The lancashire boiler's water intake stays a port; it is a connection
  target, not a node. The walk already resolves the neighbour side this way; A1 makes the source
  side match.
- The peripheral-block pattern as a *requirement*. Splitting a machine's connections across cells
  stays available because it is often the right shape physically — an intake belongs where the pipe
  arrives — but it is no longer the only way to be on more than one network.

Unchanged: every network's own physics and state model. This page moves where membership is
recorded and how the graph is walked. It does not touch what flows.

---

## Code

Current shape, for reference while the target is built.

| Type / member | file:line | Role today |
|---|---|---|
| `BlockNetworkNode` | `ExpandedLib/Blocks/Networks/BlockNetworkNode.cs` | abstract `Block`; 8 subclasses; the thing the walk tests for |
| `BlockNetworkModSystem.GetConnectedNeighbors` | `ExpandedLib/Blocks/Networks/BlockNetworkModSystem.cs:351` | resolves a node by block type — Seam 2's problem in one line |
| `BlockNetworkModSystem` fracture walk | `:242` | the same test on the split path |
| `INetworkConnector` | `ExpandedLib/Blocks/Networks/INetworkConnector.cs` | the escape hatch, 9 implementors |
| `BlockEntityProductionMachine` | `ExpandedLib/Blocks/Machines/BlockEntityProductionMachine.cs:21` | the process, as a base class |
| `.CanRunProduction` / `.AutoStartProduction` | `:31`, `:39` | the gate the form overrides |
| `.ConnectedNetwork<TNet>` / `.NetworkAt<TNet>` | `:168`, `:173` | network access bolted to the process base |
| `BlockEntityMultiblockStructure` | `ExpandedLib/Blocks/Structures/BlockEntityMultiblockStructure.cs:23` | the form, extending the process |
| `BlockStructureFiller` | `ExpandedLib/Blocks/Structures/BlockStructureFiller.cs` | a plain `Block`; hosts behaviours but is not a node |
| `BEBehaviorMPFillerPort` | `ExpandedLib/Blocks/Structures/BEBehaviorMPFillerPort.cs` | membership done right already, for vanilla MP only |

Vanilla, as the worked example.

| Type / member | file:line | Role |
|---|---|---|
| `IMechanicalPowerBlock` | `vssurvivalmod/Systems/MechanicalPower/Network/IMechanicalPowerBlock.cs` | the entire block-side contract: 3 methods |
| `BEBehaviorMPBase` | `.../BlockEntityBehavior/BEBehaviorMPBase.cs:55` | all network state and the walk, in a behaviour |
| `.spreadTo` | `:514-540` | resolves the neighbour by `GetBehavior<BEBehaviorMPBase>()`, then asks the block through the interface |
| `BlockMultiblock` + `IMultiBlock*` | `vsessentialsmod/Block/BlockMultiblock.cs` | three documented levels of multiblock modularity; every hook carries a `Vec3i offset` |
| `BlockMPMultiblockGear` / `BEMPMultiblock` | `.../MechanicalPower/Block/`, `.../BlockEntity/BEMultiblock.cs` | vanilla's own invisible filler and its principal back-pointer |
