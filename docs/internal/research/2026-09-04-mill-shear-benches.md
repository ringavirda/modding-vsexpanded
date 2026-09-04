# Research snapshot - mill-shear-benches

**Written** 2026-09-04 by a read-only research agent, against commit `cf2e8c89` (the tree before the
line-ending renormalisation). **Covers** mpenergy drive (flywheel, shafts, transmission), rolling mill, crop shear, nail cutter and riveter, stock items between stations.
**Purpose** grounding for the roadmap's Phase 1 walkthrough and Phase 2 machining-line plan
(`docs/internal/plans/2026-09-04-*.md`).

Line numbers were right on that day and drift; cite by symbol when reusing. "UNVERIFIED" marks are the
agent's own and were not re-checked. Nothing here is a decision - decisions live on the design pages.

---

**Scope note.** Tree at `bd428603` (2026-09-04); the forming/bench commits are `2dcb4d2b`/`5f490d74` (2026-08-21) and nothing here has been walked. The vendored vanilla source (`.compat/vintagestory`, `docs/vanilla/`) is **absent from this checkout**, so two vanilla facts below are UNVERIFIED. Path prefixes: **F** = `src/IronIndustryExpanded/BlockStructures/Forming`, **E** = `src/IronIndustryExpanded/BlockNetworkEnergy`, **X** = `src/ExpandedLib`, **R** = `src/IronIndustryExpanded/Recipes/Grid`, **C** = `src/IronIndustryExpanded/IiexConfig.cs`, **L** = `assets/iiex/lang/en.json`, **D** = `docs/design`.

---

## 1. mpenergy drive

**BUILD**
- Spur gear: `_K_,III` = chisel + **3** `game:ingot-iron` -> 1 `iiex:spurgear`; or `_K_,_C_` = chisel + 1 `iiex:ingot-castiron` -> 2 (R/EnergyRecipeDefinitions.cs:29-45). (D/machines/flywheel-and-shafting.md:188 says 2 ingots - doc drift.)
- Flywheel (normal): `DW,PG` = `iiex:diagram-mpenergy-flywheel` (tool, kept) + 4 `iiex:castwheelsection` + 4 `iiex:castplate-heavy` + 1 spurgear -> `mpenergy-flywheel-normal-ns` (R/EnergyRecipeDefinitions.cs:62-81). Large: `DSW` = large diagram + 8 sections + 1 normal wheel (:84-101). Diagram is drafted at the design table from 1 drawing medium + 1 parchment; the picker lists every `diagram-*` item (`.../Crafting/Gui/GuiDialogDesignTable.cs:18,:68`; D/machines/design-table.md:26-34).
- Shaft: `HRP` = hammer + 1 `game:rod-*` + 1 `game:metalplate-*` -> **2** `mpenergy-shaft-ns` (:112-120; ingredient codes X/Definitions/ExIngredients.cs:15-16,23-24,41-42).
- Bevel: **creative-only** - `iiex:bevelgear` has no recipe (`src/IronIndustryExpanded/Items/BevelGearItemDefinitions.cs:13-23`; D/machines/flywheel-and-shafting.md:201-204); the bevel block is made in-world by using the gear on a placed shaft (E/Blocks/BlockCastIronShaft.cs:63-88).
- Transmission: housing `P_P,PHP` = 4 plate + hammer -> `transmission-x2-n`; x4 = `PTP` (x2 housing + 2 plate); clutch = `RTR` (x2 housing + 2 rod) (:123-153). RCC stages: 1 free (`Base`); 2 requires 2 `iiex:spurgear` + 4 `game:ingot-iron` (`MainShafts`); 3 requires 2 `game:ingot-iron` (`SupportShaft`) (E/Blocks/BlockTransmission.cs:55-80). Block never drops; RCC scatters materials (:43, :225-230).

