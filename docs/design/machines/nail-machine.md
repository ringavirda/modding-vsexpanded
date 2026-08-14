# Nail machine
**Status** designed - art drawn but untracked and wired to nothing; no block, no BE, no recipe   **Mod** iiex (`IronIndustryExpanded`)

**Owns**
* the cut-nail bench: 1 `nailplate` → 4 `game:metalnailsandstrips`, and the fact that this route mints nothing against vanilla's own anvil rate;
* its mechanism: a big spoked flywheel and a crank working internal shears, no gearing at all, on a wooden A-frame trestle with an inclined feed table and collecting trays;
* the flip-between-shears rule that makes a cut nail tapered;
* its footprint, drive contract, feed/collect verbs and drops;
* the state of `machine-megablock-nailcutter.json` - the one drawn asset - and what has to happen to it.

**Depends on**
[mp-energy](../mechanics/mp-energy.md) (the run it loads; a crank stroke is a pulsed load, which is why it carries its own flywheel) ·
[heading machine](heading-machine.md) (owns the `ItemDie` tooling contract and the BE base this bench shares) ·
[shear](shear.md) (crops the `rolledrod` and the `nailplate` this bench eats - the mill never hands a product straight over) ·
[rolling mill](rolling-mill.md) (rolls a rod into nail plate on `flat`) ·
[multiblock & fillers](../mechanics/multiblock.md) (why 1 × 1 needs none of it, and how a shape may overhang its cell) ·
[density rule](../mechanics/density-rule.md) (`nailplate` 4 × 1 × 10 = 100 u; nails 25 u each) ·
[recipes & config](../mechanics/recipes-config.md) · [STATE.md § Fasteners](../../internal/plans/STATE.md)

---

## Role

Nails are the most-demanded input in the mod - every bolted joint, every pipe run, every machine bill. `ExIngredients.Nails` (`ExIngredients.cs:36`) is reached for by name in machine after machine; all four iiex pipe segments cost one (`PipeRecipeDefinitions.cs:25`, `:34`, `:43`, `:52`), and so does the tall hopper (`FurnaceRecipeDefinitions.cs:60`). What must be industrialised is making them at scale.

The win is not yield: the whole route contains no anvil work at all. Puddle → roll → crop → cut, and one 400 u bar becomes 16 `metalnailsandstrips`. The machine's contribution is to collapse "crop the plate, shear each nail, taper each nail" into one operation on a whole 100 u plate.

**No route may beat 4 nails per 100 u.** Vanilla's 36-voxel anvil pattern gives 1 ingot → 4 `metalnailsandstrips`, so at 25 u apiece that is exact and it is a hard anchor. The ladder already sits on the ceiling - one nail plate is four nails-and-strips exactly - so there is no yield left for a machine to win. Vanilla's other pattern (54 voxels) outputs 8, i.e. 200 u out of a 100 u ingot; do not copy it.

It is a bench, and the player builds several. The reference photograph is a hall of identical machines in a row, all driven off one overhead line shaft, each with its flywheel and its box of nails. Industrial scale here is many identical machines on one shaft, as with the six-mill hall and the bank of cupolas - so this block must be cheap by design and sized to line up.

---

## Structure

1 × 1 × 1 on the `"mpenergy"` network, with cast-iron shafting. Not vanilla MP and not wooden axles: cast iron is the shared prerequisite for both the machine's frame and the shafting, so there is no second tier to gate.

| Aspect | Proposal | Note |
|---|---|---|
| Footprint | 1 × 1 × 1, its own `BlockNetworkNode` | no filler, no multiblock, no projection |
| Orientation | `ns` / `we`, shaft along the orientation axis | the mill's pattern (`BlockRollingMill.cs:41-59`, `StructureAngle` at `:106`) |
| Drive | connectors on the two shaft-axis faces | a row of benches on one shaft must pass power through |
| Feed face | the inclined table, opposite the trays | the table's job is to point at the face the player interacts with |
| Gearing | none | the flywheel is on the drive shaft itself; wheel speed is shaft speed |

