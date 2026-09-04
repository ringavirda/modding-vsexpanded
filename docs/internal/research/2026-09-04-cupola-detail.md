# Research snapshot - cupola-detail

**Written** 2026-09-04 by a read-only research agent, against commit `cf2e8c89` (the tree before the
line-ending renormalisation). **Covers** cupola furnace in depth (sub-agent pass).
**Purpose** grounding for the roadmap's Phase 1 walkthrough and Phase 2 machining-line plan
(`docs/internal/plans/2026-09-04-*.md`).

Line numbers were right on that day and drift; cite by symbol when reusing. "UNVERIFIED" marks are the
agent's own and were not re-checked. Nothing here is a decision - decisions live on the design pages.

---

All facts are in hand; nothing further to fetch. Report follows.

# Cupola furnace - research report

Path legend (all under `/home/fallen/src/modding-vsexpanded/`): **DOC** `docs/design/machines/cupola.md` . **CORE** `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockCupolaFurnaceCore.cs` . **BE** `.../Furnaces/BlockEntities/BlockEntityCupolaFurnace.cs` . **SHAFT** `.../Furnaces/BlockEntities/BlockEntityShaftFurnace.cs` . **FC** `.../Furnaces/BlockEntityFurnaceCore.cs` . **TAP** `.../Furnaces/Blocks/BlockFurnaceTap.cs` . **HOP** `.../Furnaces/Blocks/BlockHopperTall.cs` . **HOPBE** `.../Furnaces/BlockEntities/BlockEntityHopperTall.cs` . **PILE** `.../Furnaces/Blocks/BlockChargePile.cs` . **REC** `src/IronIndustryExpanded/Recipes/Grid/FurnaceRecipeDefinitions.cs` . **CFG** `src/IronIndustryExpanded/IiexConfig.cs` . **LANG** `assets/iiex/lang/en.json` . **MBB** `src/ExpandedLib/Blocks/Structures/BlockBehaviorMultiblockStructure.cs`.

## BUILD
- Everything is C#-defined; no cupola JSON exists under `assets/iiex` except lang (grep). Blocktype golden: `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/cupolacore.json`.
- **Grid recipe** `iiex:grid/cupola` (REC:130-142; golden `.../goldens/iiex/recipes/grid/cupola.json:1-47`): pattern `BRB,PCP,BRB`, 3x3. B = `game:refractorybrick-fired-*` x4 (tier1/2/3 captured as `{tier}`), R = `game:rod-*` (iron|steel) x2, P = `game:metalplate-*` (iron|steel) x1, C = `game:clay-fire` x8 -> `iiex:furnace-cupolacore-{tier}-n`. DOC:143 says "2 x plate"; code/golden say 1.
- **No RCC stages / requireStacks** anywhere; construction is the multiblock layout. Fittings (REC:43-105): Iron Tap `BPH,BFC,BBB` -> `iiex:furnace-irontap-s`; Slag Tap `B_H,BFC,BBB` -> `iiex:furnace-slagtap-s` (B = `game:refractorybrick-fired-tier3` qty 2, F = fire clay qty 12, hammer, chisel); Tuyere `BHB,BCB,BPB` with P = `iiex:pipe-plated-straight*` -> `iiex:furnace-tuyere-s`; Tall Hopper `_H_,PSP,SPS` -> `iiex:hopper-tall-n`.
- **Diagram**: `iiex:diagram-furnace-cupola` exists (`Items/DiagramItemDefinitions.cs:42`; LANG:173,206) but no recipe consumes it (`Recipes/Grid/DiagramRecipeDefinitions.cs:15-36` covers two canals only); diagrams are creative-only (DiagramItemDefinitions.cs:16-17).

