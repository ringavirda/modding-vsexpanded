# Ironworking Expanded (iwex)

The iron / low-tech tier. Depends on **exlib** only. Owns the cold-blast furnace, the molten-canal network, ore handling, the mechanical (MP) air blower, the **bolted** pipe tier and `gear-iron`. Runs entirely on **water/MP power** — a vanilla waterwheel drives the blower, so no steam setup is required to make iron. This mod plus `lpex` (optionally `smex`) is a complete self-contained experience (see [overview.md](overview.md)).

Units, invariants (R1–R7), the heat-balance model and the network semantics all live in [conventions.md](conventions.md); the material catalogue (pig / cast / wrought iron compositions, ladle rules) lives in [materials.md](materials.md). This doc cites them and never restates their numbers. Every interaction is in-world and verb-based (R7). Numbers below are iwex-owned machine tunables (via the exlib config system / `ExRecipeCosts`) unless noted.

---

## The iron loop

```
coal → coke oven → coke ─┐
iron ore + coke + flux → ore mixer → burden (blast mix) → ore bunker
                                              │  (chute / skip hoist)
                              cold-blast furnace  ◀── cold air (MP blower)
                                              │
                                       molten pig iron → molten canal → mold pedestal → pig (200 u)
                                              │                              │
                              cupola (pig+scrap → cast iron)      puddling (pig → wrought balls)
                                              │                              │
                                     cast components               wrought bar/plate (= vanilla iron)
                                              └──────────► boring machine (finishing) ◀───┘
```

Pig iron is the shared intermediate: the **cupola** remelts it to castable cast iron, the **puddling furnace** decarburises it to forgeable wrought iron (materials.md). Cast iron substitutes vanilla iron in build recipes and gates LP machinery; wrought iron = vanilla iron.

---

## Ore handling

| Machine | Footprint | Power | Input → Output | Key | Status |
|---|---|---|---|---|---|
| **Ore mixer** | megablock | — | crushed iron ore + coke + limestone flux → **burden** (blast mix) | writes the `BurdenMix` (iron/flux/fuel parts) onto the output; names the target grade; off-spec / low-flux flagged in block-info | *(live)* |
| **Ore bunker** | megablock (RCC, brick-colour variants) | — | burden in → buffered burden out | front opening accepts a vanilla **chute / Archimedes screw** (or a *planned* skip hoist) to auto-charge the furnace top | *(live)* |

- **Burden** *(live)* — the prepared charge item (`iwex:burden`), replaces smex's count-only blast-mix. Carries its iron/flux/fuel mix as stack attributes, read back as fractions so split/merge preserves proportions (R2). The furnace reads the mix to scale heat, pig-per-tick and slag. Config-tunable **burden grade bands** classify a mix (`highcoke`, off-spec, low-flux). Cold blast needs a **high-coke** mix (R5, see conventions.md heat balance).
- **Dense burden** — the mixer yields one 16×-dense blast-mix per recipe, so a full pile holds ~900 u of iron charge (tunable). One pile = one furnace melt.

---

## Cold-blast furnace *(live)*

**Cold-blast furnace** — megablock, multiblock-gated, open-top stack. The iron-tier primary smelter: reduces ore burden to molten pig iron. **Ore-reduction only — never takes scrap** (materials.md).

| Footprint | Power in | Input → Output | Rate | Status |
|---|---|---|---|---|
| megablock (multiblock projection), open-top stack | cold air, **2 tuyeres @ 24 L/s** (MP blower) | dense burden + cold blast → **molten pig iron** (+ slag) | ~8 u/s pig on a high-coke burden (60 u / 10 s nominal × the temp-margin factor), all tunable | *(live)* |

