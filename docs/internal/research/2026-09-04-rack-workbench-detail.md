# Research snapshot - rack-workbench-detail

**Written** 2026-09-04 by a read-only research agent, against commit `cf2e8c89` (the tree before the
line-ending renormalisation). **Covers** storage rack and workbench in depth (sub-agent pass).
**Purpose** grounding for the roadmap's Phase 1 walkthrough and Phase 2 machining-line plan
(`docs/internal/plans/2026-09-04-*.md`).

Line numbers were right on that day and drift; cite by symbol when reusing. "UNVERIFIED" marks are the
agent's own and were not re-checked. Nothing here is a decision - decisions live on the design pages.

---

Research complete. All claims below are cited as `path:line`; `R` = `/home/fallen/src/modding-vsexpanded`. Nothing was written or run beyond greps/reads.

# (A) STORAGE RACK - `iiex:storage-rack` (variants `-n|e|s|w`)

**BUILD**
- Grid recipe `iiex:recipes/grid/storagerack`: pattern `P_P,PPP`, 3x2, `P` = `game:plank-*` x1 -> `iiex:storage-rack-n` x2 (5 planks -> 2 racks). `R/src/IronIndustryExpanded/Recipes/Grid/CraftingStationRecipeDefinitions.cs:41-51`; golden `R/test/IronIndustryExpanded.Tests/goldens/iiex/recipes/grid/storagerack.json:1-19`. Cost key `storagerack-grid` `R/src/IronIndustryExpanded/IiexRecipeConfig.cs:162`. No RCC stages, no diagram, not creative-only (creative `*-n`, `R/src/IronIndustryExpanded/BlockStructures/Storage/Blocks/BlockStorageRack.cs:61`).

**STRUCTURE**
- 1x1x3 placed megablock, not a multiblock - no completion tick. Footprint layout `+ / + / O`, origin (0,-2): fillers at (0,0,-2) and (0,0,-1), both `allowAttach: true` (racks stack vertically). `BlockStorageRack.cs:39-51`; golden `R/test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/storage/storagerack.json:42-55`.
- Placement refused with `notenoughspace` unless all cells clear; fillers placed/removed with the principal. `R/src/ExpandedLib/Blocks/Structures/BlockFilledMegastructure.cs:36-51, 53-61, 70-78`.
- Orientation: `ExOrientable` + `side` variant; `StructureAngle = AngleFromSide(side)` (no +180) `BlockStorageRack.cs:76-77`; shape `iiex:storage/storagerack` spun n0/e270/s180/w90 (golden `:29-37`). Shipped shape exists `R/assets/iiex/shapes/storage/storagerack.json`. Wood, resistance 3, walk/place/axe sounds `BlockStorageRack.cs:62-70`.

**VERBS** (click only; no hold, no sneak/sprint branches)
- Principal click -> `Worked(principal, principal)` `BlockStorageRack.cs:144-150`; filler click -> `IFillerInteractionTarget.OnFillerInteractStart` -> `Worked(principal, clickedCell)` `:153-158`; Step/Stop are no-ops `:161-176`.
- `Worked` `:183-211`: held stack non-null -> `TryLay` lays the **whole held stack** at the **lowest fitting run, ignoring which cell was clicked** (`:180-182`); refusal returns false silently. Empty hand -> `TryTake(cell)` takes whichever run covers that cell; given to inventory, else spawned at the clicked cell `:204-209`.
- Caveat: a filler click while holding a placeable *block* is not forwarded; with `allowAttach` on, the engine places the block on the rack cell instead. `R/src/ExpandedLib/Blocks/Structures/BlockStructureFiller.cs:224-252`.
- Break any cell -> `OnBlockBroken` spawns `TakeAll()` before base `BlockStorageRack.cs:222-236`.
- Help: `iiex:blockhelp-rack-lay` ("Lay it on the rack", when right hand non-empty) / `iiex:blockhelp-rack-take` ("Take from the rack") `:250-269`; filler help forwarded `:243-248`; lang `R/assets/iiex/lang/en.json:215-216`.

