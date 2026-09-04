# Research snapshot - blanks-tooling-consumers

**Written** 2026-09-04 by a read-only research agent, against commit `cf2e8c89` (the tree before the
line-ending renormalisation). **Covers** sand patterns and cast parts, drawn machined-item shapes, roll sets, gears and shafts, tooling items, cast pipe, recipe plumbing; proposed job-table roster.
**Purpose** grounding for the roadmap's Phase 1 walkthrough and Phase 2 machining-line plan
(`docs/internal/plans/2026-09-04-*.md`).

Line numbers were right on that day and drift; cite by symbol when reusing. "UNVERIFIED" marks are the
agent's own and were not re-checked. Nothing here is a decision - decisions live on the design pages.

---

I have everything needed. Here is the report.

# Machining-line content: research report

Conventions: iiex = `src/IronIndustryExpanded`, exlib = `src/ExpandedLib`. "Exists" = a code the object loader registers today.

## 1. Sand patterns and cast parts

**`PatternItemDefinitions`** (`.../BlockStructures/Casting/PatternItemDefinitions.cs:17`). Authoring helper `Mold(shape, capacity, cavity, outputCode, outputType="item", minPourTemp=1150f, size="cell")` `:24-43`; `Box(x1,y1,z1,x2,y2,z2)` `:49-64`. Eight types in `Molds` `:86-170`:

| type | capacity | output | size |
|---|---|---|---|
| `castheavyplate` :87 | `HeavyPlateUnits` (160) | `iiex:castplate-heavy` | cell |
| `castingotmold` :95 | 152 | block `iiex:casting-mold-ingot` (`outputType:"block"`) | cell |
| `castbarrel` :105 | 200 | `iiex:cast-barrel` | cell |
| `castshell` :115 | 600 | `iiex:castshell` | cell |
| `castwheelsection` :123 | 600 | `iiex:castwheelsection` (filling still named `cell-filling-flywheelpart` :125) | cell |
| `castbillets` :142 / `castblooms` :154 / `castslab` :162 | 3x600 / 2x1000 / 3000 | `iiex:caststock-{billet,bloom,slab}` | `longcell` |

`MoldSpec` record `MoldSpec.cs:31-39`: `Schema, Size (MoldSize.Cell|LongCell :10-16), Shape, Capacity (int units), Cavity (Cuboidf[]), Output (JsonItemStack), MinPourTemp`. No count field - a multi-piece impression is expressed as `capacity = n x units` and the output stack (see the `castbillets` comment `:133-134`; UNVERIFIED that the cell emits N items rather than one - I did not read `Harvest`). `AttributeKey = "mold"` `:41`; `TryParse` `:51-128`, parser default `minPourTemp` 0 `:116` vs helper 1150.

Read at pour: `BlockEntitySandCastingCell.cs` - spec cached from the full pattern code `:35-37`, resolved via `Api.World.GetItem` then `MoldSpec.TryParse(pattern.Attributes["mold"])` `:77-87`; gate `FirstCodePart() == "pattern"` `:200`; `Imprint` parses the held stack `:243-247`, refuses the wrong `Size` `:258`, and charges pattern durability `:270` (the only `DamageItem` in the forming/casting code).

Adding a type = one `Molds` row + one `PatternShapes` row (`:71-82`; a missing row silently renders `game:item/plate`, `:247`) + a filling shape + the output item + two lang rows per locale (`item-pattern-{type}-*` `assets/iiex/lang/en.json:405-411,507`; `item-diagram-item-{type}` `:154-156,176-179,508`; optional `diagramdesc-item-{type}` `:212,509`) + a `diag-item-{type}` texture (`DiagramItemDefinitions.cs:19,50-56,71`). The diagram variant and the `DKP` pattern recipe derive automatically (`DiagramItemDefinitions.cs:53-56`; `PatternRecipeDefinitions.cs:17-35`). `Itemtype(domain, molds, shapes)` `:224-259` emits `attributesByType["*-{type}-*"]`, `shapeByType`, `TextureAll(debarked/{wood})`, groups `type` then `wood` (12 woods `:199-213`), `MaxStackSize(1)`, `durability = WoodenPatternDurability (24)` `:187,:256`.

