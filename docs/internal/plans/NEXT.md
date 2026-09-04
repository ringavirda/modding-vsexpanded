# NEXT — the single "what next" entry point

**Status** live — reset 2026-09-04. Everything this file used to carry (the merge record, the
forming-line order, the U-unit log) lives in [the worklog](../worklog/2026-08.md) and in git history
(`git show 1060b7b0:docs/internal/plans/NEXT.md`). This page is one paragraph and a link, and stays that
way.

## Now

**Phase 1 of [the roadmap](2026-09-04-roadmap.md) — walk the iron loop in game.** The script is
written: [2026-09-04-phase1-walkthrough.md](2026-09-04-phase1-walkthrough.md), with sixteen defects the
code reading already predicts at its top; the four that blocked casting and shingling (P1-P4) were
fixed on 2026-09-05, uncommitted, and want confirming in game. The walk needs the owner in the client.
The Phase 2 plan is written too ([2026-09-04-machining-line.md](2026-09-04-machining-line.md)) and
waits for the walk's fix list. Open for the owner: the casting bed draws its rows opposite its fillers
(P5, confirmed by the new footprint guard, unfixed); the per-mod layout proposal
([2026-09-05-per-mod-layout.md](2026-09-05-per-mod-layout.md)); Q6 (indicator readouts).

## Then

Phase 2, the machining line, all stations at once — roadmap items 6–17, plan
[2026-09-04-machining-line.md](2026-09-04-machining-line.md); the ladle (item 18, U11) fits any gap.

## Rules

[../README.md](../README.md) owns the directory rules. In one line: `docs/design/**` owns decisions,
this directory owns sequencing, `../worklog/` owns what landed. A unit is not done while the roadmap
still shows it as next.
