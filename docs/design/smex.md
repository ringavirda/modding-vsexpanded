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

## Steam forming shop *(planned)*

A **cluster of machines**, not one megablock — because rolling is rotary (MP) and hammering is impact (LP);
you cannot roll on a hammer. The **rolling mill lives in iwex** (it is early — puddling needs it; see
[iwex.md](iwex.md) § Forming); smex's own anchor is the **steam hammer** for impact ops (shear / stamp /
open-die forge). What smex *adds* to the rolling side is **steel roll sets**, an **automation upgrade** to the
mill, and the **cast semi-finished forms** the steel line rolls. Delivers the billet pipeline (R4: billets
cannot be worked on a vanilla anvil, only in this shop). Stock carries remaining mass in a unit-count
attribute (R6), so every cut is exact arithmetic; masses live in materials.md.

```
liquid steel --longcell cast--> slab / bloom / billet --reheat--> iwex ROLLING MILL (+ steel roll set) --> steel plate / rail / shape / bar / skelp→pipe / wire-rod
                                                                   steam hammer (shear / stamp / forge) --> discrete items + heavy forged components
```

### The steam hammer — the anchor *(design draft, 2026-07-25)*

> **Tier + power (updated 2026-07-26):** this is an **lpex** machine — **LP steam**, cast-iron frame (iwex),
> **iron dies** — buildable as soon as you have a boiler, *before* steel. The modeled shape is **single-action**
> (one bottom cylinder pipe: steam lifts the ram, gravity drops it). Steel die-sets are the **smex** upgrade; a
> separate **HP double-action** hammer (steam on both strokes) is an **hpex** machine for thick/complex work.
> **Die complexity gate = output thickness:** ≤1 voxel (strips, nails, shear) is LP; ≥2 voxel (tools, complex
> forgings) needs the HP hammer. **No bootstrap loop:** the whole rolled-plate chain (puddling → helve →
> rolling mill) is coke/MP-powered, so the first LP boiler is riveted from **MP-rolled narrow plates**, then it
> powers the hammer; the hammer's wide **shingled-slab → wide-roller** plate is a post-steam upgrade, not a
> prerequisite. Its **reverse (iron) usage** — faster shingling + wrought slabs — is the *original* Nasmyth use
> and keeps it useful for a "stay at iron" player. *(Section still filed here; relocate to lpex.md when the
> forming design settles.)*

The machine everything else installs onto: a **Nasmyth double-frame steam hammer**. Shape drafted at
`assets/editable/shapes/steam-hammer.json` — cast-iron frame + hammer mass, steel (`iron5`) piston / rod /
dies / lever; animated off a `Rod` element (the ram assembly hangs from it) with `idle` / `steamup` /
`leaverdown` / `hammerhit` keyframes, plus a `HandLeaver` + `ControlRod`.

**Footprint — a sparse 3×3 megablock.** Bottom layer full 3×3; middle layer **vslab–block–vslab**; top
layer a **single centre block**. The top-centre block carries the cylinder and both network faces (standard
megablock + invisible-filler pattern: fillers give per-cell collision, the top block owns the behaviours):
- **west face** = LP-steam **inlet** (pipe connector reading the adjacent cell; steam flowing → machine is
  powered);
- **east face** = the **"depleted out"** spent-steam vent — spawn exhaust particles here on each blow.

**Animation state machine** (code-driven, nothing to hand-sync):
- `idle` — no steam, ram parked at the **bottom**;
- `steamup` — steam present, ram **raised and held**;
- **operation** — while the player **holds RMB on the lever cell**: play `leaverdown` **and** loop
  `hammerhit` together, with sparks + spent-steam particles + a piston-stroke sound per cycle; release RMB →
  stop and ease back to `steamup`. The lever cell is a **behaviour-capable filler** routing the held
  interaction to the top block's BE, which owns the loop.

