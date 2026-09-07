# Shingling

**Status** partial - the ball item, the helve route and `shingledbar` are built (`ItemPuddledBall`,
`ShinglingRecipeDefinitions`, U6 2026-08-21); the steam hammer and `shingledslab` are not
**Mods** iiex - the ball, the helve route, `shingledbar` · iiex - the steam hammer and `shingledslab`

## Owns

* the pile picks the form - the rule, the two piles, the remainder rule, and the two different
  reasons neither machine needs a UI for it;
* the two-machine split and why the helve survives the steam hammer instead of being replaced by it;
* the ball → form ladder as a process: 2 balls = bar, 6 balls = slab, and how it lands against one
  puddling heat;
* the consolidate-never-section boundary - where shingling stops and [rolling](../machines/rolling-mill.md)
  begins;
* what shingling abstracts away from the real process, and the one thing it keeps that costs the player
  something;
* the check of the drawn shingled art against the settled masses.

## Does not own - cited only, never restated

| Fact | Owner |
|---|---|
| the ball's 200 u, the bar's 400 u, the slab's 1200 u as item masses; `1 vx³ = 2.5 u` | [density rule](../mechanics/density-rule.md) |
| where the balls come from and the 16-ball heat | [puddling](puddling.md) |
| the steam hammer's footprint, ram, dies, steam draw, animation states and its second job (stamping) | [steam hammer](../machines/steam-hammer.md) |
| what happens to the shingled piece next - gaps, passes, spread, roll sets | [rolling mill](../machines/rolling-mill.md), [steel roll sets](../machines/steel-roll-sets.md) |
| putting heat back into a cold piece, and the ≤ 32 / ≤ 48 handling limits | [reheat furnace](../machines/reheat-furnace.md), [recoverability](../mechanics/recoverability.md) |
| every crop taken off a shingled piece | [shear](../machines/shear.md) |
| MP torque, the helve's drive, the flywheel | [mp energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md), [flywheel & shafting](../machines/flywheel-and-shafting.md) |
| the cast route's billet/bloom/slab, which are cast, not shingled | [long cell](../machines/long-cell.md), [casting cell](../machines/casting-cell.md) |

**Depends on** [puddling](puddling.md) · [steam hammer](../machines/steam-hammer.md) ·
[density rule](../mechanics/density-rule.md) · [rolling mill](../machines/rolling-mill.md) ·
[reheat furnace](../machines/reheat-furnace.md) · [recoverability](../mechanics/recoverability.md) ·
[shear](../machines/shear.md)

---

## What it is

A puddle ball comes off the hearth as a white-hot sponge: iron crystals with liquid slag trapped through
them. **Shingling** is the blows that squeeze the slag out and weld the iron into one solid piece. It is not
forming - nothing about the finished section is decided here - it is consolidation, and until it is done
the metal is not stock, it is a lump.

Historically it was done under a helve or tilt hammer, and from 1839 under Nasmyth's steam hammer (or
Burden's rotary squeezer). The steam hammer added a size class rather than retiring the helve, because a
single blow could consolidate a pile no helve could lift.

What the mod abstracts away

| Real | Mod |
|---|---|
| slag spraying out of the ball under the first blows - the most dangerous job in the works | nothing; there is no hazard |
| shingle → bloom → reheat → billet → reheat → merchant bar, several heats deep | one step, one piece |
| shingling and the first roughing pass being run together at the same hammer | a clean split: the hammer consolidates, the [mill](../machines/rolling-mill.md) forms |
| the puddler's judgement of when a ball is "clean" | a fixed number of blows |
| slag expelled as mass loss | none - the [puddling](puddling.md) split already accounted for it |

What it keeps, and what it costs. The piece leaves the hammer at forging heat and cools in the player's
hands - vanilla's own `temperature` attribute, so the cooling is the engine's
(`StockItemDefinitions.cs:44-55`). That is what turns the walk between machines into a real cost and what
gives the [reheat furnace](../machines/reheat-furnace.md) a job.

---

## The loop

