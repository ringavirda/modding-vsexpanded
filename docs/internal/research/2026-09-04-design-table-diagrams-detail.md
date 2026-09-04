# Research snapshot - design-table-diagrams-detail

**Written** 2026-09-04 by a read-only research agent, against commit `cf2e8c89` (the tree before the
line-ending renormalisation). **Covers** design table and the 27 diagram items in depth (sub-agent pass).
**Purpose** grounding for the roadmap's Phase 1 walkthrough and Phase 2 machining-line plan
(`docs/internal/plans/2026-09-04-*.md`).

Line numbers were right on that day and drift; cite by symbol when reusing. "UNVERIFIED" marks are the
agent's own and were not re-checked. Nothing here is a decision - decisions live on the design pages.

---

## Design Table + Diagrams (iiex) - research report

Sources read in full: `docs/design/machines/design-table.md`, `docs/design/mechanics/diagram-crafting.md`, the block/BE/GUI/item/recipe files named in the brief, exlib's `BlockEntityMachineStation`, the goldens and tests. `docs/design/processes/fabrication.md` has no mention of diagrams or the table (grep empty).

### BUILD
Survival-craftable, not creative-only. `src/IronIndustryExpanded/Recipes/Grid/CraftingStationRecipeDefinitions.cs:18-28`: pattern `CPC,PPP`, size 3x2, `C` = `game:candle` x1, `P` = `game:plank-*` x1 -> `iiex:crafting-designtable-n` x1 (i.e. 2 candles + 4 planks). Golden: `test/IronIndustryExpanded.Tests/goldens/iiex/recipes/grid/designtable.json:1-25`. Cost-catalogue row: `src/IronIndustryExpanded/IiexRecipeConfig.cs:160` (`designtable-grid`).

