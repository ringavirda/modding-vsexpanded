# NEXT — the single "what next" entry point

**Status** live — updated 2026-08-13 (torque state, crop count and the shear's decision landed). Every unit's docs-sync task updates
this file (see the maintenance rule at the bottom).

Ownership, layout and the rules that govern this directory are in
[../README.md](../README.md). In one line: `docs/design/**` owns decisions, this directory owns
sequencing, `../worklog/` owns what landed.

---

## ⛔ Read first — the tree was not building, and the release identity was wrong

Both fixed 2026-08-13, but they say something about the working rhythm. `src/SteelmakingExpanded/
SteelmakingExpanded.csproj` and its test csproj had been reverted to a pre-split revision in the working
tree: 36 CS0246s, and — worse — no `<AssetDomain>`, so a release cut from that tree would have shipped
smex with **zero assets** while silently dropping out of the shipped-asset guard. Separately, all three
published mods were stamped **below** what `dist/Releases/` already holds (exlib 0.7.0 < 0.7.2, lpex
0.6.4 < ppex 0.6.8, smex 0.9.5 < 0.9.8) — the release-below-migrations failure, recurring. Both now have
guards: `ModinfoTests` asserts source > released, mutation-checked.

**A framework-hardening plan is now live:**
[2026-08-13-framework-hardening.md](2026-08-13-framework-hardening.md) — F0–F2 done, F3–F8 open. It is
packaging, diagnostics and documentation for exlib as a *published library*; it does not touch content,
and it does not compete with the forming line below. The mod-merge question it raises is **blocked on an
owner ruling**, stated in that plan.

---

## What is next, right now

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
  enforced by friction and by nothing else. `ShippedRollSetTests` pins both halves: every shipped route
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

**2 — the shear block.** It terminates every rolling schedule and unblocks the crop table and the rolled
catalogue (**B3c** in content). The registry it declares against is **already built and tested**
(`ProcessJob` / `ProcessJobRegistry`, at `config/processjobs/*.json`), the crop tally is built
(`WorkPiece.Cropped`), and as of 2026-08-13 so is the decision — **`ShearFeed` / `ShearDecision`**, which
needed no footprint and so landed ahead of the layout. ★★ Its gate reads `ProcessJob.MinTorque` and
`.MinTier` rather than `RollSetSpec`'s, so a blade set needs **no spec format of its own**.

⛔⛔ **The design's footprint was wrong and its art was there all along** *(owner, 2026-08-13)*. The machine
is drawn as `machines/mpenergy/machine-mp-megablock-**cutter**.json` — the shop name, which is why two
sweeps for "shear" missed it — and it measures **3 × 1 × 2 cells**, not the 1 × 1 × 1 shear.md assumed.
Two of its open items close on that: the **wide shear is this block** (14 voxels of edge takes `flatwide`'s
15) and the **blade set** is machining-line.md's forged-and-tempered consumable.

⛔ **Blocked on the owner: the filler layouts** for this and the nine other mpenergy megablocks. Not to be
invented — a wrong legend fails silently. What is left after them: the block, the BE, the runtime shape
(an editable → runtime conversion), the crop table itself, a recipe, lang and a handbook page.

So the order is now: ~~item 4~~ → ~~the friction re-calibration~~ → ~~the torque state~~ → ~~the crop
count~~ → **the shear block**, which is now the only thing between the forming line and a finished product.
`WorkPiece.Mass` is deferred indefinitely.

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
