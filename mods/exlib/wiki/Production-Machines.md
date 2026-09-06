# Production Machines

`Machines/` gives any block entity a clean periodic-work lifecycle, plus helpers for
reading and feeding [block networks](Block-Networks) through connector faces. This is **not**
multiblock-only - engines, sub-machines, furnaces and converters all build on it.

> ⚠ **The base class is not the only way in.** Form, process and membership are three independent
> axes: the base-class slot belongs to **form** (what the block *is*), while the production tick and
> network membership are **behaviours**. `BlockEntityProductionMachine` below is the base a machine
> derives from when form has nothing else to claim; a block entity whose form is already spoken for -
> a container, a multiblock part - attaches the production behaviour and keeps its base. Do not spend
> the slot twice.

## `BlockEntityProductionMachine`

A base block entity that owns a server-side production tick. You write the per-tick logic and the
operational gate; it handles registration, idle routing and teardown.

```csharp
public abstract class BlockEntityProductionMachine : BlockEntity
{
    protected virtual int ProductionTickMs { get; }          // tick interval, default 1000ms
    protected abstract bool CanRunProduction { get; }        // operational gate
    protected virtual bool AutoStartProduction { get; }      // register tick in Initialize? default true

    protected abstract void OnProductionTick(float dt);      // runs server-side while CanRunProduction
    protected virtual void OnIdleProductionTick(float dt);   // runs instead when not operational (default no-op)

    protected void StartProductionTick();                    // idempotent, server-side
    protected void StopProductionTick();
}
```

Minimal machine:

```csharp
[BlockEntityRegister]
public class BlockEntityKiln : BlockEntityProductionMachine
{
    protected override bool CanRunProduction => HasFuel && HasInput;

    protected override void OnProductionTick(float dt)
    {
        // Advance the smelt. Runs once per ProductionTickMs while CanRunProduction is true.
    }
}
```

When `CanRunProduction` is `false` the tick routes to `OnIdleProductionTick` instead of stopping,
so you can still run cooldown/settling logic. Override `AutoStartProduction` to `false` if the
machine should register its tick only on a state change rather than on load (e.g. a machine that
is dormant until switched on); then call `StartProductionTick()` / `StopProductionTick()` yourself.
The tick is stopped automatically in `OnBlockRemoved` and `OnBlockUnloaded`.

The tick itself lives in `BEBehaviorProductionMachine`, which this class hosts; a block entity whose
one base slot is already spent hosts the same behaviour instead of deriving from here:

```csharp
public class BlockEntityRollingMill : BlockEntityNetworkNode, IProductionReadiness
{
    // In the constructor, not Initialize: BlockEntity fans both FromTreeAttributes and Initialize
    // out over Behaviors, and a process added later misses whichever has already run.
    public BlockEntityRollingMill() => Behaviors.Add(new HostProcess(this));

    private sealed class HostProcess(BlockEntityRollingMill owner)
        : BEBehaviorProductionMachine(owner)
    {
        protected override int ProductionTickMs => 250;
        protected override void OnProductionTick(float dt) => owner.OnPassTick(dt);
    }

    public bool IsReadyToProduce => IsRolling;          // the gate, published not overridden
    public bool StopsProductionWhenNotReady => false;   // an idle mill keeps its clock
}
```

The gate goes through `IProductionReadiness` rather than an override, because a machine may publish
more than one answer and the process reads every publisher on the block entity - the block entity
itself and any of its behaviours:

| Publisher | Answers | Opt-out |
|---|---|---|
| `BlockEntityProductionMachine` | `CanRunProduction` | override the gate |
| `BlockEntityMultiblockStructure` | `StructureComplete` | `StopsProductionOnStructureLost` |
| `ExRightClickConstructable` ([Construction](Construction)) | `IsComplete` | `gatesProduction: false` |

A host that enables away-catch-up must also save the process's `LastTickHours` in its own
`ToTreeAttributes` and hand it back through `RestoreLastTickHours` - a behaviour's tree lands in the
block entity's flat tree, so exactly one of the two may write that key.

