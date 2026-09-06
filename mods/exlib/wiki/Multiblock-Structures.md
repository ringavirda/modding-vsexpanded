# Multiblock Structures

`Structures/` provides everything a multi-cell machine needs: completion monitoring, a
build-outline projection (ctrl+shift+right-click), crash-safe incomplete-part highlighting, and a
shared invisible **filler** block that gives a single-cell mega-block real per-cell collision.

There are two independent tools here - use one or both:

1. **The filler system** - make one logical block occupy several world cells with solid
   collision/selection, while all interaction routes back to the controller block.
2. **`BlockEntityMultiblockStructure`** - a base block entity that monitors whether a designed
   multiblock pattern is complete, runs production only while complete, and shows a build outline
   of missing parts.

## One grid, three uses

Both DSLs below - `MultiblockLayoutBuilder.Layer`/`Slice`/`Face` and
`FillerLayoutBuilder.Layer`/`Slice`/`Face` - draw over one core, `ExpandedLib.Structures.CellGrid`.
`Layer` is a floor plan (rows +Z, columns +X, one grid per Y level); `Slice` is a front elevation at
a fixed X (rows -Y from the top, columns +Z); `Face` is a front elevation at a fixed Z, looking
along -Z (rows -Y from the top, columns +X). A layout may mix them - draw the floor with `Layer` and
add a tall feature with `Slice` in the same builder.

```csharp
.Slice(0, """
           M##
           0##
           """)
```

draws a two-cell-tall, three-wide elevation at X=0: the top row's leftmost cell is `M`, the bottom
row's leftmost is the principal (`0`). `SymbolLegend<T>` is the matching legend core: it maps a
grid's symbols to whatever a builder resolves them into, with one duplicate-mapping policy (`Throw`
for a code-first layout, `Replace` for a filler footprint) and the "declared but never drawn" check.

## The filler system

A "mega-block" is one block whose model spans more than its own cell. By default the engine only
gives it collision in its own cell. The filler system fixes that by placing invisible
`exlib:structurefiller` blocks in the other footprint cells.

### Declaring the footprint

Your controller block implements `IFillerHost` and exposes the footprint through its
`fillerOffsets` attribute node. `BlockFilledMegastructure` already implements this by reading the
block's `Attributes["fillerOffsets"]`, so a block deriving from it only needs to supply that
attribute - either in JSON, or code-first from an `ExBlockDef` builder with
`FillerOffsets(IEnumerable<FillerCellSpec>)`, as `BlockBoilerCornish` does:

```csharp
.FillerOffsets(
  StructureFootprint.Layout(f =>
    f.Origin(-1, -5)
      .Slab('_', BlockFacing.DOWN)
      .Slab('M', BlockFacing.DOWN)
      .Solid('I')
      .Port('S', BlockFacing.UP, "pipe")
      .Port('E', BlockFacing.EAST, "pipe")
      .Layer(0, """
      # # E
      # # #
      ...
      """)
  )
)
```

`allowAttach` (default `false`) controls whether other blocks may attach to that filler cell.

### Placing and removing fillers

`StructureFillers` is the helper that resolves and manages footprint cells. Offsets are declared
in the block's north orientation and rotated to the placed angle for you.

```csharp
public static class StructureFillers
{
    public static AssetLocation FillerCode { get; set; }   // default "exlib:structurefiller"

    public static List<FillerOffset> ReadOffsets(JsonObject? offsetsNode);
    public static List<FillerCell> FootprintCells(IFillerHost principal, BlockPos principalPos, int angle);
    public static bool CanPlace(IWorldAccessor world, IEnumerable<FillerCell> cells);
    public static void PlaceFillers(IWorldAccessor world, BlockPos principalPos, IEnumerable<FillerCell> cells);
    public static void RemoveFillers(IWorldAccessor world, BlockPos principalPos, IEnumerable<FillerCell> cells);
}

public readonly struct FillerOffset { public Vec3i Offset { get; } public bool AllowAttach { get; } }
public readonly struct FillerCell   { public BlockPos Pos { get; } public bool AllowAttach { get; } }
```

