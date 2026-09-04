# Research snapshot - coke-oven-crucible

**Written** 2026-09-04 by a read-only research agent, against commit `cf2e8c89` (the tree before the
line-ending renormalisation). **Covers** beehive coke oven and the crucible furnace / crucible steel end to end.
**Purpose** grounding for the roadmap's Phase 1 walkthrough and Phase 2 machining-line plan
(`docs/internal/plans/2026-09-04-*.md`).

Line numbers were right on that day and drift; cite by symbol when reusing. "UNVERIFIED" marks are the
agent's own and were not re-checked. Nothing here is a decision - decisions live on the design pages.

---

## Playtest research: beehive coke oven (U9.1-9.4) and crucible steel (U9.5-9.11)

Both machines are marked "not walked in game" by their own design pages (`docs/design/machines/coke-oven.md:3-7`, `crucible-furnace.md:2`). Everything below is what the code does today; where a claim rests on tests or arithmetic rather than a live path, it says so.

### Shared mechanics the tester must know first

- **Nothing is lit by hand.** A firebox-branch furnace auto-ignites on the tick its structure is complete and *every* firebox bed is full (`BlockEntityFurnaceCore.cs:1237-1250`, `BlockEntityFireboxFurnace.cs:176-184`). `TryLightFromTap` is false for these machines (`BlockEntityFurnaceCore.cs:1634-1641`). Tell: the `game:sounds/torch-ignite` sound (`ExSounds.cs:28`), then a throttled fire ambience every 5 s (`core:1289-1297`). No particles are spawned by the core (grep, none).
- **Charging a firebox cell fills every firebox cell of the owning furnace** from one click, whole held stack, this cell first (`BlockEntityFirebox.cs:57-83`). Empty hand takes one course = 2 units from *that cell only* (`BlockFirebox.cs:209-216`, `BEBehaviorFirebox.cs:228-240`, `IiexConfig.cs:527`). Cell capacity 6 x 2 = 12 (`IiexConfig.cs:519,527,532`).
- **Refusals on a firebox cell** (`BlockFirebox.cs:184-203`): not fuel (burn temp < `BoilerFuelMinTemp` 1000, `IiexConfig.cs:547`) -> `iiex-firebox-notfuel` (`en.json:459`); fuel the machine won't take -> the machine's own message (below); bed full -> `-full` (`:461`); different fuel already in the bed -> `-wrongfuel` (`:460`).
- **Fire clock.** "Firing" lasts at most `FireboxMaxFuelBurnTime` 1200 s (`IiexConfig.cs:447`; `core:1314-1318`); it crosses to "Melting" after `FireboxMeltStartDelay` 300 s at/above the machine's process temperature (`IiexConfig.cs:450`; `core:1320-1331`), which resets the clock (`TransitionToMelting`). Nothing consumes a firebox bed while lit (the only `Consume` caller is burn-out, `BlockEntityFireboxFurnace.cs:221-233`), so a Melting furnace stays lit indefinitely. Ignition temperature is 900 C (`core:194`); the hearth chases its balance at 4 C/s (`core:312-325`, `IiexConfig.cs:291-294`).
- **Away catch-up** replays at most `MaxAwayCatchupSteps` 600 one-second ticks per reload (`BlockEntityFurnaceCore.cs:40`, `BEBehaviorProductionMachine.cs:107-142`, `GameTime.cs:15-45`; game-hours x 3600). So an unloaded oven gains <= 600 s of bake, however long you were gone.
- **Build outline** is ctrl+shift+RMB on the core, a door, lid, firebox, hearth or damper (`BlockFirebox.cs:170-179`, `BlockChargeDoor.cs:141-146`, `BlockCrucibleHearth.cs:109-116`, `BlockPuddlingChimneyCap.cs:91-98`). Incomplete HUD: "Structure is incomplete!" (`en.json:239`); completion posts `iiex:bf-error-complete` (`core:1046-1047`; text UNVERIFIED).

---

## 1. Beehive coke oven