- Runs the shared **dynamic heat balance** (conventions.md) *(live)*: `T_process = T_in − T_loss`, with **no maximum temperature anywhere in it**. `T_in` = a base combustion temp + a coke-ratio factor × how much blast actually reached the tuyeres + `BfPreheatCoefficient` × the blast's preheat above ambient; `T_loss` = stack radiation + cold charge mass (scaled by how full the hearth is) + a below-reference ambient. Cold blast simply means the preheat term is zero, because nothing but a charged cowper raises the pipe temperature at the tuyere — there is no ceiling constant and no branch. Per **R5** there is always a guaranteed melt path: at the shipped calibration a **high-coke** burden (≈30 % coke) settles at ~1578 °C on cold blast, clear of iron's 1482 °C line, while a low-coke burden stalls at ~1263 °C until smex's hot blast arrives. The model gates speed/efficiency, not possibility. Block-info always shows current T, the threshold, and both sides of the ledger.
- **Melt rate is a consequence, not a constant.** One yield per cycle (`BfIronPerMeltCycle`) is scaled by how far past the melt line the hearth is running (`BfMeltMarginGain`), so a hotter furnace renders faster without a second per-furnace yield number. Setting `BfMeltMarginGain` to 0 restores a flat rate.
- **No exhaust outlets** — the open top of the stack is the chimney; it does not recycle its gases. Charge loads through off-centre top hoppers by the slag-tap side. A player who falls into a working furnace dies almost instantly.
- FSM (`FurnaceState`: Idle → Firing → Melting): fills → ignites on full high-coke charge → soaks above the melt point → melts on the interval, draining pig into the tap. Disruptions (low mix, exhaust in tuyeres, choke, liquid full) count toward extinguish.
- **State machine is shared** — `BlockEntityFurnaceCore` (live base) drives firing/melting/exhaust/tuyere/drain for every furnace multiblock; the cupola and puddling furnace below are thin variants over the same core.
- Parts *(live)*: `blastfurnacecore` (the bottom-centre anchor the whole build starts from - the furnace has no door; a dead furnace is cleared by breaking its walls), reinforced hopper, bell hopper, tuyere, blast-furnace tap, slag block. Pig currently solidifies at the tap (`BlockSolidifiedIron`, chiselled out); casting to standardised 200 u pigs via the mold pedestal is *(planned)*.

---

## Molten-canal network *(live)*

Per-cell molten metal that flows cell → cell; end caps recompute on tesselation. The **ladle** (smex) is the only merge/mix point (R3); iwex owns the transport. Network semantics in conventions.md.

| Block | Role | Status |
|---|---|---|
| **Molten canal** (straight / bend / T / X) | per-cell metal conduit; flows downhill cell-to-cell | *(live)* |
| **Canal start** | furnace-tap → canal entry | *(live)* |
| **Canal tap** | canal → drains molten metal out (solidifies + chiselled) | *(live)* |
| **Mold pedestal** | canal-fed caster; pours molten metal into a held mold | *(live)* |
| **Molten barrel** | standalone molten-metal holding vessel | *(live)* |

- Craftable from cobblestone (rock variants), coloured running-brick, or fire-brick; all hammered + chiselled over fire clay.
- **Sand casting** is its own station family, not a mold-pedestal variant — the **pig bed** *(live)* casts a canal's heat into pigs, and the **casting cell / long cell** *(planned)* ram a sand mold around a wooden **pattern** to cast iron molds and machine parts. Full spec: [sand-casting.md](sand-casting.md).

---

## Remelting & refining

Both are thin `BlockEntityFurnaceCore` variants (the base is *live*; these variants are not yet built).

**Cupola furnace** — multiblock (small, 1-tuyere), MP blower air. The iron **recycler**.

| Input → Output | Key | Status |
|---|---|---|
| pigs **+ iron/steel scrap + crushed iron waste** + coke + cold air → **molten cast iron** → molds → cast components | manual load through a lid; also re-melts off-spec **waste alloy** back to cast iron (materials.md) | *(planned)* |

**Puddling furnace** — a **reverberatory** multiblock with a **3×1×1 internal hearth** (a fettled cast-iron bed the player works **directly through the door**, the manual-reverberatory idiom), manual (lid throttle + rabbling), **no GUI** (R7). The **only** wrought-iron route (materials.md: wrought is puddling-only).

| Input → Output | Key | Status |
|---|---|---|
| oxide fettling + **9 pigs** + heat → **wrought-iron balls** (100 u; 9 pigs → 18 balls, mass-conserving R2) | Each of the **3 hearth cells holds 3 pigs** (they nest on their triangular section); sneak+RMB crushed ore / roasted **slag** lays the oxide fettling — **re-fettled each heat** (the closed waste loop); lid regulates temp; **rabble** the pasty iron through the door (repeated RMB with the rabbling bar), then pull the white-hot balls out one by one → helve hammer **shingles** them to **blooms** (§ Forming), *not* straight to bar/plate | *(planned)* |

---

## Forming — reheat furnace + rolling mill *(planned)*

The wrought line's back end: consolidate the puddle balls, bring the stock to rolling heat, and roll it into
usable stock. **All hot working** — cast iron shatters; wrought iron (and the steel smex later runs through
the same mill) is worked hot. Rolling is **early**: it is the other half of Cort's 1784 puddling-and-rolling,
so it lives here, and everything downstream (lpex machines especially) leans on rolled parts.

