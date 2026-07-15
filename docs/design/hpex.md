# High Pressure Expanded (hpex)

**High Pressure Expanded** is the high-pressure steam tier and the large-scale / community machine set. It depends on **smex** (full chain `exlib → iwex → ppex → smex → hpex`); [elex](elex.md) builds on top of it. See [overview.md](overview.md) for the mod map and build order, [conventions.md](conventions.md) for units and invariants (R1–R7), and [materials.md](materials.md) for the material catalogue and the material-gated power-tier rule.

**Why it is its own mod.** Most players stay low-tech, so high pressure is deliberately opt-in — a player who never installs hpex still has a complete `iwex + ppex + smex` experience (see overview.md). The large, server-scale and community machines also live cleaner here than bloating smex.

**Material gate.** Every hpex machine is built from **hadfield steel** (see materials.md — HP machinery is gated by construction material, not recipe unlock). Finish the steel work first.

---

## What "high pressure" is

There is **no separate HP network**. The pipe network is one live pressure pool (R1); "HP" is simply steam carried **above a config pressure threshold**, and each HP machine gates on an **inlet pressure band** rather than a medium. LP machinery (ppex, cast iron) tops out at the low band; HP machinery (hadfield) engages only in the high band (see conventions.md for the atm ranges — LP ≤ ~4–5 atm, HP ~8–12 atm, tunable). Engine output pressure = inlet × efficiency (see conventions.md).

---

## HP steam power

**Lancashire boiler** — RCC **megablock** + **multiblock** (projection-gated). The HP boiler: a larger, hadfield-plated pressure vessel that raises steam into the high band. *(live)*
**Input → Output:** water + fuel → **HP steam**.
Key mechanic: same shared-tank boiler FSM as the Cornish boiler (see ppex.md), but chokes at a higher output-pressure cap (~12 atm, hpex-owned, tunable); over-pressure past the cap bursts (R5 gates efficiency, not the vessel — the burst is the consequence of ignoring it).
Build: 4× hadfield plate + 6× rolled pipe + heavy cap + injector (~3050 u, hadfield; see build table below).

**Cornish engine** — **megablock** + sub-machine. The efficient deep-lift pumping/blowing engine; wrench-throttled control rods (low / normal / high) raise the whole operating band. *(live)*
**Input → Output:** HP steam → an efficient **pump or blower** sub-machine (RCC megablock on its drive face).
Key mechanic: three throttle settings set the engage/break pressure band (~5–8 atm across settings, hpex-owned, tunable); can turn a flywheel for MP but **ungoverned → inefficient at MP** (~4 kW). The deep-lift pump variant serves deep wells.

**Corliss (horizontal) engine** — **megablock** + sub-machine. The governed **HP → MP** mill / line-shaft engine; the base platform for the elex generators (the dynamo and alternator are Corliss flywheel variants — see elex.md). *(planned)*
**Input → Output:** HP steam → **MP** (heavy flywheel drive).
Key mechanic: Corliss valve gear + governor — **steam follows load**, so it is efficient (more MP per litre than an ungoverned Cornish); **cannot pump or blow** (rotary output only). ~8 kW baseline, tunable.

**Compound / tandem Corliss** — **upgrade tier** of the Corliss engine (inline second cylinder, heavier flywheel), **not** a separate engine. Drives large MP / a generator. *(planned)*
**Input → Output:** HP (+ LP) steam → **large MP / generator drive**.
Key mechanic: tandem-compound cylinders reuse exhaust across stages; ~36 kW baseline, tunable — the drive for the elex arc-furnace and alternator banks.

---

## Large-scale / community machines

Sized for **server communities to build, fuel and operate collectively** — not solo builds.

**Large blast furnace** — **multiblock** (open-top stack, internal 5×5×8). Roughly 3× the throughput of the smex hot-blast furnace. *(planned)*
**Input → Output:** dense blast mix + hot blast → **molten pig** (per-cell molten canal, R3).
Key mechanic: **6 tuyeres** and **4 exhaust outlets**; dynamic heat balance (see conventions.md) with the cowper hot-blast buff. hpex-owned tunables: ~135 u/s pig; coke ~0.2 u/u; blast target ~160 L/s (6 × 24 = 144 L/s, ~16 L/s over to hold pressure); 4 exhaust @ 36 L/s (144 L/s out) → **3 smokestacks**; **2 cowper stoves** (smex) preheat the full blast. Driven by **two large pumping engines** (one blower, one pump). Feeds ~8–9 Bessemers + ladles.

