# Pumps & the Fluid Intake
**Status** live: fluid intake · engine fluid pump · manual fluid pump - designed: mechanical MP pump · injector   **Mod** lpex

**Owns**
- The water chain as one machine: intake → source line → pump → delivery line, and the invariant that
  the intake is the generator and the pump only transfers.
- Every pump's delivered rate, with its file:line - including the undocumented `× 3` factor in the
  engine pump (and its twin in smex's air blower), and the exact figures the shipped config comments and the
  handbook get wrong by 3×.
- The fluid intake's placement and validity rules: the water-cube scan, the ice tolerance, the exclusion
  range, and the source-side 1 atm head.
- The delivery-pressure formula each pump commands, and the settled "pumps are pressure multipliers"
  correction: the current reducer formula makes a steam pump structurally unable to feed the boiler driving
  it, and the fix is a new `PumpPressureRatio`, not a re-scaled `SteamEngineEfficiency`.
- The proposed mechanical MP pump and injector rows of the four-device feed roster.

**Does not own** - cited only, never restated:
- The pipe graph, the one-medium pool, `MaxVolume`, liquid pressure vs `FeedPressure`, leaks, bursts and the
  tick order - [pipe network](../mechanics/pipe-network.md).
- Pipe blocks, valves, the pressure valve, joints, the cast tier's burst rating - [cast pipes](cast-pipes.md).
- The planned buffer between a pump and its consumer - [fluid tank](fluid-tank.md).
- The boiler and engine FSMs, their steam/power/condensate rates, the Cornish/Watt stat tables and the
  boiler-feedwater design as a whole - [Cornish boiler](boiler-cornish.md) and [Watt engine](engine-watt.md).
- The MP generator sub-machine and vanilla-MP torque - [Watt engine](engine-watt.md),
  [mp-energy](../mechanics/mp-energy.md).
- smex's engine air blower, which shares the sub-machine base and the same `× 3` bug - smex's block
  (no design page yet).
- Multiblock fillers and footprints - [multiblock](../mechanics/multiblock.md).
- Code-first defs and the recipe-cost catalogue - [recipes-config](../mechanics/recipes-config.md).

---

## Role

The chain feeds a boiler from a pond without the player carrying buckets. It splits into two devices that are
not the same thing:

- **The fluid intake is the generator.** It is the only block that creates water out of the world, and it
  produces into its own network (the same idiom the [twin-tub blower](twin-tub-blower.md) uses for air). It
  has no rate of its own; whatever asks it to produce sets the amount.
- **A pump is a transfer.** It moves standing water from a source line to a delivery line and then tells the
  intake to refill what it took. It never reaches into the pond.

The ordering is fixed. If a pump refilled first and transferred second, the source pipe would read as an
empty "Air" pool at broadcast time and the HUD would mislabel a working water line
(`BlockEntityManualFluidPump.cs:126-130`).

---

## Structure

### Fluid intake — `lpex:pipe-fluidintake-{n,s,w,e}`

A single-cell network node (`BlockFluidIntake : BlockNetworkNode`, `BlockFluidIntake.cs:12`) on the
`pipe` network (`:14`), placed on top of water. Its block entity is a bare `BlockEntityNetworkNode` and not
an `IPipeNode`, so it never appears as a consumer in the tick's classification pass.

| | |
|---|---|
| Variants | `orientation` `n` `s` `w` `e`; fallback `s`, not the first listed (`BlockFluidIntake.cs:39`) |
| `IsFullCube` | `true` - opts into on-the-fly wrench-cycle recomputation so all four facings are reachable (`:46`) |
| Placement | refused unless the block below has `LiquidCode == "water"`; `failureCode = "lpex-fluidintake-nowater"` (`:52-79`) |
| Self-break | suppressed. `OnNeighbourBlockChange` only re-syncs orientation; losing the water disables it rather than destroying it (`:86-96`) |

### Engine fluid pump — `lpex:enginefluidpump-{side}`

A single-cell engine sub-machine (`BlockEngineFluidPump : BlockEngineSubmachine, INetworkConnector`,
`BlockEngineFluidPump.cs:16`). It is a connector, not a node: it reads the runs across its faces and never
joins either.

| face | role | file:line |
|---|---|---|
| `DOWN` | source (intake) line | `BlockEngineFluidPump.cs:45-46` |
| `LeftFace` (WEST rotated by the `side` variant) | delivery line | `:39-46` |

Placement snaps to the engine's sub-machine facing when one is nearby (`BlockEngineSubmachine.cs:14-51`); the
BE locates its master engine by inverting `BlockEngine.SubmachinePos` with a horizontal fallback
(`BlockEntityEngineSubmachine.cs:131-154`).

### Manual fluid pump — `lpex:manualfluidpump-{side}`

Two cells tall, horizontally orientable, and - like the engine pump - an `INetworkConnector` and not a
network node (`BlockManualFluidPump.cs:24-31`). The cell above is reserved by a single invisible filler
(`FillerOffsets([new FillerCellSpec(0, 1, 0)])`, `:54`), which gives it real collision and a second crank
target.

| | |
|---|---|
| Input face | SOUTH rotated by the placement angle (`:65-66`) - the crank-support side |
| Output face | NORTH rotated (`:69-70`) |
| Connectors | exactly those two (`HasConnectorAt`, `:72-73`) |
| Orientation | `HorizontalOrientable`, look-aware on placement, not wrench-orientable (`:15-22`, `:49`) |
| Filler triad | `CanPlaceBlock` / `OnBlockPlaced` / `OnBlockBroken` drive `StructureFillers` by hand (`:77-123`) |

Placement is refused with `notenoughspace` if the two-cell volume is not clear (`:87-93`).

### Mechanical MP pump *(designed — nothing in `src/`)*

Specified as a walking-beam block driven from the MP network rather than from steam, so it can feed a
boiler with the fire out. A grep for `MpPump` / `mppump` / `SteamPump` across the repo
returns no source file. Its art is drawn: `assets/editable/shapes/machine-pipe-megablock-mppump.json`
(elements `Base`, `BaseExtension`, `BasePipe`, `GearAxle`, `Piston`, `ReservoirConn`; `idle` 30 f, `cycle`
60 f - the same clip pair every lpex machine uses).

### Injector *(designed — nothing in `src/`)*

A single-cell brass connector (like the steam condenser), bridging a live-steam run, a cold-water run and
a delivery run in its block entity - mandatory, because steam and water cannot share a pool.
Union stubs rather than flanges, so the joint-family rule never applies to it
([cast pipes](cast-pipes.md)).

---

## Assets

| asset | path | state |
|---|---|---|
| Intake runtime shape | `assets/lpex/shapes/pipes/fluidintake.json` | `Cube2` `Cube11` `Cube26`; textures `iron3` `iron2` `iron`; no animation |
| Intake editable | — | none |
| Engine pump runtime | `assets/lpex/shapes/engine/fluidpump.json` | `Cube2` `Cube6` `Cube10` `Cube17` `Piston`; `idle` 30 f, `cycle` 60 f |
| Engine pump editable | — | none |
| Manual pump runtime | `assets/lpex/shapes/manualfluidpump.json` | `Pipe` `Casing` `Cylinder` `CrankSupport` `HandCrank` `Piston`; `cycle` 30 f, `idle` 30 f |
| Manual pump editable | `assets/editable/shapes/machine-pipe-megablock-manualpump.json` | identical element/clip set - the live source |
| MP pump editable | `assets/editable/shapes/machine-pipe-megablock-mppump.json` | art only, wired to nothing |
| Injector | — | not drawn |

Both live pumps are `Animatable` and hold one clip at a time - `cycle` while working, `idle` otherwise -
because letting both stop makes the animator mesh vanish and `GetBlockInfo` NRE
(`BlockEntityEngineSubmachine.cs:209-247`, `BlockEntityManualFluidPump.cs:199-236`). The engine pump's cycle
runs at the engine's speed and is phase-locked to it (`BlockEntityEngineSubmachine.cs:266-278`); the
manual pump's runs at a flat 1.0.

Sounds: the engine pump layers a `Watering` loop over the shared piston strokes while it is actually drawing
(`BlockEntityEngineFluidPump.cs:71-90`); the manual pump layers a `MetalGrinding` loop (vol 0.35) while
cranked and a `Watering` loop (vol 1.0) while drawing (`BlockEntityManualFluidPump.cs:242-276`). The
intake is silent - its only feedback is three HUD lines.

---

## Construction

| block | recipe | file:line |
|---|---|---|
| `pipe-fluidintake-s` | `_H_,PIP,NPN` - 1 `iwex:pipe-straight-*` + 3 plate + 4 nails + hammer | `MachineRecipeDefinitions.cs:39-47` |
| `enginefluidpump-north` | `_HG,PIP,RIR` - 2 straight + 2 plate + 4 rod + 1 gear + hammer | `:83-93` |
| `manualfluidpump-north` | `_GH,PIP,BRB` - 1 straight + 2 plate + 2 gear + 2 rod + 4 `game:supportbeam-*` + hammer | `:95-106` |
| mechanical MP pump | no block, no recipe | — |
| injector | no block, no recipe | — |

The two gear-driven pumps are each authored twice, once for `game:gear-rusty` and once for `lpex:gear-*`,
by a loop over both codes (`MachineRecipeDefinitions.cs:29-33`). The pipe ingredient is the plated iwex
segment in every case (`:127-128`).

Cost-catalogue keys: `pipe-fluidintake-grid` (`LpexRecipeConfig.cs:80`), `enginefluidpump-grid` (`:70`),
`manualfluidpump-grid` (`:72`) - all three live, all three backed by a real recipe.

---

## Operation

```
      pond
        │  the cube below must be all water          FluidIntakeWaterDepth = 3
        ▼
 ┌─ FLUID INTAKE ─┐   produces into ITS OWN network at a fixed 1 atm head
 │  CanIntake =   │   ProduceLiquidMeasured(amount, temp, 1f)
 │  HasWater && ! │
 │  Crowded       │        FluidIntakeExclusionRange = 6 blocks
 └───────┬────────┘
         │  source line  (a normal water run)
         ▼
   ╔══ PUMP ══╗   1. move min(rate·dt, output free capacity) OUT of the source line
   ║ transfer ║   2. produce the same litres INTO the delivery line at its own pressure
   ║   only   ║   3. tell the intake to refill `rate·dt` back into the source line
   ╚═════╤════╝
         │  delivery line, pressurised
         ▼
     boiler / anything that draws water
```

### The intake

Server-side, once a second, it re-scans and syncs only on change (`BlockEntityFluidIntake.cs:52-73`).

`ScanWaterBelow` (`:80-100`) walks a `depth³` cube below the block -
`dx ∈ [−1,1]`, `dy ∈ [−1,−3]`, `dz ∈ [−1,1]` at the shipped depth of 3 - and requires every cell to be
water, with one exemption: a top-layer outer cell may be `EnumBlockMaterial.Ice`, so a lake's frozen
skin does not stop it. The cell directly below must stay liquid, so once that freezes the intake stops.

`HasNearbyIntake` (`:103-124`) scans a Euclidean sphere of `FluidIntakeExclusionRange` and disables the
intake if another `BlockFluidIntake` is inside it - the anti-packing rule.

`ProduceWater(amount, temperature, ba)` (`:41-50`) is the entry point a pump calls:

```csharp
if (!CanIntake || amount <= 0f) return 0f;
if (NetworkSystem?.GetNetworkAt(Pos) is not PipeNetwork net) return 0f;
return net.ProduceLiquidMeasured(amount, temperature, 1f, ba);   // 1 atm gravity head
```

The `1f` is the source line's `FeedPressure`, a gravity-fed head. The delivery side's pressure is the
pump's business, never the intake's (`:48`).

### Engine fluid pump — `DoWork`, `BlockEntityEngineFluidPump.cs:29-56`

Runs on the shared production tick: server-side, 1000 ms, `dt` clamped to 2 s
(`BlockEntityProductionMachine.cs:26`, `:75`, `:126`), gated on `Engine != null`
(`BlockEntityEngineSubmachine.cs:160`).

```csharp
float pressure = (Engine?.InletPressure ?? 0f) * LpexValues.SteamEngineEfficiency;   // :46-47
float amount   = LpexValues.PumpWaterPerSecond * 3 * power * dt;                     // :48

float move  = Math.Min(amount, FluidPumpCore.OutputFreeCapacity(leftNet));           // :50
float drawn = bottomNet?.TryConsumeLiquid(move, ba) ?? 0f;                           // :51
if (drawn > 0f) leftNet?.TryProduceLiquid(drawn, 20f, pressure, ba);                 // :53
intake.ProduceWater(amount, 20f, ba);                                               // :55
```

`power` is `Engine.AvailablePower` (`BlockEntityEngineSubmachine.cs:162-163`), which is
`RunPower × demand × frac` - the engine's rated power scaled by how much steam the line could actually
supply (`BlockEntityEngine.cs:283-296`). Water is delivered at a flat 20 °C regardless of anything.

### Manual fluid pump — `DoWork`, `BlockEntityManualFluidPump.cs:131-156`

Cranked by holding right-click with an empty hand, on the pump cell or its top filler
(`BlockManualFluidPump.cs:129-224`). The BE tracks the hold with a server watchdog: `OnPumpStep` refreshes a
timestamp, and a server tick more than 1200 ms stale stops the crank, covering a missed release event
from a teleport, death or disconnect (`BlockEntityManualFluidPump.cs:116-121`). Persisted run state is
discarded at load, because nobody is holding the button after a reload (`:64-66`).

```csharp
float amount = LpexValues.ManualPumpWaterPerSecond * dt;                             // :142
float move   = Math.Min(amount, FluidPumpCore.OutputFreeCapacity(outputNet));        // :143
float drawn  = inputNet?.TryConsumeLiquid(move, ba) ?? 0f;                           // :144
if (drawn > 0f) outputNet?.TryProduceLiquid(drawn, 20f, 1f, ba);                     // :147  ← fixed 1 atm
intake!.ProduceWater(amount, 20f, ba);                                              // :148
```

No `× 3`. The manual pump is the one device whose shipped rate matches its documentation.

### The shared half — `FluidPumpCore`

Both pumps route through two helpers so the "find the intake / how much fits" logic exists once
(`FluidPumpCore.cs:16`):

| helper | file:line | behaviour |
|---|---|---|
| `FindIntake(ba, net)` | `:19-34` | the first node on the run whose BE is a `BlockEntityFluidIntake` with `CanIntake` - iteration order of `net.Nodes`, not proximity |
| `OutputFreeCapacity(net)` | `:37-40` | `Nodes.Count × LitresPerPipe − Volume`; `0` for a null net |

---

## Numbers

### Config — `src/LowPressureExpanded/LpexConfig.cs`, `ModConfig/ex_values.json`, section `lpex`

| key | value | file:line | what it does |
|---|---|---|---|
| `PumpWaterPerSecond` | `16.67` | `LpexConfig.cs:199` | base water L/s per unit of engine power - then multiplied by a hard-coded 3 |
| `ManualPumpWaterPerSecond` | `2` | `LpexConfig.cs:203` | hand-cranked transfer rate, at 1 atm |
| `FluidIntakeWaterDepth` | `3` | `LpexConfig.cs:206` | the cube edge below the intake that must be water |
| `FluidIntakeExclusionRange` | `6` | `LpexConfig.cs:209` | Euclidean radius that disables a crowded intake |
| `SteamEngineEfficiency` | `0.75` | `LpexConfig.cs:183` | sets a sub-machine's output pressure = inlet × this. Shared with smex's blower - see the warning below |

`PumpWaterPerSecond` carries a config migration: version `0.6.0` force-resets it, from the retune
5 → 16.67 when the pump started scaling off absolute engine power (`LpexConfig.cs:35-40`).

### hard-coded — the undocumented `× 3`

| value | file:line | effect |
|---|---|---|
| `* 3` on the engine fluid pump | `BlockEntityEngineFluidPump.cs:48` | triples every delivered figure below |
| `* 3` on smex's engine air blower | `BlockEntityEngineAirBlower.cs:56` | the identical expression on the identical line |

Neither factor is named, commented, or reachable from config. Both use the shape
`<ConfigRate> * 3 * power * dt` and both make the shipped machine 3× its documented output.

What the config comment claims vs what ships:

| engine | power | `LpexConfig.cs:197-198` says | actually delivered | ratio |
|---|---|---|---|---|
| Watt | 0.3 | 5 L/s | 15.0 L/s | 3× |
| Cornish low | 0.2 | 3.3 L/s | 10.0 L/s | 3× |
| Cornish normal | 0.4 | 6.7 L/s | 20.0 L/s | 3× |
| Cornish high | 0.8 | 13.3 L/s | 40.0 L/s | 3× |

Cornish powers from `HpexConfig.cs:89-91`.

Every downstream statement of the number is therefore wrong by 3×:

| source | claim | truth |
|---|---|---|
| `LpexConfig.cs:197-198` (the doc comment on the key itself) | "Watt 0.3 → 5 L/s, Cornish 0.2/0.4/0.8 → 3.3/6.7/13.3" | 15 / 10 / 20 / 40 |
| `docs/lpex/handbook/02-engines.html:22` | "about 5 L/s on a fully-powered engine" | ~15 L/s |
| `docs/lpex/handbook/02-engines.html:27` | air blower "roughly 16 L/s" | 48 × 3 × 0.3 = 43.2 L/s (`SmexConfig.cs:113`) |
| `SmexConfig.cs:110-112` | "Cornish 0.2/0.4/0.8 → 9.6/19.2/38.4, Watt 0.3 → 14.4" | 28.8 / 57.6 / 115.2 / 43.2 |

### Other hard-coded values

| value | file:line | what it does |
|---|---|---|
| intake head `1f` atm | `BlockEntityFluidIntake.cs:49` | the source line's `FeedPressure`; there is no config key |
| manual delivery `1f` atm | `BlockEntityManualFluidPump.cs:147` | the hand-cranked head |
| delivered water temperature `20f` °C | `BlockEntityEngineFluidPump.cs:53`, `:55`; `BlockEntityManualFluidPump.cs:147`, `:148` | four literals; the pump never carries the pond's temperature |
| intake rescan `1000 ms` | `BlockEntityFluidIntake.cs:58` | validity poll |
| production tick `1000 ms`, `dt` clamp `2 s` | `BlockEntityProductionMachine.cs:26`, `:75`, `:126` | the engine pump's beat |
| manual server tick `1000 ms` | `BlockEntityManualFluidPump.cs:67` | |
| manual client tick `250 ms` | `BlockEntityManualFluidPump.cs:74` | animation + sound mirror |
| crank watchdog `1200 ms` | `BlockEntityManualFluidPump.cs:117` | stale-hold cutoff |
| ice tolerance: top-layer outer cells only | `BlockEntityFluidIntake.cs:94-96` | `dy == -1 && (dx != 0 \|\| dz != 0)` |
| manual pump filler `(0,1,0)` | `BlockManualFluidPump.cs:54` | the crank cell above |
| sub-machine offset `(0,0,2)` | `BlockEntityEngineSubmachine.cs:140` | inverted to find the master engine |

### Derived — what the chain actually sustains

| quantity | value | from |
|---|---|---|
| Watt-driven pump, steady state | 15.0 L/s | `16.67 × 3 × 0.3` |
| Manual pump, steady state | 2.0 L/s | `LpexConfig.cs:203` |
| Boiler's maximum feed draw | 10 L/s | `BoilerWaterIntakeRate`, `LpexConfig.cs:95` ([Cornish boiler](boiler-cornish.md)) |
| Boiler auto-fill ceiling (Cornish) | 800 × 0.5 = 400 L | `LpexConfig.cs:91`, `:143` ([Cornish boiler](boiler-cornish.md)) |

One Watt-driven pump over-serves a boiler by 50 % (15 vs 10), and the hand crank supplies a fifth of the
boiler's maximum draw, enough to prime it. The 3× bug therefore does not break the feed loop; it makes the
pump's cost/benefit meaningless.

### Delivery pressure

| pump | delivery pressure | file:line |
|---|---|---|
| engine fluid pump | `Engine.InletPressure × 0.75` | `BlockEntityEngineFluidPump.cs:46-47` |
| manual fluid pump | fixed `1.0` | `BlockEntityManualFluidPump.cs:147` |
| fluid intake (source side) | fixed `1.0` | `BlockEntityFluidIntake.cs:49` |

That pressure is realised only once the delivery line is brim-full; below capacity a liquid run's pressure
tracks its fill ratio ([pipe network](../mechanics/pipe-network.md) § 3). The only consumer that reads
it is the boiler's steam boost (`BlockEntityBoiler.cs:342-349`).

---

## Pumps must become pressure multipliers

Settled 2026-07-24, and nothing of it is built.

The bug: `delivery = InletPressure × SteamEngineEfficiency` with 0.75 is a pressure reducer, and flow
is independent of pressure. A round trip through a heat engine therefore always arrives smaller, at every
operating point. A steam pump can never feed the boiler driving it.

It does not bite today only because the boiler's feed has no pressure gate at all: `BlockEntityBoiler`
draws whatever the line holds and rewards pressure above 1 atm with extra flash steam
(`BlockEntityBoiler.cs:326-350`). The gate the feedwater design adds - draw only while
`feedPressure ≥ InternalPressure`, ramped over `BoilerFeedFullFlowMargin` - is what turns the reducer into a
hard stop.

The fix: a feed pump is a big steam piston on a small water plunger, so delivery pressure is the area
ratio and efficiency is paid in flow.

```
delivery = InletPressure × PumpPressureRatio      — ratio > 1 (piston ÷ plunger area)
flow     = power_budget ÷ delivery                — constant power, as the MP generator already does
```

> Add a separate `PumpPressureRatio`. Do not re-scale `SteamEngineEfficiency`: smex's air blower
> reads the identical expression for blast pressure (`BlockEntityEngineAirBlower.cs:54-55`), which has to stay
> between the blast floor and the pipe burst rating. Changing 0.75 moves the blast gate.

Two consequences fall out of machinery that already exists:

- It is a flow contest, not only a pressure one. A liquid run reports its commanded pressure only once
  brim-full, and every draw recomputes it (`PipeNetworkState.ComputeLiquidPressure`,
  [pipe network](../mechanics/pipe-network.md)). A pump that cannot refill as fast as the boiler empties
  watches its own delivery pressure sag.
- The same constant-power relation governs the mechanical MP pump. Only the power source differs.

The `× 3` above is not this fix: it scales flow, not pressure, and it is applied unconditionally at both
call sites.

---

## The feed roster

| device | costs | works when | tier | state |
|---|---|---|---|---|
| Manual fluid pump | player time (a held right-click) | cold only, 1 atm | priming | live |
| Mechanical MP pump | MP | the wheel turns - fire lit or not | LP | designed, art only |
| Engine fluid pump | steam + the engine's one sub-machine slot | the engine is turning | LP / HP | live |
| Injector | live steam, no moving parts | any steam pressure | LP / HP | designed, not drawn |

Both the MP pump and the injector feed a boiler with no engine dedicated to it.

Under the settled power progression rule (`STATE.md:538-546`), at iron tier a vanilla waterwheel or windmill
is bridged into mpenergy, and in lpex the player replaces that producer with a steam engine. A pump that runs
off the line shaft is therefore the device that lets a water-powered iron shop raise its first boiler without
a bootstrap.

---

## Drops

| block | drops | file:line |
|---|---|---|
| fluid intake | itself; no `NoDrops`, no `GetDrops` override, no inventory | `BlockFluidIntake.cs:20-35` |
| engine fluid pump | itself; plain block drops | `BlockEngineFluidPump.cs:22-35` |
| manual fluid pump | itself; fillers cleared before `base.OnBlockBroken` so no invisible solid cell survives | `BlockManualFluidPump.cs:110-123` |

None of the three is a right-click construction, so `RccBrokenDropsRatio` (`LpexConfig.cs:116`) does not
apply - salvage is 1:1.

---

## Code

| piece | file:line |
|---|---|
| `BlockFluidIntake : BlockNetworkNode, IExBlockDefProvider` | `BlockNetworkPipe/Blocks/BlockFluidIntake.cs:12` - def `:17-35`, `GetFallbackOrientation` `:39`, `IsFullCube` `:46`, `TryPlaceBlock` `:52-79`, `OnNeighbourBlockChange` `:86-96` |
| `BlockEntityFluidIntake : BlockEntityNetworkNode` | `BlockNetworkPipe/BlockEntities/BlockEntityFluidIntake.cs:21` - `CanIntake` `:34`, `ProduceWater` `:41-50`, `Rescan` `:63-73`, `ScanWaterBelow` `:80-100`, `HasNearbyIntake` `:103-124`, HUD `:126-136` |
| `FluidPumpCore` | `BlockNetworkPipe/FluidPumpCore.cs:16` - `FindIntake` `:19-34`, `OutputFreeCapacity` `:37-40` |
| `BlockEngineFluidPump : BlockEngineSubmachine, INetworkConnector` | `BlockStructures/Engine/Blocks/BlockEngineFluidPump.cs:16` - def `:22-35`, `HasConnectorAt` `:45-46` |
| `BlockEntityEngineFluidPump : BlockEntityEngineSubmachine` | `BlockStructures/Engine/BlockEntities/BlockEntityEngineFluidPump.cs:21` - `DoWork` `:29-56`, `SetDrawing` `:59-65`, water loop `:71-90` |
| `BlockEntityEngineSubmachine : BlockEntityProductionMachine` | `BlockStructures/Engine/BlockEntityEngineSubmachine.cs:23` - `Engine` `:41-53`, `PowerDemand` `:59`, `FindEngine` `:131-154`, `CanRunProduction` `:160`, `OnProductionTick` `:162-163`, `LeftFace` `:297-301` |
| `BlockEngineSubmachine` (placement snap) | `BlockStructures/Engine/BlockEngineSubmachine.cs:12-51` |
| `BlockManualFluidPump : Block, INetworkConnector, IFillerInteractionTarget, IFillerHost` | `BlockStructures/ManualPump/Blocks/BlockManualFluidPump.cs:24` - def `:43-57`, faces `:65-73`, filler triad `:77-123`, crank forwarding `:129-224` |
| `BlockEntityManualFluidPump : BlockEntity` | `BlockStructures/ManualPump/BlockEntities/BlockEntityManualFluidPump.cs:26` - faces `:51-56`, `OnPumpStart/Step/Stop` `:81-95`, watchdog `:111-124`, `DoWork` `:131-156`, anim `:182-236`, sounds `:242-276` |
| `PipeNetwork.ProduceLiquidMeasured` / `TryProduceLiquid` / `TryConsumeLiquid` | `ExpandedLib/Networks/PipeNetwork.cs:284`, `:231`, `:300` |
| boiler feed draw (the consumer) | `BlockStructures/Boiler/BlockEntityBoiler.cs:326-350` |
| smex air blower (the `× 3` twin) | `SteelmakingExpanded/BlockStructures/Engine/BlockEntities/BlockEntityEngineAirBlower.cs:54-56` |

### Where a caller hooks in

- **Adding a pump**: implement transfer-then-refill. Call `TryConsumeLiquid` on the source run, then
  `TryProduceLiquid(drawn, temp, yourPressure)` on the delivery run, then `intake.ProduceWater(amount, …)`.
  Reversing steps 1 and 3 mislabels the source pool as "Air" at broadcast time.
- **Adding an engine sub-machine**: derive `BlockEntityEngineSubmachine`, supply `DoWork(power, dt)`, and give
  the def an `Animatable` behaviour with `idle` + `cycle` clips (`BlockEntityEngineSubmachine.cs:14-22`).
- **Adding a non-engine pump (MP, hand, injector)**: do not derive the sub-machine base - it hard-binds to
  `BlockEntityEngine`. Copy the manual pump's shape: a plain `BlockEntity` + `INetworkConnector` block, its
  own tick, `FluidPumpCore` for the shared half.
- **Changing the delivery-pressure model**: `BlockEntityEngineFluidPump.cs:46-47` is the single site for the
  pump; `BlockEntityEngineAirBlower.cs:54-55` is the identical expression for the blower and must be
  considered at the same time.

### Tests

| file | what it pins |
|---|---|
| `Blocks/FluidIntake/FluidIntakeBeTests.cs` | full-cube scan, dry cube blocks, nearby intake ⇒ crowded, `ProduceWater` feeds / draws nothing when not intakeable, tree round-trip |
| `Blocks/ManualPump/ManualPumpBeTests.cs` | crank toggle, watchdog stops a crank whose release was missed, `DoWork` moves standing water, no-lines moves nothing, run state round-trip |
| `Scenarios/SteamPlantScenarioTests.cs:30`, `:50`, `:64`, `:82` | steam drives the pump to lift water; no steam ⇒ idle; below engage ⇒ no drive; a fired boiler charges the main and runs an attached pump |
| `Scenarios/SteamSupplyScenarioTests.cs:73`, `:90` | cranking lifts pond water into the output main; an uncranked pump moves nothing |
| `Fixtures/EngineFixture.cs`, `SteamPlantScenes.cs`, `SteamSupplyScenes.cs` | the rigs |

No test asserts a pump's delivered rate as a number. Every pump test checks "water moved" or "no water
moved", which is how a `× 3` sat at `BlockEntityEngineFluidPump.cs:48` undetected while the config comment
beside it said 5 L/s.

---

## Gotchas

1. **The `× 3`** (`BlockEntityEngineFluidPump.cs:48`, `BlockEntityEngineAirBlower.cs:56`). Undocumented,
   unconfigurable, applied at both sub-machine call sites, and contradicted by the config comment on the very
   key it multiplies.

2. **`PowerDemand` is always 1 while an engine exists** (`BlockEntityEngineSubmachine.cs:59`; the pump does
   not override it). The engine reads it to decide how much steam to burn
   (`BlockEntityEngine.cs:279-288`), so a pump with no intake, or with a full delivery line, still burns
   the engine's full 30 L/s of steam. `SetDrawing(false)` only silences the water loop.

3. **`DoWork` returns before `SetDrawing` on zero power** but after it on a missing intake
   (`BlockEntityEngineFluidPump.cs:31-44`), so a stalled engine and a dry intake produce the same client
   state by different paths. The `_drawingWater` flag cannot distinguish them.

4. **The pump refills the intake by `amount`, not by `drawn`** (`BlockEntityEngineFluidPump.cs:55`,
   `BlockEntityManualFluidPump.cs:148`). When the delivery line is full, `move` is clamped to 0 but the
   intake is still asked for the full rate - harmless (`ProduceLiquidMeasured` clamps at the source
   run's own `MaxVolume`) but it means the source line silently tops itself off even while nothing is being
   delivered.

5. **`FindIntake` returns the first intake in node order, not the nearest** (`FluidPumpCore.cs:26-33`).
   With several intakes on one source run, which one the HUD shows as drawing is an artefact of graph
   iteration order and can change after any re-walk.

6. **Delivered water is always 20 °C.** Four literals (`BlockEntityEngineFluidPump.cs:53`, `:55`;
   `BlockEntityManualFluidPump.cs:147`, `:148`). A pond in a frozen biome and one in a desert feed a boiler
   identically, and the condenser's hot output cannot be distinguished from fresh cold water by temperature
   once it is in a line.

7. **The intake is not an `IPipeNode`.** Its BE is a bare `BlockEntityNetworkNode`
   (`BlockEntityFluidIntake.cs:21`), so `ClassifyOpenings` never counts it as a consumer - the only
   configuration in which the network's passive-cooling pass can fire at all
   ([pipe network](../mechanics/pipe-network.md) Gotcha 1).

8. **The intake never self-breaks.** Water is not an attachable surface, so the base self-break would destroy
   a freshly placed intake; losing the pond merely disables it (`BlockFluidIntake.cs:81-96`). It also means a
   drained pond leaves a working-looking intake standing.

9. **The manual pump is not wrench-orientable** (`BlockManualFluidPump.cs:18`). To reverse it,
   break and re-place. The crank-support side is the input side - the only in-world cue, and it is stated
   only in the handbook (`docs/lpex/handbook/03-fittings.html:22-23`).

10. **The manual pump forwards every interaction phase from its filler** - start, step, stop and the
    interaction help all have an `IFillerInteractionTarget` twin (`BlockManualFluidPump.cs:137-215`). Miss one
    and cranking from the upper cell half-works.

11. **`HandleStart` returns `null` to defer, not to refuse** (`BlockManualFluidPump.cs:150-166`): a held
    item falls through to the default behaviour so a wrench still works on the block.

12. **`src/LowPressureExpanded/README.md:42-43` lists `Commands/` and `Preferences/` directories that do not
    exist** - the measure feature moved to exlib (`LowPressureExpandedModSystem.cs:93-96`). The same
    README's machine list also omits the manual pump from "what it adds" while the code ships it.

13. **`LpexConfig.cs:223` documents `/exmod steam <level>`. There is no such command** - the recipe level is
    switched by the generic `/exmod recipes lpex <level>` (`LowPressureExpandedModSystem.cs:79-81`). The same
    stale string is repeated at `LpexRecipeConfig.cs:13`.

14. **The engine pump's source face is `DOWN`** (`BlockEngineFluidPump.cs:46`), i.e. the intake main must run
    under the sub-machine. Nothing in the block's name, shape or lang says so; the handbook does not
    mention it either.

---

## Open

- **Ship the pressure-multiplier fix.** Add `PumpPressureRatio`, switch the pump to
  `delivery = inlet × ratio`, `flow = budget ÷ delivery`, and gate the boiler intake on `InternalPressure`.
  Leave `SteamEngineEfficiency` alone - smex's blast pressure reads it.
- **Delete or config-ify the `× 3`.** Whichever way it is resolved, `PumpWaterPerSecond` and its doc
  comment, `AirBlowerOutputPerSecond` (`SmexConfig.cs:110-113`) and two handbook pages have to move together.
  Nothing currently asserts any of these numbers, so nothing will catch the next drift either; a rate test
  is the cheap half of this fix.
- **Build the mechanical MP pump.** The art is drawn and unwired; it is the device the settled power
  progression leans on, and the only feed that works with the fire out and no engine spare.
- **Build the injector.** Fully specified (§ Structure) and not drawn. Its defining property - it
  delivers above the pressure of the steam driving it, and refuses hot feedwater - has no analogue in the
  current code and needs the pressure model above to exist first.
- **Nothing carries water temperature.** Four hard-coded 20 °C literals stand between the condenser's hot
  output and the injector's "won't pick up on hot feedwater" rule, which cannot be implemented until a run's
  water has a real temperature at the pump boundary.
- **`FindIntake` should probably prefer the nearest intake**, or the pump should aggregate several.
- **The pump has no HUD of its own.** The engine pump reports nothing; the manual pump reports nothing. All
  feedback is the two sound loops and the animation, which is thin under R7 ("nothing is hidden") - compare
  the pressure valve, which prints its gate and its live overflow.