**BUILD** (`Recipes/Grid/FurnaceRecipeDefinitions.cs:260-287`, golden `test/IronIndustryExpanded.Tests/goldens/iiex/recipes/grid/cokeoven.json`)
- Core `BBB,BHB,BBB`: 8 x `game:burnedbrick-fire`, hammer (tool) -> `iiex:furnace-cokeovencore-n`. Tier-less (`BlockCokeOvenCore.cs:17-22,36`).
- Charge lid `_P_,BSB,_H_`: 2 cells x 2 `game:refractorybrick-fired-tier3`, 1 `metalplate-(iron|steel)`, 2 `metalnailsandstrips-(iron|steel)`, hammer -> `iiex:furnace-chargelid-s`.
- Also needed: 2 charge doors `_H_,PSP,BBB` (6 tier-3 refractory, 1 plate, 2 nails, `:217-226`); 12 fireboxes `BBB,RRR,BBB` (12 refractory any tier + 3 rods each, `:240-247`).
- No RCC stages, no diagram. Cost rows `IiexRecipeConfig.cs:82-83`.

**STRUCTURE** (`BlockCokeOvenCore.cs:50-130`; counts `coke-oven.md:108-121`)
- 9 wide x 4 deep x 4 tall, `Origin(-4,-2)`: 93 `game:claybricks-good-fire` (`VanillaCodes.cs:50`), 12 `iiex:furnace-firebox-*` in two 3x2 chambers at y=1, 8 `game:brickslabs-fire-up-free` crown springing, 4 `game:brickslabs-fire-south-free` door cheeks (`VanillaCodes.cs:101-102`), 2 `iiex:furnace-chargedoor-s` at (+/-2,1,1) plus their auto-placed upper fillers, 2 `iiex:furnace-chargelid-s` at (+/-2,3,-1) over 2-cell crown voids (+/-2,2,-1..0). Doors and the `co` label face south relative to the core's facing; the layout rotates with it (completes at every facing, `CokeOvenLayoutTests.cs:122`).
- No `CellRole.Flue` -> draught fixed at 50 % (`BlockCokeOvenCore.cs:92-93`, `StackDraught.cs:216-235`).

**VERBS**
- Firebox cell (reach it through the open door, or from the crown through the open lid): RMB with `game:ore-bituminouscoal` -> fills all 12 cells (144 coal = 21/4 stacks). Lignite/anthracite/charcoal/coke pass the bed's test but are refused by `AcceptsFireboxFuel` (`BlockEntityCokeOven.cs:263-264`, `CokeOvenCycleTests.cs:235-247`) -> `iiex-firebox-notcoking`: "A coke oven bakes raw coal into coke - a fuel that has already been through a fire will not coke." (`en.json:641`; wrong reason for lignite/anthracite). Empty hand -> 2 units from that cell. Help: `firebox-help-charge/take` (`en.json:457-458`).
- Door and lid: RMB click toggles (`BlockChargeDoor.cs:133-163`; sneak falls through to main on these). No hold, no sound; clips `open`/`closed` (`:37,60`). HUD "Main door: open/shut" (`BlockEntityChargeDoor.cs:135-142`, `en.json:449-452`) - the lid says "Main door" too.
- Core: outline only.

**PROCESS**
1. All 12 beds full -> auto-light; temp set to 900 >= `CokeOvenLightC` 400 (`IiexConfig.cs:588`, `BlockEntityCokeOven.cs:291`) -> "Melting" after 300 s. Balance settles ~1190 C (950 + 900x1.25x0.5 - (120+100+100)); venting only lowers it (`BlockEntityFireboxFurnace.cs:284,291`).
2. Bake (`BlockEntityCokeOven.cs:177-204`): each chamber (flood-filled from firebox cells, `:54-80`) accrues `dt` only while `Sealed` = its nearest lid *and* door both `IsVenting == false` (`:113-150`; `BlockEntityChargeDoor.cs:43`); a missing closure reads open. Open -> clock holds; emptied/non-coal -> clock resets to 0. Cycle `CokeOvenCycleSec` 3600 s (`IiexConfig.cs:602`) integrated in 5-s steps (10 s / melt-speed 2.0, `core:1371-1373`, `IiexConfig.cs:453,368`) -> **60 real minutes with the chunk loaded**.
3. Yield `(int)(12 x 0.9)` = 10 coke per cell, 120 per oven (`:227-230`, `IiexConfig.cs:615`); pinned against vanilla's 0.75/16-pile (`CokeOvenCycleTests.cs:117,139`; `BECoalPile.cs:258-260`).
4. Unload: 5 empty-hand clicks per cell x 12 cells. Drawing below 72 total units trips the disruption floor and the fire goes out 30 s later (`BlockEntityFireboxFurnace.cs:280-281`, `core:1264-1281`); burn-out is a no-op here so nothing is lost (`BlockEntityCokeOven.cs:278`). Relight needs 144 fresh coal - but a bed still holding coke refuses bituminous with `-wrongfuel` until emptied.

