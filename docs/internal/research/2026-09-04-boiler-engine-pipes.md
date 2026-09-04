# Research snapshot - boiler-engine-pipes

**Written** 2026-09-04 by a read-only research agent, against commit `cf2e8c89` (the tree before the
line-ending renormalisation). **Covers** Cornish boiler megablock, Watt engine + sub-machines (B20/B21), plated and cast pipes, fittings, condenser, manual pump, intake.
**Purpose** grounding for the roadmap's Phase 1 walkthrough and Phase 2 machining-line plan
(`docs/internal/plans/2026-09-04-*.md`).

Line numbers were right on that day and drift; cite by symbol when reusing. "UNVERIFIED" marks are the
agent's own and were not re-checked. Nothing here is a decision - decisions live on the design pages.

---

# Cornish boiler / Watt engine / pipes - playtest facts as the code stands (2026-09-04)

All paths relative to `/home/fallen/src/modding-vsexpanded`. Line numbers are from the files as read today (the design pages' own `:line` cites have drifted; I cite code, not the pages' numbers).

## 1. Cornish boiler (`iiex:boilercornish-{n,e,s,w}`)

**BUILD**
- Frame, grid 3x2 `PHP,BNB`: P = `metalplate-*` x1 (2 total), B = `game:burnedbrick-fire` x2 (4), N = nails-and-strips x2, H = hammer -> `iiex:boilercornish-n` (`src/IronIndustryExpanded/Recipes/Grid/MachineRecipeDefinitions.cs:56-64`). No pipe, no diagram. Cost keys `boilercornish-grid`/`-rcc` (`src/IronIndustryExpanded/IiexRecipeConfig.cs:178,174`).
- Placement needs the whole 3x6x3 clear or fails `notenoughspace` (`src/ExpandedLib/Blocks/Structures/BlockFilledMegastructure.cs:36-47`).
- RCC stages (`src/IronIndustryExpanded/BlockStructures/Boiler/Blocks/BlockBoilerCornish.cs:157-178`; golden `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/boiler/cornish.json`):

| # | plates | `iiex:rivet` | rods | fire brick | raises |
|---|---|---|---|---|---|
| 1 | - | - | - | - | `MasonryBase` (free click) |
| 2 | 6 | 8 | - | 8 | `BoilerCasing`, `CasingSegment5` |
| 3 | 8 | 8 | 4 | - | `Flues`, `CoalLayers` |
| 4 | 8 | 16 | 4 | 36 | `BoilerEnds`, `MasonryTop` |
| sum | 22 | 32 | 8 | 44 | |

Plates/rods accept iron or steel (`storeWildCard: metal`); rivets only, never nails (golden). Materials are taken from the **hotbar**, not the hand (`.compat/Vintagestory/vssurvivalmod/Systems/RightClickConstruction.cs:238-346`); a shortfall prints `ingameerror-missingstack` (`:310`); the interaction help lists the next stage's stacks (`:143-184`).

**STRUCTURE**
- Authored footprint (`BlockBoilerCornish.cs:114-156`): 40 fillers + principal `O`(0,0,0). `I`(0,1,0) solid = main hatch; `S`(0,2,-3) `Port(UP,"pipe")` = steam, pipe at (0,3,-3); `M`(0,2,-1) slab = man hatch; `E`(1,0,-5) `Port(EAST,"pipe")` = exhaust, pipe at (2,0,-5); `feedwaterFace:"south"` on the principal (`:59`).
- Everything rotates by `StructureAngle = AngleFromSide(side)+180` (`BlockBoiler.cs:44-53`); shape `rotateYByType *-n:180` (golden). `side` comes from the look direction (`src/ExpandedLib/Blocks/Behaviors/BlockBehaviorExOrientable.cs:169-178`) and the body is "raised away from the player" (`BlockBoiler.cs:39-44`). Worked `-n`: `RotateOffset(...,180)` negates z (`src/ExpandedLib/Helpers/ExOrientation.cs:38`), so the barrel runs **south** of the principal; feedwater = principal **north** face; steam pipe cell (0,3,+3); exhaust cell (-1,0,+5), port face **west**, pipe at (-2,0,+5); man hatch (0,2,+1). UNVERIFIED: which compass `side` a given stance yields - confirm in-world.
- Completion tell: HUD boiler lines and hatch help exist only once `IsConstructed` (`BlockEntityBoiler.cs:78, 1086-1087`; `BlockBoiler.cs:465-469`); an uncharged finished vessel shows bare grate bars (`BlockEntityBoiler.cs:200-206`).