## STRUCTURE
- Layout CORE:56-143: `Origin(-1,-1)`, 7 layers, 64 offsets (golden count 64), footprint x -1..2, z -1..1, y 0..6. Glyphs: `#` `game:refractorybricks-good-tier*` (`src/ExpandedLib/Definitions/VanillaCodes.cs:46`); `C` anchor at (0,0,0); `I` `iiex:furnace-irontap-e` at (-1,1,0); `S` `iiex:furnace-slagtap-w` at (1,1,0); `T` `iiex:furnace-tuyere-*` - **any orientation** (`.Any`, CORE:68), connector NORTH (CORE:69) at (0,2,-1); `H` `iiex:hopper-tall-w` at (1,4,0); `f` `exlib:structurefiller` at (1,5,0), placed by the hopper itself (HOP:46-60); `c` `*:@(air|coalpile|furnace-chargepile)` at (0,2..5,0) (`IiexCodes.cs:23`); `h` `*:@(air|coalpile|furnace-chargepile|hearthmetal-.*)` at (0,1,0) (`IiexCodes.cs:34-35`); `a` `game:air` at (0,6,0).
- Roles: Chargeable = `c` only (**4 cells**, CORE:79; pinned `test/IronIndustryExpanded.Tests/Blocks/Furnaces/ChargeableCellsTests.cs:191-197`); Pool = `h` (CORE:82); no GasOutlet. DOC:101 claims 5 chargeable cells - drift.
- Orientation: `side` variant stamped by `ExOrientable` on placement (`.../Furnaces/BlockFurnaceCoreBase.cs:47-62`); pinned parts rotate with the structure (`.../FurnaceRoleCellsTests.cs:64-69`). Iron tap pours west, slag east (TAP:62-65).
- Completion: monitor tick flips `StructureComplete` (`src/ExpandedLib/Blocks/Structures/BlockEntityMultiblockStructure.cs:106-113`); tell = core HUD `bf-info-incomplete` "Structure is incomplete!" (LANG:239, FC:2060-2061) and the outline gesture's chat report `structure-missing-header` (`assets/exlib/lang/en.json:2-5`; BlockEntityMultiblockStructure.cs:428-429). The "CF" south-face label distinguishes it (CORE:42-55).

## VERBS (all single clicks; no Step/Stop logic, no sneak/sprint gates)
1. **Ctrl+Shift+RMB** on core (behaviour, MBB:24-27, 89-101), tap (TAP:107-114), hopper top cell (HOP:119-126), pile (PILE:325-333), tuyere (`BlockTuyere.cs:28`): toggles outline while incomplete; help `blockhelp-mulblock-struc-show` (LANG:238).
2. **Hopper top filler (1,5,0)**: RMB with charge -> 1 unit, Ctrl+RMB -> whole stack (HOP:128-138; HOPBE:90-114); empty hand -> withdraw tank (HOP:139-149). Accepts `core.IsChargeItem` (HOPBE:90-93) = scrap role or fuel (BE:41-46): `game:metalbit-iron`, `game:metalbit-steel`, `iiex:metalbit-castiron`, `iiex:metalbit-pigiron`, `iiex:metalbit-cruciblesteel`, `iiex:pig`, `iiex:pigchunk`, `iiex:pigbit`, `game:coke`, `game:charcoal` (`assets/iiex/config/materialroles.json:4-13`). One material per tank; mismatch -> `iiex-hoppertall-wronggrade` (LANG:380). Help/HUD LANG:58-62. The help hint draws `iiex:burden` (HOP:237-240), which the cupola refuses.
3. **Charge pile**: empty-hand RMB takes the top band (PILE:337-353; LANG:497); a held item does nothing (PILE:338-342). The pile block cannot be hand-placed (no creative entry, NoDrops PILE:41-45; CanPlaceBlock PILE:277-300).
4. **Taps** (TAP:99-162): empty hand on plugged tap -> unplug; any `BlockBehaviorCanIgnite` block (torch, not firestarter, TAP:176-177) on an open tap -> blow-in (SHAFT:832-841; repeat -> `iiex-tapalreadylit` LANG:379); `game:clay-fire` x`TapPlugClayCost` (4, CFG:119) on open tap -> replug (short -> LANG:378). Help LANG:235-237.
5. **Tuyere**: pipe node only; needs medium Air at pressure >= clamp(2.0 + (0.20 - fuelFrac) x 7.5, 1.2, 6) atm (FC:146-157, 1182-1187; CFG:143-155).

