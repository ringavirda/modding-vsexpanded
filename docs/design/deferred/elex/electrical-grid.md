# Electrical grid (AC + DC)

**Status** deferred   **Would live in** elex (Electrical Expanded) - a mod with no project, no asset
domain and no code   **Deferred by** D8 ([STATE.md](../../../internal/plans/STATE.md)) - the release target is the
complete ferrous line, so elex is out of it; the decision and its reasoning are recorded in
[scope.md](../../scope.md) § The release target and § elex

**Owns** elex's full-realism AC/DC circuit model - the two coupled sub-networks, the DC radial-tree solve
with implicit return, `R = f(purity, length)`, the `I²R`-becomes-heat / melted-cable rule, the AC
frequency-and-phase signal and its three consequences (synchronisation, rectifier gating, three-phase), the
grid-hardware block set, and how all of that relates to and diverges from the live `mpenergy` network.

**Does not own** — cited only:

| Fact | Owner |
|---|---|
| The cut itself, its carve-outs, elex's three chemistry severities, the release target | [scope.md](../../scope.md) |
| The DC generator, the shared Corliss-flywheel-variant form, the bootstrap loop | [dynamo.md](dynamo.md) |
| The AC generator, the pure-copper gate, the prime-mover gap | [alternator.md](alternator.md) |
| The arc furnace, the electrode consumption rate, the HSS chain | [arc-furnace.md](arc-furnace.md) |
| The electrolysis cell, its bath and its anode | [electrolysis-cell.md](electrolysis-cell.md) |
| Wire, the extruder and D7 | [wire-extruder.md](wire-extruder.md) |
| The Tandem Corliss and every hpex number | the archived hpex spec (git history); the figures survive on these pages |
| The live mechanical network's model, numbers and code | [mechanics/mp-energy.md](../../mechanics/mp-energy.md) |
| R1–R7, the network-family list, the block-size vocabulary | [conventions.md](../../conventions.md) |
| Pure copper and HSS as materials | [materials.md](../../materials.md) |

**Depends on** [scope.md](../../scope.md) · the archived elex and hpex specs ·
[mechanics/mp-energy.md](../../mechanics/mp-energy.md) ·
[mechanics/pipe-network.md](../../mechanics/pipe-network.md) ·
[conventions.md](../../conventions.md) · [dynamo.md](dynamo.md) · [alternator.md](alternator.md)

---

## What it is

Nineteenth-century electrical distribution, and specifically the war of the currents. Edison's Pearl
Street station (1882) generated and distributed DC at the consumption voltage: the copper had to be thick
and the customers had to be within about a mile, because the loss is `I²R` and DC cannot change its
voltage. Westinghouse's AC answer put a transformer at each end of the run - step the voltage up, send the
same power at a fraction of the current, step it down at delivery, and the `I²R` loss falls with the
square. That asymmetry sets the shape of what a player builds here: AC to move power, DC to use it, a
rectifier where they meet.

The heavy-power tier is three-phase (Tesla / Dolivo-Dobrovolsky, 1891): three lines carrying the same
frequency 120° apart. Héroult's arc furnace (~1900) is why it matters to this suite - one carbon electrode
per phase, no rectifier anywhere in the plant.

---

## Why it is deferred

elex is a spine tier that sits after the release target. The cut itself is owned by
[scope.md](../../scope.md). Two consequences are grid facts rather than scope facts:

1. The grid model itself has no chemistry dependency at all. None of elex's three recorded chemistry needs
   ([scope.md](../../scope.md) § elex) is a wire, a pole, a transformer or a rectifier. What gates the grid
   is the conductor material - and see § Gotchas for the fact that the impure conductor has no producer
   either.
2. "DC only" is not a smaller version of the grid - it is a different grid. The subset
   [scope.md](../../scope.md) records as shippable without chemistry (arc furnace on carbon electrodes, DC
   only, impure copper wire, no alternators) inherits the DC row of the table below: resistance summed,
   voltage sags, not transformable. The loss-free backbone is the AC row. So the no-chemistry elex is a
   single-building plant - generator beside load - and not a grid in the sense this page describes.

---

## What exists today

Nothing: not one line of code, not one asset, not one lang key.

