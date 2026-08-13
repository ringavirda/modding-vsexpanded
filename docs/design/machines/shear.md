# Shear
**Status** designed, **drawn**, and the **registry and the crop tally are built** (2026-08-12 / 08-13).
Missing: the block, the BE, the runtime shape and the table itself. `ProcessJob` / `ProcessJobRegistry` /
`ProcessJobLoader` in exlib are the terminal shape this page's crop table will be declared in, at
`assets/<domain>/config/processjobs/*.json`, and `WorkPiece.Cropped` is the per-stack state a crop moves.
No entry ships yet: see Open.   **Mod** iwex (`IronworkingExpanded`)

**Owns**
* the crop station: the rule that every crop in the forming ladder passes through this one block, and that the
  mill therefore has no claim gesture at all;
* the relocation of `Outputs` / `OutputAt` off `RollSetSpec` onto the shear, and the fact that they key on
  stage, not on gap (so a half-step is a legal product point);
* the crop-not-convert rule: a crop takes one product's worth of metal and leaves the remainder on the deck as
  stock - settled 2026-08-12, and generalised into the staged-crops / whole-item-converts split that
  [process-extension](../mechanics/process-extension.md) now owns;
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
[multiblock & fillers](../mechanics/multiblock.md) (the footprint machinery a 3 × 1 × 2 megablock does need) ·
[density rule](../mechanics/density-rule.md) (why every crop divides exactly) ·
[recipes & config](../mechanics/recipes-config.md) (the code-first def, the cost catalogue, goldens) ·
[rolling mill](rolling-mill.md) (the stock it crops and the stage it arrives at) ·
[reheat furnace](reheat-furnace.md) (the alternative to a cold cut) ·
[rolling](../processes/rolling.md) · [STATE.md § placement rule](../../internal/plans/STATE.md)

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

⛔⛔ **3 × 1 × 2, not 1 × 1 × 1** *(corrected 2026-08-13 against the drawn shape, below)*. The art settles
this: the machine measures 47 × 26 × 16 voxels, so it is a megablock and the
[multiblock](../mechanics/multiblock.md) footprint machinery does apply after all. Three cells along the
blade, one deep, two high - the mill's 3 × 3 × 2 without the second feed deck. The one thing still worth
copying from the mill is that a shape may overhang its cell as long as the block is `SolidNonOpaque`
(`BlockRollingMill.cs:62-63`).

⛔ **The filler layout legend is the owner's and is not yet supplied** *(2026-08-13)*. What follows is the
cell *count*, measured off the shape - which cells are fillers, which are graph nodes and how the ASCII
legend reads are all still owed, for this machine and for the other nine mpenergy megablocks. Do not invent
one: a wrong legend fails silently and `*` inside `@()` is a regex
([multiblock](../mechanics/multiblock.md)).

| Aspect | Proposal | Why |
|---|---|---|
| Footprint | 3 × 1 × 2 cells | measured off the shape. The blade alone is 14 voxels wide, so a one-cell station was never going to hold it. The layout **within** those cells is pending |
| Orientation | `ns` / `we`, shaft along the orientation axis, exactly as the mill (`BlockRollingMill.cs:41-59`, `:106`) | so a shear lines up on the same line shaft as the mills it serves |
| Drive faces | connectors on the two faces along the shaft axis | a shear sits in the line, not on a spur |
| Throat | the face opposite the blade nest; the player feeds from there | Fig 5's open throat; also what makes the feed face readable without a label |
| Direction | does not implement `IMpEnergyDirection` | direction is last-writer-wins across a run (`MpEnergyNetwork.cs:75-77`); only the mill needs it, and a second writer would silently flip the mill's decks |

---

## Assets

⛔⛔ **The machine is drawn, and this page said otherwise until 2026-08-13.** It is filed under the shop
name - **cutter** - not the design's name, so a sweep for "shear" across `assets/editable/shapes/` found
nothing and this table recorded the absence as fact. Search art by what a machinist would call the machine,
not by what the design page is titled.

