# Watt Engine
**Status** live   **Mod** iiex

**Owns**
- The shared engine model (`BlockEntityEngine`, base of every steam engine in the suite): the
  fixed-steam-draw / pressure-gates-on-off power law, `power = RunPower × demand × suppliedFraction`, the
  condensate output, the over-pressure wear → break → wrench-repair cycle, and the broken-mesh swap.
- The sub-machine contract: which cell, the 90°-clockwise facing snap in both placement directions,
  the `+180` body frame the cell is resolved in, `PowerDemand` as the engine's throttle, and the two-way
  animation phase-lock (`SyncAnimation` / `PhaseLockToEngine` / `DriveMpCycleFrame`).
- The Watt variant's stat table, 1 × 4 × 3 footprint, six RCC stages, grid recipe and repair bill.
- The MP-generator sub-machine: its constant-power torque curve, the `MpRatedLoad` / `MpPowerBudget` /
  stall arithmetic, and the fact that it is a vanilla-MP source, not an mpenergy node.
- The engine fluid pump's delivered rate - including the undocumented `× 3` - and its output pressure.

**Does not own** - cited only, never restated:
- The steam pool, pressure, one-medium rule, burst-by-tier, the pressure valve, leaks, the network tick -
  [pipe network](../mechanics/pipe-network.md).
- The `"mpenergy"` network, `E = ½Iω²`, the flywheel, its hub bridge, `BEBehaviorMPFillerPort`, transmissions
  and every `Mp*` / `Flywheel*` key in iiex's config - [mp-energy](../mechanics/mp-energy.md).
- Fillers, footprints, the layout DSL, per-cell collision - [multiblock](../mechanics/multiblock.md).
- Code-first defs, the RCC builder, `brokenDropsRatio` resolution, the cost catalogue -
  [recipes-config](../mechanics/recipes-config.md).
- The boiler that feeds it, its choke ceiling and why the pressure valve between them is mandatory -
  [Cornish boiler](boiler-cornish.md).
- The air-blower sub-machine and its rate - smex's block (no design page yet).
- The Cornish engine (three-band control rods, its own numbers) - [Cornish engine](engine-cornish.md).

---

## Role

The Watt engine turns inlet steam pressure into a power number. Everything downstream is decided by the
single sub-machine bolted to its drive cell; the engine itself neither pumps, blows nor turns an axle.

Declared liberty *(2026-08-07)*: feeding a Watt-era condensing engine from the c. 1812
[Cornish boiler](boiler-cornish.md) is a deliberate chronological compression - historically such engines
ran on wagon and haystack boilers, though mid-19th-century practice did retrofit Cornish boilers onto
low-pressure beam engines.

Its place in the progression is not "more power". At iron tier a vanilla waterwheel or windmill is bridged
into mpenergy through the flywheel's hub ([mp-energy](../mechanics/mp-energy.md) § the vanilla-MP bridge);
in iiex the player replaces the vanilla producer with a steam engine + MP generator, and the flywheel,
shafts, transmissions and every consumer are untouched. Steam's advantage is siting and reliability: water
needs a river, wind needs weather, steam runs anywhere any time.

---

## Structure

A megablock, but not a multiblock: it reserves a filler volume, and it has no `MultiblockLayout` and
no `MultiblockStructure` behaviour - there is nothing to verify, so it runs as soon as construction
finishes.

| | |
|---|---|
| Class | `BlockEngineWatt : BlockEngine : BlockFilledMegastructure` (`BlockEngineWatt.cs:14`, `BlockEngine.cs:25-29`) |
| Block entity | `BlockEntityEngineWatt : BlockEntityEngine : BlockEntityProductionMachine` (`BlockEntityEngineWatt.cs:15`, `BlockEntityEngine.cs:27`) |
| Footprint | 1 × 4 × 3 (X = 0, Y 0..3, Z 0..2) = 12 cells, 10 fillers |
| Layout | `Origin(0, 3)`, `Slice(0, …)` - a front elevation, rows run −Y from the top (`BlockEngineWatt.cs:36-49`) |
| Principal | `(0,0,0)`, the `'O'` glyph |
| Left empty | `(0,0,2)` - the sub-machine cell (the `'.'`) |
| Orientation | `side` variant; `Angle` = raw side angle, `BodyAngle` = `Angle + 180` (`BlockEngine.cs:38-43`) |
| `StructureAngle` | `BodyAngle` (`BlockEngine.cs:46`) |
| Shape spin | `ShapeSpunPerOrientation(base, 180)` (`BlockEngine.cs:231`) ⇒ `rotateYByType` `*-north: 180`, so rotateY and `StructureAngle` agree (unlike the boiler) |
| Collision / selection | full cube on the principal only (`BlockEngine.cs:233-234`) |
| Gear housing | `(0,3,1)` - a filler cell; only the gear-hum emitter (`BlockEngine.cs:79`, `BlockEntityEngine.cs:735-748`) |
| Cylinder vent | `(0.5, 1.5, 0.5)` in the master-cell frame, horizontal part rotated by `BodyAngle` (`BlockEngine.cs:155`, `:162-169`) |

```
Slice x = 0        z→ 0   1   2
             y=3   #   #   #      <- gear housing at (0,3,1)
             y=2   #   #   #
             y=1   #   #   #
             y=0   O   #   .      <- principal, filler, SUB-MACHINE CELL
```

### The two pipe ports — `BlockEngine.cs:31-66`

Authored in the north frame and rotated by `Angle` (not `BodyAngle`):

| Port | North-frame face | Direction | Read by |
|---|---|---|---|
| Steam inlet | SOUTH | pipe → engine | `SteamInletFace`; `ConnectedNetwork<PipeNetwork>(SteamInletFace)` (`BlockEntityEngine.cs:254`) |
| Condensate out | EAST | engine → pipe | `WaterOutletFace`; `ConnectedNetwork<PipeNetwork>` (`:314`) |

