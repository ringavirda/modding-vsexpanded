# The machining line — four machine tools, one job convention

**Status** settled 2026-08-11 in design, nothing built. Nine machine shapes are drawn under
`workbench/shapes/machines/mpenergy/`; six sand-cast blanks and four structural blanks are
drawn under `workbench/shapes/items/sandcast/`. No block, no block entity, no item, no recipe,
no def, no lang key and no test exists for any of it.
**Mods** iiex owns the blanks and the bootstrap gear · the machines' placement follows the content
they feed, so the shaper is the earliest and the planer the latest

## Owns

* the roster — which machine performs which verb, and why four rather than one;
* the progression order and the **bootstrap loop**, which is the reason the roster has an order at all;
* the tooling split — one universal cutter across four machines, a separate shear blade, and what
  stays profile tooling;
* the **machining job convention**: one schema covering terminal and sequence machines, and the
  extension seam a third-party mod uses;
* the render contract — selective elements for our own content, an anchor for everyone else's;
* the rulings this page settles, each with the reason, so they are not re-litigated.

## Does not own — cited only, never restated

| Fact | Owner |
|---|---|
| Model A, the diagram as an ingredient, the consumed-vs-reusable rule, the station-window plan | [diagram crafting](diagram-crafting.md) |
| the mill's reduction model, `RollSetSpec`, `WorkPiece`, `StockForm`, every `Rolling*` key | [rolling mill](../machines/rolling-mill.md) |
| bending as a verb, curvature-up-in-passes, cold forming, the four routes | [bending](../processes/bending.md), [bending roller](../machines/bending-roller.md) |
| the cast/fabricated pair rule and the substitution loop | [fabrication](../processes/fabrication.md) |
| the `ItemDie` contract and the fastener benches | [heading machine](../machines/heading-machine.md) |
| `1 vx³ = 2.5 u` and every mass | [density rule](density-rule.md) |
| `ExBlockDef` / `ExItemDef` / `ExRecipeDef`, the RCC stage builder, goldens, the cost catalogue | [recipes & config](recipes-config.md) |
| the `"mpenergy"` run, the four node contracts, torque and speed | [mp-energy](mp-energy.md) |
| filler footprints, behaviour-capable cells, membership | [multiblock](multiblock.md), [framework composition](framework-composition.md) |

---

## Role

The finishing half of the forming line. Everything upstream shapes metal in bulk — cast rough, reduce,
crop, blank. These machines cut it to a dimension.

Four verbs, four machines. The split is not decoration: turning, boring, shaping and planing are
distinct operations on distinct geometry, and they were the four machine tools that built the
19th century.

| Machine | Verb | Geometry it owns |
|---|---|---|
| **shaper** | shaping | small flats and **teeth** |
| **lathe** | turning | anything round, worked from outside |
| **horizontal bore** | boring | internal cylindrical surfaces — things that must *fit* |
| **planer** | planing | large flats, worked from solid — and facing to thickness |
| **drill press** | drilling | through-holes, one setup, many holes |

Beside them, already designed elsewhere: the rolling mill (reduce), the shear (crop), the bending
roller (curve), the nail and heading benches (fasteners), the riveter.

**Drilling and boring are different verbs.** A bore is one hole, finished to a tolerance so something
fits it. A drill makes many holes and cares about their pattern, not their finish. The gear chain
below is what forces the distinction: a gear web is drilled out, not bored.

> Drilling *rivet holes* remains abstracted — `fabrication.md` folds those into the bill, and a step
> that only adds a property to a plate would be a decision-free tax. The drill press exists for the
> case where the holes **are** the part.

---

## The bootstrap loop, and how it is broken

Every machine except the shaper takes pinion or spur gears, and most also take shafts. Shafts are
turned on the lathe; the lathe needs gears and shafts. **That closes, and a player who cannot already
build mpenergy machines cannot build the machine that makes their parts.**

⛔ It closes harder than it first looks: a **spur** gear needs planer, drill press and shaper, and all
three of those need gears themselves. So the hand route is not a convenience — it is the only way the
line starts at all, and it must be able to supply every gear the first three machines cost.

Two things open it:

1. **A hand route for gears that is deliberately wasteful.** Cast iron rods + a wheel part + nails,
   assembled at cost, eating chisel and hammer durability. It is not a cheaper gear; it is a gear you
   can make with no machines at all. The blank-plus-shaper route is the efficient one and stays the
   reason to build the shaper.
