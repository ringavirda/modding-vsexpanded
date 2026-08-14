# Rolling mill

**Status** playable in creative - the block, the pass simulation, the two-round model, the pass clock and
the save/load are live and pinned by 136 test methods across eleven files. Nothing in survival can craft it
and no schedule can be finished, because the [shear](shear.md) that ends one does not exist.
**Mod** iiex (`IronIndustryExpanded`)

**Owns**

* the rolling mill block and block entity - its 3 × 3 × 2 footprint, the two axle-bus cells, placement,
  break, drops and the deck/gap/side interaction mapping;
* the pass lifecycle - `BeginPass` → `AdvancePass` → `CompletePass`, the stall rule, the wrench release,
  and the invariant that a reduction is committed only on completion;
* the pass physics in `RollingPass` - bite (`δ_max = μ²R`), the stand's load, spread, length
  multiplier and the cooling law - and every `Rolling*` config key and the two friction constants;
* the roll-set spec format (`rollset` attribute, `TryParse` validation rules) and the shipped roll-set
  catalogue, plus the delta between it and the settled 2026-07-29 gap tables;
* the `WorkPiece` stack format (`stockForm` / `stockThickness` / `stockGap` / `stockFed`) and how the
  two-round model is carried on it - the round target, the round reset, and the legacy read;
* `StockForm` - the two shipped forms, their spread exponents and their former names - and the composed
  work-piece mesh;