Typical flow in the controller block:

```csharp
// In TryPlaceBlock: bail if the footprint isn't clear.
var cells = StructureFillers.FootprintCells(this, blockSel.Position, placeAngle);
if (!StructureFillers.CanPlace(world, cells)) { failureCode = "notenoughspace"; return false; }
// ...place the controller block, then:
StructureFillers.PlaceFillers(world, blockSel.Position, cells);

// In OnBlockBroken: clear the fillers linked to this controller.
StructureFillers.RemoveFillers(world, pos, cells);
```

`PlaceFillers`/`RemoveFillers` are server-side; `RemoveFillers` only clears cells actually linked
to the given principal, so neighbouring mega-blocks are safe.

> **Per-cell collision gotcha.** The filler block must declare `sidesolid: true` (and a real
> collision box) for the engine to treat each cell as solid. Without it the mega-block has only
> single-cell collision regardless of fillers.

### Per-cell interactions (optional)

If clicking different footprint cells should do different things, the controller implements
`IFillerInteractionTarget`. The filler forwards the click to the controller with the clicked cell:

```csharp
public interface IFillerInteractionTarget
{
    bool OnFillerInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection principalSel, BlockPos clickedCell);
    bool OnFillerInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection principalSel, BlockPos clickedCell);
    void OnFillerInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection principalSel, BlockPos clickedCell);
    WorldInteraction[] GetFillerInteractionHelp(IWorldAccessor world, BlockSelection principalSel, IPlayer forPlayer, BlockPos clickedCell);
}
```

`BlockStructureFiller` / `BlockEntityStructureFiller` (the invisible block and its entity) reroute
break, pick, drops, sounds, HUD info and interaction help to the controller automatically; the BE
stores the `Principal` position link plus optional network-port config (`PortFace`,
`PortNetworkType`) so a filler cell can even expose a network connector on the controller's behalf.

## Completion monitoring: `BlockEntityMultiblockStructure`

For designed multiblock machines (blast furnace, cowper stove, bessemer control) subclass
`BlockEntityMultiblockStructure`. It is the **form** alone: a monitor tick that detects
completion/breakage, and the completed pattern published as readiness through `IProductionReadiness`.

```csharp
public abstract class BlockEntityMultiblockStructure : BlockEntity, IProductionReadiness
{
    public bool StructureComplete { get; protected set; }
    protected virtual int CompletionTickMs { get; }          // monitor interval, default 3000ms
    protected virtual bool CanRunProduction { get; }         // production runs only while complete
    protected virtual bool StopsProductionOnStructureLost { get; }  // false keeps a breached machine ticking

    public virtual void Interact(IPlayer byPlayer);          // toggle the build-outline projection

    // You implement these:
    protected abstract void UpdateStructureRotation();
    protected abstract string GetIncompleteMessage(int missingCount);
    protected abstract string GetCompleteMessage();

    // Optional hooks:
    protected virtual void OnStructureLost();                // complete -> incomplete
    protected virtual void OnStructureCompleted();           // incomplete -> complete
    protected virtual BlockPos GetGlobalPos(int localX, int localY, int localZ);

    protected void SetStructureAngle(int angle, int initAngleOffset = 0);   // canonical UpdateStructureRotation body
}
```

### From JSON only

A machine that is just a designed shape - no production tick, no per-cell behaviour - needs no C#
at all. Three rungs, shortest first:

1. **Zero-config**: `"class": "ExFilledMegastructure"` plus `"entityClass": "ExMultiblock"` and the
   `MultiblockStructure` behaviour give per-cell collision, the completion monitor and the
   incomplete/complete messages out of the box.
