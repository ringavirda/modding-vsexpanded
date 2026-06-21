# Testing API Reference

Full public surface of `ExpandedLib.Testing`. For setup and worked examples see the
**[Testing Harness](Testing-Harness)** page. Signatures are taken from
`test/ExpandedLib.Testing/`.

## `TestWorld`

Headless, in-process stand-in for a server world: in-memory block/BE store, a live
`BlockNetworkModSystem`, and NSubstitute-faked accessors/API.

```csharp
public sealed class TestWorld
{
    public TestWorld();

    // State:
    public Block Air { get; }
    public BlockNetworkModSystem Networks { get; }
    public IBlockAccessor Accessor { get; }
    public IServerWorldAccessor World { get; }
    public IGameCalendar Calendar { get; }
    public ICoreServerAPI Api { get; }
    public List<ItemStack> Drops { get; }

    // Setup:
    public TestWorld Attach(BlockEntity be);
    public TestWorld Initialize(BlockEntity be);
    public TestWorld RegisterNetwork(string networkType, Func<BlockNetworkModSystem, BlockNetwork> factory);
    public TestWorld Place(BlockPos pos, Block block, BlockEntity? be = null);
    public TestWorld RegisterBlockEntityFactory(string classname, Func<BlockEntity> factory);
    public TestWorld Register(Block block);
    public Item RegisterItem(string code, float meltingPoint = 0f);
    public Item? GetItem(AssetLocation? code);
    public Item? GetItem(int id);

    // Store access:
    public Block GetBlock(BlockPos pos);
    public BlockEntity? GetBlockEntity(BlockPos pos);

    // Graph:
    public void AddNode(BlockPos pos, string networkType);
    public void RemoveNode(BlockPos pos);
    public BlockNetwork? NetworkAt(BlockPos pos);

    // Time:
    public void Tick(int seconds = 1);                       // mirrors BlockNetworkModSystem.OnServerTick: OnTick(1f) per network
    public void FireBlockEntityTicks(float dt = 1f, int times = 1);   // fire RegisterGameTickListener callbacks
    public void AdvanceDays(double days);
}
```

`Tick` advances the network simulation; `FireBlockEntityTicks` fires the listeners block entities
registered via `RegisterGameTickListener` (captured from the fake event API). It supports both the
1.22 `RegisterGameTickListener` overload (with `BlockPos`) and the legacy 1.20/1.21 one via
`#if GAME_GE_1_22`.

## `Scene`

Composition layer over `TestWorld`: lay out blocks/nodes/machines, advance them together, read
back state.

```csharp
public sealed class Scene
{
    public TestWorld World { get; }

    public Scene Network(string type, Func<BlockNetworkModSystem, BlockNetwork> factory);
    public Scene Block(BlockPos pos, Block block);
    public Scene Node(BlockPos pos, Block block, BlockEntity be, string networkType);
    public Scene Machine(BlockPos pos, Block block, BlockEntity be);
    public Scene Fill(BlockPos a, BlockPos b, Block block);
    public Scene Build();                                    // add all queued nodes to the graph (call once)
    public void Step(int seconds = 1);                       // fire BE ticks then network ticks, per second
    public TNet? NetworkAt<TNet>(BlockPos pos) where TNet : BlockNetwork;
    public TBe? EntityAt<TBe>(BlockPos pos) where TBe : BlockEntity;
}
```

Flow: `Network(...)` -> `Block`/`Node`/`Machine`/`Fill(...)` -> `Build()` once -> `Step(...)` -> query
with `NetworkAt<T>` / `EntityAt<T>`.

## `SceneDiagram`

Turns ASCII layouts into placements. Columns advance +X, rows advance +Z; each `Layer` sits at a
fixed Y; `Stack` indexes layers bottom-to-top.

```csharp
public sealed class SceneDiagram
{
    public SceneDiagram On(char glyph, Action<BlockPos> place);
    public SceneDiagram Layer(string ascii, int y = 0, int originX = 0, int originZ = 0);
    public SceneDiagram Stack(int baseY, int originX, int originZ, params string[] layers);
    public SceneDiagram Stack(int baseY, params string[] layers);
}
```

## `VsAssemblyResolver`

```csharp
public static class VsAssemblyResolver
{
    public static void Register();   // idempotent; hooks AppDomain.AssemblyResolve for the game DLLs
}
```

Resolves the install from `[AssemblyMetadata("GameInstallEnv")]` (env var such as `VINTAGE_STORY`,
`VINTAGE_STORY_121`) or by walking up to `.game/<slug>` (slug from `[AssemblyMetadata("GameSlug")]`,
e.g. `1.22`). Probes the install root, `Lib/` and `Mods/`. Call from a `[ModuleInitializer]` before
any test type loads. The harness itself also has an internal module initializer that calls it, but
your test project should call it (plus `TestLang.Init`) explicitly - that is the established pattern.

