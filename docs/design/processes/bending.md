# Bending

**Status** designed - nothing built and nothing drawn. No block, no block entity, no tooling item, no recipe,
no shape, no lang key. A repo-wide grep of `src/` for `bend`, `roller` or `conical` returns only pipe bend
segments. Neither of its inputs (`skelp`, `boilerplate`) exists as an item.
Three of its four outputs do exist, but as cast parts rather than bent ones: `iiex:cast-barrel`,
`iiex:castshell` and `iiex:castwheelsection` all ship out of the casting cell. What is missing is the
fabricated route to a part that already exists, so the target item, its mass and its consumers are settled.
`castshell` and `castwheelsection` are pinned at exactly the 600 u their bent equivalents below are costed at,
so the two are alternatives rather than tiers ([cast parts](../items/cast-parts.md)). (`cast-barrel` is 200 u
and predates the rule.)
**Mods** iiex (the [bending roller](../machines/bending-roller.md), and the boiler shells that justify it) ·
hpex (rolled pipe, the tier that consumes the pipe route) · smex (the cheap steel plate that makes it worth
doing at volume)

## Owns

* curvature as a process: bending walks curvature up in passes exactly as rolling walks thickness down in
  gaps, the same shape of loop with opposite signs;
* the decision that bending is cold, and the consequence - the only forming operation in the suite with no
  heat clock, and therefore the only multi-pass loop a player may walk away from mid-schedule;
* the ruling that the work piece gains a curvature axis rather than the roller being a stage-in / item-out
  station, and the argument that multi-pass requires it;
* there is no welding verb. A seam is closed by the recipe (rivets are an ingredient) or by the act of placing
  the block. Nothing in the suite ever asks the player to weld;
* the four routes as a process - pipe from skelp, shells from plate, barrels from plate, rims from bar - with
  their inputs and what each closes;
* the two arithmetic holes that block the pipe route today.

## Does not own - cited only, never restated

| Fact | Owner |
|---|---|
| the machine - three rolls in a triangle, why it cannot be a roll set, its footprint, drive contract, tooling, verbs, drops and build list | [bending roller](../machines/bending-roller.md) |
| the reduction model - `δ_max = μ²R`, spread, elongation, cooling, `RollSetSpec`, `WorkPiece` as it ships, every `Rolling*` key | [rolling mill](../machines/rolling-mill.md) |
| the schedules that produce the plate and the skelp, and the feed arithmetic | [rolling](rolling.md), [wide hall](../machines/wide-hall.md) |
| the crop that cuts plate to length before it is bent | [shear](../machines/shear.md) |
| the rolled pipe tier - its blocktypes, its 12 atm rating, the welded joint family, B5 | [rolled pipe](../machines/rolled-pipe.md) |
| pipe tiers, burst, joint families, the one-medium rule | [pipe network](../mechanics/pipe-network.md) |
| the cast originals this route substitutes for, and the cast-vs-forged rule | [cast parts](../items/cast-parts.md), [casting cell](../machines/casting-cell.md) |
| the substitution loop the bent parts feed | [fabrication](fabrication.md) |
| the rivet, and the bench that heads it | [heading machine](../machines/heading-machine.md) |
| `1 vx³ = 2.5 u` and every mass below | [density rule](../mechanics/density-rule.md) |
| the energy model and every `Mp*` key | [mp-energy](../mechanics/mp-energy.md) |
| the ≤ 32 / ≤ 48 handling invariant | [recoverability](../mechanics/recoverability.md) |

**Depends on** [bending roller](../machines/bending-roller.md) · [rolling](rolling.md) ·
[fabrication](fabrication.md) · [rolled pipe](../machines/rolled-pipe.md) ·
[pipe network](../mechanics/pipe-network.md) · [shear](../machines/shear.md) ·
[density rule](../mechanics/density-rule.md)

---

## What it is

Plate bending rolls. Three rolls in a triangle: two below driving the plate, one above pressing down on it. The
plate goes through, comes out curved and the same thickness, and goes through again with the top roll set
lower. Repeat and the curve tightens until the ends meet.

Two rolls cannot bend. A rolling mill is a two-high stand - one gap, and the piece comes out thinner. The third
roll is the entire mechanism, which is why this is a separate machine and not a roll set
([bending roller](../machines/bending-roller.md) § Role).

