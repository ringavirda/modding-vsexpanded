# Research snapshot - puddling-helve-reheat

**Written** 2026-09-04 by a read-only research agent, against commit `cf2e8c89` (the tree before the
line-ending renormalisation). **Covers** puddling furnace whole heat, helve shingling route and its work-item class defect, reheat furnace soak.
**Purpose** grounding for the roadmap's Phase 1 walkthrough and Phase 2 machining-line plan
(`docs/internal/plans/2026-09-04-*.md`).

Line numbers were right on that day and drift; cite by symbol when reusing. "UNVERIFIED" marks are the
agent's own and were not re-checked. Nothing here is a decision - decisions live on the design pages.

---

# Playtest walkthrough research: puddling furnace, helve shingling, reheat furnace

Path legend (all under the repo root): `F/` = `src/IronIndustryExpanded/BlockStructures/Furnaces/`, `BE/` = `F/BlockEntities/`, `Bk/` = `F/Blocks/`, `I/` = `src/IronIndustryExpanded/Items/`, `R/` = `src/IronIndustryExpanded/Recipes/`, `core` = `F/BlockEntityFurnaceCore.cs`, `cfg` = `src/IronIndustryExpanded/IiexConfig.cs`, `lang` = `assets/iiex/lang/en.json`, `van` = `.compat/Vintagestory/vssurvivalmod/`, `design` = `docs/design/`.

Caveat on the docs: `design/machines/puddling-furnace.md` "Construction" (:121-135) and "Operation" (:142-185) predate U6 and are contradicted by its own Open table (:423-428); `reheat-furnace.md` Gotchas (:397-405) likewise vs Open #1 (:417). Everything below is from code.

## Shared BUILD (no RCC stages, no diagram: `R/Grid/FurnaceRecipeDefinitions.cs:151-157`; `design/machines/firebox.md:279-281`)

Group `reverberatory`, `R/Grid/FurnaceRecipeDefinitions.cs:158-248`. Ingredient helpers: B = `game:refractorybrick-fired-*` any tier, captured as `{tier}` (`:348-354`); b = tier-3 brick `game:refractorybrick-fired-tier3` (`:341-343`); R = `game:rod-*`, P = `game:metalplate-*`, N = `game:metalnailsandstrips-*`, H = hammer, C = `game:clay-fire` (`src/ExpandedLib/Definitions/ExIngredients.cs:15-50`); V = `iiex:castplate-heavy` (`:336-338`).

| Output | Pattern | Counts | Lines |
|---|---|---|---|
| `iiex:furnace-puddlingcore-{tier}-n` | `BBB,RCR,BPB` | B4 R2 C8 P1 | :163-172 |
| `iiex:furnace-heatingcore-{tier}-n` | `BPB,RCR,BBB` | same | :174-183 |
| `iiex:furnace-puddlinghearth-n` | `_H_,VVV,N_N` | V1 each, N2 each | :186-194 |
| `iiex:furnace-heatinghearth-n` | `_H_,VVV,VVV` | 6 heavy plates | :196-203 |
| `iiex:furnace-puddlingchargedoor-s` | `_H_,PSP,BPB` | b2, P1, N2 | :205-214 |
| `iiex:furnace-chargedoor-s` | `_H_,PSP,BBB` | b2, P1, N2 | :217-226 |
| `iiex:furnace-puddlingchimneycap-n` | `_R_,BPB,_H_` | b2 R1 P1 | :229-238 |
| `iiex:furnace-firebox-{tier}-n` | `BBB,RRR,BBB` | B2 R1 (any tier) | :241-248 |
| `iiex:tool-rabble` | `_H_,_R_,P__` | R2 P1 | `R/Grid/PuddlingToolRecipeDefinitions.cs:23-31` |
| `iiex:tool-paddle` | `_H_,_R_,__P` | R2 P1 | `:32-40` |
| 3x `iiex:puddlingfettle` | `FFF` | any 3 `fettlestock`-tagged: `game:crushed-iron`, `iiex:tapcinder`, `iiex:millscale` | `R/Grid/FettleRecipeDefinitions.cs:21-30`; `I/FettleItemDefinitions.cs:25,34,40,67,87` |

