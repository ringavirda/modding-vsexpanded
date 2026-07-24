# Electrical Expanded (elex)

The electric endgame tier. **elex** builds a full-realism AC/DC power grid on top of the whole spine: it
is the **deepest dependency in the family** — it needs **HP power** (the hpex Tandem Corliss to spin its
generators), **steel** (smex, for HSS feedstock and heavy plate) and the **chemistry add-on** (for good
electrodes) — and so it **ships last**. Everything below is *(planned)*; nothing is built yet. This is
the realism tier: the grid is a solved circuit, not a toy power budget. Units and invariants are in
[conventions.md](conventions.md); materials in [materials.md](materials.md) — cited, never restated.

Electricity is **two coupled sub-networks on the exlib graph** (AC + DC), the fourth network family
alongside molten / pipe / MP (see conventions.md → Networks). It is a **separate medium from the pipe
networks** — R1 (single medium) governs pipes, not wires; the electrical rules below are elex's own.

---

## Electrical networks — the grid model

Transmit on AC, consume on DC. All thresholds tunable.

| | **AC backbone** | **DC drops** |
|---|---|---|
| Made by | Alternator (slip rings) | Dynamo directly, or rectifier off AC |
| Carries | high-voltage / low-current transmission | local end-consumption |
| Resistance | **ignored** (loss-free over distance) | **summed** — every wire + block adds R |
| Voltage | held ~fixed (field regulation abstraction) | sags by `I·R_total` along the path |
| Transformable | **yes** (V↔I at constant power) | no |
| Physical signal | **frequency + phase** (tracks engine speed) | steady V |
| Feeds | light bulbs, transformers, arc furnace (3-phase) | electrolysis, batteries, small loads |

**Circuit model (DC).** The graph is solved as a radial tree with implicit return — cable is a
**doubled conductor** (out + return in one block), so each segment's resistance is already round-trip and
there are no loops. Generators are sources at fixed potential; machines/outlets/batteries are
constant-power sinks pulled toward return.

- Node voltage = source potential − accumulated `I·R` along its path. Farther / more loaded → more sag.
- A sink draws `I = P_rated / V`; as its node sags, current rises → more `I²R` loss → more sag, until it
  falls below the sink's **minimum-voltage cutoff** and the sink **cuts out**. Brownout is emergent.
- **`I²R` becomes heat, accumulated — that is what melts cable.** Brief surges survive; sustained
  over-current drives cable temperature up until it melts and breaks the circuit. So an impure or thin
  cable can't sustain a pure-copper generator's full output, and a long impure run bleeds a large
  fraction of the power as heat before it reaches the load.
- **Source ceiling = the driving engine, not the wire.** The generator supplies whatever the tree draws,
  up to the Corliss's max power (see hpex). Demand past that **stalls the engine** and drops the whole
  network (and any shared MP load) at once.

**Resistance = f(purity, length)** — impure (converter) copper is high-R, **pure (electrolytic) copper**
low-R, and R stacks with run length. Purity barely matters on the HV AC backbone; its payoff is DC-drop
reach and how hot a cable runs (see materials.md for the two coppers).

**AC frequency & phase.**

- **Frequency tracks engine speed.** Under the Corliss governor the engine slows as load rises → frequency
  sags with load, zero at stall. Voltage is held ~fixed; frequency is the physical signal.
- **Paralleling alternators needs synchronisation** — matching frequency *and* phase. A **synchroniser**
  block locks them and shares load; after sync the network is one coherent frequency/phase, so this
  concern is localised to the generation side.
- **Rectifiers are frequency-gated** — a rectifier only converts within a frequency band, so a bogged
  Corliss whose frequency droops out of band **drops its DC side** before a full mechanical stall.
- **Three-phase (heavy-power tier)** — three phase-offset lines (a three-phase alternator, or three
  synchronised single-phase ones 120° apart). Splitting the load across three conductors is the only way
  to carry that current. Payoffs: the **arc furnace runs on three-phase AC directly** (one electrode per
  phase, no rectifier), and big DC loads feed a **three-phase rectifier → one smooth high-power DC bus**.

