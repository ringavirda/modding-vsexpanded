# Diagram Crafting — Design Table & Boring Machine

Diagrams (drawn paper plans) carry the **identity** of a crafted thing as data, so one material recipe
serves a whole family of shape variants. This is the mod's answer to the 3×3-grid ceiling. Shared rules
and numbers live in [conventions.md](conventions.md); the mods that own the machines are
[iwex](iwex.md) (design table) and [lpex](lpex.md) (boring machine).

> **How to read this doc.** The baseline below is the design; **phases 1–3 are now partly built** (audited
> 2026-07-26 against code). Numbers appear once (see [conventions.md](conventions.md)); this file owns the
> diagram catalogue and the two station machines.

> **Implementation status (audited 2026-07-26).** **BUILT:** the `diagram-{type}` item — **iwex only**, 22
> variants (18 structure + 4 pattern-derived) on the exlib `diag-base` shape, whose `paper` path is now correctly
> domained; the **design table** (`BlockDesignTable` + `BlockEntityDesignTable.TryDraft` + `GuiDialogDesignTable`,
> with tests) — so Phase 3 has landed even though this doc said "nothing is live".
> **PARTIAL:** the design table is a **single cell, not the 2-wide megablock**, its candles are a static
> `lightHsv` with no particles, it has **no craft recipe** (this — not the patterns — is what actually gates the
> whole chain to creative), and no guide viewer. Its shape still has **5 undomained `block/…` texture paths** that
> will resolve against `iwex:` and fail; only `paper` was fixed.
> **Phase 2 was proven on the wrong family:** only 2 diagram grid recipes exist (`molten-straight`,
> `molten-bend`) — the **pipe family is untouched** and still uses its 4 hand-authored patterns, so the collision
> problem this doc exists to solve is unsolved. Counting the 4 diagram→pattern recipes, there are **6** diagram
> recipes repo-wide, and **every one uses `.Tool()`** — so the "structures consume the plan" half of Model A is
> entirely unproven; the `bfc`/`cf`/`sandbed` diagrams have items and textures but no recipe consuming them.
> **NOT BUILT:** the **boring machine** (nothing in lpex), the **exlib station-window base** and the **diagram
> catalogue** — `GuiDialogDesignTable` lives in iwex and enumerates `capi.World.Items` directly, so the shared
> spine in § The exlib framework does not exist. `diagramdesc-*` lang keys are **entirely missing** (the info
> panel shows raw keys for all 22). Textures: **45 drawn / 22 shipped**, 23 orphans.

---

## Why

- **The 3×3 grid is out of room.** Distinct shape variants (every pipe/canal junction, every furnace
  core, every mold) each need a distinct pattern, and they collide on overlapping ingredients — see the
  brick-wrapped-pipe disambiguation and the recorded recipe-conflict pain. We have been spending
  creativity on *telling patterns apart*, not on design.
- **A diagram is identity-as-data.** `straight-pipe-diagram + plate + nails → 2 straight pipes`;
  `bend-pipe-diagram + plate + nails → 2 bend pipes`. **One material recipe per family**, the diagram
  picks the variant. Dozens of colliding patterns collapse to a handful.
- **It is the drawing-office fantasy.** A candle-lit design table where you draft plans, and a
  boring mill that machines cast blanks to spec, are exactly the 19th-century idiom the suite is built
  around (see [overview.md](overview.md) pillars).

---

## The model (locked)

**Model A — the diagram is an ingredient, not a crafting engine.**

- The **design table produces diagrams and serves as the planning/reference terminal.** It does *not*
  craft pipes.
- **Assembly is ordinary crafting**: by hand in the vanilla 3×3 grid (diagram as an ingredient), or via
  the **multiblock system** (place a core, build it — unchanged). No new recipe engine for iwex; it is
  plain `ExRecipeDef` grid recipes.
- **Consumed-vs-reusable falls out of the ingredient flag.** A diagram used for a **simple block or
  item** is an `isTool` ingredient (`IngredientBuilder.Tool()`) — spent by durability, effectively
  reusable. A diagram used for a **multiblock / megablock structure core** is a **plain consumed
  ingredient**. That flag *is* the rule "structures consume the plan, parts don't" — for free, in the
  existing engine.
