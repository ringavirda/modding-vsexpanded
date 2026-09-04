# Research snapshot - blast-furnace-stations

**Written** 2026-09-04 by a read-only research agent, against commit `cf2e8c89` (the tree before the
line-ending renormalisation). **Covers** cold blast furnace after U4.4-U4.9, tall hopper, burdenmaker, twin-tub blower, molten canals and pig beds.
**Purpose** grounding for the roadmap's Phase 1 walkthrough and Phase 2 machining-line plan
(`docs/internal/plans/2026-09-04-*.md`).

Line numbers were right on that day and drift; cite by symbol when reusing. "UNVERIFIED" marks are the
agent's own and were not re-checked. Nothing here is a decision - decisions live on the design pages.

---

**Path key** (all under `/home/fallen/src/modding-vsexpanded/`): `F/` = `src/IronIndustryExpanded/BlockStructures/Furnaces/`, `M/` = `src/IronIndustryExpanded/BlockNetworkMolten/`, `C/` = `src/IronIndustryExpanded/BlockStructures/Casting/`, `R/` = `src/IronIndustryExpanded/Recipes/Grid/`, `Cfg` = `src/IronIndustryExpanded/IiexConfig.cs`, `Lang` = `assets/iiex/lang/en.json`, `X/` = `src/ExpandedLib/`, `D/` = `docs/design/machines/`.

## 1. Cold blast furnace

**BUILD** (no RCC, no diagram; walls hand-laid)
- Core: grid `BRP,BN_,BRP`, per cell B=4 `refractorybrick-fired-*` (tier captured), R=2 rod, N=4 nails, P=2 plate -> 12 brick/4 rod/4 nails/4 plate -> `iiex:furnace-blastcore-{tier}-n` (`R/FurnaceRecipeDefinitions.cs:31-37`).
- Iron tap `BPH,BFC,BBB`: 10 tier-3 refractory, 12 fire clay, 1 plate, hammer, chisel -> `furnace-irontap-s` (:44-52). Slag tap `B_H,BFC,BBB`: 10 brick, 12 clay, hammer, chisel (:55-62). Tuyere `BHB,BCB,BPB`: 12 tier-3 brick + 1 `iiex:pipe-plated-straight*` + hammer + chisel (:65-75).
- Plus 111 refractory bricks of one tier (`D/blast-furnace-cold.md:77-88`).

**STRUCTURE**
- 6x9x5 drawing, `Origin(-3,-2)`, core at (0,0,0) on the floor course, north frame rotated by the core's `side` variant (`F/Blocks/BlockBlastFurnaceCoreCold.cs:58-59,119-208`; `F/BlockEntityFurnaceCore.cs:1021-1026`).
- Taps at y=1: slag `S` at (-2,1,0) must face **east**, iron `I` at (2,1,0) **west** (into the furnace) (`Cold.cs:65,77`). Pour cell = one step opposite the facing, one **down** (`F/BlockEntities/BlockEntityFurnaceTap.cs:521-526`): iron start at (3,0,0), slag start at (-3,0,0), both at core level outside the wall; (-3,0,0) is deliberately unclaimed (:73-76).
- Tuyeres `Y` (0,2,-2) must open north, `T` (0,2,2) south (:84-87); enforced by `HasConnectorAt` (`X/Blocks/Structures/BlockEntityMultiblockStructure.cs:508-531`); a misfaced one is reported by chat "Turn these to face outward: ... must open to the ..." (:674-686; `assets/exlib/lang/en.json:4-5`).
- Hopper `H` at (-1,6,0) facing east with its own filler at (-1,7,0) (:91-92); air throat (0,6..8,0) is the chimney; no exhaust outlets (`F/BlockEntities/BlockEntityBlastFurnaceCold.cs:223-227`).
- Shaft: 36 `c` cells y=2..5; pool `h` = 3 cells (-1..1,1,0). Capacity 36 x 32 = **1152 u** (`F/BlockEntities/BlockEntityShaftFurnace.cs:92-93`; `Cfg:776`; `F/ChargeColumn.cs:64`). The design page's 39/1248 is stale.
- Completion: Ctrl+Shift+RMB on core/tap/tuyere/hopper/pile (`X/Blocks/Structures/BlockBehaviorMultiblockStructure.cs:26`) shows the outline and "Structure is not complete. {0} blocks..." / "Blast Furnace structure is complete!" (`MBS.cs:432-445`; `Lang:277-278`); a 3 s monitor tick also rechecks (`MBS.cs:51,88`; `Core.cs:110`).

