# Cornish Boiler
**Status** live   **Mod** iiex

**Owns**
- The shared boiler model (`BlockEntityBoiler`, base of every boiler in the suite): the one tank shared
  between water and steam, `InternalPressure`, the `Idle → Heating → Boiling` FSM and its grace timers, the
  feedwater intake and the pressurised-feed steam flash, the connected-vessel steam push and its
  equalisation formula, the steam ceiling cap, man-hatch venting, internal condensation, the choke/snuff
  rule, and the over-pressure burst (blast, salvage, shatter radius).
- The two fire models a boiler may carry - an internal `BEBehaviorFirebox` bed or a vanilla coal pile in
  the fuel cell - and the rate model that reads the burning fuel's own `combustibleProps`.
- The Cornish variant's stat table (capacity, boil window, steam rate, choke pressure, blast radius).
- The Cornish's structure: the 3 × 6 × 3 footprint, its 40 filler cells and three ports, the eight
  geometry offsets, and which cells the player interacts with.
- Its construction: grid frame recipe + the four RCC stages and their exact totals.
- Every `Boiler*` / `Cornish*` / `Steam*` config key and every hard-coded boiler constant.

**Does not own** - cited only, never restated:
- The pipe pool, one-medium rule, `LitresPerPipe`, pressure formulas, burst-by-tier, joints, leaks,
  chimney venting, evaporation-per-day, the network tick order -
  [pipe network](../mechanics/pipe-network.md).
- Fillers, the footprint DSL, `Origin`-is-the-negation, per-cell collision, the declarative filler port
  and the graph-node-when-it-declares-one rule - [multiblock](../mechanics/multiblock.md).
- The fuel bed itself - its layer arithmetic, the one-fuel rule, the burn-temperature floor and where the
  metallurgical exclusion lives - [firebox](firebox.md), [fuels](../items/fuels.md).
- Code-first defs, the RCC `ConstructionStages` builder, the `brokenDropsRatio` resolution chain, the
  recipe-cost catalogue and its `cheap` level - [recipes-config](../mechanics/recipes-config.md).
- The Watt engine's operating band, its break pressure, its power and the sub-machines it drives -
  [Watt engine](engine-watt.md).
- The Lancashire boiler's own stat table and footprint (same base class, siex's numbers) -
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
| Choke | fire lit, exhaust port backed up to `0.8 atm` | fire snuffed after 10 s |
| Leak | steam port with no pipe across its face | steam bleeds at 16 L/s, never pressurises |
| Burst | boiling + firing + at the choke ceiling for 30 s | explosion, 40 % salvage, radius 3 |

---

## Structure

A megablock and nothing else. The vessel renders across a reserved volume of invisible fillers, carries
its own masonry, grate and hatches in its shape, and verifies nothing around itself: finishing the
construction stages is the only gate on running it ([multiblock](../mechanics/multiblock.md) owns the
filler system).

| | |
|---|---|
| Class | `BlockBoilerCornish : BlockBoiler : BlockFilledMegastructure` (`BlockBoilerCornish.cs:17`, `BlockBoiler.cs:19-24`) |
| Block entity | `BlockEntityBoilerCornish : BlockEntityBoiler : BlockEntityProductionMachine` (`BlockEntityBoilerCornish.cs:13`, `BlockEntityBoiler.cs:36`) |
| Footprint | 3 × 6 × 3 (X −1..1, Y 0..2, Z −5..0) less the L3 corners = **40 fillers** plus the principal (`BlockBoilerCornish.cs:112-154`) |
| Principal | `(0,0,0)`, the `'O'` glyph of the footprint DSL - drawn for readability, never filled |
| Grid | `Origin(-1, -5)`; rows run Z −5 (far) .. 0 (near), columns X −1 .. +1 |
| Orientation | `side` variant, from `BoilerShell` (`BlockBoiler.cs:124-140`) |
| `StructureAngle` | `AngleFromSide(side) + BodySpinOffset`, `BodySpinOffset` = 180 (`BlockBoiler.cs:44`, `:49-53`) - the body frame; the vessel is drawn and reserved along local `-z` |
| Shape spin | `rotateYByType` = the same `AngleFromSide(side) + BodySpinOffset` (`BlockBoilerCornish.cs:43` → `BlockBoiler.BoilerShell`; the golden reads `*-n: 180`). `BoilerFootprintGuards` asserts the drawn mesh lands inside the declared footprint at all four orientations |
| Operable when | `IsConstructed` - the RCC behaviour reports its stages complete (`BlockEntityBoiler.cs:78`, `:82`) |

The authored grid, glyph for glyph:

```
L1 (y=0)          L2 (y=1)          L3 (y=2)
# # E             # # #             . . .
# # #             # # #             . _ .
# # #             # # #             . S .
# # #             # # #             . _ .
# # #             # # #             . M .
# O #             # I #             . _ .
```

| Glyph | Cell | Kind | What it is for |
|---|---|---|---|
| `O` | `(0,0,0)` | principal | the boiler block; takes feedwater on its own SOUTH face |
| `I` | `(0,1,0)` | `Solid` | the main (firing) hatch: open, charge, light, shut |
| `S` | `(0,2,-3)` | `Port(UP, "pipe")` | steam out; the pipe attaches at `(0,3,-3)` |
| `M` | `(0,2,-1)` | `Slab(DOWN)` | the man hatch: bucket fill, bucket drain, emergency vent |
| `E` | `(1,0,-5)` | `Port(EAST, "pipe")` | exhaust out; the pipe attaches at `(2,0,-5)` |
| `_` | `(0,2,-4)`, `(0,2,-2)`, `(0,2,0)` | `Slab(DOWN)` | the walkable top of the barrel; no interaction |
| `#` | the other 33 | `Solid` | plain filler |

`Slab` and `Port` are `FillerLayoutBuilder`'s (`FillerLayoutBuilder.cs`); the four slabs emit a
`collisionBox` of `y2 = 0.5` and the two ports emit `portFace` / `portNetwork`, all visible in
`goldens/iiex/blocktypes/boiler/cornish.json`.

### Geometry offsets — `BlockBoilerCornish.cs:57-108`, resolved through `BlockBoiler.cs:135-174`

All eight rotate by `StructureAngle`. There is no coded fallback: a missing attribute resolves to the
principal (`BlockBoiler.cs:142-145`).

