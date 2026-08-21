# Item piles

**Status** designed, nothing built - `grep -rn "PileLayout\|StockPile\|HearthPile" --include=*.cs src/`
returns nothing   **Mod** exlib (`ExpandedLib`)

**Owns**

* the pile computation itself - given a bed, a piece and an index, where that piece sits and at what yaw;
* the two stacking modes (`pyramid`, `flat`), their steps and why each reads the way it does;
* the rule that the mode is the **section class** rather than an axis of its own, and how a caller resolves
  that class for an arbitrary item;
* capacity - what "how many fit" means for each mode;
* the jitter rule: reproducible, derived, never an RNG;
* the caching, texture and threading contract every consumer of the pile must honour;
* the requirement that there be exactly **one** implementation, in exlib, because the reheat hearth's bed and
  the stock rack compose the same pile out of different furniture.

**Does not own - cited only, never restated**

| Fact | Owner |
|---|---|
| the hearth's 3 x 2 footprint, its two seatings and their cell geometry | [reheat furnace](../machines/reheat-furnace.md) |
| the rack's block, footprint, recipe and vertical-stacking flag | [stock rack](../machines/stock-rack.md) |
| the section law itself (`square` vs `flat` deformation) and which sets are which | [rolling](../processes/rolling.md) |
| `1 vx3 = 2.5 u` and every piece's drawn section | [density rule](density-rule.md) |
| what may be left where, and the handling limits behind it | [recoverability](recoverability.md) |
| the work piece, its family and its stage | [rolling mill](../machines/rolling-mill.md) |

**Depends on** [rolling](../processes/rolling.md) (the section class) ·
[density rule](density-rule.md) · [reheat furnace](../machines/reheat-furnace.md) ·
[stock rack](../machines/stock-rack.md) · [multiblock & filler structures](multiblock.md)

---

## Role

Two machines hold loose pieces on a bed and draw them: the reheat hearth's hearth and the stock rack. A
third will follow, because "somewhere to put a piece down" is furniture, not a machine feature. The
arrangement is the same computation in every case - lay a piece in a bed, put the next one beside it, and
start a second layer when the first is full - and only the furniture differs.

Authoring that arrangement as shape elements does not survive contact with the catalogue. The hearth's
shape carries **fifteen** authored groups (`ShingledBlooms`, `ShingledSlabs`, `CastBillets`, `CastBlooms`,
`CastSlabs` x three rows), which is five stock forms x three positions and covers no rolled stage, no
product and nothing another mod ships. Every new form is three more groups drawn by hand, and a form that
gains a stage renders at the wrong gauge with nothing failing.

Composed instead, the count is zero. Each piece is tesselated from the item's own shape - the same literal
the held item uses - so every rolled stage, every product and every foreign item renders for free, and the
bed and the hand show the same mesh.

---

## How it works

### The bed

A bed is a rectangle and a height limit, in voxels, in the container's own frame. It is furniture: the
hearth's two seatings and the rack's shelf are three different beds over one computation, and neither the
bed's dimensions nor which of them a container offers is configurable - both fall out of the block's
geometry.

| Consumer | Bed | Where it comes from |
|---|---|---|
| hearth, lengthwise | 3 beds, each 1 cell across x 2 cells deep | the hearth's 3 x 2 footprint read along its depth |
| hearth, crosswise | 2 beds, each 3 cells across x 1 cell deep | the same footprint read across its width |
| stock rack | 1 bed, 1 cell across x 3 cells long | the rack's own 1 x 1 x 3 (owner-confirmed 2026-08-15; its shape is not drawn yet) |

The two hearth seatings are mutually exclusive - a crosswise piece physically occupies all three lengthwise
beds - so a container offering both holds one contents model with a mode tag, never one array per bed.

### The two modes

| Mode | Layer holds | x step | y step | Reads as |
|---|---|---|---|---|
| `pyramid` | `n, n-1, n-2 …` | half a piece width per layer | `0.866` x piece height | billets, bars and blooms nesting |
| `flat` | one | - | a full piece height | slabs, plates and beams stacked |

`n` is `floor(bedWidth / pieceWidth)`, so a wide piece gets one per layer in either mode and the two agree
at the degenerate case.

The half-width offset and the `0.866` step are what make a pyramid read as *nested* rather than as a grid
with a gap: `sqrt(3)/2` is the height of an equilateral triangle of unit side, i.e. the drop of a round
piece settling into the valley between two below it. Using a full height instead leaves the upper layer
floating, which is exactly the tell that the pile was placed rather than stacked.

`flat` is a single column by design, not by arithmetic. A flat piece stacked beside another reads as two
piles, and the pieces this mode exists for - slabs, boiler plates - are near enough the bed's full width
that there is no room beside them anyway.

### The mode is the section class