**VERBS** (`BlockBoiler.cs:216-408`; both hatches are filler cells, forwarded via `IFillerInteractionTarget`)
- Main hatch, empty hand, hold >= 0.5 s (`:214, :353`): swings the door (`:363`), **or lights** when door open + bed full (16) + unlit (`CanLightBed`, `BlockEntityBoiler.cs:889`). Help shows `iiex:blockhelp-boiler-ignite` or `-mainhatch`, whichever the next hold does (`:479-481`).
- Main hatch open, fuel in hand, click: `TryChargeBed` (`:264-266` -> `BlockEntityBoiler.cs:905-926`), takes `min(free, stack)` units - 16 coal items fill it. Refusals: `iiex-firebox-notfuel`, `-full`, `-wrongfuel` (`assets/iiex/lang/en.json:459-461`). No "take fuel out" verb exists (design `boiler-cornish.md:153-155`).
- Man hatch, empty hand, hold 0.5 s: toggle (`:365`). Open + bucket of water (liquid code containing "water", `:410-415`): pours everything up to 1000 L (`BlockEntityBoiler.cs:932-963`). Open + empty bucket: bails only water **above 300 L** (`:971-1014`). Help `blockhelp-boiler-manhatch/-fill/-drain` (`:501-525`).
- Nothing uses sneak/sprint. Sounds: coke-oven door open/close (`BlockEntityBoiler.cs:875-882`), `WaterPour` (`:959, :1010`), `Ignite` at the fuel cell (`:897`).

**PROCESS** (`BlockEntityBoiler.cs:317-464`, 1 s tick)
- Fuel admitted iff `combustibleProps.BurnTemperature >= BoilerFuelMinTemp` 1000 (`src/IronIndustryExpanded/BlockStructures/Furnaces/BEBehaviorFirebox.cs:34-35`; `src/IronIndustryExpanded/IiexConfig.cs:547`) - lignite (1100) admitted. Rate mult `(flame-Tsat)/(1200-Tsat)` (`:607-616`; design temp `IiexConfig.cs:540`): lignite ~ 0.91 -> 58 L/s and heat-up 198 s; all others 1.0. Bed 4x4 = 16 units (`BlockBoilerCornish.cs:49-56`); one unit per vanilla `burnDuration` (`:505-518`): anthracite 196 s/unit ~ 52 min, bituminous 84 ~ 22 min, lignite 77 ~ 21 min, coke/charcoal 40 ~ 11 min (design `boiler-cornish.md:325-331`). The bed burns down while Heating too (Gotcha 13).
- FSM: Idle->Heating when burning and water >= 300 (`:381-387`; `IiexConfig.cs:1062`); ->Boiling after `180/mult` s (`:400-401`; `:997`); fire out or water < 300 for 10 s -> Idle (`:391-394, 408-414`; `:1000`). Boiling: 64 L/s steam = 4 L/s water (`:590-598`; `:1068`, expansion 16 `:977`). `InternalPressure = steam/(1600-water)` (`:137-138`; `:1059`), hard-capped at 5.0 (`:713-718`; `:1072`); danger zone >= 4.5 (`:144-146`).
- Feedwater: draws from the network on the feedwater face up to 800 L (0.5xcapacity, `:64-65`, `:1008`) at <= 20 L/s (`:352-361`; `:1011`); feed pressure > 1 atm while Boiling flashes extra steam (`:365-372`).
- Choke: exhaust-run pressure >= 0.8 (`:330-333`; `:991`) -> `Choked` line; after 10 s (`:1017`) the fire is snuffed with the extinguish sound and the line clears (`:341-347`, Gotcha 7). Exhaust 16 L/s "Exhaust" at 0.6xT_steam (`:454-461`; `:1014`).
- Steam port unpiped: bleeds 16 L/s only (`:662-669`; `:1039`) - not a relief. Man hatch open: vents 200 L/s to 1 atm while running, to 0 when Idle, and resets the burst clock (`:429-431, 725-735`; `:1035`).
- Burst: `Boiling && burning && P >= 5` for 30 s (`:438-451`; `:994`) -> `Explode` (`:786-819`): 40 % of construction stacks spawned (`:1027`), fillers removed and principal set to air (`:799-801`), every block within radius 3 with resistance < 20 broken (`:834-853`; `:1022`, `:1074`), then `CreateExplosion(centre,(0,1,-2), EntityBlast, 3, 5)` (`:813-818`).
- **Running dry does not burst**: water < 300 stops `BoilStep` (`:416-421`) and shuts down after 10 s. The burst needs water >= 300 and a blocked/unpiped steam side: unpiped, 1000 L water -> ceiling in ~ 60 s, burst ~ 30 s later, before the water runs out. Gate step 8 is mis-worded.

