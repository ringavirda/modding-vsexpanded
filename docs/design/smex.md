# Steelmaking Expanded (smex)

The **steel tier**: hot-blast ironmaking, the Bessemer/open-hearth steel routes, ladle alloying, and the steam forming shop that turns bulk billets into vanilla goods. Depends on `lpex` + `iwex` + `exlib`. Owns the materials **Bessemer steel**, **open-hearth steel**, **ingot iron** and **hadfield steel** (see [materials.md](materials.md)). See [overview.md](overview.md) for the mod map and build order; all units, invariants (R1–R7), the heat-balance model and the distillation model live in [conventions.md](conventions.md) and are cited here, never restated.

Design frame: smex does not add a new blast furnace block. It fits the **cold-blast furnace built in `iwex`** with hot-blast infrastructure (cowpers + smokestack + exhaust outlets) to run it hotter and cheaper, then adds the two steel converters, the ladle, and the forming shop downstream.

---

## Materials owned

| Material | Route | Character | Cite |
|---|---|---|---|
| **Bessemer steel** | Bessemer converter blows pig | low C, **high-N**; structural, strain-age brittle | materials.md |
| **Open-hearth steel** | open hearth (pig + scrap) | low C, **low-N**; scrap-capable, the pressure grade | materials.md |
| **Ingot iron** | Bessemer over-blow | ~0 % C, slag-free; **not** wrought iron | materials.md |
| **Hadfield steel** | ladle: mild steel (Bessemer / open-hearth) + Mn | austenitic, work-hardening; gates the HP tier by material | materials.md |

Bessemer steel and open-hearth steel are **distinct materials, not a tagged "mild steel"** and **not vanilla steel** (vanilla `game:steel` is shear steel — see materials.md). The **nitrogen grade is the material identity**, so there is no ppm attribute or sim: **open-hearth (low-N) is the required *mild steel* for boiler plate, pressure parts and HSS feedstock** (wrought iron and hadfield also pass the pressure gate — see hpex.md; only HSS feedstock is open-hearth-exclusive), while **Bessemer (high-N) is barred** from those and serves as the cheap structural steel. Off-spec ladle mixes → **waste alloy** (recover Fe in the cupola or as capped cold scrap in the Bessemer — never the blast furnace; see materials.md ladle rules). All melt/refine steps are mass-conserving (R2); every threshold gates speed/efficiency, not possibility (R5).

---

## Hot-blast ironmaking

The hot-blast furnace is the `iwex` open-top stack fitted with two exhaust outlets, cowper stoves and a smokestack. Charged cowpers raise **T_in** via the blast-preheat buff (see conventions.md heat balance); an underblown or cold-cowper furnace simply falls back to cold-blast behaviour (R5).

### Cowper stove — megablock *(live)*
Regenerative checker-brick stove: soaks heat from furnace exhaust on one half-cycle, reheats intake air into **hot blast** on the other.
- **Input → output:** furnace hot exhaust (charge cycle) + cold blower air (blow cycle) → hot blast air.
- **Mechanic:** internal regenerator temperature climbs on the exhaust half-cycle (faster on anthracite), bleeds into the air it reheats; delivered blast temperature = current checker charge. Alternating heat/blow cycle.
- **Owned numbers (tunable):** regenerator cap **1240 °C**; intake **24 L/s** per intake; air-reheat cooldown 0.0012/s, exhaust heat-soak 0.3/s.
- **Footprint / power / IO / rate / status:** megablock / air + exhaust / 2 intakes @ 24 L/s / — / *(live)*.

### Smokestack — megablock *(live)*
Vents spent furnace exhaust after the cowpers have soaked their heat.
- **Input → output:** spent exhaust gas → vented (cosmetic plume).
- **Owned numbers (tunable):** vent **48 L/s**.
- **Footprint / power / IO / rate / status:** megablock / — / gas intake / 48 L/s / *(live)*.

