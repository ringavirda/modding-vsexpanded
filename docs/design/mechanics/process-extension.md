# Process Extension — how a machine learns what it can make

**Status** settled 2026-08-12; the sequence half is **built** — `ProcessRoute`, `ProcessRouteRegistry`,
`ProcessRouteLoader`, `SpecSchema`, `ProcessItemEmitter` and `ProcessItemRenames` in exlib, `MillSchedule`
and the shipped shingledbar/shingledslab catalogues in iiex. The rule and the two registry shapes are fixed; the schemas
below are the contract a third-party mod writes against.
**Mod** exlib (the contract and the emitter) · every mod (every registry)
**Owns** the rule that a machine names no product, the two shapes a process registry takes, how a
declaration becomes items, and what we promise about the schema over time.
**Depends on** [framework-composition](framework-composition.md) (the machine bases the registries hang
off) · [machining-line](machining-line.md) (the terminal-versus-sequence axis, the die contract) ·
[recipes & config](recipes-config.md) (`ExItemDef`, the injection path) ·
[rolling mill](../machines/rolling-mill.md) · [shear](../machines/shear.md)

---

## Role

Other mods must be able to add to our processes — a roll set, a mold, a diagram, a die, a crop — from
their own mod, without our source and without a fork. This page fixes how.

It exists because the idiom was real but unstated. Three processes already worked this way and nothing
said so, nothing enforced it, and the two unbuilt ones had made no commitment.

---

## The rule

> **Tooling carries its own spec. The machine reads it and names no product.**

A machine that hard-codes a product code cannot be extended, and every such code is a place a modder
has to fork instead of declare. `RollSetItemDefinitions` states the target form: *"the mill reads what
to do off the fitted set and never names a product in code."*

The corollary decides where state lives:

> **An item code is a discrete stage. An attribute is continuous state within a stage.**

Thickness, curvature, turned-ness and strip count vary as a piece is worked and no recipe should ever
match on them, so they are attributes. Crossing a stage boundary changes the code.

⛔ This is not a preference. `CraftingRecipeIngredient.SatisfiesAsIngredient` compares type, code,
stack size, skip-variants and tags on the wildcard path — **never stack attributes**
(`vsapi/Common/Crafting/CraftingRecipeIngredient.cs`). A half-rolled piece distinguished only by an
attribute would satisfy every recipe asking for that code. Two stacks of one item with differing
attributes also refuse to merge, so the player gets identical-looking piles that will not combine.

---

## Every machine has a registry, in one of two shapes

**Terminal** — one input, one job, one output. The output count is part of the job: the shear crops a
`rolledrod` into **four** rods, so a terminal job that could only say "one output" could not express
the crop table at all.

Declared at `assets/<domain>/config/processjobs/*.json`, one file per machine by convention and merged
exactly as a ladder is — a second job on one input is reported and the first stands:

```json
{
  "schema": 1,
  "machine": "shear",
  "jobs": [
    { "input": "iiex:stock-shingledbar", "stage": 2.0, "family": "grooved",
      "output": "game:rod-iron", "count": 4, "minTorque": 0.3 },
    { "input": "iiex:nailplate", "output": "game:metalnailsandstrips", "count": 4 }
  ]
}
```

`stage` and `family` are optional and belong together: a job naming them takes a piece part way down a
ladder, at that gauge on that branch; one omitting them takes the whole item. A staged job wins over a
whole-item one for the same input, so a stock family can carry both.

### What a count means *(settled 2026-08-12)*

> **A staged job crops. A whole-item job converts.** The distinction decides whether the input survives,
> and `count` reads differently on each.

| Job | `count` is | The input |
|---|---|---|
| **staged** (`stage` present) | the whole piece's yield - a bar at the rod gauge is **4** | **survives**, with one fewer crop left in it, still stock at the same stage |
| **whole-item** (no `stage`) | what one conversion produces | **consumed** |

The names are the rule: a crop takes a product's worth off and leaves the remainder on the deck, and a
conversion turns the thing into another thing. Both were already in the design - crop-not-convert is
[shear](../machines/shear.md)'s, and `nailplate → 4 × nails-and-strips` is the one whole-piece conversion
in the forming ladder.

`count` is the modder's own number and nothing checks it against geometry. That the shipped rows happen to
divide their input's mass exactly - 400 / 4, 600 / 6, 3000 / 5 - is the [density rule](density-rule.md)
being applied by whoever authored them, not a constraint the loader enforces. It could not be: length and
mass are art, and a cut point is a design choice.