What the mod abstracts away:

| Real practice | Here | Why |
|---|---|---|
| pinch rolls, pyramid rolls, initial-pinch machines | one machine, tooling selects the geometry | four jobs off one block is what justifies building it; pipe alone never did |
| springback - a plate rolled to radius R relaxes to R + δ | none | it would be a second hidden number with no readout, which is R7 |
| pre-bending the flat ends, then a separate closing pass | none | the closed seam is a product state, not a stage |
| a seam weld: fire-welding, forge-welding, or a riveted lap | no welding verb at all - see below | welding is a skill, not a machine, and the suite has no machine for it |
| a hinged end housing to lift a closed cylinder off the roll | art only | draw it; it is what makes the machine read as a bender |
| bending sections (angle, channel, rail) | out of scope - rail, I-beam, angle and channel are rejected outright | zero consumers; `castframe` owns the I-section role |

### Curvature up, thickness down

| | [Rolling](rolling.md) | Bending |
|---|---|---|
| what changes | thickness, down | curvature, up |
| what is preserved | nothing - width and length both grow | everything: same thickness, same width, same length |
| the schedule is | a fixed sequence of gaps cut into the barrel | a sequence of passes, each tighter than the last |
| the clock | hot - the piece cools whether or not it moves | cold - there is no clock |
| the limit | `δ_max = μ²R` - a bite too deep skids | unchosen; the analogue is a curvature step too tight to take in one pass |
| the stopping rule | the player stops at the stage they want; the shear crops it | the seam closes, and that is the stop |

It is the same loop with the sign flipped, which is why it shares the block-entity base, the tooling item and
the `mpenergy` consumer contract with the mill - and shares none of `RollingPass`, every relation of which is a
function of thickness change.

### Cold, and that is a design statement

Bending plate cold is the period-normal case: a boiler shell was rolled cold and the plate was only heated if
it was very thick. It also falls out of the machine - three rolls grip the plate between them, so unlike a
two-high stand there is no friction problem to solve with heat.

A cold multi-pass schedule can be put down: walk away, come back, finish the shell. Every other forming loop in
the suite races a cooling piece, so this is the first cold-forming machine in the suite.

It is unbuilt and unwritten: nothing in code or config gates any machine on cold, and the mill's `RollingTempC`
(`IiexConfig.cs:490`) is the only temperature threshold the forming line has.

### Welding is not a mechanic

Three of the four routes end in a joint, and none of them is a verb:

| Joint | How it is closed | Where the joining is expressed |
|---|---|---|
| a bell-welded pipe segment | the segment is a pipe when it is placed in a run | nothing; placement is the join |
| a riveted shell or barrel seam | rivets are an ingredient of the assembly recipe | the recipe / RCC stage |
| a rim onto spokes and a hub | rivets, plus a recipe | the recipe |

Rivets-as-ingredient is what avoids a riveting machine: the joining is abstracted into the bill, so a
fabricated part simply costs rivets. A powered riveter stays available later as a pure throughput upgrade, and
nothing is blocked without one.

There is no welding heat, no weld tool, no flux and no failed-weld state. Adding one would mean a fifth verb
with no machine behind it, and the two places a seam matters - pressure tightness and structural strength - are
already expressed by which fastener the recipe asks for (nails and bolts are strong; rivets are strong and
tight).

---

## The loop

```
wide rolling ──▶ plate / skelp ──▶ shear (to length) ──▶ bending roller
                                                              │ fit tooling
                                                              │ pass 1 … pass n   (cold, no clock)
                                                              ▼
                                                    a formed part, seam closed
                                                              │
                                       ┌──────────────────────┼───────────────────┐
                                       ▼                      ▼                   ▼
                              pipe segment            castshell / cast-barrel   bent rim
                              → place it              → + rivets, recipe        → + spokes, hub, rivets
```