**Shingle → bloom (pile balls on the anvil).** The **helve hammer** (vanilla, MP) works white-hot balls on the
anvil exactly as vanilla works an iron bloom into an ingot — you **pile** balls (the same voxel-accumulation as
stacking ingots for a vanilla plate) and the helve hammers them into a **bloom** (~180 u after slag loss; the
scale → the oxide loop). Two balls → one bloom, so a 9-pig heat → 18 balls → **9 blooms**. Implementation reuses
the **vanilla anvil + helve + smithing-recipe pipeline** wholesale: the ball is a `forgable` item (cf.
`ItemIronBloom`) and shingling is a smithing recipe `2 piled balls → shingledbloom`. The bloom is the
**generic wrought stock** — the helve only *consolidates*, it never sets the section; the **mill** forms it (its
flat set flattens the bloom toward plate, a grooved set draws it toward bar). This fixes the old
"helve → bar/plate" shortcut.

| Machine | Footprint | Power | Input → Output | Key | Status |
|---|---|---|---|---|---|
| **Reheat furnace** | multiblock reverberatory | fuel (coke) | cold stock → **hot stock** (rolling temp) | firebox coalpiles + a **direct-worked hearth bed** sized for the largest form (**slab = 2 cells**); a thin variant over the shared `BlockEntityFurnaceCore`; heats on the shared heat balance (R5). The vanilla forge tops out at an ingot — a slab/bloom won't fit, so hot rolling **needs** this | *(planned)* |
| **Rolling mill** | megablock | **MP** (waterwheel) + flywheel | hot bloom/slab/billet + **roll set** → profiled stock (bar / plate / sheet / strip / rod / nail-rod) | one **two-high** stand; the swappable **roll set** carries a **fixed gap sequence cut along the roll barrel** (widest→narrowest) — the player walks the stock through successive segments, **each RMB = one pass**; **no screw-down**; feed side follows drive rotation; staged glowing work-item render | *(planned)* |

**Rolling mill detail.**
- **Hot rolling only.** `δ_max = μ²R` — hot (μ≈0.5) takes ~30× the reduction per pass of cold (μ≈0.09), at low
  force: the only process a water/MP mill can drive. A pass **requires** the work item above a rolling-temp
  threshold (reheat furnace / residual cast heat). Cold rolling (precision sheet, **thread-rolled bolts**) is
  an elex-era four-high/powered upgrade.
- **Roll sets = tooling any mod ships** (the mold-pattern/`MoldSpec` idiom): `{family, accepts:[form], passes,
  output, minTorque}`. iwex ships **flat** (→ plate/sheet/strip; a **narrow segmented** barrel for billets, plus
  **wide single-gap** barrels for slab/bloom run as a train), **grooved** (→ rod → wire-rod), **slitting**
  (→ nail-rod → nails, the early nail mill). smex adds steel sets ([smex.md](smex.md)).
- **Rolls are cast (chilled cast iron)** — a roll takes steady **compression**, not shock → cast, not forged
  (the cast-vs-forged rule); harder tiers gate on **torque**, not roll material.
