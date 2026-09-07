# Wide hall (the plate-mill train)

**Status** designed - nothing built, and almost nothing needs building. The hall is not a block: it is four
[rolling mills](rolling-mill.md) - iiex's shipped block, unchanged - standing in a row on one drive shaft,
each carrying a different single-gap wide roll set. What iiex must ship is the six wide roll-set items and
whatever the shared-shaft drive needs; three of the six roll shapes are already drawn.
**Mod** iiex (`IronIndustryExpanded`) - the roll sets and the hall as content. The mill block stays iiex.

## Owns

* the hall as a build: that it is *N* ordinary mills on one shaft, that there is no cheap "stand" block, and
  the capital-against-labour trade that keeps re-tooling one mill legal;
* the "entry gap is set by the stock's thickness" rule, and therefore how many stands a tier needs - iiex
  builds four, smex bolts two more onto the front;
* the six single-gap `flatwide` roll sets (1.0 … 3.5, barrel 16, `MaxWidth` 15) as iiex content: the
  catalogue, which three are drawn, which three are missing, and the delete of `flatwide` 0.5;
* the shared-shaft mechanics as they land on the shipped `mpenergy` code - what already works and what does
  not;
* the hall's power profile, and the fact that six idling stands cost exactly what one costs today.

## Does not own — cited only, never restated

| Fact | Owner |
|---|---|
| everything about the mill itself - its 3 × 3 × 2 footprint, the axle-bus cells, placement/break/drops, the pass lifecycle, `RollingPass`'s physics (`δ_max = μ²R`, spread, elongation, cooling), the `rollset` spec format, `WorkPiece`, `StockForm`, every `Rolling*` config key, its blockers and its gotchas | [rolling mill](rolling-mill.md) |
| the energy model, the four node contracts, merge/split, the vanilla-MP bridge, the direction flag, every `Mp*` key | [mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md) |
| the shafts, bevels, transmission and flywheel that make up the run | [flywheel & shafting](flywheel-and-shafting.md) |
| the stock ladder, crop points, cut arithmetic, and every product mass | [rolling](../processes/rolling.md), [density rule](../mechanics/density-rule.md) |
| the ≤ 32 / ≤ 48 handling invariant and the soft-locks | [recoverability](../mechanics/recoverability.md) |
| keeping the stock hot between stands | [reheat furnace](reheat-furnace.md), [heat balance](../mechanics/heat-balance.md) |
| cropping the rolled piece into products | [shear](shear.md) |
| blanking `boilerplate` into plate | [steam hammer](steam-hammer.md) |
| what shingles the slab the hall eats | [steam hammer](steam-hammer.md), [puddling furnace](puddling-furnace.md) |
| what casts the steel stock the extended hall eats | [long cell](long-cell.md), [casting cell](casting-cell.md) |
| the filler footprint system and the no-filler-graph-node rule | [multiblock & fillers](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md) |
| code-first defs, recipes, cost catalogue, goldens | [recipes & config](../mechanics/recipes-config.md) |

**Depends on** [rolling mill](rolling-mill.md) · [mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md) ·
[flywheel & shafting](flywheel-and-shafting.md) · [reheat furnace](reheat-furnace.md) · [shear](shear.md) ·
[steam hammer](steam-hammer.md) · [recoverability](../mechanics/recoverability.md) ·
[recipes & config](../mechanics/recipes-config.md) · [rolling](../processes/rolling.md) ·
[STATE.md § placement rule](../../../../docs/plans/STATE.md)

---

## Role

A wide roll set is one gap: the stock fills the whole barrel, so there is no room to cut a sequence of
segments along it the way the narrow `flat` set does. Reducing a slab is therefore six fixed reductions in
order, which is either one mill re-tooled six times or six mills standing in a row. Both stay legal. Six is
the intended build.

### There is no cheaper "stand" block, deliberately

A simplified fixed-gap variant is rejected. A player who does not want the hall re-tools fewer mills and
pays in handling instead - capital against labour, the trade the whole suite is made of. The hall eats a
3000 u slab in one schedule.

The train needs no new block code at all: six mills is six mills.

### A train abolishes the carry-back

The two-high stand cannot be fed backwards, so every pass on one mill is a walk around it. Stands in a line
share one drive shaft, so "feed side follows drive rotation" points all of them the same way and the stock
only ever moves forward.

The reference is `workbench/refs/rolling/C0229569-Zinc_rolling_mills,_19th_century.jpg` - a hall of
identical stands on one line shaft, the same silhouette as the nail-works photograph behind the
[nail machine](nail-machine.md).