**PROCESS**
- No `IiexConfig` keys (grep `rack|bay` in `R/src/IronIndustryExpanded/IiexConfig.cs` hits only unrelated lines 411/703/981).
- Catalogue `R/assets/iiex/config/bayoccupancy/storagerack.json` (`schema 1`, `store "storagerack"`): 1 cell - `iiex:stock-rod`, `iiex:stock-heavyplate`, `iiex:boilerplate`, `iiex:pig`, `iiex:caststock-billet`, `iiex:caststock-bloom` (`:5-10`); 2 cells - `iiex:stock-shingledbar`, `iiex:stock-beam`, `iiex:stock-shingledslab` (`:12-14`); 3 cells - `iiex:caststock-slab` (`:16`). All ten codes are real (goldens `R/test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/{stock-*,pig,boilerplate}.json:2`, `caststock.json:18-24` form variants).
- Loader reads every domain's `config/bayoccupancy/` at `R/src/ExpandedLib/ExpandedLibModSystem.cs:72` (`R/src/ExpandedLib/Storage/BayOccupancyLoader.cs:17, 82-91`). File refused whole if no `store`, a rule lacks `item`, or `cells < 1` (`BayOccupancy.cs:48-83`). Trailing `*` = prefix; most specific wins; a second rule for one item is reported and the first stands (`BayOccupancy.cs:22-29`, `BayOccupancyRegistry.cs:28-48, 61-72`). **Unlisted item -> null -> refused** (`BayOccupancyRegistry.cs:12-14`); `DefaultCells = 1` is only the parse default when `cells` is omitted (`BayOccupancy.cs:19, 72`).
- Bay maths: cells = principal + fillers ordered along the one varying axis = 3 (`BlockStorageRack.cs:87-119`); `Fit` = lowest start whose run is in-bounds and non-overlapping (`R/src/ExpandedLib/Storage/BayLayout.cs:37-63`); `FreeCells = cells - sum length` (`:78-83`); `IndexAt` - any cell of a run selects it (`:70-75`). Max per bay: one load per cell-run; a load is one whole stack (`R/src/IronIndustryExpanded/BlockStructures/Storage/BlockEntities/BlockEntityStorageRack.cs:66-81`). `stock-*`/`caststock-*` are `MaxStackSize(1)` (`R/src/IronIndustryExpanded/BlockStructures/Forming/StockItemDefinitions.cs:69`, `R/src/IronIndustryExpanded/Items/CastStockItemDefinitions.cs:90`) but `pig` and `boilerplate` stack to 16 (`R/src/IronIndustryExpanded/Items/ItemPig.cs:48`, `R/src/IronIndustryExpanded/BlockStructures/Forming/RolledItemDefinitions.cs:142`) - so 16 pigs = one 1-cell load.
- Refused: everything else, including the rolled products `iiex:beam|heavyplate|rivetrod|nailplate|blank|skelp` (goldens exist) and vanilla stock. Nothing touches temperature - the rack does not keep stock hot.

**READOUTS**
- `GetBlockInfo` `BlockEntityStorageRack.cs:334-352`: `iiex:rack-empty` "Empty - {0} cells free"; per load `iiex:rack-holds` "{0}x {1} ({2} cells)"; `iiex:rack-free` "{0} of {1} cells free" (`en.json:217-219`). No particles, animation, or extra sounds.
- Renderer (UNVERIFIED in game): BE is `ITexPositionSource` over the **block** atlas (`:20, 215-252`, inserts textures on demand `:243-251`); meshes built main-thread in `Initialize`/`Changed`/`FromTreeAttributes` (`:129-151, 110-113, 292`); items via `TesselateItem` + `GetCachedShape`, blocks via default block mesh clone (`:158-172`); transform = rotate by `Block.Shape.rotateY`, then (0, `RailHeight` 8/16, -`CentreOf(run)`) (`:179-195`); `OnTesselation` adds meshes and returns `base` so the frame still draws (`:197-207`). Persistence `loads`/`load{i}Start|Length|Stack` with `ResolveBlockOrItem` (`:260-293`); schematic mappings (`:297-328`).