Both are on the principal cell. `HasConnectorAt` rotates each base face and compares (`:50-58`), so a
`-north` engine takes steam on its south face and drains east.

### The sub-machine cell — the single most orientation-sensitive thing on the machine

`SubmachinePos(enginePos)` = `enginePos + RotateOffset(submachineOffset (0,0,2), BodyAngle)`
(`BlockEngine.cs:85-91`). For a `-north` engine that is `(0,0,-2)` - two cells north, on the far side of
the filler at `(0,0,1)`. This one method is the single source of truth for the cell; everything else
inverts it.

The facing snap runs in both directions, and both go through one rule (`BlockEngine.cs:97-105`):

```
SubmachineSide:  north → east,  east → south,  south → west,  west → north     // compass clockwise
```

| Order built | Path |
|---|---|
| Engine placed onto an existing sub-machine | `OnFootprintPlaced` → `ReorientSubmachine` → `ExchangeBlock` to the wanted `side` variant, keeping the BE alive (`BlockEngine.cs:178-206`) |
| Sub-machine placed at an existing engine's cell | `BlockEngineSubmachine.TryPlaceBlock` finds the engine via `TryFindEngineFor` and places the oriented variant instead of the player's look (`BlockEngineSubmachine.cs:14-51`) |

`ExchangeBlock` keeps the block entity, so `OnExchanged` is the re-bind hook: the base re-resolves the
engine and rebuilds the animator (`BlockEntityEngineSubmachine.cs:285-294`), and the MP generator
additionally re-seeds its mechanical axis (`BlockEntityEngineMPGenerator.cs:108-112` →
`BEBehaviorEngineMPGenerator.OnOrientationChanged`, `:37-47`) - without that the axle would keep the old
axis after the snap.

`TryFindEngineFor` (`BlockEngine.cs:112-139`) tests the four horizontal candidates two cells out and
confirms each engine's own `SubmachinePos` points back, so it is orientation- and offset-agnostic. The
sub-machine's own back-reference does not do that - see [Gotchas](#gotchas) 1.

---

## Assets

