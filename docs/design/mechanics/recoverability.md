# The Recoverability Invariant
**Status** designed - the lengthwise half is live, the crosswise half is unbuilt, and nothing enforces a
length anywhere in code today   **Mod** iiex (`IronIndustryExpanded`)
**Owns** the invariant itself and its exactly two escapes (≤ 32 lengthwise, ≤ 48 crosswise), their slot counts and access ordering, the definition of a soft-lock, why cold shear is not a third escape, the two known soft-locks and their crop points, and the distinction between the mill's 48-voxel refusal and the 32-voxel seating-mode switch.
**Depends on** [density rule](density-rule.md) (every length here is a drawn shape's voxel count) · [rolling mill](../machines/rolling-mill.md) (the pass model that grows a piece past the limit, and the refusal that must enforce this) · [multiblock & filler structures](multiblock.md) (the hearth's 3 × 2 filler footprint, from which both limits derive) · [stock](../items/stock.md) · [rolling](../processes/rolling.md) (the stock ladder and its crop table)

## Role

Rolling conserves volume. Every reduction that does not go into width goes into length, so a piece of
stock gets longer every pass, and a long enough piece stops fitting in the only thing that can put heat
back into it.

A soft-lock is the state that follows: the piece is too cold to roll (the mill needs friction to bite, and
cold stock barely does), too long to reheat, and - because cutting it is gated on things a player may not
have built - possibly too cold to cut. The metal is stranded with no legal action, and nothing errors.

The invariant is the rule that makes that state unreachable:

> Every stage a piece can be **left** in must be reheatable.

Crops exist to satisfy it and for no other reason. The crop points in the ladder are therefore derived,
not chosen: each one is the last legal stage before a schedule would fall out of both escapes.

## How it works

### The two escapes

The handling limit is not one number. It is the reheat hearth's 3 × 2 footprint read in two directions:

| Escape | Runs | Limit | Slots | Access |
|---|---|---|---|---|
| lengthwise | along the 2-cell depth | ≤ 32 voxels | 3 - left / centre / right | per-row; click the intended row |
| crosswise | across the 3-cell width | ≤ 48 voxels | 2 - far, then near | one LIFO stack through any top filler |

A stage that satisfies neither is a soft-lock.

Long stock costs slot count: three pieces at ≤ 32 or two at ≤ 48.

The mode is derived, never stored. Over 32 forces crosswise; at or under 32 goes lengthwise if a
lengthwise slot is free. The two are mutually exclusive: a crosswise piece physically occupies all three
lengthwise slots, so the block entity needs one contents model with a mode tag, not three independent rows.

The ordering needs no rule and no UI. A hot piece cannot be reached past, so a crosswise hearth is filled
far row first and emptied near row first - LIFO falls out of reachability. That is the same argument
the live lengthwise hearth already makes with its centre row (`HearthRows.cs:65-76`): a loaded centre blocks
both flanks, on the way out as well as in (`BlockEntityHeatingHearth.cs:77-80`).

### The geometry is not a coincidence

Both numbers fall out of the same 3 × 2 hearth, and both are hit on the nose by unrelated parts of the
design:

| Number | What lands exactly on it |
|---|---|
| 32 | the shingled bar's last legal grooved stage (2.25 gap → 32.0 long) |
| 32 | the shingled slab's last wide stage at `MaxWidth = 15` (480 / 15 = 32.0) |
| 48 | the longest billet a sand long cell can cast (3 × 3 × 27), at its 2.25 stage → 48.00 |
| 48 | the stock rack's 1 × 1 × 3 length - 3 cells = 48 voxels, so a rack holds anything that can legally exist |

### Why cold shear is not a third escape

1. It is tier-agnostic but capability-gated. Cold shearing was moved (2026-07-29) from an LP/HP hammer
   gate to a `MinTorque` gate against what the mp-energy network can actually deliver, so whether a given
   player can cut a given piece depends on how much drive they have built.
2. An invariant cannot depend on the build. It must hold for a player who has just placed their first
   mill, on the first piece they roll.
3. It was verified unnecessary. Every mandatory crop in the ladder already lands inside 48, so the base
   game is safe on two escapes alone and cold shear is pure convenience on top.

Corollary: `ColdShearMaxThickness` is deleted from the design. It is not a config key, was never one,
and does not appear anywhere in `src/`.

Shearing is temperature-free because what limits it is force, not friction. `RollingPass.CanBite`
gates rolling on temperature because rolling needs friction to drag stock between the rolls; a shear needs
none, and cold stock parts more cleanly than hot, which smears.

### 48 is the mill's refusal; 32 is only a mode switch

- The mill must refuse a pass whose output would exceed 48. 48 is the hard limit - the longest piece
  that can legally exist.
- 32 is not a limit. It is the boundary at which a piece stops seating lengthwise and starts seating
  crosswise. A 40-long piece is legal; it just costs two slots instead of three.

The wrought bar shows why 32 cannot be the limit: its 2.0 stage is 40.5 long - past 32, inside 48 - so
a bar that started lengthwise in three slots must be re-laid crosswise for its last stage. The very first
piece of stock in the game crosses the boundary, so the player learns both modes on the bar and already
knows them when the billet arrives.

