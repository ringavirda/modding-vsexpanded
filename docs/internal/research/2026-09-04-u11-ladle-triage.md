# Research snapshot - u11-ladle-triage

**Written** 2026-09-04 by a read-only research agent, against commit `cf2e8c89` (the tree before the
line-ending renormalisation). **Covers** the U11 ladle unit verified task by task against today's source; the art measured; layouts verbatim; executable task list.
**Purpose** grounding for the roadmap's Phase 1 walkthrough and Phase 2 machining-line plan
(`docs/internal/plans/2026-09-04-*.md`).

Line numbers were right on that day and drift; cite by symbol when reusing. "UNVERIFIED" marks are the
agent's own and were not re-checked. Nothing here is a decision - decisions live on the design pages.

---

# U11 ladle triage - plan vs source, 2026-09-04

Plan unit: `docs/internal/plans/2026-08-04-iwex-u2-u10-expansion.md:2596` (tasks U11.1-U11.7 at `:2782`, `:2817`, `:2837`, `:2858`, `:2898`, `:2927`, `:2939`; gate `:2993`). Design page `docs/design/machines/ladle.md` is **modified and uncommitted** (`git diff --stat`: 17+/7-).

## 1. Task-by-task verification

### U11.1 - "exlib: let a filler cell declare its own collision boxes" - **DONE (by the shear/boiler work)**
Plan says: add `Cuboidf[]? CollisionBoxes` to `FillerCellSpec`, emit `collisionBoxes` from `SerializeFillerCells`, add `FillerLayoutBuilder.Partial(char, params Cuboidf[])`, round-trip test.

- `FillerCellSpec(... IReadOnlyList<Cuboidf>? CollisionBoxes = null, ...)` exists - `src/ExpandedLib/Blocks/Structures/StructureFootprint.cs:56-65`.
- `FillerSlab.Half(BlockFacing)` gives the six half-cell cuboids - `StructureFootprint.cs:72-84` (WEST = `Cuboidf(0,0,0,0.5,1,1)`, DOWN = `(0,0,0,1,0.5,1)`).
- The builder verb is **`Slab(char symbol, BlockFacing half)`**, not `Partial` - `src/ExpandedLib/Blocks/Structures/FillerLayoutBuilder.cs:88-92`.
- `SerializeFillerCells` emits `collisionBox` (one box) / `collisionBoxes` (several) - `src/ExpandedLib/Definitions/ExBlockDef.cs:840-846`. Note it chose the **singular** form for one box, the opposite of the plan's Step 3; `StructureFillers.ReadBoxes` accepts both (`StructureFillers.cs:131`).
- Shear uses it: `.Slab('_', BlockFacing.DOWN)` - `src/IronIndustryExpanded/BlockStructures/Forming/Blocks/BlockShear.cs:78`; Cornish boiler: `.Slab('_'/'M', DOWN)` - `BlockBoilerCornish.cs:117-118`.
- Round-trip tests exist: `test/ExpandedLib.Tests/Structures/StructureFillerBoxesTests.cs:34-139` (parse, rotate, save-tree round trip).
- Stale plan citation: "`ExBlockDef.cs:859-892` emits only x/y/z/behaviors/allowAttach" - no longer true.

### U11.2 - "Export both shapes" - **NEEDS-ART (partly)**
- Only one editable exists: `assets/editable/shapes/networks/molten/molten-megablock-laddle.json` (moved from the root in commit `2c5c9381`). `molten-block-laddlecore.json` "has never been in git" - `ladle.md:199`.
- `convert-shape.py` resolves `EDITABLE/<name>.json` (`scripts/tools/convert-shape.py:33`, `:240`), so the argument must be `networks/molten/molten-megablock-laddle` (subdir invocation UNVERIFIED by example, but the join admits it).
- Target dir `assets/iiex/shapes/molten/` exists (`barrel-cast.json`, `barrel-plated.json`, `canal/`, `molds/`).
- Clip rewrite today: `holds(name)` -> `Hold` for `HOLD_CLIPS` {connected, disconnected, drill-down-pose, leaverdown, steamup} or suffix `open`; else **`Repeat`** + `close_loop` (`convert-shape.py:115-133`, `:254-261`). `poursouth`/`nournorth` are neither, so they would ship as `Repeat`; being single-keyframe, `close_loop` returns early (`:151-171`) and they render as a static pose either way. The plan's Step 3 wording ("must keep EaseOut") contradicts the script's own rule (EaseOut on an RCC-suppressed mesh makes it vanish, `:15-16`); the right move is adding the two pour clips to `HOLD_CLIPS`, leaving `idle` on Repeat (`:147`).
- `nournorth` typo still in the file (animations: `idle`, `poursouth`, `nournorth`).