Vanilla legend blocks: `#` = `game:refractorybricks-good-tier*` (`src/ExpandedLib/Definitions/VanillaCodes.cs:46`), `-`/`i` fire-brick slabs (`:101`), `K` = coke-oven door (`:179`). Goldens: `test/IronIndustryExpanded.Tests/goldens/iiex/recipes/grid/{reverberatory,puddlingtools,puddlingfettle}.json`.

## 1. Puddling furnace

**STRUCTURE.** 8x3x8, `Origin(-6,-1)`, layers at `Bk/BlockPuddlingFurnaceCore.cs:98-164`. Parts: hearth `H` facing north, door `D` south, cap `M` north, firebox `F` any tier/facing (`:65-97`). Hearth footprint `#0#` plus a `###` roof course (`Bk/BlockPuddlingHearth.cs:50-54`); door + one filler above (`Bk/BlockChargeDoor.cs:70-80`); cap + housing filler at local +Z (`Bk/BlockPuddlingChimneyCap.cs:49-60`). Chimney is NOT player-built: 4 `A` flue cells at (0,3..6,0) are in the drawing and `StackCourses = LocalCellsWithRole(CellRole.Flue).Count` (`BE/BlockEntityFireboxFurnace.cs:47-48`; `Bk/BlockPuddlingFurnaceCore.cs:154-156`). Cap is resolved as the cell above the highest flue (`BE/BlockEntityFireboxFurnace.cs:79-88`). Completion tell: ctrl+shift+RMB on core/hearth/door/cap/firebox toggles the projection and posts a shopping list in chat (`src/ExpandedLib/Blocks/Structures/BlockBehaviorMultiblockStructure.cs:78-87`, `BlockEntityMultiblockStructure.cs:688-692`); core info reads "Structure is incomplete!" until then (`core:2060-2061`, `lang:239`); unlinked parts say "Not part of a furnace." (`lang:464`). On completion `ScanForOutlets` resolves cap, door, hearth (`BE/BlockEntityFireboxFurnace.cs:76-98`).

**VERBS** (all click, server-side, `Bk/BlockPuddlingHearth.cs:90-154`; filler cells route to the same handler `:280-285`; no hold, Step returns false `:287-293`; row = clicked cell rotated into the hearth frame `:80-88`):

| Hand | Cell | Result |
|---|---|---|
| `iiex:puddlingfettle` | hearth | `TryFettle` (`BE/BlockEntityPuddlingHearth.cs:183-193`); refusal `iiex-hearth-cannotfettle` (`lang:489`) |
| `iiex:pig` (`I/ItemPig.cs:28`) | hearth | `TryChargePig` (`:199-212`), max 3/row, 9 total (`F/PuddlingHearthLayout.cs:14,17`); `iiex-hearth-needsfettle` / `-cannotcharge` (`lang:490-491`). A fettled or loaded centre blocks both flanks (`F/HearthRows.cs:58-59`; `BE/...Hearth.cs:32-34`): load flanks first, centre last |
| any / sneak | door | RMB main door, shift+RMB small working door (`Bk/BlockChargeDoor.cs:148-155`; help `lang:453-454`) |
| any | cap | throws damper (`Bk/BlockPuddlingChimneyCap.cs:99-100`; `lang:463`). Fresh cap is SHUT (`BE/BlockEntityPuddlingChimneyCap.cs:18`) |
| fuel / empty | firebox | charge whole pool / take a course back (`Bk/BlockFirebox.cs:159-216`); errors `lang:459-461,640` |
| `iiex:tool-rabble` | hearth | gates: small door open (`iiex-hearth-doorshut`), 3 s cooldown (`-toosoon`), then `TryRabble` (`-nobath`/`-bathfrozen`/`-workedout`) (`Bk/...Hearth.cs:206-232`; `lang:473-478`) |
| `iiex:tool-paddle` | hearth | same gates, `TryDrawBall` (`-noball`), gives `iiex:puddled-ironball` at `BathTemperature` (`:235-272`, `:266`) |
| empty | hearth | only when `IsWorkedOut` and no ball standing: `ClearBed` + 3 `iiex:tapcinder` (`:150-151,160-175`; `cfg:436`; `lang:479`). Otherwise silent |

