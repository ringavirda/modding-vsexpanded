# Material Catalogue

Every material, where it comes from, and what it's for. The **Owner** column is the mod/add-on that
produces it. Numbers here are the single source of truth; mod docs cite them. See
[conventions.md](conventions.md) for the units and the mass-conservation rule (R2).

---

## Materials

| Material | Owner | Produced from (process) | Carbon / character | Primary use |
|---|---|---|---|---|
| **Pig iron** | iwex | Cold blast furnace (molten) → sand "pigs" | very high C, brittle | Feedstock for cupola & puddling |
| **Cast iron** | iwex | Cupola remelts pig → molds | high C, castable, brittle | Castable components; **substitutes vanilla iron in build recipes**; gates **LP machinery** |
| **Wrought iron** | iwex | Puddling pig → shingling | low C, tough, fibrous | **= vanilla iron**; forgeable bar/plate. **Puddling-only** |
| **Blister steel** | vanilla | Vanilla cementation of wrought bars | carburised, uneven C | Feedstock for **shear** and **crucible** steel |
| **Shear steel** | vanilla | Blister bars piled + forge-welded under the **helve hammer** | homogenised, medium-high C | **= vanilla `game:steel`**; tool-grade — the pre-Bessemer baseline steel |
| **Crucible steel** | iwex-crucible | Crucible furnace melts blister | homogeneous high C | Tools/weapons (**+durability +damage**). Optional — upgrade over shear steel |
| **Bessemer steel** | smex | Bessemer converter blows pig | low C, **high-N** (~150 ppm) | Mass **structural** mild steel; cheap & fast, strain-age brittle → **barred from pressure parts** |
| **Open-hearth steel** | smex | Open hearth (pig + scrap) | low C, **low-N** (~50 ppm) | Mass mild steel, scrap-capable; the **pressure grade** — boiler plate, pressure parts, **HSS feedstock** |
| **Ingot iron** | smex / elex | Bessemer **over-blow**, or arc-smelted iron ore | ~0 % C, slag-free | Soft; **not wrought iron** — recarburise or re-melt |
| **Hadfield steel** | smex | Ladle: mild steel (Bessemer / open-hearth) + manganese | Mn austenitic, work-hardening | **HP machinery only** (gates the HP tier by material); **not** tools/weapons |
| **Converter copper** | smex-copper | Reverberatory → Pierce-Smith converter | impure | Rods, impure wire, bronze base |
| **Bronze types** | smex-copper | Ladle: copper + tin / zinc / bismuth / gold+silver | — | Tin bronze, brass, bismuth bronze, black bronze |
| **Pure copper** | elex | Electrolytic refining | high purity | Low-loss cable; **unlocks alternators (AC)** |
| **HSS** | elex | Arc furnace + tungsten + chromium | hot-hard | **Best tools; needs no tempering** |
| **Waste alloy** | any | Off-spec ladle mix | n/a | Crush → re-melt to recover base Fe or Cu (never the blast furnace) |

> **The steel model.** Vanilla `game:steel` is **shear steel** — the tool-grade product of the vanilla
> blister → helve-hammer route (cementation is vanilla). The **crucible add-on** swaps that route for
> melting blister into **crucible steel** (a better tool steel). The industrial steel lines (smex)
> produce **Bessemer steel** and **open-hearth steel** as *distinct new materials, not vanilla steel*:
> both are low-C mild steel that differ **intrinsically by nitrogen** (Bessemer high-N, open-hearth
> low-N) — the grade **is** the material identity, so there is no ppm attribute or tag. These are the
> mods' **structural / pressure** steels; tools come from shear / crucible / HSS steel.

**Material-gated power tiers (key lever).** The two steam-power tiers are separated **by construction
material**, not merely recipe: **LP machinery** (lpex) is built from **cast iron**; **HP machinery**
(hpex — Lancashire boilers, Cornish/Corliss cylinders) can **only** be built from **hadfield steel**.

**Hadfield is the alloying-mechanic introduction** — the first metal that *must* be made by mixing a
base (Bessemer or open-hearth mild steel) with an element (manganese) in the ladle, teaching the system
that later gates HSS and the bronzes. It gates HP machinery by material (mirroring how cast iron gates LP), so the player must
finish the steel work before high-pressure power.

