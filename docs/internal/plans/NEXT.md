# NEXT — the single "what next" entry point

**Status** live — updated 2026-08-15: the mod split is **BUILT**. Both merges are done, the mod set is
`exlib`/`iiex`/`siex` and closed, and the gate is 9 targets / 4,105 green. The framework-hardening plan
is live and part-landed; **M.6 landed and closed B23**, a wall that made the Bessemer vessel unbuildable
in every game mode. ★★ **The forming line is complete on both tiers, and every rolled product is
obtainable.** The owner's filler layouts arrived, the shear was built on them, **B3c closed the same day**,
and the **cast stock forms** and the **rod fork** landed behind it — so a bar, a slab, a billet, a bloom, a
cast slab and a vanilla rod all roll and crop, and both fastener benches have their input. Every unit's
docs-sync task updates this file (see the maintenance rule at the bottom).

Ownership, layout and the rules that govern this directory are in
[../README.md](../README.md). In one line: `docs/design/**` owns decisions, this directory owns
sequencing, `../worklog/` owns what landed.

---

## ⛔ Read first — the tree was not building, and the release identity was wrong

Both fixed 2026-08-13, but they say something about the working rhythm. `src/SteelIndustryExpanded/
SteelmakingExpanded.csproj` and its test csproj had been reverted to a pre-split revision in the working
tree: 36 CS0246s, and — worse — no `<AssetDomain>`, so a release cut from that tree would have shipped
smex with **zero assets** while silently dropping out of the shipped-asset guard. Separately, all three
published mods were stamped **below** what `dist/Releases/` already holds (exlib 0.7.0 < 0.7.2, lpex
0.6.4 < ppex 0.6.8, smex 0.9.5 < 0.9.8) — the release-below-migrations failure, recurring. Both now have
guards: `ModinfoTests` asserts source > released, mutation-checked.

**A framework-hardening plan is now live:**
[2026-08-13-framework-hardening.md](2026-08-13-framework-hardening.md) — **F0, F1, all of F2, all of F3,
F6.2, M.1 and M.2 are done** (F3 closed out 2026-08-14); F1.4, F4, F5, F6.1, F7, F8 and stage M from
M.4 onward are open (M.3 folded into M.4). It is packaging, diagnostics and documentation for exlib as a
*published library*, plus the merge; it does not compete with the forming line below.

**M.2 landed 2026-08-14** — the pipe tier is a variant now, declared first, and the three per-tier
registries are keyed on it rather than on `Code.Domain`. That was the one thing structurally blocking the
merge: with the tier off the domain, `iiex` can carry plated and cast at once without collapsing either.
Each tier also renders its own name at last (Plated / Cast / Rolled Piping).

### ★ Where to pick up