| # | Step | Machine | Player verb | Built |
|---|---|---|---|---|
| 1 | Draw a white-hot ball from the hearth | [puddling furnace](../machines/puddling-furnace.md) | RMB the hearth row | no |
| 2 | Pile balls on the anvil | vanilla anvil | RMB, one ball at a time - the same voxel accumulation as stacking ingots for a vanilla plate | no |
| 3a | Strike - helve | helve hammer (MP, iiex) | run the helve; it works toward its one smithing recipe | no |
| 3b | Strike - steam hammer | steam hammer (LP, iiex) | pull the lever; the machine reads the anvil at the first pull | no |
| 4 | Carry the piece off | - | it is now generic wrought stock, cooling | the item exists (`stock-*`), the route into it does not |
| 5 | Reheat / roll / crop | [reheat furnace](../machines/reheat-furnace.md) → [rolling mill](../machines/rolling-mill.md) → [shear](../machines/shear.md) | - | mill live, nothing feeds it |

Implementation is the vanilla pipeline, not a new machine. The ball is a forgeable item (cf. vanilla's
`ItemIronBloom`) and shingling is an anvil + helve + smithing-recipe interaction. iiex already drives that
pipeline for a different purpose - a pig placed on the anvil becomes a marked work item and the helve
shatters it (`Items/ItemPig.cs:98-119`, `Patches/AnvilPigBreakingPatches.cs`,
`Recipes/Smithing/PigRecipeDefinitions.cs:20-35`) - so the template for the helve half already exists and is
tested.

---

## Inputs and outputs

| Pile | Balls | Mass in | Product | Section × length | vx³ | Mass out | Machine | Mod |
|---|---|---|---|---|---|---|---|---|
| small | 2 | 400 u | `shingledbar` | 3 × 3 × 18 | 162 | 400 u | helve - its only recipe | iiex |
| large | 6 | 1200 u | `shingledslab` | 8 × 3 × 20 | 480 | 1200 u | steam hammer only | iiex |

Both are the generic wrought stock - the same item family the mill eats. The hammer sets no section; it
only makes the lump into a piece. Both leave the hammer 3 voxels thick, which is the mill's entry
thickness for either form (`StockForm.cs:13`, `:48`, `:54`).