### Hot-blast operation *(live)*
- **Input → output:** low-coke blast mix + hot blast → molten pig iron.
- **Mechanic:** the pressurized (engine-driven) blast plus charged cowpers lets a **low-coke** charge clear its melt line for a higher rate at lower coke ratio. Blast **pressure** is the tier gate (see conventions.md: pressure → combustion intensity → T_in); below ~2.5 atm (tunable `BlastPressureThreshold`) the same furnace runs as cold blast. Tuyere/exhaust balance is fixed by `iwex` (2 tuyeres @ 24 L/s = 48 L/s in; 2 exhaust @ 24 L/s = 48 L/s out).
- **smex adds no furnace behaviour at all.** The hot furnace is iwex's blast furnace, unmodified — same block entity logic, same tunables, same heat balance. What makes it hot is its **layout** (a shaft with exhaust outlets and a sealed bell top, tier-3 brick) and a **cowper on its blast line**; the preheat term in `T_in` reads the pipe temperature at the tuyere and does the rest. Run the same structure on unheated air, or let the line fall under the pressure threshold, and it behaves as a cold furnace again — no flag, no override, no second copy of the model.
- **Owned rate:** none. At the shipped calibration a standard burden on 950 °C blast settles at ~1746 °C against the cold furnace's ~1578 °C on high coke, which the melt-margin factor turns into roughly **1.5× the cold-blast rate** (~12 u/s vs ~8 u/s) — the doc's old 45 : 30 ratio, now emergent from the heat balance rather than a `BfHotIronPerMeltCycle` constant.
- **Status:** cowper + smokestack + exhaust cycle *(live)*; low-coke blow *(live)* — it is what the heat balance produces.

---

## Steelmaking