### U11.3 - `BlockLadleCore` - **READY**
Nothing exists (`grep -ri ladle src/` -> only comments; `git status` has no ladle files). Suggested home `src/IronIndustryExpanded/BlockStructures/Ladle/` (family dirs: `Boiler/ Casting/ Forming/ Furnaces/ ...`). Precedents: generic down-slab shape `game:block/basic/slab/slab-{rot}` (`SlagBrickDefinitions.cs:78`); the cupola core's orientation marker/type-label faces are now at `BlockCupolaFurnaceCore.cs:45-55` (plan cited `:52-62`); the canal family already carries a `brick(fire|black|brown|cream|gray|orange|red|tan)` variant group (`Generated/IiexBlocks.g.cs:1137`).

### U11.4 - the 5x5x4 layout - **READY, with corrections**
- `VanillaCodes.AnyBricksOrAir`: **missing**. Neighbours: `Air` `:20`, `AnyBricks` `:74-75`, `AnySlab(facing)` `:111`, `AnyStairs(half, facing)` `:135`, `CoalBed = "@(air|coalpile)"` `:192` - all `src/ExpandedLib/Definitions/VanillaCodes.cs`.
- Socket idiom: a bare `@(...)` is implicitly `game:` (`VanillaCodes.cs:30-32`); mod blocks need the `*:` prefix and inside `@()` the body is a **regex** (`hearthmetal-.*`) - `src/IronIndustryExpanded/IiexCodes.cs:16-36` (`ChargeShaft`, `HearthCell`), pinned by `test/IronIndustryExpanded.Tests/Definitions/IiexCodesHearthCellTests.cs:14-41`. Canal codes: `iiex:molten-canal-{start|straight|tap}-{brick}-{orientation}` (`IiexBlocks.g.cs:1229-1316`), so e.g. `*:@(air|molten-canal-tap-.*-.*)`. The referenced-codes guard skips `*:`/`@(` codes (`test/ExpandedLib.Testing/MultiblockCodes.cs:17`, `:105-110`).
- `LegendAnyFacing` - `src/ExpandedLib/Definitions/MultiblockLayoutBuilder.cs:53`; `Legend` `:46`; duplicate-glyph refusal `"a glyph maps to one code"` `:108-111`; `Origin(xLeft, zTop)` `:34`. **New since the plan**: `Legend` now throws on a multi-letter node token (`RefuseNetworkToken`, `:137-155`), and single-letter pins are caught by `test/ExpandedLib.Testing/PinnedNetworkNodes.cs:22-84` - U10.6 landed. No shipped layout uses `LegendAnyFacing` yet (grep over iiex/siex: none).
- `ExCodes.Filler = ExlibBlocks.Structurefiller.Code` (`ExCodes.cs:15`) is the same code megablock fillers are placed with (`StructureFillers.cs:58-59`) - the cupola precedent is `.Legend('f', ExCodes.Filler)` with `# c f .` at layer 5 (`BlockCupolaFurnaceCore.cs:71`, `:127-134`).
- `BlockMoltenCanal : BlockNetworkNode` - `BlockNetworkMolten/Blocks/BlockMoltenCanal.cs:24`; `RecalculateAndSyncOrientations` is inherited from `BlockNetworkNode` (not in the canal file itself).