**VERBS**
- Tap (`F/Blocks/BlockFurnaceTap.cs:99-162`): plugged + **empty hand** -> plug broken, destroyed, no refund (`Cfg:127`), StoneCrush. Open + a held `*-lit-*` block (torch; not the firestarter, :176-177) -> `TryLightFromTap` sets `BlownIn`, torch-ignite sound (`Shaft.cs:832-840`); twice -> `iiex-tapalreadylit` (`Lang:379`). Open + >=4 `game:clay-fire` -> re-plugged, 4 consumed (`Cfg:119`), Build sound; <4 -> `iiex-tapnotenoughclay` (`Lang:378`). Help lines gated on state (:212-254; `Lang:235-237`).
- Charge pile: empty-hand RMB lifts one band (2 items) off that column's top; a held item does nothing (`F/Blocks/BlockChargePile.cs:314-354`; `F/BlockEntities/BlockEntityChargePile.cs:115-141`; `Lang:497`). Breaking a pile splices its window out (`BlockChargePile.cs:252-272`).
- No door, no switch.

**PROCESS**
- State derived each tick (`Shaft.cs:852-872`): Idle unless every column has a fuel band in its lowest 32 units (:879-895, depth :646) **and** `BlownIn`; Melting when the first burden band at a raceway >= `BfIronMeltingPoint` 1482 (`Cfg:405`; :909-920). Blow-in on a shaft with no bottom fuel course buys nothing (test `A_lit_furnace_with_no_carbon_at_its_raceway_still_does_not_catch`).
- Carbon/s = 0.175 x 2 tuyeres x AirFactor (`Cfg:303`; :152-155, :706-707); AirFactor = 0.5 + 0.5 x blastSupplyFrac (`Core.cs:1445-1446`; `Cfg:240`). Air is drawn only while `Medium=="Air"` and pressure >= 2.0 + (0.20 - carbonFrac) x 7.5, clamped 1.2-6 (`Core.cs:1182-1188, 146-157`; `Cfg:143-155`); draw 14 L/s x clamp(f/0.2, 0.4, 1.8) per tuyere (:164-175; `Cfg:467,158,161`).
- Melt: burden units = carbon x 4 x MeltSpeedFactor (`Cfg:313`; :396-407); yield 8.5 x IronFrac per burden unit, slag 8.5/6 x IronFrac (`Cfg:350,355`; :117-122).
- Pool: on first melt the furnace **places** `iiex:hearthmetal-pigiron` into the 3 pool cells (`Core.cs:1650-1677`; `Shaft.cs:490-538`), each with an iron and a slag cell of 640 u (`Cfg:389`) -> 1920 u iron + 1920 u slag (:175-184). What happens to product over a full crucible: UNVERIFIED (design Gotcha 3 says lost).
- Tapping: open iron tap drains min(50, pool) -> stack ceil(x0.6) = 30 u/s into the start; slag 40 u/s (:1090-1160; `Cfg:471-479`). One shared 2 s sound throttle (:1116-1123).
- Cut blast: AirFactor halves, flame drops, Melting->Firing (scenario `Cutting_the_blast...`); "Starving for air" appears under 10% supply (`Cfg:266`; `Core.cs:1214-1221`) but the extinguish countdown never runs on this branch (:1252). Breach: supply forced 0, keeps burning, cannot relight while Idle (:1205-1206; `Shaft.cs:864-865`). Choke cannot happen (no outlets).
- Campaign ends when a raceway runs dry -> `Shutdown`: extinguish sound, 20 C, fuel bands burn out to lerp(0, 0.4, height), `BlownIn` cleared (`Core.cs:1229-1230, 1530-1547`; `Shaft.cs:847-850, 1010-1069`). Hearth cells latch solid below 1150 C (`assets/iiex/config/metals/pigiron.json:12`; `X/Blocks/Structures/BEBehaviorMoltenCell.cs:288-289`), chisel out below 30% (`X/ExlibConfig.cs:99`; `F/../Products/BlockEntities/BlockEntityHearthMetal.cs:161-184`). Away catch-up 600 s (`Core.cs:40`).

