# Shear
**Status** designed - nothing built (no block, no BE, no recipe, no shape)   **Mod** iwex (`IronworkingExpanded`)

**Owns**
* the crop station: the rule that every crop in the forming ladder passes through this one block, and that the
  mill therefore has no claim gesture at all;
* the relocation of `Outputs` / `OutputAt` off `RollSetSpec` onto the shear, and the fact that they key on
  stage, not on gap (so a half-step is a legal product point);
* the crop-not-convert rule: a crop takes one product's worth of metal and leaves the remainder on the deck as
  stock;
* the shear-cuts-across / die-cuts-out verb split, and why the steam hammer never shears;
* the cold-cut torque gate - that cold shearing is decided by `MinTorque` against network drive, not by tier
  and not by a thickness constant, and that this is `MinTorque`'s first and only consumer;
* that there are no hand shears and no carried shear of any kind;
* the shear's own footprint, drive contract, tooling (blade sets) and drops.

**Depends on**
[mp-energy](../mechanics/mp-energy.md) (the run it loads, `IMpEnergyConsumer`, the flywheel that carries a
pulsed bite) ·
[recoverability](../mechanics/recoverability.md) (the ≤ 32 / ≤ 48 invariant, the two mandatory crop points,
and why cold shear is not a third escape) ·
[multiblock & fillers](../mechanics/multiblock.md) (why a 1 × 1 station needs neither filler nor multiblock) ·
[density rule](../mechanics/density-rule.md) (why every crop divides exactly) ·
[recipes & config](../mechanics/recipes-config.md) (the code-first def, the cost catalogue, goldens) ·
[rolling mill](rolling-mill.md) (the stock it crops and the stage it arrives at) ·
[reheat furnace](reheat-furnace.md) (the alternative to a cold cut) ·
[rolling](../processes/rolling.md) · [STATE.md § placement rule](../../plans/STATE.md)

---

## Role

A crop shear stood at the end of every mill line and cut the long rolled piece to length, which is what lets
the mill be a pure reduction machine: two verbs, two stations.

| Verb | Station | What it does |
|---|---|---|
| reduce | [rolling mill](rolling-mill.md) | thins the piece; the player pulls it out at whatever stage they want. It only ever makes stock |
| crop | this | turns stock at a stage into a product |
| blank / stamp | [steam hammer](steam-hammer.md) (lpex) | punches a shape out of a strip - geometry, therefore forging work |

- The mill has no product stage today. `RollSetSpec.OutputAt` (`RollSetSpec.cs:95`) has no caller anywhere in
  `src/`; `BlockEntityRollingMill.CompletePass` (`:282-288`) writes the thinned piece back onto the same stack
  and ejects it, so rolling produces nothing but thinner stock. The shear is where a schedule ends.
- Two mandatory crops in the ladder exist only to keep stock reheatable
  ([recoverability](../mechanics/recoverability.md)). Without a station that can cut, those crops cannot happen
  and the cast tier soft-locks.
- Cropping is what stops part-rolled and cast stock needing a rounding rule. Take a plate off an 8 × 1 strip
  and the rest of the strip is still stock: reheat it, roll it thinner, or pile it into the next bar. Nothing
  has to divide.

It is MP-powered: every shear on the 1867 plate has a gear train and a flywheel, and parting a plate is not a
thing arms do. A carried shear is also forbidden by the station principle - it would let a piece be claimed
away from the line.

---

## Structure

1 × 1 × 1. No fillers, no multiblock, no projection. At one cell the block is simply its own
`BlockNetworkNode` on the `"mpenergy"` graph, the same as the nail and heading benches. Nothing here needs the
footprint machinery in [multiblock](../mechanics/multiblock.md); the one thing to copy from the mill is that a
shape may overhang its cell as long as the block is `SolidNonOpaque` (`BlockRollingMill.cs:62-63`).