| Offset | Cell | What sits there | Read by |
|---|---|---|---|
| `fuelOffset` | `(0,1,0)` | the fire: where the ignition sound plays and, on a coal-pile boiler, where the pile is read | `FuelWorldPos`, `BlockBoiler.cs:148-149` |
| `mainHatchOffset` | `(0,1,0)` | the `I` cell - charge, light and shut the firing door | `MainHatchWorldPos`, `:147-148` |
| `manHatchOffset` | `(0,2,-1)` | the `M` cell - fill, drain, vent | `ManHatchWorldPos`, `:151-152` |
| `steamConnectorOffset` | `(0,2,-3)` | the `S` cell; the steam pipe goes in the cell across that cell's own declared port face, `UP` here | `SteamPipeWorldPos`, `:167-168`; `SteamWorldFace`, `:86-87` |
| `exhaustOutletOffset` | `(1,0,-5)` | the `E` cell; the exhaust pipe goes east of it | `ExhaustOutletWorldPos`, `:143-144` |
| `explosionCenterOffset` | `(0,1,-2)` | geometric centre of the barrel, so the burst goes off inside the vessel | `ExplosionCenterPos`, `:173-174` |
| `lightSampleOffset` | `(0,1,-3)` | mid-body cell the animated mesh is lit from, away from the fire | `LightSampleWorldPos`, `:166-167` |
| `waterRendererBox` | voxels `(-8,4,-60)-(14,30,12)` | in-vessel water surface, inside the shell and below the flue centreline | `WaterRendererBoxes`, `BlockEntityBoiler.Client.cs:28-46` |

The blast centre, the light sample and the steam port are **three different cells on purpose**. Under the
old geometry all three resolved to one cell by coincidence, so moving the steam port moved the blast and
the lighting with it, silently (Gotcha 12).

### The three ports

`feedwaterFace` is declared data (`"south"`), so the vessel's own coupling face turns with it rather than
being written in code; the two footprint ports declare their own faces the same way and are read back off
the footprint, so the face a machine probes across and the face the cell answers on cannot drift apart.

| Port | Cell | Direction | How it is resolved |
|---|---|---|---|
| Feedwater in | the principal, SOUTH face | pipe → boiler | `FeedwaterWorldFace` rotates the declared `feedwaterFace` into the placed orientation (`BlockBoiler.cs:64-68`); `HasConnectorAt` answers on it (`:97`) and the BE reads `ConnectedNetwork<PipeNetwork>(FeedwaterWorldFace)` (`BlockEntityBoiler.cs:349-351`) |
| Steam out | `(0,3,-3)`, across the `S` port cell's own declared face | boiler → pipe | `PushSteam` reads that face off the footprint through `SteamWorldFace`, verifies a `BlockNetworkNode` with a connector on the opposite face, then reads `NetworkAt<PipeNetwork>` on the pipe cell (`BlockEntityBoiler.cs:645-677`, `BlockBoiler.cs:86-87`) |
| Exhaust out | east of the `E` port cell | boiler → pipe | `ExhaustNetwork` reads the cell's own declared `portFace` off the footprint, rotates it, and probes across it with `ConnectedNetworkAt<PipeNetwork>` (`BlockEntityBoiler.cs:475-482`, `BlockBoiler.cs:77-104`) |

A port filler is a **connector, not a graph node**: the cell answers `HasConnectorAt` for the principal
without joining the graph. That is the right arm here because the boiler is the machine on the network
and the cells are its skin - a membership would put four extra nodes in the steam run with nothing to say
([multiblock](../mechanics/multiblock.md) § A filler cell is a graph node when it declares one). A
connector cannot be probed from the principal, though, which is what `ConnectedNetworkAt` exists for:
the reciprocal test has to run from the port cell rather than from the block that owns it
(`MachinePorts.cs`). See also [pipe network](../mechanics/pipe-network.md) § connectors.

### The internal firebox

The fire is inside the shape. `BlockBoilerCornish` declares a hosted `BEBehaviorFirebox` on the block
entity with `layers` 4, `unitsPerLayer` 4, `bedElement` `CoalLayers` and `layerPrefix` `L`
(`BlockBoilerCornish.cs:49-56`) - four drawn courses of four units, **16 units full**, rendered as the
art's own `CoalLayers/L1`..`L4` elements in the charged fuel's own texture, one course per four units
standing so the charge is readable off the block as it burns. The bed's arithmetic and its
one-fuel rule are [firebox](firebox.md)'s. The firebox block's take-a-course-back-out verb is **not**
offered here: an empty hand at the main hatch is the swing/light gesture, so a boiler bed is charged and
burned, never dug out.

Which of the two fire models a boiler runs is read from whether the leaf declares that behaviour
(`BlockEntityBoiler.Client.cs:144`): a vessel with a bed is charged and lit through its main hatch; one
without has no main hatch at all and burns a vanilla coal pile in its `fuelOffset` cell, which is what
the Lancashire still does ([Lancashire boiler](boiler-lancashire.md)).

| | |
|---|---|
| Admission | the item's own `combustibleProps.BurnTemperature` ≥ `BoilerFuelMinTemp` 1000 °C (`BEBehaviorFirebox.IsFuel`) - no name list, so a coal another mod ships is admitted by declaring what it already declares |
| Lignite | **admitted.** The metallurgical exclusion is a reverberatory hearth's rule and lives on `BlockEntityFireboxFurnace.AcceptsFireboxFuel`, not on the bed ([fuels](../items/fuels.md)) |
| Lighting | an empty-handed 0.5 s hold at the main hatch, over an **open** door and a bed at capacity (`CanLightBed`, `BlockEntityBoiler.cs:892`). A part-charged bed is refused, as a furnace refuses one |
| Burn-down | `_fuelSeconds` accrues while burning and one unit leaves the bed per the fuel's own vanilla `burnDuration` (`BurnBedDown`, `:508-521`). The bed going empty puts the fire out |
| Snuffing | a choked boiler clears `_lit`; a coal-pile boiler extinguishes the pile instead (`Snuff`, `:495-501`) |

---

## Assets

| Asset | Path | State |
|---|---|---|
| Editable shape | `assets/editable/shapes/machines/steam/machine-pipe-megablock-boiler-cornish-new.json` | the source the runtime copy is converted from: `Root` plus `CoalLayers`, the three current clips |
| Editable shape, retired | `.../machine-pipe-megablock-boiler-cornish.json` | the pre-rework art - one `Root`, and the `lidopen` clip. Kept as the record of what the multiblock depicted; nothing converts from it |
| Runtime shape | `assets/iiex/shapes/boiler/cornish.json` | `Root` is unwrapped, so the top level is `MasonryBase` · `BoilerCasing` · `Flues` · `BoilerEnds` · `MasonryTop` · `CasingSegment5` · `CoalLayers` |
| Animations | same file | `idle` (30 f, `Repeat`) · `mainhatchopen` (30 f, `Hold`) · `manhatchopen` (30 f, `Hold`) - all three are poses, not motion |
| Textures | `cast-iron1`, `fire1`, `bituminous`, `iron3`, `iron4`, `iron5` | declared in the shape. `bituminous` is the fuel-layer code the bed repoints per charge |
| Water surface | `BoilerWaterRenderer` + `waterRendererBox` `(-8,4,-60)-(14,30,12)` | `BlockBoilerCornish.cs:100-107` |
| Handbook | `assets/iiex/config/handbook/06-boilers.json` ↔ `docs/iiex/handbook/06-boilers.html` | present and current |

