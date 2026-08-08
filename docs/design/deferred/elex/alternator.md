# Alternator (AC generator)

**Status** deferred   **Would live in** elex (Electrical Expanded) - a mod with no project, no asset domain
and no code   **Deferred by** two independent gates: D8 ([STATE.md](../../../plans/STATE.md)), which puts all
of elex outside the release target, and the pure-copper winding requirement, the one elex block with no
in-scope fallback. Both are recorded in [scope.md](../../scope.md) (§ The release target, § elex, § The shape
elex would take).

**Owns**

* AC generation - slip rings, single- vs three-phase, and the generation-side half of frequency and phase;
* the pure-copper gate and the chain behind it - why the alternator is the block the chemistry question
  lands on, and how severe that is;
* the core-material question: that silicon steel is the historically correct answer, that it is queued for
  elex and built nowhere, and what it would cost to add;
* the prime-mover gap - the Tandem Corliss is elex's only stated drive, and the mechanical design does not
  know it exists.

**Does not own** - cited only:

| Fact | Owner |
|---|---|
| The cut, elex's three chemistry severities, the release target | [scope.md](../../scope.md) |
| The circuit model, the hardware set, the `mpenergy` comparison, the AC/DC table | [electrical-grid.md](electrical-grid.md) |
| The shared Corliss-flywheel-variant form, the "no MP output" rule, the bootstrap loop | [dynamo.md](dynamo.md) |
| The electrolysis cell, the electrolyte-not-reagent argument, and the vanilla-acid finding | [electrolysis-cell.md](electrolysis-cell.md) |
| The arc furnace, the electrode consumption rate, the HSS chain | [arc-furnace.md](arc-furnace.md) |
| Wire, the extruder and D7 | [wire-extruder.md](wire-extruder.md) |
| The Tandem Corliss and every hpex number | the archived hpex spec (git history); the figures survive on these pages |
| The live mechanical model, its numbers and its code | [mechanics/mp-energy.md](../../mechanics/mp-energy.md) |
| Hadfield steel, the material-gated power tiers, the alloy compositions | [materials.md](../../materials.md) |
| Ladle alloying and the ferroalloy family | [processes/alloying.md](../../processes/alloying.md) |

**Depends on** [dynamo.md](dynamo.md) · [electrical-grid.md](electrical-grid.md) ·
[scope.md](../../scope.md) · the archived elex and hpex specs ·
[mechanics/mp-energy.md](../../mechanics/mp-energy.md) · [materials.md](../../materials.md)

---

## What it is

An **alternator** is the same rotating machine as the dynamo with the commutator thrown away. Instead of a
split ring that mechanically reverses each coil's connection, it has plain **slip rings** - continuous
conductors - so the alternating voltage the coils produce comes out as alternating current. A commutator is a
precision consumable with brushes arcing across dozens of segments; a slip ring is two rings.

What it buys is the thing DC cannot do: AC can be transformed, so the same power travels as high voltage and
low current and the `I²R` loss falls with the square of the current.

Two properties follow from the physics and both are modelled:

* Frequency is a mechanical fact - shaft speed times pole pairs, so a governed engine holds a frequency and a
  labouring one droops.
* Two alternators cannot simply be wired together. They must be brought to the same frequency and the same
  phase first, or they fight; matching them is **synchronising**, done on a synchroscope.

**Three-phase** (1891) is three sets of windings 120° apart on one machine. Héroult's arc furnace (~1900) is
one electrode per phase, which is why this suite cares.

---

## Why it is deferred

elex sits after the release target; the cut is [scope.md](../../scope.md)'s and is not re-argued here. The
alternator is deferred one level deeper than the rest of elex:

| Gate | Blocks | Severity |
|---|---|---|
| D8 - elex is outside the release target | all of elex | scope decision |
| Pure copper windings ([materials.md](../../materials.md)) | the alternator alone | the only elex block with no in-scope fallback |

