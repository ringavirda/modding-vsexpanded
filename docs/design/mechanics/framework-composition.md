# Framework Composition — form, process, membership
**Status** settled 2026-08-10. All three seams are built. Seam 2 is live in the code it describes;
Seam 1 is live for every host — the process is a behaviour, readiness is a published contract, and
form no longer inherits process. Seam 3 freed the block-entity base slot: membership no longer squats
in it, `BlockEntityMachineStation` carries the container and window plumbing in exlib, and the rolling
mill is its proof (2026-08-11). The megablock footprint-intactness arm remains a decision record, and
the mill is a station with no window yet.
**Mod** exlib (every seam) · every mod (every consumer)
**Owns** the three axes a machine is composed from, the two seams between them, and the rule that
decides whether a capability is a base class or a behaviour. Supersedes the implicit hierarchy in
which `BlockEntityMultiblockStructure` extended `BlockEntityProductionMachine`.
**Depends on** [multiblock](multiblock.md) (the form vocabulary and the filler mechanism, whose
"a filler can never be a graph node" limitation this page retired) · [pipe-network](pipe-network.md) ·
[mp-energy](mp-energy.md) · [conventions](../conventions.md) (the block-size vocabulary, the network
families) · [the vanilla source map](../../internal/vanilla/README.md)

---

## Role

A machine in these mods is three independent things at once: a **shape in the world**, a **process
that runs over time**, and a **member of one or more networks**. C# gives a class one base, so at most
one of the three can be inheritance: process and membership are behaviours a block entity carries,
and form keeps the slot.

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

A multiblock knows whether its pattern is complete. A process needs a gate. That was an inheritance
edge — `BlockEntityMultiblockStructure` extended `BlockEntityProductionMachine` — and it is now a
published contract. The form implements `IProductionReadiness`, answering `IsReadyToProduce` from its
own `CanRunProduction` and `StopsProductionWhenNotReady` from `StopsProductionOnStructureLost`; the
process reads every publisher on the machine through `ProductionReadiness` and names none of them. The
monitor tick drives the transitions through `ProductionProcess.Start`/`.Stop`, which reach whatever
process the machine carries and do nothing when it carries none.

The coupling is real and survives; only its expression changed. Form publishes a readiness signal,
process reads it. No process needs to know which kind of form answered.

**Taking the process on is a separate choice from being a multiblock.** The tick lives in
`BEBehaviorProductionMachine`, and a multiblock that also produces derives from
`BlockEntityMultiblockMachine`, which hosts one. A multiblock that only has to be built derives from
the form alone and carries no tick, which the type system made impossible before: the abstract
`OnProductionTick` reached every multiblock through the spent base slot.

**Teardown ordering is the host's choice, and a hosted capability may not assume one.** A block
entity drops every listener it holds *before* either teardown call fans out to its behaviours, so a
host that calls `base` first does the rest of its own teardown with no listeners left, and one that
calls `base` last still has them. Both orderings ship: the multiblock form tears down after `base`,
the furnace core before it, because its fire must be out while the tick still exists. A behaviour
therefore does only order-independent work on those paths — the process forgets its tick handle,
which is idempotent and has no side effect — and a machine with work to finish first does it before
calling `base`.

**A host does not have to be a machine base class.** Anything deriving from `BlockEntity` can add a
`BEBehaviorProductionMachine` in its constructor and publish `IProductionReadiness` from whatever it
already knows, which is what frees the base slot for the axis that needs it.
`BlockEntityRollingMill` is the shipped case: it spends its base on being a graph node, states its
pass interval and its gate on a nested process, and gets the bounded `dt` it previously did without.
It is the shape the four machine tools need, each of which wants a timed job *and* a window
inventory ([machining line](machining-line.md)) — `BlockEntityContainer` takes the base, the process
is a behaviour. One workaround from the old edge is still in the tree: `BlockEntitySmokeStack`
hand-registers its own graph node because its base went to the multiblock.

Readiness is not one signal, and this is what the seam must actually carry:

| Kind | Answered by | Classes |
|---|---|---|
| pattern completion | `StructureComplete` | 10 |
| construction completion | `IsConstructed`, off the RCC stage count | transmission, engine |
| peer presence | `Engine != null` | engine sub-machine |
| work in hand | `IsRolling` | rolling mill |