**READOUTS**
- Core (`core:2056-2160`): "State: Firing/Melting", "Internal Temp", "Above the 400 C melt line", heat in/loss, "No blast - natural draught only", "Draught: 50% ... from 0 courses", venting line if the *east* door is open (`DoorCell (2,1,1)`, `BlockEntityCokeOven.cs:43`), "... of which 100 C crossing the bridge" (transfer loss not overridden), then `Chamber n: nothing to bake / standing open - shut the lid and the door... / baking, N% through` (`:326-355`, `en.json:642-644`), then a "Burden: Grade ..." line and "Melt rate: 200%". Idle and under 144: "Blast Furnace needs more charge to fire!" (`en.json:274`). The "Lit. Seal both crown lids..." line (`en.json:620`) prints only when idle *and* full - effectively never, since full = auto-light.
- Each chamber block: "Firebox: {fuel}, n / 12 units" (`en.json:455`).

**KNOWN GAPS**
- Not walked; cycle "untuned" (`IiexConfig.cs:596-601`). Design Open #7 (self-ignition) is answered by code, not by ruling (`coke-oven.md:369`).
- Design operation table (`coke-oven.md:221-232`) still says empty hand on the *door* draws coke; code draws per cell.
- Whether all six cells of a chamber are clickable (back row under slabs / through the crown void) is untested - UNVERIFIED.
- Lang: "Main door" on a lid; "Blast Furnace needs more charge"; refusal text for lignite/anthracite; "crossing the bridge" on a retort.
- Charging spreads across both chambers; the "one chamber at a time" bank rhythm needs 144 coal anyway to light.

---

## 2. Crucible furnace and crucible steel

**BUILD** (`FurnaceRecipeDefinitions.cs:299-327`, golden `goldens/iiex/recipes/grid/cruciblefurnace.json`)
- Core `BBB,BCB,BRB`: 6 x 4 `game:refractorybrick-fired-*` (tier captured), 8 `game:clay-fire`, 2 x 2 `rod-(iron|steel)` -> `iiex:furnace-cruciblecore-{tier}-n`.
- Hearth `CCC,RRR,BBB`: 6 clay-fire, 3 rods, 6 refractory -> `iiex:furnace-cruciblehearth-{tier}-n`.
- Damper = chimney cap `_R_,BPB,_H_`: 4 tier-3 refractory, 1 rod, 1 plate, hammer (`:229-238`); 1 charge door; 1 vanilla `game:cokeovendoor` (smithed).
- Pot: clayformed from **fire clay only** (`Recipes/Clayforming/CrucibleRecipeDefinitions.cs:20-58`) -> `iiex:steelcrucible-raw`; pit-kiln fires it (`BlockSteelCrucible.cs:113-125`: meltingPoint 600, 45 s, `smeltingType fire`) -> `-burned`. Three variants `raw|burned|smelted` (`:47`), stack size 1 (`:148`).
- Pour target: `iiex:casting-mold-ingot`, `requiredUnits` 100 (`Casting/Blocks/BlockCastMold.cs:50-62`), itself sand-cast from the `castingotmold` pattern at 152 u (`PatternItemDefinitions.cs:95-101`).
- No RCC stages. Cost rows `IiexRecipeConfig.cs:84-88`.

**STRUCTURE** (`BlockCrucibleFurnaceCore.cs:52-138`)
- A 4 x 4 x 5 column, `Origin(-3,-2)`, layers -1..3: 19 `game:refractorybricks-good-tier*` (`VanillaCodes.cs:46`); ash pit air at (-2,-1,0) closed south by `game:cokeovendoor-*-north` (`VanillaCodes.cs:172-179`); hearth H at (-2,0,0) west of the core with the charge door south of it (-2,0,1) between two `brickslabs-fire-up-free`; damper `iiex:furnace-puddlingchimneycap-s` at (-2,1,0), its housing filler at (-2,1,-1) on the **north** face; flue air at (-2,2,0),(-2,3,0); one base ring of `AnyBricks` (`VanillaCodes.cs:74-75`) at y=3.
- Chimney: player-built rings `. b . / b a b / . b .` walked up from the highest drawn flue cell, stopping at the first non-ring (`BlockEntityCrucibleFurnace.cs:118-146`, cap 32). `StackCourses` = 2 drawn + built (`:97`; `BlockEntityFireboxFurnace.cs:47-48`); rated 9 (`IiexConfig.cs:669`). The chimney is outside `StructureComplete` - the furnace completes with none.
- Temperatures from `StackDraught.cs:216-235` (base 0.5, gain 0.11, friction 0.00102, damper-shut x0.35, door-open x0.6; `IiexConfig.cs:240-262`) with T_in = 950 + 900x1.25xdraught (`IiexConfig.cs:213,216,228`) and losses 120 + 50 + 0 (`:273,683`; `BlockEntityCrucibleFurnace.cs:196,202`): **6 total courses (4 built) ~ 1604 C, 9 (7 built) ~ 1621, damper shut at 9 ~ 1074, door open at 9 ~ 1284** - all below/above the 1600 line as pinned by `CrucibleFurnaceTests.cs:124-185`.