Help lines `hearth-help-fettle/charge/rabble/draw/clean` (`lang:470-472,487-488`; `Bk/...Hearth.cs:316-347`).

**PROCESS.** Ignition is automatic, no torch: Idle + complete + bed full -> `Firing` at 900 C with the ignite sound (`core:1236-1249`, `:194`; `BE/BlockEntityFireboxFurnace.cs:176-184`). Full = 12 units = 6 layers x 2 (`cfg:519,527,532`). Fuel = any item with `combustibleProps.BurnTemperature >= 1000` (`F/BEBehaviorFirebox.cs:34-35`; `cfg:547`), minus any code containing "lignite" (`BE/BlockEntityFireboxFurnace.cs:198-205`); one fuel per bed (`F/BEBehaviorFirebox.cs:202-204`). Temperature chases at 4 C/s (`cfg:291,294`; `core:314-321`) to 1421.6 C with damper open and doors shut; damper shut 907.1, main door open 1105.0, against `PuddlingProcessTempC` 1400 (`test/.../HeatBalanceTests.cs:314-325`; `cfg:240,246,253,256,260,420`; `F/StackDraught.cs:33-52`). Damper open + main door shut is therefore mandatory; the small door does not vent (`BE/BlockEntityChargeDoor.cs:43`). Melting after 300 s above 1400 (`cfg:450`; `core:1326-1335`), i.e. ~7 min after lighting; then `MeltDown(0.08)` every 10 s (`cfg:427,453`; `BE/BlockEntityPuddlingFurnace.cs:95-97`; `BE/...Hearth.cs:77-92`) -> bath of PigCount x 375 after ~130 s. Balls = units/200 (`I/WroughtBallItemDefinitions.cs:23`; `BE/...Hearth.cs:110-114`): 9 pigs -> 16 balls, 175 u remainder (`test/.../PuddlingYieldTests.cs:64-110`). Cooldown 3 s, shared, not saved (`BE/...Hearth.cs:157-166`; `cfg:444`). Ball: 200 u, stack 16, meltingPoint 1500, cools by vanilla temperature (`I/WroughtBallItemDefinitions.cs:27-50`). Freeze: `ExtinguishResidue -> FreezeBath` (`BE/BlockEntityPuddlingFurnace.cs:80-83`; `BE/...Hearth.cs:60-65`), only when a bath exists. Fuel clock: 1200 s runs only in Firing (`cfg:447`; `core:1319-1324`), resets on Melting (`core:1507-1512`) and never runs in Melting; bed units are never consumed while lit (only `BurnOutCharge` on extinguish, retained fraction 0 -> bed empties, `BE/BlockEntityFireboxFurnace.cs:221-233`; `cfg:377`). So a Melting furnace burns indefinitely on 12 coke; it goes out only by raking below 6 units (30 s grace, `BE/BlockEntityFireboxFurnace.cs:280-281`; `core:1252-1280,276`) or by dropping under 1400 for 30 s (shut damper / open main door -> back to Firing, `core:1344-1353`) and then the 1200 s clock. Fresh cap shut -> never reaches 1400 -> out at 20 min with the bed gone.