**TESTS / HANDBOOK**
- `R/test/IronIndustryExpanded.Tests/Blocks/Storage/StorageRackTests.cs` (loads the shipped catalogue `:38-44`): footprint/allowAttach `:101-113`, cell count `:115-127`, mesh-vs-footprint angle `:129-143`, three arrangements `:149-191`, any-cell take `:197-221`, `CellAt` per side `:223-244`, block-level lay/take `:246-274`, filler click `:276-292`, refusal `:298-307`, all three lengths `:309-318`, tree round-trip `:324-345`, `TakeAll` `:347-363`, `CentreOf` `:374-390`. Plus `R/test/ExpandedLib.Tests/Storage/BayLayoutTests.cs` and `BayOccupancyTests.cs:44-192`.
- NOT covered: everything in the render region except `CentreOf` (mesh build, atlas, transform, `OnTesselation`), `GetBlockInfo`, interaction help, the `OnBlockBroken` override itself, vertical stacking.
- Handbook: no page; one link in `R/docs/iiex/handbook/10-formingshop.html:45` (`handbooksearch://storage rack`). The `12-cruciblefurnace.html:32` hit is "cracks".

**KNOWN GAPS**
- Design `R/docs/design/machines/stock-rack.md:338-358` Open: renderer never seen; occupancy numbers a starting table (`caststock-slab` sole 3-cell entry); item height not modelled (code uses a constant `RailHeight`); foreign-item rendering untested; no handbook page; vertical stacking untested and `allowAttach` lets torches hang on invisible cells.
- Design body is stale below the amendment: `:169` "No recipe" (one ships), `:173` planks x4 (ships x5), `:188-192` LIFO (code takes by clicked cell, any order), `:184-247` layer model superseded (`:17-21`).
- Refusal gives the player no message; laying ignores the clicked cell.

# (B) WORKBENCH - `iiex:crafting-workbench` (variants `-n|e|s|w`)

**BUILD**
- Grid recipe `iiex:recipes/grid/workbench`: `_H_,V_V,PPP` 3x3; `H` = `game:hammer-*` isTool; `V` = `game:metalplate-*` (metal capture, iron|steel) x2 each; `P` = `game:plank-*` x2 each -> `iiex:crafting-workbench-n` x1 (hammer + 4 plates + 6 planks). `CraftingStationRecipeDefinitions.cs:29-40`; `R/src/ExpandedLib/Definitions/ExIngredients.cs:15-16, 23-24`; golden `.../goldens/iiex/recipes/grid/workbench.json:1-35`. Cost key `workbench-grid` `IiexRecipeConfig.cs:161`. No RCC, no diagram, not creative-only.

**STRUCTURE**
- 2 cells `O #` (one plain filler at (1,0,0), no allowAttach) `R/src/IronIndustryExpanded/BlockStructures/Crafting/Blocks/BlockWorkbench.cs:65-74`; golden `.../goldens/iiex/blocktypes/crafting/workbench.json:46-54`. `StructureAngle = AngleFromSide + 180` (`:77-78`), shape spun with +180 (`:44`) -> n180/e90/s0/w270 (golden `:31-36`); drawn frame is the `s` variant. Placement/fillers as the rack (`BlockFilledMegastructure.cs:36-61`). `MaxStackSize 1`, wood, `SolidNonOpaque` (`:45-53`). Shape `R/assets/iiex/shapes/crafting/workbench.json` exists. Not a multiblock; no completion.