| Probe | Result |
|---|---|
| `grep -rniE "dynamo\|alternator\|rectifier\|synchronis\|electrolys\|voltage\|three-phase" src/ --include=*.cs` | 0 hits |
| `grep -rniE "elex\|electric\|dynamo\|alternator\|voltage" assets/ .github/` | 0 hits |
| Projects in `VintageStory.sln` | `ExpandedLib`, `ExpandedLib.Generators`, `ExpandedLib.Testing`, `HighPressureExpanded`, `IronIndustryExpanded`, `IronIndustryExpanded`, `SteelmakingExpanded` (+ their `.Tests`) and `CakeBuild`. No `ElectricalExpanded` |
| Asset domains under `assets/` | `editable`, `exlib`, `game`, `hpex`, `iiex`, `iiex`, `smex`. No `elex/`, so no `assets/elex/lang/en.json` either |
| Registered network types | exactly three, all in `src/IronIndustryExpanded/IronIndustryExpandedModSystem.cs` (`"pipe"`, `"molten"`, `"mpenergy"`). No `"ac"`, no `"dc"` |

The only electrical thing that exists anywhere in the repo is the row in the family list:
[conventions.md](../../conventions.md) § Networks - "Electrical (AC + DC) — the elex tier (planned)".

`assets/exlib/config/liquids.json` declares four media (`Air`, `Steam`, `Exhaust`, `Water`) and none of them
is electrical, which is correct rather than a gap: the elex design rules that wires are a separate medium
family and that R1 governs pipes, not wires.

---

## The design as it stands

All of it comes from the archived elex spec. Every threshold is stated as tunable.

### The two sub-networks

| | **AC backbone** | **DC drops** |
|---|---|---|
| Made by | alternator (slip rings) | dynamo directly, or rectifier off AC |
| Carries | high-voltage / low-current transmission | local end-consumption |
| Resistance | ignored - loss-free over distance | summed - every wire and block adds R |
| Voltage | held ~fixed (field regulation, abstracted) | sags by `I·R_total` along the path |
| Transformable | yes (V↔I at constant power) | no |
| Physical signal | frequency + phase, tracking engine speed | steady V |
| Feeds | bulbs, transformers, arc furnace (3-phase) | electrolysis, batteries, small loads |

### The DC circuit model

What makes it solvable is in the block, not the solver: **cable is a doubled conductor** - out and return
in one block - so each segment's resistance is already round-trip and the graph can be walked as a radial
tree with implicit return. Generators are sources at fixed potential; machines, outlets and batteries are
constant-power sinks pulled toward return.

| Rule | Statement |
|---|---|
| Node voltage | `V_node = V_source − Σ(I·R)` along its path. Farther or more loaded → more sag |
| Sink current | `I = P_rated / V`. As the node sags, current rises → more `I²R` → more sag |
| Brownout | emergent: the sink cuts out when its node falls below its minimum-voltage cutoff. Nothing declares a brownout |
| Cable melt | `I²R` becomes heat, accumulated. Brief surges survive; sustained over-current drives cable temperature up until it melts and breaks the circuit |
| Resistance | `R = f(purity, length)`. Impure (converter) copper high-R, pure (electrolytic) copper low-R |
| Source ceiling | the driving engine, not the wire. Demand past the Corliss's max stalls the engine and drops the whole network - and any shared MP load - at once |

Brownout is emergent from the sag equation the way a stall is emergent from the sign of `τ_net`
([mechanics/mp-energy.md](../../mechanics/mp-energy.md) § The integration step), and cable melt is the
electrical version of the pipe's burst - the consequence of ignoring a readable number, per R5
([conventions.md](../../conventions.md)).

### AC frequency and phase

| Property | Rule | Consequence |
|---|---|---|
| Frequency | tracks engine speed. Under the Corliss governor the engine slows as load rises, so frequency sags with load and is zero at stall. Voltage is what is held fixed | frequency is the physical signal a player reads |
| Paralleling | two alternators need frequency and phase matched - a synchroniser block locks them and shares load | after sync the network is one coherent frequency/phase, so the concern is localised to the generation side |
| Rectifiers | frequency-band gated | a bogged Corliss whose frequency droops out of band drops its DC side before a full mechanical stall - a graded failure, not a cliff |
| Three-phase | three phase-offset lines: one 3-phase alternator, or three synchronised single-phase ones 120° apart | splitting current across three conductors is the only way to carry arc-furnace current; also feeds a 3-phase rectifier → one smooth high-power DC bus |

### Grid hardware — all *(planned)*

