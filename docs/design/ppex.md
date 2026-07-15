# Pipes & Power Expanded (ppex)

The **low steam-power tier**: it turns iron-age water/fuel into steam, steam into engine power, and pipes that power out as pumped water, blown air and MP torque. Depends on **iwex + exlib** (see [overview.md](overview.md)); all its machinery is built from **cast iron** — the material gate for LP machinery (see [materials.md](materials.md)), no steel required to start.

> Build-status tags: *(live)* = in code today; *(planned)* = specified, not yet built. Units, the R1–R7 invariants, network semantics and the heat-balance/distillation models are defined in [conventions.md](conventions.md) and cited by name — never restated here. Only numbers this mod **owns** appear below, and those are **config-tunable** (`ex_values.json`) unless noted.

---

## Networks

ppex adds no new network type — it rides exlib's shared graph (see conventions.md § Networks):

- **Pipe (gas *or* water)** *(live)* — single medium per network (R1). ppex ships the **rolled-pipe** material tier (cast-iron/steel) on the same network as iwex's bolted pipes.
- **Mechanical power (MP)** *(live)* — vanilla MP network; ppex drives it from the engine via a flywheel/generator sub-machine, and (planned) from a waterwheel via the mechanical pump.

---

## Steam spine

The two-block core: raise steam, turn it into power. Both are RCC **megablocks** gated by a **multiblock** projection, built from cast-iron components.

**Cornish boiler** — megablock (RCC + multiblock projection). Raises LP steam from water + solid fuel.
- **water (pipe/poured) + fuel → LP steam (+ exhaust)**
- Shared internal tank; ~3-min heat-up (blast-furnace style) before it boils; chokes and stops pushing steam at its output-pressure cap.
- Footprint ~3×4 base, 2 tall. Capacity **800 L** (150–500 L water, rest steam); **32 L/s** steam at full boil; choke **5 atm**; exhaust **16 L/s**; heat-up **180 s**. *(all tunable)*
- Its choke (5 atm) sits **above** the Watt engine's 4-atm break, so a **pressure valve between boiler and engine is mandatory** — the boiler can over-pressure the engine. *(live)*

