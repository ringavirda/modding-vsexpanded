# Extensibility — third-party mods extend our processes

**Status** **complete** 2026-08-12. Every task landed; all 15 test targets pass across 1.20/1.21/1.22.
What remains is content rather than contract — see *What is still open* at the bottom.

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

### Task 1 — Name the rule ✅ DONE 2026-08-12

`docs/design/mechanics/process-extension.md` written and linked from
[framework-composition](../../design/mechanics/framework-composition.md) and
[machining-line](../../design/mechanics/machining-line.md). It owns the rule, the two registry shapes,
the stage schema, item generation, declared renames and the versioning contract.

Everything below is now implementation of that page.

### Task 2 — The stage schema and `RollSetSpec` ✅ DONE 2026-08-12

`stages[]` replaced the `gaps` + `outputs` pair. Each stage carries `thickness`, `element`,
`acceptedBy` and an optional `code`; **`code` present is the stopping point**, which retired
the `(form, gap)` key problem entirely rather than solving it — the stage *is* the (form, gap) pair.

**Owner ruling that shaped it:** a registry is a **merged catalogue any collectible contributes to**, not
a field on the tooling item. Hang the ladder on the roll set and adding a stock family means patching every
set; hang it on the stock and adding a roller family means patching every stock item. Merging removes the
choice, and it is why `acceptedBy` is per stage rather than per declaration.

**Built:**

| Where | What |
|---|---|
| `mods/exlib/src/Processes/` | `ProcessStage`, `StageLadder` (the `stageladder` attribute + parser), `StageLadderRegistry` (merge on `(thickness, family)`, first declaration wins, conflicts reported), `StageLadderLoader` (scans collectibles at `AssetsFinalize`, wired into `ExpandedLibModSystem`) |
| `mods/iiex/src/…/Forming/` | `MillSchedule` — the fitted set's branch of one family's ladder; `RollSetSpec` slimmed to `schema`/`family`/`accepts`/`barrelWidth`/`minTorque`; the shipped bloom and slab ladders on `StockItemDefinitions`; `MillFeed`, `BlockEntityRollingMill` and `BlockRollingMill` read a schedule |
| tests | `StageLadderTests`, `StageLadderRegistryTests`, `MillScheduleTests`, `StageLadderSeeds`; `RollSetSpecTests` and `ShippedRollSetTests` re-cut; three goldens regenerated |

⛔ **`accepts` stayed on the roll set and is not derived from the ladder.** They are independent facts: the
ladder says which states the metal has, `accepts` says whether the tooling can take that stock at all. Derived,
the narrow `flat` set (barrel 6) would have started biting slabs, which only `flatwide` should.

⛔ **A ladder must be an `attributes` entry, not a top-level itemtype key.** `ExItemDef.Raw` writes the
root and `CollectibleObject.Attributes` never sees it, so the first cut shipped a ladder the loader could
not find and every feed refused as `WrongForm`. Caught by the fixtures, not by a golden — the golden was
happy either way.

⛔ **`BlockEntityRollingMill.Ladders` is settable** for the same reason `NetworkSystem` is: a machine that
reads only a process-wide static cannot have a route stood up for it in a fixture without writing to the
catalogue every other test shares.

### Task 3 — Schema versioning across the shipped specs ✅ DONE 2026-08-12

`SpecSchema` (exlib `Processes/`) holds the contract: absent reads as **schema 1**; an older form is
read as declared and is the caller's to fall back on; ⛔ a **newer** one is **refused** with an error
naming both numbers, since reading it as the form we do know would mis-parse someone's content
silently. `StageLadder`, `RollSetSpec` and `MoldSpec` all route through it; `MoldSpec` gained the field
and `PatternItemDefinitions` now emits it (pattern golden regenerated).

⛔ **A spec attribute is not save data**, which is the finding that resized this task. It sits on the
itemtype and is re-read from the declaration every load, so *our own* emitters never need a fallback —
regenerating the def replaces the old form outright. The burden exists only for declarations we do not
own, so it starts at the first schema a third party could have written against. That is why Task 2 could
drop `gaps`/`outputs` outright at schema 1 and still satisfy E3, and why no legacy-`gaps` reader was
built: it would have been dead code with a maintenance cost from the day it was written.