Filling art present under `assets/iiex/shapes/casting/`: `cell-filling-{axle,cylinder,gearblanklarge,gearblanksmall}.json` and `longcell-filling-castframe.json` are referenced by nothing (only the eight above are named in code). No filling exists for a heavy cylinder, a roller blank or a shaft blank distinct from `axle`. Blank item art: `assets/editable/shapes/items/sandcast/blanks/item-sandcast-{cylinderblank,cylinderheavyblank,gearblanklarge,gearblanksmall,rollerblanks,shaftblank}.json` (roots `Cylinder1`, `Cylinder1`, `Cube4`, `GearBlank1`, `RollerBlank1`+`RollerBlank2`, `Shaft1`), structural `item-sandcast-{frame,heavyplate,shell,wheelsegment}.json`. The editable filling sources cast-parts.md:223 / patterns.md:252 name (`molten-sandcellfilling-*`, `molten-sandlongcellfilling-*`) were not located under `assets/editable/shapes/` - UNVERIFIED. `git status` on `assets/iiex/shapes/item` and `assets/editable/shapes/items` is clean, so the docs' "untracked" claims are stale.

**`CastPartItemDefinitions`** `.../Items/CastPartItemDefinitions.cs:16`: `castshell` `:53-61` (600 u `:36`), `castplate-heavy` `:63-74` (160 `:18`), `cast-barrel` `:76-86` (200 `:21`), `castwheelsection` `:92-100` (600 `:29`); all `MaterialDensity(7200)`, `meltingPoint 1150`, `materialUnits` attribute, shapes exported (`assets/iiex/shapes/item/{castshell,heavyplate,cast-barrel,castwheelsection}.json`). Apart: `iiex:bevelgear` `BevelGearItemDefinitions.cs:16` (40 u `:11`, `iiex:item/gearbevel`), `iiex:spurgear` `SpurGearItemDefinitions.cs:28` (60 u `:20`, borrows `game:item/gear-rusty` `:32`; its comment `:29-31` names `item-gear-spur.json`, which now lives at `items/machined/item-shaped-gearspur.json`), `iiex:caststock-{billet,bloom,slab}` `CastStockItemDefinitions.cs:82-88` (600/1000/3000 `:22-28`). No `castframe`, cylinder, gear-blank, axle or pinion item exists (grep of `src/` for `pinion|gearblank|cylinderheavy|rollerblank` hits only that comment). Also relevant: **`iiex:blank`** already exists - the rolled "wide 2.0 piece the boring machine machines into cranks and gear blanks", 200 u, placeholder shape (`RolledItemDefinitions.cs:34,55,105-106`).

## 2. Drawn machined and smithed shapes

All fourteen under `assets/editable/shapes/items/machined/` use texture key `cast-iron1` with an absolute `F:/.../materials/cast-iron1` path (verified on gearbevel). Root elements:

| file | roots (children) |
|---|---|
| `item-bored-cylinderheavy` | `Cylinder` (Cube94/95/106-110) |
| `item-lathed-cylinder` / `item-lathed-cylinderheavy` | `Cylinder` (8 / 10 elems) |
| `item-drilled-gearblanklarge` | `GearBlank1` (GearBlank2, GearBlank3, Cube*) |
| `item-lathed-gearblanklarge` | three unnamed roots `Cube4`,`Cube5`,`Cube16` (21) - no selectable root |
| `item-lathed-rollers-flat` | `RollersFlat` (35), carries a `cycle` clip |
| `item-lathed-rollers-grooved` | `RollersGrooved` (99), `cycle` clip |
| `item-lathed-rollers-flatwide{5,10,15,20}` | `RollersFlatWide{n}` (FlatDown2, 20GapDown2, FlatUp2, 20GapUp2 ...) |
| `item-shaped-gearbevel` | `HubS` (66) - element-identical to the runtime `assets/iiex/shapes/item/gearbevel.json` (66, `HubS`) |
| `item-shaped-gearpinion` | `Hub1` (Hub2, Rim01..., Tooth01...; 26) |
| `item-shaped-gearspur` | `Hub1` (Hub2, Spoke01-06, Rim*, Tooth01-24; 40) |