**VERBS** (hearth: `BlockCrucibleHearth.cs:96-175`, through the open charge door)
- RMB `steelcrucible-burned` -> seats in the first free hole (NW, NE, SE, SW order, `CrucibleHearthLayout.cs:271-288`); fifth -> "Every hole already has a pot in it." (`en.json:638`). Raw or full pots fall through to the fuel test -> misleading `-notfuel`.
- RMB `iiex:blisterchunk` (25 u) / `game:metalbit-blistersteel` (5 u) -> whole items into the first pot that can take them, **all-or-nothing per item**, 110 u max (`BlockEntityCrucibleHearth.cs:61-76`, `CrucibleHole.cs:158-168`, `IiexConfig.cs:693`) = 4 chunks + 2 bits; none accepted -> "There is no pot to put that in." (`en.json:639`).
- RMB coke/charcoal/bituminous/anthracite -> firebox charge (12 units, one cell); lignite -> `iiex-firebox-refused` (`en.json:640`).
- Empty hand -> Pull: a molten pot first, else any seated pot (its loose charge is spilled at the hearth top, `:88-107`); only with all holes empty does it take a coke course. Help `crucible-help-seat/pull` (`en.json:636-637`).
- Damper: RMB click on the cap or its housing (`BlockPuddlingChimneyCap.cs:81-115`); starts shut (`BlockEntityPuddlingChimneyCap.cs:174`, `BlockEntityCrucibleFurnace.cs:81`). HUD "Damper: shut/open", help "Throw the damper" (`en.json:462-463`). Reachable only via the north housing - UNVERIFIED in game.
- Pour: hold RMB with the full pot on the mould - vanilla `BlockSmeltedContainer.OnHeldInteractStart/Step` (`.compat/Vintagestory/vssurvivalmod/Block/BlockSmeltedContainer.cs:102-248`), 2 u per step, `sounds/pourmetal`; empty-hand RMB lifts the hardened ingot (`BlockCastMold.cs:13`; too-hot error `en.json:414`).
- Crush: RMB a **cold** `game:ingot-blistersteel` on an anvil. Vanilla refuses below meltingpoint/2 (`ItemIngot.cs:55-70`; 1602 -> 801 C, `BlisterItemDefinitions.cs:192`), the postfix places `iiex:blisterworkitem-blistersteel` (40 voxels, `Patches/AnvilBlisterBreakingPatches.cs:218-255`, `BlisterBreaking.cs:155-161`) and forks the recipe list to `blistercrush` alone (`:257-279`). Helve hits shed one voxel each; a chunk spawns on the anvil every 10 hits (`:289-344`, `BlisterBreaking.cs:96-103`); the finishing hit yields 5 bits as recipe output (`Recipes/Smithing/BlisterRecipeDefinitions.cs:37-44`). 100 u exactly.

