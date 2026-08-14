# Cornish Engine
**Status** live   **Mod** hpex

**Owns**
- The control-rod model: the three settings, the fact that the throttle moves five things at once (engage
  pressure, steam draw, power, condensate, cylinder puffs) and not the break pressure, and its
  serialisation.
- The throttle interaction: which cells answer, why it is ctrl and not sneak, the wrench test, the refusals,
  the HUD line and the two block-help entries.
- Every `CornishEngine*` config key and the arithmetic derived from them - the flat 40 L per unit of power,
  the 2.5× efficiency over the Watt, the per-setting MP budget and pump delivery.
- The engine's real operating band - 5 / 6 / 7 engage, 8 break - and the three different bands the
  documentation gives for it.
- Its structure (footprint, filler column), assets, seven RCC stages, grid recipe, repair bill and drops.
- The consequence that follows from its band: which pipe tier can legally supply it.

**Does not own** - cited only, never restated:
- The shared engine model - the fixed-draw / pressure-gates-on-off law,
  `power = RunPower × demand × suppliedFraction`, the condensate path, the over-pressure wear → break →
  wrench-repair cycle, the broken-mesh swap, `ClockState`, the HUD, the sound triad, and every `Engine*` /
  `Mp*` / `SteamEngine*` key in `LpexConfig` - [Watt engine](engine-watt.md). That page is canonical for all
  of it.
- The sub-machine contract - the drive cell, the 90°-clockwise facing snap in both directions, the `+180`
  body frame, `PowerDemand` as the throttle, the two-way animation phase-lock, the MP generator's
  constant-power torque curve and the fluid pump's `× 3` - [Watt engine](engine-watt.md).
- The air blower sub-machine and its rate - smex's block (no design page yet).
- The steam pool, pressure, burst-by-tier, the pressure valve, leaks, the network tick -
  [pipe network](../mechanics/pipe-network.md).
- The rolled tier this engine's supply main must be made of, its 12 atm rating, its welded joint and its
  missing recipe (B5) - [rolled pipe](rolled-pipe.md).
- B6 in full (why the pressure valve cannot be fitted) - [cast pipes](cast-pipes.md) § B6.
- The Lancashire boiler that feeds it, its 12 atm choke and its own arithmetic -
  [Lancashire boiler](boiler-lancashire.md).
- Fillers, footprints, the layout DSL, per-cell collision - [multiblock](../mechanics/multiblock.md).
- Code-first defs, the RCC builder, the cost catalogue - [recipes & config](../mechanics/recipes-config.md).
- The `"mpenergy"` network, the flywheel and the vanilla-MP bridge - [mp-energy](../mechanics/mp-energy.md).

---

## Role

The Cornish runs the same law as the [Watt engine](engine-watt.md), drives the same three sub-machines
through the same contract, and breaks the same way. What it adds is a control rod the player moves with a
wrench, and the rod picks size rather than efficiency.

Two facts decide how the machine reads, and neither is stated anywhere in the game:

1. Efficiency is flat across the throttle. Low, normal and high all cost exactly 40 L of steam per unit of
   power and return 1.5 L of condensate per unit of power.
2. The Cornish is 2.5× the Watt at every setting. The Watt spends 30 L/s for 0.3 power - 100 L per unit.
   That constant, not the throttle, is the tier's payoff.