Only the first is form. Peer presence is topology and work in hand is the machine's own state, so a
form-only provider retires neither. Readiness also has two
**levels**, not one: `CanRunProduction` asks *may I run this tick*, while
`StopsProductionOnStructureLost` (`:131`) asks *should I still be ticking at all*. A machine that
answers only the first is frozen with its state held rather than stopped, which is exactly what the
breached furnace relies on (`BlockEntityFurnaceCore.cs:1043`).

⛔ Two arms of the target have no implementation to move. Nothing anywhere computes whether a
megablock's footprint is intact, and no shipped machine answers "always ready" — `CanRunProduction`
is abstract and all five overrides gate on something. Those arms are new code, not a refactor.

### Seam 2 — a cell carries membership, not a block class

Membership belongs to the **cell**, not to a kind of block. The walk asks a resolver what participates
at a position and never names a block class:

```csharp
INetworkMember? source = NetworkMembership.Resolve(world, pos, networkType);
if (source == null || !CouplesFrom(world, pos, source))
  yield break;                       // BlockNetworkModSystem.cs:482, in GetConnectedNeighbors
```

`GetConnectedNeighbors` is the only walk there is. `RemoveNode`'s fracture BFS
(`BlockNetworkModSystem.cs:239`) and `RebuildFromRoot` (`:330`) both step through it rather than testing anything themselves, so there is a
single place a node is resolved and it names no class.

`Resolve` answers in two arms: a `BEBehaviorNetworkMember` on the cell's block entity whose network
type matches, else the block itself through `INetworkConnector` (`NetworkMembership.cs:44`). Both
produce the same `INetworkMember`, and the neighbour side resolves through exactly the same call
(`BlockNetworkModSystem.cs:543`), so a block carrying a membership is walked **to** as well as **from**. Resolving only the
block on the far side would leave the graph one-directional.

The block arm is there because a block **with no block entity in a loaded chunk** is a real state:
`BlockConverterIntake` is an `INetworkConnector` that is deliberately not a node and has no block
entity, and a node whose block entity was torn down while its chunk stayed resident goes on answering.
⛔ A chunk unload is not that case — it takes the block too, and the graph handles it separately (see
below).

Why the resolver rather than a base class. C# gives a class one base, so as long as a node *was* a
`BlockNetworkNode`, a block that spent its base elsewhere could only reach a network through
`INetworkConnector` — a connection target, never a member — and no megastructure could be a node at
all. A filler cell felt the same rule from the other side: it is a plain `Block`, yet it could already
join the **vanilla** mechanical network, because vanilla resolves its nodes by behaviour. One cell, two
networks, one reachable and one not, and the only difference was how the walk found a node. A cell —
principal or filler — now carries one membership behaviour per network it joins, and
[multiblock](multiblock.md) § "A filler cell is a graph node when it declares one" is where a footprint
cell declares its.

### Membership is addressed by network type, never by CLR type

`BlockEntity.Behaviors` is a plain `List<BlockEntityBehavior>`, so several membership behaviours can
coexist on one block entity. `GetBehavior<T>()` returns the **first** match
(`vsapi/Common/Collectible/Block/BlockEntity.cs:52-58`), which is why exlib must supply its own
accessor keyed on network type and face rather than reuse vanilla's.

This is the one place the framework deliberately goes past vanilla, which never needed it: a vanilla
block entity carries at most one `BEBehaviorMPBase`.

### A per-cell answer must be a plain public class member

`INetworkMember` is answered by two very different kinds of implementor, so two separate C# dispatch
rules decide whether an override is reached at all. Both fail the same way: the wrong shape compiles,
and the interface's default answers instead of the code that was written.

**A block declares the per-cell pair as ordinary public members.** `INetworkConnector` supplies
`INetworkMember.HasConnectorAt` and `INetworkMember.IsConnectionBroken` as *explicit* default
implementations, which is the only shape that compiles: redeclaring them non-explicitly hides the
base member rather than implementing it (`CS0108`, then `CS0535` on the eight implementors that
answer only `HasConnectorAt(face)`). Interface mapping searches the class hierarchy before falling
through to a default, so `BlockStructureFiller`'s plain public `NetworkTypeAt`/`HasConnectorAt` and
`BlockCastIronBevel`'s `override` of `BlockNetworkNode`'s virtual both outrank it. An *explicit*
`bool INetworkMember.HasConnectorAt(...)` on one of those classes would not, because nothing can
override an explicit implementation, and the per-cell answer would become unreachable.