**READOUTS**
- HUD (`:1081-1141`; keys `en.json:557-566`): `firebox-fuel` "Firebox: {fuel}, {n} / 16 units" or `firebox-empty` (`BEBehaviorFirebox.cs:313-316`); `Water: {x} / 1000 L`; `Steam: {L} ({atm})`; one of `Boiling {rate} steam at {T}` / `Heating up... {%}` / `Needs at least 300 L...` / `Idle`; then `Main hatch open`, `Man hatch open`, `Choked, exhaust backing up!`, `Over-pressure! {s}s until bursts!`. Unfinished: no boiler lines; vanilla shows the stage only in entity-debug mode (`BEBehaviorRightClickConstructable.cs:75-82`).
- Shipped clips in `assets/iiex/shapes/boiler/cornish.json`: `idle` (30 f, Repeat), `mainhatchopen` (30 f, Hold), `manhatchopen` (30 f, Hold) - poses only, no gauges. Coal courses `CoalLayers/L1..L4` drawn one per 4 units in the charged item's own texture (`BlockEntityBoiler.Client.cs:155-162, 210-224`).
- Water surface: `BoilerWaterRenderer`, box (-8,4,-60)-(14,30,12) (`BlockBoilerCornish.cs:102-109`), levels 0 / 0.2 / 0.99 by dry / <300 / >=300 (`Client.cs:64-67`). UNVERIFIED in a world - plan CB6.4 steps 2-3 unticked (`docs/internal/plans/2026-08-23-cornish-boiler-megablock.cs:1050-1077`).
- FX: boiling `Lava` hum at the man-hatch cell (`Client.cs:72-81`); steam plumes + hiss: danger 4 at man hatch, open hatch 6, unpiped port 8 at the S cell (`:97-133`).

**KNOWN GAPS** - CB10 (`plan:1449-1478`): Lancashire still coal-pile; cowper pile; Watt dead gear (B20); `LangCallSites` skips capitalised keys; `ShapeExtents` not wired to machines; 4 + 17 `iron4` shapes. Design Open (`boiler-cornish.md:732-747`): feedwater ignores back-pressure (a 1 atm manual pump feeds a 5 atm boiler); water box unmeasured; burst leaves a hole; no away catch-up. Walk should also check Gotcha 4 (`docs/iiex/handbook/05-steampower.html:20-22` still says iron 5 / steel 10 atm).

## 2. Watt engine and sub-machines (old art)

**BUILD**
- Grid `_H_,PRP,PIP`: 4 plates, 2 rods, 1 `iiex:pipe-plated-straight-*`, hammer -> `iiex:enginewatt-n` (`MachineRecipeDefinitions.cs:67-77`). **B20**: `Gear(gear,2)` declared at `:74` but no `G` in the pattern, and the loop at `:23-27` emits it twice (`docs/internal/plans/STATE.md:28`).
- RCC 6 stages (`BlockStructures/Engine/Blocks/BlockEngineWatt.cs:49-75`): 1 free `Cylinder`; 2: 2 plate, 4 rod, 4 nails, 36 fire brick; 3: 8 rod, 4 nails; 4: 2 plate, 4 rod, 2 nails; 5: 4 rod, 2 nails; 6: 4 rod. sum 4/24/12/36 - nails accepted here.
- Fluid pump `_HG,PIP,RIR`: 2 plates, 1 gear, 2 plated straights, 4 rods (`:79-89`). MP generator `_H_,GAG,PRP`: plates x2 in two cells, 2 rods, 4 gears, 1 `game:woodenaxle-ud` (`:104-114`). Air blower is siex's; recipe UNVERIFIED (not read). Repair: wrench + 4 plate + 2 rod (`BlockEngineWatt.cs:77-81`).

