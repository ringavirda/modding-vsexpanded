# Block Entities

`Blocks/ExBlockEntity.cs` and `Blocks/ExBlockState.cs` give a plain block entity its save/load pair
for free, so a field named once is enough - no hand-written `ToTreeAttributes`/`FromTreeAttributes`
pair that spells the same key twice and can drift out of sync.

## `ExBlockEntity`

A block entity base whose whole state is declared, not hand-written:

```csharp
public abstract class ExBlockEntity : BlockEntity
{
    protected ExBlockState Persisted { get; }               // built lazily on first use
    protected virtual void DeclareState(ExBlockState state) { }
}
```

`ToTreeAttributes`, `FromTreeAttributes`, `OnStoreCollectibleMappings` and
`OnLoadCollectibleMappings` all call `base` then run through `Persisted`, so a declared field gets the
save, the load, the client sync and the schematic-paste collectible remap in one place.

A block entity whose base slot is already spent - a container, a multiblock, a network node - is
not locked out: `BlockEntityProductionMachine`, `BlockEntityMultiblockStructure`,
`BlockEntityNetworkNode` and `BlockEntityMachineStation` each carry the same `Persisted`/`DeclareState`
pair, layered on top of whatever they already write by hand (see
[Production Machines](Production-Machines), [Multiblock Structures](Multiblock-Structures),
[Block Networks](Block-Networks)); so does `ExBlockEntityContainer` for a plain `BlockEntityContainer`
(below). A block entity with none of those bases builds an `ExBlockState` directly and calls
`ToTree`/`FromTree` from its own overrides - `ExBlockEntity` is the convenience, not the mechanism.

## Behaviours

A `BlockEntityBehavior` gets the same convenience through `ExBlockEntityBehavior`:

```csharp
public abstract class ExBlockEntityBehavior : BlockEntityBehavior
{
    protected ExBlockState Persisted { get; }               // built lazily on first use
    protected virtual void DeclareState(ExBlockState state) { }
}
```

Vanilla fans a block entity's `ToTreeAttributes`/`FromTreeAttributes` out over its `Behaviors` against
the same flat tree the host itself writes into, so a behaviour's keys, its host's keys and a sibling
behaviour's keys all share one key space. `ExBlockState` only catches a key declared twice inside one
state; a key this behaviour declares that its host or another behaviour on the same host also happens
to write is not caught here and collides silently - pick keys that are unambiguous across the whole
host, not just within the behaviour.

## Containers

A block entity based on `BlockEntityContainer` gets it through `ExBlockEntityContainer`:

```csharp
public abstract class ExBlockEntityContainer : BlockEntityContainer
{
    protected ExBlockState Persisted { get; }               // built lazily on first use
    protected abstract void DeclareState(ExBlockState state);
}
```

`ToTreeAttributes`, `FromTreeAttributes` and the collectible-mapping pair call `base` first - which is
`BlockEntityContainer`'s own inventory serialization - then run `Persisted` on top, so a declared field
sits beside the inventory without touching how it saves.

## Three rungs

1. **`[Persist]`** - mark the field, write nothing else.
   ```csharp
   [Persist] private float _tempC;
   ```
2. **`Persisted`** - override `DeclareState` for a custom key, a computed accessor, or a value with
   its own tree shape.
   ```csharp
   protected override void DeclareState(ExBlockState s) =>
       s.Float("temp", () => _tempC, v => _tempC = v);
   ```
3. **Hand-written** - override the four methods yourself and call `base`. Nothing here forces
   the other two rungs onto an existing block entity.

The three combine on one type: a `[Persist]` field and a `DeclareState` entry both land in the
same `Persisted`, and a hand-written override still reaches it through `base`.

## `[Persist]` and `PersistScan`

```csharp
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class PersistAttribute(string? key = null) : Attribute
{
    public string? Key { get; }        // defaults to the member name, leading underscore stripped
    public string? Legacy { get; init; } // an older key, read only when Key is absent; never written
}
```

Supported member types: `bool`, `int`, `long`, `float`, `double`, `string`, an enum (stored as its
underlying `int`), `BlockPos`, `ItemStack`, and `IPersistable`. A field or auto-property of any
other type throws `NotSupportedException` naming the member the first time the block entity's
`Persisted` is built - a modder sees it on first placement, not silently.

`PersistScan.Declare(this, state)` runs before `DeclareState` on every base above, so an attribute
and a hand-written entry never conflict for the same key. The reflection walk (base types first)
and the compiled field/property accessors are built once per concrete type and cached; every
instance of that type reuses them.

```csharp
private class RenamedField : ExBlockEntity
{
    [Persist(Legacy = "isPouring")] private bool _plugged;
    // A save from before the rename still loads: isPouring is read only when plugged is
    // absent. Every save after this point writes plugged alone.
}
```

## `IPersistable`

```csharp
public interface IPersistable
{
    void ToTree(ITreeAttribute tree);                       // writes into a tree private to this member
    void FromTree(ITreeAttribute tree, IWorldAccessor world); // mutates in place
}
```

A `[Persist]` member of this type is stored under its own key as a nested tree - the same shape
`ExBlockState.Tree` gives a hand-declared field that manages several attributes at once (a
`MoltenCharge`, for instance). `FromTree` mutates the existing instance rather than replacing it,
so a field of this type is instantiated once at declaration and never reassigned by the scan.

## `ExBlockState`

The declaration surface both rungs above build on:

```csharp
public sealed class ExBlockState
{
    public ExBlockState Bool(string key, Func<bool> get, Action<bool> set);
    public ExBlockState Int(string key, Func<int> get, Action<int> set);
    public ExBlockState Long(string key, Func<long> get, Action<long> set);
    public ExBlockState Float(string key, Func<float> get, Action<float> set);
    public ExBlockState Double(string key, Func<double> get, Action<double> set);
    public ExBlockState String(string key, Func<string?> get, Action<string?> set);
    public ExBlockState Enum<T>(string key, Func<T> get, Action<T> set) where T : struct, Enum;
    public ExBlockState Pos(string key, Func<BlockPos?> get, Action<BlockPos?> set);
    public ExBlockState Stack(string key, Func<ItemStack?> get, Action<ItemStack?> set);
    public ExBlockState Tree(string key, Action<ITreeAttribute> write,
                              Action<ITreeAttribute, IWorldAccessor> read);

    public IReadOnlyList<string> Keys { get; }              // declared keys, in declaration order
}
```

Every method returns `this`, so a `DeclareState` override chains them. `Stack` also carries the
declared stack's collectible id mapping both ways - what makes a block entity survive being pasted
into another world. `Tree` is the escape hatch for a value that manages several attributes on the
same tree (a `MoltenCharge`) or builds a genuinely nested sub-tree itself (see `IPersistable`
above); `key` only names the declaration for the duplicate-key guard, so the callback is free to
choose its own attribute names. Declaring the same key twice throws `InvalidOperationException` at
declaration time, not on the first mismatched save.

## Converting a hand-written pair

Turning an existing `ToTreeAttributes`/`FromTreeAttributes` pair into `[Persist]`/`Persisted` must not
move, rename or retype a single key - a `TreeKeys` golden (below) is the proof. Per field:

- A field written under key `K` with a plain get/set becomes `[Persist("K")] private float _x;`. The
  default key is the member name with a leading underscore stripped, so write the key explicitly
  whenever it differs, and keep it even when it matches if that makes the mapping obvious.
- An enum stored as `int` becomes a `[Persist]` enum field; `PersistScan` already stores it that way.
- A `BlockPos` written as three ints under `kX`/`kY`/`kZ` is `[Persist]` on the `BlockPos?` member
  directly if `ExBlockState.Pos`'s three-key shape matches; check before assuming it does.
- A legacy fallback (an old key read only when the new one is absent, never written) is
  `[Persist("new", Legacy = "old")]` when the semantics match exactly; anything more - a default
  read that differs from the primitive's own, a negated flag, several attributes read as one unit,
  a value written unconditionally where `ExBlockState.Stack`/`String` would skip a null - stays a
  `state.Tree(key, write, read)` with the old body moved in unchanged.
- A nested value's own `ToTree(tree, key)`/`FromTree(tree, key, world)` pair that writes flat keys
  (not one sub-tree) stays a `Tree` declaration too; only a value that nests under one key is
  `IPersistable`.
- Anything the pair does besides reading and writing values - marking dirty, recomputing derived
  state, calling `base` in a subclass chain - stays as a `FromTreeAttributes` override that calls
  `base` first, with no key reads left in it. A pair that only forwards to `base` is dropped
  entirely.
- A subclass whose base already overrides `DeclareState` with real declarations must call
  `base.DeclareState(state)` before adding its own - most of the `Persisted` bases below
  `ExBlockEntity` make it `virtual`, not `abstract`, so skipping the call silently drops the base's
  fields. `TreeKeys.AssertDeclaresBaseKeys` (below) is the guard for exactly this.
- A `BlockEntityBehavior` converts through `ExBlockEntityBehavior`, a `BlockEntityContainer` through
  `ExBlockEntityContainer` (see Behaviours and Containers above). A class on some other vanilla base
  with no exlib base carrying `Persisted` stays hand-written; converting it would mean re-parenting
  it, which is a behaviour change, not a persistence one.

`ExpandedLib.Testing.TreeKeys.AssertGolden(be, domain)` is the proof: it reads back the sorted,
type-tagged key list a fresh instance's `ToTreeAttributes` writes and compares it against a golden
blessed before the conversion. A changed golden after converting a class means the conversion moved
a key or a type, not that the golden is stale.

`ExpandedLib.Testing.TreeKeys.AssertDeclaresBaseKeys(be)` catches the other failure mode a golden
cannot: a subclass override that skips its `base.DeclareState(state)` call. For every type in `be`'s
hierarchy that overrides `DeclareState`, it invokes that level's override alone - non-virtually,
bypassing whatever overrides it further down - against a fresh `ExBlockState`, and asserts every key
that level declares also shows up in the instance's real, built `Persisted` state. A golden alone
would not catch this: `PersistScan`'s `[Persist]` scan runs independently of `DeclareState` and
always contributes its keys regardless of what the override chain does.

## Related pages

- [Production Machines](Production-Machines) - the `Persisted` rung on `BlockEntityProductionMachine`.
- [Multiblock Structures](Multiblock-Structures) - the `Persisted` rung on `BlockEntityMultiblockStructure`.
- [Block Networks](Block-Networks) - the `Persisted` rung on `BlockEntityNetworkNode`.
- [Helpers & Renderers](Helpers-and-Renderers) - "Finding block entities" (`ExBlockAccess`), "Which
  side" (`ExSide`), "Reading a click" (`ExInteraction`) and "Block info lines" (`ExInfo`), the
  convenience layer over the lookups, side checks, interactions and `GetBlockInfo` lines a block
  entity writes.