## Machine ports

A fixed machine is usually **not** a network node - it's a `INetworkConnector` whose outlet/intake
face touches a pipe. The network it interacts with lives in the cell on the **far side** of that
face. `MachinePorts` are extension methods that resolve it:

```csharp
public static class MachinePorts
{
    public static BlockNetworkModSystem? NetworkSystem(this BlockEntity be);
    public static TNet? ConnectedNetwork<TNet>(this BlockEntity be, BlockFacing connectorFace) where TNet : BlockNetwork;
    public static TNet? NetworkAt<TNet>(this BlockEntity be, BlockPos pos) where TNet : BlockNetwork;
}
```

```csharp
protected override void OnProductionTick(float dt)
{
    // The steam network plumbed into our north outlet (null if nothing is connected there).
    var steam = this.ConnectedNetwork<PipeNetwork>(BlockFacing.NORTH);
    if (steam is null) return;
    steam.State /* ... draw steam, push condensate ... */;
}
```

`ConnectedNetwork` performs the reciprocal-connector test: it returns the network only if the
neighbour across `connectorFace` actually exposes a matching connector back. These are extensions on
any `BlockEntity`, so reading a port is not something a machine inherits: a plain block entity calls
them exactly as a production machine does.

> Air blower, engine, boiler outlet and converter intake are all `INetworkConnector` ports, not
> nodes - they read/write the network in the adjacent cell rather than being part of the graph.

## `GraceTimer`

A reusable "hold a condition for N seconds, then fire once" accumulator - hysteresis for
over-pressure, choke, burst grace and similar one-shot thresholds.

```csharp
public struct GraceTimer
{
    public float Elapsed { get; }          // seconds the condition has held continuously
    public bool IsCounting { get; }

    public bool Update(bool active, float dt, float threshold);   // true once when Elapsed >= threshold, then resets
    public void Reset();
    public float Remaining(float threshold);
    public void ToTree(ITreeAttribute tree, string key);
    public void FromTree(ITreeAttribute tree, string key);
}
```

```csharp
private GraceTimer _overPressure;

protected override void OnProductionTick(float dt)
{
    if (_overPressure.Update(pressure > BurstLimit, dt, GraceSeconds))
        Explode();   // fires exactly once after pressure stays over the limit for GraceSeconds
}
```

Any tick with `active: false` resets the accumulator, so the condition must hold *continuously*.
Persist it with `ToTree`/`FromTree` so a near-burst boiler doesn't reset its grace across a reload.

## Saved state

`BlockEntityProductionMachine` carries the same `Persisted`/`DeclareState` convenience as
`ExBlockEntity` (see [Block Entities](Block-Entities)), layered on top of the `pm_lastHours`
away-catch-up stamp it already writes by hand. Three rungs, in the order to reach for them:

1. **Attribute.** Mark the field `[Persist]` and write nothing else - the key defaults to the
   field name with its leading underscore stripped.
   ```csharp
   [Persist] private float _tempC;
   ```
2. **`Persisted`.** Override `DeclareState` for anything the attribute can't express (a computed
   getter/setter pair, a custom key, a value with its own tree shape).
   ```csharp
   protected override void DeclareState(ExBlockState s) =>
       s.Float("temp", () => _tempC, v => _tempC = v);
   ```
3. **Hand-written.** Override `ToTreeAttributes`/`FromTreeAttributes` yourself and call `base` -
   nothing here forces the other two rungs; a machine that already has the pair keeps it.

## Related pages

- [Block Networks](Block-Networks) - what `ConnectedNetwork<TNet>` returns.
- [Multiblock Structures](Multiblock-Structures) - `BlockEntityMultiblockMachine` is the multiblock
  that hosts the same process.
- [Block Entities](Block-Entities) - `ExBlockEntity`, `ExBlockState`, `[Persist]` and `IPersistable`.