2. **The shaper accepts both drives.** It is the one machine a player builds while still on a vanilla
   axle, so it must run off vanilla MP — and it must also join `mpenergy` later, or it becomes dead
   weight on a finished line. [framework composition](framework-composition.md)'s membership
   behaviour makes carrying both cheap: one footprint cell holds the `mpenergy` membership, another
   holds `exlib.BEBehaviorMPFillerPort`.

⛔ The existing bridge is **one-way** — vanilla MP into `mpenergy`, through the flywheel. No machine
has ever been offered both, so the shaper sets the precedent for which drive wins when both are live.

⛔ Every gear-taking recipe in the suite is currently authored **twice**, because `game:gear-rusty` is
loot-only. A third route must not become a third copy of each recipe.

This is the established shape, not a new mechanic: `iiex:spurgear` already ships two routes.

---

## Order, and what each machine is for

The order follows the loop above: the shaper unlocks gears, gears unlock the lathe, the lathe's rolls
unlock the mill's tooling, and the planer arrives with steel.

### 1. Shaper

Cuts teeth and small flats. **It closes a live hole**: `iiex:bevelgear` has no recipe at all today,
so a survival run cannot turn a corner or change height without creative or salvage.

| In | Out |
|---|---|
| small gear blank | pinion |
| planed-and-drilled large blank | spur gear |
| lathe-turned conical blank | bevel gear |
| turned shaft | keyed shaft — keyways and splines |
| — | racks, sprockets |

### Each gear type costs a different number of machines

This is the line's spine, and it is what makes the machines feel earned.

| Gear | Chain | Machines |
|---|---|---|
| **pinion** | small blank → shape | 1 |
| **bevel** | large blank → turn to a cone → shape | 2 |
| **spur** | large blank → face to thickness → drill the web → shape | 3 |

A bevel is two operations because that is how bevels are actually made, and it prices them as the
premium part they are — which suits their being what lets a run turn a corner. A spur is three
because a large gear is faced, lightened and only then cut; the drilled web is the visible difference
between a cast gear and a machined one.

**A pinion is shaper-only** (settled). A small blank has no web to lighten and needs no facing, so the
cheapest gear stays a one-machine part. That is what keeps the shaper worth building first and what
bounds the bootstrap grind: the hand route only has to carry the player as far as one machine, not
three.

### 2. Lathe

Anything round. Its `rollerslathe` clip is the intended route for the mill's roll blanks, and **no
recipe for any roll set exists anywhere today**, so the lathe is the mill's missing supplier.

| In | Out |
|---|---|
| roller blank | flat, grooved and the four wide roll sets |
| shaft blank | turned shaft stock |
| cylinder blank | light cylinder |
| heavy cylinder blank | turned heavy cylinder — the bore's input |
| large gear blank | conical blank for the bevel |
| — | pistons, valve spindles, pump plungers |

### 3. Horizontal bore

Internal cylindrical surfaces. Its identity is **matched pairs**: turn the shaft, bore what it runs
in; turn the piston, bore the cylinder it seals against. Neither machine is a dead end.

| In | Out |
|---|---|
| turned heavy cylinder | bored cylinder — engines, pumps, the steam hammer |
| — | valve bodies, pump barrels, bearing housings |

⛔ **It does not bore pipes.** Per-tier throughput ships and is enforced from `Code.Domain`, and three
separate config docstrings say verbatim "never from bore". The manufacturing distinction is already
spent on the joint — flanged versus welded — which is why a rolled run is an island. Fixed pipe
demand suite-wide is about twenty grid recipes at one or two segments. A bore axis would re-encode
information the design already carries.

### 4. Planer

Large flats, worked from solid — and **the waste is the point.**

Rolling conserves volume; planing removes it as scale for remelting. Same part, two routes: one cheap
in machines and expensive in metal, the other the reverse. That is the cast-versus-fabricate logic one
level down, and it gives scale a reason to exist.

| In | Out |
|---|---|
| large gear blank | blank faced to thickness — the spur gear's first operation |
| 3-thick plate (mill, off a 4-thick slab) | frame |
| heavy plate (2-thick) | bedplate |
| cast slab | plate — slow, most of the metal becomes scale |
| any cast part | the same part, planed to precision |

