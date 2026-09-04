# Definitions, Recipes & Config
**Status** live   **Mod** exlib (`ExpandedLib`) - every other mod is a consumer
**Owns** the code-first definition builders (`ExBlockDef` / `ExItemDef` / `ExRecipeDef` / `GridRecipeBuilder` / `IngredientBuilder`), how a def becomes a synthetic asset and when it is injected, the RCC `ConstructionStages` builder and the `brokenDropsRatio` resolution chain, the golden-file parity harness and its comparison rules, the recipe-cost catalogue (entry shape, level derivation, the `cheap` 0.5 factor, the apply pipeline), and the on-disk config layout (`ex_values.json` / `ex_recipes.json` sections, migrations, sanitising, ranges, the source-generated accessors).
**Depends on** [multiblock & filler structures](multiblock.md) (the ASCII layout DSL `ExBlockDef.MultiblockLayout` / `.FillerOffsets` feed, and the RCC-built mega-blocks that carry construction stages) · [repo status](../../internal/plans/STATE.md)

## Role

There are no hand-written `blocktypes/`, `itemtypes/` or `recipes/` JSON files left in the repo - `ls assets/*/blocktypes` and `ls assets/*/recipes` are both empty. Every block, item and recipe is authored in C# and injected as an in-memory asset before the vanilla object loader runs.

| Problem with JSON assets | What code-first does instead |
|---|---|
| A `class:` string desyncs when a C# class is renamed | `Class<T>()` resolves the registered key from the type - `ExBlockDef.cs:79` |
| A multiblock offset table is a coordinate pyramid nobody can read | ASCII layer diagrams, validated at build - `ExBlockDef.cs:796`, see [multiblock](multiblock.md) |
| A block's `AllowedOrientations` duplicates its variant group and drifts | derived from the def itself - `ExDefinitions.cs:63` |

Art stays in files. `Shape()`/`Texture()` take asset references, never inline geometry (`ExBlockDef.cs:20-24`).

Config covers the second half: gameplay numbers must be tunable by a server admin without a rebuild, must survive a mod upgrade that rebalances them, and must not be breakable by a bad hand edit.

## How it works

### 1. Authoring a def

Three builders, one shape. Each is a fluent wrapper that accumulates a `JObject` and hands it back through `IExDef.ToJson()` as a defensive clone (`ExBlockDef.cs:872`, `ExItemDef.cs:252`, `ExRecipeDef.cs:111`).

| Builder | Asset location it claims | Source |
|---|---|---|
| `ExBlockDef` | `{domain}:blocktypes/{assetName}.json` | `ExBlockDef.cs:72-73` |
| `ExItemDef` | `{domain}:itemtypes/{assetName}.json` | `ExItemDef.cs:64-65` |
| `ExRecipeDef` | `{domain}:recipes/{category}/{assetName}.json` | `ExRecipeDef.cs:62-63` |

`Create(domain, code)` names the asset after the code; the three-arg overload separates them, which is how several blocktype files share one `code` (the pipe family: `Create("iiex", "pipe", "pipes/straight")` - `ExBlockDef.cs:53-61`).

Only the schema subset the migrated content needs is typed. Everything else reaches the JSON through escape hatches, so nothing expressible in JSON is unrepresentable in C#: `Attribute(key, poco)` (`ExBlockDef.cs:665`), `Attributes(poco)` (`:675`), `AttributeByType` (`:817`), `RawByType` (`:832`), `Raw(key, token|poco)` (`:845`, `:850`).

`ExRecipeDef` is bimodal because the survival loaders accept both a recipe array and a lone recipe object, and the injected JSON mirrors whichever the recipe needs: `Grid`/`Add` accumulate into array mode (`ExRecipeDef.cs:68`, `:78`); `GridObject`/`Body` set single-object mode (`:87`, `:96`). Mixing the two throws (`:98-103`, `:113-120`).