### U11.5 - `BlockLadle` megablock - **READY, with corrections**
- `BlockFilledMegastructure` exists: abstract `StructureAngle`, `CanPlaceBlock`/`OnBlockPlaced`/`OnBlockRemoved` triad, `OnFootprintPlaced` hook - `src/ExpandedLib/Blocks/Structures/BlockFilledMegastructure.cs:18-80`. But every shipped RCC megablock (`BlockBoilerCornish.cs:17`, `BlockEngineWatt.cs:14`, `BlockSandCastingBed.cs:25`, `BlockBurdenmaker.cs:23`) is `partial class` with its own base; the shear re-implements the triad (`BlockShear.cs:114-146`).
- Stage builder: `ExBlockDef.Construction(Action<ConstructionStages>)` `ExBlockDef.cs:873`; `Stage`, `AddElements`, `Require(code, qty, name, type, storeWildCard)`, `RequireMetalPlate/RequireMetalNails/RequireRivets/RequireMetalRod` - `src/ExpandedLib/Definitions/ConstructionStages.cs:17`, `:46`, `:64-82`, `:93-118`. Example: `BlockBoilerCornish.cs:157-176`.
- `ExCodes.RefractoryTier(2)` **does not exist** - it is `VanillaCodes.RefractoryTier(int)` (`VanillaCodes.cs:37`). A stage ingredient takes a code + quantity, e.g. `s.Require(VanillaCodes.RefractoryTier(2), n)`.
- Plan's precedent claim is stale: the Bessemer lining stage is `Require("game:clay-fire", 12).AddElements("Root/InputLining")` (`BlockConverterBessemer.cs:138`), i.e. fire clay, the very thing ladle.md disqualifies.
- `lpex:boilercornish-n`/`lpex:enginewatt-n` -> `iiex:boilercornish` (`IiexBlocks.g.cs:23`), `iiex:enginewatt` (`:311`).
- Break path scar: `BlockConverterBessemer.OnBlockBroken` `:156-198` (fillers removed via `StructureFillers.RemoveFillers` `:195`), `GetDrops` override `:209`; `storeWildCard` lives in `ConstructionStages.cs:61-82`.
- `ExIngredients.Plate/Nails/Rod(int)` - `ExIngredients.cs:23`, `:32`, `:41`.
- Bath/pour parts: `MoltenCharge` `src/ExpandedLib/Metals/MoltenCharge.cs:17` (ladle.md says `:20`); `BEBehaviorMoltenCell` props `capacity/flowSource/drainFitting/solidifies/cooldownSpeed` - `src/ExpandedLib/Blocks/Structures/BEBehaviorMoltenCell.cs:58-62`, `PushMetal :167`, `DrainMetal :214`, `SoakHeat :245`; converter `GetMoltenCell(local)` is now in `BlockEntityConverterControl.Peripherals.cs:38` (plan/ladle.md cite `:764-766`), `DrainMetal :399`, `PushMetal :477/:518`; casting bed external pull `BlockEntitySandCastingBed.cs:255-272` (ladle.md says `:267-287`); `BlockEntityMoltenCanal.PushMetal :178` (ladle.md `:187`).
- The megablock-inside-multiblock link the vessel BE will need: `IMultiblockComponent` + `MultiblockAnchorLink<T>` (`src/ExpandedLib/Blocks/Structures/IMultiblockComponent.cs:3-13`, `MultiblockAnchorLink.cs:6-33`), as `BlockEntityHopperTall.cs:27-40` does.

### U11.6 - recipe, cost row, lang, handbook - **READY**
`Recipes/Grid/MoltenRecipeDefinitions.cs:16` exists; cost rows are `["moltenbarrel-grid"] = Grid("iiex:molten-barrel-*")` in `IiexRecipeConfig.DefaultCatalogue()` (`IiexRecipeConfig.cs:42-49`, `:98`). Lang `assets/iiex/lang/{en,ru,uk}.json`. The plan's "item and ingameerror rows are unguarded" is stale: `IiexLangCoverageTests.cs:40-53` now scans hand-asked keys via `LangCallSites`. Handbook: source `docs/iiex/handbook/NN-*.html` <-> descriptor `assets/iiex/config/handbook/NN-*.json` <-> lang key, re-blessed with `EXLIB_WRITE_HANDBOOK=1` (`test/ExpandedLib.Testing/HandbookSync.cs:11-47`; `HandbookParityTests.cs:29`, `:44`). Goldens: `test/IronIndustryExpanded.Tests/goldens/iiex/{blocktypes,itemtypes,recipes}`, regenerated with `EXLIB_WRITE_GOLDENS=<path filter>` (`DefinitionGoldens.cs:172-176`, `IiexDefinitionGoldenTests.cs:115-118`); `IiexBlocks.g.cs` with `EXLIB_WRITE_BLOCKCODES=1` (`IiexBlocks.g.cs:1-5`). Also `IiexCodePrefixTests.cs:9-22` (no base code may prefix another).