| Asset | State |
|---|---|
| editable shape | **drawn** - `assets/editable/shapes/machines/mpenergy/machine-mp-megablock-cutter.json`, beside the nine other mpenergy machine tools. Textures `cast-iron1` + `iron5`, the mill's pair |
| runtime shape | missing - needs the editable → runtime conversion (drop `editor`/`textureSizes`, repoint the two absolute texture paths at `iwex:block/metal/castiron` and `game:block/metal/sheet-plain/iron5`, flatten `Root`). `assets/iwex/shapes/forming/` holds only `rollingmill.json` and the ten stale `stock-*.json` |
| blade-set item shape | **drawn** - `assets/editable/shapes/items/smithed/item-forged-machineshears.json`; two blades 4 × 12 × 1 on the vanilla `block/metal/plate/iron` texture |
| reference art | `assets/editable/refs/rivetsnails/machine-tools-1-rivet-making-machine-2-riveting-machine-3-shearing-machine-for-bars-of-all-lengths-and-scrap-iron-4-punching-and-shearing-machine-5-double-shearing-machine-1867-technology-RY93PB.jpg` - Figs 3, 4 and 5 |
| lang | `assets/iwex/lang/en.json` carries no `shear-*` key |

### What the shape already decides

Measured off `machine-mp-megablock-cutter.json`, so these are facts rather than proposals.

| Part | Extent (authored voxels) | What it settles |
|---|---|---|
| whole machine | x −16…31, y 0…26, z 0…16 | **3 × 1 × 2 cells**, principal in the middle, one cell either side along x |
| `CutterBottom` / `CutterTop` | x −15…−1, 14 wide, at z 9–10 | the fixed and moving blades. **14 voxels of edge**, in the −x cell |
| `CutterMass` | x −16…0, y 12–15, `rotationZ` −15° at rest | a lever guillotine: a weighted arm swinging about z, not a screw or a ram |
| `Shaft` | x 6–10, y 7–9, **z 0–16** | the drive runs **along z**, across the blade, and spans the principal cell's full depth |
| `Shaft2` | x 17–21, y 7–9, z 0–14 | a second shaft in the +x cell, geared to the first |
| `PinionHub3` → `SpurHub1` | — | the reduction between them |

The `cycle` clip (60 frames, `onAnimationEnd: Repeat`) turns `Shaft` **720°** against `Shaft2`'s **360°**,
so the gearing is **2:1** and one revolution of the slow shaft is **one stroke**. `CutterMass` swings from
its −15° rest to closed at **frame 44** and eases back, so the cut lands about three-quarters of the way
through the clip rather than on frame 0 - the phase-lock the mill already needs applies here too
([mp-energy](../mechanics/mp-energy.md)). An `idle` clip holds `Shaft` at zero.