**Large Cornish pumping engine** — **megablock** (~3w × 6t). One oversized engine driving **one large sub-machine**. *(planned)*
**Input → Output:** HP steam → **one heavy blower @ ~160 L/s** *or* **one heavy pump @ ~36 L/s**.
Key mechanic: the large furnace needs **both** → **two engines**, one of each sub-machine. Heavy blower/pump sub-machines reuse the walking-beam MP shapes in `assets/editable/` (idle + cycle anims present). hpex-owned tunables: ~160 L/s blow, ~36 L/s pump (~2.16× the standard sub-machine); ~4400 u build.

**HP steam ore crusher** — **megablock**. The high-band variant of the steam ore crusher; HP jaws crush ores the LP crusher can't. *(planned)*
**Input → Output:** **chromite & wolframite** → crushed **HSS feedstock** (also crushes hardened-steel scrap for arc recycling).
Key mechanic: a **hardness gate** — LP jaws (ppex/smex) handle iron/soft ores; only HP jaws (hadfield, hpex) crack chromite/wolframite, gating the HSS alloying elements (see materials.md — HSS). ~1400 u build.

**Skip hoist** — **megablock** (inclined). The **one surviving transport machine** (all other transport is cut). *(planned)*
**Input → Output:** bulk solids at the base → lifted to a tall furnace top.
Key mechanic: powered vertical/inclined lift; **primary and only use — charges the large and hot blast furnaces** from the ore-mixer bunkers (iwex). MP-driven skip car + chain.

---

## Steel grade gates

The two industrial steels are **distinct materials**, not one steel with a nitrogen tag (see materials.md): **Bessemer steel** is high-N, **open-hearth steel** low-N. hpex defines and **enforces** the hard gates that key off *which steel a part is built from* — there is no ppm attribute or arithmetic. High-N Bessemer steel also carries a light flavour penalty (a chance of forge/roll crack-waste). *(planned)*

| Gate | Requires | Rationale |
|---|---|---|
| **Boiler plate / pressure-critical parts** | **open-hearth steel**, wrought iron, **or** hadfield | high-N Bessemer steel is strain-age brittle under pressure; hadfield passes on being an alloy |
| **HSS feedstock** | **open-hearth steel** | the arc furnace (elex) won't make good HSS from high-N Bessemer steel |

The open-hearth (low-N) **source** is the smex open hearth (see smex.md / overview.md — smex owns it); hpex only enforces the gates.

---

## Machine cards

Footprint uses the block-size vocabulary (block / megablock / multiblock, see conventions.md). Costs in metal units (u); "+R" = refractory counted separately. All u/s and kW are nominal full-margin baselines (see conventions.md heat-balance model, R5) and tunable via ExRecipeCosts.

| Machine | Footprint | Power in | Input → Output | Rate (tunable) | Build | Status |
|---|---|---|---|---|---|---|
| Lancashire boiler | RCC megablock + multiblock | fuel + water | water + fuel → HP steam | chokes ~12 atm | ~3050 u hadfield (+R) | *(live)* |
| Cornish engine | megablock + sub-machine | HP steam | HP steam → pump / blower | ~4 kW (MP ungoverned) | 2700–4000 u hadfield | *(live)* |
| Corliss engine | megablock + sub-machine | HP steam | HP steam → MP | ~8 kW | ~2700 u hadfield | *(planned)* |
| Compound/tandem Corliss | megablock + sub-machine (Corliss upgrade) | HP + LP steam | steam → large MP / generator | ~36 kW | ~4000 u hadfield | *(planned)* |
| Large blast furnace | multiblock (5×5×8) | 2 large engines | blast mix + hot blast → pig | ~135 u/s; 6 tuyeres, 4 exhaust | tens of billets (+R) | *(planned)* |
| Large Cornish pumping engine | megablock (~3w×6t) | HP steam | HP steam → 1 heavy blower **or** pump | ~160 L/s blow / ~36 L/s pump | ~4400 u hadfield | *(planned)* |
| HP steam ore crusher | megablock | HP steam (MP jaws) | chromite/wolframite → HSS feedstock | hard-ore gate | ~1400 u (hadfield jaws) | *(planned)* |
| Skip hoist | megablock (inclined) | MP | bulk solids → furnace top | — | minor (buckets/chain) | *(planned)* |

---

## Config knobs (hpex-owned)

The HP pressure threshold, the boiler choke cap, and the Cornish engine's three-band engage/break pressures are config-tunable. They currently live in the ppex config (the live Lancashire boiler + Cornish engine ship there) and are destined to move with the blocks into hpex.

> *Footnote:* Historically the Cornish/Corliss/Lancashire family (~1840s–1880s) sits after Bessemer and hadfield in the timeline (see materials.md), matching the material gate.
