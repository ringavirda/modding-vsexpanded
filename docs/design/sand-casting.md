# Sand Casting — Cells, Patterns & Iron Molds

Molten metal arriving on a [molten canal](iwex.md) is poured into a **rammed sand mold** and comes out a
finished casting. This doc owns the sand-casting station family (the **casting cell** and **long cell**),
the **pattern** items that shape a mold, and the **iron mold** family that replaces the placeholder
ceramic tool molds. Shared rules and numbers live in [conventions.md](conventions.md); the
[pig bed](iwex.md) already ships and is the proven substrate this builds on.

> **How to read this doc.** This is the design baseline; much of it is now **built** (audited 2026-07-25).
> Ownership is split across [iwex](iwex.md) and [lpex](lpex.md), see § Ownership.

> **Implementation status (audited 2026-07-25).** The **iwex core is live**: the pattern-item contract
> (`MoldSpec` parse+validate, now also a load-time sweep — `PatternValidation` at `AssetsFinalize`), the
> 1×1 **casting cell** (state machine, ram/imprint/shake-out, launder puller, dynamic sand render, molten
> renderer — now orientation-correct), the per-instance `BEBehaviorMoltenCell.SetCapacity` framework hook,
> the **iron mold** family (`BlockCastMold`, direct output, reusable, heat-sink **glow** via `GetLightHsv`),
> the **clay heat gate** (pedestal shatters / crucible refuses), native mold handling (vanilla opt-in), the
> **heavy cast plate** item+pattern+shape+diagram, the **cast barrel** route, and the **ceramic-mold
> removal** (deleted + `CeramicMoldRemoval` migration). Quality: **misrun**, **short-pour scrap**, **pattern
> wear** (durability), and **sand attrition** (config, default off) are in.
> **Still to build:** Phase 4 consumers — the **puddling furnace** multiblock + fettling + furnace
> door/frame/grate blocks & recipes off the heavy plate; Phase 5 **lpex parts** — cylinder, frame casting,
> flywheel segments, axle, gearblanks (+ the **long cell** block, and the deleted `castframe` geometry to
> re-author); the **handbook** rewrite (`docs/smex/handbook/03-casting.html`); capacity-derived-from-shape;
> the metal-pattern tier; and the gate/riser **head** allowance (§ Open items defers it to Phase 5).

> **⚠ Corrections to the block above (re-audited 2026-07-26 against code — several claims were stale):**
> - **The diagram→pattern craft IS built**, contrary to the note further down: `PatternRecipeDefinitions` ships
>   one grid recipe per type (`diagram-item-{type}` + knife, both `.Tool()`, + **2× plank** — not a log). The
>   real creative gate is one level up: **the design table has no craft recipe**.
> - **Four patterns exist**, not the full catalogue: `heavyplate`, `moldplate`, `molddoubleingot`, `castbarrel`
>   (× 12 woods). The **iron mold family has only 2 tooltypes** (`plate`, `doubleingot`).
> - **The heavy cast plate has no shape** — it still uses `game:item/plate`; the authored `item-heavyplate.json`
>   was never converted. Roughly **19 authored shapes remain unexported**, including the *entire* long-cell family.
> - **`/exmod molds` is gone, not repurposed** — the command was deleted outright; the vanilla-mold enhancement
>   is now a plain config field `IwexConfig.EnhanceVanillaMolds` (default false).
> - **lpex ships nothing** for casting — no patterns, no parts, no shapes. § Ownership's cross-mod split is
>   still entirely aspirational.
> - **`CeramicMoldRemoval` has no test** — the old `MoldRemovalTests` was deleted with the feature and never
>   replaced, so the migration is unverified.
> - The `fillingearblank…` → `fillinggearblank…` rename flagged below is **still unfixed**.
> - **FIXED 2026-07-26:** ramming up was **completely broken** — `Imprint` took `LastCodePart()`, which returns
>   the *wood* (`pattern-{type}-{wood}`), and `ResolveSpec` rebuilt `pattern-{type}`, a code that **no item has**.
>   Both paths always returned null, so every pattern failed with `badpattern`. The cell now reads the spec off
>   the held stack and persists the **full item code** — which also fixes the domain-hardcoding that made the
>   cross-mod contract impossible. Pinned by `PatternCodeLayoutTests`.

---

## Why

- **The ceramic tool molds were placeholders.** `smex:toolmold-{plate,doubleingot,quadrod}` shortcut the
  early plate/rod progression, needed a config gate to defuse (`/exmod molds`), and needed a Harmony
  patch to make cast iron come out of them at all. They are being **removed**, not kept alongside.
- **Iron molds are the correct answer.** Clay fails above cast iron; ingot molds were cast iron by
  definition. Iron molds are what makes molten **iron and steel** castable, and they are a prerequisite
  for **crucible steel**. Making them the sand cell's first product gives casting its own bootstrap.
- **Casting is the front half of a chain that already exists.** The rough cylinder from a sand cell is
  the boring machine's input ([diagram-crafting.md](diagram-crafting.md) § Boring machine). Cast rough,
  machine to spec — historically honest and mechanically load-bearing.

---

## Terminology

The trade has precise words and using them costs nothing. Corrections to the working vocabulary:

| Working term | Correct term | Note |
|---|---|---|
| form | **pattern** | The wooden positive a mold is rammed around. Made by a *patternmaker*. |
| imprint *(verb)* | **ram up**, **mold** | You ram sand around the pattern, then withdraw it. |
| imprint *(noun)* | **impression**, **mold cavity** | |
| pull it out | **shake out** | Breaking the mold to free the casting. Cleaning the gates off afterwards is **fettling**. |
| — | **flask**, **drag**, **cope** | The box and its halves. We model **drag only** — see § Historical liberties. |
| — | **sprue**, **runner**, **ingate**, **riser** | Downgate, distribution channel, cavity entry, shrinkage feeder. The launder stands in for all four. |
| — | **core**, **core print** | The sand body forming an internal cavity, and the seat that holds it. The cylinder mold has both. |
| cast iron mold | **iron mold** *(family)*, **chill mold** *(flavour)* | "Chill" specifically means casting against metal to harden the skin. |
| casting cell | **molding box** *(flavour)* | "Casting cell" is fine as the block name. |
| sand casting bed | **pig bed** | Already correct. Its centre runner is the **sow**, the side molds the **pigs**. |

**On "slab" — rename it, but not because it is wrong.** *Slab* is genuinely ambiguous rather than
incorrect: in **steelmaking** it is a rolled semi-finished product (wide-and-thin stock destined for
plate, with *bloom* and *billet* the squarer grades), while in the **machine shop** a plain flat
rectangular casting is quite reasonably called a slab. The problem is that this suite has *both*
contexts. [smex.md § Steam forming shop](smex.md) already spends **billet** on the rolling mill's input
stock (2400 u / 800 u) and produces plate, bar and rod from it — so **slab and billet are both taken**,
and a rolling mill needs its own real slab. Free the words.

