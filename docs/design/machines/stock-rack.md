# Stock rack
**Status** **BUILT 2026-08-21** as `iiex:storage-rack`. Block, block entity, occupancy catalogue, recipe,
lang and tests all ship; the composed-contents renderer is written but unverified in game (see § As built).
**Mod** iiex (`IronIndustryExpanded`)

> ## ⛔ Amendment 2026-08-21 - the owner's rule, which supersedes parts of this page
>
> The owner drew `workbench/shapes/machines/machine-megablock-storagerack.json` and ruled:
>
> * *"footprint xz slice `O # #`, 3 block 1 tall, 1 wide, 1 deep megablock. It has 3 cells and all have
>   interactions."*
> * *"It can store 3 stacks of items that occupy 1 block length, 1 stack of 2 block length + 1 stack of
>   1 block length, or 1 stack of 3 block length item."*
> * *"**Length is intended**, everything is configurable via json, including how many cells and which
>   stack of specific item occupies."*
>
> **Capacity is length, not layers.** The [Operation](#operation) section's
> `piecesPerLayer = floor(rackWidth / pieceWidth)` x `RackLayers` model and the [Numbers](#numbers)
> worked table are **superseded**: a rack is a row of N cells, each stored stack occupies a contiguous
> run of `L` cells, and the rack is full when the runs fill it. The three arrangements above are the
> complete enumeration for N = 3. Nothing stacks upward.
>
> **What that dissolves.** The two stacking modes (`pyramid` / `flat`), the half-width offset, the
> 0.866 y-step, the layer count and the jitter belong to the *reheat hearth's bed*, not to the rack.
> The rack therefore **no longer depends on `PileLayout`**
> ([plan](../../../../docs/superpowers/plans/2026-08-15-item-piles.md), stage 1, unstarted) - which this page named as
> its blocking prerequisite. It can be built on its own.
>
> **What that opens.** *Mixed contents* under [Open](#open) is answered: yes, by length - a 2-run beside
> a 1-run is one of the three arrangements. *One inventory or per-layer* is answered: one ordered list of
> runs.
>
> ### As built
>
> | Piece | Where |
> |---|---|
> | `BayRun` / `BayLayout` | `exlib/src/Storage/BayLayout.cs` - pure, world-free capacity |
> | `BayOccupancy` + registry + loader | `exlib/src/Storage/` - contributed-to, `config/bayoccupancy/*.json` |
> | `BlockStorageRack` | `mods/iiex/src/BlockStructures/Storage/Blocks/` |
> | `BlockEntityStorageRack` | `.../Storage/BlockEntities/` |
> | the shipped catalogue | `mods/iiex/assets/iiex/config/bayoccupancy/storagerack.json` |
> | recipe | `storagerack-grid`, 5 planks -> 2 racks |
> | tests | `BayLayoutTests`, `BayOccupancyTests` (exlib), `StorageRackTests` (iiex) - 66 cases |
>
> ⛔ **The renderer is the one part no headless test can see.** It follows vanilla's
> `BlockEntityDisplay`: meshes are built on the **main thread** (`Initialize` and every content change),
> cached per load, and `OnTesselation` only adds them with a transform - because resolving a stored item's
> texture may insert it into the block atlas and the tesselation pass runs on a chunk worker. The rack's
> own frame is left to the default block mesh, so `OnTesselation` returns `base`, not `true`. What is
> checked headlessly is only the centring math (`CentreOf`).
>
> ⛔ **The shipped occupancy numbers are a starting table**, grounded in `StockForm.BaseLength` rounded up
> to whole cells, not measured in game. `caststock-slab` is the only 3-cell entry, so it is the only item
> that exercises the whole-rack arrangement today.
>
> **Both numbers are config, not constants.** Cell count is the blocktype's own footprint (so a longer
> rack is another blocktype, not a code change), and per-item occupancy is a JSON table. That widens the
> page's "a rack is not a chest" rule: the rack takes what its catalogue lists, at the length the
> catalogue declares, rather than what a hardcoded stock-code prefix matches. Piece size is still
> **declared, never measured** off a drawn bounding box.

**Owns**
* the 1 × 1 × 3 wooden rack: its footprint, its capacity-by-layer rule and the numbers that fall out of it,
  its LIFO access, its vertical stacking, and its block-info readout;
* the rule that it is the one block in the suite that must be cheap;
* the requirement that pile placement become `StockPile.Place` in exlib, shared with the reheat hearth's bed -
  the rack is that code's second consumer and the reason it should not be a furnace detail;
* the fact that a rack stores but does not reheat, and is therefore not an escape from the recoverability
  invariant.

**Depends on**
[recoverability](../mechanics/recoverability.md) (owns the ≤ 48 limit the rack's 3-cell length is sized to,
and the reachability/LIFO argument the rack reuses) ·
[multiblock & fillers](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md) (the footprint DSL, filler interaction rerouting,
`allowAttach`, and why this needs no behaviour-capable cell) ·
[reheat furnace](reheat-furnace.md) (the hearth whose bed composition this shares) ·
[rolling mill](rolling-mill.md) · [shear](shear.md) (what fills the rack) ·
[density rule](../mechanics/density-rule.md) (every unit total below) ·
[recipes & config](../mechanics/recipes-config.md) · [rolling](../processes/rolling.md)

---

## Role

A store for the suite's bulk output: 3000 u slabs, 7500 u casting beds, six-mill halls.

It is the one block that must be cheap. Every other block trades complexity in the build for efficiency in the
operation; a rack returns no efficiency. It is limited by the space given to it, not by its price, and its
cost must never compete with the machines. A few planks.

The pile is the readout. A rack of bars looks like a stack of bars; a rack of slabs looks like a few enormous
slabs. Same block, and the silhouette reports what the factory has been making - the "nothing is hidden" rule
satisfied by geometry rather than by a number.

Three cells is 48 voxels, exactly the longest piece that legally exists. The number fell out of the reheat
furnace's footprint ([recoverability](../mechanics/recoverability.md)); a 3-long rack therefore holds anything
that can be owned, and no piece can ever be homeless.

A rack stores; it does not reheat. A piece that cannot be reheated is stranded whether or not there is
somewhere to put it down.

---

## Structure

1 × 1 × 3 megablock: a principal plus two fillers, laid along the block's facing axis.

| Aspect | Proposal | file:line |
|---|---|---|
| Footprint | `StructureFootprint.Rectangle(0, 3)` - depth 3 rows along +Z, one column, principal skipped, `AllowAttach: false` on both fillers | `StructureFootprint.cs:51-69` |
| …or equivalently | `StructureFootprint.Layout(f => f.Origin(0, 0).Layer(0, """0\n#\n#"""))` | `:78-83`, `FillerLayoutBuilder.cs:81` |
| Base class | `BlockFilledMegastructure` - folds the `CanPlace` → `PlaceFillers` → `RemoveFillers` triad | `BlockFilledMegastructure.cs:31`, hook at `:83` |
| Orientation | `side` variant via `HorizontalOrientable`, `StructureAngle => ExOrientation.AngleFromSide(Variant["side"])` | `BlockHeatingHearth.cs` |
| Interaction | `IFillerHost` + `IFillerInteractionTarget`, so any of the three cells works the rack | `BlockPuddlingHearth.cs:25-30` is the template |
| Multiblock | none - a rack is placed, not built. No `MultiblockStructure` behaviour, no projection, no completion tick | - |

It does not need behaviour-capable fillers. `Host(ch, …specs)` (`FillerLayoutBuilder.cs:69-77`) exists so a
cell can carry a real block-entity behaviour - an MP port, a molten cell. A rack hosts none. Plain fillers
already reroute interaction, breaking, drops, pick-block, look-at info and sounds to the principal
([multiblock § invisible fillers](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md)), which is the entire requirement.

Racks stack vertically, so shelving is just racks - see the `allowAttach` trap in Gotchas.

---

## Assets

Nothing is drawn.

| Asset | State |
|---|---|
| editable shape | `workbench/shapes/machines/machine-megablock-storagerack.json` - drawn 2026-08-21, 47 elements, three bays along -Z at z 0..16 / -16..0 / -32..-16, X -2..18 (2 vx of bracket overhang each side, no footprint widening), Y 0..16 |
| runtime shape | `mods/iiex/assets/iiex/shapes/storage/storagerack.json` - exported; both texture keys (`generic`, `iron5`) already mapped in `convert-shape.py` |
| textures | plain vanilla planks; no new texture needed |
| animations | none, ever - a rack is static and only its contents change, exactly like the reheat hearth (`BlockEntityHeatingHearth.cs:97-98`) |
| lang / handbook | no key in `mods/iiex/assets/iiex/lang/en.json`, no page in `mods/iiex/docs/handbook/` |

The rack's mesh is two things: a plank frame, and a composed pile. The frame is the only authored art. The
pile is not authored at all - it is composed from each stored item's own shape, which is what makes the rack
accept forms nobody has drawn a rack variant for. That is the same job the reheat hearth's bed does, and it is
why the two share one implementation:

```
for each occupied slot:
    shape = the stock item's own shape asset
    mesh  = TesselateShape(Pruned(shape, [stageElement]))   // the SAME literal the held item uses
    mesh.Translate(SlotOffset(layer, index))                 // local space
    combined.AddMeshData(mesh)
ExMesh.RotateByShape(combined, Block)                        // ONE rotation, at the end
```

Every step of that already exists: `ExShapeElements.Pruned` selects a named subtree, `ExMesh.RotateByShape` is
the single final rotation, and `BlockEntityHeatingHearth.OnTesselation` (`:99-126`) is a working example of
load-shape → prune → tesselate → rotate → `AddMeshData`, returning `true` so the default block mesh is not
also drawn.

Textures resolve dynamically - never hardcode the key set. A rack is generic enough that anything may end up
on it, including another mod's stock. Not via `ITesselatorAPI.GetTextureSource(Item)`: that returns item-atlas
UVs, and a mesh handed to `ITerrainMeshPool` must carry block-atlas ones. The correct call is the five-arg
`ShapeTextureSource`, whose override dictionary carries the itemtype's `texturesByType` so a steel bar renders
steel off the same shape file an iron one uses. The in-repo precedent is `BlockMoltenBarrel`
(`Blocks/BlockMoltenBarrel.cs:190`), which inserts an arbitrary metal's texture into the block atlas at
runtime with `GetOrInsertTexture` and caches a base mesh it then `Clone()`s.

---

## Construction

No recipe. Proposed, and the cost is a design statement rather than a placeholder:

| Slot | Ingredient | Rationale |
|---|---|---|
| `P` | `game:plank-*` ×4 | planks only. No metal, no tool, no nails. A rack must never compete with a machine for materials |
| output | 2 racks | so the player builds a wall of them without thinking about it |

Precedent for a trivial cost is the design table - "planks and candles, no metal and no tool"
(`CraftingStationRecipeDefinitions.cs:15-20`).

Cost key `stockrack-grid` in `IiexRecipeConfig.DefaultCatalogue`. Whatever `RecipeLevel` does to it, it must
stay cheap at the expensive end too.

---

## Operation

| Verb | Effect |
|---|---|
| RMB with stock on any cell | lay one piece on the top layer |
| RMB empty on any cell | take the top piece back |
| look at it | block info: what is on it and the running unit total |

LIFO, and it needs no rule of its own: a pile cannot be reached under. This is the same reachability argument
the reheat hearth makes for its centre row (`HearthRows.CanReach`, `HearthRows.cs:75-76`; the hearth refuses a
flank draw while the centre is loaded, `BlockEntityHeatingHearth.cs:77-83`) and the same argument the
crosswise seating makes for far-row-first ([recoverability](../mechanics/recoverability.md)).

Capacity is by layer, not by slot. A layer holds as many pieces as fit across the rack's width, and layers
stack:

```
piecesPerLayer = floor(rackWidth / pieceWidth)     rackWidth = 16 voxels (one cell across)
capacity       = piecesPerLayer × layers
```

Two stacking modes, and the chooser already exists:

| Mode | Layers | Step | Reads as |
|---|---|---|---|
| pyramid | `n, n−1, n−2 …` | x offset ½ width per layer, y step 0.866 × height | billets, bars, blooms nesting |
| flat | one per layer | y step = full height, plus yaw and xz jitter | slabs, plates, beams stacked |

The half-width offset and the 0.866 y-step are what make a pyramid read as nested - each upper piece settling
into the valley between two below - rather than as a grid with a gap. The mode is not a new axis: it is the
section class. Square sections nest and are rolled on grooved sets (`w = t`); flat sections stack and are
rolled flat. The single `square | flat` property the roll sets need anyway drives the deformation law and the
shape of the pile.

---

## Numbers

All proposed. Nothing here exists in code: `grep -rn "StockPile\|HearthPile" --include=*.cs src/` returns nothing.

| Key | Proposed | file:line | What it does |
|---|---|---|---|
| rack length | 3 cells | design - sized to [recoverability](../mechanics/recoverability.md)'s ≤ 48 | 3 × 16 = 48 voxels; holds anything that can legally exist |
| `RackWidth` | 16 voxels | - | one cell across; the divisor in `piecesPerLayer` |
| `RackLayers` | 5 | - | the height of a full pile before the rack refuses |
| pyramid x-offset | ½ piece width per layer | - | nesting |
| pyramid y-step | 0.866 × piece height | - | `√3/2` - pieces settling into the valley below |
| flat y-step | 1.0 × piece height | - | stacking |
| jitter | `GameMath.MurmurHash3(pos, layer, index)` | - | reproducible, never an RNG (see Gotchas) |

Worked capacity, applying the rule to the settled ladder (piece widths and masses are owned by [density
rule](../mechanics/density-rule.md) and [rolling](../processes/rolling.md)):

| Stock | Width | Per layer | 5 layers | Total |
|---|---|---|---|---|
| `shingledbar` | 3 | 5 | 25 | 10 000 u |
| `castbillet` | 3 | 5 | 25 | 15 000 u |
| `castbloom` | 4 | 4 | 20 | 20 000 u |
| `shingledslab` | 8 | 2 | 10 | 12 000 u |
| `castslab` | 12 | 1 | 5 | 15 000 u |

The one real constant that exists today is the hearth's, and it is not the rack's:
`HeatingHearthLayout.Rows = 3` (`HeatingHearthLayout.cs:31`) - three rows, one piece each. The rack does not
copy that model; see Gotchas.

---

## Drops

| Broken | Returns |
|---|---|
| any cell | the rack and every piece on it |
| the fillers | nothing of their own - `BlockStructureFiller`'s drops are always `[]`, the principal owns all drops ([multiblock](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md)) |

This has to be written explicitly. Breaking any cell breaks the whole megablock (the filler reroutes
getting-broken and broken to the principal), so the principal's `OnBlockBroken` must spawn the contents before
calling base - exactly as the mill spawns its jammed piece and its roll set
(`BlockEntityRollingMill.cs:374-383`). A rack that eats 15 000 u of slabs on a misclick is the worst bug this
block can have, and it is one missing line.

---

## Code

⛔ **Built 2026-08-21 - the table below is the pre-build proposal and its names are not the ones that
shipped.** `StockPile.Place` became exlib's `BayLayout` (length, not piles), `BlockStockRack` became
`BlockStorageRack`, and the whole `HearthRows`-versus-layers discussion is moot. Kept because the
reasoning about *where* each piece belongs still holds. See § As built for what exists.

Where it hooks in:

| Piece | Where | Model it on |
|---|---|---|
| `BlockStockRack` | `mods/iiex/src/BlockStructures/Forming/Blocks/` (or a new `Storage/`) | `BlockPuddlingHearth.cs:25-62` - `BlockFilledMegastructure` + `IFillerHost` + `IFillerInteractionTarget` + `IExBlockDefProvider`, with `.FillerOffsets(...)` in the def and `StructureAngle` from the `side` variant |
| cell → slot routing | `RowAt`-style: rotate the world offset back into the block's frame before reading it | `BlockPuddlingHearth.cs:70-77`, `BlockHeatingHearth.cs` (`ExOrientation.RotateOffset(world, -StructureAngle)`) |
| `BlockEntityStockRack` | `.../BlockEntities/` | `BlockEntityHeatingHearth.cs:26-174` end to end: the slot array (`:30`), `TryLoad`/`TryTake` (`:57`, `:73`), `Changed()` marking the block dirty client-side (`:86-91`), `OnTesselation` (`:99-126`), per-slot `SetItemstack` persistence with `ResolveBlockOrItem` (`:132-154`), and the block-info readout (`:160-171`) |
| `StockPile.Place` | `exlib/src/` - not iiex | the hearth composes a pile of stock in a firebox, the rack composes a pile of stock on planks. Same computation, so it gains a second consumer before it is built and stops being a furnace detail |
| its inputs | `(item shape, stage element, mode, slot index, layer)` → a `Vec3f` offset + yaw | pure, therefore testable headless - the house style (`StockMesh.SideOf`, `StockMesh.cs:36`) |
| mesh cache | one entry per `(form, stage)`, then `Clone()` → rotate about its own centre → translate to the slot | `StockMesh.CacheKey` (`:67-73`) is the existing key function; `BlockMoltenBarrel.cs:190` is the runtime-texture + cached-base-mesh precedent |
| def + recipe | `IExBlockDefProvider.Definitions(domain)` + an `ExRecipeDef` grid | `BlockHeatingHearth.cs`, `CraftingStationRecipeDefinitions.cs:23-36` |

Where a caller hooks in: nothing needs to know about the rack. It accepts any item whose shape can be pruned
to a stage element and whose textures resolve. The one contract is the recognition predicate - today the
hearth uses a code-prefix match (`HeatingHearthLayout.StockOf`, `:64-79`, matching `stock-shingledbar`, `stock-shingledslab`,
`castbillet`, `castbloom`, `castslab`), which is the part that should become an attribute test so another
mod's stock qualifies without an iiex edit.

---

## Gotchas

- `HearthRows` cannot be reused, and must not be. It is a fixed `Left/Centre/Right` enum with a hard-coded
  reachability rule (`HearthRows.cs:20-25`, `:75-76`), and `BlockEntityPuddlingHearth` already sizes its
  arrays off `HeatingHearthLayout.Rows` - so touching that model touches the puddling furnace. The rack wants
  layers × index, which is a different shape entirely. Share `StockPile.Place`, not `HearthRows`.
- Stock items are `MaxStackSize(1)` (`StockItemDefinitions.cs:42`) because each piece carries its own state -
  per-side thickness, temperature. So a rack holds N distinct stacks, never a count with a quantity. Merging
  them silently destroys work-piece state.
- Vertical stacking needs `allowAttach`. Footprint fillers default to `allowAttach: false`
  (`StructureFootprint.cs:33`, and `Rectangle` only opts in flanking columns, `:64`), or torches and vines
  hang on the invisible footprint. But "racks stack vertically, so shelving is just racks" means the next
  rack's principal must land on a cell that accepts it. Either the player always places on the principal, or
  the two filler cells opt in. Decide this before the footprint is written; it is a one-flag decision that is
  invisible until a player tries it.
- Jitter must be reproducible or the pile jumps on every re-tesselation. Derive it from
  `GameMath.MurmurHash3(pos, layer, index)` - two racks differ, one rack is stable forever, and the function
  stays pure and testable.
- Cache the piece, transform the copy. Baking jitter into the cached mesh gives one entry per world position ×
  slot - unbounded.
- Translate before the single final rotation, or each slot rotates about its own origin instead of the rack's
  (`ExMesh.RotateByShape(mesh, Block)` last - `BlockEntityHeatingHearth.cs:121`).
- Atlas insertion is main-thread only. `GetOrInsertTexture` mutates the atlas while `OnTesselation` runs on the
  chunk worker. The tesselation pass may only read the cache; on a miss, skip the piece, queue the build with
  `capi.Event.EnqueueMainThreadTask` and `MarkBlockDirty` when it lands. That is why the cache belongs to the
  mod system, not to the block entity.
- `ResolveBlockOrItem` after reading a stack off the tree, or the piece has no `Collectible` and silently
  fails to draw - the hearth documents exactly this symptom in-source
  (`BlockEntityHeatingHearth.cs:148-150`).
- Return `true` from `OnTesselation` when the BE draws the whole mesh, or the default block mesh is drawn as
  well (`BlockEntityHeatingHearth.cs:122-125`).
- A rack is not a chest. It must refuse anything that is not stock, for the same reason the hearth does:
  "a furnace is not a chest, and stock it cannot draw is stock it cannot reheat"
  (`BlockEntityHeatingHearth.cs:53-56`). Here the failure is worse - a rack that accepts arbitrary items has
  to render arbitrary items.
- `StockForm` has only two forms today, at the wrong dimensions - `Bloom` 3 × 3 × 16 and `Slab` 8 × 3 × 20
  (`StockForm.cs:48`, `:54`) - so the capacity table above cannot be computed from code yet.

---

## Open

Everything above the amendment describes the page as it was written on 2026-08-15; the block shipped on
2026-08-21 and what is genuinely still open is this.

- ⛔ **The renderer has never been seen.** It is written - vanilla's `BlockEntityDisplay` pattern, meshes
  built on the main thread and cached per load - but nothing headless can check what it draws, only where
  it centres a run (`CentreOf`). First in-game look is the acceptance test.
- ⛔ **The occupancy numbers are a starting table.** Grounded in `StockForm.BaseLength` rounded up to whole
  cells, not measured. `caststock-slab` is the only 3-cell entry, so it alone exercises the whole-rack
  arrangement; whether a rack of it reads as one slab or as a filled shelf is an in-game question.
- **Item height is not modelled.** A run is a length and nothing else, so a piece is drawn at the rails'
  height whatever its section. If a slab and a rod need to sit differently the transform grows a per-item
  y, which is another catalogue column rather than a code change.
- **The recognition test is the catalogue**, which answers the old question about `stockForm` /`rackable`
  attributes: another mod qualifies its stock by shipping a `config/bayoccupancy/` file, and iiex is not
  edited. What is untested is whether a *foreign* item's shape actually renders, since only iiex items are
  listed today.
- **No handbook page.** `HandbookParityTests` is one case per page that exists, not one per block, so
  nothing failed - the page is simply missing. Lang keys and goldens did land.
- **Does the rack keep stock hot?** It does not, and nothing was added to make it: heat is the reheat
  furnace's job and a rack that slowed cooling would become the escape this page says it is not. Drawing
  a glow while a piece cools normally is still an option nobody has taken.
- **Vertical stacking is untested in play.** `allowAttach` is on for both filler cells (owner, 2026-08-21),
  which is what lets a rack land on any cell of the one below - but also what lets a torch hang on an
  invisible cell. Both halves want an eye.