⛔ **The diagram scan is out of scope, and not by omission.** A `diagram-*` item is identified by code
shape alone and holds no spec for a parser to version. Recorded on the design page; if diagrams ever gain
data, that is when they gain a number.

`ShippedSpecSchemaGuards` (iwex) pins that every spec we emit declares `schema` explicitly even though
absent would parse — our JSON is the template a third party copies, and one that omits the field teaches
them to omit it. Mutation-checked.

### Task 3b — The item emitter ✅ DONE 2026-08-12

`ProcessItemEmitter` builds an `ExItemDef` per stopping point, wired into
`ExDefinitionModSystem.AssetsLoaded` at 0.04. All five `MetalFamilyEmitter` properties carried across, plus
one the metals did not need: **the `game:` domain is never generated into**, since injecting an itemtype
there would replace a base-game item. `ProcessItemRenames` turns declared `formerCodes` into item remaps
through `BlockMigrationModSystem`, discovered like any other migration.

⛔⛔ **The ladders moved from an item attribute to `config/stageladders/*.json`, and the load order forced
it.** Generation must run at 0.04 — before the patch loader (0.05) and the object loader (0.2, verified in
`vsessentialsmod/Loading/`) — so a ladder carried on an itemtype could not be read in time: **you cannot
build an itemtype from data that lives on an itemtype.** Keeping the attribute would have meant two parses
of one contract (raw pre-patch JSON for the emitter, resolved collectibles for the registry), which is the
same shape as the `.Raw`/`.Attribute` bug from Task 2. Owner ruling; the merged-registry ruling is
untouched, only the file's home changed.

The catalogue is now read twice by **one** parser: at 0.04 for generation and at `AssetsFinalize` for the
registry the machines consult. ⛔ Consequence: **patching our catalogue adds a route but no item**, because
the patch lands after generation. Ship your own file instead — the merge puts it in the same family.

**B3c is dissolved in mechanism**: the rolled catalogue can now fall out of the declaration. It is not yet
dissolved in content — no shipped rung names a `code`, because every shipped stage is a shear crop and the
shear does not exist. The first real product arrives with Task 3d.

### Task 3c — Mid-pass rendering ✅ DONE 2026-08-12

`ItemStockPiece.OnBeforeRender` now has two routes: the **drawn** stage, tesselated from the family shape
file at the stage's element, when the ladder names both; and the **composed** mesh otherwise, which is what
already shipped. `StockMesh.IsBaseState` and `.ElementFor` hold the decision, so it is testable without a
client — the upload itself is not reachable headlessly.

⛔ **The bug this found: every single-sided piece rendered as unworked.** The early return was
`piece.Sides <= 1 && piece.IsEven`, and a one-sided piece is *always* even — so a bloom taken from 3.0 down
to 2.0 looked exactly like one straight off the helve. The settled two-round model makes uneven pieces
impossible, which would have made the whole composition path dead code. Now the test is "one side, still at
the base gauge". Mutation-checked against the old expression.

⛔⛔ **Thickness alone cannot address a stage, so the piece had to gain its branch.** A fork draws one gauge
two ways (`Grooved200` / `Flattened200` at 2.0), so a renderer keyed on thickness is wrong half the time.
`WorkPiece` gained `Family`, written with the reduction in `CompletePass` and carried on the stack under
`rollerFamily`; the mesh cache key includes it. Absent on an old stack, which reads as "no branch known" and
falls back to the composed mesh rather than guessing.

The handbook trap was already avoided — the cache was keyed on geometry, never on a per-stack id — and is
now pinned by a test rather than left to the comment.

### Task 3d — The shear registry ✅ DONE 2026-08-12

`ProcessJob` / `ProcessJobSet` / `ProcessJobRegistry` / `ProcessJobLoader` — the terminal shape, declared at
`config/processjobs/*.json` and merged exactly as a ladder is. **Count** is the field that makes it a shape
of its own; `stage` + `family` are optional and let a job take a piece part way down a ladder, which is what
the crop table needs and what a gap-keyed table could never express.

