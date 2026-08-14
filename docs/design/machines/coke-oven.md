# Beehive Coke Oven

**Status** designed — the oven itself is not built. There is no core, no block entity, no oven recipe and
no golden for the structure; what exists is the settled design, a draft layout in
[layouts.md](../../internal/workbench/layouts.md) § 1, and the crown lid, which is built
(`iiex:furnace-chargelid-{side}`, 2026-08-03).
**Mod** iiex (`IronIndustryExpanded`)

**Owns** everything about this machine: the two-chamber bank and its draft layout, the crown-charging
decision (`hopper-tall` + the charge lid, not a trapdoor) and the reasoning behind it, the
lid closed = coking rule, the coke-only / no-byproduct scope decision, the reuse of vanilla fireclay
brick and vanilla `game:coke`, the deliberate refusal to use `game:cokeovendoor` (so vanilla's own coking
does not fire inside the chambers), and the list of what has to be built.

**Depends on**
[multiblock & filler structures](../mechanics/multiblock.md) — owns the layout DSL, origin-is-the-negation,
oriented parts and the trapdoor caveat, and the invisible-filler footprint system ·
[definitions, recipes & config](../mechanics/recipes-config.md) — owns code-first defs, goldens, RCC stages ·
[heat balance](../mechanics/heat-balance.md) — owns the furnace core the oven rides ·
[blast furnace](blast-furnace-cold.md), [cupola](cupola.md), [puddling furnace](puddling-furnace.md) — the
consumers of what it makes · [layouts.md](../../internal/workbench/layouts.md) § 1

---

## Role

Coke is the single fuel behind the whole iron tier: it is the fuel half of every shaft charge - blast
furnace and cupola alike, laid in its own courses ([layered-charge](../layered-charge.md)) - and it is what
burns in the [puddling](puddling-furnace.md) and [reheat](reheat-furnace.md) fireboxes. Vanilla already
makes coke, in a 3 × 3 × 3 chamber with a `game:cokeovendoor`, one chamber at a time; a blast furnace on the
shipped calibration eats coke faster than a player can build and cycle enough vanilla chambers.

This machine is the bulk answer, and it is the cheap one on purpose. A historical beehive oven recovered
nothing - it burned its own volatiles - so: coke only, no tar, no gas, no ammonia. By-product recovery is a
later mod's business ([overview.md](../overview.md) § Scope).

It adds no new item and no new material: it produces vanilla `game:coke` and is built out of vanilla
fireclay brick, which vanilla itself already tags as coke-oven material
(`claybricks` carries `cokeOvenViableByType: { "*-fire": true }`,
`.game/1.20/assets/survival/blocktypes/clay/brick.json:9-11`). The player scales a process they already
understand, the same trick the [cupola](cupola.md) plays on the blast furnace.

### Ruled 2026-08-05 — the oven runs vanilla's coking process, scaled

The mechanism is neither the heat balance nor a new simulation. The oven is two sealed chambers, charged
with coal piles and lit without access to air - it runs vanilla's own coking process, scaled up. The
arched ceiling makes it more efficient, and a chamber takes many piles per charge against vanilla's single
block, so the player gets coke faster and in larger quantity.

That needs no new physics: there is no coking temperature and no `MeltingPoint` override - the heat-balance
route would have needed a stated temperature and a ≈0 transfer-loss override, or the oven strands short of
it. It is also not the self-heated, air-regulated beehive where a door aperture trades yield against
burn-off: the chambers are sealed, and "it burned its own volatiles for heat" is history, not the model.
The oven still rides the furnace core for the charge walk, ignition, away-catch-up and the state machine;
the cycle gates on sealed + timer, never on temperature.

---

## Structure

Draft only - [layouts.md](../../internal/workbench/layouts.md) § 1, "Beehive coke oven". Nothing below has been
parsed by `StructureLayout` or pinned by a golden, unlike the shipped structures.

Settled 2026-08-03 - vanilla coal piles are the charge, and the oven is all firebrick:

* Not a reheat hearth. That hearth is a cast-iron bed, and a coke oven never had one. A beehive oven is
  a firebrick structure throughout - floor, walls and crown - and a cast bed would not survive the heat it
  is asked to hold for days.
* The charge is vanilla `game:coalpile`. Coking is already implemented on the pile itself:
  `BlockEntityCoalPile` carries the conversion and manages the quantity change from coal to coke. A charge
  level modelled beside it would re-implement, less well, what the game already does correctly.