| Block | Block-type | What it is |
|---|---|---|
| Cable | block (on a face) | surface-run doubled conductor; normal capacity; melts under sustained over-current |
| Heavy cable | block (on a face) | high-current tier; required at the generator and at high-power machines |
| Inset cable | block (inset) | hidden in-wall / in-floor wiring |
| Inset outlet | block (inset) | AC tap for lighting and low-draw loads |
| Power pole | megablock (tall) | long spans; click pole→pole to draw a beam-style line. 2 normal + 1 heavy slots, sneak to select |
| Transformer | block | AC→AC at stepped voltage; iron core + copper coil (see [alternator.md](alternator.md) § Silicon steel) |
| Rectifier | block (AC-driven) | single-phase AC→DC at point of use; frequency-band gated; rotary or mercury-arc |
| 3-phase rectifier | megablock (AC-driven) | three phase-offset lines → one smooth high-power DC bus |
| Synchroniser | block | matches frequency + phase to parallel alternators and share load; synchroscope |
| Battery (acid / dry cell) | megablock (store) | DC ↔ stored DC backup - the electric analogue of the gasholder |
| Wire extruder | megablock | MP-driven, not electric (needs iiex MP): copper rod/plate → wire, impure or pure |

The wire extruder is the one block on this list that needs no grid to work - it is an MP machine. Under D7
([STATE.md](../../../internal/plans/STATE.md)) wire lives in elex, which is what puts it on this table at all.

### The numbers, and they are self-consistent

| Quantity | Value | Check |
|---|---|---|
| Tandem Corliss at the shaft | ~36 kW | the only stated prime mover (archived hpex spec) |
| Coil efficiency, impure / pure | ~20 % / ~80 % | elex-owned, tunable |
| Impure dynamo | ~7.2 kW | = 36 × 0.20 |
| Pure dynamo / pure alternator | ~28.8 kW each | = 36 × 0.80 |
| Electrolysis cell draw | ~7.2 kW | impure dynamo runs exactly one; pure runs 4 |
| Arc furnace (three-phase) | ~86 kW | = 3 × 28.8 = 86.4 - three engines, three alternators, one synchroniser |

The whole tier is one number times a ratio. Changing the Corliss's 36 kW moves every figure in elex with
it, including the "one dynamo = one cell" identity the bootstrap loop is built on ([dynamo.md](dynamo.md)).

---

## How it relates to `mpenergy` — and where it diverges

The live [MP energy network](../../mechanics/mp-energy.md) is the closest thing in the repo to what elex
wants, and close enough to mislead. The two model different physics and would not share a solver.

| | **`mpenergy`** *(live)* | **elex grid** *(deferred)* |
|---|---|---|
| The thing modelled | one lumped spinning shaft - `I·dω/dt = τ_drive − τ_load − τ_fric` | a circuit - node potentials solved along a path |
| State per network | one `MpEnergyNetworkState`: ω, I, E, Reversed | per-node voltage and current; no single pooled scalar |
| Topology | any connected node set; pooled on merge, split proportionally | a radial tree with implicit return - explicitly no loops |
| Storage | derived - capacity is `½Iω_max²`, a flywheel spinning at max | declared - a battery block with stored DC |
| Position matters? | no. Every node sees the same ω | yes. Distance is the whole DC mechanic (`I·R` sag) |
| Failure mode | stall - ω winds to 0 when `τ_net < 0` | brownout - a sink drops below its cutoff; the rest keeps running |
| Supply ceiling | a torque–speed curve; a drive that cannot out-torque the load never starts | a power ceiling at the engine; past it the engine stalls and everything drops at once |
| Units | SI: rad/s, N·m, J, W | V, A, Ω, W |

### elex's ceiling is written in the *old* mechanical model

The archived elex spec's rule - "the generator supplies whatever the tree draws, up to the Corliss's max
power … demand past that stalls the engine and drops the whole network at once" - is the constant-power
semantics of `BEBehaviorEngineMPGenerator` (`torque = budget / speed`, settles at `speed = budget / load`,
stalls past ~2× rated), which is the model `mpenergy` superseded.

Under the live model there is no such cliff and no such rating: a producer returns `DriveTorque(ω)`, not kW
([mechanics/mp-energy.md](../../mechanics/mp-energy.md) § Torque governs, not power), an over-drawn run winds
down rather than dropping, and a flywheel on the run buffers the excursion. So the rule has to be either
ported (the generator becomes an `IMpEnergyConsumer` whose electrical demand is a `LoadTorque`, and
"stalling the engine" becomes the ordinary ω→0 stall) or kept deliberately as a different family's rule.
It cannot be left as written and be true. The drive-side half of this gap - that the Corliss is specified
only in the archived elex/hpex specs and nowhere in the mechanical design - is
[alternator.md](alternator.md) § The prime mover.

