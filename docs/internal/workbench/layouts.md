# Multiblock layout scratchpad

**This file is a workbench, not a source of truth.** Layouts are drafted and edited here by hand before
they reach C#, and the shipped ones are copied back so there is one place to read them all side by side.

**Nothing generates this file, and nothing should.** A generator would overwrite work in progress, which
is the opposite of what the file is for.

**The goldens are the truth.** `test/*/goldens/*/blocktypes/**` is what actually ships; a copy here is a
convenience snapshot taken by hand at some point, and can be stale. If a copy and its golden disagree, the
golden is right.

The file is split **by mod**, with a status word per layout, because "which mod owns this" is the question
that comes up while drawing and "is it in C# yet" is a one-word answer.

| Status | Means |
|---|---|
| shipped | in C#, has a golden. The copy here is a snapshot |
| draft | drafted here only — **not** in C# |
| stale | in C# but in an older form, or known to disagree with its copy |

---

## How to read a layout

### The grid

| | |
|---|---|
| `Origin(xLeft, zTop)` | where the top-left glyph of every grid sits. Cell `(col, row)` → `X = xLeft + col`, `Z = zTop + row` |
| `Layer(y, grid)` | one top-down grid per Y level; rows run **+Z**, glyphs run **+X**. Any Y order |
| `.` | empty cell — advances the column, requires nothing |
| *space* | separator, ignored. Grids are spaced out purely for reading |
| `@(a\|b)` | alternation — any one of these codes satisfies the cell. A **regex over the path only**, so it is implicitly `game:` unless a `*:` domain wildcard is written in front |

> Caution: **`Origin` is the negation of the core glyph's `(col, row)`**, so the core `C` lands on the
> anchor's own `(0, 0, 0)`. Getting this wrong builds the whole structure offset from the block the player
> placed — the single most error-prone thing about this DSL, and it has been got wrong three times.

### Codes

Legend codes come from a named table rather than being retyped. A typo in a raw string does not throw at
compile, at load, or at first placement — it becomes a `blockNumbers` entry matching nothing, and the
structure simply can never complete. Referenced by name, a typo is a compile error.

* `ExCodes` — vanilla and `exlib:` codes (`ExpandedLib.Definitions`)
* `IwexCodes` — the `iwex:` codes more than one layout draws
* `LpexCodes` — the `lpex:` pipe fittings

A code drawn by exactly one layout, for a block that layout owns (`iwex:puddlingchimneycap-north`), stays
inline — it reads as part of the drawing.

**The brick codes are a ladder of permissiveness.** Which rung a layout picks is a *gameplay* decision — how
much the furnace dictates the player's material — so it belongs on the drawing rather than inside a wildcard:

| Rung | Admits |
|---|---|
| `ExCodes.RefractoryTier(3)` | that tier only. The hot blast furnace is the one shell that pins its material |
| `ExCodes.Refractory` | refractory brick, any tier |
| `ExCodes.RefractoryOrFire` | …or fire brick. Between them, exactly what vanilla marks `cokeOvenViable` |
| `ExCodes.ColouredBricks` | any `brickcourse`, any bond, any colour |
| `ExCodes.AnyBricks` | any brick a wall can be built from — fire, clinker, refractory, coloured course |

`ExCodes.FireSlab(facing)` ⊂ `ExCodes.AnySlab(facing)`, and the same for `FireStairs` / `AnyStairs`.

None of these admit **damaged** refractory, which declares `sidesolid: false` — a structure built from it
would not hold up.

### Oriented parts

A legend code containing a **whole side segment** — `north`/`south`/`east`/`west`, or the single letters
`n`/`s`/`e`/`w` — is orientation-checked: the required facing is rotated with the structure, so the part must
be placed the right way round rather than merely be present. Authored in the north-default frame, like
everything else.

```csharp
.Legend('i', ExCodes.FireSlab(BlockFacing.SOUTH))  // south when the build faces north,
                                                   // east when it faces west, and so on
.Legend('-', ExCodes.FireSlab(BlockFacing.UP))     // vertical: never rotates
.LegendAnyFacing('x', "…-north")                   // opt out, accept any rotation
```

**The facing has to be in the string.** That is what makes the cell checked at all — which is why the
helpers interpolate it rather than taking it as an inert parameter.

**The last whole side segment wins.** Stairs carry two orientation groups
(`brickstairs-fire-up-south-free`); scanning from the front would orientation-check `up`, rotate it to a
cardinal, and produce a code matching no block.

Wildcards are unaffected — `-*` still means "any variant", and a `*` *before* the facing
(`brickslabs-*-south-free`) still leaves the cell checked.

**Trapdoors cannot be checked this way.** `game:trapdoor` keeps its facing *and* its open/closed state in
the **block entity**, not in the block code, so no code match can see it. Slabs, stairs, doors and coke-oven
doors all carry theirs in the code and do work.

### Roles

A layout records the **code** per cell — what may occupy it. A **role** records the **purpose** — what the
cell is *for* — so a machine can ask its own drawing "where are my tuyeres?" instead of carrying a second,
hand-written offset list beside the drawing it was copied from. Those hand-written lists drifted; this is
what replaced them.