**STRUCTURE**
- 1x4x3 slice: principal (0,0,0), filler (0,0,1), drive cell (0,0,2) left empty, three rows above (`BlockEngineWatt.cs:35-48`). `BodyAngle = side+180` (`BlockEngine.cs:37-45`), shape spin 180 (`:231`), so for `-north` the body and drive cell run north; steam inlet = principal **south** face, condensate **east** (`:30-34, 58-63`).
- Snap: `SubmachineSide` north->east, east->south, south->west, west->north (`:93-100`). Engine placed last: `ReorientSubmachine` via `ExchangeBlock` (`:167-193`); sub-machine placed last: `BlockEngineSubmachine.TryPlaceBlock` (`BlockEngineSubmachine.cs:13-47`) - both verified through `TryFindEngineFor` (`BlockEngine.cs:107-131`).
- Pump connectors: DOWN = source, "left" = WEST rotated by the pump's own side = delivery (`Blocks/BlockEngineFluidPump.cs:38-45`) - the intake main must run **under** the pump. Blower: left face only (`src/SteelIndustryExpanded/BlockStructures/Engine/Blocks/BlockEngineAirBlower.cs:41-47`). Generator: axle on N/S (E/W for e/w sides) (`Blocks/BlockEngineMPGenerator.cs:40-53`); it is a vanilla-MP source - couple it to the flywheel hub, not the mpenergy graph (`docs/design/mechanics/mp-energy.md:107-133`).

**VERBS** - a held placeable block falls through to vanilla placement (`BlockEngine.cs:356-358`); a broken engine answers only RMB-with-wrench: no wrench -> `engine-repair-wrench` + bill in chat (`:383-390`), materials short -> bill (`:400-403`), creative free (`:393-394`), success `engine-repaired` (`:414-420`); help `blockhelp-engine-repair` (`:296-301`). Sub-machines have no interaction at all.

**PROCESS** (`BlockEntityEngine.cs:229-294`): inlet counts only if medium is `Steam` (`:246-247`); engaged iff pressure >= 2.0 (`IiexConfig.cs:1079`) **and** a sub-machine demands (`:269-270`; no sub-machine -> inert but HUD says Nominal); draws 30 L/s (`:1088`), power 0.3 x supplied fraction (`:1085`); condensate 1 L/s at 90 C, 0 atm out east, else a water-jet spill (`:280-282, 300-315`; `:1091`). Break: > 4.0 for 60 s (`:252-263`; `:1082`, `:1102`) -> 120 steam + 80 smoke + explosion sound (`:108-134`). Pump: delivery pressure = inlet x 0.75 (`BlockEntityEngineFluidPump.cs:44-45`; `:1099`), amount = `16.67 x 3 x power` (`:46`) = **15 L/s**, the undocumented x3 (design `engine-watt.md:629-631`); blower `48 x 3 x 0.3` = 43 L/s (`BlockEntityEngineAirBlower.cs:51`; `SiexConfig.cs:115`). Generator budget 0.2625, stall above 0.525 (`BlockEntityEngine.cs:159-171`; `:1111`).
- **B21** (`BlockEntityEngineSubmachine.cs:120-139`): the "inverse" rotates (0,0,2) by `AngleFromSide(sub side)+180`. North engine -> sub side east -> angle 90 -> offset (2,0,0) (`ExOrientation.cs:37`) -> it probes 2 cells **west** of the pump; the engine is 2 cells **south**. It always falls to the fallback that takes the first `BlockEntityEngine` found N, E, S, W two cells out (`:133-137`; order `.compat/Vintagestory/vsapi/Math/BlockFacing.cs:87`), unverified. **To observe**: with engine A (north) + pump working, place a second Watt *frame* (unbuilt is enough - the BE exists) two cells west or two cells north of the pump, then break and re-place the pump (`EnginePos` is cached, `:39`). A's beam keeps cycling and its HUD reads "Draws steam 30 L/s" (A sees the pump; the pump's demand is 1 because *some* engine resolved, `:53`), but the pump's piston stays idle, no watering loop, no water moves - `DoWork` reads the decoy's `AvailablePower` 0. Corollary for Gate step 6: put the two engines side by side; one placed in line 4 cells north of the other cross-binds.