### Bessemer converter — multiblock *(live)*
Tilting side-blown vessel: control block + intake (blast) + transmission (MP tilt) + the 3×3×3 vessel shell. A **dynamic autothermal converter** — the carbon model, over-blow, cold-scrap gate, slag byproduct and split-pour tilt sequence are all live.
- **Input → output:** molten **pig iron** (`iwex:ingot-pigiron`) poured from a canal, plus an optional cold **steel-scrap** charge (any exlib Scrap-role item — `game:metalbit-steel` and the like, classified by role not by path), + pressurized air → molten **Bessemer steel** (`smex:ingot-bessemersteel`, high-N) + molten **slag** (the same `iwex:slag` the furnaces make).
- **Acid process, no flux — historically correct, not a shortcut.** We model the **acid** Bessemer (materials.md high-N mild steel = air-blown acid process): an acidic lining and a **self-forming siliceous slag**, and **no flux**. Flux (lime) belongs only to the **basic**/Thomas process for phosphorus removal — a separate future thing, deliberately absent here, not omitted.
- **Dynamic carbon model + over-blow (the headline mechanic).** The charge carries a per-unit **carbon fraction** (pig ~4 % C, see materials.md); the air blow **oxidises it, so carbon falls monotonically the longer you blow**. That oxidised carbon feeds the **autothermal T_in** of the shared heat balance (conventions.md) — no external fuel. The player picks the product by **when they stop** (cut the blast, or tilt away): stop early with carbon still high → still **pig** (not yet steel); reach `BessemerSteelCarbonTarget` → **Bessemer steel** (the intended product); keep blowing past it (`< BessemerOverblowCarbon`) → **over-blow to soft ingot iron** (`game:ingot-iron`) — the deliberate plain-iron route now the blast furnace makes pig, and a botched heat, never a shortcut to wrought iron. The live **carbon %** reads from block-info (R7) so the stop can be timed. Carbon is an **abstraction for total oxidisable content** (real Bessemer heat is silicon-dominated); modelling reaching the target *as* the steel self-terminates the way the real flame drops at blow's end.
- **Steel scrap = the temperature gate.** Cold scrap adds cold mass to **T_loss**, so more scrap ⇒ lower T_process; melted into the steel at the target it recycles steel scrap (the small-scale recycler — bulk scrap is the open-hearth's job). There is **no hardcoded scrap cap**: past the ceiling the blast can't hold the bath above the refine floor and the heat stalls, then freezes into the existing solidified/chisel path (conventions.md). Tuned so ~**15–20 %** cold scrap on a full charge is the practical ceiling (the real acid-Bessemer figure; ~30 % is too generous).
- **Slag + split-pour state machine.** Slag accumulates into its own pool **during** the blow as impurities oxidise. The vessel now has **four tilt states**: upright (blow/refine) ↔ fill; and upright → **slag-pour** (a shallow tilt) → **steel-pour** (the deeper tilt), returning upright from any. Slag floats, so the shallow tilt spills it off the top first; the deeper tilt reaches the steel beneath. **Both drain the same output cell** — the tilt state selects which pool feeds it; the player's fireclay valves / cut-off taps keep the canal clean between phases.
- **Mass balance (R2, config-tunable):** per 100 u pig → **90 u molten steel + 6 u molten slag + 4 u gas** (`BessemerSteelYield` 0.90 / `BessemerSlagYield` 0.06; the gas is carbon burned off — gone, not a material). Per 100 u steel scrap → ~97 u steel. **steel + slag ≤ input, never more.** Over-blowing to ingot iron keeps the same steel yield (the carbon that leaves is already in the 4 u gas).
- **Owned numbers (tunable):** blast draw **8 L/s**; capacity **≥4800 u** — sized to hold **≥2 large billets** so the steel line never stalls. A full charge blows in ~5 min and pours in ~2 min (slag seconds, steel ~110 s), the insulated vessel holding a ~3–4 min liquid window; a heavier scrap charge lowers the peak and shortens that window (the natural "don't overload" pressure).
- **Footprint / power / IO / rate / status:** 3×3×3 multiblock / pressurized air + MP tilt / molten-canal in, molten-canal out (slag then steel through one cell) / ~16 u/s / *(live)*.

### Open-hearth furnace — multiblock *(planned)*
Regenerative bath over checker chambers; the bulk-scrap, low-N route.
- **Input → output:** pig + **bulk scrap** + flux → **open-hearth steel** (low-N) (+ HSS feedstock).
- **Mechanic:** fuel flame + regenerator over the bath (no bath air-blast); slow, melt time scales with scrap. Because it doesn't blow air through the melt it stays **low-N** — its steel is what boiler plate, pressure parts and HSS feedstock require (see materials.md), and it recycles bulk scrap the Bessemer can't.
- **Footprint / power / IO / rate / status:** multiblock (hearth + 2 regenerative chambers) / fuel + regenerated air / molten-canal out / slow / *(planned)*.

---

## Ladle — block *(planned)*

A **single canal-fed vessel** (one block, not a big multiblock) — the alloying/recarburise/carbon-cover mix step. It is the **only** block that merges molten canals (R3); canal junctions upstream already converge the streams into it.
- **Input → output:** converged canals (base metal + poured alloying metal) → mixed alloy → poured into billet/component molds or onward canals.
- **Mechanic:** mix **by held proportion** into the target alloy (hadfield = mild steel, Bessemer or open-hearth, + ~12.5 % Mn; see materials.md ratios). **Hadfield is the alloying-mechanic intro** — the first metal you must mix rather than smelt. Recarburise by hand-dropping powdered coke (fixed C mass per unit). Zinc alloys need a **coke cover** or the zinc boils off. Off-ratio → **waste alloy** (keeps base mass, never snaps to nearest alloy — not vanilla `AlloyRecipe`; see materials.md).
- **Footprint / power / IO / rate / status:** block / manual (drop-in + tilt-pour) / canal in, mold/canal out / — / *(planned)*.

---

## Steam forming shop — one megablock *(planned)*

**One megablock anchored on the steam hammer**, with the rolling/shear/draw tooling installed as swappable dies. Delivers the billet pipeline (R4: billets cannot be worked on a vanilla anvil, only here). Billet masses and the stock-item unit economy live in materials.md; stock carries remaining mass in a unit-count attribute (R6), so every cut is exact arithmetic.

```
Billet (2400u large / 800u small)  --rollers-->  profiled stock (plate/bar/pipe)  --dies-->  discrete vanilla items
```

### Rollers (profile tooling) *(planned)*
- **Input → output:** billet → profiled stock (forms/reduces, does **not** cut).
- **Mechanic:** roller type sets the **cross-section family** (flat / grooved / pipe); **gap setting** picks the thickness step within that family, so one roller = one product family. Each pass reduces one gap step; a budget single mill re-feeds, a train pre-sets each stand. *(Owned gap map — flat 5→2: heavy plate/plate/sheet/strip; grooved 5→2: heavy bar/rod/thin rod/wire rod; pipe 5→3: large pipe/rolled pipe/small tube. Tunable.)*
- **Deferred:** the tandem-train hand-off choreography and the jam mechanic — ship the single-mill budget path first.

### Dies (shear / stamp) *(planned)*
- **Input → output:** profiled stock → discrete items (shear-die divides by item mass); billet → forged heavy components (open die).
- **Mechanic:** installable dies select the op. **Shearing is the LP exception** — one decisive blow cuts billet stock into plates/rods/pipes from day one. Bulk **stamp/blanking** (nails, rivets, brackets) needs sustained HP-steam energy → that capability lives with hpex. **Batch size = die cavity count** (read off the die mesh). Shearing/stamping hadfield or HSS needs **quench-hardened dies** (mirrors the boring-machine bit tiers).

### Draw-die (wire) *(planned)*
- **Input → output:** wire rod → wire.
- **Mechanic:** drawing tooling on the same shop; useful only once there is an electrical grid to consume the wire, so effectively an elex-era attachment.

- **Footprint / power / IO / rate / status:** megablock / steam (LP shear/forge) + MP (rollers) / billet in, item/stock out / ~3 s per pass / *(planned)*.

---

## Steam ore crusher (LP) — megablock *(planned)*

Bulk ore comminution, the mass alternative to the hand pulverizer.
- **Input → output:** bulk nuggets/stone → crushed ore (iron + softer ores + stone/lime + **rhodochrosite → Mn** for hadfield).
- **Mechanic:** MP-driven jaws on an LP-steam machine; a **hardness gate** — hard ores (chromite, wolframite) are the **HP crusher** branch and belong to hpex.
- **Footprint / power / IO / rate / status:** megablock / LP steam (MP jaws) / solids in/out / bulk / *(planned)*.

---

## Add-on: Copper

Off-spine bulk-copper line (materials in materials.md: converter copper, the bronzes). **No acid plant** — the sulfur/sulfuric this line historically threw off is *produced* by the chemistry add-on (this add-on only ever consumes it, and here only cosmetically). Copper alloys are mixed in the **ladle**, same as the ferrous alloys.

### Reverberatory furnace — multiblock *(planned)*
- **Input → output:** crushed copper ore + heat → copper matte (+ slag).
- **Mechanic:** coal burns in a **separate part of the multiblock** — the flame plays over the charge, fuel never mixes with it. Also the copper-side waste-alloy recycler (see materials.md).

### Pierce-Smith converter — multiblock *(planned, reuses Bessemer)*
- **Input → output:** copper matte + pressurized air (+ MP tilt) → blister/molten copper.
- **Mechanic:** a **side-blown converter** — implemented as a **copper-matte mode of the built Bessemer vessel**, not a new machine.

### Zinc retorts — multiblock *(planned)*
- **Input → output:** zinc ore → distilled metallic zinc.
- **Mechanic:** a distillation (retort) unit — an instance of the general distillation model (see conventions.md), feedstock for brass in the ladle (which needs the coke cover).

Copper alloys (tin bronze / brass / bismuth bronze / black bronze) are mixed in the ladle by held proportion; ratios in materials.md.

---

> **Footnote.** Cowper hot-blast stoves belong only with the hot blast furnace — never cold blast; historical timeline (Bessemer 1856 / Hadfield ~1882 / reverberatory-copper 1860s–70s) in materials.md.