### And the two models are 4 orders of magnitude apart

`mpenergy`'s live calibration ([mechanics/mp-energy.md](../../mechanics/mp-energy.md) § Numbers):
`MpMaxSpeed = 2.0` rad/s (`src/ExpandedLib/ExlibConfig.cs:98`) and `FlywheelBridgeChargePower = 1.0` N·m
(`src/IronIndustryExpanded/IiexConfig.cs:467`). One bridge at full speed therefore supplies
τ·ω = 1 × 2 = 2 W, of which friction takes `0.05·2 + 0.5 = 0.6` N·m, leaving 0.8 W of usable headroom -
the mill's calibration comment says exactly this at `src/IronIndustryExpanded/IiexConfig.cs:508-520`. The
flywheel's own block-info suppresses the supply/demand line below 1 W
(`src/IronIndustryExpanded/BlockNetworkEnergy/BlockEntities/BlockEntityFlywheel.cs:302`).

elex speaks in kW; the live mechanical network runs at ~2 W. `mpenergy`'s numbers are a first-pass
calibration in arbitrary-but-consistent units and elex's are nominal physical ones, but they are not the
same scale, and "the Corliss delivers 36 kW to the generator" has no meaning on the network as it stands.
Deciding whether hpex rescales `mpenergy` into real watts, or the generator reads the engine directly and
never touches the shaft network, is a prerequisite to either generator page.

### What the graph would give elex for free, and what it would not

| Needed | Available? |
|---|---|
| Add / remove / merge / split on placement | yes - `BlockNetworkModSystem` BFS, shared by all three live families |
| A once-a-second tick | yes - `BlockNetworkModSystem.cs:42-45`, the shared `dt` |
| A machine that reads two networks without merging them | yes - the transmission's pattern: `GetNetworkAt` on each port cell, projected onto a constraint (`BlockEntityTransmission.cs:256-305`). This is exactly the transformer / rectifier shape |
| A block on two different network families at once | yes, but only via the flywheel's trick - host a behaviour for the other family on a footprint cell (`BEBehaviorMPFillerPort`), because `BlockNetworkNode.NetworkType` is one abstract string per block (`src/ExpandedLib/Blocks/Networks/BlockNetworkNode.cs:701`) |
| Acyclic topology | no - nothing enforces it. Every live family pools its state, so a ring is harmless; a circuit solved as a tree is not. See § Gotchas |
| A per-node solve (voltages differing along a run) | no - every live family computes one state for the whole network. This is new machinery, not a subclass |

---

## What it would unblock

| Waiting on the grid | Severity | Why |
|---|---|---|
| [Arc furnace](arc-furnace.md) | wall | it is fed by three-phase AC directly, one electrode per phase. Not a grid, not a furnace |
| HSS | wall (twice) | arc furnace + ladle W/Cr ([materials.md](../../materials.md)) - and separately, tungsten has no source anywhere in the suite ([processes/alloying.md:289](../../processes/alloying.md)). Chain owned by [arc-furnace.md](arc-furnace.md) |
| Pure copper → alternators | wall | the [electrolysis cell](electrolysis-cell.md) is a DC sink; without any grid there is no DC ([alternator.md](alternator.md)) |
| Long-distance power | wall | the loss-free path is the AC row of the table. There is no DC alternative - see § Gotchas, no inverter |
| Light bulb | nothing waiting | it is a consequence, not a dependency |
| [Wire](wire-extruder.md) (D7) | not waiting | the extruder is MP-driven; it could be built the day elex exists, grid or no grid |
| Cold rolling | degraded path | the mechanical design parks cold rolling as the elex-era machine - a power-scale statement, not an electrical one |

---

## Gotchas

* **The impure conductor has no producer.** The whole DC tier is built on impure copper - cable, the
  kickstart dynamo's coils, the electrolysis anode - and [materials.md](../../materials.md) sources
  converter copper from the reverberatory → Pierce-Smith chain, which is deferred as non-ferrous
  ([scope.md](../../scope.md) § Non-ferrous). Vanilla copper does exist (`game:metalplate-copper` is real -
  `metalplate.json` takes its variants from `worldproperties/block/metal.json`, which lists `copper`), and the
  wire extruder's stated input is a generic "copper rod/plate". Nothing records which one it is, and the
  answer decides whether the elex subset in [scope.md](../../scope.md) § The shape elex would take is
  buildable at all or is silently gated on D8 a second time. This is the single most load-bearing unrecorded
  fact about elex.