The drawn shape is authored as a megablock and the design says 1 × 1. In `machine-megablock-nailcutter.json` the bench body fits one cell (x 0–16, z 0–16) but the shaft group runs x −2 → 2 and z 8 → 24, i.e. it pokes two voxels west and half a cell into the +Z neighbour. That is correct for a line shaft and needs no fillers - the mill already declares `SolidNonOpaque` for exactly this ("the stand overhangs its cell; the placed cell is solid but must not cull neighbour faces", `BlockRollingMill.cs:62-63`). Either keep 1 × 1 and accept the overhang, or rename the asset. Do not add a footprint to justify the filename.

---

## Assets

| Asset | Path | State |
|---|---|---|
| **editable shape** | `assets/editable/shapes/machine-megablock-nailcutter.json` | drawn, and untracked (`git status` reports it as `??`). Referenced by no code, no test, no def, no generator input |
| runtime shape | `assets/iiex/shapes/…` | missing - never exported |
| textures | inside the shape: `iron5 → block/metal/sheet-plain/iron5`, and `cast-iron1 → F:/repos/modding-vsexpanded/assets/editable/textures/cast-iron1` | the second is an absolute authoring path and must become an asset code before export |
| animations | none in the file (no `animations` key) | the crank stroke and the wheel spin both still have to be authored |
| reference art | `assets/editable/refs/rivetsnails/an-old-engraving-of-nail-making-machine-…-jacob-perkins-in-1795-….jpg` and `historic-wire-nail-tack-machine-….webp` | the folder is untracked |
| lang / handbook | `assets/iiex/lang/en.json`, `docs/iiex/handbook/` | no key, no page |

What is already drawn, read off the element tree:

| Group | Elements | Reads as |
|---|---|---|
| `Base` | four corner blocks with uprights (`Cube3` / `Cube6` / `Cube9` / `Cube12`, each with 2 posts + 2 feet), bench top `Cube5` with under-slung boxes `Cube15` / `Cube16` / `Cube17` | the timber trestle and its collecting trays |
| `Machine` | head `Cube26` on two standards, plus two mirrored stacks of nine stepped cubes (`Cube30`–`Cube38`, `Cube39`–`Cube47`) and a back plate `Cube48` | the internal shear stacks - the stepped profile is the taper |
| `ShaftGroup` | `Shaft` with `Hub1`/`Hub2`, six `Spoke01`–`Spoke06`, eight `Rim01`–`Rim08` | the spoked flywheel on the drive shaft |

Drawing notes that are design, not taste: the trestle is wood, because wood reads as bench at a glance next to a mod of all-cast-iron megablocks, and it is period-exact. The tray under the working point tells the player what the machine makes without a tooltip. The flywheel is the biggest thing in the silhouette; do not shrink it.

---

## Construction

No recipe. Same blocker class as the mill and the [shear](shear.md).

Proposed cost - the cheapest of the three benches, because the player is meant to build six:

| Slot | Ingredient | Rationale |
|---|---|---|
| trestle | `game:plank-*` ×4 | the A-frame is timber; planks-and-candles cheapness is already precedent (`CraftingStationRecipeDefinitions.cs:16-20`) |
| head | `castplate` ×1 | the one cast part |
| shears | `game:metalplate-iron` ×2 | proposed |
| flywheel | *(built in, not a separate `iiex:mpenergy-flywheel`)* | the block-scale flywheel is a 3 × 3 megablock and far too big; this wheel is art |
| fasteners | `Nails(1)` (`ExIngredients.cs:36`) | the machine that makes nails costs nails - one bootstrap batch off the anvil, then it feeds itself |

Cost-catalogue key `nailmachine-grid` in `IiexRecipeConfig.DefaultCatalogue` (`IiexRecipeConfig.cs:47-70`).

---

## Operation

```
nailplate (4 × 1 × 10, 100 u)  ──RMB on the feed table──▶  4 × game:metalnailsandstrips
```

| Verb | Effect |
|---|---|
| RMB with `nailplate` on the feed table | lay the plate on; the machine consumes it over its stroke cycle while the shaft turns |
| RMB empty on the trays | collect finished nails |
| RMB with a die | fit the nail die (see [heading machine](heading-machine.md) for the contract) - or the machine hard-codes the nail die, see Open |
| Sneak + RMB | take the plate back off the table, uncut |

Rate is the shaft, not a timer. The bench is a pulsed load: each crank revolution is one shear stroke, so the natural rate is `ω / 2π` strokes per second with the wheel's own inertia carrying each bite. That is what lets a row of benches run off one shaft without dragging it down.