* every bug and stale comment listed under [Gotchas](#gotchas).

**Does not own - cited only, never restated**: the mechanical-energy model, the consumer contract, the
`Mp*` constants and the mill's pass-tick interval ([mp-energy](../mechanics/mp-energy.md)); the drive that
feeds it ([flywheel & shafting](flywheel-and-shafting.md)); the filler footprint system and the
no-filler-graph-node rule ([multiblock](../mechanics/multiblock.md)); the 32 / 48 handling limits and the
soft-locks ([recoverability](../mechanics/recoverability.md)); the product ladder, crop points and cut
arithmetic ([rolling](../processes/rolling.md)); stock item masses and lengths ([stock](../items/stock.md));
the reheat that keeps stock hot ([reheat furnace](reheat-furnace.md), [heat balance](../mechanics/heat-balance.md));
the 1 vx³ = 2.5 u rule ([density rule](../mechanics/density-rule.md)); code-first defs, RCC and goldens
([recipes & config](../mechanics/recipes-config.md)).

**Depends on** [mp-energy](../mechanics/mp-energy.md) · [multiblock](../mechanics/multiblock.md) ·
[recoverability](../mechanics/recoverability.md) · [flywheel & shafting](flywheel-and-shafting.md) ·
[recipes & config](../mechanics/recipes-config.md) · [rolling](../processes/rolling.md)

---

## Role

The mill is where the mechanical-energy network is spent: the waterwheel, the bridge, the flywheel's
inertia, the shaft run and the transmission's ratio all exist to deliver the torque one bite of hot iron
demands. It is also the only route past the vanilla ingot - vanilla has no path from a puddled bloom to
plate, sheet, rod or nail-rod, and the machines downstream (iiex's boilers especially) are made of rolled
parts. Historical basis: Cort's 1783-84 pairing of puddling and grooved rolling.

Hot work is the governing rule. `δ_max = μ²R` makes hot iron bite about thirty times deeper per pass than
cold, and the flow-stress term makes cold iron load about ten times harder, so a cooled piece both refuses
to enter and drags the run to a stop; the carry-back between passes is a heat budget, answered by the
reheat furnace.

There is no screw-down: the barrel carries a fixed sequence of gaps and the stock is walked along it. The
schedule is geometry - a jump deeper than `δ_max` skids rather than being forbidden by a rule, and since
2026-08-12 `δ_max` is calibrated so that *any* skipped gap skids. The walk is enforced, and nothing enforces
it: the only rule is friction.

---

## Structure

A 3 × 3 × 2 megablock (three cells along the barrel, three across the line, two high), authored in the
`we` frame - axle along X, `rotateY: 0` - with `ns` the 90° rotation
(`BlockRollingMill.cs:56-59`, `:106`).

| Cell (we frame) | What it is | Kind |
|---|---|---|
| `(0,0,0)` | **principal** - the `mpenergy` consumer node, the roll stand's drive end | `BlockRollingMill` |
| `(-1,0,0)`, `(-2,0,0)` | **axle bus** - invisible, solid, pass-through graph nodes | `BlockRollingMillAxle` |
| `(-2..0, 1, 0)` | roll stand above the axle line | invisible filler |
| `(-2..0, 0, -1)` | one feed deck | invisible filler |
| `(-2..0, 0, +1)` | the other feed deck | invisible filler |

Nine fillers, two axle nodes, one principal - twelve cells (`BlockRollingMill.cs:74-94`, `:99`). The
footprint is authored with the ASCII layout DSL and rotated on placement; see
[multiblock](../mechanics/multiblock.md).

The axle cells are not fillers. The network BFS used to traverse `BlockNetworkNode` cells only, so a filler
could never bridge the drive line and the mill would connect on one shaft end only. Two dedicated invisible
node blocks make the three-cell line one connected bus, drivable from either end and chainable into a train
of stands on a shared shaft (`BlockRollingMillAxle.cs:11-18`). That constraint is retired - a footprint cell
declaring a `passThrough` membership does the same job ([multiblock](../mechanics/multiblock.md) § A filler
cell is a graph node when it declares one) - but the block stays, because removing a placed block needs a
migration.

Placement refuses unless the whole volume is clear - both the filler cells and the axle cells
(`BlockRollingMill.cs:122-153`). Breaking any axle cell routes to the principal and takes the whole machine
(`BlockRollingMillAxle.cs:65-88`), with a self-heal fallback if the principal's break did not clear it.

### Which deck is which

The stand only turns one way, so a two-high mill can only be fed from one side. Which side follows the drive:
`InputDeck` = `Deck(DriveReversed)`, `OutputDeck` always the opposite (`BlockEntityRollingMill.cs:264-281`).
Reversing the run reverses the feed side. Direction enters the network only at the bridge; see
[mp-energy](../mechanics/mp-energy.md).

Caution: `Deck()` returns one cell - the `z = ±1` cell at `x = 0`, directly beside the principal - and that
is the cell a finished piece *lands* on. The deck the player *feeds* from is the whole three-cell row
(`DeckRow` / `IsInputDeck`), because a click's position along it is what picks the gap.

---

## Assets

| Asset | Path | State |
|---|---|---|
| Mill shape (runtime) | `assets/iiex/shapes/forming/rollingmill.json` | shipped; textures `iron5`, `cast-iron1`; clips `idle` + `cycle`, 30 frames each |
| Mill shape (editable) | `assets/editable/shapes/machine-mp-megablock-rollingmill.json` | drawn, not yet reflected in the runtime shape name |
| Roll-set item art | `assets/editable/shapes/item-finished-rollers-{flat,flatwide5,flatwide10,flatwide15,grooved}.json`, `item-rollers-flatwide20.json`, `item-sandcast-rollers-blank.json` | drawn and not wired - the item ships `game:item/ingot` (`RollSetItemDefinitions.cs:122`) |
| Stock stage shapes | `assets/iiex/shapes/forming/stock-{shingledbar,shingledslab}-{5,10,15,20,30}.json` | shipped, 10 files, generated. ⛔ the bar's are drawn **16 long** and the settled form is 18 - the art is item 7's, and nothing reads a shape's length |
| Stock base art (editable) | `item-shingled-bar.json`, `item-shingled-slab.json` | drawn; the generator still expects the `item-shingledbloom` / `item-shingledslab` names, which do not exist |
| Axle cell | `exlib:block/empty` (`BlockRollingMillAxle.cs:39`) | intentionally invisible |
| Handbook page | — | none. `docs/iiex/handbook/` has no forming page, yet the def declares `Handbook("rollingmill-*")` (`BlockRollingMill.cs:54`) |

### The shape already draws four roll families, and nothing selects between them

`rollingmill.json`'s top-level element groups are `Supports`, `ShaftHousings`, `MainShafts`, `RollersFlat`,
`InputTables`, `RollersGrooved`, `RollersFlatWide20`, `RollersFlatWide15`, `RollersFlatWide10`,
`RollersFlatWide5`. `BlockRollingMill` declares no `ShapeSelectiveElements` and no
`EntityBehavior("Animatable")`, and `BlockEntityRollingMill` holds no animator, so:

* every roll family renders simultaneously, whichever set is fitted (or none);
* the authored `cycle` clip never plays; the stand is visually static while it rolls.

The fitted set is legible only from block info. The flywheel and the transmission solve the same problem
with `.EntityBehavior("Animatable")` + a `ToggleAnimator`/`ConstructedAnimator`; the mill has neither. See
[flywheel & shafting](flywheel-and-shafting.md).

### The authored roll art disagrees with the shipped config, and agrees with the settled design

| Family | Gaps in the **shape** | Gaps in **code** (`RollSetItemDefinitions.cs`) | Settled 2026-07-29 |
|---|---|---|---|
| `RollersFlat` | 2.0 / 1.5 / 1.0 / 0.5 | 2.0 / 1.5 / 1.0 / 0.5 (`:66`) | 2.5 / 2.0 / 1.5 / 1.0, barrel 4 |
| `RollersGrooved` | 2.5 / 2.0 / 1.5 / 1.0 | 1.0 / 0.5 (`:89`) | 2.5 / 2.0 / 1.5 / 1.0, barrel 16 |
| `RollersFlatWide*` | 2.0 / 1.5 / 1.0 / 0.5 (four) | one `flatwide` item, gaps 2.0/1.5/1.0/0.5 (`:76`) | six single-gap sets, 1.0 … 3.5, `MaxWidth` 15, and they are iiex |
| — | — | ~~`slitting`, gaps 0.5~~ | deleted, and now retired in code too (2026-08-12) |

The grooved barrel was drawn to the settled schedule; the config does not match it. The wide art is four
gaps where the settled train needs six, and includes the dropped 0.5.

### The stage shapes are generated, and the generator's inputs no longer exist

`scripts/tools/generate-rolled-stock.py` derives all ten `stock-*.json` files from two authored bases
(`item-shingledbloom`, `item-shingledslab`), both gone from `assets/editable/shapes/`. The generated
outputs still ship and `RolledStockStagesTests` still measures them off disk, so the tests pass while the
pipeline cannot be re-run. The forming build list calls for deleting the generator and its ten outputs and
wiring the new authored art instead.

The generated files were renamed with the forms on 2026-08-12 and their geometry was not touched, so the
bar's stages are still drawn off a 16-long base while `ShingledBar.BaseLength` is 18. Nothing reads a
shape's length - `RolledStockStagesTests` measures the base off disk and scales the others from it - so the
two are internally consistent and disagree only with the settled form. Wiring the authored art (18 long,
drawn as two 9-long halves) is what closes it.

---

## Construction

There is no recipe. This is a blocker.

`grep`ping `src/IronIndustryExpanded/Recipes/` for `rollingmill`, `rollset` or `stock-` returns nothing, and
there are no hand-written recipe JSON assets anywhere in the repo
([recipes & config](../mechanics/recipes-config.md)). The mill, the four roll sets and both stock items are
reachable only from the creative inventory (`.CreativeCommon(...)` at `BlockRollingMill.cs:60`,
`RollSetItemDefinitions.cs:127`, `StockItemDefinitions.cs:57`).

There is no construction path of any kind: the mill is a plain placed `BlockNetworkNode`, not a
RightClickConstructable, so it has neither a grid recipe nor build stages. The transmission is RCC-built
([flywheel & shafting](flywheel-and-shafting.md)).

The stock items have no source either - nothing produces a `stock-shingledbar` or `stock-shingledslab`. The settled route
is puddling → helve shingling, neither of which is built ([shingling](../processes/shingling.md); puddling
is blocked by B8).

---

## Operation

### Verbs

| Held | Where | Result |
|---|---|---|
| a wrench (`Code.FirstCodePart() == "wrench"`, any domain) | anywhere on the machine, while rolling | frees the stuck piece, state unchanged (`BlockRollingMill.cs:265-291`) |
| a roll set (any collectible with a `rollset` attribute) | anywhere on the machine | fits it, hands back the previous one; refused mid-pass with `iiex-rollingmill-busy` (`:293-320`) |
| stock, right-click | the input deck | feeds the near side (`MillFeed.SideIndex(false, sides) = sides-1`) |
| stock, sneak + right-click | the input deck | feeds the far side (index 0) |
| anything | the output deck | nothing - a two-high stand cannot be fed backwards |

Where along the deck the click lands picks the gap: the hit point is taken into the mill's own frame, mapped
to `0..1` along the barrel, and split into `gapCount` equal bands (`BlockRollingMill.cs:368-375`,
`MillFeed.cs:62-89`).

### The pass

1. `TryFeed` divides the piece for this barrel (`ceil(width / barrelWidth)` sides), then asks
   `MillFeed.Decide` (`BlockEntityRollingMill.cs:193-237`).
2. On `Ok` the gap and side are held as pending and `BeginPass` puts the piece under the rolls with the
   post-reduction bite width and length. The player's stack is taken (`BlockRollingMill.cs:344-347`).
3. Every 250 ms the mill advances the bite by `v = ωR · dt`, reading the live network speed so progress
   stays in step with the load the mill itself imposes (`:75-82`, `:333-371`).
4. The piece cools whether or not it is moving, so a jam is self-worsening (`:341-346`).
5. When `remaining` reaches 0, `CompletePass` applies the pending reduction and spawns the piece on the
   output deck at `+0.5, +0.6, +0.5` with a near-zero upward velocity (`:282-303`).

Nothing is committed mid-pass. Losing the drive, letting the stock go cold, breaking the mill or wrenching
the piece out all leave the reduction unapplied and the gap simply redone. That is what `_pendingGap` is for.

### How the two rounds are carried *(built 2026-08-12)*

The feed arithmetic is [rolling](../processes/rolling.md)'s. What lives here is the state that carries it:
`WorkPiece` holds **one `Thickness`** for the whole piece, the **`Gap`** it is half way through, and a
per-side **`Fed`** flag.

| Where the piece is | `Gap` | The next round lands |
|---|---|---|
| standing between gaps | `0` | the half-step, `(Thickness + gap) / 2` |
| half way through gap *g* | *g* | *g* itself |

`Gap` is written **when a round completes**, never during one, which is what tells round 2 from round 1
without a round counter. `WorkPiece.RoundTarget` is the whole rule and `WorkPiece.Feed` the whole
transition; the gauge moves only on the feed that fills the last `Fed` flag, so a piece is one thickness
across its width at every moment a player can see it and a lopsided piece is unreachable.

Three consequences worth stating, because each replaces something the per-side model needed:

* **The round resets on a barrel that divides the piece differently.** Side count is `Fed.Length`, so
  `ForSides` re-sizing the array *is* the reset - the feeds recorded were of a different set of sides. Two
  barrels that give the same count divide it the same way and the round survives; that is what "a different
  barrel" means here.
* **A side already through this round reads as `NoReduction`.** It is at this round's gauge already, so it
  really would pass through untouched.
* **The draft the rolls are asked to bite is the round's, not the gap's** - half the reduction. Every
  shipped gap is a 0.5 step, so a round is a 0.25 bite against `δ_max = 1.0`: the limit no longer decides
  legality on the schedule itself, only on how far ahead a player may skip.

**And skipping is now bounded to nothing at all.** Halving every bite doubled how far `δ_max` reached, so
for a few hours a fresh bar could be walked to the plate gap in two feeds against twelve. Closed the same
day by calibration rather than by a rule: `δ_max` is **0.36**, which sits above one round's 0.25 draft and
below one gap's 0.5, so the next rung is the only rung that bites. The barrel has to be walked, and no
ordering check does the walking. `ShippedRollSetTests` pins both halves - every shipped route walkable
round by round, and every skipped gap refused.

### States

| State | Condition | Reported as |
|---|---|---|
| idle | `_remaining == 0` | `iiex:rollingmill-info-idle` |
| rolling | `_remaining > 0`, travelling | `iiex:rollingmill-info-rolling` + temperature |
| stalled | `_remaining > 0`, `travelled ≤ 0` (ω = 0 or stock below rolling heat) | `iiex:rollingmill-info-stalled` + temperature |

A stall is not a lost pass: it resumes where it stopped once the run spins back up or the stock is re-heated.
Since a stalled piece keeps cooling, past the bite threshold the only answer is the reheat furnace
(`BlockEntityRollingMill.cs:359-380`).

### Refusal messages

`MillFeed.FeedVerdict` (`MillFeed.cs:6-29`) → `SendIngameError` (`BlockRollingMill.cs:350-359`):
`NoRollSet`, `WrongForm`, `NoReduction` (gap wider than the stock - it would pass through untouched),
`WontBite` (deeper than `δ_max`), `TooCold`. `TooCold` is kept apart from `WontBite` because the fix is a
furnace, not a wider gap.

---

## Numbers

### The relations

| Relation | Formula | file:line |
|---|---|---|
| Bite | `δ_max = μ²R` | `RollingPass.cs:31-32`, `:39-46` |
| Load torque | `T = T_run · Y` while stock is in the rolls, `0` idle | `:83-92` |
| Flow stress | `Y = 1` hot; `1 + (m−1)·clamp((T_roll−T)/span, 0, 1)` below | `:58-70` |
| Spread | `w = w₀·(t₀/t)^e`, capped at `maxWidth` | `:118-131` |
| Length | `L = L₀ · (t₀/t) / (w/w₀)` - from the actual, possibly capped, width | `:138-146` |
| Cooling | `T ← ambient + (T−ambient)·e^(−rate·dt)` | `:154-165` |
| Travel | `Δs = ω · R · dt`, zero below rolling heat | `BlockEntityRollingMill.cs:513-516` |

⛔ **Load torque is the one relation that is not geometry, as of 2026-08-13.** The stand has two states -
working or empty - and the load follows them: the declared `T_run` while stock is between the rolls, zero
otherwise, times whatever flow stress the piece's heat has climbed to. The contact arc `L_c = √(R·δ)`, the
roll force `F = Y·w·L_c` and `RollingTorqueScale` are all **gone**; see [Open](#open) for why a derived load
was the wrong shape for this dial.

### Config — iiex, `ex_values.json` (domain `iiex`)

| Key | Value | file | What it does |
|---|---|---|---|
| `RollingTempC` | `900` | `IiexConfig.cs` | the hot/cold line; range `[0, 3000]`. Below it friction collapses and flow stress climbs |
| `RollingColdStressMultiplier` | `10` | `IiexConfig.cs` | flow stress of fully-cold stock; range `[1, 1000]` |
| `RollingColdSpanC` | `400` | `IiexConfig.cs` | degrees below `RollingTempC` over which the multiplier is reached; range `[1, 3000]` |
| `RollingRollRadius` | `4` | `IiexConfig.cs` | roll radius in block-space units; sets `δ_max` and the travel speed `v = ωR`; range `[0.01, 100]` |
| `RollingLoadTorque` | `0.34` | `IiexConfig.cs` | N·m the stand draws while working, before the cold multiplier; range `[0, 10000]` |
| `RollingCoolRate` | `0.005` | `IiexConfig.cs` | fraction of excess heat shed per second, under the rolls and during the carry-back; range `[0, 10]` |
| `RollingAmbientC` | `20` | `IiexConfig.cs` | what the stock cools toward; range `[-50, 500]` |

`RollingLoadTorque` is calibrated against the network's numbers, not the mill's: one bridge drive (1 N·m)
less friction at ω_max (0.05·2 + 0.5 = 0.6) leaves **0.4 N·m** of headroom, and 0.34 takes 85 % of it -
the *"about 15 % to spare"* the design always claimed. Retuning `MpFrictionCoeff`, `MpIdleTorque`,
`MpMaxSpeed` or `FlywheelBridgeChargePower` moves the mill's balance with it; see
[mp-energy](../mechanics/mp-energy.md). Nothing about the stock moves it except heat, which is the point.

Caution: the reheat furnace's `MeltingPoint` is bound to `IiexValues.RollingTempC`
(`BlockEntityHeatingFurnace.cs:61`) - the mill's hot/cold line doubles as the reheat furnace's target.
Changing one changes the other silently.

### Hard-coded — not config, source-only

| Constant | Value | file:line | Note |
|---|---|---|---|
| `RollingPass.HotFriction` | `0.3` | `RollingPass.cs:21` | `public const`. With `R = 4` gives δ_max = **0.36** - above one round's 0.25 draft and below one gap's 0.5, which is the whole skip bound |
| `RollingPass.ColdFriction` | `0.055` | `RollingPass.cs:25` | `public const`. δ_max = 0.0121 - ~30× worse, hence a hot mill |
| `WorkPiece.FeedsPerSide` | `2` | `WorkPiece.cs:38` | the two rounds a gap costs: the half-step, then the gap |
| `MillFeed.DeckCells` | `3` | `MillFeed.cs:78` | cells the gap bands are spread across |
| `MillFeed.DeckOriginOffset` | `2` | `MillFeed.cs:81` | deck offsets run `−2 … 0` in the mill frame |
| `StockMesh.CentreX` | `8` | `StockMesh.cs:27` | base shapes are authored centred on x = 8 |
| Axle offsets | `(−1,0,0)`, `(−2,0,0)` | `BlockRollingMill.cs:99` | `static readonly Vec3i[]` |
| Deck offsets | `z = ±1`, `x = 0` | `BlockEntityRollingMill.cs:274-281` | one cell per deck - see [Blockers](#blockers) |
| `stackSize` | `1` | `BlockRollingMill.cs:53`, `StockItemDefinitions.cs:42`, `RollSetItemDefinitions.cs:125` | every stock piece carries its own state, so they can never merge |

### Shipped roll-set catalogue — `RollSetItemDefinitions.cs`

*Re-cut 2026-08-12: a set no longer carries gaps or outputs. It declares only what the tooling knows, and
the gauges come from the stock's ladder below.*

*Re-cut again 2026-08-12 (item 4): the narrow barrel is 4, and `slitting` is retired.*

| Variant | Family | Accepts | Barrel | `minTorque` |
|---|---|---|---|---|
| `rollset-flat` | flat | `shingledbar`, `billet` | 4.0 | 0.2 |
| `rollset-flatwide` | flat | `shingledslab`, `shingledbar` | 16.0 | 0.5 |
| `rollset-grooved` | grooved | `shingledbar`, `billet` | 16.0 | 0.3 |

`slitting` was **retired, not renamed** - it accepted `"plate"`, which is not a `StockForm`, and no ladder
declared a slitting rung, so it was tooling with no route. No migration: remapping a retired code onto a
surviving one would hand the player an item they never had, which is the rule
`CastingNameMigration` already set. `ShippedRollSetTests` now pins the dead-set list **empty**.

### Shipped stage ladders — `StockItemDefinitions.cs`

Declared on the stock items under `attributes.stageladder`, merged into `StageLadderRegistry` at
`AssetsFinalize` ([process-extension](../mechanics/process-extension.md)). No rung names a `code` yet:
every shipped stage is a shear crop, so the mill ejects the piece it drew through.

| Family | Rung | Accepted by |
|---|---|---|
| `shingledbar` | 2.5 / 2.0 / 1.5 / 1.0 | flat, grooved |
| `shingledslab` | 2.5 / 2.0 / 1.5 / 1.0 | flat |

Every rung is a 0.5 step and every family walks all four, so **every draft in the game is uniform**: a
round is 0.25 and a gap is two of them, whichever branch takes it. That is what the skip bound is
calibrated against. `flat` is kept off the slab by its `accepts` rather than by the ladder - the narrow
barrel is the reason, and no ladder can carry that.

The 0.5 rung is gone with the re-cut. Its cost is `game:metalsheet`, a vanilla cladding block that now has
no unlock; re-adding 0.5 to the wide family alone would restore one ([rolling](../processes/rolling.md)).

⛔ The rungs are the **gaps**, not the rounds. The half-step a round lands sits between two rungs and is
declared nowhere: it is arithmetic (`(thickness + gap) / 2`), it is never a stopping point because
`OutputAt` only matches a declared rung, and it is drawn by the composed mesh. A ladder that declared its
half-steps as rungs would put them on the deck as gap bands of their own, which is a barrel with twice the
grooves the art has.

Caution: `billet` is accepted by two sets and is not a `StockForm` - it is forward-declared data for
smex's cast stock, which no mod registers yet. Unlike the retired `slitting` set it costs nothing: a set
that accepts a form nobody ships simply never matches it.

### `StockForm` — `StockForm.cs:48`, `:60`

*Renamed onto the settled ladder 2026-08-12; the old names live on as `FormerNames` and nothing else.*

| Form | Former name | Base w × t | `MaxWidth` | `BaseLength` | Spread `e` | Width at 2.0 / 1.5 / 1.0 / 0.5 |
|---|---|---|---|---|---|---|
| `shingledbar` | `bloom` | 3 × 3 | 8 | 18 | 0.846 | 4.23 / 5.39 / 7.60 / 8.00 (capped) |
| `shingledslab` | `slab` | 8 × 3 | 14 | 20 | 0.463 | 9.65 / 11.03 / 13.30 / 14.00 (capped) |

The bar's exponent is tuned so it reaches nearly its full 8 wide exactly at the 1.0 gap - one voxel thick
and about eight wide being vanilla plate proportion. The two exponents are correct against the intended
numbers and should not be retuned. Masses are [stock](../items/stock.md)'s: 400 and 1200.

### Worked round — fresh bar, first flat gap

Fresh bar, `sides = 1` (3 wide ≤ 4 barrel), fed at gap 2.5. A gap is two rounds; this is round 1, which
lands the half-step at 2.75:

| Quantity | Value |
|---|---|
| draft δ | `3.0 − 2.75 = 0.25` - 69 % of `δ_max` |
| width after | `3 · (3/2.75)^0.846 = 3.23` |
| length after | `18 · 1.0909 / 1.0764 = 18.24` |
| load torque, hot | `0.34 N·m` - 85 % of one bridged wheel's headroom |
| load torque, fully cold | `10 ×` that = `3.4 N·m` |
| round duration at ω_max | `18.24 / (2 · 4) ≈ 2.3 s` |

The two torque rows are the same for **every** hot pass in the game: the stand's demand is declared, not
read off this round's geometry. Only the piece's heat moves it. ★ That is what makes this table safe to
re-derive - a re-cut schedule changes the first three rows and nothing else, where before 2026-08-13 it
silently changed the mill's whole balance against the drive.

### Cited — owned elsewhere

The mill's pass-tick interval (`BlockEntityRollingMill.cs:42`) and the network tick it samples belong
to [mp-energy](../mechanics/mp-energy.md); the clock that carries it belongs to
[framework composition](../mechanics/framework-composition.md). The stock items' `materialUnits`, `MaterialDensity`,
`combustibleProps.meltingPoint` and `temperatureDamage` (`StockItemDefinitions.cs:23-27`, `:43`, `:48-56`)
belong to [stock](../items/stock.md), and are at their settled figures (400 / 1200) since 2026-08-12. The
bevel-gear item and every `Mp*` / `Flywheel*` / `ShaftInertia`
key belong to [mp-energy](../mechanics/mp-energy.md). The 32 / 48 handling limits and the crop points
belong to [recoverability](../mechanics/recoverability.md).

---

## Drops

| Broken | Returns |
|---|---|
| the principal | the mill itself (default `BlockNetworkNode` drop, `stackSize 1`), plus any piece stuck in the rolls, plus the fitted roll set - both spawned at the principal / output deck by `BlockEntityRollingMill.OnBlockBroken` (`:374-383`) |
| an axle cell | routes to the principal and breaks the whole machine (`BlockRollingMillAxle.cs:65-88`); the axle itself is `NoDrops()` (`:43`) and `OnPickBlock` yields the mill (`:100-107`) |
| a filler | routes to the principal, per the shared filler system ([multiblock](../mechanics/multiblock.md)) |

Nothing is lost on break. The one path that does destroy a piece is `CancelPass` - see
[Gotchas](#gotchas).

---

## Code

| Type / member | file:line | Role |
|---|---|---|
| `BlockRollingMill` | `BlockStructures/Forming/Blocks/BlockRollingMill.cs:31` | `BlockNetworkNode` + `IExBlockDefProvider` + `IFillerHost` + `IFillerInteractionTarget` |
| `.Definitions` | `:44-64` | the code-first blocktype; `ns`/`we`, `SolidNonOpaque`, `FillerOffsets(Footprint)` |
| `.Footprint` | `:74-94` | the nine filler cells, ASCII DSL |
| `.PlaceAxleNodes` / `.RemoveAxleNodes` | `:169-192` / `:210-215` | stamps `Principal` onto each axle BE; removal lets each BE `RemoveNode` itself |
| `.HandleInteract` | `:247-273` | the wrench / roll-set / feed router, shared by principal and filler clicks |
| `.Feed` | `:322-361` | gap zone + side from the click, then `TryFeed`; consumes the held stack on `Ok` |
| `.AlongBarrel` | `:368-375` | world hit point → mill frame → `0..1` |
| `BlockRollingMillAxle` | `Forming/Blocks/BlockRollingMillAxle.cs:20` | invisible graph node, not a filler; `OnNeighbourBlockChange` empty by design (`:59-63`) |
| `BlockEntityRollingMill` | `Forming/BlockEntities/BlockEntityRollingMill.cs:24` | `BlockEntityNetworkNode` + `IMpEnergyConsumer` + `IProductionReadiness` |
| `.HostProcess` | `:48-53` | the 250 ms pass clock, a hosted `BEBehaviorProductionMachine`; added in the constructor (`:28`) |
| `.IsReadyToProduce` / `.StopsProductionWhenNotReady` | `:59`, `:63` | a pass is the gate; an empty stand keeps its clock |
| `.OnPassTick` | `:89-93` | reads the live run's ω and advances the bite by a bounded `dt` |
| `.LoadTorque(speed)` | `:482-490` | the consumer contract; speed-independent by design, and geometry-independent since 2026-08-13 |
| `.AdvancePass(dt, speed)` | `:498-534` | cool → travel → stall-or-progress → `CompletePass` |
| `.TryFeed` | `:193-237` | divide for the barrel, `Decide`, arm the pending reduction, `BeginPass`. Writes the piece back **only on acceptance** |
| `.CompletePass` | `:302-311` | the only place the stock changes |
| `.ReleaseStuckPiece` | `:243-256` | the wrench recovery |
| `.ToTree` / `.FromTree` | `:576-599` | temp, remaining, stalled, pending gap+side, piece, roll set; both stacks re-resolved. Draft and width are **not** persisted - nothing past the bite test reads them |
| `RollingPass` | `Forming/RollingPass.cs:13` | pure physics; the entire model, pinned headless |
| `MillFeed.Decide` | `Forming/MillFeed.cs:95-128` | pure feed verdict |
| `RollSetSpec` | `Forming/RollSetSpec.cs:31` | the tooling record; `TryParse` at `:108-201` |
| `RollSetValidation.Validate` | `Forming/RollSetValidation.cs:20-32` | `AssetsFinalize` sweep; a malformed set is logged rather than failing silently |
| `WorkPiece` | `Forming/WorkPiece.cs:28` | the per-stack record: one gauge, the gap it is half way through, a flag per side |
| `.RoundTarget` / `.Feed` | `WorkPiece.cs:87`, `:97` | the whole two-round rule, and the only place the gauge moves |
| `.ForSides` | `WorkPiece.cs:74` | divide for a barrel; a different division resets the round |
| `StockForm` | `Forming/StockForm.cs:30` | authored base dimensions, spread, and the names the form used to go by |
| `StockMesh.ScaleOf` | `Forming/StockMesh.cs:26-40` | the scale that draws a gauge nothing has art for |
| `ItemStockPiece.OnBeforeRender` | `Forming/Items/ItemStockPiece.cs:26-48` | composes and caches the part-rolled mesh |
| `StockFormRenameMigration` | `BlockMigrations/StockFormRenameMigration.cs:20` | `stock-<former>` → `stock-<name>`, walked off the registry |
| `RollSetItemDefinitions` | `Forming/RollSetItemDefinitions.cs:17` | the four sets |
| `StockItemDefinitions` | `Forming/StockItemDefinitions.cs:19` | one item per `StockForm` |

### Where a caller hooks in

To add a rolling product, add an entry to `RollSetItemDefinitions.Sets` and its output item. The mill
never names a product in code: the tooling owns the data, the same idiom the casting patterns use.

To add a stock form, add a `StockForm` to `StockForm.All`; `StockItemDefinitions` emits the item
automatically from `StockForm.All.Values` (`:30`). The stage shapes must also be authored or generated, or
`RolledStockStagesTests` fails.

To drive the mill, connect an `mpenergy` run to either shaft end. See
[flywheel & shafting](flywheel-and-shafting.md).

### Tests

`test/IronIndustryExpanded.Tests/Blocks/Forming/` - fifteen files. The two-round model is pinned by
`WorkPieceTests` (its own file, rewritten 2026-08-12), the round's draft by `MillFeedTests`, and the walk
end to end by `RollingMillFeedTests`; `StockFormRegistryTests` and
`Migrations/StockFormRenameMigrationTests` pin the rename and the former names.

`RollingMillClockTests` is the only one that drives the mill's real listener; every other file calls
`AdvancePass` directly with a `dt` of its own, so the clock and the physics are pinned separately.

Caution: the suite's blind spot *was* the bootstrap - every mill test built its work piece with
`WorkPiece.Fresh(...).ToStack(stack)` by hand, which no production path does, and that is how B3 stayed
invisible. Two tests now start from a stack carrying nothing but the itemtype's declaration
(`Stock_straight_off_the_grid_is_already_a_work_piece`, `A_bloom_straight_off_the_grid_takes_its_first_pass`);
the rest still hand-write their state, so the premise is pinned in one place rather than in every file.

---

## Blockers

*B3, B4 and B17 all closed during the extensibility and forming work; the mill rolls in creative. They are
kept here in one line each because other pages still cite them by number.*

| # | Was | Closed by |
|---|---|---|
| B3 | no stock item carried a `stockForm` the mill could read - the form was on the itemtype and `FromStack` read the stack tree, so every fresh piece was `WrongForm` | `FromStack` falls back to the collectible attribute (`WorkPiece.cs:155-175`), 2026-08-12 |
| B4 | grooved's first gap was `1.0`, a 2.0 draft on fresh stock, so it could never bite | the gaps became the stock's ladder; the bar's grooved branch enters at 2.5, and a round bites half of that |
| B17 | only the middle deck cell was an input, so the widest gaps were unreachable | `IsInputDeck` accepts the whole deck row (`BlockEntityRollingMill.cs:359-379`) |

### B12 — `castbloom`'s 50-long 1.0 stage

A soft-lock; cold shear is not an escape ([recoverability](../mechanics/recoverability.md)). Owned there.

### Downstream: the mill's own output has nowhere to go

No shipped ladder rung names a `code`, because every shipped stopping point is a shear crop and the shear
does not exist. A schedule therefore cannot be finished - the piece leaves the mill as stock at whatever
gauge the player stopped at. See [shear](shear.md).

---

## Gotchas

- `TryFeed` ignores `BeginPass`'s return value (`BlockEntityRollingMill.cs:225-236`). It arms
  `_pendingGap` / `_pendingSide`, calls `BeginPass(...)` discarding the `bool`, and returns the accepted
  decision regardless. `BlockRollingMill.Feed` then takes the stack out of the player's hand
  (`:344-347`). If `BeginPass` ever refuses - it re-checks `CanBite`, and rejects `length ≤ 0` or
  `width ≤ 0` (`:122-132`) - the item is destroyed silently. Today the two checks happen to agree; the
  contract is unguarded.
- ~~A refused offer still mutates the held stack.~~ **Fixed 2026-08-12** by the two-round model. `ForSides`
  returns a new record instead of writing through, and `TryFeed` writes the piece back only after the
  decision is accepted, so offering a piece to the wrong barrel no longer drops the round it is part way
  through.
- `CancelPass` destroys the piece. It nulls `_piece` without ejecting (`:289-296`), unlike
  `ReleaseStuckPiece` and `OnBlockBroken`, which both return it. No production caller today (tests only), so
  it is latent, but it is one call site away from an item-loss bug.
- `CancelPass` does not clear `_pendingGap`/`_pendingSide`; `ReleaseStuckPiece` clears only
  `_pendingGap`. `_pendingSide` is never reset anywhere (`:226`, `:289-296`). Harmless only because
  `TryFeed` overwrites both before every pass.
- A malformed roll set reports "busy". `TryFitRollSet` returns `false` both when a pass is running and
  when `TryParse` fails (`:163-182`), and `FitRollSet` maps every `false` to
  `iiex-rollingmill-busy` (`BlockRollingMill.cs:312`). The gate that got you there only checked that the
  `rollset` attribute exists (`:293-294`).
- ~~`RollSetSpec.Outputs` is a `Dictionary<float, string>` compared with `==`.~~ **Fixed 2026-08-12.**
  `Outputs` and `Gaps` are gone; the states are the stock's stage ladder
  ([process-extension](../mechanics/process-extension.md)) and `MillSchedule.OutputAt` matches on the
  ladder's own tolerance. The set no longer names a product at all.
- `RollSetSpec.MinTorque` is parsed, stored, validated and never read. No production call site; the
  only reference is `RollSetItemDefinitions.cs:13`'s prose. The settled design makes it the
  [shear](shear.md)'s cold-cut gate, which would be its first real use.
- Some of `WorkPiece` still is. `PassesAt`, `PassesForGap`, `NextGap`, `NextDraft`, `IsWide`,
  `OverhangsBarrel`, `WidthAt` and `RollingPass.CanCarry` have no callers in `src/`, only tests. The
  two-round model deleted the rest of the dead surface (`StripWidth`, `StripLength`, `Thickest`,
  `Thinnest`, `IsEven`, `Resplit`, `WithStrip`, `IsTurned`, `StockMesh.SideOf`, `SidePlacement`). `OutputAt` gained its first production caller on 2026-08-12 (`ClaimFinishedPiece`), so the
  stopping-point half is now wired end to end - but no shipped ladder names a `code`, so nothing is
  claimed at the mill yet.
- `ColdShearMaxThickness` does not exist in `src/` at all. Any design text that names it is describing a
  constant that was never written; the settled design deletes the concept ([shear](shear.md)).
- ~~The composed work-piece mesh is only used for uneven pieces.~~ **Repointed 2026-08-12.** A piece can
  no longer be uneven, so the composition now draws the **half-step** - the gauge between two rungs that no
  ladder declares and no art draws - and `IsBaseState` tests the gauge alone. A piece merely divided for a
  narrow barrel is still its base shape, which the old test got wrong in the other direction.
- ~~`WorkPiece.Resplit` silently no-ops on an uneven piece.~~ **Gone 2026-08-12** with the per-side model.
  `ForSides` always re-divides, because there is no uneven state left to refuse, and a piece carried
  between mills with different barrels can no longer reach a state neither will take.
- Stale doc: `RollSetSpec.cs:22-23` says "a flat set running 2.0 → 1.5 → 1.0 → 0.5 yields plate at 1.0
  and sheet at 0.5" - both output codes are unresolvable, and 0.5 is dropped by the settled schedule.
- ~~Stale doc: `RollSetItemDefinitions.cs` explains the flat set's "2, 2, 4, 4" schedule cost off
  `barrelWidth: 6.0`.~~ Fixed 2026-08-12 with the barrel. The counts survived the re-cut unchanged, for a
  different reason: at barrel 4 the piece outgrows the rolls one gap earlier, which moves the 4-feed gaps up
  by one and leaves the total at twelve.
- ~~⛔ **The mill's torque calibration was set when a gap was one bite.**~~ **Closed 2026-08-13** by making
  the load a declared state rather than a formula. The dial is `RollingLoadTorque` = 0.34 and it is 85 % of
  one bridged wheel's headroom by construction, so a re-cut schedule cannot move it again.
- ⛔ **The stage art disagrees with the form it is named for.** The bar's ten generated shapes are drawn off
  a 16-long base and `ShingledBar.BaseLength` is 18. Nothing reads a shape's length, so this shows only in
  the held item's proportions; item 7 replaces the art.

---

## Open

Sizes are the forming build list's.

| # | Work | Notes |
|---|---|---|
| ~~0~~ | ~~Fix B3~~ | **done** - `FromStack` falls back to the collectible attribute |
| ~~0b~~ | ~~Fix B17~~ | **done** - the whole deck row is an input |
| — | The skip bound: `δ_max` calibrated below one gap | **done 2026-08-12** - `HotFriction` 0.5 → 0.3, `ColdFriction` 0.09 → 0.055. Ruled, built and guarded in the same change |
| 1 | `OutputAt`'s counterpart at the shear: the crop table, keyed on stage | the mill only ever makes stock; there is no claim gesture on the mill. The registry is built (`ProcessJob`), the shear block is not |
| 2 | ~~`WorkPiece.Mass`~~; the 48-voxel refusal as a **declared** stage property | ⛔ ruled 2026-08-13: length comes from the art and cut points are config, so neither mass nor a computed length is wanted. A mandatory crop is a stage that says so. The crop tally itself is **built** - `WorkPiece.Cropped`, and the mill refuses a part piece ([shear](shear.md)) |
| ~~3~~ | ~~The two-round pass model~~ | **done 2026-08-12** - see [How the two rounds are carried](#how-the-two-rounds-are-carried-built-2026-08-12). A net deletion of nine members; retired the lopsided art |
| ~~4~~ | ~~Config: `flat` barrel 6 → 4, gaps 2.5 / 2.0 / 1.5 / 1.0; `grooved` the same four; delete `slitting`~~ | **done 2026-08-12.** ⛔ `MaxWidth` did **not** move to the roll set - that clause contradicts the settled `min(barrel, form cap)` ruling ([rolling § Open 1](../processes/rolling.md#open)), which keeps a cap on the form so the bar's 9-wide plate stage and skelp's 8 both stay expressible. Split out below |
| 5 | Section law `StockForm` → `RollSetSpec` (`square` \| `flat`); `SpreadWidth` gains a square branch | |
| ~~6~~ | ~~`StockForm`: `bloom` / `slab` → `shingledbar` / `shingledslab`~~ | **done 2026-08-12** - with the masses (400 / 1200), the lang keys, an item-code migration and `FormerNames` on the record |
| 7 | Wire the authored stock art; delete `generate-rolled-stock.py` and its ten outputs | its two inputs are already gone, and the bar's generated stages are 16 long where the form is 18 |
| 15/16 | The wide train is iiex - four ordinary mills at 2.5 / 2.0 / 1.5 / 1.0 on one shared drive shaft, plus six single-gap `flatwide` items (1.0 … 3.5, `MaxWidth` 15). No new block: the mill block stays iiex and iiex ships the sets | smex bolts two more stands (3.5 / 3.0) onto the front |

Not on that list, and still open:

- ~~⛔⛔ **The load model becomes a state, not a formula**~~ *(ruled and **built** 2026-08-13)*. `LoadTorque`
  is `RollingLoadTorque` while a pass is under the rolls and zero when idle, with only the cold multiplier
  on top. `LoadTorque(draft, width, R, temp, …)`, `ContactLength` and `RollingTorqueScale` are deleted, and
  the pass no longer carries a draft or a width past the bite test.

  The reason is what went wrong at `0.02`: an ordinary round loaded the run at 0.065 N·m against ~0.4 N·m of
  headroom - **16 %**, where the design's own worked pass claimed *"about 15 % to spare"*, i.e. 85 %. The
  two-round model quartered the draft and the re-cut narrowed the entry piece; neither touched the scale,
  and nothing could have caught it, because a geometry-derived load silently follows any change to the
  geometry. A declared demand cannot drift that way.

  ★ What changed for the player: a hot pass now costs the same wherever it is on the schedule, one bridged
  wheel carries it with 15 % to spare, and **cooling is the only thing that stalls one** - a piece that
  drops below rolling heat asks up to 3.4 N·m, which nothing on an iron-tier line can hold. That last part
  was already true before this landed, but by accident rather than by design.
- **`MaxWidth` and the section law**, split out of item 4. The settled cap is `min(roll-set barrel,
  stock-form cap)` and item 4's "MaxWidth moves to the roll set" is the older reading; the form keeps a cap
  (the bar's 9, skelp's 8) or those geometries stop being expressible. Item 5's section law is the same
  edit's other half.
- Craftability. No recipe, no RCC, no construction stages - for the mill, the roll sets or the stock.
  Everything is creative-only.
- Art wiring. The roll sets ship `game:item/ingot` while the roll shapes sit drawn and unwired; the
  mill renders all four roll families at once and never plays its `cycle` clip.
- A handbook page. The def declares a `rollingmill-*` handbook group with no page behind it.
- `RollSetValidation` never checks that an output code resolves, which is how four dangling codes ship.
- The reheat side. The mill's whole heat budget assumes a furnace that is a shell
  ([reheat furnace](reheat-furnace.md)); reheat and cooling should both scale on `k·A/V`, with no ×2
  furnace multiplier.
- Where the stock comes from. Puddling → helve shingling is the settled route and neither exists;
  puddling is additionally blocked by B8.