The mode is not a new axis to declare. It is the **section class** the deformation law already needs
([rolling](../processes/rolling.md)): a square section is rolled on grooved sets, refuses the spread and
nests; a flat section is rolled flat, spreads and stacks. One property, two consumers, and the pile is the
cheaper of the two to get wrong.

A caller resolves it in this order, taking the first that answers:

1. **the family that last rolled the piece** - `WorkPiece.Family` is on the stack, and a piece is whatever
   shape the last set left it in. A bar taken grooved is square however it started;
2. **the item's own `sectionClass` attribute** - which is what a product carries. A `boilerplate` has no
   work piece at all, and a foreign mod's item declares this and needs nothing else;
3. **`flat`** - the safe default. A flat pile of square pieces looks careless; a pyramid of flat pieces
   floats, which reads as a bug.

Family to class is a contributed-to registry keyed on the family name, seeded with `grooved` -> square and
`flat` / `flatwide` -> flat. A roll set declaring an unseen family registers its class with it, so a mod's
own family piles correctly without an exlib edit.

### Piece size is declared, not measured

The pile needs a width, a height and a length for the piece it is laying, and takes all three **as
declared**. Two homes, and they are the same split `ProcessJob` already makes between a staged job and a
whole-item one:

* a **staged** item declares its size per stage, beside the `element` that draws that stage - a bar at 2.5
  and the same bar at 2.0 are different pieces and pile differently;
* everything else declares it once, as an item attribute - a `boilerplate` has one size and no stages.

⛔ Measuring the drawn shape's bounding box instead was refused. The box exists only once the shape is
loaded, which is tesselation time, and the pile is arithmetic that has to answer capacity before a bed is
ever drawn - `TryLoad` needs to know whether a piece fits without tesselating anything. Declaring it also
puts the number in the same file as the art it describes; the stage tests that already hold generated art
to the form table (`RolledStockStagesTests`, `CastStockStagesTests`) are where the two are held together.

### Jitter

Hand-placed pieces are not aligned. Each seat therefore carries a small xz offset, and `flat` additionally
a small yaw, so a stack of plates sits askew. `pyramid` takes no yaw: the nesting is the point, and turning
a piece breaks it.

⛔ The jitter is **derived, never rolled**. `GameMath.MurmurHash3` over `(position, layer, index)` gives a
value that is stable for one container forever and different between two containers a block apart. An RNG
makes the pile jump on every re-tesselation, and re-tesselation happens for reasons that have nothing to do
with the pile.

### Capacity

Capacity is by layer, not by slot:

```
perLayer = floor(bedWidth / pieceWidth)
pyramid  = perLayer + (perLayer-1) + … down to 1, or until layers run out
flat     = layers
```

So capacity is a function of the piece, not a constant of the container, and a container states only how
many layers it will take. A bed holding narrow bars holds many more of them than it holds slabs, which is
the behaviour a player expects and needs no rule of its own.

---

## The interface

Pure static maths, no world, no VS types beyond `Vec3f` - so the whole rule set is pinnable headless, which
is the house style for this kind of code (`StockMesh.SideOf`, `MillFeed.Decide`, `ShearFeed.Decide`).

| Member | Answers |
|---|---|
| `PileMode { Pyramid, Flat }` | the two modes |
| `PileBed(Width, Length, Layers)` | the furniture, in voxels |
| `PieceSize(Width, Height, Length)` | the piece being laid, from its drawn shape |
| `PileLayout.PerLayer(bed, piece)` | how many fit across one layer |
| `PileLayout.Capacity(bed, piece, mode)` | how many the bed holds in total |
| `PileLayout.Seat(bed, piece, mode, index, seed)` | `(Vec3f offset, float yaw)` for the `index`-th piece |

`Seat` takes a flat index rather than `(layer, slot)`: a caller has *n* pieces and wants the *i*-th seated,
and which layer that lands in is the pile's arithmetic rather than the caller's.

⛔ **Two names were rejected and should not come back.** `ItemPile` collides with vanilla's
`BlockEntityItemPile`, which is a pile *block's* entity and a different idea entirely - one is already cast
to in `BlockEntityCowperStove`. `StockPile` is the name the expansion plan's U8.9 proposed, and it is not
generic: this bed holds `boilerplate` and any mod's item, not only rolling stock. What the type computes is
a layout, so that is what it is called.

---

## Numbers

All proposed. None exists in code.

| Key | Value | What it does |
|---|---|---|
| pyramid x-offset | 1/2 piece width per layer | nesting |
| pyramid y-step | `0.866` x piece height | `sqrt(3)/2` - a piece settling into the valley below |
| flat y-step | `1.0` x piece height | stacking |
| flat yaw jitter | small, seeded - magnitude is a playtest knob | hand placement |
| xz jitter | small, seeded, both modes - magnitude is a playtest knob | hand placement |
| jitter source | `GameMath.MurmurHash3(pos, layer, index)` | reproducible; see Gotchas |
| hearth bed, lengthwise | 1 x 2 cells, 3 of them | [reheat furnace](../machines/reheat-furnace.md) |
| hearth bed, crosswise | 3 x 1 cells, 2 of them | [reheat furnace](../machines/reheat-furnace.md) |
| rack bed | 1 x 3 cells, 1 of them | [stock rack](../machines/stock-rack.md) |
| rack layers | 5 | [stock rack](../machines/stock-rack.md) |