The taper is free. A cut nail is tapered because the plate is flipped between shears, alternating the cut - the same left-right alternation the mill itself uses. It costs no art and no state beyond a flip flag.

Cold or hot? Nail plate is sheared, not rolled, so nothing here gates on temperature - see the [shear](shear.md)'s force-not-friction argument. Whether the bench should nonetheless want some torque headroom (the `MinTorque` idiom) is open.

---

## Numbers

All proposed; the bench has no config section and no keys. Values that exist today are cited from the pages that own them.

| Key | Proposed | file:line | What it does |
|---|---|---|---|
| `NailsPerPlate` | 4 | — | the conversion. Fixed by the mass ledger, not chosen: 4 × 25 = 100 u |
| `NailStrokeMs` | 250 ms | — (mirror `PassTickMs`, hard-coded at `BlockEntityRollingMill.cs:42`) | stroke tick |
| `NailStrokesPerPlate` | 4 | — | one stroke per nail, so a plate visibly takes four |
| `NailMinTorque` | 0.15 | — | below the `flat` roll set's 0.2 (`RollSetItemDefinitions.cs:68`) - a nail bench must be the easiest load on the run |
| `NailStrokeEnergy` | ≪ one mill pass | — | sized against `RollingLoadTorque = 0.34` — the mill's declared working demand, 85 % of one bridged wheel's headroom (`IiexConfig.cs`) |

Masses are not owned here - `nailplate` 4 × 1 × 10 = 40 vx³ = 100 u and `game:metalnailsandstrips` at 25 u both come from the [density rule](../mechanics/density-rule.md) and the [fasteners](../items/fasteners.md) page; the ladder that produces them is the forming line's ([stock](../items/stock.md), [rolling](../processes/rolling.md)).

Hard-coded elsewhere, and relevant:

| Constant | Value | file:line |
|---|---|---|
| network tick (the run's `dt`) | 1000 ms | `BlockNetworkModSystem.cs:42-45` |
| `EnergyAnim.StoppedFraction` | 0.01 | `EnergyAnim.cs:14` - below 1 % of ω_max the wheel reads as stopped |
| `MpMaxSpeed` | 2.0 rad/s | `ExlibConfig.cs:98` |
| `ShaftInertia` | 0.5 | `IiexConfig.cs:477` |

---

## Drops

| Broken | Returns |
|---|---|
| the bench | itself, one item |
| a plate on the table | handed back uncut - no partial credit, the same rule the mill applies to an interrupted pass (`BlockEntityRollingMill.cs:302-311`) |
| nails in the tray | all of them - spawn the contents before `base.OnBlockBroken`, as `BlockEntityRollingMill.cs:395-404` does for its piece and roll set |
| the fitted die | spawned, not destroyed |

No fillers, so nothing routes through [multiblock](../mechanics/multiblock.md)'s drop rerouting.

---

## Code

Nothing exists. `grep -i "nailcutter\|nailmachine" src/` returns nothing.

| Piece | Where | Model it on |
|---|---|---|
| `BlockNailMachine` | `src/IronIndustryExpanded/BlockStructures/Forming/Blocks/` | `BlockRollingMill.cs:31` (`BlockNetworkNode` + `IExBlockDefProvider`), minus the filler interfaces |
| `BlockEntityNailMachine` | `.../Forming/BlockEntities/` | shares a base with the [heading machine](heading-machine.md): `BlockEntityNetworkNode` + `IMpEnergyConsumer`, `LoadTorque` at idle = 0 (`BlockEntityRollingMill.cs:336-350`) |
| stroke tick | a hosted `BEBehaviorProductionMachine`, server-side only | `BlockEntityRollingMill.cs:28`, `:48-53`; the gate is published as `IProductionReadiness` (`:59`, `:63`) rather than tested at the top of the stroke. The bounded `dt` (`BEBehaviorProductionMachine.cs:77`, `:146`) and the away-catch-up (`:107`, `:113`) come with it |
| speed read | per stroke | `(NetworkSystem?.GetNetworkAt(Pos) as MpEnergyNetwork)?.State?.Speed` - `BlockEntityRollingMill.cs:89-93` |
| the die spec | `ItemDie`, owned by [heading machine](heading-machine.md) | `RollSetSpec.cs:31` / `MoldSpec.cs:32` - `TryParse` returning a human-readable error (`RollSetSpec.cs:108`, `MoldSpec.cs:49`) |
| animation | `EnergyAnim.SpinSpeed` for the wheel | `EnergyAnim.cs:10-24`; clips authored as one revolution |
| def + recipe | a `IExBlockDefProvider` static `Definitions(domain)` and an `ExRecipeDef` grid | `BlockRollingMill.cs:44-64`, `CraftingStationRecipeDefinitions.cs:23-36` |
| item defs | `nailplate` | `StockItemDefinitions.cs:32-59` is the nearest template (`MaterialDensity`, `materialUnits`, `combustibleProps`) |

Where a caller hooks in. To add a bench of this family: `BlockNetworkNode` with `NetworkType => "mpenergy"`, a BE implementing `IMpEnergyConsumer`, and no implementation of `IMpEnergyDirection` - direction is last-writer-wins across the whole run (`MpEnergyNetwork.cs:75-77`).

---

## Gotchas

- **`nailplate` does not exist.** Neither does any item that carries a `stockForm` on its stack - `StockItemDefinitions.cs:45` writes `stockForm` as a collectible attribute, while `WorkPiece.FromStack` reads the per-stack tree (`WorkPiece.cs:162-180`), so a fresh piece deserialises to `null` and `MillFeed.Decide` returns `WrongForm` (`MillFeed.cs:107-108`). That is blocker B3 seen from the item side, and the nail line sits downstream of it.
- **The `flat` roll set cannot make the plate today.** Its accepted forms are `["bloom", "billet"]` (`RollSetItemDefinitions.cs:64`) and its outputs name `iiex:rolledplate-iron` / `iiex:rolledsheet-iron` (`:66`), items that do not exist and that the design rejects in favour of rolling into vanilla codes.
- **Nails come from PLATE, never from rod.** Rod-nails are the 1870s wire nail - a different machine and far out of period. The rod fork goes to the [heading machine](heading-machine.md) instead.
- **No gearing means no ratio.** The mpenergy network has exactly one ratio device, the transmission, and it is a separate block whose ratios are a `switch` on the variant (`BlockEntityTransmission.cs:59-64`). Do not smuggle a ratio into this bench because the drawing has a wheel on a shaft - the drawing has no gear train, which is the distinction from the heading machine's spur gear.
- **A running clip must repeat.** With no clip active the animator drops the suppressed mesh back to the static shape; the chimney cap documents the trap in-source (`BlockEntityPuddlingChimneyCap.cs:31-33`).
- **`SpinSpeed` assumes one revolution per clip** (`EnergyAnim.cs:16-22`). A wheel clip authored as two revolutions animates at half the true speed with no error anywhere.
- **A run with no storage node has no state.** `MpEnergyNetwork.OnTick` nulls it at `Σ I ≤ 0` (`:81-89`) - a bench on a bare bridge never runs.
- **Do not let the machine mint metal.** 4 per plate is the ceiling; any "efficiency" bonus breaks the anchor and invalidates the no-minting argument the forming line is balanced on.
- **The shape's texture map contains an absolute Windows path.** It will resolve to nothing in game and fail silently as a missing texture.
- **Nails come from plate here; the rod fork is the [heading machine](heading-machine.md)'s input** - do not conflate the two benches' inputs.

---

## Open

- **Nothing is built** - block, BE, def, recipe, runtime shape, animations, lang, handbook, tests.
- **`nailplate` the item does not exist** (build item 12d), nor does the `flat` 1.5 → 1.0 schedule that rolls it from a rod (build item 4).
- **Does the bench take a die at all?** The nail die is the only die it can ever hold, so a fitted-tooling slot may be pure ceremony. Against that: sharing one `ItemDie` code path with the heading machine is the reason the two benches share a base. Undecided.
- **Atomic or per-stroke consumption.** Four strokes per plate reads better and costs a `strokesDone` field; one-shot is simpler and hides the machine's only animation.
- **Footprint vs the drawn asset** - 1 × 1 with an overhanging shaft, or a genuine megablock. The filename says one thing and the design says the other.
- **Whether the bench needs a torque gate at all.** Nails are the cheapest fastener in the game and gating them on drive would be the wrong friction; but a bench that runs at any speed makes the flywheel decorative.
- **Tray capacity and whether it is a real inventory.** A visible box of nails is worth a lot of the machine's readability, and R7 (nothing is hidden) asks for the count to be legible.