**STRUCTURE**
- Flywheel normal 3x3x1 `Face` grid, principal `O` bottom-centre, hub `M` directly above it hosting two `BEBehaviorMPFillerPort`s (north+south in the `ns` frame; `we` rotates them east/west) (E/Blocks/BlockFlywheel.cs:37-56; large 5x5x2 hubs at (0,2,0),(0,2,1) :60-84). Placement refuses unless the whole volume is clear (:144-160). mpenergy connectors are on the **principal's** two orientation-axis faces (`X/Blocks/Networks/BlockNetworkNode.cs:754-755`), i.e. a cast-iron shaft attaches to the placed bottom cell, the vanilla axle to the hub cell one block up.
- Vanilla bridge: the hub port is a real vanilla-MP participant, resistance 0.5 (X/Blocks/Structures/BEBehaviorMPFillerPort.cs:25,:83), couples both ends of its axis (:73-81). Drive torque = `1.0 N.m x clamp(hubSpeed/1.0)` (E/BlockEntities/BlockEntityFlywheel.cs:52-71; C:847,852); direction from the port's sign (:75-89).
- Shaft: `ns/we/ud`, thin 0.3125-0.6875 collision (E/Blocks/BlockCastIronShaft.cs:40,:47-48). Bevel: connector on every face, gears drawn only toward perpendicular connected neighbours (E/Blocks/BlockCastIronBevel.cs:154-158; E/BlockEntities/BlockEntityCastIronBevel.cs:22-35); breaking returns shaft + gear (:206-221).
- Transmission: 2x2 (fillers (1,0,0),(0,1,0),(1,1,0)=lever cell), ports on the principal's +Z/-Z cells, not a graph node (E/Blocks/BlockTransmission.cs:47-51; E/BlockEntities/BlockEntityTransmission.cs:232-245). Ratios x2->2, x4->4, clutch->1 (:54-59).
- Completion tells: transmission renders only `Base/*` until built (:87); clutch lever cell shows `iiex:transmission-help-clutch` (L:305) once built (:191-218).

**VERBS** - flywheel/shaft: none. Shaft + `iiex:bevelgear` in hand -> swaps to bevel (BlockCastIronShaft.cs:63-88). Clutch lever cell RMB -> `ToggleEngaged` (BlockEntityTransmission.cs:95-101); every other click on a built transmission is swallowed (BlockTransmission.cs:123-142).

**PROCESS** (X/Networks/MpEnergyNetworkState.cs:76-84; ExlibConfig.cs:120,126,132; C:831,836,857): tau_fric = 0.05omega + 0.5; omega clamped to [0, 2 rad/s]; I = 10 (normal) / 150 (large) + 0.5 per shaft/bevel. Consequences (derived from those constants, not measured): the axle must exceed 0.5 rated speed to move the run at all and >=0.6 to reach omega_max; a bare normal wheel spins up in ~45 s (large ~11 min) and coasts down in ~40 s. A hot mill pass (0.34) leaves 0.06 N.m spare at full drive, so it does **not** slow the run; a run with no storage node has no state (`X/Networks/MpEnergyNetwork.cs:76-82`).

**READOUTS** (BlockEntityFlywheel.cs:266-295; L:74-77): "Idle (not driven)" when no state; else "Charge: N% (E kJ)", "Speed: N rpm" (2 rad/s -> 19 rpm), and "Supply x / Draw y" only when either >1 W. !! Power prints in **kW at one decimal** and energy in kJ (X/Helpers/ExMeasure.cs:118-149,:288-291): at this calibration (2 W, 20 J) every reading is "0.0 kW" / "N% (0.0 kJ)". Sync throttled to 2 % steps (:250-264). Disc plays `cycle` at omega/2pi, `idle` below 1 % of omega_max (:141-174; E/EnergyAnim.cs:13-27). **Shafts and bevels never animate** (D/machines/flywheel-and-shafting.md:161-165). Stall = disc drops to idle pose + "Speed: 0 rpm"; no other tell.

**KNOWN GAPS** - bevel gear uncraftable; no shaft support block; bridge ignores shaft speed (D/mechanics/mp-energy.md:359-374); stale comments C:817-820,:840 ("torque not modelled"). UNVERIFIED: whether a vanilla waterwheel reaches hub speed >=0.6 (BridgeDriveTorque input).

---

## 2. Rolling mill