★★ **A staged crop needs one integer on the stack, and nothing else** *(ruled and built 2026-08-13)*. The
piece carries **crops taken**, and `count - taken` is what is left; at zero left it is spent. It needs no
mass and no length, because **length is not defined programmatically - it comes from the art** - and a cut
point is declared rather than derived. `count` being the modder's own number is the whole point: what a
piece divides into is a design choice, not arithmetic we can do for them.

★★ **Taken, not remaining**, so that zero means *untouched*: a piece that has never met the machine and one
worked out to nothing must not read alike, no piece already in a world needs migrating, and the declared
`count` stays the authority - retuning a row from 4 to 6 gives every existing piece the two extra crops
rather than stranding it on the number it was cut against.

⛔⛔ **A part-worked piece cannot re-enter a sequence.** The tally is against *this* stage's count, so a
piece carried to the next stage would be worth that stage's whole count again however much of it had gone -
metal from nothing. The mill refuses one outright (`FeedVerdict.PartCropped`). That refusal is what lets the
tally stay a single `int` instead of a proportion carried between stages, and any other sequence machine
declaring staged jobs owes the same refusal.

**Sequence** — a ladder the work walks, one step at a time, carrying state between steps. Only the
rolling mill and the bending roller are sequences today: the mill walks thickness *down* in gaps, the
bender walks curvature *up* in passes. Everything else — shear, drill press, lathe, shaper, planer,
nail machine, rivet machine, sand casting, the design table — is terminal.

| Machine | Shape | Registry declares |
|---|---|---|
| rolling mill | sequence | roller families, the process route, which family accepts each stage |
| bending roller | sequence | curvature steps |
| shear | terminal | the crop table: input, output, count |
| drill · lathe · shaper · planer | terminal | the die's job ([machining-line](machining-line.md)) |
| nail · rivet machine | terminal | input, output, count |
| sand casting | terminal | the mold's cavity and product |
| design table | terminal | the diagram, scanned by code |

A machine is not a special case because its registry is small. A terminal registry with one entry is
still a registry, and it is what lets someone add a second entry.

### A registry is contributed to, never owned *(settled 2026-08-12)*

The registry is **not a field on the tooling item**. It is a merged catalogue any collectible may declare
into, and that is what makes both extension directions cost the same:

| A mod adds | It ships | It patches |
|---|---|---|
| a machine family (a serrated roll set) | its own item, declaring the stages that family accepts | nothing of ours |
| a stock family (a bronze bar) | its own item, declaring that family's whole ladder | nothing of ours |

Hang the ladder on the tooling and the second row has to patch every roll set; hang it on the stock and the
first row has to patch every stock item. Merging removes the choice.

**Merging is by (thickness, accepting family)** — a stage's address, never thickness alone, because a fork
means two families draw different geometry at one gauge. An unclaimed pair is added. A pair already drawn
the same way is a no-op, so contributing twice is safe and load order does not matter. ⛔ A pair **redrawn
differently is reported and the first declaration stands**: taking the last writer would make the ladder
depend on mod load order, which nothing can reproduce.

**What stays on the tooling is what only the tooling knows.** A roll set keeps its roller family, its barrel
width, its torque and the stock forms it will bite — a narrow barrel refuses a slab whatever states the slab
has, and no ladder can carry that. The two facts are independent, so neither is derived from the other.

---

## Sequence: the process route

A stage declares its **thickness**, the **element** that draws it, which **machine families accept**
it, and — only if it is a stopping point — the **code** of the item it becomes.

It is declared as a **config asset**, at `assets/<domain>/config/processroutes/<anything>.json`. One file per
stock family is the convention and nothing enforces it — the registry merges whatever arrives, so two mods
may both contribute to one family.

```json
{
  "schema": 1,
  "family": "shingledbar",
  "shape": "iiex:item/smithed/shingled-bar",
  "stages": [
    { "thickness": 2.50, "element": "Grooved250",   "acceptedBy": ["grooved"] },
    { "thickness": 2.00, "element": "Grooved200",   "acceptedBy": ["grooved"], "code": "rolledrod" }
  ]
}
```

