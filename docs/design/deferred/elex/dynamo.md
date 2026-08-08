# Dynamo (DC generator)

**Status** deferred   **Would live in** **elex** (Electrical Expanded) - a mod with no project, no asset
domain and no code   **Deferred by** **D8** ([STATE.md](../../../plans/STATE.md)); the decision, its reasoning and
the release target are recorded in [scope.md](../../scope.md)

**Owns**

* the DC generator - the commutator machine, its three output tiers, and the coil-purity efficiency lever;
* the form both generators share: an RCC megablock Corliss flywheel variant, and the rule that upgrading
  removes the MP output - a generator is not also an MP source;
* the bootstrap loop - why an impure tier exists, why it is a dead end, and the one-dynamo-one-cell identity
  the tier's arithmetic rests on;
* why the dynamo is the unblocked half of elex's generation, and the one unrecorded fact that decides
  whether that is true.

**Does not own** - cited only:

| Fact | Owner |
|---|---|
| The cut, the carve-outs, elex's chemistry severities, the release target | [scope.md](../../scope.md) |
| The circuit model, the hardware set, the `mpenergy` comparison | [electrical-grid.md](electrical-grid.md) |
| AC generation, the pure-copper gate, the prime-mover gap | [alternator.md](alternator.md) |
| The electrolysis cell - its bath, its anode, and the vanilla-acid finding | [electrolysis-cell.md](electrolysis-cell.md) |
| The arc furnace and the electrode consumption rate | [arc-furnace.md](arc-furnace.md) |
| Wire, the extruder and D7 | [wire-extruder.md](wire-extruder.md) |
| The Tandem Corliss (~36 kW, *(planned)*) | the archived hpex spec (git history) |
| The live flywheel as hardware, and `mpenergy`'s model | [machines/flywheel-and-shafting.md](../../machines/flywheel-and-shafting.md) · [mechanics/mp-energy.md](../../mechanics/mp-energy.md) |
| Who needs bearings and why the requirement stops at the Corliss | [machines/bearings.md:241-271](../../machines/bearings.md) |
| Pure copper as a material | [materials.md](../../materials.md) |

**Depends on** [electrical-grid.md](electrical-grid.md) · [alternator.md](alternator.md) ·
[scope.md](../../scope.md) · the archived elex and hpex specs ·
[mechanics/mp-energy.md](../../mechanics/mp-energy.md) ·
[machines/flywheel-and-shafting.md](../../machines/flywheel-and-shafting.md) ·
[machines/bearings.md](../../machines/bearings.md)

---

## What it is

A **dynamo** is a rotating machine with a **commutator**: a split ring of segments the brushes slide over,
which reverses the connection to each coil exactly as that coil's induced voltage would reverse. The machine
generates alternating current internally and the commutator rectifies it mechanically, at the shaft. That is
the entire difference from the alternator: no external rectifier, no transformer, no theory of phase.

Gramme's ring dynamo (1871) was the first commercially useful one. By the 1880s a bipolar dynamo belted or
direct-coupled to a mill engine was the standard installation, powering arc lamps and the electroplating and
electrorefining baths that are the job it does here. The voltage it makes is the voltage it delivers, so the
copper is thick and the customer is close.

---

## Why it is deferred

elex sits after the release target. The decision is **D8**, and the whole cut is owned by
[scope.md](../../scope.md).

The dynamo survives every one of elex's three recorded chemistry dependencies:

| elex's recorded chemistry dependency | Does the dynamo need it? |
|---|---|
| Graphite arc-furnace electrodes | no - the dynamo has no electrodes |
| Sulphuric-acid electrolyte | no - it powers the cell; the acid is inside the cell |
| Sulfur for copper roasting | no |

[scope.md](../../scope.md) § The shape elex would take names DC only as the subset that could ship without
any chemistry at all. The dynamo is that subset's generator.

Caution - see § Gotchas: its copper may be deferred anyway, by D8's other half, non-ferrous, which is a
different deferral from the one everyone checks.

---

## What exists today

**Nothing.**

| Probe | Result |
|---|---|
| `grep -rniE "dynamo\|commutator\|generator" src/ --include=*.cs` | dynamo: 0. commutator: 0. The only `generator` hits are `ExpandedLib.Generators` (the source generator) and `BEBehaviorEngineMPGenerator` - the lpex vanilla-MP engine behaviour, unrelated |
| `grep -rniE "elex\|electric\|dynamo" assets/` | 0 hits - no `assets/elex/` domain, so no blocktype, no shape, no lang key |
| Projects in `VintageStory.sln` | no `ElectricalExpanded` (full list on [electrical-grid.md](electrical-grid.md) § What exists today) |

