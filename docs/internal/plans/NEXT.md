# NEXT — the single "what next" entry point

**Status** live — updated 2026-08-12. Every unit's docs-sync task updates this file (see the
maintenance rule at the bottom).

Ownership, layout and the rules that govern this directory are in
[../README.md](../README.md). In one line: `docs/design/**` owns decisions, this directory owns
sequencing, `../worklog/` owns what landed.

---

## What is next, right now

The extensibility layer is **complete** ([2026-08-12-extensibility.md](2026-08-12-extensibility.md), all
seven tasks). The contract a third party writes against now exists and is guarded; what is left of it is
content, listed in that plan's *What is still open*.

**Next: the tier spine.** **B3c** is dissolved in mechanism — a stage naming a `code` builds its item — but
no shipped rung names one yet, because every shipped stage is a shear crop and the shear is not built. So
the order is now: the **shear block** (which unblocks the crop table and the rolled catalogue together),
then hpex's **B5/B6**.

Extensibility became a stated product target on 2026-08-12: other modders must be able to add to
diagram crafting, sand casting, rolling, the steam hammer and the machining line, and people have
asked for it directly. Four rulings settled it, and they are why this comes before the tier spine —
the attribute schemas a modder writes against are a public contract, so the breaking changes have to
land before anyone builds on them, not after.

After that, the tier spine: **B3c** (no rolled product item exists), then hpex's **B5/B6**.

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
| **U4.4–U4.9** | **next, once the extensibility layer lands** |
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
