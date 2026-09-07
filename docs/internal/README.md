# docs/internal — planning, records and reference

Everything here is for whoever is building the mods. None of it ships: the packager takes
`mods/*/assets/` and the built dlls, never `docs/`.

**Start at [plans/NEXT.md](plans/NEXT.md)**, which points at the plan of record,
[plans/2026-09-04-roadmap.md](plans/2026-09-04-roadmap.md).

## Why this directory exists

These four trees used to sit directly under `docs/` and were excluded from git in
`.git/info/exclude`. That cost us a plan: `docs/design/master-plan.md` was written 2026-07-13,
deleted at some point, and has **no recoverable history** because it was never committed. Its only
surviving trace is a note in an assistant memory file.

It also cost us duplicated work. A1's task list included migrating `PossibleOrientations` off
`System.Text.Json`; nothing recorded that the task was outstanding, so it was rediscovered two days
later through a separate audit and investigated from scratch.

So: one root, tracked, not disposable. Staging it is the repo owner's call - nothing here does that.

## Who owns what

The split is fixed and worth restating every time it is tempting to blur it.

| Tree | Owns | Does not own |
|---|---|---|
| `docs/design/**` (tracked, outside this dir) | **decisions** - numbers, rules, mechanisms | sequencing, status |
| `internal/plans/` | **sequencing** - what order settled decisions get built in | decisions |
| `internal/worklog/` | **what actually landed**, newest first, monthly | intent |
| `internal/vanilla/` | a map of the vendored engine source at `.compat/vintagestory/` | our own code |
| `internal/research/` | **dated read-only snapshots of the code** - what a research agent found on one day, kept so the next session reads a map instead of re-deriving it | decisions, status |

Working scratch - measurements, layouts - lives at the repo-root [`workbench/`](../../workbench/)
alongside the editable shapes and textures it describes.

If a number, a rule or a mechanism is settled, it is settled on a **design page** - never in a plan,
never in a code comment alone.

## Rules that came out of losing a plan

1. **A plan file states its own status.** Put it in the header, and update it when a stage lands.
   Reconstructing completion from worklog prose is how a finished stage reads as unstarted.
2. **A completed plan is marked done, not deleted.** It carries the reasoning behind what shipped,
   and that reasoning is the expensive part.
3. **A superseded plan says what superseded it**, in its header, before anything else.
4. **Cite by symbol, not by line.** Line numbers here were right on the day they were written.
   13.6% of the repo's doc citations already point past end-of-file.

## Index

### plans/

