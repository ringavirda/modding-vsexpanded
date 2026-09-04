# Research snapshot - casting-cells-detail

**Written** 2026-09-04 by a read-only research agent, against commit `cf2e8c89` (the tree before the
line-ending renormalisation). **Covers** sand casting cell and long cell in depth: pour face, misrun at shake-out, long-cell intake blocked by its own filler, art vs footprint.
**Purpose** grounding for the roadmap's Phase 1 walkthrough and Phase 2 machining-line plan
(`docs/internal/plans/2026-09-04-*.md`).

Line numbers were right on that day and drift; cite by symbol when reusing. "UNVERIFIED" marks are the
agent's own and were not re-checked. Nothing here is a decision - decisions live on the design pages.

---

**Root** `R=/home/fallen/src/modding-vsexpanded`. Aliases: `C=R/src/IronIndustryExpanded/BlockStructures/Casting`, `BE=C/BlockEntities/BlockEntitySandCastingCell.cs`, `LOGIC=C/CastingCellLogic.cs`, `PAT=C/PatternItemDefinitions.cs`, `LC=C/Blocks/BlockSandCastingLongCell.cs`, `REC=R/src/IronIndustryExpanded/Recipes/Grid/CastingRecipeDefinitions.cs`, `MC=R/src/ExpandedLib/Blocks/Structures/BEBehaviorMoltenCell.cs`, `EN=R/assets/iiex/lang/en.json`, `T=R/test/IronIndustryExpanded.Tests`.

## BUILD
- **Cell** `iiex:casting-sandcell-{brick}-{n|e|s|w}` (brick: fire,black,brown,cream,gray,orange,red,tan): grid `BHB,BFB,BKB` 3x3 - B=6x `game:brickcourse-four-running-*` (captures `{brick}`) or 6x `game:claybricks-good-fire` (->`-fire-n`), F=2x `game:clay-fire`, H=`game:hammer-*` tool, K=`game:chisel-*` tool -> x1 `REC:71-94`; golden `T/goldens/iiex/recipes/grid/sandcastingcell.json`. Cost key `sandcastingcell-grid` `R/src/IronIndustryExpanded/IiexRecipeConfig.cs:127`.
- **Long cell** `iiex:casting-sandlongcell-{brick}-{side}`: same pattern, B x2 per slot (12 bricks), F x4 `REC:104-126`; cost key `:128`.
- **Green sand** `iiex:greensand` x8 = 8x `game:sand-*` (block) + 1x `game:clay-blue`, `SSS,SCS,SSS` `REC:59-69`.
- **Patterns** (all 8 types x 12 woods, `PAT:86-170,199-213`): `iiex:pattern-{castheavyplate|castingotmold|castbarrel|castshell|castwheelsection|castbillets|castblooms|castslab}-{wood}`. Recipe `DKP` 3x1: D=`iiex:diagram-item-{type}` tool, K=`game:knife-*` tool, P=2x `game:plank-*` (captures wood) -> x1 `R/src/IronIndustryExpanded/Recipes/Grid/PatternRecipeDefinitions.cs:16-35`. Diagrams are **creative-only** (`:11-13`; no recipe outputs `iiex:diagram-*` - grep of `Recipes/`), so no survival route. Pattern: maxstack 1, durability 24 `PAT:187,254-256`.
- No RCC stages on either cell. Bed differs: grid `BBB,_H_` 4x `game:burnedbrick-*`+hammer -> `iiex:casting-sandbed-{brick}-n` `REC:35-51`, then RCC stages 8x `burnedbrick-{brick}` (storeWildCard), 16x brick, 12x `iiex:greensand` `C/Blocks/BlockSandCastingBed.cs:73-104`.

## STRUCTURE
- **Cell**: single block; hosted `BEBehaviorMoltenCell` capacity 200, drainFitting (`C/Blocks/BlockSandCastingCell.cs:38-40`), not a network node - the BE pulls itself. Shape spun n0/e270/s180/w90 (`:55`; golden `sandcell.json:47-55`). `ExOrientable` writes `side` = player's look direction `R/src/ExpandedLib/Blocks/Behaviors/BlockBehaviorExOrientable.cs:143-151`.
- **Long cell**: `BlockFilledMegastructure`, one filler at model `(0,0,+1)` `C/LongCellLayout.cs:28-29`, filler hosts no behaviours (`:8-10`); `StructureAngle = AngleFromSide+180` `LC:101-102`, shape spun with offset 180 (`LC:72`; golden n180/e90/s0/w270). Placement refused unless footprint clear (`notenoughspace`), fillers spawned/removed with the principal `R/src/ExpandedLib/Blocks/Structures/BlockFilledMegastructure.cs:36-77`. No completion step/tell - complete on placement. Filler clicks reroute to the principal, clicked cell ignored `LC:125-132`. Unimpressed capacity 3400 `LC:35`.
- **Pour**: only a canal `IMoltenCell` at `Pos + LaunderFace`, `LaunderFace = FacingFromSide(Variant["side"])` `BE:184-185`, pulled at 25 u/tick `BE:32,159-178`. No `ILiquidMetalSink` on either cell (only the mold `C/BlockEntities/BlockEntityCastMold.cs:26`) -> no ladle/crucible pour.
- Bed differs: 3x1x4, 11 fillers each hosting a molten cell; pulls from **any** horizontal neighbour of the principal `C/BlockEntities/BlockEntitySandCastingBed.cs:249-273`.

