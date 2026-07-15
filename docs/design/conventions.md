# Conventions, Units & Shared Rules

The single source of truth for units, global invariants, network semantics and the shared simulation
models. Every mod doc cites this file rather than restating these.

---

## Units

| Quantity | Unit | Notes |
|---|---|---|
| Metal mass | **units (u)** | 100 u = 1 vanilla ingot |
| Fluid/gas volume | **litres (L)** *(live)* | A pipe segment holds **30 L** *(live)*; a run's capacity = node count × 30 L. Litres everywhere, never m³ |
| Water → steam | **1 : 16** *(live)* | 1 L water boils to 16 L steam (`SteamExpansionFactor`) |
| Mechanical power | **MP** *(live)* | Vanilla MP network; constant-power generator model (see [ppex](ppex.md)) |
| Steam/water flow | **L/s** | Per-tick flow, EMA-smoothed for the throughput readout *(live)* |
| Temperature | **°C** | One network-wide pipe temperature *(live)*; molten canals are per-cell |
| Pressure | **atm** | 1 atm = ambient. LP steam ≤ ~4–5 atm; HP ~8–12 atm (tunable) |
| Steam-engine efficiency | **0.75** *(live)* | Output pressure = inlet × efficiency |

---

## Global invariants (named rules)

Referenced by name from the mod docs.

- **R1 — Single medium.** A pipe network carries **one medium at a time** (gas *or* water) with a
  unified Volume/Temperature/Pressure/MediumType pool *(live)*. Air, steam, exhaust (and the chemistry
  fractions) are gas media; water and the liquid fractions are liquid media.
- **R2 — Mass-conserving, no hidden yield loss.** Every smelt/convert/refine/distil step preserves
  input mass (1 u in → 1 u out of the new material). Slag, smoke and fume are **cosmetic only**.
  Process *tiers* differ in **throughput and fuel cost, never material yield** — a run's output is
  always predictable.
- **R3 — Molten is per-cell.** Molten metal lives in **per-cell molten canals** (each block owns its
  metal, flows cell to cell) *(live)*. The **ladle** is the only block that merges canals and mixes
  metals.
- **R4 — Steam-only forming.** Billets and profiled stock **cannot** be worked on a vanilla anvil —
  only on the rolling mill / steam hammer (see [smex](smex.md)).
- **R5 — Gate efficiency, not possibility.** The heat-balance and distillation models gate **speed and
  efficiency**, never hard-block a process: there is always one guaranteed path (high-coke + cold blast
  melts at the iron tier). Every threshold is config-tunable.
- **R6 — Stock carries its mass.** Stock/billet items carry remaining mass in a **unit-count stack
  attribute**, so any cut or divide step is exact arithmetic.
- **R7 — No GUI.** All interaction is in-world and verb-based; all state is readable from block-info.

---

## Block-size vocabulary

- **block** — a single 1×1×1 block.
- **megablock** — occupies more than one cell via the **filler-block** mechanic; often a
  RightClickConstructable (RCC).
- **multiblock** — a structure the player **builds by hand in a specific shape**, guided by an in-world
  **projection**. Blocks *and* megablocks can be parts of a multiblock.

A block can be **both** — e.g. boilers are RCC **megablocks** whose construction is also gated by a
**multiblock** projection.

---

## Networks

Four transport-network families, all on exlib's shared block-network graph. The network **logic** lives
in exlib; the pipe **blocks** are per-mod tiers.

- **Molten-canal** *(live)* — per-cell metal, flows cell→cell, end caps recomputed on tesselation. The
  ladle is the only merge/mix point. Owned by [iwex](iwex.md).
- **Pipe (gas *or* water)** *(live)* — single medium per network (R1). Used for water, steam, compressed
  air, exhaust, coal gas and the chemistry fractions. Two **material tiers** of pipe block on the same
  network: **bolted** (iwex, iron) and **rolled** (ppex, steam). Connectors read the adjacent cell;
  valves sever/flow; pressure valves overflow.
- **Mechanical power (MP)** *(live)* — vanilla MP network; drives the mechanical blower (iwex) and
  mechanical pump (ppex) as well as engine sub-machines.
- **Electrical (AC + DC)** — the [elex](elex.md) tier (planned).

**The medium set is data-driven** *(live)*: each medium is a `LiquidDef` in exlib's liquid taxonomy
(code, gas/liquid phase, merge priority, boil/condense points). A mod adds a medium by shipping one
JSON entry; the built-in four (Air / Steam / Exhaust / Water) reproduce the old hardcoded behaviour
exactly. The chemistry add-on registers its distillation fractions (coal tar, benzene, kerosene, crude
oil, the acids, …) as further media that ride the very same pipes, condensers, valves and tanks.

---

## Shared simulation model — dynamic heat balance

Furnaces have **no hardcoded max temperature**. Each runs a per-tick heat balance:

```
T_process = T_in − T_loss     — melts/refines only while  T_process ≥ T_threshold(material)
```

- **T_in** (heat source): coke combustion (coke ratio × air flow) + a blast-preheat buff
  (cowper/regenerator); OR electrical power (arc); OR autothermal oxidation (converter — the pig's own
  C/Si burned by the blow); OR fuel flame + regenerator (open hearth, reverberatory).
- **T_loss** (heat sinks): cold-charge mass (scrap, ore, wet feed) + radiation/ambient (worse in
  winter).

Block-info always shows current T, the threshold and the contributors, so a stall reads
"1410 °C, needs 1538 °C — add coke or hot blast", never a silent failure. Per R5 the model gates
efficiency, not possibility, and every threshold is tunable. Consequences (all emergent, not
hardcoded): cold-blast vs hot-blast falls out of whether cowpers are charged; melt rate scales with the
temp margin; the converter's scrap cap emerges from bath freezing past ~20–30 %; an underfed
boiler → weak blower → cold furnace is one loop.

## Shared simulation model — distillation & phase change (the general still)

The boiler's water→steam conversion *(live)* is the simplest case of one mechanism the whole liquid
line reuses: **heat a liquid, boil off its fractions in ascending boiling-point order, condense each
back to a liquid at a cooler stage.** Steam is the one fraction water throws off; a fractionating still
does the same to a *mixture*. It runs entirely on the live medium taxonomy (a `LiquidDef` has a boiling
point; its vapour is the gas phase; it condenses back per `condensesTo`/`condenseBelow`).

```
while  T_column ≥ boil(fraction):  fraction boils → rises as vapour → condenses at its stage → tapped
residue (never boils in range)  :  stays in the pot → the bottom product (pitch / petroleum coke)
```

Mass-conserving and band-gated (R2, R5): distillation is a set of **recipes keyed to temperature
bands** (`source + band → fraction (+ remaining source)`), not per-litre composition vectors. The still
is its **own multiblock**, sibling to the boiler and ladle — built to a height that sets how many
fractions it can split (short pot still = one/two cuts; tall column = the full light→middle→heavy→
residue set). All its I/O reuses live blocks (pipe charge-in, coke firebox or steam jacket for heat,
one vapour take-off per stage condensing at a condenser block, residue tap at the bottom). One still
runs any distillation recipe by its charge. Spec in [ppex chemistry add-on](ppex.md).