Facing a gear blank to thickness gives the planer an **early** job as well as a late one, so it is not
purely a steel-tier machine. Facing a casting before machining it is also the historically correct
first operation.

### 5. Drill press

Through-holes, many per setup. Its job is the one boring cannot do: a pattern of holes rather than
one finished surface.

| In | Out |
|---|---|
| faced large gear blank | drilled blank — the spur gear's second operation |
| — | any part where the holes *are* the part |

⛔ **The planer is not an assembler.** Machines are grid-crafted with a diagram and then built as
RCC; everything else is grid-craftable. No assembly bench is needed and none is planned.

Needed for the scale route: a scale/swarf item with a remelt sink. That ruling belongs to
[metal recovery](metal-recovery.md) and is unwritten.

---

## Tooling

Two new wear items, not one per machine.

| Tooling | Machines | Form |
|---|---|---|
| **machine cutter** | lathe · shaper · planer · bore | forged and tempered; material sets hardness and life. `workbench/shapes/items/smithed/item-forged-machinecutter.json` |
| **shear blade** | shear | the same idea at a larger size, so its own item. `…/item-forged-machineshears.json` |
| **drill bit** | drill press | its own item — a bit is not a cutter |
| roll set | rolling mill | profile tooling — a shape, not a consumable. `RollSetSpec` ships |
| die | heading · nail | `ItemDie`, designed |

Forging and tempering are **vanilla mechanics**, so the tier ladder needs no new system: the temper
a player reaches is the hardness they get. This closes the "bit tiers and blade sets must be decided
together and neither has been" question in [boring machine](../machines/boring-machine.md) and
[shear](../machines/shear.md) — there is one ladder and vanilla already expresses it.

★★ **The contract is built: `MachineTool` in exlib** (2026-08-14, with the shear's blade sets, its first
consumer). A tool carries `{ schema, tier }` under a `machinetool` attribute and nothing else, and is
recognised by carrying a tier that parses rather than by its code — so a third party's cutter needs no
naming blessing. `MachineTool.Itemtype` builds the whole itemtype from a mod's own tier table, the seam
`ItemDie.Itemtype` opens for dies.

⛔ **A tool is not a die, and the split is load-bearing.** A die *names the job*, so a bench with no die
has no work at all; a tool names only its hardness and the jobs come from the declared table. The four
cutter machines and the shear take tools; the heading, nail and rivet benches take dies.

Consequences for the block layer: the four cutter machines get **one tool slot** in their window and
render the fitted cutter by selective element. The mill and the benches keep their fitted-tooling
gesture instead, because a roll set is a profile rather than a consumable.

---

## The job convention

The terminal-versus-sequence axis below is now general: every machine has a registry in one of those
two shapes, and [process-extension](process-extension.md) owns the contract. `MachineJob` is the
terminal shape for the four machine tools.

**`MachineJob` lives in exlib** (settled 2026-08-11), alongside `ItemDie`. Both are mechanics rather
than content: a mod adding rails to our mill should depend on the framework, not on a mod full of
furnaces. It hangs off `BlockEntityMachineStation`, exlib's container-plus-window base, which the
rolling mill already derives from.

✅ **Built 2026-08-12, and `MachineJob` turned out to be `ProcessJob`.** The sketch below and the terminal
registry the shear needed are the same shape, so no second type was written - the job gained `minTier` and
`seconds`, which is what a machine tool's work costs beyond a crop's. `ItemDie` carries a job set under a
`machinejob` attribute, is recognised by *parsing* rather than by its code, and ships
`ItemDie.Itemtype(domain, jobs)` - the public factory seam this page asks for below.
⛔ `RenderSpec` is **not** built: no machine renders a job yet, so every field would be design with no
consumer. The sketch stands as the design for when one does.

`MoldSpec` and `RollSetSpec` differ on one axis — **terminal versus sequence**. The four machine tools
are terminal; the mill and the bender are sequences. One schema covers both when the sequence is
optional.