| Asset | Path | State |
|---|---|---|
| Editable shape (engine) | — | missing. No `assets/editable/shapes/` source for the Watt engine |
| Runtime shape (engine) | `assets/iiex/shapes/engine/watt.json` | root children `Cylinder` · `BeamSupport` · `Beam` · `Piston` · `ControlPiston` · `Rod` - exactly the six RCC stages |
| Engine animations | same file | `cyclepump` (60 f, `Repeat`) · `cyclemp` (60 f, `Repeat`) · `idlepump` (30 f) · `idlemp` (30 f) |
| Engine textures | `fire1`, `iron3`, `iron5`, `iron` | |
| MP generator shape | `assets/iiex/shapes/engine/mpgenerator.json` | root `Cube2` + `Axle`; `idle` (30 f) · `cycle` (60 f) |
| Fluid pump shape | `assets/iiex/shapes/engine/fluidpump.json` | root cubes + `Piston`; `idle` (30 f) · `cycle` (60 f) |
| Fluid pump editable | `assets/editable/shapes/machine-pipe-megablock-mppump.json` | present (untracked in git) |
| Handbook | `assets/iiex/config/handbook/02-engines.json` ↔ `docs/iiex/handbook/02-engines.html` | present, wrong by 3× on both sub-machine rates - see [Gotchas](#gotchas) |

All running clips use `onAnimationEnd: Repeat`, which they must: a `Hold` cycle would freeze and the
RCC-suppressed mesh would vanish.

The MP generator draws no cycle of its own. Vanilla's MP renderer spins only the `Axle*` elements
(`BEBehaviorMPSubmachineBase.GetShape`, `:47-55`) and the static remainder is tesselated separately, so the
generator's own `cycle` clip is unused - instead the generator drives the engine's `cyclemp` frame. See
[Operation](#operation).

---

## Construction

### 1. The frame block — grid recipe (`MachineRecipeDefinitions.cs:71-81`)

```
_ H _          P = metalplate-* (iron/steel) ×1 each  → 4
P R P          R = rod-* ×2                            → 2
P I P          I = iiex:pipe-plated-straight-* ×1      → 1
               H = hammer (tool)
→ iiex:enginewatt-north
```

The builder also declares `Ingredient("G", Gear(gear, 2))` (`:78`) which never appears in the
pattern, and the whole recipe is emitted twice, once per gear code (`:29-33`), producing two
byte-identical recipes that differ only in a dead ingredient. See [Gotchas](#gotchas) 9.

The three genuinely gear-driven machines (fluid pump, manual pump, MP generator) do use `G`, and each
accepts `game:gear-rusty` or `iiex:gear-*`.

### 2. The RCC stages — `BlockEngineWatt.cs:50-79`

| # | Adds | Plates | Rods | Nails | `burnedbrick-fire` |
|---|---|---|---|---|---|
| 1 | `Root/Cylinder` | — | — | — | — |
| 2 | `Root/BeamSupport` | 2 | 4 | 4 | 36 |
| 3 | `Root/Beam` | — | 8 | 4 | — |
| 4 | `Root/Piston` | 2 | 4 | 2 | — |
| 5 | `Root/ControlPiston` | — | 4 | 2 | — |
| 6 | `Root/Rod` | — | 4 | — | — |
| | **total** | **4** | **24** | **12** | **36** |

Every metal requirement accepts iron or steel with `storeWildCard: "metal"`. The handbook's engine figures
match these exactly (unlike the boiler's).

### Sub-machine recipes

| Block | Pattern | Cost | file:line |
|---|---|---|---|
| MP generator | `_H_,GAG,PRP` | 2 plates · 2 rods · 4 gears · 1 `game:woodenaxle-ud` | `MachineRecipeDefinitions.cs:108-118` |
| Fluid pump | `_HG,PIP,RIR` | 2 plates · 4 rods · 1 gear · 2 `iiex:pipe-plated-straight-*` | `:83-93` |

Cost catalogue entries: `enginewatt-grid`, `enginewatt-rcc`, `enginempgenerator-grid`,
`enginefluidpump-grid` (`IiexRecipeConfig.cs:65`, `:69`, `:70`, `:71`).

---

## Operation

```
   steam run  ──►  SOUTH face (north frame)
        │  pressure gates ON/OFF only; the draw is FIXED
        ▼
   ┌──────────────────────────────────────────────┐
   │  engaged  = pressure >= 2 atm  AND  demand>0 │
   │  want     = 30 × demand × dt                 │
   │  used     = inlet.TryConsumeGas(want)        │
   │  frac     = used / want                      │
   │  POWER    = 0.3 × demand × frac              │
   │  water    = 1 × demand × frac × dt  ──► EAST face @ 90 °C, 0 atm (or spills)
   └───────────────────────┬──────────────────────┘
                           │ AvailablePower
                           ▼
                 sub-machine at (0,0,2)
                   ├─ MP generator  → vanilla MP torque
                   ├─ fluid pump    → water @ 0.75 × inlet pressure
                   └─ air blower    → air  (smex)
```

### The tick — `OnProductionTick`, `BlockEntityEngine.cs:235-306`

Every 1000 ms server-side, gated on `CanRunProduction => IsConstructed` (`:195`). A broken engine
still ticks, so it can hold its power at zero and render the break.

| # | Step | Line |
|---|---|---|
| 1 | broken ⇒ zero power, stop, return | `:243-252` |
| 2 | read the inlet; pressure counts only if `MediumType == "Steam"` | `:254-256` |
| 3 | over-pressure grace: `pressure > 4 atm` for 60 s ⇒ `Break()` | `:261-275` |
| 4 | `demand = SubmachineBE?.PowerDemand ?? 0`; `engaged = pressure >= 2 && demand > 0` | `:279-280` |
| 5 | consume steam, derive `frac`, derive power, emit condensate | `:283-294` |
| 6 | `AnimationSpeed = run ? 0.5 + power : 1`; sync to clients on a real change | `:296-305` |

Inlet pressure is a gate, not a throttle: a 2 atm line and a 3.9 atm line make identical power. An engine
with no sub-machine draws no steam and makes no power at all, because `demand` is 0 (`:279-280`,
`BlockEntityEngineSubmachine.cs:59`) - the engine is a load-driven machine, not a generator with a load
attached.

A starved line yields proportionally less power: a line that can only supply half the 30 L/s produces
0.15 power.

### Condensate — `OutputCondensate`, `:312-318`

`TryProduceLiquid(amount, 90 °C, pressure 0)` into the east network. Only a pump pressurises water, so
the engine's drain is gravity-fed. With no network (or a full one) it calls `SpawnWaterSpill` - a water jet
particle out the outlet face plus a splash (`:320-328`). The volume is a fixed per-engine rate, not the
steam it consumed, so the water-loop numbers stay clean (`:288-290`).

### Break and repair

| | |
|---|---|
| Arms at | `InletPressure > BreakPressure` (4 atm) |
| Fires after | `EngineOverPressureSeconds` = 60 s, serialised (`:378`) so it survives a reload |
| Recovers | any tick back inside the band resets the `GraceTimer` (`ExpandedLib/Helpers/GraceTimer.cs:29-33`) |
| Break effects | 120 steam particles at the block, 80 smoke at the cylinder vent, `MediumExplosion` at 0.5 volume / 24 m (`:118-134`) |
| While broken | `AvailablePower = 0`, `_running = false`, the grace timer is reset so the warning plume stops, and the piston subtree is hidden |
| Repair | RMB with a wrench + `4× iron/steel plate` and `2× iron/steel rod` (`BlockEngineWatt.cs:81-85`); creative repairs free (`BlockEngine.cs:399-401`) |

The broken mesh is a separate animator cache key (`AnimCacheKey + "-broken"`, `:447-448`) so it cannot
collide with the intact one. `GetBrokenSelectiveElements` (`:468-489`) walks the shape and emits one
`"<path>/*"` per maximal clean subtree, recursing only into ancestors of a hidden element - the per-segment
`SelectiveElements` matching rule. Hidden subtrees: `Cube21` and `Piston` (`:460`).

A broken engine refuses all other interaction until fixed (`BlockEngine.cs:365-374`), except that a held
placeable block still falls through to vanilla placement so the player can keep plumbing (`:360-362`).

### HUD — `GetBlockInfo`, `:414-442` + `BlockEntityEngineWatt.cs:25-42`

`ClockState` (`:180-184`) maps the inlet against the band to a lang-key fragment:

| state | condition | lang key |
|---|---|---|
| `over` | `> 4 atm` | `iiex:engine-info-clock-over` - "Over-pressured!" |
| `nominal` | `≥ 2 atm` | "Nominal" |
| `under` | `> 0.01 atm` | "Not enough pressure" |
| `idle` | otherwise | "Idle" |

Plus the steam draw while running, the over-pressure countdown, and (Watt only) the operating band through
`ExMeasure.PressureRange` so it converts with the player's unit preference rather than reading a hardcoded
"2-4 atm".

---

## The sub-machines

`BlockEntityEngineSubmachine` (`:23`) owns everything generic: engine discovery, the per-second `DoWork`,
and the `idle`/`cycle` animator. A new sub-machine needs only an `Animatable` behaviour with those two clips
and a `DoWork` override.

| | |
|---|---|
| Gate | `CanRunProduction => Engine != null` (`:160`) |
| Work | `DoWork(Engine.AvailablePower, dt)` (`:162-163`) |
| Throttle | `PowerDemand` - default `1` when an engine is found (`:59`); the engine multiplies both its steam draw and its power by it |
| Engine lookup | lazy and retried: BEs initialise in arbitrary chunk-load order, so a one-shot `Initialize` lookup misses (`:41-53`) |

### MP generator — `iiex:enginempgenerator`

This is a vanilla-MP torque source. `BEBehaviorEngineMPGenerator` derives from
`BEBehaviorMPSubmachineBase : BEBehaviorMPBase` - it is a participant on the vanilla mechanical graph, not
an `"mpenergy"` node. It couples on both ends of its axis: the base seeds only
`OutFacingForNetworkDiscovery`, so `Initialize` wires the opposite face too (`:22-30`).

```csharp
GetTorque(tick, speed, out resistance):                     // BEBehaviorEngineMPGenerator.cs:51-73
    budget = engine.MpPowerBudget                            // 0 ⇒ no torque
    torque = budget / max(speed, 0.25 × ratedSpeed)          // constant-power source
    if speed >= 1.5 × rated: return 0                        // soft top-speed cap
    if speed >  1.0 × rated: torque × (1.5·r − speed)/(0.5·r) // linear taper
```

Constant power means the network settles at `speed = budget / load`: rated speed at rated load, slower
under more. The taper (rather than a hard cap) keeps an unloaded line from sawtoothing.

```
MpRatedLoad   = MaxPower × MpLoadPerEnginePower = 0.3 × 0.875 = 0.2625   // :161
MpPowerBudget = AvailablePower × 0.875 × MpRatedSpeed(1.0)   = 0.2625    // :168-169, at full power
IsMpOverstressed(load) ⇔ load > 2 × 0.2625 = 0.525                       // :173
```

`MpRatedLoad` uses `MaxPower`, not current output, or the stall check would latch off once the engine
stopped and could never recover (`:157-160`). The generator's own `PowerDemand` returns 0 while
overstressed, judged by the network's resistance rather than its live speed, so shedding machines un-stalls
it (`BlockEntityEngineMPGenerator.cs:36-45`).

`ResolveDiscoveryFace` seeds from the back of the axis (south/west) because vanilla's
`IsRotationReversed` keys off the discovery direction, and the far end must reverse the shaft's rendered
spin to match the engine's beam linkage (`:75-84`). `AxisSign` is set per axis, not per facing, or
opposite facings on one axle line counter-rotate (`BEBehaviorMPSubmachineBase.cs:37-40`).

### Fluid pump — `iiex:enginefluidpump`

Connectors on DOWN (source) and left (delivery), where "left" is `WEST` rotated by the
sub-machine's own side angle (`BlockEngineFluidPump.cs:39-46`) - for a north-facing engine, `NORTH`.

```csharp
pressure = Engine.InletPressure × SteamEngineEfficiency (0.75)   // BlockEntityEngineFluidPump.cs:46-47
amount   = PumpWaterPerSecond × 3 × power × dt                   // :48   the ×3
move     = min(amount, FluidPumpCore.OutputFreeCapacity(left))
drawn    = bottom.TryConsumeLiquid(move)
left.TryProduceLiquid(drawn, 20 °C, pressure)
intake.ProduceWater(amount, 20 °C)                               // :55   refill AFTER the transfer
```

The pump is not the source - the fluid intake is the generator; the pump is a transfer that then tells
the intake to refill. Transfer-then-refill is load-bearing: doing it the other way round mislabels the pool
as "Air".

### Air blower — smex

`smex:engineairblower`. Same base, same contract; its rate is smex's fact (`SmexConfig.cs:113`). It
carries the identical `× 3` factor (`BlockEntityEngineAirBlower.cs:56`), so the discrepancy below is not
a one-off typo.

### Animation phase-lock — two directions, two mechanisms

Engine → sub-machine (pump, blower). `ApplyPose` starts the engine's clip and, from the same call,
`SubmachineBE?.SyncAnimation(run, speed)` (`BlockEntityEngine.cs:638`) so the two begin in the same client
frame. The sub-machine's `ApplyAnim` then calls `PhaseLockToEngine`, which snaps
`cycle.CurrentFrame = engine.CycleAnimProgress × frames`
(`BlockEntityEngineSubmachine.cs:266-278`, `BlockEntityEngine.cs:646-653`). A 500 ms client poll
(`:166-179`) is the backstop for a sub-machine that initialised after the engine.

Both sides of that product span the **whole** frame count, never the last keyframe's number. The
animator's live frame space is `[0, QuantityFrames)`, so the stretch from the last keyframe back to
the first is an ordinary interpolation segment it renders like any other — one frame wide, carrying
the last `360/QuantityFrames` degrees. Spanning `frames − 1` maps a revolution onto everything but
that segment, which the clip's own advance then walks into out of step with the axle. The clips
driven this way are therefore authored as vanilla authors them (`gencore.json`, `genmixer.json`):
last keyframe at `360 × (frames − 1) / frames` with `rotShortestDistance` set, never a duplicate of
frame 0. `LoopingAnimationTests` enforces that on every shipped shape.

Sub-machine → engine (MP generator only). Inverted, because the generator's motion is the vanilla
axle. Every render frame it pushes the axle's absolute angle:

```csharp
engine.DriveMpCycleFrame(turning, _mp.AngleRad);                 // BlockEntityEngineMPGenerator.cs:69-80
    → cyclemp.CurrentFrame = MPAnim.FrameFromAngle(angleRad, frames)   // BlockEntityEngine.cs:603-606
```

Locking to the absolute angle (not an accumulated delta) is what makes the beam's phase deterministic:
the connecting rod's big-end is pinned to the crank, both rest at frame 0 / angle 0, and the beam tracks
from there with zero offset (`:599-602`). It also means the beam keeps cycling while the flywheel coasts
after the steam is cut, which is why `CyclePose()` reports `_mpTurning` rather than `_running` for the MP
variant (`:561-566`), and why the steam puff is gated on `_running` separately (`:707-714`).

Which pose plays is decided by polling the block code at the sub-machine cell for `"mpgenerator"`
(`IsMPGenerator`, `:533-540`), re-checked every 500 ms because the cell is two blocks away and never fires
`OnNeighbourBlockChange` (`:337-344`).

### Sound

| Sound | Where | Trigger |
|---|---|---|
| Piston strokes | engine + sub-machine, client-side | `cycle` frame crossing 45 (up, `TorchUnequip`) or 15 (down, `AnvilMergeHit`) - `PistonCycleSounds.cs:16`, `:19`, `:27-54` |
| Gear hum | gear-housing cell `(0,3,1)` | while running; `PlanetaryGears` at volume 0.5, range 16, pitch 0.65 (`BlockEntityEngine.cs:735-748`) |
| Axle grind | generator cell | while the vanilla network turns; `MetalGrinding` 0.3 / 16 / 0.85 (`BlockEntityEngineMPGenerator.cs:83-102`) |
| Water trickle | pump cell | while `_drawingWater` (synced) - `Watering` loop (`BlockEntityEngineFluidPump.cs:71-90`) |

Volume and pitch are applied every running tick through `SoundVolumeFactor` / `SoundPitchFactor` so a
throttle change is audible without restarting the loop (`:746-748`). The Watt returns 1 for both; the
Cornish engine overrides them.

---

## Numbers

### iiex config — `IiexConfig.cs`, `ModConfig/ex_values.json`, section `iiex`

| key | value | file:line | what it does |
|---|---|---|---|
| `WattEngineEngagePressure` | `2.0 atm` | `IiexConfig.cs:163` | inlet at/above which it runs - a gate, not a throttle |
| `WattEngineBreakPressure` | `4.0 atm` | `:166` | above this it wears toward a burst |
| `WattEngineMaxPower` | `0.3` | `:169` | both `MaxPower` and `RunPower` - the Watt has no throttle |
| `WattEngineSteamRate` | `30 L/s` | `:172` | fixed draw while engaged |
| `WattEngineWaterRate` | `1 L/s` | `:175` | condensate out the east face |
| `SteamEngineEfficiency` | `0.75` | `:183` | sub-machine output pressure = inlet × this |
| `EngineOverPressureSeconds` | `60 s` | `:186` | wear grace before the break |
| `MpRatedSpeed` | `1.0` | `:190` | vanilla-MP speed held within rated load |
| `MpLoadPerEnginePower` | `0.875` | `:195` | MP load held per unit of engine power |
| `PumpWaterPerSecond` | `16.67 L/s` | `:199` | pump base rate per unit power - then tripled in code |
| `ManualPumpWaterPerSecond` | `2 L/s` | `:203` | the hand-cranked alternative |

### Derived — the arithmetic that the config comments and the handbook get wrong

| quantity | formula | value |
|---|---|---|
| `MpRatedLoad` | `0.3 × 0.875` | `0.2625` |
| `MpPowerBudget` at full power | `0.3 × 0.875 × 1.0` | `0.2625` |
| Stall threshold | `2 × MpRatedLoad` | `0.525` |
| Pump delivery, Watt at full power | `16.67 × 3 × 0.3` | `15.0 L/s` |
| Pump output pressure at 2 atm inlet | `2 × 0.75` | `1.5 atm` |
| Pump output pressure at 4 atm inlet | `4 × 0.75` | `3.0 atm` |
| Running animation speed | `0.5 + 0.3` | `0.8` |

> Two shipped comments are wrong. `IiexConfig.cs:193-194` says "A Watt at full power (0.3) × this = ~0.5 =
> four helve hammers". The product is 0.2625, not 0.5. The handbook says the same figure is "enough to
> run two helve hammers". Both are guesses at a number neither of them computes; the value in the game
> is 0.2625, and how many helve hammers that is depends on vanilla's per-machine resistance, which nothing
> here measures.
>
> `IiexConfig.cs:197-198` says "Water (L/s) the engine fluid pump moves per unit of mechanical power
> (Watt 0.3 → 5 L/s)". `BlockEntityEngineFluidPump.cs:48` multiplies by a bare literal `3`, so the
> shipped figure is 15 L/s. The doc comment describes the config key correctly and the machine
> incorrectly. The handbook repeats the 5 L/s. Both sub-machines carry the same undocumented `× 3`
> (`BlockEntityEngineAirBlower.cs:56`), so the handbook's blower figure is 3× low too.

### Cited, owned elsewhere

| quantity | value | owner | file:line |
|---|---|---|---|
| Cornish boiler choke ceiling | `5.0 atm` | [Cornish boiler](boiler-cornish.md) | `IiexConfig.cs:156` |
| Cornish boiler steam output | `32 L/s` | [Cornish boiler](boiler-cornish.md) | `:152` |
| cast (iiex) pipe burst | `5.0 atm` | [pipe network](../mechanics/pipe-network.md) | `:50` |
| pressure-valve gate step / ceiling | `0.25 atm` / block burst rating | [pipe network](../mechanics/pipe-network.md) | `BlockEntityPressureValve.cs:31`, `:41-42` |
| `MpMaxSpeed`, `MpFrictionCoeff`, flywheel inertia, the bridge torque | iiex's | [mp-energy](../mechanics/mp-energy.md) | `ExlibConfig.cs:86-98`, `IiexConfig.cs:451-477` |
| air-blower output rate | smex's | smex | `SmexConfig.cs:113` |

```
2.0  ────────────────  4.0  |  5.0
engage        band     break | boiler chokes
```

One Cornish boiler makes 32 L/s and one Watt drinks 30 L/s: one boiler runs exactly one engine,
with 2 L/s of headroom. A second engine on the same run starves both (each gets `frac ≈ 0.53`, so ~0.16
power each). Nothing in the code says this; it is the ratio of two config values.

### Hard-coded — not config

| value | file:line | what it does |
|---|---|---|
| production tick `1000 ms` | `BlockEntityProductionMachine.cs:56` | engine and sub-machine beat |
| `dt` clamp `2 ×` tick | `BEBehaviorProductionMachine.cs:77`, `:146` | |
| sub-machine type poll `500 ms` | `BlockEntityEngine.cs:225` | mp ⇄ pump pose switch |
| engine client tick `50 ms` | `BlockEntityEngine.cs:228` | stroke sounds + over-pressure plume |
| sub-machine anim mirror `500 ms` / keyframe watch `50 ms` | `BlockEntityEngineSubmachine.cs:76-77` | |
| run threshold `power > 0.001` | `BlockEntityEngine.cs:297` | |
| animation speed `0.5 + power`, idle `1.0` | `:299` | |
| speed-change epsilon `0.05` | `:300`, `:408`, `BlockEntityEngineSubmachine.cs:171` | sync + re-pose threshold |
| broken hidden elements `["Cube21", "Piston"]` | `:460` | the burst-off subtree |
| condensate `90 °C`, pressure `0` | `:315` | |
| cylinder puff count `2` (Watt) | `:75` | 0 suppresses it |
| over-pressure plume `200 ms`, `3` particles | `:724-728` | |
| burst FX `120` steam / `80` smoke / vol `0.5` / range `24 m` | `:118-134` | |
| gear hum `0.5` vol, `16 m`, pitch `0.65` | `:735-748` | |
| `PistonCycleSounds.UpFrame = 45`, `DownFrame = 15` | `PistonCycleSounds.cs:16`, `:19` | on a 60-frame cycle |
| `DefaultSubmachineOffset (0,0,2)` | `BlockEngine.cs:76` | and duplicated at `BlockEntityEngineSubmachine.cs:140` |
| `DefaultGearHousingOffset (0,3,1)` | `BlockEngine.cs:79` | |
| `DefaultCylinderVent (0.5,1.5,0.5)` | `BlockEngine.cs:155` | no engine declares `cylinderVentOffset` (`:172-173`) |
| `SubmachineSide` compass map | `BlockEngine.cs:97-105` | the 90°-CW snap |
| `MpRatedLoad` divisor clamp `0.25 × rated`, taper end `1.5 × rated` | `BEBehaviorEngineMPGenerator.cs:63`, `:67` | |
| generator `GetResistance` `0.0005` | `BEBehaviorEngineMPGenerator.cs:49` | its own vanilla-MP drag |
| `AxisSign` per-axis `−1` | `BEBehaviorMPSubmachineBase.cs:37-40` | or opposite facings counter-rotate |
| pump `× 3` | `BlockEntityEngineFluidPump.cs:48` | the undocumented factor |
| pump water temperature `20 °C` | `:53`, `:55` | |
| generator render order `0.0`, range `64` | `BlockEntityEngineMPGenerator.cs:29-30` | must precede the opaque pass |
| turning epsilon `0.001` | `BlockEntityEngineMPGenerator.cs:73` | |
| resistance `45`, mining tier `3` | `BlockEngine.cs:222-223` | above the boiler's blast threshold (20) on purpose |
| repair bill `4 plate + 2 rod` | `BlockEngineWatt.cs:81-85` | iron or steel for the Watt |

---

## Drops

The engine keeps its self-drop, unlike the boiler. `EngineShell` sets neither `NoDrops()` nor a
`GetDrops` override (`BlockEngine.cs:218-235`; golden has no `drops` key), and `BlockBoiler.cs:147-152`
calls this out explicitly: the engines have a craftable frame, so breaking one returns the frame plus
the RCC salvage.

| Path | Returns |
|---|---|
| Mined | 1 × `iiex:enginewatt-<side>` + 80 % of the construction materials (`RccBrokenDropsRatio`, `IiexConfig.cs:116`, registered at `LowPressureExpandedModSystem.cs:28-31`) |
| Fillers | removed by `BlockFilledMegastructure`, never dropped |
| Broken (burst) engine | still drops normally - the break costs nothing on break; the loss is the repair bill |
| Sub-machine | ordinary block drop; independent of the engine |

An engine burst destroys nothing and only disables the machine, so there is no engine equivalent of the
boiler's explosion salvage path.

---

## Code

| Piece | file:line |
|---|---|
| `BlockEngine : BlockFilledMegastructure, INetworkConnector, IFillerInteractionTarget` | `BlockStructures/Engine/BlockEngine.cs:25` |
| `Angle` / `BodyAngle` / `StructureAngle` | `:38`, `:43`, `:46` |
| `SteamInletFace` / `WaterOutletFace` / `HasConnectorAt` | `:61`, `:65`, `:50-58` |
| `SubmachinePos` - the single source of truth for the drive cell | `:85-91` |
| `SubmachineSide` - the one snap rule | `:97-105` |
| `TryFindEngineFor` - the verified inverse | `:112-139` |
| `GearHousingPos` / `CylinderVentPos` | `:145-151` / `:162-169` |
| `OnFootprintPlaced` → `ReorientSubmachine` | `:178-206` |
| `EngineShell` (the shared def surface) | `:218-235` |
| `TryRepair` | `:383-430` |
| `BlockEngineWatt` - footprint, six stages, repair bill | `Engine/Blocks/BlockEngineWatt.cs:14` |
| `BlockEngineSubmachine.TryPlaceBlock` - the reverse snap | `Engine/BlockEngineSubmachine.cs:14-51` |
| `BlockEntityEngine : BlockEntityProductionMachine` | `Engine/BlockEntityEngine.cs:27` |
| `OnProductionTick` | `:235-306` |
| `OutputCondensate` / `SpawnWaterSpill` | `:312-318` / `:320-328` |
| `MpRatedLoad` / `MpPowerBudget` / `IsMpOverstressed` | `:161` / `:168-169` / `:173` |
| `Break` / `Repair` / `EngineCacheKey` / `GetBrokenSelectiveElements` | `:109-137` / `:99-106` / `:447` / `:468-489` |
| `DriveMpCycleFrame` (absolute-angle lock) | `:577-607` |
| `ApplyPose` (+ the `SyncAnimation` handoff) | `:609-640` |
| `CycleAnimProgress` / `ReadCycleFrame` | `:646-653` / `:656-668` |
| `OnEngineClientTick` | `:674-730` |
| `BlockEntityEngineWatt` - the stat table + band line | `Engine/BlockEntities/BlockEntityEngineWatt.cs:15` |
| `BlockEntityEngineSubmachine` | `Engine/BlockEntityEngineSubmachine.cs:23` |
| `Engine` (lazy retry) / `PowerDemand` / `FindEngine` | `:41-53` / `:59` / `:131-154` |
| `SyncAnimation` / `PhaseLockToEngine` / `OnExchanged` | `:253-260` / `:266-278` / `:285-294` |
| `BlockEntityEngineMPGenerator : …, IRenderer` | `Engine/BlockEntities/BlockEntityEngineMPGenerator.cs:19` |
| `BEBehaviorEngineMPGenerator.GetTorque` | `Engine/BlockEntities/BEBehaviorEngineMPGenerator.cs:51-73` |
| `BlockEntityEngineFluidPump.DoWork` | `Engine/BlockEntities/BlockEntityEngineFluidPump.cs:29-56` |
| `PistonCycleSounds` | `Engine/PistonCycleSounds.cs:13` |
| `BEBehaviorMPSubmachineBase` (static body + spun axle split) | `ExpandedLib/Blocks/Machines/BEBehaviorMPSubmachineBase.cs:19` |
| `MPAnim.FrameFromAngle` | `ExpandedLib/Helpers/MPAnim.cs:49-57` |

### Where a caller hooks in

- A new engine variant: implement the six abstract stats (`BlockEntityEngine.cs:57-72`), optionally
  `CylinderSteamPuffCount` / `SoundVolumeFactor` / `SoundPitchFactor`, and call `EngineShell`. hpex's Cornish
  engine is the worked example (three-band control rods on top of the same base).
- A new sub-machine: derive `BlockEntityEngineSubmachine`, override `DoWork(power, dt)`, give the block
  an `Animatable` behaviour with `idle` + `cycle`. Override `PowerDemand` to throttle the engine (the
  generator returns 0 while overstressed). Override `OnCycleStroke` for per-stroke effects.
- Driving mpenergy from steam: do not wire this generator into `"mpenergy"`. Put the vanilla axle it
  produces onto the flywheel's hub port and let the existing bridge convert it
  ([mp-energy](../mechanics/mp-energy.md) § the vanilla-MP bridge) - the
  "swap the waterwheel for an engine" progression.

### Tests — `test/IronIndustryExpanded.Tests/Blocks/Engine/`

| file | pins |
|---|---|
| `EngineTickTests.cs` | idle below engage · engages and makes power in band · bursts after sustained over-pressure then stops · a broken engine stays inert · repair resets the timer so one tick cannot re-break · tree round-trip |
| `EngineRecursionRegressionTests.cs` | a constructed engine's tick must not stack-overflow - the fixed bug where `this.NetworkAt(...)` bound to the instance method instead of the extension and recursed forever |

`Fixtures/EngineFixture.cs` builds the minimum rig: a north-facing Watt, a sealed single-cell steam
inlet on the south face, and a fluid pump at the sub-machine cell - the engine will not engage
without a sub-machine demanding power.

---

## Gotchas

1. The sub-machine's own engine lookup is wrong, and only the fallback saves it.
   `BlockEntityEngineSubmachine.FindEngine` (`:131-145`) claims to invert `SubmachinePos` but rotates by
   `AngleFromSide(this sub-machine's side) + 180`. The sub-machine's side is the engine's rotated 90° CW
   (`SubmachineSide`), i.e. `engineAngle − 90`, so the lookup rotates by `engineAngle + 90` where
   `SubmachinePos` used `engineAngle + 180`. It is off by 90° in every orientation and never finds the
   engine. Every sub-machine in the game is bound by the fallback loop at `:146-153` - which, unlike
   `BlockEngine.TryFindEngineFor`, does not verify the engine points back, so two engines within two
   cells can cross-bind. Fix by calling `TryFindEngineFor`.

2. The sub-machine offset is duplicated as a literal. `Vec3i off = new(0, 0, 2)` at
   `BlockEntityEngineSubmachine.cs:140` is a hand-kept copy of `BlockEngine.DefaultSubmachineOffset`
   (`:76`), and the comment says it "is read from this block's own JSON so it stays in step" - it is not
   read from JSON at all; the sub-machine blocktypes declare no `submachineOffset`. Stale comment on top
   of a duplicated constant. An engine that ever moved its drive cell would break the inversion silently.

3. The name "flywheel" is overloaded across two mods. `iiex:enginempgenerator` is a pure vanilla-MP
   torque source with no inertia, no stored energy and no `"mpenergy"` membership, and is easy to
   mislabel a flywheel. iiex's `iiex:mpenergy-flywheel` is a
   real `IMpEnergyStorage` disc on the `"mpenergy"` network with `I = 10` (normal) / `150` (large) and the
   vanilla bridge in its hub ([mp-energy](../mechanics/mp-energy.md)). They are different blocks in
   different networks. Calling the iiex generator a flywheel implies it buffers, which is the
   property it does not have.

4. `IsMPGenerator` is a code-substring match. `path.Contains("mpgenerator")` (`:533-540`). Any block
   from any mod whose code path contains that substring, placed at the drive cell, flips the engine to the
   `idlemp`/`cyclemp` pose.

5. Inlet pressure is a gate, not a throttle - but the HUD reads like a throttle. `engine-info-clock-*`
   reports where the pressure sits in the band, which invites the player to tune toward 4 atm. 2.0 atm and
   3.9 atm produce identical power (`:279-296`); the only thing more pressure buys is a higher
   sub-machine output pressure (`× 0.75`) and a shorter distance to the break.

6. An engine with no sub-machine is completely inert and says nothing about it. `demand = 0` ⇒ never
   engaged ⇒ no steam drawn, no power, no animation - and the HUD reports `nominal` the whole time, because
   `ClockState` reads only the inlet (`:180-184`, `:279-280`).

7. The engine is a megablock with no structure to verify. No `MultiblockStructure` behaviour, no
   `MultiblockLayout` - so `CanRunProduction => IsConstructed` alone (`:195`). The
   ctrl+shift+RMB projection gesture therefore does nothing on an engine, unlike a boiler.

8. `Break()` resets the grace timer, and that is load-bearing (`:113-115`). The broken tick returns
   early at `:243-252` without touching the timer, so without the reset the client would vent the
   over-pressure warning plume forever on a dead engine.

9. The Watt grid recipe is emitted twice and neither copy uses a gear. `MachineRecipeDefinitions.cs:29-33`
   loops the gear code over four builders, but `WattEngine`'s pattern `"_H_,PRP,PIP"` has no `G`
   (`:75-81`). The result is two identical `3×3` recipes for the same output - a duplicate that the
   conflict rules for identical patterns with overlapping ingredients would normally flag.

10. Both sub-machine rates are `config × 3`. `BlockEntityEngineFluidPump.cs:48` and
    `BlockEntityEngineAirBlower.cs:56`. The factor lives in code and neither config
    comment nor handbook mentions it, so every documented sub-machine rate in the suite is 3× low.

11. `cylinderVentOffset` is documented as an attribute and read from nowhere. `CylinderVentPos` says it
    reads "the optional `cylinderVentOffset` JSON attribute", and `ReadVentOffset` (`BlockEngine.cs:172-173`)
    returns the coded default unconditionally. No engine declares one, so it is currently honest - but the
    doc comment above it is not.

12. `ReorientSubmachine` uses `ExchangeBlock`, so `OnExchanged` is the only rebind hook. Anything a
    sub-machine caches from its orientation (animator, mechanical axis, engine reference) must be re-derived
    there; `Initialize` never re-runs (`BlockEngine.cs:187-206`, `BEBehaviorEngineMPGenerator.cs:32-36`).

13. A broken engine still accepts block placement. `OnBlockInteractStart` lets a held placeable block
    fall through before the broken check (`BlockEngine.cs:358-374`), so plumbing against a
    dead engine still works.

14. The repair materials are matched by bare code path. `Matches` compares
    `stack.Collectible.Code.Path` against `["metalplate-iron", "metalplate-steel"]` / `["rod-iron",
    "rod-steel"]` (`BlockEngine.cs:440-442`, `BlockEngineWatt.cs:81-85`) - domain-agnostic, so a modded item
    with a colliding path satisfies a repair.

15. `RepairDescription` is an untranslated English string. `"iron/steel plate"` / `"iron/steel rod"` are
    literals in `BlockEngineWatt.cs:83-84`, interpolated into the translated
    `iiex:engine-repair-materials` line.

16. Several summaries misdescribe this area.
    `IiexConfig.cs:221-224` documents `/exmod steam <level>` - the actual command is
    `/exmod recipes iiex <level>` (`LowPressureExpandedModSystem.cs:34-46`); the config summaries name
    `lpex_values.json` while the attribute registers `ex_values.json` with that as a legacy alias
    (`IiexConfig.cs:16-21`). The dead cost key `pipe-straight-grid` (`IiexRecipeConfig.cs:76`) prices a grid
    recipe that does not exist.

---

## Open

- No editable shape for the engine. `assets/iiex/shapes/engine/watt.json` is the only copy.
- The sub-machine back-reference should call `TryFindEngineFor` (Gotcha 1). Two engines two cells apart
  is a legal, buildable layout today and the binding is undefined.
- Nothing consumes the engine's power except the pump, the generator and smex's blower. Per the settled
  design, iiex is due to own the steam hammer, the wide rolling hall, stamping, the bending roller, the
  boring machine and the rivet die - none of which exists in `src/` ([steam-hammer](steam-hammer.md),
  [wide-hall](wide-hall.md), [bending-roller](bending-roller.md) and
  [boring-machine](boring-machine.md) hold the designs).
- The MP generator is the swap-in for the waterwheel, and nothing documents the swap. The player has to
  work out that the generator's vanilla axle feeds the flywheel's hub port, not the mpenergy graph
  directly. See [mp-energy](../mechanics/mp-energy.md) § the vanilla-MP bridge.
- `0.2625` is a number nobody wrote down: neither the config comment nor the handbook states the actual
  MP load, and it has never been playtested against vanilla machine resistances.
- Fix the `× 3` one way or the other. Either fold it into the config defaults (`PumpWaterPerSecond` 50,
  `AirBlowerOutputPerSecond` 144) or delete it and retune. Leaving it means every rate a player reads is
  wrong.
- No away-catch-up. `MaxAwayCatchupSteps` stays 0, so an engine in an unloaded chunk pumps nothing and
  burns no steam - while the boiler feeding it also does nothing, so the loop is at least self-consistent.
- The over-pressure grace survives a reload (`:378`) but the engine has no way to see it coming down
  from a saved state until it next ticks. A boiler saved at 59 s breaks a second after chunk load.
