# Electrolysis cell
**Status** deferred   **Would live in** `elex` (Electrical Expanded — no project, no asset domain, no code)   **Deferred by** **D8** ([STATE.md](../../../internal/plans/STATE.md), release target = the ferrous line) **and again by D8's non-ferrous half** ([scope](../../scope.md) § Non-ferrous) — this is a *copper* machine

**Owns**
* the electrolyte-not-reagent rule in mechanical terms — why a bath that recirculates is a one-time charge and never a supply chain;
* the finding that the acid is already in the base game — `game:acid-full-sulfuric`, cooked from vanilla water + saltpetre + sulfur, present in all three supported game versions — which moves elex's #2 dependency to zero new content;
* the real dependency knot: the cell's *anode* is missing long before its electrolyte is;
* the design that exists for the cell and its bootstrap loop.

**Does not own** — cited only, never restated:

| Fact | Owner |
|---|---|
| the cut, the non-ferrous deferral, the three chemistry severities, the release target | [scope](../../scope.md) |
| the DC circuit model and the generator tiers | [electrical grid](electrical-grid.md) · [dynamo](dynamo.md) |
| what converter copper and pure copper *are* | [materials.md](../../materials.md) |
| the reverberatory furnace and the Pierce-Smith converter that make the anode | [copper reverberatory](../non-ferrous/copper-reverberatory.md) · [Pierce-Smith](../non-ferrous/pierce-smith.md) |
| R1 (single medium per pipe network) | [conventions.md](../../conventions.md) · [pipe network](../../mechanics/pipe-network.md) |
| the medium-agnostic fluid tank | [fluid tank](../../machines/fluid-tank.md) |
| the DC source that powers it, and the AC machine its output unlocks | [dynamo](dynamo.md) · [alternator](alternator.md) · [electrical grid](electrical-grid.md) |