**READOUTS.** Core: "State: Firing/Melting" (`lang:248-251`), ledger (`lang:252-256`), "Draught: {0}% ... from 4 courses" + damped/venting lines (`BE/BlockEntityFireboxFurnace.cs:303-318`; `lang:258-260`), bridge loss (`:320-326`; `lang:257`), "Melting progress: {0}%" (`core:2072-2091`; `lang:271`), "Extinguishing in {0}s" (`core:2103-2111`), "The charge is going down: {0}%" / "Balls: {0} on the bed, {1} still in the bath" / no-hearth line (`BE/BlockEntityPuddlingFurnace.cs:112-127`; `lang:467-468,480`). Hearth: "Charged: {0} / 9 pigs", "{0} row(s) still need fettling", centre-blocks line (`BE/...Hearth.cs:348-369`; `lang:465-466,486`). Door "Main door/Working door: open|shut" (`BE/BlockEntityChargeDoor.cs:135-154`; `lang:449-452`); cap "Damper: ..." (`BE/...ChimneyCap.cs:54-64`); firebox "Firebox: {fuel}, {n} / 12 units" (`F/BEBehaviorFirebox.cs:313-316`; `lang:455-456`). Renderers: hearth prunes to base + fettle cubes + pigs, or `Bath/*` in place of pigs with fettle kept (`F/PuddlingHearthLayout.cs:59-76`; `BE/...Hearth.cs:247-294`); no frozen art. Firebox draws `Coke/CokeL1..6` retextured per fuel (`BE/BlockEntityFirebox.cs:118-166`). Door clips `open-main/closed-main/open-small`; cap `idle/open` (`Bk/BlockChargeDoor.cs:40-53`; `BE/BlockEntityChargeDoor.cs:79-101`; `BE/...ChimneyCap.cs:29-30`). Sounds: ignite, fire loop every 5 s, extinguish (`core:1248,1290-1298,1531`). No particles anywhere in these files.

**KNOWN GAPS.**
- Working-stroke animation never plays: `PlayStroke` is only called server-side (`Bk/...Hearth.cs:109,230,270`) and `ToggleAnimator.Pose` is a client-only no-op (`src/ExpandedLib/Renderers/ToggleAnimator.cs:73-77`); nothing syncs it. The `rabbling` clip is also `onAnimationEnd: Repeat` (shape) so it would loop if it did.
- Not-lit line is the blast furnace's "Blast Furnace is ready! Ignite the charge inside." (`BE/BlockEntityPuddlingFurnace.cs:105-106`; `lang:276`) though ignition is automatic.
- During the bath the hearth HUD reads "Charged: 0 / 9 pigs" and "centre row blocks" (`BE/...Hearth.cs:352-369`); `puddling-bath` (`lang:469`) is unused.
- Cinder is a flat 3 (`cfg:436`); `CinderUnits` (`BE/...Hearth.cs:172-173`) is unread. Fuel never burns down (above).
- Breaking a charged hearth destroys fettle/pigs (`design/machines/reheat-furnace.md:337-339`).
- Stale class docs claim the cycle is unbuilt (`BE/BlockEntityPuddlingFurnace.cs:16-17`; `BE/...Hearth.cs:18-19`). No handbook page (`lang` handbook titles :425-433,606-616,645-647). Renderers untested (only `ElementsFor`, `test/.../FurnacePartsTests.cs:124-128`).

## 2. Helve shingling

Route: hot ball -> any anvil (`I/ItemPuddledBall.cs:24`), placement needs >= 750 C (`CanWork`, `:37-45`; meltingPoint 1500). `TryPlaceOn` lays a 16 x 5 metal layer at z 5..9, refuses a foreign work item, third ball refused (`:47-75,100-113`; `I/Shingling.cs:20,30,33,39-50`). Vanilla `TryPut` keeps the standing work item so the second ball piles (`van/BlockEntity/BEAnvil.cs:267-275`); recipe chosen without a dialog because the ball matches exactly one (`:277-291`; `test/.../PuddlingYieldTests.cs:203-220`). Recipe `iiexshingle`: 2 layers of 5 rows x 16 `#`, output `iiex:stock-shingledbar` (`R/Smithing/ShinglingRecipeDefinitions.cs:22-55`; golden `.../smithing/shingle.json`); 2 balls = 160 voxels = bar, 400 u (`src/.../Forming/StockItemDefinitions.cs:23`). Transposition fix: vanilla `GenVoxels` centres and transposes the pattern, so the pile starts at `OriginZ` (`I/ItemPuddledBall.cs:95-99`); guard runs vanilla's own layout over the shipped pattern (`test/.../BlisterBreakingTests.cs:151-162`). Helve vs anvil: helve hits call `OnHelveHammerHit`; `FullyWorkable` fills/clears one voxel per hit toward the recipe (`van/.../BEAnvil.cs:507-538`); a hand hammer works normally. Output takes the work item's temperature (`:580-584`), i.e. the first ball's. Player sees: a voxel pile on the anvil, the helve beating, "Shingled Bar" (`lang:88`) hot in hand. Slab half not built (`design/processes/shingling.md:265-268`).

