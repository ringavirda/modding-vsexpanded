# Pipe Network

**Status** live   **Mod** `exlib` owns the graph substrate and the base pipe blocks - `BlockPipe`/`BlockEntityPipe`, `BlockNetworkNode`, `BlockNetworkModSystem` and `ChimneyVent` live in `src/ExpandedLib/Blocks/Networks` (since 2026-08-07), `PipeNetwork`/`PipeNetworkState` in `src/ExpandedLib/Networks`; `iiex` owns the plated tier and the network registration; `iiex` owns the cast tier + every fitting; `hpex` owns the rolled tier (segments only).

**Owns**
- The shared block-network graph substrate used by every network in the suite: node add/remove, merge, BFS fracture detection, `RebuildFromRoot`, per-second tick dispatch, the `dt` catch-up clamp, open-connector (leak) detection, the connector-reciprocity rule, `AcceptsNeighbour`, and `IsConnectionBroken` re-walk. Other network pages cite this one for those facts.
- The pipe pool model: one medium per run, capacity `nodes × LitresPerPipe`, gas pressure as a volume ratio, liquid pressure as fill-ratio-then-pump-pressure.
- Uniform network temperature (no spatial gradient) and where phase change is allowed to happen.
- Burst pressure per material tier and the joint-family rule, including which blocks are exempt from both.
- Leak, chimney-vent, evaporation, passive-cooling, throughput-EMA and over-pressure-burst behaviour, and every constant behind them.
- The plain valve (in-line sever) and the pressure valve (directional overflow), including the gate constants.

**Depends on**
- [molten network](molten-network.md) - the other consumer of the same graph substrate.
- `ExpandedLib.Fluids.ExLiquids` / `assets/*/config/liquids.json` - the medium catalogue: which codes exist, their phase, their boil/condense points and volume factors. This page states only how the pipe network consults the catalogue, never what is in it.
- The iiex steam machines that produce into and consume from a run, and their own rates -
  [Cornish boiler](../machines/boiler-cornish.md) · [Watt engine](../machines/engine-watt.md) ·
  [pumps & fluid intake](../machines/pumps.md).

---

## Role

One network type carries air, steam, exhaust and water between machines:

- A blower or boiler pushes into a run; a furnace tuyere, engine or cowper draws from it.
- The run's pressure gates progression: a blast furnace demands a pressure derived from its burden, and the pipe tier the player can build caps what any blower can deliver. Pipe strength is the tier gate.
- Leaks, bursts and closed valves are the failure surface, all local and all visible.

A run is one homogeneous pool, not a fluid simulation: no flow direction inside a run, no per-cell pressure, no temperature gradient. Everything spatial happens at the edges of a run - where it is produced into, drawn from, vented, split by a valve, or refused by an incompatible joint.

---

## How it works

### 1. The shared graph substrate

Every network in the suite - pipe, molten, mpenergy - is a `BlockNetwork` subclass (`BlockNetwork.cs:15`) managed by one `BlockNetworkModSystem` (`BlockNetworkModSystem.cs:16`). The manager does graph work only; all typed state lives in the subclass.

| operation | file:line | behaviour |
|---|---|---|
| register a type | `BlockNetworkModSystem.cs:35-38` | `RegisterNetworkType(name, factory)` in `ModSystem.Start`. iiex registers all three (`IronIndustryExpandedModSystem.cs:94-108`) |
| add a node | `BlockNetworkModSystem.cs:108` | isolated → new network; otherwise joins `adjacentNetworks[0]` and merges the rest into it (`:136-157`), each merge gated by `CanMerge` |
| remove a node | `BlockNetworkModSystem.cs:173` | removes, then hands the rest to `ReviewConnectivity` (`:206`) |
| review connectivity | `BlockNetworkModSystem.cs:206` | walks from any node (`:239`); all reached → `Settle`, same instance kept (`:307`); some unreached and every node readable → `Fracture`, each component rebuilt as its own network with `OnSplitFragment` (`:263`); some unreached and any node behind an unloaded chunk → **suspended**, network left whole (`:219-225`) |
| rebuild | `BlockNetworkModSystem.cs:330` | `RebuildFromRoot` BFS-discovers everything reachable, tears down overlapping networks, and preserves the old root network's state via `InheritStateFrom` (`:381`) |
| tick | `BlockNetworkModSystem.cs:46-52, 403` | one server listener at 1000 ms, `dt` clamped to 2 s; resumes suspended reviews (`:420`) then dispatches `OnTick` to every live network |
| broadcast | `BlockNetwork.cs:55-64` | pushes the typed state payload to every `INetworkNode` BE in the run |

Connectivity is reciprocal and four-way gated. `IsValidNetworkNeighbour` (`BlockNetworkModSystem.cs:535`) is the single chokepoint that both the traversal and the leak scan go through. Source and neighbour are resolved identically, by `NetworkMembership.Resolve` (`NetworkMembership.cs:44`) - a membership behaviour on the cell's block entity, else the block itself - so neither side names a block type and a block that spent its base class elsewhere still walks. A neighbour connects only when all of:

1. `Resolve` finds a membership there for the source's `NetworkTypeAt(world, sourcePos)` (`BlockNetworkModSystem.cs:543-547`),
2. it exposes a connector on the touching face - `HasConnectorAt(world, pos, facing.Opposite)` (`:550`),
3. `source.AcceptsNeighbour(neighbourBlock)` is true (`:557`) - the physical-joint test,
4. it is neither an `IsNetworkEndPoint` nor severed - `CouplesFrom` (`:510-514`).

A face that passes 1-4 nowhere is an **open end** (`GetOpenConnectorFaces`, `:449-469`), which the pipe tick turns into a leak or a vent.