| File | What | Status |
|---|---|---|
| [NEXT.md](plans/NEXT.md) | the entry point - one paragraph on what is in flight, and a link | live |
| [2026-09-04-roadmap.md](plans/2026-09-04-roadmap.md) | **the plan of record** - state per loop, assessment, phases 0-5, the open questions Q0-Q7 | live · **awaiting the owner's answers** |
| [STATE.md](plans/STATE.md) | status and open decisions; the blocker table; the ladder | live · refreshed 2026-09-04 |
| [2026-09-04-phase1-walkthrough.md](plans/2026-09-04-phase1-walkthrough.md) | the in-game walk of every unwalked iron-loop station, with the sixteen defects the code reading predicts | **ready** - the owner walks it |
| [2026-09-04-machining-line.md](plans/2026-09-04-machining-line.md) | roadmap Phase 2: one `machinetool` blocktype over eight stations, window, hold-to-operate, blanks, tools, job tables, the mill's raise/lower cells | **ready** - executes after the walk |
| [2026-09-05-exlib-framework-assessment.md](plans/2026-09-05-exlib-framework-assessment.md) | exlib assessed as a generic framework for other modders: what it brings, what blocks adoption, fifteen ranked items, the hardening plan's open F-items re-verified | proposed - awaiting rulings R1-R6 |
| [2026-09-05-exlib-testing-assessment.md](plans/2026-09-05-exlib-testing-assessment.md) | the testing harness and the three suites assessed as a generic kit for VS mods: patterns proved, what our mods and the reference mods still need tested, thirteen ranked items | proposed - awaiting rulings TR1-TR4 |
| [2026-09-05-exlib-framework-plan.md](plans/2026-09-05-exlib-framework-plan.md) | boundary, loud failures, adoption layer, JSON megablock: stages A-F, one implementer per task | complete 2026-09-06, committed in `bbf22691` |
| [2026-09-05-exlib-convenience-plan.md](plans/2026-09-05-exlib-convenience-plan.md) | the convenience layer: state by attribute, block-entity lookups, interaction guards, info lines, the family adopts ExBlockState | complete 2026-09-06, committed in `bbf22691` |
| [2026-09-05-exlib-testing-plan.md](plans/2026-09-05-exlib-testing-plan.md) | the harness as a generic kit: lifted rigs, doubles, template, smoke lane, Harmony and packet fixtures, exlib-verify | complete 2026-09-06, committed in `bbf22691` |
| [2026-09-06-repo-restructure.md](plans/2026-09-06-repo-restructure.md) | **the rulings of record** for the multi-repo split: four packages, latest-TFM-only, trusted publishing, the sibling layout, the tools submodule, order of work and traps | ruled 2026-09-06, not started |
| [2026-09-06-the-split.md](plans/2026-09-06-the-split.md) | exlib and exmods become sibling repositories under a private workspace: what goes where, the dual-mode switch, the memory stores, the gates in both modes | written 2026-09-07, not started (restructure step 5) |
| [2026-09-06-extools-repo.md](plans/2026-09-06-extools-repo.md) | the CLI, the packaging build and the verify tool in their own repository, consumed through two checked-in wrappers and a pinned version | written 2026-09-06, not started (restructure step 4) |
| [2026-09-06-exmod-manifest.md](plans/2026-09-06-exmod-manifest.md) | exmod reads a repo from exmod.json: resolvers in the dispatcher, the code emitter replaced by the suites' own switch, the verify tool standalone, Cake manifest-driven, dependency mods provisioned from a sibling or a release | complete 2026-09-07 (restructure step 3) |
| [2026-09-06-package-tidy-and-plumbing.md](plans/2026-09-06-package-tidy-and-plumbing.md) | what ships in the ExpandedLib package (build plumbing, generators), central versions, the sample in dual mode, repo-wide guards scoped per mod, consumer InternalsVisibleTo retired | complete 2026-09-07 (restructure step 2) |
| [2026-09-06-exlib-module-system.md](plans/2026-09-06-exlib-module-system.md) | the module system as exlib's extension mechanism: module identity and host, dependency order, full phase set and registries per module assembly, definition contributors, a sample module shipped as its own mod | complete 2026-09-07 (restructure step 1) |
| [2026-09-06-exlib-industry-split.md](plans/2026-09-06-exlib-industry-split.md) | the family domain layer becomes its own assembly and package; the engine's one-ModSystem-dll-per-folder rule turned the runtime half into a companion-assembly capability (`IExModule`) | complete 2026-09-06, uncommitted |
| [2026-08-23-cornish-boiler-megablock.md](plans/2026-08-23-cornish-boiler-megablock.md) | the boiler as a self-contained megablock | **built 2026-08-23** · CB10 follow-through open |
| [2026-08-15-item-piles.md](plans/2026-08-15-item-piles.md) | the pile-placement foundation (reheat hearth only, since the rack ruled length) | written, nothing started · parked |
| [2026-08-15-mid-gap-crop.md](plans/2026-08-15-mid-gap-crop.md) | a crop may yield stock, guarded | written, nothing started · parked |
| [2026-08-13-framework-hardening.md](plans/2026-08-13-framework-hardening.md) | exlib as a published library, plus stage **M** (the mod merges) | live · M.0-M.6 done; F1.4, F4, F5, F6.1, F7, F8, M.8 open (parked) |
| [2026-08-14-m4-iiex-merge-execution.md](plans/2026-08-14-m4-iiex-merge-execution.md) | the `iiex` merge, stage by stage | **executed 2026-08-14** |
| [2026-08-14-m5-siex-merge-execution.md](plans/2026-08-14-m5-siex-merge-execution.md) | the `siex` merge, stage by stage | **executed 2026-08-14** · records three things the plan got wrong |
| [2026-08-12-extensibility.md](plans/2026-08-12-extensibility.md) | third-party extension of our processes | **done 2026-08-12** |
| [2026-08-10-backport-and-vanilla-backlog.md](plans/2026-08-10-backport-and-vanilla-backlog.md) | § A backport (58 open) · § B vanilla practice (12 open) | live · parked; verify each row before acting |
| [2026-08-04-iwex-u2-u10-expansion.md](plans/2026-08-04-iwex-u2-u10-expansion.md) | U2-U11, 87 tasks / 707 steps | **closed 2026-08-22** - every unit landed; a record |
| [2026-08-04-iwex-completion.md](plans/2026-08-04-iwex-completion.md) | U1 remainder, Global Constraints, Commands | record; the Global Constraints and Commands blocks are still the house rules |
| [iwex-bringup.md](plans/iwex-bringup.md) | the art queue and the playtest gates | **superseded 2026-09-04** by the roadmap's Phase 1 |
| [2026-08-05-iwex-plan-audit.md](plans/2026-08-05-iwex-plan-audit.md) | design-vs-plan findings for unstarted units | **closed 2026-09-04** - no unit it audited is unstarted |
| [2026-08-05-iwex-plan-coherence.md](plans/2026-08-05-iwex-plan-coherence.md) | plan-internal coherence findings | **closed 2026-09-04** - same reason |
| [2026-08-10-framework-composition-staging.md](plans/2026-08-10-framework-composition-staging.md) | A0-A4 sequencing | **done 2026-08-12** |
| [2026-08-10-a0-lifecycle-fixes.md](plans/2026-08-10-a0-lifecycle-fixes.md) | A0 task detail | done |
| [2026-08-10-a1-network-membership-behaviour.md](plans/2026-08-10-a1-network-membership-behaviour.md) | A1 task detail | done |
| [2026-08-11-a3-form-consolidation.md](plans/2026-08-11-a3-form-consolidation.md) | A3 task detail | done |
| [furnace-and-machine-rebalance.md](plans/furnace-and-machine-rebalance.md) | the 0.9 rebalance plan of record | executed 0.9.7; the furnace core has since been rewritten (U2-U10) |

A2 and A4 have no plan document. They were executed directly, which is why the paper trail thins
out after A1 and why the arc reads as unfinished from the outside.

### The other trees

- [worklog/](worklog/) - `2026-07.md`, `2026-08.md`. Newest first; each entry is roughly what a
  commit message would have said.
- [vanilla/](vanilla/) - where every engine type lives, the practices vanilla follows, the traps its
  source hides. Two things inside `vsapi` that nothing else documents: `docs/api/` is the full
  generated API reference and `docs/json-docs/` is the **JSON asset schema**.
- [research/](research/README.md) - the 2026-09-04 station and machining-line snapshots that ground the
  Phase 1 walkthrough and the Phase 2 plan.