**A behaviour subclass must override a base virtual.** Interface mapping is fixed at the class that
lists the interface. `BEBehaviorNetworkMember` lists `INetworkMember`, so a subclass declaring a
matching public member without re-listing the interface never enters the map, and the base's answer
keeps winning. That is why `BEBehaviorNetworkMember` declares every member a subclass may answer
differently — `NetworkType`, `NetworkTypeAt`, `HasConnectorAt`, `IsConnectionBroken`,
`IsNetworkEndPoint`, `AcceptsNeighbour` — as a `public virtual`, instead of inheriting any of them as
a default. `NetworkType` included: a hosted membership does not own its answer, and a copy of it
drifts (see below).

`NetworkMembershipTests` guards both shapes against production classes, and asks each through an
`INetworkMember`-typed reference — the only way to exercise the map rather than the class member:
the block side against `BlockStructureFiller` and a `BlockNetworkNode` subclass, the behaviour side
against the membership `BlockEntityPipe` hosts.

### Both arms of the resolver ask the same question

`NetworkMembership.Resolve` finds a membership behaviour first and the block second, and both arms
compare `NetworkTypeAt(world, pos)`, never the declared `NetworkType`. Keying the two on different
properties would make a membership whose type varies by cell, which is exactly what a filler cell is,
unfindable at a position where the equivalent block is found. `MemberOf(be, networkType)` remains the
position-less accessor for callers holding only a block entity, and matches the declared type.

### A membership may state its own connector faces

`BEBehaviorNetworkMember.Connectors` holds the faces the membership couples on. Empty — the default —
means the block answers, which is what every shipped network node relies on: a membership added to a
node block inherits its orientation without restating it. A non-empty set outranks the block, because
the two cases that need one have a block that cannot answer for the cell at all. A plain `Block` is no
`INetworkConnector`, so a membership on one would report no connector on any face and bridge nothing.
A filler cell's block is the single shared `exlib:structurefiller` singleton — always north, no
variants — so its port face comes from the footprint declaration instead.

Faces are stated either as an orientation string of single-letter side codes (`"ns"`, `"we"`,
`"nsewud"`, mapped through `BlockNetworkModSystem.SideToFace`) or as `BlockFacing`s directly, which is
what a filler hands over: its declared face arrives already rotated into the placed orientation. A
JSON `connectors` key reaches the same property through `Initialize`.

A set already configured in code therefore wins over a JSON declaration, and the disagreement is
logged — the same precedence and the same noise as a losing `networkType` declaration. The reason is
the rotation: a configured face names a face the cell actually exposes, while a declaration is written
in the unrotated frame, so a declaration that won would move the port.

### A hosted membership registers, and the engine fixes when

`BlockEntityNetworkNode` keeps its whole surface and hosts a private nested `BEBehaviorNetworkMember`
that forwards to it; only graph registration moved. A nested type reaches its enclosing type's private
and protected members, so no member changed visibility and no concrete block entity was touched.

Persistence deliberately did **not** move. Vanilla fans behaviour persistence out over the block
entity's **own flat tree**, with no subtree (`vsapi/Common/Collectible/Block/BlockEntity.cs:320-323`,
`:339-342`), so if a block entity and its behaviour both wrote network state they would write the same
keys and the second writer would win. Exactly one thing may write them, and it stays the block entity —
which is also what keeps `BlockEntityPipe`'s conditional keys, and its rewrite of `temp`/`medium`/
`pressure` *after* `base.ToTreeAttributes`, working unchanged. Nothing on disk changes.

Three orderings the engine dictates, each of which silently produces a wrong answer if ignored:

- **The membership is added in the constructor.** `BlockEntity.FromTreeAttributes` and `.Initialize`
  both fan out over `Behaviors`; a membership added any later misses whichever has already run.
- **It never holds a copy of its network type.** Every point at which one could be taken is wrong.
  A derived field initializer runs before the base constructor body, so the constructor sees the
  declared default rather than the loaded value; `FromTreeAttributes` reaches the behaviours *before*
  it assigns the block entity's own `NetworkType`, so the tree fan-out is too early; and
  `FromTreeAttributes` runs again on **every** `MarkDirty(…)`, so even a value taken at `Initialize`
  drifts afterwards. ⛔ `redrawOnClient` does not gate that: `BlockEntity.MarkDirty` calls
  `MarkBlockEntityDirty` unconditionally and only then, `if (redrawOnClient)`, adds the mesh redraw
  (`BlockEntity.cs:451-459`) — so the bare no-argument call resyncs the tree just the same. The
  hosted membership therefore *overrides* `NetworkType` and reads its owner's live.
  `BlockEntityFluidIntake` is the case that proves all three: its `NetworkType` is a plain
  auto-property the save tree fills in, and it calls bare `MarkDirty()` once a second.
