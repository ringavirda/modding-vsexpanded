# Multiblock Structures

`Blocks/Structures/` provides everything a multi-cell machine needs: completion monitoring, a
build-outline projection (ctrl+shift+right-click), crash-safe incomplete-part highlighting, and a
shared invisible **filler** block that gives a single-cell mega-block real per-cell collision.

There are two independent tools here - use one or both:

1. **The filler system** - make one logical block occupy several world cells with solid
   collision/selection, while all interaction routes back to the controller block.
2. **`BlockEntityMultiblockStructure`** - a base block entity that monitors whether a designed
   multiblock pattern is complete, runs production only while complete, and shows a build outline
   of missing parts.

## The filler system

A "mega-block" is one block whose model spans more than its own cell. By default the engine only
gives it collision in its own cell. The filler system fixes that by placing invisible
`exlib:structurefiller` blocks in the other footprint cells.

### Declaring the footprint

Your controller block implements `IFillerHost` and declares its footprint cells in JSON via a
`fillerOffsets` attribute. The simplest implementation is to let the
[attribute generator](Source-Generators) surface the attribute:

```csharp
public interface IFillerHost
{
    JsonObject? FillerOffsets { get; }   // the block's `fillerOffsets` JSON node, or null
}
```

```jsonc
// in your blocktype attributes
"fillerOffsets": [
  { "x": 1, "y": 0, "z": 0, "allowAttach": true },
  { "x": 0, "y": 1, "z": 0 }
]
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
`BlockEntityMultiblockStructure`. It extends [`BlockEntityProductionMachine`](Production-Machines),
adding a monitor tick that detects completion/breakage and gates production on it.

```csharp
public abstract class BlockEntityMultiblockStructure : BlockEntityProductionMachine
{
    public bool StructureComplete { get; protected set; }
    protected virtual int CompletionTickMs { get; }          // monitor interval, default 3000ms
    protected override bool CanRunProduction { get; }        // production runs only while complete
    protected virtual bool AutoStartProduction { get; }      // register production tick on load if already complete

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

A minimal subclass:

```csharp
[BlockEntityRegister]
public class BlockEntityBlastFurnace : BlockEntityMultiblockStructure
{
    protected override void UpdateStructureRotation()
        => SetStructureAngle(ExOrientation.AngleFromSide(Block.Variant["side"]));

    protected override string GetIncompleteMessage(int missingCount)
        => Lang.Get("smex:blastfurnace-incomplete", missingCount);

    protected override string GetCompleteMessage()
        => Lang.Get("smex:blastfurnace-complete");

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

## Related pages

- [Production Machines](Production-Machines) - the tick lifecycle this builds on.
- [Block Networks](Block-Networks) - a structure that is also a network node must call `AddNode`/`RemoveNode` itself.
- [Helpers & Renderers](Helpers-and-Renderers) - `ExOrientation` for the rotation math; `SurfaceRenderer` for fluid surfaces.