**Watt engine** — megablock (RCC + multiblock projection). Beam engine; converts LP steam to a power budget for one sub-machine.
- **LP steam (fixed draw, no governor) → engine power (+ condensate)**
- Runs at inlet **≥ 2 atm**, wears toward a break above **4 atm** (needs the boiler's pressure valve); draws a **fixed 30 L/s** steam; delivers **0.3 power**; spits **~1 L/s** condensed water out its outlet (returned to a pipe, or spilled if unpiped). Efficiency 0.75 (see conventions.md) sets sub-machine output pressure. *(live, tunable)*

---

## Pipe network blocks

All *(live)*. One base pipe blocktype (straight/bend/T/X) plus specialised nodes; each carries an iron/steel **material** variant. Semantics (single-medium R1, connectors read the adjacent cell, over-pressure burst) are in conventions.md.

| Block | What it is | Key mechanic |
|---|---|---|
| **Rolled pipe** (straight/bend/tjunction/xjunction) | the network segment | only a plain segment bursts + caps a run's pressure; burst **iron 5 atm / steel 10 atm** *(tunable)* — the weakest pipe limits the run |
| **Pipe outlet** | terminal / machine tap | open connector face; a vanilla chimney capping it draws **16 L/s** gas *(tunable)* |
| **Pipe passthrough** (+ bend) | in-wall run | passes medium through a wall; the exhaust/heating route (see the Domestic add-on → Climate control) |
| **Plain valve** | in-line sever | closed = `IsConnectionBroken`, severs the network; toggle re-walks the graph |
| **Pressure valve** | directional overflow | input→output overflow **only above** its gate pressure; gas pushes downhill, equalise-capped — the mandatory boiler→engine relief |
| **Steam condenser** | phase-change node | pulls **30 L/s** steam off one line, condenses it, passes **≤ 50 L/s** water through its cross line *(tunable)* — steam condenses only here (see conventions.md, pipe uniform temperature) |
| **Fluid intake** | water source (generator) | draws water only when a full **3-deep** cube directly below is water; disabled if another intake is within **6 blocks**; feeds the bottom network *(tunable)* |

---

## Water & power

Sub-machines attach one-per-engine and scale off **absolute** engine power (see conventions.md § MP model). The **air-blower** sub-machine is **not** here — it ships with [smex](smex.md), where it feeds the hot blast; ppex's sub-machines are the pump and the flywheel.

| Machine | Block type | Power in | IO | Rate | Status |
|---|---|---|---|---|---|
| **Engine fluid pump** | sub-machine (attaches to engine) | engine | intake water → output line | **16.67 L/s per unit power** (Watt 0.3 → 5 L/s); transfers-then-refills | *(live)* |
| **MP generator / flywheel** | sub-machine | engine | engine power → MP network | constant-power torque: speed = budget ÷ load; holds rated speed within capacity, stalls past ~2× load (see conventions.md) | *(live)* |
| **Manual fluid pump** | block (2 tall) | hand (RMB-hold) | intake line → output line | **2 L/s @ 1 atm** — a manual boiler-startup feed, slower than the engine pump; not wrench-orientable | *(live)* |
| **Mechanical MP pump** | block (walking-beam) | **MP** (waterwheel) | intake water → output line | waterwheel-driven **alternative to the steam pump** — lets a player pump on pure MP, no boiler; shape `assets/editable/mechanical-pump.json` exists | *(planned)* |

The mechanical MP pump is the low-tech counterpart to the mechanical **air blower** iwex ships: together they let the whole iron tier run on a vanilla waterwheel, with steam as an upgrade rather than a prerequisite (see overview.md § low-tech combo).

---

## Storage & farm automation

| Machine | Block type | IO | Key mechanic | Status |
|---|---|---|---|---|
| **Fluid tank / cistern** | block (storage node) | pipe ↔ bulk buffer | a medium-agnostic high-capacity storage node on the pipe network; buffers pumped water so downstream draws don't need the pump running continuously | *(planned)* |
| **Mechanical sprinkler** | block (floor- or ceiling-mounted) | tank/pipe water → soil | waters soil in a **3-block radius beneath** to 100 % moisture then **stops** (consumes only the deficit); a **wrench-set interval** counts down before the next top-up — saturate-then-stop, not continuous | *(planned)* |

Sprinklers run off the tank buffer, so the pump need only top it up every few in-game days. The tank is a **medium-agnostic** storage node — the Domestic add-on reuses the same block as a **gasholder** for its coal-gas lighting.

---

## Gears & components

**Gears** — items (`gear`, `largegear`), each with an **iron/steel** metal variant; replace `game:gear-rusty` in machine recipes. *(live)*

> Ownership note: per overview.md, **`gear-iron` belongs to iwex** (the iron tier). The steel gears and large gears are ppex's; the code currently carries both variants here pending the iwex split.

---

## Content living here, destined for hpex

The **HP steam** blocks below are **already built** and currently ship in ppex, but they are **HP-tier content bound for [hpex](overview.md)** — they must be built from **hadfield steel**, not cast iron (the material gate, see materials.md). Treat them as *(live)* previews, not part of the LP spine.

| Machine | Block type | IO / rate | Status |
|---|---|---|---|
| **Lancashire boiler** | megablock (RCC + multiblock) | water + fuel → HP steam; capacity **1200 L** (200–800 water), **48 L/s** steam, choke **12 atm** *(tunable)* | *(live → hpex)* |
| **Cornish engine** | megablock (RCC + multiblock) | HP steam → power; **3-position throttle** (low/normal/high): power **0.2 / 0.4 / 0.8**, steam **8 / 16 / 32 L/s**, band **5–8 atm**; *the* deep-pump specialist *(tunable)* | *(live → hpex)* |

---

## Add-on: Domestic (lights, fuel, colours & climate)

The coal-gas / coal-chemistry complex — one gasworks gives a settlement its **light, heat, cold, colour and fuel**, all products of the same ~1850–1885 industrialisation of the home. This **merges the former Heating and "Lights, Fuel & Colors" add-ons**: the ammonia that links refrigeration to the gasworks is now *internal*, so there is no cross-add-on dependency. Runs on the live medium taxonomy and the general distillation model (see conventions.md § distillation); every step is mass-conserving (R2) and band-gated (R5). Deps: ppex (liquid net + still) + iwex (coke). **Produces the sulfur/acids** the smex copper add-on consumes (copper has no acid plant of its own). EM is a **soft dependency** for dyes. All *(planned)*.

### Gasworks & chemistry

| Machine | Block type | IO | Key mechanic |
|---|---|---|---|
| **Gasification plant** | **multiblock** (retort house + gas main as its parts) | coal (no air) → **coal gas + coal tar + coke** (+ **ammonia** + sulfur, from the ammoniacal liquor) | destructive distillation, gas-primary; retorts over a coke firebox, a water-sealed gas main dropping tar/liquor; stream split is the tunable. The **ammonia** byproduct charges the refrigeration loop below |
| **Distillation still** | **multiblock**, height = number of cuts | charge (coal tar / crude oil) → fractions in ascending boiling-point order (+ residue) | the general still (conventions.md): short pot = 1–2 cuts, tall column = full light→heavy→residue; one still runs any recipe by its charge |
| **Oil derrick** | multiblock over a worldgen **oil reservoir** | crude-oil seep → crude oil on the liquid network | reservoir prospectable, finite (**does not recharge**); lift depth-gated by the pump tier — reuses the fluid pump |
| **Chemistry benchtop / vat** | **shared** small block / megablock (**not** a full multiblock) | reagents → product, **by recipe** | one bench runs nitration / reduction / dyeing / kerosene acid-wash / acid-making by its charge |

### Gas lighting

The pre-electric lighting tier — coal gas from the gasworks (never the coke oven, which makes coke only), piped and burned for light, parallel to the later electric tier. Gas lighting lives **entirely** in this add-on: the fixture and its fuel ship together.

| Block | What it is | IO | Status |
|---|---|---|---|
| **Gas lamp** | block (fixture) | pipe coal gas → light | *(planned)* |
| **Gasholder** | the core-ppex medium-agnostic storage node holding coal gas (telescoping bell cosmetic) | pipe ↔ bulk gas buffer — keeps lamps lit when the gasworks idles | *(planned)* |

### Climate control — heating *and* cooling

A **room heat-balance**, the room-scale twin of the furnace/still heat balance (conventions.md), setting an enclosed room's temperature offset:

```
T_room = T_ambient + Σ(heater ΔT) − Σ(cooler ΔT)     — bounded by insulation & leak to ambient
```

One **shared climate-control room behavior** hosts both signs; block-info shows current/target temp + contributors (R7). Insulation (sealed walls, glass) sets how well the room holds the offset — the mirror of a furnace's `T_loss`, so build quality gates efficiency, not possibility (R5).

| Emitter | Sign | Chain | Delivers |
|---|---|---|---|
| **Cast radiator** (block, chainable) | **+ heat** | boiler → radiator → condensate returned | winter warmth; warm cellars; crop heating (patched, beyond vanilla's +5) |
| **Exhaust passthrough wall** *(passthrough + chimney vent live)* | **+ heat** | stove/firepit → passthrough → chimney | waste-heat room warming |
| **Cooling coil / evaporator** (block, chainable) | **− heat** | **ammonia** loop (Carré absorption cycle) cold side | refrigerated cold storage (vanilla cellar); crop cooling (patched); ice |

- **Cooling is ammonia refrigeration** — the **absorption cycle** (heat-driven, no engine, so it suits a low-tech engine-free tier), charged from the gasworks ammonia. A **closed loop**: charge once + top up for leaks (like the electrolysis acid fill), not a continuous feed. A Linde-style **compression** ice machine (engine-driven, bulk ice) is the optional industrial upgrade.
- **The hot side must vent outside the cooled room** — the loop's condenser (and, in the absorption cycle, its absorber) dump rejected heat elsewhere, so a fridge *warms* its surroundings (thermodynamically + historically true). Route that waste heat into a room you want warm and you have a **heat pump** — emergent from the shared balance, no special case.
- **Cold storage rides vanilla directly** (the killer app): a cooled sealed room is a vanilla **cellar** held below the biome/season floor → slowest food perish **anywhere, any season**. Confirmed against the game — an enclosed room's temperature already drives the cellar perish rate, no patch needed.
- **Crop climate control is a mod extension.** Vanilla crops read *outdoor* temperature plus a **fixed +5 °C greenhouse** bonus (glass roof, **warming-only — vanilla has no crop cooling**). So the add-on **patches the crop temperature check** to read the room's ΔT both ways: stronger-than-+5 heating for cold-sensitive crops, and the cooling vanilla lacks for heat-sensitive crops in hot climates.
- **Ice:** a coil in water freezes it to **ice** (Linde's actual product).
- **Ammonia is toxic** — a leak hazard (the danger-layer, like boiler steam), historically why refrigeration later moved off ammonia.

### Products

- **Fuels & light:** kerosene → **kerosene lanterns** (items); naphtha, lubricating oil; coal gas feeds the **gas lamps** (above).
- **Colours:** the aniline/coal-tar dye chain converges on vanilla `game:dye-*` (drop-in bulk alternative to barrel dyes) + a new `dye-brown`. EM soft-dep enriches the reagent set.
- **Acids & sulfur:** **produces** sulfuric/nitric acids and sulfur — **consumed by smex-copper** (zinc retorts, converter) and its own nitration/dye steps.
- **Ammonia:** the refrigerant charge for climate-control cooling (above), and a fertiliser precursor.
- **Downstream suite** (fertiliser, medicine, explosives, waterproofing, wax, recarburiser) via **existing barrels / furnaces / molds** — reuse, not new machines.

> All fractions (coal tar, benzene, kerosene, crude oil, ammonia, the acids, …) are `LiquidDef` media riding the same pipes, condensers, valves and tanks — shipped as the add-on's `config/liquids.json` (see conventions.md § Networks). Refrigeration reuses the **condenser** block (steam→water generalised to ammonia gas→liquid).