Both merges and the plan triage are done. The open options are listed under
*[What is actually next](#-what-is-actually-next)* below — nothing among them blocks anything else.

★★ **M.5 is DONE, and with it M1.** `smex` + `hpex` are one mod, **`siex` 0.9.9** — one assembly
(`siex.dll`), one domain, one ModSystem, one config section, one test suite, `assets/siex/`,
`docs/siex/`, handbook 00–05. `src/HighPressureExpanded`, `assets/hpex`, `docs/hpex` and
`test/HighPressureExpanded.Tests` are gone. The gate is now **9 targets, 3,990 tests**, green on
1.20/1.21/1.22. The mod set is `exlib`, `iiex`, `siex` and closed. Detail in
[the worklog](../worklog/2026-08.md); the record, including what its plan got wrong, is
[2026-08-14-m5-siex-merge-execution.md](2026-08-14-m5-siex-merge-execution.md).

⛔⛔ **What M.5 proves about the merge traps: the guards caught what the reading missed.** All three of
the plan's misses were found by a failing test, not by review.

1. **The plan's migration table was incomplete** — it never listed the 38 released `smex:` codes whose
   blocks *stayed*. They had been passing only because a stale `("smex", …)` row in
   `ReleasedCodeCoverageTests.Domains` injected smex as a live domain. That is trap 3 below, caught in
   the act: the vacuous pass is not hypothetical.
2. **"Exactly one block-entity alias" was six.** `ReleasedEntityClassTests` named the five extras.
3. **A config section is keyed by mod id**, so the rename silently discarded every player's tuning —
   `LegacyFileNames` folds a legacy *file*, which is a different move. Now covered by
   `LegacySectionIds` / `ExConfigDocument.FoldLegacySections` in exlib.

★★ **Both merges also found live data loss in shipped content.** A block-entity **class string** lives in
the save, appears in no definition, and nothing migrates it — so an unregistered class means the block
migrates and its contents are silently dropped. smex 0.9.8 shipped 19 such classes and none were
registered: 11 relocated ones are aliased in iiex and 6 retained ones in siex.
`ReleasedEntityClassTests` now holds every shipped class to resolving.

⛔ **A definition must never spell a block-entity class as a literal.** `.EntityClass("smex.BlockEntityX")`
survives a domain rename intact and thereby names a class nothing registers. Use `.EntityClass<T>()` /
`.EntityBehavior<T>()`, which compose the key from the assembly's own domain.

⛔ **The coverage gate had not gated iiex since M.4** (fixed 2026-08-14). `FLOORS` is keyed by *assembly*
name; M.4's rename left the old project name there. A key matching no package is silently ungated **and**
dropped from TOTAL. It now fails the run instead, with the renamed-assembly hint. The floors were
re-ratcheted off a measured full-solution run — exlib 73.6, iiex 60.3, siex 66.8, total 65.4 — having
drifted far below the truth while two of the three keys gated nothing.

⛔⛔ **The four merge traps, kept because they generalise beyond merges.**

1. **A per-mod discriminator becomes a bug the moment the mods merge.** `PipeMigration` gated three
   branches on `Code.Domain`, and both pipe tiers carry the same `type` variants, so a mechanical
   domain rewrite claimed every released segment code **twice**. Gate on the thing that actually varies
   (the `tier` variant); keep the domain only to scope the walk. `RolledJointTests` had the same shape as
   a *test* — parameterised by domain as a tier proxy, so half its rows became literal duplicates.
2. **`ExRecipeProfiles` and `ExConfigProfiles` key on the mod id and REPLACE silently.** Two config
   sections under one assembly leave whichever registered first unreachable. When the catalogues merge,
   watch for colliding keys — a collection initialiser assigns through the indexer, so one set overwrites
   the other with no error. (M.4 hit this; M.5's catalogues did not collide.)
3. **A stale row in `ReleasedCodeCoverageTests.Domains` makes the contract pass vacuously.**
   `DefinitionCodes.ForDomain` *injects* the domain rather than filtering, so a leftover row synthesises
   phantom live blocks. Delete every merged-away row and add the survivor **together**.
4. **Block-entity class strings live in the SAVE.** `CodeRelocation` remaps codes and never touches them;
   an unresolved key drops the block entity. The ground truth is the `entityClass` values inside the
   shipped zip — read them out of `dist/Releases/`, not out of the source.

★ Two mechanics worth reusing. Lang trees merge by **JSON key union**, never a directory move: all three
locale filenames collide, and a move leaves the locales mutually consistent while losing a whole mod's
strings — the parity guard passes over it. Assert the arithmetic (M.5: 174 + 25 → 194, the 4 shared keys
and 1 collision accounted for). And prove coverage by diffing **distinct test-method names** across the
merge (M.4 1139 → 1139, M.5 219 → 219), which distinguishes a deleted duplicate from a deleted check;
the raw test count cannot.

⛔ **`siex` names `iiex` explicitly and must keep doing so.** `<Private>false</Private>` does not
propagate to a transitive reference, so dropping the explicit item lets the SDK synthesise one with
`Private=true` and copy a second ModSystem-bearing dll into the output — Vintage Story then refuses to
load the mod at all. Verify by **staging the output and counting dlls**, never by reading a csproj: that
failure is green in every test run and visible only in game.

## ★★ The mod split is BUILT: five mods became two

**M1–M6** in [STATE.md](STATE.md), ruled 2026-08-13 and built 2026-08-14. `{iwex + lpex}` →
**Iron Industry Expanded (`iiex`)**, the early-industrial loop; `{smex + hpex}` → **Steel Industry
Expanded (`siex`)**, the steel loop. `exlib` unchanged. Full domain consolidation, so every block code
moved and every one is covered by a migration.

⛔⛔ **A merge collapses no tier (M3).** The plated pipe family and the iron gears are the early loop's
**bootstrap rung** — a player plumbs and gears the works before steam exists and upgrades to cast
afterwards. Two near-identical pipe families are progression, not duplication; do not delete one. The
tier moves onto a `tier` variant group (M4), which supersedes
[rolled-pipe](../../design/machines/rolled-pipe.md)'s *"the tier is the mod, not a variant axis"* —
that ruling's premise was tier == mod, which is exactly what the merge removes.

⛔ Closure is now per **loop**, not per mod (M2), and the loops are **nested**: the steel loop extends
the early loop's machinery and cannot close alone. ~15 entity pages cite the retired per-mod rule and
need re-justifying (stage M.7).

Stage **M** carried it in a forced order — decouple domain from mod id (M.1), then the pipe tier variant
(M.2), then the two merges (M.4, M.5). All four are done. What remains of stage M is **M.7**, the
re-justification of the ~15 entity pages that cite the retired per-mod closure rule, which is part of the
plan triage below.

---

## What is next, right now

### ★ The plan triage — DONE 2026-08-14

Both merges landed first, so it was done once. What it produced, and what it deliberately did not:

- **578 lines across 28 files** repointed at paths and types that exist. M.4's sweep never reached the
  plans: they still named `src/IronworkingExpanded`, `assets/iwex`, `IwexConfig`, `LpexRenameMigration`
  and 400-odd more. ⛔ Also **77 invocations of `scripts/run-tests.sh`, which no longer exists** — the
  entry point is `scripts/exmod.sh test` / `exmod.ps1 test`.
- **The iwex-era plans now carry verified unit state tables**, checked against `src/` rather than against
  their checkboxes — which were never ticked, because completion is recorded in prose per unit. The live
  queue in the expansion plan is **U4.4–U4.9, U6, U7.4, U8, U9, U10**; U1, U2, U3, U5 are records.
- **M.7 done** — 12 pages, not ~15, and the citation was the `**Mod**` header, not prose. Forming-line
  ownership is settled in writing: iiex owns the line (mill, wide hall, bending roller); siex owns the
  cast forms and the steel roll sets.
- ⛔ **101 `smex:`/`hpex:` code literals split by hand**: 94 rewritten, **7 left untouched as migration
  sources**. `naming.md` states the rule — rewriting one silently deletes a migration.

⛔ **What the triage confirmed rather than changed:** STATE's **B3c** stands. The mill, `WorkPiece`, the
roll sets and the process routes are built, but `StockForm` carries only input forms, no rolled *product*
item exists, and `ShearFeed.cs` has no shear behind it.

★ **Unresolved doc citations: 340 → 298.** The remaining 218 distinct paths are overwhelmingly files the
**open** units are specified to create, which is what an open plan looks like. A guard over this was
considered and **declined**: it would fail on all 218 immediately and need an allow-list that is itself
the drift.

### ★ M.6 — DONE 2026-08-14, and it found a shipped wall

`ReferencedCodes` + `CrossModReferenceTests` resolve every code the mods *point at* — recipe outputs and
ingredients, RCC `requireStacks`, and every stack a definition body names — against the union catalogue of
all three mods. **928 references, 530 into a mod domain.** Detail in
[framework-hardening](2026-08-13-framework-hardening.md) M.6.

⛔⛔ **It printed B23 on its first run.** `iiex:pipe-straight-ns-{metal}` named nothing, and
`ExConstruction` hard-fails a non-wildcard miss *before* the creative shortcut, so the Bessemer vessel
could not be raised in **any** game mode. It is now `iiex:pipe-cast-straight*`, the wildcard its own gas-intake
recipes already use. ⛔ Only that wall is gone — **B19 still leaves the segment uncraftable**, so the
vessel is creative-only until the cast tier gets recipes.

★ **The second find generalises further than the first:** five slag blocks dropped `slag-path-free` and
kin with **no domain**, which parses as `game:` and names vanilla blocks that do not exist. A bare code in
a mod definition is never right; all five now carry `iiex:`, and the guard holds the rule.

⛔ **Vanilla codes are deliberately unjudged** — 398 of the 928. Doing them needs a per-version manifest
off `.game/<slug>/assets`, since we ship 1.20/1.21/1.22 and a code added in 1.21 is a real defect for 1.20.
That is a unit of work, not a follow-up; the guard states both counts so the skip cannot read as a pass.

### ★ What is actually next

**Updated 2026-08-15.** The forming line is complete for every drawn route; what is queued is one designed
station, one item merge and two rulings. Pick one; nothing among them blocks anything else.

- ★★ **The workbench — designed 2026-08-15, nothing built.**
  [workbench.md](../../design/machines/workbench.md). A 5 × 5 bench for assemblies a player cannot make by
  hand, whose recipes may declare an **interaction sequence** (hold RMB with hammer, hands, wrench, …).
  ⛔⛔ Its justification is **stack size**, not room for layout: an ingredient's `quantity` is capped by what
  a slot holds, so 48 rods against a 16 cap needs three cells. ★★ It needs **no recipe engine** — vanilla's
  matcher is grid-size agnostic — and everything is JSON so a third party ships a bench without C#.
  ⛔ Largest unbudgeted cost is **handbook rendering**: vanilla draws recipes in a 3 × 3 widget and a
  sequence has no page at all. ⛔ Size the grid against a real bill of materials first; nothing shipped
  needs 5 × 5 yet.
- ★ **The diagram / sand-pattern catalogues as JSON config** *(owner-asked, not started)*. The same shape as
  `ProcessRoute` and `MaterialRole`: a catalogue, a loader, a contributed-to registry. ⛔ It carries a
  **guide payload** too — pages and images, since the design table doubles as the guide — and it is worth
  deciding up front whether guide pages are their own catalogue *referencing* diagrams, rather than fields
  inside a diagram entry. The workbench's discoverability leans on this, so it reasonably comes first.
- ★ **M.8 — one `heavyplate` with a metal axis.** Unblocked apart from recipes: the cast art is redrawn to
  600 u (matching the rolled one's 240 vx³ exactly) and `castplate` is struck as a name that never was a
  second item. What remains: the metal variant, `castplate-heavy` re-massed 160 → 600, its casting-pattern
  capacity following, and the migration. ⛔ **Recipe interchangeability is deferred by the owner** to the
  recipe pass — do not infer it.
- ★ **The mid-gap crop rule.** Now the highest-value forming item, because it is one ruling that unblocks
  two declared routes and closes B12: what happens when a crop yields pieces that each still want a pass
  (billet grooved 2.25 → 6, bloom wide 3.0 → 5). Today `FeedVerdict.PartCropped` refuses to roll a
  part-cropped piece, which is exactly what makes those two rows unexpressible.
- **B25 — the 72 recorded unmigrated codes.** ⛔ Sharper than it was: `BessemerToConverterMigration`'s
  right-hand sides emit word-spelled sides (`convertercontrol-north`) where the live blocks use letters,
  so those rows name no live block and do nothing. Paying this off needs content rulings on which retired
  families get migrated and which get purged.
- **The live iwex queue** — U4.4–U4.9 (delete the float pools), U6 (puddling), U7.4 + the shear (B3c),
  U8, U9, U10.
- **F4/F5** of the framework-hardening plan, and **M.8** (`heavyplate` absorbs `castplate`).
- ⛔ **`src/SteelIndustryExpanded/modicon.png` is byte-identical to iiex's** — blocks publishing, and
  needs art rather than code.

### Then

The extensibility layer is **complete** ([2026-08-12-extensibility.md](2026-08-12-extensibility.md), all
seven tasks). The contract a third party writes against now exists and is guarded; what is left of it is
content, listed in that plan's *What is still open*.

### The forming line, in this order *(owner ruling 2026-08-12)*

**1 — rework items 3 and 6. Done 2026-08-12.**

- **3, the two-round pass model.** Built. `Strips[]` + `Turned[]` → one `Thickness`, the `Gap` the piece is
  half way through, and a per-side fed-this-round flag. The `Gap` field is what tells round 2 from round 1
  without a round counter; the side count is the flag array's own length, so re-dividing for a different
  barrel *is* the round reset. Nine members deleted, the composition repointed at the half-step, and the
  "a refused offer still mutates the held stack" gotcha closed as a side effect.
- **6, the form rename.** Done, with the masses (400 / 1200), the ten stage shapes, both ladder files, the
  roll sets' `accepts`, three lang files, two goldens — and `StockForm.FormerNames` plus
  `StockFormRenameMigration`, so a piece already in a world survives both the item-code change and the
  form name on its own stack.

**1b — item 4 and the skip bound. Both done 2026-08-12.**

- **Item 4, the config re-cut.** Both narrow families now walk 2.5 / 2.0 / 1.5 / 1.0 off the stock's own
  ladder, `flat` runs barrel **4**, the 0.5 rung is gone and `slitting` is **retired** (no migration: a
  retired code must not be remapped onto a surviving one). Ladders, roll sets, three lang files and the
  golden moved together.
- **The skip bound.** `HotFriction` 0.5 → **0.3** (δ_max 1.0 → **0.36**), `ColdFriction` 0.09 → **0.055**.
  An ordinary 0.25 round bites, a 0.5 skip skids, and `MillSchedule.NextGap` stays uncalled — the walk is
  enforced by friction and by nothing else. ⛔ **Amended 2026-08-14 for `flatwide` only** (owner): its top
  roller is movable and the player sets the gap by holding RMB on the mill's raise/lower cell, so one wide
  set covers every wide gap instead of one item per gap. `flat` and `grooved` stay locked and stay
  friction-walked, and friction still bounds the wide set — it decides whether the chosen gap bites. Not
  built: it needs the two `i1` cells of [machines.txt](../workbench/machines.txt), which the mill lacks.
  `ShippedRollSetTests` pins both halves: every shipped route
  walkable round by round, every skipped gap refused.

⛔⛔ **And it exposed a balance defect, now closed.** `RollingTorqueScale` was calibrated when a gap was one
bite. An ordinary round loaded the run at **0.065 N·m against ~0.4 of headroom — 16 %**, where the design's
own worked pass claims *"about 15 % to spare"*, i.e. 85 %. Retuning the scale to ~0.097 was the obvious fix
and the wrong one: it would have restored the number while leaving the mechanism that lost it. See **1c**.

**1c — the torque state. Done 2026-08-13.** `LoadTorque` is now `RollingLoadTorque` (**0.34 N·m**, exactly
85 % of a bridged wheel's headroom) while stock is between the rolls and zero when it is not, with only the
cold multiplier on top. Deleted with the formula: `ContactLength`, `RollingTorqueScale`, and the pass's own
`_draft`/`_width` — nothing past the bite test read them, so `BeginPass` lost a parameter and the block
entity lost two persisted keys.

★ The point is not the number, it is that **a re-cut schedule can no longer move the mill's demand at
all**. `RollingMillLoadTests` was rebuilt around that: a "heavy" pass is now a property of the run (a weak
drive, a lost drive) rather than a synthetic 40-wide piece of stock that does not exist. Both halves are
mutation-checked — raising the demand to 0.45 breaks the headroom test, dropping the cold multiplier breaks
three others.

**The crop interaction is ruled: one product plus a remainder** *(2026-08-12)*. A stroke takes one product
off and writes the rest back as stock at the same stage. The two readings were not alternatives — `count` is
the piece's total yield in every published crop row (400 / 4, 600 / 6, 3000 / 5), so recoverability's "into
6" is that yield and crop-not-convert is how it leaves. Generalised as **a staged job crops, a whole-item
job converts**, now owned by [process-extension](../../design/mechanics/process-extension.md).

⛔⛔ **And the remainder is a COUNT, not a mass** *(owner ruling 2026-08-13)*. Length is not defined
programmatically — it comes from the art — and **cut points are config, because what a piece divides into is
the modder's choice and not something we can calculate**. So `ProcessJob.count` already carries the
declaration, and the only new per-stack state a crop needs is **one `int`**.

★★ That takes `WorkPiece.Mass` **off the critical path** — it was only ever wanted so a remainder could
weigh less. The 48-voxel refusal shrinks with it: a mandatory crop becomes a **declared** property of a
stage rather than arithmetic over a length nobody tracks.

**1d — the crop count. Done 2026-08-13.** `WorkPiece.Cropped` tallies crops **taken**, with
`Crop(count)` / `CropsLeft(count)` / `IsSpent(count)` over it, round-tripped on the stack and written as an
absence when zero so whole pieces still stack.

★★ Taken rather than remaining, which the ruling's wording implied: counting up keeps zero meaning
*untouched*, so a piece that has never met a shear and one worked out to nothing cannot read alike, no
piece already in a world needs migrating, and the **declared count stays the authority** — retuning a crop
row from 4 to 6 gives every existing piece the two extra crops instead of stranding it.

⛔⛔ **And it forced a rule the design had not stated: a part-cropped piece cannot be rolled.**
`FeedVerdict.PartCropped` refuses it at the mill before the gap is judged. Without it a player crops three
products out of a bar, rolls the rest to the next stage and it is worth that stage's whole count again —
metal from nothing. That refusal is exactly what lets the tally be one `int` per stage rather than a
proportion carried between stages, i.e. it is what keeps `WorkPiece.Mass` deferred. It costs the player only
the order they work in: finish the cut, then roll the pieces on — which is how
[rolling](../../design/processes/rolling.md) already describes the mandatory crop ("each of the five pieces
… rolled on through 1.5 to 1.0").

**2 — the shear block. BUILT 2026-08-14.** ★★ The owner's filler layouts arrived
([machines.txt](../workbench/machines.txt)) and unblocked it the same day. `BlockShear`,
`BlockEntityShear`, the runtime shape, the blade sets, both grid recipes, three locales, 11 station tests.
The layout confirmed the 3 × 1 × 2 measured off the art, and building it needed two framework pieces:
**slab footprint cells** (the runtime read a per-cell `collisionBox`, but nothing could author one) and
**`MachineTool`** in exlib, the tiered-consumable contract the machining line's universal cutter reuses.

★★ **And B3c closed the same day: the rolled catalogue is BUILT and the line makes a product.** Six items
with their settled masses, three drawn shapes exported, and a crop table with four working routes — a
shingled bar becomes 4 rods, 2 beams or 2 plates; a shingled slab becomes 2 boiler plates.

⛔ **Three declared routes stay unreachable, each on its own blocker**, and the middle one is the surprise:
the **rod fork** needs `stock-rod`; the **cast routes need cast stock forms — only `shingledbar` and
`shingledslab` are registered, so cast stock cannot enter the mill at all**; and `heavyplate` waits on
**M.8**. ⛔⛔ That last one means the **cast-slab route is not walkable**: its 2.0 crop is recoverability's
mandatory one, so without a product to claim there a cast slab rolled to 1.0 lands at 80 long against the
48 limit.

The handbook page is deliberately skipped — the mill has none either and the forming shop wants one page
for both stations.

★ **It closed the drive-torque question without any of the three proposals.** `MpEnergyNetworkState`
publishes `SupplyPower = driveTorque * Speed`, so torque comes back as `SupplyPower / Speed` — no new state
field, and `NotEnoughDrive` stays a verdict that can fire.

*What follows is the pre-build record.* The registry it declares against was **already built and tested**
(`ProcessJob` / `ProcessJobRegistry`, at `config/processjobs/*.json`), the crop tally is built
(`WorkPiece.Cropped`), and as of 2026-08-13 so is the decision — **`ShearFeed` / `ShearDecision`**, which
needed no footprint and so landed ahead of the layout. ★★ Its gate reads `ProcessJob.MinTorque` and
`.MinTier` rather than `RollSetSpec`'s, so a blade set needs **no spec format of its own**.

⛔⛔ **The design's footprint was wrong and its art was there all along** *(owner, 2026-08-13)*. The machine
is drawn as `machines/mpenergy/machine-mp-megablock-**cutter**.json` — the shop name, which is why two
sweeps for "shear" missed it — and it measures **3 × 1 × 2 cells**, not the 1 × 1 × 1 shear.md assumed.
Two of its open items close on that: the **wide shear is this block** (14 voxels of edge takes `flatwide`'s
15) and the **blade set** is machining-line.md's forged-and-tempered consumable.

~~⛔ Blocked on the owner: the filler layouts.~~ **Supplied 2026-08-14** for all ten mpenergy megablocks
and the three pipe machines, in [machines.txt](../workbench/machines.txt). ⛔ Read its per-machine notes,
not just the global legend: `_` and `-` are **redefined per machine** (the lathe, planer and rolling mill
all make `_` a vertical south slab where the global legend says horizontal bottom), and the steam hammer
introduces `|`. A wrong legend fails silently.

So the order is now: ~~item 4~~ → ~~the friction re-calibration~~ → ~~the torque state~~ → ~~the crop
count~~ → ~~the shear block~~ → ~~the rolled catalogue (B3c)~~ — **the forming line is done end to end for
the shingled route**. `WorkPiece.Mass` is deferred indefinitely.

★★ **The cast stock art is DONE and the stages are generated** *(2026-08-14; the forms followed the same
day — see above)*. It was **one** shape short,
not three: `castbloom` was already 4 × 4 × 25 and `castslab` already 12 × 4 × 25, and only `castbillet` sat
at 3 × 3 × 24 against the settled 27 — its parent half went 12 → 15. All 17 cast stage shapes now ship, and
every generated width and length lands exactly on [rolled parts](../../design/items/rolled-parts.md)'s crop
table. **Registering the three `StockForm`s is now ordinary code**, and it unblocks five crop rows.

- ~~**Cast stock forms**~~ — ★★ **DONE 2026-08-14, and the cast side of the line now works end to end.**
  The three forms are siex's (`CastStockForms`), their ladders and their five crop rows ship under
  `assets/siex/config/`, and iiex's roll sets accept them. Gate 9 targets / **4,148**.

  ★★ **The piece was never missing — the form was.** `iiex:caststock-{billet,bloom,slab}` has shipped
  since U1 at exactly the settled 600 / 1000 / 3000, poured by the long cell's lane patterns; nothing had
  ever told the mill what they were. So no item was minted and no art moved domain: the itemtype gained a
  `stockForm` attribute and `ItemStockPiece`, and siex put the forms in the registry. **iiex ships the
  pieces, siex makes them rollable** — an iiex-only player holds cast stock the rolls refuse, which is the
  tier gate falling out of the split for free.

  ⛔⛔ **The wide roll sets are cancelled, not built** *(owner ruling 2026-08-14)*. The wide stand's top
  roller is the movable one and the player sets the gap on the mill, so **one wide set covers every wide
  gap** — `rollset-flatwide35` / `-flatwide30` are struck from [steel roll sets](../../design/machines/steel-roll-sets.md),
  and 3.5 / 3.0 are ordinary rungs on the two cast ladders instead. ⛔ Its cost is the one that page's
  open question predicted: one item carries one `MinTorque`, so the **wide route has no torque gate at
  all** and cast stock rolls behind the iron-tier 0.5. The shear side does gate — every cast crop asks
  `minTier: 2`, the steel blade.

  ⛔ **And it found a shipped wall of its own**: `HeatingHearthLayout.StockOf` still matched the prefixes
  `castbillet` / `castbloom` / `castslab` against an item that became `caststock-{form}` back in U1, so
  **no cast piece could be reheated at all** — on the one tier that is on the crosswise seating from its
  first pass. The rows that pinned it were three literals; they read the shipped variant list now.

  ★ Two other things worth keeping. `StockItemDefinitions` emitted one item per **registered** form, so
  the first foreign form would have minted an iiex item for somebody else's stock and thrown on the mass
  lookup; it emits the forms it masses now. And `bloom` is the shingled bar's *former* name, so a cast
  bloom declaring the bare variant would have rolled as a 400 u wrought bar with the mass as the only
  symptom — `FormOf` writes `cast` + variant and a guard holds it.

  ⛔ **Two rows stay unreachable, and they are the same question**: the billet's grooved 2.25 and the
  bloom's wide 3.0 — a crop that yields pieces each still wanting a pass, against a shear that does one
  product plus a remainder. (`heavyplate` on M.8 is the third row, and is not this question.) The billet's
  ladder is therefore flat-only, and B12 is now measured: a bloom rolled to 1.0 is 50 long against the 48
  seating.
- ~~**The rod fork**~~ — ★★ **DONE 2026-08-14.** Both branches work: a vanilla `game:rod-iron` fed at the
  deck becomes 4 × `iiex:rivetrod` down the grooved branch or 1 × `iiex:nailplate` down the flat one, so
  both fastener benches have their input. Gate 9 targets / **4,192**.

  ★★ **Admission is a new extensibility seam, and it is keyed on CODES, not forms.**
  `StockForm.RegisterFeedstock(offered, entersAs)` + `BlockEntityRollingMill.Admit` convert an offered
  stack into a stock item at the deck, heat carried across — which is how the re-rollable rod stays
  vanilla's `game:rod-*` and none of the 32 call sites that name it had to change. Idempotent, because
  both the deck's gap mapping and the feed itself call it; ⛔ and it has to run **before** the mapping, or
  feedstock picks its gap against a one-band schedule and is then fed at another.

  ★ **The rod is the one family drawn at every rung**, so its ladder names a `shape` and a per-stage
  `element` and `ItemStockPiece` renders the authored art instead of scaling the base. Every other form
  composes. ⛔ Its `RolledStockStagesTests` blocker was real: the fixed `Gaps` list assumed every form
  leaves the helve 3 thick, and a rod entering at 2.0 would have demanded a 3.0 shape of a state it can
  never be in. The list is per-form now, mirroring the generator's own `stages_for`.
- **M.8** — unblocks `heavyplate` and with it the cast-slab route's mandatory crop.

⛔ One thing still to settle: **pass duration** is the last consumer of computed length
(`length / ωR` in `BeginPass`). Under art-declared length it reads a declared length off the stage or
becomes a declared time — but the ω coupling must survive either way, since "the network speed draws the
stock through" is the mill's whole point. The only other consumer is the composed mesh's Z scale, which
item 7 retires anyway.

**3 — then the tier spine**: hpex's **B5/B6**.

### Deferred by this ruling

**iwex U4.4–U4.9** was parked on "once the extensibility layer lands", which it now has, and it is fully
specified with no open questions — the most shovel-ready work in the repo. It is not next only because the
forming line was chosen over it. Pick it up whenever the forming order stalls on a decision.

---

## Where the architecture pass stands

Live since 2026-08-10, when vendoring the game source made it worth doing before more features.

| Half | State |
|---|---|
| Framework composition A0–A4 ([staging](2026-08-10-framework-composition-staging.md) · [design](../../design/mechanics/framework-composition.md)) | **done 2026-08-12** — all five stages |
| Vanilla-practice findings ([backlog](2026-08-10-backport-and-vanilla-backlog.md) § B, 30 items) | **18 done** (6 worked, 12 found already fixed) — **12 open** |
| Backport, remaining upheld ([backlog](2026-08-10-backport-and-vanilla-backlog.md) § A, 58 items) | not started; mostly fixes, not architecture |

The 12 open § B rows are mostly one cluster — MP correctness (generator torque sign, overstress
frame mismatch, filler-port resistance against a waterwheel's envelope) — plus the per-second
`MarkDirty(true)` on every pipe and `GetDropsForHandbook` missing from ~20 BE-driven droppers.

⛔ **Two standing cautions on that backlog**, both learned the hard way:

- **16 of 83 backport claims were wrong on inspection.** Open the file and confirm the bug is live
  before acting on any row.
- **12 of 30 § B rows were already fixed** and never struck off, five of them in the first seven
  opened. The counts above are now accurate; the habit that produced the drift is not fixed.
- **The audit was written against the 1.22 source alone, and we ship 1.20/1.21/1.22.** At least one
  recommendation names API absent before 1.22. Check any recommended member against `.game/1.20`
  and `.game/1.21` before using it.

---

## The iwex expansion — paused, not superseded

**Live plan:** [2026-08-04-iwex-u2-u10-expansion.md](2026-08-04-iwex-u2-u10-expansion.md) (U2–U11).
The [completion plan](2026-08-04-iwex-completion.md) keeps only the **U1** remainder, the Global
Constraints and the Commands block; for everything else the expansion wins.

| Unit | State |
|---|---|
| U2 (charge-column cutover) | done 2026-08-06 |
| U3 (counter-current) | done 2026-08-06 |
| U4.1–U4.3 (hearthmetal · tap normalisation · hearth cells) | done 2026-08-07 |
| U5 (burdenmaker; ore mixer + bunker deleted in U5.7) | done 2026-08-07 |
| **U4.4–U4.9** | unblocked (the extensibility layer landed 2026-08-12), but **deferred behind the forming line** by the ruling above. Fully specified, no open questions |
| U6 onward | after U4, per the plan's execution order |
| U1 remainder | tracked in the completion plan, runnable any time |

Blockers and open design questions live in [STATE.md](STATE.md).

---

## Maintenance rule

Each unit's docs-sync task updates this file — the position tables, and any deferred defect it
retires or adds. A unit is not done while NEXT.md still shows it as next.

A **stage** is not done until its own plan file says so in its header. Reconstructing completion
from worklog prose is how a finished stage reads as unstarted, which has already happened once.

---

## Deferred defects *(recorded during implementation review, 2026-08-07)*

Known, deliberately not scheduled; pick one up when its file is already open for other reasons.

1. `MultiblockCodes.AnyProvides` matches on code prefix existence, not real variant-state membership,
   so `FurnacePartsTests` cannot detect a layout legend naming a non-existent variant (same class as
   B24's tautological `RolledJointTests`).
2. Headless test walls rely on `new Block()`'s implicit `Replaceable=0` default rather than an
   explicit override — fragile if the VS API default changes.
3. No exact-value regression test for `DrainSlagTap`/`TapSlagStackFactor` — a copy-paste slip reusing
   `TapIronStackFactor` would not be caught today.
4. `CupolaScenarioTests` (lpex) and `BlastFurnaceScenarioTests` (smex) use direction-only tap
   assertions (>0, <prior), so a ~4x rate change passes both unchanged.
5. BlastFurnace preheat assertion: `Assert.Equal(0f, PreheatGain)` is satisfied by rig construction
   (headless climate null -> ambient 20 = blast 20); a hot-blast control would make it real.
6. `HopperTallTests` doc-comment says "check 1 or 2" where derived indices are 1 and 3 (wording only).
7. `TeardownSymmetryTests` checks **per file**, so a partial class could split its two teardown
   halves past it.
8. 91 relative links across `docs/` point at files that do not exist. None are from the 2026-08-12
   consolidation (that move was link-checked to zero); they predate it.