Roles are **orthogonal to codes**: a cell can be `Flue` and hold air, and most cells have a code and no role
at all.

| Role | Meaning | Arity |
|---|---|---|
| `Chargeable` | a cell of the **burden column** — where a charge pile may stand. Its bounds *are* the shaft box | set |
| `Firebox` | a cell of a **fuel bed** — the reverberatory hearth's firebox | set |
| `Tuyere` | a blast intake — where a tuyere stands and air enters | set |
| `GasOutlet` | where combustion gas leaves into a pipe or stove | set |
| `MetalTap` | where liquid metal leaves the hearth | **one cell** |
| `SlagTap` | where slag is skimmed, above the metal tap | **one cell** |
| `Pool` | a liquid pool — the hearth bath, and where metal freezes if untapped | set |
| `Flue` | a cell of the **stack column**, the draught path out. Cannot be inferred: a flue cell is air, and so is every other empty cell | set |
| `Damper` | a cell that throttles the flue | set |

**The rule for what earns a role:** a role exists only where code asks the layout *"where are my X cells?"*.
A block that finds its own core — a charge door, a hopper, a hearth, a filler, the core itself — needs none,
because that lookup runs the other way. Deliberately absent for that reason: `Core`, `Filler`, `ChargeDoor`,
`Hopper`, `Hearth`, and `ShaftCentre`.

#### The taps split in two *(done 2026-08-03)*

**A role never replaces a block**, and the shaft layouts used to be the confusing case: they drew `T` and
`S` on the *same* code and told them apart only by role, which read as though the roles had taken over from
two separate tap blocks. They had not — there was only ever **one** tap block, and before roles existed the
two taps were told apart by a pair of hand-written `Vec3i` literals in the block entity. The second glyph was
what made one block expressible as two purposes.

**That has collapsed.** `iwex:furnace-irontap` and `iwex:furnace-slagtap` are real blocktypes, each glyph
carries its own code, and the roles are back to being the *lookup* (`MetalTapPos` / `SlagTapPos` still ask
the drawing where they are) rather than identification:

```csharp
.Legend('T', IwexBlocks.FurnaceIrontap.Any)   // was: two glyphs on one code
.Legend('S', IwexBlocks.FurnaceSlagtap.Any)
.Role('T', CellRole.MetalTap)                 // unchanged - the consumer still reads the drawing
.Role('S', CellRole.SlagTap)
```

Both are still needed. The **code** says what block must be there; the **role** says what the furnace does
with that cell. With one block they were doing double duty; with two they are orthogonal again.

**The gain is an oracle, not tidiness.** A cell could previously hold either tap and complete the
structure, so `CellsAccepting` could only be played off the *union* of the two roles and a drawing that
swapped them was invisible. Each role is now pinned to its own code
(`FurnaceRoleCellsTests.Each_tap_role_is_exactly_the_cells_its_own_tap_block_may_stand_in`).

**The two still share one shape.** The drawn art (`furnace-block-{iron,slag}tap.json`) puts the height
difference in the *geometry* — iron spouts from 2/16, slag from 10/16 — which only reads correctly once both
taps sit at y=1, and today's drawings still stack them a course apart. Adopting the shapes belongs to that
layout move; see [blast-furnace-cold](../../design/machines/blast-furnace-cold.md).

#### The tuyeres are orientation-pinned *(done 2026-08-03)*

A tuyere is walled in on three sides, so its cell admits **exactly one** connector face, and
`BlockNetworkNode.RecalculateAndSyncOrientations` exchanges a placed node onto that face as soon as the brick
goes up. The drawing can therefore state it, and `MultiblockFacings` rotates the letter with the structure —
single direction letters rotate, not just the full side words (`ExOrientation.IsHorizontalSideWord`).

```csharp
.Legend('Y', IwexBlocks.FurnaceTuyere.WithOrientation("n"))   // the north wall's inlet
.Legend('y', IwexBlocks.FurnaceTuyere.WithOrientation("s"))   // the south wall's
.Role('Y', CellRole.Tuyere)
.Role('y', CellRole.Tuyere)
```

**Two glyphs for one block again — but for the opposite reason to the taps.** There the glyphs differed
because the blocks did; here one block wears two *orientations*, and a legend is one code, so each wall needs
its own glyph. The cupola is blown from one wall and needs only `Y`.

**A test asking about a pinned cell must rotate its expectation.** A west-facing furnace wants
`iwex:furnace-tuyere-e` in the cell drawn `n`; asking with the authored letter finds nothing and passes
vacuously.

#### The two glyph idioms

**A role attaches to the glyph, not the code.** One code routinely serves several purposes in one drawing
— `game:air` is the vent shaft, the flue *and* the tap alcove — so "the air cells are the flue" is not a
statement this DSL can make. Two idioms follow, and both are used below:

1. **A second glyph on the same code, for disjoint roles or differing orientations.** The taps used to be
   the example and no longer are — they are two blocks now. What still uses it is the **tuyere pair**: one
   block, but the north wall's inlet and the south wall's want opposite connector faces, so each needs its
   own glyph. Several glyphs sharing one code share one block number; two glyphs on two codes (the taps) get
   one number each.