- **It reads the state to restore before `AddNode`, not after.** `AddNode` broadcasts, the broadcast
  reaches `OnNetworkUpdate`, and that clears the saved state it is about to restore.

Two more traps around the same method. `properties` is null on the programmatic path —
`BlockEntityBehavior.Initialize` sets only `Api`, and only `CreateBehaviors` assigns `properties`
(`BlockEntity.cs:117`) — so a membership added in code must guard every read of it. And
`BlockEntityBehavior.Api` is assigned *only* by that same `Initialize`, while a block entity can be
handed an api without being initialised, so anything gated on the behaviour's own `Api` silently
never runs on that path; teardown reads `Blockentity.Api`, which is assigned before the behaviour
fan-out and is therefore set whenever the behaviour's is.

**A declared `networkType` loses to a block entity that already names one, and the disagreement is an
error.** The JSON key is for a membership with no other source — a plain block's, or a filler cell's.
A block entity's own answer is a compiled contract, and every node family but `BlockEntityFluidIntake`
implements the setter as a no-op, so a declaration that "won" would vanish on the way through and the
cell would register under the constant regardless; on the intake, which has a real setter, it would
win *and* be persisted by `ToTreeAttributes`. Both silent outcomes read to whoever wrote the JSON as
if it had taken effect, and they disagree with each other, so neither may be silent.

A membership that ends up naming no network at all is logged as an **error** and joins nothing.
Registering a blank type throws out of the network factory lookup, and this runs inside a chunk load,
so one bad declaration would take a world down; but a cell silently outside its run reads to a player
as a network that stopped working, which is not a warning.

The removal-only teardown opt-out moved with the code that owns it. Deregistering on chunk unload
would fracture a live run every time a player walked away, and the guard in `TeardownSymmetryTests`
reads a file carrying the marker as exempt — so a marker left behind on a class that no longer tears
anything down would quietly exempt whatever lands in that file next.

Keeping the node is only half of it. ⛔ An unloaded chunk hides the **block** as well as the block
entity, so neither arm of the resolver answers there and the cell reads to the walk as empty space —
the block arm keeps a cell walkable across an absent block entity, not across an absent chunk. The
graph therefore suspends its fracture check rather than answering it whenever part of a network is
unreadable; see [pipe network](pipe-network.md) § 1, "An unloaded chunk suspends the fracture check".

### Seam 3 — the block-entity base slot belongs to form, and nothing else may squat in it

Membership became a behaviour in Seam 2, but `BlockEntityNetworkNode` still held the base slot of every
machine that happened to be on a network. That is not form: a rolling mill is not *a kind of network
node*, it is a machine that belongs to one. While the slot was spent that way, no such machine could be
a **container**, and a container is what a machine with a window is.

So a machine now takes the base its form actually calls for and hosts the rest. The mill derives from
`BlockEntityMachineStation` — exlib's container-with-a-window-and-a-handshake — and carries its
membership and its pass clock as behaviours. `BlockEntityNetworkNode` survives for the seventeen blocks
that genuinely are only nodes; it is a convenience for them, never a requirement.

**The slot pays for itself twice.** The mill's two `ItemStack` fields became inventory slots, which
retired both its hand-written stack persistence and its ~40 lines of collectible id mapping — the
container base carries both, and that mapping is the one whose absence corrupts a schematic paste.

⛔ **A shipped machine's loose stacks need a load-time migration, not a clean break.** The mill's keys
`rmPiece`/`rmRollSet` sat at the tree root; a container writes under `inventory` instead. Without the
one-way adopt in `FromTreeAttributes`, every saved mill comes back with its roll set gone and any piece
mid-pass destroyed — and the world looks fine, because nothing errors.

