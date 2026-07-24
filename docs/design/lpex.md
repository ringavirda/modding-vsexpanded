# Pipes & Power Expanded (lpex)

The **low steam-power tier**: it turns iron-age water/fuel into steam, steam into engine power, and pipes that power out as pumped water, blown air and MP torque. Depends on **iwex + exlib** (see [overview.md](overview.md)); all its machinery is built from **cast iron** — the material gate for LP machinery (see [materials.md](materials.md)), no steel required to start.

> Build-status tags: *(live)* = in code today; *(planned)* = specified, not yet built. Units, the R1–R7 invariants, network semantics and the heat-balance/distillation models are defined in [conventions.md](conventions.md) and cited by name — never restated here. Only numbers this mod **owns** appear below, and those are **config-tunable** (`ex_values.json`) unless noted.

---

## Networks

lpex adds no new network type — it rides exlib's shared graph (see conventions.md § Networks):

- **Pipe (gas *or* water)** *(live)* — single medium per network (R1). lpex ships the **cast-pipe** material tier (assembled from cast pipe-parts finished on the boring machine) on the same network as iwex's bolted pipes; the top **rolled** tier (Hadfield steel, on smex's rolling mill) is hpex's. All three tiers share one pipe diagram per shape (see [diagram-crafting.md](diagram-crafting.md)).
- **Mechanical power (MP)** *(live)* — vanilla MP network; lpex drives it from the engine via a flywheel/generator sub-machine, and (planned) from a waterwheel via the mechanical pump.

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

All *(live)* except where tagged. One base pipe blocktype (straight/bend/T/X) plus specialised nodes; each carries an iron/steel **material** variant. Semantics (single-medium R1, connectors read the adjacent cell, over-pressure burst) are in conventions.md.

| Block | What it is | Key mechanic |
|---|---|---|
| **Cast pipe** (straight/bend/tjunction/xjunction) | the network segment | only a plain segment bursts + caps a run's pressure; burst above iwex's bolted, below hpex's rolled *(tunable — the per-tier material/burst split across bolted/cast/rolled is being finalised; was iron 5 atm / steel 10 atm in the old 2-tier model)* — the weakest pipe limits the run |
| **Pipe outlet** | terminal / machine tap | open connector face; a vanilla chimney capping it draws **16 L/s** gas *(tunable)* |
| **Pipe passthrough** (+ bend) | in-wall run | passes medium through a wall; the exhaust/heating route (see the Domestic add-on → Climate control) |
| **Plain valve** | in-line sever | closed = `IsConnectionBroken`, severs the network; toggle re-walks the graph |
| **Pressure valve** | directional overflow | input→output overflow **only above** its gate pressure; gas pushes downhill, equalise-capped — the mandatory boiler→engine relief |
| **Steam condenser** | phase-change node | pulls **30 L/s** steam off one line, condenses it, passes **≤ 50 L/s** water through its cross line *(tunable)* — steam condenses only here (see conventions.md, pipe uniform temperature) |
| **Fluid intake** | water source (generator) | draws water only when a full **3-deep** cube directly below is water; disabled if another intake is within **6 blocks**; feeds the bottom network *(tunable)* |
| **Injector** *(planned)* | steam-driven feed pump, no moving parts | takes **live** steam + **cold** water and delivers water **above its own steam pressure**; won't pick up on hot feedwater; failure dribbles out an open overflow spout — see § Boiler feedwater |

---

## Water & power

Sub-machines attach one-per-engine and scale off **absolute** engine power (see conventions.md § MP model). The **air-blower** sub-machine is **not** here — it ships with [smex](smex.md), where it feeds the hot blast; lpex's sub-machines are the pump and the flywheel.