```
MachineJob {
  Machine    : "lathe" | "bore" | "shaper" | "planer" | "mill" | "bender"
  Input      : code            // wildcard-capable
  Tooling    : code?           // fitted cutter / blade / roll set
  MinTier    : int             // temper floor
  MinTorque  : float           // harder metal costs more drive
  Output     : code
  Count      : int
  Seconds    : float
  Stages     : Stage[]?        // absent = terminal; present = mill / bender
  Render     : RenderSpec
}

Stage      { Tooling: code, Shape: code?, Seconds: float }
RenderSpec { Elements: string[]?, Anchor: string?, Shape: code?, Transform: …?, Animation: code? }
```

Three properties make it an extension seam rather than merely tidy:

**`Machine` is a string, not a type.** A third-party mod targets our machines without referencing our
C#.

**Render has two modes.** Our own content names `Elements` — the workpiece is already modelled inside
the machine shape and revealed per job, which is what the lathe's `cylinderlathe` / `shaftlathe` /
`rollerslathe` clips do. A third party cannot add elements to our shape, so it names an `Anchor` the
machine declares (`chuck`, `bed`, `table`, `vice`) and gets its own item shape positioned there.
`Animation` defaults to `cycle`, so a working machine costs a modder no animation work.

**The factory is the seam, not the table.**
`PatternItemDefinitions.Itemtype(domain, molds, shapes)` is public, takes a foreign mold table, and is
mirrored by `DiagramItemDefinitions.Itemtype`. That is the proven pattern and the one the mill lacks
— `RollSetItemDefinitions.Sets` and `RollSet()` are both private. `MachineJobDefinitions.Itemtype`
follows it.

### Two pre-existing blockers this convention inherits

✅ **One of the two is fixed (2026-08-11).** "Nothing can be rolled at all today" was exactly right:
`WorkPiece.FromStack` read `stockForm` from the **stack tree** while `StockItemDefinitions` declares it
on the **item type**, so every unrolled piece was refused as `WrongForm`. `FromStack` now falls back to
the item type, stack state still winning once a piece has been rolled.

✅ **Both are now fixed (2026-08-12).** `StockForm` is a registry - `Register` / `Unregister` / `TryGet` /
`SeedDefaults`, and deliberately **no `Clear`**, since a mod emptying the table would take our stock and
every shipped roll set with it. A third party's `accepts` value no longer dead-ends at `WrongForm`.

⛔ ~~`StockForm.All` is a closed static dictionary with no registration hook.~~ Was the literal wall on
mill extensibility; retired with the extensibility layer.

⛔ ~~Nothing can be rolled at all today.~~ `stockForm` was written to the itemtype and read from the stack
tree; `FromStack` reads both.

Good news: **the mill renders nothing during a pass** — no animator, its authored `cycle` clip never
plays, and all four roll families render at once. The render layer is greenfield, not a retrofit.

---

## Gotchas

* ⛔ **Texture paths under `workbench/` are unprefixed on purpose.** VS Model Creator cannot
  resolve domains and renders a prefixed shape untextured; vanilla textures go bare and ours go as an
  absolute local path. Domaining happens when a shape is copied into a mod's runtime assets. The
  "absolute path bug" entries on [boring machine](../machines/boring-machine.md) and
  [design table](../machines/design-table.md) are copy-out checklists, not defects in the source.
* All nine machine shapes had `cycle` authored `onAnimationEnd: EaseOut`, which drops the mesh back to
  the static shape after one pass. Fixed 2026-08-11: pose clips (`idle`, `rollergap*`) are `Hold`,
  motion clips are `Repeat`. `onActivityStopped: EaseOut` is a different enum and is correct.
* The bending machine's roller-gap poses are **six**, evenly spaced, with the hand screw's rotation
  derived from its travel at 45°/voxel — a screw's turn is coupled to its travel by the thread pitch.
  ⛔ The pose number rises as the gap *falls*, which inverts the mill's `Gaps` convention, where the
  number is the gap and the array descends.
* ~~`iiex:stock-slab` is stale — 400 u is shingled-bar size.~~ Fixed 2026-08-12: it is
  `iiex:stock-shingledslab` at 1200 u, and the bar beside it is 400. The old codes resolve through
  `StockForm.FormerNames` and `StockFormRenameMigration`.
* The cast-iron ingot route is mid-change. `config/metals/castiron.json:9` still emits `"ingot"` and
  the "Spur Gear (Cast Iron)" recipe still consumes `iiex:ingot-castiron`, while the design has moved
  the ingot mould to crucible steel. Move the recipe with the route or it points at an unresolvable
  ingredient — and nothing establishes what that does at load.
