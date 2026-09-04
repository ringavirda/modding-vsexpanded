# Framework composition — staging

**Status: DONE 2026-08-12.** All five stages landed; the design page
([framework-composition](../../design/mechanics/framework-composition.md)) is the record of what they
built. One task closed late — see the note under A1.

Sequencing for the design page; that page carries no sequencing by convention.

Agreed 2026-08-10. Order is driven by what unblocks what, except A0, which is independent.

| | Stage | Unblocks | Risk | Landed | Task detail |
|---|---|---|---|---|---|
| **A0** | Verified lifecycle bug fixes | nothing — ship independently | low | **2026-08-10** | [a0](2026-08-10-a0-lifecycle-fixes.md) |
| **A1** | Membership → behaviour; walk by behaviour; multi-membership accessor | A2, A3 | high | **2026-08-11** | [a1](2026-08-10-a1-network-membership-behaviour.md) |
| **A2** | Process → behaviour; readiness seam replaces the inheritance edge | A3 | medium | **2026-08-11** | none written |
| **A3** | Form consolidation — filler cells first-class, megablock and multiblock compose | — | medium | **2026-08-11** | [a3](2026-08-11-a3-form-consolidation.md) |
| **A4** | Shared block-entity root: teardown, disposal, declared synced fields, analyzer | — | low | **2026-08-12** | none written |

A4 is last on purpose. Built first, it would harden a hierarchy A1–A3 dismantle. Its *fixes* do not
depend on it, which is why they split out as A0.

## What each stage actually shipped

- **A0** — the pressure valve's vent readout, megastructure fillers cleared on every removal path,
  teardown symmetry across eight block entities plus its guard, the design table's dialog, and
  collectible mappings on eight block entities plus its guard.
- **A1** — `BEBehaviorNetworkMember`, `NetworkMembership.Resolve` as the single node resolver, filler
  cells as graph nodes, and the unloaded-chunk fracture suspension.
- **A2** — `BEBehaviorProductionMachine`, `IProductionReadiness`/`ProductionReadiness`,
  `ProductionProcess`; form no longer inherits process.
- **A3** — `BlockEntityMachineStation` (container + window + handshake) in exlib; the rolling mill and
  the design table converted; the mill's loose-stack load-time migration.
- **A4** — `ExBlockState` and `ExBlockEntity`: declared persistence, composable so a block entity that
  has spent its base slot can still use it.

## ⛔ A1 Task 7 was not finished with A1

Task 7 had three parts. Two are settled:

- **Narrow `INetworkConnector` to a port** — done; the interface doc now says a connector is a
  connection target and never a graph member.
- **Migrate persisted state** — *correctly not done*. During implementation the decision reversed:
  persistence stays on the block entity, because vanilla fans behaviour persistence over the block
  entity's own flat tree and two writers would collide. The design page records this as "Nothing on
  disk changes", so `NetworkMembershipMigration` was never needed.

The third was outstanding for two days:

- **`PossibleOrientations` off `System.Text.Json` onto the tree's own string array** — landed
  **2026-08-12**, found independently through the § B backlog and investigated from scratch, because
  nothing recorded that an A1 task was still open. It is the reason this file now carries per-stage
  status and the reason `docs/internal/` is tracked.

## Deferred out of the arc, deliberately

- **Megablock footprint intactness** and an "always ready" readiness provider: both are arms of Seam 1
  with *no implementation to move*. New code, not a refactor, and no shipped machine wants either yet.
- **`BlockRollingMillAxle`** — retired by the design, still placed in existing worlds. Needs its own
  content migration.
- **`BlockEntitySmokeStack`** still hand-registers its graph node, a leftover from when its base slot
  went to the multiblock. Harmless; tidy it when the file is open anyway.

## A0 — the fixes, ready now

All verified against the tree on 2026-08-10; each cites where the bug is live.