The platform does not exist either. The Corliss engine and the Compound/Tandem Corliss are both *(planned)*
with no build table - logged at [machines/bearings.md:315](../../machines/bearings.md) (*"pure guess. It
should fall out of the Corliss's build table, which does not exist"*) and again at
[:404](../../machines/bearings.md). The dynamo is a variant of a block that is itself a variant of an unbuilt
block.

---

## The design as it stands

### The machine (archived elex spec)

| Property | Value |
|---|---|
| Block-type | RCC megablock, a Corliss flywheel variant - it replaces the flywheel on the engine, in place |
| Prime mover | the hpex Tandem Corliss, and only it - ~36 kW at the shaft |
| Input → output | flywheel → DC at fixed voltage |
| Key mechanic | integral commutator; wires straight to a load - no transformer, no rectifier |
| Material | buildable from impure or pure copper - the only elex generator that accepts impure |
| Output | `engine kW × coil efficiency`, efficiency set by coil-wire purity: impure ~20 %, pure ~80 % |
| Readout | engine kW and generator kW both show in block-info - the loss is visible, not implied |
| Cost of the upgrade | it removes the MP output. A generator is not also an MP source |
| Bearings | required - the Corliss and its variants are the machines the bearing gate lands on ([machines/bearings.md:253](../../machines/bearings.md), [:271](../../machines/bearings.md)) |

### The three tiers, and the loop they exist for

| Tier | Output | Runs | Its whole job |
|---|---|---|---|
| Impure-copper dynamo | ~7.2 kW | exactly one electrolysis cell (~7.2 kW) | refine the first batch of pure copper. It never scales |
| Pure-copper dynamo | ~28.8 kW | ~4 electrolysis cells | the working DC plant; pure copper is also what lets you build alternators |
| *(pure alternator)* | ~28.8 kW each | the AC backbone and heavy loads | [alternator.md](alternator.md) |

```
Tandem Corliss (36 kW)
        │
        ├── impure coils (20 %) ──▶  7.2 kW DC ──▶ 1 electrolysis cell ──▶ first pure copper
        │                                                                        │
        │   ◀─────────────── rebuild the coils ────────────────────────────────┘
        │
        └── pure coils   (80 %) ──▶ 28.8 kW DC ──▶ 4 cells  ──▶ pure copper at volume
                                                                    │
                                                                    ▼
                                                           alternator windings (AC)
```

The build-complexity-buys-operating-efficiency pillar ([overview.md](../../overview.md)) as a single number:
the same machine is built twice, and the second is four times better because the first one's entire product
is the material the second is wound with. Nothing is unlocked by a recipe.

The impure tier is a dead end by design. It is not a worse-but-viable path (R5's usual shape,
[conventions.md](../../conventions.md)); it is a bootstrap, whose only output is the ability to stop using
it. 7.2 kW against a 7.2 kW cell leaves zero margin, so the kickstart plant runs exactly one cell and cannot
be stretched.

---

## What it would unblock

| Waiting on the dynamo | Severity | Why |
|---|---|---|
| [Electrolysis cell](electrolysis-cell.md) | wall | it is a DC sink and the only DC sources are the dynamo and a rectifier off AC - and AC needs the alternator, which needs what the cell makes. The dynamo is the only way into the loop |
| Pure copper | wall | same chain ([materials.md](../../materials.md)) |
| Alternator | wall, transitively | its windings need pure copper ([alternator.md](alternator.md)) |
| Arc furnace / HSS | wall, transitively | three-phase AC → alternators → pure copper → cell → dynamo |
| Light bulbs, small loads | degraded path | a bulb takes AC or DC, so a dynamo lights a shop on day one - just not a village, because DC sags |

The dynamo is the root of the electrical tier: every other elex consumer traces back through the electrolysis
cell to it. If elex is brought forward, this is the first thing built, and [scope.md](../../scope.md)'s
no-chemistry subset is the part of elex reachable from here.

Nothing outside elex waits on it. It is a leaf of the ferrous line, not a link in it.

---

## Gotchas

* **"Flywheel variant" is ambiguous, and the two readings are different code.** The Corliss's output is
  *"MP (heavy flywheel drive)"*, and iwex already ships a flywheel block that is the `mpenergy` storage node
  and the vanilla-MP bridge
  ([machines/flywheel-and-shafting.md](../../machines/flywheel-and-shafting.md)). So "Corliss flywheel
  variant" means either (a) a new variant of `BlockFlywheel` - a real graph node with `Inertia`, hub cells
  and a hosted `BEBehaviorMPFillerPort` - or (b) an engine sub-machine in the pump/blower sense
  (`BEBehaviorMPSubmachineBase`), a separate pattern with its own orientation snap and animation phase-lock.
  Nothing chooses. The two differ in whether the generator is on the mechanical network at all.
* **"Upgrading removes the MP output" may delete the shop's mechanical reservoir.** If the generator replaces
  the flywheel under reading (a), converting it removes a storage node - and `MpEnergyNetwork.OnTick` nulls
  the whole network state when `Σ I ≤ 0` ([mechanics/mp-energy.md](../../mechanics/mp-energy.md) § Gotchas,
  `MpEnergyNetwork.cs:81-89`). Every consumer on that run then reads `Speed == 0` and stops, with no error
  anywhere. The failure is silent, which R7 ([conventions.md](../../conventions.md)) would not accept: a shop
  that electrifies its engine should be told its rolling mill just lost its drive.
* **Where does the copper come from?** The impure tier's coils, the cable and the cell's anode are all impure
  copper. [materials.md](../../materials.md) sources converter copper from the reverberatory → Pierce-Smith
  chain, which is deferred as non-ferrous ([scope.md](../../scope.md) § Non-ferrous), while vanilla
  `game:metalplate-copper` exists and the wire extruder's stated input is a generic *"copper rod/plate"*.
  Unrecorded anywhere. If it is converter copper, the dynamo is gated on D8 twice and the no-chemistry subset
  does not exist; if it is vanilla copper, the subset is real. Full statement of the problem at
  [electrical-grid.md](electrical-grid.md) § Gotchas.
* **"36 kW at the shaft" has no representation in the live mechanical model.** A producer on `mpenergy`
  returns a torque-speed curve, not a kW rating ([mechanics/mp-energy.md](../../mechanics/mp-energy.md)
  § Torque governs, not power), and the live calibration puts the whole network's bridge supply at ~2 W. The
  scale gap and what to do about it are on [electrical-grid.md](electrical-grid.md) § How it relates; the
  drive-side specification gap is on [alternator.md](alternator.md) § The prime mover.
* **The efficiency lever is a build property, not an operating one.** ~20 % vs ~80 % is set by the wire the
  coils were wound with, so it is fixed at construction and can only change by rebuilding. Block-info's
  *"engine kW and generator kW"* line therefore shows a constant ratio forever.
* **A dynamo cannot reach the AC backbone, ever.** There is no inverter in the hardware set - the rectifier
  is one-way and DC is not transformable ([electrical-grid.md](electrical-grid.md) § Gotchas). The dynamo is
  not an "early alternator": it is a permanently local machine, and a player who builds a DC plant and then
  wants distance must build a second, different generator. The tier table reads like an upgrade ladder; it is
  not one.
* **The Corliss cannot be built at all yet**, so no part of this is testable. It is *(planned)* with no build
  table ([machines/bearings.md:315](../../machines/bearings.md), [:404](../../machines/bearings.md)), and the
  ~36 kW that every elex number is derived from is a bare tunable in a machine card.

---

## Open

* **Which "flywheel variant" reading is right** (see § Gotchas). It is the first decision, because it settles
  whether the dynamo is a graph node, a sub-machine, or a block that reads a network without joining it - all
  three patterns are live in the repo and they do not mix.
* **What happens to a mechanical run when its engine is electrified.** Warn and refuse? Convert and let the
  run stall? Allow both outputs at a split? The current text says a generator is not an MP source; the
  player-facing consequence has never been designed.
* **Whether an impure dynamo can be rebuilt into a pure one in place**, or must be broken and re-made. The
  RCC salvage rule (`RccBrokenDropsRatio`, archived hpex spec) decides how much of the impure copper comes
  back - a real gameplay number, since the impure machine's entire purpose is to be replaced.
* **Whether 7.2 kW / one cell is the intended margin.** Any friction in the model (a second bulb, a cable
  loss) makes the kickstart plant unable to run the one thing it exists to run. Either the cell is sized
  under the dynamo, or the loop needs a stated tolerance.
* **What a DC-only elex actually looks like in a shop.** With no AC and no transformer, the generator sits
  beside its load and there is no grid to build - a much smaller product than the § Conductors table implies.
  Worth a paragraph in whatever brings elex forward, since [scope.md](../../scope.md) names DC-only as the
  shippable subset.
