# Mechanical-energy network (full-authenticity MP) — design

Status *(audited 2026-07-26 — verified against code, not claims)*: **the plumbing is built, the authenticity is
not.** BUILT: the torque-on-inertia core (§2.3), merge/split, the flywheel as storage, the shaft/bevel run, and
all three transmissions with their coupling, clutch and animation — well test-pinned. **NOT built, despite §4/§8
listing them in phases 1–4:** *pulsed supply* (§4.1), the *governor curve* `g(ω)` (§4.2 — the only limiter is the
hard `ω_max` clamp), *over-speed burst* (§4.5 — `IsOverSpeed` exists with **zero production callers**),
*bridge back-drive* (§5), the *flywheel spin animation* (§3.1 — the shipped flywheel shapes are 14-cube stand-ins
with the authored `idle`/`cycle` dropped), the shaft/bevel spin renderer (§3.2 gap), and — the big one — **any
consumer at all**: `BlockEntityRollingMill.LoadTorque` returns a hard-coded `0f`, so *nothing in the mod draws a
single N·m*. The lpex engine is also still a pure vanilla-MP source (it implements no `IMpEnergy*`), reaching
this network only indirectly through a vanilla axle into the flywheel hub. **So phase 1 is not actually complete**
— it required "engine-as-producer + one heavy consumer" and neither exists. Owner: **exlib**
(`ExpandedLib.Networks`), beside
`PipeNetwork` / `MoltenNetwork`. A **cast-iron** MP network — a heavy-duty alternative to the wooden vanilla
network, parallel to it and bridged by the flywheel. Supersedes the "vanilla MP; constant-power generator"
line in [conventions.md](conventions.md) once complete; until then the live MP is still the vanilla model.

> The one-line thesis: **model each run as one spinning shaft — torque on inertia — not an energy bucket.**
> A drive applies torque; machines and friction resist it; the net spins a lumped inertia up or down
> (`I·dω/dt = τ_drive − τ_load − τ_fric`), and the stored energy is simply `E = ½Iω²`. Reciprocating engines
> deliver torque in **strokes**, heavy machines resist in **bites**; a heavy **flywheel** (large `I`) levels
> both by *inertia*. Crucially the flywheel is **not a battery** — a drive that can't out-torque the loaded
> resistance never spins it up, so you can never trickle-charge your way to a pulse. It is also the natural
> **bridge** from the vanilla torque network into this one.

---

## 1. Why this, and why it's not just the vanilla model

The current model ([BEBehaviorEngineMPGenerator](../../src/LowPressureExpanded/BlockStructures/Engine/BlockEntities/BEBehaviorEngineMPGenerator.cs))
is a **constant-power torque source** on the vanilla `MechanicalNetwork`: `torque = budget / speed`, so the
network settles at `speed = budget / load` and stalls past ~2× rated load. That is correct as a **time
average** — and this design keeps that steady state — but it cannot express the two things full authenticity
needs:

- **Bursty supply.** A single-acting engine makes power **once per revolution**; a double-acting Watt, twice.
  Between strokes the crank coasts. With no stored energy a bare engine cannot hold a steady load — it stalls
  between strokes. *This is the historical reason every engine has a flywheel.*
- **Bursty demand.** A rolling **pass** or a hammer **blow** is a large energy pulse over a fraction of a
  second, then nothing until the next. The instantaneous force can far exceed the engine's average output; the
  flywheel dumps its stored energy into the bite and the engine refills it between bites.

The vanilla torque model has (effectively) no storage, so it cannot represent "a small engine + a big flywheel
runs a heavy intermittent machine the engine could never drive continuously." That combination *is* the
19th-century mill, and it is the mechanic this network exists to deliver.

---

## 2. The model

### 2.1 Quantities

The simulation runs in **SI**; the table's "displayed as" column is what the HUD shows through `ExMeasure`
(see §2.4).

| Symbol | Meaning | Internal (SI) | Displayed as |
|---|---|---|---|
| `τ` | torque | N·m | internal (players read power + speed) |
| `ω` | shaft speed | rad/s | **RPM** |
| `I` | lumped rotational inertia | kg·m² | internal (per flywheel/buffer, from config) |
| `E = ½Iω²` | stored mechanical energy | J | **charge % of `E_cap`** + kJ/MJ |
| `P = τ·ω` | power (rate of energy) | W | **kW** (metric) / **hp** (imperial) |
| `E_cap = ½I·ω_max²` | capacity | J | the wheel's max store (large ≈ 15× normal) |