Runtime twins: only `gearbevel.json`; the other thirteen have none (`assets/iiex/shapes/item/` holds cast-barrel, castbillet, castbloom, castshell, castslab, castwheelsection, gearbevel, heavyplate, ingotmold, metalchunk, puddled-ironball, rolled-beam, rolled-nailplate, rolled-rivetrod, shearblade, steelcrucible, tool-paddle, tool-rabble).

`items/smithed/`: `item-forged-machinecutter` (`Cutter1`/`Cutter11`, texture `iron -> block/metal/plate/iron`, no twin); `item-forged-machinedrill` (`DrillBit`, 12 elems, clips `drill`, `drill-down-pose`, `idle`, no twin); `item-forged-machineshears` (`Shear1`/`Shear11` - not `Sear11` as machining-line.md:343 says) -> twin `shearblade.json`; `item-forged-steamhammer-dieflat` (`DieUp`,`Buffer`,`DieBottom`, no twin); `item-puddled-ironball` -> `puddled-ironball.json`; `item-shingled-metalchunk` -> `metalchunk.json`; `item-shingled-bar`/`-slab` are the bases the stock stages are generated from, not exported directly.

## 3. Roll sets

`RollSetItemDefinitions.cs:18`; `Set(family, accepts, barrelWidth, minTorque)` authored in `double` `:24-38`; `Sets` `:45-82`:

| code | family | accepts | barrel | minTorque |
|---|---|---|---|---|
| `iiex:rollset-flat` :51 | flat | shingledbar, castbillet, rod, beam | 4.0 | 0.2 |
| `iiex:rollset-flatwide` :59 | flatwide | shingledslab, shingledbar, castbloom, castslab, heavyplate | 16.0 | 0.5 |
| `iiex:rollset-grooved` :76 | grooved | shingledbar, castbillet, rod, beam | 16.0 | 0.3 |

`SetTypes` `:86`; emit `:91-105` (shape `game:item/ingot` `:99`, `MaxStackSize(1)` `:102`, `attributesByType["*-{type}"]` `:92-94`). `RollSetSpec` record `RollSetSpec.cs:24-30` (`Schema, Family, Accepts, BarrelWidth, MinTorque`), key `"rollset"` `:32`. Confirmed: no recipe outputs any roll set - `rollset` appears in no file under `src/*/Recipes`, neither `IiexRecipeConfig` nor `SiexRecipeConfig`, and no `goldens/iiex/recipes` file. After 2026-08-14 the only wide set is `rollset-flatwide`; `rollset-flatwide35/30` were "cancelled, not deferred" (`docs/internal/worklog/2026-08.md:2007-2013`) and flatwide got manual gap control, not built (`:2176-2180`). Note the art disagrees: four `item-lathed-rollers-flatwide*` shapes for one item.

## 4. Gears and shafts

Codes: items `iiex:spurgear`, `iiex:bevelgear`, `iiex:gear-{iron,steel}`, `iiex:largegear-{iron,steel}` (`GearDefinitions.cs:18,26,91`); blocks `iiex:mpenergy-shaft-{ns,we,ud}` (`BlockCastIronShaft.cs:31,39-40`), `iiex:mpenergy-bevel-*` (`IiexBlocks.g.cs:1599`), `iiex:mpenergy-flywheel-{normal,large}-*`, `iiex:mpenergy-transmission-{x2,x4,clutch}-*` (`:1621,:1679`). No pinion exists.