2. **One glyph carrying several roles, for overlapping ones.** The shaft furnaces' crucible floor is burden
   *and* pool — `p` is `Chargeable` **and** `Pool`. A cell holds exactly one glyph, so overlapping roles
   cannot be split across two of them. Distinct roles accumulate; there is no last-writer-wins.

#### Guards, and what they cost you

* **`Chargeable` XOR `Firebox`.** A furnace holds a burden column or a fuel bed, never both — that is the
  shaft/firebox class split. The builder refuses a layout claiming both.
* **`[SingleCell]` promises "at most one", never "exactly one".** A layout that declines to declare the
  role draws none, which is legal and frequently the truth — **both** reverberatory hearths mark neither tap,
  because a puddling furnace is cleaned rather than tapped. So a consumer must be **nullable, not
  `.Single()`**. Writing `.Single()` turns a silent no-op into an exception thrown once per frame the player
  looks at the block.
* A role glyph the drawing never uses is a **build error**, not a silent empty set.
* A role a drawing declines to mark has no cells and therefore **no bounding box** — so a bounds consumer
  is nullable for the same reason.

---

# iwex — Ironworking Expanded

## Cold blast furnace — `iwex:blastfurnacecore` *(shipped)*

> Caution — **the drawing below is stale; read `BlockBlastFurnaceCoreCold.cs` instead** *(flagged
> 2026-08-05)*. The grids still draw `Y` at **layer 1**, the pre-move position, and some legends do not
> resolve (`ExCodes.Refractory`, `IwexCodes.Tuyere`, `IwexCodes.HopperTall`, `ExCodes.ChargeShaft`,
> `ExCodes.Air` name nothing). In code the tuyeres sit at layer 2, `(0,2,-2)` / `(0,2,2)`, and the `P`
> passthrough legend is **deleted** — a passthrough existed only to reach a tuyere buried in the brick,
> and a tuyere on the wall face is itself the outermost cell.


```csharp
.MultiblockLayout(s =>
  s.Origin(-3, -2)
    .Legend('#', ExCodes.Refractory)
    .Legend('C', "iwex:blastfurnacecore-*")
    .Legend('T', IwexCodes.MoltenMetalTap)
    .Legend('S', IwexCodes.MoltenMetalTap)   // same block as T; own glyph only for the role
    .Legend('Y', IwexCodes.Tuyere)
    .Legend('H', IwexCodes.HopperTall(BlockFacing.NORTH))
    .Legend('f', ExCodes.Filler)
    .Legend('c', ExCodes.ChargeShaft)
    .Legend('p', ExCodes.ChargeShaft)        // same code as c; own glyph only for the Pool role
    .Legend('a', ExCodes.Air)
    .Role('c', CellRole.Chargeable)          // 38 cells. `a` is air too and is deliberately NOT
                                             // chargeable - it is the vent shaft above the stockline
    .Role('p', CellRole.Chargeable)
    .Role('p', CellRole.Pool)                // burden AND pool, today
    .Role('Y', CellRole.Tuyere)
    .Role('T', CellRole.MetalTap)
    .Role('S', CellRole.SlagTap)
    .Layer(0, """
              . . # . # .
              # # # # # #
              # # # C # #
              # # # # # #
              . . # . # .
              """)
    .Layer(1, """
              . . # . # .
              # # # Y # #
              . # # p p T
              # # # Y # #
              . . # . # .
              """)
    .Layer(2, """
              . . # # # .
              # # c c c #
              . S c c c #
              # # c c c #
              . . # # # .
              """)
    .Layer(3, """
              . . # # # .
              # # c c c #
              . # c c c #
              # # c c c #
              . . # # # .
              """)
    .Layer(4, """
              . . # # # .
              . # c c c #
              . # c c c #
              . # c c c #
              . . # # # .
              """)
    .Layer(5, """
              . . # # # .
              . # c c c #
              . # c c c #
              . # c c c #
              . . # # # .
              """)
    .Layer(6, """
              . . . # . .
              . . # # # .
              . # H a # #
              . . # # # .
              . . . # . .
              """)
    .Layer(7, """
              . . . . . .
              . . # # # .
              . . f a # .
              . . # # # .
              . . . . . .
              """)
    .Layer(8, """
              . . . . . .
              . . . # . .
              . . . a # .
              . . . # . .
              . . . . . .
              """)
)
```

The open top **is** the cold furnace's chimney — it takes no exhaust outlets, which is why it marks no
`GasOutlet`.

**Pending (layered charge).** The design moves `ShaftMin` to y=2 so the box and the charge volume become
identical at 36 cells, and makes the crucible **pool-only** — at which point `p` drops `Chargeable` and
nothing else moves.

## Cupola furnace — `iwex:cupolafurnacecore` *(shipped)*