**BUILD** - `PRP,PGP,PHP` 3x3: **6** `iiex:castplate-heavy` (one per P cell), 1 stack of 2 `game:rod-*`, 1 `iiex:spurgear`, hammer -> `forming-rollingmill-we` (R/FormingRecipeDefinitions.cs:34-50; cost row `rollingmill-grid` IiexRecipeConfig.cs:163). (Docs say four plates - D/machines/rolling-mill.md:639-640; the pattern has six.) Not RCC. **Every roll set is creative-only**: `iiex:rollset-flat|flatwide|grooved`, `MaxStackSize 1`, placeholder ingot art (F/RollSetItemDefinitions.cs:45-105; R/FormingRecipeDefinitions.cs:10-11; D/items/roll-sets.md:275-278).

**STRUCTURE** - 3x3x2 in the `we` frame: principal `(0,0,0)` east end of the axle line, invisible `forming-millaxle-*` nodes at x-1, x-2, roll stand row above the axle, decks at z=+/-1 (F/Blocks/BlockRollingMill.cs:72-97,:160-180). `ns` = 90 deg (:104): axle cells run **south** of the principal, decks west/east (X/Helpers/ExOrientation.cs:32-43). Drive faces: the principal's two axis faces and the far axle cell's far face. Input deck = north row (`we`) / west row (`ns`) unless the run is reversed, which swaps decks (F/BlockEntities/BlockEntityRollingMill.cs:361-383). Completion tell: none - placement is atomic; refusal `notenoughspace` (:118-136).

**VERBS** (BlockRollingMill.cs:229-253; fillers forward clicks and info to the principal: X/Blocks/Structures/BlockStructureFiller.cs:228-258,:367-393)
- Wrench (`Code.FirstCodePart()=="wrench"`) **while rolling** anywhere -> piece returned unchanged (:255-270; BE :344-354). Help `iiex:rollingmill-help-free` (L:87).
- Roll set (any stack with `rollset` attribute) anywhere -> fit/swap; refused mid-pass with `iiex-rollingmill-busy` (L:102) (:272-301; BE :228-247).
- Stock, plain RMB on an input-deck cell -> feeds the **near** side (index `sides-1`); **sneak** -> far side (index 0) (F/MillFeed.cs:70-71; help L:100-101). Click position along the deck picks the gap: 0..1 along the three deck cells, split into `gapCount` equal bands, thickest gap at the **far** end from the principal, thinnest beside it (BlockRollingMill.cs:355-375; MillFeed.cs:59-64,:84-85; pinned at both facings by `test/.../RollingMillStationTests.cs:275`). Bar branches have 4 rungs -> 0.75-cell bands; rod/beam/heavyplate/slab branches have 2 -> half-deck each (routes `assets/iiex/config/processroutes/*.json`).
- `game:rod-iron`, `iiex:beam`, `iiex:heavyplate` are admitted and converted on the deck to `iiex:stock-rod|beam|heavyplate` at the same heat (BE :316-338; F/StockForm.cs:198-202).
- Output deck / principal: nothing. Single click only (:377-383).
- Refusals -> `SendIngameError` (:338-347; L:86,103-107): `NoRollSet`, `WrongForm`, `NoReduction` (also returned for **any feed while a pass runs**, BE :259-260 - misleading text), `TooCold`, `PartCropped`, `WontBite`. Order of checks MillFeed.cs:113-142.

**PROCESS** - bite `delta_max=mu^2R`, HotFriction 0.3 / ColdFriction 0.055, R=4 -> 0.36 hot (F/RollingPass.cs:21,25,31-32; C:883). A gap = two rounds (half-step, then rung); draft per round 0.25 (F/WorkPiece.cs:34,:89-117). Sides = ceil(width/barrel) (:50-53): bar on `flat` (barrel 4) becomes 2 sides from the 1.5 gap; `grooved`/`flatwide` barrel 16 never overhang (RollSetItemDefinitions.cs:51-81). Gates: stack temperature must be >= `RollingTempC` 900 C (MillFeed.cs:136; C:868). Load: 0.34 N.m while rolling x flow stress (1 -> 10 over 400 C below 900) (BE :517-526; C:873,878,893). Travel `omega.R.dt` per 250 ms tick, zero when cold or stopped -> `_stalled` (BE :99,:543-581); a fresh bar's first round ~18.2 units ~2.3 s at omega_max (derived from WorkPiece.cs:160-167, StockForm.cs:49-59). Cooling under the rolls 0.005x(2/t+2/w)/s (C:909; BE :533-556). Reduction committed only in `CompletePass`; piece spawned on the output deck at +0.5,+0.6,+0.5 (:428-447,:493-506). Claim: if the landed rung names a `code`, the piece becomes that item, heat carried across (:458-491) - today `shingledbar` flat 2.0 -> 1x`iiex:beam`, `rod` flat 1.0 -> 1x`iiex:nailplate`, `heavyplate` flatwide 1.0 -> 1x`iiex:boilerplate` (processroutes/shingledbar.json:46-50, rod.json:38-44, heavyplate.json:19-25). All other rungs stay stock for the shear. Cast forms (`castbillet` flat 2.5-1.0; `castbloom`/`castslab` flatwide 3.5-1.0) come from `assets/siex/config/processroutes/*.json` and register only with siex loaded (`src/IronIndustryExpanded/Items/CastStockItemDefinitions.cs:124-131`).