The chain is `alternator ← pure copper ← electrolysis cell ← sulphuric acid`, and
[scope.md](../../scope.md) § elex rates that acid "probably one small recipe", not a wall - it is an
electrolyte, not a reagent (a one-time charge plus top-ups), the period route is the lead chamber process and
both its inputs are vanilla. So the alternator is gated on a small, in-period recipe, not on a chemistry
industry. It is nevertheless the only elex block that stops dead if that recipe is never written, which is
why [scope.md](../../scope.md)'s no-chemistry subset ends with "but no alternators".

It may be smaller still. [electrolysis-cell.md](electrolysis-cell.md) reports that the acid already exists in
the base game - `game:acid-full-sulfuric`, from a vanilla cooking recipe
(`assets/survival/itemtypes/liquid/acid.json:9`, `assets/survival/recipes/cooking/acid.json`) - which would
move this gate from "probably one recipe" to no new content at all. That finding is the cell's to own; if it
holds, what stands between a player and AC is the copper anode (see below), not the bath.

There is a second, unrecorded gate: the impure copper that feeds the electrolysis cell is converter copper,
deferred as non-ferrous. See [electrical-grid.md](electrical-grid.md) § Gotchas - the same fact decides
whether the DC half is buildable either.

---

## What exists today

Nothing.

| Probe | Result |
|---|---|
| `grep -rniE "alternator\|slip ring\|three-phase\|synchronis" src/ --include=*.cs` | 0 hits |
| `grep -rniE "elex\|electric\|alternator" assets/ .github/` | 0 hits - no `assets/elex/`, no blocktype, no shape, no lang key |
| `grep -rniE "silicon steel" docs/` | 0 hits. The material is named nowhere else in the design tree - this page is its first mention |
| Projects in `VintageStory.sln` | no `ElectricalExpanded` |

Its platform does not exist either: the Compound/Tandem Corliss is *(planned)* with no build table
([machines/bearings.md:315](../../machines/bearings.md)).

---

## The design as it stands

### The machine (archived elex spec)

| Property | Value |
|---|---|
| Block-type | RCC megablock, a Corliss flywheel variant - the same form as the dynamo ([dynamo.md](dynamo.md)) |
| Prime mover | the hpex Tandem Corliss, and only it - ~36 kW at the shaft |
| Input → output | flywheel → AC at fixed voltage, frequency ∝ speed |
| Key mechanic | slip rings; single- or three-phase |
| Material | gated behind pure copper - the windings need it. No impure variant exists |
| Output | ~28.8 kW each (= 36 kW × ~80 % pure-coil efficiency) |
| Cost of the upgrade | removes the MP output, same as the dynamo ([dynamo.md](dynamo.md)) |
| Bearings | required - the bearing gate lands on the Corliss and its variants ([machines/bearings.md:253](../../machines/bearings.md), [:271](../../machines/bearings.md)) |

### What the AC side buys

| Only AC can | Consequence |
|---|---|
| be transformed (V↔I at constant power) | step-up before a long run, step-down at delivery - the loss-free backbone |
| carry power with resistance ignored over distance | the reach of the whole grid; the DC row sums R and sags |
| run the arc furnace directly | three-phase, one electrode per phase, no rectifier in the plant |
| feed a 3-phase rectifier | one smooth high-power DC bus for big electrolysis banks |

### The generation-side concerns are the alternator's own

Frequency and phase are made here, and the archived elex spec confines the difficulty to the generation side:
once a synchroniser has locked two machines, *"the network is one coherent frequency/phase, so this concern is
localised"*. Everything downstream sees one number.

The arc-furnace plant is a build, not a machine. ~86 kW of three-phase means three engines + three
alternators + a synchroniser - 3 × 28.8 = 86.4. It is the largest single construction in the suite, and it
has no footprint, no layout and no multiblock spec anywhere.

### Silicon steel — the core material, named here for the first time

No table above states what the core is made of. The one place a core material appears at all is the
transformer: *"iron core + copper coil"*. Plain iron is the wrong answer:

| | Plain iron core | Silicon steel |
|---|---|---|
| What it is | soft iron laminations | Fe + ~3–4 % Si |
| Why it matters | high hysteresis and eddy-current loss - the core heats and the machine wastes power | roughly halves core loss and raises resistivity, so eddy currents fall |
| Date | — | Hadfield, 1900 |
| Used for | — | transformer and alternator cores, from 1900 to today |

It is the same metallurgist the suite already ships. Robert Hadfield's manganese steel (~1882) is in
[materials.md](../../materials.md) and gates the whole HP tier; his silicon steel (1900) is the electrical
one, and it lands at the arc-furnace/HSS date the tier is already pinned to (the
[materials.md](../../materials.md) timeline). The suite already plans its input: ferrosilicon is named as
part of the cold blast furnace's ferroalloy family alongside FeMn and FeCr, and hadfield is already made by
ladle addition of an element to a mild steel base
([processes/alloying.md](../../processes/alloying.md)). So silicon steel is one catalogue row and one ladle
ratio in machinery that exists - the identical shape to the hadfield recipe.

Status: queued for elex, not built. It has no row in [materials.md](../../materials.md), no composition, no
owner and no consumer, and there is no core-loss model for it to gate - so adding it today would buy flavour
and a build gate, not behaviour. The answer is silicon steel, the cost is small, and the reason to wait is
that nothing measures core loss yet.

---

## The prime mover — the top of the mechanical chain is specified in the old model

| Document | What it says about what drives the generators |
|---|---|
| the archived elex spec | both generators *"upgrade the **hpex Tandem Corliss** in place (**only it** can drive them; ~36 kW at the shaft)"* |
| the archived hpex spec | the Tandem Corliss is *(planned)*, ~36 kW, *"the drive for the elex arc-furnace and alternator banks"* |
| [mechanics/mp-energy.md](../../mechanics/mp-energy.md) § Role | the prime mover is a vanilla waterwheel or windmill; *"in lpex the player swaps the vanilla producer for a steam engine; the network itself is unchanged"*. The chain stops there - the live mechanical design never mentions hpex or the Corliss |

The entire top of the mechanical chain - a governed HP engine, its 36 kW, and the two machines that hang off
it - is specified only in the archived elex and hpex specs, in the vocabulary of the model `mpenergy`
replaced. Three concrete consequences:

1. "36 kW at the shaft" is not a quantity the live network has. A producer implements
   `IMpEnergyProducer.DriveTorque(ω)` - a torque–speed curve, not a flat power, because a flat power lets an
   under-powered drive buffer its way past any load
   ([mechanics/mp-energy.md](../../mechanics/mp-energy.md) § Torque governs, not power). The Corliss must be
   specified as a curve before a generator can read anything off it, and no such curve exists.
2. The scale is off by ~4 orders of magnitude. The live bridge supplies `1 N·m × 2 rad/s = 2 W`
   (`src/IronworkingExpanded/IwexConfig.cs:467`, `src/ExpandedLib/ExlibConfig.cs:98`), against elex's 36 kW.
   Worked out on [electrical-grid.md](electrical-grid.md) § How it relates.
3. Even the lpex engine is not on that network yet. It is still a pure vanilla-MP source (it implements no
   `IMpEnergy*`), reaching this network only indirectly through a vanilla axle into the flywheel hub
   ([mechanics/mp-energy.md](../../mechanics/mp-energy.md)). The Corliss would be the second engine to need
   that connection, and the first one never got it.

The cheapest resolution is probably that the generator never touches `mpenergy` at all - it reads its engine
directly, the way a sub-machine reads its engine today, and the mechanical network keeps its own scale. That
is a decision, not a fact; it is recorded as Open below and on
[electrical-grid.md](electrical-grid.md).

---

## What it would unblock