**READOUTS** - HUD (`:393-419`): `Broken! Repair with a wrench.` or `engine-info-clock-{over|nominal|under|idle}` (`:178-182`), `Draws steam 30 L/s` while running, `Over-pressure! Will break in {s}s`, Watt adds `Operating band 2-4 atm` (`BlockEntityEngineWatt.cs:32-37`). Clips `idlepump/cyclepump/idlemp/cyclemp`, speed 0.5+power (`:288, 573-600`); sub-machine `idle/cycle` phase-locked (`Submachine:195-255`); generator drives `cyclemp` from the axle (`:544-571`). Sounds: gear hum at (0,3,1) (`:687-702`), stroke sounds, over-pressure puffs every 200 ms (`:677-683`), pump `Watering` loop (`FluidPump:72-88`), generator `MetalGrinding` (`MPGenerator:75-91`). Pump and generator print no HUD of their own.

**KNOWN GAPS**: B20, B21 (`STATE.md:28-29`); x3 and handbook `07-engines.html:22,27` 3x low; "two helve hammers" vs 0.2625 (`engine-watt.md:428-432`); repair bill untranslated (Gotcha 15); `IsMPGenerator` substring (`BlockEntityEngine.cs:504-510`); no editable engine shape; roadmap: old shapes, new ones need the grown footprint (`docs/internal/plans/2026-09-04-roadmap.md:86`).

## 3. Pipes and fittings

**BUILD** - plated segments craftable per fastener (nails *or* rivets): straight `HPN` -> 2, bend `_H,NP,_N`, T `_H_,NPN,_N_`, X `HN_,NPN,_N_` (`Recipes/Grid/PlatedPipeRecipeDefinitions.cs:19-60`). **Cast segments: creative-only** - `CastPipeRecipeDefinitions.cs` holds only fittings, no recipe anywhere outputs `pipe-cast-{straight,bend,tjunction,xjunction}` (grep; `MachineRecipeDefinitions.cs:117-119`; `docs/design/machines/cast-pipes.md:165-174`). Fittings, all worked from a **plated** straight: passthrough `BHB,BPB,B_B` (12 brick + 1), bend passthrough (12 + 2), outlet `BHB,BNB,BPB` (12 brick + 1 + 2 nails), valve `_H_,GPL,_L_` (1 `pipe-plated-straight-ns` + 2 plate + 2 gears), pressure valve `_H_,LPL,G_G` (+ 4 gears) (`CastPipeRecipeDefinitions.cs:20-88`). Fluid intake `_H_,PIP,NPN` (3 plate, 1 straight, 4 nails), condenser `_H_,IPI,NP_` (2 straight, 2 plate, 2 nails), manual pump `_GH,PIP,BRB` (2 plate, 2 gears, 1 straight, 2 rod, 8 `game:supportbeam-*`) (`MachineRecipeDefinitions.cs:33-51, 91-102`).

**STRUCTURE / PROCESS** - tiers: plated 2.5 atm / 50 L/s, cast 5.0 / 120 (`IiexConfig.cs:175,185,189,194`; registered `IronIndustryExpandedModSystem.cs:88-105`), both flanged so they join (`:96,105`); 30 L per node, leaks 8 gas / 10 water, evaporation 50 L/day, burst grace 30 s (`src/ExpandedLib/ExlibConfig.cs:52-67`). A plated steam main cannot hold a Cornish (2.5 < 5) (`cast-pipes.md:350-353`), so the walk must spawn cast pipe. Pressure valve: endpoint, sides never merge (`Blocks/BlockPressureValve.cs:70`); gate steps 0.25, 0-5 atm (`BlockEntities/BlockEntityPressureValve.cs:27-39, 61-70`). Condenser: N steam, W/E water (`Blocks/BlockSteamCondenser.cs:76-93`), 30 L/s steam -> /16 water (`IiexConfig.cs:1130`; BE `:143-150`); no water line -> vents gas (`:112-140`), no outlet -> sprays (`:152-165`). Intake: only on water (`iiex-fluidintake-nowater`, `Blocks/BlockFluidIntake.cs:52-77`), needs a 3-deep cube (`BlockEntityFluidIntake.cs:72-90`; `:1122`), 6-block exclusion (`:1125`), source head 1 atm (`:37-45`). Manual pump: 2 cells tall, input = south rotated (crank-support side), output = north rotated (`ManualPump/Blocks/BlockManualFluidPump.cs:63-71`), 2 L/s at 1 atm (`BlockEntityManualFluidPump.cs:121-145`; `:1119`), not wrench-orientable (`docs/design/machines/pumps.md:502-504`). Outlet connects on its single orientation face (`Blocks/BlockPipeOutlet.cs:74-75`); only `-u` + a chimney vents, 16 L/s (`IiexConfig.cs:199`, `cast-pipes.md` Gotcha 10).