The RCC behaviour suppresses the default mesh, so the boiler is only visible through the animator holding
a pose. One clip must always be running or the vessel disappears, which is why `ApplyPose` starts a hatch
clip before it stops `idle` (`BlockEntityBoiler.cs:272-280`); the seeding guard at `:253-257` covers the
legacy (1.20/1.21) `AnimatableRenderer` constructor, which leaves `ShouldRender` false while
`StartAnimation` skips the state change when the pose is already active.

`SwapBoilerRenderer` (`:236-259`) replaces vanilla's renderer with `BoilerAnimatableRenderer` to move the
light sample off the fire cell; otherwise the whole vessel tints red at night.

The coal courses are drawn in whatever fuel is charged, and only as many of them as are standing. The two
halves compose rather than one winning: the construction stage raises the whole `CoalLayers` group,
because a finished vessel has a grate, and the bed narrows that entry to one course per four charged units
- `BEBehaviorFirebox.ComposeOver` (`:126-147`), handed to the animator as
`BlockEntityBoiler.DrawnElements` (`:200-206`) and applied on every rebuild. An uncharged boiler therefore
shows bare bars, not a full grate.

The texture code `bituminous` is intercepted by the block entity's own `ITexPositionSource`
(`BlockEntityBoiler.Client.cs:210-224`), which resolves the charged item's first declared texture and
inserts it into the atlas on demand - a third-party coal is never one of a fixed key set, so the bed's own
charge is the only fixed point that works. A charge and a burn-down both only change the tree, so
`FromTreeAttributes` refreshes the animator whenever the fuel code or the course count moves
(`BlockEntityBoiler.cs:1063-1071`).

---

## Construction

Two steps, both required.

### 1. The frame block — grid recipe (`MachineRecipeDefinitions.cs:56-64`)

```
P H P          P = metalplate-* (iron/steel) ×1 each
B N B          B = game:burnedbrick-fire ×2 each
               N = metalnailsandstrips-* ×2
               H = hammer (tool)
→ iiex:boilercornish-north
```

Totals: 2 plates · 4 fire bricks · 2 nails-and-strips. No pipe: the vessel's couplings are footprint port
cells rather than fittings the player sets, so nothing in the frame is plumbing. The siex Lancashire's
frame is the same shape for the same reason.

### 2. The RCC stages — `BlockBoilerCornish.cs:155-176`

Right-click the placed block with the materials in the hotbar; each stage adds one element subtree.

| # | Adds | Plates | Rivets | Rods | `burnedbrick-fire` |
|---|---|---|---|---|---|
| 1 | `MasonryBase` | — | — | — | — |
| 2 | `BoilerCasing`, `CasingSegment5` | 6 | 8 | — | 8 |
| 3 | `Flues`, `CoalLayers` | 8 | 8 | 4 | — |
| 4 | `BoilerEnds`, `MasonryTop` | 8 | 16 | 4 | 36 |
| | **total** | **22** | **32** | **8** | **44** |

Rivets, not nails: a riveted seam is the one joint that is both strong and tight, so a pressure vessel
takes `iiex:rivet` and nothing else ([fasteners](../items/fasteners.md)). `PressureVesselGate` asserts the
negative - that no boiler stage accepts nails.

Every metal requirement accepts `iron` or `steel` with `storeWildCard: "metal"` (golden
`goldens/iiex/blocktypes/boiler/cornish.json`), so an iron-age player can build it and the stored variant
drives the salvage.

Catalogued twice for the cost system: `boilercornish-grid` and `boilercornish-rcc`
(`IiexRecipeConfig.cs:178`, `:174`) - level switched by `/exmod recipes iiex <level>`
([recipes-config](../mechanics/recipes-config.md)).

---

## Operation

```
   internal bed (0,1,0)                          exhaust port (1,0,-5) ──► chimney
     16 units, lit through   │                     ▲  16 L/s "Exhaust" @ 0.6×T_steam
     the main hatch          │                     │  refused above 0.8 atm ⇒ CHOKE
                             ▼                     │
   ┌───────────────────────────────────────────────┴─────────┐
   │  ONE 1600 L VESSEL                                      │
   │   water  ──(180 s / mult)──►  boiling: 4 L/s water      │──► steam port (0,2,-3)
   │                                    ×16 = 64 L/s steam   │    equalising push
   │   InternalPressure = steam / (1600 − water)             │
   └──────────────▲──────────────────────────────────────────┘
                  │ ≤20 L/s up to 800 L, from the SOUTH network
            feedwater pipe, south of the principal
```

Both boiling figures are multiplied by `FuelRateMultiplier` and the heat-up is divided by it; on every
shipped fuel but lignite that term is exactly 1 (§ Fuel drives the rate).

### The tick — `OnProductionTick`, `BlockEntityBoiler.cs:317-464`

Runs every 1000 ms server-side (`BlockEntityProductionMachine.cs:57`), gated on `CanRunProduction` =
`IsConstructed` (`:82`, `:318-319`). `dt` is clamped to 2× the tick
(`BEBehaviorProductionMachine.cs:77`, `:146`). The boiler does not opt into away-catch-up
(`MaxAwayCatchupSteps` stays 0); only the furnace core does.

| # | Step | Line |
|---|---|---|
| 1 | `ApplyEvaporation` - calendar-based water loss | `:323`, `:775-787` |
| 2 | read the fire: `_lit && Bed.Units > 0` on a bedded vessel, otherwise the coal pile at `FuelWorldPos` | `:325-327`, `:486-492` |
| 3 | read the exhaust network across the `E` cell's own port face; `draughtBlocked` when its pressure ≥ `0.8 atm`; `burning = fireOn && !draughtBlocked` | `:329-333`, `:475-482` |
| 4 | `BurnBedDown` - credit the burn against the fuel's own duration | `:335-336`, `:508-521` |
| 5 | choke grace: `fireOn && draughtBlocked` for 10 s ⇒ `Snuff` + sound | `:340-347` |
| 6 | feedwater draw + the pressurised-feed steam flash | `:349-373` |
| 7 | the FSM switch | `:380-423` |
| 8 | `CapSteamToCeiling` | `:425`, `:716-721` |
| 9 | man hatch open ⇒ `VentExcessSteam`, reset the burst grace; else `PushSteam` + the burst grace | `:429-452` |
| 10 | exhaust production, 16 L/s at `0.6 × SteamTemperature()`, medium `"Exhaust"`, capped 0.8 atm | `:454-461` |