`ExIngredients.Gear(code, qty)` `exlib/Definitions/ExIngredients.cs:54-57` (no metal capture). Consumers: iiex `MachineRecipeDefinitions.cs` loop over `game:gear-rusty`/`iiex:gear-*` `:23-27` - Watt x2 `:74` (pattern `_H_,PRP,PIP` `:70` still has no `G` cell, so the gear is free), fluid pump x1 `:85`, manual pump x2 `:97`, MP generator x2 `:111`; `CastPipeRecipeDefinitions.cs` valve x2 `:56/:76`, pressure valve x2 `:66/:86`; siex `EngineRecipeDefinitions.cs:28` x2, `HotBlastFurnaceRecipeDefinitions.cs:48` x4, `MachineRecipeDefinitions.cs:43` x4, `ConverterRecipeDefinitions.cs:54` x16. Exact-code consumers: transmission RCC stage 2 `iiex:spurgear` x2 + 4 iron ingots (`BlockTransmission.cs:60-70`), stage 3 2 ingots (`:72-78`); flywheel x1 spurgear (`EnergyRecipeDefinitions.cs:76-79`); riveter x1 spurgear + `castplate-heavy` (`FormingRecipeDefinitions.cs:111-115`); Bessemer spawn `iiex:largegear-{iron|steel}` (`...ConverterControl.Peripherals.cs:205-208`).

Routes: spurgear chisel + 2 `game:ingot-iron` -> 1 `:29-36`; chisel + 1 `iiex:ingot-castiron` -> 2 `:38-45`. Smithing (`Recipes/Smithing/GearRecipeDefinitions.cs`): `2gears-{metal}` `:37-51`, `4gears-{metal}` `:52-66`, `largegear-{metal}` `:67-98`, from `game:ingot-*` iron|steel `:15-21`. There is no `SmithingRecipeDefinitions` class; smithing providers are Gear/Pig/Shingling/Blister. Bevel assembly: using `iiex:bevelgear` on a shaft swaps in `mpenergy-bevel-{orientation}` and takes one gear (`BlockCastIronShaft.cs:63-88`, `slot.TakeOut(1)` `:85`); the item has no recipe (`EnergyRecipeDefinitions.cs:10-12`).

## 5. Tooling

`MachineTool` `exlib/Processes/MachineTool.cs:23`: key `"machinetool"` `:25`, `TryParse -> tier` `:35-53`, `IsTool` `:56`, `TierOf` (-1 when none) `:64`, `Tier(int)` `:71`, `Itemtype(domain, code, tiers, shape="game:item/ingot", variantGroup="type")` `:85-104` - sets `MaxStackSize(1)` `:101` and **no durability**. Sole implementer: `ShearBladeItemDefinitions.cs:13` - `shearblade-{iron:1, steel:2}` `:19-22`, shape `iiex:item/shearblade`, group `metal` `:26-34`. Recipe: 4 x `game:metalplate-*` (`Named("metal","iron","steel")`) + hammer -> `shearblade-{metal}` (`FormingRecipeDefinitions.cs:154-167`); cost row `shearblade-grid` `IiexRecipeConfig.cs:165`; gate `bladeTier < job.MinTier -> BladeTooSoft` (`ShearFeed.cs:107-108`). Wear: none built - `tooling-wear.md:3` "ruled 2026-08-05 - nothing built".

`ItemDie` `exlib/Processes/ItemDie.cs:18`: key `"machinejob"` `:20`, `Job(machine, input, output, count, minTier, minTorque, seconds)` `:71-97`, `Itemtype(domain, jobs, shape, shapeByType)` -> code `die` `:108-134`. Shipped `BenchDieItemDefinitions.cs:19`: `iiex:die-nail` (`nailcutter`: `iiex:nailplate` -> `game:metalnailsandstrips-iron` x4, torque 0.2, 2 s `:47-54`) and `iiex:die-rivet` (`riveter`: `iiex:rivetrod` -> `iiex:rivet` x2 `:55-62`, `RivetsPerRod` `FastenerItemDefinitions.cs:26`); recipes 2 plate + hammer `FormingRecipeDefinitions.cs:130-144`; cost row `die-grid` `:170`.

