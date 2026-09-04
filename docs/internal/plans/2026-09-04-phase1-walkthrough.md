# Phase 1 - walk the iron loop in game

**Status** ready 2026-09-04 (roadmap item 3). The owner walks it; the assistant fixes what it finds
(roadmap item 5). Every station below is built and green in the harness and has never been seen by a
player. Findings go in [section Findings](#findings) at the bottom, one line each, and become the fix list.
The facts behind each section are in [../research/](../research/README.md) - this page keeps only what a
tester needs at the keyboard.

**World.** A flat creative area, `exlib` + `iiex` only (`siex` off: the cast-stock rows and the Bessemer
are its, and both wait on Phase 2's cast pipe). Take materials from creative; each station lists its
survival recipe so the survival gates can be walked at the end. Handy: `.exmod network hi` / `unhi`
colours every network; `/exmod config iiex <Key> <value>` changes a config value live; Ctrl+Shift+RMB on
any multiblock part shows the build outline and prints the shopping list; the crop, mill and furnace
readouts are the block-info text on the block you look at.

## Read first - what the code reading already predicts

Confirm these in a minute each; they are the fix list's first rows. "Research" names the file under
`docs/internal/research/2026-09-04-*.md`.

| # | Prediction | Research |
|---|---|---|
| P1 | The shingling work item `iiex:shingleworkitem-iron` names a class nothing registers; the log prints "no such class registered" and the anvil dereferences nothing on the first helve hit. Even if registered, vanilla's helve answers "not workable" for a recipe not named plate or blister steel. **Expect the helve route to fail**. **Fixed 2026-09-05** (own `ItemShingleWorkItem`; `IiexCollectibleClassTests`); confirm in game | puddling-helve-reheat section 2 |
| P2 | A full sand cast reads as a misrun at shake-out because the check runs against the cooled temperature: **every cast yields metal bits, never the part**. **Fixed 2026-09-05** (`cc_filltemp`, `IsMisrun` on the fill-tick temperature); confirm in game | casting-cells-detail section gaps 1 |
| P3 | The long cell's only intake face is the face its own filler stands on, so **cast stock cannot be poured**. **Fixed 2026-09-05** (shape spin 0, `LaunderFace` = the spout face for both cells; `CastingCellFootprintGuards`); confirm in game | casting-cells-detail section gaps 2 |
| P4 | A three-lane billet pour yields **one** billet, a two-lane bloom pour one bloom. **Fixed 2026-09-05** (lane count on the mold output: 3 billets, 2 blooms); confirm in game | casting-rack-workbench section 2 |
| P5 | The long cell's and the casting bed's drawn bodies sit **opposite** their footprints. Long cell **fixed 2026-09-05**; the bed **confirmed** by the same guard at all four sides (draws its rows on the side opposite its fillers), unfixed - owner's call | casting-cells-detail section gaps 3 |
| P6 | Chiselling a warm hearth block prints the raw key `ingameerror-iiex-hearthtoohot` (no lang entry) | blast-furnace-stations section 1 |
| P7 | Twin-tub output is **linear** in shaft speed; the August saturating-curve ruling is not built, and its "% of rated output" line lies under a leak | blast-furnace-stations section 4 |
| P8 | The flywheel prints "0.0 kW" and "0.0 kJ" at this calibration | mill-shear-benches section 1 |
| P9 | The puddling working-stroke animation never plays (server-side call, nothing syncs it) | puddling-helve-reheat section 1 |
| P10 | The coke oven's crown lid says "Main door"; lignite is refused as "already been through a fire"; the idle line says "Blast Furnace needs more charge" | coke-oven-crucible section 1 |
| P11 | A firebox bed is **never consumed while lit**: a puddling, reheat, coke or crucible furnace in Melting burns forever on one charge | puddling-helve-reheat section 1 |
| P12 | The boiler's water surface box was computed and never seen; **running the boiler dry only shuts it down** - the burst needs water above 300 L with the steam side blocked | boiler-engine-pipes section 1 |
| P13 | Sub-machine engine lookup is off by 90 deg (B21): a second Watt frame two cells west or north of a pump captures it | boiler-engine-pipes section 2 |
| P14 | Shear, bench and mill products spawn inside a filler cell; whether they can be picked up is unverified | mill-shear-benches section 2-4 |
| P15 | The crucible pot never shows its firing count (it shatters on the third pour with no warning) | coke-oven-crucible section 2 |
| P16 | The idle blast-furnace readout says "needs more charge to fire" on a shaft that is lightable but not full | blast-furnace-stations section 1 |

## Decide at the keyboard - process questions the code raised

These are not defects; they are the places where the built behaviour and the pillars disagree, or where
two machines follow different rules. Each needs your ruling; the walk is the right moment to feel them.

| # | Question | What the code does today | The choice |
|---|---|---|---|
| R1 | Should a firebox bed burn down while lit? | Puddling, reheat, coke and crucible furnaces never consume their bed once Melting; twelve units light them and they run until disrupted. The shaft furnace spends carbon every tick and ends when the raceway is dry | (a) beds burn at a rate per machine, campaigns end; (b) beds are an ignition cost only, as built |
| R2 | One ignition ritual, or three? | Blast furnace: torch through the open tap-hole. Boiler: hold the hatch with a full bed. Puddling, reheat, coke, crucible: light themselves the instant their beds are full | (a) hold-to-light on every firebox, so the crucible can be seated and charged before its fire starts; (b) keep auto-ignition and teach it |
| R3 | The crucible's damper trap | Fire starts on charging, damper starts shut, a shut damper never reaches heat, and after 20 minutes the fire dies and the bed is emptied | (a) R2's hold-to-light dissolves it; (b) the damper starts open and the pots crack if not preheated, as designed; (c) the readout warns "shut damper, N minutes of fire left" |
| R4 | The coke oven's hour | A bake is 60 loaded minutes; a reload catches up at most 10 minutes; vanilla's coal pile takes 12 game hours | (a) shorten the loaded cycle; (b) raise the catch-up cap for this machine; (c) keep it and make it a plan-your-day process |
| R5 | Feedwater against pressure | A one-atmosphere manual pump fills a five-atmosphere boiler | the injector design already rules it (intake gated on internal pressure, pumps as pressure multipliers); confirm it stays in Phase 3 |
| R6 | Products leave cold | Shear and bench products are minted at ambient; the remainder keeps its heat; mill claims carry heat | (a) products inherit the input's heat (the machining plan assumes this); (b) cold by design |
| R7 | Aiming a gap on the deck | A six-rung cast ladder splits three deck cells into half-cell click bands | confirmed by the raise-and-lower roller in the Phase 2 plan; nothing to decide unless the bands feel worse than predicted |

## Second tier - legibility and text

Tick what you see; each is a one-line fix and none blocks the walk.

| # | Where | What is wrong |
|---|---|---|
| L1 | casting cells, long cell | no interaction help at all |
| L2 | storage rack | refusals are silent |
| L3 | crucible pot | firing count never shown; shatters on the third pour without warning |
| L4 | rolling mill | fitted roll set never shown; "Jammed" for cold stock while the run turns |
| L5 | manual pump | no readout |
| L6 | blast furnace | idle line says "needs more charge to fire" on a lightable shaft |
| L7 | cupola | "Blast Furnace is ready!" |
| L8 | coke oven | lid says "Main door"; lignite refused as "already been through a fire"; "crossing the bridge" on a retort; idle line says "Blast Furnace needs more charge" |
| L9 | puddling hearth | "Charged: 0 / 9 pigs" during the bath; the not-lit line says "Ignite the charge inside" though ignition is automatic |
| L10 | flywheel | "0.0 kW" and "0.0 kJ" at this calibration |
| L11 | engine | repair bill printed untranslated |
| L12 | handbook | steam page quotes iron 5 / steel 10 atm tiers; blast-furnace page says "ignite the burden" (no plug, no torch); no page for the cupola, casting cells and patterns, storage rack, workbench or puddling furnace |

## Numbers that are wrong

| # | Where | Code | Documented |
|---|---|---|---|
| N1 | fluid pump / air blower | an undocumented x3 in the rate: 15 L/s and 43 L/s | 5 L/s and 14.4 L/s |
| N2 | Watt recipe | the gear ingredient has no cell, so it is free (B20) | 2 gears |
| N3 | sub-machine lookup | off by 90 deg (B21) | - |
| N4 | twin-tub blower | linear in shaft speed | saturating, per the August ruling |
| N5 | spur gear | chisel + 3 iron ingots | 2 ingots |
| N6 | puddling clean-out | cinder is a flat 3 | `CinderUnits` computed and unread |

## Art and footprint disagree

| # | Where | What |
|---|---|---|
| A1 | nail cutter | the shipped footprint is the mirror of the drawn body; the machine pokes into a cell it does not own, and a fresh export moves it a cell north |
| A2 | long cell, casting bed | the drawn body sits on the opposite side from the footprint (P5): long cell fixed 2026-09-05, bed confirmed and unfixed |
| A3 | 1 x 1 casting cell | the spout is drawn on the side opposite the face it pulls from - fixed 2026-09-05: both cells now pull at the drawn spout (`CastingCellFootprintGuards`) |
| A4 | casting bed east-west | runners and moulds read swapped at those two facings |
| A5 | hearth metal | a flat metal cube: no fill height, no glow |
| A6 | twin-tub blower, shafts, bevels, shear, benches, mill | no animation plays although the clips exist |

---

## 0. The drive

**Build** a vanilla waterwheel and axles; flywheel `DW,PG` = the flywheel diagram (kept) + 4 cast wheel
sections + 4 heavy cast plates + 1 spur gear; cast-iron shaft `HRP` = hammer + rod + plate -> 2 shafts;
spur gear = chisel + 3 iron ingots -> 1, or chisel + 1 cast-iron ingot -> 2.
**Place** the flywheel: 3 x 3 x 1, principal bottom-centre, hub cell directly above it. Vanilla axle into
the hub cell; cast shaft onto the principal's two axis faces.
**Do** turn the wheel; look at the flywheel.
**Expect** "Idle (not driven)" becomes "Charge: N %" and "Speed: N rpm" (19 rpm is the maximum); a bare
wheel spins up in about 45 s and coasts down in about 40 s. The axle must exceed half its rated speed to
move the run at all and 0.6 to reach full speed.
**Check** whether one waterwheel gets there (the `BridgeDriveTorque` open question); P8; shafts and bevels
never animate (known); the bevel gear is creative-only (the shaper makes it in Phase 2).

## 1. Cold blast furnace, hopper, burdenmaker, blower, canals, pig bed

**Build** core `BRP,BN_,BRP` (12 refractory brick of one tier, 4 rods, 4 nails, 4 plates); iron tap
`BPH,BFC,BBB` and slag tap `B_H,BFC,BBB` (10 tier-3 brick, 12 fire clay, plate on the iron one, hammer,
chisel); tuyere `BHB,BCB,BPB` (12 tier-3 brick, a plated straight pipe, hammer, chisel); 111 refractory
bricks of the core's tier; tall hopper `_H_,PSP,SPS` (3 plates, 3 nails or rivets); burdenmaker grid
`BBB,BBB` (12 burned bricks of one colour) then four RCC stages (12 brick, 16 brick, 6 iron plates,
3 plates + 2 iron ingots - one click per stage from the hotbar); twin-tub blower `LPL,PNP,_H_`
(4 leather, 3 planks, nails); canal start `HCK,FFF`, straight `H_K,FCF`, bends and junctions likewise
(cobble or brick + 2 fire clay per cell, hammer, chisel); pig bed `BBB,_H_` (12 burned brick) then RCC
8 brick, 16 brick, 12 green sand (8 sand + 1 blue clay -> 8).
**Place** to the outline: 6 x 9 x 5, core on the floor course; slag tap west facing **east**, iron tap
east facing **west** (both into the furnace); tuyeres at height 2 opening north and south; hopper at the
top facing east; canal starts one step out and one **down** from each tap. Blower 1 x 2 x 3, MP port on the
cell above its principal, blast main on the principal's orientation face only.
**Do**
1. Ctrl+Shift+RMB the core: outline; turn a tuyere inward and read the chat line naming it.
2. Burdenmaker: ore in the two wide front cells, flux in the narrow one (RMB one, Ctrl+RMB a stack), open
   the gate on the principal, take burden from the bunker.
3. Hopper: coke first (one load = one course, 128 units at 8/s), then burden; watch the shaft line.
4. Empty hand on the iron tap: the clay plug breaks, no refund; hold a lit torch into the open hole.
5. Blower on the geared axle; read the tuyere's pipe line.
6. Wait for Melting; open the iron tap over a canal start; carve the pig bed with plain RMB on sand.
7. Stop the axle mid-campaign; break a wall; let the raceway run out.
**Expect** shaft capacity 1152 units, 36 columns; "Top course: ... of 16 bands"; blow-in makes the torch
sound and "Firing"; tuyere pressure at or above about 2 atm; on the first melt three `hearthmetal` blocks
appear in the pool cells and the tap reads "Molten Pig Iron: x / 1920"; the start's "Poured" tally climbs
about 30 units a second; pigs harvest at 375 units; slag runs make slag bricks. Stopping the blast:
"Starving for air", Firing not Melting, no countdown. A breach keeps burning. A dead raceway: extinguish
sound, piles shrink, the hearth latches solid below 1150 C and chisels out below 30 %.
**Check** P6, P7, P16; a second torch says "already blown in"; re-plugging takes 4 fire clay; the tap
pours nothing without a start ("No canal start found below the tap"); a bed placed east-west reads its
runners and moulds correctly (design gotcha 1); the hopper has no fill indicator (known).

## 2. Cupola, casting cells, long cell

**Build** cupola core `BRB,PCP,BRB` (4 refractory of one tier, 2 rods, 1 plate, 8 fire clay) plus about
52 hand-laid refractory bricks, one tap pair and one tuyere; sand cell `BHB,BFB,BKB` (6 running or fire
bricks, 2 fire clay, hammer, chisel); long cell the same at double bricks and 4 clay; green sand as above;
patterns `DKP` = the item diagram (kept) + knife (kept) + 2 planks - the item diagrams are drafted at the
design table (charcoal or coal + paper).
**Place** cupola to its outline (taps at the bottom layer, tuyere above, hopper at layer 4, open top).
Cell and long cell: the canal end must touch the **spout** side, which faces you as you placed the
block; the long cell's body runs away from you.
**Do**
1. Cupola: coke then pigs through the hopper (Ctrl+RMB stacks); unplug the lower tap; torch it.
2. Ram green sand into a cell, imprint the heavy-plate pattern, feed it from the canal, wait, shake out
   with an empty hand once hardened.
3. Long cell: imprint the billet pattern; feed it; count what comes out.
4. Bed and long cell: stand in front and compare the drawn body with the outline.
**Expect** cupola melts at 1200 C, 60 units of cast iron per 12 charge units, tap line "Molten Cast Iron:
x / 640"; cell fills at 25 units a second (a plate in 7 s, a slab in 120 s), reads "Casting: n / cap,
liquid (T)", hardens at 360 C (0.3 x 1200), then "shake-out" is allowed.
**Check** P2, P3, P4, P5 first - they decide whether casting works at all; the cupola's ready line says
"Blast Furnace is ready" (wording); the canal feeds at the drawn spout (guarded headless, never seen in
game); no help text on either cell; breaking a cell with metal voids it.

## 3. Puddling furnace

**Build** the `reverberatory` set: puddling core `BBB,RCR,BPB` (4 brick, 2 rods, 8 fire clay, 1 plate),
hearth `_H_,VVV,N_N` (3 heavy cast plates, 4 nails), charge door `_H_,PSP,BBB` (2 tier-3 brick, plate,
2 nails), chimney cap `_R_,BPB,_H_` (2 tier-3 brick, rod, plate), firebox `BBB,RRR,BBB` (2 brick, rod),
rabble `_H_,_R_,P__`, paddle `_H_,_R_,__P`, fettle `FFF` (any three of crushed iron ore, tap cinder, mill
scale -> 3).
**Place** 8 x 3 x 8 to the outline; hearth facing north, door south, cap north; the four flue cells and
the chimney are part of the drawing (the cap sits above the top flue). Not player-extended.
**Do**
1. Fettle each row; load pigs - the two flank rows first, the centre last (a loaded centre blocks the
   flanks); 9 pigs.
2. Charge the firebox with 12 units of coke in one click. It lights itself. Throw the damper **open**,
   keep the main door **shut**.
3. After Melting begins, watch the bath replace the pigs. Open the small door (Shift+RMB the door).
4. Rabble with the rabble; draw with the paddle; sixteen balls. Clean out with an empty hand at the end.
5. Second heat: rake fuel below 6 units mid-bath and watch the countdown; let it go out.
**Expect** damper open + door shut settles about 1420 C against the 1400 C process line; damper shut
about 907 C (never melts; the fire dies after 20 minutes with the bed emptied); Melting after 300 s at
temperature, then the charge goes down 8 % every 10 s; a ball is 200 units at the bath's temperature, about
1400 C; nine pigs give 16 balls and 175 units of cinder; a stroke within 3 s says "too soon"; clean-out
gives 3 tap cinder; the frozen bath is only reported, never drawn.
**Check** P9, P11; the not-lit line is the blast furnace's ("Ignite the charge inside") though ignition
is automatic; during the bath the hearth still reads "Charged: 0 / 9 pigs"; lignite refused with the
metallurgical message.

## 4. Helve shingling

**Build** a vanilla helve hammer over an anvil.
**Do** with a ball above 750 C, RMB the anvil: a 16 x 5 metal layer; a second ball piles on it; run the
helve. **Before that, read `server-main.log`** for "no such class registered" on the shingle work item.
**Expect** two balls -> one shingled bar of 400 units, hot in hand, named "Shingled Bar".
**Check** P1 - this is the walk's likeliest crash; a third ball is refused; the bar takes the first
ball's temperature.

## 5. Reheat (heating) furnace

**Build** heating core `BPB,RCR,BBB`, heating hearth `_H_,VVV,VVV` (6 heavy cast plates), plain charge
door, two fireboxes.
**Place** 8 x 4 x 5 to the outline, two flue cells, no cap.
**Do** charge one firebox (the deposit spreads over both cells, 24 units), it lights itself; lay a bar on
a row with RMB (both depth cells of a column are one row); try a rod, a beam, a heavy plate; take a hot
bar back with an empty hand.
**Expect** a bar reaches 900 C in about 40 s, a slab in about 58 s; "Soaking: n piece(s), m at rolling
heat"; rod, beam and heavy plate are refused (no bed art, Open 12).
**Check** P11; a part-rolled bar draws as a fresh one (per-form art); the crosswise seating is not built.

## 6. Rolling mill

**Build** `PRP,PGP,PHP` = 6 heavy cast plates, 2 rods, 1 spur gear, hammer. Roll sets are
**creative-only** (the lathe makes them in Phase 2): flat, grooved, flatwide.
**Place** the mill (3 x 3 x 2; the principal is the east end of the axle line in the `we` frame); drive
on the principal's axis faces or the far axle cell.
**Do**
1. Fit the flat set (RMB anywhere with it). Feed a **hot** shingled bar on the input deck - the north row
   in the `we` frame - plain RMB for the near side, sneak for the far side; the click position along the
   three deck cells picks the gap band, thickest at the far end from the principal.
2. Feed the same side twice: the second feed is refused until the other side is fed.
3. Walk 2.5 -> 2.0 with the grooved set; take the piece from the output deck; look at it.
4. Feed a vanilla iron rod (hot) on the grooved set, then on the flat set.
5. Fit a set mid-pass; wrench mid-pass.
**Expect** a cold piece is refused "too cold" (creative pieces carry no temperature - heat them first); a
0.25 draft bites, a skipped gap "won't bite"; the pass takes about 2.3 s at full speed; the piece renders
at its stage on the deck; rod -> 4 rivet rods (grooved) or a nail plate claimed at the mill (flat);
shingled bar at flat 2.0 claims a beam.
**Check** P14 (the ejected piece lands inside the deck filler); the mill never shows the fitted set;
"Jammed" appears for cold stock while the run turns; a wrench on an **idle** mill may rotate it and
strand its fillers (do this last, in a spare mill).

## 7. Crop shear

**Build** `PRP,PGP,_H_` = 4 heavy cast plates, 2 rods, spur gear, hammer; blade set `PP,PP,_H` = 4 iron
or steel plates + hammer.
**Place** 3 x 1 x 2: nest cell, principal, gear cell; drive on the principal's north/south faces.
**Do** fit the blade set; RMB a rolled piece against the **nest cell**; repeat; try a cold piece; try with
the run stopped; try a beam that was rolled on the wide set.
**Expect** one product per stroke and the remainder handed back; a bar at grooved 2.0 gives 4 rods and is
spent after four; a cold cut needs twice the torque (all shipped rows pass at full drive); "Cutting - N s
left"; refusals in the order no blades, no job, spent, blade too soft, at rest, not enough drive.
**Check** P14; a mid-stroke click reports "at rest"; a beam carrying the wide family matches no crop row
(known); no animation or sound.

## 8. Nail cutter and riveter

**Build** nail cutter `_R_,PGP,_H_` (2 heavy plates, rod, gear), riveter `PRP,PGP,_H_` (4 plates, 2 rods,
gear); nail die `PP,_H` (2 iron plates), rivet die `P_,PH`.
**Place** nail cutter 1 x 2 x 2 (working face above the principal), riveter 3 x 2 (working face west of
the principal in the `ns` frame).
**Do** fit the die; RMB a nail plate on the nail cutter's face cell; a rivet rod on the riveter's; swap
the dies.
**Expect** 4 bundles of nails per plate in 2 s, 2 bundles of rivets per rod in 3 s; a die naming the
other bench gives "no job".
**Check** P14 (the bundles spawn one block above the principal, a solid filler); the class comment says
the nail cutter's shaft runs east-west while the shape runs north-south.

## 9. Storage rack

**Build** `P_P,PPP` = 5 planks -> 2 racks. **Place** it: 1 x 3, attachable.
**Do** lay a cast slab (fills all three cells); lay billets; take with an empty hand on any cell of a
run; put a torch on a filler cell.
**Expect** occupancy: rod, heavy plate, boiler plate, pig, billet, bloom take one cell; bar, beam, slab
two; cast slab three; items drawn on the rails at half height, centred on their run.
**Check** the renderer (never seen); refusals are silent; vertical stacking of racks.

## 10. Workbench and design table

**Build** workbench `_H_,V_V,PPP` (2 plates, 3 planks, hammer); design table `CPC,PPP` (2 candles,
4 planks).
**Do** open the workbench from either cell; lay a vanilla 3 x 3 recipe inside the 5 x 5; hold the Craft
button. Design table: charcoal + paper, pick a diagram, Draw.
**Expect** about three crafts a second into the five-slot output row while held, stopping on release;
the preview updates as you place items; every loaded diagram is in the picker and Draw consumes one
medium and one sheet.
**Check** the held-button loop in play; the design table model overhangs its neighbour with no filler
(known); 15 of 27 diagrams have no consumer (known).

## 11. Beehive coke oven

**Build** core `BBB,BHB,BBB` (8 fire burned bricks), 2 crown lids `_P_,BSB,_H_` (4 tier-3 brick, plate,
2 nails), 2 charge doors, 12 fireboxes, 93 fire clay bricks, 8 fire brick slabs (crown) and 4 (door
cheeks).
**Place** 9 x 4 x 4 to the outline, doors south.
**Do** open a door, RMB bituminous coal on a firebox cell (144 coal fills all twelve); shut both doors and
both lids; read the core; open one lid mid-bake; reload the world mid-bake; wait.
**Expect** it lights itself (torch sound) at 144; "Chamber n: standing open" until lid and door are shut,
then "baking, N %" at about 1.7 % a minute - **a real loaded hour**; a reload catches up at most 600 s;
10 coke per cell, 120 per oven, drawn two units a click; the fire dies about 30 s after the total drops
below 72.
**Check** P10, P11; lignite, anthracite, charcoal and coke are all refused; whether every cell of a
chamber is reachable through the door and the crown void.

## 12. Crucible furnace and crucible steel

**Build** core `BBB,BCB,BRB` (24 refractory of one tier, 8 fire clay, 4 rods), hearth `CCC,RRR,BBB`
(6 fire clay, 3 rods, 6 brick), chimney cap, charge door, a vanilla coke-oven door for the ash pit,
19 good refractory bricks, one base ring of bricks; pots: clay-form a crucible from fire clay, fire it in a
pit kiln; blister chunks: RMB a **cold** blister-steel ingot on an anvil and run the helve; ingot mould:
sand-cast from the ingot-mould pattern.
**Place** 4 x 4 x 5 to the outline; then build the chimney upward as rings of bricks around an air column
from the top drawn flue cell - six courses in total melts, nine is the peak.
**Do** - order matters, the fire lights itself the moment the beds are full:
1. Damper **shut** (it starts shut; throw it from the north housing). Seat four fired pots (RMB the
   hearth); a fifth is refused. Charge each: 4 chunks + 2 bits (110 units), a fifth chunk refused whole.
2. Charge the firebox with 12 coke. Wait 240 s per pot for "warming through".
3. Open the damper. If any pot has not finished warming it **cracks** (ceramic sound, contents drop into
   the ash pit - collect through its door).
4. Melting after 300 s above 1600 C; each pot takes about 91/2 minutes; pull with an empty hand (a molten
   pot first); hold RMB with the pot on the ingot mould to pour 100 units.
5. Pour twice more from the same pot.
**Expect** "Stack: N course(s) ... a taller chimney would pull harder (peak at 9)"; "Damper shut: the pots
are coming up gently"; "Hole n: ... 100 u of crucible steel, ready to pull"; the ingot is `cruciblesteel`;
a crucible-steel pickaxe shows 3300 durability; the pot shatters on the third pour.
**Check** P11, P15; the damper's reach from the ground; a raw or full pot handed to the hearth gives a
misleading "not fuel"; pouring into a vanilla clay mould is refused silently.

## 13. Cornish boiler megablock, Watt engine, pipes

**Build** frame `PHP,BNB` (2 plates, 4 fire burned bricks, 2 nails, hammer); RCC from the hotbar:
stage 1 free; 2 = 6 plates, 8 rivets, 8 fire brick; 3 = 8 plates, 8 rivets, 4 rods; 4 = 8 plates,
16 rivets, 4 rods, 36 fire brick - 22 / 32 / 8 / 44 in all, rivets only. Watt `_H_,PRP,PIP` (4 plates,
2 rods, a plated straight; the gear is free - B20) and six RCC stages; pump `_HG,PIP,RIR`; **cast pipe
from creative** - a plated main bursts at 2.5 atm and the boiler runs to 5.
**Place** the boiler needs a clear 3 x 6 x 3 and is raised away from you: feedwater on the principal's
own face opposite the body, steam on the up-port cell three back, exhaust on the east-port cell at the far
end. Watt: 1 x 4 x 3 slice, the sub-machine snaps 90 deg clockwise into the drive cell whichever is placed
first.
**Do** (the boiler plan's gate, corrected)
1. Raise the four stages and watch each course appear; no fire brick placed by hand.
2. Hold the main hatch open; click 16 **lignite** in; hold to light; hold to shut.
3. Feedwater pipe on the feed face; steam pipe on the cell above the up-port; exhaust pipe east.
4. Block the exhaust: "Choked, exhaust backing up!" at once, the fire out after 10 s.
5. Two Watts **side by side** (not in line) on a cast main through a pressure valve at 2-3 atm.
6. Man hatch: bail with an empty bucket (stops at 300 L); open it under pressure.
7. Burst: keep water above 300 L, fire lit, steam side unpiped or blocked.
8. B21: with one Watt and its pump working, place a second Watt **frame** two cells west or north of the
   pump, then break and re-place the pump.
**Expect** lignite about 58 L/s and a 198 s heat-up (every other coal 64 L/s at once; anthracite runs the
bed 52 minutes, coke 11); "Water: x / 1000 L", "Steam: L (atm)", "Boiling ..."; man hatch under pressure
vents 200 L/s and resets the burst clock; "Over-pressure! 30 s until bursts!" then the explosion; the
Watt engages at 2 atm and breaks above 4 for 60 s; the pump delivers about 15 L/s at 0.3 power (the
undocumented x3); in the B21 setup the first engine keeps cycling and "Draws steam 30 L/s" while the pump
sits idle.
**Check** P12 (look at the water surface through the open man hatch), P13; the handbook's steam page
still quotes the old iron 5 / steel 10 atm tiers; a refused joint between tiers does not leak (B18);
the manual pump has no readout.

## Survival gates, once the stations work

1. Ore to pig: burdenmaker, blast furnace, canal, bed - every recipe above from a survival inventory.
2. Pig to bar: puddling heat, helve.
3. Bar to parts: reheat, mill (creative roll set), shear, benches - the roll set is Phase 2's gate.
4. Coal to coke, blister to crucible steel.
5. Boiler and one Watt running one pump, all from survival (cast pipe stays creative until Phase 2).

## Findings

One line each, newest at the bottom: `station . what happened . what was expected . P# if predicted`.

| # | Station | Finding |
|---|---|---|
| | | |