Power never relaxes the 48 refusal. A bigger plant unlocks cutting a crop cold; it never unlocks rolling
into a longer state.

## Numbers

Every value on this page is a design constant, not a config key. None of `32`, `48`, `2.25` or `2.0`
exists anywhere in `src/` - a grep for `48` across `mods/iiex/src/BlockStructures/Forming/`
returns only unrelated `Array.Length` uses. The only number that is real in code is the hearth's slot count.

| Key | Value | file:line | What it does |
|---|---|---|---|
| lengthwise limit | 32 voxels | design only | longest piece that seats along the hearth's 2-cell depth |
| lengthwise slots | 3 | live as `HeatingHearthLayout.Rows` | `HeatingHearthLayout.cs:31` - the one real constant |
| crosswise limit | 48 voxels | design only | longest piece that exists at all |
| crosswise slots | 2 | design only | far, then near; LIFO |
| hearth footprint | 3 wide × 2 deep | `BlockHeatingHearth.cs:43-54` | `###` / `#0#` filler layout; both numbers derive from it |
| mill length refusal | 48 | not implemented | see Open |
| `ColdShearMaxThickness` | deleted | does not exist in `src/` | was never a config key |
| cold-cut gate | `RollSetSpec.MinTorque` | `RollSetSpec.cs:30`, `:37` | parsed, stored and never consulted |

### The two soft-locks and their crop points

Walking the five stock forms against the invariant finds exactly two stages that fall through both
escapes. Lengths are volume-conservation arithmetic at the settled `MaxWidth` 15:

| Form | The soft-lock stage | Crop at | Length there | Into | Each piece then |
|---|---|---|---|---|---|
| `castbillet` 3 × 3 × 27 | grooved 2.0: 60.75 long × 2.0 thick | 2.25 - the half-step, not a gap | 48.00 - the crosswise limit, on the nose | 6 | takes the 2.0 pass alone and lands at 10 long = one `rolledrod` |
| `castslab` 12 × 4 × 25 | wide 1.5: 53.3 long × 1.5 thick (1200 / (15 × 1.5)) | 2.0 - the last legal point | 40.0 (1200 / (15 × 2.0)) | 5 | 600 u each, finishing at 15 × 1 × 16 = one `boilerplate` |

The billet must crop mid-gap. Carried to the 2.0 gap it would be 60.75 long - past the hearth in both
seatings. So it stops a round early at the 2.25 half-step, where it is 48.00 exactly. That makes the
half-step a legal place to leave a piece rather than merely the art between two gaps, so the shear must
accept half-steps, and `Outputs` must key on stage, not on gap.

The cast tier is on the crosswise seating from its first pass. A 27-long billet is already 32.1 at its
very first half-step, so cast stock lies across the hearth while puddled bar lies along it. Three slots
or two: the handling difference is the tier difference, and it comes from the furnace's own footprint.

The other three forms are clean: `shingledbar` tops out at 40.5 (legal crosswise); `shingledslab` tops out
at exactly 32.0 - `480 / 15` at the settled `MaxWidth` 15, so the wrought slab's true maximum sits on the
lengthwise limit to the voxel, its last wide stage being the longest piece that still seats lengthwise.
`castbloom` - see Gotchas.

### A rescue that looks reasonable and is not

Casting the billet at 36 instead of 27 and shearing it into two 18s does not work: 36 does not fit the
hearth in either seating, and at 3 thick a cold cut needs a torque the early network cannot reach. A billet
left to cool at 36 would be both unreheatable and unshearable - the case the invariant exists to forbid.

## Code

Only the lengthwise half exists today, and it does not measure anything.

| Type | Where | Role |
|---|---|---|
| `HearthRows` | `mods/iiex/src/BlockStructures/Furnaces/HearthRows.cs:17` | the `Left`/`Centre`/`Right` enum (`:20-25`), `CanReach` (`:75`), `Reachable` (`:82`), `FromLocalOffset` (`:54`) |
| `HeatingHearthLayout` | `.../HeatingHearthLayout.cs:18` | `Rows = 3` (`:31`), `StockOf` (`:64`), `Element` (`:57`), `ElementsFor` (`:85`) |
| `BlockEntityHeatingHearth` | `.../BlockEntities/BlockEntityHeatingHearth.cs:26` | one `ItemStack?` per row (`:30`), `TryLoad` (`:57`), `TryTake` (`:73`) |
| `BlockHeatingHearth` | `.../Blocks/BlockHeatingHearth.cs:20` | the 3 × 2 footprint (`:43-54`), `RowAt` rotates a clicked cell back into the hearth's frame (`:73-78`) |
| `BlockHeatingFurnaceCore` | `.../Blocks/BlockHeatingFurnaceCore.cs:20` | places `H` + its five fillers in layer 0 (`:58-66`); the slab shoulders round the door exist so all three rows are reachable at all (`:46-50`) |
| `WorkPiece` | `.../Forming/WorkPiece.cs:35` | carries `Strips`/`Turned`; has no `Length` and no `Mass` - `StripLength(t)` (`:115`) is a per-strip figure nothing compares to a limit |
| `StockForm` | `.../Forming/StockForm.cs:28` | only two forms exist: `Bloom` 3 × 3 × 16 (`:48`) and `Slab` 8 × 3 × 20 (`:54`) |