- The **boring machine (lpex)** is the one genuine processing station: insert part(s) + a schematic →
  machined output, in a window. Same idiom (inputs + diagram → result), but powered and timed rather
  than hand-crafted. Its schematic is an item output, so **reusable** (never consumed).
- **No progression gates.** Every diagram is freely draftable from the start; the only gate is
  materials. (Gating was explicitly rejected — the rusty-gear complaints on Homo Sapiens settings are
  the cautionary tale.)
- **No holograms.** Multiblock output is explained in **text**, never a ghost preview — projection
  previews stay the in-world Ctrl+Shift build-outline they already are.

### Relationship to R7 (No GUI)

[conventions.md](conventions.md) **R7** says all interaction is in-world and verb-based. These two
stations are the **sanctioned exception** the overview allows ("no GUI windows *unless unavoidable*"):
the interaction *is* "pick one plan from a catalogue of ~two dozen, and read what it builds" — which
block-info cannot express. The exception is bounded:

- **Only these two blocks** get a window. Nothing else in the suite gains one.
- The windows are **read-mostly**: a selectable list, an info panel, item slots. No dashboards.
- All *simulation* state stays on block-info as everywhere else (R7 still governs the furnaces, pipes,
  engines, …).

---

## The diagram item

- **One item, many variants:** `{mod}:diagram-{type}` with a variant group over the diagram types.
- **Shape** = the slightly-folded **`diag-base`** sheet (three Z-strips, `up`-face UVs partition the full
  texture so the drawing stays complete across the fold); **texture** = a **parchment-sheet base +
  `diag-{type}` overlay** on the sheet face (same overlay idiom as the furnace-core labels and tool
  molds), so the drawing reads on the sheet's top face. All 24 `diag-*` textures are drawn
  (`diag-bfh` included). The shape ships once in **exlib** (shared); the items are per-mod.
- **Held view.** A diagram is a **two-handed** held item: the `holdbothhands` idle pose plus an
  **inclined first-person transform** so the drawing on the top face angles toward the player's camera —
  you can read the plan you are carrying. Exact scale/rotation are **in-game-tuned**, like every held
  transform; there is no headless way to get them right.
- Each mod defines the diagram variants for the blocks **it** owns (iwex pipes/canals/cores, lpex
  passthroughs/machine schematics/part patterns, smex hot-core), discovered the same per-mod way as the other
  code-first defs.