```csharp
.MultiblockLayout(s =>
  s.Origin(-1, -1)
    .Legend('#', ExCodes.Refractory)
    .Legend('C', "iwex:cupolafurnacecore-*")
    .Legend('T', IwexCodes.MoltenMetalTap)
    .Legend('S', IwexCodes.MoltenMetalTap)
    .Legend('Y', IwexCodes.Tuyere)
    .Legend('H', IwexCodes.HopperTall(BlockFacing.WEST))
    .Legend('f', ExCodes.Filler)
    .Legend('c', ExCodes.ChargeShaft)
    .Legend('p', ExCodes.ChargeShaft)
    .Legend('a', ExCodes.Air)
    .Role('c', CellRole.Chargeable)   // five cells, one column - the cupola is the only furnace whose
                                      // shaft box IS its charge volume rather than merely containing it
    .Role('p', CellRole.Chargeable)
    .Role('p', CellRole.Pool)         // a SINGLE cell: the whole molten charge freezes into one block
    .Role('Y', CellRole.Tuyere)       // one tuyere, not two - the cupola is the narrow furnace
    .Role('T', CellRole.MetalTap)     // cast iron out low to the west…
    .Role('S', CellRole.SlagTap)      // …slag off the top of the bath to the east, a course up
    .Layer(0, """
              # # # #
              # C # .
              # # # #
              """)
    .Layer(1, """
              # Y # #
              T p # .
              # # # #
              """)
    .Layer(2, """
              # # # #
              # c S .
              # # # #
              """)
    .Layer(3, """
              # # # .
              # c # .
              # # # .
              """)
    .Layer(4, """
              # # # .
              # c H .
              # # # .
              """)
    .Layer(5, """
              # # # .
              # c f .
              # # # .
              """)
    .Layer(6, """
              . # . .
              # a . .
              . # . .
              """)
)
```

## Puddling furnace — `iwex:puddlingfurnacecore` *(shipped)*

A **reverberatory** megablock: the fire is beside the hearth, not under the charge.

```csharp
.MultiblockLayout(s =>
  s.Origin(-6, -1)                            // C is at col 6 / row 1
    .Legend('#', ExCodes.Refractory)
    .Legend('F', IwexBlocks.FurnaceFirebox.Any)        // the fuel BED - a required block
    .Legend('-', ExCodes.FireSlab(BlockFacing.UP))     // vertical: never rotates
    .Legend('i', ExCodes.FireSlab(BlockFacing.SOUTH))  // orientation-checked
    .Legend('K', ExCodes.CokeOvenDoor)
    .Legend('C', "iwex:puddlingfurnacecore-*")
    .Legend('H', "iwex:puddlinghearth-north")
    .Legend('D', "iwex:puddlingchargedoor-south")
    .Legend('M', "iwex:puddlingchimneycap-north")
    .Legend('f', ExCodes.Filler)
    .Legend('a', ExCodes.Air)
    .Role('F', CellRole.Firebox)              // a fuel BED, not a burden column
    .Layer(0, """
              # # # - - - # .
              - a # f H f C #
              # # # - - - # .
              """)
    .Layer(1, """
              # # # # # # # .
              # F # f f f a #
              # K # i D i # .
              """)
    .Layer(2, """
              # # # # # # # .
              # a a - # # a #
              # # # # f # # .
              """)
    .Layer(3, """
              . . . . . . # .
              . # # # # # a #
              . . . . . . # .
              """)
    .Layer(4, """
              . . . . . . # .
              . . . . . # a #
              . . . . . . # .
              """)
    .Layer(5, """
              . . . . . . # .
              . . . . f # a #
              . . . . . . # .
              """)
    .Layer(6, """
              . . . . . . # .
              . . . . f # a #
              . . . . . . # .
              """)
    .Layer(7, """
              . . . . . . . .
              . . . . . . M .
              . . . . . . f .
              """)
)
```

**The slab shoulders are load-bearing on the gameplay, not the fiction.** A half-height course opens the
mouth far enough that the player can reach all three hearth rows through it, which is what lets the hearth's
flanking filler cells be the interface — no split mesh, no guessing which row a click meant.

**No taps.** A puddling furnace is *cleaned* after a heat, not tapped; slag comes back with the fettle. So
neither `MetalTap` nor `SlagTap` is declared — legal, and the reason those consumers are nullable.

Vanilla ships no refractory-brick slab, so the shoulders are **fireclay** brick slabs against refractory
walls. Functionally fine, visually a slightly different brick.

Firebox cutover (2026-08-03): `c` (`@(air|coalpile)`) became `F` → `iwex:furnace-firebox-*-*` and the
`Firebox` role moved with it — so an *empty* firebox no longer satisfies the structure. `G` left with it: the
firebars are part of the block now, and the cell below is left as air (the ash pit that is deliberately not
modelled). Pending: the `a` column at col 6 still wants a `Flue` glyph once the chimney goes dynamic, and
`M` is the `Damper`.

## Heating / reheat furnace — `iwex:heatingfurnacecore` *(shipped)*

The same reverberatory arrangement as the puddling furnace, one row deeper.