## `TestLang`

```csharp
public static class TestLang
{
    public static void Init();   // idempotent; registers an echo-the-key "en" translation service
}
```

Returns the key unchanged from `Lang.Get`, so assert on numeric payloads, not localized labels.

## `ReflectionHelpers`

```csharp
public static class ReflectionHelpers
{
    public static void SetProperty(object target, string propertyName, object? value);
    public static void SetField(object target, string fieldName, object? value);
    public static object? GetField(object target, string fieldName);
    public static object? Invoke(object target, string methodName, params object?[] args);
}
```

All walk the base-class hierarchy and access non-public members.

## `TestBlocks`

```csharp
public static class TestBlocks
{
    public static T Configure<T>(T block, string code, int id, params (string key, string value)[] variants) where T : Block;
}
```

Assigns `Code` and `BlockId`, populates `VariantStrict` from the pairs, and wraps it in a relaxed
dictionary (absent keys return `null` instead of throwing). Returns the block for chaining - this
is how you build blocks without the asset-load pipeline.

```csharp
var pipe = TestBlocks.Configure(new BlockPipe(), "ppex:pipe-straight-iron-ns", id: 1,
    ("material", "iron"), ("type", "straight"), ("orientation", "ns"));
```

## Test doubles (`ExpandedLib.Testing.Doubles`)

### `StubNetwork : BlockNetwork`

Medium-less concrete network for exercising the graph engine (add/remove/merge/fracture/rebuild)
without gameplay state.

```csharp
public sealed class StubNetwork : BlockNetwork
{
    public StubNetwork(BlockNetworkModSystem system, string networkType = "test");
    public override string NetworkType { get; }
    public string? Tag { get; set; }   // arbitrary marker that survives merge/split/inherit - assert state propagation
    public override void OnMerge(BlockNetwork other, IBlockAccessor world);
    public override void OnSplitFragment(BlockNetwork original, IBlockAccessor world);
    public override void InheritStateFrom(BlockNetwork source);
    public override void OnTick(IBlockAccessor world, float dt, BlockNetworkModSystem manager);   // empty
}
```

### `TestNetworkBlock : BlockNetworkNode`

Minimal concrete node block with a configurable connector set; bypasses asset loading.

```csharp
public sealed class TestNetworkBlock : BlockNetworkNode
{
    public override string NetworkType { get; }
    public override Dictionary<string, string[]> AllowedOrientations { get; }
    public static TestNetworkBlock Create(string networkType, string orientation, int id, string? code = null);
}
```

The `orientation` string is the connector set (`"ns"`, `"we"`, `"nswe"`). Default code is
`test:{networkType}-{orientation}-{id}`.

### `CapturingNode : BlockEntity, INetworkNode`

Records broadcasts and open-connector notifications so tests can assert propagation.

```csharp
public sealed class CapturingNode : BlockEntity, INetworkNode
{
    public object? LastState { get; }
    public int UpdateCount { get; }
    public BlockFacing[]? LastOpenFaces { get; }
    public string? Orientation { get; set; }
    public string[] PossibleOrientations { get; set; }
    public string NetworkType { get; set; }

    public bool HasConnectorAt(BlockFacing face);                 // true if Orientation contains the face code
    public void OnOpenConnectorsChanged(BlockFacing[] openFaces);
    public void OnNetworkUpdate(object? state);                  // captures state, increments UpdateCount
}
```

### `SeverableNode : BlockEntityNetworkNode`

A real node whose connectivity toggles at runtime, for testing dynamic fracture.

```csharp
public sealed class SeverableNode : BlockEntityNetworkNode
{
    public bool Broken { get; set; }
    public override string NetworkType { get; set; }
    public override bool IsConnectionBroken();   // returns Broken - graph severs here when true
}
```

## Project configuration recap

`ExpandedLib.Testing.csproj`: `net10.0` by default (multi-targets `net8.0`/`net7.0` with
`-p:Legacy=true`), C# 14, nullable enabled. References `NSubstitute` 5.3.0 and the game DLLs
(`VintagestoryAPI`, `VSSurvivalMod`, `VSEssentials`, `0Harmony`, `Newtonsoft.Json`, all
`Private=false`), and project-references `ExpandedLib`. A **consuming** test project additionally
needs `Microsoft.NET.Test.Sdk` 17.12.0, `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2, plus a
reference to the mod under test.

## Related pages

- [Testing Harness](Testing-Harness) - setup, module initializer, worked examples.
- [Block Networks](Block-Networks) - the real types these doubles stand in for.