"Forged and tempered" today maps to **nothing vanilla**: blades and dies are grid crafts from plate whose captured metal is the tier; `temper`/`quench` occur nowhere in `src/` except the `ProcessJob.MinTier` docstring (`ProcessJob.cs:20`), and no `ItemWorkItem` recipe produces a tool. `MetalToolEmitter` (`exlib/Metals/MetalToolEmitter.cs:20`) emits the vanilla tool set (pickaxe/axe/shovel/hammer/saw/knife/chisel/scythe) from presets `brittle (150 dur, tier 4)`, `standard (1000, tier 4)`, `good (2600, 2.5, tier 5, 9.0)` `:54-59`, resolved by `ResolveStats` `:64-83`; `MetalToolSpec {Preset, Durability, AttackPower, MiningTier, ToolTypes}` `MetalDef.cs:110-125`. Configs: crucible steel `tools: {preset: "good", durability: 3300}` (`assets/iiex/config/metals/cruciblesteel.json:13-16`), cast iron `brittle` (`castiron.json:13-15`), Bessemer `good` (`siex/config/metals/bessemersteel.json:13-15`). Generated metals are kept off vanilla's `block/metal` worldproperty precisely so no anvil `workitem-<metal>` appears (`MetalFamilyEmitter.cs:15-16`); how a `cruciblesteel` tool is obtained in survival (tool mold vs other) - UNVERIFIED. `MetalRegistry` exposes no tool accessor (`MetalRegistry.cs:30-134` is molten/cast lookups).

## 6. Cast pipe

`CastPipeDefinitions.cs:16-30` -> `BlockPipe.Segments(domain, BlockPipe.CastTier)` `:19` gives `iiex:pipe-cast-{straight,bend,tjunction,xjunction}-{orientation}`, plus passthroughs `:24-28`. B19 stands: `CastPipeRecipeDefinitions.cs:20-88` outputs only passthrough/passthroughbend/outlet/valve/pressurevalve, every one from `iiex:pipe-plated-straight-*` (`:113-121`). Dead cost keys `pipe-cast-{straight,bend,tjunction,xjunction}-grid` with cheap output 4/2/2/2 at `IiexRecipeConfig.cs:187-190` (cast-pipes.md cites `:72-79` - drifted). Design: STATE.md B19 (`docs/internal/plans/STATE.md:27`) and cast-pipes.md:176-184 - "cast segments assembled from cast pipe-parts bored on the boring machine"; no pipe-part item; boring-machine.md:158 designs "cylinder blank + (pipe-part schematic) -> cast pipe-parts". The `item-cylinder-pipesegment.json` those pages cite is not among the machined shapes; not located - UNVERIFIED. Diagrams: `iiex:diagram-{pipe-straight,pipe-bend,pipe-tjunction,pipe-xjunction}` exist (`DiagramItemDefinitions.cs:23-26`) and nothing consumes them. `.Tool()` sets `isTool: true` (`GridRecipeBuilder.cs:135-138`); used by flywheel diagrams (`EnergyRecipeDefinitions.cs:68,90`), canal diagrams (`DiagramRecipeDefinitions.cs:24,32`), pattern diagram + knife (`PatternRecipeDefinitions.cs:23-24`), hammer/chisel (`ExIngredients.cs:16,20`).

## 7. Recipe plumbing

`ExItemDef` `exlib/Definitions/ExItemDef.cs:19`: `Create(domain, code)` `:34`, `Class<T>` `:61`, `MaxStackSize` `:72`, `MaterialDensity` `:75`, `Shape` `:98`, `ShapeSelectiveElements` `:107`, `Texture(key, ...)` `:115`, `TextureAll` `:128`, `VariantGroup(code, states)` `:144`, `CreativeTab` `:163`, `CreativeCommon` `:170`, transforms `:179-187`, `CombustibleProps` `:196`, `Attribute(key, value)` `:206`, `Attributes(poco)` `:213`, `Raw` `:228-232`. `IExItemDefProvider` = `static abstract IEnumerable<ExItemDef> Definitions(string domain)` (`IExItemDefProvider.cs:15-18`), discovered by assembly scan `ExDefinitions.Discover<ExItemDef>(...typeof(IExItemDefProvider)...)` `ExDefinitions.cs:95` from `EntityRegistry.RegisterAll` (`EntityRegistry.cs:80`) - never instantiated.

