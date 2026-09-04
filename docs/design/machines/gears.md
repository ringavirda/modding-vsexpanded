# Gears
**Status** live - both items and all three smithing recipes ship   **Mod** iiex (`IronIndustryExpanded`)

**Owns**
* the gear item family - `iiex:gear-{metal}` and `iiex:largegear-{metal}`: codes, the variant axis, shapes, transforms, stack sizes, density, creative tabs, lang keys;
* their only production route - the three anvil smithing plans and their voxel patterns;
* the domain-ownership fact: `gear-iron` is defined in iiex and no mod upstream of iiex references it - iiex ships its own `iiex:spurgear` for its RCC stages ([flywheel & shafting](flywheel-and-shafting.md));
* the consumer census - every recipe and every RCC stage in the repo that requires a gear, with counts and file:line;
* the `game:gear-rusty`-or-craftable dual-recipe idiom and the `ExIngredients.Gear` helper contract;
* the defects this family carries: the duplicate Watt-engine recipe, the absent `materialUnits`, the dead texture key, and the single consumer of the large gear.

**Depends on** - cited, never restated here:
[recipes & config](../mechanics/recipes-config.md) owns `ExItemDef` / `ExRecipeDef`, code-first defs, the golden harness and the recipe-cost catalogue ·
[flywheel & shafting](flywheel-and-shafting.md) owns the transmission block, its RCC stage table and the `iiex:spurgear` / `iiex:bevelgear` items ·
[heading machine](heading-machine.md) owns the fastener benches and the `ItemDie` contract ·
[boring machine](boring-machine.md) owns the machine designed to cut gears from blanks ·
[casting cell](casting-cell.md) owns the patterns that would cast the gear blanks ·
[mp-energy](../mechanics/mp-energy.md) owns the `"mpenergy"` run the transmission serves ·
[density rule](../mechanics/density-rule.md) owns masses and the `materialUnits` convention ·
[overview.md](../overview.md) · [STATE.md](../../internal/plans/STATE.md)

---

## Role

A gear is the suite's machinery ingredient: the token that separates a machine recipe from a masonry one. Ten grid recipes across four mods take one.

`game:gear-rusty` cannot be crafted in vanilla - it is ruin loot, so a recipe requiring sixteen of them can strand a player permanently ([diagram-crafting.md:68-70](../mechanics/diagram-crafting.md)). Every gear-taking recipe in the suite is therefore authored twice, once for the vanilla loot gear and once for the smithed one, so the loot route stays a shortcut rather than a gate.