The cost is the band. The Watt engages at 2 atm; the Cornish engages at 5, 6 or 7 and breaks at 8. Raising
the rod raises the floor: "a line that runs Low comfortably may sit below the High setting's engage pressure
entirely" is the one thing the handbook gets exactly right. Because a supply main has to survive whatever the
boiler is choking at, the band is also a pipe-tier gate - see
[Which pipe can supply it](#which-pipe-can-supply-it).

---

## Structure

A megablock but not a multiblock: it reserves a filler column and has no `MultiblockLayout` and no
`MultiblockStructure` behaviour, so it runs the moment construction finishes. The mechanism is
[Watt engine](engine-watt.md) § Structure's.

| | |
|---|---|
| Class | `BlockEngineCornish : BlockEngine : BlockFilledMegastructure` (`BlockEngineCornish.cs:24`) |
| Block entity | `BlockEntityEngineCornish : BlockEntityEngine : BlockEntityProductionMachine` (`BlockEntityEngineCornish.cs:19`) |
| Footprint | 1 × 4 × 3 (X = 0, Y 0..3, Z 0..2) = 12 cells, 10 fillers, none attach-allowing (golden `goldens/hpex/blocktypes/engine/cornish.json`) |
| Layout | `Origin(0, 3)`, one `Slice(0, …)` - a front elevation, rows run −Y from the top (`BlockEngineCornish.cs:46-59`) |
| Principal | `(0,0,0)`, the `'O'` glyph |
| Left empty | `(0,0,2)` - the sub-machine cell (the `'.'`) |
| Orientation | `side` variant; `Angle` = raw side angle, `BodyAngle` = `Angle + 180` (`BlockEngine.cs:38-43`) |
| Shape spin | `ShapeSpunPerOrientation(base, 180)` (`BlockEngine.cs:231`) ⇒ `*-north: 180`, so `rotateY` and `StructureAngle` agree |
| Collision / selection | full cube on the principal only (`BlockEngine.cs:233-234`) |
| Sub-machine cell | `(0,0,2)` via `submachineOffset` (`BlockEngine.cs:227`) |
| Gear housing | `(0,3,1)` via `gearHousingOffset` (`BlockEngine.cs:228`) - sound emitter only |
| Resistance / stack | 45 / 1 (`BlockEngine.cs:222-223`) |
| Mining tier | 3 (bronze), inherited from `EngineShell` (`BlockEngine.cs:221`) - pinned by `HpMegablockDropTierTests.Cornish_engine_needs_a_bronze_tier_pickaxe` |

```
Slice x = 0        z→ 0   1   2
             y=3   #   #   #      <- gear housing at (0,3,1)
             y=2   #   #   #
             y=1   #   #   #      <- (0,1,0) is the SECOND throttle cell
             y=0   O   #   .      <- principal (throttle cell), filler, SUB-MACHINE CELL
```

This slice is character-for-character identical to the Watt's (`BlockEngineWatt.cs:36-49`), and both carry
the same comment - "Authored per-engine because each engine's column differs" (`:43-45` in both). The
columns do not differ.

### The two pipe ports

Authored in the north frame on the principal cell and rotated by `Angle`, not `BodyAngle`
(`BlockEngine.cs:31-66`) - the mechanism is [Watt engine](engine-watt.md)'s:

| Port | North-frame face | Direction |
|---|---|---|
| Steam inlet | SOUTH | pipe → engine |
| Condensate out | EAST | engine → pipe |

Both are machine ports, not `BlockPipe`s, so `AcceptsNeighbour` lets a rolled run bolt straight onto them
([pipe network](../mechanics/pipe-network.md) § 5). That is the one reason an HP main has anything to connect
to at all.

---

## Assets

| Asset | Path | State |
|---|---|---|
| Editable shape | — | missing. No `assets/editable/shapes/` source for the Cornish engine |
| Runtime shape | `assets/hpex/shapes/engine/cornish.json` | root children `Cylinder` · `BeamSupport` · `Beam` · `Piston` · `ControlPiston` · `ControlPistonSteam` · `Rod` - exactly the seven RCC stages, one more than the Watt's six |
| Animations | same file | `cyclepump` (60 f, `Repeat`) · `cyclemp` (60 f, `Repeat`) · `idlepump` (30 f, `Repeat`) · `idlemp` (30 f, `Repeat`) - the same four clips the Watt has, and they must Repeat ([Watt engine](engine-watt.md) § Assets) |
| Textures | `fire1`, `iron3`, `iron5`, `steel5`, `iron` | the "steel" engine is mostly iron sheet in the art |
| Broken-mesh subtrees | `Root/Cylinder/Cube21` and `Root/Piston` both exist, so the inherited `BrokenHiddenElements = ["Cube21","Piston"]` (`BlockEntityEngine.cs:460`) resolves - but `ControlPistonSteam` is not hidden, so a burst Cornish still displays its steam control gear intact |
| Handbook | `assets/hpex/config/handbook/00-highpressure.json` ↔ `docs/hpex/handbook/00-highpressure.html` | present, shared with the boiler, and wrong about this engine's band and its build cost - [Gotchas](#gotchas) 4 |

---

## Construction

Two steps, both required.

### 1. The frame block — grid recipe (`MachineRecipeDefinitions.cs:39-50`)

```
G H R          P = game:metalplate-steel ×1 each   → 4
P N P          R = game:rod-steel ×4               → 4
P I P          G = gear ×4                          → 4
               N = game:metalnailsandstrips-steel ×4
               I = iwex:pipe-plated-straight-* ×2
               H = hammer (tool)
→ hpex:enginecornish-north
```

Totals: 4 steel plate · 4 steel rod · 4 gears · 4 steel nails-and-strips · 2 plated pipe segments.

Emitted twice, once per gear code - `game:gear-rusty` then `lpex:gear-*` (`:23-25`), source order preserved.
Both copies genuinely use their `G`, so the two recipes are real alternatives rather than byte-identical
clones ([Watt engine](engine-watt.md) Gotcha 9).

The pipe ingredient is the plated (iwex) segment (`:60-61`), not a cast or rolled one. Tier-gating the HP
builds waits on those segments getting craft recipes of their own, and on the hadfield material gate
(`:52-59`) - both open ([rolled pipe](rolled-pipe.md), [Gotchas](#gotchas) 6).

Golden: `goldens/hpex/recipes/grid/machines.json`.

### 2. The RCC stages — `BlockEngineCornish.cs:60-93`

Seven stages - the Watt's six plus `ControlPistonSteam`, the control-rod gear itself.

| # | Adds | `metalplate-*` | `rod-*` | `metalnailsandstrips-*` | `game:burnedbrick-fire` |
|---|---|---|---|---|---|
| 1 | `Root/Cylinder` | — | — | — | — |
| 2 | `Root/BeamSupport` | 12 | 12 | — | 36 |
| 3 | `Root/Beam` | 16 | 8 | — | — |
| 4 | `Root/Piston` | 8 | 6 | — | — |
| 5 | `Root/ControlPiston` | 6 | — | 6 | — |
| 6 | `Root/ControlPistonSteam` | 4 | 8 | — | — |
| 7 | `Root/Rod` | — | 8 | 8 | — |
| | **total** | **46** | **42** | **14** | **36** |

Every metal requirement is the iron-or-steel wildcard - `metalplate-*` / `rod-*` / `metalnailsandstrips-*`
with `allowedVariants: ["iron","steel"]` and `storeWildCard: "metal"` (golden). The high-pressure engine can
be built entirely out of iron, and the stored variant drives the salvage.

| | Watt | Cornish | ratio |
|---|---|---|---|
| stages | 6 | 7 | |
| plate | 4 | 46 | 11.5× |
| rod | 24 | 42 | 1.75× |
| nails | 12 | 14 | 1.17× |
| fire brick | 36 | 36 | 1× |

The handbook says the Cornish's "construction stages cost the same materials as the Watt engine - the
difference in price lies in the engine's main block"
(`docs/hpex/handbook/00-highpressure.html:23-25`). They differ by 42 plates.

### Repair — `BlockEngineCornish.cs:95-99`

| item | qty | display string |
|---|---|---|
| `metalplate-steel` | 4 | `"steel plate"` |
| `rod-steel` | 2 | `"steel rod"` |

Steel only - the Watt accepts `metalplate-iron` or `metalplate-steel` (`BlockEngineWatt.cs:81-85`). The
Cornish is built from iron-or-steel and repaired from steel only. Matching is by bare `Code.Path`
([Watt engine](engine-watt.md) Gotcha 14), and the display strings are untranslated English literals
interpolated into the translated `lpex:engine-repair-materials` line (Gotcha 15 there). Creative repairs are
free (`BlockEngine.cs:399-401`).

Cost-catalogue keys: `enginecornish-grid`, `enginecornish-rcc` (`HpexRecipeConfig.cs:51`, `:55`).

---

## Operation

### The control rods

```
                 wrench + RMB            ──►  raise
                 wrench + ctrl + RMB     ──►  lower
 low ◄────────────────► normal ◄────────────────► high
  0                       1                       2      (clamped, no wrap)
```

| | |
|---|---|
| State | one int, `_throttle`, default 1 = normal (`BlockEntityEngineCornish.cs:22`) |
| Read as | `ThrottleIndex => Math.Clamp(_throttle, 0, 2)` (`:25`) - defensive against a corrupt save |
| Lang fragment | `ThrottleKey` → `low` / `normal` / `high` (`:27-30`) |
| Change | `AdjustThrottle(direction)` clamps and `MarkDirty(true)`; returns `false` when already at the end (`:96-104`) |
| Persistence | `tree.SetInt("throttle", …)` / `GetInt("throttle", 1)` (`:106-119`) - an old save with no key loads as normal |
| HUD | `hpex:engine-info-throttle` = "Throttled {0} (runs {1})" with `ExMeasure.PressureRange(EngagePressure, BreakPressure)`, appended only when constructed and unbroken (`:121-137`) |

Which cells answer: `IsThrottleCell` accepts the engine's own cell and the filler directly above it,
`(0,1,0)` (`BlockEngineCornish.cs:191-194`). The own-cell path is `OnBlockInteractStart` (`:101-112`); the
filler path is `OnFillerInteractStart`, which the megablock forwards (`:114-131`). `GetFillerInteractionHelp`
narrows the help to that one filler (`:207-224`).

Why ctrl and not sneak: `BlockEngineCornish.cs:18-21` - vanilla diverts sneak + right-click to the held item,
where the wrench's reverse-rotate would consume it first. The lower action therefore declares
`HotKeyCode = "ctrl"` (`:256`).

The four refusals, in order (`TryThrottle`, `:138-188`):

| # | condition | result |
|---|---|---|
| 1 | not a throttle cell | `false` → falls through to the base |
| 2 | BE is not a `BlockEntityEngineCornish`, or `IsBroken` | `false` → falls through, so a burst engine still takes its wrench repair |
| 3 | held item's `Code.Path` does not contain `"wrench"` | `false` |
| 4 | already at the end of the range | `true` (consumed) + `SendIngameError("hpex-engine", …max/…min)` |

On a real change: `ExSounds.ToggleSwitch` at the block and a `Notification` chat line
(`hpex:engine-throttle-set`). Everything but the cell test runs server-side only (`:158`); the client still
returns `true`, so the click is consumed on both sides and never reaches vanilla placement.

### What the rod actually moves

Five overrides read `ThrottleIndex`. `BreakPressure` is not one of them in effect - all three settings
resolve to 8.0 (`BlockEntityEngineCornish.cs:44-50`, `HpexConfig.cs:76-78`), so the band's ceiling is fixed
and only its floor moves.

| | low | normal | high | file:line |
|---|---|---|---|---|
| `EngagePressure` | 5.0 atm | 6.0 atm | 7.0 atm | `BlockEntityEngineCornish.cs:36-42` |
| `BreakPressure` | 8.0 | 8.0 | 8.0 | `:44-50` |
| `RunSteamRate` | 8 L/s | 16 L/s | 32 L/s | `:68-74` |
| `RunPower` | 0.2 | 0.4 | 0.8 | `:76-82` |
| `RunWaterOutput` | 0.3 L/s | 0.6 L/s | 1.2 L/s | `:84-90` |
| `CylinderSteamPuffCount` | 0 | 2 | 4 | `:53-59` |
| `SoundVolumeFactor` | 1 | 1 | 1.8 | `:62-63` |
| `SoundPitchFactor` | 1 | 1 | 0.8 | `:65-66` |
| usable band width | 3.0 atm | 2.0 atm | 1.0 atm | derived |

The sound factors are applied every running tick through the base's `SoundVolumeFactor` / `SoundPitchFactor`
hooks, so a throttle change is audible without restarting the loop ([Watt engine](engine-watt.md) § Sound).
At `low` the cylinder emits no steam puff at all - the only visual cue that the machine is running at reduced
admission.

### The tick

Unchanged from [Watt engine](engine-watt.md) § The tick - 1000 ms, `CanRunProduction => IsConstructed`,
inlet pressure counted only when `MediumType == "Steam"`, over-pressure grace 60 s, `demand` from the
sub-machine, `power = RunPower × demand × frac`. The Cornish overrides only the numbers above. In particular:

- Inlet pressure is a gate, not a throttle. 6.0 atm and 7.9 atm produce identical power at the normal
  setting. The rod is the throttle; the line is not.
- An engine with no sub-machine is completely inert, `demand = 0`, at every throttle setting.
- Repairing resets the over-pressure timer so one tick cannot re-break it.

### Which pipe can supply it

The engine's engage pressure has to be reached inside a pipe run, and a run is capped by its weakest
burstable segment ([pipe network](../mechanics/pipe-network.md) § 3, § 5). Cross the two tables:

| tier | burst | reaches low (5.0)? | normal (6.0)? | high (7.0)? |
|---|---|---|---|---|
| plated (iwex) | 2.5 | no | no | no |
| cast (lpex) | 5.0 | only exactly at 5.000 - and that is already the burst threshold | no | no |
| rolled (hpex) | 12 | yes | yes | yes |

`TickOverpressureAndBurst` fires at `Pressure >= minBurst − 0.001` (`PipeNetwork.cs:788`), so a cast main held
at exactly 5.0 to satisfy the low setting is inside its 30-second burst grace, losing one segment every 30 s,
indefinitely.

So the Cornish engine at normal or high throttle can only be supplied through rolled pipe - and rolled pipe
has no recipe (B5, [rolled pipe](rolled-pipe.md)). Outside creative the engine is reachable only at its low
setting, on a cast main that is permanently self-destructing. Nothing in the code or the handbook says this;
it falls out of two config tables that live in different mods.

### Sub-machines

The contract, the snap and the phase-lock are [Watt engine](engine-watt.md) § The sub-machines'. Only the
numbers change, and they change per throttle setting:

| | low | normal | high | Watt (for scale) |
|---|---|---|---|---|
| `AvailablePower` at full demand | 0.2 | 0.4 | 0.8 | 0.3 |
| `MpPowerBudget` = `power × 0.875 × 1.0` | 0.175 | 0.35 | 0.70 | 0.2625 |
| Fluid-pump delivery = `16.67 × 3 × power` | 10.0 L/s | 20.0 L/s | 40.0 L/s | 15.0 L/s |
| Sub-machine outlet pressure at engage = `inlet × 0.75` | 3.75 atm | 4.5 atm | 5.25 atm | 1.5 atm |
| Sub-machine outlet pressure just under break | 6.0 atm | 6.0 atm | 6.0 atm | 3.0 atm |
| Animation speed = `0.5 + power` | 0.7 | 0.9 | 1.3 | 0.8 |

The pump's `× 3` is the undocumented factor [Watt engine](engine-watt.md) Gotcha 10 owns; the air blower
carries the same one, and its rate is smex's (`SmexConfig.cs:113`).

`MpRatedLoad` is computed from a power this engine can never produce. `MpRatedLoad = MaxPower ×
MpLoadPerEnginePower` (`BlockEntityEngine.cs:161`) and `MaxPower` is `CornishEngineMaxPower` = 1.0
(`HpexConfig.cs:81`), but the largest `RunPower` is 0.8. Consequences:

```
MpRatedLoad        = 1.0  × 0.875 = 0.875
MpPowerBudget(max) = 0.8  × 0.875 = 0.700
speed at rated load = 0.700 / 0.875 = 0.80 × rated      // the Watt hits exactly 1.00
stall threshold     = 2 × 0.875   = 1.75
speed at the stall threshold = 0.700 / 1.75 = 0.40 × rated
```

`IsMpOverstressed`'s doc comment (`BlockEntityEngine.cs:171-172`) says the threshold is "what keeps the
network above half rated speed". That invariant holds for the Watt (`MaxPower == RunPower`) and is broken
here: a Cornish-driven MP line drops to 0.4 × rated before the generator cuts demand, so it can sit stalled
well below half speed with no recovery cut-out. Setting `CornishEngineMaxPower` to `0.8` would restore both
properties. The key's doc comment calls it a "display reference" (`HpexConfig.cs:80`) - it is displayed
nowhere; `MaxPower`'s only consumer in the entire repo is `MpRatedLoad`.

---

## Numbers

### hpex config — `HpexConfig.cs`, file `ModConfig/ex_values.json`, section `hpex`

| key | value | file:line | what it does |
|---|---|---|---|
| `CornishEngineEngagePressureLow` | `5.0 atm` | `HpexConfig.cs:70` | inlet at/above which it runs, low rod |
| `CornishEngineEngagePressureNormal` | `6.0 atm` | `:71` | normal rod |
| `CornishEngineEngagePressureHigh` | `7.0 atm` | `:72` | high rod |
| `CornishEngineBreakPressureLow` | `8.0 atm` | `:76` | above this it wears toward a burst |
| `CornishEngineBreakPressureNormal` | `8.0 atm` | `:77` | identical |
| `CornishEngineBreakPressureHigh` | `8.0 atm` | `:78` | identical |
| `CornishEngineMaxPower` | `1.0` | `:81` | not a display value - the sole input to `MpRatedLoad` |
| `CornishEngineSteamLow / Normal / High` | `8 / 16 / 32 L/s` | `:84-86` | fixed draw while engaged |
| `CornishEnginePowerLow / Normal / High` | `0.2 / 0.4 / 0.8` | `:89-91` | power at full demand and full supply |
| `CornishEngineWaterLow / Normal / High` | `0.3 / 0.6 / 1.2 L/s` | `:94-96` | condensate out the east face |
| `CornishEngineOverclockVolume` | `1.8` | `:101` | high-rod sound volume factor |
| `CornishEngineOverclockPitch` | `0.8` | `:102` | high-rod sound pitch factor |
| `RccBrokenDropsRatio` | `0.8` | `:124` | salvage on mining, registered at `HighPressureExpandedModSystem.cs:35-38` |

`HpexConfig.Migrations` is empty (`:37`) - nothing here is force-reset on upgrade.

### Derived — owned here

| quantity | formula | low | normal | high |
|---|---|---|---|---|
| steam per unit power | `RunSteamRate / RunPower` | 40 L | 40 L | 40 L |
| condensate per unit power | `RunWaterOutput / RunPower` | 1.5 L | 1.5 L | 1.5 L |
| band width | `Break − Engage` | 3.0 atm | 2.0 atm | 1.0 atm |
| `MpPowerBudget` at full power | `power × 0.875 × 1.0` | 0.175 | 0.35 | 0.70 |
| pump delivery | `16.67 × 3 × power` | 10.0 L/s | 20.0 L/s | 40.0 L/s |
| animation speed | `0.5 + power` | 0.7 | 0.9 | 1.3 |

Constants, not per-setting:

| quantity | value |
|---|---|
| `MpRatedLoad` | 0.875 (from `MaxPower` 1.0) |
| MP stall threshold | 1.75 |
| MP speed at rated load, high rod | 0.80 × rated |
| Cornish : Watt steam efficiency | `100 / 40` = 2.5× |
| Cornish : Watt condensate per unit power | `3.33 / 1.5` = 2.2× less water |

### Cited, owned elsewhere

| quantity | value | owner |
|---|---|---|
| Watt engage / break / power / steam | 2.0 / 4.0 / 0.3 / 30 L/s | [Watt engine](engine-watt.md) |
| `SteamEngineEfficiency` | 0.75 | [Watt engine](engine-watt.md) |
| `EngineOverPressureSeconds` | 60 s | [Watt engine](engine-watt.md) |
| `MpLoadPerEnginePower` / `MpRatedSpeed` | 0.875 / 1.0 | [Watt engine](engine-watt.md) |
| `PumpWaterPerSecond` (+ the `× 3`) | 16.67 L/s | [Watt engine](engine-watt.md) |
| air-blower rate | smex's | `SmexConfig.cs` |
| Lancashire choke / steam | 12 atm / 48 L/s | [Lancashire boiler](boiler-lancashire.md) |
| rolled / cast / plated pipe burst | 12 / 5.0 / 2.5 | [rolled pipe](rolled-pipe.md), [cast pipes](cast-pipes.md), [pipe network](../mechanics/pipe-network.md) |
| lpex pressure-valve gate ceiling | 5.0 atm | [cast pipes](cast-pipes.md) § B6 |

```
2.0──4.0   |  5.0    6.0    7.0  |  8.0        12.0
Watt band  | low-N  norm-N  hi-N | Cornish     Lancashire choke
           |    Cornish engage   | break       == rolled pipe burst
5.0 = lpex pressure-valve ceiling ─┘ reaches ONLY the low setting
```

### Hard-coded — not config

The engine's hard-coded constants are inherited and tabulated by
[Watt engine](engine-watt.md) § hard-coded (tick intervals, the run threshold, the speed epsilon, the burst
FX, the gear hum, `PistonCycleSounds`' frames, the offsets, the generator's drag and taper). This block's
own:

| value | file:line | what it does |
|---|---|---|
| throttle range `0..2` | `BlockEntityEngineCornish.cs:25`, `:98` | clamped in both the accessor and the mutator |
| default throttle `1` | `:22`, `:118` | normal, in the field and the tree default |
| throttle key table `["low","normal","high"]` | `:27` | the lang-key fragments |
| puff counts `0 / 2 / 4` | `:53-59` | 0 suppresses the cylinder puff entirely |
| wrench test `Code.Path.Contains("wrench")` | `BlockEngineCornish.cs:154-156` | substring match - see [Gotchas](#gotchas) 3 |
| throttle cells `pos` and `pos.UpCopy()` | `:191-194` | Y is orientation-independent, so no rotation is applied |
| direction from `Controls.CtrlKey` | `:161` | `-1` with ctrl, `+1` without |
| error code `"hpex-engine"` | `:177` | the `SendIngameError` channel |
| repair bill `4 plate + 2 rod`, steel only | `:95-99` | |
| mining tier `3`, resistance `45` | `BlockEngine.cs:221-223` | inherited from `EngineShell` |

---

## Drops

The engine keeps its self-drop, unlike the boiler. `EngineShell` sets neither `NoDrops()` nor a `GetDrops`
override, and the golden has no `drops` key - pinned by
`HpMegablockDropTierTests.Cornish_engine_still_drops_its_craftable_frame` (`HpMegablockDropTierTests.cs:62-76`).

| Path | Returns |
|---|---|
| Mined | 1 × `hpex:enginecornish-<side>` + 80 % of the RCC materials - ~37 plate, ~34 rod, ~11 nails, ~29 brick |
| Fillers | removed by `BlockFilledMegastructure`, never dropped |
| Broken (burst) engine | still drops normally - the break costs nothing on break; the loss is the repair bill and the downtime |
| Sub-machine | ordinary block drop, independent of the engine |

There is no engine equivalent of the boiler's explosion salvage path: a Cornish burst destroys nothing.

---

## Code

| Piece | file:line |
|---|---|
| `BlockEngineCornish : BlockEngine, IFillerHost, IEngineGeometry, IExBlockDefProvider` | `BlockStructures/Engine/Blocks/BlockEngineCornish.cs:24` |
| `Definitions(domain)` → the single def | `:32-33` |
| `Cornish(domain)` - footprint + seven stages | `:35-93` |
| `RepairItems` (steel only) | `:95-99` |
| `OnBlockInteractStart` (own-cell throttle) | `:101-112` |
| `OnFillerInteractStart` (forwarded throttle) | `:114-131` |
| `TryThrottle` - the four refusals, sound, chat, error | `:138-188` |
| `IsThrottleCell` | `:191-194` |
| `GetPlacedBlockInteractionHelp` / `GetFillerInteractionHelp` | `:196-205` / `:207-224` |
| `WithThrottleHelp` (raise + ctrl-lower entries) | `:231-260` |
| `BlockEntityEngineCornish : BlockEntityEngine` | `BlockStructures/Engine/BlockEntities/BlockEntityEngineCornish.cs:19` |
| `ThrottleIndex` / `ThrottleKey` | `:25` / `:30` |
| the eight per-setting overrides | `:32-90` |
| `AdjustThrottle` | `:96-104` |
| tree round-trip | `:106-119` |
| `GetBlockInfo` (the throttle line) | `:121-137` |
| `HpexConfig` § Cornish engine | `HpexConfig.cs:66-103` |
| grid recipe (looped over two gear codes) | `Recipes/Grid/MachineRecipeDefinitions.cs:39-50`, `:23-25` |
| cost-catalogue keys | `HpexRecipeConfig.cs:51`, `:55` |
| lang (`engine-throttle-*`, `blockhelp-engine-throttle-*`) | `assets/hpex/lang/en.json` |
| save migration off `lpex:` / `ppex:` | `BlockMigrations/HpexExtractionMigration.cs:34-46` - owned by [rolled pipe](rolled-pipe.md) § Gotchas 1 |
| everything that runs | `LowPressureExpanded/BlockStructures/Engine/BlockEngine.cs`, `BlockEntityEngine.cs` - [Watt engine](engine-watt.md) § Code |

### Where a caller hooks in

- A fourth throttle setting: `ThrottleKeys` (`:27`), the three clamps (`:25`, `:98`) and eight `switch`
  expressions all hard-code `0..2` independently. Nothing derives the range from the key table.
- A different throttle gesture: `IsThrottleCell` (`:191-194`) is the only cell rule and `TryThrottle` the
  only entry point; both interaction overrides funnel into it.
- A governed engine (the planned Corliss): it wants steam to follow load, i.e. `RunSteamRate` scaled by
  `demand` rather than a rod. The base already multiplies the draw by `demand`
  (`BlockEntityEngine.cs:283-286`), so a Corliss is closer to "no rod, `RunPower` proportional to inlet" than
  to a fourth setting here.

### Tests — `test/HighPressureExpanded.Tests/`

| file | pins |
|---|---|
| `Scenarios/HpSteamPlantScenarioTests.cs` | boiler-stand-in → Cornish → MP generator delivers a budget at 7 atm, and nothing without steam; → smex air blower pressurises past `SmexValues.BlastPressureThreshold`, and nothing without steam |
| `Blocks/Engine/MPGeneratorBehaviorTests.cs` | the generator's constant-power torque curve, its 0.0005 drag, the soft speed cap, the axle axis from the side variant, and `PowerDemand` cutting out past `2 × MpRatedLoad` - all driven through a Cornish rig |
| `Definitions/HpMegablockDropTierTests.cs` | keeps its self-drop · mining tier 3 · no JSON `brokenDropsRatio` |
| `Definitions/HpexDefinitionGoldenTests.cs` | the def reproduces `goldens/hpex/blocktypes/engine/cornish.json`; shapes resolve |
| `Fixtures/HpSteamPlantScenes.cs` | `MPGeneratorPlant` / `AirBlowerPlant` - the only fixture in the repo that needs hpex and smex in one assembly |

Nothing tests the throttle. There is no test that `AdjustThrottle` clamps, that the tree round-trips
`throttle`, that the three settings return the three bands, or that a wrench click on the filler above the
engine reaches `TryThrottle`.

The two scene fixtures comment "The Cornish engine's band is high (≥6 atm) - a plated pipe bursts at 5, so
feed it through cast" (`HpSteamPlantScenes.cs:57`, `:152`) and then pass `material: "hadfield"`, which
`PipeTestWorld.DomainOf` maps to hpex - rolled (`PipeTestWorld.cs:65-71`). The code is right and the comment
names the wrong tier twice; a cast main would not hold 7 atm.

---

## Gotchas

1. Three documented bands, one real one. The engine engages at 5 / 6 / 7 and breaks at 8
   (`HpexConfig.cs:70-78`). The documentation gives:

   | source | band | wrong how |
   |---|---|---|
   | `HpexConfig.cs:67-69` | "low works on a gentle 5-8, normal on 6-8, high demands a hot 7-8" | correct |
   | `src/HighPressureExpanded/README.md:17` | "the efficient high-pressure beam engine (6-8 atm)" | drops low and high |
   | `docs/hpex/handbook/00-highpressure.html:19` | "Running at 6-8 atm" | same |

   The in-game HUD is the only source that is always right, because it renders
   `ExMeasure.PressureRange(EngagePressure, BreakPressure)` from the live config
   (`BlockEntityEngineCornish.cs:130-136`).

2. The engine cannot legally be supplied at normal or high throttle. Rolled pipe is the only tier that
   reaches 6-7 atm without bursting, and rolled pipe has no recipe (B5, [rolled pipe](rolled-pipe.md)). The
   pressure valve that would let a cast main serve it safely cannot be fitted to a rolled run and tops out at
   5.0 anyway (B6, [cast pipes](cast-pipes.md) § B6), and a refused joint does not even leak, so the player
   gets no signal (B18). See [Which pipe can supply it](#which-pipe-can-supply-it).

3. The wrench test is a substring match on the item's code path. `Contains("wrench")`
   (`BlockEngineCornish.cs:154-156`) - any item from any mod whose path contains that substring drives the
   control rods. Same class of bug as [Watt engine](engine-watt.md) Gotcha 4's `IsMPGenerator`.

4. A plain wrench right-click on the engine cell or the cell above is always consumed by the throttle
   (`:108-111`, `:122-123` - `TryThrottle` returns `true` even when it only prints the "already at the end"
   error, and returns `true` client-side without doing anything). It reaches the base's interaction chain
   only when the engine is broken, which is what makes the wrench repair still work. Any future wrench
   gesture on those two cells has to be added inside `TryThrottle`.

5. `CornishEngineMaxPower = 1.0` is unreachable and mis-documented. Its doc comment calls it a "display
   reference" (`HpexConfig.cs:80`); it is never displayed, and its only consumer is `MpRatedLoad`
   (`BlockEntityEngine.cs:161`), which it inflates to 0.875 against a real maximum of 0.8. That breaks the
   "2 × rated load = half rated speed" invariant the overstress cut-out is documented against. See
   [Sub-machines](#sub-machines).

6. The hadfield material gate does not exist. The design gates hpex machinery on hadfield steel as a
   construction material. In code the seven stages accept `metalplate-*` / `rod-*` /
   `metalnailsandstrips-*` restricted to iron or steel, and the grid frame takes plain
   `game:metalplate-steel` / `game:rod-steel`. There is no hadfield item, block or variant anywhere in
   `src/`. The recipe source records the gate as pending (`MachineRecipeDefinitions.cs:57-59`);
   `../STATE.md:275-277` records the same. The only real gate on this engine is a bronze pickaxe to mine it.

7. `modinfo.json` declares a dependency the build does not have and cannot enforce. `modinfo.json:9-15` lists
   `smex: 0.1.0`; `HighPressureExpanded.csproj:86-100` references only ExpandedLib and LowPressureExpanded,
   and the comment at `:76-85` explains why - the Cornish drives smex's air blower purely through lpex's
   `BlockEntityEngine` contract, so there is no code edge. The declaration is a load-order statement, not a
   compile-time one; if smex is absent the engine simply has one fewer sub-machine.

8. The broken mesh does not hide the control gear. `BrokenHiddenElements` is the inherited
   `["Cube21", "Piston"]` (`BlockEntityEngine.cs:460`); the Cornish's seventh subtree
   `Root/ControlPistonSteam` is not in the list, so a burst engine still renders its steam control piston in
   place. Both named elements do exist in `cornish.json`, so nothing is broken - the omission is cosmetic and
   undocumented.

9. The Cornish engine's footprint slice is a byte-copy of the Watt's, comment included, and that comment
   asserts they differ (`BlockEngineCornish.cs:43-45` vs `BlockEngineWatt.cs:33-35`). Nothing derives one
   from the other.

10. The engine is built from iron and repaired with steel. The stages wildcard `iron|steel` (`:63-92`);
    `RepairItems` is `metalplate-steel` / `rod-steel` only (`:95-99`). An iron-built Cornish is unrepairable
    until the player reaches steel.

11. The steam-per-power ratio is flat, so "efficient" means "cheaper than a Watt", not "cheaper at low
    throttle". All three settings cost 40 L per unit; the handbook's "wringing the same power from less steam
    because it can regulate how much steam enters the cylinder"
    (`docs/hpex/handbook/00-highpressure.html:17-19`) attributes the efficiency to the wrong mechanism.

12. The handbook's claims about this engine's cost and output are unsupported. "on High it will comfortably
    drive six helve hammers" (`:22`) is a number nothing in the repo computes - the same class of guess as
    [Watt engine](engine-watt.md)'s "four helve hammers", and the actual `MpPowerBudget` is 0.70. "Its
    construction stages cost the same materials as the Watt engine" (`:23-25`) is off by 42 plates.

13. The default throttle is `normal`, so a freshly placed Cornish needs 6 atm, not 5. Both the field
    initialiser and the tree default say 1 (`BlockEntityEngineCornish.cs:22`, `:118`). A player who builds
    the engine on a 5 atm line sees `under` in the HUD and has to know to lower the rod.

---

## Open

- Nothing tests the throttle - the one feature the class exists for. A BE test (clamp, round-trip, band per
  setting) and a block test (filler-forwarded wrench click) would cost very little.
- No editable shape. `assets/hpex/shapes/engine/cornish.json` is the only copy.
- Fix `CornishEngineMaxPower` (Gotcha 5): set it to 0.8 to restore the rated-speed and half-speed invariants,
  or change `MpRatedLoad` to read the current setting's `RunPower` and re-derive the stall guard's
  latch-safety argument.
- The supply problem is the release blocker, not the engine. `../STATE.md:290` makes B5/B6 release-critical
  for exactly this reason: without a craftable rolled tier and an hpex-domain pressure valve, the Cornish
  engine has no legal supply above 5 atm.
- The hadfield gate is either a design to build or a claim to delete (Gotcha 6). It appears in
  `../materials.md` and a code comment, and nowhere in `src/`.
- A governed engine is designed and unbuilt (the Corliss). Until it lands, the Cornish is the only machine in
  the suite where the player chooses an operating point, and it chooses it with a wrench rather than a load
  signal.
- The documented bands should collapse to one source. The HUD already reads the live config; the README and
  the handbook should quote it or say nothing.