**VERBS** - valve: empty-hand RMB toggles/severs (`Blocks/BlockValve.cs:58-85`, help `blockhelp-valve-toggle`). Pressure valve: empty-hand RMB raises, sneak+RMB lowers, wrench flips direction (`BlockPressureValve.cs:73-123`). Manual pump: hold empty-hand RMB on either cell (`BlockManualFluidPump.cs:120-212`); release or 1.2 s without a step stops it (`BE:103-114`). `.exmod network hi` / `unhi` (`src/ExpandedLib/Commands/NetworkSubCommand.cs:22-39`; text `assets/exlib/lang/en.json:63-67`).

**READOUTS** - pipe (`src/ExpandedLib/Blocks/Networks/BlockEntityPipe.cs:306-361`, keys `exlib/lang/en.json:71-81`): `Leaking!` if any open face; water: `Throughput: {L/s} of Water at { C}` + `Pressure: {atm}(g)`; gas: same with `Steam/Exhaust/Air/Gas`, plus `Over pressure!` on burstable cells at rating; else `Empty`. Pressure valve: `Gate pressure: {g}-{5}(g)`, `Venting {L} of gas` (`BE:259-273`). Condenser `Condensing steam`/`Idle` (`BE:275-287`). Intake `Drawing water...` / `Not enough water below!` / `Another intake is too close!` (`BE:114-123`).

**KNOWN GAPS** - cast tier uncraftable (B-class, `cast-pipes.md:496-499`); handbook 05 stale tiers; refused joints do not leak (Gotcha 3); manual pump has no HUD (`pumps.md:546-548`); dead cast cost keys still catalogued (`IiexRecipeConfig.cs:187-190`).

## Gate checklist (`docs/internal/plans/2026-08-23-cornish-boiler-megablock.md:1480-1492`)

- [ ] 1. Craft the Cornish boiler frame (`PHP,BNB`) and place it - 3x6x3 clear.
- [ ] 2. Four RCC stages from the hotbar, each course appearing (22 plate / 32 rivet / 8 rod / 44 fire brick).
- [ ] 3. Hold to open main hatch; click 16 **lignite** in; hold to light (bed full); hold to shut. Expect ~ 58 L/s and ~ 198 s heat-up.
- [ ] 4. Feedwater pipe on the principal's feedwater face; steam pipe on the cell above `S`.
- [ ] 5. Exhaust pipe across the `E` port; block it -> `Choked...` within a tick, fire out after 10 s (line vanishes).
- [ ] 6. Two Watt engines side by side (not in line) on a **cast** main through a pressure valve at 2-3 atm.
- [ ] 7. Man hatch: bail with an empty bucket (stops at 300 L); open under pressure -> 200 L/s plume, burst clock resets.
- [ ] 8. Burst: keep >= 300 L and fire with the steam side unpiped/blocked - `Over-pressure! 30s` -> explosion. (Running dry only shuts down.)
- [ ] No fire brick placed by hand.

Engine and pipe lines:
- [ ] E1. Sub-machine placed at an engine's drive cell snaps 90 deg CW; engine placed onto a sub-machine snaps it likewise.
- [ ] E2. Engine without a sub-machine draws nothing and still reads `Nominal`.
- [ ] E3. Pump delivers ~ 15 L/s at 0.3 power (x3), delivery pressure = inlet x 0.75.
- [ ] E4. B21 repro: decoy Watt frame 2 W of the pump, re-place pump -> engine cycles, pump idle.
- [ ] P1. Plated main on the boiler bursts (2.5 < 5); cast main holds at `Over pressure!` without failing.
- [ ] P2. `.exmod network hi` colours the runs; pipe HUD shows throughput / medium / pressure / `Leaking!`.
