# Fluid Tank / Cistern
**Status** designed - no block, no block entity, no shape, no recipe, no config key. Nothing of it exists in `src/`   **Mod** iiex

**Owns**
- The tank's purpose and shape as a design: a medium-agnostic bulk buffer on the pipe network, and the one
  reason it survived the metalworking-only scope cut when the rest of the domestic complex did not.
- The capacity problem that blocks it: `PipeNetwork` computes `MaxVolume` as `Nodes.Count × LitresPerPipe`
  at ten sites in exlib and five more in content mods, so a "high-capacity node" is not expressible today.
  The three build routes out of that, and which one this page recommends.
- The proposed numbers (all unbuilt, all marked as proposals) and the anchors in shipped code they are
  derived from.
- Where a tank would hook into existing code, and which existing block is its structural template.

**Does not own** - cited only, never restated:
- The graph, the one-medium-per-run rule, `MaxVolume`, gas vs liquid pressure, `FeedPressure`, leaks,
  bursts, merge/split and the tick order - [pipe network](../mechanics/pipe-network.md).
- Pipe blocks, valves, the pressure valve, joints, the cast tier's 5 atm rating - [cast pipes](cast-pipes.md).
- The intake, both live pumps, the delivery-pressure formula and the pressure-multiplier fix -
  [pumps](pumps.md).
- The boiler's feed draw, `InternalPressure`, the boiler-feedwater design, the injector, the steam condenser
  - [Cornish boiler](boiler-cornish.md) and [pumps](pumps.md).
- The scope decision that deferred the gasholder, the sprinkler and the chemical complex to Industrial
  Homestead - `../overview.md` § Scope, restated nowhere else.
- Code-first defs, RCC stages, the recipe-cost catalogue - [recipes-config](../mechanics/recipes-config.md).
- Fillers and footprints - [multiblock](../mechanics/multiblock.md). Build-cost masses -
  [density rule](../mechanics/density-rule.md). Break-return policy - [recoverability](../mechanics/recoverability.md).

---

## Role

A pipe run holds thirty litres per cell and nothing else ([pipe network](../mechanics/pipe-network.md) § 3).
That is enough for moving a medium and not for holding one, so everything on the network today has to be
simultaneous: the pump must run while the boiler drinks, the blower must turn while the furnace breathes.
There is no way to fill something now and spend it later.

The tank is the block that decouples supply from demand. Its stated job is:

> a medium-agnostic high-capacity storage node on the pipe network; buffers pumped water so downstream draws
> don't need the pump running continuously — a boiler-feed reservoir

Two things make it worth building:

1. It is the natural consumer of the pressure-multiplier fix. Once a boiler's intake gates on
   `feedPressure ≥ InternalPressure` ([boiler-cornish](boiler-cornish.md)), feeding becomes a flow contest:
   a device that cannot refill the line as fast as the boiler empties it watches its own delivery pressure
   sag. A reservoir between pump and boiler is a hotwell, and it turns an intermittent feed (a hand crank, a
   windmill in a lull) into a continuous one.
2. It is the one block from the deferred Domestic complex that metalworking still needs. The gasworks, the
   still, the gas lamps, the radiators and the ammonia loop all went to Industrial Homestead; the tank, the
   condenser and the general phase-change model stayed, because the tank is plumbing: a shop with a Cornish
   boiler wants a buffer between its pump and its feed.