All magnitudes are **baseline, config-tunable** (R: numbers are tunable). The one seam to the abstract vanilla
model is the bridge (§5): a single tunable factor maps vanilla-MP power to watts, so a waterwheel's torque
reads as a sensible kW rather than an abstract `MpPowerBudget ≈ 0.26`.

### 2.2 Energy is stored as *spin*

A flywheel stores `E = ½Iω²`. Two consequences the design leans on:

1. **Charge level is visible for free** — a well-fed network's flywheels spin fast (`ω` high), a drained one
   crawls. `ω` stays vanilla MP's shared observable, but now it is a *readout of the reservoir*, not an
   instantaneous torque balance.
2. **Bigger/heavier flywheels store more** at a given max speed (`E_cap = ½I·ω_max²`), and take **longer to
   spin up** — a cold mill cannot run until its flywheel is charged. Startup is a ritual, not instant.

### 2.3 The per-tick balance (the whole simulation)

`MpEnergyNetwork.OnTick(dt)` treats each BFS-connected run as **one spinning shaft** — one pool, the R1 idiom
(like [PipeNetworkState](../../src/ExpandedLib/Networks/PipeNetworkState.cs)):

```
τ_drive = Σ producer.DriveTorque(ω)   # a torque–speed curve: max τ_stall at rest, easing toward 0 near ω_max (the governor)
τ_load  = Σ consumer.LoadTorque(ω)    # each working machine's resistance while it cuts; 0 when idle
τ_fric  = b·ω + τ_idle                 # windage (∝ω) + a standing-resistance floor
dω      = (τ_drive − τ_load − τ_fric) / I · dt    # I = Σ flywheel + buffer inertia
ω       = clamp(ω + dω, 0, ω_max)
E       = ½Iω²                         # derived, for the charge readout / capacity
```

Everything is emergent from the sign of the net torque — no special cases:
- **Coast:** drive cut (`τ_drive → 0`) → `ω` decays under load + friction → a charged flywheel runs a machine a
  while, then stops.
- **Not a battery / hard stall:** if `τ_drive(ω) < τ_load + τ_fric`, `dω < 0` — the shaft *decelerates*. A drive
  that can't out-torque the loaded resistance never spins up, so there is nothing to accumulate; sustained
  under-torque winds `ω` to 0 = a hard stall, not a slow buffer-and-fire.
- **Startup ritual:** a heavy wheel (big `I`) spins up slowly; its consumers can't run until it is turning fast
  enough.
- **Buffering:** big `I` → `ω` barely moves under a brief `τ_load` spike (a pass) or between engine strokes →
  smooth supply and demand.

The **op-gate flips** accordingly: not "grant the pass if `E ≥ E_op`" (a battery) but "the pass proceeds while
the shaft keeps turning; if `τ_load` drags `ω` to 0 it jams mid-op." Steady state still reproduces the old
average — a continuously-fed, continuously-loaded run settles where `Σ P_drive = load·ω`, a constant `ω` — so
the flywheel only *matters* on the transients, which is exactly where authenticity lives.

### 2.4 Units & display

The sim runs in **SI** (rad/s, kg·m², N·m, J, W); values are formatted for the look-at HUD / block-info through
the shared [`ExMeasure`](../../src/ExpandedLib/Helpers/ExMeasure.cs) layer — **metric by default**, a player
switches to imperial with `.exmod measure` (as for volume/pressure/temperature). New display categories:

| Quantity | Metric | Imperial |
|---|---|---|
| Speed `ω` | RPM | RPM |
| Power `P` | kW | hp (horsepower — period-authentic) |
| Energy `E` | charge % of `E_cap`, plus kJ / MJ | charge % (+ kJ) |

Torque stays internal — a machine reads as *power in/out*, a wheel as *RPM + charge*. The **flywheel** block-info
shows its run's charge % + energy + RPM (its spin animation ∝ `ω` is the ambient gauge, R7); an **engine/drive**
shows the power it produces, a **machine** the power it draws and whether it is engaged or stalled.

---

## 3. Node types

The network is a graph of typed nodes on the [`BlockNetwork`](../../src/ExpandedLib/Networks/BlockNetwork.cs)
base (BFS add/remove/merge/split already provided by `BlockNetworkModSystem`).