⛔ **The crop table itself is not shipped, deliberately.** Seven of its nine products are items that do not
exist, and shipping codes that resolve to nothing is precisely the mistake the four dangling roll-set
outputs already made once. Recorded in [shear.md](../../design/machines/shear.md) § Open; it waits on the
rolled catalogue, and on the split-versus-remainder interaction that page still lists as undecided.

**The sweep landed** and the convention holds everywhere. `item-shingled-bar.json` → `CutRod1..4` + `Beam`;
`item-rolled-rod.json` → `CutRivetRod1..4`; `item-rolled-beam.json` → `CutPlate1..2`. All are the shear's,
tabulated on its page. ⛔ `NailPlate` inside `item-rolled-rod.json` is **not** — it is the one whole-piece
conversion, so it is a ladder stopping point, not a crop.

### Task 4 — The public C# registration API (E1) ✅ DONE 2026-08-12

`ProcessExtensions` — `AddStages` and `AddJobs` over both registries, with `Shared` pointing at the same
instances the loaders fill.

★★ **The code route re-states none of the rules.** It builds the declaration a file would have held and runs
it through the same `TryParse`, throwing on anything the JSON route would have refused. That is what keeps
one set of rules rather than two, and it is why the surface cannot drift wider than the schema.

### Task 5 — The guard ✅ DONE 2026-08-12

`ProcessExtensionGuards` — a source scan in `ShapeLoadingGuards`' shape. Mutation-checked red by pointing
`ClaimFinishedPiece` at a literal `iwex:nailplate`.

★★ **The corpus derives itself**: a file is a process machine because it *reads a process registry*. A hand
list would have to be remembered; this way a machine joins the guard by adopting a registry, and cannot be
added outside its reach. Art paths are exempt — a machine's own appearance is not a product.

### Task 6 — Document it where a modder will look ✅ DONE 2026-08-12

`mods/exlib/wiki/Extending-Processes.md`, linked from `_Sidebar.md` and `Home.md`. Both registry shapes with
copy-pasteable JSON, the generation rules, the C# route, the schema promise, and a symptom table for when
nothing appears.

### Task 7 — `MachineJob` and `ItemDie` (E2) ✅ DONE 2026-08-12

⛔ **`MachineJob` is not a new type — it is `ProcessJob`.** The machining line's sketch and the terminal
registry are the same shape; a second near-identical record would have been two schemas for one idea. The
job gained `minTier` and `seconds`, which are what a machine tool's work costs beyond a crop's.

`ItemDie` carries a job set in a `machinejob` attribute, is recognised by *parsing* rather than by its code
— so a third party's die needs no naming blessing — and ships `ItemDie.Itemtype(domain, jobs)`, the public
factory seam `PatternItemDefinitions` proved and the roll sets still lack.

⛔ **`RenderSpec` is deliberately not built.** No machine renders a job yet, so every field would have been
unverifiable design with no consumer — the same call as the legacy-`gaps` reader in Task 3.

★★ **`StockForm` is now a registry**, which the machining line named as *the literal wall on mill
extensibility*: a third party's roll set could declare it accepts their stock and the piece still dead-ended
at `WrongForm`, because nothing could add the form. `Register` / `Unregister` / `TryGet` / `SeedDefaults`,
and deliberately **no `Clear`** — a mod emptying the table would take our stock and every shipped roll set
with it.

---

## What is still open

Contract complete; the rest is content.

| Open | Why it is not here |
|---|---|
| the shear's crop table | seven of nine products do not exist — see Task 3d |
| the rolled catalogue (B3c) | dissolved in **mechanism**: a stage naming a `code` builds its item. No shipped rung names one, because every shipped stage is a shear crop and the shear is not built |
| the four machine tools, the shear block | machines, not contract. They now have a registry to be built against |
| `RenderSpec` | no machine renders a job yet |
| stage art wiring | the shipped ladders declare no `shape`/`element`, so every piece takes the composed-mesh route. The drawn route is built and waits on the export |

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
