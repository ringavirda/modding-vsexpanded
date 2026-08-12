# Cornish Boiler
**Status** live   **Mod** lpex

**Owns**
- The shared boiler model (`BlockEntityBoiler`, base of every boiler in the suite): the one tank shared
  between water and steam, `InternalPressure`, the `Idle → Heating → Boiling` FSM and its grace timers, the
  feedwater intake and the pressurised-feed steam flash, the connected-vessel steam push and its
  equalisation formula, the steam ceiling cap, lid venting, internal condensation, the choke/snuff rule,
  and the over-pressure burst (blast, salvage, shatter radius).
- The Cornish variant's stat table (capacity, boil window, steam rate, choke pressure, blast radius).
- The Cornish's structure: 3 × 3 × 8 volume, 23 filler cells, the firebox masonry bill, the six geometry
  offsets, and which cells the player interacts with.
- Its construction: grid frame recipe + the four RCC stages and their exact totals.
- Every `Boiler*` / `Cornish*` / `Steam*` config key and every hard-coded boiler constant.

**Does not own** - cited only, never restated:
- The pipe pool, one-medium rule, `LitresPerPipe`, pressure formulas, burst-by-tier, joints, leaks,
  chimney venting, evaporation-per-day, the network tick order -
  [pipe network](../mechanics/pipe-network.md).
- Fillers, the ASCII layout DSL, `Origin`-is-the-negation, projection, per-cell collision, the
  filler-can-never-be-a-graph-node rule - [multiblock](../mechanics/multiblock.md).
- Code-first defs, the RCC `ConstructionStages` builder, the `brokenDropsRatio` resolution chain, the
  recipe-cost catalogue and its `cheap` level - [recipes-config](../mechanics/recipes-config.md).
- The Watt engine's operating band, its break pressure, its power and the sub-machines it drives -
  [Watt engine](engine-watt.md).
- The Lancashire boiler's own stat table (same base class, hpex's numbers) -
  [Lancashire boiler](boiler-lancashire.md).
- The pressure valve that must sit between this boiler and a Watt engine - the valve is
  [pipe network](../mechanics/pipe-network.md)'s; the arithmetic that makes it mandatory is below.

---

## Role

The entry into steam: the first machine in the suite that turns fuel into a transportable, pressurised
medium. Iron-tier power comes from a river or the wind, steam-tier power comes from anywhere
([mp-energy](../mechanics/mp-energy.md) § the vanilla-MP bridge).

Declared liberty (2026-08-07): pairing this c. 1812 Cornish boiler with the Watt-era condensing
[engine](engine-watt.md) is a chronological compression. Such engines were historically fed by wagon and
haystack boilers, though mid-19th-century practice did retrofit Cornish boilers onto low-pressure beam
engines.

Mechanically the boiler is a single vessel with two contents. Water and steam share one capacity, so boiling
both makes steam and shrinks the space the steam lives in; pressure climbs on both counts.

Its three failure surfaces are all visible and all local:

| Failure | Trigger | Consequence |
|---|---|---|
| Choke | fire lit, exhaust outlet backed up to `0.8 atm` | fuel pile snuffed after 10 s |
| Leak | steam outlet with no pipe above it | steam bleeds at 16 L/s, never pressurises |
| Burst | boiling + firing + at the choke ceiling for 30 s | explosion, 40 % salvage, radius 3 |

---

## Structure

A megablock and a multiblock at once: it renders across a reserved volume of invisible fillers and it
verifies a player-built firebox around itself before it will run
([multiblock](../mechanics/multiblock.md) owns both systems).

