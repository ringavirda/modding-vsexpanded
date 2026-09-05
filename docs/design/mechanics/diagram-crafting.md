# Diagram crafting

**Status** built in part — the mechanic ships and three families use it; the pipe family and the
consumed-diagram half are not migrated   **Mod** exlib (shape) + iiex (items, recipes, station); iiex and
smex contribute variants when their phases land

**Owns**

* **Model A** — the diagram as an ingredient, and the consumed-vs-reusable rule;
* the diagram item — shape, texture convention, held view, variant scheme;
* the catalogue — every shipped diagram variant and which have a consuming recipe;
* the recipe idiom for diagram-led grids;
* the migration state — what is proven, what still runs on hand-authored patterns;
* the station idiom shared by the design table and the boring machine.

**Does not own** — cited only, never restated:

| Fact | Owner |
|---|---|
| The design table (block, window, craft recipe, draft cost) | [design-table](../machines/design-table.md) |
| The boring machine (footprint, jobs, tooling) | [boring-machine](../machines/boring-machine.md) |
| Pipe tiers and pressure numbers | [pipe-network](pipe-network.md) |
| The pattern → cavity → cast-part chain | [casting-cell](../machines/casting-cell.md) · [long-cell](../machines/long-cell.md) |
| `ExRecipeDef`, goldens, the cost catalogue | [recipes-config](recipes-config.md) |

---

## Why

The 3×3 grid is out of room: distinct shape variants - every pipe and canal junction, every furnace core,
every mold - each need a distinct pattern, and patterns collide on overlapping ingredients. A diagram is
identity as data, so one material recipe serves a whole family and the diagram picks the variant. It is also
the drawing-office idiom the suite is built around: drafting plans at a table, machining blanks to a schematic.

---

## The model

Settled: Model A - the diagram is an ingredient, not a crafting engine.

* The design table produces diagrams and serves as the planning terminal. It does not craft parts.
* Assembly is ordinary crafting: plain `ExRecipeDef` grid recipes with the diagram as an ingredient, or
  the unchanged multiblock system. No new recipe engine.
* Consumed vs reusable falls out of the ingredient flag. A diagram for a simple block or item is an
  `isTool` ingredient (`IngredientBuilder.Tool()`) - spent by durability, effectively reusable. A diagram
  for a multiblock structure core is a plain consumed ingredient. That flag is the whole rule "structures
  consume the plan, parts don't".
* No progression gates. Every diagram is draftable from the first table; the only gate is materials.
* No holograms. Multiblock output is explained in text; build previews stay the in-world
  Ctrl+Shift outline.
* Windows on the two stations only. The design table and the boring machine get a read-mostly window
  (a plain case of R7 - the catalogue interaction is what block info cannot express); nothing else in the
  suite gains one.

### Amendment 2026-08-15: assembly may be work, at one station

⛔⛔ **"Assembly is ordinary crafting" gains an exception, and "no new recipe engine" does not.** The
[workbench](../machines/workbench.md) adds a 5 × 5 bench whose recipes may declare an **interaction
sequence** - an ordered list of tool-held gestures - so a boiler or a fitting is assembled rather than
clicked. Two things about it are worth stating here, where Model A is read:

* **It adds no recipe format and no matcher.** Vanilla's `GridRecipe.Width`/`.Height` are settable and
  `ConsumeInput` takes an arbitrary grid width, scanning sub-positions - so a 5 × 5 bench uses vanilla's own
  matching and every existing 3 × 3 recipe works in it unchanged. Model A's clause stands.
* **The reason for the bigger grid is stack size, not layout.** An ingredient's `quantity` is bounded by
  what a slot holds, so 48 rods against a 16 cap needs three cells. That does not weaken "one cell per
  distinct ingredient" below - three cells of rods is still rods, not a picture of the product.

★ The sequence is declared per recipe in `config/craftsequences/`, keyed by machine and contributed to, so
another mod can put labour on our recipe and we on theirs. Format and rules: [workbench](../machines/workbench.md).

### The recipe idiom

A diagram-led grid is the plan plus its bill of materials: one cell per distinct ingredient, carrying
its quantity. It is not a picture of the product arranged in the grid - the diagram already carries the
shape, so a grid that mimics assembly encodes the same information twice. The flywheel grids
(`Recipes/Grid/EnergyRecipeDefinitions.cs`) are the reference examples.

---

## The diagram item

One itemtype per mod, `{mod}:diagram-{type}`, defined in `mods/iiex/src/Items/DiagramItemDefinitions.cs`.
Today only iiex ships one - 27 variants, each with its drawn texture.