### U11.7 - docs sync - **partly DONE**
`ladle.md:8` already says **Mod iiex**; section Structure (`:196-202`) and section Assets (`:243-252`) are updated. Still stale: section Code (`:431-433` "Nothing exists", `:442-443` `.../Ladle/Blocks/` paths are fine but describe nothing), the Status line (`:3`), `molten-network.md:135-137`, `:309`. **`WORKLOG.md` does not exist anywhere** (`find`, `git log --all` empty); **`scripts/test-floors.txt` does not exist** - floors are in `scripts/tools/coverage_gate.py:27-65`.

## 2. The art
`molten-megablock-laddle.json`: 282 face-bearing elements, top-level `Root` with groups `Base` (38), `VesselIron` (126: `Shaft4`, `Vessel`), `Controls` (61: `Shaft3` = crank, px x 14...40, y 15...35, z 8...26), `VesselLining` (56); 117 rotated elements. Rotation-aware extent: **px x -17...41, y 0...40, z -7...23 -> cells x -1.06...2.56, y 0...2.5, z -0.44...1.44** (principal = 0...16). Cell occupancy (1 px tolerance) matches the plan's 3x3 + `I` + six slabs exactly except: the rim intrudes **3 px in y** into the three L4 tap sockets `(-1,2,0)`, `(0,2,-1)`, `(0,2,1)`; corner grazes <=1 px at `(2,1,-1)`, `(2,2,0)`, `(+/-2,0,0)`. The crank ends at x 40 = the west half of cell +2, confirming `Slab('I', WEST)`. `Base` starts at y 0, so with a *down-slab* core and `d` slabs under it the supports float 8 px - that is what the never-drawn `laddlecore` (slab with raised N/S cradle rails, plan `:2846`) was for.

Textures: `cast-iron1` (absolute `F:/...` path), `front1` (`tier2/front1`), `iron3`, `iron5`, plus 4 faces on `#null`. All four keys are in `TEXTURES` (`convert-shape.py:40`, `:49`, `:52`, `:83-84`); `castiron.png` exists. Caveat: `front1` maps to **tier3** - the block def must override it (`.Texture("front1", ...)` as `BlockFirebox.cs:53`). `#null` faces ship unhandled in 6 runtime shapes already; rendering effect UNVERIFIED.

Stand-in for the core: `game:block/basic/slab/slab-down` with a brick texture variant (ruling, plan `:2841-2845`). A canal start cannot stand in - it is a network node and the core must not be one (`ladle.md:225-236`).

## 3. Layout grids (verbatim, plan `:2649-2741`)

Megablock:
```
L1:
# # #
# C #
# # #

L2:
# # #
# # # I
# # #

L3:
_ . _
. _ _
_ . _
```
Multiblock:
```
L1:
. b N b .
# n n n #
# d C d #
# s s s #
. b S b .

L2:
. b . b .
b f f f .
b f L f .
b f f f .
. b . b .

L3:
. b . b .
b f f f .
b f f f f
b f f f .
. b . b .

L4:
. b A b .
b f C f .
B D f f .
b f E f .
. b A b .
```
Optional per ruling (`:2743-2761`): every `b` (12 cells) and every canal glyph `N`, `S`, `A`x2, `B`, `C`(L4), `D`, `E` (8 cells; the plan says "seven sockets"). Required: `#`, `d`, `n`, `s`, `C`(L1), `L`, `f`. Glyph `C` is used twice and must be re-lettered in C#.

## 4. Tests - copy from
- Structure completion at four angles: `test/ExpandedLib.Testing/StructureRig.cs:79` (`Around(world, anchor, def, angle)`), `Raise :207`, `Complete :264`, `Missing :237`; shared `FurnaceLayoutRig.Stand(...)` `test/IronIndustryExpanded.Tests/Fixtures/FurnaceLayoutRig.cs:155`; four-facing theory `HopperTallTests.cs:406-410`; `TestBlocks.Configure` `:63`.
- Footprint/boxes: `StructureFillerBoxesTests.cs`; `BoilerFootprintGuards.cs`.
- Molten: `Fixtures/MoltenPlantScenes.cs:16-36` (`CastingLine`), `Blocks/Molten/MoltenCanalStartTests.cs`, `MoltenCanalTapTests.cs`, `Networks/MoltenFlowTests.cs`, `CastingCellTests.cs`.
- Goldens: `Definitions/IiexDefinitionGoldenTests.cs`; codes: `IiexBlockCodeTests.cs`, `IiexCodePrefixTests.cs`, `IiexCodesHearthCellTests.cs`.