* Neither tool shape's mass divides off the 25 u rod: the cutter measures 26 vx³ = 65 u and the shears
  96 vx³ = 240 u. The ladder's rule is that every crop divides exactly. 20 vx³ = 50 u and 100 vx³ =
  250 u keep both silhouettes.
* `item-forged-machineshears.json` names its second blade `Sear11`. Harmless until selective-element
  rendering refers to it.

---

## Finding 2026-08-20: this is two families, not nine machines

⛔ Recorded as a finding, not a ruling - one question below is still with the owner.

**Owner:** *"all of the machining machines are the same machine with different shape, interaction cells and
recipes loaded. The only machines that differ are rolling mill and bending machine, because those have in
from one side and out from another mechanic."* Checked against
[machines.txt](../../../workbench/machines.txt), and it is **seven** station machines, not five:
cutter, drill press, horizontal bore, lathe, nail cutter, planer, riveter, shaper all read *"put item in
window interface, pull resulting item after machine finishes operating"*.

★★ **The two exceptions are the same as each other.** The mill and the bender share *both* mechanics -
direction picks which of two cells is the input, and roller height is set by pushing down - and the mill's
own note says so: *"the same mechanic as for bending machine."* So the line is two families.

⛔⛔ **Six of the eight stations also need held RMB to operate**, which the *Mode of operation* lines do not
say and only the per-machine *Notes* do: a separate `i` cell on the bore, lathe and planer; the `I` cell
itself on the shaper, nail cutter and riveter; plus the steam hammer's lever. Hold-to-operate is the
suite's dominant machine verb.

★★ **Nearly all of the framework exists**: `ShapeByType`, `FillerOffsetsByType`,
`ProcessJobRegistry.Jobs(machine)` off `config/processjobs/*.json`, `MachineTool`, and
`BlockEntityMachineStation` - which both the shear and the mill already derive from. The one hardcoded
thing is `BlockShear.MachineKey`, a `const`.

⛔ **The one missing mechanic is hold-to-progress.** `ProcessJob.Seconds` is a duration; six machines want
it consumed only while RMB is held. That is the same mechanic as the [workbench](../machines/workbench.md)'s
craft sequence, so it should be built once for both.

★ Building the pass-through family also pays off the **flatwide roller collapse**, which is ruled but not
built for want of the mill's two `i1` cells - specified in the mill's own note.

⛔ **Open, with the owner:** machines.txt gives the cutter a **window**, but `BlockShear` was built as
click-with-stock-in-hand and has none. Does the shear adopt the window when the station family lands, or
was the direct-click verb a deliberate change to the spec?

---

## Open

| # | Question | Weight |
|---|---|---|
| 1 | Which drive wins when a machine carries both vanilla MP and `mpenergy`. The shaper is the first to need an answer | high |
| 2 | Whether the bender's *conical* job needs tapered rolls, or is simply the tightest screw setting. If the latter, its tooling family disappears and the bearing race's missing small-radius tool closes for free | high |
| 3 | The scale/swarf item and its remelt sink, without which the planer's slab route mints nothing | high |
| 4 | Masses for every machined part and blank. [density rule](density-rule.md) has no settled figure for hollow or toothed geometry, which is what all of these are | high |
| 5 | The hand gear's exact bill and durability cost, against the shaper route, so the ladder is a real choice | medium |
| ~~6~~ | ~~Whether `MachineJob` lives in exlib or iiex~~ — **settled 2026-08-11: exlib**, and `ItemDie` with it. Both are mechanics other mods consume, which is the whole point of the string-keyed `Machine` field; a contract living in iiex would force a dependency on a *content* mod to use it. The station they hang off is already there — `BlockEntityMachineStation` | closed |
| 7 | Seconds and `MinTorque` per job — none proposed anywhere | medium |
| 8 | Footprints. The planer is 73 × 64 × 46 voxels and the bender 74 × 58 × 41; both overhang their cell heavily on negative axes | medium |
| 9 | The drill press has one consumer (the gear web) and no shape yet. It needs a second job before it is more than a step in one chain — the test every other machine here had to pass | medium |
| 10 | Whether the faced and drilled intermediates are distinct item codes or stack states. Three codes per gear size multiplies fast; `WorkPiece` is the precedent for states | medium |
