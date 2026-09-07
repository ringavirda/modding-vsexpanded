# docs/internal/research - dated read-only snapshots of the code

Each file is what one research agent found on one day, kept so the next session reads a map instead of
re-deriving it. They are **records**, not decisions: numbers, mechanics and rulings live on the design
pages; status lives in `../plans/STATE.md`. Line citations drift - cite by symbol when reusing.

| File | Covers |
|---|---|
| [2026-09-04-blanks-tooling-consumers.md](2026-09-04-blanks-tooling-consumers.md) | sand patterns and cast parts, drawn machined-item shapes, roll sets, gears and shafts, tooling items, cast pipe, recipe plumbing; proposed job-table roster |
| [2026-09-04-blast-furnace-stations.md](2026-09-04-blast-furnace-stations.md) | cold blast furnace after U4.4-U4.9, tall hopper, burdenmaker, twin-tub blower, molten canals and pig beds |
| [2026-09-04-boiler-engine-pipes.md](2026-09-04-boiler-engine-pipes.md) | Cornish boiler megablock, Watt engine + sub-machines (B20/B21), plated and cast pipes, fittings, condenser, manual pump, intake |
| [2026-09-04-casting-cells-detail.md](2026-09-04-casting-cells-detail.md) | sand casting cell and long cell in depth: pour face, misrun at shake-out, long-cell intake blocked by its own filler, art vs footprint |
| [2026-09-04-casting-rack-workbench.md](2026-09-04-casting-rack-workbench.md) | cupola, casting cells and long cell, casting bed, storage rack, workbench, design table and diagrams |
| [2026-09-04-coke-oven-crucible.md](2026-09-04-coke-oven-crucible.md) | beehive coke oven and the crucible furnace / crucible steel end to end |
| [2026-09-04-design-table-diagrams-detail.md](2026-09-04-design-table-diagrams-detail.md) | design table and the 27 diagram items in depth (sub-agent pass) |
| [2026-09-04-cupola-detail.md](2026-09-04-cupola-detail.md) | cupola furnace in depth (sub-agent pass) |
| [2026-09-04-rack-workbench-detail.md](2026-09-04-rack-workbench-detail.md) | storage rack and workbench in depth (sub-agent pass) |
| [2026-09-04-footprints-shapes-defs.md](2026-09-04-footprints-shapes-defs.md) | how megablock defs are authored, machines.txt transcribed to filler cells, the nine mpenergy editables measured, export tooling, goldens, lang guards; a def sketch for the machine-tool family |
| [2026-09-04-mill-flatwide-cells.md](2026-09-04-mill-flatwide-cells.md) | rolling mill footprint vs machines.txt, gap selection, roll-set spec, the flatwide set, the movable-roller ruling, minimal change set for the i1 cells |
| [2026-09-04-mill-shear-benches.md](2026-09-04-mill-shear-benches.md) | mpenergy drive (flywheel, shafts, transmission), rolling mill, crop shear, nail cutter and riveter, stock items between stations |
| [2026-09-04-puddling-helve-reheat.md](2026-09-04-puddling-helve-reheat.md) | puddling furnace whole heat, helve shingling route and its work-item class defect, reheat furnace soak |
| [2026-09-04-station-base-and-jobs.md](2026-09-04-station-base-and-jobs.md) | BlockEntityMachineStation, shear, fastener bench, ProcessJob registry, MachineTool, ItemDie, hold-to-operate precedents, station tests |
| [2026-09-04-station-window-and-hold.md](2026-09-04-station-window-and-hold.md) | design-table and workbench dialogs, station base dialog support, vanilla GUI bases, hold-to-operate hooks, filler interaction routing, lang plumbing; a design for the shared window |
| [2026-09-04-u11-ladle-triage.md](2026-09-04-u11-ladle-triage.md) | the U11 ladle unit verified task by task against today's source; the art measured; layouts verbatim; executable task list |
| [2026-09-06-ease-audit.md](2026-09-06-ease-audit.md) | exlib convenience plan Task V7: first-snippet line counts and rungs for every wiki capability page, ranked proposed doc fixes; three cross-cutting ease facts against source |
| [2026-09-06-asset-loading-spike.md](2026-09-06-asset-loading-spike.md) | Task T13 spike: the working in-process call sequence to load a mod's real assets through the game's own AssetManager/patch loader/object loader, walls hit and worked around (ReadOnlySpan tag registries, protobuf-net, vanilla class registration), the `TestWorld.LoadAssets` design landed from it |
| [2026-09-06-exlib-repo-split.md](2026-09-06-exlib-repo-split.md) | whether exlib should move to its own repository: what a split buys and costs, the measurements, the subtree-mirror middle ground, and the two things that would have to move first |
| [2026-09-07-memory-split.md](2026-09-07-memory-split.md) | the assistant memory store split by subject for the exlib/exmods/workspace split: destination per note, the prune, the overrides decided against the code |

Written 2026-09-04 for the roadmap's Phase 1 walkthrough and Phase 2 machining-line plan.

Paths in these snapshots predate the 2026-09-05 per-mod layout: `src/<Project>/` -> `mods/<mod>/src/`,
`test/<Project>.Tests/` -> `mods/<mod>/tests/`, `assets/<domain>/` -> `mods/<mod>/assets/<domain>/`,
`assets/editable/` -> `workbench/`, `docs/wiki/` -> `mods/exlib/wiki/`, `dist/CakeBuild/` ->
`infra/CakeBuild/`, `scripts/tools/` -> `infra/tools/` (mod = exlib/iiex/siex). The snapshots keep
their old citations by rule; read them through this mapping rather than re-deriving a new snapshot.