Caution: the shipped code delivers the direction half of this and nothing else - see
[Numbers](#numbers) and [Gotchas](#gotchas).

---

## Structure

There is no hall block, no hall footprint and no hall block entity. A hall is a placement pattern.

| Piece | What it is | Owner |
|---|---|---|
| each stand | one `BlockRollingMill` - 3 × 3 × 2, principal + two axle-bus nodes + nine fillers | [rolling mill](rolling-mill.md) |
| the drive line | the mills' own axle cells, butted end to end, optionally extended by `BlockCastIronShaft` / `BlockCastIronBevel` | [flywheel & shafting](flywheel-and-shafting.md) |
| the reservoir | one flywheel on the run | [mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md) |
| the prime mover | vanilla waterwheel at iron tier, replaced by a steam engine in iiex | [STATE.md](../../../../docs/plans/STATE.md) |

### The stands chain on their own axle bus

The mill is authored in the `we` frame with its axle along X: the principal at `(0,0,0)` and two
`BlockRollingMillAxle` pass-through nodes at `(−1,0,0)` and `(−2,0,0)`
(`BlockRollingMill.cs:74-94`, `:99`). Those axle cells are graph nodes, not fillers, so the three-cell line
is one connected bus, *"drivable from either shaft end and chainable into a manual train"*
(`BlockRollingMillAxle.cs:11-18`, and the class doc at `BlockRollingMill.cs:24-27` says the same).

Stand A's principal exposes an east connector; stand B's westmost axle cell exposes a west connector; they
join with no shaft block in between. Six stands occupy 18 cells along the shaft axis.

### Which way the player actually walks

The mill's two feed decks sit at z = ±1, perpendicular to the shaft
(`BlockEntityRollingMill.cs:248-257`). Chaining along the shaft axis therefore lays the stands side by side,
and the piece crosses each one in z. Walking down the line is a zig-zag: exit stand *n* on the far side,
step along the shaft, re-enter stand *n+1* on the near side. One short step per stand rather than a lap, but
not literally "the stock only ever moves forward".

The alternative layout - stands in a row along z, each fed off a common shaft running alongside through
bevels - gives a straight-line walk, at the cost of a bevel and a shaft run per stand. The choice is open;
see [Open](#open).

---

## Assets

The hall itself needs no art. What iiex must ship is the roll-set items.

| Asset | Path | State |
|---|---|---|
| wide roll art, gap 2.0 | `workbench/shapes/item-rollers-flatwide20.json` | drawn, unwired |
| wide roll art, gap 1.5 | `item-rollers-flatwide15.json` | drawn, unwired |
| wide roll art, gap 1.0 | `item-rollers-flatwide10.json` | drawn, unwired |
| wide roll art, gap 0.5 | `item-rollers-flatwide5.json` | drawn - the settled schedule deletes 0.5 |
| wide roll art, gaps 2.5 / 3.0 / 3.5 | — | must be drawn. 2.5 is iiex's; 3.0 and 3.5 are smex's |
| cast roll blank | `item-rollers-castblank.json` | drawn - the roll is a cast part |
| diagram textures | `workbench/textures/diag-item-rollersflatwide{5,10,15,20}.png` | drawn |
| item shape actually shipped | `game:item/ingot` | every roll set renders as an ingot today (`RollSetItemDefinitions.cs:122`) |

### The drawn gap is literally the modelled gap

Each `flatwide` shape is a pair of 16-long rolls (`FlatDown*` / `FlatUp*`), and the void between them
measures exactly the set's gap in voxels:

| Shape | Down-roll top y | Up-roll bottom y | Gap drawn |
|---|---|---|---|
| `item-rollers-flatwide20.json` | 11.0 | 13.0 | 2.0 |
| `item-rollers-flatwide15.json` | 11.25 | 12.75 | 1.5 |
| `item-rollers-flatwide10.json` | 11.5 | 12.5 | 1.0 |
| `item-rollers-flatwide5.json` | 11.75 | 12.25 | 0.5 |

The barrel is drawn 16 long in every one of them, which is the `barrelWidth: 16.0` the shipped `flatwide`
already declares (`RollSetItemDefinitions.cs:79`). The art and the number agree; what does not agree is that
the shipped item is one four-gap set rather than four single-gap ones
([rolling mill](rolling-mill.md) § the authored roll art).

---

## Construction

There is no recipe for the mill, for any roll set, or for any wide stock. All three are creative-only
(`BlockRollingMill.cs:60`, `RollSetItemDefinitions.cs:127`, `StockItemDefinitions.cs:57`) - the mill's own
blocker, owned by [rolling mill](rolling-mill.md) § Construction.

| Question | Status |
|---|---|
| what one mill costs | open - no recipe exists at all |
| whether four mills is a sane ask at iiex tier | open - the recipe/economy sanity of building four is the unresolved half of forming build item 15 ([rolling mill](rolling-mill.md) § Open) |
| what a wide roll set costs | open - no recipe; the roll is cast (chilled cast iron), so it should come off the sand cell as a `rollers-castblank` and be finished, not forged |
| whether the hall needs its own recipe-cost level | open - [recipes & config](../mechanics/recipes-config.md) owns the catalogue; there is no hall entry |

Caution: a dead cost key `pipe-straight-grid` already ships against a grid recipe that does not exist; do
not add a second one for a hall that is not a block.

---

## Operation

Identical to one mill, four times. Every verb, refusal message, stall rule and wrench recovery is
[rolling mill](rolling-mill.md)'s. The hall changes exactly three things:

| | One mill, re-tooled | The hall |
|---|---|---|
| between gaps | swap the roll set, walk around the stand | walk to the next stand |
| the gap you get | whichever set is fitted | fixed by which stand you are at |
| where you click along the deck | picks the gap band | irrelevant - see below |

### A single-gap set makes the deck-position bug disappear

`MillFeed.GapZone` returns `0` unconditionally when `gapCount <= 1` (`MillFeed.cs:62-65`). Every wide set
has exactly one gap, so the click position along the deck carries no information at all on a hall stand.

The mill's live reachability blocker - B17, only the middle deck cell is an input, so a click can only ever
land in the last third of the barrel and two of four gap bands are unreachable
(`BlockEntityRollingMill.cs:250-260` vs `MillFeed.cs:78-89`; [STATE.md](../../../../docs/plans/STATE.md)) - therefore
cannot bite a hall stand. The wide route is the only route that is not blocked by it.

B17 still blocks the narrow `flat` set and must still be fixed
([rolling mill](rolling-mill.md) § Blockers).

### The schedule, by tier

The schedules themselves - gaps, feeds, stage geometry and crops per stock - are
[rolling](../processes/rolling.md)'s. What this page fixes is the stand count that falls out of them: the
entry gap is set by the stock's thickness, so the 3-thick `shingledslab` enters at 2.5 and uses four stands
(iiex's), and the 4-thick cast stock enters at 3.5 on the two more smex bolts onto the front - six in all.
The upgrade is an extension of the hall, and no stand in the line ever becomes obsolete.

`castbillet` never sees the train - at 3 × 3 it is narrow stock and runs the iiex mill's grooved barrel with
a steel roll set ([steel roll sets](steel-roll-sets.md)).

Nothing is gated on an unreached mod: iiex's train eats iiex's own slabs, so the wide route is complete the
moment steam is available; smex then feeds the same train pieces 2.5× bigger.

### What comes off the end

Which product comes off which stand - `heavyplate`, `boilerplate`, `blank`, `skelp`, the plate crops, and
the claim-it-now-or-roll-on decision at the 2.0 stand - is [rolling](../processes/rolling.md) § the
schedules. The crop is the [shear](shear.md)'s and the stamp is the [steam hammer](steam-hammer.md)'s.

---

## Numbers

### The six wide roll sets — proposed, iiex content

| Gap | Barrel | `MaxWidth` | Ships with | Art |
|---|---|---|---|---|
| 3.5 | 16 | 15 | smex | missing |
| 3.0 | 16 | 15 | smex | missing |
| 2.5 | 16 | 15 | iiex | missing |
| 2.0 | 16 | 15 | iiex | drawn - `flatwide20` |
| 1.5 | 16 | 15 | iiex | drawn - `flatwide15` |
| 1.0 | 16 | 15 | iiex | drawn - `flatwide10` |
| ~~0.5~~ | — | — | deleted | drawn - `flatwide5` |

Every bite in the game is then exactly 0.5, so under the two-round pass model every gap costs the same two
passes per strip and the schedule is uniform.

`MaxWidth` 15 is the roll barrel's usable width and therefore belongs on the roll set, not on the stock -
forming build item 4 moves it there ([rolling mill](rolling-mill.md) § Open). Today it lives on `StockForm`
(`StockForm.cs:32`, `:48`, `:54`). 15 rather than 16 is load-bearing: it puts the shingled slab's last stage
at `480 / 15 = 32.0`, exactly the lengthwise seating limit, and the cast slab's 2.5 stage on 32.0 as well;
at 16 both fall to 30 and land on nothing ([recoverability](../mechanics/recoverability.md)).

The cost of dropping 0.5 is `game:metalsheet`, a vanilla cladding block with no recipe anywhere, which
nothing will now unlock. Reversible by re-adding 0.5 to the wide family alone.

### Shipped, and wrong against all of the above

| Key | Shipped | file:line | Settled |
|---|---|---|---|
| `flatwide` gaps | `[2.0, 1.5, 1.0, 0.5]` - one four-gap item | `RollSetItemDefinitions.cs:76` | six single-gap items, 1.0 … 3.5 |
| `flatwide` barrel | `16.0` | `:79` | 16 - already agrees |
| `flatwide` `minTorque` | `0.5` | `:80` | parsed, stored, validated and never read by any production code ([rolling mill](rolling-mill.md)) |
| `flatwide` accepts | `["slab", "bloom"]` | `:75` | `shingledslab` + the two cast wide forms |
| `flatwide` outputs | `iiex:rolledplate-iron`, `iiex:rolledsheet-iron` | `:77` | neither code exists anywhere in `src/`; `Outputs` moves to the [shear](shear.md) and keys on stage |
| `RollSetSpec.IsWide` | `Gaps.Length == 1` | `RollSetSpec.cs:70` | already the right definition - and it has no callers |

### The hall on the shipped `mpenergy` model

All connected `mpenergy` nodes are one `MpEnergyNetwork` with one `MpEnergyNetworkState`
(`MpEnergyNetwork.cs:21`). One walk per second sums inertia, drive torque and load torque, and sets one
direction flag (`MpEnergyNetwork.cs:53-108`).

| Hall property | What the code does | file:line |
|---|---|---|
| all stands face the same way | `Reversed` is one flag on the run, set by any `IMpEnergyDirection`; every mill reads it through `DriveReversed` → `InputDeck` / `OutputDeck` | `MpEnergyNetwork.cs:76-77`, `:93`; `BlockEntityRollingMill.cs:234-247` |
| load sums | `loadTorque += consumer.LoadTorque(speed)` over every node | `MpEnergyNetwork.cs:72-73` |
| an idle stand costs zero | `LoadTorque` returns `0f` unless `IsRolling` | `BlockEntityRollingMill.cs:314-326` |
| standing friction is per-run, not per-node | `τ_fric = MpFrictionCoeff·ω + MpIdleTorque`, both read once from config | `MpEnergyNetworkState.cs:84`; `ExlibConfig.cs:86` (`0.05`), `:92` (`0.5`) |
| ω_max | `MpMaxSpeed` = 2 rad/s | `ExlibConfig.cs:98` |

A six-stand hall imposes exactly the same standing load as one stand today. Hand-fed, only one stand rolls
at a time, so the peak load is one mill's pass torque (≈ 0.338 N·m hot on a fresh bloom -
[rolling mill](rolling-mill.md) § Worked pass) plus the run's single 0.5 N·m idle torque. A hall is meant to
be a sustained draw that a waterwheel will not carry; the shipped code has no mechanism for that, and the
settled per-consumer idle-draw rule supplies one - see [Gotchas](#gotchas).

---

## Drops

Per stand, and owned by [rolling mill](rolling-mill.md): the principal returns the mill, the stuck piece
and the fitted roll set; an axle cell routes to its own principal and takes that machine only
(`BlockRollingMillAxle.cs:65-88`).

Nothing routes between stands. Breaking stand 3 leaves stands 1–2 and 4–6 standing, and the run fractures at
that cell - the shared graph substrate's BFS fracture detection splits the network and `OnSplitFragment`
divides the reservoir proportionally (`MpEnergyNetwork.cs:133-155`;
[pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) owns the substrate). The half without the flywheel loses its
inertia entirely and is dropped (`MpEnergyNetwork.cs:81-89`). A hall wants its flywheel where a broken stand
cannot orphan the rest, which is build guidance, not a code rule.

---

## Code — what actually has to be written

| Work | Where | Note |
|---|---|---|
| `flatwide` → six single-gap items | `RollSetItemDefinitions.Sets` (`:73-81`) | config only; the mill *"never names a product in code — the tooling owns the data"* ([rolling mill](rolling-mill.md)) |
| `MaxWidth` onto `RollSetSpec` | `RollSetSpec.cs:31`, off `StockForm.cs:32` | forming build item 4 ([rolling mill](rolling-mill.md) § Open) |
| wire the drawn roll art | `RollSetItemDefinitions.cs:122` currently ships `game:item/ingot` | plus three new shapes |
| draw gaps 2.5 (iiex), 3.0 / 3.5 (smex) | `workbench/shapes/` | |
| a recipe for the mill and for each roll set | `IronIndustryExpanded/Recipes/` and iiex's | the hall is four purchases; nothing costs anything yet |
| decide the hall layout axis | — | see [Open](#open) |
| wire the per-consumer idle draw | `IMpEnergyConsumer.LoadTorque` | settled 2026-08-05, owned by [mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md) § Idle draw - see [Gotchas](#gotchas) |

There is no new block, no new block entity and no new footprint.

---

## Gotchas

* An idling stand is meant to cost torque; the shipped code charges none. Settled 2026-08-05, owned by
  [mp-energy § Idle draw](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md): every connected consumer contributes a standing
  torque, so six stands idling on one shaft are a real, sustained draw. The disconnect is a block, not a
  flag: put the branch behind the clutch transmission (shipped: 2 × 2, lever cell, persisted `_engaged`) and
  it stops drawing - a main shaft, branches, and fast-and-loose pulleys to throw an idle machine off the
  line. The shipped code does not implement the rule yet: `LoadTorque` is zero when a mill is idle
  (`BlockEntityRollingMill.cs:314-326`) and friction is a per-network constant, not per node
  (`MpEnergyNetworkState.cs:84`, `ExlibConfig.cs:86`, `:92`), so today six stands cost what one costs.
* "The stock only ever moves forward" holds only for one of the two layouts. With the stands chained on
  their own axle bus the decks are perpendicular to the walk and the piece zig-zags. See
  [Structure](#structure).
* Direction is last-writer-wins across the network walk (`MpEnergyNetwork.cs:75-77`). Two bridges turning
  opposite ways on one run produce an arbitrary `Reversed` - which on a hall silently flips every stand's
  feed deck at once, not just one. Not detected, not reported
  ([mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md)).
* A hall is one network, so one wrong bevel re-gears the whole line. The transmission couples two networks
  and is not a node ([mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md)); a hall on one side of a transmission is one
  run.
* Each stand cools its own piece the whole time it holds it, moving or not
  (`BlockEntityRollingMill.cs:341-346`). A hall does not remove the heat budget - it shortens the walk. The
  [reheat furnace](reheat-furnace.md) stays routine.
* `RollSetSpec.Outputs` is a `Dictionary<float, string>` compared with `==` (`RollSetSpec.cs:95-101`).
  With six single-gap sets each dictionary has one entry, so the hazard shrinks - but `Outputs` is leaving
  for the [shear](shear.md) anyway.
* The automation / reversing-mill upgrade is deferred and the train stays hand-fed
  ([steel roll sets](steel-roll-sets.md)).
* Stale in `RollSetItemDefinitions.cs:70-72`: the `flatwide` comment explains a four-gap wide set as
  "the same schedule with a barrel that swallows the work". The settled wide family is six one-gap items
  and the comment describes an item that is being deleted.
* `overview.md`'s iiex row lists neither the wide hall nor the steam hammer among iiex's content.

---

## Open

| # | Question | Notes |
|---|---|---|
| 1 | Which axis is the hall laid on? Stands chained on their own axle bus (zero extra parts, zig-zag walk) vs stands in a row along the feed axis fed through bevels off a parallel line shaft (straight walk, one bevel + shaft run per stand) | The second is what the reference photograph shows and what "walk down the line" describes. It costs [flywheel & shafting](flywheel-and-shafting.md) parts per stand, which is the only thing that makes the hall cost more power hardware than one mill |
| 2 | Wire the per-consumer idle draw (settled 2026-08-05, [mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md) § Idle draw) | The shipped code still charges friction per run and zero idle load per stand, so the hall's power cost has no mechanism until it lands |
| 3 | What does a mill cost, and is 4 × that sane at iiex tier? | The open half of forming build item 15 ([rolling mill](rolling-mill.md) § Open); nothing has a recipe |
| 4 | Three roll shapes to draw (2.5 / 3.0 / 3.5) and four to wire | |
| 5 | Does `flatwide5` (0.5) get deleted or kept for a later re-add? | The art exists; the schedule drops it; `game:metalsheet` is the casualty |
| 6 | Is re-tooling one mill actually playable? The claim is that it stays legal and costs handling. With `MaxWidth` on the roll set and single-gap items that is six tool swaps and six walk-arounds per schedule - verify in game that it is tedious, not impossible | |
| 7 | Nothing feeds the hall. `shingledslab` is not a `StockForm` (forming build item 6, [rolling mill](rolling-mill.md) § Open), the [steam hammer](steam-hammer.md) that would shingle it does not exist, and the mill cannot roll anything at all (B3, [rolling mill](rolling-mill.md)) | the hall is downstream of three separate blockers |
| 8 | `heavyplate` and `boilerplate` do not exist as items ([rolled parts](../items/rolled-parts.md)), so the hall has nothing to produce | |