**The anvil — a separate DOCKED block** (the canal-tap / molten-barrel idiom, maintainer's call). The hammer
BE stores an anvil `ItemStack`, tesselates its mesh at the anvil cell in `OnTesselation`, and sneak+RMB
docks / undocks it — carrying any installed die out with it, exactly as a mold carries its contents. Payoff:
**anvil variety for free** and a customization hook. Slot layout: **top die on the ram** (`HammerDie`),
**bottom die on the docked anvil**, the work item between them. **NOT a vanilla `BlockAnvil` variant** —
vanilla's class carries the free-sculpt hand-smithing voxel mechanic a die machine does not want; make it
its own block (a die/sow block that can share the anvil look, not its class).

**Dies — forged steel, installable.** A die takes the blow *and* imparts the profile → **forged** (tough),
not cast (brittle), per the cast-vs-forged rule. Two tiers mirroring the boring-machine bits: plain steel
for soft stock, **quench-hardened** to shear/stamp hadfield or HSS. Prefer **one "die-set" item** that
renders in both faces (can't be mismatched) over two independent top/bottom items.

**Visible work item on the anvil — our own renderer, not vanilla's.** Vanilla's anvil renderer is welded to
the free-sculpt smithing system (`ItemWorkItem` + voxel grid + per-blow player choice); a die machine is
**deterministic** (stock + die → output), so grafting it fights the design. Build the work-item render with
the mod's own BE-renderer toolkit (as `MoltenRenderer` / the barrel content mesh / the held-mold surface do):
- **Staged / morphing stock (preferred):** the docked work item sits on the bottom die, **glowing off its
  own temperature**, and each `hammerhit` cycle advances it through a few forge stages (raw billet →
  half-formed → the die's output profile) with sparks + an impact flash. Reads as "metal worked under the
  hammer", matches the die logic, cheap to build.
- **True moving voxels (possible, more work):** store a small voxel grid on the BE and carve/stamp it toward
  the die's target shape each blow, rendered as glowing cubes — closer to vanilla's look, but more work and
  it slightly implies the shape is sculptable. Reach for it only if the staged version doesn't sell in-game.

**Materials.** Frame + hammer mass + **anvil block = cast iron** (compression members — built from
`castplate-heavy`, the heavy plate's structural third job alongside the puddling hearth and machining
stock); piston / rod / dies = steel. **Power:** **LP steam** drives the ram. Rolling is **not** on this
machine — it is the iwex **rolling mill** (MP), to which smex only adds tooling (below).

**Build order when scheduled:** megablock shell + LP-steam-driven ram (idle → steamup → lever-hold cycle) →
docked-anvil socket + die-set items + staged work-item renderer.

### Steel roll sets + mill upgrade (for the iwex rolling mill) *(planned)*
smex does **not** ship a second mill — it ships **roll sets** (spec-carrying tooling) and an **automation
upgrade** for the one iwex mill (the "one machine, more tooling" rule; a second mill would be a reskin).
- **Cast semi-finished forms** (the steel line's stock): liquid Bessemer/OH steel → **longcell** sand-cast →
  **slab / bloom / billet** (the continuous-casting shortcut — cast the form directly; see materials.md). The
  form's section is the gate (slab = flat, bloom = square, billet = small-square).
- **Steel roll sets** (cast chilled rolls, higher `minTorque` than the wrought sets): **wide-flat** (slab →
  steel plate), **shape** (bloom → rail / I-beam / angle), **pipe/skelp** (slab → **skelp** strip → bell-weld
  → **rolled pipe**, feeding hpex's pipe blocks), **wire** (billet → wire-rod). *(Owned gap/pass maps live
  with each roll set; tunable.)*
- **Mill-type upgrade = throughput/automation, orthogonal to roll sets.** The iwex base is a manual **two-high**
  (re-feed each pass); smex's upgrade makes it **auto-run the pass schedule** (**reversing / three-high**, on
  the production-machine framework — one flywheel energy-pulse per pass). Four-high / continuous (thin gauge,
  cold, mass) is elex's. **Deferred:** ship the manual mill first; add the automation upgrade when throughput
  demands it.

### Dies (shear / stamp) *(planned)*
- **Input → output:** profiled stock → discrete items (shear-die divides by item mass); billet → forged heavy components (open die).
- **Mechanic:** installable dies select the op, and **the die's output thickness gates the hammer tier** — **≤1-voxel-high** work (strips, nails, shear, thin brackets) runs on the **LP single-action** hammer (lpex, gravity-drop blow); **≥2-voxel-high** work (tools, complex/thick forgings) needs the **HP double-action** hammer (hpex, steam on both strokes). Shearing is thin → LP from day one. **Batch size = die cavity count** (read off the die mesh). Shearing/stamping hadfield or HSS needs **quench-hardened dies** (mirrors the boring-machine bit tiers).

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