`ExRecipeDef.Create(domain, category, name)` `ExRecipeDef.cs:30`; `.Grid(Action<GridRecipeBuilder>)` `:52`, `.Add`/`.Body` for smithing objects `:61,:77`. `GridRecipeBuilder` `GridRecipeBuilder.cs:12`: `Name :17, Pattern :24, Size :30, Ingredient(key, Func<IngredientBuilder,IngredientBuilder>) :40, Output :52, OutputBlock :65, OutputItem :69, Raw :74`; `IngredientBuilder :89`: `Item :94, Block :101, Named :109, Metal() :116, Quantity :119, Tagged :129, Tool() :136`.

Cost row: `RecipeCostEntry { Type "grid"|"rcc", Match wildcard, Profiles: Dictionary<string, RecipeProfileCost> }` (`exlib/Registries/Recipes/RecipeCostEntry.cs:12-26`); iiex catalogue `IiexRecipeConfig.Defaults()` `:63-197` via `Grid/Rcc/GridOut` `:45-58`; keys are player config - re-point `Match`, never rename (naming.md:354-356).

Handbook: `docs/iiex/handbook/NN-*.html` <-> `assets/iiex/config/handbook/NN-*.json` descriptor naming a `text` lang key, joined on the `NN-` prefix (`test/ExpandedLib.Testing/HandbookSync.cs:12-16,85-96`, `Problems` `:105+`); `EXLIB_WRITE_HANDBOOK=1` re-blesses `en.json` `:46-47`. Pages 00-12 ship; `10-formingshop` is the nearest home.

Job tables: `assets/{domain}/config/processjobs/*.json` (`ProcessJobLoader.CataloguePath` `:17`), schema `ProcessJobSet.TryParse` `ProcessJob.cs:70-134`: `{schema, machine, jobs:[{input, output, count, stage?, family?, minTorque, minTier, seconds}]}`; a whole-item job matches on input alone, first match wins (`:39-47`). Output resolves item first, then block (`BlockEntityMpBench.Resolve` `:129-141`), so block outputs (pipe segments, shafts) are legal in the bench family. Machine key is read off `Variant["type"]` on the benches (`BlockFastenerBench.cs:155`).

Guards that fire on new content: goldens `test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/{pattern,diagram,rollset,shearblade,die,spurgear,bevelgear,...}.json` and `recipes/grid/{pattern,diagram,die,flywheel,machines,pipes-cast,riveter,nailcutter,...}.json`, re-blessed scoped (`EXLIB_WRITE_GOLDENS=iiex/itemtypes/<name>`, `DefinitionGoldens.cs:144-147`; never wholesale, naming.md:358-360); `IiexRecipeOutputTests` (`:10-20`); `ReferencedCodes` (outputs, ingredients, RCC requires, def-body stacks including mold outputs, `ReferencedCodes.cs:15-45`); `ShippedCropTableTests` (shear.json rows, `:15-22` - each new processjobs file wants its twin); `LangCoverage`; `ProcessExtensionGuards.No_process_machine_names_a_product_in_code` (`:114`).

## Proposed job-table roster