★★ **Two open items close on this.** The blades are 14 voxels of edge, which takes the widest plate the
forming line makes (`flatwide`'s `MaxWidth` 15), so **the wide shear is this block** - not a second machine
and not a hammer die. And the blade set is `item-forged-machineshears.json`, which
[machining-line](../mechanics/machining-line.md) already assigns: forged and tempered, with **vanilla's
temper ladder as the tier ladder**, so no new hardness system is owed. That page also notes the drawn
shears measure 96 vx³ = 240 u and want **100 vx³ = 250 u** to divide off the rod.
| handbook | `docs/iwex/handbook/` has no page; the sync pipeline joins on the `NN-` prefix and drift fails a test |

### The shear's products are already drawn — inside the mill's shape files

*Swept 2026-08-12, across `assets/editable/shapes/items/`.* The convention holds everywhere: an element drawn
off the shared origin is **not a stage**, it is another machine's output, and it belongs in that machine's
registry ([process-extension](../mechanics/process-extension.md)).

| Shape file | Stage elements (the mill's ladder) | Off-origin — **this** registry's |
|---|---|---|
| `smithed/item-shingled-bar.json` | `ShingledBar1`, `Grooved275/250/225`, `Flattened275/250/225` | `CutRod1..4`, `Beam` |
| `rolled/item-rolled-rod.json` | `RolledRod200`, `Grooved175/150/125`, `Flattened175/150/125` | `CutRivetRod1..4` |
| `rolled/item-rolled-beam.json` | `Beam`, `Flattened175/150/125` | `CutPlate1..2` |
| `smithed/item-shingled-slab.json` | `ShingledSlab1`, `ShingledSlab11` | — |

Two products have a file of their own rather than an in-situ cut: `rolled/item-rolled-rivetrod.json`
(`RivetRod1..4`) and `rolled/item-rolled-nailplate.json` (`NailPlate1`). Both conventions are supported — an
element of a family file, or a whole file as the stage.

`NailPlate` inside `item-rolled-rod.json` is **not** this page's. It is the one whole-piece conversion in the
design (a rod taken flat, no crop), so it is a stopping point on the mill's ladder — a stage with a `code` —
and not a job here. The `Cube2..11` elements in three of the files are modelling leftovers with no meaning.

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
| RMB with stock on the throat face | crop: one product leaves and the remainder stays a `WorkPiece` at the same stage, with one more crop tallied against it. A piece with nothing left to take cannot be cropped |
| RMB with a blade set | fit / swap the tooling, refused mid-stroke - mirror `TryFitRollSet` (`BlockEntityRollingMill.cs:143-155`) and its block-side handler (`BlockRollingMill.cs:296-320`) |
| Sneak + RMB | take the fitted blade set back. The two-piece-split alternative is retired with the ruling above |
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
| `ShearStrokeEnergy` | ≈ 1 stroke ≙ one mill pass's demand | - | drawn from the run as a pulse; sized against `RollingLoadTorque = 0.34`, the mill's declared working demand (`IwexConfig.cs`). ★ A shear stroke is the same shape of number: a state the machine is in, not a formula over the cut |
| `ShearColdTorqueMultiplier` | ×3 over the hot cut | - | the whole cold-cut gate is this number against `MinTorque` |
| `ShearMinTorqueHot` | 0.2 | - | matches the `flat` set's shipped `minTorque` so a starter waterwheel carries a hot thin crop |
| `RollSetSpec.MinTorque` | (existing field, moves here) | `RollSetSpec.cs:30` (doc), `:37` (field), `:198` (parsed) | parsed, stored and never consulted - a repo-wide grep finds no read. The shear is its first consumer |

Shipped `minTorque` values, for calibration only - these belong to the [roll sets](../items/roll-sets.md) and
are cited, not owned:

| Set | `minTorque` | file:line |
|---|---|---|
| `flat` | 0.2 | `RollSetItemDefinitions.cs:68` |
| `grooved` | 0.3 | `:92` |
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
| `ShearDecision` (pure) | **built 2026-08-13** - `.../Forming/ShearFeed.cs`, 13 tests | `ShearVerdict` has seven cases in the order a player can fix them: `NoBladeSet` → `NoJob` → `Spent` → `BladeTooSoft` → `NotTurning` → `NotEnoughDrive`. Needs no footprint, so it landed ahead of the layout |
| the torque gate | **built** - inside that decision | `RollingPass.CanCarry` **now has its first production caller**. ★★ The gate reads `ProcessJob.MinTorque` and `.MinTier`, not `RollSetSpec`'s: the job already declares both, so the blade set needs no spec format of its own and the `Outputs`/`OutputAt` move below is the only relocation still owed |
| `Outputs` / `OutputAt` | move off `RollSetSpec` (`:28`, `:35`, `:95-101`) onto the shear's own stage table | small, and it unblocks the whole product half of the forming line |
| stage → product table | a code table with a `PathFor`-style formatter and a test that every reachable stage names a real item | the `HearthRows` / `SandBedLayout` treatment |
| the crop itself | **built 2026-08-13** - `WorkPiece.Cropped`, `.Crop(count)`, `.CropsLeft(count)`, `.IsSpent(count)` (`WorkPiece.cs:119-142`) | one `int` on the stack, tallying crops **taken**. Neither mass nor length is involved |
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

- **The crop table is not shipped, deliberately.** The registry exists and the table is settled
  ([rolled parts](../items/rolled-parts.md)), but seven of its nine products are items that do not exist, and
  shipping a table of codes that resolve to nothing is exactly the mistake the four dangling roll-set outputs
  already made once. It waits on the rolled catalogue. The two entries that would resolve today
  (`game:metalplate`, `game:rod-iron`) are not worth shipping alone, because the interaction they would drive
  is the one still undecided below.
- Block, BE, def, recipe, shape, lang, handbook and tests are all absent. The forming build list says build
  the shear first of the three benches, because the `Outputs` move depends on it.
- How the torque gate reads drive torque - see Gotchas. Options: add `DriveTorque` to `MpEnergyNetworkState`;
  gate on `StoredEnergy ≥ k` instead; or have the shear attempt the stroke and let a stall be the answer
  (cheapest, and consistent with the mill's "a pass that overdraws simply freezes").
- ~~Blade sets are named and unspecified.~~ **Closed 2026-08-13.** The item is drawn
  (`item-forged-machineshears.json`) and [machining-line](../mechanics/machining-line.md)'s tooling table
  already rules it: a **forged and tempered consumable**, one of two (the other being the universal machine
  cutter), with **vanilla's temper ladder** standing in for a hardness system. Fitted and swappable in the
  `RollSetSpec` idiom, since the block needs a tool slot either way. ⛔ Its mass wants to be 100 vx³ = 250 u
  rather than the drawn 96 vx³, so it divides off the rod.
- ~~Whether a crop yields two stacks or one plus a remainder.~~ **Settled 2026-08-12: one product plus a
  remainder**, and the remainder is a **tally**, not a mass (corrected 2026-08-13). A stroke takes one
  product off and writes the rest back as stock at the same stage with one more crop counted against it.
  `count` in the built `ProcessJob` is the piece's total yield, so recoverability's "into 6" is that yield
  and crop-not-convert is how it leaves. ⛔ The mandatory crops therefore stop being a special operation - a
  player crops until the piece seats. See
  [process-extension § What a count means](../mechanics/process-extension.md).
- ~~The wide shear.~~ **Closed 2026-08-13 by the art.** The drawn blades are **14 voxels of edge**, which
  takes `flatwide`'s `MaxWidth` 15 - the widest plate the forming line makes. There is no second cutting
  station: this block covers the whole range, which is also what the verb split wanted (a die would have
  made the steam hammer shear).
- Stroke pacing is untuned. `k` for cooling, the stroke energy and the cold multiplier are all playtest knobs,
  and none of the mp-energy calibration has been playtested either.
- ~~`WorkPiece` cannot express a crop.~~ **Ruled and built 2026-08-13.** Length is not defined
  programmatically - it comes from the art - and cut points are **config**, because what a piece divides into
  is the modder's choice and not something we can calculate. `ProcessJob.count` is already that declaration,
  so a crop is **one `int` on the stack**. `WorkPiece.Mass` is **not** needed and is off the critical path.

  ★★ Built as crops **taken**, not crops remaining. Counting up keeps zero meaning *untouched*, so a piece
  that has never met a shear and one worked out to nothing cannot read alike, and no piece already in a world
  needs migrating. It also leaves the **job's declared count as the authority**: retuning a crop row from 4
  to 6 gives every existing piece the two extra crops instead of stranding it on the number it was cut
  against.

  ⛔⛔ **A part-cropped piece cannot be rolled.** `FeedVerdict.PartCropped` refuses it at the mill, before
  the gap is even judged. Without that refusal a player crops three products out of a bar, rolls what is
  left to the next stage, and it is worth that stage's whole count again - metal from nothing. The refusal
  is what lets the tally be a single `int` per stage instead of a proportion carried between stages, and it
  costs the player only the order they work in: finish the cut, then roll the pieces on.