## VERBS (right-click only; `OnBlockInteractStart`, no Step/Stop, no sneak/sprint checks `BE:192-229`, `LOGIC:54-78`)
| Held | Cell state | Result |
|---|---|---|
| empty | metal, hardened | Harvest `BE:281-306` |
| empty | metal, not hardened | error `iiex-castingcell-toohot` `BE:220-225`, `EN:422` |
| anything | metal | falls through |
| `iiex:greensand` (full-code match `LOGIC:94-95`) | sand!=Full | RamSand: -1 (not creative), sound `BE:231-241` |
| item with `FirstCodePart()=="pattern"` `BE:200` | Full, no impression | Imprint: no `mold` attr -> `iiex-castingcell-badpattern` `BE:248-254`; wrong size -> `iiex-castingcell-wrongsize`/`iiex-longcell-wrongsize` `BE:258-262`, `C/BlockEntities/BlockEntitySandCastingLongCell.cs:20-23`; else store full code, `SetCapacity`, `DamageItem` x1 `BE:264-277` |
No verb removes a pattern or sand; only Harvest clears the impression `BE:298-302`. Break: block drops itself (no `GetDrops` override); metal voided (`OnBlockRemoved` only disposes the renderer `BE:124-128`). **No `WorldInteraction` help** (no override; filler forwards base `LC:150-155`; no `blockhelp` keys in `EN`). Bed: carve/harvest per slot `BlockEntitySandCastingBed.cs:325-400`, errors `iiex-castingbed-alreadycarved/-toohot` `EN:392-393`.

## PROCESS
- Server tick 1 s `BE:117`: `CanIntake = impression && !full && !solidified` `LOGIC:112-116` -> pull; then `UpdateThermal` `BE:153-154`.
- Capacities (`PAT:86-170`, golden `T/goldens/iiex/itemtypes/pattern.json`): castheavyplate 160->`iiex:castplate-heavy`; castingotmold 152->block `iiex:casting-mold-ingot`; castbarrel 200->`iiex:cast-barrel`; castshell 600->`iiex:castshell`; castwheelsection 600->`iiex:castwheelsection`; castbillets 1800 (3x600)->`iiex:caststock-billet`; castblooms 2000 (2x1000)->`iiex:caststock-bloom`; castslab 3000->`iiex:caststock-slab` (units `R/src/IronIndustryExpanded/Items/CastStockItemDefinitions.cs:22-28`; caststock maxstack 1, `stockForm cast{form}` `:75-90`).
- Fill time = capacity/25 u.s^-1 (7 s plate ... 120 s slab). Intake stops **permanently** once the pool's temp < melting point (`Solidified` latch `MC:288-291`, refused push `MC:182-183`) -> short pour. Cast iron melts at 1200 `R/assets/iiex/config/metals/castiron.json:12`, pig iron 1150.
- Cooling: `cooldownSpeed` 24 C/game-hour (`R/src/ExpandedLib/ExlibConfig.cs:92`, `MC:39,234-239`), vanilla scales by max(1,T/200) `R/.compat/Vintagestory/vsapi/Common/Collectible/Collectible.cs:3322-3327` -> exponential; 1200->360 C (hardened = 0.3xmelt, `ExlibConfig.cs:99`, `MC:145-159`) ~ 10 game hours. No cell-specific `IiexConfig` key; ambient 20 `R/src/IronIndustryExpanded/IiexConfig.cs:63`. Whether `IiexConfig.MoltenCooldownSpeed` (`:48`) bridges into exlib's default: UNVERIFIED.
- Harvest: full && !misrun -> `spec.Output`, `StackSize = max(1, Quantity)` `BE:316-323`; else `MoltenChisel.BuildRecovery` -> `iiex:metalbit-castiron` (`castiron.json:4`) xunits/5 `R/src/ExpandedLib/Metals/MoltenChisel.cs:52-70`, `BE:328-335`. Then contents+capacity cleared, pattern cleared, sand->Full `BE:298-302`, `LOGIC:84`. `minPourTemp` 1150 on every shipped pattern `PAT:30`; parser default 0 `C/MoldSpec.cs:116`.
- Bed differs: no patterns/misrun; mold cavity = impressionsx375 `C/SandBedLayout.cs:125-146`; harvest denominates `iiex:pig/pigchunk/pigbit` or `iiex:slagbrick`, runners -> scrap `BlockEntitySandCastingBed.cs:407-468`.