- **Multi-pass = walk the stock along the barrel, not a screw-down.** A heavy manual two-high isn't re-gapped
  mid-schedule, so the roll barrel carries a **fixed sequence of gaps** (widest→narrowest); the player passes the
  work through successive segments, **each RMB = one pass**. `δ_max = μ²R` is enforced by the *geometry* (each
  segment is one step — you can't skip): the work item carries its **current thickness/form**; the next-narrower
  segment reduces + re-renders it (the steam-hammer **staged work-item renderer** morphs it), while too-tight a
  gap **stalls** it (over-reduction overdraws the flywheel). **The product is the thickness you stop at** — a flat
  set runs 2.0→1.5→1.0→0.5 where **1.0 = plate, 0.5 = sheet**; you pull the stock at the form you want, not always
  to the end.
- **Wide stock needs a train, not a wider screw.** Slab/bloom fill the whole barrel → no room for segments, so
  each **wide roll set is a single gap** (flat-wide 2.0 / 1.5 / 1.0 / 0.5). Reducing wide stock is a **sequence of
  stands**: swap the wide set between passes (occasional) or build a short **manual train** of fixed-gap mills
  (throughput) — the period plate-mill hall (`assets/editable/refs/C0229569`). **Automatic / reversing trains are
  deferred** (too advanced while the rest of the line is hand-fed); trains stay hand-fed for now.
- **Feed side follows drive rotation.** The mill reads its drive axle's spin and sets the input face from it
  (clockwise / fed-from-the-right → **south**, counter-clockwise → **north**; output the opposite) — reverse the
  drive to reverse the mill.
- **Power** is MP buffered by a **flywheel** ([mp-energy-network.md](mp-energy-network.md)): each pass is an
  energy pulse the flywheel dumps into the bite; cold stock overdraws it and **stalls** the mill — the
  keep-it-hot loop the reheat furnace answers.

---

## Fuel, flux & finishing

| Machine | Footprint | Power | Input → Output | Key | Status |
|---|---|---|---|---|---|
| **Coke oven** (bulk) | megablock | fuel | coal → **coke** | **COKE ONLY, no tar** (tar comes from the chemistry gasification plant, not here); bulk replacement for the tiny vanilla oven — the single fuel behind burden, cupola, crucible and recarburising | *(planned)* |
| **Lime kiln / cement** | — | — (coke-fired shaft) | limestone + coke → **quicklime**; quicklime + ground slag → **cement / concrete** | **recipe modes / barrel mixes, not separate machines** — quicklime is a continuous-shaft-kiln output on the same heat balance; cement is a barrel/recipe payoff | *(planned)* |
| **Boring machine** | megablock | MP (waterwheel) | rough cast/forged part + **drill bit** → precision bore / turn / screw-cut | the finishing gate for every rotating/mating part (cylinders, bearings, shafts, threads); **bit material gates the hardest metal it can machine** (cast-iron → quench-hardened steel → HSS); predates and enables the steam engines | *(planned)* |

---

## Power, pipes & parts

| Feature | Footprint | Input → Output | Key | Status |
|---|---|---|---|---|
| **Mechanical (MP) air blower** | megablock (sub-machine) | MP (waterwheel) → **low-pressure air** into pipes/tuyeres | walking-beam, waterwheel/MP-driven — **this is why iron needs no steam**; feeds the cold-blast furnace and cupola tuyeres. Cannot unlock the hot-blast low-coke blow. Shape authored at `assets/editable/shapes/airblower.json` (idle + cycle anims) | *(planned)* |
| **Bolted pipe** | block (on the shared exlib pipe network) | gas *or* water segment | the **iron material tier** of pipe (R1, single medium per network), hand-riveted from plates — the lowest-pressure tier; rides the same network as lpex's cast pipes and hpex's rolled pipes so iwex never depends on them. Connectors read the adjacent cell; valves sever/flow | *(planned)* |
| **`gear-iron`** | item | — | iron-tier MP transmission gear; iwex's own gear so MP builds don't require lpex/vanilla rusty gears (recipes currently accept `game:gear-rusty` or `lpex:gear-*`) | *(planned)* |

---

## Items

| Item | Role | Status |
|---|---|---|
| **Burden** (`iwex:burden`) | prepared blast-furnace charge; carries iron/flux/fuel mix | *(live)* |
| **Slag** + slag paths/slabs/stairs | blast-furnace byproduct → building material | *(live)* |
| **Pig** (200 u) | cast at the mold pedestal (pig mold variant); feeds cupola & puddling | *(planned)* |
| **Coke** | coke-oven fuel output | *(planned)* |
| **Wrought-iron ball** (100 u) | puddling output; helve-shingled to a **bloom** | *(planned)* |
| **Bloom** (~180 u) | shingled wrought iron; the rolling mill's input stock (= vanilla iron once rolled) | *(planned)* |
| **Roll set** | swappable, spec-carrying rolling-mill tooling (flat / grooved / slitting; smex adds steel sets) | *(planned)* |
| **Cast components** | poured from molten cast iron into component molds — **`castframe`** (the I-section machine standard shared by the flywheel / rolling-mill / steam-hammer frames), cylinder sleeve, bedplate, cast pipe, grate/door, valve body | *(planned)* |

Cast components come out of the **sand-casting** stations, not the mold pedestal — see
[sand-casting.md](sand-casting.md) for the cell, the pattern items, and the **iron mold** family that
replaces the placeholder ceramic tool molds.

---

## Add-on: Crucible

Optional, off the core spine (see overview.md). Adds a small crucible-steel line. **Cementation is vanilla — not implemented here.** Materials in materials.md (blister steel, shear steel, crucible steel).

| Machine | Footprint | Input → Output | Status |
|---|---|---|---|
| Cementation furnace | *(vanilla)* | wrought bars + charcoal → **blister steel** | *(vanilla)* |
| Helve hammer | *(vanilla)* | blister steel (piled + forge-welded) → **shear steel** (= vanilla `game:steel`) | *(vanilla)* |
| **Crucible-steel furnace** | simple multiblock | blister steel + coke (+ fired clay crucible) → **molten crucible steel** → cast ingot | *(planned)* |

The vanilla **helve-hammer route turns blister into shear steel** — that *is* vanilla `game:steel` (a homogenised tool steel, see materials.md). This add-on **swaps** that route for melting blister into **crucible steel**, a separate high-carbon **tool** alloy (+durability, +damage), better than shear steel and **not** vanilla steel.

---

<sup>Historical note: the water-powered boring mill (Wilkinson, 1774) is what made steam-engine cylinders seal — hence the boring machine sits in the iron tier and enables the engines.</sup>