### Conductors & grid hardware

| Block | Block-type | What it is | Status |
|---|---|---|---|
| **Cable** | block (on a face) | surface-run doubled conductor; normal capacity; **melts under sustained over-current** | *(planned)* |
| **Heavy cable** | block (on a face) | high-current tier; **required at the generator and at high-power machines** (arc furnace) | *(planned)* |
| **Inset cable** | block (inset) | hidden in-block wall/floor wiring | *(planned)* |
| **Inset outlet** | block (inset) | AC tap for lighting / low-draw loads | *(planned)* |
| **Power pole** | megablock (tall) | long-distance spans; click pole→pole to draw a beam-style cable line. **2 normal + 1 heavy** cable slots, sneak to select | *(planned)* |
| **Transformer** | block | AC → AC at stepped voltage (step-up before a long run, step-down at delivery); iron core + copper coil | *(planned)* |
| **Rectifier** | block (AC-driven) | remote single-phase **AC → DC** at point of use; **frequency-band gated** (rotary / mercury-arc) | *(planned)* |
| **3-phase rectifier** | megablock (AC-driven) | three phase-offset AC lines → one smooth high-power **DC bus** (big electrolysis banks) | *(planned)* |
| **Synchroniser** | block | matches **frequency + phase** to parallel alternators and share load; synchroscope | *(planned)* |
| **Battery** (acid / dry cell) | megablock (store) | **DC ↔ stored DC** backup — keeps lamps and low loads lit when the generator is idle (the electric analog of the gasholder) | *(planned)* |
| **Wire extruder** | megablock | **MP-driven** (needs lpex MP, not electric): copper rod/plate → **wire**, impure or pure; pure wire lowers cable R and **unlocks alternator windings** | *(planned)* |

---

## Generation — dynamo & alternator

Both are **RCC megablock** flywheel variants that upgrade the **hpex Tandem Corliss** in place (only it
can drive them; ~36 kW at the shaft — see hpex). Output = `engine kW × coil efficiency`; **efficiency is
set by coil-wire purity** — impure ~20 %, pure ~80 % (elex-owned, tunable). Engine kW and generator kW
both show in block-info. Upgrading removes the MP output — a generator is not also an MP source.

| Machine | Block-type | Power in | Input → Output | Key mechanic | Status |
|---|---|---|---|---|---|
| **Dynamo (DC)** | RCC megablock (Corliss flywheel variant) | Tandem Corliss | flywheel → **DC @ fixed V** | integral commutator; buildable from **impure or pure** copper; wires straight to a load, no transformer/rectifier | *(planned)* |
| **Alternator (AC)** | RCC megablock (Corliss flywheel variant) | Tandem Corliss | flywheel → **AC @ fixed V, freq ∝ speed** | slip rings; single- or three-phase; **gated behind pure copper** (windings need it) | *(planned)* |

**Output tiers (tunable):** impure dynamo ~7.2 kW · pure dynamo ~28.8 kW · pure alternator ~28.8 kW each.

**Bootstrap loop** (why the tiers exist):

1. **Impure-copper dynamo (~7.2 kW)** — the kickstart. Enough for exactly **one electrolysis cell**,
   wired straight to DC. Its whole job is to refine the *first* batch of pure copper; it never scales.
2. **Pure copper unlocks the rest** — a **pure-copper dynamo (~28.8 kW)** runs ~4 electrolysis cells, and
   pure copper is what **lets you build alternators**.
3. **Alternators (AC, ~28.8 kW each)** carry power long-distance and feed heavy loads; a pure-copper
   supply also lights a *lot* of bulbs — electricity's civic payoff.

---

## Consumers

### Arc furnace — the universal electric melter

