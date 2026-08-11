# Bessemer converter

**Status** live; the only machine in the suite still authored as a coordinate layout, scheduled for a rebuild
as a single RCC megablock. The vessel cannot be finished in survival (B7) - see Construction.
**Mod** smex (`SteelmakingExpanded`)

**Owns** - the facts this page is canonical for:

* the converter's current-state anatomy: four separate placeable blocks (control, transmission, gas intake,
  vessel) and the exact 33-cell coordinate layout with its eight block numbers;
* the peripheral offset table and the `+180` local-frame convention the control resolves them through - the
  control sits at the origin and the transmission at `(0,−1,0)`;
* the four-state tilt machine (`ConverterOpState`) and the modifier-key verbs that select each state,
  including the held commit on the deep steel pour;
* the dynamic carbon model - start / target / over-blow bands, the decarburisation rate, and every derived
  duration (blow length, the over-blow window, pour length);
* the autothermal terms the converter feeds into the shared law (`T_in`, `T_loss`, the refine floor) and the
  emergent cold-scrap ceiling arithmetic - the law itself belongs to
  [heat balance](../mechanics/heat-balance.md);
* the slag pool and the split-pour sequencing through one shared output cell;
* the mass balance as shipped: 90 u steel + 6 u slag + 4 u gas per 100 u pig, and the scrap yield;
* every `Bessemer*` config key, plus smex's own `BlastPressureThreshold` and `RccBrokenDropsRatio`;
* the vessel's 7-stage RCC recipe, its 25-cell filler footprint, the chisel hatch offset, and the
  drops / chisel / break-loss rules;
* the contradictions between the shipped machine and the settled design - capacity, blown iron and mandatory
  recarburisation.

**Does not own** - cited only, never restated:
[heat balance](../mechanics/heat-balance.md) - `T_process = T_in − T_loss`, `HeatBalance.Compute`, the HUD
ledger, and the furnace FSM the converter does not use ·
[molten network](../mechanics/molten-network.md) - canal cells, `IMoltenCell`, push/drain/soak semantics, the
metal-type refusal, the missing `Ladle` ·
[molten canal](molten-canal.md) - the tap, the canal start, valve/seal behaviour, canal throughput ·
[pipe network](../mechanics/pipe-network.md) - the air pool, pressure, `TryConsumeGas`, connectors ·
[multiblock & fillers](../mechanics/multiblock.md) - `MultiblockLayout`, `StructureComplete`, filler footprints,
`IFillerInteractionTarget` ·
[recipes & config](../mechanics/recipes-config.md) - code-first defs, `ExRecipeCosts`, `/exmod` ·
[cast pipes](cast-pipes.md) - the `lpex:pipe-straight-*` blocktype the build asks for ·
[gears](gears.md) - `lpex:largegear-*` and `lpex:gear-*` ·
[twin-tub blower](twin-tub-blower.md) and the engine air blower - where the blast comes from ·
[long cell](long-cell.md) - what the steel is cast into ·
[ladle](ladle.md) · [open hearth](open-hearth.md) · [cupola](cupola.md) ·
[blast furnace (cold)](blast-furnace-cold.md) - the pig source ·
[materials.md](../materials.md) - what Bessemer steel is · [STATE.md](../../plans/STATE.md) - D4, N1, the blocker list

**Depends on** [heat balance](../mechanics/heat-balance.md) · [molten network](../mechanics/molten-network.md) ·
[pipe network](../mechanics/pipe-network.md) · [multiblock & fillers](../mechanics/multiblock.md) ·
[molten canal](molten-canal.md) · [recipes & config](../mechanics/recipes-config.md)

---

## Role

The steel tier's volume machine: it takes molten pig off a canal, blows air through it until the carbon is
gone, and pours the result back into a canal - minutes per heat, thousands of units at a time. Its input is
liquid metal rather than ore or fuel, and it runs on no fuel at all; the pig's own oxidisable content, burned
by the blast, is the entire heat source.

It is the acid Bessemer - acidic lining, self-forming siliceous slag, no flux. Lime belongs to the
basic/Thomas process for phosphorus removal, which is absent rather than omitted
(`BlockEntityConverterControl.cs:34-41`).

### What it sells, against the [open hearth](open-hearth.md)