- **Drafting cost is trivial:** a drawing medium (**charcoal or black coal** — both draw in vanilla) +
  **parchment** (vanilla's paper-like material) at the design table (see below). No tiers, no unlocks.
- *Asset fix:* `diag-base`'s `paper` texture is an absolute local path — must become a domain path before
  it ships (same fix the `design-table` shape needs).

---

## Diagram catalogue

The authored `diag-*` set, by family. **Consumed?** = spent when crafting (yes for structure cores /
megablocks that carry a projection + fillers; no — an `isTool` reusable — for simple blocks and items).

| Family | Diagrams | Makes | Consumed? | Owner |
|---|---|---|---|---|
| **Pipe** *(shared plan)* | straight, bend, tjunction, xjunction, outlet | a pipe of each mod's tier | no (tool) | iwex |
| **Passthrough** | straight, bend | brick-passthrough pipe | no (tool) | lpex |
| **Tuyere** | tuyere | furnace tuyere | no (tool) | iwex |
| **Molten canal** | straight, bend, tjunction, xjunction, start, tap, furnacetap, moldpedestal, barrel | canal pieces + molten barrel | no (tool) | iwex |
| **Casting** | sandcell | sand casting cell | no (tool) | iwex |
| **Casting (megablock)** | sandbed | sand casting bed | **yes** | iwex |
| **Mold patterns** | mold-plate, mold-doubleingot | the wooden **pattern** a sand mold is rammed around (diagram + knife + log) | no (tool) | iwex |
| **Mold patterns** (parts) | mold-cylinder, mold-boilerplate, mold-bedplate | patterns for the machine castings lpex consumes; the bedplate takes 2 logs | no (tool) | lpex |
| **Furnace cores** | bfc, cf, bfh | cold-blast / cupola / hot-blast core | **yes** | iwex / smex |

The **consumed?** column is not per-diagram data to maintain — it is simply whether the recipe uses
`.Tool()` or a plain ingredient, decided by whether the output is a structure core.

**Shared plans.** Some diagrams are cross-mod plans, not one mod's property — the **pipe** family is one
plan used by *every* pipe tier: iwex **bolted** pipe (hand-assembled from plates, lowest pressure), lpex
**cast** pipe (from cast pipe-parts off the boring machine, higher pressure), and future hpex **rolled**
pipe (Hadfield steel on the rolling mill, highest). The diagram picks the *shape*; each mod's own grid
recipe + materials pick the *tier* and its pressure, and each tier is its **own block with a slightly
different shape**. A shared plan lives in the lowest mod that uses it (**iwex**), which the higher mods
depend on. *(This 3-tier bolted→cast→rolled progression supersedes the 2-tier "bolted iwex / rolled lpex"
line in [conventions.md](conventions.md) — that doc still needs the update.)*

---

## How diagrams craft (Model A)

Ordinary `ExRecipeDef` grid recipes, one per family, with the diagram as an ingredient:

- **Simple block / item** — diagram is a reusable tool:
  `diagram-pipe-straight (tool) + plate + nails → 2 bolted straight pipe` (iwex). Swap the diagram
  variant, get a different pipe *shape* from the same pattern; the diagram is the selector. The **same**
  `diagram-pipe-straight` also drives lpex's `+ cast pipe-parts → cast straight pipe` and hpex's rolled
  tier — one shared plan, each mod's recipe + material choosing the tier.
- **Structure core** — diagram is consumed:
  `refractory brick + diagram-bfc (consumed) → cold-blast-furnace core`. The crafted core then carries
  its multiblock layout and is placed + built by the existing **multiblock projection** system —
  nothing downstream changes.

This is the whole crafting mechanic. It reuses the recipe-cost framework, the handbook "created-by",
and the golden harness with zero new engine code. The collision problem disappears because a family
shares one pattern and the diagram disambiguates.

---

## The design table (iwex)

A candle-lit, two-wide draughting table: draft diagrams, read what they build, browse setup guides.

**Block.** Two-wide **multiblock** (the authored `design-table` shape spans ~2 cells) built on the
filler mechanic. Candle wicks (`Top1`/`Top2` in the shape) emit **flame particles + dynamic light**
(`GetLightHsv`, as the temperature glow does). *Asset fix:* the shape's `paper` texture currently
points at an absolute local path and must become a domain path (`iwex:block/…`) before it ships.

**Window.** Read-mostly, three regions:

1. **Draft list** — every diagram (no gates), each showing its `diag-*` icon and name. Selecting one
   consumes a **drawing medium (charcoal or black coal) + parchment** and outputs the diagram item. That
   is the table's only *crafting*.
2. **Info panel** — for the selected diagram: description, what it is for, and — for structure-core /
   megablock diagrams — the **construction constituents** (what building it will cost and require), so
   the player commits knowingly. Text, per R7's spirit; no ghost preview.
3. **Guides tab** — the setup-diagram viewer (below).

### Guide viewer (the drawio setups)

The `docs/setups` overviews (`starter_iron_production`, `improved_steel_production`, … up to ~4600×2124
px) can be shown in-window. Two routes:

- **Custom GUI viewer (recommended)** — load the PNG into a `LoadedTexture` and draw it in a scroll/
  zoom clip area (drag-to-pan). A 4600 px image is one GPU texture on any modern card (max ≥ 8192), so
  no tiling — but **downscale to ~2048–2560 px** before shipping to save memory and mod size; zoom
  recovers detail. Ship the chosen PNGs into `assets/<domain>/textures/guides/` (docs/ is not packaged).
- **Handbook pages (lighter)** — add setups as handbook entries and link from the table. Near-zero
  custom code; less layout control and fussier image sizing.

Either way this gives new players in-game build references without alt-tabbing — squarely the design
office's job.

---

## The boring machine (lpex)

The machine-shop counterpart: a **powered** window station that machines cast blanks and simple stock
into finished parts.

- **Block entity** = `BlockEntityProductionMachine` (powered, timed work) + the shared station window.
- **Operation** = insert part(s) + a **schematic** → machined output. The schematic is an item and is
  **never consumed** (reusable tooling).
- **The cast → machine chain** (the payoff): the sand casting system pours a rough **cast cylinder**
  (via a sand cell); the boring machine + schematic then either
  - finishes it into an **engine cylinder** (cylinder + plates + nails → the real, usable part), or
  - bores it into **cast pipe-parts** — which assemble into pipes **faster** than bolted pipe and hold
    **more pressure** (a reward for the casting investment; pressure/throughput numbers live in
    [lpex.md](lpex.md)).
- This gives casting a downstream purpose and is historically honest: cast rough, machine to spec.

### Form & interaction *(shapes authored 2026-07-25)*

- **Two-block-high megablock.** Bottom **principal** block is the main interaction + the menu window; top
  **filler** block swaps out the **drill head** (a wear/tier part — quench-hardened heads for hard metal,
  mirroring the shear-die tiers). **Both** blocks report powered/unpowered in their block-info.
- **MP connection is on the top filler's south face** (default orientation north). The drill **spins while
  powered** (the sub-machine rotor idiom).
- **Work is timed and visible.** A craft takes a couple of seconds; a stack (e.g. cylinders → pipe-parts)
  runs item-by-item and takes proportionally longer. While a job runs, the **background animation plays** —
  drill down, a few cycles of the top traversing, drill up, down again, repeat — so the player reads it as
  a machine doing work, not a bare inventory window. Animations are already authored on the boring-machine
  shape; drive them off the `BlockEntityProductionMachine` job state (start on job begin, loop while
  processing, stop when the queue drains), the same network-driven pattern as the engine/sub-machine sync.
- Idle-vs-working and powered-vs-unpowered are distinct: an unpowered machine shows its status and does not
  animate; a powered idle machine spins the drill but does not run the work cycle.

---

## The exlib framework (shared spine)

Both stations are one thing wearing two hats, so the reusable parts live in exlib (matching its
framework role):

- **`diagram-{type}` item** shape + overlay-texture convention, and a lightweight **diagram catalogue**
  (per-mod-registered metadata: name/description lang keys, what it makes, guide image) that the design
  table renders from.
- **Station window base** — item slots + a selectable list + an info panel + the image/guide viewer —
  parameterised so the design table (hand, draft-only) and the boring machine (powered, process) are
  thin subclasses.
- Per-mod content: iwex owns the design table + its diagrams; lpex owns the boring machine + machine
  schematics + part patterns; smex contributes the hot-core diagram.

---

## Testing

- **Headless (unit/golden):** the grid recipes (goldens), the `diagram-{type}` item defs, the diagram
  catalogue (per-mod discovery + lang parity), and the boring machine's **input + schematic → output**
  matching (pure logic, like the pig-breaking arithmetic and canal `FlowEdge`).
- **In-game only:** the two windows and their **packet sync** (custom container GUIs need
  `OnReceivedClientPacket` — the hopper desync is the recorded scar), candle light/particles, and the
  guide-image viewer.

---

## Phasing (each phase shippable + testable)

1. **exlib core** — `diagram-{type}` item (paper + overlay) + the diagram catalogue. A temporary
   creative/grid path to obtain diagrams, so the system is testable before the table exists.
2. **Prove Model A on pipes** — convert the pipe family to one diagram grid recipe (tool ingredient).
   Validates identity-as-data and retires the worst pattern collisions. Headless goldens.
3. **Design table** — the two-wide block (candles/light) + the station window (draft list + info
   panel). Replaces the temporary diagram path.
4. **Guide viewer** — ship downscaled setup PNGs + the pan/zoom pane.
5. **Boring machine (lpex)** — powered station + the cast→machine chain (cylinder → engine cylinder /
   cast pipe-parts).
6. **Migrate the rest** — canals, molds, and the consumed-diagram cores off their colliding grid
   patterns onto the diagram families.

---

## Open items

- **Where diagram drafting lives before Phase 3** (creative-only vs a stopgap grid recipe) — a Phase 1
  call, not a design commitment.
- **Draft cost** — drawing medium (charcoal / black coal) + parchment quantities — a config number, set
  when the table lands.