### STRUCTURE
Single cell, `ExOrientable` with `side` variants n/e/s/w, wood, static `lightHsv [5,7,12]`, max stack 1, resistance 2 (`BlockStructures/Crafting/Blocks/BlockDesignTable.cs:22-37`; golden `goldens/iiex/blocktypes/crafting/designtable.json:5-37`). The shape `iiex:crafting/designtable` spans x 1..31/16, y 0..22/16, z 1..15/16 (computed from element bounds, rotations ignored) - two cells wide with no filler claiming the second cell (`BlockDesignTable.cs:15-16`; design Open #1, `design-table.md:102-103`). No collision-box override in the golden.

### VERBS
- **Open**: plain right-click, no sneak, hand contents irrelevant - `BlockDesignTable.cs:40-54` -> `BlockEntityDesignTable.OnInteract` (`.../BlockEntities/BlockEntityDesignTable.cs:52`) -> `ToggleWindow` (`src/ExpandedLib/Blocks/Machines/BlockEntityMachineStation.cs:62-87`, client-only, sends open packet 1000). Help: `BlockDesignTable.cs:56-72` -> `iiex:blockhelp-designtable-open` = "Open drafting table" (`assets/iiex/lang/en.json:213`).
- **Window** (`.../Gui/GuiDialogDesignTable.cs:126-144`): title `designtable-title`; label `designtable-inputs` "Charcoal + Parchment" (en:181); slot grid [0,1] inputs, [2] output; dropdown over every loaded item whose first code part is `diagram` with a `type` variant, domain not matched (`:66-70`), sorted by code (`:50-53`); name + description (`:156-172`: `{domain}:diagramdesc-{type}` else `iiex:designtable-desc-generic`, en:184); **Draw** button (`designtable-draw`, en:183) sends BE packet 1002 with the selected code (`:174-182`).
- **Slots** (`BlockEntityDesignTable.cs:36-41`): slot 0 accepts `IsDrawingMedium` - `game:charcoal` or any `coal-*` (`:83-85`); slot 1 accepts `IsParchment` - `game:paper` only (`:88-89`); slot 2 is take-only (`MachineStationSlots.cs:82-89`, `CanHold`/`CanTakeFrom` false).
- **Draft** (server): `OnStationPacket` (`:64-76`) -> `TryDraft` (`:102-132`): needs both inputs (`:92-96`), resolves the code to an item, refuses if the output holds a different diagram or is at max stack, takes 1 medium + 1 paper, adds 1 diagram. Claim + reach check before any station packet: `BlockEntityMachineStation.cs:129-136,183-212`. Last choice persisted as `dt_selected` (`:137-149`).

### PROCESS
Cost constants `MediumCost = 1`, `ParchmentCost = 1` (`BlockEntityDesignTable.cs:30-31`); drafting is instantaneous (synchronous `TryDraft`, no timer). No config keys: `IiexConfig.cs` has no diagram/design entries (grep hits are doc-path comments only). No progression gate (`design-table.md:34-35`).

**27 diagram variants** (`Items/DiagramItemDefinitions.cs:21-62`; golden `goldens/iiex/itemtypes/diagram.json:14-45`; 27 textures in `assets/iiex/textures/item/diagram/`). Every consumer uses `.Tool()`; no recipe anywhere consumes a diagram as a plain ingredient (grep for `Item(...diagram` without `Tool()` - none). No JSON recipe or RCC stage references `iiex:diagram` (assets grep empty).

| Diagram code | Consumer |
|---|---|
| `iiex:diagram-molten-straight` | `Recipes/Grid/DiagramRecipeDefinitions.cs:24` - `D,C` + cobble -> `molten-canal-straight-{rock}-ns` |
| `iiex:diagram-molten-bend` | `DiagramRecipeDefinitions.cs:32` -> `molten-canal-bend-{rock}-nw` |
| `iiex:diagram-mpenergy-flywheel` | `Recipes/Grid/EnergyRecipeDefinitions.cs:68` - `DW,PG`: 4 `castwheelsection`, 4 `castplate-heavy`, 1 spur gear -> `mpenergy-flywheel-normal-ns` |
| `iiex:diagram-mpenergy-flywheellarge` | `EnergyRecipeDefinitions.cs:90` - `DSW`: 8 `castwheelsection` + 1 `mpenergy-flywheel-normal-*` -> `mpenergy-flywheel-large-ns` |
| `iiex:diagram-item-{castheavyplate, castingotmold, castbarrel, castshell, castwheelsection, castbillets, castblooms, castslab}` (8) | `Recipes/Grid/PatternRecipeDefinitions.cs:23` - `DKP`: diagram + `game:knife-*` (tool) + 2 `game:plank-*` -> `iiex:pattern-{type}-{wood}` (golden `pattern.json:15...316`) |
| `iiex:diagram-pipe-{straight, bend, tjunction, xjunction}` (4) | **NONE** |
| `iiex:diagram-molten-{tjunction, xjunction, start, tap, furnacetap, moldpedestal, sandbed, sandcell}` (8) | **NONE** |
| `iiex:diagram-tuyere` | **NONE** |
| `iiex:diagram-furnace-{coldblast, cupola}` (2) | **NONE** |

12 consumed, 15 orphaned - matches `diagram-crafting.md:115`.

### READOUTS
None of its own: no `GetBlockInfo` in `BlockDesignTable`, `BlockEntityDesignTable` or `BlockEntityMachineStation`, and no `-info-` lang keys. Vanilla `BEContainer.GetBlockInfo` (`.compat/Vintagestory/vssurvivalmod/BlockEntity/BEContainer.cs:134-150`) only prints a perish line when the `InventoryGeneric` has food factors; `MachineStationInventory` is an `InventoryGeneric` (`MachineStationSlots.cs:38-44`) - whether that factor is null there is UNVERIFIED, but nothing is authored. No renderer (no renderer file under `BlockStructures/Crafting/`; light is static). Handbook page: `assets/iiex/config/handbook/04-designtable.json:1-5` -> `handbook-designtable-title/-text` (en:433-434).

### KNOWN GAPS
1. **15 diagrams with no consumer** (table above); the consumed-diagram half of Model A is unproven (`diagram-crafting.md:138-140`, Open `:159-164`).
2. **Stale "creative-only until the design table can draft it"** comments: `DiagramItemDefinitions.cs:15-16`, `DiagramRecipeDefinitions.cs:11`, `PatternRecipeDefinitions.cs:11-12` - the table is built and craftable.
3. **Doc-path drift**: six C# comments cite `docs/design/diagram-crafting.md`, which does not exist (real path `docs/design/mechanics/`): `BlockDesignTable.cs:16`, `BlockEntityDesignTable.cs:16`, `GuiDialogDesignTable.cs:14`, `DiagramRecipeDefinitions.cs:13`, `CraftingStationRecipeDefinitions.cs:13`, `EnergyRecipeDefinitions.cs:56`. `diagram-crafting.md:135` cites `PipeRecipeDefinitions.cs`, which does not exist (`PlatedPipeRecipeDefinitions.cs` / `CastPipeRecipeDefinitions.cs`).
4. **Lang, doc vs code**: `design-table.md:91-96` + Open #5 claim four gaps including `castshell`; `castshell` now has name and description in en/ru/uk (`en.json:508-509`, `ru.json:508-509`, `uk.json:508-509`). Remaining: `diagramdesc-item-castbillets/-castblooms/-castslab` absent in all three locales (fall back to the generic line). Doc counts (26/23) are stale (27/24).
5. Handbook text is English in ru/uk (`ru.json:433-434`, `uk.json:433-434`).
6. `designtable-inputs` says "Charcoal + Parchment" but coal is accepted too (`BlockEntityDesignTable.cs:83-85`; `DesignTableDraftTests.cs:19-24`).
7. `design-table.md:65-68` says the handshake lives in `BlockEntityDesignTable.OnReceivedClientPacket`; it is now sealed in exlib `BlockEntityMachineStation.cs:117-155` (BE only overrides `OnStationPacket`). Open #4 ("dialog is iiex-local") still holds for the GUI.
8. Draft packet id is defined twice: GUI hardcodes 1002 (`GuiDialogDesignTable.cs:25`), BE uses `FirstMachinePacketId` (`BlockEntityDesignTable.cs:34` = `BlockEntityMachineStation.cs:27` = 1002). Consistent today, two sources of truth.
9. Design Open items still open in code: 2-cell collision, candle particles, guide viewer, shared window base (`design-table.md:100-111`).
10. Doc test list (`design-table.md:71-72`) omits `test/IronIndustryExpanded.Tests/Blocks/Crafting/DesignTableDialogTests.cs`.

No files were written or modified.