* **There is no inverter.** Rectifier and 3-phase rectifier are AC→DC; the transformer is AC→AC; DC is
  explicitly not transformable. So a dynamo-only grid can never reach the AC backbone - the DC tier is not a
  stepping stone to AC, it is a parallel one.
* **Cycles.** The DC model is a tree and nothing on the exlib graph forbids a player closing a cable ring.
  Every existing family is loop-indifferent because it pools a single state; a circuit is not. Either the
  traversal spans a tree and ignores the closing edge, or the joint rule refuses it the way the pipe tiers
  refuse a mismatched flange ([mechanics/pipe-network.md](../../mechanics/pipe-network.md)).
* **The synchroniser is given two different jobs.** The archived elex spec and the block table row describe
  it as paralleling - lock two alternators to the same phase and share load. The same spec then builds
  three-phase from "three synchronised single-phase ones 120° apart", which is the opposite constraint:
  hold a fixed offset, not zero. One block cannot be described by both sentences; it needs a mode, or
  three-phase needs the single 3-phase alternator.
* **AC resistance is "ignored" in one line and load-bearing two paragraphs later.** The archived elex spec
  says resistance is ignored on the backbone, then that purity's payoff includes "how hot a cable runs", and
  that an impure cable cannot sustain a pure generator's output. The coherent reading is that R is dropped
  from the voltage solve on AC but kept in the thermal model, which is not what "ignored" says.
* **The battery is blocked by non-ferrous, not by chemistry, and nobody has written that down.** A lead-acid
  cell needs lead (non-ferrous, deferred) and sulphuric acid; a Leclanché dry cell needs a zinc can, and
  zinc retorts are on the same deferral list ([scope.md](../../scope.md) § Non-ferrous). Both of the
  battery's two named chemistries land on a deferred material. Derived here, recorded nowhere else -
  flagged rather than ruled.
* **"Heavy cable required at the generator"** is a build gate on the kickstart. The first dynamo can only
  be wired with impure heavy cable, and whether impure heavy cable survives a pure dynamo's 28.8 kW is
  exactly the melt rule - so the cable tier is an upgrade path the bootstrap loop implies but never states.
* **[overview.md](../../overview.md) puts off-spine copper inside the tech-tree spine** at step 4
  ("HP power + pure copper → electric tier") while its own add-on table calls copper off-spine and
  unscheduled. Already logged in [scope.md](../../scope.md) § Gotchas; repeated here because a reader
  arriving at the grid from that line will start with the wrong picture.

---

## Open

* **Whether electrical joins the exlib graph at all.** Every live family reduces a connected node set to
  one state object; a circuit needs per-node values. It may be a `BlockNetwork` whose `State` is a solved
  node table, or it may want its own traversal. Nothing has been decided, and it is the first decision.
* **AC + DC as two registered types, or one type with a mode.** Two types (`"ac"`, `"dc"`) fits
  `BlockNetworkNode.NetworkType`'s one-string-per-block contract; the rectifier then follows the flywheel's
  two-family pattern (host a behaviour for the other side) or the transmission's (read both without
  merging). Both precedents are live and neither has been chosen.
* **When the circuit re-solves.** The shared network tick is 1000 ms
  (`src/ExpandedLib/Blocks/Networks/BlockNetworkModSystem.cs:42-45`). A sink cutting out changes the currents
  of every other sink on its path, so a grid may need to iterate to a fixed point within one tick rather than
  step once like the shaft does.
* **Where cable heat is stored.** `I²R` accumulating per segment implies a temperature on every cable block -
  potentially hundreds of block entities on a long run. A per-network or per-segment-run temperature
  (as the pipe network does with its one uniform temperature) is the cheap alternative and changes what
  "a long impure run melts in the middle" looks like.
* **Whether the source-ceiling rule is ported to torque-on-inertia** (see § How it relates). This is a
  cross-mod decision between elex and hpex, not an elex-internal one.
* **Whether the grid re-uses the pipe network's joint-family rule** to keep tiers apart
  ([conventions.md](../../conventions.md)). Cable/heavy-cable/inset is a three-tier family with exactly
  the same "cheap segment on an expensive run" temptation the pipe tiers already solved - except here the
  weakest segment does not cap the run, it melts.
