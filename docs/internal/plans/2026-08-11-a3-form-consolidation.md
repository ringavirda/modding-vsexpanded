# A3 — form consolidation

The block-entity base slot belongs to **form**. A1 made membership a behaviour, but nothing adopted
it: `BlockEntityNetworkNode` still squats in the slot for machines whose form is not "a node". A3
evicts it where it does not belong, which is precisely what lets a machine be a **container** — and
that is the station window's precondition. A3 and the station-window promotion are the same work.

## Verified before planning (2026-08-11, against `6745420`)

- `MpEnergyNetwork.cs:67` resolves consumers with `be is IMpEnergyConsumer`, never by base class, so
  evicting the base does not drop a machine off its network.
- The only two casts to the base (`BlockNetworkNode.cs:156`, `:401`) are both **orientation**, and
  both degrade gracefully — the first skips a store transfer, the second falls back to
  `ComputeValidOrientations`.
- The mill's orientation is a **block variant** (`BlockRollingMill.cs:103`,
  `Variant?["orientation"]`), never the block entity's `PossibleOrientations`. The mill is unaffected.
- The mill ships a grid recipe (`FormingRecipeDefinitions.cs:27` → `forming-rollingmill-we`), so its
  two stack keys are in published saves and need a **load-time migration**, not a clean break.
- ⛔ The filler's `PortFace`/`PortNetworkType` arm is a **port**, which A1 deliberately kept when it
  narrowed `INetworkConnector`. It is *not* redundant with hosted behaviours. Do not remove it.
  Its one production consumer is `BlockBoiler.cs:138-139`.

## Tasks

### 1 — the mill off the node base

`BlockEntityRollingMill` stops deriving from `BlockEntityNetworkNode` and derives from
`BlockEntityContainer`, hosting `BEBehaviorNetworkMember` (type `mpenergy`) and keeping its existing
`BEBehaviorProductionMachine`. Its `_piece` and `_rollSet` become inventory slots, which retires the
hand-rolled `ToTreeAttributes`/`FromTreeAttributes` stack pair **and** the ~40 lines of
`OnStore`/`OnLoadCollectibleMappings` that a container gets for free.

Migration: on load, `rmPiece`/`rmRollSet` present in the tree are moved into the inventory and not
written back. One-way and self-healing; `BlockMigrationModSystem` is not involved (it maps block
codes, and the mill's code does not change).

⛔ The mill reads `NetworkSystem?.GetNetworkAt(Pos)` in three places. That accessor comes from the
base today; it must come from the hosted membership after.

### 2 — the station window, in exlib

Promote the design table's window into a reusable exlib pairing: a container-backed machine station
with declared slot groups, an optional picker, an info panel and an action button. Re-express
`GuiDialogDesignTable` on it — the one existing consumer proves the generalisation — and give the
mill its own window (roll-set tooling slot, input, output, gap/strip selection, feed action).

This is the shape the four machine tools need, and the reason A3 comes before them.

### 3 — record the decisions

- `MachineJob` and `ItemDie` live in **exlib**, not iwex: both are mechanics other mods consume.
  Closes `machining-line.md` open question 6.
- Vanilla's multiblock model (`vsessentialsmod/Block/BlockMultiblock.cs`): **keep ours**, and say why
  in `framework-composition.md`. Vanilla needs a blocktype per offset
  (`multiblock-monolithic-{dx}-{dy}-{dz}`) and puts **no block entity** on a filler cell; ours puts
  the offset in the filler's block entity, which is the only reason A1 could host a graph node there.
  Adopting their vocabulary would retract A1's main win. Worth borrowing: their recursion guards.

## Global constraints

- **No commits.** Work stays in the tree; the record goes to `docs/internal/worklog/2026-08.md`.
- `.compat/` and `assets/editable/` are untouched.
- Three targets green: 1.22/net10.0, 1.21/net8.0, 1.20/net7.0. `-p:Legacy=true` on net8.0/net7.0,
  always an explicit `-f <tfm>`.
- `CommentStyleGuards`: doc comments ≤16 lines, ≤3 `<para>`.
- UK spelling in docs and comments, US in identifiers.
- Test-count floors are **gone** (`scripts/test-floors.txt` removed 2026-08-11); the zero-test guard
  in `exmod.ps1` stays. Do not reintroduce a floors file.
- A change can pass all 15 targets and still be wrong — ask what a player would see.
