# Production Machines

`Blocks/Machines/` gives any block entity a clean periodic-work lifecycle, plus helpers for
reading and feeding [block networks](Block-Networks) through connector faces. This is **not**
multiblock-only - engines, sub-machines, furnaces and converters all build on it.

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

    // Network port access (typed):
    protected TNet? ConnectedNetwork<TNet>(BlockFacing face) where TNet : BlockNetwork;
    protected TNet? NetworkAt<TNet>(BlockPos pos) where TNet : BlockNetwork;
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
neighbour across `connectorFace` actually exposes a matching connector back. The same two helpers
are exposed as `protected` methods directly on `BlockEntityProductionMachine` for convenience.

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

## Related pages

- [Block Networks](Block-Networks) - what `ConnectedNetwork<TNet>` returns.
- [Multiblock Structures](Multiblock-Structures) - `BlockEntityMultiblockStructure` extends this base.