Mass is conserved exactly across the step. 2 × 200 = 400 and 6 × 200 = 1200; nothing is minted and
nothing is burned, because the puddling split already took the cinder out
([puddling § Numbers](puddling.md#numbers)).

### Against one heat

A [puddling](puddling.md) heat is 16 balls, and 16 is divisible by 2 and leaves only bar-sized remainders
against 6. There are exactly three mixes:

| Mix | Balls | Wrought out |
|---|---|---|
| 8 bars | 8 × 2 | 3200 u |
| 1 slab + 5 bars | 6 + 10 | 3200 u |
| 2 slabs + 2 bars | 12 + 4 | 3200 u |

Three slabs would need 18 balls. No heat can strand metal, and that is the property the ball mass was
chosen for.

---

## Numbers

### The rule

| Rule | Value | Consequence |
|---|---|---|
| pile threshold, bar | 2 balls | the fallback form; also what any remainder becomes |
| pile threshold, slab | 6 balls | 3× the bar - the wide route's entire premise |
| forms per action | one | a pull/blow consumes one form's worth and leaves the rest on the anvil |
| selection | largest form the pile affords | ⌊pile / 6⌋ first, else ⌊pile / 2⌋ |
| ceiling | 6 | nothing consumes more; a 7-ball pile makes a slab and leaves 1 |

Worked: 5 balls on the steam hammer → 5 < 6, so it makes a bar and leaves 3 on the anvil. 7 balls → a slab
and 1 left. 1 ball → nothing happens; a single ball is not a form.

### The two drawn shapes, measured

Measured off the files, children counted as additional solid per
[density rule § Measuring a shape](../mechanics/density-rule.md#how-it-works).

| Shape | State | Elements | Drawn | Rule says | Settled | Verdict |
|---|---|---|---|---|---|---|
| `workbench/shapes/item-shingled-slab.json` | untracked | `ShingledSlab1` 8 × 3 × 10 + child `ShingledSlab11` 8 × 3 × 10 | 8 × 3 × 20 = 480 vx³ | 1200 u | 1200 u | exact |
| `workbench/shapes/item-shingled-bar.json` | untracked | `ShingledBar1` 3 × 3 × 9 + child `SingledBar11` 3 × 3 × 9 | 3 × 3 × 18 = 162 vx³ | 405 u | 400 u | 5 u over - 400 is a rounding, and it needs saying |

### What ships instead

| Live thing | Value | file:line | Against |
|---|---|---|---|
| `StockForm.ShingledBar` | `("shingledbar", w 3, t 3, maxW 8, len 18, e 0.846)` | `StockForm.cs:48` | matches the settled bar |
| `StockForm.ShingledSlab` | `("shingledslab", w 8, t 3, maxW 14, len 20, e 0.463)` | `StockForm.cs:60` | matches the settled slab |
| `StockForm.All` | shingledbar, shingledslab - two entries | `StockForm.cs:80` | `bloom`/`slab` survive only as `FormerNames` |
| `stock-shingledbar` mass | 400 u | `StockItemDefinitions.cs:24` | a 2-ball bar - correct |
| `stock-shingledslab` mass | 1200 u | `StockItemDefinitions.cs:25` | a 6-ball slab - correct |
| `stock-shingledbar-30.json` | two 3 × 3 × 8 cubes = 3 × 3 × 16 = 144 vx³ | `mods/iiex/assets/iiex/shapes/forming/` | ⛔ the drawn shingled bar is 162 vx³ - the art still lags the form by 2 voxels of length |
| `stock-shingledslab-30.json` | two 8 × 3 × 10 cubes = 8 × 3 × 20 = 480 vx³ | " | same geometry as the drawn slab |
| iiex smithing recipes | exactly one, and it is pig-breaking | `mods/iiex/tests/goldens/iiex/recipes/smithing/pig.json` | the helve's "one recipe" is not the shingling one |

---

## Why it is like this

### The form needs no UI - for two different reasons

The two machines get there by opposite routes.

The helve cannot choose. It has exactly one smithing recipe, so however many balls are piled under it, a
bar is the only thing it can produce. The constraint is the absence of a second recipe, not a check anyone
wrote. A player who piles six balls on a helve gets a bar and four balls left, which reads as this machine
is too small for that.

The steam hammer has a moment to look. It acts only when the lever is pulled
([steam hammer § Operation](../machines/steam-hammer.md)), and that discreteness is what the helve
lacks: a continuously-running MP hammer has no instant at which "read the anvil" is meaningful, but a
lever-pull is one. The hammer reads the pile as it stands at the first pull, forms the largest form it
affords, and leaves the remainder.

No mode switch, no error state, no dialog. The rule is legible from the metal sitting on the anvil, which
is R7 ("Nothing is hidden", [conventions](../conventions.md)) satisfied by geometry rather than by a
readout.

### Why the helve survives the steam hammer

The bar is the narrow route's input, and the helve makes it at MP tier - before steam exists at all. Every
player passes through the helve, and nothing about owning a steam hammer makes the bar route worse.

What the hammer adds is three times the pile in one blow. That is a bigger piece, not a faster one, and a
bigger piece is what the wide route needs: the wide train's argument is that one 1200 u slab is fewer
handling operations than three 400 u bars carrying the same metal
([steam hammer § Numbers](../machines/steam-hammer.md)). The second machine buys operating efficiency with
build complexity, and does not obsolete the first.

### Why 2 and 6

* 2 is the smallest pile worth welding, and 400 u lands the narrow ladder on numbers that divide
  (400 → 200 plate → 100 → 25).
* 6 is 3 × 2, so the slab is exactly three bars' worth of metal and the two routes cannot mint against
  each other.
* Both divide into a 16-ball heat with only bar-sized remainders - see
  [Against one heat](#against-one-heat). A 4-ball form would also divide, but it would sit between the two
  and give the hammer a third case to read for no new capability.

### Why the hammer never sets the section

Shingling ends the moment the piece is solid. Everything after that - thickness, width, profile - is the
mill's, walked down in gaps. Letting the hammer produce a finished bar or plate would collapse the mill out
of the line entirely. Keeping the hammer to consolidation is what makes the forming line exist.

---

## Gotchas

* The *process* on this page is still not built: no ball item, no shingling smithing recipe, no
  steam-hammer block. What it produces now exists - `shingledbar` at 400 u and `shingledslab` at 1200 u,
  renamed and re-massed 2026-08-12 - so the missing link is the recipe, not the target.
* ~~The stock item's own doc-comment states the contradiction.~~ Closed: the comment said *"a bloom is the
  helve's output from two puddle balls"* while the constant read 180 against 2 × 200. It reads 400 now, and
  the comment derives both masses from geometry.
* ~~The form is still named `bloom`, not `bar`.~~ Renamed 2026-08-12 to `shingledbar` / `shingledslab`,
  across the ten stage shapes, both ladder files, the roll sets' `accepts`, three lang files and two
  goldens. `bloom` and `slab` survive as `StockForm.FormerNames`, which is what carries a piece already in
  a world; the item codes are carried by `StockFormRenameMigration`.
* ⛔ Two different bars are still drawn. `ShingledBar.BaseLength` is 18 and the live `stock-shingledbar-*`
  art is 3 × 3 × 16 - the rename moved the files, not their geometry. The
  [reheat furnace](../machines/reheat-furnace.md)'s beds were authored for a 16-long piece. Both lengths sit
  inside the ≤ 32 lengthwise escape, so this is a fit problem, not a soft-lock
  ([recoverability](../mechanics/recoverability.md)), and wiring the authored art closes it
  ([rolling mill § Open](../machines/rolling-mill.md#open) item 7).
* `item-shingled-bar.json` is not only the shingled stage. It carries the whole narrow ladder as
  sibling top-level elements - `ShingledBar1`, `Grooved275` / `Grooved250` / `Grooved225`,
  `Flattened275` / `Flattened250` / `Flattened225`, `CutRod1..4` and `Beam` - each drawn at very nearly the
  same solid volume (≈ 81 vx³ per half, 162 total), i.e. the rolling model's volume conservation drawn into
  the art. Whoever measures a mass off this file must pick a subtree, which is the unresolved
  convention at [density rule § Open 2](../mechanics/density-rule.md#open).
* A typo is already in the art. `ShingledBar1`'s child is named `SingledBar11` (no `h`). Selective
  element matching drops an unrecognised name without raising anything, so a pruning map written from the
  design rather than from the file silently loses half the bar.
* Both editable shapes are untracked (`??`) and carry absolute `F:/repos/…/.game/1.22/…` texture paths
  for `iron5`. Those are Blockbench working paths; runtime shapes must use domain-relative references, and
  `editable/` is source-only by convention.
* The helve's "one recipe" is not the shingling recipe. iiex ships exactly one smithing recipe and it
  is `iwexpigbreak` (`Recipes/Smithing/PigRecipeDefinitions.cs:20-35`, golden
  `mods/iiex/tests/goldens/iiex/recipes/smithing/pig.json`). The claim in this page's Why
  section is a design constraint that a second recipe must be written to satisfy - and, once written, must
  stay the only one.
* Piling is a hot operation with no stated threshold. The stock item cools using vanilla's
  temperature attribute (`StockItemDefinitions.cs:44-55`) and the mill has a `RollingTempC` gate
  (`IiexConfig.cs:490`), but nothing says how cold a ball may be before it will not weld - and a welded joint
  that did not take is the one failure mode shingling actually has.

---

## Open

| # | Question | Blocking? |
|---|---|---|
| 1 | The wrought ball item. Code, mass, shape, heat, stack size. Everything here starts with it | yes |
| ~~2~~ | ~~`bloom` → `bar` rename, the form at 3 × 3 × 18, and the re-massing~~ | **done 2026-08-12** - the form is `shingledbar`, 18 long, 400 u; the slab `shingledslab`, 1200 u. Only the *art* still lags, at 16 long |
| 3 | The helve's shingling recipe - a second smithing recipe, and the guarantee that it stays the only one | yes |
| 4 | The pile as anvil state. Vanilla's anvil holds one work item; "pile 6 balls" needs either a voxel-accumulation path (the vanilla plate idiom, which the design cites) or a counted stack on the machine. The pig-breaking patch is the closest precedent and it accumulates nothing | yes |
| 5 | Blows per form. Unchosen on both machines; the reference behaviour is a vanilla smithing recipe | |
| 6 | The steam hammer does not exist - see [steam hammer § Open](../machines/steam-hammer.md#open). Until it does, only the bar half of this process is even reachable in principle | yes |
| 7 | Weld heat threshold for piling, and what happens on a cold pile: refuse, or a failed weld that costs a ball? | |
| 8 | Does the helve refuse an over-large pile, or quietly take 2 off the top? The design says the latter (it "cannot make a slab however much you pile"), which means the helve also needs the remainder rule - not only the hammer | |
| 9 | What is a single leftover ball for? One ball has no form. It must remain pileable into the next heat's balls, or 1-ball remainders accumulate as junk | |