| Machine | Block type | Power in | IO | Rate | Status |
|---|---|---|---|---|---|
| **Engine fluid pump** | sub-machine (attaches to engine) | engine | intake water → output line | **16.67 L/s per unit power** (Watt 0.3 → 5 L/s); transfers-then-refills | *(live)* |
| **MP generator / flywheel** | sub-machine | engine | engine power → MP network | constant-power torque: speed = budget ÷ load; holds rated speed within capacity, stalls past ~2× load (see conventions.md) | *(live)* |
| **Manual fluid pump** | block (2 tall) | hand (RMB-hold) | intake line → output line | **2 L/s @ 1 atm** — a manual boiler-startup feed, slower than the engine pump; not wrench-orientable | *(live)* |
| **Mechanical MP pump** | block (walking-beam) | **MP** (waterwheel) | intake water → output line | waterwheel-driven **alternative to the steam pump** — lets a player pump on pure MP, no boiler; constant-power (§ Boiler feedwater), cast iron so its delivery pressure tops out in the LP band; shape `assets/editable/shapes/mechanical-pump.json` exists | *(planned)* |

The mechanical MP pump is the low-tech counterpart to the mechanical **air blower** iwex ships: together they let the whole iron tier run on a vanilla waterwheel, with steam as an upgrade rather than a prerequisite (see overview.md § low-tech combo). Because its power comes from **outside** the steam circuit it has no circular dependency on the boiler it feeds — it works with the fire out.

---

## Boiler feedwater

*(planned — design settled 2026-07-24; nothing below is built yet.)*

A boiler's feed has to **push in against the boiler's own internal pressure**. That number already exists as `BlockEntityBoiler.InternalPressure` (steam ÷ headspace), so the rule is a comparison, not a new formula: the network intake draws only while `feedPressure ≥ InternalPressure`, ramped to full rate over a small margin (`BoilerFeedFullFlowMargin`) so the intake cracks open like a check valve instead of snapping on and off.

Two consequences fall out of machinery that is already built:

- **It is a flow contest, not only a pressure one.** A liquid run reports its pump's commanded pressure only once it is **brim-full** (`PipeNetworkState.ComputeLiquidPressure`), and every draw recomputes it. A feed device that cannot refill the line as fast as the boiler empties it watches its own delivery pressure sag — so throughput matters as much as head.
- **Hand-feeding under pressure is already impossible.** Manual fill is gated on the lid, and an open lid vents. To pour, you blow down first. That is the historical trade, and it means the rule can never strand a player.

Recovery needs no safety net either: a boiler that cannot feed boils down, and as its water falls the headspace grows, so `InternalPressure` *drops*. Below `MinBoilWater` it shuts down and condenses the remainder — it re-opens its own feed gate.

### Pumps are pressure multipliers *(fix)*

The sub-machine pump currently delivers at `InletPressure × SteamEngineEfficiency` — a pressure *reducer*, with flow independent of pressure. That is backwards, and it is why a steam pump can never feed the boiler driving it: a round trip through a heat engine always arrives smaller, at **every** operating point, not just near choke. A real feed pump is a big steam piston on a small water plunger — delivery pressure is the *area ratio*, and efficiency is paid in **flow**:

```
delivery = InletPressure × PumpPressureRatio     — ratio > 1 (piston ÷ plunger area)
flow     = power_budget ÷ delivery               — constant power, as the MP generator does
```

> Add a **separate** `PumpPressureRatio`; do **not** re-scale `SteamEngineEfficiency`. The smex air blower reads the identical expression for blast pressure, which has to stay between the blast floor and the pipe burst rating (see smex.md).

The same constant-power relation governs the mechanical MP pump; only its power source differs.

### Injector

A brass **connector** block — not a network node. Like the steam condenser it keeps the runs it touches as separate networks and bridges them in its block entity, which is mandatory here: steam and water cannot share a pool (R1).