The **source** cell is gated differently by the two consumers, deliberately. The traversal asks `CouplesFrom` of the source as well (`:483`), so an endpoint or a severed cell yields no graph neighbours at all. The open-end scan does not: whether a face is open is a physical fact rather than a graph one, and a closed valve, a solidified canal and a pressure valve all still meet the pipe they touch. Gating the scan on the source too would cap a closed tap's inlet a second time over its own end-cap mesh (`BlockEntityMoltenCanalTap.cs:684-696`) and report every endpoint's coupled face as a leak.

Connectors read the adjacent cell, not their own. Two position-aware members carry that: `NetworkTypeAt(world, pos)` on `INetworkMember` (`INetworkMember.cs:26`) and `HasConnectorAt(world, pos, face)`, which `INetworkConnector` supplies for a block as an explicit default (`INetworkConnector.cs:29`). A node block answers both from its `orientation` variant (`BlockNetworkNode.cs:732, 743`); a per-cell connector - a megablock structure filler exposing a port on exactly one footprint cell - declares them as plain public members that outrank the default, reads the block entity at `pos`, and stays inert everywhere else. So a machine need not be a network node to be plumbed in: a structure block implementing `INetworkConnector` is a valid connection target but is never added to the graph (`INetworkConnector.cs:7-17`).

The machine side of the same rule is `MachinePorts` (`MachinePorts.cs:15`): `be.ConnectedNetwork<PipeNetwork>(face)` resolves the network in the cell across the connector face, and returns `null` unless the cell over there presents a connector back - asked of whatever speaks for it, a membership or the block (`BlockNetworkModSystem.cs:74-87`). A pipe merely sitting adjacent with its connectors pointing elsewhere is not plumbed in. Every fixed machine (boiler, engine, pumps, intake, converter, cowper, condenser) uses this one helper.

State survives unload: `BlockEntityNetworkNode` serialises the last broadcast state (`BlockEntityNetworkNode.cs:56, 70, 110`) and the cell's membership injects it back into the freshly built network on `Initialize`, capturing it before `AddNode` can null it (`BEBehaviorNetworkMember.cs:220-231`). ⛔ The block entity stays the only writer: vanilla fans behaviour persistence over that same flat tree, so a membership that persisted anything would collide with the keys already there. Nothing about the format moved when membership did (`SaveFormatTests.cs`).

#### An unloaded chunk suspends the fracture check

⛔ **The walk cannot tell "there is nothing here" from "I cannot see here."** `IBlockAccessor.GetBlock` answers the air block for an unloaded chunk rather than null (`vsapi/Common/API/IBlockAccessor.cs:256-262, :272`), and `GetBlockEntity` answers null, so an unloaded cell resolves to no member at all - exactly like an empty one. Nothing about the resolver changes that: the block arm keeps a node walkable across a block entity dropped on its own, not across a chunk that went away.

The graph outlives the unload - nothing calls `RemoveNode` there (`BEBehaviorNetworkMember.cs:249`, `BlockEntitySmokeStack.cs:47`) - so the node set stays right while the cells behind it are invisible. What breaks is the **fracture check**, which reads unreachable as gone. Left alone it splits a run around a player who walked away, and the run stays split: the returning cell finds its position already in a network and never re-joins (`BEBehaviorNetworkMember.cs:230`).

So the check is **suspended rather than answered** when it comes up short and any node of the network is unreadable (`BlockNetworkModSystem.cs:214-225`). The network is left whole and the positions the walk could not read are recorded against it; `ServerTick` re-decides it once at least one of them is back (`:420-441`). Three things follow from the shape:

1. **Positions are recorded, not chunk coordinates.** Asking `GetChunkAtBlockPos(pos)` is dimension-aware by contract; deriving a chunk key by hand means reproducing the engine's convention, and vanilla's mechanical-power version of this gets it wrong - `spreadTo` divides raw `.Y` rather than `.InternalY` (`vssurvivalmod/…/BEBehaviorMPBase.cs:526`). Several missing chunks are simply several positions, and each review recomputes the whole set, so a partial return shrinks it.
2. **The re-decision runs on the tick, not on `Event.ChunkDirty`.** A chunk event fires while the chunk's block entities may not be back, and a footprint cell answers *only* through its block entity, so a review in that window would read the chunk as loaded and the cell as absent and split a healthy run. The tick cannot land inside a load - both are main-thread - and it needs no reason filter and no module-level "everything is loaded" flag, the pair that lets vanilla's re-trigger go stale (`MechanicalPowerMod.cs:328`). It is idempotent: a review is a recomputation from the current world, so running it again on an unchanged world changes nothing.
3. **A network entirely behind unloaded chunks is covered by the same rule**, not a special case - no node is readable, so nothing is decided. The cost when nothing is suspended is one dictionary count per second.

The trade-off is deliberate: while a run is suspended, a break that really did cut it keeps both halves sharing one pool until the chunk returns. That is strictly better than the alternative it replaces, which shredded the unloaded half into one network per cell **permanently**.

### 2. One medium per run

`PipeNetworkState` (`PipeNetworkState.cs:12`) is a single pool: `Volume`, `MaxVolume`, `Temperature`, `MediumType`, `Pressure`, `FeedPressure`, `OpeningsCount`, `FlowRate`.

The medium is claimed by the first producer and held until the run is empty. Both producers refuse a run carrying an incompatible medium only while it physically holds something:

```
if (State.Volume > 0f && !_taxonomy.Compatible(State.MediumType, gasType)) return false;
```
- `PipeNetwork.cs:110` (gas) and `:243` (liquid). The guard is on `Volume > 0`, not on the label: a drained run keeps its label for a few seconds as a display ghost (see §6) and a new medium must be able to re-claim it (`:106-109`, `:239-242`).