**READOUTS**
- Core (1 s cache): incomplete / "State: ..." / temp ledger (`bf-info-temp/heatok/heatstall/heatin/heatloss/blastcold/nodraught`) / burden grade / melt rate / air-starved / hung (`Core.cs:2056-2156`; `Shaft.cs:981-992`; `Lang:239-267`). Idle HUD says "needs more charge to fire!" until the shaft is **full to 1152** (`Shaft.cs:294`; `Core.cs:2188-2191`) - a lightable half-full furnace reads wrong; "ready" says "Ignite the charge inside" (`Lang:274,276`).
- Taps: "Tap is Open/Closed", "No canal start found below the tap!", "Molten Pig Iron: x / 1920" only while Melting or pooled (`TapBE.cs:446-470`; `Shaft.cs:1220-1244`; `Lang:268-269,279-282`).
- Tuyere: pipe readout only ("Throughput: ... Pressure: ...(g)", `X/Blocks/Networks/BlockEntityPipe.cs:306-361`; `assets/exlib/lang/en.json:71-80`).
- Piles glow and draw bands (`BlockChargePile.cs:122-135`). Sounds: torch-ignite (blow-in, Idle->lit), fire loop 5 s at shaft centre (`Core.cs:1290-1298`), extinguish, MoltenMetal at taps. No furnace particles; no animation - the plug is a pruned mesh (`TapBE.cs:371-416`).
- Commands: `/exmod config iiex <Key> <value>` applies next tick (`Core.cs:1112-1114`); `/exmod network hi` colours the blast main; `/exmod heal` (`assets/exlib/lang/en.json:53,63-68`).

**KNOWN GAPS**
- Hearthmetal is a flat metal cube: no fill height, no glow, no renderer (`F/../Products/Blocks/BlockHearthMetal.cs:57-60`); `HearthSlagSpoutBand` is read nowhere (only a comment, `Shaft.cs:181`).
- `ingameerror-iiex-hearthtoohot` has **no lang entry** (`BlockEntityHearthMetal.cs:169`; absent from `Lang`) -> raw key.
- Handbook `docs/iiex/handbook/02-coldblastfurnace.html` says "ignite the burden" (no plug/torch ritual) and that an unplumbed tap "solidifies at the tap"; code pours nothing without a start (`TapBE.cs:489-496`).
- Renderers untested in-game: tap prune pinned only by element names (`test/.../FurnaceTapPlugTests.cs:280-297`); `bf-info-partiallylit` unused.

## 2. Tall hopper

**BUILD**: `_H_,PSP,SPS` -> 3 plates, 3 fasteners (nails or rivets - two recipes), hammer -> `hopper-tall-n` (`R/FurnaceRecipeDefinitions.cs:77-108`; `src/IronIndustryExpanded/Recipes/RecipeIngredients.cs:27-29`). Both design pages say 4/4 - stale.
**STRUCTURE**: 1x2x1, filler above (`F/Blocks/BlockHopperTall.cs:49-60`); side variant; must face east at (-1,6,0) in the BF; finds its core by anchor scan 3/8/1 (`F/BlockEntities/BlockEntityHopperTall.cs:282-288`; `Core.cs:1827-1829`); over nothing it accepts nothing (:333-336).
**VERBS** (top filler only, `BlockHopperTall.cs:105-153`): RMB with `iiex:burden`/coke/charcoal -> 1 unit; **Ctrl**+RMB -> whole stack; empty hand -> whole tank; Ctrl+Shift -> outline. Mismatch -> `iiex-hoppertall-wronggrade` (`Lang:380`); full tank silent.
**PROCESS**: 128 u (`Cfg:757`), 8 u/s (`Cfg:760`) per 1000 ms (:318) onto `NextChargeColumn` - lowest column, fuel never onto fuel (`Core.cs:712-748`); holds when full/refused (:409-410). One load = one course.
**READOUTS**: "Empty..."/"Holds: {0}/128 - {2}" (`Lang:58-59`) + furnace slice: wrong-burden, "Top course: {0} {1}, {2} burden (of 16 bands)", "This charge melts/chills...", "Shaft: x / 1152" (`Core.cs:1858-1882, 1970-2043`; `Lang:240-247,262`). Dust below + StoneCrush 0.4 per drip (:435-436). No fill render.
**KNOWN GAPS**: no fill indicator (`D/tall-hopper.md:288-289`); help carousel shows burden only (`BlockHopperTall.cs:237-240`); `itemdesc-burden` still says coke is mixed in and names a "reinforced hopper" (`Lang:307`).

## 3. Burdenmaker