**READOUTS** - L:111-113: "Idle - no stock under the rolls" / "Rolling stock at T C" / "Jammed - the run stalled under the load (T)" (BE :596-612). !! "Jammed" is also set by cold stock while the run still turns (:560-570). Fitted set is **not** shown. No sound, no particles, no animator; all four roll families render at once and `cycle` never plays (grep of F: none; `assets/iiex/shapes/forming/rollingmill.json` elements; D/machines/rolling-mill.md:125-137). Piece on the deck is an item entity drawn by `ItemStockPiece`: the route's stage element if the piece carries a family, else the base shape scaled (F/Items/ItemStockPiece.cs:22-112; F/StockMesh.cs:26-94). The base stage (fresh) uses `selectiveElements` `ShingledBar1` etc. (F/StockItemDefinitions.cs:64-68).

**KNOWN GAPS** - `flatwide` "movable top roller" is a comment, not code (RollSetItemDefinitions.cs:62-65; D/machines/rolling-mill.md:58-66). Mill `_tempC` is never written back to the stack (BE :130,:344-354). A creative piece carries no temperature -> refused `TooCold` until heated (MillFeed.cs:136). Wrench on an **idle** mill falls through to `IWrenchOrientable.Rotate`, which exchanges the block without re-laying fillers/axle cells (BlockNetworkNode.cs:342-400,:440-441; no override in F) - UNVERIFIED whether the vanilla wrench item reaches it; check the "Rotate" hint. Item spawns inside a full-cube invisible deck filler (BlockStructureFiller.cs:48) - UNVERIFIED that it is reachable.

---

## 3. Crop shear

**BUILD** - `PRP,PGP,_H_`: 4 `castplate-heavy`, 2 rods, 1 spurgear, hammer -> `forming-shear-ns` (R/FormingRecipeDefinitions.cs:57-73; row IiexRecipeConfig.cs:164). Blade sets **are craftable**: `PP,PP,_H` = 4 `game:metalplate-{iron|steel}` + hammer -> `iiex:shearblade-{metal}` (:152-168); tiers iron 1, steel 2 (F/ShearBladeItemDefinitions.cs:19-22), `machinetool` attribute, stack 1 (X/Processes/MachineTool.cs:25,:85-104).

**STRUCTURE** - `ns` frame (creative default): `I O #` with three floor slabs `_ _ _` above; nest at x-1, principal `O`, gear cell x+1; `we` = 90 deg (F/Blocks/BlockShear.cs:75-99). Slab collision = lower half (X/Blocks/Structures/StructureFootprint.cs:75-82). Drive on the principal's N/S faces (`ns`); the drawn shaft is along Z in that cell (`shear.json` `Shaft` x6-10,z0-16 - measured). `Animatable` declared, no animator (:61).

**VERBS** (:176-201) - wrench while stroking -> piece back (:206-218); any `machinetool` stack anywhere -> fit/swap, refused mid-stroke silently (:220-245); empty hand + sneak -> blades back (:247-260); stock on the **nest cell only** -> crop (:262-282); single click (:285-300). Help on nest: L:117-119. Refusals L:123-128 (:341-351); order NoBladeSet -> NoJob -> Spent -> BladeTooSoft -> NotTurning -> NotEnoughDrive (F/ShearFeed.cs:82-116). A click mid-stroke reports **NotTurning** (BlockEntityShear.cs:102-103).