**PROCESS** (seat and charge *before* fuelling - the fire auto-lights and the 1200 s Firing budget starts)
1. Damper shut, 12 coke -> light; hearth ~1074 C, state Firing. Each seated pot soaks `CruciblePreheatSec` 240 s (`CrucibleHole.cs:174-177`, `IiexConfig.cs:707`; `BlockEntityCrucibleHearth.cs:123-147`, run from `OnProductionTick` `BlockEntityCrucibleFurnace.cs:165-171`). Pots seated while idle do not soak.
2. Open the damper: any pot with Preheat < 240 cracks on the next tick - `ceramicbreak` sound, pot gone, chunks/bits spawned one block **down** into the ash pit (`CrucibleHole.cs:188,217-221`, `BlockEntityCrucibleHearth.cs:130-145,185-210`). Temp climbs to ~1621 in ~2 min, Melting after 300 s more (~11 min from lighting; margin to the 1200 s cut-off ~ 9 min - leave the damper shut too long and the fire dies and the bed is **emptied**, `IiexConfig.cs:377`).
3. Melt: each charged, preheated pot advances 60 s per cycle (`BlockEntityCrucibleFurnace.cs:183-185`, `CrucibleHole.cs:194-206`) every 60 s / melt-speed (~1.08 at 1621) -> 600 s in ~91/2 min (`IiexConfig.cs:718,730`). Metal = 100 u (`:697`). Opening the charge door drops the hearth below 1600 within ~5 s; 30 s below and it falls back to Firing (`core:1340-1352`).
4. Pull -> `steelcrucible-smelted` carrying `output` = `iiex:ingot-cruciblesteel`, `units` 100, 1600 C, firings +1 (`CruciblePot.cs:53-80`). Pour before it sets (`en.json:672`). When emptied vanilla swaps to the burned pot; our stop re-stamps the count or, at `CruciblePotFirings` 3, deletes the pot with a `ceramicbreak` (`BlockSteelCrucible.cs:205-233`, `CrucibleFiring.cs:178-179`, `IiexConfig.cs:643`).
5. Tools: `cruciblesteel.json` preset `good`, durability 3300 (`assets/iiex/config/metals/cruciblesteel.json:15-18`; `goldens/iiex/itemtypes/cruciblesteel/pickaxe.json:21`; `materials.md:38-58`).

**READOUTS**
- Core (`BlockEntityCrucibleFurnace.cs:220-262`, `en.json:625-635`): the shared ledger, "Draught: N% ... from N courses", "... the damper is shut...", venting line, then `Stack: N course(s), draught N% - a taller chimney would pull harder (peak at 9) / at the peak / past the peak`, `Damper shut: the pots are coming up gently. / Damper open: full draught.`, `No hearth...`.
- Hearth (`BlockEntityCrucibleHearth.cs:337-362`, `en.json:626-629`): firebox line plus `Hole n: warming through, N%. Keep the damper shut until it is. / N u of blister steel - it wants a full charge... / melting, N%. / 100 u of crucible steel, ready to pull.`
- Render: pot, charge fill, cover (once soaking), slag (molten) per hole (`CrucibleHearthLayout.cs:333-351`, `BlockEntityCrucibleHearth.cs:246-252`). No animation on the crack.

**KNOWN GAPS**
- Pot firing count is invisible: no `GetHeldItemInfo` on `BlockSteelCrucible` (grep). R7 miss.
- Helve crush and vanilla's held pour are covered as arithmetic only (`CrucibleSteelScenarioTests.cs:30-34,187-189,236-262`); the recipe-picker behaviour with one recipe is UNVERIFIED.
- Damper reach, the "Lit. Seat charged crucibles..." line (idle-and-full only), the anneal phase, slag input, calibration "to be re-checked in play" (`crucible-furnace.md:476-483`, `IiexConfig.cs:679`).
- The burned pot still carries vanilla crucible firepit props (`BlockSteelCrucible.cs:88-101`) - a firepit smelt path exists, untested.
- Pouring into a vanilla clay mould is refused silently (`Patches/ToolMoldHeatGatePatch.cs:428-443`); molten canal/barrel help text may not name the pot (memory).

---

### Tester checklist
1. Coke oven: 144 bituminous charged from one door click fills both chambers; the oven lights itself (torch sound) with lids open.
2. Lignite/anthracite/charcoal/coke each refused with the "already through a fire" message; a stick says "does not burn hot enough".
3. Core HUD shows "Chamber n: standing open" until both lid and door are shut; "baking, N%" then climbs ~1.7 %/min.
4. Reload mid-bake: progress resumes and gains at most 600 s.
5. At 60 min every cell holds 10 coke; all 12 cells can be emptied by hand (5 clicks each); fire dies ~30 s after the total drops below 72.
6. Crucible: 4 burned pots seat, 5th refused; 4 chunks + 2 bits charge one pot, a 5th chunk is refused whole.
7. Damper found and thrown from the north housing; HUD flips between "coming up gently" and "full draught".
8. Open the damper early on one pot: ceramic crack, pot gone, chunks retrievable through the ash-pit door.
9. Stack line reads 2 courses with no chimney, 6 courses melts, 9 says "at the peak", 10+ says "past the peak".
10. Pull -> pour 100 u into the ingot mould -> `iiex:ingot-cruciblesteel`; the pot survives twice and shatters after the third pour; a crucible-steel pickaxe shows 3300 durability.