`GridRecipeBuilder` emits `{ name, ingredientPattern, ingredients{}, width, height, output }` and omits every unset key (`GridRecipeBuilder.cs:13-86`). `IngredientBuilder` covers `Item`/`Block`/`Named`/`Quantity`/`Tool`/`Tagged`/`Raw`; `Metal()` is the shorthand for the ubiquitous `Named("metal", "iron", "steel")` capture (`GridRecipeBuilder.cs:123`).

### 2. Discovery

A class opts in by implementing `IExBlockDefProvider` / `IExItemDefProvider` / `IExRecipeDefProvider` and declaring a `public static IEnumerable<…> Definitions(string domain)` factory. `ExDefinitions.Discover` scans the assembly and invokes it (`ExDefinitions.cs:107-123`).

The lookup is `BindingFlags.DeclaredOnly` on purpose (`ExDefinitions.cs:155-161`): several blocks subclass a def-providing base (the special pipes extend `BlockPipe`), and without it a derived class would re-register the base's defs. A class contributes only the defs it declares itself.

The registries are keyed by `Location.ToString()`, so a re-register replaces rather than duplicates (`ExDefinitions.cs:19-29`).

### 3. Injection

`ExDefinitionModSystem` serialises every registered def and calls `api.Assets.Add` (`ExDefinitionModSystem.cs:30-72`).

| Fact | Value | Source |
|---|---|---|
| Side | server only | `ExDefinitionModSystem.cs:24` |
| `ExecuteOrder` | 0.04 | `ExDefinitionModSystem.cs:28` |
| Vanilla JSON-patch loader | 0.05 | `ExDefinitionModSystem.cs:15` (comment, verified against engine) |
| Vanilla `ModRegistryObjectTypeLoader` | 0.2 | `ExDefinitionModSystem.cs:14` |

0.04 sits below the patch loader, so an injected asset is still a legal patch target for other mods, and well below the object loader that consumes it. Recipe loaders run later still (they resolve codes the object loader just built), so one injection point carries all three categories (`ExDefinitionModSystem.cs:64-66`).

Payload bytes are `def.ToJson().ToString()` - the parameterless `ToString`, because the game's bundled Newtonsoft does not expose the `Formatting` overload at runtime (`ExDefinitions.cs:198-201`).

The generated metal families are registered inside `AssetsLoaded` from `config/metals/` read directly, not from `MetalRegistry`, because the registry is populated at `AssetsFinalize` - after this phase (`ExDefinitionModSystem.cs:38-46`).

### 4. RCC construction stages

`ExBlockDef.Construction(configure)` appends the block-entity behaviour `ExRightClickConstructable` with a typed `{ "stages": [...] }` properties blob (`ExBlockDef.cs:772-779`, `ConstructionStages.cs:36-42`).

A stage carries `addElements` / `removeElements` (which shape elements it reveals or hides) and any number of `requireStacks` (`ConstructionStage.AddElements` `:53`, `RemoveElements` `:59`, `Require` `:73`). `Require` accumulates, so one stage may need several materials.

Three shorthands cover the shape every steam/steel mega-block repeats - a metal-captured wildcard code, `storeWildCard: "metal"`, `allowedVariants: ["iron","steel"]`, and the conventional `{domain}:rcc-ingredient-{kind}` hint key (`ConstructionStages.cs:120-132`):

| Helper | Code | Hint key kind |
|---|---|---|
| `RequireMetalPlate` | `metalplate-*` | `metalplate` |
| `RequireMetalNails` | `metalnailsandstrips-*` | `nailsandstrips` |
| `RequireMetalRod` | `rod-*` | `rod` |

The behaviour itself is version-split. On 1.22 it is a thin subclass of vanilla `BEBehaviorRightClickConstructable` (`ExRightClickConstructable.cs:19-21`); on 1.20/1.21, where that type does not exist, it is a full reimplementation over `ExRightClickConstruction` (`:75-76`). Owning the JSON name on every version means mod C# references one type.

`GetConstructionDrops` works around a vanilla off-by-one: `rcc.GetDrops` loops `i < CurrentCompletedStage`, silently omitting the last built stage - the most expensive one on a mega-block. The wrapper advances the public counter by one across the call and restores it (`ExRightClickConstructable.cs:31-43`).