## 5. Config
No ladle keys exist. Related: `IiexConfig.MoltenCooldownSpeed = 24f` `:48`, `BarrelCooldownCoefficient :52`, `TapMoldCooldownCoefficient :56`, `MoldPedestalCooldownCoefficient :60`, `CanalDefaultUnitCapacity = 50` `:97`, `CanalDefaultDrainSpeed = 20f` `:100`, `TapDrainPerTick = 50` `:471`; exlib `ExlibConfig.MoltenFlowRate = 50` `:84`, `MoltenMinFlowAmount = 10` `:88`; siex `BessemerPourRate = 44f` `SiexConfig.cs:233`, `BessemerCooldownCoefficient = 0.5f` `:241`. ladle.md proposes `LadleCapacity` **6000 u** (`:398`, contradicting `:33` "~24 000 u"; Open #3), `LadlePullRate` 25, `LadlePourRate` 44, `LadleCooldownCoefficient` 0.5 (`:399-401`).

## Executable task list
1. **DONE** - U11.1 (use `FillerLayoutBuilder.Slab`; no exlib change).
2. **NEEDS-ART / NEEDS-RULING** - U11.2: export `networks/molten/molten-megablock-laddle` -> `assets/iiex/shapes/molten/laddle.json`; add `poursouth`/`pournorth` to `HOLD_CLIPS`; rename `nournorth` (owner's file - ask); no core shape to export.
3. **READY** - U11.3 `BlockLadleCore` at `src/IronIndustryExpanded/BlockStructures/Ladle/Blocks/`, down-slab shape, `brick` variants, `Handbook`, lang.
4. **READY** - U11.4: add `VanillaCodes.AnyBricksOrAir` and three `*:@(air|molten-canal-<kind>-.*-.*)` rungs (IiexCodes, with a `HearthCell`-style test); layout with `Origin(-2,-2)`, `LegendAnyFacing` for all eight sockets, re-letter L4 `C`; StructureRig tests: four angles, no-canal completes, canal-added still complete, brick-in-socket fails, both build orders.
5. **READY** - U11.5 `BlockLadle` + `BlockEntityLadle`: `FillerOffsets(Layout(... .Slab('_', DOWN).Slab('I', WEST)))`, `.Construction` shell (`RequireMetalPlate 8`, `RequireMetalNails 8`, `RequireMetalRod 4`) then lining `Require(VanillaCodes.RefractoryTier(2), n)`, `.Texture("front1", tier2)`, `GetDrops -> []`, fillers cleared in `OnBlockRemoved`, `MultiblockAnchorLink<BlockEntityLadleCore>`.
6. **READY** - U11.6: grid recipe, `["ladle-grid"] = Grid("iiex:molten-ladle-*")`, en/ru/uk, handbook page or a section in `03-moltencanals`, regenerate `IiexBlocks.g.cs` and goldens by path.
7. **READY** - U11.7: `ladle.md:3`, `:431-433`; `molten-network.md:135-137`, `:309`; no WORKLOG (file absent - decide where the entry goes).

**Open questions**: (a) 8 px gap under the vessel on a down-slab core - accept, extend the art, or draw the core; (b) capacity 6 000 vs 24 000 u; (c) is the bath a `BEBehaviorMoltenCell` on the principal (single-metal, refuses a second code) or a `MoltenCharge` field - the plan says "one metal" but the design's merge needs the latter; (d) `L`'s legend code and the ladle's own code/family prefix (`molten-ladle` vs a new family) under `IiexCodePrefixTests`; (e) lining brick count and whether the plate/cast shape variants (`ladle.md:23-28`) are in scope; (f) subdirectory invocation of `convert-shape.py` UNVERIFIED.