The 8 × 4 × 24 piece is used for the boring machine's side supports, the base its top slides on, and the
same parts on the steam hammer. Those have exact names — the **bed** (the base carrying the machined
**ways**) and the **standards** or **housings** (the vertical side frames) — and their collective term is
the machine's **frame**. So: **frame casting**. *(Alternative: **bed casting**, if it leans toward bases.
Either beats a word the rolling mill will want back.)*

**On "boilerplate" — it is really a machining blank.** Real boiler plate was **wrought iron, rolled or
hammered**; a cast plate is not boiler plate, and cast-iron pressure vessels are the thing that killed
people. Early low-pressure boilers *did* use cast-iron **end plates**, so that use is defensible for the
cast-iron tier — but its main job is to be machined into specific parts, which makes it generic stock,
not a boiler component. Call it a **heavy cast plate** (`castplate-heavy`, distinct from the thin
`iwex:metalplate-castiron` the mold pedestal casts), and keep the gate that already exists:
[hpex.md § Steel grade gates](hpex.md) requires open-hearth, wrought, or hadfield for pressure-critical
parts — **a cast plate must never satisfy a Lancashire build.**

---

## The player loop

```
place cell  →  ram sand (RMB sand block)  →  ram up (RMB pattern)  →  pour from canal
            →  wait for it to freeze  →  shake out (RMB empty hand)
            →  casting + gate scrap + sand block back;  cell drops to half sand
            →  ram sand  →  ram up  →  …
```

Three clicks and a wait per casting. The sand comes back, so the loop is sand-neutral; what it costs is
the **labour of ramming up**, which is the point.

### The impression is destroyed. The sand is not.

This is the settled answer to "should molds persist between castings", and it is not close:

1. **Shake-out is what green sand *is*.** The mold is broken to free the casting — that is the defining
   property of the process, not a detail.
2. **It is the only thing that justifies the product.** Reusability is precisely what separates a
   permanent (chill) mold from a sand mold. If sand molds persisted, the iron mold the cell exists to
   produce would have no reason to exist and the progression would invert.
3. It rate-limits the cell without a timer.
4. It makes sand a live consumable loop instead of a one-time build cost.

But foundries **reconditioned and reused the same sand for years**, replacing a few percent. So the sand
returns: one block out on shake-out, one block in to re-ram, in the **same `{sand}` variant** that was
rammed so the loop cannot launder rock types.

The throughput objection is real — a furnace tapping at 135 u/s would swamp hand-tended cells — but it
points at the **pig bed** (already six molds in parallel) and at building more cells, not at reusable
sand. The cell's products are **capital goods**: you cast a handful, not a stream.

---

## Architecture — the pattern carries the mold spec

**The single most important decision in this doc.** A pattern item declares, in its item attributes,
everything the cell needs to know:

```jsonc
mold: {
  size:     "cell",                              // or "longcell"
  shape:    "lpex:casting/cell-filling-cylinder", // the rammed-sand mesh
  capacity: 297,                                  // units, = cavity volume
  cavity:   [ { x1: 4, y1: 4, z1: 4, x2: 12, y2: 14, z2: 12 } ],  // MoltenRenderer boxes
  output:   { type: "item", code: "lpex:cylinder-rough", quantity: 1 },
  minPourTemp: 1150
}
```

The cell reads this off the held stack. It never names a mod, a shape or a product in code.

This buys three things at once:

- **Cross-mod without dependency inversion.** iwex ships the station; lpex ships the cylinder and
  frame-casting patterns *and their shapes*, in lpex's own domain. iwex never references lpex.
- **Adding a castable part is data only** — a pattern def, a shape, an output item. No new block, no BE
  change, no new state.
- It is the **same per-mod discovery shape** the diagram catalogue already uses, so the two systems read
  alike.

iwex defines the contract and validates it; a malformed `mold` block is a load-time error, not a
mystery at the anvil.

---

## Blocks

### Casting cell — 1×1, iwex

A brick box, visually empty when placed. Interior cavity **x2‑14, y2‑14, z2‑14** = 12³ = **1728** sand
voxels. The pour **launder** sits on the low (+Z model-space) wall at `[6,14,10]→[10,15,16]` — geometry
identical to the pig bed's tap head, so the `{brick}`/`{sand}` variant machinery transfers unchanged.

**Not a network node — a puller.** [`MoltenNetwork.FlowEdge`](../../src/ExpandedLib/Networks/MoltenNetwork.cs)
equalises absolute amounts and has **no** "drain fittings never give back" rule; that rule lives only in
the pig bed's local copy. A cell joined to the graph would fill to the canal's level and then drain back
out. So copy the bed's `PullFromNeighbours`, **restricted to the launder face** so the model does not lie
about where metal enters. The cell hosts a `BEBehaviorMoltenCell` with `drainFitting: true`.

### Long cell — 1×2, iwex

`BlockFilledMegastructure` with one filler at (0,0,1); the body extends −Z in model space, same +180
convention as the bed. Interior 12 × 12 × 28 = **4032**. The whole thing is **one casting**, so the
second cell is a filler for collision and interaction only — it hosts no molten cell of its own.

### Persisted state is tiny

```
SandState { Empty, Full, Half }   +   string? PatternCode
```

Everything else derives — metal amount, temperature and hardness all come from the hosted
`BEBehaviorMoltenCell`.

Interaction routing, in order:

| Held | Precondition | Effect |
|---|---|---|
| sand block | `SandState != Full`, no metal | consume 1 → `Full` |
| `pattern-*` | `Full`, no impression, no metal | set impression, set capacity, damage pattern |
| empty hand | `cell.IsHardened` | hand over casting + gate scrap, `ClearContents()`, drop 1 sand, → `Half` |
| empty hand | metal present, not hardened | error (reuse `iwex-castingbed-toohot`) |

Guard: never re-ram or re-pattern while metal is present.

---

## The mold catalogue

Cavity volumes are **voxel-exact** — for every shape, `sand + cavity == base`. Capacity should be
**derived from the shape at load**, not hard-coded, so the model *is* the spec and a unit test can pin it.

| Pattern | Cavity | Station | Casting reads as | Owner |
|---|---|---|---|---|
| `plate` | 136 | cell | lid + 1-deep rim → flipped, a tray with an 8×8 recess | iwex |
| `doubleingot` | 152 | cell | same tray, two 8×3 pockets split by a rib | iwex |
| `cylinder` | **297** | cell | 8×8 outer / 6×6 bore annulus, 9.5 tall, around a **core**; solid head on top | lpex |
| `heavyplate` *(was "boilerplate")* | 160 | cell | 2 × 8 × 10 plate cast **on edge** | **iwex** |
| `castframe` *(was "slab")* | 768 | longcell | 8 × 4 × 24 machine frame | lpex |
| `castbarrel` | 406 | cell | **cored** hollow barrel (sand core → open-top vessel) | iwex |

### MP part blanks *(cast rough, cut on the boring machine)*