What actually gates loading today: `TryLoad` refuses on three conditions only - the row is unreachable
past a loaded centre, the row is occupied, or the item's code path is not recognised stock
(`BlockEntityHeatingHearth.cs:57-69`). Recognition is a code-prefix match - `stock-shingledbar`, `stock-shingledslab`,
`castbillet`, `castbloom`, `castslab` (`HeatingHearthLayout.cs:64-79`). No length is read, because no
stock item carries one.

Where a caller would hook in. The invariant needs three insertions, none of which exist:

1. `WorkPiece.Length` (and `.Mass`);
2. a refusal in the mill's pass path when the resulting length would exceed 48 - the same place
   `RollingPass.CanBite` already rejects a pass;
3. a seating-mode decision in the hearth's load path, replacing the fixed three-row model.

## Gotchas

The invariant currently has a hole: `castbloom` at the 1.0 gap. It reaches 50 long there, and
50 > 48 - under two escapes only, that stage is a third soft-lock. The excuse that "1.0 is cold-shearable,
so it needs no crop at all" leans on cold shear, which this page rules out as an escape. It needs either a
mandatory crop (its plate route already crops into 5 at the 3.0 gap) or a demonstration that the uncropped
1.0 stage is unreachable. This is the highest-value thing on this page.

`HearthRows` as a fixed enum cannot express crosswise seating, and it is shared. A crosswise piece
occupies all three lengthwise cells, so the two modes cannot both be modelled as three independent rows.
`BlockEntityPuddlingHearth` sizes its own arrays off `HeatingHearthLayout.Rows`
(`BlockEntityPuddlingHearth.cs:30-31`) - changing the reheat hearth's contents model silently touches the
puddling furnace.

In crosswise mode the hearth is one block. Every interaction routes through any top filler and the
piece spans three cells, so it must be drawn on the principal's mesh. Verify in game that the outer
cells are not culled - the one part of this that cannot be settled headlessly.

The reheat cycle is not built. The hearth holds and shows stock; nothing puts heat into it. The class
comment says so plainly (`BlockEntityHeatingHearth.cs:20-23`). Today a piece cools on the bed, the
invariant protects nothing, and nothing tests it.

The hearth art does not agree with the seating limits. `heatinghearth.json` draws its stock at
3 × 3 × 16, one cell deep, so its beds hold a 16-long piece while the shingled bar is now 18. The fix is
to compose the pile from the item's own shape rather than authoring 15 element groups.

`Items1 / Items2 / Items3` are not in positional order. `Items1` = left (x0), `Items3` = centre
(x16), `Items2` = right (x32) - the group pivot decides where each draws, and the children are
byte-identical. Reading "Items2 = middle" off the name puts every loaded piece one cell out, silently:
selective-element matching drops an unknown name without an exception, and a wrongly-known name just draws
in the wrong place (`HearthRows.cs:30-40`, `HeatingHearthLayout.cs:44-48`). Pinned by a test against the
shipped shape.

Both rows of a column are the same row. `RowAt` collapses the far cell onto the near one
(`BlockHeatingHearth.cs:71-78`, `HearthRows.cs:54-63`): the second cell is depth for a slab, not a fourth
place to put something. When crosswise seating lands, that mapping has to change, not be extended.

`MinTorque` is the cold-cut gate and is currently dead code. It is parsed and stored on `RollSetSpec`
(`:30`, `:37`) and never read. The planned shear gate makes it live, so the first thing that ever consults
it is the mechanism that replaced cold shear as an escape.

A stock rack is not an escape. The 1 × 1 × 3 rack holds anything ≤ 48, but it stores, it does not
reheat. A piece that cannot be reheated is stranded whether or not there is somewhere to put it down.

## Open

- Nothing enforces the invariant. No length refusal in the mill, no length check in the hearth, no
  `WorkPiece.Length` at all. Every guarantee on this page is currently a document.
- The proposed headless test does not exist - walk every `(form, set)` schedule and assert no stage
  falls through both escapes. It is the right test: it would have caught the `castbloom` hole above
  mechanically. It cannot be written until `StockForm` carries the five real forms (today it has two, at the
  wrong dimensions - `StockForm.cs:48`, `:54`) and roll sets carry the settled gaps.
- Crosswise seating is unbuilt. `HearthPile.Place` covering both seatings - crosswise is a different
  `rowAnchor` plus a 90° base yaw - and the placement code is slated to move to exlib as `StockPile.Place`
  so the stock rack shares it.
- A deeper reheat furnace would raise both limits at once, which is a better upgrade reward than a
  throughput multiplier - but it also means 32 and 48 must be derived from the built footprint, not
  hard-coded, when they are finally implemented.
- The invariant is stated only for iiex's five forms. iiex's wide line and smex's cast tier add stock
  and roll sets; nothing yet says the invariant is re-checked when a downstream mod adds a form.