| Aspect | Proposal | Why |
|---|---|---|
| Footprint | 1 × 1 × 1 | cheap by design - the player builds one per mill line, and the nail-works reference is a row of small machines on one shaft |
| Orientation | `ns` / `we`, shaft along the orientation axis, exactly as the mill (`BlockRollingMill.cs:41-59`, `:106`) | so a shear lines up on the same line shaft as the mills it serves |
| Drive faces | connectors on the two faces along the shaft axis | a shear sits in the line, not on a spur |
| Throat | the face opposite the blade nest; the player feeds from there | Fig 5's open throat; also what makes the feed face readable without a label |
| Direction | does not implement `IMpEnergyDirection` | direction is last-writer-wins across a run (`MpEnergyNetwork.cs:75-77`); only the mill needs it, and a second writer would silently flip the mill's decks |

---

## Assets

Nothing is drawn. No editable shape, no runtime shape, no texture set, no animation, no lang key, no handbook
page.

| Asset | State |
|---|---|
| editable shape | missing - nothing under `assets/editable/shapes/` is a shear (`machine-megablock-nailcutter.json` is the [nail machine](nail-machine.md)) |
| runtime shape | missing - `assets/iwex/shapes/forming/` holds only `rollingmill.json` and the ten stale `stock-*.json` |
| reference art | `assets/editable/refs/rivetsnails/machine-tools-1-rivet-making-machine-2-riveting-machine-3-shearing-machine-for-bars-of-all-lengths-and-scrap-iron-4-punching-and-shearing-machine-5-double-shearing-machine-1867-technology-RY93PB.jpg` - Figs 3, 4 and 5 |
| lang | `assets/iwex/lang/en.json` carries no `shear-*` key |
| handbook | `docs/iwex/handbook/` has no page; the sync pipeline joins on the `NN-` prefix and drift fails a test |

Draw Fig 5, the double shearing machine: the heaviest and most legible of the three, and its giant flywheel is
the right visual promise for a machine that takes one enormous bite and then nothing. Elements the design asks
for: a heavy cast bed / C-frame, a big flywheel on a geared shaft, a ram driven by a vertical connecting rod, a
blade nest at one end and an open throat to feed.

Animation: one stroke clip plus an `idle` rest pose. Two conventions are fixed by the network and must be
obeyed - clips are authored as one revolution so playback is `ω / 2π` (`EnergyAnim.cs:23-24`), and a running
clip must repeat or the animator drops the suppressed mesh back to the static shape
(`BlockEntityPuddlingChimneyCap.cs:31-33`).

---

## Construction

There is no recipe, a blocker of the same class as B3: the mill has none either (`grep rollingmill
src/IronworkingExpanded/Recipes/` returns nothing), so the whole forming line is creative-only.

Proposed cost, in the established idiom (`ExRecipeDef` grid, [recipes &
config](../mechanics/recipes-config.md); the design table at `CraftingStationRecipeDefinitions.cs:27-35` is
the shortest worked example):

| Slot | Ingredient | Rationale |
|---|---|---|
| frame | cast-iron plate ×4 (`castplate` / `castplate-heavy`) | cast bed - the shared cast-iron prerequisite that also unlocks the shafting, so there is no second tier to gate |
| blade seat | `game:metalplate-iron` ×2 | proposed |
| fasteners | `Nails(1)` - `ExIngredients.cs:36` | every other iwex machine bill uses the same helper (`PipeRecipeDefinitions.cs:25`) |
| tool | `Hammer` - `ExIngredients.cs:22` | convention |

A cost-catalogue key `shear-grid` belongs in `IwexRecipeConfig.DefaultCatalogue` (`IwexRecipeConfig.cs`) so the
recipe rescales with `RecipeLevel`.

Blade sets are separate tooling, not part of the block: STATE.md's D9 names "the shear's blade sets" among
crucible steel's consumers. They are unspecified - see Open.

---

## Operation

```
stock at a named stage  ──RMB on the throat──▶  one product  +  the remainder, still stock
```