```csharp
.MultiblockLayout(s =>
  s.Origin(-6, -2)                            // C is at col 6 / row 2 - one row DEEPER than the
                                              // puddling furnace, and the one line that must not be
                                              // copied between the two near-identical drawings
    .Legend('#', ExCodes.Refractory)
    .Legend('F', IwexBlocks.FurnaceFirebox.Any)
    .Legend('-', ExCodes.FireSlab(BlockFacing.UP))
    .Legend('i', ExCodes.FireSlab(BlockFacing.SOUTH))
    .Legend('K', ExCodes.CokeOvenDoor)
    .Legend('C', "iwex:heatingfurnacecore-*")
    .Legend('D', "iwex:chargedoor-south")
    .Legend('H', "iwex:heatinghearth-north")
    .Legend('f', ExCodes.Filler)
    .Legend('a', ExCodes.Air)
    .Role('F', CellRole.Firebox)              // TWO cells: this hearth is a row deeper and its
                                              // firebox runs the full depth beside it
    .Layer(0, """
              # # # - - - # .
              a a # f f f # #
              a a # f H f C #
              # # # - - - # .
              """)
    .Layer(1, """
              # # # # # # # .
              # F # f f f a #
              # F # f f f a #
              # K # i D i # .
              """)
    .Layer(2, """
              # # # # # # # .
              # a a - # # a #
              # a a - # # # #
              # # # # f # # .
              """)
    .Layer(3, """
              . . . . . . . .
              . # # # # # # .
              . # # # # # a #
              . . . . . . # .
              """)
    .Layer(4, """
              . . . . . . . .
              . . . . . . # .
              . . . . . # a #
              . . . . . . # .
              """)
    .Layer(5, """
              . . . . . . . .
              . . . . . . # .
              . . . . . # a #
              . . . . . . # .
              """)
    .Layer(6, """
              . . . . . . . .
              . . . . . . # .
              . . . . . # a #
              . . . . . . # .
              """)
)
```

Same firebox cutover as the puddling furnace (2026-08-03). The origin is `Origin(-6,-2)` — one row deeper
than the puddling furnace's, and the one line that must not be copied between the two near-identical
drawings. Pending: a dynamic chimney here is what lets the player *tune* the reheat temperature by how tall
they build the stack — which needs a `Flue` glyph on the `a` column.

## Draft crucible furnace — `iwex:draftcruciblefurnacecore` *(draft)*

**Not in C#.** Pots are lifted out with tongs and poured by hand into cast-iron ingot moulds — the furnace
itself never pours.

```csharp
.MultiblockLayout(s =>
  s.Origin(-1, -3)
    .Legend('#', ExCodes.Refractory)
    .Legend('b', ExCodes.AnyBricks)           // was "game:claybricks*" - the outer shell is the
                                              // player's choice of masonry
    .Legend('H', "iwex:draftcruciblefurnacehearth*")
    .Legend('C', "iwex:draftcruciblefurnacecore-*")
    .Legend('L', "iwex:chargelid-north*")
    .Legend('K', ExCodes.CokeOvenDoor)
    .Legend('a', ExCodes.Air)
    .Layer(0, """
              b b b
              b b b
              b b b
              b C b
              b b b
              """)
    .Layer(1, """
              a a a
              a a a
              b K b
              b a b
              b b b
              """)
    .Layer(2, """
              a a a
              a a a
              b b b
              b a b
              b b b
              """)
    .Layer(3, """
              # # #
              # H #
              # a #
              # a #
              # # #
              """)
    .Layer(4, """
              . . .
              . L .
              # # #
              # a #
              # # #
              """)
    .Layer(5, """
              . . .
              . . .
              . b .
              b a b
              . b .
              """)
)
```

Caution — roles are not authored, and this draft predates two decisions:

1. **There is no fuel cell at all.** The design now puts wrought-iron firebars in the hearth and a firebox
   under the pots — so a `F` cell and a `Firebox` role are missing, and the layout cannot be right without
   them.
2. **The flue needs its own glyph.** The `a` column running up col 1 from y=1 to y=5 is the stack, but `a`
   is also the open space at rows 0–1 of layers 1–2. `Flue` cannot be put on `a` without claiming both — so
   the column wants a second glyph on the same code, idiom 1 above. `NaturalDraughtFor(courses, damper)`
   needs it.
3. **The damper is not drawn.** The design calls for a flue-base bypass used to preheat.

## Beehive coke oven — `iwex:beehiveovencore` *(draft)*

**Not in C#.** A bank of two chambers sharing one wall.

```csharp
.MultiblockLayout(s =>
  s.Origin(-4, -1)                            // C is at col 4 / row 1
    .Legend('#', ExCodes.FireBricks)
    .Legend('-', ExCodes.FireSlab(BlockFacing.UP))
    .Legend('i', ExCodes.FireSlab(BlockFacing.SOUTH))
    .Legend('C', "iwex:beehiveovencore-north*")
    .Legend('D', "iwex:chargedoor-north*")
    .Legend('T', IwexCodes.HopperTall(BlockFacing.SOUTH))
    .Legend('L', "iwex:chargelid-south*")
    .Legend('f', ExCodes.Filler)
    .Legend('c', ExCodes.CoalBed)
    .Legend('a', ExCodes.Air)
    .Layer(0, """
              # # # # # # # # #
              # # # # C # # # #
              # # # # # # # # #
              # # # # # # # # #
              """)
    .Layer(1, """
              # i D i # i D i #
              # c c c # c c c #
              # c c c # c c c #
              # # T # # # T # #
              """)
    .Layer(2, """
              # # f # # # f # #
              # - a - # - a - #
              # - a - # - a - #
              # # f # # # f # #
              """)
    .Layer(3, """
              . # # # . # # # .
              . # # # . # # # .
              . # # # . # # # .
              . . L . . . L . .
              """)
)
```

