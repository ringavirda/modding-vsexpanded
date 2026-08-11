# Rolling mill

**Status** blocked - the block, the pass simulation, the pass clock and the save/load are live and
pinned by 129 test methods across ten files; nothing in survival can craft it, and nothing in survival can feed it
(B3 / B4, plus the deck-reachability blocker B17 - see [Blockers](#blockers)).
**Mod** iwex (`IronworkingExpanded`)

**Owns**

* the rolling mill block and block entity - its 3 × 3 × 2 footprint, the two axle-bus cells, placement,
  break, drops and the deck/gap/strip interaction mapping;
* the pass lifecycle - `BeginPass` → `AdvancePass` → `CompletePass`, the stall rule, the wrench release,
  and the invariant that a reduction is committed only on completion;
* the pass physics in `RollingPass` - bite (`δ_max = μ²R`), contact arc, load torque, spread, length
  multiplier and the cooling law - and every `Rolling*` config key and the two friction constants;
* the roll-set spec format (`rollset` attribute, `TryParse` validation rules) and the shipped roll-set
  catalogue, plus the delta between it and the settled 2026-07-29 gap tables;
* the `WorkPiece` stack format (`stockForm` / `stripThickness` / `stripTurned`), the shipped strip model,
  and how it differs from the settled two-round model;
* `StockForm` - the two shipped forms and their spread exponents - and the composed work-piece mesh;
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
plate, sheet, rod or nail-rod, and the machines downstream (lpex's boilers especially) are made of rolled
parts. Historical basis: Cort's 1783-84 pairing of puddling and grooved rolling.

Hot work is the governing rule. `δ_max = μ²R` makes hot iron bite about thirty times deeper per pass than
cold, and the flow-stress term makes cold iron load about ten times harder, so a cooled piece both refuses
to enter and drags the run to a stop; the carry-back between passes is a heat budget, answered by the
reheat furnace.

There is no screw-down: the barrel carries a fixed sequence of gaps and the stock is walked along it. The
schedule is geometry - a segment cannot be skipped, because a jump deeper than `δ_max` skids.

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

Caution: `Deck()` returns one cell - the `z = ±1` cell at `x = 0`, directly beside the principal - while
the deck is three cells wide. See [Blockers](#blockers).

---

## Assets

| Asset | Path | State |
|---|---|---|
| Mill shape (runtime) | `assets/iwex/shapes/forming/rollingmill.json` | shipped; textures `iron5`, `cast-iron1`; clips `idle` + `cycle`, 30 frames each |
| Mill shape (editable) | `assets/editable/shapes/machine-mp-megablock-rollingmill.json` | drawn, not yet reflected in the runtime shape name |
| Roll-set item art | `assets/editable/shapes/item-finished-rollers-{flat,flatwide5,flatwide10,flatwide15,grooved}.json`, `item-rollers-flatwide20.json`, `item-sandcast-rollers-blank.json` | drawn and not wired - the item ships `game:item/ingot` (`RollSetItemDefinitions.cs:122`) |
| Stock stage shapes | `assets/iwex/shapes/forming/stock-{bloom,slab}-{5,10,15,20,30}.json` | shipped, 10 files, generated |
| Stock base art (editable) | `item-shingled-bar.json`, `item-shingled-slab.json` | drawn; the generator still expects the `item-shingledbloom` / `item-shingledslab` names, which do not exist |
| Axle cell | `exlib:block/empty` (`BlockRollingMillAxle.cs:39`) | intentionally invisible |
| Handbook page | — | none. `docs/iwex/handbook/` has no forming page, yet the def declares `Handbook("rollingmill-*")` (`BlockRollingMill.cs:54`) |

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
| `RollersFlatWide*` | 2.0 / 1.5 / 1.0 / 0.5 (four) | one `flatwide` item, gaps 2.0/1.5/1.0/0.5 (`:76`) | six single-gap sets, 1.0 … 3.5, `MaxWidth` 15, and they are lpex |
| — | — | `slitting`, gaps 0.5 (`:99`) | deleted |

The grooved barrel was drawn to the settled schedule; the config does not match it. The wide art is four
gaps where the settled train needs six, and includes the dropped 0.5.

### The stage shapes are generated, and the generator's inputs no longer exist

`scripts/generate-rolled-stock.py` derives all ten `stock-*.json` files from two authored bases
(`item-shingledbloom`, `item-shingledslab`), both gone from `assets/editable/shapes/`. The generated
outputs still ship and `RolledStockStagesTests` still measures them off disk, so the tests pass while the
pipeline cannot be re-run. The forming build list calls for deleting the generator and its ten outputs and
wiring the new authored art instead.

---

## Construction

There is no recipe. This is a blocker.

`grep`ping `src/IronworkingExpanded/Recipes/` for `rollingmill`, `rollset` or `stock-` returns nothing, and
there are no hand-written recipe JSON assets anywhere in the repo
([recipes & config](../mechanics/recipes-config.md)). The mill, the four roll sets and both stock items are
reachable only from the creative inventory (`.CreativeCommon(...)` at `BlockRollingMill.cs:60`,
`RollSetItemDefinitions.cs:127`, `StockItemDefinitions.cs:57`).

There is no construction path of any kind: the mill is a plain placed `BlockNetworkNode`, not a
RightClickConstructable, so it has neither a grid recipe nor build stages. The transmission is RCC-built
([flywheel & shafting](flywheel-and-shafting.md)).

The stock items have no source either - nothing produces a `stock-bloom` or `stock-slab`. The settled route
is puddling → helve shingling, neither of which is built ([shingling](../processes/shingling.md); puddling
is blocked by B8).

---

## Operation

### Verbs

| Held | Where | Result |
|---|---|---|
| a wrench (`Code.FirstCodePart() == "wrench"`, any domain) | anywhere on the machine, while rolling | frees the stuck piece, state unchanged (`BlockRollingMill.cs:265-291`) |
| a roll set (any collectible with a `rollset` attribute) | anywhere on the machine | fits it, hands back the previous one; refused mid-pass with `iwex-rollingmill-busy` (`:293-320`) |
| stock, right-click | the input deck | feeds the near strip (`MillFeed.StripIndex(false, sides) = sides-1`) |
| stock, sneak + right-click | the input deck | feeds the far strip (index 0) |
| anything | the output deck | nothing - a two-high stand cannot be fed backwards |

Where along the deck the click lands picks the gap: the hit point is taken into the mill's own frame, mapped
to `0..1` along the barrel, and split into `gapCount` equal bands (`BlockRollingMill.cs:368-375`,
`MillFeed.cs:62-89`).

### The pass

1. `TryFeed` re-splits the piece for this barrel (`ceil(width / barrelWidth)` sides), then asks
   `MillFeed.Decide` (`BlockEntityRollingMill.cs:193-237`).
2. On `Ok` the gap and strip are held as pending and `BeginPass` puts the piece under the rolls with the
   post-reduction width and length. The player's stack is taken (`BlockRollingMill.cs:344-347`).
3. Every 250 ms the mill advances the bite by `v = ωR · dt`, reading the live network speed so progress
   stays in step with the load the mill itself imposes (`:75-82`, `:333-371`).
4. The piece cools whether or not it is moving, so a jam is self-worsening (`:341-346`).
5. When `remaining` reaches 0, `CompletePass` applies the pending reduction and spawns the piece on the
   output deck at `+0.5, +0.6, +0.5` with a near-zero upward velocity (`:282-303`).

Nothing is committed mid-pass. Losing the drive, letting the stock go cold, breaking the mill or wrenching
the piece out all leave the reduction unapplied and the gap simply redone. That is what `_pendingGap` is for.

### States

| State | Condition | Reported as |
|---|---|---|
| idle | `_remaining == 0` | `iwex:rollingmill-info-idle` |
| rolling | `_remaining > 0`, travelling | `iwex:rollingmill-info-rolling` + temperature |
| stalled | `_remaining > 0`, `travelled ≤ 0` (ω = 0 or stock below rolling heat) | `iwex:rollingmill-info-stalled` + temperature |

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

### The three relations

| Relation | Formula | file:line |
|---|---|---|
| Bite | `δ_max = μ²R` | `RollingPass.cs:43-44`, `:52-57` |
| Contact arc | `L_c = √(R·δ)` | `:88-89` |
| Load torque | `T = Y·w·L_c²·k = Y·w·R·δ·k` | `:101-118` |
| Flow stress | `Y = 1` hot; `1 + (m−1)·clamp((T_roll−T)/span, 0, 1)` below | `:71-84` |
| Spread | `w = w₀·(t₀/t)^e`, capped at `maxWidth` | `:146-160` |
| Length | `L = L₀ · (t₀/t) / (w/w₀)` - from the actual, possibly capped, width | `:168-176` |
| Cooling | `T ← ambient + (T−ambient)·e^(−rate·dt)` | `:190-196` |
| Travel | `Δs = ω · R · dt`, zero below rolling heat | `BlockEntityRollingMill.cs:370-373` |

### Config — iwex, `ex_values.json` (domain `iwex`)

| Key | Value | file | What it does |
|---|---|---|---|
| `RollingTempC` | `900` | `IwexConfig.cs` | the hot/cold line; range `[0, 3000]`. Below it friction collapses and flow stress climbs |
| `RollingColdStressMultiplier` | `10` | `IwexConfig.cs` | flow stress of fully-cold stock; range `[1, 1000]` |
| `RollingColdSpanC` | `400` | `IwexConfig.cs` | degrees below `RollingTempC` over which the multiplier is reached; range `[1, 3000]` |
| `RollingRollRadius` | `4` | `IwexConfig.cs` | roll radius in block-space units; sets `δ_max`, the contact arc and the travel speed; range `[0.01, 100]` |
| `RollingTorqueScale` | `0.02` | `IwexConfig.cs` | raw torque → network units; range `[0, 10000]` |
| `RollingCoolRate` | `0.005` | `IwexConfig.cs` | fraction of excess heat shed per second, under the rolls and during the carry-back; range `[0, 10]` |
| `RollingAmbientC` | `20` | `IwexConfig.cs` | what the stock cools toward; range `[-50, 500]` |

`RollingTorqueScale`'s calibration comment is derived from the network's numbers, not the mill's - one
bridge drive less friction at ω_max leaves the headroom it is scaled against. Retuning `MpFrictionCoeff`,
`MpIdleTorque`, `MpMaxSpeed` or `FlywheelBridgeChargePower` moves the mill's balance with it; see
[mp-energy](../mechanics/mp-energy.md).

Caution: the reheat furnace's `MeltingPoint` is bound to `IwexValues.RollingTempC`
(`BlockEntityHeatingFurnace.cs:61`) - the mill's hot/cold line doubles as the reheat furnace's target.
Changing one changes the other silently.

### Hard-coded — not config, source-only

| Constant | Value | file:line | Note |
|---|---|---|---|
| `RollingPass.HotFriction` | `0.5` | `RollingPass.cs:33` | `public const`. With `R = 4` gives δ_max = 1.0 |
| `RollingPass.ColdFriction` | `0.09` | `RollingPass.cs:36` | `public const`. δ_max = 0.0324 - ~30× worse, hence a hot mill |
| `WorkPiece.FeedsPerSide` | `2` | `WorkPiece.cs:38` | pass, turn over, pass again |
| `MillFeed.DeckCells` | `3` | `MillFeed.cs:78` | cells the gap bands are spread across |
| `MillFeed.DeckOriginOffset` | `2` | `MillFeed.cs:81` | deck offsets run `−2 … 0` in the mill frame |
| `StockMesh.CentreX` | `8` | `StockMesh.cs:27` | base shapes are authored centred on x = 8 |
| Axle offsets | `(−1,0,0)`, `(−2,0,0)` | `BlockRollingMill.cs:99` | `static readonly Vec3i[]` |
| Deck offsets | `z = ±1`, `x = 0` | `BlockEntityRollingMill.cs:274-281` | one cell per deck - see [Blockers](#blockers) |
| `stackSize` | `1` | `BlockRollingMill.cs:53`, `StockItemDefinitions.cs:42`, `RollSetItemDefinitions.cs:125` | every stock piece carries its own state, so they can never merge |

### Shipped roll-set catalogue — `RollSetItemDefinitions.cs:56-104`

| Variant | Family | Accepts | Gaps | Barrel | `minTorque` | Outputs | file:line |
|---|---|---|---|---|---|---|---|
| `rollset-flat` | flat | `bloom`, `billet` | 2.0 / 1.5 / 1.0 / 0.5 | 6.0 | 0.2 | 1.0 → `iwex:rolledplate-iron`; 0.5 → `iwex:rolledsheet-iron` | `:62-69` |
| `rollset-flatwide` | flat | `slab`, `bloom` | 2.0 / 1.5 / 1.0 / 0.5 | 16.0 | 0.5 | same two | `:73-81` |
| `rollset-grooved` | grooved | `bloom`, `billet` | 1.0 / 0.5 | 16.0 | 0.3 | 1.0 → `game:rod-iron`; 0.5 → `iwex:wirerod-iron` | `:86-93` |
| `rollset-slitting` | slitting | `plate` | 0.5 | 16.0 | 0.4 | 0.5 → `iwex:nailrod-iron` | `:96-103` |

Caution: four of the five output codes do not exist anywhere in `src/` - `iwex:rolledplate-iron`,
`iwex:rolledsheet-iron`, `iwex:wirerod-iron`, `iwex:nailrod-iron`. `TryParse` only checks that an output's
gap is one of the barrel's gaps (`RollSetSpec.cs:172-175`); it never resolves the code, so
`RollSetValidation` passes them.
`slitting` accepts `"plate"`, which is not a `StockForm` - `StockForm.All` holds only `bloom` and
`slab` (`StockForm.cs:57-64`). No piece can ever satisfy it.
`billet` is likewise accepted by two sets and is not a `StockForm` either.

### `StockForm` — `StockForm.cs:48`, `:54`

| Form | Base w × t | `MaxWidth` | `BaseLength` | Spread `e` | Width at 2.0 / 1.5 / 1.0 / 0.5 |
|---|---|---|---|---|---|
| `bloom` | 3 × 3 | 8 | 16 | 0.846 | 4.23 / 5.39 / 7.60 / 8.00 (capped) |
| `slab` | 8 × 3 | 14 | 20 | 0.463 | 9.65 / 11.03 / 13.30 / 14.00 (capped) |

The bloom's exponent is tuned so it reaches nearly its full 8 wide exactly at the 1.0 gap - one voxel thick
and about eight wide being vanilla plate proportion. The two exponents are correct against the intended
numbers and should not be retuned.

### Worked pass — fresh bloom, first flat gap

Fresh bloom, `sides = 1` (3 wide ≤ 6 barrel), fed at gap 2.0:

| Quantity | Value |
|---|---|
| draft δ | `3.0 − 2.0 = 1.0` - exactly `δ_max` |
| width after | `3 · 1.5^0.846 = 4.23` |
| length after | `16 · 1.5 / 1.4093 = 17.03` |
| load torque, hot | `1 · 4.23 · 4 · 1.0 · 0.02 = 0.338 N·m` |
| load torque, fully cold | `10 ×` that = `3.38 N·m` |
| pass duration at ω_max | `17.03 / (2 · 4) ≈ 2.1 s` |

Against the ~0.4 N·m of drive headroom one bridged waterwheel leaves ([mp-energy](../mechanics/mp-energy.md)),
the hot pass runs with about 15 % to spare and the cold one stalls by a factor of eight.

### Cited — owned elsewhere

The mill's pass-tick interval (`BlockEntityRollingMill.cs:42`) and the network tick it samples belong
to [mp-energy](../mechanics/mp-energy.md); the clock that carries it belongs to
[framework composition](../mechanics/framework-composition.md). The stock items' `materialUnits`, `MaterialDensity`,
`combustibleProps.meltingPoint` and `temperatureDamage` (`StockItemDefinitions.cs:23-27`, `:43`, `:48-56`)
belong to [stock](../items/stock.md); the shipped `bloom` / `slab` unit figures predate the settled
`shingledbar` / `shingledslab` masses. The bevel-gear item and every `Mp*` / `Flywheel*` / `ShaftInertia`
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
| `.Feed` | `:322-361` | gap zone + strip from the click, then `TryFeed`; consumes the held stack on `Ok` |
| `.AlongBarrel` | `:368-375` | world hit point → mill frame → `0..1` |
| `BlockRollingMillAxle` | `Forming/Blocks/BlockRollingMillAxle.cs:20` | invisible graph node, not a filler; `OnNeighbourBlockChange` empty by design (`:59-63`) |
| `BlockEntityRollingMill` | `Forming/BlockEntities/BlockEntityRollingMill.cs:24` | `BlockEntityNetworkNode` + `IMpEnergyConsumer` + `IProductionReadiness` |
| `.HostProcess` | `:48-53` | the 250 ms pass clock, a hosted `BEBehaviorProductionMachine`; added in the constructor (`:28`) |
| `.IsReadyToProduce` / `.StopsProductionWhenNotReady` | `:59`, `:63` | a pass is the gate; an empty stand keeps its clock |
| `.OnPassTick` | `:89-93` | reads the live run's ω and advances the bite by a bounded `dt` |
| `.LoadTorque(speed)` | `:336-350` | the consumer contract; speed-independent by design |
| `.AdvancePass(dt, speed)` | `:355-393` | cool → travel → stall-or-progress → `CompletePass` |
| `.TryFeed` | `:193-237` | re-split, `Decide`, arm the pending reduction, `BeginPass` |
| `.CompletePass` | `:302-311` | the only place the stock changes |
| `.ReleaseStuckPiece` | `:243-256` | the wrench recovery |
| `.ToTree` / `.FromTree` | `:428-460` | draft, width, temp, remaining, stalled, pending gap+strip, piece, roll set; both stacks re-resolved |
| `RollingPass` | `Forming/RollingPass.cs:30` | pure physics; the entire model, pinned headless |
| `MillFeed.Decide` | `Forming/MillFeed.cs:95-128` | pure feed verdict |
| `RollSetSpec` | `Forming/RollSetSpec.cs:31` | the tooling record; `TryParse` at `:108-201` |
| `RollSetValidation.Validate` | `Forming/RollSetValidation.cs:20-32` | `AssetsFinalize` sweep; a malformed set is logged rather than failing silently |
| `WorkPiece` | `Forming/WorkPiece.cs:35` | the per-stack record |
| `StockForm` | `Forming/StockForm.cs:28` | authored base dimensions + spread |
| `StockMesh.SideOf` | `Forming/StockMesh.cs:36-59` | per-side scale/offset for the composed mesh |
| `ItemStockPiece.OnBeforeRender` | `Forming/Items/ItemStockPiece.cs:26-48` | composes and caches the part-rolled mesh |
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

`test/IronworkingExpanded.Tests/Blocks/Forming/` - ten files, 129 `[Fact]`/`[Theory]` methods:
`RollingPassTests` (21), `RollingMillFeedTests` (19), `RollingMillTests` (17), `MillFeedTests` (15),
`RollSetSpecTests` (15), `RollingMillLoadTests` (10), `WorkPieceTests` (9), `RolledStockStagesTests` (9),
`StockMeshTests` (8), `RollingMillClockTests` (6).

`RollingMillClockTests` is the only one that drives the mill's real listener; every other file calls
`AdvancePass` directly with a `dt` of its own, so the clock and the physics are pinned separately.

Caution: the suite's blind spot is the bootstrap. Every mill test builds its work piece with
`WorkPiece.Fresh(...).ToStack(stack)` by hand (`RollingMillFeedTests.cs:62-66`). No production code path
does that, which is why B3 is invisible to the tests.

---

## Blockers

### B3 — no stock item carries a `stockForm` the mill can read

`StockItemDefinitions.cs:45` writes `stockForm` through `ExItemDef.Attribute(...)`, which lands in the
itemtype's `attributes` block, i.e. `CollectibleObject.Attributes`
(`ExpandedLib/Definitions/ExItemDef.cs:207-211`).

`WorkPiece.FromStack` reads `stack?.Attributes` - the per-stack `ITreeAttribute` tree
(`WorkPiece.cs:162-168`). Those are two different stores. A freshly created `stock-bloom` therefore has no
work-piece state at all, `FromStack` returns `null`, and `MillFeed.Decide` short-circuits to `WrongForm`
(`MillFeed.cs:107`).

The only two production `ToStack` calls are inside `TryFeed` (`:182`) and `CompletePass` (`:285`), both of
which require `FromStack` to have already succeeded. There is no bootstrap. Something must seed the stack
tree at item creation (an `OnCreatedByCrafting`/`OnHeldIdle` hook, or `FromStack` falling back to the
collectible attribute).

### B4 — grooved cannot bite fresh stock

Grooved's first gap is `1.0` (`RollSetItemDefinitions.cs:89`) and every accepted form enters 3.0 thick, so
the draft is 2.0 against `δ_max = 0.5² · 4 = 1.0` → `WontBite`, always. The settled schedule
(2.5 / 2.0 / 1.5 / 1.0) fixes it; the drawn barrel already has those four grooves.

### B17 — only the middle deck cell is an input, so most gaps are unreachable

`Deck()` returns the single cell at offset `(0, 0, ±1)` (`BlockEntityRollingMill.cs:274-281`) and
`IsInputDeck` compares against exactly that (`:260`). The deck is three cells wide, and
`MillFeed.AlongBarrel` maps the mill-frame `x` over the range `−2 … 0` (`MillFeed.cs:78-89`).

A click on the only accepted cell has `localX ∈ [0, 1)`, so `AlongBarrel ∈ [0.667, 1.0)` - the last third
of the barrel. With the flat set's four gaps that reaches only zones 2 and 3 (gaps 1.0 and 0.5); gaps
2.0 and 1.5 are unreachable. Fresh 3.0 stock at gap 1.0 is a 2.0 draft and at 0.5 a 2.5 draft - both beyond
`δ_max`. So even with B3 fixed, the flat set still cannot take a first bite through the deck.

The fix is one of two: accept all three deck cells in `IsInputDeck`, or shrink `DeckCells`/`DeckOriginOffset`
to the one cell that is actually clickable. The former matches the class doc
(`BlockRollingMill.cs:26-28`) and the barrel-walking design; the latter does not.

`MillFeedTests.cs:99-100` pins `GapZone(AlongBarrel(−2.0), n) == 0`, exercising a `localX` the block can
never produce.

### B12 — `castbloom`'s 50-long 1.0 stage

A soft-lock; cold shear is not an escape ([recoverability](../mechanics/recoverability.md)). Owned there.

### Downstream: the mill's own output has nowhere to go

Four of five output codes do not exist, and `OutputAt` has no production caller - see
[Open](#open).

---

## Gotchas

- `TryFeed` ignores `BeginPass`'s return value (`BlockEntityRollingMill.cs:225-236`). It arms
  `_pendingGap` / `_pendingStrip`, calls `BeginPass(...)` discarding the `bool`, and returns the accepted
  decision regardless. `BlockRollingMill.Feed` then takes the stack out of the player's hand
  (`:344-347`). If `BeginPass` ever refuses - it re-checks `CanBite`, and rejects `length ≤ 0` or
  `width ≤ 0` (`:122-132`) - the item is destroyed silently. Today the two checks happen to agree; the
  contract is unguarded.
- A refused offer still mutates the held stack. `TryFeed` re-splits the piece and writes it back with
  `piece.ToStack(stack!)` before `MillFeed.Decide` runs (`:203-209`). `Resplit` allocates a fresh
  `Turned` array (`WorkPiece.cs:73-76`), so a rejected feed against a differently-sized barrel silently
  clears the "first pass done" flags. Move the write-back inside the accepted branch.
- `CancelPass` destroys the piece. It nulls `_piece` without ejecting (`:289-296`), unlike
  `ReleaseStuckPiece` and `OnBlockBroken`, which both return it. No production caller today (tests only), so
  it is latent, but it is one call site away from an item-loss bug.
- `CancelPass` does not clear `_pendingGap`/`_pendingStrip`; `ReleaseStuckPiece` clears only
  `_pendingGap`. `_pendingStrip` is never reset anywhere (`:226`, `:289-296`). Harmless only because
  `TryFeed` overwrites both before every pass.
- A malformed roll set reports "busy". `TryFitRollSet` returns `false` both when a pass is running and
  when `TryParse` fails (`:163-182`), and `FitRollSet` maps every `false` to
  `iwex-rollingmill-busy` (`BlockRollingMill.cs:312`). The gate that got you there only checked that the
  `rollset` attribute exists (`:293-294`).
- `RollSetSpec.Outputs` is a `Dictionary<float, string>` compared with `==` (`RollSetSpec.cs:96-101`).
  It works only because the same literals flow through the same `float` conversion; any derived thickness
  (a half-step, for instance) will miss. This is one of the reasons `Outputs` must move to the shear and key
  on stage, not gap.
- `RollSetSpec.MinTorque` is parsed, stored, validated and never read. No production call site; the
  only reference is `RollSetItemDefinitions.cs:13`'s prose. The settled design makes it the
  [shear](shear.md)'s cold-cut gate, which would be its first real use.
- So is most of `RollSetSpec` and half of `WorkPiece`. `PassesAt`, `PassesForGap`, `NextGap`,
  `NextDraft`, `OutputAt`, `Outputs`, `IsWide`, `OverhangsBarrel`, `Thickest`, `Thinnest`, `WidthAt` and
  `RollingPass.CanCarry` have no callers in `src/`, only tests. The "product / stopping point" half of
  the design is written but not wired.
- `ColdShearMaxThickness` does not exist in `src/` at all. Any design text that names it is describing a
  constant that was never written; the settled design deletes the concept ([shear](shear.md)).
- The composed work-piece mesh is only used for uneven pieces. `ItemStockPiece.OnBeforeRender` returns
  early when `piece.Sides <= 1 && piece.IsEven` (`:36`) - correct today, but under the settled two-round
  model a piece can never be uneven, so the whole composition path becomes dead and the half-step becomes
  the thing that needs art.
- `WorkPiece.Resplit` silently no-ops on an uneven piece (`WorkPiece.cs:69-70`). By design - an uneven
  piece is meant to fail to bite until its sides are levelled - but it means a piece carried between mills
  with different barrels can enter a state where neither barrel will take it.
- Stale doc: `RollSetSpec.cs:22-23` says "a flat set running 2.0 → 1.5 → 1.0 → 0.5 yields plate at 1.0
  and sheet at 0.5" - both output codes are unresolvable, and 0.5 is dropped by the settled schedule.
- Stale doc: `RollSetItemDefinitions.cs:58-61` explains the flat set's "2, 2, 4, 4" schedule cost off
  `barrelWidth: 6.0`. The settled barrel is 4, which changes every one of those counts.
- Stale doc: `StockItemDefinitions.cs:33-36` says a part-rolled piece "really does read thin down one
  side"; the settled model deletes lopsided pieces entirely.
- The settled `WorkPiece` ("one thickness and a per-side fed-this-round flag") is design, not code. The
  shipped record is `Strips: float[]` + `Turned: bool[]` (`WorkPiece.cs:35`). The design is ahead of the
  code here, not behind it.

---

## Open

Sizes are the forming build list's.

| # | Work | Notes |
|---|---|---|
| 0 | Fix B3 - seed the stack tree, or make `FromStack` fall back to the collectible attribute | nothing else can be play-tested until this lands |
| 0b | Fix B17 - accept all three deck cells | see [Blockers](#blockers); B4's fix alone is not sufficient |
| 1 | `Outputs` / `OutputAt` move to the shear, keyed on stage, not gap | the mill only ever makes stock; there is no claim gesture on the mill. The shear (a 1 × 1 `mpenergy` station) must be built first |
| 2 | `WorkPiece.Mass` + `.Length`; the 48-voxel refusal | the refusal lives on the mill; the invariant is [recoverability](../mechanics/recoverability.md)'s |
| 3 | The two-round pass model - `Strips[]` + `Turned[]` → one `Thickness` + a per-side fed-this-round flag; round 1 lands the half-step, round 2 the gap; `sides = ceil(entryWidth / barrelWidth)`; the round resets on a different barrel. Deletes `StripWidth`, `StripLength`, `Thickest`, `Thinnest`, `IsEven`, `Resplit` | a net deletion; retires the lopsided art |
| 4 | Config: `flat` barrel 6 → 4, gaps 2.5 / 2.0 / 1.5 / 1.0; `grooved` the same four; delete `slitting`; delete the four dangling output codes; `MaxWidth` moves to the roll set | config only; also fixes B4 |
| 5 | Section law `StockForm` → `RollSetSpec` (`square` \| `flat`); `SpreadWidth` gains a square branch | |
| 6 | `StockForm`: `bloom` / `slab` → `shingledbar` 3 × 3 × 18 and `shingledslab` 8 × 3 × 20 | masses belong to [stock](../items/stock.md) |
| 7 | Wire the authored stock art; delete `generate-rolled-stock.py` and its ten outputs | its two inputs are already gone |
| 15/16 | The wide train is lpex - four ordinary mills at 2.5 / 2.0 / 1.5 / 1.0 on one shared drive shaft, plus six single-gap `flatwide` items (1.0 … 3.5, `MaxWidth` 15). No new block: the mill block stays iwex and lpex ships the sets | smex bolts two more stands (3.5 / 3.0) onto the front |

Not on that list, and still open:

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
