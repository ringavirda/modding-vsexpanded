# Nail machine
**Status** designed - art drawn but untracked and wired to nothing; no block, no BE, no recipe. ★ **Its input landed 2026-08-14**: `iiex:nailplate` ships and is obtainable, claimed off the rod's flat 1.0 rung, so this bench is no longer blocked on the forming line   **Mod** iiex (`IronIndustryExpanded`)

**Owns**
* the cut-nail bench: 1 `nailplate` → 4 `game:metalnailsandstrips`, and the fact that this route mints nothing against vanilla's own anvil rate;
* its mechanism: a big spoked flywheel and a crank working internal shears, no gearing at all, on a wooden A-frame trestle with an inclined feed table and collecting trays;
* the flip-between-shears rule that makes a cut nail tapered;
* its footprint, drive contract, feed/collect verbs and drops;
* the state of the drawn asset and what happened to it (exported 2026-08-21).

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

★★ **BUILT 2026-08-21 on a 4-cell footprint, not 1 × 1 × 1.** The authority is the owner's own layout in `workbench/machines.txt` - a `zy` elevation, `I m` over `O #`, so the bench is two cells deep and two tall with its working face above the principal. On the `"mpenergy"` network with cast-iron shafting: not vanilla MP and not wooden axles, since cast iron is the shared prerequisite for both the machine's frame and the shafting and there is no second tier to gate.

| Aspect | Proposal | Note |
|---|---|---|
| Footprint | 4 cells (`I m` / `O #`, a `zy` elevation), principal is the `BlockNetworkNode` | three fillers; the principal takes the drive |
| Orientation | `ns` / `we`, shaft along the orientation axis | the mill's pattern (`BlockRollingMill.cs:41-59`, `StructureAngle` at `:106`) |
| Drive | connectors on the two shaft-axis faces | a row of benches on one shaft must pass power through |
| Feed face | the inclined table, opposite the trays | the table's job is to point at the face the player interacts with |
| Gearing | none | the flywheel is on the drive shaft itself; wheel speed is shaft speed |

~~The drawn shape is authored as a megablock and the design says 1 × 1.~~ **Resolved 2026-08-21**: the owner's layout settled it at four cells, and the filename was right. The bench still declares `SolidNonOpaque` - the press overhangs its cell, and a solid cell that culled its neighbours' faces would leave holes.

⛔ **The drive connector sits on the principal, not on the `m` cell the drawing marks.** `BEBehaviorMPFillerPort` is a **vanilla-MP** intake - the flywheel's bridge - and not an mpenergy connector, so there is no shipped way to put an mpenergy connector on a filler cell. Cosmetic rather than functional, and the shear ships the same simplification; all three want re-homing together when the station family lands.

---

## Assets

| Asset | Path | State |
|---|---|---|
| **editable shape** | `workbench/shapes/machines/mpenergy/machine-mp-megablock-nailcutter.json` | drawn and tracked. Exported to `mods/iiex/assets/iiex/shapes/forming/nailcutter.json` (2026-08-21) through `convert-shape.py` |
| runtime shape | `mods/iiex/assets/iiex/shapes/forming/nailcutter.json` | **shipped** (2026-08-21) |
| textures | inside the shape: `iron5 → block/metal/sheet-plain/iron5`, and `cast-iron1 → F:/repos/modding-vsex/exmods/workbench/textures/cast-iron1` | the second is an absolute authoring path and must become an asset code before export |
| animations | `idle` and `cycle` | both authored and both exported as `Repeat` |
| reference art | `workbench/refs/rivetsnails/an-old-engraving-of-nail-making-machine-…-jacob-perkins-in-1795-….jpg` and `historic-wire-nail-tack-machine-….webp` | the folder is untracked |
| lang / handbook | `mods/iiex/assets/iiex/lang/{en,ru,uk}.json`, `mods/iiex/docs/handbook/10-formingshop.html` | **both shipped**; the page covers the whole forming shop rather than this bench alone |

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
| `BlockNailMachine` | `mods/iiex/src/BlockStructures/Forming/Blocks/` | `BlockRollingMill.cs:31` (`BlockNetworkNode` + `IExBlockDefProvider`), minus the filler interfaces |
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