## READOUTS
- `GetBlockInfo` `BE:450-483`: `iiex:castingcell-needssand` / `-needspattern` / `-ready {pattern}` / `-casting {0}/{1} units, {state} ({temp})` `EN:418-421`; state only `metalstate-hardened|liquid` (`EN:363-364`) - the Cooling band reads "liquid".
- Renderer: `MoltenRenderer` over `spec.Cavity`, rotated by `Block.Shape.rotateY`, refreshed 1 s `BE:351-390`. Filling mesh via `OnTesselation`/`ExMeshCache` `BE:396-444`; state->shape `LOGIC:131-143`: `cell-filling-base`/`-half`/pattern shape; long-cell fillings `longcell-filling-{billets,blooms,castslab}.json` (all tracked; `git ls-files`).
- Sounds: `game:sounds/effect/stonecrush` (`R/src/ExpandedLib/Helpers/ExSounds.cs:49-51`) at 0.5/0.6/0.7. No particles, animations or dynamic light. Bed: `castingbed-basin/-empty/-pigs-ready/-cooling` `EN:388-391`, per-cell surfaces `BlockEntitySandCastingBed.cs:482-534`.

## KNOWN GAPS
1. **Misrun is always true for shipped patterns.** `IsMisrun` is evaluated at shake-out against `cell.CellTemperature` `BE:287-292`, but Harvest requires hardened (<360 C cast iron); 1150 min -> every full cast yields metalbits. Design intends pour temp (`R/docs/design/machines/casting-cell.md:173`). Only `minPourTemp 0` patterns yield parts. By code reading; UNVERIFIED in game; no end-to-end harvest test.
2. **Long cell can never pull.** Filler is at `Pos+FacingFromSide(side)` (pinned `T/Blocks/Casting/LongCellTests.cs:88-104`, `RotateOffset` `R/src/ExpandedLib/Helpers/ExOrientation.cs:32-42`) - the same cell `LaunderFace` reads; its BE is `BlockEntityStructureFiller` (not `IMoltenCell`, `R/src/ExpandedLib/Blocks/Structures/BlockEntityStructureFiller.cs:18`) -> `PullFromLaunder` returns `BE:161-167`. Cast stock route unpourable. UNVERIFIED in game.
3. **Art vs footprint**: long-cell body authored at z -16..0 (`R/assets/iiex/shapes/casting/sandcastinglongcell.json` Cube6/7/13/14, offsets composed, no element rotations) while the filler is +Z, both spun 180 -> drawn body opposite the filler; same reading for the bed (rows z -16..-46 vs dz +1..+3, `SandBedLayout.cs:97-105`, spins `BlockSandCastingBed.cs:125-128,193-194`), contradicting bed's "live" status (`casting-bed.md:2-3`). No `MegablockFrames` guard covers either (only `BoilerFootprintGuards`). Needs in-game check.
4. **Multi-lane yields one item**: `Mold` emits output without quantity `PAT:40` -> 1 billet from 1800 u, 1 bloom from 2000 u; design says several (`long-cell.md:158-159`).
5. 1x1 cell: spout drawn at +Z (south at spin 0, `sandcastingcell.json` Cube8 z10-16) but pull face = `side` (north for `-n`) - design Gotcha 2, UNVERIFIED.
6. Rammed long cell draws the 1x1 flat sand (`LOGIC:119-123`, subclass overrides nothing); `longcell-filling-base/half.json` unreferenced.
7. Pattern from a removed mod strands the cast (`BE:284-285`). `minPourTemp` default mismatch 0 vs 1150. `castframe` filling has no pattern; axle/cylinder/gearblank fillings orphaned. Break voids metal (`long-cell.md:166-167` claims recovery rules that don't exist).
8. Stale docs: `processes/casting.md:3-4,111,174-176` and `items/stock.md:310-311` say the long cell/items don't exist; `casting-cell.md:285-288` says `MoldSize` unenforced (it is, `BE:52-56`); `casting.md:83` bed sand = `game:sand-*` (code: `iiex:greensand`); "untracked" shape claims are stale (all tracked).
9. **No tests** for: pull/thermal tick, Harvest/BuildHarvest, `RebuildRenderer`/`OnTesselation`, `GetBlockInfo`, serialization, `MoltenRenderer`, bed surfaces/animator/HUD/pull/carve/harvest. Handbook: no sand-casting/pattern page (`R/docs/iiex/handbook/` - canal page mentions pedestal casting only, `EN:432`).