> **Timeline** (correctly ordered): shear steel (piled blister, early 1700s) / crucible steel
> (Huntsman 1740s) / Bessemer steel (1856) / open-hearth / Siemens-Martin steel (1860s–70s) / Hadfield
> (~1882) / reverberatory + electrolytic copper (1870s) + Bessemer-style copper converting (Manhès-David
> ~1880 → Pierce-Smith 1909) / electric arc + HSS (~1900). Cowper hot-blast stoves belong only with the
> **hot** blast furnace (smex), never cold blast.

---

## Alloy compositions (target ratios)

Every alloy carries a **composition** (mass fractions). **Carbon is set by *process*** (the blast
furnace carburises, the Bessemer blows it out, cementation adds it back); the metallic elements
(Mn, W, Cr, Sn, Zn) are added **by held proportion in the ladle**. Tunable.

| Material | Fe / Cu base | Carbon | Other (target) | Character |
|---|---|---|---|---|
| **Pig iron** | ~96 % Fe | ~4.0 % C | Si/Mn/P/S (slag flavour) | very high C, brittle |
| **Cast iron** | ~97 % Fe | ~3.0 % C | — | castable, brittle |
| **Wrought iron** | ~99.9 % Fe | <0.1 % C | fibrous slag stringers | = vanilla iron |
| **Blister steel** | ~99 % Fe | ~1.0 % C | (surface-carburised, uneven) | shear + crucible feedstock |
| **Shear steel** | ~99 % Fe | ~0.8 % C | — | **= vanilla `game:steel`**; homogenised tool steel |
| **Crucible steel** | ~98.8 % Fe | ~1.2 % C | — | homogeneous high-C tool steel |
| **Bessemer steel** | ~99.8 % Fe | ~0.2 % C | high-N | structural mild steel |
| **Open-hearth steel** | ~99.8 % Fe | ~0.2 % C | low-N | pressure-grade mild steel |
| **Ingot iron** | ~100 % Fe | ~0 % C | — (slag-free) | over-blow / arc; **not** wrought iron |
| **Hadfield steel** | ~86.3 % Fe | ~1.2 % C | **~12.5 % Mn** | austenitic, work-hardening |
| **HSS** | ~77.25 % Fe | ~0.75 % C | **~18 % W, ~4 % Cr** | hot-hard; W-Cr (no vanadium) |
| **Tin bronze** | ~88 % Cu | — | ~12 % Sn | bearings, fittings, cocks |
| **Brass** | ~70 % Cu | — | ~30 % Zn | gauges; **needs a coke cover or the Zn boils off** |
| **Bismuth bronze** | ~60 % Cu | — | ~25 % Zn + ~15 % Bi *(vanilla range)* | tool/deco |
| **Black bronze** | ~84 % Cu | — | ~8 % Au + ~8 % Ag *(vanilla range)* | tool/deco |
| **Converter copper** | ~98 % Cu | — | impurities | impure rod/wire stock |
| **Pure copper** | ~99.95 % Cu | — | — | electrolytic; low-resistance wire |

> Bismuth-/black-bronze fractions follow the **vanilla `metalalloy` ranges** — verify against the
> installed game before pinning exact numbers.

---

## Unit economy (baseline, tunable)

| Item | Mass (u) | Item | Mass (u) |
|---|---|---|---|
| Ingot (vanilla) | 100 | Sand "pig" | 200 |
| Small billet | 800 | **Large billet** | **2400** |
| Heavy plate | 400 | Plate (vanilla) | 200 |
| Sheet metal | 100 | Strip | 50 |
| Heavy bar | 300 | Rod (vanilla) | 100 |
| Thin rod | 50 | Wire rod | 25 |
| Large pipe | 300 | Rolled pipe | 150 |
| Small tube | 75 | Nail (stamped) | ~3 (batched) |
| Wrought ball | 100 | **Bloom** (wrought) | ~180 |
| Skelp | 50 | Slab / cast form | *(R6, cast-sized)* |

---

## Ladle mixing rules

The ladle is the only merge/mix point (R3). Pour the base metal and add each element in its listed
fraction; mass-conserving (R2). Worked examples for an 800 u small billet:

- **Hadfield:** ~700 u mild steel (Bessemer / open-hearth) + ~100 u manganese (12.5 %).
- **HSS:** ~624 u open-hearth-steel base + ~144 u tungsten + ~32 u chromium.
- **Tin bronze:** ~704 u copper + ~96 u tin.

**Off-spec → waste alloy.** Held proportions outside the valid window (wrong ratio, missing element,
boiled-off zinc) do **not** snap to the nearest alloy — they yield a **waste alloy** that keeps the full
base-metal mass but is otherwise useless. Crush and re-melt to recover the base metal: **iron** waste in
the **cupola** (→ cast iron), as **capped cold scrap in the Bessemer** on a live heat (→ Bessemer steel), or
in the **arc furnace** (→ ingot iron); **copper** waste in the **reverberatory** or **arc**. The **blast
furnace never takes scrap** — it is an ore-reduction primary smelter. A botched mix costs time and the
alloying additions, never the underlying iron or copper.

> Alloying is the bespoke **ladle** mechanic, **not** vanilla `AlloyRecipe` emission — off-ratio mixes
> must yield waste, never snap. (`MetalDef.Alloy` / `MetalAlloySpec` is inert and must stay so.)

**Carbon is adjustable both ways.** A converter blow *removes* carbon; the player *adds* it back by
hand-dropping **powdered coke** into the molten metal in the ladle. Each unit adds a fixed *mass* of
carbon (its effect on % C is relative to the iron present) — how you hit crucible/Hadfield/HSS carbon
targets after a clean blow, and how you carburise the arc furnace's ingot iron.

**Wrought iron is puddling-only.** Its toughness comes from slag *fibres* worked into a pasty ball and
elongated by shingling; a fully molten route (Bessemer, arc) can't reproduce that, so an over-blown or
arc-smelted melt yields **ingot iron** (slag-free, ~0 % C), never wrought iron.

## Semi-finished forms (rolling stock)

The rolling mill's input is a **semi-finished form**, and the line between the tiers is *how the form is
made*:

- **Wrought bloom = hammer-made.** Wrought iron is never molten, so it can't be cast — it is **consolidated by
  hammering**. The helve hammer shingles the puddle balls into a **bloom** (~180 u; expelled scale → the oxide
  loop) by **piling** them on the anvil (the vanilla iron-bloom→ingot / stacked-ingots→plate mechanic reused
  wholesale). A **9-pig puddling heat → 18 balls → 9 blooms** — a clean batch (see [iwex.md](iwex.md) § Forming).
  The bloom is the **one generic wrought form**: the *mill* sets the section (flat set → plate, grooved → bar),
  so there is **no separate wrought slab/billet**. Rolled wrought stock **is vanilla iron**, so bar off the mill
  is the smith's feedstock — no new material.
- **Steel slab/bloom/billet = cast.** Only molten steel (Bessemer / open-hearth) can be poured into a form, via
  the **longcell** sand casting (the continuous-casting shortcut — cast the form directly rather than rolling a
  big ingot down). One pour yields **1 slab / 2 blooms / 3 billets** (by form size). These are the steel line's
  rolling stock (see [smex.md](smex.md)).

**Definitions (textbook), and why the form is the product gate** — the form's cross-section pre-commits the
product family, so a roll set `accepts` only the matching form:

| Form | Section | Rolls into |
|---|---|---|
| **Bloom** | square, large | structural shapes, rails (and → billets) |
| **Billet** | square, small | bars, rods, wire-rod |
| **Slab** | flat, width ≥ 2× thickness | plate, sheet, strip, **skelp** → rolled pipe |

**Rolling stock ≠ cast components.** A `slab` is *steel stock* to be rolled; a **`castframe`** is a *cast-iron
structural member* — the I-section machine standard that carries the flywheel, rolling-mill and steam-hammer
frames (poured, **never rolled**; cast iron's compression strength is exactly what a machine bed wants). The two
were briefly one shape and are now split. Cast components (castframe, bedplate, cylinder sleeve, cast pipe,
grate/door, valve body) come off the **sand-casting** stations as near-net parts feeding machine-build recipes —
they are not semi-finished forms.