## PROCESS
- States Idle/Firing/Melting (`FurnaceState.cs:4-13`), derived each tick (SHAFT:851-873): Idle if choked, no fuel band in the lowest `RacewayDepthUnits` = 3000 u (SHAFT:646, 879-899), or not `BlownIn`; Melting when not `ConversionBlocked` and the first raceway burden band >= melting point (SHAFT:909-921). No extinguish grace (FC:1220-1235 is `!DerivesState` only); going out clears BlownIn (SHAFT:847-850).
- Charge: 3000 u/block (`CupolaChargeMetalUnitsPerBlock`, CFG:785; BE:27-28), 4 cells = 12,000 u (SHAFT:92-93). Pig 375 u, chunk 25 u, bit 5 u (`Items/ItemPig.cs:21-25`). Rounds: fuel only onto non-fuel, lowest column first (FC:712-737). Hopper drips 8/s, holds 128 (CFG:757-760).
- Rate: carbon/s = 0.175 x tuyeres(1) x ChargeUnitScale (3000/32) x AirFactor (SHAFT:136-157, 725-728; CFG:303); melt = carbon x `BfBurdenPerCarbonUnit` 4 x MeltSpeedFactor (SHAFT:397-407). Yield 60/12 = **5.0 u cast iron** and 8/12 u slag per charge unit (BE:93-99; CFG:496-502). Melt point 1200 C (CFG:492); intake 12 L/s (CFG:505); blast threshold inherited 2.0 atm (SHAFT:79-80).
- Pool: `PoolCells(1) x HearthUnitsPerBand 640` for metal and slag (SHAFT:175-184; CFG:389), written live into `iiex:hearthmetal-castiron` at (0,1,0) (FC:1650-1677; `Scenarios/CupolaScenarioTests.cs:150-166`). Taps drain 50 u/tick x 0.6 (iron) / 0.8 (slag) into a `BlockEntityMoltenCanalStart` below the spout, tap open only (SHAFT:1090-1170; CFG:471-479).
- `iiex:burden` in a cupola burns but never converts (CupolaScenarioTests.cs:215-249; FC:1010).

## READOUTS
- Core (FC:2056-2120, 2159-2169): `bf-info-state` + `bf-state-*`, `bf-info-temp/heatok/heatstall/heatin/heatloss/nodraught/blasthot/blastcold/burdengrade/meltrate/airstarved`, idle `bf-info-exhaustfull/needsmix/ready` (LANG:239-276). `bf-info-ready` reads "Blast Furnace is ready!" on a cupola.
- Iron tap: `cupola-info-moltencastiron` "Molten Cast Iron: {0}/{1}" (LANG:270; BE:67-68; SHAFT:1220-1230); slag tap `bf-info-moltenslag`; both `tap-state`, `tap-err-nocanal` (LANG:279-282; `BlockEntityFurnaceTap.cs:148-171`). Hopper: `hoppertall-empty/holds` + `bf-info-shaftfull/course/wrongburden` (HOPBE:253-273; LANG:240-247, 262).
- Sounds: `game:sounds/torch-ignite` at tap and ShaftCentre (0,3,0) (SHAFT:837; FC:1232; BE:115), `environment/fire` loop every 5 s (FC:1287-1296), `effect/extinguish1` (FC:1531), `effect/moltenmetal` at pouring taps (SHAFT:1115-1122), stonecrush/build on plug (TAP:133,160) (`src/ExpandedLib/Helpers/ExSounds.cs:15-62`).
- Particles: only hopper-drip `ExParticles.FallingDust` (HOPBE:190). Renderers: core is a vanilla cube (BlockFurnaceCoreBase.cs:66); pile bands via `OnTesselation` (`BlockEntityChargePile.cs:323-376`) with pig/scrap drawn as coke (PILE:188-198); hearthmetal cube with two `BEBehaviorMoltenCell` (`Products/Blocks/BlockHearthMetal.cs:37-58`). No animator.

## KNOWN GAPS
- DOC drift vs code: plate x2 vs x1 (DOC:143); tuyere "orientation n"/"orientation-pinned" (DOC:85,92) vs `.Any` (CORE:68); 5 chargeable cells (DOC:101) vs 4; `CupolaMaxMoltenCastIron`/`CupolaMaxMoltenSlag` and `MaxMoltenProduct`/`MaxMoltenSlagPool` overrides (DOC:14-15, 165, 229-230) do not exist (grep empty) - ceiling is 640 via `HearthUnitsPerBand`; "zero methods" (DOC:157, 270) vs two private helpers (BE:48-54); "hand-place charge in the column" (DOC:199) has no code path (PILE:41-45, 338-342); "dead blower ... snuffs a cupola" (DOC:176-177) - derived branch only cools via AirFactor and flags `airStarved`; no snuff path found (FC:1214-1235, SHAFT:851-873) - UNVERIFIED whether starvation ends the campaign indirectly.
- BE:36 cites `CupolaChargeIdentityTests`, which does not exist (the cases live in `ChargeCodeGateTests.cs:109-127`).
- `IiexRecipeConfig.cs:89-91` comment says the cupola has no cost row; a row exists at :122.
- DOC Open items (DOC:320-333): ferroalloy/ladle unbuilt; no handbook page (confirmed: `assets/iiex/config/handbook/` has 00-12, none cupola); scenario suite in wrong project; `docs/internal/workbench/layouts.md` drift.
- Diagram exists, consumed by nothing. No tests cover the pile mesh or hearthmetal rendering (grep of `test/IronIndustryExpanded.Tests` for renderer/mesh returned nothing).