| Node | Role | Contributes |
|---|---|---|
| **Producer** (engine generator) | drive | `τ_drive(ω)` (a torque–speed curve); a small *internal* inertia so it self-runs past dead-centre |
| **Storage** (flywheel block) | the reservoir + the bridge (§5) | `I` (→ `E_cap = ½Iω_max²`); the signature block |
| **Buffer** (cast-iron shaft / gear) | the physical transmission line | a small `I` so light continuous machines ride jitter without a dedicated flywheel |
| **Consumer** (heavy machine) | load | imposes `τ_load(ω)` while working; the run must out-torque it or the op stalls |
| **Bridge** | vanilla-MP ↔ energy transition | the flywheel, reading vanilla-MP torque in as `τ_drive` (§5) |

The **Buffer** nodes are the cast-iron transmission the player builds — a heavy-duty parallel to vanilla's
wooden axles/gears: a straight **shaft** (three orientations, `ns`/`we`/`ud`, so a run can climb) and a **bevel
junction**. A bevel is reached by using one bevel-gear item on a shaft; after that it works like a vanilla
angled gear but **multi-branch and gear-free** — it presents a connector on every face, so placing a shaft
against a perpendicular side (west/east/up/down) auto-connects it and grows a bevel gear there, while an axis
continuation shaft just extends the run. The geared faces are derived from the neighbours, not stored. They form
the `MpEnergyNetwork` graph exactly as pipes/canals form theirs. (There is **no standalone spur/pinion node** —
the bevel covers all shaft-branching; spur and pinion are gear *parts inside* the transmission blocks, §3.2.)

**Multiple producers → one flywheel** just sum into the pool; their stroke pulses interleave, so *more
engines = smoother aggregate supply* (authentic — more cylinders/engines run smoother), and the flywheel
smooths the remainder. This is the "two engines feeding one flywheel" build the maintainer wants.

**Light machines stay on vanilla MP.** The planned wooden MP tier, existing gears, the mechanical blower/pump
keep using vanilla's torque network unchanged. This network is for the **heavy, intermittent** consumers
(rolling mill, steam hammer, later the pulverizer/stamp) that genuinely need buffered energy.

### 3.1 Flywheel variants