Bessemer steel is cheap structural volume, one grade, no spec. Its nitrogen grade bars it from pressure work
([materials.md](../materials.md)), and that wall is the only reason the two steelmakers coexist. The balance
rule for the pair is settled and lives on the [open
hearth](open-hearth.md#the-settled-balance-rule--rhythm-not-rate) page: tune rhythm, not rate. The Bessemer is
the attended machine.

### Settled 2026-07-29: the product is **blown iron**, and recarburisation is mandatory

The blow burns out all the carbon and the manganese, leaving iron that is oxygen-saturated and unusable
([STATE.md](../../plans/STATE.md) N1). Recarburisation - Mushet's 1856 spiegeleisen addition - is mandatory,
not a refinement; without it the process does not work. The settled chain:

```
molten pig ──▶ BESSEMER (blow) ──▶ blown iron ──▶ LADLE (recarburise + deoxidise) ──▶ steel
```

Powdered coke cannot substitute: it adds carbon only, and it is manganese that scavenges the oxygen
([STATE.md](../../plans/STATE.md) § "How ferroalloys are added"). The additive rules and the chill model are
the [ladle](ladle.md)'s.

Two recarburisers, two grades (settled 2026-08-07). A 10–15 % spiegeleisen dose on the converter's heat lands
rail-grade steel at 0.45–0.65 % C; a small high-carbon ferromanganese trim at the [ladle](ladle.md) lands mild
at ~0.2 % C. They are distinct items ~10× apart in Mn strength; the split and the arithmetic are
[recarburising](../processes/recarburising.md)'s.

The shipped machine does not do this - see [Open #1](#open).

---

## Structure

### Four blocks, and why

The converter predates the filler system: its vessel was retrofitted onto fillers, nothing else was, and that
is the only reason the control, intake and transmission are separate placeable blocks. The planned rebuild
deletes all three behind a migration.

| Block | Code | Class | Job |
|---|---|---|---|
| control | `smex:convertercontrol-{side}` | `BlockConverterControl.cs:18` | layout anchor, operator lever, and the brain - owns all state |
| transmission | `smex:convertertransmission-{side}` | `BlockConverterTransmission.cs:16` | MP endpoint; the axle that tilts the vessel |
| gas intake | `smex:converter-intake-{side}` | `BlockConverterIntake.cs:19` | fixed pipe connector (not a node) for the blast |
| vessel | `smex:converterbessemer-{side}` | `BlockConverterBessemer.cs:28` | the 3×3×3 shell; RCC-built, control-spawned, never placed from an item |

The control is at (0,0,0) and the transmission at (0,−1,0) (`BlockConverterControl.cs:51-52`).

### The layout — 33 cells, coordinate form

Anchor: the control block, at its own `(0,0,0)`. Authored as explicit `.At(…)`/`.Fill(…)` calls rather than an
ASCII layout - the last structure in the suite in this form (`BlockConverterControl.cs:40-71`). Golden:
`test/SteelmakingExpanded.Tests/goldens/smex/blocktypes/converter/control.json` (33 offsets, verified).

| # | Required block | Count | Structure-local cells |
|---|---|---|---|
| 1 | `smex:convertercontrol*` | 1 | `(0,0,0)` |
| 2 | `smex:convertertransmission*` | 1 | `(0,−1,0)` |
| 3 | `smex:converterbessemer*` | 1 | `(0,0,2)` |
| 4 | `smex:converter-intake*` | 1 | `(0,0,4)` |
| 5 | `iwex:moltencanal-tap*` | 1 | `(1,1,2)` - the input tap |
| 6 | `iwex:moltencanal-start*` | 1 | `(1,−2,2)` - the output start |
| 7 | `iwex:moltencanal-straight*` | 2 | `(2,1,2)`, `(2,−2,2)` |
| 8 | `exlib:structurefiller` | 25 | z = 1 and z = 3 full 3×3; z = 2 minus the vessel and minus the tap |

The 25 filler cells are the same 25 the vessel places for itself; the rebuild should make that explicit rather
than rely on it. There is a one-cell gap in the vessel's own 3×3×3 footprint at its local `(−1,1,0)`, and
after the 180° frame flip that gap is the input-tap cell `(1,1,2)` (`BlockConverterBessemer.cs:64-94` vs
`BlockConverterControl.cs:65-71`).

### Peripheral offsets and the +180 frame

The control's local frame faces opposite its `side` variant. `UpdateStructureRotation` initialises the angle
with `initAngleOffset: 180` (`BlockEntityConverterControl.cs:1246-1257`) and `GetGlobalPos` resolves every
peripheral through `(_currentAngle + 180) % 360` (`:748-759`); the two must stay in step or every peripheral
moves.

| Peripheral | Local | Declared at | Read by |
|---|---|---|---|
| transmission | `(0,−1,0)` | `BlockEntityConverterControl.cs:47` | `HasPower()` `:799`, `IsTransmissionAligned()` `:844` |
| vessel | `(0,0,2)` | `:48` | `GetConverter()` `:812`, `TrySpawnConverter` `:977` |
| gas intake | `(0,0,4)` | `:49` | `TryConsumeBlast()` `:768`, `IsGasIntakeAligned()` `:829` |
| input tap | `(1,1,2)` | `:50` | `TickFilling` `:393` |
| output start | `(1,−2,2)` | `:51` | `TickSlagPouring` `:497`, `TickSteelPouring` `:554` |

Alignment is checked separately from the layout. The layout's block numbers are wildcards, so a backwards
intake or transmission completes the structure and then does not work; both are re-checked by comparing the
`side` variant string against the control's (`:829-853`). No such check exists for the tap or the canal start.

### The vessel's own footprint

A 3×3×3 cube of invisible fillers around the principal, minus the origin and minus local `(−1,1,0)` - 25
cells, drawn as three ASCII floor plans in the def (`BlockConverterBessemer.cs:64-94`). Selection and
collision are one box spanning `(−0.5,−0.5,−0.5) → (1.5, 2.0, 1.5)` (`:135-136`).

Interactive cells: the control (all operating verbs), the vessel principal and every filler cell
(construction, forwarded via `IFillerInteractionTarget`, `:217-246`), and one singled-out chisel hatch at
vessel-local `(0,1,0)`, directly above the vessel (`:63`, matched by `IsChiselCell` `:274-283`).

---

## Assets

| Asset | Path | State |
|---|---|---|
| vessel shape | `assets/smex/shapes/converter/bessemer.json` | live. Root children `GearShaft · BottomIron · GasIntake · BottomRefractory · UpRefractory · UpIron · InputLining` - one per RCC stage, in order. Textures `front1 · burned · iron3 · iron5` |
| vessel animations | same file | `idle` (`Repeat`), `filling` / `slagpouring` / `pouring` (all `Hold`, one keyframe each) - held tilt poses, not cycles, so `Hold` is correct here |
| control shape | `assets/smex/shapes/converter/control.json` | live; animations `filling` / `pouring` (both `Hold`) - the lever throw. Textures `burned · iron4` |
| intake shape | `assets/smex/shapes/converter/intake.json` | live, no animations |
| transmission shape | `assets/smex/shapes/converter/transmission.json` | live, no animations; carries an `Axle` element |
| editable sources | `assets/editable/shapes/` | only the intake has one (`machine-pipe-block-converterintake.json`, currently untracked; the old `machine-converter-intake.json` is deleted in the tree). The vessel, control and transmission have no editable source at all - the runtime shapes are the only copy |
| textures | — | none of its own; every texture key resolves to vanilla or shared iwex sheets |
| particles | — | `ExParticles.RisingPlume` of `ExParticles.Smoke` from a box at vessel-local x ∈ [−0.375, 0], y ∈ [1.5, 2.0], z ∈ [0.3125, 0.6875], rotated per `side` (`BlockEntityConverterBessemer.cs:108-159`). Server-spawned, so it replicates |
| sounds | — | `Embers` (4 s throttle), `Fire` (3 s), `Sizzle` / `MoltenMetal` (1.5 s), `MetalGrinding`, `CokeOvenDoorOpen`, `Extinguish` - all repurposed, none new (`BlockEntityConverterControl.cs:300-315`, `:466-474`, `:956-957`) |
| lang | `assets/smex/lang/en.json:30-130` | complete - 4 block-help keys, 12 errors, 15 info lines, 22 status lines |
| handbook | `docs/smex/handbook/04-bessemer.html` ↔ `smex:handbook-bessemer-text` (`lang/en.json:175`) | stale - see Gotchas #10 |

Rendering: the vessel is drawn by its animator, because `ExRightClickConstructable` suppresses the default
mesh; `idle` is a permanent held pose that keeps the built elements visible
(`BlockEntityConverterBessemer.cs:17-23`, `:169-199`). The block def's `selectiveElements` is
`Root/GearShaft/*` (`BlockConverterBessemer.cs:137`), which is the inventory/held silhouette only.

The control is not RCC, so it loads its shape by hand and hands the rotation to the renderer rather than
baking it into the mesh; `InitializeShapeAndAnimator` would do both and rotate the control 180° off
(`BlockEntityConverterControl.cs:161-183`).

---

## Construction

### The three grid recipes

All in `Recipes/Grid/ConverterRecipeDefinitions.cs`; costs registered as `convertercontrol-grid`,
`convertertransmission-grid`, `converter-intake-grid` (`SmexRecipeConfig.cs:58-60`).

| Output | Pattern | Ingredients | file:line |
|---|---|---|---|
| `smex:convertercontrol-north` | `H_R,NPP,PPR` | 12 rod, 4 plate, 8 nails, hammer (tool) | `:21-30` |
| `smex:convertertransmission-north` | `HPR,AGP,NPR` | 12 rod, 4 plate, 8 nails, 16 gear, 1 `game:woodenaxle-ud`, hammer | `:46-57`; emitted twice - once for `game:gear-rusty` (`:31`), once for `lpex:gear-*` (`:43`) |
| `smex:converter-intake-north` | `HP_,LPP,RN_` | 16 rod, 4 plate, 8 nails, 1 `lpex:pipe-straight*`, hammer | `:32-42` |

`Rod` / `Plate` / `Nails` are the `game:*-*` metal-capture staples (`ExIngredients.cs:27-46`); `PipeStar` is
the trailing-star pipe wildcard shared with the cowper and smokestack intakes (`RecipeIngredients.cs:18-19`).

### The vessel — 7 RCC stages

Raised in place by right-clicking the vessel with materials in the hotbar. Cost key `converterbessemer-rcc`
(`SmexRecipeConfig.cs:49-53`). Declared at `BlockConverterBessemer.cs:98-131`.

| Stage | Cost | Adds |
|---|---|---|
| 1 | — | `Root/GearShaft` |
| 2 | 24 plate · 24 nails · 12 rod | `Root/BottomIron` |
| 3 | 4 plate · 3 `lpex:pipe-straight-ns-{metal}` · 6 nails | `Root/GasIntake` |
| 4 | 60 `refractorybrick-fired-tier3` · 48 `game:clay-fire` | `Root/BottomRefractory` |
| 5 | 24 `refractorybrick-fired-tier3` · 24 fire clay | `Root/UpRefractory` |
| 6 | 12 plate · 12 nails · 6 rod | `Root/UpIron` |
| 7 | 12 fire clay | `Root/InputLining` |

Totals: 40 plate · 42 nails · 18 rod · 84 tier-3 refractory brick · 84 fire clay · 3 pipe segments. (The
handbook's totals are correct.)

Spawning the vessel is separate from building it: RMB the control with 1 `lpex:largegear-iron|-steel` and 8
`game:rod-iron|-steel` in the hotbar (`BlockEntityConverterControl.cs:1054-1066`, `SmexConfig.cs:100`,
`:103`). Creative gets it free (`:1011-1013`). The spawn accepts only lpex's smithable large gear, never
`game:gear-rusty`, so the vessel stays buildable in worlds with no loot (`:1052-1053`).

### B7 — stage 3 can never be satisfied

`lpex:pipe-straight-ns-{metal}` does not exist. The pipe blocktype declares variant groups `type` and
`orientation` only (`BlockPipe.cs:84-85`, confirmed by
`test/LowPressureExpanded.Tests/goldens/lpex/blocktypes/pipes/straight.json`), so the codes are
`lpex:pipe-straight-{ns|we|ud}` with no metal axis. The `{metal}` placeholder is filled from stage 2's
`storeWildCard` (`ExConstruction.cs:112-123`, `:200-203`) and resolves to `…-ns-iron` / `…-ns-steel`, neither
of which is a registered block. `TryConsumeIngredients` resolves every ingredient before the creative shortcut
is considered and hard-fails a non-wildcard miss (`ExConstruction.cs:165-178`), so:

> The Bessemer vessel cannot be completed, in survival or in creative-instant. Stage 3 is a wall.

One-token fix: drop the `-{metal}` suffix. The cast segment `lpex:pipe-straight-ns` exists, but it has no
recipe either (B19, [cast pipes](cast-pipes.md)), which also makes the gas intake recipe uncraftable - so the
converter is blocked in two independent places. Neither is on this page to fix.

---

## Operation

### Inputs → outputs

| In | Out |
|---|---|
| molten pig iron (`iwex:ingot-pigiron`) through the input tap | molten Bessemer steel (`smex:ingot-bessemersteel`) through the output cell |
| optional cold steel scrap - any exlib `Roles.Scrap` item, by role not by path | molten slag (`iwex:slag`) through the same output cell |
| air at ≥ `BlastPressureThreshold` on the pipe across the intake's connector face | gas - 4 % of the pig mass, gone, not a material |
| mechanical power on the transmission - to tilt only | on over-blow: soft ingot iron (`game:ingot-iron`) |

Metal identities resolve through `MetalRegistry.MoltenItemOf(token)` for `pigiron` / `bessemersteel` / `iron`
/ `slag` (`BlockEntityConverterControl.cs:95-99`), so a mod can redirect any of them.

### The four states

`ConverterOpState` (`ConverterTypes.cs:10-23`). `Normal` is reachable from anywhere; the pour deepens
`Normal → SlagPouring → SteelPouring`, because slag floats.

| State | Verb on the control | Tick | Effect |
|---|---|---|---|
| `Normal` | plain RMB | `TickNormal` `:229` | upright; blows while blast + a blowable bath are present |
| `Filling` | Sneak + RMB | `TickFilling` `:385` | tilted to the input tap; drains it into the bath |
| `SlagPouring` | Sprint + RMB from upright/fill | `TickSlagPouring` `:483` | shallow tilt; skims the floating slag |
| `SteelPouring` | Sprint + RMB again, held 1 s | `TickSteelPouring` `:540` | deep tilt; pours the steel beneath |

Only the deep pour is a held interaction - it destroys a finished heat, so `OnBlockInteractStart` returns
`true` to continue into `OnBlockInteractStep`, which commits after `BessemerPourHoldSeconds`
(`BlockConverterControl.cs:139-197`). Target selection is `ResolveTarget` (`:202-215`).

Scrap intercepts the click: a `Roles.Scrap` item in hand charges cold scrap instead of selecting a state
(`BlockConverterControl.cs:120-131` → `TryChargeScrap` `BlockEntityConverterControl.cs:615-661`). A non-scrap
held item falls through to state selection.

### The tick

One `OnProductionTick` per second, server-side, inherited unmodified from `BlockEntityProductionMachine`
(`ProductionTickMs` = 1000, `BlockEntityProductionMachine.cs:56`; the converter does not override it). It
gates in order (`BlockEntityConverterControl.cs:189-227`):

1. `StructureComplete` ∧ vessel `IsConstructed` - else "not built";
2. `IsGasIntakeAligned()` - else "misaligned";
3. `IsTransmissionAligned()` - else "transmission misaligned";
4. `UpdateSolidified()` then `SyncContentCooldown()`;
5. branch on `OpState`.

Gates 2 and 3 block everything, including the pour. A transmission knocked to the wrong facing mid-heat
freezes the charge in the vessel.

### The blow

`TickNormal` draws blast first, because the amount that arrives drives both the heat balance and the
decarburisation (`:258-262`):

```
demand        = BessemerBlastPerSecond · dt
blastConsumed = TryConsumeBlast(demand)          // 0 unless medium == "Air" and P ≥ BlastPressureThreshold
airFactor     = clamp(blastConsumed / demand, 0, 1)
```

`TryConsumeBlast` (`:768-796`) reads the network across the intake's connector face - the intake is a
connector, not a node, so the pipe is in the neighbouring cell.

Then `BlowStep` (`:328-369`):

```
carbon ← max(0, carbon − BessemerCarbonPerBlastLitre · blastConsumed)
```

* while the bath is pig, the part of the drop lying inside the band `[target, start]` sheds mass:
  `shed += pigCharged · (1 − SteelYield) · bandBurned / (start − target)`, split into slag by
  `SlagYield / (1 − SteelYield)` with the remainder as gas. A sub-unit carry (`_shedCarry`) keeps fractions;
* carbon ≤ `SteelCarbonTarget` → `RetypeToSteel()` (`:374-383`), which also melts any cold scrap in at
  `ScrapSteelYield`;
* Bessemer steel with carbon ≤ `OverblowCarbon` → retype to `game:ingot-iron`. Mass unchanged - the carbon
  that left is already counted in the gas.

The player picks the product by when they stop: cut the blast, or tilt away. Both work; only tilting is under
the player's hand.

### The heat balance the converter supplies

`ComputeHeatBalance` (`:694-718`) calls the shared `HeatBalance.Compute` ([heat
balance](../mechanics/heat-balance.md), `ExpandedLib/Process/HeatBalance.cs:55`) with:

```
T_in   = min(BessemerAutothermalCeiling, BessemerAutothermalBase + BessemerHeatPerCarbonUnit · airFactor)
T_loss = BessemerRadiationLoss + BessemerColdScrapLossCoefficient · scrapUnits
```

`fuelFrac`, `preheatGain` and `ambientLoss` are passed as zero and `fuelFactor` as 1 - the converter has no
coke and no ambient term, and its lang keys do not print those lines (`:702-717`, `:1301-1310`).

`HoldBathTemperature` forces the bath to `T_process` (`:680-685`); it is not a chase. It runs only on a tick
where blast actually arrived; with the blast cut, the charge falls at its own cooldown rate.

Refining is gated on `T_process ≥ BessemerRefineTemperature` (`:290-296`). Below it the blow stalls, the bath
keeps cooling, and `UpdateSolidified` eventually latches it frozen.

### Cold scrap = the temperature gate

Scrap is pure cold mass on `T_loss`. There is no hardcoded cap: past the ceiling the blast cannot hold the
bath over the refine floor, the heat stalls, and it freezes into the existing solidified/chisel path. Scrap
counts against vessel capacity, and may only be charged onto an empty vessel or a raw pig heat - never into
finished steel (`:631-635`).

### Pouring

Both pours go through the same output cell; the state selects which pool feeds it. `PourPerTick` is
`max(1, (int)(BessemerPourRate · dt))` (`:601`) - at the 1 s tick, 44 u/tick. On a refusal
(`accepted == 0` because the canal is full or already carrying the other medium) the converter soaks heat
into the cell so it does not cool to a plug, and says so (`:518-523`, `:563-569`) - the same idiom as the
furnace tap.

### Power is for tilting, not for blowing

`HasPower()` (`:799-810`) reads the transmission's `BEBehaviorMPBase.Network.Speed × GearedRatio` against
`BessemerPowerSpeedThreshold`. It is checked by `CanOperate` (`:903-932`), i.e. by state changes and scrap
charging only. `OnProductionTick` never checks it. An axle that stops mid-blow does not stop the blow; it
strands the heat, because the vessel cannot be tilted to pour.

---

## Numbers

`SmexValues.X` is a generated accessor over `SmexConfig.X`; the file:line is the config declaration.

### Owned — `src/SteelmakingExpanded/SmexConfig.cs`

| Key | Value | file:line | What it does |
|---|---|---|---|
| `BessemerConverterCapacity` | 4800 u | SmexConfig.cs:145 | Vessel capacity, counting charge + cold scrap. Settled value is 6000 - see Open #2 |
| `BessemerBlastPerSecond` | 8.0 L/s | :148 | Blast demanded per second while blowing |
| `BlastPressureThreshold` | 2.5 atm | :108 | Minimum pipe pressure that counts as "blast". The source comment says 3 atm (`BlockEntityConverterControl.cs:791`) |
| `BessemerPigCarbonStart` | 0.04 | :159 | Carbon fraction of fresh molten pig |
| `BessemerSteelCarbonTarget` | 0.002 | :162 | Retype-to-steel threshold |
| `BessemerOverblowCarbon` | 0.0005 | :167 | Retype-to-ingot-iron threshold |
| `BessemerCarbonPerBlastLitre` | 0.000016 | :171 | Decarburisation per litre of blast that reaches the bath |
| `BessemerAutothermalBase` | 1250 °C | :177 | `T_in` floor - the heat the pig arrives with |
| `BessemerHeatPerCarbonUnit` | 600 °C | :183 | `T_in` gain at full blast |
| `BessemerAutothermalCeiling` | 2000 °C | :186 | Cap on `T_in`. Dead at shipped values - the maximum reachable is 1850 |
| `BessemerRadiationLoss` | 50 °C | :189 | Always-on `T_loss` term |
| `BessemerRefineTemperature` | 1500 °C | :194 | Refine floor. Numerically equal to Bessemer steel's melting point |
| `BessemerColdScrapLossCoefficient` | 0.35 °C/u | :205 | `T_loss` per unit of cold scrap - the whole scrap cap |
| `BessemerScrapUnitValue` | 5 u | :208 | Molten units one scrap bit adds |
| `BessemerScrapSteelYield` | 0.97 | :213 | Scrap mass that becomes steel |
| `BessemerSteelYield` | 0.90 | :221 | Pig mass that becomes steel |
| `BessemerSlagYield` | 0.06 | :226 | Pig mass that becomes slag; the remaining 0.04 is gas |
| `BessemerPourRate` | 44 u/s | :231 | Drain rate through the output cell |
| `BessemerPowerSpeedThreshold` | 0.1 | :234 | Geared MP speed above which the converter counts as powered |
| `BessemerCooldownCoefficient` | 0.5 | :240 | Multiplier on `IwexValues.MoltenCooldownSpeed` for the charge |
| `BessemerChiselMaxFraction` | 0.2 | :246 | Residue fraction of capacity below which it can be chiselled instead of broken |
| `BessemerPourHoldSeconds` | 1.0 s | :97 | Hold time before the deep steel pour commits |
| `BessemerRequiredGears` | 1 | :100 | Large gears consumed to spawn the vessel |
| `BessemerRequiredRods` | 8 | :103 | Rods consumed to spawn the vessel |
| `RccBrokenDropsRatio` | 0.8 | :250 | Construction-material salvage on break; read live via `ExRccSettings` (`SteelmakingExpandedModSystem.cs:61`) |

Migrations that touch this machine: `0.9.0` resets `BessemerBlastPerSecond` (1 → 8 L/s,
`SmexConfig.cs:38-46`); `0.9.2` resets the gear/rod spawn cost (4 rusty gears + 12 rods → 1 large gear + 8
rods, `:51-60`); `0.9.5` resets `BessemerConverterCapacity` and retires `BessemerProcessDuration` /
`BessemerProcessTemperature` entirely (`:66-73`).

### Owned, hard-coded — not config

| Constant | Value | file:line |
|---|---|---|
| every peripheral offset | 5 `(int,int,int)` literals | `BlockEntityConverterControl.cs:47-51` |
| the local-frame flip | `+180` in two places | `:758`, `:1254` |
| metal tokens | `"pigiron"`, `"bessemersteel"`, `"iron"`, `"slag"` | `:95-99` |
| break-loss roll | `Random.Shared.Next(3) * 5` → 0, 5 or 10 u | `:1109` |
| recovery granularity | 5 u per metal bit (`MoltenChisel.BuildRecovery` default) | `ExpandedLib/Metals/MoltenCharge.cs:120-133` |
| smoke plume box + rates | 6 float literals + the `RisingPlume` arguments | `BlockEntityConverterBessemer.cs:108-158` |
| sound throttles | 4000 / 3000 / 1500 ms | `BlockEntityConverterControl.cs:305`, `:313`, `:472` |
| transmission MP resistance | `0.25f` | `BEBehaviorMPConverterTransmission.cs:20` |
| RCC stage costs | 13 integer literals | `BlockConverterBessemer.cs:98-131` |
| vessel mining tier / resistance | 4 (= iron pickaxe in this game version) / 45.0 | `:57-58`, pinned by `MegablockDropTierTests.cs:69` |
| production tick | 1000 ms, inherited | `ExpandedLib/Blocks/Machines/BlockEntityProductionMachine.cs:56` |
| away catch-up | `MaxAwayCatchupSteps` not overridden ⇒ 0 - the converter does not replay unloaded time | `…/BlockEntityProductionMachine.cs:105` |

### Derived — the numbers that matter in play

All at shipped values, full blast, no scrap.

| Quantity | Arithmetic | Result |
|---|---|---|
| decarburisation rate | `0.000016 × 8` | 0.000128 /s |
| blow length (pig → steel) | `(0.04 − 0.002) / 0.000128` | 296.9 s ≈ 4 min 57 s |
| over-blow window (steel → iron) | `(0.002 − 0.0005) / 0.000128` | 11.7 s - 12 ticks |
| steel window as a share of the blow | `11.7 / 308.6` | 3.8 % |
| `T_in` at full blast | `min(2000, 1250 + 600×1)` | 1850 °C |
| `T_process`, no scrap | `1850 − 50` | 1800 °C |
| scrap stall point | `(1800 − 1500) / 0.35` | 857 u = 172 bits = 17.9 % of 4800 (14.3 % of 6000) |
| blast starvation floor | `T_process ≥ 1500` ⇒ `airFactor ≥ 0.4167` | 3.33 L/s - below it the blow stalls |
| products of a full 4800 u pig charge | `×0.90 / ×0.06 / ×0.04` | 4320 u steel + 288 u slag + 192 u gas |
| products of a settled 6000 u charge | same | 5400 u steel + 360 u slag + 240 u gas |
| steel pour, 4800 capacity | `4320 / 44` | 98 s |
| slag pour, 4800 capacity | `288 / 44` | 7 ticks |
| chiselable residue ceiling | `0.2 × capacity` | ≤ 959 u (4800) / ≤ 1199 u (6000) |
| charge cooldown | `IwexValues.MoltenCooldownSpeed 24 × 0.5` | 12 - the VS per-in-game-hour rate (`IwexConfig.cs:31`, `ExlibConfig.cs:58-60`) |
| liquid / hardened thresholds | `0.8 × mp` / `0.3 × mp`; pig overrides liquid to 0.75 | steel liquid > 1200, hardened < 450; pig liquid > 862.5, hardened < 345 (`ExlibConfig.cs:63`, `:67`, `assets/iwex/config/metals/*.json`) |

The blow length does not depend on charge size. Carbon is a fraction and the decarburisation rate is not
divided by mass, so 500 u and 6000 u both blow in ~5 minutes. Every capacity decision is therefore free of
time cost - see Gotchas #1.

### Cited — owned elsewhere

| Thing | Owner |
|---|---|
| `T_process = T_in − T_loss`, `HeatBalance.Compute`, `HeatBalanceHud.AppendLedger`, `IsHotBlast` | [heat balance](../mechanics/heat-balance.md) |
| canal capacity, `MoltenFlowRate` (50), push/drain/soak, the metal-type refusal | [molten network](../mechanics/molten-network.md), [molten canal](molten-canal.md) |
| pipe pool volume, pressure, burst, `TryConsumeGas` | [pipe network](../mechanics/pipe-network.md) |
| `AirBlowerOutputPerSecond` (SmexConfig.cs:113) and the blower's undocumented ×3 | [twin-tub blower](twin-tub-blower.md) / lpex sub-machines |
| Bessemer steel's composition, grade and bar from pressure work | [materials.md](../materials.md) |
| cast slab / bloom / billet pour sizes | [long cell](long-cell.md) |

---

## Drops

| Broken | Returns | file:line |
|---|---|---|
| vessel | never itself - `drops: []` in the def and a `GetDrops` override returning `[]`, because a per-side variant can still be handed its own code as a registration fallback | `BlockConverterBessemer.cs:59`, `:206-211`; pinned by `MegablockDropTierTests.cs:41` |
| vessel - construction | every completed stage's materials × `RccBrokenDropsRatio` (0.8) | `ExConstruction.cs:96-142`; pinned by `MegablockDropTierTests.cs:57` |
| vessel - solidified charge | metal bits at 5 u each, minus a random 0 / 5 / 10 u mangling loss | `BlockEntityConverterControl.cs:1104-1114` |
| vessel - liquid charge | nothing; `OnConverterBroken` clears the charge regardless | `:1090-1102` |
| chisel-out instead | the residue at full value, no mangling loss; requires solidified ∧ hardened ∧ `< 0.2 × capacity` | `:1152-1174` |
| control / transmission / intake | themselves (no `GetDrops` override), `MaxStackSize` 1 | their defs |
| fillers | removed by the vessel's `OnBlockBroken` before `base` runs, so a throwing drop path cannot leave invisible solids behind | `BlockConverterBessemer.cs:163-167` |

A non-metal charge (slag, or anything without a `solidDrop`) falls back to slag via
`BuildRecovery(slagFallback: true)` (`BlockEntityConverterControl.cs:1121-1122`).

`OnBlockBroken` wraps `base` in a try/catch: a converter raised before `storeWildCard` was added throws while
expanding `metalplate-*`, and the exception escapes before the block is cleared, crashing the client. The
guard degrades to "no construction drops" and still removes the block (`BlockConverterBessemer.cs:169-193`).

---

## Code

| Type / member | file:line | Notes |
|---|---|---|
| `BlockEntityConverterControl` | `…/Converter/BlockEntities/BlockEntityConverterControl.cs:44` | 1471 lines - the brain. Extends `BlockEntityMultiblockStructure` |
| `OnProductionTick` | `:189` | the five gates + the state branch |
| `TickNormal` / `BlowStep` / `RetypeToSteel` | `:229` / `:328` / `:374` | the carbon model |
| `TickFilling` | `:385` | tap-closed check, capacity, single-metal rule, carbon re-seed by mass average |
| `TickSlagPouring` / `TickSteelPouring` / `PourPerTick` | `:483` / `:540` / `:601` | the split pour |
| `TryChargeScrap` | `:615` | `Roles.Scrap` classification, hotbar take, returns `false` when the click is not ours |
| `ComputeHeatBalance` / `HoldBathTemperature` / `UpdateSolidified` | `:694` / `:680` / `:720` | |
| `GetGlobalPos` / `TryConsumeBlast` / `HasPower` | `:748` / `:768` / `:799` | peripheral resolution |
| `IsGasIntakeAligned` / `IsTransmissionAligned` | `:829` / `:844` | the wildcard-layout backstop |
| `CanOperate` / `TrySetState` | `:903` / `:939` | every player gate, in order |
| `TrySpawnConverter` | `:977` | filler-volume pre-check via `StructureFillers.CanPlace` `:1005` |
| `OnConverterBroken` / `CanChiselOut` / `ChiselOutContent` | `:1090` / `:1152` / `:1162` | |
| `AppendStructureState` | `:1320` | the readout the vessel prints (the control shows power only) |
| `WriteHeatBalance` / `ReadHeatBalance` | `:1442` / `:1453` | the whole balance rides the tree - `GetBlockInfo` is client-side |
| `BlockEntityConverterBessemer` | `…/BlockEntityConverterBessemer.cs:26` | 296 lines; a mirror - no state of its own beyond the pose |
| `UpdateMirror` / `ApplyPose` / `SpawnSmokeParticles` | `:85` / `:169` / `:116` | |
| `IChiselableMolten` impl | `:229-234` | forwards every member to the control |
| `BlockConverterControl` | `…/Blocks/BlockConverterControl.cs:18` | def `:29-80` (layout `:40-71`); `OnBlockInteractStart` `:84`; `OnBlockInteractStep` `:169`; `ResolveTarget` `:202` |
| `BlockConverterBessemer` | `…/Blocks/BlockConverterBessemer.cs:28` | `BaseCode` `:39`; def `:51-138`; `OnBlockBroken` `:149`; filler-interaction routing `:217-283` |
| `BlockConverterIntake` | `…/Blocks/BlockConverterIntake.cs:19` | `INetworkConnector`; `ConnectorFace` `:47` |
| `BlockConverterTransmission` | `…/Blocks/BlockConverterTransmission.cs:16` | `IMechanicalPowerBlock`; connector face table `:39-47` |
| `BEBehaviorMPConverterTransmission` | `…/BlockEntities/BEBehaviorMPConverterTransmission.cs:17` | `BEBehaviorMPSubmachineBase`; resistance 0.25 |
| `BlockEntityConverterTransmission` | `…/BlockEntityConverterTransmission.cs:8` | an empty BE - it exists only to host the MP behaviour |
| `ConverterOpState` | `…/ConverterTypes.cs:10` | |
| `ConverterRecipeDefinitions` | `…/Recipes/Grid/ConverterRecipeDefinitions.cs:15` | |

### Where a caller hooks in

* A second converter mode (the deferred [Pierce-Smith copper converter](../deferred/non-ferrous/pierce-smith.md)
  is specified as exactly this) needs no new machine: swap the four metal tokens at
  `BlockEntityConverterControl.cs:95-99` and the carbon bands, and the vessel, tilt states, blast draw and
  pour all carry over unchanged.
* The rebuild replaces the layout at `BlockConverterControl.cs:40-71` with an ASCII layout and folds the
  intake + transmission into behaviour-capable fillers; the peripheral offsets (`:47-51`) become filler cells,
  and `IsGasIntakeAligned` / `IsTransmissionAligned` disappear with them.

### Tests

| File | Covers |
|---|---|
| `test/SteelmakingExpanded.Tests/Fixtures/SteelPlantScenes.cs:31` | `ConverterRig` - builds the real footprint through `StructureRig`, places every service port under the code its own layout cell names, and lets the control's monitor tick complete the structure (`:150`). It does not force `StructureComplete`. Five block codes were wrong before this rig existed |
| `Scenarios/BessemerScenarioTests.cs:28` `:56` | commissioning through the machine's own production tick; a breached shell stopping at the first gate |
| `…:84` `:124` `:146` `:162` | charge → blow → pour; slag off the shallow tilt through the shared cell; over-blow to ingot iron; a second heat after pouring the first |
| `…:189` `:210` `:228` `:240` `:249` | scrap yielding more steel; no blast ⇒ no refine; powered / stalled / no-network transmission |
| `Blocks/Converter/ConverterControlProcessTests.cs:166` … `:415` | fill + carbon seeding, retype at target, stopping above it, over-blow, mass conservation (`:237`), the three scrap cases (`:261`, `:279`, `:304`), both pours, the solidify latch theory (`:375`), `CanOperate` reasons |
| `Blocks/Converter/ConverterChiselTests.cs:104` … `:310` | the cooldown coefficient reaching metal already in the vessel (`:153`), all four chisel gates, the three solidified status messages, and that the vessel never self-drops (`:235`) |
| `Blocks/Converter/ConverterBessemerTests.cs:63` … `:129` | the mirror and the control link, including the tree round-trip |
| `Blocks/Converter/ConverterOrientationTests.cs:33` | every peripheral tracks the `side` variant in all four facings |
| `Blocks/Converter/ConverterTransmissionTests.cs:32` `:42` `:58` | resistance, discovery face, one axis sign per axis |
| `Definitions/MegablockDropTierTests.cs:41` `:57` `:69` | no self-drop, 80 % salvage, iron mining tier |
| `Definitions/SmexDefinitionGoldenTests.cs:17` | all four converter blocktype goldens and the three grid recipes |

No test asserts a rate as a number - not the blow length, not the pour rate, not the blast draw. The scenario
suite checks directions (metal moved / did not move), which is why `BessemerPourRate` could go from 16 to 44
without anything noticing.

---

## Gotchas

1. **The over-blow window is 11.7 seconds.** Reaching `BessemerSteelCarbonTarget` retypes the bath to
   steel, and `IsBlowable()` still returns true for steel (`:878-879`), so the blow continues. Unless the
   player tilts or the blast is cut within ~12 ticks, a finished heat becomes soft ingot iron. The status
   line that says "pour it, or blow on for soft iron" (`bessemer-status-steelready`) is printed only on a
   tick where no blast arrived (`:276-281`), so the warning appears exactly when it is not needed. The
   ~5-minute pig→steel leg is unaffected by charge size, so the whole difficulty of the machine sits in
   those 12 ticks.

2. **`BessemerRefineTemperature` (1500) equals Bessemer steel's melting point** (`meltingPoint: 1500`,
   `assets/smex/config/metals/bessemersteel.json`). The moment `T_process` falls under the refine floor, a
   steel bath is also below its own melting point, so `UpdateSolidified` latches on the next tick. The stall
   state is therefore a freeze for steel. It is a warning band for pig, whose melting point is 1150.

3. **`BessemerAutothermalCeiling` is dead.** `airFactor` is clamped to 1, so `T_in` maxes at
   `1250 + 600 = 1850` against a 2000 °C cap. Only a retune of `BessemerHeatPerCarbonUnit` above 750 would
   make it bind.

4. **Power gates the lever, not the blow.** `OnProductionTick` never calls `HasPower()`. A converter whose
   axle stops keeps refining and then over-blows and freezes, because the player cannot tilt it. MP is the
   tilt, not the drive.

5. **Stale source comment**: `BlockEntityConverterControl.cs:791` says "air at or above the blast threshold
   pressure (≥ 3 atm)". `BlastPressureThreshold` ships at 2.5 (`SmexConfig.cs:108`).

6. **Stale source comments ×2 on the chisel hatch**: `BlockConverterBessemer.cs:24` and `:215` both name the
   "upper-rear `(0,1,1)` footprint cell". The shipped `chiselOffset` is `(0,1,0)` (`:63`, and the golden) -
   top-centre, directly above the principal.

7. **Alignment is a raw string comparison.** `intake.Variant["side"] == (Block.Variant["side"] ?? "north")`
   (`:837`, `:852`). A variant renamed or a fifth facing added silently breaks both checks, and the layout -
   which only wildcards `smex:converter-intake*` - still reports complete.

8. **The converter does not replay unloaded time.** `MaxAwayCatchupSteps` is not overridden (default 0), so a
   heat left in an unloaded chunk is not simulated - but the charge's temperature is vanilla time-based, so
   it keeps falling. A player returns to a frozen bath with no intermediate states having run.

9. **Slag pouring can create up to 1 u of matter.** `amount = min(ceil(_moltenSlag), PourPerTick)` (`:504`)
    rounds up, then subtracts what was accepted, so a 0.4 u remainder pushes 1 u and leaves `_moltenSlag`
    negative. Harmless in play, but an R2 leak in a machine whose mass balance is otherwise exact.

10. **The handbook page teaches the retired machine.** `docs/smex/handbook/04-bessemer.html` describes a
    three-state converter (Filling / Normal / Pouring), a fixed "about 1800 °C for roughly five minutes"
    refine, and "break the converter with a steel pickaxe" as the only way to recover a frozen charge. The
    shipped machine has four states, an emergent temperature, a chisel-out path, cold scrap, slag and an
    over-blow - none of which the page mentions. Its material totals are correct; everything about operation
    is not. Its mining tier is also wrong: tier 4 is iron in this game version
    (`MegablockDropTierTests.cs:36-37`).

11. **`ChargeIsHardened` and the too-full case share one error slot.** `IChiselableMolten.ChiselBlockedError`
    returns `smex-bessemertoofull` when hardened and `smex-bessemertoohot` otherwise (`:231-232`), so a large
    residue that is still hot reports "too hot" rather than "too full". The block-info status line
    (`SolidifiedStatus`, `:1180-1190`) gets it right; the chisel feedback does not.

12. **`SmexConfig` has two `#region Bessemer converter` blocks** (`:95` and `:142`) with the machine's keys
    split across them. Anything that reads the file top-down will miss half of them.

13. **The transmission recipe asks for 16 gears in one grid cell** (`ConverterRecipeDefinitions.cs:55`) and is
    emitted twice - once for `game:gear-rusty`, once for `lpex:gear-*`. Both entries are otherwise identical.

14. **`ScrapHintStacks` is decorative.** The block-help preview hard-codes `game:metalbit-steel` and
    `game:metalbit-iron` (`BlockConverterControl.cs:285-295`), but the actual classification is the exlib
    `Roles.Scrap` role. Anything else registered under that role is chargeable and never shown.

---

## Open

1. **The shipped product contradicts the settled design (N1).** The blow yields
   `smex:ingot-bessemersteel` - a finished, directly usable material with `generateItemFamily: true`,
   `itemForms: [ingot, plate, rod, nails]` and a full tool preset (`assets/smex/config/metals/
   bessemersteel.json`), so Bessemer steel currently makes pickaxes and knives
   (`assets/smex/lang/en.json:12-19`, goldens under
   `test/SteelmakingExpanded.Tests/goldens/smex/itemtypes/bessemersteel/`). The settled design says the blow
   yields blown iron, which is unusable until it is recarburised in a [ladle](ladle.md) - and
   [materials.md](../materials.md) already says tools come from shear / crucible / HSS steel, never Bessemer.
   Three things have to change together: a `blowniron` metal def, the retype target at
   `BlockEntityConverterControl.cs:96-97`, and the tool preset. None of it is possible until the ladle
   exists, and the ladle does not exist as a type anywhere in `src/`.

2. **Capacity: 4800 shipped, 6000 settled - and 6000 still does not land on the ladder.**
   [STATE.md](../../plans/STATE.md) D4 sets 6000 u as "exactly 2 slab pours / 3 bloom pours". But
   `CapacityUnits` gates the charge, which is pig, and the blow sheds 10 % of it. A brim-full 6000 u pig
   charge pours 5400 u of steel = 1.8 slab pours. Either the key must become 6667 u of pig (6000 ÷ 0.90), or
   D4 must be restated as a steel capacity and the fill gate changed to count product. This needs deciding
   before the number is changed, or the tier gets a third capacity iteration that still misses.

3. **B7 blocks the build** (Construction). One token in `BlockConverterBessemer.cs:108`. B19 blocks it
   again - `lpex:pipe-straight-*` has no recipe, so even the corrected code is creative-only, and the same
   block is 1 of the gas intake's grid ingredients.

4. **The rebuild is scheduled and nothing has started.** Four blocks → one RCC megablock, coordinate layout →
   ASCII, intake/transmission → behaviour-capable fillers, plus a migration for existing worlds. Until then
   this machine is the only one in the suite whose structure cannot be read off
   [layouts.md](../../workbench/layouts.md).

5. **The blow length is charge-independent** (Numbers). If capacity is meant to be a meaningful choice, the
   decarburisation rate should divide by bath mass - at which point a 6000 u heat takes 1.25× a 4800 u heat
   and the converter blow ≫ pour relationship the tier wants becomes real. Doing so changes every derived
   duration on this page.

6. **The Pierce-Smith copper converter is specified as a mode of this vessel**
   ([pierce-smith](../deferred/non-ferrous/pierce-smith.md)) and nothing prepares for it. The four metal
   tokens are already indirected through `MetalRegistry`; the carbon bands are not.

7. **No `/exmod` verb exposes the converter's state**, and R7's "nothing is hidden" is satisfied only by the
   block-info readout on the vessel. The control shows power alone - a player looking at the block they just
   clicked sees the least.

8. **The layout does not check the tap's or the canal start's orientation**, the same trap the
   [cupola](cupola.md#gotchas) has: `iwex:moltencanal-tap*` is a wildcard, so a backwards tap completes the
   structure and then never pours in.