**PROCESS** - table `assets/iiex/config/processjobs/shear.json`: `stock-shingledbar` 2.0 grooved -> 4 `game:rod-iron` (0.2, 2 s) :5-13; `stock-beam` 1.0 flat -> 2 `game:metalplate-iron` :14-22; `stock-shingledslab` 2.0 flatwide -> 2 `iiex:heavyplate` (0.3, 3 s) :23-31; `stock-rod` 1.0 grooved -> 4 `iiex:rivetrod` :32-40; **whole-item** `game:metalplate-iron` -> 2 `iiex:nailplate` :41-47. siex adds cast rows, all `minTier 2` (steel blades): billet 1.0 flat -> 3 plate; bloom 2.0 -> 5 `blank`, 1.0 -> 5 `skelp`; slab 1.0 -> 5 `boilerplate` (`assets/siex/config/processjobs/shear.json:5-44`). Job keyed on (input code, stage, roller family the piece last saw) (BlockEntityShear.cs:82-94; X/Processes/ProcessJobRegistry.cs:61-76). Staged job: one product per stroke, remainder written back with `stockCropped`+1, `Spent` at `count` (BlockEntityShear.cs:136-167; WorkPiece.cs:123-140); whole-item job: all `count` at once, input consumed. Torque gate: required = `minTorque` hot, x`ShearColdMultiplier` 2 below 900 C (ShearFeed.cs:62-70; C:946); available = `SupplyPower/Speed` (BlockEntityMpBench.cs:172-173) - at full bridge (1.0) every shipped row passes hot or cold; `NotTurning` when omega=0. Stroke = `seconds` counted only while omega>0 (BlockEntityMpBench.cs:104-116). Products spawn at the **principal** +0.5,+1.0,+0.5 (:149-153) - inside the slab's lower half; UNVERIFIED reachable. Products carry no temperature; the remainder keeps its stack heat.

**READOUTS** - "Blades fitted - tier N" / "No blades fitted", "Cutting - Ns left" (BlockEntityShear.cs:227-239; L:120-122). No animation/sound (no animator in F).

**KNOWN GAPS** - machines.txt wants a window + hold-RMB; built as click-with-item (D/mechanics/machining-line.md:361-364,:372-381; `docs/internal/workbench/machines.txt:60-75`). A beam rolled on `flatwide` carries family `flatwide` and matches no crop row (shear.json:14-22 says `flat`). Blade wear unbuilt. Not walked (D/machines/shear.md:6-7).

---

## 4. Nail cutter / riveter (`forming-bench`, `type` variant)

**BUILD** - nail cutter `_R_,PGP,_H_`: 2 `castplate-heavy`, 1 rod, 1 spurgear, hammer -> `forming-nailcutter-ns` (R/FormingRecipeDefinitions.cs:81-97); riveter `PRP,PGP,_H_`: 4 plates, 2 rods, gear, hammer -> `forming-riveter-ns` (:104-120). Dies **are craftable**: nail die `PP,_H` (2 `game:metalplate-iron` + hammer), rivet die `P_,PH` (:127-145) -> `iiex:die-nail`, `iiex:die-rivet`, stack 1, ingot placeholder art (F/BenchDieItemDefinitions.cs:47-66; X/Processes/ItemDie.cs:108-134). Rows IiexRecipeConfig.cs:168-170.

**STRUCTURE** - nail cutter `zy` slice: `I m / O #` - 1 wide, 2 deep (+Z), 2 tall; working face `I` = cell **above** the principal (F/Blocks/BlockFastenerBench.cs:104-116,:142-145). Riveter `xy` face: `# M # / I O #` - 3 wide, 2 tall; face `I` = cell **west** of the principal (`ns`) (:126-138). Both `ns` default, `we` = 90 deg (:152). Drive on the principal's N/S faces; the drawn shafts run along Z one cell up (`nailcutter.json` `Shaft` z8-23 y23-25; `riveter.json` `Shaft1` in the M cell - measured), i.e. the "connector on the principal, not `m`/`M`" simplification (D/machines/nail-machine.md:48; D/machines/rivet-machine.md:244-247).

