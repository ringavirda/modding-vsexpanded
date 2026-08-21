# Rivet machine (riveter)
**Status** ★★ **BUILT 2026-08-21** — block, block entity, footprint, shape, die, item, recipe and tests.
Not walked in game.   **Mod** iiex (`IronIndustryExpanded`)

**Owns**
* this bench: rivet rod in, rivet bundles out, its footprint, drive contract, verbs and drops;
* the rivet item's mass and the yield that makes the rivet route worth walking.

**Depends on**
[mp-energy](../mechanics/mp-energy.md) (the run it loads) ·
[machining line](../mechanics/machining-line.md) (the station family it belongs to, and the die contract) ·
[fasteners](../items/fasteners.md) (where rivets are spent, and the substitution rule) ·
[shear](shear.md) (which cuts the rivet rod it eats) ·
[nail machine](nail-machine.md) (its twin — the same class, a different die)

---

## Role

The riveter is where the forming line stops being a chain of half-products. A rolled rod taken down the
grooved branch to 1.0 is cropped into four rivet rods at the shear; each rod is upset here into two rivet
bundles. Rivets are what a joint that must be **tight** is made with, so this bench is the gate on every
pressure vessel in the suite — the two boilers accept rivets and nothing else.

It is not the [heading machine](heading-machine.md), which was designed to cover rivets, bolts and bearing
balls with one bench and a die catalogue. Bolts were struck, the rivet got its own machine, and what is left
of the heading bench is bearing balls.

---

## Structure

One blocktype with the [nail cutter](nail-machine.md), separated by a `type` variant — the owner's ruling
that the machining machines are the same machine with different shapes, cells and recipes. Six cells, an
`xy` elevation from `docs/internal/workbench/machines.txt`:

```
# M #
I O #
```

| | |
|---|---|
| `O` | the principal, and the `mpenergy` node |
| `I` | the working face: feed a rod, fit or take a die |
| `M` | the cell the drawn shaft runs through |
| `#` | plain filler |

⛔ **The drive connector sits on the principal, not on `M`.** `BEBehaviorMPFillerPort` is a vanilla-MP
intake — the flywheel's bridge — and there is no shipped mpenergy equivalent for a filler cell, so the
connector stays where the shear's is. Cosmetic rather than functional, and all three benches want re-homing
together when the station family lands.

---

## Operation

| Verb | Where | Effect |
|---|---|---|
| RMB with a die | anywhere on the machine | fits it, swapping out whatever was there |
| RMB with a rivet rod | the working face | starts a stroke |
| Sneak + RMB empty-handed | the working face | takes the fitted die back |
| RMB with a wrench | anywhere, mid-stroke | frees a stuck blank, unchanged |

A stroke is drawn from a turning run: a run that stops holds the press where it is and resumes when the run
does, so nothing is lost. The blank is converted **whole** — no remainder, which is what makes this a
terminal station rather than another rung of the ladder.

**A die names the job and the machine.** A nail die fitted here has no work, and is refused rather than
quietly doing the nail cutter's job. That is also the extensibility seam: a third party adds a bench job by
shipping a die, with no table of ours to patch.

---

## Numbers

| Figure | Value | Why |
|---|---|---|
| input | `iiex:rivetrod`, 25 u | four off a rolled rod, grooved to 1.0 |
| output | `iiex:rivet` × 2 | mass-neutral: 2 × 12.5 u = 25 u |
| rivet bundle | **12.5 u** | half a nail bundle — the whole of the substitution trade |
| yield | **8 rivets per 100 u** of iron | against the nail route's 4 |
| `minTorque` | 0.3 | above the nail cutter's 0.2: a press that upsets a head asks more of the run than a cutter that only shears |
| stroke | 3 s | against the nail cutter's 2 |

★ **12.5 u is fractional on purpose.** The quantum is fixed by what feeds it, and rounding to 12 or 13 would
mint or lose metal on every stroke. The ledger is worth more than a round number.

★ **The yield advantage is what the riveter is for.** It costs a second machine and a longer schedule at the
mill, and it pays that back in fasteners per unit of iron. The corollary is that the
[nail cutter](nail-machine.md) survives on **build cost alone** — which is why its recipe is deliberately
the cheaper of the two.

---

## Drops

Breaking the bench returns the fitted die and any blank under the press. Neither is destroyed with the
machine; a blank mid-stroke was never converted.

---

## Code

| Piece | Where |
|---|---|
| `BlockFastenerBench` | `BlockStructures/Forming/Blocks/` — shared with the nail cutter, `type` variant |
| `BlockEntityFastenerBench` | `BlockStructures/Forming/BlockEntities/` — reads its machine key off the variant |
| `BlockEntityMpBench` | the shared base: membership, stroke clock, torque reads, ejection |
| `BenchFeed` / `BenchVerdict` | the pure decision — die, job, turning, drive |
| `BenchDieItemDefinitions` | both dies, through `ItemDie.Itemtype` |
| `FastenerItemDefinitions` | the rivet |
| tests | `test/IronIndustryExpanded.Tests/Blocks/Forming/FastenerBenchTests.cs` |

---

## Open

| # | Work | Size |
|---|---|---|
| 1 | ⛔ Nothing has been seen in game — the shape, the stroke animation and the working face are all unwalked | - |
| 2 | The drive connector on `M` rather than the principal, once mpenergy has a filler-cell connector | medium |
| 3 | The station window and hold-to-operate, which `machines.txt` specifies for this bench and which no station has yet | medium |
| 4 | A handbook page. The forming shop has none, so neither bench is discoverable in game | small |
| 5 | Die wear. `ItemDie` gives every die `MaxStackSize(1)` so it *can* carry wear, and nothing wears it yet | small |