| # | Where | Player verb | What comes out |
|---|---|---|---|
| 1 | [wide hall](../machines/wide-hall.md) or the mill | roll to the 1.0 stand | a 1-voxel plate stage |
| 2 | [shear](../machines/shear.md) | RMB on the throat | `skelp` @200 or `boilerplate` @600, and the remainder still stock |
| 3 | [bending roller](../machines/bending-roller.md) | RMB holding a roll set (conical · cylindrical · large radius) | the geometry is fitted; the previous set is handed back |
| 4 | the feed side | RMB with the plate | one pass: the piece comes out curved, same thickness. No heat is spent |
| 5 | - | repeat | curvature climbs, pass by pass |
| 6 | - | the last pass | the seam meets; the piece becomes a named formed part |
| 7 | a recipe / RCC stage, or simply placing it | - | shell, barrel, rim or a length of pipe |

Steps 4-6 are not designed. How many passes, what a pass costs, and what the readout is are all open - see
[Numbers](#numbers).

---

## Inputs and outputs

Every mass is the [density rule](../mechanics/density-rule.md)'s; every input is a
[rolling](rolling.md) product.

| Tooling | In | Mass | Out | Consumer |
|---|---|---|---|---|
| conical | `skelp` 8 × 1 × 10 | 200 u | rolled-pipe segment → a length of rolled pipe | [rolled pipe](../machines/rolled-pipe.md) - hpex's 12 atm tier |
| cylindrical | `boilerplate` 15 × 1 × 16 | 600 u | boiler barrel; `castshell` substitute | Cornish / Lancashire boilers · cistern · crusher casing |
| cylindrical | `boilerplate` / plate | 600 / 200 u | `cast-barrel` substitute | the molten barrel · the fluid tank |
| large radius | bar / `heavyplate` 12 × 2 × 10 | 600 u | bent rim → `castwheelsection` substitute | flywheels · wheels |

Every row is blocked. `skelp`, `boilerplate`, `heavyplate`, `castshell` and `castwheelsection` do not exist as
items; only `cast-barrel` does, and it is the cast original (`CastPartItemDefinitions.cs:44-54`, 200 u at
`:24`), not the fabricated substitute.

`skelp` cannot currently be produced at all. Its 8 × 1 × 10 section requires a width cap of exactly 8, and no
settled roll-set `MaxWidth` supplies one - see [rolling](rolling.md) § Gotchas. Until that is resolved the pipe
route has no feedstock even on paper.

---

## Numbers

There are none. Not one value for bending exists in config, in code, or in any design document. What follows is
the set of constraints any implementation must satisfy, and the numbers it will be sized against - all of them
owned elsewhere.

### Constraints

| Constraint | Consequence |
|---|---|
| a bent piece keeps its thickness, width and length | volume is conserved trivially; no mass arithmetic is needed, which is why this process mints nothing by construction |
| a bent piece must still respect ≤ 32 / ≤ 48 | [recoverability](../mechanics/recoverability.md) - but a closed shell is not a long piece any more, and nothing says how a curved piece is measured |
| cast iron cannot be bent - it shatters | every input is wrought or steel, which is a second reason the machine cannot be iiex even though its frame is cast |
| the load is a steady draw, not a pulse | derived, not settled: it would be the first `mpenergy` consumer that is not pulsed, and the only one with no reason to carry its own flywheel |

### Sized against - owned elsewhere

| Anchor | Value | Owner |
|---|---|---|
| drive headroom one bridged waterwheel leaves | ≈ 0.4 N·m | [mp-energy](../mechanics/mp-energy.md) |
| a hot fresh-bloom mill pass | ≈ 0.338 N·m | [rolling mill](../machines/rolling-mill.md) § Worked pass |
| network standing resistance | `MpIdleTorque` 0.5 (`ExlibConfig.cs:92`), `MpFrictionCoeff` 0.05 (`:86`), `MpMaxSpeed` 2 rad/s (`:98`) | [mp-energy](../mechanics/mp-energy.md) |
| `RollSetSpec.MinTorque` | declared `RollSetSpec.cs:37`, parsed `:198`, never read | [shear](../machines/shear.md) claims its first use |
| the Lancashire boiler's requirement | 6 × rolled pipe | [rolled pipe](../machines/rolled-pipe.md) |

### Unchosen - all of it

| Quantity | Note |
|---|---|
| passes per closed shell | the single most load-bearing unknown: it is what makes bending feel like a schedule rather than a recipe |
| curvature step per pass, and whether there is a "too tight for one pass" refusal | the analogue of `δ_max`; without it the multi-pass model has no rule |
| `LoadTorque` while bending | must sit inside the headroom above, or the machine becomes a steam-tier build by accident |
| plates per `castshell` / `cast-barrel` / `castwheelsection`, and rivets per part | this is [fabrication](fabrication.md)'s balance question, and it is what decides whether the substitution is real |
| whether a partially bent piece is a legal thing to put down | it must be, or "cold means no clock" buys nothing |
| the small-radius bearing race | named as a requirement by N2 and absent from all four tooling routes |

---

## Why it is like this

Expressing bending as a roll set would have meant a `gaps` array that silently means radius, which is what the
roll-set idiom exists to prevent. The machine that makes boiler barrels, machine shells, molten barrels and
wheel rims is also what turns `boilerplate` from a boiler ingredient into a general feedstock.

Nothing at iron tier bends, which is why it is iiex. iiex's pipes are hammered from flat plates - what the
`plated` tier's name records - and its structural parts are cast, not fabricated. The first thing in the suite
that needs a curved plate is a boiler shell.

---

## Gotchas

* The settled design overrules [bending roller](../machines/bending-roller.md) § Open #1. That page weighs
  "station in / named item out" against "`WorkPiece` gains a curvature axis" and recommends the station. The
  2026-07-29 settlement chose the curvature axis: a station cannot express a multi-pass schedule, because it
  has nowhere to keep the partly-bent piece. Once bending is multi-pass, per-piece state is mandatory.
* The `WorkPiece` simplification it had to wait for **landed 2026-08-12**: the record is now one gauge,
  the gap it is half way through and a flag per side, and the nine per-side members are gone. A curvature
  axis is now an addition to a small record rather than a change to a lopsided one, which is exactly the
  order that was wanted.
* `skelp` has no producible geometry (see [rolling](rolling.md) § Gotchas), so the pipe route's input does not
  merely not-exist, it does not currently fall out of the ladder.
* Making rolled pipe reachable does not make an HP line buildable. The welded joint family means rolled pipe
  joins only rolled pipe, and hpex ships no fittings at all - no valve, no outlet, no passthrough
  ([rolled pipe](../machines/rolled-pipe.md), [pipe network](../mechanics/pipe-network.md)). B5 and this
  process are the same work item; B6 is a different one and is not fixed by it.
* There is no mill roll set that produces pipe. The mill makes the skelp; the curl is this machine's. The
  machine is the bending roller and it is iiex - not a smex "conical pipe roller", and not a `pipe/skelp` roll
  set on the mill.
* No small-radius tooling exists for the bearing race, which N2 requires. Four routes are named and none of
  them is a ring.
* The `cast-barrel` name is already taken by the cast original (`CastPartItemDefinitions.cs:46`). The
  fabricated substitute needs its own code or the pair needs renaming; [fabrication](fabrication.md) owns that
  decision.

---

## Open

| # | Question | Weight |
|---|---|---|
| 1 | How many passes, and what does a pass cost? Without an answer "multi-pass" is a word. It is the difference between a schedule and a recipe with extra clicks | high |
| 2 | How is a curved piece measured against ≤ 32 / ≤ 48? A 32-long plate rolled into a closed cylinder is not 32 long any more. Nothing addresses it | medium |
| 3 | The fabrication balance - plates and rivets per substitute, against the cast original's cupola cost. [fabrication](fabrication.md)'s question, unanswerable without this machine's pass cost | medium |
| 4 | Is the bell-weld a stage, a recipe, or nothing? This page rules that welding is not a verb; what is still open is whether the roller outputs a segment that a recipe closes, or a finished length of pipe | medium |
| 5 | Nothing is built and nothing is drawn - no shape has been chosen, though the 1867 machine-tool plate and the boiler-shop C-frame silhouette are the obvious references | medium |
| 6 | Footprint. The benches are 1 × 1 by design; the pieces here are 15 voxels across and a plate roll is a wide machine | low |
| 7 | Does it need its own flywheel? Every other `mpenergy` consumer is a pulsed load and carries one; a steady draw may be the first that does not | low |
| 8 | A powered riveter is explicitly allowed as a later throughput upgrade and entirely undesigned | low |