| Gear | What it is for | Where it is consumed |
|---|---|---|
| `gear` (small) | the ordinary machine gear - a line item in a bill of materials | 10 grid recipes across 4 mods (iiex's RCC stages take `iiex:spurgear`, not this family) |
| `largegear` | a heavy drive gear | one consumer: the Bessemer vessel's spawn gate |

> Three other "gear" concepts exist and are not this page's. `iiex:bevelgear` is a cast-iron block component consumed to turn a shaft into a bevel junction, and `iiex:spurgear` (60 u, `SpurGearItemDefinitions.cs`) is the iron tier's own machine gear, consumed by iiex's RCC stages - [flywheel & shafting](flywheel-and-shafting.md) owns both. `gearpinion`, the block-scale heavy cast drive gear the design names, exists nowhere in code. The distinction: a gear-iron is a part in a bill of materials, a pinion is a block-scale drive component.

---

## Structure

There is no block. The family is two items × one variant axis, both authored code-first in `GearDefinitions.cs`.

| | `gear` | `largegear` |
|---|---|---|
| code | `iiex:gear-{metal}` | `iiex:largegear-{metal}` |
| variants | `metal` ∈ {`iron`, `steel`} - `GearDefinitions.cs:20` | same (shared `Common`) |
| resolved codes | `gear-iron`, `gear-steel` | `largegear-iron`, `largegear-steel` |
| shape | `game:item/gear-rusty` - `:29` | `game:block/machine/jonas/steamengine/gear24` - `:61` |
| max stack | 64 - `:30` | 16 - `:63` |
| density | 7870 kg/m³ (steel) - `:21` | same |
| textures | `rusty-iron` ← `game:block/metal/ingot/{metal}` - `:22` | `rusty-iron` (inherited, dead - see Gotchas) + `gold` ← same - `:62` |
| creative tabs | `general`, `items`, `iiex` - `:23-25` | same |
| `materialUnits` | absent | absent |

Both variants share one surface through the private `Common` helper (`:19-25`); only the shape, the stack size, the extra texture key and the three model transforms differ. Golden parity files: `test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/gear.json` and `…/largegear.json`.

---

## Assets

No new art: both gears reuse vanilla shapes retextured off the ingot texture, the same route `iiex:spurgear` takes.

| Asset | State |
|---|---|
| shapes | vanilla, referenced not copied (`GearDefinitions.cs:29`, `:61`) - nothing under `assets/iiex/shapes/` |
| textures | vanilla, `game:block/metal/ingot/{metal}` - the ingot texture tints the model, so iron and steel read differently with zero art |
| model transforms | authored per item: gui / tp-hand / ground (`:31-57` for `gear`, `:64-90` for `largegear`) - hand-tuned values, not seeds |
| lang | `assets/iiex/lang/en.json:4-7` - all four codes named; `ru.json:4-7` and `uk.json:4-7` complete |
| handbook | none - `docs/iiex/handbook/` has 5 pages and no gear entry. The sync pipeline joins on the `NN-` prefix, so adding one is a page and a lang block |

---

## Construction

Three anvil smithing recipes, and they are the only route. No grid recipe, no RCC, no machine produces a gear.

| Recipe code | Plan | Ingredient | Output | file:line |
|---|---|---|---|---|
| `2gears-{metal}` | one 5-row voxel plan | `game:ingot-*`, variants `iron`/`steel` | 2 × `iiex:gear-{metal}` | `GearRecipeDefinitions.cs:38-49` |
| `4gears-{metal}` | the same plan stacked twice (10 rows) | same | 4 × `iiex:gear-{metal}` | `:50-59` |
| `largegear-{metal}` | a single 10 × 10 ring plan | same | 1 × `iiex:largegear-{metal}` | `:60-86` |

The 2-gear plan (`:27-34`), which the 4-gear plan concatenates with itself (`:55`):

```
_#_#__#_#_
##########
_#_#__#_#_
##########
_#_#__#_#_
```

Smithing recipes are declarative voxel patterns, so they are authored through the `ExRecipeDef.Add` / `.Body` escape hatch (an anonymous object per recipe) rather than a builder - `GearRecipeDefinitions.cs:9-12`; [recipes & config](../mechanics/recipes-config.md) owns the mechanism.

Smithing is outside the recipe-cost framework. `IiexRecipeConfig` only constructs `Type = "rcc"` and `Type = "grid"` entries (`:40-54`), and its catalogue (`:59-86`) has no gear key, so `/exmod recipes iiex cheap` rescales every machine bill but not the gears themselves.

---

## Operation

A gear has no verbs - it is an inert crafting item. Its whole behaviour is who requires it.

### The dual-code idiom

Every gear-taking recipe is emitted twice, once per gear code, through one helper:

```csharp
// ExIngredients.cs:57-58
public static Func<IngredientBuilder, IngredientBuilder> Gear(string code, int qty) =>
  i => i.Item(code).Quantity(qty);
```

Contract: the caller passes the literal code. There is no metal capture - unlike `Plate` (`:29`), `Nails` (`:37`) and `Rod` (`:45`), which all call `.Metal()`. So `iiex:gear-*` is an uncaptured wildcard: iron and steel gears are interchangeable in every recipe and neither keys the output's metal. The house pattern is a `foreach` over both codes (`MachineRecipeDefinitions.cs:29-34`, `HighPressureExpanded/.../MachineRecipeDefinitions.cs:23-24`) or two explicit `.Grid(…)` calls (`ConverterRecipeDefinitions.cs:31`, `:43`).

### Consumer census

Every gear requirement in `src/`. This census is this page's; the per-machine stage tables are not - follow the links.

Grid recipes - each row exists twice, once for `game:gear-rusty` and once for `iiex:gear-*`, unless noted:

| Mod | Recipe | Gears | file:line |
|---|---|---|---|
| iiex | Watt Engine | 2 - declared but never placed | `MachineRecipeDefinitions.cs:78` |
| iiex | Fluid Pump | 1 | `:89` |
| iiex | Manual Fluid Pump | 2 | `:101` |
| iiex | Mechanical Power Generator | 2 | `:115` |
| iiex | Piping (Valve) | 2 | `PipeRecipeDefinitions.cs:59` (rusty), `:79` (iiex) |
| iiex | Piping (Pressure Valve) | 2 | `:69` (rusty), `:89` (iiex) |
| smex | Air Blower | 2 | `EngineRecipeDefinitions.cs:31` |
| smex | Bell Hopper | 4 | `HotBlastFurnaceRecipeDefinitions.cs:51` |
| smex | Converter Transmission | 16 | `ConverterRecipeDefinitions.cs:55` |
| hpex | Cornish Engine | 4 | `HighPressureExpanded/.../MachineRecipeDefinitions.cs:46` |

RCC stages and gates - these take an exact code, not a wildcard:

| Mod | Where | Requires | file:line | Owned by |
|---|---|---|---|---|
| iiex | transmission, stage 2 | `iiex:spurgear` × 2 - iiex's own gear, not this family | `BlockTransmission.cs:72` | [flywheel & shafting](flywheel-and-shafting.md) |
| smex | Bessemer vessel spawn | `iiex:largegear-iron` or `-steel` × `BessemerRequiredGears` | `BlockEntityConverterControl.Peripherals.cs:218-219` | [bessemer](bessemer.md) |

The Bessemer gate takes a smithable iron/steel large gear so the converter vessel stays buildable in worlds where looted rusty gears cannot be obtained.

---

## Numbers

| Key | Value | file:line | What it does |
|---|---|---|---|
| `gear` max stack | 64 | `GearDefinitions.cs:30` | hard-coded |
| `largegear` max stack | 16 | `:63` | hard-coded |
| material density | 7870 kg/m³ | `:21` | hard-coded; steel's density, used for both metals |
| `2gears` output | 2 | `GearRecipeDefinitions.cs:47` | hard-coded |
| `4gears` output | 4 | `:57` | hard-coded |
| `largegear` output | 1 | `:84` | hard-coded |
| allowed ingot variants | `iron`, `steel` | `:23` | hard-coded in `IngotMetal` |
| `BessemerRequiredGears` | 1 | `SiexConfig.cs:100` | the only config-tunable gear count in the suite |
| `materialUnits` | absent | — | every other cast/formed item declares it (`CastPartItemDefinitions.cs:40`, `:53`; `BevelGearItemDefinitions.cs:22`; `ItemPig.cs:56`) - a gear cannot be scrap-valued |

Grid gear counts are in the census above; none of them is a named constant - every one is a literal argument at the `Gear(...)` call site.

---

## Drops

Items, so there is no break behaviour of their own.

| Situation | Result |
|---|---|
| gear on the ground / in inventory | ordinary item, stacks to 64 / 16 |
| iiex transmission broken | the RCC scatters its construction materials - the block itself is `NoDrops()`; [flywheel & shafting](flywheel-and-shafting.md) owns the salvage rule |
| gear melted / scrapped | not possible through the `materialUnits` path - the attribute is absent |

---

## Code

| Piece | file:line | Note |
|---|---|---|
| `GearDefinitions : IExItemDefProvider` | `src/IronIndustryExpanded/Items/GearDefinitions.cs:12` | a never-instantiated stand-alone provider - both gears use the vanilla `Item` class, so there is no mod class to hang the defs on (`:6-11`) |
| `Common(ExItemDef)` | `:19-25` | the shared surface: variant axis, density, texture, three creative tabs |
| `Gear(domain)` / `LargeGear(domain)` | `:27-57` / `:59-90` | shape + stack + transforms |
| `GearRecipeDefinitions : IExRecipeDefProvider` | `src/IronIndustryExpanded/Recipes/Smithing/GearRecipeDefinitions.cs:14` | the three plans |
| `IngotMetal` | `:17-24` | the shared ingredient object |
| `TwoGearRows` | `:27-34` | the 5-row plan; `:55` concatenates it with itself for the 4-gear plan |
| `ExIngredients.Gear(code, qty)` | `src/ExpandedLib/Definitions/ExIngredients.cs:57-58` | the recipe-side helper; no metal capture |
| Bessemer gate | `src/SteelIndustryExpanded/.../BlockEntityConverterControl.Peripherals.cs:218-219` | `IsSpawnGear` + `HasSpawnMaterials` |

---

## Gotchas

- No iiex block requires an iiex gear. iiex's transmission RCC takes `iiex:spurgear` (`BlockTransmission.cs:72`); blocker B7 is closed.
- `overview.md:69` credits `gear-iron` to iiex. It is not an iiex item - there is no `gear` def anywhere in `src/IronIndustryExpanded/` (iiex's own gear is `spurgear`). The row should say iiex.
- The craftable gears augment `game:gear-rusty`, they do not replace it - every recipe is authored for both codes.
- The Watt engine declares a gear it never places, and therefore ships as a duplicate recipe. `MachineRecipeDefinitions.cs:78` adds `Ingredient("G", Gear(gear, 2))` but the pattern is `_H_,PRP,PIP` (`:74`) - there is no `G` cell. The `foreach` at `:29-34` emits the recipe twice, so the shipped golden contains two Watt Engine recipes with byte-identical grids differing only in an unused ingredient (`goldens/iiex/recipes/grid/machines.json`, entries 3 and 7). Consequences: (a) the Watt engine costs no gear at all; (b) two recipes match the same 3×3 pattern with overlapping ingredients - the recorded recipe-conflict failure mode. Fix is one character in the pattern, or deleting the ingredient and the loop entry.
- Gears carry no `materialUnits`. Every other formed part declares it (see Numbers), so gears are invisible to remelt/scrap arithmetic. `castplate-heavy` is 160 u and a bevel gear is 40 u (`BevelGearItemDefinitions.cs:12`), so a machine gear ought to be a small number, but choosing it is a [density rule](../mechanics/density-rule.md) decision, not this page's.
- `largegear` carries a dead `rusty-iron` texture key. `Common` sets `rusty-iron` for both items (`GearDefinitions.cs:22`), and `LargeGear` adds `gold` (`:62`) because the `gear24` shape uses that key. The shipped golden has both (`goldens/iiex/itemtypes/largegear.json`). One of the two must be unused on the large gear; the explicit `.Texture("gold", …)` override is strong evidence it is `rusty-iron`. (Not verified against the vanilla shape - `D:/Gaming/Others/Vintagestory` is not readable from this checkout.)
- The creative tab is still named "Pipes & Power". `assets/iiex/lang/en.json:2` maps `game:tabname-iiex` to "Pipes & Power" (ru: "Трубы и приводы", uk likewise), and `GearDefinitions.cs:25` puts both gears in that tab. The mod's display name is Low Pressure Expanded (`modinfo.json`).
- The shipped route and the designed route disagree. Gears ship as an anvil smithing product; the designed route is rod → gear blank → the boring machine cuts the teeth, with a meshing gear cut from a cast blank. Both routes cannot be the primary one. The smithing route is the only one that works today - the [boring machine](boring-machine.md) is not built and `PatternItemDefinitions.Molds` has no gear-blank entry.
- Steel gears have no distinct function. `metal` ∈ {iron, steel} on both items, but every consumer takes `iiex:gear-*` uncaptured, so a steel gear costs a steel ingot and buys nothing. The only place metal is mentioned is the Bessemer gate, which accepts either (`BlockEntityConverterControl.cs:1056-1057`).
- The large gear has exactly one consumer in the repo, and it is a hotbar count in a block entity rather than a recipe (`BlockEntityConverterControl.cs:1063-1065`), defaulting to 1 (`SiexConfig.cs:100`) - a 10 × 10 voxel smithing plan for a single-use item.
- A gear count is never a named constant. All ten grid quantities are literals at the call site, so rebalancing means editing ten files; the cost catalogue scales them at load, but only the grid ones ([recipes & config](../mechanics/recipes-config.md) owns that pipeline).

---

## Open

- Two near-identical small gears coexist. `iiex:spurgear` serves iiex's constructions; `iiex:gear-*` serves the steam-tier grid recipes. Whether they merge, and in which mod, is open; a merge breaks every `iiex:gear-*` recipe and every save holding one, so it needs a block/item migrator ([recipes & config](../mechanics/recipes-config.md)).
- What an unresolvable RCC `requireStacks` entry does - whether the stage silently requires nothing, logs, or refuses the blocktype at load - is not established anywhere. Needs a headless test.
- Does the [boring machine](boring-machine.md) route replace smithing, or add to it? Options: keep smithing as the expensive hand route and make the machine cheaper per gear (the efficiency ladder the suite is built on), or retire smithing when the machine lands. The first fits the thesis. iiex's `spurgear` already ships the two-route pattern: chiselled from iron ingots, or cheaper from cast iron (`EnergyRecipeDefinitions.cs:39-59`).
- Give steel gears a job, or delete them. Candidates: gate the hpex Cornish engine on steel gears (it already takes steel plate, rod and nails, `HighPressureExpanded/.../MachineRecipeDefinitions.cs:44-49`), or add durability/efficiency semantics. Doing nothing leaves a strictly dominated item.
- `gearpinion` still has no item. The design's split is bill-of-materials gear (covered by `gear-iron` and `iiex:spurgear`) vs block-scale drive gear (`gearpinion`); the second half exists nowhere.
- No handbook page. Both a page under `docs/iiex/handbook/` and its `NN-`-prefixed lang block are missing; the handbook-sync test fails on drift, so this is a two-file change.
- `materialUnits` value. Needs a number before gears can participate in scrap/remelt.