* The cycle is conventional: charge (the tall hopper places real coal-pile blocks into the chamber; the
  player may also walk in and stack them by hand) → seal (charge door and crown lid closed) → fire → wait
  (the piles convert to coke at the pile's own rate and its own quantity loss). The lid exists for exactly
  this: the hopper is not a sealed block, and coal must coke without air.
* Caution: this keeps a coal-pile dependency the rest of the suite is shedding. Everywhere else the vanilla
  pile is going - the two hearths take the [firebox](firebox.md) block, the boilers take internal fireboxes,
  the [cowper](cowper.md) loses its pile in a remake. The oven's piles are plain coal.
* The oven fits no `CellRole`. Its `c` cells are neither `Firebox` (fuel heating something else) nor
  `Chargeable` (a burden column) - the coal is the workpiece. Either the oven needs no role and walks its
  own chamber box, or the enum gains a `Retort`. Undecided; see [layouts.md](../../internal/workbench/layouts.md).

A bank of two chambers sharing a wall. Each chamber is charged from above through the crown and drawn
from the side through a door - the historically correct pair of openings, and mechanically the reason the
lid state has something to say.

**Footprint** 9 wide (X) × 4 deep (Z) × 4 tall (Y); 128 declared cells of a 144-cell box (16 `'.'`).
`Origin(-4, -2)` - the negation of `C`'s `(col 4, row 2)` on layer 0.

| Glyph | Block | Count | Notes |
|---|---|---|---|
| `#` | `game:claybricks-good-fire` | 89 | vanilla's own coke-oven brick |
| `-` | `game:brickslabs-fire-up-free` | 8 | crown springing; vertical, never rotates |
| `i` | `game:brickslabs-fire-south-free` | 4 | door shoulders; orientation-checked |
| `C` | `iiex:beehiveovencore-*` | 1 | does not exist |
| `T` | `iiex:hopper-tall*` | 2 | one per chamber, in the crown |
| `L` | `iiex:furnace-chargelid-{side}` | 2 | built - the crown lid |
| `D` | `iiex:chargedoor-{side}` | 2 | the drawing doors; the block exists |
| `f` | `exlib:structurefiller` | 4 | the hoppers' and doors' upper halves |
| `a` | `game:air` | 4 | chamber crown void |
| `c` | `@(air\|coalpile)` | 12 | the two chambers: 3 wide × 2 deep × 1 tall, each |
| `K` | *(dead legend - never used in any grid)* | 0 | see [Gotchas](#gotchas) |

**Cells that matter** (anchor frame, `C` = `(0,0,0)`; the two chambers mirror about X = 0):

| Cell(s) | What it is |
|---|---|
| `(0, 0, 0)` | the core, sitting inside the shared wall at the base |
| `(-3…-1, 1, -1…0)` and `(1…3, 1, -1…0)` | the two 6-cell coking chambers |
| `(±2, 1, -2)` | the crown hopper base; `(±2, 2, -2)` its filler; `(±2, 3, -2)` the lid |
| `(±2, 1, 1)` | the drawing door; `(±2, 2, 1)` its filler |
| `(0, 0…3, *)` | the shared wall, all brick |

**Filler accounting checks out** - 4 declared, 4 produced (each `hopper-tall` places one above itself, and
each `chargedoor` likewise; the lid emits no filler). This is not true of the
[puddling](puddling-furnace.md) or [reheat](reheat-furnace.md) layouts, both of which declare filler cells
no part can produce - worth checking again once this layout is real.

### Why `hopper-tall` + a lid, and not a trapdoor *(decided 2026-07-28)*

A vanilla trapdoor is the one block the multiblock orientation check cannot verify. Its variantgroups
are `style` / `material` / `age` - no facing in the code at all - and it carries
`entityBehaviors: [{ name: "TrapDoor" }]`, so the hinge lives in the block entity
(`.game/1.20/assets/survival/blocktypes/metal/trapdoor.json:11-22`). A layout can only require
`trapdoor-plate-iron-1`, and the player may hinge it any way with the structure still completing. That is
the blind spot of the oriented-parts feature ([multiblock](../mechanics/multiblock.md)).

`hopper-tall` is already the charging block of the [blast furnace](blast-furnace-cold.md) and the
[cupola](cupola.md), so the player has learned it before they build an oven; it also carries
`MultiblockStructure`, so the build outline can be previewed from the hopper. A beehive oven is charged
from above through the crown, which is what the hopper does. A trapdoor is only a lid.

Coking is destructive distillation and the hopper is not a sealed block. Coal must be heated without access
to air, or it burns instead of coking. The hopper's tank is open storage with an open top, so the crown
needs something above it that actually closes. That block is built: `iiex:furnace-chargelid-{side}`, a third
def on the charge-door chassis. (`chargelid` won over `ovenlid` because the part is not oven-specific - the
draft crucible furnace wants it too - and the `furnace-` prefix is the shared furnace-part code.) Its facing
lives in the block code, so `Legend` auto-detects the cardinal segment, the layout rotates it with the
structure, and the orientation check verifies it - every property the trapdoor lacks. A hopper with a hinge
direction still would not seal, so folding the lid into the hopper stays wrong.

### The lid as shipped

Editable source `assets/editable/shapes/furnace-block-chargelid.json`, runtime
`assets/iiex/shapes/furnace/chargelid.json`. Two top-level elements, `Masonry` and `Lid`, which is what
`SelectiveElements` needs and what lets the hinge animate without dragging the brickwork.
`rotationOrigin [2,3,2]` runs through `Cube8`, the burned-clay lip - the lid's leading edge being buried in
that lip is the hinge barrel, not an overlap bug. At the `open` pose's −67.5° the far edge stays inside its
own block. The frame is asymmetric in z (short front rail, taller back rail with an under-lip), which is
what gives the block a real facing for the orientation check to verify.

Two properties the shipped def holds and any re-export must preserve:

1. **`onAnimationEnd: Repeat`** on both clips, forced by `convert-shape.py` - a held clip is how this
   project has previously made a mesh vanish.
2. **No filler.** `Door()` does not apply the two-cell footprint itself: the upper-half filler is a
   named `UpperHalf` member the two doors opt into per-def, so the lid inherits everything else and emits
   no filler - otherwise it would place an invisible solid block on top of the finished oven and unbalance
   the filler accounting. The lid takes a 4/16 collision and selection box, since it is that tall shut.

Minor: the `Lid` bottom face and `Cube8`'s bottom face are coplanar at y=2 over x 2–14, z 2–3, so
z-fighting is possible seen from below. A 0.01 nudge if it shows.

---

## Assets

| Need | State |
|---|---|
| the crown lid | built - see above; golden `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/chargelid.json` |
| the core's block | reuse - a plain cube with a type-label overlay, like every other furnace core (`BlockFurnaceCoreBase`) |
| the drawing door | reuse `iiex:chargedoor` verbatim, shape and all |
| the crown hopper | reuse `iiex:hopper-tall` verbatim |
| the coke | vanilla `game:coke` - no new item |
| the brick | vanilla `game:claybricks-good-fire` |
| oven core shape / lang / handbook | missing - nothing drawn or written for the oven itself |

---

## Construction

No oven recipe, because there is no oven block. The only two `iiex` blocks in the whole layout the player
crafts new are the core and the lid - and the lid has no recipe yet either; the hoppers and doors have
their own.

The oven should be cheap: it is the earliest bulk machine in the tier and it gates everything after it, so
it must not be a second blast furnace to build. Vanilla fireclay brick and a hopper each is the right order
of cost. Recipe-cost placement is [recipes & config](../mechanics/recipes-config.md)'s.

---

## Operation

*(All proposed. Nothing below is implemented.)*

| Verb | Where | Effect |
|---|---|---|
| RMB | crown lid | open / shut |
| RMB with coal | crown hopper | charge the chamber below |
| RMB | drawing door | open / shut |
| RMB empty-handed | drawing door / chamber | draw the finished coke |
| Ctrl + Shift + RMB | core, hopper, door, lid | build outline + shopping list |

Coal in → `game:coke` out, per chamber, on a timer.

Lid closed = coking; lid open = charging or drawing. The volatiles burn inside with no air, which is what
makes coke instead of ash, so an oven left open does not coke. That is the reason the oven needs to read
`IsVenting` (`BlockEntityChargeDoor`) at all. Today `IsVenting` has no consumers anywhere in the tree; this
machine would be its first.

Two chambers means the player alternates: one coking sealed while the other is drawn and recharged. The
sharing wall is thermal as well as structural, which is why beehive ovens were built in banks.

### The crown hopper can only auto-charge one cell of six

`BlockEntityHopperTall`'s drip never seeds above its own Y. In the draft the hopper's base cell sits at
Y = 1 - the same level as the chamber floor - with layer-0 brick underneath, so the only cell it can seed
is the chamber cell directly beside it, `(±2, 1, -1)`; once that pile is full the next cell up is Y = 2,
which the seed check rejects. The other five cells of each chamber would have to be hand-placed.

Three ways out, all live: raise the hopper a layer and drop the chamber floor; give the oven core its own
charge-spreading walk (it owns the chamber volume anyway - and on the furnaces the charge walk is already
the core's job, not the hopper's); or accept hand-charging and treat the hopper as a lid-with-a-tank. This
has to be settled before the layout is committed to C#.

---

## Numbers

Everything here is proposed. Nothing is in code, so nothing has a `file:line` in `src/`; the citations
are to the draft and to the vanilla assets it reuses.

| Key | Proposed value | Source | What it does |
|---|---|---|---|
| chambers | 2 | [layouts.md](../../internal/workbench/layouts.md) § 1 | a bank sharing one wall |
| cells per chamber | 6 (3 wide × 2 deep × 1 tall) | draft layer 1, `c` glyph | vs vanilla's 3 × 3 × 3 single chamber |
| footprint | 9 × 4 × 4, 128 declared cells | draft | 89 brick, 12 fuel, 12 slab, 4 filler, 4 air, 2 hopper, 2 lid, 2 door, 1 core |
| origin | `(-4, -2)` | draft | negation of `C`'s `(col 4, row 2)` |
| output item | `game:coke` | vanilla | no new item |
| wall material | `game:claybricks-good-fire` | vanilla | vanilla's own coke-oven-viable brick |
| coke yield per coal | *(undecided)* | - | must at least match vanilla's, or the bulk oven is a downgrade |
| cycle time | *(undecided)* | - | should reward the build without trivialising fuel |
| lid gate | coking only while shut | design, above | reads `BlockEntityChargeDoor.IsVenting` |

The yield and the cycle time decide whether this machine is worth building. Neither has been chosen, and
both have to be set against vanilla's own coke oven rather than in isolation - the bulk oven must be a
better rate, not merely a bigger box.

---

## Drops

*(Proposed.)* Each block returns itself; walls are vanilla brick broken individually. Chamber contents
must drop - a chamber holds up to twelve coal piles, and silent destruction of contents is the mistake the
hearths made and this machine must not repeat. Coal piles are ordinary world blocks in the layout
(`@(air|coalpile)`), so this is nearly free: they survive the core being broken on their own.

---

## Code

The lid is a third `Door()` entry in `BlockChargeDoor.Definitions`, with its own clip attributes and no
filler. What remains to be written:

| Thing | Shape of it |
|---|---|
| `BlockBeehiveOvenCore : BlockFurnaceCoreBase` | def + layout, exactly like the puddling core: `Core(domain, …)` |
| `BlockEntityBeehiveOven` | rides the furnace core for the charge walk, ignition, away-catch-up and state machine; the cycle itself is sealed + timer (§ Role) |
| a grid recipe | a further group in `FurnaceRecipeDefinitions` |
| goldens | `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/beehive-core.json` |
| a layout test | the furnace-parts test already asserts every layout code resolves to a real block; adding this structure extends it for free |

---

## Gotchas

*(Traps waiting in the draft, found by reading it against the code.)*

* **`K` is a dead legend.** The draft declares `.Legend('K', "game:trapdoor-plate-iron-1")` but no grid in
  any of the four layers uses `K`. Delete it with the decision it belongs to.
* **`iiex:hopper-tall-south*` matches nothing.** The tall hopper's `side` variant uses single letters
  (`hopper-tall-n` …), so the draft's spelled-out `-south*` suffix resolves to no block - which fails
  `BlockNumber` validation at load. Use the letter form, as the furnace layouts do.
* **The draft's lid legend is misspelled.** The built block is `iiex:furnace-chargelid-{side}`; the draft
  still writes `iiex:chargelid-south*` - wrong prefix and wrong side form.
* **Vanilla's coking must not fire inside these chambers, and the draft is already right about why.** The
  doors are `iiex:chargedoor`, not `game:cokeovendoor`, so vanilla's own coke-oven detection never sees a
  sealed chamber. That is deliberate - the core owns the bulk cycle - but it means the walls being
  `cokeOvenViable` buys nothing mechanically, only thematically. Swap to `game:cokeovendoor` only if
  the intent ever becomes "let vanilla run it".
* **`game:claybricks-fire*` matches nothing.** Vanilla's variant order is `{state}-{type}`, so the real
  code is `claybricks-good-fire`. Already fixed in the draft; do not "correct" it back.

---

## Open

| # | Decision / gap | Size |
|---|---|---|
| 1 | **Coke yield per coal and cycle time.** Undecided, and they are what make the machine worth building | small |
| 2 | **The crown hopper only reaches one of six chamber cells** (above). Raise the hopper, lower the floor, or let the core spread the charge | medium |
| 3 | **`iiex:beehiveovencore`** - block, block entity, layout in C#, goldens, recipe | medium |
| 4 | **The lid has no recipe** | small |
| 5 | The draft's dead/wrong legends must be cleaned before it is committed - the lid legend spelling, `K`, and `hopper-tall-south*` | small |
| 6 | Does the second chamber get its own state, or do both chambers share one cycle? Two states is the honest model and the reason for a bank; one is far less code | small |
| 7 | Should the oven light itself from a charged, sealed chamber, or need an ignition source? Vanilla needs a fire. Decided 2026-08-05: no bank bonus attaches to this - banks are throughput-only ([plant-layout](../mechanics/plant-layout.md)) - so it is a plain question about this oven's ignition | small |