| Verb | Effect |
|---|---|
| RMB with stock on the throat face | crop: one product of the stage's `Outputs` entry leaves; the remainder stays a `WorkPiece` at the same stage |
| RMB with a blade set | fit / swap the tooling, refused mid-stroke - mirror `TryFitRollSet` (`BlockEntityRollingMill.cs:143-155`) and its block-side handler (`BlockRollingMill.cs:296-320`) |
| Sneak + RMB | take the fitted blade set back (or: crop the other half of a two-piece split - one of the two, not both; see Open) |
| RMB with a wrench while jammed | free the piece unchanged, exactly as the mill does (`BlockRollingMill.cs:265-291`) |

States. Idle → one stroke (a pulse of work drawn from the flywheel) → product ejected onto the far side,
remainder handed back. A stroke that the run cannot carry does not half-cut: the reduction of a mill pass is
only committed on completion (`BlockEntityRollingMill.cs:280-288`) and a crop must be at least as atomic,
because a half-cut piece has no representable state.

Hot and cold.

| Cut | Gate |
|---|---|
| hot | free - shearing needs no friction, only force, so `RollingPass.CanBite`'s temperature rule (`RollingPass.cs:52-57`) has no analogue here |
| cold | the run must deliver at least the blade set's `MinTorque`. Cold stock parts more cleanly than hot, which smears; what limits it is force |

Cold shearing is therefore a power achievement, not a tier unlock: continuous rather than a step, needing no
second block and no mod dependency, and it gives the large flywheel (`FlywheelInertiaLarge = 150`, 15× the
normal disc) a second job. It is not a recoverability escape; see [recoverability § why cold shear is not a
third escape](../mechanics/recoverability.md).

---

## Numbers

Everything on this page is proposed; the shear has no config section, no keys and no code. The right-hand
column is what exists today and what it would be read from.

| Key | Proposed value | file:line | What it does |
|---|---|---|---|
| `ShearStrokeMs` | 250 ms | - (mirror `PassTickMs`, hard-coded at `BlockEntityRollingMill.cs:41`) | stroke tick; the mill's own tick is a private const, not config, and the shear should not copy that mistake |
| `ShearStrokeEnergy` | ≈ 1 stroke ≙ one mill pass's demand | - | drawn from the run as a pulse; sized against `RollingTorqueScale = 0.02` and its calibration note (`IwexConfig.cs`) |
| `ShearColdTorqueMultiplier` | ×3 over the hot cut | - | the whole cold-cut gate is this number against `MinTorque` |
| `ShearMinTorqueHot` | 0.2 | - | matches the `flat` set's shipped `minTorque` so a starter waterwheel carries a hot thin crop |
| `RollSetSpec.MinTorque` | (existing field, moves here) | `RollSetSpec.cs:30` (doc), `:37` (field), `:198` (parsed) | parsed, stored and never consulted - a repo-wide grep finds no read. The shear is its first consumer |

Shipped `minTorque` values, for calibration only - these belong to the [roll sets](../items/roll-sets.md) and
are cited, not owned:

| Set | `minTorque` | file:line |
|---|---|---|
| `flat` | 0.2 | `RollSetItemDefinitions.cs:68` |
| `grooved` | 0.3 | `:92` |
| `slitting` | 0.4 | `:102` |
| `flatwide` | 0.5 | `:80` |

Hard-coded values that will bite:

| Constant | Value | file:line | Note |
|---|---|---|---|
| network tick | 1000 ms | `BlockNetworkModSystem.cs:42-45` | the run's `dt`; a stroke faster than this reads a stale ω |
| mill pass tick | 250 ms | `BlockEntityRollingMill.cs:41` | `private const int PassTickMs` - not config |
| `MpMaxSpeed` | 2.0 rad/s | `ExlibConfig.cs:98` | capacity scales with its square; owned by [mp-energy](../mechanics/mp-energy.md) |