Compatibility comes from the injected `IMediumTaxonomy` (`IMediumTaxonomy.cs:9`), defaulting to `ExLiquids.Taxonomy`: an empty run accepts anything; two gases always mix; two liquids mix only if identical; gas and liquid never mix (`ExLiquids.cs:106-116`). When gases mix, the dominant label is the higher `Priority` (`ExLiquids.cs:118-119`). Which codes exist, and their priorities, boil points and volume factors, is `ExLiquids`' fact, not this page's.

On merge, incompatible media cannot blend: the larger run wins outright and the smaller's contents are discarded (`PipeNetwork.cs:370-387`).

### 3. Capacity and pressure

```
MaxVolume = Nodes.Count × LitresPerPipe            (PipeNetwork.cs:112, 245, 351, 541)

gas    Pressure = Volume / MaxVolume, uncapped     (PipeNetworkState.cs:55-56)
liquid Pressure = Volume / MaxVolume  while below capacity
                = FeedPressure        once brim-full  (PipeNetworkState.cs:62-69)
```

A gas is compressible and may sit above 1 atm; a liquid cannot be packed past `MaxVolume`, so once the line is full its pressure is whatever the pump commands (`FeedPressure`, set by the producing pump, `PipeNetwork.cs:248`).

A gas producer's injection is clamped by two ceilings (`PipeNetwork.cs:118-125`):