The heaviest consumer and the real endgame gate. Powered by **three-phase AC directly** (one electrode
per phase, no rectifier) it **bypasses the fuel/air-blast chain** — no coke, cowpers or blowers — at a
heavy electrical cost. Its temperature runs the shared **dynamic heat balance** (see conventions.md):
`T_arc = f(delivered electrical power) − charge_sink`, so **voltage sag or frequency droop → less power →
cooler arc → it won't melt** — electricity and metallurgy become one system. All roles are
mass-conserving with no yield bonus (R2).

- **Footprint / power in / status:** multiblock · **three-phase AC**, three synchronised alternators
  (~86 kW; a genuine 3-engine + 3-alternator + synchroniser power-plant build) · refractory + heavy plate
  + electrodes · *(planned)*.

| Role | Input → Output | Notes | Rate (tunable) |
|---|---|---|---|
| **HSS melt** | **open-hearth (low-N) steel** scrap/ingot + 3-phase AC → **molten low-N base** | high-N Bessemer steel is too high in nitrogen; **W + Cr are alloyed in the ladle** (R3, see materials.md) | ~10 u/s |
| **Scrap / recycle remelt** | crushed waste alloy / ingot iron / scrap + AC → **molten base metal** | coke-free late-game route for the recycle loop | ~30 u/s |
| **Direct iron smelter** *(blast-furnace alt.)* | crushed iron ore (+ minor C reductant) + AC → **molten ingot iron (~0 % C) + slag** | Stassano-type; **recarburise in the ladle** to a target steel — replaces smelting, not carburising/alloying | ~25 u/s |
| **Copper smelter** *(reverberatory alt.)* | crushed copper ore + AC → **copper matte + slag** | replaces the reverberatory **only**; Pierce-Smith → blister → electrolysis chain still required (smex-copper) | ~20 u/s |

> One-line footnote: authentic — the Stassano furnace (~1898) smelted iron ore to steel electrically, and
> the Héroult three-phase arc furnace (~1900) is exactly one electrode per phase.

### Electrolysis cell & light bulb

| Machine | Block-type | Power in | Input → Output | Key mechanic | Status |
|---|---|---|---|---|---|
| **Electrolysis cell** | block | **DC** (~7.2 kW each, tunable) | impure copper plate (anode) + DC → **pure copper plate** (cathode) | **acid electrolyte** = one-time fill + slow top-up (acid from the chemistry add-on; regenerates); refines the pure copper that unlocks alternators | *(planned)* |
| **Light bulb** | block | AC or DC | current → light | very low draw — one generator lights many; hangs off AC directly via an inset outlet | *(planned)* |

---

## Materials owned & electrodes

elex owns two materials on the catalogue (full compositions and rules in [materials.md](materials.md) —
not restated here):

| Material | Made by | One-liner |
|---|---|---|
| **Pure copper** | electrolysis cell | electrolytic, high-purity; low-resistance cable; **unlocks alternators (AC)** |
| **HSS** | arc furnace (melt) + ladle (W + Cr) | hot-hard top tool alloy; **needs no tempering** |

Arc-route **ingot iron** (~0 % C) is also produced here, sharing ownership with smex's Bessemer over-blow
(see materials.md); it is **not** wrought iron — recarburise or re-melt.

**Electrodes — the arc furnace's running cost.** Beyond power, a three-phase furnace burns **three carbon
electrodes** that deplete with arc-hours and are **replaced in-world** (sneak + RMB, wear-consumable like
drill bits / dies).

- **Good electrodes** are made in the **chemistry add-on** from **calcined petroleum coke + coal-tar
  pitch** — low wear, high conductivity. This is why the endgame furnace leans on the coal-and-oil branch.
- **Fallback:** with the chemistry add-on absent, the furnace accepts a simpler **charcoal/coke carbon
  electrode** — higher wear, lower conductivity — so Stage V is **never hard-blocked**. The chemistry
  electrode is the *good* one, not the *only* one.

All interaction is in-world and block-info-readable (R7); no GUI windows.