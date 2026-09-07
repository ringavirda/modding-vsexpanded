# Wire extruder
**Status** deferred   **Would live in** `elex` (Electrical Expanded — no project, no asset domain, no code)   **Deferred by** **D7** ([STATE.md](../../../../../docs/plans/STATE.md)) — *"Wire. Exists, in elex."* — and with the mod, by **D8**

**Owns**
* the D7 placement record — that wire is one machine at one tier, and the two prior rejections D7 reconciles rather than overturns;
* what wire is for — cables and alternator windings, both elex-only;
* the live code leftover — `iiex:wirerod-iron`, a roll-set output for an item that does not exist, on a gap that was retired.

**Does not own** — cited only, never restated:

| Fact | Owner |
|---|---|
| the cut, the release target, the non-ferrous deferral | [scope](../../scope.md) |
| the cable / heavy-cable / winding hardware the wire feeds, and the extruder's own row | [electrical grid](electrical-grid.md) |
| the rolling mill, its roll sets, gaps and `δ_max` rule | [rolling mill](../../machines/rolling-mill.md) · [roll sets](../../items/roll-sets.md) · [rolling](../../processes/rolling.md) |
| the steel-tier roll sets and the rejected `wire` set | [steel roll sets](../../machines/steel-roll-sets.md) |
| the grooved set's *iron* products and the rivet decision that took its 1.0 gap | [fasteners](../../items/fasteners.md) |
| copper as a material | [materials.md](../../materials.md) |
| the MP-energy network the machine would draw on | [mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md) |
| the cable, the circuit solve and what resistance does to a run | [electrical grid](electrical-grid.md) |
| the windings its pure wire unlocks | [alternator](alternator.md) |