2. **Declarative**: an `attributes.multiblockLayout` ASCII grid - the JSON twin of
   `MultiblockLayoutBuilder` - states the shape in one place. `fillerOffsets` is derived from it (every
   drawn cell but the principal's own) unless you declare your own.

```json
{
  "class": "ExFilledMegastructure",
  "entityClass": "ExMultiblock",
  "behaviors": [{ "name": "MultiblockStructure" }],
  "attributes": {
    "multiblockLayout": {
      "origin": [1, 0],
      "legend": { "C": "mymod:kiln-core", "B": "mymod:kiln-brick" },
      "layers": [["BBB", "BCB", "BBB"], ["B.B", "...", "B.B"]],
      "core": "C"
    }
  }
}
```

Orientation follows the block's own `side`/`orientation` variant; the messages are your domain's own
`multiblock-<blockpath>-incomplete`/`-complete` lang keys when you declare them, else exlib's own. A
malformed `multiblockLayout` logs one Error naming the block and the problem, and the structure never
completes - it never throws at chunk load.

3. **The explicit API**: for anything the grid cannot say - roles, connectors, oriented parts, a
   production tick - drop to the code-first `ExBlockDef` builder above, or subclass
   `BlockEntityMultiblockStructure` yourself.

## Multiblocks that also produce: `BlockEntityMultiblockMachine`

Running a production tick is a separate choice from being a multiblock. Subclass
`BlockEntityMultiblockMachine` to take it on: it hosts the same
[`BEBehaviorProductionMachine`](Production-Machines) every other machine runs, registers the tick on
load only when the structure is already complete, and lets the monitor tick start and stop it across
completion transitions. A structure that only has to be built subclasses the form above and carries
no tick at all.

```csharp
public abstract class BlockEntityMultiblockMachine : BlockEntityMultiblockStructure
{
    protected virtual int ProductionTickMs { get; }          // tick interval, default 1000ms
    protected virtual bool AutoStartProduction { get; }      // register on load if already complete
    protected virtual int MaxAwayCatchupSteps { get; }       // 0 disables the unloaded-time replay

    protected abstract void OnProductionTick(float dt);
    protected virtual void OnIdleProductionTick(float dt);
}
```

A minimal subclass:

```csharp
[BlockEntityRegister]
public class BlockEntityBlastFurnace : BlockEntityMultiblockMachine
{
    protected override void UpdateStructureRotation()
        => SetStructureAngle(ExOrientation.AngleFromSide(Block.Variant["side"]));

    protected override string GetIncompleteMessage(int missingCount)
        => Lang.Get("siex:blastfurnace-incomplete", missingCount);

    protected override string GetCompleteMessage()
        => Lang.Get("siex:blastfurnace-complete");

    protected override void OnProductionTick(float dt) { /* smelt while complete */ }
}
```

### How completion is wired

The actual pattern (which cells must be which blocks) is a vanilla **`multiblockStructure`**
JSON definition referenced by your block. `SetStructureAngle` loads that JSON, calls the engine's
`InitForUse` at the right angle, and clears any stale projection - this is the canonical body for
`UpdateStructureRotation`. The monitor tick re-checks completeness on `CompletionTickMs` and fires
`OnStructureCompleted` / `OnStructureLost` on transitions.

> **`GetGlobalPos` / `_currentAngle` invariant.** The base `GetGlobalPos(angle)` is equivalent to
> `InitForUse(angle)`, so the angle you pass to `SetStructureAngle` must match the structure's
> `_currentAngle`. A mismatch shows up as the build outline appearing rotated 180°.

## The build-outline behaviour

`BlockBehaviorMultiblockStructure` is a `BlockBehavior` (not a base class) that centralises the
ctrl+shift+right-click toggle of the missing-block hologram. Add it to your block's behaviours,
**before** any other right-click consumer, and gate it on the structure being incomplete:

```jsonc
"behaviors": [ { "name": "MultiblockStructure" } ]
```

It calls back into the BE's `Interact`, which re-checks completeness, shows the build outline of
missing parts (or clears it on completion). The highlighting is a **crash-safe reimplementation**:
vanilla's `HighlightIncompleteParts` throws `IndexOutOfRange` when a wanted `blockNumbers` code
resolves to no block, so the base falls back to a neutral tint instead of crashing the client.

> **`blockNumbers` validity.** Every offset in your `multiblockStructure` definition needs a
> `blockNumbers` entry that resolves to at least one real block, or the build outline (and vanilla
> highlighting) misbehaves.

## Orientation-checked parts

Vanilla rotates a structure's **offsets** through `InitForUse(angle)` but never its **codes**. So a
layout that asked for `brickslabs-fire-south-free` would demand a *south*-facing slab at every structure
angle - wrong three times out of four. The only workable answer used to be `-*`, "any rotation", which is
how a structure could report itself complete while its walls still had visible gaps in them.

A legend code containing a whole horizontal side segment (`north`/`south`/`east`/`west`, or the letters
`n`/`s`/`e`/`w`) is now **orientation-checked**: the required facing rotates with the structure. Layouts
stay authored in the north-default frame, like everything else.

```csharp
.Legend('i', "game:brickslabs-fire-south-free")  // south at angle 0, east at 90, ...
.Legend('-', "game:brickslabs-fire-up-free")     // vertical - a Y rotation cannot move it
.Legend('#', "game:claybricks-good-fire")        // no facing - untouched
.LegendAnyFacing('x', "mod:thing-north")         // opt out: accept the literal code at any angle
```

How it works, and why it costs almost nothing:

- `MultiblockLayoutBuilder` records **which dash-segment** of each oriented code is the facing word, into
  a sibling attribute `attributes.multiblockFacings`. It is a *sibling* rather than a member of
  `multiblockStructure`, because that object is deserialised by vanilla's own `MultiblockStructure` and
  must stay exactly its schema.
- `MultiblockFacings.Rotate` swaps that segment for the structure-rotated one
  (`ExOrientation.RotateSideWord`, the string counterpart of `RotateFacing` and sharing its convention:
  a part authored `north` reads as the side whose `AngleFromSide` equals the structure's angle).
- `BlockEntityMultiblockStructure.IncompleteBlockCount` replaces vanilla's `InCompleteBlockCount` and
  routes every wanted code through that rotation. The build outline shares the same resolver, so the
  tint and the count can never disagree - and an oriented slot now resolves to a **real, correctly-facing
  block**, so the outline colours from the right variant and the missing-blocks report names it.

Three things to know:

- **Only whole segments count.** `westward` or a `-we-` axis token is not mistaken for a facing, and the
  *last* matching segment wins, because block codes put the orientation at the end.
- **Codes are keyed in full domained form.** `AssetLocation.ToShortString()` elides `game:`, so a table
  keyed the way an author typed it would never match a vanilla block at runtime.
- **Trapdoors cannot be checked this way.** `game:trapdoor` keeps its facing and open/closed state in its
  *block entity*, not its code, so no code match can see it. Slabs, stairs, doors and `cokeovendoor` all
  carry theirs in the code and work fine.

A layout that declares no oriented part emits no attribute at all and behaves exactly as before - which
is why this needed no migration and changed no shipped structure's goldens.

## Cell roles

A `CellRole` is an open string key naming what a layout cell is for - exlib declares none of its own.
Mint one with `CellRole.Of`, mark it on a glyph with `MultiblockLayoutBuilder.Role`, and read it back
through `BlockEntityMultiblockStructure.CellsWithRole`:

```csharp
public static readonly CellRole Tuyere = CellRole.Of("tuyere");
// ...
.Role('t', Tuyere)
// ...
IReadOnlyList<BlockPos> tuyereCells = CellsWithRole(Tuyere);
```

Pass `single: true` to `CellRole.Of` if a layout may give the role at most one cell - the arity is
then enforced at build time and a consumer may call `CellsWithRole(role).Single()` without risk.

## Related pages

- [Production Machines](Production-Machines) - the tick lifecycle this builds on.
- [Block Networks](Block-Networks) - a structure that is also a network node must call `AddNode`/`RemoveNode` itself.
- [Helpers & Renderers](Helpers-and-Renderers) - `ExOrientation` for the rotation math; `SurfaceRenderer` for fluid surfaces.
