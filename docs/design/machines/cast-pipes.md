# Cast Pipes & Fittings
**Status** live (segments + 5 fittings ship) · **blocked** for the cast segments themselves — no recipe   **Mod** lpex

**Owns**
- The cast tier's identity: which blocktypes lpex contributes to the shared `pipe` network, where their
  ratings and joint family are registered, and what the cast segments are made of (art, textures, stacks).
- Every lpex fitting as a block: plain valve, pressure valve, outlet, passthrough, passthrough-bend -
  their defs, variants, shapes, interactions, recipes and drops.
- B6 in full: why hpex's "mandatory" pressure valve cannot be installed on an HP line, in two
  independent ways (joint family and gate ceiling), with the numbers on both sides.
- The construction gap: which lpex pipe blocks have a grid recipe, which do not, and which recipe-cost
  keys are dead.
- The fact that no cast pipe-part item and no boring machine exist, so the cast tier's intended
  cast → bore → assemble route is entirely unbuilt.

**Does not own** - cited only, never restated:
- The graph substrate, the one-medium pool, capacity, pressure formulas, leaks, vents, bursts, the tick
  order, the plain-valve sever semantics and the pressure-valve overflow arithmetic, and every exlib
  constant behind them - [pipe network](../mechanics/pipe-network.md), canonical for the
  burst-by-tier table and the joint-family rule. This page states only lpex's rows of it and the
  consequences specific to lpex blocks.
- Pumps, the fluid intake, the water chain - [pumps](pumps.md).
- The planned pipe-network buffer - [fluid tank](fluid-tank.md).
- Code-first defs, the recipe-cost catalogue, `/exmod recipes` - [recipes-config](../mechanics/recipes-config.md).
- The boiler, the Watt engine, the steam condenser and their rates - [Cornish boiler](boiler-cornish.md),
  [Watt engine](engine-watt.md).
- Diagrams, the design table, the boring machine as a system -
  [diagram crafting](../mechanics/diagram-crafting.md), [design table](design-table.md),
  [boring machine](boring-machine.md).

---

## Role

The middle rung of a three-rung pipe ladder. iwex ships plated pipe (2.5 atm) so an iron-age shop can
plumb a blast furnace; hpex ships welded rolled pipe (12 atm) for the high-pressure tier. lpex sits between
them at 5.0 atm - enough to carry a Cornish boiler's full choke pressure without bursting, which is the
pressure the Watt engine's supply main has to survive.

The tier is the mod, not a variant axis. There is no `material` variant: a cast pipe is
`lpex:pipe-straight-ns`, a plated one is `iwex:pipe-straight-ns`, and the domain resolves both the burst
rating and the joint family (`ExpandedLib/Blocks/Networks/BlockPipe.cs`). One material per tier, one model
per tier.