⛔ **A config asset and not an item attribute, and the load order is why.** Items are *generated* from the
stopping points below, and that has to happen at ExecuteOrder **0.04** — before the JSON patch loader (0.05)
and well before the object loader (0.2) that builds itemtypes. A ladder carried on an itemtype could not be
read in time to generate one: you cannot build an itemtype from data that lives on an itemtype.

The catalogue is therefore read **twice, by one parser**: at 0.04 for generation, and again at
`AssetsFinalize` for the registry the machines consult — which is the post-patch one, as the earlier read
cannot be. A consequence worth knowing: **patching our catalogue file adds a route but no item**, because
the patch lands after generation. To add a stopping point, ship your own file; the merge makes it land in
the same family.

**`code` present means a stopping point** and an item is generated for it. **`code` absent means a
render-only intermediate.** That one field carries the whole distinction, so the item catalogue, the
mill's stopping points and the held-item appearance all come from one declaration.

**`acceptedBy` makes the ladder a graph, not a line.** A stage several families accept is a fork: the
same piece at the same thickness continues on grooved toward a rivet rod or switches to flat toward a
nail plate — same feed, same mass, two routes. A line could not express that, and the fork is the
mill's whole point.

⛔ **A rung is a gap, not a round** *(corrected 2026-08-12, when the two-round model was built)*. A gap
costs two rounds - 3.00 → 2.75 → 2.50 is gap 2.5 taken in two bites - but only **2.50 is a rung**. The
half-step between them is arithmetic (`(thickness + gap) / 2`), it is never declared, and it is drawn by
the composed mesh rather than by an element.

This page previously said the ladder is drawn per *pass*, with the half-steps as the rungs carrying no
`code`. That reading puts twice as many gap bands on the mill's deck as the barrel has grooves, because
`MillSchedule` builds the deck's bands straight from the rungs a family accepts. A third-party ladder
should therefore declare **one rung per gap**, and get its half-steps for free.

### Mid-pass states are a per-stack mesh, not an item

An uneven piece is never claimed, so a half-rolled piece is *the same item code* as the stock it came
from with a different thickness attribute. It cannot be an item def; it is rendered by swapping
`renderinfo.ModelRef` in `OnBeforeRender` from the stack's own thickness, exactly as vanilla's
`ItemWorkItem` renders its voxel state (`vssurvivalmod/Item/ItemWorkItem.cs`).

⛔ Vanilla documents the trap in place: the handbook clones the stack **every frame**, so the
no-attribute path must cache under a **stable shared key** and never a per-stack mesh-ref id.

This is why a family's stages belong in **one shape file**. The renderer walks the ladder by
thickness, and every step must be addressable from one place.

⛔ **Thickness alone does not address a stage — the piece must carry its branch.** A fork is two
families drawing one gauge differently (`Grooved200` and `Flattened200` both at 2.0), so a renderer
keyed on thickness would be visibly wrong half the time. The work piece therefore records the roller
family that last worked it, written with the reduction, and the mesh cache is keyed on it too. A piece
that names no branch — never rolled, or rolled before the field existed — falls back to the composed
mesh rather than guessing one.

The two mesh routes, in order: the **drawn** stage when the ladder names a shape file and the stage an
element in it, and the **composed** mesh otherwise — the form's base shape scaled from the same numbers
the simulation uses. Drawn art wins where it exists because it carries detail a scale cannot: a groove
is not a thinner rectangle. The composition covers every gauge nobody has drawn.

---

## Shapes: both conventions are supported

`element` is optional.

- **Present** — the stage is one element of a family file. Right for a progression, because the
  geometry encodes the physics: thickness falls and length grows across the elements, a relationship a
  reader can check at a glance in one file and would have to hold in their head across four.
- **Absent** — the whole shape file is the stage. Right for a finished product with its own model.

Elements of a progression are authored superimposed on a shared origin, so each is already in the item
frame and needs no re-centring. An element drawn off to the side is by convention **not a stage** — it
is another machine's output, and it belongs in that machine's registry. `CutRod1` in
`item-shingled-bar.json` is the worked example: it is the shear's product, declared by the shear.

⛔ `selectiveElements` matching is the engine's per-segment prefix rule, so naming an ancestor keeps
more than intended and naming an element exactly drops its children. Flat, distinctly-named top-level
elements are safe; a nested one needs care.

---

## Items are generated from the declaration

