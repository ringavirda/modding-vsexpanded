# Puddling

**Status** live - the whole heat is built (U6, 2026-08-21) and the ball leaves at the bath's own temperature;
the stroke cooldown and the ball count are play-tuning numbers, and none of it has been seen in game
**Mods** iiex owns the whole process end to end. Only the downstream consumption crosses a mod boundary
(the balls go to [shingling](shingling.md), whose slab half is iiex's)

## Owns

* The charge arithmetic - 9 pigs = 3375 u → 16 balls (3200 u) + 175 u tap cinder, its derivation, and
  the fact that both the ball count and the remainder are forced by division, not chosen;
* the loop: which player verb performs each step, in which order, and which steps exist in code today;
* the fettle economy - 3 fettle per heat, the `fettlestock` interchange, and the closed oxide loop the
  clean-out is supposed to close;
* the charge-order rule as a process fact (fettle before pig; centre row blocks the flanks on the way in
  and on the way out) - the machine page owns the code that enforces it;
* what the mod abstracts away from real puddling, and the four things it keeps;
* the recovery accounting for this process: why the historical 8-12 % loss band is not a constraint here
  (R2, R6).

## Does not own - cited only, never restated

| Fact | Owner |
|---|---|
| the furnace's layout, cells, part blocks, art, drops, and the reasons it cannot run (**B8** + the orphan fillers) | [puddling furnace](../machines/puddling-furnace.md) |
| `T_process = T_in − T_loss`, every furnace tunable, the Idle → Firing → Melting FSM | [heat balance](../mechanics/heat-balance.md) |
| `1 vx³ = 2.5 u`, the pig mass of 375 u, and every measured mass | [density rule](../mechanics/density-rule.md) |
| the pile-picks-form rule, the ball → bar/slab ladder, why the helve survives | [shingling](shingling.md) |
| where pigs come from, and the bed rotation that supplies them | [blast furnace](../machines/blast-furnace-cold.md), [casting bed](../machines/casting-bed.md) |
| the other pig consumer, and pig remelt | [cupola](../machines/cupola.md) |
| where the wrought stock goes, and the reheat/handling limits it must respect | [reheat furnace](../machines/reheat-furnace.md), [rolling mill](../machines/rolling-mill.md), [recoverability](../mechanics/recoverability.md) |
| filler footprints, interaction rerouting, the completion walk | [multiblock & fillers](../mechanics/multiblock.md) |
| code-first defs, grid recipes, goldens, config layout | [recipes & config](../mechanics/recipes-config.md) |

**Depends on** [puddling furnace](../machines/puddling-furnace.md) ·
[heat balance](../mechanics/heat-balance.md) · [density rule](../mechanics/density-rule.md) ·
[shingling](shingling.md) · [casting bed](../machines/casting-bed.md) ·
[cupola](../machines/cupola.md) · [reheat furnace](../machines/reheat-furnace.md) ·
[rolling mill](../machines/rolling-mill.md) · [recoverability](../mechanics/recoverability.md)

---

## What it is

Puddling is the only route to wrought iron in the suite, and the one process in the iron line that is a
craft rather than a machine setting.

The real thing (Cort 1784, then Hall's wet puddling c. 1830): pig iron is melted on the fettled bed of a
reverberatory furnace, where the flame reaches it but the fuel never does. Carbon burns out of the melt
against the oxygen in the iron-oxide fettling and in the flame. As the carbon leaves, iron's melting point
rises past the furnace's own temperature, so the bath does not stay liquid: it goes pasty, "comes to
nature", and the puddler gathers the stiffening metal into balls with a rabbling bar, working through a
hand-sized door for the better part of an hour so the heat does not pour out. Each ball is drawn white-hot
and taken straight to the hammer.

What the mod abstracts away

| Real | Mod |
|---|---|
| ~1 hour of continuous, brutal manual work per heat | a handful of right-clicks on a timer |
| dry (Cort) vs wet / pig-boiling (Hall) puddling | one process |
| silicon, phosphorus and manganese chemistry; the slag's composition | one `fettlestock` oxide family, chemically undifferentiated ([`FettleRecipeDefinitions.cs:16-19`](../../../mods/iiex/src/Recipes/Grid/FettleRecipeDefinitions.cs)) |
| "coming to nature" as a continuously observed state | a discrete ball-up step |
| ball weights varying with the puddler's judgement | a fixed 200 u ball |

What it keeps - each of these is a mechanic, not flavour:

1. Fettling is a consumable, charged every heat. It is the reagent the reaction runs on, not a lining
   ([`FettleItemDefinitions.cs:6-22`](../../../mods/iiex/src/Items/FettleItemDefinitions.cs)).
2. Fuel never touches work. One coordinate change on the shared furnace core - see
   [puddling furnace](../machines/puddling-furnace.md).
3. Two doors. The charge goes in through the big one; the bath is worked through the small one, so the
   heat does not leave ([`BlockChargeDoor.cs:19-21`](../../../mods/iiex/src/BlockStructures/Furnaces/Blocks/BlockChargeDoor.cs)).
4. Natural draught. No blower and no tuyeres - a damper on the stack is the only air control there is
   (`BlockEntityPuddlingFurnace.cs:44`, `:46-47`). It involves no pipe network.

---

## The loop

One heat = one full pass of this table. "Built" is the state in the tree today.

| # | Step | Where | Player verb | Built | Cite |
|---|---|---|---|---|---|
| 1 | Prepare fettle - 3 `fettlestock` → 3 `iiex:puddlingfettle` | crafting grid | grid craft | live | `FettleRecipeDefinitions.cs:29-42` |
| 2 | Fettle the bed - one per row, 3 total | any hearth cell | RMB holding fettle | live | `BlockPuddlingHearth.cs:103-110`, `BlockEntityPuddlingHearth.cs:48-57` |
| 3 | Charge 9 pigs - 3 per row | any hearth cell | RMB holding pig | live | `BlockPuddlingHearth.cs:112-121`, `BlockEntityPuddlingHearth.cs:63-72` |
| 4 | Shut the big door | charge door | RMB | pose only | `BlockChargeDoor.cs:118-121` - `IsVenting` has no reader (`BlockEntityChargeDoor.cs:44`) |
| 5 | Fire the firebox with coke | firebox cell / `K` door | place fuel | live | `BEBehaviorFirebox.IsFuel` takes coke, bituminous, anthracite or charcoal and refuses lignite |
| 6 | Set the damper | chimney cap | RMB | pose only | `BlockEntityPuddlingChimneyCap.cs:25-30`; `IsOpen` has no reader (`:22`) |
| 7 | Melt down | the core's FSM | wait | unreachable | `T_process ≤ ~1392.5 °C` against a melt point of 1482 - **B8**, [puddling furnace § Gotchas](../machines/puddling-furnace.md#gotchas) |
| 8 | Rabble the pasty bath | small working door | repeated RMB with the rabbling bar | not built | art exists three times over - see [Gotchas](#gotchas) |
| 9 | Ball up | hearth | (falls out of rabbling) | not built | - |
| 10 | Draw the balls, one at a time | hearth cells, centre row first | RMB empty-handed | not built | reach rule live at `HearthRows.cs:75-76` |
| 11 | Clean the bed - spent fettle and cinder come out together | hearth | RMB with a tool? (verb unchosen) | method exists, no caller | `BlockEntityPuddlingHearth.cs:83-91` |

Then the balls go to [shingling](shingling.md), and the tap cinder goes back into step 1.

The order in steps 2-3 is the mechanic, not a convenience. A row that has not been fettled refuses pig
(`BlockEntityPuddlingHearth.cs:67`) because pig laid on a bare bottom plate welds itself to it. A loaded
centre row blocks both flanks (`HearthRows.cs:75-76`), so the bed is loaded flanks-first and unloaded
centre-first, which is the order a puddler drew balls, arrived at from geometry rather than written as a
rule.

---

## Inputs and outputs

### Per heat - settled

| Direction | Item | Qty | Mass each | Total | Cite |
|---|---|---|---|---|---|
| in | `iiex:pig` | 9 | 375 u | 3375 u | count `PuddlingHearthLayout.cs:22`; mass [density rule](../mechanics/density-rule.md) |
| in | `iiex:puddlingfettle` | 3 | - (carries no mass constant) | - | `FettleItemDefinitions.cs:46` × 3 rows |
| in | coke | not chosen | - | - | [Open](#open) |
| out | wrought ball | 16 | 200 u | 3200 u | this page; ball mass [shingling](shingling.md) |
| out | `iiex:tapcinder` | 3 (settled, code pending - [economy landing](../items/economy-landing.md)) | - | 175 u | this page; item at `FettleItemDefinitions.cs:77-78` |

### The fettle loop

`iiex:puddlingfettle` is made from any three `fettlestock` items, and there are three of them
(`FettleItemDefinitions.cs:43`, `:57`):

| `fettlestock` material | Comes from | Cite |
|---|---|---|
| `game:crushed-iron` | mined and crushed ore - magnetite, hematite and limonite all reduce to this one item | `FettleItemDefinitions.cs:33` |
| `iiex:tapcinder` | this process, step 11 | `FettleItemDefinitions.cs:77-78` |
| `iiex:millscale` | the [rolling mill](../machines/rolling-mill.md) | `FettleItemDefinitions.cs:85-86` |

The recipe is 1 : 1 - three stock in, three fettle out (`FettleRecipeDefinitions.cs:35-41`) - so a works
that is already running displaces its ore cost one part at a time until fettling costs no ore at all. A new
shop pays in ore; a working one feeds itself.

---

## Numbers

Everything in this section is this page's, and every row below the first is arithmetic on the row above
it. Nothing here is a free tuning knob.

### The charge

| Quantity | Value | Derivation / cite |
|---|---|---|
| pigs per row | 3 | `PuddlingHearthLayout.cs:19` - a pig is triangular in section, so two lie on the bed and the third nests in the groove |
| rows | 3 | `HeatingHearthLayout.Rows`, shared with the reheat hearth (`BlockEntityPuddlingHearth.cs:30-31`) |
| charge | 9 pigs | `PuddlingHearthLayout.cs:22` (`3 * PigsPerRow`) |
| pig mass | 375 u | [density rule](../mechanics/density-rule.md) - shipped (`Items/ItemPig.cs:39`) |
| charge mass | 3375 u | 9 × 375 |
| fettle per heat | 3 | `FettleItemDefinitions.cs:46` (`PerHearthCell = 1`) × 3 rows |

### The yield - derived, not chosen

| Quantity | Value | Derivation |
|---|---|---|
| ball mass | 200 u | [shingling](shingling.md) |
| balls per heat | 16 | ⌊3375 / 200⌋ = 16 - the count is forced |
| iron out | 3200 u | 16 × 200 |
| tap cinder | 175 u | 3375 − 3200 = 3375 mod 200 - the remainder is forced |
| split | 94.81 % iron / 5.19 % cinder | 3200 / 3375 |

Once the pig is 375 u and the ball is 200 u, 16-and-175 is the only answer integer division allows.

### The heat divides through both shingling piles

16 is divisible by 2 and leaves only bar-sized remainders against 6, so no heat ever strands metal:

| Mix | Balls used | Cite |
|---|---|---|
| 8 bars | 8 × 2 = 16 | [shingling](shingling.md) |
| 1 slab + 5 bars | 6 + 10 = 16 | " |
| 2 slabs + 2 bars | 12 + 4 = 16 | " |

There is no third product and no fourth mix; 3 slabs would need 18 balls, i.e. 3600 u, i.e. more metal than
the charge contains.

### Recovery

The historical puddling loss band is 8-12 %, and this process does not hit it, because nothing is
destroyed. The 175 u is split off, not burned away: it comes back as tap cinder and fettles the next heat.
That keeps R2 (declared recovery, nothing hidden) and R6 (stock carries its mass) - both
[conventions](../conventions.md)'s rules - exact through the one process that would otherwise have had to
break them.

---

## Why it is like this

Nine pigs, because the bed is nine pigs. The charge is what the drawn hearth holds: three rows, three pigs
each, stacked on their triangular section (`PuddlingHearthLayout.cs:17-19`), and the shipped shape draws
exactly nine pig elements at the pig item's own geometry
(`mods/iiex/assets/iiex/shapes/furnaces/puddlinghearth.json`, `Pigs/Pig1…Pig9`, each 5 × 2 × 12 with a
3 × 1 × 12 child = 156 vx³ - the same volume [density rule](../mechanics/density-rule.md) measures off
`item-pig.json`). The art, the layout constant and the charge are one fact.

The loss is a split, not a loss. Modelling puddling's real 8-12 % burn-off as destroyed metal makes the
number a tuning knob nobody can check, and violates R2 and R6 the moment a player weighs what went in.
Recovering it as tap cinder is also the historically correct answer - roasted tap cinder ("bull dog") was
the standard British fettling. This furnace is a little more efficient than the real one, which is a stated
consequence.

The bed is cleaned, not tapped. A puddling furnace makes far too little slag to plumb, and what it makes is
stiff tap cinder rather than a pour - the 175 u remainder could not even fill one 375 u slag brick
(`SlagBrickUnits = ItemPig.PigUnits`, `Items/SlagItemDefinitions.cs:27`). So there is no slag tap block and
no canal: one clean-out returns the spent fettle and the cinder together. That removes a block from the
layout and puts the by-product back in the player's hands as an ingredient instead of routing it to a
second disposal problem ([puddling furnace](../machines/puddling-furnace.md)).

A damper instead of a blower. The blast furnace's air is a supply the player builds and plumbs; this
furnace's is a draught that exists for free and is only ever throttled. That is why the machine can never
air-starve (`BlockEntityPuddlingFurnace.cs:44`).

---

## Gotchas

* The clean-out has no caller. `ClearBed()` (`BlockEntityPuddlingHearth.cs:83-91`) is the design's
  answer to where the cinder goes and nothing in the tree calls it. Until it does, step 11 of the loop
  does not exist and the fettle loop is open at one end.
* The 175 u has no item count in code yet. `iiex:tapcinder` is a plain `Recovered` item with no
  `materialUnits` attribute and no unit constant (`FettleItemDefinitions.cs:62-70`, `:77-78`). The count is
  settled at 3 - the number that closes the fettle loop - and lands with the mass batch
  ([economy landing](../items/economy-landing.md)).
* The wrought ball does not exist as an item. Nothing in `src/` produces, consumes or names one, so the
  entire output side of this table is unimplemented. See [shingling § Open](shingling.md#open).
* The rabbling art exists three times and is wired to nothing.
  1. `workbench/shapes/furnace-megablock-puddlingchargedoor.json` carries a `Tools` group with
     `Tools/Rabble` and `Tools/Paddle` racked on the door;
  2. it also carries `rabbling` (4 keyframes) and `paddle` (5 keyframes) animation clips;
  3. `workbench/shapes/item-tool-rabble.json` and `item-tool-paddle.json` are drawn as standalone
     items - and both are untracked in git.

  Code only ever plays the three door clips read from block attributes
  (`BlockEntityChargeDoor.cs:34-37`, `:70-95`); there is no rabble item, no lang key, and no grep hit for
  `rabble` or `paddle` outside doc-comments.
* Both tool clips are authored `onAnimationEnd: EaseOut`. Rabbling is a repeated verb; a clip played
  on a loop must be `Repeat` or the mesh vanishes mid-cycle.
* The hearth matches the held item on `Code.Path` alone, with no domain check
  (`BlockPuddlingHearth.cs:103`, `:112`). Any mod shipping an item whose path is `pig` or `puddlingfettle`
  charges this hearth, and the pig it charges is worth whatever this mod's constant says.
* `iiex:puddlingfettle` carries no mass and no `materialUnits` (`FettleItemDefinitions.cs:88-95`), so
  the oxide side of the loop is counted in items while the iron side is counted in units. That is probably
  right, but it is undeclared.
* The furnace cannot run today - the structure cannot complete, and even lit it could not melt:
  `T_process` tops out near 1392 °C against a 1482 °C melting point, an override knowingly left wrong
  pending the draught model (**B8**, `BlockEntityPuddlingFurnace.cs:55-63`). Both are
  [puddling furnace](../machines/puddling-furnace.md)'s to state and fix; neither is this process's design.

---

## Open

| # | Question | Size |
|---|---|---|
| 1 | Settled 2026-08-07: the 175 u is 3 `tapcinder` items - exactly the count that closes the fettle loop (3 cinder → 3 fettle → the next heat). Ruled with the mass batch in [economy landing](../items/economy-landing.md); the code lands with that batch | settled, code pending |
| 2 | `ClearBed` needs a caller and a verb. Empty-handed RMB? A tool? It is the only step with no gesture chosen | small |
| 3 | The rabbling verb's shape - partly settled 2026-08-05: the count is fixed at 16, one per ball (Open 5). What remains is calibration - the interval, settled to be a cooldown between rabbles rather than a fixed count, so pacing can move without changing the model - plus the held item (Open 4) and whether the small door must be open. The `rabbling` clip's 4 keyframes are the only other constraint | small |
| 4 | Is there a rabbling-bar item at all, or is rabbling done bare-handed against the door? Two item shapes are drawn for one that does not exist | small |
| 5 | Settled 2026-08-05: one ball per rabble. 16 gathering gestures, each producing a ball, each drawn out separately - as § *What it is* describes the craft. It is the only reading under which the `PuddlingProcessTempC = 1400` ruling's own justification holds: the ball is emergent from the temperature window, and rabbling is a verb the player performs rather than watches. "One per row" was rejected - 16 does not divide by 3, and the bed was worked as one bath; the rows are this mod's hearth abstraction, and `HearthRows`/`CanReach` stays the draw order only. Consequence for the art: 16 invisible gestures would violate R7, so the bath and ball group must be drawn on the hearth | settled |
| 6 | Coke per heat is unchosen. The firebox takes coke now; nothing consumes it at a per-heat rate | blocked on the cycle |
| 7 | The damper's effect on the heat balance. `IsOpen` and `IsVenting` are the intended draught model's two inputs and neither is read (`BlockEntityPuddlingChimneyCap.cs:22`, `BlockEntityChargeDoor.cs:44`). Until they are, "the furnace's only air control" controls nothing | medium |
| 8 | Does a heat have a duration the player can lose? Nothing spoils, nothing burns, and an abandoned charge is currently eternal - a fettled and charged bed is even silently destroyed if the block is broken ([puddling furnace § Drops](../machines/puddling-furnace.md)) | medium |
| 9 | Roasting. Real fettling was roasted oxide, which is what a reverberatory furnace is for; `FettleRecipeDefinitions.cs:22-25` already declares the hand-craft "interim" pending the [reheat furnace](../machines/reheat-furnace.md) learning to roast | medium |