- ~~**`nailplate` does not exist.**~~ **Stale.** It ships as of B3c (`RolledItemDefinitions`, 2026-08-14), and the `stockForm` half went with B3 on 2026-08-11: `WorkPiece.FromStack` falls back to the collectible attribute when the stack tree carries none, so a fresh piece off the grid is a work piece. And the rod fork landed the same day: a player feeds `game:rod-iron` at the deck, it enters as `iiex:stock-rod`, and the flat 1.0 rung claims `iiex:nailplate` with no crop. **The nail line's input now exists and is obtainable**; what is left is this bench itself.
- ~~**The `flat` roll set cannot make the plate today.**~~ **Stale.** The set no longer names outputs at all - a stopping point is a stage on the stock's ladder ([process-extension](../mechanics/process-extension.md)) - and its accepted forms are `["shingledbar", "castbillet"]`. Plate is the flat 1.0 crop of either, into vanilla `game:metalplate-iron`.
- **Nails come from PLATE, never from rod.** Rod-nails are the 1870s wire nail - a different machine and far out of period. The rod fork's other branch goes to the rivet bench instead ([heading machine](heading-machine.md), whose premise the 2026-08-15 ruling contests).
- **No gearing means no ratio.** The mpenergy network has exactly one ratio device, the transmission, and it is a separate block whose ratios are a `switch` on the variant (`BlockEntityTransmission.cs:59-64`). Do not smuggle a ratio into this bench because the drawing has a wheel on a shaft - the drawing has no gear train, which is the distinction from the heading machine's spur gear.
- **A running clip must repeat.** With no clip active the animator drops the suppressed mesh back to the static shape; the chimney cap documents the trap in-source (`BlockEntityPuddlingChimneyCap.cs:31-33`).
- **`SpinSpeed` assumes one revolution per clip** (`EnergyAnim.cs:16-22`). A wheel clip authored as two revolutions animates at half the true speed with no error anywhere.
- **A run with no storage node has no state.** `MpEnergyNetwork.OnTick` nulls it at `Σ I ≤ 0` (`:81-89`) - a bench on a bare bridge never runs.
- **Do not let the machine mint metal.** 4 per plate is the ceiling; any "efficiency" bonus breaks the anchor and invalidates the no-minting argument the forming line is balanced on.
- **The shape's texture map contains an absolute Windows path.** It will resolve to nothing in game and fail silently as a missing texture.
- **Nails come from plate here; the 25 u `rivetrod` is the other bench's input** - do not conflate the two.

---

## Open

- ~~Nothing is built.~~ **Built 2026-08-21** - block, BE, def, recipe, runtime shape, animations, lang, handbook and tests all ship. ⛔ **Nothing has been walked in game.**
- ~~`nailplate` the item does not exist.~~ **Shipped**, and the shear crops a plate into two of them.
- ~~Does the bench take a die at all?~~ **Yes, ruled by the owner 2026-08-21** - and for a reason neither page had: the steam hammer's stamping wants dies too. It holds `iiex:die-nail`, and a rivet die fitted here is refused rather than quietly working.
- ~~Atomic or per-stroke consumption.~~ **One stroke, one plate.** The plate converts whole - four bundles at once - which is what makes this a terminal station rather than another rung of the ladder.
- ~~Footprint vs the drawn asset.~~ **Four cells**, from the owner's layout. The filename was right and the design page was wrong; see § Structure.
- ~~Whether the bench needs a torque gate at all.~~ **It has one**, at `minTorque` 0.2 - the lightest on the line, matching the `flat` roll set, so a starter waterwheel carries it. The gate is what makes the flywheel load-bearing rather than decorative.
- Die wear. `ItemDie` gives every die `MaxStackSize(1)` so it can carry wear, and nothing wears it yet.
- **Tray capacity and whether it is a real inventory.** A visible box of nails is worth a lot of the machine's readability, and R7 (nothing is hidden) asks for the count to be legible.