**Against vanilla's model, deliberately: ours stays.** `vsessentialsmod/Block/BlockMultiblock.cs`
offers three levels of multiblock modularity and an `IMultiBlock*` interface family whose every hook
carries a `Vec3i offset`. It needs **a blocktype per offset** (`multiblock-monolithic-{dx}-{dy}-{dz}`,
generated over a range) and puts **no block entity on a filler cell at all**. Ours puts the offset in
the filler's block entity — which is the only reason Seam 2 could host a graph node there, and the only
reason a footprint cell can carry a behaviour on its principal's behalf. Adopting their vocabulary
would retract that. Worth borrowing, and not yet borrowed: their `is BlockMultiblock` recursion guards,
which we have already paid for once in a port-forwarder stack overflow.

### What stays a base class

Little. Once membership, process and structure-completion are behaviours, a shared block-entity root
holds only what every block entity needs and none can be trusted to repeat by hand: teardown that
runs identically whether the block was removed or its chunk unloaded, disposal of renderers, dialogs
and looping sounds, and declared fields that round-trip through the attribute tree.

That last one is a class of bug, not an inconvenience. A field written only by a server tick, read
in `GetBlockInfo`, and absent from `ToTreeAttributes` reads zero on the client for ever — and pays for
a chunk re-tesselation to synchronise a value it never sends if it also calls `MarkDirty(true)`.
`BlockEntityPressureValve._lastVentVolume` was the worked example of both halves; it has since been
fixed, which is why the declared-state base (`ExBlockState`) exists rather than the discipline.

---

## Consequences

Retired by this design:

- [multiblock](multiblock.md)'s "a filler can never be a graph node" rule, now § "A filler cell is a
  graph node when it declares one", and with it the rolling mill's dedicated `BlockRollingMillAxle`
  cells, which exist only because the two drive cells could not be fillers and nodes at once. The
  block itself stays until a migration can retire it: it is placed in existing worlds.
- `INetworkConnector` as a parallel mechanism **for membership**. A block that is *on* a network has
  a membership behaviour, and there is no second way. The interface itself survives, narrowed to
  what it was named for: a **port** — a face another network may couple to, on a block that is not
  itself a graph member. The lancashire boiler's water intake stays a port; it is a connection
  target, not a node. It is also how a *block* answers `INetworkMember`, which is what keeps a cell
  resolvable when it has no block entity.
- The peripheral-block pattern as a *requirement*. Splitting a machine's connections across cells
  stays available because it is often the right shape physically — an intake belongs where the pipe
  arrives — but it is no longer the only way to be on more than one network.

Unchanged: every network's own physics and state model. This page moves where membership is
recorded and how the graph is walked. It does not touch what flows.

---

## Code

Membership — the built axis.

| Type / member | file:line | Role |
|---|---|---|
| `INetworkMember` | `ExpandedLib/Networks/INetworkMember.cs:21` | the whole source-side contract, five members; what the walk holds |
| `INetworkConnector` | `ExpandedLib/Networks/INetworkConnector.cs:18` | a port, and the block-side answer to `INetworkMember`; 10 implementors, 9 of them not nodes |
| `NetworkMembership.Resolve` | `ExpandedLib/Blocks/Networks/NetworkMembership.cs:44` | membership behaviour first, block second; the only place a node is resolved |
| `.MembersOf` / `.MemberOf` | `:17`, `:27` | every membership on a block entity, and the one declaring a network type |
| `.CouplesAt` | `:68` | network-agnostic: does this cell expose a connector on this face, from whichever side answers |
| `BEBehaviorNetworkMember` | `ExpandedLib/Blocks/Networks/BEBehaviorNetworkMember.cs:22` | one membership; registers its cell and drops it on removal |
| `.Connectors` / `.DeclareConnectors` | `:53`, `:60` | the faces it couples on, empty to leave the answer to the block |
| `.ConfigureFromFiller` | `:107` | takes a footprint cell's face, already rotated into the placed orientation |
| `.Initialize` / `.OnBlockRemoved` | `:191`, `:246` | the graph join, and the removal-only teardown |
| `BlockEntityNetworkNode.HostMembership` | `ExpandedLib/Blocks/Networks/BlockEntityNetworkNode.cs:38` | private nested membership forwarding to its owner; no persistence moved |
| `BlockNetworkModSystem.GetConnectedNeighbors` | `ExpandedLib/Blocks/Networks/BlockNetworkModSystem.cs:477` | the one walk; every other traversal steps through it |
| `.IsValidNetworkNeighbour` | `:535` | the far side, resolved by the same call as the near side |
| `.GetOpenConnectorFaces` | `:449` | open ends; deliberately does not gate its source (see [pipe network](pipe-network.md) § 1) |
| `.ReviewConnectivity` | `:206` | fracture, settle, or suspend when part of the run is unreadable |
| `BlockNetworkNode` | `ExpandedLib/Blocks/Networks/BlockNetworkNode.cs:19` | abstract `Block`; 17 subclasses; a convenience now, not the contract |
| `BEBehaviorMPFillerPort` | `ExpandedLib/Blocks/Structures/BEBehaviorMPFillerPort.cs` | the same shape for vanilla MP, and the precedent this followed |