Fire brick is exactly right here and vanilla agrees — `claybricks` carries
`cokeOvenViableByType: { "*-fire": true }`, i.e. it **is** the game's coke-oven material. Coke itself is
vanilla `game:coke`; we add no item.

Because the doors are `iwex:chargedoor` and not `game:cokeovendoor`, vanilla's own coking will not fire
inside these chambers. Deliberate — the core owns the bulk cycle. Swap to `ExCodes.CokeOvenDoor` only if we
ever want vanilla to run it.

Caution — roles are not authored, and the reason is a real gap: no existing role fits. The `c` cells hold the
coal being *coked* — it is the **workpiece**, not fuel for something else and not a burden column. So it is
neither `Firebox` nor `Chargeable`, and forcing either would be a lie the consumers then read. Either the
oven needs no role at all (it can walk its own chamber box), or the enum needs a `Retort`/`Charge` member.
**Undecided.**

The `K` legend was a leftover in an older draft — declared but drawn in no grid, which is now a build
error. Removed here.

---

# lpex — Low Pressure Expanded

## Cornish boiler — `lpex:boilercornish` *(shipped)*

```csharp
.MultiblockLayout(s =>
  s.Origin(-1, -2)
    .Legend('#', ExCodes.Filler)
    .Legend('L', "lpex:boilercornish*")
    .Legend('p', LpexCodes.PipePassthroughFire)
    .Legend('B', LpexCodes.PipePassthroughBendFireUp)
    .Legend('b', ExCodes.FireBricks)
    .Legend('a', ExCodes.Air)
    .Legend('c', ExCodes.CoalBed)
    .Legend('d', ExCodes.CokeOvenDoor)
    .Legend('o', LpexCodes.PipeOutletFireUp)
    .Layer(1, """
             . b .
             . b .
             # # #
             # # #
             # # #
             # # #
             . o .
             . b .
             """)
    .Layer(0, """
             b d b
             b c b
             # L #
             # # #
             # # #
             # # #
             b a b
             b b b
             """)
    .Layer(-1, """
              b p b
              b p b
              b B b
              b b b
              b b b
              b b b
              b b b
              b b b
              """)
)
```

**Pending.** The boiler is getting an **internal** firebox — its own shape redrawn to carry one — rather
than a separate `c` cell in the layout. When that lands the `c` glyph goes away entirely here.

---

# hpex — High Pressure Expanded

## Lancashire boiler — `hpex:boilerlancashire` *(shipped)*

The same setting as the Cornish, two rows longer. The pipe fittings are **lpex's** cast tier (hpex depends on
lpex), which is why those legends keep the lpex domain.

```csharp
.MultiblockLayout(s =>
  s.Origin(-1, -2)
    .Legend('#', ExCodes.Filler)
    .Legend('L', "hpex:boilerlancashire*")
    .Legend('p', LpexCodes.PipePassthroughFire)
    .Legend('B', LpexCodes.PipePassthroughBendFireUp)
    .Legend('b', ExCodes.FireBricks)
    .Legend('a', ExCodes.Air)
    .Legend('c', ExCodes.CoalBed)
    .Legend('d', ExCodes.CokeOvenDoor)
    .Legend('o', LpexCodes.PipeOutletFireUp)
    .Layer(1, """
             . b .
             . b .
             # # #
             # # #
             # # #
             # # #
             # # #
             # # #
             . o .
             . b .
             """)
    .Layer(0, """
             b d b
             b c b
             # L #
             # # #
             # # #
             # # #
             # # #
             # # #
             b a b
             b b b
             """)
    .Layer(-1, """
              b p b
              b p b
              b B b
              b b b
              b b b
              b b b
              b b b
              b b b
              b b b
              b b b
              """)
)
```

Pending: same internal-firebox change as the Cornish.

---

# smex — Steelmaking Expanded

## Hot blast furnace — `smex:blastfurnacecore` *(shipped)*