**Depends on** [scope](../../scope.md) · [electrical grid](electrical-grid.md) (its output *is* the grid's conductor) · [alternator](alternator.md) · [electrolysis cell](electrolysis-cell.md) (makes the pure copper it draws) · [arc furnace](arc-furnace.md) (the load at the far end of the cable) · [mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md) · [roll sets](../../items/roll-sets.md) · [stock](../../items/stock.md)

---

## What it is

A **wire machine**: copper rod or plate is pulled down to wire through a series of shrinking dies, each pass reducing the section a little. Historically this is a **draw bench** - a chain or a rack hauls the rod through the die.

The archived elex spec calls the block a wire extruder, a megablock, MP-driven:

| Field | Value |
|---|---|
| Block type | megablock |
| Power | MP - needs iiex MP, not electric |
| In → out | copper rod / plate → wire, impure or pure |
| Payoff | pure wire lowers cable R and unlocks alternator windings |

"MP, not electric" is a bootstrap requirement, not an oversight. An electrically-driven wire machine could never make the first cable, because there would be no cable to deliver the electricity. It is the same shape as the impure-copper dynamo that exists only to refine the first pure copper - the electric tier has to be entered on mechanical power.

---

## Why it is deferred

Every consumer of wire is in elex - cable, heavy cable, inset cable, transformer coils, alternator windings - so the machine is deferred with its consumers, by D8's release target ([scope](../../scope.md) § The release target).

D7 reconciles two rejections that had already reached the same conclusion from opposite ends:

| Prior ruling | What it said | Status after D7 |
|---|---|---|
| iiex rejects wire at the iron tier | *"Wire / wire-rod / draw bench — Explicitly elex-era"* | stands - it rejected wire at the iron tier, not wire; the record now lives at [steel roll sets](../../machines/steel-roll-sets.md) |
| the smex spec proposed a draw-die attachment on the rolling shop | *"useful only once there is an electrical grid to consume the wire, so effectively an elex-era attachment"* | removed - it was a duplicate of this machine, and it went with the archived smex spec |

Both rejections agreed on the reason - no grid, no consumer - and disagreed only on where to put the machine. D7 puts it in elex.

One clause of iiex's original rejection is stale: it rejected wire because *"elex is gated on deferred chemistry"*. [scope](../../scope.md) downgraded that gate to a degraded path, and D8 made the deferral a release-scheduling decision instead. The rejection survives either way - wire has no iron-tier consumer - but do not cite that clause as if it were still the argument.

---

## What exists today

No block, no BE, no shape, no lang key, no item. The grep is not empty, though: there is one live reference, and it is a bug.

### `iiex:wirerod-iron` is a roll-set output for an item that does not exist

```
$ grep -rn "wirerod" src/ assets/ test/
mods/iiex/src/BlockStructures/Forming/RollSetItemDefinitions.cs:90:      [Out(1.0, "game:rod-iron"), Out(0.5, "iiex:wirerod-iron")],
mods/iiex/tests/goldens/iiex/itemtypes/rollset.json:96:            "code": "iiex:wirerod-iron"
```

Two hits, and neither is a definition. The grooved roll set names `iiex:wirerod-iron` as its 0.5-gap product; nothing anywhere emits an item with that code. Three rulings have already passed over it:

| Fact | Consequence |
|---|---|
| wire-rod is rejected at the iron tier ([steel roll sets](../../machines/steel-roll-sets.md)) | the output should not exist at all |
| the 0.5 gap itself is retired by decision, still live in three roll sets ([roll sets](../../items/roll-sets.md)) | the gap carrying it is dead weight too |
| roll-set validation only checks that a spec parses (`mods/iiex/src/BlockStructures/Forming/RollSetValidation.cs:20-33`) | a dangling output code raises nothing - the mill would simply produce nothing at that gap |

The leftover is baked into a golden (`rollset.json:96`), so removing it is a golden update, not a silent edit.

### What *is* there — the framework, not the content

| Piece it would need | Status | Where |
|---|---|---|
| an MP-energy consumer contract | live | `mods/exlib/src/Networks/MpEnergyNodes.cs` (`IMpEnergyConsumer`); the network is live in exlib + iiex ([mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md)) |
| tooling that carries its own spec (a die series, as roll sets do) | live idiom | `RollSetSpec.cs:9-24`, fitted via `BlockEntityRollingMill.TryFitRollSet` (`BlockEntities/BlockEntityRollingMill.cs:143`) |
| a stock item family with per-stage meshes | live | `StockItemDefinitions.cs`, `StockMesh.cs` ([stock](../../items/stock.md)) |
| copper as a material | absent | `grep -rni copper src/ --include=*.cs -l` → 3 files, all vanilla-facing plumbing |

---

## The design as it stands

Beyond the four spec fields above, what has been worked out is the placement, stated as a chain because each link was decided separately:

```
copper rod/plate ──MP wire machine──▶ wire ──▶ cable / heavy cable / inset cable
                                          └──▶ transformer coil
                                          └──▶ alternator windings (pure only)
```

| Question | Answer on record |
|---|---|
| Which tier? | elex — **D7** ([STATE.md](../../../../../docs/plans/STATE.md)) |
| Which power? | MP (iiex-tier), never electric |
| Which feedstock? | copper rod or plate |
| Two grades? | yes - impure and pure, and the grade is the whole payoff |
| Ferrous wire? | no - rejected, and the freed grooved gap went to rivet stock instead ([fasteners](../../items/fasteners.md)) |

iiex needed a product for the grooved 1.0 gap and explicitly chose rivets over wire: *"Wire is not the answer here … Rivets are in-tier, in-demand and already named as this machine's second die."* The iron tier had an open slot for wire and turned it down.

Feedstock is the unresolved half. The steel-tier roll-set design once listed a `wire` set producing wire-rod from a billet, which is steel wire-rod; the extruder wants copper rod, which is non-ferrous and deferred. So the machine is deferred twice over - once with elex, once with copper - and steel wire-rod has no consumer under D7 either ([steel roll sets](../../machines/steel-roll-sets.md)).

---

## What it would unblock

| Waiting on it | Severity | Why |
|---|---|---|
| **Cable, heavy cable, inset cable** | wall | a grid with no conductor is not a grid; every generator, pole and outlet in the elex hardware set assumes it |
| **Transformer coils** | wall | iron core + copper coil |
| **Alternator windings** | wall - but the binding gate is the metal, not the machine | windings need *pure* copper, which needs the [electrolysis cell](electrolysis-cell.md) |
| **Silicon steel** (Hadfield, 1900) — the period-correct transformer/alternator core | not blocked by wire; queued and unwritten | see [alternator](alternator.md) § Silicon steel |
| **Cold-drawn / thread-rolled bolts** | no | cold work is an elex-era four-high upgrade and was rejected for the iron tier along with cold rolling ([stock](../../items/stock.md)) |
| **Anything in the ferrous release** | none | nothing on `exlib → iiex → iiex → smex → hpex` consumes wire |

---

## Gotchas

* Delete `iiex:wirerod-iron` when the mill is next touched (`RollSetItemDefinitions.cs:90`), together with the retired 0.5 gap. It is an output naming a non-existent item, on a dead gap, for a rejected product, protected by a golden.
* The machine's name is wrong for the process it models. Wire is drawn through dies, not extruded; extrusion presses (Bramah, 1820) were for lead pipe, and lead is non-ferrous and deferred. "Wire extruder" is the archived spec's name and this page does not overrule it - but if the block is ever built, draw bench is both the historical term and the one iiex already used when rejecting it.
* Do not make it electric to "fit the tier". The MP requirement is the bootstrap ([§ What it is](#what-it-is)); an electric wire machine makes the grid unbuildable.
* Wire is not a fastener route. The mod's nails come from plate and its rivets from grooved rod ([fasteners](../../items/fasteners.md)); wire nails are a later technology and were considered and declined. Reintroducing wire must not quietly reopen that.

---

## Open

| # | Question | Notes |
|---|---|---|
| 1 | Rod or plate — or both? | the spec says "rod/plate". Plate implies a slitter first; rod is the single-input version and matches a draw bench |
| 2 | Does it carry die tooling, like roll sets? | the `RollSetSpec` / `MoldSpec` idiom is right there, and a die series is literally a pass schedule. If yes, it inherits the "where does the spec type live" question ([dies](../../items/dies.md):314) |
| 3 | Who rolls copper rod? | the mill's roll sets are iron and steel ([roll sets](../../items/roll-sets.md)); copper stock has no route at all, and it is non-ferrous |
| 4 | Does steel wire-rod survive? | under D7 its only consumer would be a copper machine, so either the set goes or a ferrous wire product returns - and iiex already refused the second ([steel roll sets](../../machines/steel-roll-sets.md)) |
| 5 | Silicon steel: whose is it? | historically the transformer/alternator core material and queued for elex, but it is a steel - nothing is written, anywhere, and the alloy would want an open-hearth heat like every other spec'd grade ([alloys](../../items/alloys.md):104) |