Salvage fraction resolution chain, evaluated live at break time so a `/exmod config` change applies immediately (`ExRightClickConstructable.cs:47-48`):

1. `ExRccSettings.BrokenDropsRatio(block.Code.Domain)` - a getter the owning mod registered (`ExRccSettings.cs:25-37`);
2. else the JSON `brokenDropsRatio`, default 1.0 on the legacy path (`ExRightClickConstructable.cs:79`, `:94`).

### 5. The goldens harness

Every def has a committed file at `test/{Mod}.Tests/goldens/{domain}/{Location.Path}`. A mod's parity test is then four thin cases (`IiexDefinitionGoldenTests.cs:28-66`):

1. each def reproduces its golden - `DefinitionGoldens.CheckGolden` (`DefinitionGoldens.cs:94`);
2. the golden set exactly covers the defs - missing (new def, no golden) and orphans (deleted def, stale golden) both empty (`:115`);
3. every shape reference resolves to a shipped file - `DefinitionAssets.MissingShapes` (`DefinitionAssets.cs:34`);
4. regenerate, a no-op unless opted into (`DefinitionGoldens.cs:149`).

`Collect` scans the mod assembly directly and never touches the process-wide `ExDefinitions` registry, so parity tests stay isolated and parallel (`DefinitionGoldens.cs:34-48`). Generated metal families have no provider class, so they are re-emitted from the same source-tree `config/metals/` JSON and filtered to the domain (`:53-77`). Goldens are read and written in the source tree, anchored by walking up to `VintageStory.sln` (`:166`, `:176-185`) - no build-output copy step.

Comparison is semantic, not textual (`DefinitionParity.cs:23-33`). Three normalisations, each matching something the engine does not distinguish:

| Normalisation | Rule | Source |
|---|---|---|
| Numbers | every integer/float coerced to `double` | `DefinitionParity.cs:56-57` |
| `attributes.multiblockStructure` | reduced to the set of `(x,y,z,block-code)` cells plus the set of block codes; `w`-numbering and offset order are engine-internal | `:42`, `:65-87` |
| `attributes.fillerOffsets` | reduced to cells sorted by x,y,z with `allowAttach` made explicit and hosted `behaviors` normalised (a cell's behaviours are part of its identity) | `:44`, `:92-123` |

Everything else stays order-sensitive, because those arrays are ordered in the schema (`variantgroups`, `behaviors`, construction stages).

`DefinitionAssets` covers what the goldens structurally cannot: a def can emit a stable but wrong `shape.base` forever. It checks mod-domain shape paths only (`game:` lives in the install, which a headless test has no business requiring - `DefinitionAssets.cs:44-47`), and treats a path with `{variant}` placeholders as a family - at least one file must match (`:64-76`).

### 6. Recipe-cost catalogue

A mod ships a curated list of the grid recipes and RCC blocks whose cost it wants tunable. It ships almost no numbers: the `normal` level is read off the live recipe at load, and every other level is scale-filled from it.

`ExRecipeProfiles.Apply` is the one pipeline every mod shares (`ExRecipeProfiles.cs:45-60`):

```
Reconcile(live, defaults)        repair a hand-edited file       ExRecipeCosts.cs:121
EnsureNormalExtracted(api, live) read normal off live recipes    ExRecipeCosts.cs:36
EnsureScaledLevel(live, l, f)    per derived level               ExRecipeCosts.cs:71
SaveCatalogue()                  server side only                ExRecipeProfiles.cs:56-57
Apply(api, live, GetLevel())     rewrite the live recipes        ExRecipeCosts.cs:212
```

- Grid recipes are matched by wildcard against the output code and edited in the live `api.World.GridRecipes` list; both `Ingredients` and `ResolvedIngredients` are rewritten, and setting an output count writes both `Output.Quantity` and `ResolvedItemStack.StackSize` (`ExRecipeCosts.cs:243-304`).
- RCC blocks are matched by block code, and their stages are rewritten on the behaviour-properties JSON, which the vanilla behaviour re-parses per construction (`ExRecipeCosts.cs:331-413`).

`Reconcile` is the anti-footgun pass: it restores a deleted entry wholesale, forces `Type`/`Match` back to the code defaults (those are not meant to be edited), restores a pinned default profile or output quantity a player removed, and clamps every quantity to ≥ 1 - while keeping player-set numbers and player-added entries (`ExRecipeCosts.cs:121-208`).

The switch is `/exmod recipes [<mod> [<level>]]`, server-side (`RecipesSubCommand.cs:10-33`). It writes `<Mod>Config.RecipeLevel` and applies on the next world reload.

### 7. Config

One physical file per purpose, sectioned by mod id. `ExConfigDocument` owns a `JObject` whose top-level keys are mod ids; each `ExConfigRegister<T>` reads and writes only its own section (`ExConfigDocument.cs:26-90`). The document is cached per `ICoreAPI` instance via a `ConditionalWeakTable` - in game all mods share one API and therefore one document loaded once; each headless test has its own fake API, so the cache isolates itself with no global reset (`:28-31`, `:45-57`).

`Load` is a fixed sequence (`ExConfigRegister.cs:67-86`):

```
ForFile(api, fileName)  →  FoldLegacy(modId, legacyNames)  →  GetSection<T>(modId) ?? new T()
   →  ApplyMigrations(config, runningVersion)  →  Sanitize(config)  →  config.ConfigVersion = running  →  Save()
```

so the file is created on first run and gains newly added keys on every update.

`Sanitize` resets any numeric property that is NaN/infinite or outside its range, and any reference-typed property the player nulled out whose coded default is non-null, then logs what it reset (`ExConfigRegister.cs:96-139`). `ApplyMigrations` fires each `ExConfigMigration` whose `ToVersion` falls in the gap between the file's stamped version and the running build, oldest first (`:143-208`).

The accessors are generated. `[ExConfigRegister(fileName, modId)]` makes `ExConfigGenerator` emit a `static partial` class - `IiexConfig` → `IiexValues` - carrying `ConfigFileName`, the backing store, `Load(ICoreAPI)`, `Edit(Action<T>)`, `Save()`, and one read-only static property per config value (`ExConfigGenerator.cs:116-231`). A static `Migrations` member on the POCO is forwarded automatically (`:81-82`, `:144-145`). `Manageable = true` additionally registers the store with `ExConfigProfiles` from the generated `Load`, exposing its simple-typed values to `/exmod config` (`:176-186`).

## Numbers

### Config file layout — the shipped registrations

| Config class | Attribute file | Section | Manageable | Legacy names folded | file:line |
|---|---|---|---|---|---|
| `ExlibConfig` | `ex_values.json` | `exlib` | yes | `exlib_values.json` | `ExlibConfig.cs:17-22` |
| `IiexConfig` | `ex_values.json` | `iiex` | yes | `iwex_values.json`, `lpex_values.json`, `lpex.json` | `IiexConfig.cs:23-33` |
| `SiexConfig` | `ex_values.json` | `smex` | yes | `smex_values.json`, `smex.json` | `SiexConfig.cs:13-18` |
| `SiexConfig` | `ex_values.json` | `hpex` | yes | - | `SiexConfig.cs:23` |
| `IiexRecipeConfig` | `ex_recipes.json` | `iiex` | no | `iwex_recipes.json`, `lpex_recipes.json` | `IiexRecipeConfig.cs:22-26` |
| `SiexRecipeConfig` | `ex_recipes.json` | `smex` | no | `smex_recipes.json` | `SiexRecipeConfig.cs:15-19` |
| `SiexRecipeConfig` | `ex_recipes.json` | `hpex` | no | - | `SiexRecipeConfig.cs:17` |

Both files live under the game's `ModConfig` folder. Folding a legacy file renames it to `<name>.migrated` rather than deleting it, so the carry-over is reversible (`ExConfigDocument.cs:99-139`).

### Framework constants

| Key | Value | file:line | What it does |
|---|---|---|---|
| `ExRecipeCosts.ProfileNormal` | `"normal"` | `ExRecipeCosts.cs:27` | the auto-extracted baseline level name. Hard-coded const |
| `ExRecipeCosts.RccBehaviorName` | `"ExRightClickConstructable"` | `ExRecipeCosts.cs:29` | which behaviour carries the editable `stages`. Hard-coded |
| `RecipeProfile.Levels` | `["normal", "cheap"]` | `RecipeProfile.cs:37` | selectable level names, display order; first is the baseline. Overridable per registration, not a config key |
| `RecipeProfile.DerivedLevels` | `{ "cheap": 0.5 }` | `RecipeProfile.cs:41-42` | the `cheap` profile is half cost, scale-filled from `normal`. Overridable per registration, not a config key |
| scale floor | `Math.Max(1, Round(v * f))` | `ExRecipeCosts.cs:235-236` | a derived level can never make an ingredient free |
| clamp floor | `1` | `ExRecipeCosts.cs:184-208` | every player-edited quantity is clamped on load |
| `RecipeCostEntry.Type` default | `"grid"` | `RecipeCostEntry.cs:16` | the only other accepted value is `"rcc"` (`:238-239`) |
| default numeric range | `[0, +∞)` | `ExConfigRegister.cs:455-459` | a tunable with no `[ExConfigRange]` must still be non-negative and finite |
| `ExDefinitionModSystem.ExecuteOrder` | `0.04` | `ExDefinitionModSystem.cs:28` | injection phase. Hard-coded |
| `EXLIB_WRITE_GOLDENS` | `"1"` | `DefinitionGoldens.cs:160-161` | env var that opts into golden regeneration |
| repo-root marker | `VintageStory.sln` | `DefinitionGoldens.cs:179` | how the harness finds the source tree to read/write goldens in place. Hard-coded |

### Per-mod recipe-level plumbing

| Mod | `RecipeLevel` default | Catalogue entries | RCC salvage ratio registered | file:line |
|---|---|---|---|---|
| iiex | `"normal"` | 14 (all grid) | none | `IiexConfig.cs:412`; `IiexRecipeConfig.cs:46-71` |
| iiex | `"normal"` | 19 (2 rcc, 17 grid) | `0.8` | `IiexConfig.cs:224`, `:116`; `IiexRecipeConfig.cs:58-86` |
| smex | `"normal"` | 10 | `0.8` | `SiexConfig.cs:265`, `:250`; `SiexRecipeConfig.cs:45-71` |
| hpex | `"normal"` | 4 | `0.8` | `SiexConfig.cs:131`, `:124`; `SiexRecipeConfig.cs:43-57` |

The only pinned cost numbers anywhere are iiex's doubled cheap pipe outputs - straight 2 → 4, bend/T/X 1 → 2 (`IiexRecipeConfig.cs:46-53`, `:76-79`). Everything else in every `cheap` profile is `normal × 0.5`, computed at load.

### Golden coverage

| Suite | Goldens | Root |
|---|---|---|
| `ExpandedLib.Tests` | 1 | `test/ExpandedLib.Tests/goldens/exlib/` |
| `IronIndustryExpanded.Tests` | 103 | `test/IronIndustryExpanded.Tests/goldens/iiex/` |
| `IronIndustryExpanded.Tests` | 22 | `test/IronIndustryExpanded.Tests/goldens/iiex/` |
| `SteelmakingExpanded.Tests` | 29 | `test/SteelIndustryExpanded.Tests/goldens/siex/` |
| `HighPressureExpanded.Tests` | 7 | `test/SteelIndustryExpanded.Tests/goldens/siex/` |

Counts are of committed `*.json` files under each domain root as of 2026-07-29; they are not asserted anywhere, only the missing/orphan sets are.

## Code

| Type | Where | Key members |
|---|---|---|
| `ExBlockDef` | `src/ExpandedLib/Definitions/ExBlockDef.cs:32` | `Create` `:50`/`:57` · `Class<T>` `:78` · `Multiblock` `:784` · `MultiblockLayout` `:796` · `FillerOffsets` `:712` · `Construction` `:778` · `VariantStates` `:861` · `ToJson` `:872` |
| `ExItemDef` | `ExItemDef.cs:29` | same shape, leaner; `CombustibleProps` `:197` · `GrindingProps` `:202` |
| `ExRecipeDef` | `ExRecipeDef.cs:31` | `Grid` `:68` · `Add` `:78` · `GridObject` `:87` · `Body` `:96` · `Count` `:107` |
| `GridRecipeBuilder` / `IngredientBuilder` | `GridRecipeBuilder.cs:13` / `:92` | `Pattern` `:27` · `Size` `:33` · `Ingredient` `:46` · `Output` `:56` · `Metal()` `:123` · `Tagged` `:138` |
| `ConstructionStages` / `ConstructionStage` | `ConstructionStages.cs:13` / `:46` | `Stage` `:20` · `BrokenDropsRatio` `:30` · `Require` `:73` · `RequireMetal*` `:104`–`:115` |
| `ExDefinitions` | `ExDefinitions.cs:17` | `DiscoverAndRegister*` `:83`/`:91`/`:99` · `OrientationMap` `:63` · `Build*Assets` `:172`–`:186` |
| `ExDefinitionModSystem` | `ExDefinitionModSystem.cs:20` | `AssetsLoaded` `:30` |
| `ExRightClickConstructable` | `Blocks/Construction/ExRightClickConstructable.cs:20` (1.22) / `:76` (legacy) | `GetConstructionDrops` `:31` · `OnBlockBroken` `:55` |
| `ExRccSettings` | `Blocks/Construction/ExRccSettings.cs:15` | `RegisterBrokenDropsRatio` `:25` · `BrokenDropsRatio` `:34` |
| `ExRecipeCosts` | `Registries/Recipes/ExRecipeCosts.cs:23` | `EnsureNormalExtracted` `:36` · `EnsureScaledLevel` `:71` · `Reconcile` `:121` · `Apply` `:212` |
| `ExRecipeProfiles` | `Registries/Recipes/ExRecipeProfiles.cs:13` | `Register` `:21` · `ApplyAll` `:33` · `Apply` `:45` |
| `RecipeProfile` / `RecipeCostEntry` / `RecipeProfileCost` | `Registries/Recipes/RecipeProfile.cs:11` / `RecipeCostEntry.cs:12` / `RecipeProfileCost.cs:12` | the catalogue data model |
| `ExConfigRegister<T>` | `Registries/Config/ExConfigRegister.cs:24` | `Load` `:67` · `Save` `:213` · `Sanitize` `:96` · `Set` `:270` |
| `ExConfigDocument` | `Registries/Config/ExConfigDocument.cs:26` | `ForFile` `:45` · `GetSection` `:64` · `FoldLegacy` `:99` · `Flush` `:90` |
| `ExConfigRegisterAttribute` | `Registries/Config/ExConfigRegisterAttribute.cs:29` | `AccessorName` `:48` · `LegacyFileNames` `:54` · `Manageable` `:61` |
| `ExConfigGenerator` | `src/ExpandedLib.Generators/ExConfigGenerator.cs:21` | `Emit` `:116` · `DefaultAccessorName` `:227` |
| `DefinitionGoldens` / `DefinitionParity` / `DefinitionAssets` | `test/ExpandedLib.Testing/` | `CheckGolden` · `CheckCompleteness` · `Equal` · `MissingShapes` |

Where a caller hooks in. A mod's `ModSystem.Start`, in this order (`IronIndustryExpandedModSystem.cs:40-60` is the canonical example):

```csharp
IiexValues.Load(api);                      // tunables first - before any BE is constructed
IiexRecipeValues.Load(api);                // then the catalogue
ExRecipeProfiles.Register(new RecipeProfile {
    Code = Mod.Info.ModID,
    Catalogue     = () => IiexRecipeValues.Recipes,
    Defaults      = IiexRecipeConfig.DefaultCatalogue,
    GetLevel      = () => IiexValues.RecipeLevel,
    SetLevel      = level => IiexValues.Edit(c => c.RecipeLevel = level),
    SaveCatalogue = IiexRecipeValues.Save,
});
```

Defs are picked up by `EntityRegistry.RegisterAll`, which calls the three `ExDefinitions.DiscoverAndRegister*` passes. exlib then runs `ExRecipeProfiles.ApplyAll` from both `StartClientSide` (`ExpandedLibModSystem.cs:107`) and `StartServerSide` (`:117`), after every mod's `Start`.

## Gotchas

Every tunables config's own `<summary>` names a file that does not exist. The doc comment says a per-mod file; the attribute two lines below says the shared one:

| File | Comment claims | Attribute says |
|---|---|---|
| `ExlibConfig.cs:7` (and `:13`) | `ModConfig/exlib_values.json`, "each mod's own config (`lpex_values.json` etc.)" | `ex_values.json` (`:18`) |
| `IiexConfig.cs:11` (and `:545`) | `ModConfig/iwex_values.json`, "retune freely in `iwex_values.json`" | `ex_values.json` (`:15`) |
| `IiexConfig.cs:8` | `ModConfig/lpex_values.json` | `ex_values.json` (`:17`) |
| `SiexConfig.cs:9` | `ModConfig/smex_values.json` | `ex_values.json` (`:14`) |
| `IiexRecipeConfig.cs:8` | `ModConfig/lpex_recipes.json` "alongside the main `lpex_values.json`" | `ex_recipes.json` (`:17`) |
| `SiexRecipeConfig.cs:8` | `ModConfig/smex_recipes.json` | `ex_recipes.json` (`:16`) |

`IiexRecipeConfig.cs:9`, `SiexConfig.cs:8-9` and `SiexRecipeConfig.cs:8-9` are correct. The same stale names appear in the mod-system comments: `IronIndustryExpandedModSystem.cs:43`, `IronIndustryExpandedModSystem.cs:26` and `:33`, `SteelIndustryExpandedModSystem.cs:55` and `:63`. These are the only names anyone reads when hunting a config file; treat every `*_values.json` / `*_recipes.json` in a comment as a legacy name, not a live path.

Two configs advertise commands that do not exist. `IiexRecipeConfig.cs:13` says `/exmod steam <level>` and `SiexRecipeConfig.cs:12` says `/exmod steel <level>`. The generic command is `/exmod recipes <mod> <level>` (`RecipesSubCommand.cs:10`). `RecipesSubCommand.cs:14` is itself half-stale - it says the numbers live in "each mod's `*_recipes.json`".

"byte-faithful" is not what the goldens check. `DefinitionGoldens.cs:19` calls a golden "the byte-faithful record of the JSON it injects", `ExRecipeDef.cs:12` says the injected JSON is "byte-faithful", and `ExRecipeDef.cs:67` and `GridRecipeBuilder.cs:11` speak of byte-for-byte parity with the hand-written form. `DefinitionParity` is semantic (`DefinitionParity.cs:9-21`): key order, number type, multiblock cell order and filler cell order are all normalised away. A change that reorders multiblock offsets will pass; that is intended, but the comment misleads.

A `cheap` profile can silently stop matching. RCC stage costs are keyed by stage index as a string, and within a stage by the ingredient's `name` - the lang key - falling back to its `code` (`ExRecipeCosts.cs:364-367`, `:400-409`). Renaming an `rcc-ingredient-*` hint key, or inserting a stage, orphans the player's saved numbers without any error. `Reconcile` cannot repair it, because it only repairs `Type`/`Match` and pinned defaults.

`EnsureNormalExtracted` must run before `Apply`. `Apply` mutates the live recipes, so extracting afterwards would record the applied numbers as the baseline and bake a discount in permanently (`ExRecipeCosts.cs:33-35`). `ExRecipeProfiles.Apply` orders it correctly (`:49-52`); anything calling `ExRecipeCosts` directly must too.

The client runs the apply pass but must not persist it. `ExRecipeProfiles.Apply` gates `SaveCatalogue` on `api.Side == Server` (`:56-57`) - in single-player both sides share the file, and a client write clobbers the server's extracted grid entries.

Tool ingredients are not tunable. `ReadGrid` skips anything with `IsTool` (`ExRecipeCosts.cs:259-265`), so a hammer slot never appears in a profile and its durability cost cannot be discounted.

A config value that may legitimately be negative needs an explicit range. With no `[ExConfigRange]` the accepted range is `[0, +∞)` (`ExConfigRegister.cs:455-459`), so a below-freezing ambient would be reset to its default on every load. `IiexConfig.RollingAmbientC` carries `[ExConfigRange(-50, 500)]` for exactly this reason (`IiexConfig.cs:536`).

Legacy folding runs once and only when the section is absent. `FoldLegacy` returns immediately if `HasSection(modId)` (`ExConfigDocument.cs:100-106`). Once a mod has written its section, an old per-mod file on disk is ignored forever - deleting the section to "reset" will re-fold a stale file.

A corrupt shared file does not take out every mod. `LoadOrEmpty` copies the file to `<name>.corrupt` and starts from an empty document, so each section independently falls back to coded defaults (`ExConfigDocument.cs:141-160`).

Injection is server-only. `ShouldLoad` returns false on the client (`ExDefinitionModSystem.cs:24`) - the client receives resolved block/item types over the network. Client-side code must never expect to `api.Assets.TryGet` a synthetic `blocktypes/` asset.

Only declared defs are discovered. A class that inherits `IExBlockDefProvider` from a base but declares no `Definitions` of its own contributes nothing (`ExDefinitions.cs:155-161`). This is correct, but it means adding a def to a base class does not give it to subclasses.

Author model-transform decimals as `double`, not `float`. `0.32f` widens to a different `double` than the JSON-parsed `0.32` and breaks parity (`ExItemDef.cs:24-27`, `ExBlockDef.cs:133-137`). `ExBlockDef.WalkSpeedMultiplier` takes `double` for this reason alone.

The regeneration test is green whether or not it does anything. `Regenerate_goldens_when_requested` is a no-op unless `EXLIB_WRITE_GOLDENS=1` (`IiexDefinitionGoldenTests.cs:61-66`). A passing suite proves nothing about regeneration.

iiex registers no RCC salvage ratio. iiex, smex and hpex each call `RegisterBrokenDropsRatio` with 0.8; iiex does not, so any iiex-domain RCC block would fall through to the JSON `brokenDropsRatio` (default 1.0 on legacy). Today iiex ships no RCC entries in its catalogue, so this is latent rather than live.

## Open

- No alternate-balance numbers ship. Every `cheap` value except iiex's four pinned pipe outputs is `normal × 0.5` derived at load, so the profile is a uniform discount rather than a designed second balance. Tuning it means editing the generated file, and nothing re-derives it afterwards (`EnsureScaledLevel` skips any entry that already has cost data - `ExRecipeCosts.cs:87-92`).
- Only grid and RCC are managed. `IsRcc` is a two-way branch (`ExRecipeCosts.cs:238-239`); smithing, clayforming, barrel and knapping recipes have no cost switch at all.
- `ExRecipeDef` has a builder only for grid recipes. Every other category goes through `Add(poco)` / `Body(poco)` untyped (`ExRecipeDef.cs:76-104`), so those recipes get no compile-time shape checking and no schema help.
- `ExConfigRegister` exposes only scalars to `/exmod config`. `IsEditableType` accepts string/bool/int/long/float/double (`ExConfigRegister.cs:316-322`); collection tunables - `IiexConfig.BurdenProfiles`, every recipe catalogue - are file-edit-only.
- No test asserts golden counts. Completeness is symmetric (missing ∪ orphans = ∅), which catches a deleted or unmigrated def but not a def family that silently stopped being emitted on both sides.
- `ExConfigDocument` has no schema version. The document itself carries none; only each section does, so a future change to the sectioning has nothing to migrate on.