Two storage tiers. Capacity climbs *steeply* with radius (`E_cap = ½I·ω_max²`, disc inertia `I ∝ R⁴·t`), so
the large is a genuine industrial reservoir, not a small step up — and by the same `I`, it takes proportionally
longer to **spin up** (the startup cost that money can't skip):

| Variant | Footprint | Face R | Rel. `E_cap` (≈ R⁴·t) | Role |
|---|---|---|---|---|
| **Normal** | **3×3×1** | ≈1.5 | ~1× | one heavy machine |
| **Large** | **5×5×2** | ≈2.5 | **~15×** | a shop-wide reservoir / a row of rollers |

Both are `BlockFilledMegastructure` vertical discs that **animate their spin ∝ ω = √(2E/I)** (reuse the
engine flywheel's animation), so the disc speed *is* the charge gauge (R7, no GUI). The **hub face** carries
the vanilla-`BEBehaviorMPBase` intake (the §5 bridge); the rim is the mass. Both burst at the same `ω_max`
(§4.5), so the large simply stores more energy at that ceiling.

### 3.2 Transmissions (gear-ratio couplers) — a *sixth* node kind

A **transmission** changes the *speed* of the run at a point. The bigger gear sits on the **north** main shaft,
so a transmission is a **reduction S→N** — driving from the south, the north side turns *slower* and with *more
torque* (the reduction that feeds a heavy forming machine); drive it from the north and the south side *speeds
up*. Three variants, each a compact **RightClickConstructable mega-block** whose *shape decides the ratio* (the
player picks by choosing which block to build — no runtime toggle):

| Variant | Reduction `r` (big gear on N) | Mechanism (in the model) | Runtime shape |
|---|---|---|---|
| **x2** | 2 | belt drive (small pulley → belt → big gear) | `mpenergy/transmission-x2` |
| **x4** | 4 | compound gear train (two meshing stages) | `mpenergy/transmission-x4` |
| **clutch** | 1 | straight coupling, **engage/disengage** only | `mpenergy/transmission-clutch` |

**Footprint & connectivity.** A **2×2 cross-section, 1 deep along the run** (X-slice, viewed S→N):

```
-  i     ← upper: slab over the principal, then the quarter-block `i` (clutch lever / interaction)
O  -     ← principal `O`, then a side slab
```

i.e. principal `(0,0,0)` + side slab `(±1,0,0)` + slab-above `(0,1,0)` + quarter-block `(±1,1,0)`, authored
**north-default** (run axis S→N), built with `StructureFootprint.Layout(b => b.Layer(...))`. **MP connectivity
lives ONLY on the principal cell** — a *two-port coupler*: a **south port** and a **north port** (read via
`GetNetworkAt` on the adjacent shaft cells; the transmission is a machine, not a graph node, so the two sides
never merge). The fillers are ordinary structure fillers (collision + mesh fill); the **quarter-block `i` hosts
the clutch's interaction** (RMB there throws the lever). Fillers are never graph nodes (the standing filler rule).

**RCC stages + animation** *(BUILT 2026-07-26)*. The shape is grouped for the build: **`Base` → `MainShafts` →
`SupportShaft`** (three RCC stages). The ratio blocks carry an **`idle`** pose (keeps the RCC-suppressed mesh
visible, mixer-style) and a **`cycle`** clip driven at the run's speed — one rigid gear train, both main shafts +
gears turning together. **The ratio is baked into the clip**: `cycle` is authored as *one north revolution* with
the south shaft geared up (and counter-rotating) inside it, so the whole train is one clip played at **ω_north**.
The clutch has no rigid train and is animated per shaft instead: **`mainshaft1cycle`** (north) at ω_north /
**`mainshaft2cycle`** (south) at ω_south — each from *its own* side, which is precisely how a player reads that
the lever is open — **`sideshaftcycle`** for the coupling shaft (runs only while engaged; the `disconnected` pose
slides that shaft out of mesh, so the two must never overlap), and **`connected` / `disconnected`** poses for the
`SupportMovable` lever.

*Speed → playback is derived, not tuned:* a clip is one revolution, so its rate is the shaft's **revolutions per
second**, `ω / 2π` (`EnergyAnim.SpinSpeed`). The only knob is the physical `MpMaxSpeed`. Because the transmission
is **not** a graph node it receives no network broadcast — the server records the two side speeds it just read and
pushes them on a **throttled `MarkDirty`** (≥2% of full scale, or a stop), the coupler's equivalent of the
flywheel's block-info sync. Side speeds are published **whether or not a coupling happened**, so a disengaged
clutch still animates each side independently.

> **Gap — the shafts and bevels themselves do not spin.** They are static meshes drawn into the *chunk* terrain
> mesh (`OnTesselation` → `AddMeshData`), which cannot animate; vanilla solves the same problem with a per-block
> renderer (`BEBehaviorMPAxle`). Giving the cast-iron run visible rotation therefore needs a **rotating-mesh
> renderer** and a per-run perf budget (a long line is dozens of animated block entities) — its own task. The
> **direction convention it will consume is already settled and pinned**: `EnergyAnim.BranchSpinSign` derives from
> the mitre pair that a bevel branch on the *positive* side of its axis (east/up/south) reverses and one on the
> *negative* side (west/down/north) keeps the driving sense — so the two branches of one bevel counter-rotate,
> exactly as a real crown-and-pinion pair does.

**The coupling (physics).** The principal does **not** merge its two sides — south and north stay *separate*
`MpEnergyNetwork`s (like the flywheel bridges vanilla-MP ↔ energy, §5). The coupler imposes the gear constraint
**ω_north = ω_south / r** (reduction S→N), power conserved across the mesh (**τ_north = τ_south · r**, minus a
small gear-mesh friction). Concretely, each tick the principal reads the *input* side's speed and acts as a
**producer on the output side** driving it toward the ratioed speed and a **consumer on the input side** drawing
the reflected load — the "reflect the far side by `r` (torque) / `r²` (inertia)" rigid-gear reduction, applied
through the two ports rather than by merging pools. Direction (which side is input) follows power flow: the side
currently supplying net torque drives the other. *This coupling integration is the one part that needs careful
test-pinning* (no energy creation at the ratio boundary, stable at steady state, correct reverse).

**Clutch = `r = 1` + a coupling switch.** Same two-port principal, ratio 1, plus a persisted **engaged** flag:
disengaged ⇒ the ports transfer nothing (the two networks are fully independent — the run is severed at the
clutch, and each main shaft free-spins with its own side); engaged ⇒ 1:1 transfer. Toggle interaction on the
quarter-block filler cell; the `disconnected` pose is shown while open.

---

## 4. Authenticity mechanics (the "full" in full authenticity)

### 4.1 Pulsed supply
A producer emits `P_peak` during the **power fraction** `f` of its stroke cycle and ≈0 otherwise, with
`average = MpPowerBudget`. Single-acting `f≈0.5` of one stroke/rev (very pulsed); double-acting Watt = two
strokes/rev (smoother). The pulse phase advances with the engine's crank angle (the engine already tracks an
animation phase). A **bare engine with no flywheel and a steady load visibly labors and can stall between
strokes** — the failure the flywheel exists to prevent.

### 4.2 Governor (why "constant power" emerges)
A centrifugal governor throttles supply as `ω` rises toward `ω_rated`: `P_out = MpPowerBudget · g(ω)`, `g`
falling to 0 near `ω_max`. Lightly loaded → the governor closes, the engine self-limits speed (doesn't run
away); heavily loaded → wide open, `ω` sags. This *is* the observed "constant-power" behavior, now a
consequence of the governor rather than an axiom — and it is what makes surplus energy safe (the engine backs
off instead of over-speeding).

### 4.3 Coasting
Cut steam and the producer stops adding energy; the flywheel **coasts**, draining as machines draw and
friction bleeds it. The engine already models a coast ([BlockEntityEngine](../../src/LowPressureExpanded/BlockStructures/Engine/BlockEntityEngine.cs)
"while the flywheel coasts"); this design **externalizes** the big reservoir to the flywheel block while
leaving the engine a *small internal* flywheel so a single-cylinder engine can still self-run.

### 4.4 Friction / windage
A small `friction(ω)·dt` drain (≈ linear in `ω`) means an unpowered flywheel **slowly winds down** — no free
perpetual storage, and idle mills eventually stop. Tunable; small enough that a charged flywheel holds for a
useful while.

### 4.5 Over-speed burst
If the governor fails to hold (no load + surplus supply, or a deliberately un-governed drive) and
`E ≥ E_burst` (`ω ≥ ω_max`), the flywheel **bursts** — the mod's weakest-part-caps/burst idiom
([pipe over-pressure](conventions.md), pipe-tier weakest-caps): it shatters, drops debris, and is dangerous
to stand near. Normally the governor prevents it; removing/failing the governor is the risk. This gates raw
power and gives the flywheel a real downside, mirroring pipe burst.

### 4.6 Startup inertia
A stopped heavy flywheel must be **spun up** (charged) before its consumers can run: the engine pours energy
in until `E ≥` the consumer's `E_op`. A big mill needs a warm-up. This is emergent from the balance, not a
special case.

### 4.7 Temperature coupling (ties to the forming design)
Consumers may set `E_op` from *state*: a rolling pass on **cold** stock has high flow stress → large `E_op` →
it overdraws the flywheel and **stalls the mill** (see [sand-casting](sand-casting.md) / the rolling-mill
design — hot rolling is `δ_max = μ²R`, ~30× cheaper than cold). So "keep it hot or it jams" falls out of the
energy balance for free, and couples temperature ↔ energy ↔ flywheel into one system.

---

## 5. The vanilla-MP bridge (the flywheel as transition point)

The flywheel block is the **only** node that touches both networks:

```
 vanilla MP torque  ─▶  [ FLYWHEEL ]  ─▶  energy network (E, ω)
 (windmill / water        stores as         (rolling mill, steam
  wheel / our engine       ½Iω²              hammer, heavy consumers)
  generator, via a
  vanilla axle face)
```

- **In:** a vanilla-MP face reads the incoming `torque·ω` (power) from any vanilla source (windmill, water
  wheel, or our engine's generator) and **integrates it into `E`**. A vanilla waterwheel can therefore charge
  the energy network — the low-tech combo (overview.md) still works.
- **Out:** the flywheel serves `E` to the energy network's consumers, and can also **push stored energy back
  as torque to vanilla MP** when our side has surplus and the vanilla side has demand (so the network can
  drive light vanilla machinery too).
- **Result:** players build power however they like (vanilla or ours) and the flywheel is the clean seam. It
  is also where the two speed conventions reconcile (vanilla `ω` ↔ our derived `ω`).

Implementation: the flywheel BE carries a **vanilla `BEBehaviorMPBase`** on one face (like
[BEBehaviorMPSubmachineBase](../../src/ExpandedLib/Blocks/Machines/BEBehaviorMPSubmachineBase.cs)) and an
**`INetworkNode`** membership in the `MpEnergyNetwork` — it is a submachine on one side and a network node on
the other.

---

## 6. Integration with the existing code

- **`MpEnergyNetwork : BlockNetwork`** in `ExpandedLib.Networks`, state = one `MpEnergyNetworkState`
  (`E`, `E_cap`, `I_total`, producer/consumer registries), `OnTick` = §2.3, `OnMerge`/`OnSplitFragment`
  clamp/partition `E` proportionally to `E_cap` (the [pipe merge/split clamp](conventions.md) idiom),
  `OnTopologyChanged` recomputes `E_cap`/`I_total`/`E_burst` (weakest-flywheel `ω_max` caps burst, like the
  weakest-pipe burst rating).
- **Engine generator becomes a producer**: expose `DriveTorque(ω)` (max `τ_stall` at rest, easing near
  `ω_rated` = the governor); the pulse rides the existing crank phase. Backward-compatible: with the flywheel
  absent and a small internal inertia, steady state ≈ today's `speed = budget/load`.
- **Heavy machines become consumers**: the rolling mill / steam hammer expose `LoadTorque(ω)` per pass/blow
  through the [production-machine framework](../../src/ExpandedLib/Blocks/Machines/BlockEntityProductionMachine.cs)
  (`MachinePorts` + `GraceTimer`): a machine reads the run's `ω` across its connector, imposes its resistance,
  and advances the op only while the shaft keeps turning. The auto-run (reversing/3-high) mill walks its **pass
  schedule** one turning op at a time.
- **Config** (exlib/lpex, `ExConfigRange`): flywheel `I` per size, drive `τ_stall` + `ω_rated`, friction `b` +
  `τ_idle`, `ω_max`, per-op `τ_load` baselines. All the numbers in §2/§4.
- **Display (R7, no GUI):** block-info shows `E`/`E_cap` (charge %), `ω`, and supply-vs-demand; the flywheel's
  spin speed *is* the charge gauge.

---

## 7. Proposed invariant (for conventions.md)

> **R8 — Mechanical energy is stored and conserved.** Mechanical power flows as **energy** through the MP
> energy network; it is conserved except a modeled friction drain, **stored** in flywheels (and small node
> buffers) as `½Iω²`, **capped** at `E_cap`, and surplus is shed by **governor throttling** or, ungoverned,
> **over-speed burst**. Heavy intermittent machines draw energy **pulses**; they run only when the reservoir
> can grant the pulse.

---

## 8. Build phases (incremental, each shippable)

1. **Reservoir core.** `MpEnergyNetwork` + `MpEnergyNetworkState` + the flywheel block (storage only) +
   engine-as-**average**-producer + one heavy consumer (the rolling mill's single pass). Steady-state energy
   balance, `ω` derived, block-info readout. *Delivers the "engine → flywheel → mill" chain.*
2. **Pulsed supply + governor.** Producer `Power(phase)`; the governor `g(ω)`; a bare engine now visibly
   labors/stalls without a flywheel. *Delivers the authenticity of supply.*
3. **Risk + loss + startup.** Friction drain, startup spin-up gate, over-speed **burst** (governor-fail /
   ungoverned). *Delivers the flywheel's downside and the power cap.*
4. **The vanilla-MP bridge.** Flywheel reads vanilla torque in / pushes torque out; waterwheel-charges-network
   and network-drives-vanilla both work. *Delivers interop and the transition point.*
5. **Consumer expansion.** Steam hammer, pulverizer/stamp, auto-run mill pass-schedules on the framework;
   temperature-coupled `E_op` (§4.7).

---

## 9. Open questions (playtest / maintainer)

- **Pulse fidelity vs. jitter.** How pulsed before it feels janky? Tune `f`/`P_peak`; more producers smooth it.
- **One energy network per flywheel, or shared across flywheels on a shaft?** Baseline: BFS-connected nodes =
  one network, `E_cap` = Σ flywheels (multiple flywheels on one line pool their storage). Confirm that reads
  right vs. per-flywheel reservoirs.
- **Bridge back-drive.** Is pushing energy *back* to vanilla MP worth the complexity, or is the flywheel an
  in-only sink from vanilla (simpler)? Lean in-only first (phase 4a), add back-drive if a use appears.
- **Burst severity.** Cosmetic shatter + drops, or genuine AoE hazard? Match the pipe-burst severity the mod
  already ships.
- **`E_op` for each consumer** and flywheel `E_cap` tiers (small/medium/large) — the core balance numbers.