| Waiting on the alternator | Severity | Why |
|---|---|---|
| [Arc furnace](arc-furnace.md) | wall | it is fed by three-phase AC directly. A dynamo cannot feed it and there is no inverter |
| HSS | wall (and a second, separate one) | arc melt + ladle W/Cr ([materials.md](../../materials.md)) - and tungsten has no source anywhere in the suite ([processes/alloying.md:289](../../processes/alloying.md)), so HSS is blocked twice. Chain owned by [arc-furnace.md](arc-furnace.md) |
| Long-distance power, transformers, power poles | wall | the loss-free backbone is AC-only; DC sums resistance ([electrical-grid.md](electrical-grid.md)) |
| Lighting at range - electricity's civic payoff | wall | a bulb runs on either current, but a lot of bulbs spread over a base needs the backbone |
| 3-phase rectifier → big DC bus / electrolysis banks | wall | its input is three phase-offset AC lines |
| The arc-route ingot-iron and scrap-remelt paths | degraded path | both are alternatives to routes that already exist ([materials.md](../../materials.md), [processes/recarburising.md:93](../../processes/recarburising.md)) - coke-free convenience, not capability |

Nothing outside elex waits on it.

---

## Gotchas

* The synchroniser is given two contradictory jobs. Paralleling means locking machines to the same phase;
  three-phase from three single-phase alternators means holding them at a fixed 120° offset. Full statement
  on [electrical-grid.md](electrical-grid.md) § Gotchas - noted here because it is the alternator's
  operation, not the grid's.
* "Pure copper" is load-bearing in exactly two places and they are not the same place. The windings
  hard-require it; the cable does not - *"purity barely matters on the HV AC backbone"*, since the backbone
  ignores resistance. So the alternator's copper gate is about the machine, and a player who has enough pure
  copper to wind one has no particular reason to spend more on transmission. Worth designing around, or the
  second batch of pure copper has no buyer.
* The gate is a one-time hurdle, not a running cost, and "gated behind pure copper" reads like the opposite.
  The electrolysis bath is an electrolyte, not a reagent - one charge plus top-ups
  ([scope.md](../../scope.md) § elex). Once the cell runs, pure copper is limited by power, not by chemistry.
  Anyone reading the header table will assume a supply chain that does not exist.
* Frequency droop is the only operating consequence of load on the AC side, and it is invisible without a
  readout: the rectifier's frequency-band cutout is the mechanic that makes it felt, so a grid with no
  rectifier on it never surfaces frequency at all. Under R7 ([conventions.md](../../conventions.md)) the
  synchroscope is the only place the number lives, and it is close to the strongest case in the suite for a
  GUI window.
* Three alternators on one network is the design's own hardest case and it is one parenthesis. ~86 kW, three
  engines, three boilers' worth of steam, three sets of bearings, a synchroniser and enough heavy cable to
  reach the furnace. It has no footprint, no build table and no layout, and the Corliss it multiplies by
  three has no build table either ([machines/bearings.md:315](../../machines/bearings.md)).

---

## Open

* Whether the generator is on `mpenergy` at all (see § The prime mover). Everything else about the drive side
  waits on this one.
* Whether the Corliss gets a torque–speed curve, or `mpenergy` gets rescaled into real watts. The two answers
  produce very different hpex config tables, and hpex is on the release path while elex is not - so the
  decision may need making before elex, not with it.
* Whether silicon steel is added as a material. One catalogue row and one ladle ratio; the reason to wait is
  that no core-loss model exists to gate. Related: whether the transformer's *"iron core"* is a placeholder
  or a deliberate cheaper tier below it - the two-tier reading is more interesting and costs nothing extra.
* Single 3-phase alternator vs three synchronised single-phase ones. The archived elex spec offers both; the
  first is one block and one build, the second is the power-plant fantasy. They imply different synchroniser
  semantics and different arc-furnace build costs.
* What "gated behind pure copper" gates, exactly - the recipe, the RCC stage, or the windings as a consumed
  part. If the windings are a part, they can also fail and be rewound, which would give the alternator a
  consumable sink of its own the way electrodes give the arc furnace one.
* Whether an alternator can be downgraded to a dynamo (or the reverse) on the same Corliss. Both are variants
  of one flywheel replacement, so it is nearly free - and it is the natural way to let a player who built DC
  first move to AC without demolishing the engine.