lpex's second and larger job is every fitting on the network. iwex ships segments and two machine
fittings (tuyere, blower); hpex ships segments only. Valves, pressure valves, outlets and passthroughs are
lpex-domain blocks, which is why a rolled run has none of them (see [B6](#b6--the-hp-line-cannot-take-the-lpex-pressure-valve)).

---

## Structure

Nothing here is a multiblock. Every block in this page is a single cell.

### The cast segments

lpex contributes four blocktypes by calling the shared factory under its own domain:

```csharp
public class CastPipeDefinitions : IExBlockDefProvider
{
  public static IEnumerable<ExBlockDef> Definitions(string domain) => BlockPipe.Segments(domain);
}
```
— `CastPipeDefinitions.cs:15-19`. It is a stand-alone provider because the class it binds to lives in
iwex: the injected blocktypes name `iwex.BlockPipe` / `iwex.BlockEntityPipe` as their class keys
(`BlockPipe.cs:55-56`), so lpex ships no pipe C# class of its own for the plain segments.

| blocktype | variants | max stack | file:line |
|---|---|---|---|
| `lpex:pipe-straight-*` | `ns` `we` `ud` | 16 | `BlockPipe.cs:80-91` |
| `lpex:pipe-bend-*` | 12 (`nw` … `de`) | 8 | `BlockPipe.cs:94-119` |
| `lpex:pipe-tjunction-*` | 12 | 8 | `BlockPipe.cs:122-148` |
| `lpex:pipe-xjunction-*` | `nswe` `nsud` `weud` | 8 | `BlockPipe.cs:151-164` |

Collision/selection is the shared 5⁄16 → 11⁄16 core (`BlockPipe.cs:89-90`).

### The fittings

| block | class | BE | variants | file:line |
|---|---|---|---|---|
| `pipe-valve-*` | `BlockValve : BlockPipe` | `BlockEntityValve : BlockEntityPipe` | orientation `ns we ud sn ew du` | `BlockValve.cs:19`, `:39` |
| `pipe-pressurevalve-*` | `BlockPressureValve : BlockValve` | `BlockEntityPressureValve : BlockEntityPipe` | same six | `BlockPressureValve.cs:20`, `:39` |
| `pipe-outlet-{brick}-*` | `BlockPipeOutlet : BlockPipe, IChimneyVentable` | `BlockEntityPipeOutlet` (empty) | 8 bricks × orientation `s n w e u d` | `BlockPipeOutlet.cs:18`, `:47-51` |
| `pipe-passthrough-{brick}-*` | `BlockPipePassthrough : BlockPipe, IChimneyVentable` | `BlockEntityPipePassthrough` (empty) | 8 bricks × `ns we ud` | `BlockPipePassthrough.cs:19`, `:74` |
| `pipe-passthroughbend-{brick}-*` | same class | same BE | 8 bricks × 12 orientations | `BlockPipePassthrough.cs:88-93` |

The brick list is `fire black brown cream gray orange red tan` (`BlockPipePassthrough.cs:31-32`,
`BlockPipeOutlet.cs:49`). Both brick fittings are `SideSolid(true)` so a wall can be built through them;
both segments and valves are `SideSolid(false)`.

**Connector geometry.** Segments and valves take the default: `Orientation.Contains(face.Code[0])`
(`BlockNetworkNode.cs:768-769`). The outlet overrides to a single face -
`Orientation.EndsWith(face.Code[0])` (`BlockPipeOutlet.cs:67-68`) - because its orientation string is one
character. Everything else about how connectors are read (the DIM overloads, reciprocity, the port rule)
is [pipe network](../mechanics/pipe-network.md)'s.

The pressure valve is a network endpoint. `IsNetworkEndPoint => true` (`BlockPressureValve.cs:55`) keeps its
two sides apart: `GetConnectedNeighbors` bails at an endpoint (`BlockNetworkModSystem.cs:389-390`) and
`IsValidNetworkNeighbour` refuses one as a neighbour (`:444-445`). A pressure valve is always its own
one-node network and the runs on either side never merge - the mechanism behind "its two networks must be
genuinely separate" that [pipe network](../mechanics/pipe-network.md) Gotcha 10 states.

---

## Assets

| asset | path | state |
|---|---|---|
| Editable shapes | — | none. No pipe or fitting source file exists under `assets/editable/shapes/` |
| Cast straight | `assets/lpex/shapes/pipes/straight.json` | elements `Cube2` `Cube6` `Cube18`; textures `cast-iron1` → `iwex:block/metal/castiron`, `iron4`, `iron` |
| Cast bend | `assets/lpex/shapes/pipes/bend.json` | `Cube2` `Cube18`; its body key is `iron42`, not `iron4` |
| Cast T / X | `assets/lpex/shapes/pipes/tjunction.json`, `xjunction.json` | present |
| Valve | `assets/lpex/shapes/pipes/valve.json` | elements `Cube2` `Handle` `Lid`; `open` animation, 30 frames; textures `normal4`, `iron4`, `copper3` |
| Pressure valve | `assets/lpex/shapes/pipes/pressurevalve.json` | single element `Cube2`; no animation; textures include `copper3` (the input-side ring the handbook points at) and `steel3` |
| Outlet | `assets/lpex/shapes/pipes/outlet.json` | `Cube2` `Cube10` |
| Passthrough / bend | `assets/lpex/shapes/pipes/passthrough.json`, `passthroughbend.json` | present |

`BlockPipe.Segments` resolves `{domain}:pipes/*` (`BlockPipe.cs:82`, `:96`, `:124`, `:153`) and each of the
three tiers ships its own four shapes - iwex `iron3/iron4/iron42/iron`, lpex `cast-iron1` over an iwex
cast-iron texture, hpex `cast-iron1` + `game:block/metal/plate/steel`. There is no blanket texture override
on the shared surface: the shapes disagree on key names (`iron4` vs `iron42`), so an override would repaint
some segments and miss others (`BlockPipe.cs:69-72`).

Only the plain valve is animated. It declares `EntityBehavior("Animatable")` (`BlockValve.cs:31`) and
`BlockEntityValve` drives the `open` pose through `ToggleAnimator` (`BlockEntityValve.cs:40`, `:168-186`).
The pressure valve inherits the block class but not the animatable behaviour (its def at
`BlockPressureValve.cs:27-53` has no `EntityBehavior`), and its shape has no clip - a pressure valve is
static art whose only feedback is the HUD line.

---

## Construction

| block | recipe | file:line |
|---|---|---|
| `pipe-passthrough-{brick}-ns` | `BHB,BPB,B_B` — 6 brick + 1 `iwex:pipe-straight-*` + hammer | `PipeRecipeDefinitions.cs:23-31` |
| `pipe-passthroughbend-{brick}-nw` | `BPB,PHB,B_B` — 6 brick + 2 straight + hammer | `:32-40` |
| `pipe-outlet-{brick}-n` | `BHB,BNB,BPB` — 12 brick + 1 straight + 2 nails + hammer | `:41-50` |
| `pipe-valve-sn` | `_H_,GPL,_L_` — 1 `iwex:pipe-straight-ns` + 2 plate + 2 gears + hammer | `:52-61` (rusty gear) and `:72-81` (`lpex:gear-*`) |
| `pipe-pressurevalve-sn` | `_H_,LPL,G_G` — 1 straight + 2 plate + 4 gears + hammer | `:62-71` and `:82-90` |
| `pipe-straight` / `bend` / `tjunction` / `xjunction` | none | — |

Every fitting is worked from a plated (iwex) segment, not a cast one - `StraightBlock` and `ValvePipe` both
resolve `iwex:pipe-straight-*` (`PipeRecipeDefinitions.cs:103-107`). The valve pair is authored twice so
either `game:gear-rusty` or `lpex:gear-*` crafts it (`:14`).

### The cast segments are uncraftable

Nothing outputs `lpex:pipe-straight-*` or its siblings:

> *"lpex's own cast segments are a higher tier with no recipe of their own yet."*
> — `MachineRecipeDefinitions.cs:126`

This is the same class of blocker as B5 (hpex's rolled tier, four blocktypes and zero recipes). The cast
tier ships four blocktypes, four shapes, a burst rating, a joint registration and a migration path, and no
way to obtain a single segment outside creative.

The intended route is cast segments assembled from cast pipe-parts bored on the boring machine
([boring machine](boring-machine.md)). Neither half exists:

| piece | state |
|---|---|
| boring machine block | no source file anywhere in `src/` — a grep for `boring` returns only unrelated cylinder-particle code |
| cast pipe-part item | does not exist. The art does: `assets/editable/shapes/item-cylinder-castblank.json`, `item-cylinder-pipesegment.json`, `item-cilinder-bored.json` |
| `diagram-pipe-*` items | not built ([diagram crafting](../mechanics/diagram-crafting.md)) |

`overview.md:70` lists the boring machine as shipped lpex content. It is not.

### Dead recipe-cost keys

`LpexRecipeConfig.Defaults()` catalogues four segment entries whose recipes do not exist:

| key | match | file:line |
|---|---|---|
| `pipe-straight-grid` | `lpex:pipe-straight-*`, cheap output pinned to 4 | `LpexRecipeConfig.cs:76` |
| `pipe-bend-grid` | `lpex:pipe-bend-*`, cheap 2 | `:77` |
| `pipe-tjunction-grid` | `lpex:pipe-tjunction-*`, cheap 2 | `:78` |
| `pipe-xjunction-grid` | `lpex:pipe-xjunction-*`, cheap 2 | `:79` |

STATE.md's "Remove" list names only `pipe-straight-grid` (`STATE.md:652`) - all four are dead, and the
comment above them at `:46-47` describes an authored output count ("straight 2, bend/t/x-junction 1") for
recipes that were never written for this domain.

Fittings are catalogued correctly: `pipe-outlet-grid`, `pipe-passthrough-grid`, `pipe-passthroughbend-grid`,
`pipe-valve-grid`, `pipe-pressurevalve-grid` (`LpexRecipeConfig.cs:81-85`).

---

## Operation

### The cast tier's two registrations

Both happen once, in `ModSystem.Start`, keyed by domain:

```csharp
BlockPipe.RegisterBurst(Mod.Info.ModID, () => LpexValues.CastPipeBurstPressure);  // :60
BlockPipe.RegisterJoint(Mod.Info.ModID, BlockPipe.FlangedJoint);                  // :62
```
— `LowPressureExpandedModSystem.cs:57-62`. The burst getter is a `Func<float>` read live, so a retune applies
without reconstructing networks.

Cast pipe is flanged, the same family as bolted, because both are square in section and bolted through
flanges (`:61`), so an iron-tier main and a cast main interconnect and a line upgrades segment by segment.

### The plain valve

Toggled by an empty-handed right-click (`BlockValve.cs:55-84`; a held item defers, `:67-68`), server-side
only, with a door-open sound. The sever semantics, the `RemoveNode`+`AddNode` re-walk and the stale-pool
discard are [pipe network](../mechanics/pipe-network.md) § 7's. The block's own side: `Lockable` behaviour,
`MaxStackSize(1)`, and the `lpex:blockhelp-valve-toggle` interaction line appended to the base help
(`BlockValve.cs:86-102`).

The valve holds its state in an animation pose, which needs two pieces of machinery:

- `AnimatableRenderer` honours only the Y rotation, so the vertical (`ud`/`du`) variants posed flat against a
  vertical static mesh. The BE builds the full `T(centre)·RotateXYZ·T(−centre)` matrix itself and assigns it
  to the renderer's `CustomTransform` (`BlockEntityValve.cs:70-78`, `88-108`).
- A wrench rotation goes through `ExchangeBlock`, which keeps the BE alive so `Initialize` never re-runs.
  `OnExchanged` rebuilds the animator, but only on a real orientation change, because a network re-walk
  can re-exchange `ns` ↔ `sn` and would otherwise reset the open pose (`:115-129`).

### The pressure valve

Right-click raises the gate, sneak + right-click lowers it (`BlockPressureValve.cs:59-88`), in
`GatePressureStep` increments, with a switch sound on an actual change. The inherited toggle help line is
filtered out and two of its own are appended (`:90-112`). The overflow arithmetic - gas downhill-only,
equalise cap, leaking-output trickle, vent-to-atmosphere, liquid spill - is
[pipe network](../mechanics/pipe-network.md) § 8's.

This page owns the ceiling:

```csharp
public float MaxGatePressure => Block is BlockPressureValve v ? v.BurstPressure : 0f;
```
— `BlockEntityPressureValve.cs:41-42`. `BurstPressure` resolves through `_burstByDomain[Code.Domain]`, and
the block only exists in the lpex domain, so the gate tops out at 5.0 atm - borrowed from cast pipe's
rating even though the valve itself is exempt from bursting (`CanBurst` is false for every `BlockPipe`
subclass, `BlockPipe.cs:196`). It is clamped again on load against a possibly-reconfigured rating
(`BlockEntityPressureValve.cs:48-52`).

### B6 — the HP line cannot take the lpex pressure valve

hpex tells the player, in its own README and handbook, to gate the Lancashire → Cornish line with a pressure
valve (`src/HighPressureExpanded/README.md:22`, `docs/hpex/handbook/00-highpressure.html:29`,
`docs/hpex/moddb.html:56`). It cannot be installed, and even if it could it would not reach. Two
independent failures:

**1 — the joint refuses it.** hpex registers `WeldedJoint` (`HighPressureExpandedModSystem.cs:45`); lpex
registers `FlangedJoint` (`LowPressureExpandedModSystem.cs:62`); `AcceptsNeighbour` couples a `BlockPipe` only
to a matching family (`BlockPipe.cs:238-239`). The pressure valve is a `BlockPipe` subclass, so welded pipe
will not bolt to it. hpex ships no fittings of its own - `RolledPipeDefinitions.cs` is segments only.

**2 — the gate cannot reach the engine's band.** Even bolted into a cast section, the ceiling is 5.0:

| quantity | value | file:line |
|---|---|---|
| lpex pressure-valve `MaxGatePressure` | 5.0 atm | `LpexConfig.cs:50` via `BlockEntityPressureValve.cs:41` |
| Cornish engine engage, low throttle | 5.0 atm | `HpexConfig.cs:70` |
| Cornish engine engage, normal | 6.0 atm | `HpexConfig.cs:71` |
| Cornish engine engage, high | 7.0 atm | `HpexConfig.cs:72` |
| Cornish engine break (all three) | 8.0 atm | `HpexConfig.cs:76-78` |
| Lancashire boiler choke | 12.0 atm | `HpexConfig.cs:60` |

```
5.0   <   6.0        7.0        8.0        12.0
gate    engage-N   engage-H    break     boiler choke
ceiling
```

A valve gated at its maximum holds the downstream main at 5 atm, below the pressure at which a
normal-throttle Cornish engine engages. Only the low throttle setting (5.0) is reachable, and only exactly at
the ceiling.

The fix is hpex-side: two registrations plus four defs. An hpex-domain valve/pressure-valve pair (welded,
rating 12) resolves both halves at once, because both the joint and the ceiling read the block's own domain.

---

## Numbers

### Config — `src/LowPressureExpanded/LpexConfig.cs`, `ModConfig/ex_values.json`, section `lpex`

| key | value | file:line | what it does |
|---|---|---|---|
| `CastPipeBurstPressure` | `5.0` atm | `LpexConfig.cs:50` | the cast tier's plain-segment rating; also the run's buffer multiplier and the pressure valve's gate ceiling |

That is the whole of lpex's pipe config. Everything else the fittings use - `LitresPerPipe`, `GasLeakRate`,
`LiquidLeakRate`, `EvaporationLitresPerDay`, `PipeOverpressureSeconds` - is exlib's, and `ChimneyGasDrawRate`
is iwex's; all six are tabulated by [pipe network](../mechanics/pipe-network.md) § Numbers
(`LpexConfig.cs:43-46`).

### Hard-coded — not config

| value | file:line | what it does |
|---|---|---|
| `MinGatePressure = 0f` | `BlockEntityPressureValve.cs:28` | pressure-valve floor |
| `GatePressureStep = 0.25f` | `BlockEntityPressureValve.cs:31` | per interaction |
| default gate `1f` | `BlockEntityPressureValve.cs:35`, `:310` | initial value and what a pre-configurable save migrates to |
| valve tick `1000 ms` | `BlockEntityPressureValve.cs:54` | server-side overflow beat |
| overflow HUD epsilon `0.01 L` | `BlockEntityPressureValve.cs:103` | re-sync threshold on `_lastVentVolume` |
| gate-change epsilon `0.001` | `BlockEntityPressureValve.cs:70` | "no change" test for the interaction sound |
| downhill epsilon `0.001` | `BlockEntityPressureValve.cs:168` | gas will not cross a near-equal pressure |
| valve open-anim `speed 2.5`, ease 8/8 | `BlockEntityValve.cs:176-180` | pose only |
| collision/selection `0.3125 → 0.6875` | `BlockValve.cs:47-48`, `BlockPressureValve.cs:47-48` | the fitting core, matching the segment |
| `FlangedJoint = "flanged"` | `BlockPipe.cs:255` (exlib) | the family string lpex registers |
| `DefaultBurstPressure = 5f` | `BlockPipe.cs:182` (exlib) | fallback for an unregistered domain — numerically identical to cast, so a missing `RegisterBurst` would be invisible |

### The tier ladder — one row owned here

| tier | domain | burst | joint | owner |
|---|---|---|---|---|
| plated | iwex | 2.5 | flanged | [pipe network](../mechanics/pipe-network.md) |
| cast | lpex | 5.0 | flanged | this page (`LpexConfig.cs:50`) |
| rolled | hpex | 12 | welded | [pipe network](../mechanics/pipe-network.md) |

The rating doubles as the buffer size - a run holds `burst × pipes × LitresPerPipe`
(`PipeNetwork.cs:337-341`) - so a cast run is both stronger and twice the reservoir of a plated one at the
same node count. That is the tier's whole mechanical benefit; there is no throughput or length advantage (see
[Open](#open)).

### Where 5.0 sits against lpex's own machines

| quantity | value | owner |
|---|---|---|
| Watt engine engage | 2.0 atm | `LpexConfig.cs:163` ([Watt engine](engine-watt.md)) |
| Watt engine break | 4.0 atm | `LpexConfig.cs:166` ([Watt engine](engine-watt.md)) |
| Cornish boiler choke | 5.0 atm | `LpexConfig.cs:156` ([Cornish boiler](boiler-cornish.md)) |
| cast pipe burst | 5.0 atm | this page |
| plated pipe burst | 2.5 atm | [pipe network](../mechanics/pipe-network.md) |

The boiler chokes exactly at the pipe's rating, so a cast steam main can carry a maxed Cornish boiler and sit
permanently at burst-comparison distance - `TickOverpressureAndBurst` compares with a `0.001` epsilon
(`PipeNetwork.cs:788`), so a main pinned at 5.000 is inside the grace window, not outside it. Plated pipe
cannot carry a Cornish boiler at all (2.5 < 5.0): the moment the boiler passes half choke, an iron-tier main
is on its 30-second burst clock. That, not a rule, is what makes cast pipe a prerequisite for the steam
spine.

---

## Drops

Every block on this page uses plain block drops - none of the five fitting defs sets `NoDrops()` and none of
the classes overrides `GetDrops`.

| block | drops | note |
|---|---|---|
| cast segments | itself | stack 16 / 8 (`BlockPipe.Segments`, exlib) |
| valve, pressure valve | itself | `MaxStackSize(1)` (`BlockValve.cs:34`, `BlockPressureValve.cs:34`) |
| outlet, passthrough, passthrough-bend | itself, brick variant preserved | `MaxStackSize(1)` (`BlockPipeOutlet.cs:41`, `BlockPipePassthrough.cs:46`) |

Salvage is 1:1 and lossless - none of these is a right-click construction, so `RccBrokenDropsRatio`
(`LpexConfig.cs:116`) does not apply. A burst segment is the exception: it drops its items, puffs steam and
is replaced with air by the network's burst pass (`PipeNetwork.cs:888-916`,
[pipe network](../mechanics/pipe-network.md) § 5).

---

## Code

| piece | file:line |
|---|---|
| `CastPipeDefinitions : IExBlockDefProvider` | `BlockNetworkPipe/CastPipeDefinitions.cs:15-19` |
| burst + joint registration | `LowPressureExpandedModSystem.cs:57-62` |
| `BlockValve : BlockPipe` | `BlockNetworkPipe/Blocks/BlockValve.cs:19` — def `:23-53`, toggle `:55-84`, help `:86-102` |
| `BlockEntityValve : BlockEntityPipe` | `BlockNetworkPipe/BlockEntities/BlockEntityValve.cs:22` — `IsConnectionBroken` `:33`, `BuildAnimator` `:48-81`, `BuildShapeRotationTransform` `:88-108`, `OnExchanged` `:115-129`, `ToggleOpen` `:134-155`, `DiscardNetworkPool` `:162-166`, `FromTreeAttributes` `:214-228` |
| `BlockPressureValve : BlockValve` | `BlockNetworkPipe/Blocks/BlockPressureValve.cs:20` — def `:24-53`, `IsNetworkEndPoint` `:55`, gate interaction `:59-88` |
| `BlockEntityPressureValve : BlockEntityPipe` | `BlockNetworkPipe/BlockEntities/BlockEntityPressureValve.cs:25` — `MaxGatePressure` `:41-42`, `Initialize` clamp `:44-55`, `AdjustGatePressure` `:62-75`, `OnTick` `:77-106`, `OverflowGas` `:113-214`, `OverflowLiquid` `:221-268`, HUD `:280-295` |
| `BlockPipeOutlet : BlockPipe, IChimneyVentable` | `BlockNetworkPipe/Blocks/BlockPipeOutlet.cs:18` — `BurstPressure => MaxValue` `:22`, `HasConnectorAt` `:67-68` |
| `BlockPipePassthrough : BlockPipe, IChimneyVentable` | `BlockNetworkPipe/Blocks/BlockPipePassthrough.cs:19` — `BurstPressure => MaxValue` `:23`, both defs `:70-106`, `OnNeighbourBlockChange` no-op `:116-120` |
| `BlockEntityPipeOutlet` / `BlockEntityPipePassthrough` | `:8` in each — empty `BlockEntityPipe` subclasses |
| `PipeRecipeDefinitions` | `Recipes/Grid/PipeRecipeDefinitions.cs:16` |
| `PipeMigration` | `BlockMigrations/PipeMigration.cs:29` |
| `BlockPipe` (segments, burst registry, joint registry) | `ExpandedLib/Blocks/Networks/BlockPipe.cs` — in exlib since 2026-08-07 |

### Migrations

`PipeMigration` lands every legacy code on the current tier split (`PipeMigration.cs:29`):

| old | new | file:line |
|---|---|---|
| `ppex:pipe-{segment}-{orient}-{iron\|steel}` | `iwex:pipe-{segment}-{orient}` | `:69-75` |
| `ppex:pipe-{valve\|pressurevalve}-{orient}-{iron\|steel}` | `lpex:pipe-{type}-{orient}` | `:77-83` |
| `smex:gas{passthrough\|passthroughbend\|outlet}-…` | `lpex:pipe-…` | `:85-86` |
| removed refractory brick tiers | fall back to `fire` | `:89-106` |
| removed inline gas machines (`gaspipe-blower/heated/intake`) | `iwex:pipe-straight-{axis}` | `:120-143` |

The segment remaps point away from lpex. A world's old `ppex` steel pipes become iwex plated pipes, not cast
ones, so nobody who upgrades ends up holding a cast segment either. The ppex → lpex flat rename itself is
`LpexRenameMigration` (`PipeMigration.cs:16-19`).

### Tests

`test/LowPressureExpanded.Tests/`

| file | what it pins |
|---|---|
| `Blocks/Valves/ValveBeTests.cs` | closed-by-default sever, `IsConnectionBroken` tracking, re-join / re-sever, pool discarded on close, tree round-trip, closed load drops a persisted pool |
| `Blocks/Valves/ValveReloadRegressionTests.cs` | `Closed_valve_does_not_reload_a_pressurised_pool` |
| `Blocks/Valves/PressureValveBeTests.cs` | `MaxGatePressure_is_the_blocks_burst_rating`, step up/down, clamping at both ends, tree round-trip, legacy default 1 atm, vent-to-atmosphere above the gate, nothing at/below it |
| `Blocks/Pipe/*` | pipe BE, serialization, tick |
| `Scenarios/SteamSupplyScenarioTests.cs:21`, `:51` | the relief valve bleeding an over-pressured main into band; a gate above the charge never opening |
| `Definitions/LpexDefinitionGoldenTests.cs` | the shipped defs against `goldens/lpex/blocktypes/pipes/*.json` |

No test covers gas-to-gas overflow into a second network (the downhill rule and the equalise formula,
`BlockEntityPressureValve.cs:159-192`) or `OverflowLiquid` at all (`:221-268`). Both vent-to-atmosphere
paths are tested; neither transfer path is.

---

## Gotchas

1. The cast tier is creative-only. Four blocktypes, four shapes, a rating, a joint and a migration, and no
   recipe. See [Construction](#construction).

2. `DefaultBurstPressure` is 5, identical to cast (`BlockPipe.cs:175`). If
   `LowPressureExpandedModSystem.cs:60` were ever dropped, the tier would keep working at the same number and
   nothing would surface the loss. The plated and rolled tiers do not have this problem.

3. A refused joint does not leak, despite what the source says. `BlockNetworkNode.cs:751-754` and
   [pipe network](../mechanics/pipe-network.md) § 5 both state that a refused joint "reads as an open end,
   so the run leaks rather than silently merging". `ClassifyOpenings` only counts an open face as a leak
   when the neighbour block is air - `if (neighbour.FirstCodePart() == "air")`, `PipeNetwork.cs:632`.
   A welded segment butted against a cast one is not air, so `TotalLeaks` stays 0, `IsLeaking` stays false,
   and the two runs simply do not connect, silently. The intended feedback does not fire. (Reported here
   because the joint rule is what makes B6 invisible in play; the rule itself is
   [pipe network](../mechanics/pipe-network.md)'s to correct.)

4. The passthrough's "seals against a wall" works, but not the way it says it does.
   `BlockPipePassthrough.cs:13-15` credits the seal to a connector against a solid neighbour not being
   treated as a leak - the hook for that is `IsValidNonNetworkConnection` (`BlockNetworkNode.cs:739-742`),
   and nothing in the repo overrides it. The behaviour is delivered by the same air test as above. Correct
   outcome, wrong explanation; do not "fix" the missing override without checking what would change.

5. The pressure valve is an endpoint, so every run ends at it. `IsNetworkEndPoint => true`
   (`BlockPressureValve.cs:55`) means the neighbouring pipes see its face as an open connector. It is not
   air, so it does not leak (Gotcha 3) - but any change to the leak test would turn every installed pressure
   valve into two leaks at once.

6. The two valves disagree on how `du` is rotated. `BlockValve.cs:45` uses `rotateX: 90, rotateY: 180`;
   `BlockPressureValve.cs:45` uses `rotateX: -90`. Both land the pipe on the vertical axis but with different
   handle/ring orientation, and the pressure valve, being unanimated, never exercises the `CustomTransform`
   path that made the valve's vertical variants correct (`BlockEntityValve.cs:70-78`).

7. `pressurevalve` inherits `BlockValve`, not the valve's entity. `BlockEntityPressureValve` extends
   `BlockEntityPipe` (`:25`), so it gets none of `BlockEntityValve`'s open/close state, animator or
   pool-discard logic - which is right (it never severs), but means the inherited
   `lpex:blockhelp-valve-toggle` help line has to be filtered out by hand (`BlockPressureValve.cs:96`).

8. Outlet and passthrough are exempt from bursting twice over - once as `BlockPipe` subclasses
   (`CanBurst`, `BlockPipe.cs:203`, exlib) and again via `BurstPressure => float.MaxValue`
   (`BlockPipeOutlet.cs:22`, `BlockPipePassthrough.cs:23`). The override is what keeps them from capping
   the run, since `MinBurstPressure` walks every node.

9. `BlockPipePassthrough.OnNeighbourBlockChange` is an empty override (`:116-120`). It suppresses the base
   self-break so a passthrough embedded in a wall survives the wall being rebuilt around it. Silent - there
   is no comment saying why.

10. The chimney vent is matched by code substring on the neighbour (`ChimneyVent.cs:50`) and requires
    `face == BlockFacing.UP` (`:49`). So only the `-u` outlet variant and the `ud` passthrough can ever vent:
    any other orientation has no top connector for a chimney to cap. The handbook's "capped with an ordinary
    chimney stood upright on top of it" (`docs/lpex/handbook/03-fittings.html:31-33`) is right but
    under-specified.

11. Fitting recipes consume iwex segments, so lpex's fittings are gated on iwex's craftability. With B1 live
    (the tuyere blocking the iron tier) this is a chain, not an independent path.

12. The outlet's shape still points at a refractory texture. `assets/lpex/shapes/pipes/outlet.json` and
    `passthrough.json` declare `front1` → `game:block/clay/refractory/tier3/front1`, while the def overrides
    `front1` per brick variant (`BlockPipeOutlet.cs:58-63`). Harmless, but the refractory tiers were removed
    from these blocks - `PipeMigration.cs:89-106` migrates them away.

13. Config-file naming is inconsistent in the docs, not the code. `LpexConfig.cs:9` says the file is
    `ModConfig/lpex_values.json`; the attribute at `:17-21` writes `ex_values.json` and lists
    `lpex_values.json` only as a legacy name. The attribute wins.

---

## Open

- B-class blocker: no recipe for the cast segments. Either write a grid recipe against the four
  already-catalogued cost keys, or build the cast → bore → assemble chain the design specifies. Until then
  the four `pipe-*-grid` entries in `LpexRecipeConfig.cs:76-79` cost nothing.
- B6 needs hpex-side fittings, not an lpex change. A welded valve + pressure valve registered in the hpex
  domain fixes both halves (joint and 12-atm ceiling) with no core change, because both read `Code.Domain`.
- No boring machine and no cast pipe-part item exist. The art for the parts is drawn and untracked (three
  `item-cylinder-*` / `item-cilinder-*` shapes); `overview.md:70` still claims the machine ships and should
  be corrected to match the code.
- Nothing distinguishes cast pipe in play except burst pressure. Same litres per node, same throughput, same
  length behaviour, same fittings. [Diagram crafting](../mechanics/diagram-crafting.md) promises cast
  pipe-parts that "assemble into pipes *faster* than plated pipe and hold *more* pressure" - the second half
  exists, the first has nowhere to live in the current model.
- The pressure valve's ceiling should probably be its own number, not the tier's plain-pipe rating. It is
  exempt from bursting, so 5.0 is borrowed, and borrowing it is what produces B6's second half.
- Neither transfer path of the pressure valve is tested (gas-to-gas downhill/equalise, and liquid entirely).
  The vent-to-atmosphere path is.
- No editable shapes for any pipe or fitting. The runtime shapes are the only copies, for all three tiers.
