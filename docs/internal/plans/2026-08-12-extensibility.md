# Extensibility — third-party mods extend our processes

**Status** live, next. Written 2026-08-12 after extensibility became a stated product target.

**Goal:** a modder can add tooling to diagram crafting, sand casting, rolling, the steam hammer and
the machining line from their own mod, without our source and without a fork.

**Why now, and not after the tier spine:** the attribute schemas a modder writes against are a
**public contract**. The breaking changes have to land before anyone builds on them. `RollSetSpec`'s
`Outputs` key is the worked example - it needs to become `(form, gap)`, which is a breaking change
today and a broken third-party roll set the day after someone ships one.

---

## The idiom already exists

Three processes already work this way, and it was deliberate:

| Process | Where the spec lives | Evidence it is open |
|---|---|---|
| rolling | the roll-set item's `rollset` attribute | "the mill reads what to do off the fitted set and **never names a product in code**" |
| sand casting | the pattern item's mold attribute (`MoldSpec`) | `PatternItemDefinitions` builds "a mod's whole `pattern` itemtype from its own mold table" |
| diagram crafting | every `diagram-*` item in the world | the picker scans by code, **domain deliberately not matched** |

**The rule, named:** *tooling carries its own spec; the machine reads it and names no product.*

Three instances and no statement of the rule is why it is a habit rather than a contract. Nothing
enforces it, nothing documents it, and the two unbuilt processes have made no such commitment.

`MoldSpec` and `RollSetSpec` already differ on the one axis that matters -
[machining line](../../design/mechanics/machining-line.md) names it: **terminal versus sequence**. A
mold is terminal (one impression, one product); a roll set is a sequence (a ladder walked one gap at
a time). Any general contract has to carry both.

---

## Rulings (2026-08-12)

| # | Ruling |
|---|---|
| **E1** | **Both routes.** JSON attributes stay the primary, dependency-free path; exlib also exposes a **public C# registration API** for mods that want to compute a spec at load or take a hard dependency. |
| **E2** | **The die carries its job spec**, same idiom as roll sets and mold patterns. `MachineJob` hangs off `ItemDie`; a modder adds a die exactly the way they add a roll set. Confirms the settled `MachineJob`-in-exlib design. |
| **E3** | **Every spec attribute is versioned.** Each carries a `schema` number and its parser reads every shipped form, following the `possibleOrientations` migration shape: read the current form first, fall back to the older one, rewrite on next save. |
| **E4** | **This layer lands before the tier spine.** B3c, the shear and the machining line get built on a settled contract, not the other way round. |

⛔ E3 is the expensive one and it was chosen with that understood: every spec grows a migration path
we maintain. The alternative - freeze at release, additive-only - was cheaper for us and worse for
the people who asked for this, because it makes their content break on our schedule.

---

## Global Constraints

- **The JSON attribute schema is the API.** Our code-first `ExItemDef`/`ExBlockDef` builders are a
  private convenience: a third party cannot add a C# def to our assembly. Golden parity tests protect
  our authoring, **not** the contract - so the contract needs its own guards.
- **Multi-version.** exlib ships against 1.20/1.21/1.22. Anything the public API touches must exist in
  all three, or sit behind `#if GAME_GE_1_22`. Check with a byte scan of `.game/<ver>/VintagestoryAPI.dll`
  before using an engine member.
- **A machine may not name a product.** That is the rule this whole plan exists to make real.
- Neutral repo voice; comments describe, never narrate. UK spelling in prose, US in identifiers.
- `scripts/exmod.ps1 format` and `scripts/exmod.ps1 test all` (15 targets) before any task is done.

---

## Tasks

### Task 1 — Name the rule

**Files:** create `docs/design/mechanics/process-extension.md`; link it from
[framework-composition](../../design/mechanics/framework-composition.md) and
[machining-line](../../design/mechanics/machining-line.md).

The design page states the rule, the terminal-vs-sequence axis, the versioning contract (E3), and
both routes (E1). It is a **decisions** page, so it owns the schema shape; this plan owns only the
order things get built in.

Until this exists the rule is three instances and a habit.

### Task 2 — `RollSetSpec.Outputs` becomes `(form, gap)`

**Files:** `RollSetSpec.cs`, `RollSetItemDefinitions.cs`, `BlockEntityRollingMill.ClaimFinishedPiece`,
the shipped golden, `RollSetSpecTests`, `ShippedRollSetTests`.

The one genuine whole-piece conversion in the design is unwritable on the current key: flat 1.0
yields `nailplate` from a `rolledrod` but plate from a bloom - same gap, different product, decided by
the form that entered. `ClaimFinishedPiece` is built, tested and waiting on this.

Breaking, and deliberately first: nobody depends on it yet.

### Task 3 — Schema versioning across the three shipped specs

**Files:** `RollSetSpec`, `MoldSpec`, and the diagram scan; `ExTree`-style read-both helpers.

Each spec attribute gains `schema`. Each parser reads the current form, falls back to the previous,
and the item rewrites on next save. Model it on the `possibleOrientations` migration, whose lesson is
recorded: **the absent/null distinction is the migration** - a reader that cannot tell "never written"
from "written empty" cannot fall back.

Pin each with a test that loads an old-form spec and asserts it still parses.

### Task 4 — The public C# registration API (E1)

**Files:** new `src/ExpandedLib/Processes/` - the registry and its per-process entry points.

A mod calls exlib to register a roll set, a mold, a diagram or a machine job programmatically. Shape
it on the existing `ExKeyedRegistry<T>` rather than a fourth hand-rolled keyed dictionary.

⛔ This is the surface we owe stability on. Keep it as small as the JSON route and no smaller: anything
expressible only in C# is a gap in the JSON schema, and the schema should grow instead.

### Task 5 — The guard

**Files:** `test/ExpandedLib.Tests/Invariants/ProcessExtensionGuards.cs`.

A source scan in the shape of `ShapeLoadingGuards`: no machine block entity names a product code in
code. Whole-file matching, and assert the corpus is non-empty - a guard that scans nothing passes.

Mutation-check it red before trusting it.

### Task 6 — Document it where a modder will look

**Files:** `docs/wiki/Extending-Processes.md`, plus `_Sidebar.md` and `Home.md`.

The wiki has 17 pages on our **frameworks** and not one on any process extension point. A capability
nobody can find is, for the people who asked for this, the same as one that does not exist.

One worked example per process, each a complete copy-pasteable item def.

### Task 7 — `MachineJob` and `ItemDie` (E2)

**Files:** `src/ExpandedLib/Processes/MachineJob.cs`, `ItemDie.cs`; the four machine tools.

Build them on the contract Tasks 1–4 settle, not before. This is the largest surface and the only one
that is still greenfield - designed extensible from the start it costs nothing, retrofitted it costs a
rewrite.

---

## Out of scope, and why

- **`BoilerCore` / `ConverterCore` pure cores** and structure-angle unification, from the old master
  plan's tail: internal tidiness, no modder ever sees them. They stay deferred.
- **§ A backport (58 rows)**: fixes, not architecture.
- **The mill's window.** `CreateDialog` returns null and the mill is a station with no face. That is
  content work, and it waits on the machining line.

## Consequences for the record

This plan un-defers one thing from the old master plan: **Phase 2 (taxonomy)** - `MetalRegistry`,
`ExLiquids`/`IMediumTaxonomy`, the shipped `liquids.json` - was parked as "built, adoption partial,
low value" when the only consumer was us. Adding a metal or a medium from your own mod is exactly the
kind of extension this target names, so its remaining adoption is now front-line work rather than an
untidy edge. It is not scheduled here; it needs its own pass once the contract above exists.