Naming per naming.md N2/N5/N7 (`:82-91,157-169,187-212`): family-first, compounds squashed, no prefix collision. `iiex:blank` exists, so blanks must not be `blank-*`; propose one `castblank` itemtype with a `type` group (mirrors `caststock-{form}`), one `machined` itemtype for intermediates (Open 10, machining-line.md:398, still asks whether these are codes or states), tools `machinecutter-{metal}` / `drillbit-{metal}` via `MachineTool.Itemtype(..., variantGroup:"metal")` exactly as `shearblade`. Pattern types `cast{blank}` following `PatternShapes`. Masses: none settled (machining-line.md:392); derive from the impression at 2.5 u/vx^3 (patterns.md:341-343); the drawn roller blanks are 16-long against the cell's 12-voxel ceiling (casting.md:119) - may need `longcell`. Machine keys (`Machine` strings, machining-line.md:263): `lathe`, `bore`, `shaper`, `planer`, `drillpress`; `shear`, `nailcutter`, `riveter` exist.

| machine | input -> output x n | status |
|---|---|---|
| lathe | `iiex:castblank-roller` [mint; pattern `castrollers`] -> `iiex:rollset-flat` / `-grooved` / `-flatwide` x1 | outputs exist; !! one input, three outputs - `ProcessJob` has no schematic/selector field; either three blank types or the diagram-as-schematic row boring-machine.md:154 designs |
| lathe | `iiex:castblank-shaft` [mint; filling `cell-filling-axle`] -> `iiex:mpenergy-shaft-ns` x2 (block) | exists; already grid-craftable (`EnergyRecipeDefinitions.cs:112-119`) |
| lathe | `iiex:castblank-cylinder` [mint; filling exists] -> `iiex:machined-cylinder` [mint] | light cylinder; no consumer today |
| lathe | `iiex:castblank-cylinderheavy` [mint; no filling] -> `iiex:machined-cylinderheavy` [mint] | art `item-lathed-cylinderheavy` |
| lathe | `iiex:castblank-gearlarge` [mint; filling exists] -> `iiex:machined-gearcone` [mint] | art `item-lathed-gearblanklarge` (unnamed roots) |
| bore | `iiex:machined-cylinderheavy` -> `iiex:boredcylinder` [mint] | art `item-bored-cylinderheavy`; no recipe consumes it - Watt/pump recipes must be re-authored to take it |
| bore | cast pipe-part -> `iiex:pipe-cast-{straight,bend,tjunction,xjunction}-*` (blocks, exist) | closes B19; same one-input/four-outputs problem; no pipe-part item or filling |
| shaper | `iiex:castblank-gearsmall` [mint; filling exists] -> `iiex:spurgear` x1 | reuses the existing "small machine gear" (flywheel, riveter, transmission RCC); makes the shaper cheaper than chisel + 2 ingots. If the design's "pinion" must be distinct, mint `iiex:piniongear` with no consumer |
| shaper | `iiex:machined-geardrilled` -> `iiex:gear-iron` x2 | reuses the line-item gear ten grid recipes take (`iiex:gear-*` uncaptured); material mismatch with the smithing route is cosmetic |
| shaper | `iiex:machined-gearcone` -> `iiex:bevelgear` x1 | exists; closes the "no bevel recipe" hole |
| planer | `iiex:castblank-gearlarge` -> `iiex:machined-gearfaced` [mint] | no faced-blank shape drawn |
| drill press | `iiex:machined-gearfaced` -> `iiex:machined-geardrilled` [mint] | art `item-drilled-gearblanklarge` |
| nail cutter | `iiex:nailplate` -> `game:metalnailsandstrips-iron` x4 | exists, on `die-nail` |
| riveter | `iiex:rivetrod` -> `iiex:rivet` x2 | exists, on `die-rivet` |
| cutter (shear) | five rows in `assets/iiex/config/processjobs/shear.json`: shingledbar@2.0 grooved -> `game:rod-iron` x4; beam@1.0 flat -> `game:metalplate-iron` x2; shingledslab@2.0 flatwide -> `iiex:heavyplate` x2; rod@1.0 grooved -> `iiex:rivetrod` x4; `game:metalplate-iron` -> `iiex:nailplate` x2 | exists |

Planer's slab/frame/bedplate jobs and the shaper's keyed shaft have no consumer code and no scale/swarf sink (machining-line.md:199-201, 391) - leave them out of the first table.