```csharp
.MultiblockLayout(s =>
  s.Origin(-3, -2)
    .Legend('#', ExCodes.RefractoryTier(3))   // tier 3 exactly - the hot blast leaves no
                                              // cheaper tier viable. The one shell that pins its material
    .Legend('C', "smex:blastfurnacecore-*")
    .Legend('T', IwexCodes.MoltenMetalTap)
    .Legend('S', IwexCodes.MoltenMetalTap)
    .Legend('Y', IwexCodes.Tuyere)
    .Legend('P', LpexCodes.PipeOutlet)
    .Legend('R', "smex:hopperreinforced*")
    .Legend('B', "smex:hopperbell*")
    .Legend('c', ExCodes.ChargeShaft)
    .Legend('p', ExCodes.ChargeShaft)
    .Legend('a', ExCodes.Air)
    .Role('c', CellRole.Chargeable)
    .Role('p', CellRole.Chargeable)
    .Role('p', CellRole.Pool)
    .Role('Y', CellRole.Tuyere)
    .Role('P', CellRole.GasOutlet)   // the only one of the three that vents. The cold furnace and
                                     // the cupola answer empty, which the drawing now states on all three
    .Role('T', CellRole.MetalTap)
    .Role('S', CellRole.SlagTap)
    .Layer(0, """
              . . . . # .
              # # # # # #
              # # # C # #
              # # # # # #
              . . . . # .
              """)
    .Layer(1, """
              . . . . # .
              # . # Y # #
              . . # p p T
              # . # Y # #
              . . . . # .
              """)
    .Layer(2, """
              . . # # # .
              # # c c c #
              . S c c c #
              # # c c c #
              . . # # # .
              """)
    .Layer(3, """
              . . # # # .
              # # c c c #
              . # c c c #
              # # c c c #
              . . # # # .
              """)
    .Layer(4, """
              . . # # # .
              . # c c c #
              . # c c c #
              . # c c c #
              . . # # # .
              """)
    .Layer(5, """
              . . # # # .
              . # c c c #
              . # c c c #
              . # c c c #
              . . # # # .
              """)
    .Layer(6, """
              . . . . . .
              . . # P # .
              . . # a # .
              . . # P # .
              . . . . . .
              """)
    .Layer(7, """
              . . . . . .
              . . # # # .
              . . # B # .
              . . # # # .
              . . . . . .
              """)
    .Layer(8, """
              . . . . . .
              . . . # . .
              . . # R # .
              . . . # . .
              . . . . . .
              """)
)
```

The shells differ from the cold furnace; the **shafts and the hearth plumbing do not** — same 38
chargeable cells, same tap cells, stated on this drawing rather than inherited.

## Hot blast furnace — the enlarged draft *(deferred)*

**Not shipped, and deliberately kept.** A wider furnace: a **4×4** shaft over eight courses instead of 3×3
over four, two tuyere *pairs* on opposite walls, and two outlet pairs. Recorded because it is a design
direction, not a stale copy — see [blast-furnace-hot](../../design/machines/blast-furnace-hot.md) Open #2 for the four
ways it would break the furnace as written.

**The mod codes in this draft do not resolve today — but they are not typos, they are the restructure.**
Two different things are mixed in here and it matters which is which:

| Code | What it is |
|---|---|
| `iwex:irontap-*`, `iwex:slagtap-*` | **built 2026-08-03**, though spelled `iwex:furnace-irontap-*` / `iwex:furnace-slagtap-*` — every furnace part shares the `iwex:furnace` code (N7). See [The taps split in two](#the-taps-split-in-two-done-2026-08-03) |
| `iwex:ironblock` | **planned** — the solidified pool. Needs renaming: the cell can now be *molten* too, so "ironblock" names only half of what it is |
| `-tier3-` on the taps and tuyeres | **fictional.** Neither carries a tier variant group, and adding one is not part of any current design |
| `lpex:pipe-outlet-refractorytier3-north*` | **fictional.** The outlet has no `refractorytier3` material |

So this draft needs re-legending before it could build, but most of that is *waiting for blocks*, not fixing
mistakes.

```csharp
.MultiblockLayout(s =>
  s.Origin(-3, -2)
    .Legend('#', ExCodes.RefractoryTier(3))
    .Legend('C', "smex:blastfurnacecore-north*")
    .Legend('M', "iwex:irontap-tier3-west*")                    // fictional
    .Legend('S', "iwex:slagtap-tier3-east*")                    // fictional
    .Legend('T', "iwex:tuyere-tier3-north*")                    // fictional
    .Legend('Y', "iwex:tuyere-tier3-south*")                    // fictional
    .Legend('P', "lpex:pipe-outlet-refractorytier3-north*")     // fictional
    .Legend('Q', "lpex:pipe-outlet-refractorytier3-south*")     // fictional
    .Legend('B', "@(smex:hopperbell|exlib:structurefiller)")
    .Legend('m', "@(game:air|iwex:ironblock|iwex:chargepile)")  // iwex:ironblock is fictional
    .Legend('c', ExCodes.ChargeShaft)
    .Legend('a', ExCodes.Air)
    .Legend('f', ExCodes.Filler)
    .Layer(0, """
              . # # # # # .
              # # # # # # #
              . # # C # # #
              . # # # # # #
              # # # # # # #
              . # # # # # .
              """)
    .Layer(1, """
              . # # # # # .
              # # # # # # #
              . S c c c c M
              . S c c c c M
              # # # # # # #
              . # # # # # .
              """)
    .Layer(2, """
              . # # Y Y # .
              # # c c c c #
              . # c c c c #
              . # c c c c #
              # # c c c c #
              . # # T T # .
              """)
    .Layer(3, """
              . # # # # # .
              # # c c c c #
              . # c c c c #
              . # c c c c #
              # # c c c c #
              . # # # # # .
              """)
    // 4 and 5 as 3, the latter losing its outer corners
    .Layer(6, """
              . . . # # . .
              . . # c c # .
              . # c c c c #
              . # c c c c #
              . . # c c # .
              . . . # # . .
              """)
    // 7 as 6
    .Layer(8, """
              . . . . . . .
              . . # Q Q # .
              . # # a a # #
              . # # a a # #
              . . # P P # .
              . . . . . . .
              """)
    .Layer(9, """
              . . . . . . .
              . . # # # # .
              . # # B B # #
              . # # B B # #
              . . # # # # .
              . . . . . . .
              """)
    .Layer(10, """
               . . . . . . .
               . . . # # . .
               . . # f f # .
               . . # f f # .
               . . . # # . .
               . . . . . . .
               """)
)
```

**`S` and `M` are drawn twice each**, which would fail the `[SingleCell]` guard on `MetalTap`/`SlagTap` the
moment roles were added — a *second* reason this cannot be adopted as drawn.

## Cowper stove — `smex:cowperstove` *(shipped)*

```csharp
.MultiblockLayout(s =>
  s.Origin(-1, 0)
    .Legend('#', ExCodes.Refractory)
    .Legend('I', "smex:cowperstove-intake*")
    .Legend('P', LpexCodes.PipeOutlet)
    .Legend('X', LpexCodes.PipePassthroughAny)   // any brick, unlike the boilers' fire-only
    .Legend('H', "smex:cowperstoveheatsink*")
    .Legend('D', ExCodes.CokeOvenDoor)
    .Legend('a', ExCodes.Air)
    .Legend('c', ExCodes.CoalBed)
    .Layer(-1, """
               # c #
               # # #
               # # #
               """)
    .Layer(0, """
             # I #
             D H #
             # P #
             """)
    .Layer(1, """
             # P #
             # H #
             # X #
             """)
    .Layer(2, """
             # # #
             # H #
             # # #
             """)
    .Layer(3, """
             # # #
             # H #
             # # #
             """)
    .Layer(4, """
             # # #
             # a #
             # # #
             """)
    .Layer(5, """
             . # .
             # # #
             . # .
             """)
)
```

**Pending a remake, not a migration.** The burning coal pile goes away entirely: a gravitational filter
on the exhaust converts it to **fuel gas**, which is what burns inside the stoves. That is how a cowper
actually worked. So `c` is not migrating to a firebox — it is being deleted.

That also closes the last hold-out: once the boilers take internal fireboxes and this loses its pile,
**nothing in the suite uses a vanilla coal pile**, and `Patches/CoalPileBlastmixPatches.cs` can be deleted
outright rather than carefully unhooked.

## Smokestack — `smex:smokestack` *(shipped)*

```csharp
.MultiblockLayout(s =>
  s.Origin(-1, 0)
    .Legend('#', ExCodes.Refractory)
    .Legend('I', "smex:smokestack-intake*")
    .Legend('a', ExCodes.Air)
    .Legend('B', ExCodes.AnyBricks)   // the courses are the player's choice of masonry
    .Layer(-1, """
               # # #
               # # #
               # # #
               """)
    .Layer(0, """
             # I #
             # a #
             # # #
             """)
    .Layer(1, """
             # # #
             # a #
             # # #
             """)
    // layers 2..10 all identical:
    .Layer(2, """
             . B .
             B a B
             . B .
             """)
    // …3, 4, 5, 6, 7, 8, 9, 10 the same
)
```

**Nine identical courses are hand-written in C#.** This is the layout that most wants a dynamic stack —
record the base and measure what the player built, rather than fixing the height in the footprint.

## Bessemer converter — `smex:convertercontrol` *(stale — coordinate form)*

**Coordinate form, to be replaced.** Still a hand-written `MultiblockBuilder` with explicit `.Number()` and
offsets rather than an ASCII layout. The grid below is the drafted replacement.

```csharp
.MultiblockLayout(s =>
  s.Origin(-1, 0)
    .Legend('A', "smex:convertercontrol*")
    .Legend('B', "smex:convertertransmission*")
    .Legend('E', "smex:converterbessemer*")
    .Legend('F', "smex:converter-intake*")
    .Legend('I', IwexCodes.MoltenCanalTap)
    .Legend('J', IwexCodes.MoltenCanalStart)
    .Legend('L', IwexCodes.MoltenCanalStraight)
    .Legend('#', ExCodes.Filler)
    .Layer(-2, """
              . . . .
              . . . .
              . . J L
              . . . .
              . . . .
              """)
    .Layer(-1, """
              . B . .
              # # # .
              # # # .
              # # # .
              . . . .
              """)
    .Layer(0, """
             . A . .
             # # # .
             # E # .
             # # # .
             . F . .
             """)
    .Layer(1, """
             . . . .
             # # # .
             # # I L
             # # # .
             . . . .
             """)
)
```

The canal codes are the one place a layout in another mod names iwex's molten network, which is why they
are in `IwexCodes` — a rename in iwex would otherwise break this with nothing between the two but a string.

---

## Structures with no layout

Not everything multiblock is drawn here. These are **filler footprints** (`StructureFootprint.Layout`) — a
mega-block covering its own cells — which is a different mechanism with no legend and no roles:

* casting bed, burdenmaker, and the boilers' own bodies
* gas producer — **no layout exists at all**, and no `MultiblockLayout` in `src/`