The crop points, stage lengths and product masses are not this page's: they belong to
[recoverability](../mechanics/recoverability.md) (the two mandatory crops), [density
rule](../mechanics/density-rule.md) (every mass) and [rolling](../processes/rolling.md) (the stage → product
table).

---

## Drops

| Broken | Returns |
|---|---|
| the shear | itself, one item (`MaxStackSize(1)` - the mill's convention, `BlockRollingMill.cs:53`) |
| the fitted blade set | spawned at the block, not destroyed - copy `BlockEntityRollingMill.OnBlockBroken` (`:374-383`), which spawns both the jammed piece and the roll set before calling base |
| a piece in the throat | handed back unchanged - a crop is committed only on completion |

There are no fillers, so none of [multiblock](../mechanics/multiblock.md)'s drop-rerouting applies.

---

## Code

Nothing exists. Where it hooks in:

| Piece | Where it goes | Model it on |
|---|---|---|
| `BlockShear` | `src/IronworkingExpanded/BlockStructures/Forming/Blocks/` | `BlockRollingMill.cs:31` - `BlockNetworkNode` + `IExBlockDefProvider`, minus `IFillerHost`/`IFillerInteractionTarget` (no fillers at 1 × 1) |
| `BlockEntityShear` | `.../Forming/BlockEntities/` | `BlockEntityRollingMill.cs:33` - `BlockEntityNetworkNode`, `IMpEnergyConsumer`, `NetworkType => "mpenergy"` (`:35-39`), `LoadTorque(speed)` (`:314-326`) returning 0 while idle |
| speed read | inside the stroke tick | `(NetworkSystem?.GetNetworkAt(Pos) as MpEnergyNetwork)?.State?.Speed ?? 0f` - `BlockEntityRollingMill.cs:79-81` |
| `ShearDecision` (pure) | `.../Forming/ShearFeed.cs` | `MillFeed.Decide` (`MillFeed.cs:95-128`) and `FeedDecision`/`FeedVerdict` (`:34`, `:6`) - a pure decision record is the house style and is what makes the rules testable headless |
| the torque gate | inside that decision | `RollingPass.CanCarry(loadTorque, availableTorque, speed)` - `RollingPass.cs:126-127`, which has no caller in `src/` either. It is the natural home of the cold-cut check |
| `Outputs` / `OutputAt` | move off `RollSetSpec` (`:28`, `:35`, `:95-101`) onto the shear's own stage table | small, and it unblocks the whole product half of the forming line |
| stage → product table | a code table with a `PathFor`-style formatter and a test that every reachable stage names a real item | the `HearthRows` / `SandBedLayout` treatment |
| the crop itself | needs `WorkPiece.Mass` and `.Length` | `WorkPiece.cs:35` has neither; `StripLength(t)` (`:115`) is a derived per-strip figure nothing compares to a limit |
| def + recipe | `Forming/ShearItemDefinitions.cs`-style provider and a `ExRecipeDef` grid | `BlockRollingMill.Definitions` (`:44-64`), `CraftingStationRecipeDefinitions.cs:23-36` |

Caller-side contract for anyone adding a product: declare a stage entry (`{stage, code, count, minTorque}`) -
the same tooling-owns-the-data idiom as `RollSetSpec` (`RollSetSpec.cs:9-24`) and `MoldSpec`
(`MoldSpec.cs:18-25`). No shear code should ever name a product.

---

## Gotchas

- `RollSetSpec.TryParse` rejects any output whose gap is not one of the barrel's gaps
  (`RollSetSpec.cs:172-176`). `castbillet` must crop at the 2.25 half-step, which is not a gap, so the billet's
  product cannot be declared under today's schema. Moving `Outputs` to the shear is what makes the cast tier
  expressible.
- `OutputAt` compares floats with `==` (`RollSetSpec.cs:98`). Keyed on stage, the table will hold values like
  `2.25` and `1.75` that arrive from float arithmetic, not literals. Key on an integer (`t × 100`) or compare
  with a tolerance - and format invariantly, `2.5` never `2,5`.
- A half-step is a legal place to leave a piece. That is why the shear keys on stage. Treating a half-step as
  "mid-schedule, no product" reintroduces the billet soft-lock.
- The mill must gain no claim gesture. With the shear owning the crop, `CompletePass`
  (`BlockEntityRollingMill.cs:282-288`) stays exactly as it is: reduce, write back, eject.
- `ColdShearMaxThickness` does not exist and must not be added. It is not a config key and appears in `src/`
  nowhere. The cold cut gates on torque, not thickness.
- The network exposes no drive torque. `MpEnergyNetworkState` carries `Speed`, `Inertia`, `StoredEnergy`,
  `SupplyPower`, `DemandPower`, `Reversed` (`MpEnergyNetworkState.cs:23-43`) - τ_drive is not among them, and
  `SupplyPower = τ_drive·ω` is unrecoverable at ω = 0, which is the state a shear starts from. Gating on
  `MinTorque` therefore needs either a new state field or a gate expressed in stored energy. This is the single
  largest unknown on the page.
- A run with no storage node has no state at all. `OnTick` nulls `State` when `Σ I ≤ 0`
  (`MpEnergyNetwork.cs:81-89`), so a shear wired to a bare bridge with no flywheel and no shaft segment sees
  `Speed == 0` and can never cut. Every practical run needs at least one shaft segment (`ShaftInertia = 0.5`).
- Do not let the shear declare direction. `IMpEnergyDirection` is last-writer-wins across the whole run
  (`MpEnergyNetwork.cs:75-77`); a second writer silently swaps the mill's input and output decks
  (`BlockEntityRollingMill.cs:236-247`).
- The steam hammer never sheared. A hammer forges, a shear parts.
- Blanking is not shearing. Turning a 15 × 1 × 16 `boilerplate` into three 9 × 1 × 9 plates cannot be done with
  straight cuts - 15 × 16 does not partition into 9 × 9 - so it is die work on the [steam
  hammer](steam-hammer.md), not a crop.
- 48 is the mill's refusal, 32 is only a seating switch. Power never relaxes either. See
  [recoverability](../mechanics/recoverability.md); getting this backwards breaks the cast tier outright.

---

## Open

- Nothing is built. Block, BE, def, recipe, shape, lang, handbook and tests are all absent. The forming build
  list says build the shear first of the three benches, because the `Outputs` move depends on it.
- How the torque gate reads drive torque - see Gotchas. Options: add `DriveTorque` to `MpEnergyNetworkState`;
  gate on `StoredEnergy ≥ k` instead; or have the shear attempt the stroke and let a stall be the answer
  (cheapest, and consistent with the mill's "a pass that overdraws simply freezes").
- Blade sets are named and unspecified. STATE.md's D9 lists them as crucible-steel consumers. Undecided:
  whether they are tooling in the `RollSetSpec` idiom (fitted, swappable, one at a time), a durability
  consumable, or both - and whether blade material gates thickness the way bit material gates the boring
  machine.
- Whether a crop yields two stacks or one plus a remainder.
  [recoverability](../mechanics/recoverability.md)'s crop table says "into 6" and "into 5", which reads as a
  full split in one action; the crop-not-convert rule reads as one product at a time. Both are defensible; they
  are not the same interaction and the code cannot do both by accident.
- The wide shear. The forming build list assigns "the wide shear" to the lpex steam hammer, i.e. a second
  cutting station with a different reach. Whether that is this block re-tooled, a hammer die, or a third
  machine is undecided; the verb split argues it must not be a die.
- Stroke pacing is untuned. `k` for cooling, the stroke energy and the cold multiplier are all playtest knobs,
  and none of the mp-energy calibration has been playtested either.
- `WorkPiece` cannot express a crop. No `Length`, no `Mass`; the two-round pass model that would simplify it is
  also unbuilt. The shear cannot be finished before that lands.