Medium-agnostic is load-bearing. The same block is the gasholder of the deferred domestic complex ("the
core-iiex medium-agnostic storage node holding coal gas") and would be the buffer for producer gas at the
open hearth. Building it as a water tank forecloses both.

---

## Structure *(proposed — nothing is built)*

### The blocking problem: capacity is per-node and uniform

`PipeNetworkState.MaxVolume` is documented as "`LitresPerPipe` per pipe node" (`PipeNetworkState.cs:17`)
and is recomputed from scratch on every path that touches the pool:

| site | file:line |
|---|---|
| gas production | `PipeNetwork.cs:112` |
| liquid production | `PipeNetwork.cs:245` |
| merge (four branches) | `PipeNetwork.cs:351`, `:358`, `:366`, `:374` |
| split fragment | `PipeNetwork.cs:449` |
| per-tick recompute | `PipeNetwork.cs:541` |

and five more places re-derive the same expression independently rather than reading `State.MaxVolume`:

| site | file:line | what it is doing |
|---|---|---|
| `FluidPumpCore.OutputFreeCapacity` | `FluidPumpCore.cs:40` | how much a delivery line can still take ([pumps](pumps.md)) |
| pressure valve, gas branch | `BlockEntityPressureValve.cs:163` | the output run's ceiling ([cast pipes](cast-pipes.md)) |
| pressure valve, liquid branch | `BlockEntityPressureValve.cs:247` | the output run's free space |
| steam condenser | `BlockEntitySteamCondenser.cs:181` | through-flow headroom |
| boiler steam push | `BlockEntityBoiler.cs:561` | fallback when the run has no state yet |

There is no such thing as a node with its own capacity. Making the tank a graph node that contributes 4000 L
means changing all fifteen sites to sum a per-node capacity, and then auditing every burst, pressure, merge
and split path that assumes the pool scales with the node count. `PoolVolumeCeiling`
(`PipeNetwork.cs:337-341`) multiplies `maxVolume` by the weakest burst rating, so a large-capacity node
would also multiply the run's gas over-pressure headroom, which is almost certainly not intended.

This is why the tank has never been built. It is not a "write the block" task.

### Three routes, and the recommendation

| route | what it means | cost | verdict |
|---|---|---|---|
| A - capacity-bearing graph node | add `INetworkCapacity` (or read a per-node litres value) and sum it; fix all 15 sites | changes exlib's core pool model; touches burst, merge, split, and every content-mod re-derivation | highest risk, and the burst-headroom side effect has to be designed, not patched |
| B - connector with its own tank | the tank is not a graph node. It is an `INetworkConnector` holding its own `float _volume` + `MediumType`, ticking once a second: pull from the fuller side, push to the emptier | zero exlib change. Every mechanism already exists | recommended |
| C - a very long dead-end run | no new block; the player just builds 120 pipes | free | rejected: 120 cells for 3600 L is absurd, and a dead-end run is one open connector away from leaking itself dry |

Route B's template already ships twice. The steam condenser is this shape - "a tjunction-shaped fixed port
(not a network node) … the three adjacent runs stay separate networks (it's a connector, not a node); the BE
bridges the two water sides itself" (`BlockSteamCondenser.cs:12-20`) - and the planned injector is specified
the same way and for the same reason: steam and water cannot share a pool, so a device touching both must
keep them separate. A tank is the condenser's chassis with a reservoir instead of a phase change.

Route B also gets the medium-agnostic requirement for free: the tank stores a `(MediumType, Volume,
Temperature)` triple of its own and consults `ExLiquids.Taxonomy` for compatibility exactly as
`PipeNetwork` does - one medium at a time, claimed by the first thing that fills it
([pipe network](../mechanics/pipe-network.md) § 2).

### Proposed footprint

| | proposed |
|---|---|
| Form | a megablock - a standing riveted cylinder, 2 × 2 × 2 or 2 × 3 × 2 |
| Principal | bottom-front cell; the rest invisible fillers ([multiblock](../mechanics/multiblock.md)) |
| Connectors | two, on opposite horizontal faces of the principal - in / out, like the condenser's W↔E water line, so a tank drops into an existing run without re-plumbing |
| Orientation | `HorizontalOrientable` + `VariantGroupFromProperties("side", …)`, the condenser's and the pumps' convention (`BlockSteamCondenser.cs:37-38`) |
| Graph role | none. `INetworkConnector` only, `NetworkType => "pipe"` |

Open: whether it is one block or a stackable course (the "multiply, don't enlarge" pattern the cupola, the
nail benches and the crucible holes all use - `STATE.md:363-368`). A tank that scales by stacking courses
makes capacity a build decision rather than a config number. See [Open](#open).

---

## Assets *(none exist)*

| asset | path | state |
|---|---|---|
| Editable shape | — | not drawn. No candidate under `workbench/shapes/` - the two `molten-block-moltenbarrel*.json` shapes are iiex molten-network barrels, not this |
| Runtime shape | — | none |
| Animations | — | none needed; a fill-level renderer is wanted instead (see below) |
| Lang | — | no `block-fluidtank*` / `blockdesc-*` key in `mods/iiex/assets/iiex/lang/en.json` |
| Handbook | — | the tank appears in none of the five iiex pages (`mods/iiex/assets/iiex/config/handbook/00…04`) |

It should show its level. `BoilerWaterRenderer` draws the Cornish boiler's water surface between two
configured heights (`BoilerWaterSurfaceLowLevel` / `HighLevel`, `IiexConfig.cs:128`, `:134`), and exlib's
shared liquid-surface renderer exists for exactly this - `Renderers/SurfaceRenderer`, "a liquid line inside
a block - water tanks, molten canals" (`exlib/wiki/Helpers-and-Renderers.md:149-151`). Under R7 ("nothing is
hidden") a buffer whose whole purpose is "how much do I have banked" must not be readable only from
block-info.

Compare the [tall hopper](tall-hopper.md), which has no visible fill and is called out for it. Do not
repeat that.

---

## Construction *(proposed)*

There is no recipe, and there is no cost-catalogue key (`IiexRecipeConfig.cs:59-86` lists ten iiex entries;
none is a tank).

Proposed, following the pattern the boiler and engine already use:

- Right-click construction (RCC), not a grid recipe. It is a plate vessel of the same class as a boiler
  shell, and the RCC path already resolves salvage through `RccBrokenDropsRatio`
  (`IiexConfig.cs:116`, default 0.8) and the shared `ExRccSettings` registration
  (`IronIndustryExpandedModSystem.cs:29-32`).
- Built from plate + rivets, the settled fabricated-part idiom (`STATE.md:417-442`): rivets are the
  ingredient, so no riveting machine is required, and the rivet die ships with iiex.
- A cost key `fluidtank-rcc` in `IiexRecipeConfig.Defaults()` so `/exmod recipes iiex <level>` reaches it.

This makes the tank a rivet consumer. Rivets are the steam tier's fastener because a riveted joint is strong
and tight (`STATE.md:486-496`); a water cistern under a couple of atmospheres is a riveted vessel and gives
the die a second customer after the boiler.

---

## Operation *(proposed)*

```
   in-run  ──▶ ┌──────────────┐ ──▶ out-run
               │  TANK        │
               │  medium      │   once a second, server-side:
               │  volume      │   1. resolve both runs through GetConnectedNetworkAcross
               │  temperature │   2. compatible medium?  (ExLiquids.Taxonomy, else do nothing)
               └──────────────┘   3. draw min(rate, in-run volume, free space)   → store
                                  4. push min(rate, stored, out-run free space)  → out-run
```

Everything in that loop already exists as a method:

| step | existing call | file:line |
|---|---|---|
| resolve a run across a connector face | `be.ConnectedNetwork<PipeNetwork>(face)` | `MachinePorts.cs:15` ([pipe network](../mechanics/pipe-network.md)) |
| medium compatibility | `ExLiquids.Taxonomy.Compatible(a, b)` | `ExLiquids.cs:106-116` |
| draw water / gas | `TryConsumeLiquid` / `TryConsumeGas` | `PipeNetwork.cs:300`, `:200` |
| push water / gas, measured | `ProduceLiquidMeasured` / `ProduceGasMeasured` | `PipeNetwork.cs:284`, `:175` |
| free space on the far run | the `Nodes.Count × LitresPerPipe − Volume` expression | `FluidPumpCore.cs:37-40` |

Pressure. A liquid pushed out of the tank has to be given a `setPressure`
(`PipeNetwork.TryProduceLiquid`, `:231-248`). Proposed: the tank is a gravity head, so it delivers at
`1 atm` - the same value the fluid intake and the manual pump use
(`BlockEntityFluidIntake.cs:49`, `BlockEntityManualFluidPump.cs:147`), and it is why the tank does not
replace a pump. Feeding a pressurised boiler still needs a pump or an injector downstream of the tank; the
tank only guarantees that device never runs dry. A tank that preserved the inlet pressure would let a player
pressurise once and coast.

Gas. For a gas the same loop works, but the push must be clamped by a `maxOutputPressure` or the tank
becomes an infinite compressor. Proposed: the tank pushes at 1 atm for gas too - a gasholder bell, not a
receiver. A pressurised gas receiver is a different machine.

---

## Numbers *(all proposed — none exists in code)*

| quantity | proposed | anchored on | file:line of the anchor |
|---|---|---|---|
| Capacity | 4000 L | a Cornish boiler drawing at its maximum feed rate for one full heat-up: `20 L/s × 180 s = 3600 L`, plus margin | `BoilerWaterIntakeRate` `IiexConfig.cs:1011`; `BoilerHeatUpSeconds` `IiexConfig.cs:997` |
| Transfer rate, each side | 30 L/s | above the boiler's 20 L/s draw and above a Watt-driven pump's real 15 L/s output, so the tank is never the bottleneck. It has to clear the draw, not match it: a tank transferring at exactly the boiler's rate is the bottleneck it was built to remove | `IiexConfig.cs:1011`; [pumps](pumps.md) |
| Delivery pressure | 1 atm, fixed | matches the intake's and the manual pump's gravity head | `BlockEntityFluidIntake.cs:49`, `BlockEntityManualFluidPump.cs:147` |
| Tick period | 1000 ms, server-side | every other fitting and machine in iiex | `BlockEntitySteamCondenser.cs:42`, `BlockEntityPressureValve.cs:54` |
| Config key | `FluidTankCapacity`, section `iiex` | — | would join `IiexConfig.cs` § Storage |

### What 4000 L buys, against shipped numbers

| scenario | result |
|---|---|
| Cornish boiler at maximum feed draw, pump stopped | 4000 / 20 = 200 s unattended |
| Cornish boiler auto-fill ceiling | 1600 × 0.5 = 800 L → the tank holds 5 fills (`IiexConfig.cs:1008`, `:1059`) |
| Filling the tank from a Watt-driven pump | 4000 / 15 = 267 s ([pumps](pumps.md)) |
| Filling it by hand | 4000 / 2 = 2000 s - deliberately not a hand job (`ManualPumpWaterPerSecond`, `IiexConfig.cs:1119`) |
| Equivalent in pipe cells | 4000 / 30 = 133 pipes (`LitresPerPipe`, `ExlibConfig.cs:52`) |

### Build cost *(proposed)*

Not derivable yet - the tank has no drawn shape, and every mass in the suite comes from a drawn shape
through 1 vx³ = 2.5 u ([density rule](../mechanics/density-rule.md)). Draw the vessel first, then the plate
count follows; do not pick a number and back-fill the art.

---

## Drops *(proposed)*

- RCC salvage at `RccBrokenDropsRatio` (0.8 by default, `IiexConfig.cs:116`), like the boiler and engine.
- The contents are lost. Water and gas are not items, nothing in the suite drops a medium, and the pipe
  network already discards content silently on an incompatible merge
  ([pipe network](../mechanics/pipe-network.md) Gotcha 14). Under
  [recoverability](../mechanics/recoverability.md)'s declared recovery this is fine, but it must be
  declared: the block-info line should say so, and the break should at minimum spill particles rather than
  vanishing 4000 L in silence.

Open: whether breaking a full tank should be refused, or should splash. A 4000 L vessel evaporating on a
mis-click is the kind of silent loss R7 exists to prevent.

---

## Code — where it will hook in

Nothing exists. The files a tank would add, and the shipped file each one copies:

| new piece | template to copy | file:line |
|---|---|---|
| `BlockFluidTank : Block, INetworkConnector, IExBlockDefProvider` | `BlockSteamCondenser` - connector, not node; horizontal orientation; rotated collision boxes | `BlockNetworkPipe/Blocks/BlockSteamCondenser.cs:20`, def `:26-53`, `GetCollisionBoxes` `:62-66` |
| `BlockEntityFluidTank : BlockEntity` | `BlockEntitySteamCondenser` - 1000 ms server tick, `ConnectedNetwork(face)` per side, synced display flag | `BlockNetworkPipe/BlockEntities/BlockEntitySteamCondenser.cs:23`, `:34-35`, `:37-44` |
| filler footprint (if 2 cells or more) | `BlockManualFluidPump` - the hand-rolled `IFillerHost` triad | `BlockStructures/ManualPump/Blocks/BlockManualFluidPump.cs:54`, `:77-123` |
| fill-level renderer | `BoilerWaterRenderer` + exlib's `Renderers/SurfaceRenderer` | `BlockStructures/Boiler/BoilerWaterRenderer.cs`; `exlib/wiki/Helpers-and-Renderers.md:149-151` |
| `FluidTankCapacity` config key | `IiexConfig` § Storage (new region) | `IiexConfig.cs` |
| `fluidtank-rcc` cost entry | `IiexRecipeConfig.Defaults()` | `IiexRecipeConfig.cs:59-86` |
| RCC stages | the Cornish boiler / Watt engine construction stages | [recipes-config](../mechanics/recipes-config.md) |
| tests | `Blocks/Condenser/CondenserBeTests.cs` is the closest shape (a connector BE bridging two runs) | `mods/iiex/tests/Blocks/Condenser/CondenserBeTests.cs` |

### The one thing that has no template

Persisting the stored medium. Every other connector in iiex is stateless between ticks - the condenser holds
nothing, the pressure valve holds only a gate setting. The tank is the first block that owns a pool outside
a network, so it needs its own `ToTreeAttributes` / `FromTreeAttributes` for
`(MediumType, Volume, Temperature)`, and it must survive the same trap the valve hit: a cached pool that
serialises and is restored into a context that cannot hold it
([pipe network](../mechanics/pipe-network.md) § 7 and Gotcha 9, `BlockEntityValve.cs:162-166`, `:222-225`).
Clamp the loaded volume to the live capacity on `Initialize`, exactly as the pressure valve clamps its gate
against a possibly-reconfigured rating (`BlockEntityPressureValve.cs:47-52`).

---

## Gotchas — the traps waiting for whoever builds it

1. Do not make it a graph node without doing route A properly. `MaxVolume` is `Nodes.Count × LitresPerPipe`
   at fifteen sites (listed in [Structure](#structure-proposed--nothing-is-built)), and a node that
   contributed extra capacity would also multiply the run's gas burst headroom through `PoolVolumeCeiling`
   (`PipeNetwork.cs:337-341`).

2. A tank on a gas run must clamp its output pressure. `TryProduceGas` fills up to
   `min(maxOutputPressure, MinBurstPressure) × MaxVolume` (`PipeNetwork.cs:118-125`); a tank that passes
   `float.MaxValue` would drive an attached run straight to its pipes' burst rating.

3. One medium at a time, and the guard must be on volume, not on the label. `PipeNetwork` refuses an
   incompatible medium only while `Volume > 0` (`:110`, `:243`), because a drained run keeps its label as a
   display ghost for `EmptyClearDelaySeconds`. A tank that latched on the label would refuse to be re-filled
   with anything else after being emptied.

4. The connector must be reciprocal. `GetConnectedNetworkAcross` returns a run only when the block over
   there presents a connector back (`BlockNetworkModSystem.cs:67-79`). A pipe merely sitting adjacent is
   not plumbed in - the same rule every iiex machine port already lives by.

5. A connector face with nothing across it is an open connector, not automatically a leak.
   `ClassifyOpenings` counts a face as a leak only when the neighbour is air (`PipeNetwork.cs:632`). A tank
   butted against a wall is sealed; a tank with a bare face on the run side makes that run leak, not the
   tank.

6. Do not give it a pressure ceiling by accident. If the tank ever becomes a `BlockPipe` subclass to get the
   fitting look, it inherits `CanBurst == false` but also the domain's joint family, and would then refuse a
   welded hpex run exactly as the pressure valve does ([cast pipes](cast-pipes.md) § B6). A plain `Block` +
   `INetworkConnector`, like the condenser, has no joint family and couples to every tier.

7. `OutputFreeCapacity` and its four cousins re-derive the pool ceiling independently
   ([Structure](#structure-proposed--nothing-is-built)). Any tank that changes how capacity is computed has
   to change all five, and none of them is covered by a test that would notice.

---

## Open

- Decide route A vs route B. This page recommends B (connector with its own tank, zero exlib change). Route
  A is the only one that makes a tank raise a run's pressure ceiling, which nothing has asked for. Nothing
  else in the design can proceed until this is chosen.
- One block, or a stackable course? The suite's settled pattern is multiply-don't-enlarge - cupola shafts,
  nail benches, crucible holes, smokestack courses (`STATE.md:363-368`). A tank whose capacity is
  `courses × per-course litres` fits that and turns capacity into a build decision. Against it: the
  connector route wants a single principal with two faces, and a stack complicates where those faces live.
- Gravity head only, or pressure-preserving? Proposed above as a fixed 1 atm so the tank never replaces a
  pump. Untested against how it feels in play.
- Does it hold gas in practice? Medium-agnostic is required by the design, but the only gas consumer that
  would want a buffer today is the furnace blast main, and [twin-tub blower](twin-tub-blower.md) already
  sizes its output to one furnace's thirstiest burden. The gas case may be real only once producer gas lands
  at the open hearth.
- Contents on break. Refuse, splash, or silently lose - must be declared either way
  ([recoverability](../mechanics/recoverability.md)).
- No art. Until a vessel is drawn there is no volume, so there is no build cost
  ([density rule](../mechanics/density-rule.md)).
- `../overview.md:161` lists the fluid tank among iiex's content with no build-status marker; the overview
  row should carry one.