**Depends on** [scope](../../scope.md) · [dynamo](dynamo.md) (the only thing that can power it at first) · [alternator](alternator.md) (what its output unlocks) · [copper reverberatory](../non-ferrous/copper-reverberatory.md) (its anode) · [arc furnace](arc-furnace.md) (the machine on the far end of the copper it makes) · [wire extruder](wire-extruder.md) (its output's first consumer) · [materials.md](../../materials.md) · [pipe network](../../mechanics/pipe-network.md) · [fluid tank](../../machines/fluid-tank.md)

---

## What it is

**Copper electrorefining** (Elkington, 1865): an impure cast copper **anode** and a thin pure **cathode** hang in a bath of copper sulphate acidified with sulphuric acid; DC dissolves copper off the anode and plates it onto the cathode, leaving the impurities behind as anode slime. The product is ~99.95 % copper - the only copper with low enough resistance to be worth stringing across a landscape.

In the mod it is one block, DC-powered, one plate in and one plate out.

---

## Why it is deferred

Two independent deferrals land on it:

| Deferral | Ruling | Effect on this cell |
|---|---|---|
| elex is off the release path | D8 ([STATE.md](../../../internal/plans/STATE.md)) - release target is the ferrous line | defers the whole mod, this block included |
| non-ferrous is deferred later | D8's second half, [scope](../../scope.md) § Non-ferrous | defers its input and its output independently of elex |

Both belong to [scope](../../scope.md). The consequence that is this page's: bringing elex forward would not bring this block forward, because its anode is converter copper and its two upstream machines are on the non-ferrous list. It is the one elex block that needs a different deferral reversed.

---

## What exists today

Nothing in this repo; the electrolyte exists in the base game.

### In `src/` and `assets/` — nothing

| Probe | Command | Result |
|---|---|---|
| the cell, and the deferral generally | `grep -rniE "coalgas\|sprinkler\|gasholder\|distill\|retort\|petcoke\|graphite\|electrolys" src/` | 1 hit, a doc comment at `mods/exlib/src/Fluids/IMediumTaxonomy.cs:58` about distillation fractions |
| its grid hardware | `grep -rniE "arcfurnace\|electrode\|dynamo\|alternator\|rectifier" src/ assets/` | 0 hits |
| copper anywhere | `grep -rni "copper" src/ --include=*.cs -l` | 3 files, all vanilla-facing plumbing - `MetalCatalogueLoader.cs`, `MetalToolEmitter.cs`, `IiexConfig.cs` |
| an acid medium | `mods/exlib/assets/exlib/config/liquids.json` | declares four liquids only: `Air`, `Steam`, `Exhaust`, `Water` |

### In vanilla — the electrolyte already ships

| Vanilla fact | Path | Detail |
|---|---|---|
| sulphuric acid is a real vanilla item | `assets/survival/itemtypes/liquid/acid.json:2`, `:8-9` | `acid-full` with `allowedVariants: ["acid-full-sulfuric"]` - an `ItemLiquidPortion`, bucket-carryable, `itemsPerLitre: 100` (`:19`) |
| and it is craftable in survival | `assets/survival/recipes/cooking/acid.json` | cooking-pot recipe: 1 L `waterportion` (`:13-14`) + 1 `saltpeter` (`:20-21`) + 2 `powder-sulfur` (`:26-27`) → 100 portions of `acid-full-sulfuric` (`:32`) |
| both inputs are vanilla and obtainable | `assets/survival/blocktypes/stone/saltpeter.json`; `assets/survival/recipes/grid/blastingpowder.json:5` | saltpetre is a mined stone-ore block; `powder-sulfur` is already a blasting-powder ingredient, from vanilla sulfur ore (`blocktypes/stone/ore-ungraded.json`, `looseores.json`) |
| present in every version the suite builds against | `.game/1.20/`, `.game/1.21/`, `.game/1.22/` | `assets/survival/recipes/cooking/acid.json` exists in all three; the 1.20 copy has the same three ingredients and the same `cooksInto … quantity: 100` |
| vanilla even consumes it industrially | `assets/survival/recipes/cooking/sulfate.json` | acid + `crushed-chromite` → `sulfate-full-chromite`, used by `recipes/cooking/hide-chromium.json` |

[scope](../../scope.md) § elex rates this dependency "probably one small recipe" and predicts the period route - lead chamber, sulfur + saltpetre, both vanilla. The recipe is already written, by the base game, from precisely those two inputs: elex would consume an existing item code.

---

## The design as it stands

### The block

| Field | Value |
|---|---|
| Block type | plain block (not a megablock) |
| Power | DC only, ~7.2 kW each, tunable |
| In → out | impure copper plate (anode) + DC → pure copper plate (cathode) |
| Electrolyte | one-time fill + slow top-up; regenerates |
| Why it matters | pure copper is what unlocks alternators |

### The bootstrap loop it anchors

The cell is the reason the generator tiers exist:

```
impure-copper dynamo  ~7.2 kW  ──▶ powers exactly one cell ──▶ the first pure copper
pure-copper dynamo   ~28.8 kW  ──▶ powers ~4 cells         ──▶ pure copper at scale
pure copper          ─────────────▶ alternators (AC) ──▶ 3-phase ──▶ arc furnace
```

The impure dynamo's whole job is to refine the first batch and then never scale. That one-way gate is why "the shape elex would take with no chemistry" stops at DC only, no alternators ([scope](../../scope.md)).

### The electrolyte, as a mechanic

| Property | Consequence for the mod |
|---|---|
| the bath is an **electrolyte, not a reagent** - copper leaves the anode and arrives at the cathode, the acid is unchanged | consumption is not proportional to output; a cell that has been charged runs indefinitely |
| real losses are drag-out, spillage and slime removal | a slow top-up, i.e. a small trickle rate, not a per-batch ingredient |
| the bath is a liquid held in a vessel, never piped | a bucket-fill interaction, exactly like vanilla's `ItemLiquidPortion` handling |

Keeping the electrolyte a held liquid means:

* no new medium is needed - `mods/exlib/assets/exlib/config/liquids.json` stays at four entries, and R1 is never engaged, because R1 governs pipe networks and the bath is not on one ([conventions.md](../../conventions.md));
* the [fluid tank](../../machines/fluid-tank.md) stays out of it - medium-agnostic or not, no acid main is wanted;
* nothing in the phase-change model is touched.

An acid pipe would be a new medium, an R1 decision and a burst/temperature story. Do not start there.

### The period route, if the vanilla acid is ever rejected

**Lead chamber process (1746)** - burn sulfur to SO₂ with saltpetre as the oxygen carrier, absorb in water. Lead chambers want lead, which is non-ferrous and deferred; the earlier small-scale method used glass bell vessels, and small scale is exactly this case. So even the from-scratch route is one small block, and it never needs a metal the suite does not have.

---

## What it would unblock

| Waiting on it | Severity | Why |
|---|---|---|
| **Alternators, and therefore all AC** | wall | alternator windings are gated behind pure copper; no other source of pure copper exists |
| **Three-phase, and therefore the arc furnace's supply** | wall, transitively | the furnace wants ~86 kW as three synchronised alternators |
| **Long-distance transmission** | degraded | impure copper works; it is high-R, so runs are short and cables run hot |
| **Generator efficiency** | degraded | coil-wire purity sets it: impure ~20 %, pure ~80 % |
| **The whole DC subset** | not blocked | arc furnace + HSS on carbon electrodes, DC only, impure copper wire ([scope](../../scope.md)) |

The cell is a wall for AC and nothing else, and the wall is made of copper, not of acid: pure copper needs converter copper, which is a non-ferrous problem with a non-ferrous answer.

---

## Gotchas

* The anode is the missing piece, not the bath. Converter copper comes from the reverberatory furnace → Pierce-Smith converter chain, both on the non-ferrous deferral list ([copper reverberatory](../non-ferrous/copper-reverberatory.md)). A cell with a free electrolyte and no anode refines nothing. Any plan that "unblocks elex by solving the acid" has solved the wrong dependency.
* The vanilla acid is a cooking-pot recipe, in `recipes/cooking/`, with `perishableProps` that transition the acid into itself (`acid.json:3-9` - it never spoils). Consuming it is free; whether an industrial mod wants its reagent made in a soup pot is a taste question, and the alternative is the one small lead-chamber block above, not a new chemistry tier.
* Do not model the bath as a per-batch ingredient. It is the most likely implementation mistake, because every other consumable in the suite (coke, flux, electrodes, patterns, dies) is one. This one is not.
* Copper has no material rows in code. Converter and pure copper exist only in [materials.md](../../materials.md); `grep -rni copper src/` finds nothing but vanilla-facing plumbing. Two metal identities and a molten-item mapping precede the block.

---

## Open

| # | Question | Notes |
|---|---|---|
| 1 | Does elex simply consume `game:acid-full-sulfuric`? | recommended: yes - it exists in 1.20/1.21/1.22, it is bucket-carryable, and its inputs are exactly the period ones. The alternative is a small lead-chamber block for flavour |
| 2 | Bath charge and top-up numbers | litres per cell, litres per hour of operation. Nothing on paper fixes them; the only constraint is that top-up must be far smaller than the charge, or the "not a reagent" argument collapses in play |
| 3 | Does the cell ever want a liquid *network*? | recommended: no. Keeping it a bucket fill keeps `liquids.json` at four entries and R1 uninvolved |
| 4 | Is there a purity/efficiency curve, or a binary? | impure plate → pure plate is currently binary; a slower plate-out on dirtier anodes would be R5-shaped, and the DC circuit already models power delivery finely enough to drive it |
| 5 | Where does the anode slime go? | R2 is "declared recovery, nothing hidden" ([conventions.md](../../conventions.md)). Real slimes carry the precious metals; the mod has no consumer for them, so the honest options are "declare it and drop nothing" or a slag-family by-product |