**VERBS**
- Right-click either cell (no sneak/sprint, no hold on the block): principal -> `bench.OnInteract` -> `ToggleWindow` (`BlockWorkbench.cs:84-98`; `R/src/IronIndustryExpanded/BlockStructures/Crafting/BlockEntities/BlockEntityWorkbench.cs:68`), client-only dialog plus `Inventory.Open` and open packet 1000 (`R/src/ExpandedLib/Blocks/Machines/BlockEntityMachineStation.cs:62-87`). Filler cell forwards because the bench is not `IFillerInteractionTarget` (`BlockWorkbench.cs:28-30`; `BlockStructureFiller.cs:231-243`), except while holding a placeable block (`:224-229, 254-256`).
- Help `iiex:blockhelp-workbench-open` "Open workbench" `BlockWorkbench.cs:100-116`, `en.json:214`; forwarded from the filler `BlockStructureFiller.cs:377-393`.
- GUI `R/src/IronIndustryExpanded/BlockStructures/Crafting/Gui/GuiDialogWorkbench.cs:59-139`: title `iiex:workbench-title`; 5x5 grid (slots 0-24, "Assembly"); passive preview slot ("Result") re-matched on every grid slot change (`:145-176`); Craft button; output row of 5 (slots 25-29, "Finished work"). Lang `en.json:185-189`.
- Hold-repeat: the button's own handler does nothing (`:186`); mouse-down inside the button bounds starts a run with the first craft due immediately (`:194-204`); `OnRenderGUI` sends packet 1002 every 0.35 s while held (`:25, 211-222`); mouse-up or close stops (`:206-209, 226-229`). UNVERIFIED in game.
- Server `OnStationPacket` -> `TryCraft` (`BlockEntityWorkbench.cs:78-88`) after the claim/range check (`BlockEntityMachineStation.cs:117-155, 183-212`). `TryCraft` `:108-137`: first matching `GridRecipe` at width 5 (`:98-101`), output cloned and passed through `OnCreatedByCrafting` via a `DummySlot` (`:121-126`), simulated `Distribute` refuses with nothing consumed when the row is full (`:128-130`), then vanilla `ConsumeInput(player, grid, 5)` consumes ingredients (`:131-132`), then real `Distribute` (`:134`). Output slots are take-only; grid slots take anything (`R/src/ExpandedLib/Blocks/Machines/MachineStationSlots.cs:25-30, 82-90`). `Distribute` tops up mergeable partial stacks before empties, capped at `MaxStackSize` (`BlockEntityWorkbench.cs:145-209`).

**PROCESS**
- Recipe selection: **no tagging** - every loaded `GridRecipe` (vanilla and every mod) up to 5x5 matches; the only recipe mentioning the bench is its own. No shipped recipe exceeds 3 wide (grep `.Size(4-9)` empty) and the largest ingredient quantity is 8, once. 1.20/1.21 `Matches` shim `R/src/ExpandedLib/Legacy/LegacyApi.cs:30-35`.
- Craft time: none - one craft per packet; client repeat 0.35 s; no server-side rate limit.
- No `IiexConfig` keys (grep `workbench|craft` -> only unrelated `:431`). **`config/craftsequences/` does not exist** anywhere in `assets/` or `src/`; the only reference is the comment `GuiDialogWorkbench.cs:21-24`.

**READOUTS**
- No `GetBlockInfo` override (inherits `BlockEntityContainer`; base output UNVERIFIED). No particles, animations, or renderer; sounds walk/place only (`BlockWorkbench.cs:48-49`). The window is the readout.

**TESTS / HANDBOOK**
- `R/test/IronIndustryExpanded.Tests/Blocks/Crafting/WorkbenchTests.cs`: footprint `:50-58`, mesh/footprint turn `:60-74`, south frame `:76-82`, 25+5 inventory and slot types `:90-109`, output refuses input `:111-121`, `Distribute` x4 `:137-203`. NOT covered: `TryCraft`/`MatchingRecipe`/`ConsumeInput`, `OnCreatedByCrafting`, the GUI (preview, hold-repeat), packet handshake, block interaction.
- Handbook: none (`R/docs/iiex/handbook/` has 00-12; no workbench page or mention).

**KNOWN GAPS**
- Design `R/docs/design/machines/workbench.md:197-211` Open: 5x5 unjustified by any shipped recipe; interaction sequence not built (no `craftsequences`, no gesture path, no `WorldInteraction.Itemstacks` tool hint as `:139-147` specifies); which recipes get sequences; tier; 5x5 handbook rendering.
- Repeat is ungated because no sequence exists (`GuiDialogWorkbench.cs:23-24`), consistent with design `:120-127`.
- The first matching recipe wins in `GridRecipes` order; which recipe that is for an ambiguous layout is UNVERIFIED.
- Memory hints (`workbench-art-arrived-grid-unjustified.md`, `storage-rack-built-length-capacity.md`) agree with the code on every point checked.