| Fix | Where | Symptom |
|---|---|---|
| `_lastVentVolume` never written to the tree, and `MarkDirty(true)` paid for anyway | `LowPressureExpanded/BlockNetworkPipe/BlockEntities/BlockEntityPressureValve.cs:33`, `:99-101`, `:282-285` | overflow readout permanently absent on the client; a re-tesselation per change for nothing |
| Footprint fillers cleared only in `OnBlockBroken` | `ExpandedLib/Blocks/Structures/BlockFilledMegastructure.cs:70-79` | an explosion or any `SetBlock` leaves invisible solid orphan cells. `Block.OnBlockExploded` goes straight to `BulkBlockAccessor.SetBlock(0, pos)` and never calls `OnBlockBroken` (`vsapi/.../Block.cs:2536`), so the cleanup belongs on `Block.OnBlockRemoved`, as vanilla's `BlockLargeGear3m` does |
| Build-outline highlights cleared on remove but not on unload | `ExpandedLib/Blocks/Structures/BlockEntityMultiblockStructure.cs:570` (no `OnBlockUnloaded`) | highlights stay drawn for ever when the player walks away. Vanilla's `BEBeeHiveKiln` clears in both |
| Eight block entities override `OnBlockRemoved` without `OnBlockUnloaded` | see list below | leaked listeners, undisposed renderers, stale highlights |
| Design-table dialog never closed or disposed | `IronworkingExpanded/BlockStructures/Crafting/BlockEntities/BlockEntityDesignTable.cs` | GUI survives the block; its `OnClosed` then packets a dead position |
| Eight block entities store raw `ItemStack`s with no collectible mappings | ten store stacks; `BlockEntityMoltenBarrel` and `BlockEntityCastMold` already implement the mappings, the other eight do not | `ItemStack.ToBytes` writes the runtime id, so a schematic paste resolves the wrong item. Copy the two that are already correct |

The eight asymmetric block entities:

```
ExpandedLib/Blocks/Networks/BlockEntityNetworkNode.cs
ExpandedLib/Blocks/Structures/BlockEntityMultiblockStructure.cs
IronworkingExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityHopperTall.cs
IronworkingExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs
LowPressureExpanded/BlockNetworkPipe/BlockEntities/BlockEntityFluidIntake.cs
LowPressureExpanded/BlockNetworkPipe/BlockEntities/BlockEntityPressureValve.cs
SteelmakingExpanded/BlockStructures/HotBlastFurnace/BlockEntities/BlockEntityHopperBell.cs
SteelmakingExpanded/BlockStructures/SmokeStack/BlockEntities/BlockEntitySmokeStack.cs
```

Two of them are exlib base classes, so fixing those two covers everything beneath them.

## A1 — the open questions

To settle during A1's own design, not now:

- **Persistence.** Network state is saved per node. Moving membership to a behaviour changes where
  it is written (`BlockEntityBehavior.ToTreeAttributes` is called through the base's fan-out). A
  migration is required for three published mods; `BlockMigrationModSystem` is the existing seam.
- **Discovery order.** `BEBehaviorMPFillerPort` already shows the trap: a behaviour created in
  `Initialize` misses the base's tree-routing loop, and the filler replays `_savedTree` to work
  around it (`BlockEntityStructureFiller.cs:58-64`, `:130-136`). The membership behaviour will meet
  the same ordering and should solve it once, generally.
- **The unloaded chunk.** The current walk treats an unloaded chunk as the end of the network, which
  phantom-fractures a run. Vanilla returns `missingChunkPos` and re-discovers on `Event_ChunkDirty`
  (`BEBehaviorMPBase.cs:522-527`, `MechanicalPowerMod.cs:325-345`). A1 should adopt that rather than
  port the bug.
- **Scope of the accessor.** Keyed by network type alone, or by type and face? The rolling mill
  wants two `mpenergy` couplings on one machine; whether those are two behaviours on two cells or
  two faces on one is a form question that A3 may answer differently.

## A3 — decide against vanilla's model

`vsessentialsmod/Block/BlockMultiblock.cs` documents three levels of multiblock modularity —
monolithic simple, monolithic configurable, modular configurable — and an `IMultiBlock*` interface
family whose every hook carries a `Vec3i offset`. Our filler system converged on the same shape
independently. A3 should decide deliberately whether to adopt that vocabulary or keep ours, rather
than continue to diverge by default.