The cast-iron mechanical-power parts are **not** cast to their final shape. Sand casts a plain **blank**;
the boring machine cuts the teeth. This is the same *cast rough, machine to spec* chain as the cylinder,
and it is the historical precision route — coarse mill gears were cast to shape, but any gear that had to
mesh cleanly was **cut from a blank** on a gear-cutting machine (the boring mill's close cousin). A plain
disc is exactly what a gear blank *is*, so "representative" here is not a compromise — it is literally
correct. The `gear12` / `gear24` / `gearbevel` diagrams are therefore **boring-machine cutting schematics**
(they pick which gear you cut), not mold patterns; the mold patterns are just blanks by size.

| Pattern | Cavity | Station | Casts | → machined into | Owner |
|---|---|---|---|---|---|
| `axle` | 318 | cell | 4 **vertical octagonal rods** | *(nothing — a rod is finished)* | lpex |
| `gearblank-small` | 235 | cell | octagonal disc blank(s) | 12-tooth pinion | lpex |
| `gearblank-large` | 255 | cell | large octagonal disc | 24-tooth spur / large gear **and** the bevel | lpex |

**One large blank serves both the spur and the bevel** — the boring machine cuts the flat teeth for a spur
or turns the cone and cuts mitre teeth for a bevel, so a single disc blank covers both. The axle is the
only one cast to final shape: a plain octagonal rod, cast **vertical** (standing in the sand, cross-section
matching the shaft), four to a cell. Shapes:
`sandcasting-cell-filling{axle,earblanksmall,earblanklarge}.json`, cavities voxel-exact against the 12³
interior. **⚠ Rename the two blank shapes** `fillingearblank…` → `fillinggearblank…` — the diagrams and
pattern names use `gearblank` (with the `g`); the shape files dropped it, and the pattern's `shape`
reference must match exactly.

There is **no rod mold** for *stock* rod. The ceramic `quadrod` let players duplicate iron — the reason `/exmod molds`
existed at all — and it is being removed with no replacement, which costs nothing: [smex.md § Rollers](smex.md)
already produces *heavy bar / rod / thin rod / wire rod* from grooved rolls. Rod is a **rolled** product,
and rolling it from **wrought iron** is the historically correct route besides — a cast-iron rod snaps.
Henry Cort's grooved rolls (1783) are contemporaneous with his puddling furnace, which is exactly the
pairing the mod already models.

The plate and double-ingot molds are cast **cavity-side down** against a raised sand pad. That is correct
practice — the drag face takes the good finish and dross floats to the free surface, which is what you
want on a mold's working face.

The **cylinder is the best piece in the set**: a vertical green-sand mold with a central core is exactly
how a Wilkinson-era cylinder blank was made, and it feeds straight into the boring mill the design
already plans. Its core stops at y 13.5 with a 2×2 print to 13.75, leaving the top half-layer continuous
so the launder actually feeds the annulus, and giving the casting a head the boring mill removes.

---

## Mold tiers — and the ceramic heat gate

Introducing iron molds makes molds a **tiered** family, so the ceramic tier needs a ceiling. It gets one
the honest way: **clay molds shatter when you pour something too hot into them.**

| Mold tier | Material | Takes | Fails how |
|---|---|---|---|
| **Ceramic** | fired clay (vanilla + `game:toolmold-*`) | up to **bronze / copper** | pour anything hotter → **shatters instantly**, metal spills |
| **Iron** | cast iron, sand-cast in the cell | **cast iron, wrought iron, steel** | wears out over many heats *(see § Quality)* |

**Threshold ≈ 1100 °C**, which is exactly the right line and needs no special-casing: every vanilla
casting metal sits below it (tin bronze 950, black bronze 1000, brass 920, silver 961, gold 1063, copper
1084), and everything the suite adds sits above (cast iron ~1150+, wrought iron and steel ~1500). So
**vanilla progression is untouched** — a vanilla player never meets this rule — and it bites exactly when
a player tries to shortcut the iron mold. Config-tunable, iwex-owned.

This also **retires a hack**: `iwex/Patches/ToolMoldCastDomainPatch.cs` exists solely to make cast iron
come *out* of a clay mold. Once clay refuses iron, the patch has no purpose and is deleted with the
ceramic tool molds (see § Removing the ceramic tool molds). One less Harmony patch on vanilla.

**Implementation (built).** One pure decision — `BlockNetworkMolten/Blocks/ClayHeatGate.WouldShatter(mold,
pourTemp)`: true for a *small* fired-clay tool mold (`MoldKinds.FitsPedestal`) poured above
`IwexValues.ClayMoldHeatCeiling` (1100 °C, config-tunable). Large anvil / helve-hammer molds are cast at
iron temperatures in the canal tap, so they are **exempt** (`FitsPedestal` excludes them); our own cast-iron
molds are a different block class and never satisfy the predicate. Two pour paths share the decision:

- **Mold pedestal — shatters.** This is where the loophole actually lives: the pedestal fills its mold by
  *draining the run directly*, bypassing `CanReceive`, so without a gate a clay mold on an iron run would
  silently trap the cast forever (`game:metalplate-castiron` has no product). On an over-ceiling drain the
  mold cracks apart — destroyed (no drop), a crack of sound, and an `iwex-clayshatter` error to nearby
  players. The metal it would have taken stays in the run, with nowhere to go until an iron mold is placed.
- **Vanilla crucible pour — refuses.** `Patches/ToolMoldHeatGatePatch` prefixes `BlockEntityToolMold.CanReceive`
  to return false above the ceiling. It **refuses** rather than shatters: a hand-pour is deliberate, and
  destroying the mold on a mis-click is a worse gotcha than a refusal. In practice this is a defensive guard
  — no metal a crucible can pour is both over the ceiling *and* has a clay-mold product — but it satisfies
  "apply to the vanilla molds" and future-proofs a config that gives clay an iron-family drop. It replaces
  the retired `ToolMoldCastDomainPatch`, which hooked the same area to do the opposite (cast iron *out* of
  clay).

> **Make it predictable, not a gotcha.** The mold's block info and handbook page must state the limit
> *before* a player finds it. Shattering is memorable and historically true; shattering with no warning
> is a bug report.

---

## What iwex needs first

The minimum coherent scope, and it closes on itself:

```
blast furnace → molten cast iron ─┬→ sand cell + plate/doubleingot patterns → IRON MOLDS
                                  │                                             ↓
                                  │        thin cast plates + ingots  ←  mold pedestal (live)
                                  │                    ↓
                                  │            vanilla recipes (lanterns, &c.)
                                  │
                                  └→ sand cell + castplate-heavy pattern → HEAVY CAST PLATE
                                                       ↓
                                     puddling hearth · furnace doors & frames · machining stock
```

**Two different plates, and keeping them distinct is the point.** The **thin** plate
(`iwex:metalplate-castiron`) comes off the mold pedestal through an iron mold and exists to feed
**vanilla recipes** — lanterns and the rest. The **heavy cast plate** comes straight out of the sand cell
and is *structural*: the puddling furnace hearth, furnace doors and frames, and blank stock for the
boring machine. Same metal, different product, different station.

The iron molds are placed and filled from the **mold pedestal**, which already ships. Nothing else in
iwex needs to change for casting to be useful.

### The puddling furnace hearth — why plates matter

Your reading of the diagram is right, and it is specifically the **good** puddling furnace. Cort's
original 1784 **dry puddling** used a **sand bottom**: silica is acidic, it reacted with the charge, and
yields were poor. The fix was **wet puddling** (or "pig boiling", Joseph Hall ~1830), which lined the
hearth with **iron oxide** — roasted tap cinder, "bull dog", hammer scale, mill scale. That oxide is not
inert lining; it is the **reagent** that decarburises the iron, which is what the "boil" is. And that
oxide lining was carried on **cast iron bottom plates**, water-cooled in the developed furnace, because
the hearth had to stay below its own melting point.

The lining material is called the **fettling** — the same word as cleaning castings, in its other sense;
"to fettle a furnace" is to reline it.

So [iwex.md](iwex.md)'s puddling furnace should gain:

- **cast iron plates as a build material** for the hearth bottom — the plate mold's reason to exist;
- its "3 crushed-ore oxide beds" renamed to **fettling**, with **roasted slag** admissible alongside
  crushed ore. iwex already produces slag as a blast-furnace byproduct, so this closes a waste loop and
  makes relining a recurring beat rather than a one-time build.

---

## Downstream: machining cast blanks

**Casting a generic heavy blank and machining it to the specific part is exactly 19th-century practice** —
it is the entire reason the machine-tool industry existed, and the mod already commits to it
([diagram-crafting.md](diagram-crafting.md): "cast rough, machine to spec"). So yes to the premise.

One correction on the machine. A **boring mill** bores *cylindrical* work — Wilkinson's 1774 mill boring
Watt's cylinders is the famous case, and that is the right home for the cylinder blank and pipe bores.
Flat work — beds, **ways**, standards, plates — was machined on a **planing machine** (Roberts 1817,
later Whitworth) or, for smaller pieces, a **shaping machine** (Nasmyth ~1836); slots and keyways on a
**slotting machine**.

**Decision: the boring machine is universal.** It takes round and flat work alike, and the name stays
narrow. This is right — a second station that is a boring machine with a different mesh would be
duplication, not diversity, and the schematic system already carries part identity so nothing structural
is gained by splitting. A genuine **planer** can arrive later if flat work ever wants mechanics of its
own (workpiece traverse, ways to wear); until then there is nothing for it to do that the borer cannot.

The *practice* is sound either way; only the machine's name is doing work it historically would not.

---

## Candidate castings

Beyond the parts already spoken for, things genuinely sand-cast in this period, ranked by how well they
fit what the suite already has:

| Candidate | Verdict |
|---|---|
| **Flywheel segments** | **Adopt (lpex).** Real flywheels were cast in segments and bolted at the rim; the Watt engine and MP generator already have one to justify them. |
| **Grate bars, furnace doors and frames** | **Adopt — as recipes off the heavy cast plate**, not as their own patterns. See below. |
| **Pots, kettles, cauldrons** | **Defer to the homestead mod.** Darby's 1707 sand-cast pots are the founding act of the coke-iron industry, so the *flavour* is impeccable — but domestic ironware is not what iwex is about. It belongs with the planned stove. |
| **Firebacks / stove plates** | **Defer to the homestead mod**, likely as part of the multiblock stove. |
| **Swage block** | **Drop.** I recommended it for authenticity and it does not earn a slot: forming in this suite is anvil voxels plus the rolling mill's dies, and a swage block would duplicate the dies with no mechanic of its own. |
| **Anvil** | **Drop.** Real anvils were **wrought iron with a forge-welded steel face** — a *cast iron* anvil is the cheap, bad one, so it can only ever be an inferior tier. *(If it ever comes back: **Fisher-Norris, 1843**, cast an iron body onto a steel face — a two-material recipe that is period-exact.)* |

### The molten barrel gets a cast route

The molten barrel (iwex, holds a heat / byproducts / waste metal) becomes a **two-route variant block**,
because players need a lot of them:

- **`moltenbarrel-bolted`** — the existing craft: `PHP,PCP,PNP` (plates + fireclay + nails). Fabricated.
- **`moltenbarrel-cast`** — sand-cast a **cored** `cast-barrel` blank (unlined), then **add a fireclay
  lining** to make it hold metal. Cheaper in bulk, and the lining step is the barrel's version of "finish
  the casting" — the same beat as boring a cylinder, but a vessel is lined, not machined.

Casting a hollow vessel around a sand core is exactly how cast-iron pots were made (Darby again), so this
is period-honest and gives the sand cell a high-volume consumable to produce. Cost: `BlockMoltenBarrel`
gains a construction/type variant + the two shapes, and one new craft (`cast-barrel` + fireclay → lined).

> **Built 2026-07-25.** `moltenbarrel` is now a `construction` variant block (`-bolted` / `-cast`, same
> `BlockEntityMoltenBarrel`), with `BarrelConstructionMigration` remapping old `moltenbarrel` worlds to
> `-bolted`. `cast-barrel` is a cast-iron blank item (200 u); a `castbarrel` pattern casts it in the cell,
> and the grid recipe `cast-barrel` + fire clay → `moltenbarrel-cast`. The `castbarrel` pattern is
> creative-only for now — **exactly like every other pattern** (heavyplate, moldplate, molddoubleingot):
> the *diagram → pattern* craft (diagram + knife + log) is unbuilt for the whole family, a separate
> pattern-crafting phase, not a barrel-specific gap.

### Doors, frames and grates come off the heavy cast plate

Yes — use the heavy plate rather than drawing a pattern per part. Doors and frames still need to exist as
**blocks**, but their *recipes* take heavy cast plate as stock, exactly as a foundry would cut and drill
plate rather than pattern a one-off. That keeps item count down, and it gives the heavy plate a **third**
job alongside the puddling hearth and machining stock — which is what makes it worth a pattern at all.

Only draw a dedicated pattern when a part needs a silhouette a plate cannot imply.

---

## Machine parts — cast, machined, or forged

There is a single 19th-century rule that decides almost every part, and it happens to be exactly the
constraint this suite wants:

> **Cast iron takes compression. Wrought iron takes tension. Anything that has to *fit* is machined
> after casting.**

Cast iron is strong in compression and rigid, and it **shatters** under shock or pull. Wrought iron is
tough and fibrous and survives reversal. So frames, cylinders, beds and flywheels are *cast*; rods,
straps, cranks and bolts are *forged*; and bores, faces and ways are *cut* afterwards.

The design payoff: this forces a player to run **both** branches — the casting line (blast furnace →
cupola → sand cell) *and* the wrought line (puddling → helve hammer). Neither branch alone builds an
engine, which is precisely the shape the mod already has.

| Route | Parts | Why |
|---|---|---|
| **Cast** (sand cell) | cylinder blank; **frame casting** → engine bed, standards, entablature; **flywheel segments**; beam; pump barrel; piston body; valve bodies; bearing housings / plummer blocks; gears; grate bars; stuffing-box glands; counterweights | rigid, compression-loaded, complex shapes cheaper to cast than to forge |
| **Cast → machined** (boring machine) | **cylinder bore**; cast pipe-parts; piston turned to its bore; valve seats and faces; **slideways** on the frame; bearing journals; bolt holes | anything whose *fit* matters — a bore that is not round leaks steam, which is the whole Wilkinson story |
| **Forged** (wrought iron, anvil / helve) | **piston rod**; connecting rod; **crank / crankshaft**; beam straps, gudgeons, parallel-motion links; **bolts, nuts, studs, rivets**; chains; levers and handles | tension and reversal — a cast piston rod snaps on the first stroke |
| **Other** | safety-valve **springs** (steel); the **injector** (brass — resists steam and water, and casts to fine cones); packing and gaskets (hemp / leather); bearing surfaces (bronze) | material chosen for a property neither iron has |

### This is a rule for deciding, not a shopping list

**The table above is how you pick what a recipe asks for. It is not a list of items to create.** The mod's
existing convention is already lean — the Lancashire's whole bill is *"4× hadfield plate + 6× rolled pipe
+ heavy cap + injector"*, four line items for a boiler — and casting must not break that.

**Rule: no new part item unless it is used by at least two machines, or it is a machine's signature.**
Everything else is stock material.

That collapses the whole tier to **six new items**, because the forged side needs *none* — vanilla already
ships `rod-{metal}` and `metalnailsandstrips-{metal}`:

| # | Item | Route | Used by |
|---|---|---|---|
| 1 | **heavy cast plate** | cast (iwex) | puddling hearth · furnace doors & frames · machining stock · valve bodies |
| 2 | **frame casting** | cast (lpex) | engine bed · boring machine standards · steam hammer standards |
| 3 | **cylinder blank** | cast (lpex) | → items 5 and 6 |
| 4 | **flywheel segment** | cast (lpex) | Watt engine · MP generator |
| 5 | **bored cylinder** | machined | Watt engine · pumps |
| 6 | **cast pipe-parts** | machined | the whole cast pipe tier *(already designed)* |
| — | `rod-iron` | forged — **vanilla** | piston rods · connecting rods · cranks · levers |
| — | `metalnailsandstrips-iron` | forged — **vanilla** | every bolted joint in the tier |

So a machine's bill is three to five lines, not twenty:

| Machine | Bill |
|---|---|
| Cornish boiler | heavy cast plate · cast pipe · nails & strips · brick |
| Watt engine | frame casting ×2 · bored cylinder · flywheel segments · rod ×2 · nails & strips |
| Boring machine | frame casting ×2 · heavy cast plate · rod · nails & strips |
| Engine / mechanical pump | bored cylinder · rod · nails & strips |
| Valves, condenser | heavy cast plate · rod |

Two things worth keeping from the long version, because they earn their place:

- **Bolts and rivets are wrought, and engines need hundreds.** A real sink for puddled iron that stops
  wrought being a pipes-only material — and it costs nothing, because the item already exists.
- **The flywheel is cast in segments and bolted at the rim**, so one part needs *both* routes. That makes
  it the tier's teaching recipe.

---

## Cast iron in the vanilla world

Surveyed against `assets/survival` (1.20). The headline result is better than expected:

> **Vanilla's entire mechanical-power network is wood.** Windmill rotor, axle, angled gears, brake,
> toggle, large gear + sections, clutch, transmission, helve-hammer base — every one is logs, planks,
> resin and fat. The *only* metal in the whole set is the Archimedes screw.

That is the single largest opening, and it is historically exact: replacing wooden millwork with
**cast-iron gearing** is the defining mill-engineering story of precisely this period (Smeaton onward,
then Fairbairn's mill work). It is also already half-committed — lpex ships craftable gears today.

Ranked by value per unit of work:

| # | Opportunity | Why it is worth it | Shape cost |
|---|---|---|---|
| 1 | **Cast-iron mechanical power tier** — axle, angled gears, large gear + sections, toggle, brake, clutch, transmission | The big one. Wooden millwork → cast-iron gearing *is* the period, and it retires the wooden parts currently sitting in engines only for vanilla cohesion. | **none** — see below |
| 2 | **Add `castiron` to existing `allowedVariants`** — lantern, metal plaque, iron door, trapdoor, pulverizer pounder caps | Nearly free: a patch per recipe, no new blocks. **Pounder caps especially** — chilled cast iron was *the* material for stamp-mill shoes and dies, so it is not a substitution, it is the correct answer. | none |
| 3 | **Cast-iron chute sections** | Vanilla chutes are **copper only** — expensive, and copper is wanted elsewhere. Cheap cast-iron chutes are a genuine quality-of-life win. | none — **the chute is the cylinder blank**, same hollow section |
| 4 | **Cast-iron railings and gates** | The most recognisable decorative product of the era. | **none** — see the correction below |
| — | Troughs, bedsteads, stoves, pots, firebacks | Real, and all **homestead mod** — noted so they are not re-derived later. | — |
| — | Anvil, quern, anchor | **No.** Anvils are wrought + steel face, querns are stone, anchors are forged. Leave them. | — |

> **Correction to an earlier claim in this doc's history:** I wrote that vanilla has *no metal fence*. That
> was drawn from the grid-recipe list and is wrong. **`blocktypes/metal/ironfence.json` exists** —
> `BlockFence`, the full 20-state connection variant set, shapes at `block/metal/fence/{base,top}/*` — it
> is simply marked `handbook: exclude` and has no craft recipe surfaced. A cast-iron railing is therefore
> a **retexture plus a recipe**, with the connection logic and geometry free. Gratings likewise have
> `shapes/block/clay/grating.json` to work from.

### The mechanical power tier has no shape problem

Two findings, both checked against `assets/survival`:

**1. Do not retexture the wooden shapes — vanilla already ships cast-iron gear art.**

Retexturing `axle.json` (2 elements) or `largegear3.json` (10) would read as *grey planks*, not a new tier,
because a wooden gear and an iron gear are genuinely different objects: wood is a solid or built-up disc
with chunky peg teeth on a square timber shaft; iron is a **spoked wheel** — hub, arms, rim — with fine
teeth on a round shaft.

Vanilla's **devastation / Jonas machinery** set is exactly that, already built:

| Shape | Construction | Texture |
|---|---|---|
| `block/devastation/machinery/gear12` | 22 elements — **6 spokes** at 30° steps, platter + hub, **12 teeth** at 30° steps | `block/metal/sheet/iron1` |
| `…/gear24`, `…/gear36` | same idiom at 24 and 36 teeth (44 / 70 elements) | iron sheet |
| `…/gear1290` | **two gears meshed at 90°** — a crown-wheel-and-pinion pair | rusty iron |
| `…/gearbox1`, `…/geartrain3`, `machine/jonas/steamengine/gearjunction` | housings and trains | iron sheet |
| `…/gearhugemetal9`, `…/gearhugemetal15` | 226 / 351-element showpieces | machinic |

`gear1290` is the arrangement in the reference image — a **bevel pair** (mill term: *bevel* or *mitre*
gears; the automotive name is crown wheel and pinion). That is what an angled gear should look like in iron,
and it is already drawn.

These live under `game:`, so a blocktype can reference them **by path without copying** — e.g.
`shape: { base: "game:block/devastation/machinery/gear24" }` — with a single `metal` texture key to repoint
at cast iron. First pass costs nothing.

**Generating new ones is tractable too.** The construction is a pure **polar array**: teeth at 360/N°,
spokes at 360/M°, every element a cuboid with an explicit `rotationOrigin`. That is exact trigonometry, not
freehand modelling, so a gear family can be *generated* to any tooth count in vanilla's own idiom — which
is what keeps it sitting correctly next to vanilla art. What generation cannot do is art-direct: choose the
tooth count that reads best at block scale, or judge the final silhouette. Generate, then adjust in Model
Creator.

> **Worth stealing: the mortise wheel.** A cast-iron wheel with *wooden* cogs pegged into the rim — mills
> used them to run quietly and to make the wooden tooth the sacrificial part. It is the historically perfect
> **transition** piece between the wood and iron tiers, and visually it is the iron gear with wood-textured
> teeth.

**2. There is nothing to synchronise, because vanilla MP is not animated at all.** Every one of those
shapes has **zero `animations` entries**. Rotation is done in code: `blocktypes/mechanics/axle.json` is
`class: BlockAxle`, `entityClass: Generic`, `entityBehaviors: [{ name: "MPAxle" }]`. The engine's MP
behaviour owns the angle and spins the mesh, and it does so identically whatever the mesh looks like. Reuse
`BlockAxle` + `MPAxle` and rotation, network propagation and phase all come for free — the same way they
already work for wood.

So the worry that this needs custom shapes and hand-synced animation does not apply. What it *does* need is
the tiering rule, and that maps 1:1 onto a pattern this codebase has already shipped:

| Pipes | Mechanical power |
|---|---|
| `BlockPipe.RegisterBurst(domain, pressure)` | `RegisterMaxTorque(domain, torque)` |
| the weakest segment caps the run's pressure | the weakest part caps the network's torque |
| over-pressure → **burst** | over-torque → **break** |
| joint families keep tiers apart | **no joint rule** — wood and iron interconnect |

Wood and iron should interconnect freely: mills genuinely mixed them through the transition, and forcing
separation would be irritating with no historical payoff. The weakest part setting the ceiling is enough
to make upgrading worthwhile on its own.

**This has outgrown a casting doc.** It should graduate to its own design doc when it is scheduled; what is
recorded here is the feasibility finding, so the shape question does not get re-litigated.

---

## Malleable cast iron (ковкий чугун)

Worth adding, but **not as a forging route** — and the distinction matters, because the name misleads in
both languages.

**Malleable cast iron is a heat treatment, not a forging stock.** You cast the part in *white* iron, then
**anneal it for days** — packed in iron oxide (**whiteheart**, Réaumur 1722, European) or in neutral sand
(**blackheart**, Seth Boyden 1826, American) — which breaks the brittle carbide down into temper carbon.
The result is a casting that **bends instead of shattering**. It is machinable and can be cold-bent a
little. It is *not* hammered to shape: the shape is cast first. "Malleable" and "ковкий" both mean "does
not shatter", not "can be worked at the forge".

### So: no, cast iron should not become forgeable

And the codebase has already made this call correctly — `ItemPig` is deliberately a plain non-forgeable
`Item`, and `MetalFamilyEmitter` keeps cast iron out of the `block/metal` worldproperty *specifically* so
it never becomes anvil-forgeable. Do not undo that. If cast iron could be forged, the **puddling furnace
would have no reason to exist** — and puddling is the mod's only wrought-iron route and a whole planned
multiblock. The un-forgeability of cast iron is load-bearing.

### What to add instead — an annealing step

```
sand cell → white-iron casting → pack in a box with iron oxide → long bake → MALLEABLE IRON part
```

Three reasons it earns its place here rather than being flavour:

1. **Pipe fittings.** Malleable iron elbows, tees, unions and flanges are *the* canonical product of this
   process — still are today. The pipe network needs fittings, and this is where they should come from.
2. **It consumes the same oxide as the puddling fettling** — roasted slag / mill scale. One waste loop
   feeding two processes is the kind of economy the mod is built on.
3. **Timeline fits.** 1722 and 1826 both sit before Bessemer, so it is legitimately an iwex/lpex-era
   material rather than something borrowed from later.

**Good malleable-iron products:** pipe fittings; hinges, straps and brackets (including the furnace-door
hardware above); chain links; valve handwheels, levers and linkage forks; wrenches.

**Explicitly not:** piston rods, crankshafts, boiler shells. Those stay wrought or steel — the
compression/tension rule does not bend for a better casting.

**Cost to build:** a new metal in `MetalRegistry`, an annealing box or oven block (iwex — it is an
ironworking process), and a long timed bake. Real but bounded. Flagged as *optional* — the casting system
is complete without it.

---

## Ownership

| Thing | Mod | Why |
|---|---|---|
| casting cell, long cell, `fillingbase` / `fillinghalf` | **iwex** | stations, next to the pig bed |
| pattern-item contract + validation | **iwex** | lowest mod that casts |
| **iron mold** family + its patterns | **iwex** | a material-tier product everything above needs, incl. crucible steel |
| **heavy cast plate** pattern, shape, output | **iwex** | iwex itself consumes it — puddling hearth, furnace doors and frames — so it cannot live higher |
| cylinder / frame-casting patterns, shapes, outputs | **lpex** | lpex consumes them; iwex must not reference lpex |

---

## The iron mold is its own class *(decided 2026-07-25)*

The iron molds are **not** a `BlockToolMold` subclass. Metal-pouring is recognised by the
`ILiquidMetalSink` **interface**, not the class (the molten barrel proves it — crucibles and the pedestal
pour into it with no subclassing), so owning the class costs nothing in interop and sheds all of vanilla's
clay baggage (shatter, shattered-shape, beehive firing, fired-vs-raw, `TemperatureSensitive`).

`BlockCastMold` + `BlockEntityCastMold : BlockEntity, ILiquidMetalSink` — a sibling of the molten barrel
(both cast-iron vessels that receive metal, glow, harden, and yield a cast). It:

- **Outputs the cast directly** (`iwex:metalplate-castiron` for cast iron, `game:metalplate-{metal}` /
  `game:ingot-{metal}` for the rest) — so **`ToolMoldCastDomainPatch` is retired**: with the output owned,
  there is nothing to rehome. (This is the correct way to reach the design's "remove the patch" goal.)
- **Never shatters** and is **reusable** — a durable capital good, unlike the clay mold.
- **Is a heat sink that glows.** Pouring hot metal raises the *mold body's own* temperature (not just the
  cast's); the mold emits block light off that, then fades as it cools — the same `GetLightHsv` +
  `MarkBlockDirty`-on-glow-change idiom as the barrel, but driven by the mold body, so an iron mold sits
  glowing after a pour even as the casting inside sets.

The clay **heat gate** (vanilla `game:toolmold` shatters when poured above ~1100 °C, i.e. bronze max) is a
**separate** small patch on the vanilla clay mold — unrelated to our class.

**Mold-handling behaviours belong to our mold, vanilla is opt-in *(decided 2026-07-25)*.** The niceties
smex bolted onto vanilla molds via Harmony — spill molten metal when a filled mold is moved, burn the hand
that holds a hot one, render the metal surface on the held mold, pick the mold up with its contents,
clean pedestal-retrieval — should be **native behaviours of `BlockEntityCastMold`**, not patches. The
vanilla `BlockToolMold` patching that provided them becomes **optional, behind a command toggle** (the
`/exmod molds` command is repurposed from the retired enable/disable gating to "apply the enhanced mold
handling to vanilla clay molds too — on/off"). Default off: our tier stands on its own molds, and a server
that still wants the treatment on vanilla molds opts in.

> The shared cast-iron-receptacle base (barrel + cast mold: receive / glow / harden / extract / chisel) is
> worth lifting into a `BlockEntityMoltenReceptacle`, the same move that produced `BEBehaviorMoltenCell` —
> done opportunistically, not as a prerequisite.

## Charge piles are their own class too *(decided 2026-07-25, deferred)*

The blast-furnace charge currently **is** a literal `game:coalpile` (`GetBlock("game","coalpile") →
SetBlock`, detected by `Code.Path.StartsWith("coalpile")`), and `ItemBlastmix : ItemPileable`. So iron
burden inherits coal semantics — ignitable, a fuel, coal-textured, merge-with-coal — a real leak, plus the
fragile string-matches. The fix is a **generic exlib pile substrate** (burden / coke / ore / blastmix all
use it), mirroring `BEBehaviorMoltenCell`. It is costlier than the mold (layered ground storage, count
rendering, grid interaction) and the current reuse works, so it is **sequenced to the next charge/fuel or
coke/ore-pile work**, fixing the `coalpile` string-matches then.

## Removing the ceramic tool molds

A deliberate deletion, not a deprecation. Touches:

- `smex/Molds/ToolMoldDefinitions.cs`, `MoldGating.cs`, `Patches/ToolMoldPatches.cs`,
  `Recipes/ClayForming/ToolMoldRecipeDefinitions.cs`
- `iwex/Patches/ToolMoldCastDomainPatch.cs` — the cast-iron-in-a-clay-mold hack exists only to serve
  these molds and goes with them
- `/exmod molds` and the three `SmexConfig` flags → **config version bump** (version-reset migration)
- **Block migration** for placed `smex:toolmold-*`. `ToolMoldDomainMigration` is the template. Drop the
  build materials rather than silently upgrading — the iron mold is a much more expensive item and a free
  upgrade would read as a bug.

- **`docs/smex/handbook/03-casting.html`** describes the ceramic molds and must be rewritten against the
  new flow. The handbook↔lang sync joins on the `NN-` prefix and the drift test will fail otherwise; the
  page arguably moves to iwex with the stations.

Vanilla `game:toolmold-*` is untouched; only the three mod-added tooltypes go. The smex blocktype and
clay-forming goldens **shrinking** is the proof the removal is clean.

---

## Quality mechanics *(recommended, cheap)*

These use state the cell already has and give casting failure modes worth caring about:

- **Misrun.** If the cavity finishes filling below `minPourTemp`, the casting comes out **defective** —
  scrap for remelt, not the part. Free tension on canal length, and it connects to a system that already
  exists (metal cools per cell along a canal).
- **Short pour → scrap.** Shaking out an under-filled cavity yields metal scrap, mass-conserving, mirroring
  the pig bed's `Denominate`.
- **Gate and riser scrap.** Shake-out returns the part **plus** a small runner-scrap item. That is
  *fettling*; it costs one extra drop, explains why casting is lossy, and pairs with a configurable ~10%
  over-capacity head so the numbers are honest.
- **Pattern wear.** Wooden patterns wore out. Durability on the pattern, and later a **metal pattern**
  tier for long runs — a real historical progression, free flavour.
- **Sand attrition.** A config chance the returned sand block is lost, so sand is not purely ceremonial.
  Default generous.

**Rejected:** tempering sand with water (fiddly, no payoff); a two-part cope-and-drag flask (a whole
second block state for no gameplay); a timed drying stage (a wait with no decision).

**Undecided — the separate core.** A cylinder core was made in a **core box** and set into the rammed
mold as its own step. Requiring a core item for the cylinder is the single most *educational* addition —
it is *why* a cylinder mold is harder than a plate mold — but it costs a click, an item and a recipe. See
§ Open items.

---

## Historical liberties (deliberate)

- **No cope.** Every mold here is open-top, drag only. That is genuinely correct for plates, frames and
  mold trays — **open-sand casting** was standard for exactly those. It is *not* right for the cylinder,
  which wants a cope and core prints. Left alone; worth one handbook sentence rather than a second block.
- **No sprue, runner or riser modelled.** The launder stands in for the whole feed system.
- **A cast-iron boiler plate** is period-correct for the cast-iron LP tier and wrong for anything above it
  — which is why the hpex gate matters (above).

---

## Material-driven textures *(requested 2026-07-25)*

The casting stations and patterns should show what they were made from:

- **Patterns carry a `wood` variant** — the texture follows the log they were carved from
  (`pattern-{type}-{wood}`), the wood captured from the log ingredient in the diagram+knife+log recipe
  (storeWildCard, the same idiom the bed uses for brick). Combines with the "cast-item shape in wood" art
  rule: the shape is the part, the texture is the chosen wood.
- **Casting cell + bed carry a `brick` variant** — the shell's colour follows the fired brick built from,
  captured in the craft/RCC recipe (the bed already does this; the cell needs the same brick variant +
  tint-overlay texture).
- **The sand inside is dynamic, not a variant** — the texture of the rammed sand follows the **last sand
  block the player added**, so it is a per-BE value applied in `OnTesselation` (remap the filling shape's
  `andesite` key to that sand's texture), not a static `{sand}` blocktype variant. **This changes the bed**,
  which currently bakes sand into a `{sand}` variant at construction — move it to the same dynamic path.

### Status *(2026-07-25)*

- **Cell — DONE.** The 1×1 cell now carries the canal's 8-state `brick` variant (fire first = default so
  the original look survives) + an explicit `side` group + the fire1 tint-overlay; a craft recipe pair
  (coloured running-brick capturing `{brick}`, plus a fire-brick route) in `CastingRecipeDefinitions`,
  sharing the canal's `Fhk`/`RunningBrick`/`FireBrick` helpers (promoted to `RecipeIngredients`); a
  migration `iwex:sandcastingcell → iwex:sandcastingcell-fire-north`. The **dynamic rammed-sand render** is
  built: `BlockEntitySandCastingCell.OnTesselation` draws the state's filling shape
  (`CastingCellLogic.FillingShape`: Empty→none, Half→half, Full→base, Full+impression→pattern cavity) on
  top of the brick shell and remaps its `andesite` key per-BE to the last-rammed sand (a private
  `ITexPositionSource`). Pure state→shape logic is unit-tested; the visual is in-game-only.
- **Bed — DEFERRED as a coupled bundle** (dropping the static `{sand}` variant *and* the dynamic render
  must ship together, else bed sand regresses to a fixed texture with no replacement). Two hard blockers:
  1. **Shape split (user, Blockbench).** `SandRunners` already has six discrete per-cell subtrees, but each
     **fuses the cavity impression with that cell's base sand** — hiding a cell removes *all* its sand
     (a void to brick), not the "flat sand remains after shake-out" look the cell station uses. Needs each
     mold cell re-authored as a named `…Base` + `…Impression` pair (all still `#andesite`), runner sand
     kept as its own always-shown subtree.
  2. **Render path (in-game confirmation).** Unlike every sibling constructed mega-block (ore bunker,
     engine, boiler, converter, ore mixer), the bed has **no `Animatable` behavior and no
     `ConstructedAnimator`** — it only sets `.Shape` + `ShapeRotateYByType`. So it is unclear whether its
     solid brick+sand mesh renders via the default block mesh or not at all. This decides whether per-cell
     impression destruction is "add a per-BE `OnTesselation` with a dynamic `SelectiveElements` set
     (complete-construction ∩ not-shaken-out)" or "first give the bed a real render path, then that hook."

## Framework changes needed

- **`BEBehaviorMoltenCell` capacity setter.** `_capacity` is config-only
  ([BEBehaviorMoltenCell.cs](../../src/ExpandedLib/Blocks/Structures/BEBehaviorMoltenCell.cs)); the cell's
  capacity depends on which pattern is rammed up. This is the **only** framework change the whole system
  needs — everything else composes from what exists.

---

## Asset work

- Export the **twelve** shapes from `assets/editable/shapes/` to the owning domain
  (`iwex:casting/*`, `lpex:casting/*`). Geometry copies verbatim.
- **Domain the texture paths** — bare `block/...` → `game:block/...`, the known editable→shipped step
  (see the pipe-tier conversion).
- **Trap:** the filling shapes' only texture key is `andesite`, and the cell's *base* shape never
  references it. `TesselateShape` resolves codes against the **Block**, so the cell blocktype must declare
  `andesite` (→ `game:block/stone/sand/{sand}`) anyway or the rammed sand renders untextured.
- `diag-mold-{plate,doubleingot,cylinder,boilerplate,slab}` overlays exist and ship to the owning mod.
  The last two are drawn under the **old** names — rename the files to `castplate-heavy` / `castframe`
  with the parts, so texture, pattern and item all agree.

---

## Testing

- **Headless:** state-machine transitions (pure); capacity-derived-from-geometry (pins all six cavity
  volumes); pattern-attribute parsing and validation; `DefinitionAssets.MissingShapes` over the twelve new
  shapes; lang parity en/ru/uk; goldens **shrinking** on the ceramic-mold removal; the migration.
- **In-game only:** mesh swap on state change, `MoltenRenderer` cavity boxes (especially the cylinder's
  4-box annulus), N/S/E/W orientation, and that the puller reads the launder face.

---

## Phasing

1. **Contract** — pattern-item attribute schema + validation (iwex), `BEBehaviorMoltenCell` capacity
   setter (exlib). Testable with a stub pattern before any block exists.
2. **The cell** (iwex) — state machine, RMB routing, dynamic mesh, launder-face puller, molten renderer.
   Creative-only, one hard-coded test pattern.
3. **Iron molds + the ceramic heat gate** (iwex) — the mold family and its patterns, the **~1100 °C pour
   gate** that shatters clay molds, and the **removal** of the ceramic molds plus migration. Casting now
   bootstraps itself and the tier boundary is enforced.
4. **The heavy cast plate** (iwex) — pattern, shape, output; then the puddling hearth and the furnace
   door/frame recipes that consume it. Gives the plate its three jobs.
5. **lpex parts** — cylinder and frame-casting patterns, shapes, outputs; the **long cell**; flywheel
   segments. Closes the cast → machine chain.
6. **Quality mechanics** — misrun, short pour, gate scrap, pattern wear.
7. **Handbook + lang** — en/ru/uk, the open-sand / shake-out explanation, and the mold-tier limit stated
   *before* a player can shatter something.
8. **Cheap vanilla uses** — add `castiron` to the lantern / plaque / door / trapdoor / pounder-cap variant
   lists; cast-iron chute sections **off the cylinder blank**; cast-iron railings by retexturing vanilla's
   existing `ironfence`. Recipe and texture work only, no new systems or shapes — and it is what makes the
   foundry feel worth building. Can land beside 4.
9. **Malleable iron** *(optional)* — the annealing box and pipe fittings. Self-contained; any time after 4.
10. **Cast-iron mechanical power tier** *(own feature, own doc)* — axle, gears, toggle, brake, clutch,
    transmission, built on vanilla's **metal machinery** gear art (not retextured wooden shapes) with a
    torque ceiling. Shape and animation risk is **cleared** (see § The mechanical power tier has no shape
    problem); what remains is scoping the torque numbers, deciding which wooden parts the engines shed, and
    an art pass on which gears get generated versus referenced.

---

## Open items

- **Which mod owns the wrought-iron rolling mill.** [smex.md](smex.md) puts the rolling mill on the steam
  hammer's megablock, but Cort's grooved rolls (1783) are contemporaneous with **puddling**, not with
  steel — a wrought-iron mill historically belongs in the iwex/lpex era, and rod has to come from
  somewhere once `quadrod` is gone. Likely a **two-mill split**: an early wrought-iron mill low in the
  chain, and smex's heavy steam forming shop for billets and steel. Not a sand-casting decision, but this
  doc's rod answer depends on it.
- **iwex ships a long cell with no long pattern** (the only one is lpex's frame casting). Either accept it
  (everything is creative-only until its mod lands anyway) or give iwex one long pattern.
- **Separate core item for the cylinder** — build it or fold the core into the pattern. See § Quality.
- **Head allowance** — whether capacity is exactly the cavity or cavity + ~10%, and whether the surplus
  comes back as gate scrap. A number, set when Phase 5 lands.
- **Ceramic shatter threshold** — ~1100 °C is the argued line (above copper, below cast iron); the exact
  number and whether shards drop are config calls at Phase 3.
- **Whether malleable iron happens at all** — it costs a metal registration and an annealing block. See
  § Malleable cast iron.
- **Homestead hand-offs** — Darby pots/kettles, stove plates, firebacks and bedsteads are deferred to the
  planned homestead mod rather than dropped; they want a pointer once that doc exists.
- **MP torque numbers** — what wood's ceiling is, what iron's is, and whether breaking is destructive
  (like a pipe burst) or just a stall. The *mechanism* is settled; the values are not.
- **Which wooden parts the engines shed** once an iron tier exists — some are there purely for vanilla
  cohesion and can go; that is a per-machine call.
- **Whether cast-iron parts get their own silhouette** eventually — spoked gears and I-beam standards
  instead of retextured planks. Pure polish, and explicitly *not* a prerequisite.