### The FSM — `BlockEntityBoiler.cs:85-89`, `:380-423`

| State | Enters when | Leaves when | Does |
|---|---|---|---|
| `Idle` | start, or shutdown | `burning && water ≥ 300 L` → `Heating` | `CondenseInternal` |
| `Heating` | — | `EffectiveHeatUpSeconds` elapsed → `Boiling`; fire out / water low for 10 s → `ShutDown` | accumulates `_heatingSeconds`; `HeatProgress` is the HUD % |
| `Boiling` | the heat-up elapsed | same 10 s grace → `ShutDown` | `BoilStep` while `InternalPressure < 5 atm` |

There is no upper water cutoff (`:375-377`): all three fill paths (auto intake, manual pour, condensation)
already cap at their own ceilings.

`ShutDown` (`:766-771`) resets to `Idle` and clears the timers; leftover steam then condenses in `Idle`. The
heating clock restarts from zero, so a 10-second gap in the fire costs the full heat-up again.

### Fuel drives the rate — `FuelRateMultiplier`, `:610-619`

A boiler is limited by its heating surface, not by its flame: every coal it will take is 7-16× hotter than
the water it is boiling, so flame temperature reads as a pass/fail gate and **burn duration is what
distinguishes one fuel from another**.

```
mult   = clamp((flame − Tsat) / max(1, BoilerFuelDesignTemp − Tsat), 0, 1)   // design 1200 °C
rate   = SteamPerSecond × mult
heatUp = BoilerHeatUpSeconds / mult                     // EffectiveHeatUpSeconds, :611-612
burn   = 16 units × the fuel's own vanilla burnDuration
```

`flame` is `BedBurnTemperature` (`:559-560`), which is 0 on an empty or unlit bed - and a `flame <= 0`
short-circuits to **1**, so an empty boiler's arithmetic is the rated one rather than collapsing to zero.
A fuel at exactly the design temperature cancels `Tsat` algebraically and gives 1.0 at any pressure, which
is why coke (1340 °C) and bituminous (1200 °C) are indistinguishable on rate.

| Fuel | vanilla burn °C | duration | mult | L/s | Watt engines | a full bed lasts |
|---|---|---|---|---|---|---|
| `game:ore-anthracite` | 1200 | 196 s | 1.00 | 64.0 | 2.13 | 52.3 min |
| `game:ore-bituminouscoal` | 1200 | 84 s | 1.00 | 64.0 | 2.13 | 22.4 min |
| `game:ore-lignite` | 1100 | 77 s | 0.91 | 58.2 | 1.94 | 20.5 min |
| `game:coke` | 1340 | 40 s | 1.00 | 64.0 | 2.13 | 10.7 min |
| `game:charcoal` | 1300 | 40 s | 1.00 | 64.0 | 2.13 | 10.7 min |

Vanilla's own figures, from `.game/*/assets/survival/itemtypes/resource/{coke,charcoal,ore-ungraded}.json`;
the engine count is the rate over `WattEngineSteamRate` 30 L/s.

`EffectiveHeatUpSeconds` is the single member both the Heating-to-Boiling gate and `HeatProgress` read
(`:151-152`, `:400`), so a cool fire cannot show 100 % before the vessel has actually finished heating.

### Water → steam — `BoilStep`, `:593-601`

```
rate     = SteamPerSecond × FuelRateMultiplier               // 64 L/s at mult 1
waterUse = min(water, rate × dt / expansion)                 // 64 / 16 = 4 L/s
water   -= waterUse
steam   += waterUse × expansion                              // 64 L/s
```

`expansion` is not a literal: `BoiledMedium` (`:575-589`) asks `ExLiquids.Taxonomy.VaporisationTarget` what
`"Water"` boils into and by what factor, falling back to `"Steam"` / `SteamExpansionFactor` when the
catalogue leaves it open. The medium catalogue is [pipe network](../mechanics/pipe-network.md)'s.

### Pressure and temperature

```
InternalPressure = steam / max(1, Capacity − water)               // :137-138
SteamTemperature = 100 × (InternalPressure + 1)^0.25 °C           // :619-623
```

Pressure is gauge, so the `+1` converts to absolute and 0 atm gauge reads exactly the boiling point.

| gauge atm | °C |
|---|---|
| 0 | 100.0 |
| 2 (Watt engages) | 131.6 |
| 4 (Watt breaks) | 149.5 |
| 5 (Cornish choke) | 156.5 |

### Pushing steam into the run — `PushSteam`, `:645-705`

Boiler and pipe run are treated as connected vessels, so the boiler never empties itself into the line:

```
freeSpace  = max(1, Capacity − water)
eqPressure = (steam + netVolume) / (freeSpace + netMaxVolume)
transfer   = steam − eqPressure × freeSpace          // ≤ 0.001 ⇒ hold
accepted   = net.ProduceGasMeasured(transfer, T, medium, maxOutputPressure: InternalPressure)
```

A freshly-built run with no `PipeNetworkState` yet is treated as empty at `Nodes.Count × LitresPerPipe`
(`:683-684`) so the boiler can charge it at all.

With no pipe across the port's face the neck is open: `min(steam, 16 L/s × dt)` bleeds to atmosphere and the method
returns `true`, which is the only driver of the outlet-leak plume (`:665-673`).

### Relief paths

| Path | Rate | Floor | Line |
|---|---|---|---|
| Man hatch open (the emergency release) | `BoilerLidVentRate` 200 L/s | 1 atm while running; 0 once `Idle` | `:728-738` |
| Unpiped steam port | `BoilerSteamLeakRate` 16 L/s | 0 | `:665-673` |
| Hard cap | instant | steam clamped to `5 × (1600 − water)` every tick | `:716-721` |
| Internal condensation | 200 L/s, `Idle` only | stops at `MaxBoilWater` 1000 L | `:745-763` |

`BoilerLidVentRate` still says *lid*, and the hatch it governs is the man hatch. Renaming a config key
resets every player's value for it, so the key keeps the old word.

The hard cap sits at the ceiling, not below it, so it bounds the readout without disarming the burst (which
fires at `>= MaxOutputPressure`).