**KNOWN GAP (code-derived, UNVERIFIED in game).** `shingleworkitem-iron` is `.Class<ItemWorkItem>()` - vanilla's type (`I/WroughtBallItemDefinitions.cs:55`). `KeyFor` names it `iiex.ItemWorkItem` (`src/ExpandedLib/Registries/Entities/EntityRegistry.cs:120-130`; golden `goldens/iiex/itemtypes/shingleworkitem-iron.json:3`), which nothing registers (only `[ItemRegister]` types, `:32-48`; no `RegisterItemClass` elsewhere in `src/`). Vanilla logs "Item with code ... has defined an item class ..., but no such class registered. Will ignore." and makes a plain `Item` (`.compat/Vintagestory/vsessentialsmod/Loading/ItemType.cs:130-134`); the anvil then dereferences a missing `IAnvilWorkable` on the first hit (`van/.../BEAnvil.cs:152-154,453`). Even as vanilla `ItemWorkItem`, the helve asks the work item (`:461-462`) and vanilla answers `NotWorkable` for a recipe not named plate/blistersteel (`van/Item/ItemWorkItem.cs:285-289`); the pig and blister chains use subclasses (`I/ItemPigWorkItem.cs:39-42`, `I/ItemBlisterWorkItem.cs:46-49`). Check `server-main.log` for that error first.

## 3. Reheat (heating) furnace

**BUILD.** Core, hearth, plain charge door, 2 fireboxes from the table above; no cap.

**STRUCTURE.** 8x4x5, `Origin(-6,-2)`, layers `Bk/BlockHeatingFurnaceCore.cs:59,85-130`. Two `F` cells (`BE/BlockEntityHeatingFurnace.cs:25-27`), 2 flue cells -> "2 courses"; no cap -> damper reads open (`BE/BlockEntityFireboxFurnace.cs:68`). Hearth 3 wide x 2 deep + roof course, principal on the door row (`Bk/BlockHeatingHearth.cs:48-66`); `ShaftCentre (-2,0,0)` (`BE/BlockEntityHeatingFurnace.cs:35`). Same completion tells as above.

**VERBS** (`Bk/BlockHeatingHearth.cs:100-142`): RMB with stock -> `TryLoad`; accepted paths `stock-shingledbar`, `stock-shingledslab`, `caststock-billet/bloom/slab` only (`F/HeatingHearthLayout.cs:51-69`); refusals `iiex-hearth-rowfull/-notstock/-centreblocks` (`lang:492-494`). Empty hand -> `TryTake`; a loaded centre blocks flanks both ways (`BE/BlockEntityHeatingHearth.cs:67-95`). Both depth cells of a column are one row (`Bk/BlockHeatingHearth.cs:90-98`) - that is the lengthwise seating; crosswise is not built (`design/machines/reheat-furnace.md:193-215,420`). Door: main only. Firebox: one deposit spreads over both cells, full = 24 (`BE/BlockEntityFirebox.cs:57-83`). Help `lang:484-485`. Breaking a loaded hearth drops the pieces (`BE/...HeatingHearth.cs:107-115`).