**BUILD**: grid `BBB,BBB` (3x2), 2 same-colour `game:burnedbrick-*` per cell = 12 -> `burdenmaker-{brick}-n` (`R/OreProcessingRecipeDefinitions.cs:377-400`). RCC (`src/IronIndustryExpanded/BlockStructures/OreProcessing/Blocks/BlockBurdenmaker.cs:72-107`): stage 1 free; 2: 12 brick; 3: 16 brick; 4: 6 `metalplate-iron`; 5: 3 plate + 2 `ingot-iron`. One RMB draws a whole stage from anywhere in the hotbar; shortfall -> `ingameerror-missingstack` (`X/Blocks/Construction/ExConstruction.cs:161-214`). No diagram.
**STRUCTURE**: 3x2x2, `Origin(-1,-1)`, hoppers over the front row only (:50-68); angle = side (:147-148). "Complete" = 5 stages (`BlockEntityBurdenmaker.cs:52`); no message - stage count shows only in entity debug mode (`X/Blocks/Construction/ExRightClickConstructable.cs:131-134`).
**VERBS** (:243-296, classifier :199-221): ore hopper = the two wide front cells, flux = narrow front cell, gate = principal, bunker = other floor cells. RMB 1 unit, **Ctrl** stack, empty hand takes a stack back; bunker is take-only; gate toggles. Errors `nothingloaded`/`emptybunker` (`Lang:375-376`). Ore = `IronOreCompat.IsCrushedIronOre`, flux = `Roles.Flux` (`BlockEntityBurdenmaker.cs:125-132`).
**PROCESS**: caps 512/205/1152 (`Cfg:741-747`). Opening the gate instantly converts both hoppers to one `iiex:burden` stack stamped ore/flux fractions (:256-307); basin must be empty first. Grades: <3% under, 3-8% standard, >8% over (`Cfg:957-971`). Lid poses `open`/`closed` (:100-114).
**READOUTS**: "Ore: a / 512 Flux: b / 205", "Would make burden at N% flux - Grade...", "Burden in the basin: n" (:338-373; `Lang:283-286,309-313`). No sound or particles on load/gate (none in code).
**KNOWN GAPS**: no contents rendering (`D/burdenmaker.md:524-525`); "roasted ore" hint is forward-looking (Open 1); lid clip endings set only by the export script, untestable (:436-443).

## 4. Twin-tub blower

**BUILD**: `LPL,PNP,_H_` -> 4 `leather-normal-plain` (2 per L cell), 3 planks, 1 nails, hammer -> `furnace-twintubblower-n` (`R/FurnaceRecipeDefinitions.cs:112-126`). No RCC.
**STRUCTURE**: 1x2x3 `M##/0##`, principal bottom-front, MP port cell directly above it, port on the rotated west face (+ opposite) (`F/Blocks/BlockTwinTubMPBlower.cs:36-37,72-87`); housing extends +z. Refused unless the volume is clear ("notenoughspace", :115-131). The blast main joins **only the principal's `orientation` face** (`-n` -> north, the side away from the housing) (`X/Blocks/Networks/BlockNetworkNode.cs:774-777`); the source comment :78-80 is wrong. Plated-tier burst 2.5 atm (`Cfg:175`).
**VERBS**: none. Ctrl+Shift does nothing (:54-58).
**PROCESS**: every 1000 ms (`F/BlockEntities/BlockEntityTwinTubMPBlower.cs:199-200`) reads port speed (:281-293); fraction = clamp((s-0.5)/1.0) (:270-278; `Cfg:816,819`); 45 x f L/s "Air" at climate ambient, ceiling 2.2 atm (:238-257; `Cfg:807,813`). **The curve is linear, not saturating** - no `MpBlower*`/half-speed key exists in `src/` (grep); the 2026-08-09 ruling is unbuilt. A vanilla wheel at speed ~1 -> 22.5 L/s vs 42 L/s a rich two-tuyere charge draws (`D/twin-tub-blower.md:237-263`) - gear up.
**READOUTS**: pipe lines + "Bellows idle..."/"Blowing {0} ({1}% of rated output)" (:308-324; `Lang:8-9`) - rated, never accepted. No sound, particles, or animation; `cycle`/`idle` clips exist (`assets/iiex/shapes/furnace/twintubmpblower.json:3434`) but no `Animatable` (:44-89).
**KNOWN GAPS**: static art; HUD lies under a leak/ceiling (design gotchas 5-6); no handbook page; tick order unenforced.

## 5. Molten canal + pig bed

