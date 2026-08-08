# Diagram crafting

**Status** built in part — the mechanic ships and three families use it; the pipe family and the
consumed-diagram half are not migrated   **Mod** exlib (shape) + iwex (items, recipes, station); lpex and
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

### The recipe idiom

A diagram-led grid is the plan plus its bill of materials: one cell per distinct ingredient, carrying
its quantity. It is not a picture of the product arranged in the grid - the diagram already carries the
shape, so a grid that mimics assembly encodes the same information twice. The flywheel grids
(`Recipes/Grid/EnergyRecipeDefinitions.cs`) are the reference examples.

---

## The diagram item

One itemtype per mod, `{mod}:diagram-{type}`, defined in `src/IronworkingExpanded/Items/DiagramItemDefinitions.cs`.
Today only iwex ships one - 27 variants, each with its drawn texture.

* Shape: the shared folded-sheet `exlib:item/diag-base`; texture: a parchment base plus the
  per-type `diag-{type}` drawing overlaid on the sheet's top face
  (`assets/iwex/textures/item/diagram/`, 27 files, one per variant).
* Held two-handed (`holdbothhands`) and inclined so the drawing angles toward the camera. The
  transforms are seed values; they can only be judged in-game.
* A diagram is a plain item - it carries no logic. Pattern diagrams are derived from
  `PatternItemDefinitions.PatternTypes`, so a new castable part gets its diagram and its craft
  automatically.
* Drafted at the [design table](../machines/design-table.md) for 1 drawing medium + 1 parchment.

---

## Catalogue

27 shipped variants (19 structure + 8 pattern), all iwex. "Recipe" is whether a grid recipe consumes the
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
tier's own recipe and material pick the tier. A shared plan lives in the lowest mod that uses it (iwex),
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
* The shared window base does not exist. `GuiDialogDesignTable` is iwex-local. Its picker enumerates
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