---

## Code

Nothing exists. What it replaces, and where it hooks in:

| Piece | Where | State |
|---|---|---|
| `PileLayout` | `src/ExpandedLib/Blocks/Structures/` | to write; pure, therefore tested headless first |
| section class registry | `src/ExpandedLib/Processes/` beside `ProcessRoute` | to write; contributed to, like the other registries |
| the hearth's bed | `BlockEntityHeatingHearth.OnTesselation` | authored groups today; composes instead |
| the hearth's 15 groups | `assets/editable/shapes/furnaces/firebox/furnace-megablock-heatinghearth.json`, `Fillings/Items1`, `Items2`, `Items3` | deleted by the change |
| the hearth's element map | `HeatingHearthLayout` | deleted with them |
| the rack's shelf | `BlockEntityStockRack` | unwritten; this is its second consumer |

⛔ `HearthRows` is **not** the thing to share. It is a fixed `Left/Centre/Right` enum with a hard-coded
reachability rule, and a bed wants layers x index, which is a different shape entirely. Share the pile, not
the rows.

⛔⛔ **The puddling furnace must stop borrowing the heating hearth's row count first** *(owner ruling,
2026-08-15: it is a separate structure with different logic, nearer the crucible furnace's hearth than this
one)*. `BlockEntityPuddlingHearth` sizes **two** arrays - `_pigs` and `_fettled` - off
`HeatingHearthLayout.Rows` (`BlockEntityPuddlingHearth.cs:21-22`), so today any change to the heating
hearth's contents model silently resizes the puddling furnace's state. Giving the puddling furnace its own
constant is a prerequisite of this work rather than a consequence of it: it is a small, independently
verifiable change that makes every later step incapable of reaching the wrong machine.

---

## Gotchas

⛔⛔ **`Items1 / Items2 / Items3` are not in positional order.** `Items1` is the left cell, `Items3` the
centre, `Items2` the right - the group's own pivot decides where each draws, and the children are
byte-identical. Reading "Items2 = middle" off the name puts every piece one cell out, silently, because
selective-element matching drops an unknown name without an exception and a wrongly-known name just draws
in the wrong place. The composed pile deletes this trap rather than working around it, which is most of its
value.

⛔ **Cache the piece, transform the clone.** One entry per `(item, stage)`, never per world position x
index: baking a seat or its jitter into the cached mesh makes the cache unbounded.

⛔ **Translate before the single final rotation.** One `ExMesh.RotateByShape(mesh, block)` at the end, after
every piece is seated. Rotating per piece turns each about its own origin instead of the container's.

⛔ **The atlas insert is main-thread only** and `OnTesselation` runs on the chunk worker. The tesselation
pass may only *read* the texture cache; on a miss it skips the piece, queues the build with
`capi.Event.EnqueueMainThreadTask`, and marks the block dirty when it lands. That is why the cache belongs
to the mod system rather than to a block entity.

⛔ **Textures come from the five-arg `ShapeTextureSource`.** Item-atlas UVs are wrong for
`ITerrainMeshPool`, and the symptom is a piece that draws with another item's texture rather than a crash.

⛔ **`ResolveBlockOrItem` after reading a stack off the tree**, or the piece has no `Collectible` and
silently fails to draw.

⛔ **Return `true` from `OnTesselation`** when the block entity draws the whole mesh, or the default block
mesh is drawn as well.

⛔ **Stock items are `MaxStackSize(1)`** (`StockItemDefinitions`), because each piece carries its own gauge,
crop tally and temperature. A bed therefore holds *n* distinct stacks and never a count with a quantity.
Merging them destroys work-piece state silently.

⛔ **A piece drawn off to the side of its shape file is not this piece.** Family shape files carry other
machines' outputs as extra elements; the pile seats the element the stage names, not the file.

---

## Open

- Whether a container ever mixes classes in one bed. A pyramid with a plate on top is expressible and
  probably should not be; refusing it is a load-time rule on the container, not on the pile.
- The stock rack's vertical-stacking flag is a prerequisite for it and not for the hearth
  ([stock rack](../machines/stock-rack.md)) - footprint fillers default to `allowAttach: false`, so a rack
  placed on a rack needs the two filler cells to opt in.
- Nothing states what happens to a pile when its container is broken. The hearth destroys loaded stock
  today, which is [reheat furnace](../machines/reheat-furnace.md)'s Open #10 and would be inherited.
- Whether the coke oven's hand-stacked chambers and the charge pile want this too. Both arrange loose items
  in a bounded space; neither has been read against this model.