**BUILD**: start `HCK,FFF`, straight `H_K,FCF`, bend `HFK,FC_`, T `HFK,FCF`, X `HFK,FCF,_F_`, pedestal `HFK,CFC`; C = 1 cobble (rock captured) or running/fire brick; F = 2 fire clay per cell, hammer, chisel (`R/MoltenRecipeDefinitions.cs:48-236`; `RecipeIngredients.cs:111-114`); tap `HFK` with 4 clay (:113-114). Diagram route for straight/bend is creative-only per `D/molten-canal.md:140-142` (UNVERIFIED in code). Bed: `BBB,_H_` = 12 brick + hammer (`R/CastingRecipeDefinitions.cs:275-291`), then RCC 8 brick / 16 brick / 12 `iiex:greensand` (`C/Blocks/BlockSandCastingBed.cs:73-104`); greensand = 8 sand + 1 blue clay -> 8 (:299-309).
**STRUCTURE**: start fallback faces south (`M/Blocks/BlockMoltenCanalStart.cs:65`) and must sit at the tap's spout cell. Bed 3x1x4, 11 fillers, angle side+180 (:193-194); feed must touch the **principal** horizontally (`C/BlockEntities/BlockEntitySandCastingBed.cs:249-273`).
**VERBS**: start plain RMB returns false (crucible pour path, :76-88). Canal: chisel in hand + hammer off-hand on solidified+hardened -> chip out (`M/Blocks/BlockMoltenCanal.cs:449-458`; `X/Metals/MoltenChisel.cs:38-42`); >=4 fire clay on an empty straight -> seal (:465-484; `Cfg:109`); chisel on sealed -> unseal, refund 2 (:488-506). Tap (`M/Blocks/BlockMoltenCanalTap.cs:96-178`): **Sneak**+RMB park/retrieve barrel or large mold; **Ctrl**+RMB toggle pour (Latch sound); plain RMB chisels a clog. Bed (`BlockEntitySandCastingBed.cs:325-400`): RMB on sand -> carve (spine = runner, flank = mold); RMB on a filled cell -> harvest if hardened else `iiex-castingbed-toohot`.
**PROCESS**: capacities start 100 / canal 50 / tap 25 (`M/BlockEntities/BlockEntityMoltenCanalStart.cs:169-170`; `Cfg:97`; `BlockEntityMoltenCanalTap.cs:48-49`); 50 u/s per edge, min 10 (`X/ExlibConfig.cs:84,88`); tap drains 20 u/s (`Cfg:100`) into barrel 800/mold; bed pulls 25 u/tick into basin 200, runner 50, mold 750/1125 (:37; `BlockSandCastingBed.cs:66-68`). Harvest: pigs **375** / chunks 25 / bits 5 (`src/IronIndustryExpanded/Items/ItemPig.cs:250-255`; :407-414), slag -> `slagbrick` (:456-460), runner residue -> bits (:442-454). Solidifies below 1150, chisel below ~345 C.
**READOUTS**: "Poured: n units" (5 s); canal content/solidified/cooling/chiselready/sealed (`M/BlockEntities/BlockEntityMoltenCanal.cs:631-678`; `Lang:341-346`); tap Barrel/Mold + "Pouring: on/off" (:585-613); bed "Basin: n units, T", "{0} pig(s) ready", "{0} mold(s) still cooling" (:589-628; `Lang:388-391`). Glow light, molten surface renderers, PourMetal at the start (:260-268), MoltenMetal at the tap drain (:412-420), Sizzle when breaking a liquid canal (`BlockMoltenCanal.cs:369`).
**KNOWN GAPS**: E/W-facing bed misreads runners vs molds (`:611`, design gotcha 1); "ready" counts slag as pigs (:619); a bed broken with metal voids it; hand pour into a barrel transfers nothing (design Open, unverified in game); tap barrel code now falls back to `moltenbarrel-plated` (`BlockEntityMoltenCanalTap.cs:303-305`) - design gotcha 1 partly stale, UNVERIFIED in game; bed shape is now **tracked** (`assets/iiex/shapes/casting/sandcastingbed.json`, design "untracked" stale) but has no editable source.

## Tester checklist
1. Ctrl+Shift+RMB the core: outline appears; a backwards tuyere/tap is named by position in chat; "complete!" fires.
2. Hopper: coke load lays a bottom course level across all 9 columns; burden refuses to sit under coke; "Top course" line matches.
3. Idle HUD reads "needs more charge" on a lightable but non-full shaft - note it.
4. Empty hand breaks the plug (plug gone, `Tap is Open`); torch through the open hole -> torch-ignite sound, state Firing; second torch -> "already blown in".
5. Blower on a geared axle: tuyere pressure >= ~2.0 atm, throughput non-zero; ungeared wheel stalls the melt (verify the HUD claims 100% while the line leaks).
6. Melting: hearthmetal blocks appear in the 3 pool cells; iron tap line "Molten Pig Iron: x / 1920".
7. Open iron tap with a start at (3,0,0): "Poured" tally climbs ~30 u/s; without a start: "No canal start found".
8. Bed fed at the principal: basin fills, molds harden, harvest gives 375-u pigs; slag run yields bricks.
9. Stop the axle: "Starving for air", Firing not Melting, no countdown; break a wall: keeps burning; let the raceway die: extinguish sound, piles shrink, hearth stays.
10. Chisel a cold hearth block: expect the raw `ingameerror-iiex-hearthtoohot` key while warm.
