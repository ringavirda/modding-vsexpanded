# Multiblock & Filler Structures
**Status** live   **Mod** exlib (the whole system; every mod authors layouts against it)
**Owns** the ASCII layout DSL (`Origin` / `Legend` / `Layer` / `Slice` / `Face`, the `'.'`/`' '`/`'O'` glyph rules, `@(a|b)` alternation), the origin-is-the-negation-of-the-core rule, oriented-part rotation and the trapdoor caveat, cell roles (what a cell is for, attached to glyphs), the invisible-filler footprint system (per-cell collision, interaction/break/info rerouting, `allowAttach`, partial collision boxes), behaviour-capable filler cells, the declarative filler port and the connector-versus-node choice, the completion/projection machinery, and the rule that a filler cell is a graph node exactly when it declares a membership.
**Depends on** [mp-energy](mp-energy.md) (the `BEBehaviorMPFillerPort` a hosted cell carries, and the rolling mill's axle cells - the canonical case of the graph-node rule) · [conventions](../conventions.md) (block / megablock / multiblock vocabulary) · [layouts-workbench.md](../../../workbench/layouts.md) (the layout scratchpad)

---

## Role

exlib solves two separate problems:

- **Megablock** - one block that renders across many cells. The engine resolves collision per cell, so
  the surrounding cells must be filled or the player walks through a boiler. Invisible filler blocks
  reserve the volume and forward every player-facing operation to the principal.
- **Multiblock** - a structure the player builds by hand in a specific shape (a furnace, a boiler
  chamber), guided by an in-world projection of what is missing. A declared cell → block-code table that
  a monitor tick checks against the world.

A block can be both, and several furnace cores are: a reserved footprint of its own plus a layout of
player-placed cells around it (`conventions.md`, § Block-size vocabulary). It is a choice, not a
progression - the Cornish boiler is a megablock and nothing else, because its own art carries the masonry
a layout used to demand ([Cornish boiler](../machines/boiler-cornish.md)).

Both DSLs replace hand-typed coordinate arrays with a drawing, validated at load. A duplicate offset, a
`w` with no `blockNumbers` entry, or an origin off by one is reported at load rather than failing silently
as a structure that never completes.

---

## How it works

### The drawing model

`StructureLayout` (`Definitions/StructureLayout.cs:20`) parses ASCII grids into `LayoutCell(X, Y, Z, Symbol)`
(`:6`). Three projections:

| Parser | Grid = | Rows run | Columns run | Origin pair | file:line |
|---|---|---|---|---|---|
| `Parse` | one Y level (floor plan, top-down) | `+Z` | `+X` | `(xLeft, zTop)` | `:24-52` |
| `ParseVertical` | one X level (side elevation) | `−Y` (down) | `+Z` | `(zLeft, yTop)` | `:61-89` |
| `ParseFrontal` | one Z level (front elevation, looking along `−Z`) | `−Y` (down) | `+X` | `(xLeft, yTop)` | `:99-127` |

Glyph rules, identical in all three (`StructureLayout.cs:13-18`):

| Glyph | Meaning |
|---|---|
| `.` | empty cell - advances the column, emits nothing |
| space / tab | separator, ignored entirely (grids are spaced for readability) |
| anything else | a cell carrying that legend symbol |

Leading and trailing blank lines are trimmed (`TrimBlankEnds`, `:131-139`) so a raw C# string literal does
not shift Z; a blank line in the middle is a genuine empty row.

`ParseFrontal` covers thin-in-Z structures whose face lies in the X-Y plane (a flywheel disc, a hammer
A-frame), which neither of the other two can draw in-plane (`:93-98`).

### The multiblock DSL

`MultiblockLayoutBuilder` (`Definitions/MultiblockLayoutBuilder.cs:20`), reached from
`ExBlockDef.MultiblockLayout(…)` (`Definitions/ExBlockDef.cs:796-810`).

```csharp
.MultiblockLayout(s =>
  s.Origin(-3, -2)                                        // MultiblockLayoutBuilder.cs:32
   .Legend('#', "game:refractorybricks-good-tier*")        // :48
   .Legend('C', "iiex:blastfurnacecore-*")
   .Legend('c', "@(air|coalpile)")
   .Layer(0, """
              . . # . # .
              # # # C # #
              . . # . # .
              """))                                        // :130
```

- **`Origin(xLeft, zTop)`** - where the top-left glyph of every layer sits. Cell `(col, row)` becomes
  `X = xLeft + col`, `Z = zTop + row`. Defaults to `(0, 0)` (`:32-37`).
- **`Legend(symbol, code)`** - maps a glyph to a block code, wildcards allowed. `'.'` and space are
  rejected as symbols, and a glyph may be declared only once. Codes go through oriented-part detection -
  see below.
- **`LegendAnyFacing(symbol, code)`** - same, but never rotates the code's side segment.
- **`Role(symbol, role)`** - optional; marks what a glyph's cells are for - see Cell roles below.
- **`Layer(y, grid)`** - one Y level; layers may be declared in any Y order.
- **`@(a|b)` alternation** - not an exlib feature. It is vanilla `WildcardUtil` regex-alternation syntax
  passed through untouched; matching happens in `WildcardUtil.Match`
  (`Blocks/Structures/BlockEntityMultiblockStructure.cs:419`). Used for `@(air|coalpile)` fuel cells
  everywhere, and for the smokestack's brick-family alternation
  (`BlockSmokeStackIntake.cs:53`).

`Build()` assigns each distinct legend code a private block number `w` starting at 1, in declaration
order, then feeds every parsed cell to `MultiblockBuilder`. An unknown symbol throws naming its coordinate.

> Numbering is per code, not per glyph (`MultiblockLayoutBuilder.cs:170-181`), so several glyphs may
> share one code - the thing cell roles depend on. It must stay per code because `blockNumbers` is a JSON
> object keyed by code: numbering per glyph would emit two numbers for two glyphs on one code but only
> one entry, the first silently overwritten. Every cell holding the lost number would then stop being
> required by `IncompleteBlockCount` (`WantedCodeAt` returns null and the cell is skipped) and make
> vanilla's own `InCompleteBlockCount` throw `KeyNotFoundException` (it does a bare `BlockCodes[w]`).

`MultiblockBuilder` (`Definitions/MultiblockBuilder.cs:16`) is the validating layer:

| Guard | Throws on | file:line |
|---|---|---|
| duplicate cell | two offsets at the same `(x,y,z)` | `:37-40` |
| unresolved block number | an offset whose `w` has no `blockNumbers` entry | `:90-97` |
| inverted `Fill` range | `x2 < x1` etc. - would silently emit zero cells | `:71-74` |

The emitted JSON is byte-identical to the hand-written form, so vanilla's `MultiblockStructure`
deserialiser reads it unchanged (`:13-14`). The game treats `offsets` as an unordered set and `w` as a
private index, so the generated table is behaviour-identical to any hand ordering of the same cells
(`MultiblockLayoutBuilder.cs:16-18`).

### Origin is the negation of the core

> Origin is the **negation** of the core glyph's `(col, row)`, so the core lands on the anchor's own
> `(0,0,0)`. Getting this wrong builds the whole structure offset from the block you placed.
> — `../layouts-workbench.md:36-38`

In the cold blast furnace, `C` sits at column 3, row 2 of layer 0, so `Origin(-3, -2)`
(`BlockBlastFurnaceCoreCold.cs:58`). In the heating furnace `C` is at column 6, row 1, so `Origin(-6, -1)`
(`BlockHeatingFurnaceCore.cs:43`).

The rule is enforced only in the filler DSL, never in the multiblock DSL. `FillerLayoutBuilder.Build()`
throws when the principal glyph `'O'`/`'0'` is drawn anywhere but the origin
(`Blocks/Structures/FillerLayoutBuilder.cs:128-132`); `MultiblockLayoutBuilder` has no equivalent check -
the core is just another legend entry. Three shipped layouts carried a wrong `Origin` until they were
checked against their drawings by hand (`../layouts-workbench.md:36-38`).

### Oriented parts

Vanilla's `MultiblockStructure` rotates a structure's offsets through `InitForUse(angle)` but never its
codes, so a cell wanting `brickslabs-fire-south-free` demands a south-facing slab at every structure angle -
wrong three times out of four. A layout writing `-*` instead accepts any rotation, which lets a "completed"
furnace still have visible gaps in its walls (`Blocks/Structures/MultiblockFacings.cs:12-18`).

1. Detection. `MultiblockLayoutBuilder.AddLegend` scans the code's path for a whole dash-separated
   segment that is a horizontal side word (`north`/`south`/`east`/`west` or `n`/`s`/`e`/`w`) and records its
   index (`FindOrientationSegments`, `MultiblockLayoutBuilder.cs`). Every rotating segment is recorded,
   because block codes put the orientation at the end and a material name earlier could otherwise shadow it
   (`:113-115`). Only whole segments count, so `westward` or a `-we-` axis token is never mistaken for a
   facing.
2. Emission. The table `code → segment index` rides in a sibling attribute
   `attributes.multiblockFacings`, not inside `multiblockStructure` - that object is deserialised by
   vanilla and must stay exactly its schema (`ExBlockDef.cs:801-804`). It is keyed by the full domained
   code form (`AssetLocation.ToString()`), because `ToShortString()` elides `game:` and would never match a
   vanilla block at runtime (`MultiblockLayoutBuilder.cs:104-106`, `MultiblockFacings.cs:71-74`). The
   authored facing itself is not stored - it is already in the code, the one copy that cannot drift
   (`MultiblockLayoutBuilder.cs:141-143`).
3. Check time. `MultiblockFacings.Rotate(code, angle)` swaps that segment for its rotated side word
   (`MultiblockFacings.cs:66-78`, `RotateSegment` at `:86-96`, `ExOrientation.RotateSideWord` at
   `ExOrientation.cs:172-178`). Full words stay words, single letters stay letters; vertical `up`/`down` and
   unrecognised words come back unchanged, because a Y rotation does not move them
   (`ExOrientation.cs:162-164`, `IsHorizontalSideWord` at `:182-183`).

The angle used is `_structureInitAngle` = `_currentAngle + initAngleOffset`, i.e. the angle actually handed
to `InitForUse` - not `_currentAngle` (`BlockEntityMultiblockStructure.cs:36-40`, `:146`, `:161`, `:439`). The
bessemer control's `+180` frame is the case where using `_currentAngle` would face every part backwards.

A layout that declares nothing oriented emits no attribute and gets `MultiblockFacings.None` (`:29-30`);
no migration was needed (`:39-43`).

Live users: the heating furnace (`BlockHeatingFurnaceCore.cs:51, 54, 55`) and the puddling furnace
(`BlockPuddlingFurnaceCore.cs:59, 62, 63, 64`).

### Cell roles

A layout records what block may occupy a cell, not what the cell is for, and at runtime only the code
string survives - the authored glyph is gone. Eight semantic cell sets carry that fact as roles:
`ChargeableCells`, `TuyereCells`, `GasOutletCells`, `SolidifyCells`, `MetalTapCell`, `SlagTapCell` and the
shaft box `ShaftMin`/`ShaftMax`. `ShaftCentre`, a geometric point rather than a cell set, is the only
hand-written offset left on a furnace.

Three of the eight had drifted silently as hand-kept C# offset lists, none ever observed because a
reverberatory hearth pools and pours nothing:

- neither hearth overrode `SolidifyCells`, so both inherited the blast furnace's `(0,1,0)`/`(1,1,0)` - on
  their own drawings one air cell and one solid brick;
- neither hearth overrode `MetalTapCell` either, so both inherited `(2,1,0)`, which on their 8-wide
  `Origin(-6,-1)` drawings is grid column 8 - off the structure entirely;
- `BlockEntityPuddlingFurnace` overrode `SlagTapCell` to `(-3,1,1)` with a comment citing a layout `T` glyph
  the drawing has never contained; that cell is one of the fire-brick slab shoulders round its doorway.

`Role(glyph, CellRole)` puts the fact in the drawing.

```csharp
.Legend('a', "game:air")            // what may occupy the cell
.Legend('v', "game:air")            // same code...
.Role('v', CellRole.Flue)           // ...different role
```

Roles attach to glyphs, never to codes. `game:air` is simultaneously the vent shaft, the flue column
and the tap alcove in shipped layouts, so "the air cells are the flue" is not expressible. The author gives
each role its own glyph - the DSL's existing `T`/`Y` tuyere idiom - and several glyphs may share one
code. Several glyphs may equally share one role.

One glyph may carry several roles, because a cell can be two things at once. The shipped case is the shaft
furnaces' crucible: burden rests on it while the furnace runs and metal freezes onto it when the furnace is
put out, so it is `Chargeable` and `Pool`. A cell holds exactly one glyph, so two overlapping roles cannot
be split across two of them. Distinct roles accumulate rather than overwrite (restating one is idempotent),
so last-writer-wins drift is absent by construction.

`CellsAccepting` answers "which cells would take this block", the right question when the caller has a
block in hand, but it couples the caller to a block code that a retype can move out from under it. A role
says what the layout knows the cell is for, independent of what fills it. Both stay.

| | |
|---|---|
| **Authoring** | `MultiblockLayoutBuilder.Role(char, CellRole)` (`MultiblockLayoutBuilder.cs:77`) |
| **Enum** | `Blocks/Structures/CellRole.cs:42` - `Chargeable`, `Firebox`, `Tuyere`, `GasOutlet`, `MetalTap`, `SlagTap`, `Pool`, `Flue`, `Damper` |
| **Arity** | `[SingleCell]` on the enum member (`CellRole.cs:113`), read through `CellRoles.IsSingleCell` (`:135`) |
| **Emission** | sibling attribute `attributes.multiblockRoles` (`ExBlockDef.cs:806-808`), role name → authored offsets |
| **Reading** | `MultiblockCellRoles.FromAttributes` (`MultiblockCellRoles.cs:71`), `CellsOf(role)` (`:51`) |
| **Runtime** | `BlockEntityMultiblockStructure.CellsWithRole(role)` (`BlockEntityMultiblockStructure.cs:256`) → world `BlockPos`, cached |

### Connector marks - what a cell must open onto

A third sibling, and the one that replaced the orientation pins on the three shaft furnaces. A cell marked
`Connector` is satisfied only by an occupant exposing a network connector on each of the named faces; a code
match alone is not enough. It exists because a network node re-picks its own orientation from its
neighbours, so pinning its variant in the legend states a fact the node is free to contradict - the
structure can be left uncompletable, or a complete one broken when the player plumbs something nearby.

| | |
|---|---|
| **Authoring** | `MultiblockLayoutBuilder.Connector(char, params BlockFacing[])`, authored in the north frame |
| **Emission** | sibling attribute `attributes.multiblockConnectors`, face letter → authored offsets, omitted when nothing is marked |
| **Reading** | `MultiblockConnectors.FromAttributes`, `OutwardFacesAt(authoredOffset)` - total and never-throwing, as `MultiblockCellRoles` is |
| **Runtime** | rotated by `_structureInitAngle` inside `IncompleteBlockCount`, asked through `INetworkMember.HasConnectorAt`; `ConnectorFacesAt(worldCell)` answers the same set for a report |
| **Refusal** | `MultiblockLayoutBuilder` throws on a legend pinning a multi-letter token; `PinnedNetworkNodes` catches the single-letter cases per mod |

Satisfied by a superset: a passthrough wearing `ns` answers a demand for north, so a legitimate re-pick does
not break a standing structure. An occupant that is not on a network answers nothing, so a brick dropped
into a connector cell cannot satisfy the mark. The rig mirrors the same rule - `StructureRig.Missing` counts
by code *and* connector, because a rig that counted by code alone would raise a footprint the machine then
refuses, and `Complete()` would throw "0 of N cells unsatisfied".

The rule for what earns a role: a role exists only where code asks the layout "where are my X cells?".
A block that finds its own core - a charge door, a hopper, a hearth, a filler, the core itself - needs none,
because that lookup runs the other way through `FindAnchorOwning<T>`. Absent for that reason:
`Core`, `Filler`, `ChargeDoor`, `Hopper`, `Hearth`, and `ShaftCentre` (a geometric point, not a cell set).
The player-built chimney needs no `StackBase` either - it starts at the highest `Flue` cell.

Rotation is inherited, not reimplemented. The table stores the offsets the author drew, in the
north-default frame. Vanilla's `InitForUse` builds `TransformedOffsets` by walking `Offsets` in order and
rotating each one, leaving the two lists index-aligned and `Offsets` itself untouched. `CellsWithRole`
therefore matches the authored offset in `Offsets` and reads the same index out of `TransformedOffsets`,
so there is no second copy of the rotation maths and no choice between `_currentAngle` and
`_structureInitAngle`.

Build-time guards (all `InvalidOperationException` from `Build()`, except the `ArgumentException` on a
duplicate `Legend`, which throws at the call):

| Guard | Why |
|---|---|
| role on a glyph with no `Legend` | the glyph is not in the drawing's alphabet; the role would answer empty for ever |
| role on a glyph no `Layer` draws | same silent empty set by the other route. Checked per glyph, so a role two glyphs share still fails when one is dropped |
| one glyph, two codes | the cell gets one number, so one code silently stops being required |
| `Chargeable` and `Firebox` in one layout | a burden column or a fuel bed, never both - the shaft/firebox furnace split made structural |
| connector on a glyph with no `Legend`, or on one no `Layer` draws | the same silent empty set a role has, and the worse half of it: an undrawn demand reads as a structure with no facing requirement at all, which completes with the node backwards |
| a `Legend` code carrying a multi-letter direction token | only a network node spells one, and a node re-picks its own orientation; `LegendAnyFacing` is the opt-out |
| a `[SingleCell]` role drawn on ≠ 1 cell | `MetalTapCell`/`SlagTapCell` are a single `Vec3i`, so the migration writes `.Single()`; without this that throws at runtime instead. Counted over drawn cells, so two glyphs sharing the role fails too (`ValidateRoleArity`, `MultiblockLayoutBuilder.cs:236`) |

Arity is part of a role's meaning. `MetalTap` and `SlagTap` are the two `[SingleCell]` roles - a hearth
is drained at one point, and the hand-lists they replace are a single `Vec3i`. Everything else is a genuine
set: a shaft is a column, a hearth has two tuyeres, and a stack throttled at both ends is one layout with two
`Damper` cells. Unmarked is the default and the loose end: tightening a role later is a build error the
author sees, while loosening one silently invalidates a `.Single()` already written against it.

The reader is total; the builder is the gate. `MultiblockCellRoles.FromAttributes` skips anything it
cannot parse - an unknown or undefined role key, a coordinate that is not an `int` - rather than throwing,
because it is re-read in `SetStructureAngle`, i.e. on the server monitor tick. A throw there is a
repeating exception on a live block entity mid-session. Every value is type-checked rather than cast
(`MultiblockCellRoles.cs:119`), and `Enum.IsDefined` backs `Enum.TryParse`, which alone would admit `"99"`
and `"Flue, Damper"` as roles no caller can ask for. The price is that a hand-edited patch loses cells
silently; a code-first layout is the only supported route.

Additive, and it must stay that way. A layout that calls no `Role` emits no attribute at all and gets
`MultiblockCellRoles.None`. `Adding_roles_changes_nothing_about_the_structure_a_layout_emits` pins the
stronger statement at source: the same drawing with and without `Role` calls emits byte-identical
`multiblockStructure` JSON, and a layout that does mark something moves its golden only inside
`multiblockRoles`.

Live users: the five furnace cores.

| layout | glyph → role | cells |
|---|---|---|
| `BlockBlastFurnaceCoreCold.cs` | `c`,`p` → `Chargeable` · `p` → `Pool` · `Y` → `Tuyere` · `T` → `MetalTap` · `S` → `SlagTap` | 38 · 2 · 2 · 1 · 1 |
| `BlockCupolaFurnaceCore.cs` | the same five | 5 · 1 · 1 · 1 · 1 |
| `BlockBlastFurnaceCoreHot.cs` (smex) | as the cold furnace, plus `P` → `GasOutlet` | 38 · 2 · 2 · 1 · 1 · 2 |
| `BlockPuddlingFurnaceCore.cs` (`'c'`) | `Firebox` | 1 |
| `BlockHeatingFurnaceCore.cs` (`'c'`) | `Firebox` | 2 |

`p` is a duplicate of `c`, and `S` is a duplicate of `T`: same code, own glyph, so each can carry its
own role - `Pool` beside `Chargeable`, and `SlagTap` apart from `MetalTap`. Two glyphs on one code share one
block number, so neither addition touched `blockNumbers` or `offsets`. Both taps are the same block, so
which drain a cell is cannot be read off the code at all, only off the drawing.

| consumer | reads | replaced |
|---|---|---|
| `ChargeableCells` | `Chargeable` | `CellsAccepting(BlockChargePile.PileCode)` |
| `PoolCells` | `Pool` | `SolidifyCells` (a `Vec3i[]` virtual + one cupola override) |
| `ScanForOutlets` → `_tuyeres` | `Tuyere` | `TuyereCells` (virtual + 2 overrides) |
| `ScanForOutlets` → `_gasOutlets` | `GasOutlet` | `GasOutletCells` (virtual + 3 overrides) |
| `MetalTapPos` (→ `DrainIronTap`, the tap HUD) | `MetalTap` | `MetalTapCell` (a `Vec3i` virtual + one cupola override) |
| `SlagTapPos` (→ `DrainSlagTap`, the tap HUD) | `SlagTap` | `SlagTapCell` (a `Vec3i` virtual + cupola and puddling overrides) |

An absence needs no declaring: the drawing states it. The six `=> []` overrides that existed only to say
"my drawing has none of these" (`GasOutletCells` on the cold furnace and the cupola,
`TuyereCells`/`GasOutletCells` on the firebox branch) are gone with no replacement. `MetalTapPos` and
`SlagTapPos` are nullable for the same reason: a `[SingleCell]` role a layout does not mark answers no
cell rather than inventing one.

The role↔code cross-check does not generalise, and `Pool` and the taps are where it runs out.
`Chargeable`, `Firebox`, `Tuyere` and `GasOutlet` each sit on a glyph with a distinguishing code
(`chargepile`, `coalpile`, `iiex:tuyere*`, `iiex:pipe-outlet*`), so a test can play the role off
`CellsAccepting` and catch a `Role()` hung on the wrong glyph. `Pool` cannot be checked that way by
construction - its glyph is a deliberate duplicate of the shaft glyph. What stands in for it: the pool must
be exactly the chargeable cells on the lowest level, it must be a proper subset of the burden column, and
every cell must pass `OwnsCell`. The two taps get half an oracle: both are `iiex:moltenmetaltap*`, so
`CellsAccepting` pins their union - enough to catch a tap role hung on brick - but nothing about which is
which. That half is stated by the physical relation: slag floats, so the slag tap is the higher of the two.

These five layouts are also the first to exercise the `Chargeable` XOR `Firebox` guard: with no layout
declaring a role, its first operand is false everywhere and the second is never evaluated.

The shaft box is derived too. `BlockEntityFurnaceCore.ShaftBox` is the bounding box of
`Chargeable` ∪ `Firebox` - the two are mutually exclusive by build guard, so the union is whichever one the
drawing carries and no per-branch virtual is needed. `ShaftBounds()` keeps its per-component re-sort
unchanged, and both it and `ShaftBox` are nullable, because two corners cannot express "no cells" -
everything that re-sorts would normalise an "impossible" pair back into a box at the origin. Load-order
note: vanilla assigns `Block` in `CreateBehaviors` immediately before `FromTreeAttributes` on every load
path, so a block entity restoring a save does know its own block; what it lacks that early is `Api`.

### The trapdoor caveat

Trapdoors cannot be orientation-checked by this mechanism. `game:trapdoor` keeps both its facing and its
open/closed state in the block entity, not in the block code, so no code match can see either
(`../layouts-workbench.md:93-95`). Slabs, stairs, doors and coke-oven doors all carry theirs in the code and
do work. The beehive coke oven's trapdoor cell is therefore permanently lax - it can only ever require a
trapdoor, in any rotation and any state.

A second trap in the same family: the code must actually exist. `game:trapdoor-iron-down*` matches
nothing - vanilla's real variant chain narrows to `trapdoor-plate-iron-1` - and `game:claybricks-fire*`
matches nothing because vanilla's variant order is `{state}-{type}`, i.e. `claybricks-good-fire`.

### Completion, projection and the missing-blocks report

`BlockEntityMultiblockStructure` (`Blocks/Structures/BlockEntityMultiblockStructure.cs:28`) is the base for
every multiblock anchor:

- A monitor tick every `CompletionTickMs` (default 3000 ms, `:49`) recomputes the rotation and
  flips `StructureComplete`, starting and stopping whatever process the machine carries across the
  transition, through `ProductionProcess` (`:99-120`) - a form-only multiblock has none and the two
  calls are no-ops.
- `IncompleteBlockCount` (`:403-426`) walks the same `TransformedOffsets` vanilla does and matches with the
  same `WildcardUtil.Match`, with one addition: the wanted code is run through `MultiblockFacings.Rotate`
  first (`WantedCodeAt`, `:433-441`). The number → code map is rebuilt from the public `BlockNumbers`
  because vanilla keeps its own private, and cached (`:443-452`).
- Projection is Ctrl+Shift+right-click (`BlockBehaviorMultiblockStructure.cs:34-38`), routed to
  `Interact` (`BlockEntityMultiblockStructure.cs:338-401`), which both draws the hologram and chats an exact
  shopping list. The behaviour is carried by the anchor and by every functional component of the same
  structure (a tap, hopper, tuyere), each of which scans up to its owning anchor
  (`BlockBehaviorMultiblockStructure.cs:13-19`, `IMultiblockComponent`). Add it before any other
  right-click behaviour so its `PreventSubsequent` wins (`:21-25`).
- `HighlightIncompleteSafe` (`:487-544`) is a crash-safe reimplementation of vanilla's
  `HighlightIncompleteParts`, which does `SearchBlocks(wantedCode)[0]` and throws
  `IndexOutOfRangeException` when a wildcard resolves to nothing. Falls back to a neutral blue tint.
- Air-satisfied and filler cells are excluded from the shopping list, because the player does not gather
  them (`IsAutoFilled`, `:546-554`).
- `OwnsCell(worldCell)` (`:178-192`) is the reverse lookup that disambiguates two adjacent structures whose
  component scan boxes overlap; `FindAnchorOwning<T>` (`:320-346`) is the bounded scan a component uses.

### Invisible fillers

`BlockStructureFiller` (`Blocks/Structures/BlockStructureFiller.cs:22`) is one shared exlib block
(`exlib:structurefiller`, `StructureFillers.cs:50-51`) authored code-first at
`BlockStructureFiller.cs:33-51`: `json` drawtype over `exlib:block/empty`, side-solid but not opaque,
`lightAbsorption 0`, `replaceable 500`, `resistance 45`, no drops, full-cube collision and selection.

`sidesolid: true` is what gives the megablock real per-cell collision; drawing nothing is what makes it
invisible. Everything else it does is rerouting to the principal:

| Operation | Reroute | file:line |
|---|---|---|
| interact start/step/stop | to the principal, cell-aware via `IFillerInteractionTarget` | `:224-312` |
| getting broken / broken | to the principal (breaking any cell breaks the whole machine) | `:314-362` |
| drops | always `[]` - the principal owns all drops | `:372-377` |
| pick block | the principal's `OnPickBlock` | `:364-369` |
| look-at info | the principal's `GetPlacedBlockInfo`, and the BE's `GetBlockInfo` | `:381-390`, `BlockEntityStructureFiller.cs:257-263` |
| sounds | the principal's, so the invisible footprint is not silent | `:172-190` |

Two subtleties in the interaction reroute (`:224-266`): a player holding a placeable block is building,
not driving the machine, so the forward is skipped - except for liquid containers, which the principal still
needs to see. And an unhandled click on an `allowAttach` cell returns `false` so the engine places normally,
while on a non-buildable cell it is swallowed so nothing drops onto the invisible face.

Per-cell knobs, all from the `fillerOffsets` entry (`StructureFillers.cs:53-59`, parsed at `:61-79`):

| Key | Default | Effect |
|---|---|---|
| `x, y, z` | — | offset from the principal, in the block's north/authored frame |
| `allowAttach` | `false` | whether other blocks may attach here (`BlockStructureFiller.cs:121-141`). Default off, or torches and vines would hang on the invisible footprint |
| `collisionBox` / `collisionBoxes` | full cube | partial-fill cuboids for a cell the megablock only half occupies - a slab. Selection matches collision so the player cannot target solid-looking empty space (`:146-166`) |
| `behaviors` | none | see below |

Rotation. `StructureFillers.FootprintCells(principal, pos, angle)` (`:144-170`) rotates the offset
(`ExOrientation.RotateOffset`), the partial boxes (`RotateBoxes`, pivoting on the cell centre) and each
declared behaviour's connector face (`RotateBehaviorFaces`, `:177-194`) into the placed orientation. The
principal supplies the angle through `IFillerHost` / `StructureAngle`.

Lifecycle triad - `CanPlace` → `PlaceFillers` → `RemoveFillers` (`:196-277`). `RemoveFillers` only
clears a cell that actually holds a filler linked to this principal, so a neighbouring structure's fillers
are never disturbed (`:247-251`). `BlockFilledMegastructure` (`Blocks/Structures/BlockFilledMegastructure.cs:31`)
folds this triad for blocks that can inherit from it, with an `OnFootprintPlaced` hook (`:79-83`); blocks
that already have a base class (the flywheel is a `BlockNetworkNode`) call the three helpers themselves -
`BlockFlywheel.cs:149-190` is the canonical hand-rolled copy.

### The footprint DSL

`StructureFootprint.Layout(…)` → `FillerLayoutBuilder` (`Blocks/Structures/FillerLayoutBuilder.cs:17`) is
the filler-side counterpart of the multiblock DSL, on the same `StructureLayout` parser:

| Member | Meaning | file:line |
|---|---|---|
| `Origin(a, b)` | axis pair depends on the grid kind: `(xLeft,zTop)` for `Layer`, `(zLeft,yTop)` for `Slice`, `(xLeft,yTop)` for `Face` | `:37-42` |
| `Solid(ch)` / `Attach(ch)` | register a glyph as plain / attach-allowing; `'#'` and `'+'` are the defaults | `:45-56` |
| `Slab(ch, half)` | register a glyph as a half-height cell, emitting the matching `collisionBox` | `:88-98` |
| `Host(ch, …specs)` | register a glyph as an attach-allowing cell that hosts behaviours | `:69-80` |
| `Port(ch, face, networkType)` | register a glyph as a plain filler carrying a passive network port on `face` | `:104-113` |
| `Layer(y,…)` / `Slice(x,…)` / `Face(z,…)` | the three grid kinds | `:116`, `:124`, `:133` |
| `'O'` / `'0'` | the principal, for readability - skipped, never becomes a filler | `:126-127` |

Build-time guards (`:106-150`):

- mixing grid kinds throws - their `Origin` axis pairs differ (`:108-114`);
- `'O'` drawn anywhere but `(0,0,0)` throws, naming the coordinate - a misplaced grid (`:128-132`);
- an unregistered glyph throws (`:133-137`);
- then `StructureFootprint.Validate` (`StructureFootprint.cs:101-115`) rejects a cell at the principal
  origin and any duplicate cell.

`StructureFootprint.Rectangle(halfWidth, depth)` (`:51-69`) is a computed shortcut for a linear
footprint: `depth` rows along `+Z`, `2·halfWidth+1` columns emitted centre-out (`0, +1, −1, +2, −2, …`),
principal skipped, every flanking column opting into attachment. No shipped block uses it today - every
current footprint is a hand-drawn `Layout` grid.

Per-variant footprints go through `ExBlockDef.FillerOffsetsByType(typeWildcard, cells)`
(`ExBlockDef.cs:721-735`), which writes `attributesByType.{wildcard}.fillerOffsets` - the flywheel's
`normal` 3×3×1 vs `large` 5×5×2 discs (`BlockFlywheel.cs:122-123`).

### Behaviour-capable fillers

A footprint cell can host real block-entity behaviours on the principal's behalf, because the principal,
two cells away, cannot accept a connection at the face where an axle physically couples
(`BEBehaviorMPFillerPort.cs:10-17`).

Declared as `FillerBehaviorSpec(Code, Face?, Properties?)` (`StructureFootprint.cs:16-20`), serialised as
`{ code[, face][, properties] }` in the cell's `behaviors` array (`ExBlockDef.cs:737-770`), read back by
`StructureFillers.ReadBehaviors` (`:88-111`).

Instantiation lives in `BlockEntityStructureFiller.ApplyHostedBehaviors` (`:96-142`): resolve the class code
through `Api.ClassRegistry`, call `IFillerHostedBehavior.ConfigureFromFiller(principal, rotatedFace,
properties)` before `Initialize` so the behaviour's `SetOrientations` sees the right face, then attach
and initialise. Unknown class codes log a warning and are skipped (`:118-125`).

Re-applying is safe: being handed the declaration set already applied returns without touching the live
behaviours (`:97-99`), and a genuine change detaches the previous set with `OnBlockRemoved` first, so a
detached behaviour deregisters whatever it registered rather than leaving it behind (`:101-107`).

Three sync traps are handled explicitly, all documented in-source:

- `SetHostedBehaviors` is called by `PlaceFillers` right after the principal link is set, so an MP port
  joins the network at placement rather than at the next reload (`StructureFillers.cs:227-229`).
- The load order is `FromTreeAttributes` → `Initialize`, so a behaviour created in `Initialize` missed the
  base class's tree-routing loop. The tree is kept in `_savedTree` and replayed to each behaviour -
  client only, because on the server the behaviour establishes its own state and a stale saved
  `NetworkId` would fight it (`BlockEntityStructureFiller.cs:63-67`, `:135-140`). ⛔ The consequence is
  that a hosted behaviour's *saved* state is never read back on the server; a membership is unaffected
  because it persists nothing and re-registers from its declaration.
- When a megablock is placed while a client is watching, the filler block is set first (client creates the
  BE with no behaviours) and `HostedBehaviors` arrives a moment later as a sync update, after `Initialize`
  has already run. `FromTreeAttributes` re-runs `ApplyHostedBehaviors` in that case
  (`BlockEntityStructureFiller.cs:203-208`).

`BlockStructureFiller` then advertises the hosted behaviour to the two foreign networks:

- exlib pipe/molten via `INetworkConnector.NetworkTypeAt` / `HasConnectorAt(world, pos, face)`, reading
  `PortNetworkType` / `PortFace` off the BE (`BlockStructureFiller.cs:62-73`);
- vanilla MP via `IMechanicalPowerBlock.HasMechPowerConnectorAt`, which accepts the declared face or
  its opposite - an axle couples along an axis, so a player can run it straight through the cell and
  attach from either side (`:84-110`). ⛔ It reads every hosted declaration's face, not just an MP one's,
  so a cell hosting a network membership *and* an MP port would offer an axle the membership's face too.

Live users: the flywheel's hub cells hosting `exlib.BEBehaviorMPFillerPort` north and south
(`BlockFlywheel.cs:45-48, 55, 71`), the twin-tub blower's west port (`BlockTwinTubMPBlower.cs:69`), and the
sand casting bed's per-cell `exlib.BEBehaviorMoltenCell` with different `{capacity, drainFitting}` per slot
(`BlockSandCastingBed.cs:36-38, 140`).

### The declarative filler port

A *passive* port is the lighter of the two arms, and it is declared on the drawing rather than hosted:

```csharp
f.Port('S', BlockFacing.UP, "pipe")     // steam leaves through the top of this cell
 .Port('E', BlockFacing.EAST, "pipe")   // flue gas leaves eastward from this one
```

`FillerLayoutBuilder.Port` (`:104-113`) registers the glyph as a plain, non-attaching filler and records
`(face letter, networkType)`; the pair rides through `FillerCellSpec` → `fillerOffsets[].portFace` /
`portNetwork` (`StructureFootprint.cs:56-65`, `ExBlockDef`), is read back by `StructureFillers.ReadOffsets`
and is rotated into the placed orientation with the rest of the footprint. `BlockStructureFiller` then
answers `INetworkConnector.NetworkTypeAt` / `HasConnectorAt` off the placed cell's `PortFace` /
`PortNetworkType` - the same two fields a hosted behaviour would have had to be instantiated to provide.

The face is authored in the north frame, so a machine reads it back off its own footprint rather than
writing it a second time in code; `BlockBoiler.PortWorldFaceAt` (`BlockBoiler.cs:89-104`) is the worked
example - it finds the cell at a declared offset, takes that cell's `PortFace`, and rotates it, so the face
a machine probes across and the face the cell answers on cannot drift apart.

**Connector or node - which arm to pick.** They are not interchangeable, and the choice is per machine:

| | A declared **port** (connector) | A hosted **`BEBehaviorNetworkMember`** (node) |
|---|---|---|
| What the cell is | skin. It answers for the principal and is invisible to the graph | a member of the graph in its own right |
| Costs | two strings in the footprint | a block entity behaviour instantiated, registered and torn down per cell |
| Pick it when | the machine is the thing on the network and the cell is only where a pipe touches it | the run has to **pass through** the footprint, or the cell must be reachable as a node from more than one side |
| Live examples | the Cornish boiler's steam and exhaust cells ([Cornish boiler](../machines/boiler-cornish.md)) | the rolling mill's drive line, where a shaft runs straight through (§ A filler cell is a graph node) |

⛔ A connector cannot be probed from the principal. `ConnectedNetwork` runs its reciprocal test from the
block entity's own cell, and a port two cells away is not that cell - so a machine reading a footprint port
uses `ConnectedNetworkAt<TNet>(portCell, face)` instead (`MachinePorts.cs`). Reading from the principal
answers `null` on a correctly plumbed machine, silently.

### A filler cell is a graph node when it declares one

A footprint cell joins an exlib `BlockNetwork` the way any other cell does: by carrying a
`BEBehaviorNetworkMember` for that network. The graph resolves a cell's participation through
`NetworkMembership.Resolve` - the memberships on its block entity first, the block second
(`NetworkMembership.cs:44-60`) - so a filler being a plain `Block` no longer keeps it out, and one can
bridge two nodes on opposite sides of itself. A cell that declares no membership is still not a node:
the footprint stays empty space to the graph unless a cell says otherwise.

The declaration is an ordinary hosted behaviour, so nothing new carries it:

```csharp
f.Host('P', new FillerBehaviorSpec(
  "exlib.BEBehaviorNetworkMember", "north",
  new { networkType = "pipe", passThrough = true }));
```

- `networkType` names the graph the cell joins. Without it the cell logs an error and joins nothing,
  because registering a blank type throws inside a chunk load (`BEBehaviorNetworkMember.cs:205-216`).
- the cell's `face`, rotated into the placed orientation by `FootprintCells`, is the face it couples
  on; `passThrough` adds the opposite face too, so a run passes straight through the cell - the same
  axis rule an axle follows (`BEBehaviorNetworkMember.cs:107-119`).
- a `connectors` string in the properties is written in the *unrotated* frame and does not turn with
  the structure, so a footprint that rotates states its `face` instead.

**A membership and a port on one cell.** `PortFace`/`PortNetworkType` still mean what they always
meant: a face another network couples *to* on a cell that is not itself a node, as both boilers' steam
cells are. The two arms compose per network type - a membership answers for the network
it names, the port for any other - and where both name the same one **the membership wins**: it is the
cell's own participation and the thing that registered the node, so a port cannot move a node's faces.
A membership that states no face of its own falls through to the port's, which turns an existing port
cell into a node without restating where it couples.

⛔ A footprint cell answers *only* through its block entity, where an ordinary node block has the block
arm to fall back on as well. That difference is narrower than it looks: an unload takes the block too,
so both kinds of cell are equally invisible while their chunk is away. The graph answers that for both
by suspending its fracture check rather than acting on it, so a run bridged through a footprint cell is
left whole until the chunk returns - see [pipe network](pipe-network.md) § 1.

`BlockRollingMillAxle` is the workaround this retires. The mill needs a three-cell drive line so it can
be driven from either shaft end and chained with other stands, so it leaves those two cells out of its
filler footprint (the `.` cells at `BlockRollingMill.cs:78-85`) and places dedicated
`BlockRollingMillAxle` blocks there instead (`:169-192`) - `BlockNetworkNode`s with
`NetworkType => "mpenergy"` (`BlockRollingMillAxle.cs:23-26`), otherwise filler-shaped. A `passThrough`
membership on those footprint cells does the same job now, and the block is redundant. It stays: it is
placed in existing worlds, and removing a placed block needs a migration. See
[mp-energy](mp-energy.md).

### `layouts-workbench.md` — the scratchpad

`../layouts-workbench.md` is the working surface for layouts, split by mod with a status word per
layout (`:13-20`): *shipped* (in C#, has a golden - the copy there is a hand-taken snapshot), *draft*
(drafted there only, not in C#), *stale* (in C# in an older form). The goldens are the truth: where a
copy and its golden disagree, the golden is right.

Goldens live at `test/<Mod>.Tests/goldens/<domain>/blocktypes/…`. Edit a layout there, then paste it back
into the owning block.

---

## Numbers

Everything in this system is a hard-coded constant or a build-time rule; there is no config surface for
multiblocks or fillers. A layout is content, authored in C# and pinned by a golden.

| Constant | Value | file:line | What it does |
|---|---|---|---|
| `StructureFillers.FillerCode` | `exlib:structurefiller` | `StructureFillers.cs:50-51` | settable `static` property, so a fork could point it elsewhere; nothing does |
| filler `sidesolid` | `true` | `BlockStructureFiller.cs:43` | required - this is what gives per-cell collision |
| filler `sideopaque` | `false` | `:44` | so the footprint does not cull neighbour faces |
| filler `lightAbsorption` | `0` | `:45` | an invisible cell must not cast shadow |
| filler `replaceable` | `500` | `:46` | so `CanPlace` treats an existing filler as clear |
| filler `resistance` | `45.0` | `:47` | never actually mined - the break reroutes to the principal |
| filler drawtype / shape | `json` / `exlib:block/empty` | `:41-42` | renders nothing |
| default `allowAttach` | `false` | `StructureFillers.cs:72`, `BlockEntityStructureFiller.cs:28` | opt-in per cell |
| default partial boxes | `null` (full cube) | `StructureFillers.cs:123-141` | `collisionBoxes` wins over `collisionBox` |
| `CompletionTickMs` | `3000` | `BlockEntityMultiblockStructure.cs:49` | `protected virtual`, overridable per machine |
| first block number `w` | `1`, incrementing per distinct code in legend-declaration order | `MultiblockLayoutBuilder.cs:170-181` | private index; the game only cares that it resolves |
| reserved legend symbols | `'.'`, `' '` | `MultiblockLayoutBuilder.cs:91-94` | throw if used as a legend symbol |
| cell roles defined | 9 | `Blocks/Structures/CellRole.cs:42` | emitted only when a layout marks something; 5 layouts do (the furnace cores), using 5 of the nine - `Chargeable`, `Firebox`, `Tuyere`, `GasOutlet`, `Pool`. `MetalTap`/`SlagTap` are next; `Flue`/`Damper` have no consumer |
| single-cell roles | 2 - `MetalTap`, `SlagTap` | `CellRole.cs:68`, `:75` | `[SingleCell]`; the other seven are sets of any size |
| principal glyphs | `'O'`, `'0'` | `FillerLayoutBuilder.cs:126-132` | skipped at origin, throw elsewhere |
| default filler glyphs | `'#'` plain, `'+'` attach | `FillerLayoutBuilder.cs:21` | overridable via `Solid`/`Attach` |
| "no principal" sentinel | `(-1,-1,-1)` | `BlockEntityStructureFiller.cs:142-145` | in the save tree |
| projection gesture | Ctrl + Shift + RMB | `BlockBehaviorMultiblockStructure.cs:34-38` | |
| wrong-block highlight | RGBA `215,94,94,0x60` | `BlockEntityMultiblockStructure.cs:490` | red |
| unresolvable-slot highlight | RGBA `94,94,215,0x60` | `:498` | neutral blue fallback |
| projection help key | `<domain>:blockhelp-mulblock-struc-show` | `BlockBehaviorMultiblockStructure.cs:102` | resolved against the block's own domain |
| missing-report lang keys | `ExlibLang.StructureMissingHeader` / `…Line` | `BlockEntityMultiblockStructure.cs:539`, `:549` | exlib owns them; generated accessors, so a rename is a compile error |

Shipped drawn layouts: 9 (`../layouts-workbench.md` lists them per mod), plus the bessemer converter
still in coordinate form.

---

## Code

### Authoring (compile-time)

| Type / member | file:line | Role |
|---|---|---|
| `ExBlockDef.MultiblockLayout(cfg)` | `Definitions/ExBlockDef.cs` | the entry point; writes `attributes.multiblockStructure` + the siblings `multiblockFacings`, `multiblockRoles` and `multiblockConnectors` |
| `ExBlockDef.Multiblock(cfg)` | `:784-790` | raw coordinate form, for a layout not yet drawn (the bessemer converter) |
| `ExBlockDef.FillerOffsets(cells)` | `:712-716` | writes `attributes.fillerOffsets` |
| `ExBlockDef.FillerOffsetsByType(wc, cells)` | `:721-735` | per-variant footprint |
| `MultiblockLayoutBuilder` | `Definitions/MultiblockLayoutBuilder.cs:20` | `Origin`/`Legend`/`LegendAnyFacing`/`Role`/`Connector`/`Layer`; `RefuseNetworkToken`, `FindOrientationSegments`, `BuildFacings`, `BuildRoles`, `BuildConnectors`, `ValidateRoleArity`, `ValidateRoles` |
| `CellRole` | `Blocks/Structures/CellRole.cs:42` | the nine roles, and the doc-comment naming what is absent |
| `SingleCellAttribute` / `CellRoles` | `Blocks/Structures/CellRole.cs:113`, `:116` | the arity declaration and its cached reader |
| `MultiblockBuilder` | `Definitions/MultiblockBuilder.cs:16` | `Number`/`At`/`Fill`/`Build` + the three guards |
| `StructureLayout` | `Definitions/StructureLayout.cs:20` | `Parse` / `ParseVertical` / `ParseFrontal` |
| `StructureFootprint` | `Blocks/Structures/StructureFootprint.cs:43` | `Layout` (`:78`), `Rectangle` (`:51`), `Validate` (`:101`) |
| `FillerLayoutBuilder` | `Blocks/Structures/FillerLayoutBuilder.cs:17` | `Origin`/`Solid`/`Attach`/`Host`/`Layer`/`Slice`/`Face` |
| `FillerCellSpec`, `FillerBehaviorSpec` | `StructureFootprint.cs:29`, `:16` | the typed footprint records |

### Runtime

| Type / member | file:line | Role |
|---|---|---|
| `BlockEntityMultiblockStructure` | `Blocks/Structures/BlockEntityMultiblockStructure.cs:28` | the form alone: monitor tick, completion, projection, missing report, and the readiness it publishes |
| `.UpdateStructureRotation` | `:138` | abstract - every anchor implements it, normally by calling `SetStructureAngle` |
| `.SetStructureAngle(angle, offset)` | `:146-169` | the canonical body: reload the JSON, `InitForUse(angle+offset)`, cache, drop stale projection |
| `.OnStructureCompleted` / `.OnStructureLost` | `:581`, `:123` | the two hooks a machine overrides |
| `.IsReadyToProduce` / `.StopsProductionWhenNotReady` | `:59`, `:135` | the readiness a process reads; see [framework composition](framework-composition.md) |
| `.OwnsCell` / `.FindAnchorOwning<T>` | `:192`, `:307` | component → anchor reverse lookup |
| `BlockEntityMultiblockMachine` | `Blocks/Structures/BlockEntityMultiblockMachine.cs:20` | the form plus a hosted production process; what a multiblock that also runs derives from |
| `BlockBehaviorMultiblockStructure` | `Blocks/Structures/BlockBehaviorMultiblockStructure.cs:28` | registered as `"MultiblockStructure"`; `TryToggleProjection` (`:73`) is the one shared entry point |
| `.CellsAccepting(code)` / `.CellsWithRole(role)` | `:232`, `:283` | the two layout-derived cell queries; both cached, both dropped in `SetStructureAngle` |
| `MultiblockFacings` | `Blocks/Structures/MultiblockFacings.cs:27` | `FromAttributes` (`:44`), `Rotate` (`:66`), `RotateSegment` (`:86`) |
| `MultiblockCellRoles` | `Blocks/Structures/MultiblockCellRoles.cs:25` | `FromAttributes` (`:71`), `CellsOf` (`:51`), `Coord` (`:119`) - authored offsets, never world ones; skips what it cannot parse |
| `StructureFillers` | `Blocks/Structures/StructureFillers.cs:44` | `ReadOffsets` (`:61`), `FootprintCells` (`:144`), `CanPlace`/`PlaceFillers`/`RemoveFillers` (`:197`, `:215`, `:252`) |
| `BlockStructureFiller` | `Blocks/Structures/BlockStructureFiller.cs:22` | the invisible block + all rerouting |
| `BlockEntityStructureFiller` | `Blocks/Structures/BlockEntityStructureFiller.cs:18` | `Principal`, `AllowAttach`, `CollisionBoxes`, `PortFace`/`PortNetworkType`, `HostedBehaviors` |
| `BlockFilledMegastructure` | `Blocks/Structures/BlockFilledMegastructure.cs:31` | the shared place/break triad; `StructureAngle` is abstract |
| `IFillerHost` / `IFillerHostedBehavior` / `IFillerInteractionTarget` / `IMultiblockComponent` | `Blocks/Structures/` | the four contracts |
| `ExOrientation.RotateOffset` / `RotateFacing` / `RotateSideWord` / `GlobalPos` | `Helpers/ExOrientation.cs:30`, `:135`, `:172`, `:54` | all structure rotation goes through these |

### Where a caller hooks in

To make a megablock: implement `IFillerHost` (or inherit `BlockFilledMegastructure`), expose
`StructureAngle`, declare the footprint with `StructureFootprint.Layout(…)` and `.FillerOffsets(…)`, then
wire `CanPlaceBlock` → `StructureFillers.CanPlace`, `OnBlockPlaced` → `PlaceFillers`, `OnBlockBroken` →
`RemoveFillers` before `base` (`BlockFlywheel.cs:149-190` is the copyable shape). Override
`GetDrops` - the base will otherwise drop the block and the RCC materials.

To make a multiblock: derive the BE from `BlockEntityMultiblockStructure`, implement
`UpdateStructureRotation` via `SetStructureAngle`, add `{ "name": "MultiblockStructure" }` to the block
before any other right-click behaviour, and author the layout with `.MultiblockLayout(…)`.

To put a port on a footprint cell: `f.Host('M', new FillerBehaviorSpec("exlib.BEBehaviorMPFillerPort",
"west"))`, then read it back from the principal with `GetBehavior<BEBehaviorMPFillerPort>()` at the rotated
cell (`ExOrientation.GlobalPos(Pos, hx, hy, hz, angle)`) - `BlockEntityFlywheel.cs:205-219`.

---

## Gotchas

- `Origin` is unvalidated in the multiblock DSL. Nothing checks that the core glyph lands on `(0,0,0)`;
  a wrong `Origin` builds the whole structure offset from the placed block and reads as "the structure
  never completes". The filler DSL does check (`FillerLayoutBuilder.cs:128-132`). Three shipped layouts
  had this bug (`../layouts-workbench.md:36-38`).
- `'.'` advances the column; a space does not. `.` is an empty cell that still moves `+X`; a space is a
  pure separator (`StructureLayout.cs:42-47`). Swapping them shifts every glyph after it on that row.
- Only the last whole side segment is treated as the facing (`MultiblockLayoutBuilder.cs:117-126`). A
  code like `something-north-east-free` resolves to the `east` segment - block codes put the orientation
  at the end.
- A role cannot be keyed by code, only by glyph. `game:air` is the vent shaft, the flue and the tap
  alcove in shipped layouts. Give each role its own glyph pointing at the same code
  (`MultiblockLayoutBuilder.cs:59-87`).
- Block numbers are per code, not per glyph. Numbering per glyph produced two `w`s and one
  `blockNumbers` entry for two glyphs sharing a code; the lost number's cells silently stopped being
  required, and vanilla's own `InCompleteBlockCount` threw on them. The shape of `blockNumbers`
  is what forces it (`MultiblockLayoutBuilder.cs:170-181`).
- `multiblockFacings` is keyed by the full domained code. Writing the key as the author typed it
  (`ToShortString()`) drops `game:` and silently never matches a vanilla block, so the rotation becomes a
  no-op with no error (`MultiblockLayoutBuilder.cs:104-106`, `MultiblockFacings.cs:71-74`).
- Use `_structureInitAngle`, never `_currentAngle`, for facing rotation. They differ whenever a machine
  passes an `initAngleOffset` - the bessemer control's `+180`
  (`BlockEntityMultiblockStructure.cs:36-40`, `:439`).
- A wildcard code with `*` cannot be orientation-checked usefully - `-*` matches every rotation by
  construction. Oriented legends must name a concrete facing.
- A megablock must refuse placement when its volume is not clear, or the fillers silently fail to spawn
  and the machine has a hole in its collision that blocks can be placed inside
  (`BlockFlywheel.cs:158-166`).
- `RemoveFillers` before `base.OnBlockBroken`. The base call removes the principal (and its graph node);
  running it first orphans invisible solid cells (`BlockFlywheel.cs:186-189`,
  `BlockFilledMegastructure.cs:92-95`).
- Hosted behaviours need the client re-apply paths. Without the `_savedTree` replay and the
  `FromTreeAttributes` re-apply, a client-side MP port never joins its network and the driven part never
  turns - the failure is purely visual and easy to miss in a headless test
  (`BlockEntityStructureFiller.cs:58-64`, `:204-209`).
- `ConfigureFromFiller` must run before `Initialize` or the behaviour's `SetOrientations` sees the
  unrotated default face (`BlockEntityStructureFiller.cs:121-129`).
- Vanilla's `HighlightIncompleteParts` crashes on a wildcard that resolves to nothing. Always use the
  safe reimplementation (`BlockEntityMultiblockStructure.cs:460-513`).
- `MultiblockLayoutBuilder`'s doc says nothing about the origin rule (`:11-19`), the most error-prone
  thing about the DSL. The rule lives only in `layouts-workbench.md`.
- `BlockNetworkNode.cs:18` claims the network base is "currently used for gas pipes and molten canals".
  It is also the base for mpenergy nodes, including the mill's filler-shaped axle cells. Stale.

---

## Open

- No origin validation in the multiblock DSL. A `Core(char)` call, or reusing the filler DSL's `'O'`
  convention, would have caught all three shipped bugs at load. Not built.
- `LegendAnyFacing` has zero call sites in content. It exists for the "world-absolute facing" case (a
  chimney that must always vent north) that has not arisen. Untested against a real layout.
- `ShaftCentre` will never be a role. It is a geometric point with no layout meaning - the enum's own
  doc-comment says so - but it is still a hand-written offset that has to land in the `c` column.
- `CellsAccepting` has no production caller, only tests. It is still the capability that answers "may this
  block stand here" for a caller holding a block, and the oracle the role migration is pinned against.
- Nothing checks a role against the code its glyph carries. A layout could mark a brick cell
  `Chargeable` and the build would pass. The `Chargeable`/`Firebox` exclusion is the only build-time
  cross-check, and it is between two roles rather than between a role and a code. The test that covers
  the shipped layouts (`role cells == CellsAccepting(that block)`) has run out: `Pool` shares its code with
  the shaft glyph on purpose, and `Flue`/`Damper` will be air cells among other air cells. Every role from
  here on needs a structural relation invented for it instead.
- The cowper and smokestack layouts have firebox-ish cells and no `Firebox` role, and the rule that
  settled that still holds: they are not `BlockEntityFurnaceCore` machines, nothing asks them for a
  fuel-bed cell set, and a role with no consumer is the speculative kind the enum's own rule refuses. The
  boilers are outside this question entirely - neither declares a layout, and the Cornish's fuel bed is a
  behaviour on its block entity rather than a cell anything could mark
  ([firebox](../machines/firebox.md) § The pool is a behaviour, not a block feature).
- The alternation syntax is undocumented anywhere but `layouts-workbench.md`. `@(a|b)` is vanilla
  `WildcardUtil`; nothing in exlib parses, validates or mentions it, so a malformed alternation fails
  as "this cell can never be satisfied" with no error.
- Trapdoors are permanently unorientable, which leaves the beehive coke oven's door cells lax. The only
  fixes are a custom trapdoor block that carries its facing in the code, or a BE-aware completion check -
  neither is designed.
- The bessemer converter is still in coordinate form (`../layouts-workbench.md:1073-1075`, marked
  "to be replaced"), so it does not benefit from the drawing, the legend or the oriented-part check.
- The beehive coke oven layout is drafted but not in C# (`../layouts-workbench.md:594`).
- Nothing checks that a footprint's hosted-port cells agree with the principal's own hard-coded cell
  list. The flywheel duplicates its hub coordinates in two places (`BlockFlywheel.cs:52-92` vs
  `BlockEntityFlywheel.cs:105-106`) with no cross-check - see [mp-energy](mp-energy.md).
- Filler footprints and multiblock layouts are validated separately and never against each other. A
  megablock that is also part of a multiblock could declare a filler cell where its own layout demands a
  player-placed block, and neither builder would notice.