- its own choke, `maxOutputPressure` (a blower's rating), and
- `MinBurstPressure` - the weakest burstable pipe in the whole run.

and additionally by 1 atm if the run is leaking, unless `bypassLeakCap` is set (`:122-123`). That clamp, not the leak loss, is what stops a leaking run building pressure.

### 4. Uniform temperature

`PipeNetworkState.Temperature` is one network-wide value (`PipeNetworkState.cs:20-21`, default 20 °C). Every producer blends it volume-weighted into the existing pool (`PipeNetwork.cs:130-136` gas, `:263-266` liquid), merges blend the same way (`:390-398`), and split fragments copy it (`:467`). `IPipeNode.Temperature` documents the rule explicitly - "uniform across the run - no spatial gradient" (`IPipeNode.cs:27`), echoed on the BE (`BlockEntityPipe.cs:29-31`).

The network itself never changes phase. Two passive effects cool a run - a gas leak drops it 5 °C, and passive cooling sheds `PipeGasCoolPerSecond` (2 °C/s, dt-scaled) toward `PipeAmbientTemperature` whenever the run holds gas above ambient - but steam only becomes water at an active device that supplies the cooling: the steam condenser, which draws steam from one face and injects condensate into the water line crossing its other two (`BlockEntitySteamCondenser.cs:16-21, 73-136`). The condenser consults `CondensationTarget`/volume factor through the taxonomy and falls back to iiex's own steam-expansion default when the def leaves it open (`:87-91`).

### 5. Burst pressure by tier, and joints

Two orthogonal per-tier axes, both registered by **tier** from each mod's `ModSystem.Start`. The tier is the
block's own `tier` variant (`BlockPipe.Tier`, `BlockPipe.cs:231`), the high-order segment of its code:
`pipe-{tier}-{type}-{orientation}`. It was the code's *domain* until M4 (2026-08-14), which could not survive
the merge putting plated and cast in one domain.

**Rating** - `BlockPipe._burstByTier` (`BlockPipe.cs:238`), resolved by the block's own tier (`:252-255`):

| tier | domain | key | value | registration | config |
|---|---|---|---|---|---|
| plated | `iiex` | `PlatedPipeBurstPressure` | 2.5 | `IronIndustryExpandedModSystem.cs:67` | `IiexConfig.cs:206` |
| cast | `iiex` | `CastPipeBurstPressure` | 5.0 | `IronIndustryExpandedModSystem.cs:56` | `IiexConfig.cs:50` |
| rolled | `hpex` | `RolledPipeBurstPressure` | 12 | `SteelIndustryExpandedModSystem.cs:39` | `SiexConfig.cs:115` |
| (no tier, or unregistered) | - | `DefaultBurstPressure` | 5, hard-coded | - | `BlockPipe.cs:241` |

The rating doubles as the tier's buffer size: a run holds `burst × pipes × LitresPerPipe`, so the plated tier is both the low-pressure tier and the small-buffer one.

Only a plain segment participates: `CanBurst => GetType() == typeof(BlockPipe)` (`BlockPipe.cs:262`). Every fitting is a subclass and is therefore exempt by default - it neither bursts nor caps the run's pressure. Outlet and passthrough additionally override `BurstPressure => float.MaxValue` (`BlockPipeOutlet.cs:22`, `BlockPipePassthrough.cs:26`). The outlet names no tier and would take the default anyway; the passthroughs are tiered, but for identity rather than rating - see below.

**Joint** - `BlockPipe._jointByTier` (`BlockPipe.cs:301`), two families:

| family | constant | tiers | registration |
|---|---|---|---|
| `flanged` | `BlockPipe.FlangedJoint` (`:307`) | plated, cast - both square in section, bolted through flanges - **and every untiered fitting** (outlet, fluid intake, tuyere), which is what keeps them reachable from either | `IronIndustryExpandedModSystem.cs:75`, `IronIndustryExpandedModSystem.cs:65` |
| `welded` | `BlockPipe.WeldedJoint` (`:310`) | rolled - octagonal and welded, no flange to bolt to | `SteelIndustryExpandedModSystem.cs:49` |

```csharp
public override bool AcceptsNeighbour(Block neighbour) =>
    neighbour is not BlockPipe other || other.JointFamily == JointFamily;
```
- `BlockPipe.cs:238-239`. Rolled pipe joins only rolled pipe. Anything that is not a `BlockPipe` - a machine port, a condenser, a fluid intake - is unaffected, because those are ports on a machine, not lengths of run (`:227-236`). Two consequences the source calls out explicitly:

- Because every fitting (valve, outlet, passthrough, tuyere, blower) is a `BlockPipe` subclass, a rolled run cannot reach iiex's fittings either. Until hpex ships its own, a rolled run is segments plus machine ports only (`BlockPipe.cs:231-235`).
- A refused joint reads as an open end, not a hidden wall, so the run leaks rather than silently merging. The refusal is checked in `IsValidNetworkNeighbour`, the same chokepoint the leak scan uses, so it cannot be connected from one direction and open from the other (`BlockNetworkNode.cs:749-761`, `BlockNetworkModSystem.cs:434-438`). `AcceptsNeighbour` implementations must be symmetric (`BlockNetworkNode.cs:755-761`).

**Burst mechanics.** A run that sits at or above its weakest burstable pipe's rating with nowhere to vent accumulates `_overpressureSeconds`; at `PipeOverpressureSeconds` one random qualifying pipe fails (`PipeNetwork.cs:774-809, 858-882`). Any relief that drops the pressure below the rating resets the grace (`:799-800`), and the timer is transient - a reload resets it (`:74-76`). Failure drops the pipe's items, puffs steam, pops, removes the node (fracturing the run) and sets the cell to air (`:888-916`). Burst selection prefers the world RNG so a seeded world is deterministic (`:877`).

### 6. Tick order

`PipeNetwork.OnTick` (`PipeNetwork.cs:484-524`) runs a fixed pass sequence, and the order matters:

| # | pass | file:line | effect |
|---|---|---|---|
| 0 | fold accumulators, invalidate `_minBurstCache` | `:494-497` | runs even for a null state |
| 1 | `SmoothFlow` | `:527-536` | EMA of throughput, idle timer |
| 2 | `RecomputePressureAndFlow` | `:539-562` | refresh `MaxVolume`, pressure, displayed flow |
| 3 | `ComputeLeakFractions` | `:566-578` | particle density only |
| 4 | `ClassifyOpenings` | `:585-661` | one pass over nodes: count `IPipeNode` consumers, classify each open face as vent (chimney) or leak (air), fire `OnLeak`/`OnOpenConnectorsChanged`, refresh `OpeningsCount` |
| 5 | `ApplyVentDraw` | `:665-677` | the injected `IPipeVentStrategy` pulls gas out through vents |
| 6 | `ApplyLeakLoss` | `:681-701` | leak volume loss |
| 7 | `ApplyEvaporation` | `:705-729` | calendar-based water loss |
| 8 | `RepressureAfterVentLeak` | `:732-739` | gas only |
| 9 | `ApplyPassiveCooling` | `:784` | gas only, whenever the run holds gas above ambient |
| 10 | `ClearIfEmptyAndIdle` | `:758-766` | drops `State` once drained and idle for `EmptyClearDelaySeconds` |
| 11 | `TickOverpressureAndBurst` | `:774-809` | last, so it never mutates the node set while another pass reads it |

The vent strategy is injected per network at registration, so the network core never hard-wires a policy. exlib ships `ChimneyVent` (`Blocks/Networks/ChimneyVent.cs:20`), and iiex injects it with its own draw rate when it registers the pipe network (`IronIndustryExpandedModSystem.cs:94-97`). It classifies a vanilla-or-modded chimney (matched by code substring) capping the top connector of an `IChimneyVentable` fitting as a vent, and draws `ChimneyGasDrawRate` L/s per chimney with smoke and a fire-roar loop. A network with no strategy vents nothing - every open end is a leak.

### 7. Plain valve = in-line sever

`BlockEntityValve` (`BlockEntityValve.cs:22`) is a normal pipe node while open; closed, it severs the run at its own cell:

```csharp
public override bool IsConnectionBroken() => !_open;   // BlockEntityValve.cs:33
```

`IsConnectionBroken` is consulted by the graph in two places - the traversal's early bail for the source cell (`BlockNetworkModSystem.cs:392-396`) and the neighbour test (`:446-451`) - so a closed valve is isolated from both sides. Toggling does an explicit `RemoveNode` + `AddNode` so the split or re-merge happens immediately rather than at the next rebuild (`BlockEntityValve.cs:141-149`).

Closing also discards the cached pool (`:150-154, 162-166`). Without that, the pressurised state the valve cached while open would serialise into the now-isolated single-cell network and be restored on load - into a cell whose `MaxVolume` is one pipe - bursting it. `FromTreeAttributes` drops it again before `Initialize` can capture it (`:222-225`).

The state is shown by holding the shape's `open` animation pose; the animator is rebuilt on wrench rotation, but only on a real orientation change, because a network re-walk can re-exchange to an equivalent variant and would otherwise reset the pose (`:115-129`).

### 8. Pressure valve = directional overflow

`BlockEntityPressureValve` (`BlockEntityPressureValve.cs:25`) extends the pipe BE but is not a sever: it is a one-way relief between two different runs. It ticks once a second (`:54`) and:

1. Reads the input face `orientation[0]` and output face `orientation[1]`; a wrench flip swaps `ns` ↔ `sn` to reverse the direction (`:86-89`, variants at `BlockPressureValve.cs:39`).
2. Resolves both sides through `GetConnectedNetworkAcross`, so a pipe adjacent but not facing the valve is not plumbed in (`:93-97`).
3. Runs `OverflowGas` then `OverflowLiquid` (`:99-101`).

**Gas** (`:113-214`):

```
allowed = gatePressure × inState.MaxVolume
if inState.Volume <= allowed                          -> nothing        (:123-125)
if the output run carries water                       -> nothing        (:127-129)
excess = inState.Volume - allowed
```
- Output leaking: feed it only `min(excess, GasLeakRate)` with `bypassLeakCap: true`, so the trickle flows straight through and out (`:141-158`).
- Output pressurised: gas flows downhill only - if `outPressure >= inPressure` nothing moves, otherwise it moves `min(excess, equalise)` where `equalise` is the exact amount that balances the two runs' pressures, with the output ceiling additionally capped at the input pressure as a double guard (`:159-192`). Without the downhill rule a loop would pump itself up until its pipes burst (`:159-162`).
- No output network at all: the face is an open end and vents to atmosphere at `min(excess, GasLeakRate)` with particles and a swoosh (`:195-213`).

**Liquid** (`:221-268`): once `inState.Pressure` (the pump-set feed pressure on a brim-full line) tops the gate, water moves into the output's free space, or sprays out capped at `LiquidLeakRate` when there is no output run.

The gate is player-dialled in `GatePressureStep` increments between `MinGatePressure` and the valve's own material rating (`:62-75`). `MaxGatePressure` reads `BlockPressureValve.BurstPressure` (`:41-42`), which resolves through the per-tier registry off the valve's own `tier` variant - `cast` (`BlockPressureValve.cs:39`) - so it tops out at 5 atm even though the valve itself never bursts (`CanBurst` is false for any subclass).

---

## Numbers

### exlib config - `ExlibConfig.cs`, file `ModConfig/ex_values.json`, section `exlib`

| key | value | file:line | what it does |
|---|---|---|---|
| `LitresPerPipe` | `30` | `ExlibConfig.cs:39` | litres one pipe holds at 1 atm; run capacity is this × node count. Range-guarded ≥ 1 because it divides pressure |
| `GasLeakRate` | `8.0` | `ExlibConfig.cs:43` | gas lost per leaking tick, and the cap on a pressure valve's vent-to-atmosphere |
| `LiquidLeakRate` | `10.0` | `ExlibConfig.cs:46` | water drained per second while leaking |
| `EvaporationLitresPerDay` | `50` | `ExlibConfig.cs:50` | water lost per in-game day, measured off the calendar |
| `PipeOverpressureSeconds` | `30` | `ExlibConfig.cs:54` | grace at burst pressure before a pipe lets go |

### Per-tier content config

| key | value | file:line | what it does |
|---|---|---|---|
| `PlatedPipeBurstPressure` | `2.5` | `IiexConfig.cs:206` | iiex tier rating (and buffer multiplier) |
| `PlatedPipeThroughput` | `50` | `IiexConfig.cs:232` | L/s the plated tier passes; the run's cap is the weakest segment |
| `ChimneyGasDrawRate` | `16.0` | `IiexConfig.cs:237` | L/s one chimney draws through a ventable fitting |
| `CastPipeBurstPressure` | `5.0` | `IiexConfig.cs:50` | iiex tier rating |
| `RolledPipeBurstPressure` | `12` | `SiexConfig.cs:115` | hpex tier rating |

### Hard-coded - not config

| constant | value | file:line | note |
|---|---|---|---|
| network tick interval | `1000 ms` | `BlockNetworkModSystem.cs:42-45` | makes every "per tick" rate a per-second rate |
| `dt` catch-up clamp | `2 s` | `BlockNetworkModSystem.cs:341` | stops a rejoin leaping the burst grace in one step |
| `FlowSmoothingAlpha` | `0.3` | `PipeNetwork.cs:67` | EMA weight for the displayed throughput |
| `EmptyClearDelaySeconds` | `3` | `PipeNetwork.cs:68` | how long a drained run keeps its medium label |
| flow-EMA cutoff | `0.01 L` | `PipeNetwork.cs:530-534` | below this the EMA snaps to 0 |
| `DefaultBurstPressure` | `5` | `BlockPipe.cs:175` | fallback for a domain that never registered a rating |
| gas-leak temperature drop | `5 °C`, floor `20 °C` | `PipeNetwork.cs:696-697` | per tick, not dt-scaled |
| passive cooling | `PipeGasCoolPerSecond` 2 °C/s, floor `PipeAmbientTemperature` 20 °C | `ExlibConfig.cs:71`, `:75` | dt-scaled, applies whenever the run holds gas above ambient |
| pipe throughput | plated 50 · cast 120 · rolled 250 L/s | `IiexConfig` / `IiexConfig` / `SiexConfig` | weakest segment caps the run; fittings and ports are exempt - a tuyere is the machine's intake, not a length of main |
| gas leak particle ramp | `1 → GasLeakRate`, clamp `0..4` | `PipeNetwork.cs:568-576` | density only |
| water leak particle ramp | `1 → 5 L`, clamp `0..1` | `PipeNetwork.cs:577` | density only |
| pressure broadcast epsilon | `0.02 atm` | `PipeNetwork.cs:551` | also the HUD sync threshold (`BlockEntityPipe.cs:233`) |
| flow broadcast epsilon | `0.01 L/s` | `PipeNetwork.cs:557` | |
| brim-full liquid epsilon | `0.001 L` | `PipeNetworkState.cs:68` | |
| burst-pressure comparison epsilon | `0.001 atm` | `PipeNetwork.cs:788, 871` | |
| `MinGatePressure` | `0 atm` | `BlockEntityPressureValve.cs:28` | pressure-valve floor |
| `GatePressureStep` | `0.25 atm` | `BlockEntityPressureValve.cs:31` | per interaction |
| default gate pressure | `1 atm` | `BlockEntityPressureValve.cs:35, 310` | also the value legacy saves migrate to |
| default state temperature | `20 °C` | `PipeNetworkState.cs:21` | |
| chimney fire-loop restart | `9000 ms` | `ChimneyVent.cs:25` | just under the 9.26 s clip |
| pipe ambience chance / range | `0.08` per second, 7 m, vol 0.25 | `BlockEntityPipe.cs:113-131` | gas above 1 atm bubbles, water trickles |

### Pipe geometry (code-first defs, shared by all three tiers)

Every tier calls the same `BlockPipe.Segments(domain, tier)` factory (`BlockPipe.cs:59-65`), each provider passing its own tier - iiex via `PlatedPipeDefinitions.cs:19`, iiex via `CastPipeDefinitions.cs:19`, hpex via `RolledPipeDefinitions.cs:17`. Four blocktypes per tier: straight (`:118`), bend (`:130`), tjunction (`:167`), xjunction (`:204`). Collision/selection is a 5⁄16→11⁄16 core (`:126-127`). Max stack: 16 straight, 8 for the rest. Each tier ships its own shapes at `{domain}:pipes/*` - a tier is a different model, not a tint.

A null `tier` yields the same four blocktypes with no tier axis and the default rating, throughput and joint. That is what `BlockPipe.Definitions` uses to derive `AllowedOrientations` (the map reads only `type` and `orientation`, so no tier is needed and none is invented), and what a consumer shipping one pipe family gets.

---

## Code

| type | file:line | role |
|---|---|---|
| `BlockNetworkModSystem` | `BlockNetworkModSystem.cs:16` | the graph manager. `RegisterNetworkType` (`:28`), `GetConnectedNetworkAcross` (`:67`), `AddNode` (`:101`), `RemoveNode` (`:163`), `RebuildFromRoot` (`:259`), `GetOpenConnectorFaces` (`:352`), `GetConnectedNeighbors` (`:380`), `IsValidNetworkNeighbour` (`:417`) |
| `BlockNetwork` | `BlockNetwork.cs:15` | abstract base: `Nodes`, `State`, `OnTick`, `OnMerge`, `OnSplitFragment`, `InheritStateFrom` (`:110`), `OnTopologyChanged` (`:117`) |
| `BlockNetworkNode` | `BlockNetworkNode.cs:20` | abstract block base: orientation/placement/wrench, `AcceptsNeighbour` (`:762`), `HasConnectorAt` (`:768, 777`), `IsNetworkEndPoint` (`:732`), `IsValidNonNetworkConnection` (`:739`) |
| `BlockEntityNetworkNode` | `BlockEntityNetworkNode.cs` | node lifecycle + state persistence; `IsConnectionBroken` (`:130`), `OnNetworkUpdate` (`:117`) |
| `INetworkConnector` | `INetworkConnector.cs:15` | the extension point for a machine port. Implement it to be a valid pipe target without joining the graph |
| `IPipeNode` | `IPipeNode.cs:11` | the extension point for a producer/consumer. Implement it to inject/withdraw without inheriting `BlockEntityPipe` |
| `IBurstablePipe` | `IBurstablePipe.cs:9` | opt into the burst model: `CanBurst` + `BurstPressure` |
| `IPipeVentStrategy` | `IPipeVentStrategy.cs` | the injected vent policy; `ChimneyVent.cs:21` is iiex's implementation |
| `IMediumTaxonomy` | `IMediumTaxonomy.cs:9` | the injected medium policy; `ExLiquids.Taxonomy` is the default |
| `PipeNetwork` | `PipeNetwork.cs:18` | the pool. `TryProduceGas` (`:96`), `ProduceGasMeasured` (`:175`), `TryConsumeGas` (`:200`), `TryProduceLiquid` (`:231`), `ProduceLiquidMeasured` (`:284`), `TryConsumeLiquid` (`:300`), `OnTick` (`:484`), `MinBurstPressure` (`:838`) |
| `PipeNetworkState` | `PipeNetworkState.cs:12` | the state object + the two pressure formulas (`:55, 62`) |
| `MachinePorts` | `MachinePorts.cs:15` | `be.ConnectedNetwork<TNet>(face)` / `be.NetworkAt<TNet>(pos)` - the one place the "port = the cell across the face" rule lives |
| `BlockPipe` | `BlockPipe.cs:24` | segments + the burst registry (`:173-197`) and the joint registry (`:206-239`) |
| `BlockEntityPipe` | `BlockEntityPipe.cs:21` | `IPipeNode` delegating to the network; leak particles (`:156-194`); HUD (`:327`) |
| `BlockEntityValve` / `BlockValve` | `BlockEntityValve.cs:22` / `BlockValve.cs:19` | in-line sever |
| `BlockEntityPressureValve` / `BlockPressureValve` | `BlockEntityPressureValve.cs:25` / `BlockPressureValve.cs:20` | directional overflow |

**Blocks on the pipe network today**

| block | mod | class | notes |
|---|---|---|---|
| pipe straight/bend/T/X | iiex, iiex, hpex | `BlockPipe` | the only burstable blocks |
| valve, pressure valve | iiex | `BlockValve`, `BlockPressureValve` | |
| outlet | iiex | `BlockPipeOutlet` | `IChimneyVentable`; `BurstPressure = MaxValue`; names no tier |
| passthrough, passthrough-bend | iiex (plated) **and** iiex (cast) | `BlockPipePassthrough` | `IChimneyVentable`; `BurstPressure = MaxValue`; **tiered** - see below |
| fluid intake | iiex | `BlockFluidIntake` (`:14`) | BE is a bare `BlockEntityNetworkNode`, not an `IPipeNode` |
| steam condenser | iiex | `BlockSteamCondenser` (`:22`) | `INetworkConnector` only |
| boiler, engine, engine fluid pump, manual fluid pump | iiex | `BlockBoiler:46`, `BlockEngine:48`, `BlockEngineFluidPump:37`, `BlockManualFluidPump:31` | machine ports |
| tuyere, twin-tub MP blower | iiex | `BlockTuyere:14`, `BlockTwinTubMPBlower:28` | `BlockPipe` subclasses - non-bursting, and flanged, so they refuse a rolled run |
| converter intake, cowper intake, engine air blower, smokestack | smex | `BlockConverterIntake:41`, `BlockCowperStoveIntake:134`, `BlockEngineAirBlower:39`, `BlockEntitySmokeStack:54` | machine ports |

---

## Gotchas

1. Never gate a tick pass on `pass.Consumers == 0`. `ClassifyOpenings` increments `Consumers` for every `IPipeNode` block entity, and `BlockEntityPipe` implements `IPipeNode`, so "no consumers" is unsatisfiable on any run containing a segment. Passive cooling shipped behind exactly that guard and was dead code in game; the guard is gone and the source warns against re-adding it (`PipeNetwork.cs:765`). The testing trap that hid it is still live: `PipeTestWorld.Run` places blocks with no block entities, so `Consumers` is 0 in a headless test - any test of tick-time node classification must use `PipeTestWorld.LiveRun`, which attaches real BEs; a test written against the bare fixture passes whether the feature works or not.

2. Gas leak loss is not dt-scaled; liquid leak loss is. `ApplyLeakLoss` uses `LiquidLeakRate * dt` for water but a bare `GasLeakRate` for gas (`PipeNetwork.cs:687` vs `:694`). With the `dt` clamp at 2 s, a catch-up tick leaks twice as much water but the same gas.

3. `GasLeakRate`'s doc comment does not match `ApplyLeakLoss`. `ExlibConfig.cs:34-35` says "only the volume above the network's 1 atm capacity is vented, so a leaking run can never build pressure". The loss pass subtracts `GasLeakRate` from `Volume` unconditionally (`PipeNetwork.cs:694`). The excess-only calculation exists only in `ComputeLeakFractions` (`:568-571`), which sizes particles. The "can never build pressure" outcome is real but comes from the 1-atm production clamp (`:122-123`), not the leak. Stale comment.

4. Leak loss is per-network, not per-opening. One open end and twenty leak the same volume (`PipeNetwork.cs:683-698`); `TotalLeaks` only gates whether it leaks and how dense the particles are. The comment at `:679-680` states this is deliberate ("bulk venting needs a chimney/stack").

5. `"gas"` is a dead network type. `INetworkConnector.cs:17`, `BlockNetwork.cs:21` and `BlockNetworkModSystem.cs:25` all give `"gas"` as an example network type. Only `"pipe"`, `"molten"` and `"mpenergy"` are ever registered (`IronIndustryExpandedModSystem.cs:88-103`). Stale comments.

6. A `RemoveNode` that does not fracture keeps the same network instance (`BlockNetworkModSystem.cs:305-315`). Anything cached on the instance survives. `PipeNetwork` handles this by overriding `OnTopologyChanged` to drop `_minBurstCache` (`:830-833`); `MoltenNetwork` does not override it at all - see [molten network](molten-network.md) Gotcha 5.

7. `MinBurstPressure` is `float.MaxValue` when the run holds no burstable pipes (`PipeNetwork.cs:843-847`). A run of only machine ports and passthroughs therefore has no production ceiling from the pipe side (`:118-121` clamps against `MaxValue`) and can never burst (`:787`).

8. `PoolVolumeCeiling` bypasses the cache. Merge/split call `ComputeMinBurstPressure` directly (`PipeNetwork.cs:341`), walking every node, while the hot path uses `MinBurstPressure` (`:838`). Correct, but an O(N) walk in the merge path.

9. A closed valve must clear its saved pool, and it must do so twice. Once on close (`BlockEntityValve.cs:150-154`) and again in `FromTreeAttributes` before `Initialize` runs (`:222-225`). Miss either and a reload restores a pressurised pool into a one-cell network and bursts it.

10. The pressure valve is not a valve. It never severs and it never bursts; it is a scheduled once-per-second transfer between two adjacent runs. It appears in the graph as a normal pipe cell, and its two networks must be genuinely separate for it to do anything - one in the middle of a single run does nothing.

11. The pressure valve's ceiling comes from a tier it is not made of. `MaxGatePressure` reads `BurstPressure` off the block (`BlockEntityPressureValve.cs:41-42`), which resolves from `_burstByTier[Tier]`. The valve declares `tier: cast`, so the answer is 5 - but it is exempt from bursting, so this is a rating borrowed from that tier's plain pipe, not the valve's own strength. Sharper since M4: the valve's craft recipe consumes a **plated** segment (B19 leaves the cast tier uncraftable), so a valve is built from one tier and rated at another. The `tier` variant states which of the two governs; the recipe is the thing that is wrong, and it is wrong for a reason tracked elsewhere.

12. Chimney vents are matched by code substring. `neighbour.Code?.Path.Contains("chimney")` (`ChimneyVent.cs:50`). Any block whose path contains "chimney", from any mod, becomes a gas sink on the top connector of a ventable fitting.

13. `bypassLeakCap` is easy to misuse. It lifts the 1-atm clamp on a leaking run (`PipeNetwork.cs:116-117, 122-123`). It is only correct for a caller that has already hand-limited its volume to the leak rate; anything else pressurises a leaking line.

14. Incompatible merges destroy content silently. Joining a water run to a gas run keeps only the larger pool and discards the smaller (`PipeNetwork.cs:370-378`). No warning, no drop, no particle.

15. `OnTopologyChanged` runs on the primary network only during a merge (`BlockNetworkModSystem.cs:159`); the merged-away networks are simply dissolved (`:156`).

16. Node registration happens in `BlockEntityNetworkNode.Initialize`, not `OnBlockPlaced`. Calling `AddNode` from `OnBlockPlaced` would trigger a redundant O(N) broadcast and freeze the server on large networks (`BlockNetworkNode.cs:183-186`).

17. ⛔ **A node added beside an unloaded chunk never merges with what is over there.** `AddNode` resolves neighbours through the same blind walk, so the new cell forms a network of its own - and the neighbour, already in the graph from before its chunk left, skips `AddNode` on its return (`BEBehaviorNetworkMember.cs:230`), so nothing ever joins the two. Separate from the suspended fracture check, which only defers a *split*; suspension records the network's own nodes, and this cell is not one of them. A player interaction cannot reach it (the chunks around a player are loaded), but a tick-driven `RemoveNode` + `AddNode` at the edge of the loaded area can: `BlockEntityMoltenCanal.ResyncNetworkNode` (`:160-168`) and `BlockEntityValve.cs:138-139`. The fix, if it is ever worth one, is to make `AddNode` merge for a position it already holds and call it unconditionally on load, rather than to widen suspension.

18. `RebuildFromRoot` drops the nodes it cannot see. It tears down every overlapping network including their unreadable nodes and rebuilds only what the walk reached (`BlockNetworkModSystem.cs:368-374`), so cells behind an unloaded chunk leave the graph entirely and rejoin by `AddNode` when their chunk returns. Self-healing, and its one caller is a player-driven local action (`BlockEntityMoltenCanal.ClearSolidified`), but the run's state is redistributed in the meantime.

---

## Throughput, bore and stored heat - settled 2026-08-05

### 1. A run gates its own throughput, and the gate is a weakest-link walk

The run carries a per-run flow cap, derived from the tier as a weakest-link walk over
`IThroughputLimitedPipe.MaxThroughput` (`PipeNetwork.cs:887-895`) - the structural mirror of
`MinBurstPressure`, so the invalidation hook at `OnTopologyChanged` already exists. Without it there is no
rate cap in the pool at all: production is bounded by headroom and consumption by availability only, and the
implicit ceiling is ~75 L/s through a single plated pipe, above every line in the mod. Precedent in the same
substrate: `MoltenFlowRate` = 50 u/s already gates the molten network per transfer.

The machine-side rates stand. The twelve shipped per-second constants (tuyere 14 L/s, cowper 24,
smokestack 48, chimney 16 …) gate what a machine draws; the run's cap gates what the plumbing can carry.

### 2. Bore is not built - and if it ever is, it is orthogonal to tier

No large-bore pipe family. Three reasons, any one sufficient:

* Already ruled against. [fluid-tank.md](../machines/fluid-tank.md) - "there is no such thing as a node
  with its own capacity" - costed at 15 re-derivation sites and rejected as the highest-risk route. A
  large-bore segment is a capacity-bearing node.
* Physically backwards. Hoop stress is `σ = pr/t`, so at the same wall a wider bore bursts at lower
  pressure. Bore and burst are not independent, and `PoolVolumeCeiling` multiplies capacity by the burst
  rating, so a fatter pipe would silently buy more over-pressure headroom.
* Cost against value: a full mirror is +20 blocktypes, +394 variants, +20 goldens, +18 shapes, while the
  existing cast and rolled tiers still have no recipe.

The numbers to size it do not exist yet. [gas-system.md](gas-system.md) § 9 still lists the 48 L/s
exhaust retune, burner consumption, grade calorific values and holder capacity as underived, so a bore set
sized against the 48 L/s figure would be sized against an acknowledged placeholder. Build the gate, measure,
then decide.

> If bore is ever added, it must be orthogonal to tier, never a fourth rung on the pressure ladder.
> The largest pipework in a works carried the lowest pressure: blast, gas and exhaust run 24-160 L/s at
> 1.25-2.75 atm, while steam runs 30-32 L/s at up to 12 atm. Friction loss goes as `Δp ∝ Q²/D⁵`, so halving
> a bore multiplies the drop ×32, which is why a service with only a few psi of head must buy area instead.
> A large-bore plated blast main and a small-bore rolled steam pipe are both correct; folding bore into the
> pressure ladder produces "the strongest pipe is also the fattest", which is false.

### 3. Stored gas cools - including gas parked in a pipe run

Passive cooling fires on any run holding gas above ambient (Gotcha 1), so a main loses heat exactly as a
holder does and a long run cannot be a gasholder that stores gas hot. The price
[gas-system.md](gas-system.md) sets for buffering ("stored gas comes out cold - smoothness or temperature,
never both") therefore holds universally, and the holder is not strictly worse than pipe at its own job. It
also gives run length its first consequence: a long main costs flame temperature.

## Open

- No pressure drop along a run. Length is free for pressure: a one-block run and a 300-block run
  behave identically apart from capacity. Length is not free for temperature - see ruling 3 above. Whether
  pressure should also fall with distance remains undecided.
- hpex has segments but no fittings. The welded joint means a rolled run cannot use iiex's valve / pressure valve / outlet / passthrough (`BlockPipe.cs:231-235`). Until hpex ships its own, a rolled run is straight pipe and machine ports.
- Gas leak loss is not dt-scaled (Gotcha 2) and the two leak paths should probably agree.
- Burst selection is uniform-random among qualifying pipes (`PipeNetwork.cs:875-879`). There is no notion of the pipe nearest the producer, or of fatigue.
- Only one pipe fails per burst event (`:878`), then the grace resets - so a run held over-pressure loses one pipe every `PipeOverpressureSeconds`, indefinitely.
- Phase change is condenser-only. The taxonomy already exposes passive `TryCondensation`/`TryVaporisation` (`IMediumTaxonomy.cs:47, 61`), but nothing in `PipeNetwork.OnTick` calls them - a steam line below 100 °C does not condense on its own.
- A suspended review suspends the *whole* network, not the part it cannot see. A component of readable nodes with no cell adjacent to an unreadable one is provably isolated and could be split off at once, leaving only the fog and what touches it waiting. Not built, because a partial split needs the surviving network to be *debited* by whatever the fragment takes: `OnSplitFragment` hands a fragment its proportional share on the assumption the original is dissolved, so splitting one component off a surviving network would create material from nothing in all three subclasses. That is a new state-partition contract on `BlockNetwork`, not a graph change. Until then, a break in a loaded area is deferred whenever any part of the same run is away.