**VERBS** (:233-258) - wrench while stroking -> blank back; `machinejob` die anywhere -> fit/swap; empty + sneak -> die back; blank on the face cell -> press (:319-338); single click (:340-347). Help L:134-136; refusals L:140-143, order NoDie -> NoJob -> NotTurning -> NotEnoughDrive (F/BenchFeed.cs:54-69). A die naming another machine gives `NoJob` (BlockEntityFastenerBench.cs:68-94; test `FastenerBenchTests.cs:205`).

**PROCESS** - nail: `iiex:nailplate` -> 4 `game:metalnailsandstrips-iron`, minTorque 0.2, 2 s; rivet: `iiex:rivetrod` -> 2 `iiex:rivet`, 0.3, 3 s (BenchDieItemDefinitions.cs:27-36,:47-62). Whole-item: blank consumed, bundles ejected at principal +1.0 (BlockEntityFastenerBench.cs:126-141; MpBench :149-153) - that cell is a **solid filler** on both benches; UNVERIFIED reachable. No temperature gate. Rivet = 12.5 u, stack 64 (`src/IronIndustryExpanded/Items/FastenerItemDefinitions.cs:23-52`).

**READOUTS** - "Die fitted - {name}" / "No die fitted", "Working - Ns left" (:192-204; L:137-139). No animation/sound.

**KNOWN GAPS** - hold-RMB/window unbuilt (machines.txt:128-141,:172-184; D/machines/rivet-machine.md:312-320); die wear unbuilt; class doc says nail-cutter shaft "west-east" (BlockFastenerBench.cs:28) while the shape/connectors run N-S.

---

## 5. Stock between stations

Codes: `iiex:stock-shingledbar` (400 u), `stock-shingledslab` (1200), `stock-rod` (100), `stock-beam` (400), `stock-heavyplate` (600) - all `MaxStackSize 1`, `ItemStockPiece`, creative-listed (F/StockItemDefinitions.cs:22-33,:57-85); products `rivetrod` 25, `nailplate` 100, `beam` 400, `heavyplate`/`boilerplate` 600, `blank`/`skelp` 200, stack 16 (F/RolledItemDefinitions.cs:24-42,:132-145). State on the stack: `stockForm`, `stockThickness`, `stockGap`, `stockFed[]`, `rollerFamily`, `stockCropped` (WorkPiece.cs:36-41,:227-247). Heat is vanilla's `temperature` attribute (StockItemDefinitions.cs:73-74) read via `GetTemperature` at feed/crop time (BlockEntityRollingMill.cs:264-267; BlockEntityShear.cs:105-108); vanilla's decay rate is UNVERIFIED here (no vendored source in this checkout). Mill claims carry heat across (:485-489); shear/bench products are minted cold; admitted feedstock inherits heat (:332-336). `temperatureDamage 4` on stock (:83).

---

## Tester checklist
1. Waterwheel -> axle -> flywheel hub: "Idle (not driven)" turns into "Charge/Speed"; record the rpm reached and whether the axle beats the 0.5/0.6 thresholds.
2. Power/energy lines read "0.0 kW"/"0.0 kJ" - confirm and log the readout scale problem.
3. Shafts/bevels stay visually still while the disc spins; bevel gear only from creative.
4. Clutch lever cell toggles both shafts' spin; x2/x4 north side turns slower.
5. Mill: creative bar is refused `TooCold`; heated bar at 2.5 band feeds; near vs sneak side; band order along the deck at **both** facings; the ejected piece is reachable and drawn at its stage.
6. Walk bar->grooved 2.0 (4 feeds), shear -> 4 `rod-iron` + remainder x4 then `Spent`; part-cropped piece refused at the mill.
7. Rod fork: `game:rod-iron` (hot) on grooved -> 1.0 -> 4 `rivetrod`; on flat -> 1.0 -> `nailplate` claimed at the mill.
8. Shear/bench refusal messages appear in the right order; mid-stroke click shows "shaft is at rest" text.
9. Benches: nail die + nailplate -> 4 nails; rivet die + rivetrod -> 2 rivets; wrong die -> `NoJob`; outputs not stuck in the filler above the principal.
10. Wrench on an idle mill/shear/bench/flywheel: does a "Rotate" hint appear and does rotation strand fillers?