**PROCESS.** Auto-ignites at 24 units; `MeltingPoint = RollingTempC` 900 (`BE/BlockEntityHeatingFurnace.cs:74`; `cfg:868`) which ignition already meets, so Melting at +300 s. Chamber: natural(2) = 0.6515 -> T_in 1682.9 -> ~1363 C (derived from `core:1445-1465` with `HeatBalanceTests.cs:314-318` constants; UNVERIFIED as a shipped figure); main door open ~1070, still above 900. `SmeltCycle -> SoakTick` every 10 s (`BE/BlockEntityHeatingFurnace.cs:64-66`; `BE/...HeatingHearth.cs:133-174`): target = min(chamber, stock meltingPoint 1500); rate = `ReheatRateK` 0.02 x (2/t + 2/w) (`cfg:926`; `src/.../Forming/RollingPass.cs:196-197`), asymptotic (`:173-183`). Bar 3x3 -> ~40 s cold to 900; slab 8x3 -> ~58 s (`cfg:356-359`; `test/.../ReheatSoakTests.cs:274`). Cooling: vanilla in hand; on the mill `RollingCoolRate` 0.005 x A/V (`src/.../BlockEntityRollingMill.cs:554`; `cfg:909`). Unlit soaks nothing (`ReheatSoakTests.cs:238`). Fuel: same never-consumed clock as above.

**READOUTS.** Ready line "Lit. Lay stock on the hearth..." (`lang:417`, shown while still Idle-and-full, `core:2185-2191`); "Soaking: {0} piece(s), {1} at rolling heat" / no-hearth (`BE/BlockEntityHeatingFurnace.cs:87-95`; `lang:481-482`); draught "from 2 courses". Hearth "Loaded: {0} / 3 rows" + centre line (`BE/...HeatingHearth.cs:298-313`; `lang:483`). Renderer: `Items1` = left, `Items3` = centre, `Items2` = right (`F/HearthRows.cs:31-37`; `F/HeatingHearthLayout.cs:37-45`); art is per form only - a part-rolled bar draws as fresh; groups exist for the 5 recognised forms and nothing else (`assets/iiex/shapes/furnace/heatinghearth.json`).

**KNOWN GAPS.** Open #12: rod/beam/heavyplate refused, no forge route (`reheat-furnace.md:183-191,428`); crosswise (#4), composed renderer (#5), no damper (#9), roasting (#8) (`:420-428`); bed art 16 long vs 18 bar (`:128-132`); enum still `ShingledBloom` (`F/HeatingHearthLayout.cs:14`); handbook only a paragraph in the forming-shop article (`lang:617`; `docs/iiex/handbook/10-formingshop.html:40`); renderer untested.

## Tester checklist

1. Craft all eight `reverberatory` recipes plus rabble, paddle, fettle in survival; projection + chat shopping list appears on core, hearth, door, cap, firebox.
2. Puddling: fettle refuses pig on a bare row; centre blocks flanks; 9 pigs draw in the right cells at all four facings.
3. Fill firebox to 12 (one click), it self-ignites with a whoosh; lignite refused with the "low-rank" message.
4. Damper OPEN, main door SHUT: core reads ~1421 C, "Melting progress" climbs, bath art replaces pigs after ~9 min; with the damper shut it stalls at ~907 and dies at 20 min with the bed emptied.
5. Rabble/paddle through the small door: 16 strokes, "toosoon" at <3 s, ball leaves ~1400 C; no stroke animation is expected (gap).
6. Clean-out only when worked out and no balls: 3 tap cinder; three cinder craft to 3 fettle.
7. Rake fuel below 6 units mid-bath: 30 s countdown, extinguish, "bath has set solid"; balls still drawable, clean-out still allowed.
8. Anvil: check `server-main.log` for "no such class registered" on `iiex:shingleworkitem-iron`; then pile 2 balls, run the helve, expect a Shingled Bar - or a crash/inert helve (gap).
9. Reheat: bar loads in a chosen row (both depth cells), rod/beam/heavyplate refused; lit furnace brings a bar to 900 in ~40 s, slab ~58 s; "Soaking: n, m at rolling heat" counts up; taking a hot bar keeps its temperature.
10. Both furnaces: the firebox stays "12 / 12" (or 24) for the whole burn and never goes out on its own once Melting - confirm whether that is the intended feel.