Condensation refuses above `InternalPressure ≥ SteamExpansionFactor` (`:752`) because condensing 16 L of
steam frees only 1 L of headspace, so in a nearly-full vessel it would raise pressure. That guard is
unreachable on either shipped boiler - see [Gotchas](#gotchas).

### Feedwater — `:349-373`

```
if waterNet != null && water < MaxWaterIntakeFill (400 L):
    request = min(400 − water, 10 × dt)
    drawn   = waterNet.TryConsumeLiquid(request)
    water  += drawn
    if drawn > 0 && feedPressure > 1 && state == Boiling && InternalPressure < 5:
        steam += drawn × (feedPressure − 1) × WaterPressureSteamBoost
```

Pumped water flashes: a pump delivering above 1 atm adds 1 extra litre of steam per litre per atm of excess.
The gate on `InternalPressure < MaxOutputPressure` (`:365-370`) is what keeps this path from lifting pressure
past the ceiling every boiling tick and ramping the readout into the hundreds of atm.

The auto intake stops at half capacity so a piped supply cannot overfill; manual pouring reaches
`MaxBoilWater` 1000 L (`:64-65`, `:943`).

### Burst — `Explode`, `:789-822`

Armed when `Boiling && burning && InternalPressure >= 5 atm` for 30 s (`:438-451`). An open man hatch
resets it unconditionally (`:429-432`) - the design's answer to a runaway boiler.

1. Scatter 40 % of the construction materials from the RCC behaviour (`:798-801`, `:829-830`).
2. `RemoveStructure` clears the 40 fillers; the principal cell is set to air (`:802-804`).
3. `ShatterFragileBlocks` breaks everything within radius 3 whose `Resistance < 20` - pipes, ports, soft
   terrain - with `BreakBlock(pos, null, 0.25f)` (`:810-815`, `:837-857`).
4. `CreateExplosion(center, EntityBlast, 3, 5)` supplies particles, sound, drops and entity damage
   (`:816-821`).

The threshold of 20 sits below the resistance-45 boilers, engines and their fillers, so one boiler bursting
cannot chain into the next (`IiexConfig.cs:1022`).

### The player's verbs

Interactions arrive on the boiler's own cell or are forwarded from a filler through
`IFillerInteractionTarget`; both funnel into the same three handlers (`BlockBoiler.cs:203-526`). A cell that
is neither hatch defers to the default behaviour, which is what drives the construction stages.

| Verb | Where | Gate | Line |
|---|---|---|---|
| Right-click with materials | any cell | pre-construction | RCC stages |
| Hold right-click, empty hand, 0.5 s | main hatch cell | constructed, vessel has a bed | swings the firing door - or **lights** it, when the door is open over a full unlit bed (`BlockBoiler.cs:214`, `:344-357`) |
| Right-click, fuel in hand | main hatch cell, hatch open | constructed | charges the bed and deducts what it took (`BlockEntityBoiler.cs:908-929`) |
| Hold right-click, empty hand, 0.5 s | man hatch cell | constructed | swings the man hatch (`:871-875`) |
| Right-click, water container | man hatch cell, hatch open | constructed | pour in, capped at 1000 L (`:935-966`) |
| Right-click, empty liquid container | man hatch cell, hatch open | constructed | bail out - only water above 300 L is reachable (`:974-1017`) |

Lighting and swinging share one gesture, so only one of them is advertised at a time - whichever the next
hold will actually do (`BlockBoiler.cs:475-485`).

Both fill and drain measure the transfer by the litre delta on the container, so vanilla's transfer-size
rounding cannot desync the two sides. Drain sets the source stack to `int.MaxValue` so a bucket fills to its
own free space in one click rather than 0.01 L (`BlockEntityBoiler.cs:1001-1004`).

### HUD — `GetBlockInfo`, `BlockEntityBoiler.cs:1085-1145`

An unfinished vessel shows nothing of its own. Otherwise: the bed's own line, water range, steam volume +
pressure, then one of boiling (rate + temperature) / heating % / needs water / idle, plus `Main hatch
open`, `Man hatch open`, `Choked` and the over-pressure countdown when they apply. The boiling line shows
`SteamPerSecond × FuelRateMultiplier` - the rate actually being made, not the rated ceiling (`:1116`). All
volumes/pressures/temperatures go through `ExMeasure` so they respect the metric/imperial preference.

---

## Numbers

### iiex config — `IiexConfig.cs`, file `ModConfig/ex_values.json`, section `iiex`

Shared by every boiler variant (LP and HP):

| key | value | file:line | what it does |
|---|---|---|---|
| `BoilingPoint` | `100 °C` | `IiexConfig.cs:202` | water⇄steam phase point; base of the saturation curve |
| `SteamExpansionFactor` | `16` | `:977` | litres of steam per litre of water (range-guarded ≥ 1) |
| `SteamSaturationExponent` | `0.25` | `:982` | exponent of `T = 100 × (p+1)^n` |
| `ExhaustMaxOutputPressure` | `0.8 atm` | `:991` | above this the exhaust run refuses gas ⇒ choke |
| `BoilerOverpressureSeconds` | `30 s` | `:994` | burst grace |
| `BoilerHeatUpSeconds` | `180 s` | `:997` | the rated `Heating` → `Boiling` time, divided by `FuelRateMultiplier` |
| `BoilerShutdownDelaySeconds` | `10 s` | `:1000` | grace after fire-out / water-low |
| `BoilerShutdownCondenseRate` | `200 L/s` | `:1003` | steam → water while `Idle` |
| `BoilerWaterIntakeFillFraction` | `0.5` | `:1008` | auto-intake ceiling as a fraction of capacity |
| `BoilerWaterIntakeRate` | `20 L/s` | `:1011` | auto-intake draw cap |
| `BoilerExhaustPerSecond` | `16 L/s` | `:1014` | exhaust vented while burning - fixed for every variant |
| `BoilerChokeExtinguishSeconds` | `10 s` | `:1017` | choked ⇒ fire snuffed |
| `BoilerBlastResistanceThreshold` | `20` | `:1022` | blocks below this resistance shatter in the blast |
| `BoilerExplosionDropRatio` | `0.4` | `:1027` | salvage from a burst |
| `RccBrokenDropsRatio` | `0.8` | `:1032` | salvage from mining it intact; wired at `IronIndustryExpandedModSystem.cs:53-56` |
| `BoilerLidVentRate` | `200 L/s` | `:1035` | open man-hatch blow-off (the key keeps the old name) |
| `BoilerSteamLeakRate` | `16 L/s` | `:1039` | unpiped steam-port bleed |
| `BoilerWaterSurfaceLowLevel` | `0.2` | `:1044` | rendered surface below the operating threshold |
| `BoilerWaterSurfaceHighLevel` | `0.99` | `:1050` | rendered surface once operable |
| `WaterPressureSteamBoost` | `1.0` | `:1054` | extra L of steam per L of feedwater per atm above 1 |

The fuel model's two keys sit with the firebox rather than with the boilers, because the floor governs
every bed in the suite:

| key | value | file:line | what it does |
|---|---|---|---|
| `BoilerFuelMinTemp` | `1000 °C` | `IiexConfig.cs:547` | the floor below which an item is not firebox fuel at all |
| `BoilerFuelDesignTemp` | `1200 °C` | `:540` | flame temperature at which a grate saturates its heating surface; `FuelRateMultiplier` is 1 at or above it |
| `FireboxLayersPerCell` / `FireboxUnitsPerLayer` | `6` / `2` | `:532`, `:527` | the *defaults* a bed uses when its blocktype declares neither. The Cornish declares 4 / 4 |

Cornish variant:

| key | value | file:line | what it does |
|---|---|---|---|
| `CornishBoilerCapacity` | `1600 L` | `IiexConfig.cs:1059` | one tank, shared between water and steam |
| `CornishBoilerMinBoilWater` | `300 L` | `:1062` | operating floor; also the bucket-drain floor |
| `CornishBoilerMaxBoilWater` | `1000 L` | `:1065` | manual-fill / condensation ceiling |
| `CornishBoilerSteamPerSecond` | `64 L/s` | `:1068` | ⇒ 4 L/s of water consumed at full rate |
| `CornishBoilerMaxOutputPressure` | `5.0 atm` | `:1072` | choke ceiling, hard cap, and burst trigger |
| `CornishBoilerExplosionRadius` | `3` | `:1074` | blast radius (blast damage radius is `r + 2` = 5) |

Derived (auto intake fills to `Capacity × 0.5`): `MaxWaterIntakeFill` = 800 L (`BlockEntityBoiler.cs:64-65`).

One boiler at full rate carries **2.13 Watt engines** (64 / `WattEngineSteamRate` 30).

### Cited, owned elsewhere — the numbers that make the ladder

| quantity | value | owner | file:line |
|---|---|---|---|
| Watt engine engage pressure | `2.0 atm` | [Watt engine](engine-watt.md) | `IiexConfig.cs:1079` |
| Watt engine break pressure | `4.0 atm` | [Watt engine](engine-watt.md) | `:1082` |
| Watt engine steam draw | `30 L/s` | [Watt engine](engine-watt.md) | `:1088` |
| Cornish boiler choke | `5.0 atm` | this page | `:1072` |
| cast (iiex) pipe burst | `5.0 atm` | [pipe network](../mechanics/pipe-network.md) | `:189` |
| plated (iiex) pipe burst | `2.5 atm` | [pipe network](../mechanics/pipe-network.md) | `:175` |
| `LitresPerPipe` | `30 L` | [pipe network](../mechanics/pipe-network.md) | `ExlibConfig.cs:52` |
| `EvaporationLitresPerDay` | `50 L/day` | [pipe network](../mechanics/pipe-network.md) | `ExlibConfig.cs:63` |

```
2.0        <   4.0        <   5.0
Watt runs      Watt breaks    boiler chokes  ==  cast pipe bursts
```

The pressure valve is mandatory: the boiler's ceiling is a full atmosphere above the point at which the
engine begins wearing toward a break, and nothing in the boiler knows about the engine. A gate set in the
2-3 atm band satisfies both.

At 5.0 the boiler's ceiling equals the cast pipe's burst rating, so a boiler choking into a cast run holds
that run exactly at its burst threshold. `TryProduceGas` takes `min(maxOutputPressure, MinBurstPressure)`
([pipe network](../mechanics/pipe-network.md) § 3), so it is safe by a hair; a plated (iiex, 2.5 atm) segment
anywhere in the steam run silently halves the usable ceiling.

### Hard-coded — not config

| value | file:line | what it does |
|---|---|---|
| production tick `1000 ms` | `BlockEntityProductionMachine.cs:57` | one boiler beat per second |
| `dt` clamp `2 ×` tick | `BEBehaviorProductionMachine.cs:77`, `:146` | catch-up bound |
| client tick `250 ms` | `BlockEntityBoiler.cs:182` | water surface, glow, particles, hum |
| danger zone `0.9 × MaxOutputPressure` | `:144-146` | the warning-plume threshold |
| exhaust temperature `0.6 × SteamTemperature()` | `:457` | flue-gas temperature |
| feed liquid `"Water"` | `:569` | the taxonomy lookup key |
| fallback gas `"Steam"` / fallback expansion | `:584-588` | when the catalogue declares neither |
| transfer epsilon `0.001 L` | `:695` | below this the boiler holds its steam in |
| empty-run capacity fallback `Nodes × LitresPerPipe` | `:684` | so a fresh run can be charged |
| hatch hold `0.5 s` | `BlockBoiler.cs:214` | toggle (and ignition) threshold, both hatches |
| `waterRendererBox` fallback `(-16,0,0)-(16,16,48)` | `BlockEntityBoiler.Client.cs:32` | when the attribute is absent |
| boil hum `2500 ms`, vol `0.4`, range `16` | `Client.cs:72-81` | the `Lava` loop, tuned low |
| plume counts: danger `4`, man hatch `6`, steam port `8` | `Client.cs:100`, `:117`, `:124` | particle density |
| fuel texture code `"bituminous"`, default `game:block/coal/bituminous` | `Client.cs:193`, `:201-203` | the shape's own baked-in fuel look, and the key the bed repoints. `BoilerFuelTextureGuards` checks the code against the shipped shape |
| blast call `CreateExplosion(c, EntityBlast, r, r+2)` | `BlockEntityBoiler.cs:816-821` | damage radius is 2 more than the shatter radius |
| shatter break chance `0.25` | `:855` | `BreakBlock` drop multiplier |
| resistance `45`, `MaxStackSize` 1 | `BlockBoiler.cs:131-132` | above the blast threshold on purpose |
| `BodySpinOffset` `180` | `BlockBoiler.cs:44` | the offset both `StructureAngle` and the leaf's shape spin carry |
| mining tier `3` | `BlockBoilerCornish.cs:45` | |
| shape selective elements `MasonryBase/*` | `BlockBoilerCornish.cs:47` | the unbuilt-state mesh |

---

## Drops

The boiler block never drops itself. `GetDrops` returns `[]` unconditionally (`BlockBoiler.cs:196-201`),
overriding the def's `"drops": []`, which is not reliably honoured for a variant block: the per-side variant
can still be handed its own code as a fallback drop at registration. Pinned by
`BoilerDropTests.A_boiler_never_drops_itself_even_if_registered_with_a_self_drop`.

| Path | Returns |
|---|---|
| Mined intact | 80 % of the construction materials, scattered by the RCC behaviour (`RccBrokenDropsRatio`, `IiexConfig.cs:1032`) |
| Burst | 40 % (`BoilerExplosionDropRatio`, `:1050`), pulled through `ExRightClickConstructable.GetConstructionDrops` because a burst skips the normal break path (`BlockEntityBoiler.cs:824-830`) |
| Fillers | removed, never dropped - `RemoveStructure` (`BlockBoiler.cs:186-187`) on the burst path, `BlockFilledMegastructure` on the break path |
| Fuel in the bed | lost. The vessel offers no take-a-course verb - an empty hand at the main hatch is the swing/light gesture - so a mischarged bed can only be burned off |
| Water / steam held | lost. Nothing spills, nothing is refunded |

The engines keep their self-drop (they have a craftable frame); the boilers do not
(`BlockBoiler.cs:190-195`).

---

## Code

| Piece | file:line |
|---|---|
| `BlockBoiler : BlockFilledMegastructure, INetworkConnector, IFillerInteractionTarget, IBoilerGeometry` | `BlockStructures/Boiler/BlockBoiler.cs:19` |
| `BoilerShell` - the def surface both variants share | `BlockBoiler.cs:124-140` |
| `BodySpinOffset` / `StructureAngle` (= `AngleFromSide + 180`) | `BlockBoiler.cs:44`, `:49-53` |
| `FeedwaterWorldFace` / `ExhaustWorldFace` / `SteamWorldFace` / `PortWorldFaceAt` | `BlockBoiler.cs:64-104` |
| `GetDrops` ⇒ `[]` | `BlockBoiler.cs:196-201` |
| the hatch interaction triad (own cell + filler-forwarded) | `BlockBoiler.cs:203-526` |
| `BlockBoilerCornish` - footprint, offsets, bed, stages | `Boiler/Blocks/BlockBoilerCornish.cs:17` |
| `BlockEntityBoiler : BlockEntityProductionMachine` | `Boiler/BlockEntityBoiler.cs:36` |
| `IsConstructed` / `CanRunProduction` | `:78`, `:82` |
| `ApplyPose` / `HoldClip` (the three-clip hold) | `:272-280` / `:284-302` |
| `OnProductionTick` | `:317-464` |
| `InternalPressure` / `InDangerZone` / `HeatProgress` | `:137-138`, `:144-146`, `:151-152` |
| `ExhaustNetwork` / `PileIsBurning` / `Snuff` / `BurnBedDown` | `:475-482` / `:486-492` / `:495-501` / `:508-521` |
| `BedStack` / `BurnSecondsPerUnit` / `BedBurnTemperature` | `:534-552` / `:555-556` / `:559-560` |
| `BoiledMedium` (taxonomy lookup) | `:575-589` |
| `BoilStep` | `:593-601` |
| `FuelRateMultiplier` / `EffectiveHeatUpSeconds` | `:610-619` / `:628-629` |
| `SteamTemperature` | `:636-640` |
| `PushSteam` (the connected-vessel equalisation) | `:645-705` |
| `CapSteamToCeiling` | `:716-721` |
| `VentExcessSteam` | `:728-738` |
| `CondenseInternal` | `:745-763` |
| `ApplyEvaporation` | `:775-787` |
| `Explode` / `ShatterFragileBlocks` | `:789-822` / `:837-857` |
| `ToggleMainHatch` / `ToggleManHatch` / `CanLightBed` / `LightBed` | `:864-868` / `:871-875` / `:892` / `:895-902` |
| `TryChargeBed` / `TryManualFill` / `TryManualDrain` | `:908-929` / `:935-966` / `:974-1017` |
| `DrawnElements` (the stage set narrowed to the standing courses) | `:200-206`, `BEBehaviorFirebox.ComposeOver` `:126-147` |
| `Bed` / `FuelTexturePath` / the `ITexPositionSource` indexer | `Boiler/BlockEntityBoiler.Client.cs:144` / `:155-162` / `:210-224` |
| `BlockEntityBoilerCornish` - the stat table | `Boiler/BlockEntities/BlockEntityBoilerCornish.cs:13` |
| `IBoilerGeometry` | `Boiler/IBoilerGeometry.cs` |
| `BoilerWaterRenderer` / `BoilerAnimatableRenderer` | `Boiler/BoilerWaterRenderer.cs` / `BoilerAnimatableRenderer.cs` |
| `BEBehaviorFirebox` - the hosted fuel bed | `BlockStructures/Furnaces/BEBehaviorFirebox.cs` |
| `ConstructedAnimator` (the shared RCC + animator triad) | `ExpandedLib/Blocks/Construction/ConstructedAnimator.cs:21` |
| `GraceTimer` | `ExpandedLib/Helpers/GraceTimer.cs:12` |

### Where a caller hooks in

- A new boiler variant implements the six abstract stats on `BlockEntityBoiler` (`:50-73`) and calls
  `BoilerShell` for the def, passing the spin offset its own art needs. siex's Lancashire is the worked
  example: same base, own numbers, own footprint, spin 0.
- Giving a vessel an internal fire: declare `BEBehaviorFirebox` on the leaf blocktype with the bed's
  `layers` / `unitsPerLayer` / `bedElement` / `layerPrefix`. Everything downstream - the main hatch, the
  ignition gesture, the burn-down, the fuel texture - keys off `Bed != null`.
- Retargeting the phase change: nothing hardcodes steam. `BoiledMedium` asks the taxonomy what `"Water"`
  vaporises into and by what factor; the condenser does the mirror lookup.
- Changing the feed model: `:349-373` is the only water-in path from a network. The pending
  intake-gate-on-`InternalPressure` design ([pumps](pumps.md)) replaces exactly this block.

### Tests — `test/IronIndustryExpanded.Tests/Blocks/Boiler/`

| file | pins |
|---|---|
| `BoilerMathTests.cs` | `InternalPressure`, its clamped denominator, the saturation curve, `HeatProgress`, the danger zone's three cases |
| `BoilerTickTests.cs` | every FSM edge: ignition, both refusals, heat-up, boiling conversion, both grace shutdowns, and re-firing after a shutdown with leftover steam |
| `BoilerFireboxTests.cs` | the firing gesture, the burn-down at the charged fuel's own rate, and the port faces at all four orientations |
| `BoilerFuelTests.cs` | `FuelRateMultiplier` per fuel, including the lignite case and the empty/unlit fallback to 1 |
| `BoilerFuelTextureTests.cs` | fuel code → the charged item's own texture path |
| `BoilerSteamCycleTests.cs` | man-hatch venting (3 cases), condensation (3, incl. the anti-burst refusal), `BoilStep` via the taxonomy, shutdown reset, the ceiling cap |
| `BoilerBeTests.cs` | tree round-trip, each hatch's default and toggle |
| `BoilerManualWaterTests.cs` | fill/drain refusals |
| `BoilerDropTests.cs` | never self-drops |

Invariants: `Invariants/BoilerFootprintGuards.cs` (the drawn mesh against the declared footprint, all four
orientations, in both suites) and `Invariants/BoilerFuelTextureGuards.cs` (the fuel texture code against
the shipped shape). Rigs: `Fixtures/BoilerRig.cs`, `Fixtures/BoilerFakes.cs`, `Fixtures/SteamPlantScenes.cs`.

---

## Gotchas

1. `CondenseInternal`'s anti-burst guard is unreachable on both shipped boilers. It refuses to condense at
   `InternalPressure >= SteamExpansionFactor` = 16 atm (`BlockEntityBoiler.cs:752`), but `CapSteamToCeiling`
   clamps pressure to `MaxOutputPressure` every tick - 5 for the Cornish, 12 for the Lancashire
   (`SiexConfig.cs`). The guard is correct defensively but cannot fire today; it is not live protection.

2. The mesh spin and the footprint rotation are two separate numbers, and they have to carry the same
   offset. `StructureAngle` is `AngleFromSide + BodySpinOffset` and `BoilerShell` takes the shape spin as
   a per-leaf argument; the Cornish passes `BodySpinOffset` so the two agree, and the Lancashire passes 0
   because its art and its footprint are authored along opposite axes and its `StructureAngle` alone
   reconciles them ([Lancashire boiler](boiler-lancashire.md)). Nothing about the pairing is visible in
   either file on its own, which is why `BoilerFootprintGuards` measures the drawn shape against the
   declared footprint in both suites rather than leaving it to be read.

3. The `"Cornish"` name collides across two mods. `iiex:boilercornish` is the low-pressure entry boiler;
   `siex:enginecornish` is the high-pressure engine. They are unrelated machines from different tiers that a
   player will meet in the same sentence ("a Cornish boiler cannot drive a Cornish engine" - literally true,
   `BlockEntityBoilerCornish.cs:5-8`).

4. The pipe article is still on the retired two-tier model. `docs/iiex/handbook/05-steampower.html:20-22`
   says "an iron pipe bursts above 5 atm while a stronger steel pipe holds up to 10 atm"; the model is
   plated 2.5 / cast 5.0 / rolled 12 ([pipe network](../mechanics/pipe-network.md)). The boilers page
   itself is current.

5. An unpiped steam port cannot save an over-pressured boiler. The leak is 16 L/s while the boiler makes
   64 L/s. Only the man hatch (200 L/s, and it resets the burst grace) is a real relief (`:429-432`,
   `:665-673`).

6. Opening the man hatch on an `Idle` boiler empties the steam pocket completely, but on a running one it
   stops at 1 atm (`:728-738`). The floor is state-dependent, not a constant.

7. `_choked` is cleared by the snuff, not by the fix. After `BoilerChokeExtinguishSeconds` fires, the code
   sets `_choked = false` (`:346`), so the HUD's "Choked" line disappears at the moment the fire goes out,
   which reads as the problem resolving itself.

8. Evaporation is charged against the calendar, not the tick. `_lastEvapDays` is not serialised (`:155`,
   `:753-765`), so it re-seeds to "now" on load: an unloaded chunk is never charged, by design. It is also
   not charged for a loaded chunk between the last tick and an unload.

9. The exhaust cap and the choke test share one number. `TryProduceGas(..., maxOutputPressure: 0.8)`
   (`:460`) and `draughtBlocked = pressure >= 0.8` (`:330-332`) both read `ExhaustMaxOutputPressure`. A
   chimney on the exhaust run draws it back down; without one the boiler snuffs its own fire in 10 s.

10. The burst grace is serialised, the pipe network's is not. `_overpressure.ToTree` (`:1036`) survives a
    reload, so a boiler saved 29 s into its countdown bursts a second after the chunk loads. The pipe
    network's equivalent timer is transient ([pipe network](../mechanics/pipe-network.md) § 5).

11. `_waterContainerStacks` / `_emptyContainerStacks` are `static` (`BlockBoiler.cs:537`, `:553`) and
    resolved once from `world.Blocks` - shared across every boiler and every world in the process.

12. **Resolved.** The blast centre, the light sample and the steam port used to be the same cell by
    coincidence of the geometry, so moving the steam port moved the other two silently. They are three
    distinct cells now - `(0,1,-2)`, `(0,1,-3)`, `(0,2,-3)` - and deliberately so; the reason is written
    beside them (`BlockBoilerCornish.cs:80-92`). The Lancashire's three are separate as well
    ([Lancashire boiler](boiler-lancashire.md)). Nothing asserts the separation, so keep it in mind when
    re-deriving offsets for a third vessel.

13. Two burn rates read as one number. `BurnSecondsPerUnit` is the fuel's own vanilla `burnDuration`, which
    the bed spends whether the boiler is boiling or only heating - a bed lit into a vessel that never gets
    enough water still burns down on the clock.

---

## Open

- Feedwater does not fight back-pressure. The intake draws whatever the line offers regardless of
  `InternalPressure`, so a 1 atm manual pump feeds a 5 atm boiler. The settled fix - gate on
  `feedPressure ≥ InternalPressure` with a ramp, and turn pumps into pressure multipliers - is designed and
  unbuilt ([pumps](pumps.md)).
- `waterRendererBox` is derived from the shape's voxel extents, not measured in a running world. No test can
  catch a surface drawn slightly outside the shell; it wants an eye in a creative world.
- Nothing consumes the burst's aftermath. The blast takes the whole vessel with it - the masonry is in
  the shape - so what is left is a hole and 40 % of the materials, and rebuilding is a fresh placement.
- The Lancashire is still a coal-pile boiler on a footprint authored for the old art
  ([Lancashire boiler](boiler-lancashire.md)). The shared base carries both fire models for that reason;
  the second one goes away when that vessel is converted.
- No away-catch-up. A boiler left boiling in an unloaded chunk resumes exactly where it stopped: no water
  burnt, no steam made, no fuel consumed. The furnace core opts in (`MaxAwayCatchupSteps => 600`); the
  boiler does not.