* Shape: the shared folded-sheet `exlib:item/diag-base`; texture: a parchment base plus the
  per-type `diag-{type}` drawing overlaid on the sheet's top face
  (`mods/iiex/assets/iiex/textures/item/diagram/`, 27 files, one per variant).
* Held two-handed (`holdbothhands`) and inclined so the drawing angles toward the camera. The
  transforms are seed values; they can only be judged in-game.
* A diagram is a plain item - it carries no logic. Pattern diagrams are derived from
  `PatternItemDefinitions.PatternTypes`, so a new castable part gets its diagram and its craft
  automatically.
* Drafted at the [design table](../machines/design-table.md) for 1 drawing medium + 1 parchment.

---

## Catalogue

27 shipped variants (19 structure + 8 pattern), all iiex. "Recipe" is whether a grid recipe consumes the
diagram today; every consuming recipe uses `.Tool()`.

| Family | Types | Recipe today | Planned use |
|---|---|---|---|
| Pipe | straight, bend, tjunction, xjunction | none | tool - one shared plan across the pipe tiers |
| Molten canal | straight, bend | **built** (`DiagramRecipeDefinitions`) - diagram + cobblestone → canal | tool |
| Molten canal | tjunction, xjunction, start, tap, furnacetap, moldpedestal | none | tool |
| Casting | sandcell | none | tool |
| Casting | sandbed | none | consumed (megablock core) |
| Tuyere | tuyere | none | tool |
| Furnace cores | furnace-coldblast, furnace-cupola | none | consumed (structure cores) |
| MP energy | mpenergy-flywheel, mpenergy-flywheellarge | **built** (`EnergyRecipeDefinitions`) - diagram + cast sections/plates/gear → flywheel | tool |
| Patterns | item-{castheavyplate, castingotmold, castbarrel, castshell, castwheelsection, castbillets, castblooms, castslab} | **built** (`PatternRecipeDefinitions`) - diagram + knife (both tools) + 2 planks → wooden pattern | tool |

That is 12 diagram-led grid recipes repo-wide: 2 canal + 2 flywheel + 8 pattern. The other 15 variants
have items, textures and (mostly) lang text but nothing consumes them yet.

**Shared plans.** The pipe diagrams are one plan for every pipe tier: the diagram picks the shape; each
tier's own recipe and material pick the tier. A shared plan lives in the lowest mod that uses it (iiex),
which the higher mods depend on. Tiers and their numbers: [pipe-network](pipe-network.md).

---

## Migration state

Proven - the reusable-tool half of Model A, three times over:

* Canals: `diagram-molten-straight` / `-bend` + cobblestone, one pattern for the family, coexisting
  with the legacy hand-authored canal patterns in `MoltenRecipeDefinitions` until the family migrates
  wholesale.
* Patterns: every sand-casting pattern is carved from its diagram; the recipe set is derived from the
  single pattern-type source, so it cannot drift.
* Flywheels: both wheel sizes are diagram-led bills of materials.

Not migrated - the pipe family. `Recipes/Grid/PipeRecipeDefinitions.cs` still ships four hand-authored
hammer + plate + nails patterns, and no recipe consumes a pipe diagram.

Not proven - the consumed half. No recipe anywhere consumes a diagram as a plain ingredient; the
structure-core diagrams (furnace cores, sand bed) wait on their structures being crafted-from-diagram at
all.

---

## Stations

* [Design table](../machines/design-table.md) - built and craftable. Drafts diagrams; the
  station-window precedent.
* [Boring machine](../machines/boring-machine.md) - designed, art drawn, nothing built. The powered
  counterpart: part(s) + a reusable schematic → machined output, in a window.
* The shared window base does not exist. `GuiDialogDesignTable` is iiex-local. Its picker enumerates
  the loaded `diagram-*` items directly, which is what already lets another mod's diagrams appear with no
  catalogue registration - so the planned exlib extraction (slots + list + info panel, parameterised for a
  hand station and a powered one) is deferred until the boring machine, the second consumer, is built.

---

## Open

1. Migrating the pipe family onto its four diagrams - the original motivating case.
2. First consumed-diagram recipe - a structure core crafted from refractory + a consumed diagram, to
   prove the second half of Model A.
3. Whether the legacy canal patterns retire once the remaining six canal diagrams get recipes, or the
   two routes coexist permanently.
4. The exlib station-window extraction (§ Stations) - deferred to the boring machine.