Form — the freed base slot.

| Type / member | file | Role |
|---|---|---|
| `BlockEntityMachineStation` | `ExpandedLib/Blocks/Machines/BlockEntityMachineStation.cs` | container + window + packet handshake; the base a machine with slots takes |
| `.OnReceivedClientPacket` | same | **sealed**, so no override can drop the claim check by forgetting `base` |
| `.OnStationPacket` | same | where a machine adds its own actions, from `FirstMachinePacketId` |
| `.CreateDialog` | same | null leaves the machine windowless, which the mill is today |
| `MachineSlotSpec` / `MachineStationInventory` | `ExpandedLib/Blocks/Machines/MachineStationSlots.cs` | slots as data; built through `InventoryGeneric`'s own slot-factory delegate |
| `BlockEntityRollingMill` | `IronworkingExpanded/.../BlockEntityRollingMill.cs` | the proof: container base, membership + process as behaviours |
| `.MigrateLooseStacks` | same | the one-way adopt of a pre-A3 save's two root-level stacks |
| `BlockEntityDesignTable` | `IronworkingExpanded/.../BlockEntityDesignTable.cs` | the promotion's first consumer; keeps only its slot rules and its draft |

Form and process.

| Type / member | file:line | Role |
|---|---|---|
| `BEBehaviorProductionMachine` | `ExpandedLib/Blocks/Machines/BEBehaviorProductionMachine.cs:15` | the process: the tick, its gate, teardown and away-catch-up |
| `IProductionReadiness` / `ProductionReadiness` | `ExpandedLib/Blocks/Machines/IProductionReadiness.cs:17`, `ProductionReadiness.cs:14` | one publisher's answer, and the two aggregates over every publisher on a machine |
| `ProductionProcess` | `ExpandedLib/Blocks/Machines/ProductionProcess.cs:14` | starts and stops a machine's process without naming the class that carries it |
| `BlockEntityProductionMachine` | `ExpandedLib/Blocks/Machines/BlockEntityProductionMachine.cs:20` | hosts a process for a machine that is not a multiblock |
| `MachinePorts` | `ExpandedLib/Blocks/Machines/MachinePorts.cs:14` | network access as extensions on any `BlockEntity`, so it is not a machine's to inherit |
| `BlockEntityMultiblockStructure` | `ExpandedLib/Blocks/Structures/BlockEntityMultiblockStructure.cs:28` | the form: the pattern, its monitor tick, and the readiness it publishes |
| `BlockEntityMultiblockMachine` | `ExpandedLib/Blocks/Structures/BlockEntityMultiblockMachine.cs:20` | the form plus a hosted process; what the five concrete multiblocks derive from |
| `BlockStructureFiller` | `ExpandedLib/Blocks/Structures/BlockStructureFiller.cs:22` | a plain `Block`; hosts behaviours, and a cell of it is a node when it declares one |

Vanilla, as the worked example.

| Type / member | file:line | Role |
|---|---|---|
| `IMechanicalPowerBlock` | `vssurvivalmod/Systems/MechanicalPower/Network/IMechanicalPowerBlock.cs` | the entire block-side contract: 3 methods |
| `BEBehaviorMPBase` | `.../BlockEntityBehavior/BEBehaviorMPBase.cs:55` | all network state and the walk, in a behaviour |
| `.spreadTo` | `:514-540` | resolves the neighbour by `GetBehavior<BEBehaviorMPBase>()`, then asks the block through the interface |
| `BlockMultiblock` + `IMultiBlock*` | `vsessentialsmod/Block/BlockMultiblock.cs` | three documented levels of multiblock modularity; every hook carries a `Vec3i offset` |
| `BlockMPMultiblockGear` / `BEMPMultiblock` | `.../MechanicalPower/Block/`, `.../BlockEntity/BEMultiblock.cs` | vanilla's own invisible filler and its principal back-pointer |