| | |
|---|---|
| Class | `BlockBoilerCornish : BlockBoiler : BlockFilledMegastructure` (`BlockBoilerCornish.cs:14`, `BlockBoiler.cs:19-24`) |
| Block entity | `BlockEntityBoilerCornish : BlockEntityBoiler : BlockEntityMultiblockStructure` (`BlockEntityBoilerCornish.cs:13`, `BlockEntityBoiler.cs:30`) |
| Structure volume | 3 × 3 × 8 (X −1..1, Y −1..1, Z −2..5) = 72 cells |
| Filler footprint | 23 cells - `Origin(-1, 0)`, layer 0 all `'+'`, layer 1 `# + #` (`BlockBoilerCornish.cs:54-76`) |
| Principal | `(0,0,0)`, the `'L'` glyph of the layout |
| Orientation | `side` variant from `abstract/horizontalorientation` (`BlockBoiler.cs:70`) |
| `StructureAngle` | `AngleFromSide(side) + 180` (`BlockBoiler.cs:39-43`) - the body frame; the vessel extends along local `+z` |
| Shape spin | `rotateYByType` = `AngleFromSide(side)`, no `+180` offset (`BlockBoiler.cs:73` → `ExBlockDef.cs:200-208`; golden `goldens/lpex/blocktypes/boiler/cornish.json`) - see [Gotchas](#gotchas) |
| Verified layout | `Origin(-1, -2)`, three layers (`BlockBoilerCornish.cs:77-127`); rendered in [layouts.md](../../internal/workbench/layouts.md) § Section 2 |
| Completion monitor | every 3000 ms (`BlockEntityMultiblockStructure.cs:49`) |

### Geometry offsets — `BlockBoilerCornish.cs:35-52`, resolved through `BlockBoiler.cs:79-114`

All six rotate by `StructureAngle`. There is no coded fallback: a missing attribute resolves to the
principal (`BlockBoiler.cs:76-78`).

| Offset | Cell | What sits there | Read by |
|---|---|---|---|
| `fuelOffset` | `(0,0,-1)` | the coal pile - `@(air\|coalpile)` in the layout | `FuelWorldPos`, `BlockBoiler.cs:83-84` |
| `lidOffset` | `(0,1,1)` | filler cell carrying the access lid; fill/drain/toggle only respond here | `LidWorldPos`, `:91-92` |
| `steamConnectorOffset` | `(0,1,2)` | filler turned into an upward "pipe" port; the steam pipe goes in the cell above it, `(0,2,2)` | `SteamPipeWorldPos`, `:98-99` |
| `exhaustOutletOffset` | `(0,1,4)` | `lpex:pipe-outlet-fire-u` - a real block the player builds, and a graph node in its own right | `ExhaustOutletWorldPos`, `:87-88` |
| `lightSampleOffset` | `(0,1,2)` | body cell the animated mesh is lit from, instead of the firebox-adjacent principal | `LightSampleWorldPos`, `:106-107` |
| `explosionCenterOffset` | `(0,1,2)` | blast centre, so the burst goes off inside the vessel | `ExplosionCenterPos`, `:113-114` |

### The three ports

| Port | Cell | Direction | How it is resolved |
|---|---|---|---|
| Feedwater in | the boiler's own cell, DOWN face | pipe → boiler | `HasConnectorAt(DOWN)` (`BlockBoiler.cs:48`); BE reads `ConnectedNetwork<PipeNetwork>(DOWN)` (`BlockEntityBoiler.cs:326`). The layout supplies the `passthroughbend-fire-u` at `(0,-1,0)` |
| Steam out | `(0,2,2)`, above the port filler | boiler → pipe | `MarkSteamPort` sets the filler's `PortFace = "u"`, `PortNetworkType = "pipe"` right after placement (`BlockBoiler.cs:122-145`); `PushSteam` verifies a `BlockNetworkNode` with a DOWN connector then reads `NetworkAt<PipeNetwork>` on the pipe cell (`BlockEntityBoiler.cs:515-535`) |
| Exhaust out | `(0,1,4)` | boiler → pipe | `NetworkAt<PipeNetwork>(ExhaustOutletWorldPos)` - the outlet block is the node (`BlockEntityBoiler.cs:305-308`) |

The port filler is a connector rather than a graph node: the cell carries a port, not a membership. A
footprint cell that declares one *is* a node now ([multiblock](../mechanics/multiblock.md) § A filler
cell is a graph node when it declares one); the boiler's does not, and the port arm still answers for it.
See also [pipe network](../mechanics/pipe-network.md) § connectors.

### The player-built firebox — `BlockBoilerCornish.cs:77-127`

Derived from the layout glyph counts:

| Block | Count | Where |
|---|---|---|
| `game:claybricks-good-fire` | 33 | the shell: 21 on layer −1, 9 on layer 0, 3 on layer 1 |
| `game:cokeovendoor*` | 1 | `(0,0,-2)` - the stoking door |
| `lpex:pipe-passthrough-fire-*` | 2 | `(0,-1,-2)`, `(0,-1,-1)` - the feedwater line crossing the firebox wall |
| `lpex:pipe-passthroughbend-fire-u*` | 1 | `(0,-1,0)` - turns the feed up into the boiler's DOWN port |
| `lpex:pipe-outlet-fire-u` | 1 | `(0,1,4)` - the exhaust neck; cap it with a vanilla chimney |
| `@(air\|coalpile)` | 1 | `(0,0,-1)` - the fuel slot behind the door |
| `game:air*` | 1 | `(0,0,4)` - must stay clear |

---

## Assets

| Asset | Path | State |
|---|---|---|
| Editable shape | — | missing. Nothing under `assets/editable/shapes/` matches the boiler; the runtime shape is the only copy |
| Runtime shape | `assets/lpex/shapes/boiler/cornish.json` | present; root children `Base` · `BaseExtension` · `Casing` · `Flues` - exactly the four RCC stage element sets |
| Animations | same file | `idle` (30 f, `Hold`) · `lidopen` (30 f, `Hold`) - both are poses, not motion |
| Textures | `fire1`, `iron3`, `iron4`, `iron5`, `iron` | declared in the shape |
| Water surface | `BoilerWaterRenderer` + `waterRendererBox` `(-14,2,2)-(30,30,62)` | `BlockBoilerCornish.cs:43-51` |
| Handbook | `assets/lpex/config/handbook/01-boilers.json` ↔ `docs/lpex/handbook/01-boilers.html` | present, and wrong in four places - see [Gotchas](#gotchas) |

The RCC behaviour suppresses the default mesh, so the boiler is only visible through the animator holding
`idle` (or `lidopen`). One clip must always be running or the vessel disappears; the seeding guard at
`BlockEntityBoiler.cs:232-237` covers the legacy (1.20/1.21) `AnimatableRenderer` constructor, which leaves
`ShouldRender` false while `StartAnimation` skips the state change when the pose is already active.

`SwapBoilerRenderer` (`:216-239`) replaces vanilla's renderer with `BoilerAnimatableRenderer` to move the
light sample off the firebox cell; otherwise the whole vessel tints red at night.

---

## Construction

Two steps, both required.

### 1. The frame block — grid recipe (`MachineRecipeDefinitions.cs:59-68`)

```
P H P          P = metalplate-* (iron/steel) ×1 each
B N B          B = game:burnedbrick-fire ×2 each
               N = metalnailsandstrips-* ×2
               H = hammer (tool)
→ lpex:boilercornish-north
```

Totals: 2 plates · 4 fire bricks · 2 nails-and-strips.

The recipe also declares `Ingredient("I", StraightPipe(1))` (`:65`) - an `iwex:pipe-straight-*` that never
appears in the pattern. Dead ingredient.

### 2. The RCC stages — `BlockBoilerCornish.cs:128-149`

Right-click the placed block with the materials in the hotbar; each stage adds one element subtree.

| # | Adds | Plates | Nails | Rods | `burnedbrick-fire` |
|---|---|---|---|---|---|
| 1 | `Root/Base` | — | — | — | — |
| 2 | `Root/BaseExtension` | 6 | 4 | — | 8 |
| 3 | `Root/Flues` | 8 | 4 | 4 | — |
| 4 | `Root/Casing` | 8 | 8 | 4 | 36 |
| | **total** | **22** | **16** | **8** | **44** |

Every metal requirement accepts `iron` or `steel` with `storeWildCard: "metal"` (golden
`goldens/lpex/blocktypes/boiler/cornish.json`), so an iron-age player can build it and the stored variant
drives the salvage.

Catalogued twice for the cost system: `boilercornish-grid` and `boilercornish-rcc`
(`LpexRecipeConfig.cs:64`, `:68`) - level switched by `/exmod recipes lpex <level>`
([recipes-config](../mechanics/recipes-config.md)).

---

## Operation

```
       coal pile (0,0,-1)                        exhaust outlet (0,1,4) ──► chimney
              │  IsBurning && inventory non-empty        ▲  16 L/s "Exhaust" @ 0.6×T_steam
              │                                          │  refused above 0.8 atm ⇒ CHOKE
              ▼                                          │
   ┌─────────────────────────────────────────────────────┴───┐
   │  ONE 800 L VESSEL                                       │
   │   water  ──(180 s heat-up)──►  boiling: 2 L/s water     │──► steam port (0,2,2)
   │                                       ×16 = 32 L/s steam│    equalising push
   │   InternalPressure = steam / (800 − water)              │
   └──────────────▲──────────────────────────────────────────┘
                  │ ≤10 L/s up to 400 L, from the DOWN network
            feedwater pipe (0,-1,0)
```

### The tick — `OnProductionTick`, `BlockEntityBoiler.cs:289-450`

Runs every 1000 ms server-side, gated on `StructureComplete` (base) and `IsConstructed` (`:290-291`).
`dt` is clamped to 2× the tick (`BEBehaviorProductionMachine.cs:77`, `:146`). The boiler does not opt into
away-catch-up (`MaxAwayCatchupSteps` stays 0); only the furnace core does.

| # | Step | Line |
|---|---|---|
| 1 | `ApplyEvaporation` - calendar-based water loss | `:296`, `:643-657` |
| 2 | read the coal pile at `FuelWorldPos`: `IsBurning && inventory[0] non-empty` | `:298-304` |
| 3 | read the exhaust network; `draughtBlocked` when its pressure ≥ `0.8 atm`; `burning = fireOn && !draughtBlocked` | `:305-312` |
| 4 | choke grace: `fireOn && draughtBlocked` for 10 s ⇒ `pile.Extinguish()` + sound | `:316-324` |
| 5 | feedwater draw + the pressurised-feed steam flash | `:326-350` |
| 6 | the FSM switch | `:357-405` |
| 7 | `CapSteamToCeiling` | `:407`, `:578-584` |
| 8 | lid open ⇒ `VentExcessSteam`, reset the burst grace; else `PushSteam` + the burst grace | `:411-438` |
| 9 | exhaust production, 16 L/s at `0.6 × SteamTemperature()`, medium `"Exhaust"`, capped 0.8 atm | `:440-447` |

### The FSM — `BlockEntityBoiler.cs:79-84`, `:357-405`

| State | Enters when | Leaves when | Does |
|---|---|---|---|
| `Idle` | start, or shutdown | `burning && water ≥ 150 L` → `Heating` | `CondenseInternal` |
| `Heating` | — | 180 s elapsed → `Boiling`; fire out / water low for 10 s → `ShutDown` | accumulates `_heatingSeconds`; `HeatProgress` is the HUD % |
| `Boiling` | 180 s of heating | same 10 s grace → `ShutDown` | `BoilStep` while `InternalPressure < 5 atm` |

There is no upper water cutoff (`:352-354`): all three fill paths (auto intake, manual pour, condensation)
already cap at their own ceilings.

`ShutDown` (`:634-640`) resets to `Idle` and clears the timers; leftover steam then condenses in `Idle`. The
heating clock restarts from zero, so a 10-second gap in the fire costs the full 180 s again.

### Water → steam — `BoilStep`, `:482-490`

```
waterUse = min(water, SteamPerSecond × dt / expansion)      // 32 / 16 = 2 L/s
water   -= waterUse
steam   += waterUse × expansion                              // 32 L/s
```

`expansion` is not a literal: `BoiledMedium` (`:462-478`) asks `ExLiquids.Taxonomy.VaporisationTarget` what
`"Water"` boils into and by what factor, falling back to `"Steam"` / `SteamExpansionFactor` when the
catalogue leaves it open. The medium catalogue is [pipe network](../mechanics/pipe-network.md)'s.

### Pressure and temperature

```
InternalPressure = steam / max(1, Capacity − water)               // :118-119
SteamTemperature = 100 × (InternalPressure + 1)^0.25 °C           // :498-503
```

Pressure is gauge, so the `+1` converts to absolute and 0 atm gauge reads exactly the boiling point.

| gauge atm | °C |
|---|---|
| 0 | 100.0 |
| 2 (Watt engages) | 131.6 |
| 4 (Watt breaks) | 149.5 |
| 5 (Cornish choke) | 156.5 |

### Pushing steam into the run — `PushSteam`, `:511-568`

Boiler and pipe run are treated as connected vessels, so the boiler never empties itself into the line:

```
freeSpace  = max(1, Capacity − water)
eqPressure = (steam + netVolume) / (freeSpace + netMaxVolume)
transfer   = steam − eqPressure × freeSpace          // ≤ 0.001 ⇒ hold
accepted   = net.ProduceGasMeasured(transfer, T, medium, maxOutputPressure: InternalPressure)
```

A freshly-built run with no `PipeNetworkState` yet is treated as empty at `Nodes.Count × LitresPerPipe`
(`:543-544`) so the boiler can charge it at all.

With no pipe above the port the neck is open: `min(steam, 16 L/s × dt)` bleeds to atmosphere and the method
returns `true`, which is the only driver of the outlet-leak plume (`:520-532`).

### Relief paths

| Path | Rate | Floor | Line |
|---|---|---|---|
| Lid open (the emergency release) | `BoilerLidVentRate` 200 L/s | 1 atm while running; 0 once `Idle` | `:592-603` |
| Unpiped outlet | `BoilerSteamLeakRate` 16 L/s | 0 | `:520-532` |
| Hard cap | instant | steam clamped to `5 × (800 − water)` every tick | `:578-584` |
| Internal condensation | 200 L/s, `Idle` only | stops at `MaxBoilWater` 500 L | `:611-631` |

The hard cap sits at the ceiling, not below it, so it bounds the readout without disarming the burst (which
fires at `>= MaxOutputPressure`).

Condensation refuses above `InternalPressure ≥ SteamExpansionFactor` (`:620`) because condensing 16 L of
steam frees only 1 L of headspace, so in a nearly-full vessel it would raise pressure. That guard is
unreachable on either shipped boiler - see [Gotchas](#gotchas).

### Feedwater — `:326-350`

```
if waterNet != null && water < MaxWaterIntakeFill (400 L):
    request = min(400 − water, 10 × dt)
    drawn   = waterNet.TryConsumeLiquid(request)
    water  += drawn
    if drawn > 0 && feedPressure > 1 && state == Boiling && InternalPressure < 5:
        steam += drawn × (feedPressure − 1) × WaterPressureSteamBoost
```

Pumped water flashes: a pump delivering above 1 atm adds 1 extra litre of steam per litre per atm of excess.
The gate on `InternalPressure < MaxOutputPressure` (`:341-347`) is what keeps this path from lifting pressure
past the ceiling every boiling tick and ramping the readout into the hundreds of atm.

The auto intake stops at half capacity so a piped supply cannot overfill; manual pouring reaches
`MaxBoilWater` 500 L (`:59-60`, `:768`).

### Burst — `Explode`, `:659-695`

Armed when `Boiling && burning && InternalPressure >= 5 atm` for 30 s (`:423-437`). Lid open resets it
unconditionally (`:411-416`) - the design's answer to a runaway boiler.

1. Scatter 40 % of the construction materials from the RCC behaviour (`:669-673`, `:703-704`).
2. `RemoveStructure` clears the 23 fillers; the principal cell is set to air (`:674-676`).
3. `ShatterFragileBlocks` breaks everything within radius 3 whose `Resistance < 20` - pipes, ports, coal
   piles, soft terrain - with `BreakBlock(pos, null, 0.25f)` (`:682-688`, `:711-733`).
4. `CreateExplosion(center, EntityBlast, 3, 5)` supplies particles, sound, drops and entity damage
   (`:689-694`).

The threshold of 20 sits below the resistance-45 boilers, engines and their fillers, so one boiler bursting
cannot chain into the next (`LpexConfig.cs:103-106`).

### The player's verbs

| Verb | Where | Gate | Line |
|---|---|---|---|
| Right-click with materials | any cell | pre-construction | RCC stages |
| Ctrl + Shift + right-click | any cell | always | structure projection ([multiblock](../mechanics/multiblock.md)) - explicitly deferred to at `BlockBoiler.cs:204-206` |
| Hold right-click, empty hand, 0.5 s | lid cell only | constructed | toggles the lid (`BlockBoiler.cs:168`, `:266-296`) |
| Right-click, water container | lid cell, lid open | constructed | pour in, capped at 500 L (`BlockEntityBoiler.cs:759-791`) |
| Right-click, empty liquid container | lid cell, lid open | constructed | bail out - only water above 150 L is reachable (`:799-844`) |

Both fill and drain measure the transfer by the litre delta on the container, so vanilla's transfer-size
rounding cannot desync the two sides. Drain sets the source stack to `int.MaxValue` so a bucket fills to its
own free space in one click rather than 0.01 L (`:825-831`).

### HUD — `GetBlockInfo`, `:1023-1084`

Incomplete structure ⇒ only the missing-block count. Otherwise: water range, steam volume + pressure, then
one of boiling (rate + temperature) / heating % / needs water / idle, plus `Lid open`, `Choked` and the
over-pressure countdown when they apply. All volumes/pressures/temperatures go through `ExMeasure` so they
respect the metric/imperial preference.

---

## Numbers

### lpex config — `LpexConfig.cs`, file `ModConfig/ex_values.json`, section `lpex`

Shared by every boiler variant (LP and HP):

| key | value | file:line | what it does |
|---|---|---|---|
| `BoilingPoint` | `100 °C` | `LpexConfig.cs:52` | water⇄steam phase point; base of the saturation curve |
| `SteamExpansionFactor` | `16` | `:59` | litres of steam per litre of water (range-guarded ≥ 1) |
| `SteamSaturationExponent` | `0.25` | `:65` | exponent of `T = 100 × (p+1)^n` |
| `ExhaustMaxOutputPressure` | `0.8 atm` | `:74` | above this the exhaust run refuses gas ⇒ choke |
| `BoilerOverpressureSeconds` | `30 s` | `:77` | burst grace |
| `BoilerHeatUpSeconds` | `180 s` | `:80` | `Heating` → `Boiling` |
| `BoilerShutdownDelaySeconds` | `10 s` | `:83` | grace after fire-out / water-low |
| `BoilerShutdownCondenseRate` | `200 L/s` | `:86` | steam → water while `Idle` |
| `BoilerWaterIntakeFillFraction` | `0.5` | `:91` | auto-intake ceiling as a fraction of capacity |
| `BoilerWaterIntakeRate` | `10 L/s` | `:95` | auto-intake draw cap |
| `BoilerExhaustPerSecond` | `16 L/s` | `:98` | exhaust vented while burning - fixed for every variant |
| `BoilerChokeExtinguishSeconds` | `10 s` | `:101` | choked ⇒ fuel snuffed |
| `BoilerBlastResistanceThreshold` | `20` | `:106` | blocks below this resistance shatter in the blast |
| `BoilerExplosionDropRatio` | `0.4` | `:111` | salvage from a burst |
| `RccBrokenDropsRatio` | `0.8` | `:116` | salvage from mining it intact; wired at `LowPressureExpandedModSystem.cs:28-31` |
| `BoilerLidVentRate` | `200 L/s` | `:119` | open-lid blow-off |
| `BoilerSteamLeakRate` | `16 L/s` | `:123` | unpiped-outlet bleed |
| `BoilerWaterSurfaceLowLevel` | `0.2` | `:128` | rendered surface below the operating threshold |
| `BoilerWaterSurfaceHighLevel` | `0.99` | `:134` | rendered surface once operable |
| `WaterPressureSteamBoost` | `1.0` | `:138` | extra L of steam per L of feedwater per atm above 1 |

Cornish variant:

| key | value | file:line | what it does |
|---|---|---|---|
| `CornishBoilerCapacity` | `800 L` | `LpexConfig.cs:143` | one tank, shared between water and steam |
| `CornishBoilerMinBoilWater` | `150 L` | `:146` | operating floor; also the bucket-drain floor |
| `CornishBoilerMaxBoilWater` | `500 L` | `:149` | manual-fill / condensation ceiling |
| `CornishBoilerSteamPerSecond` | `32 L/s` | `:152` | ⇒ 2 L/s of water consumed |
| `CornishBoilerMaxOutputPressure` | `5.0 atm` | `:156` | choke ceiling, hard cap, and burst trigger |
| `CornishBoilerExplosionRadius` | `3` | `:158` | blast radius (blast damage radius is `r + 2` = 5) |

Derived (auto intake fills to `Capacity × 0.5`): `MaxWaterIntakeFill` = 400 L (`BlockEntityBoiler.cs:59-60`).

### Cited, owned elsewhere — the numbers that make the ladder

| quantity | value | owner | file:line |
|---|---|---|---|
| Watt engine engage pressure | `2.0 atm` | [Watt engine](engine-watt.md) | `LpexConfig.cs:163` |
| Watt engine break pressure | `4.0 atm` | [Watt engine](engine-watt.md) | `:166` |
| Cornish boiler choke | `5.0 atm` | this page | `:156` |
| cast (lpex) pipe burst | `5.0 atm` | [pipe network](../mechanics/pipe-network.md) | `:50` |
| plated (iwex) pipe burst | `2.5 atm` | [pipe network](../mechanics/pipe-network.md) | `IwexConfig.cs:163` |
| `LitresPerPipe` | `30 L` | [pipe network](../mechanics/pipe-network.md) | `ExlibConfig.cs:32` |
| `EvaporationLitresPerDay` | `50 L/day` | [pipe network](../mechanics/pipe-network.md) | `ExlibConfig.cs:43` |

```
2.0        <   4.0        <   5.0
Watt runs      Watt breaks    boiler chokes  ==  cast pipe bursts
```

The pressure valve is mandatory: the boiler's ceiling is a full atmosphere above the point at which the
engine begins wearing toward a break, and nothing in the boiler knows about the engine. A gate set in the
2-3 atm band satisfies both.

At 5.0 the boiler's ceiling equals the cast pipe's burst rating, so a boiler choking into a cast run holds
that run exactly at its burst threshold. `TryProduceGas` takes `min(maxOutputPressure, MinBurstPressure)`
([pipe network](../mechanics/pipe-network.md) § 3), so it is safe by a hair; a plated (iwex, 2.5 atm) segment
anywhere in the steam run silently halves the usable ceiling.

### Hard-coded — not config

| value | file:line | what it does |
|---|---|---|
| production tick `1000 ms` | `BlockEntityProductionMachine.cs:56` | one boiler beat per second |
| `dt` clamp `2 ×` tick | `BEBehaviorProductionMachine.cs:77`, `:146` | catch-up bound |
| completion monitor `3000 ms` | `BlockEntityMultiblockStructure.cs:49` | structure re-verification |
| client tick `250 ms` | `BlockEntityBoiler.cs:157` | water surface, glow, particles, hum |
| danger zone `0.9 × MaxOutputPressure` | `:127` | the warning-plume threshold |
| exhaust temperature `0.6 × SteamTemperature()` | `:444` | flue-gas temperature |
| feed liquid `"Water"` | `:455` | the taxonomy lookup key |
| fallback gas `"Steam"` / fallback expansion | `:473-477` | when the catalogue declares neither |
| transfer epsilon `0.001 L` | `:555` | below this the boiler holds its steam in |
| empty-run capacity fallback `Nodes × LitresPerPipe` | `:544` | so a fresh run can be charged |
| lid hold `0.5 s` | `BlockBoiler.cs:168` | toggle threshold |
| `waterRendererBox` fallback `(-16,0,0)-(16,16,48)` | `BlockEntityBoiler.cs:867` | when the attribute is absent |
| ambient display temperature `20 °C` | `:948-950` | idle water glow |
| boil hum `2500 ms`, vol `0.4`, range `16` | `:911-918` | the `Lava` loop, tuned low |
| plume counts: danger `4`, lid `6`, outlet `8` | `:939`, `:956`, `:964` | particle density |
| blast call `CreateExplosion(c, EntityBlast, r, r+2)` | `:689-694` | damage radius is 2 more than the shatter radius |
| shatter break chance `0.25` | `:731` | `BreakBlock` drop multiplier |
| resistance `45`, `MaxStackSize` 1 | `BlockBoiler.cs:63-64` | above the blast threshold on purpose |
| mining tier `3` | `BlockBoilerCornish.cs:33` | |
| shape selective elements `Root/Base/*` | `BlockBoiler.cs:73` | the unbuilt-state mesh |

---

## Drops

The boiler block never drops itself. `GetDrops` returns `[]` unconditionally (`BlockBoiler.cs:153-158`),
overriding the def's `"drops": []`, which is not reliably honoured for a variant block: the per-side variant
can still be handed its own code as a fallback drop at registration. Pinned by
`BoilerDropTests.A_boiler_never_drops_itself_even_if_registered_with_a_self_drop`.

| Path | Returns |
|---|---|
| Mined intact | 80 % of the construction materials, scattered by the RCC behaviour (`RccBrokenDropsRatio`, `LpexConfig.cs:116`) |
| Burst | 40 % (`BoilerExplosionDropRatio`, `:111`), pulled through `ExRightClickConstructable.GetConstructionDrops` because a burst skips the normal break path (`BlockEntityBoiler.cs:697-704`) |
| Fillers | removed, never dropped - `RemoveStructure` (`BlockBoiler.cs:117-118`) on the burst path, `BlockFilledMegastructure` on the break path |
| Firebox masonry | ordinary block drops; it is player-placed and the boiler never touches it |
| Water / steam held | lost. Nothing spills, nothing is refunded |

The engines keep their self-drop (they have a craftable frame); the boilers do not
(`BlockBoiler.cs:147-152`).

---

## Code

| Piece | file:line |
|---|---|
| `BlockBoiler : BlockFilledMegastructure, INetworkConnector, IFillerInteractionTarget, IBoilerGeometry` | `BlockStructures/Boiler/BlockBoiler.cs:19` |
| `BoilerShell` - the def surface both variants share | `BlockBoiler.cs:60-74` |
| `StructureAngle` (= `AngleFromSide + 180`) | `BlockBoiler.cs:39-43` |
| `MarkSteamPort` - turns a filler into an upward pipe port | `BlockBoiler.cs:131-145` |
| `GetDrops` ⇒ `[]` | `BlockBoiler.cs:153-158` |
| lid interaction triad (own cell + filler-forwarded) | `BlockBoiler.cs:170-336` |
| `BlockBoilerCornish` - footprint, layout, stages | `Boiler/Blocks/BlockBoilerCornish.cs:14` |
| `BlockEntityBoiler : BlockEntityMultiblockStructure` | `Boiler/BlockEntityBoiler.cs:30` |
| `OnProductionTick` | `:289-450` |
| `InternalPressure` / `InDangerZone` / `HeatProgress` | `:118`, `:125`, `:130` |
| `BoiledMedium` (taxonomy lookup) | `:462-478` |
| `BoilStep` | `:482-490` |
| `SteamTemperature` | `:498-503` |
| `PushSteam` (the connected-vessel equalisation) | `:511-568` |
| `CapSteamToCeiling` | `:578-584` |
| `VentExcessSteam` | `:592-603` |
| `CondenseInternal` | `:611-631` |
| `ApplyEvaporation` | `:643-657` |
| `Explode` / `ShatterFragileBlocks` | `:659-695` / `:711-733` |
| `ToggleLid` / `TryManualFill` / `TryManualDrain` | `:740-752` / `:759-791` / `:799-844` |
| `BlockEntityBoilerCornish` - the stat table | `Boiler/BlockEntities/BlockEntityBoilerCornish.cs:13` |
| `IBoilerGeometry` | `Boiler/IBoilerGeometry.cs` |
| `BoilerWaterRenderer` / `BoilerAnimatableRenderer` | `Boiler/BoilerWaterRenderer.cs` / `BoilerAnimatableRenderer.cs` |
| `ConstructedAnimator` (the shared RCC + animator triad) | `ExpandedLib/Blocks/Construction/ConstructedAnimator.cs:32` |
| `GraceTimer` | `ExpandedLib/Helpers/GraceTimer.cs:18` |

### Where a caller hooks in

- A new boiler variant implements the six abstract stats on `BlockEntityBoiler` (`:46-68`) and calls
  `BoilerShell` for the def. hpex's Lancashire is the worked example: same base, own numbers, own layout.
- Retargeting the phase change: nothing hardcodes steam. `BoiledMedium` asks the taxonomy what `"Water"`
  vaporises into and by what factor; the condenser does the mirror lookup.
- Changing the feed model: `:326-350` is the only water-in path from a network. The pending
  intake-gate-on-`InternalPressure` design ([pumps](pumps.md)) replaces exactly this block.

### Tests — `test/LowPressureExpanded.Tests/Blocks/Boiler/`

| file | pins |
|---|---|
| `BoilerMathTests.cs` | `InternalPressure`, its clamped denominator, the saturation curve, `HeatProgress`, the danger zone's three cases |
| `BoilerTickTests.cs` | every FSM edge: ignition, both refusals, heat-up, boiling conversion, both grace shutdowns, and re-firing after a shutdown with leftover steam |
| `BoilerSteamCycleTests.cs` | lid venting (3 cases), condensation (3, incl. the anti-burst refusal), `BoilStep` via the taxonomy, shutdown reset, the ceiling cap |
| `BoilerBeTests.cs` | tree round-trip, lid default + toggle |
| `BoilerManualWaterTests.cs` | fill/drain refusals |
| `BoilerDropTests.cs` | never self-drops |

Rigs: `Fixtures/BoilerRig.cs`, `Fixtures/BoilerFakes.cs`, `Fixtures/SteamPlantScenes.cs`.

---

## Gotchas

1. `CondenseInternal`'s anti-burst guard is unreachable on both shipped boilers. It refuses to condense at
   `InternalPressure >= SteamExpansionFactor` = 16 atm (`BlockEntityBoiler.cs:620`), but `CapSteamToCeiling`
   clamps pressure to `MaxOutputPressure` every tick - 5 for the Cornish, 12 for the Lancashire
   (`HpexConfig.cs:60`). The guard is correct defensively but cannot fire today; it is not live protection.

2. The class comment about the shape rotation is wrong. `BlockBoiler.cs:37-38` says "rotateYByType is offset
   to match" the `+180` body frame. It is not: `BoilerShell` calls `ShapeSpunPerOrientation(shapeBase)` with
   the default `offset = 0` (`:73`, `ExBlockDef.cs:200-208`), and the golden confirms `*-north: 0`.
   `BlockEntityBoiler.cs:852-857` states the truth - `StructureAngle` and `Shape.rotateY` differ by 180°,
   which is why the water box rotates by the former and the animator by the latter. Trusting the class
   comment puts the water surface on the firebox side.

3. The `"Cornish"` name collides across two mods. `lpex:boilercornish` is the low-pressure entry boiler;
   `hpex:enginecornish` is the high-pressure engine. They are unrelated machines from different tiers that a
   player will meet in the same sentence ("a Cornish boiler cannot drive a Cornish engine" - literally true,
   `BlockEntityBoilerCornish.cs:5-8`).

4. The handbook is wrong in four places (`assets/lpex/config/handbook/01-boilers.json`,
   `docs/lpex/handbook/01-boilers.html`):
   - "24 metal plates, 18 nails-and-strips" - the stages total 22 and 16.
   - "48 fire bricks" - the stages total 44.
   - "16 L of steam at 160-220 °C" - the Cornish tops out at 156.5 °C (5 atm); even a Lancashire at 12 atm
     reads 189.8 °C. 220 °C would need ~22 atm, which nothing can reach.
   - the pipe article (`00-steampower`) says "an iron pipe bursts above 5 atm while a stronger steel pipe
     holds up to 10 atm" - the model is plated 2.5 / cast 5.0 / rolled 12
     ([pipe network](../mechanics/pipe-network.md)).
   The 33 fire-brick firebox, the fitting list and the 5 atm / 32 L/s / 800 L / 150 L figures are right.

5. The grid recipe declares an ingredient it never uses. `Ingredient("I", StraightPipe(1))` at
   `MachineRecipeDefinitions.cs:65` has no `I` in the `"PHP,BNB"` pattern. Harmless, but it means the boiler
   frame costs no pipe despite reading as if it does.

6. An unpiped steam outlet cannot save an over-pressured boiler. The leak is 16 L/s while the boiler makes
   32 L/s. Only the lid (200 L/s, and it resets the burst grace) is a real relief (`:411-416`, `:520-532`).

7. Opening the lid on an `Idle` boiler empties the steam pocket completely, but on a running one it stops at
   1 atm (`:592-603`). The floor is state-dependent, not a constant.

8. `_choked` is cleared by the snuff, not by the fix. After `BoilerChokeExtinguishSeconds` fires, the code
   sets `_choked = false` (`:323`), so the HUD's "Choked" line disappears at the moment the fire goes out,
   which reads as the problem resolving itself.

9. Evaporation is charged against the calendar, not the tick. `_lastEvapDays` is not serialised (`:135`,
   `:643-657`), so it re-seeds to "now" on load: an unloaded chunk is never charged, by design. It is also
   not charged for a loaded chunk between the last tick and an unload.

10. The exhaust cap and the choke test share one number. `TryProduceGas(..., maxOutputPressure: 0.8)`
    (`:446`) and `draughtBlocked = pressure >= 0.8` (`:309-311`) both read `ExhaustMaxOutputPressure`. A
    chimney on the outlet draws the run back down; without one the boiler snuffs its own fire in 10 s.

11. The burst grace is serialised, the pipe network's is not. `_overpressure.ToTree` (`:992`) survives a
    reload, so a boiler saved 29 s into its countdown bursts a second after the chunk loads. The pipe
    network's equivalent timer is transient ([pipe network](../mechanics/pipe-network.md) § 5).

12. `ExplosionCenterOffset` and `LightSampleOffset` are the same cell `(0,1,2)`, and so is the steam port.
    Three unrelated concerns pinned to one cell by coincidence of the current geometry; moving the steam port
    would silently move the blast centre unless all three attributes are edited
    (`BlockBoilerCornish.cs:37-42`).

13. `_waterContainerStacks` / `_emptyContainerStacks` are `static` (`BlockBoiler.cs:441`, `:472`) and
    resolved once from `world.Blocks` - shared across every boiler and every world in the process.

---

## Open

- No editable shape. The runtime `assets/lpex/shapes/boiler/cornish.json` is the only copy; there is no
  source file to re-edit the model from.
- Feedwater does not fight back-pressure. The intake draws whatever the line offers regardless of
  `InternalPressure`, so a 1 atm manual pump feeds a 5 atm boiler. The settled fix - gate on
  `feedPressure ≥ InternalPressure` with a ramp, and turn pumps into pressure multipliers - is designed and
  unbuilt ([pumps](pumps.md)).
- The `+180` frame is stated three times and enforced nowhere. `StructureAngle`, the fillers, the connectors
  and the water renderer all agree by hand; `Shape.rotateY` does not. Nothing tests the relationship.
- Nothing consumes the burst's aftermath. The blast leaves the firebox masonry standing (bricks are above
  resistance 20), so a rebuilt boiler drops straight back into a still-valid layout.
- The handbook needs a sync pass - four numeric drifts above, and the boilers page still describes the
  two-tier pipe model.
- No away-catch-up. A boiler left boiling in an unloaded chunk resumes exactly where it stopped: no water
  burnt, no steam made, no coal consumed. The furnace core opts in (`MaxAwayCatchupSteps => 600`); the
  boiler does not.