A stage that names a `code` gets an item built for it and injected as a synthetic itemtype, through the same
path `MetalFamilyEmitter` already uses for metal families: read the catalogue at `AssetsLoaded`, emit
`ExItemDef`s, inject at ExecuteOrder **0.04** — below the JSON patch loader (0.05), so other mods can still
patch the result, and below the object loader (0.2) that consumes it.

Five properties that path already has, and which this one keeps:

- **per-entry opt-out** — `"generate": false` means the code exists already; wire it up, build nothing.
  It is also how a declaration points at an item the mod ships itself.
- **defaults for every field**, so a sparse declaration still yields a working item: it gets the family's
  shape (drawn at the stage's `element` via `selectiveElements`, or the whole file when the stage names
  none), a stack size and a creative-tab entry.
- **the owning domain comes from the declared code**, so a modder's items land in their domain.
- **a code that cannot be built is skipped, not fatal**, and logged: a malformed code, or one already
  claimed by another branch — a fork can reach one product two ways and the object loader would reject the
  duplicate.
- **the buildable set is public** (`ProcessItemEmitter.GeneratedCodes`), so a guard can assert no
  declaration names something unbuildable and nothing hand-authored collides with a generated code.

⛔ **The `game:` domain is never generated into.** Injecting an itemtype there would replace one of the base
game's own items, so a stage naming a vanilla code such as `game:rod-iron` is wired up and never built —
declare `"generate": false` to say so explicitly and keep it out of the log.

⛔ **Lang keys cannot be generated.** An item with no `item-<code>` entry displays its raw code. The
declaring mod ships its own strings; this is the one part that is not automatic.

⛔⛔ **A generated code is save data.** Once an item exists in someone's world, the rule that produced
its code is frozen. So a declaration **states its code explicitly** and exlib never computes one.

### Renames are declared, not detected

A code that changed carries its old name, and the emitter registers the remap through
`BlockMigrationModSystem` automatically:

```json
{ "code": "nailplate", "formerCodes": ["oldnailplate"] }
```

⛔ Exlib sees only the current catalogue. A code that vanished and a code that appeared are
indistinguishable from a rename without the hint, so the contract is *declare the old code and get the
migration free* — never *change the convention and we work it out*.

---

## Schema stability

Every spec attribute carries a `schema` number, and its parser reads every shipped form: current
first, older as a fallback. The shape to copy is the `possibleOrientations` migration, whose lesson is
that **the absent-versus-empty distinction is the migration** — a reader that cannot tell "never
written" from "written empty" has nothing to fall back on.

This was chosen over freezing the schema at release and going additive-only. That was cheaper for us
and worse for the people who asked for this, because it makes their content break on our schedule.

`SpecSchema` (exlib) holds the three rules every parser routes through:

| Declared | Read as |
|---|---|
| absent | **schema 1** — absent is not unversioned; the form that shipped before the field existed has a number whether or not it was written down |
| below the current one | itself, and the parser falls back to that form's shape |
| above the current one | ⛔ **refused**, with an error naming both numbers — we cannot know what changed, so reading it as the form we do know would mis-parse someone's content silently. The fix is on the reader's side: update the library |

⛔ **A spec attribute is not save data.** It sits on the itemtype and is re-read from the declaration
every load, so *our own* emitters never need a fallback — regenerating the def replaces the old form
outright. The migration burden exists **only for declarations we do not own**, and it therefore starts
at the first schema a third party could have written against. That is why `RollSetSpec` could drop
`gaps`/`outputs` outright at schema 1 and still satisfy this section.

⛔ **The diagram contract carries no schema, because it carries no spec.** A `diagram-*` item is
identified by code shape alone — first code part `diagram`, plus a `type` variant — and holds no data
for a parser to version. If diagrams ever gain a spec, that is when they gain a number.

Our emitted JSON is the template a third party copies, so every spec we ship declares `schema`
explicitly even though absent would parse. `ShippedSpecSchemaGuards` pins it.

---

## Consequences

- The JSON attribute schema is **the API**. `ExBlockDef`/`ExItemDef` are a private authoring
  convenience — a third party cannot add a def to our assembly — so the golden tests protect our
  authoring and **not** this contract. It needs its own guards.
- A machine's registry is the only place its products are named, which is what makes the
  "names no product" rule checkable by a source scan.
- The rolled-product catalogue is no longer hand-authored item defs; it falls out of the mill's and
  the shear's registries.