- Single cell, fitting-scale. **Steam up, cold water in one side, delivery out the other, overflow spout down** (no connector — the overflow *is* the status display). Directional, unlike the condenser's fuller-side detection.
- Delivers water **above the pressure of the steam driving it** — the Giffard paradox. It feeds a boiler from that boiler's own steam because the steam condenses into the feedwater and carries its heat back in. No external power, therefore **no bootstrap and no second boiler**.
- **Won't pick up on hot feedwater** — it has to condense its steam. The condenser's hot output therefore can't feed it directly; that wants a hotwell or a cold branch.
- Not tier-gated: the pressure it delivers is a function of the steam it is given. **Union stubs, not flanges**, so the model reads correctly against both square-flanged (iwex/lpex) and octagonal-welded (hpex) pipe — and as a connector the joint-family rule never asks it (conventions.md § pipe joints).
- All its heat returns to the boiler, so it pays out through the existing `WaterPressureSteamBoost` path.

**It is not the steam condenser.** The two share a chassis and invert on the axis that matters: the condenser hands water on at the *inlet's* pressure and eats **spent** steam (a sink — it relieves pressure and produces hot water); the injector raises pressure and eats **live** steam (a running cost — and it requires cold water). The condenser stays lpex unchanged; it is also the generic phase-change block the refrigeration add-on reuses.

### The feed roster

| Device | Costs | Works when | Tier |
|---|---|---|---|
| **Manual fluid pump** | player time | cold only (1 atm) | priming |
| **Mechanical MP pump** | MP | the wheel turns — fire lit or not | LP |
| **Engine fluid pump** | steam + an engine's sub-machine slot | the engine is turning | LP / HP |
| **Injector** | live steam | any steam pressure, nothing moving | LP / HP |

Four answers to "what do you have spare right now" — water power, mechanical power, an engine, or just heat. This is also the answer to the standing complaint that the tier demands too many engines: both the mechanical pump and the injector feed a boiler with **no engine dedicated to it**.

Historically ordinary, too. Mill practice for a Lancashire was a donkey feed pump for continuous duty with an injector as standby and for raising steam; locomotives, which stop, carried injectors only. A boiler fed off a water-powered line shaft with an injector in reserve is an unremarkable 1870s installation.

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

> Ownership note: per overview.md, **`gear-iron` belongs to iwex** (the iron tier). The steel gears and large gears are lpex's; the code currently carries both variants here pending the iwex split.

---

## HP content — moved out to hpex ✅

**Done (2026-07-24, reorg-plan Task 2).** The two HP machines that used to ship here — the
**Lancashire boiler** and the **Cornish engine** — now live in their own mod; see
[hpex.md](hpex.md) for their cards. Placed machines migrate automatically.

What lpex keeps for them is the **shared machinery they inherit**: `BlockBoiler`/`BlockEntityBoiler`
(the whole boiler FSM + `BoilerShell()`), `BlockEngine`/`BlockEntityEngine` (the power/steam FSM,
sub-machine discovery + `EngineShell()`), the geometry interfaces, the renderers, `PistonCycleSounds`,
the sub-machine bases, and the shared boiler/engine config knobs. hpex depends on lpex to reach them,
which makes that shell surface a de-facto public API lpex must keep stable.

Still pending for hpex: re-gating both builds to **hadfield steel** (materials.md), a balance/recipe
change separate from the structural move.

---

## Add-on: Domestic (lights, fuel, colours & climate)

The coal-gas / coal-chemistry complex — one gasworks gives a settlement its **light, heat, cold, colour and fuel**, all products of the same ~1850–1885 industrialisation of the home. This **merges the former Heating and "Lights, Fuel & Colors" add-ons**: the ammonia that links refrigeration to the gasworks is now *internal*, so there is no cross-add-on dependency. Runs on the live medium taxonomy and the general distillation model (see conventions.md § distillation); every step is mass-conserving (R2) and band-gated (R5). Deps: lpex (liquid net + still) + iwex (coke). **Produces the sulfur/acids** the smex copper add-on consumes (copper has no acid plant of its own). EM is a **soft dependency** for dyes. All *(planned)*.

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
| **Gasholder** | the core-lpex medium-agnostic storage node holding coal gas (telescoping bell cosmetic) | pipe ↔ bulk gas buffer — keeps lamps lit when the gasworks idles | *(planned)* |

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