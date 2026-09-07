# Testing API Reference

Full public surface of `ExpandedLib.Testing`. For setup and worked examples see the
**[Testing Harness](Testing-Harness)** page. Signatures are taken from
`mods/exlib/testing/`.

## Where things are

| Folder | A test author finds... |
|---|---|
| `World/` | `TestWorld`, `TestBlocks`, `TestLang`, `VsAssemblyResolver` |
| `Scenes/` | `Scene`, `SceneDiagram`, `SceneGrid` |
| `Rigs/` | `StructureRig`, `StructureTestHooks`, `MachineRig`, `MachineTestHooks`, `RegistryLawScanner`, `ResourceInvariant<TState>`, `StaticStateCollection`, `HarmonyFixture` |
| `Doubles/` | stand-ins: `StubNetwork`, `TestNetworkBlock`, `NetworkNodeTestHooks`, `CapturingNode`, `SeverableNode`, `OrientableNode`, `RccFake`, `TestMemberBlockEntity`, `MechPower`; supported doubles: `TestPlayer`, `TestInventory`, `TestModLoader`, `WorldConfigBag`, `ModConfigFiles`, `RecordingLogger`, `TestChannels` |
| `Checks/` | the content validators: `CodeLiterals`, `CodePrefixCollision`, `CostSelectorOverlap`, `DefinitionAssets`, `DefinitionCatalogue`, `DefinitionCodes`, `DefinitionGoldens`, `DefinitionJson`, `DefinitionParity`, `HandbookSync`, `LangCallSites`, `LangCoverage`, `LangKeys`, `LangParity`, `LayoutTable`, `LoopingAnimations`, `MegablockFrames`, `MultiblockCodes`, `NetworkNodeContract`, `PinnedNetworkNodes`, `PressureVesselGate`, `RecipeCodes`, `ReferencedCodes`, `ShapeExtents`, `ShippedJson`, `TreeKeys`, `VanillaToolTiers`, `WikiParity` |
| `Repo/` | `RepoPaths`, `RepoManifest`, `ReleasedHistory`, `ReleasedCodes`, `ReleasedVersions`, `ReleasedCodeDebt`, `BlockCodeEmitter`, `RepoCheckSource` |
| (root) | `ReflectionHelpers` |

## `TestWorld`

Headless, in-process stand-in for a server world: in-memory block/BE store, a live
`BlockNetworkModSystem`, and NSubstitute-faked accessors/API.

```csharp
public sealed class TestWorld : IDisposable
{
    public TestWorld();

    // State:
    public Block Air { get; }
    public BlockNetworkModSystem Networks { get; }
    public IBlockAccessor Accessor { get; }
    public IServerWorldAccessor World { get; }
    public IGameCalendar Calendar { get; }
    public ICoreServerAPI Api { get; }
    public ICoreClientAPI ClientApi { get; }     // for a ModSystem's StartClientSide; shares Log
    public List<ItemStack> Drops { get; }
    public RecordingLogger Log { get; }          // wired as Api.Logger and World.Logger
    public WorldConfigBag Config { get; }        // wired as World.Config
    public TestModLoader Mods { get; }           // wired as Api.ModLoader; "exlib" enabled by default
    public ModConfigFiles ConfigFiles { get; }   // backs Api.LoadModConfig/StoreModConfig; deleted on Dispose

    // Setup:
    public TestWorld Attach(BlockEntity be);
    public TestWorld Initialize(BlockEntity be);
    public TestPlayer Player(string uid = "test", string name = "Tester");
    public TestWorld RegisterNetwork(string networkType, Func<BlockNetworkModSystem, BlockNetwork> factory);
    public TestWorld Place(BlockPos pos, Block block, BlockEntity? be = null);
    public TestWorld PlaceNode(BlockPos pos, string networkType, string orientation, int id = 1);
    public TestWorld PlaceMemberBlock(BlockPos pos, string networkType, string connectors, int id = 899);
    public TestWorld PlaceFiller(BlockPos pos, FillerBehavior[]? hosted = null, BlockPos? principal = null);
    public TestWorld PlaceFillerNode(BlockPos pos, string networkType, string orientation, BlockPos? principal = null);
    public TestWorld RegisterBlockEntityFactory(string classname, Func<BlockEntity> factory);
    public TestWorld RegisterBlockEntityBehaviorFactory(string classname, Func<BlockEntity, BlockEntityBehavior> factory);
    public TestWorld Register(Block block);
    public Item RegisterItem(string code, float meltingPoint = 0f);
    public Item? GetItem(AssetLocation? code);
    public Item? GetItem(int id);
    public TestChannels Channels(string channelName);   // memoised; also what Api/ClientApi.Network.RegisterChannel hands out

    // Store access (what was placed, loaded or not; the accessor reads through the chunk gate):
    public Block GetBlock(BlockPos pos);
    public BlockEntity? GetBlockEntity(BlockPos pos);

    // Chunk loading:
    public TestWorld UnloadChunkAt(BlockPos pos);            // hide that chunk from Accessor; store untouched
    public TestWorld LoadChunkAt(BlockPos pos);              // bring it back; loading a loaded chunk is a no-op
    public bool IsChunkLoaded(BlockPos pos);

    // Graph:
    public void AddNode(BlockPos pos, string networkType);
    public void RemoveNode(BlockPos pos);
    public BlockNetwork? NetworkAt(BlockPos pos);

    // Time:
    public void Tick(int seconds = 1);                       // drives BlockNetworkModSystem.ServerTick with dt = 1
    public void FireBlockEntityTicks(float dt = 1f, int times = 1);   // fire RegisterGameTickListener callbacks
    public void AdvanceDays(double days);

    // Lifecycle:
    public void Dispose();   // deletes ConfigFiles's temp directory; everything else is in-memory
}
```

`PlaceNode` and `PlaceMemberBlock` place a cell *and* register it, by running the real
`BlockEntity.Initialize` whose membership behaviour registers itself — so neither needs, or tolerates,
a following `AddNode`. Both require a prior `RegisterNetwork` for the type. `PlaceNode` places a
`TestNetworkBlock`; `PlaceMemberBlock` places a plain `Block` whose membership states its own
connector faces, for the cell that is a node only because it carries one.

`PlaceFiller` places a mega-block footprint cell - the shared `BlockStructureFiller` over a
`BlockEntityStructureFiller` - hosting whatever behaviours it is given, built through the fake class
registry (`RegisterBlockEntityBehaviorFactory`) exactly as the game builds them from
`[BlockEntityBehaviorRegister]`. `PlaceFillerNode` is that cell declaring one network membership:
`orientation` is one side letter, or two naming an opposite pair for a cell a run passes through.

`UnloadChunkAt` models a chunk unload as the walk sees one: every cell in the chunk holding that
position reads back as air, its block entities as `null` and `GetChunkAtBlockPos` as `null`, while the
store keeps everything so `LoadChunkAt` restores the chunk exactly. Chunks are real
`GlobalConstants.ChunkSize` (32) cubes, so a fixture that wants a boundary between two adjacent cells
must straddle one. ⛔ Not to be confused with `Unload(pos)`, which is the *other* half - one block
entity running its own `OnBlockUnloaded` and being dropped, with the cell left readable.

`Tick` advances the network simulation through the manager's real `ServerTick`, so it resumes any
connectivity review an unloaded chunk suspended before dispatching `OnTick`; `FireBlockEntityTicks`
fires the listeners block entities registered via `RegisterGameTickListener` (captured from the fake
event API). It supports both the 1.22 `RegisterGameTickListener` overload (with `BlockPos`) and the
legacy 1.20/1.21 one via `#if GAME_GE_1_22`.

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
var pipe = TestBlocks.Configure(new BlockPipe(), "iiex:pipe-cast-straight-ns", id: 1,
    ("material", "iron"), ("type", "straight"), ("orientation", "ns"));
```

## Test doubles (`Doubles/`)

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

### `NetworkNodeTestHooks`

```csharp
public static class NetworkNodeTestHooks
{
    public static void SetNetworkTypeForTest(this BlockNetworkNode node, string type);
    public static void ApplyOrientationForTest(this BlockNetworkNode node, string token);
}
```

Sets a real production node's shape-family and connector state directly, bypassing the asset-load
pipeline, for a fixture over a concrete node block (`TestNetworkBlock` already takes both through
its constructor and needs neither hook).

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

## Supported doubles (`Doubles/`)

Already wired into every `TestWorld` - see [Testing Harness § Doubles](Testing-Harness#doubles) for
worked examples.

```csharp
public sealed class TestPlayer
{
    public static TestPlayer Create(TestWorld world, string uid = "test", string name = "Tester");
    public IPlayer Player { get; }
    public IServerPlayer? ServerPlayer { get; }   // null only where this lane's game dll can't proxy it
    public ItemSlot ActiveSlot { get; }           // a real DummySlot
    public EntityPlayer Entity { get; }
    public bool Sneaking { get; set; }            // Entity.Controls.Sneak
    public void Hold(ItemStack? stack);
}

public static class TestInventory
{
    public static InventoryGeneric Of(TestWorld world, int slots, string id = "test");
}

public sealed class TestModLoader : IModLoader
{
    public TestModLoader Add(string modId, string version, bool enabled = true);
    public TestModLoader Register(ModSystem system);
    public bool IsModLoaded(string modId);   // alias of IsModEnabled some mods probe by reflection
    public bool HasMod(string modId);        // as above
    public bool HasModId(string modId);      // as above
}

public sealed class WorldConfigBag
{
    public ITreeAttribute Tree { get; }
}

public sealed class ModConfigFiles : IDisposable
{
    public string Directory { get; }
    public IReadOnlyList<string> Files { get; }
    public void Write(string file, object value);
    public T? Read<T>(string file);
}

public sealed class RecordingLogger : LoggerBase
{
    public IReadOnlyList<(EnumLogType Type, string Message)> Entries { get; }
    public IEnumerable<string> Errors { get; }
    public IEnumerable<string> Warnings { get; }
    public void Clear();
}

public sealed class TestChannels
{
    public static TestChannels Create(TestWorld world, string channelName);
    public string ChannelName { get; }
    public IServerPlayer Sender { get; }              // a fresh TestPlayer's server player
    public IServerNetworkChannel Server { get; }
    public IClientNetworkChannel Client { get; }
    public IReadOnlyList<object> SentToServer { get; }    // deserialised, oldest first
    public IReadOnlyList<object> SentToClients { get; }   // deserialised, oldest first
}
```

`TestModLoader.GetMod`/`IsModEnabled`/`GetModSystem`/`GetModSystem<T>`/`IsModSystemEnabled` are the
`IModLoader` members proper; `TestWorld`'s constructor registers `"exlib"` enabled and its own
`Networks` as a resolvable system. `ModConfigFiles` backs `Api.LoadModConfig`/`StoreModConfig`
through a custom NSubstitute call handler rather than `Arg.Any<T>()`/`Returns` - those bind a
return-value specification to the one closed generic method they were written against, so they
cannot answer a call made with a different `T`.

`TestChannels` is what `Api.Network.RegisterChannel`/`ClientApi.Network.RegisterChannel` hand out
(memoised per name through `world.Channels`), so a `ModSystem` that registers a channel in
`StartServerSide`/`StartClientSide` needs no test-only wiring at all. Every send round-trips through
`SerializerUtil` into a fresh instance before it is recorded and dispatched, exactly as the real wire
would reconstruct it; sending a type neither `RegisterMessageType` call named throws
`InvalidOperationException` naming it.

## `MachineRig`

```csharp
public abstract class MachineRig(TestWorld world)
{
    public TestWorld World { get; }
    public float RunUntil(Func<bool> until, float ceilingSeconds, float stepSeconds = 1f);
    public void RunLive(float seconds, Action<float>? observer = null, float stepSeconds = 1f);
    public void RunWhile(Action beforeEachStep, float seconds, float stepSeconds = 1f);
}
```

Each step fires block-entity ticks then the network tick (`World.FireBlockEntityTicks` then
`World.Tick`), the order `Scene.Step` uses. `RunUntil` throws `TimeoutException` if `until` never
holds within `ceilingSeconds`.

## `MachineTestHooks`

```csharp
public static class MachineTestHooks
{
    public static void DisablePickRangeCheck(this BlockEntityMachineStation station);
    public static void DriveProductionTick(this BEBehaviorProductionMachine behavior, float dt);
    public static void DriveProductionTick(this BlockEntityProductionMachine machine, float dt);
}
```

`DisablePickRangeCheck` turns off the engine's interaction-range test that gates a machine
station's access check, so a headless test's substitute player - never "in range" of anything -
can still exercise a packet route gated on it; the claim check is unaffected. Both
`DriveProductionTick` overloads run one production tick exactly as the registered listener would,
including the readiness gate and the catch-up `dt` clamp, for a fixture stepping a machine by hand
instead of through `MachineRig`.

## `RegistryLawScanner`

```csharp
public static class RegistryLawScanner
{
    public static IReadOnlyList<Type> ConcreteSubclasses<TBase>();
    public static void ForEach<TBase>(Action<Type> law);
}
```

`ConcreteSubclasses` walks the loaded assembly closure transitively (references load lazily) and
returns every non-abstract type assignable to `TBase`. `ForEach` runs `law` against each and throws
one `InvalidOperationException` naming every leaf that failed, rather than stopping at the first.

## `ResourceInvariant<TState>`

```csharp
public sealed class ResourceInvariant<TState>(
    Func<TState> fresh,
    IReadOnlyList<Action<TState>> moves,
    Action<TState> assert)
{
    public void Run(int sequences = 5, int movesPerSequence = 50, int seed = 1);
}
```

Applies random sequences of `moves` to a fresh `TState`, calling `assert` after every move.
`seed` makes a failure reproducible; the exception message names the sequence, the failing move
index, and every move index applied in that sequence so far.

## `StaticStateCollection`

```csharp
public static class StaticStateCollection
{
    public static void EveryCollectionNameHasADefinition(Assembly assembly);
}
```

Fails when a `[Collection("Name")]` in `assembly` has no matching `[CollectionDefinition("Name", ...)]`
in the same assembly - a collection with no definition still exists (xUnit synthesises one per unique
name), which is exactly the failure mode this catches.

## `HarmonyFixture`

```csharp
public sealed class HarmonyFixture : IDisposable
{
    public HarmonyFixture(string modId, Assembly patches, string? category = null);
    public Harmony Harmony { get; }
    public bool IsPatched(MethodBase original);
    public IReadOnlyList<MethodBase> PatchedMethods { get; }
    public void Dispose();
}
```

Built on `ExHarmony.PatchOnce`/`PatchCategoryWhenLoaded`/`UnpatchAll`, so the fixture and the library
agree on idempotence: a second fixture for the same `modId` does not double-patch. `category == null`
applies every uncategorised `[HarmonyPatch]` class in `patches`; a `category` applies only its
`[HarmonyPatchCategory(category)]` classes. `IsPatched`/`PatchedMethods` read through
`Harmony.GetPatchInfo`/`GetPatchedMethods`, scoped to this fixture's owner id. Join a collection with
`DisableParallelization = true` - Harmony patches are process-wide.

## `RepoPaths`

```csharp
public static class RepoPaths
{
    public static string Root { get; }
    public static string Mod(string id);           // a sample id resolves to its sample path
    public static string Assets(string domain);    // unknown domain falls back to mods/<domain>
    public static string Docs(string modId);
    public static string Src(string id);            // <mod path>/src, else the mod path itself
    public static IReadOnlyList<string> AllAssetTrees();
    public static void Register(string domain, string modFolder);
}
```

`Mod`, `Assets` and `AllAssetTrees` all resolve through `RepoManifest` (below), so a mod or sample the
manifest names is covered without editing this file; `Register` still covers a domain the manifest
does not know about.

## `RepoManifest`

```csharp
public static class RepoManifest
{
    public readonly record struct SampleEntry(string Path, string Tests);

    public static IReadOnlyDictionary<string, string> Mods { get; }
    public static IReadOnlyDictionary<string, SampleEntry> Samples { get; }
    public static IReadOnlyList<string> Tests { get; }
    public static IReadOnlyDictionary<string, string> Overlays { get; }
}
```

Reads the repo manifest, `exmod.json`, at the repo root: every mod's id and path, every sample's id,
path and test project, the test projects belonging to neither, and any overlay domain a mod's own
entry declares (`"overlays": { "game": "iiex" }`). Absent the file, the default layout applies: every
directory under `mods/` is a mod named after itself, no samples, no extra tests, and `game` overlaid
onto `iiex` only when `mods/iiex/assets/game` exists.

## `ReleasedHistory`

```csharp
public static class ReleasedHistory
{
    public static void Register(
        string mod,
        IReadOnlyList<ReleasedCodes.Shipped> shipped,
        IReadOnlyList<ReleasedCodes.ShippedEntityClass> entityClasses,
        IReadOnlyDictionary<string, string> versions,
        IReadOnlyList<string> debt);
    public static ReleasedModHistory? For(string mod);
    public static IEnumerable<ReleasedCodes.Shipped> AllShipped { get; }
    public static IEnumerable<ReleasedCodes.ShippedEntityClass> AllEntityClasses { get; }
    public static IReadOnlyDictionary<string, string> AllVersions { get; }
    public static IEnumerable<string> AllDebt { get; }
}
```

One mod, one `Register` call, from that mod's own test `ModuleInit`. `ReleasedCodes`,
`ReleasedVersions` and `ReleasedCodeDebt` are unchanged forwarders onto this registry.

## `SceneGrid`

```csharp
public sealed class SceneGrid
{
    public SceneGrid On(char glyph, Action<BlockPos> place);
    public void Layer(string ascii, int y = 0, int originX = 0, int originZ = 0);
    public void Stack(int baseY, int originX, int originZ, params string[] layers);
}
```

The `ExpandedLib.Structures.CellGrid` core `SceneDiagram` is a thin forwarder over: a legend maps
glyphs to placement actions, drawn as a horizontal plane per layer, `Stack` indexing layers
bottom-to-top in +Y.

## `StructureRig`

```csharp
public sealed class StructureRig
{
    public static StructureRig Around(TestWorld world, BlockEntityMultiblockStructure anchor, ExBlockDef def, int angle);
    public BlockPos Cell(int x, int y, int z);
    public void Occupy(BlockPos pos, Block block, BlockEntity? be = null);
    public void Complete();
    public void Raise();                    // fills only empty cells, then Complete()
    public bool AwaitCompletion(float ceilingSeconds = 5f);
    public IReadOnlyList<string> Missing { get; }
}
```

Stands up a mega-block's multiblock footprint headlessly so its own monitor tick observes
`InCompleteBlockCount == 0` and sets `StructureComplete` itself, rather than a test forcing the
flag - see [Testing Harness § Standing up a mega-block](Testing-Harness#standing-up-a-mega-block-with-structurerig).

## `StructureTestHooks`

```csharp
public static class StructureTestHooks
{
    public static void ApplyStructureRotation(this BlockEntityMultiblockStructure structure, string? orientationOrSide = null);
}
```

Recomputes a structure's rotation directly, the same path a load or monitor tick uses, for a
fixture that builds a structure by hand instead of through `StructureRig`. When
`orientationOrSide` is given, it is written to whichever orientation-bearing variant key ("side"
or "orientation") the block's variant map already carries before the recompute.

## Doubles internals (`Doubles/`)

Supporting types behind the doubles above, not usually constructed directly by a test:

| Type | What it's for |
|---|---|
| `MechPower` | Helpers for headless mechanical-power tests over `MechanicalNetwork` (settable `Speed`/`NetworkResistance`), a turning or stalled network without a real engine. |
| `OrientableNode` | The smallest concrete `BlockEntityNetworkNode`: one settable network type, nothing else layered on. |
| `RccFake` | Makes a machine entity that gates on `ExRightClickConstructable` (boiler, engine) read as fully constructed, since `IsComplete` is not virtual and reads a real field. |
| `TestMemberBlockEntity` | A bare block entity carrying a chosen set of network memberships, for testing the accessor and the graph walk without a concrete machine. |

`Doubles/ModConfigCallHandler` (internal, not part of the public surface) is what routes
`ICoreAPI.LoadModConfig`/`StoreModConfig` calls to `ModConfigFiles` - open generics can't be matched
with NSubstitute's `Arg.Any<T>()`, so it goes through a plain call handler instead.

## Checks (`Checks/`)

Content validators, most wrapping [`ExpandedLib.Checks`](Checks) - the same rule the game runs at
load and `/exmod verify` runs on demand. A mod's own suite calls these against its own assembly and
asset tree; `RepoCheckSource` (below) hands one a corpus over this repository's own source tree, and
the internal (not public) `Checks/AssemblyCheckSource` does the same directly from `(domain, Assembly)`
pairs.

| Type | What it checks |
|---|---|
| `CodeLiterals` | A domain-qualified block-code string literal in source that can never resolve because it names a variant-grouped block by its bare base code. |
| `CodePrefixCollision` | No block's base code is a proper prefix of another's at a `-` boundary - a wildcard built from a base code must not also match an unrelated block. |
| `CostSelectorOverlap` | No two recipe-cost selectors in a catalogue can match the same block (`ExRecipeCosts` applies every entry in sequence, not first-match). |
| `DefinitionAssets` | The asset paths a mod's code-first definitions name (shapes, textures) exist on disk - different from the goldens, which only pin what a def emits. |
| `DefinitionCatalogue` | Whether a code names something a mod registers, over blocks and items together (most callers hold a `JsonItemStack` whose `type` decides the registry). |
| `DefinitionCodes` | Expands code-first block definitions into the concrete block codes they register, so a test can stand up a world holding the blocks the mods ship. |
| `DefinitionGoldens` | Golden-file oracle for code-first definition parity - collects a mod's defs (`Collect`), compares each against `goldens/{domain}/{Location.Path}` (`CheckGolden`), and checks the golden set exactly covers the defs (`CheckCompleteness`). |
| `DefinitionJson` | Shared lenient JSON reader for def-emitted JSON (comments + trailing commas), plus field accessors the mega-block guards pin against. |
| `DefinitionParity` | Semantic comparison of an authored `ExBlockDef`'s emitted JSON against the hand-written blocktype JSON it replaces. |
| `HandbookSync` | The handbook authoring pipeline: `mods/{domain}/docs/handbook/NN-*.html` as the hand-edited source for a shipped handbook page's body. |
| `LangCallSites` | Every lang key a mod's own source hands to `Lang.Get`/`ActionLangCode`/`SendIngameError` exists in every locale it ships. |
| `LangCoverage` | Every block code a mod registers resolves to a name in every locale it ships (an unresolved key silently renders as the raw key). |
| `LangKeys` | Every literal `Lang.Get("domain:key")` under a mod's source roots resolves in one lang tree's `en.json`. |
| `LangParity` | Every locale in one lang tree carries exactly the English key set, with matching `{0}`-style placeholders. |
| `LayoutTable` | Reads a code-first multiblock layout's emitted table back into a per-cell block code, so a test can pin against the drawing rather than the JSON. |
| `LoopingAnimations` | No looping clip under one asset tree's `shapes/` unwinds a whole turn across its wrap, or poses an element at only one end. |
| `MegablockFrames` | Relates a mega-block's drawn mesh to the volume it reserves - the art and the filler footprint must agree. |
| `MultiblockCodes` | Every block code a `multiblockStructure` layout asks for is a block some mod defines (a dangling cell code throws nothing and passes the goldens). |
| `NetworkNodeContract` | The two structural rules a definition must obey to end up on a network graph: a `BlockNetworkNode` def declares a `type` variant state, and its orientation states resolve. |
| `PinnedNetworkNodes` | No shipped layout pins the orientation of a network node - a node picks its own orientation from its neighbours. |
| `PressureVesselGate` | The one place a fastener choice is a hard gate rather than a substitution: a pressure vessel must be riveted. |
| `RecipeCodes` | Every grid recipe's block output names a block the mod registers (an output is exact, never a wildcard selector). |
| `ReferencedCodes` | Every code a mod's recipes, construction stages and definition bodies point at (as opposed to register) that names nothing. |
| `ShapeExtents` | The bounding box (in voxels) of everything a shape file draws. |
| `ShippedJson` | Every JSON asset under one shipped tree parses, carries no control character, and (under `patches/`) declares the side each entry runs on. |
| `TreeKeys` | Golden-file oracle for a block entity's save shape - the keys `ToTreeAttributes` writes, pinned against a committed golden the same way `DefinitionGoldens` pins a def's JSON; see [Testing Harness § Pinning a block entity's save shape](Testing-Harness#pinning-a-block-entitys-save-shape). |
| `VanillaToolTiers` | Vanilla pickaxe tool tier constants (`Bronze`/`Iron`/`Steel`), for pinning a block's `requiredMiningTier`. |
| `WikiParity` | Reflects the API the wiki teaches against the API the assembly actually has. |
| `RepoCheckSource` (`Repo/`) | An `ICheckSource` over this repository's own source tree, for the same checks run against real committed assets rather than a stub. |

## `BlockCodeEmitter`

```csharp
public static class BlockCodeEmitter
{
    public static string Emit(string modId, Assembly asm);   // the {Mod}Blocks.g.cs source text
    public static bool CheckOrWrite(string modId, Assembly asm, string filePath);
}
```

Emits a mod's `{Mod}Blocks` table from its code-first definitions, so a layout author can name a
block code with a chosen variant instead of hand-writing the string. There is no console entry
point - `CheckOrWrite` is what the mod's own `*BlocksCodeTests` fixture calls: read-only by
default, it fails and names `exmod codes <mod>` when the table has drifted; with
`EXLIB_WRITE_BLOCKCODES=1` set, which is what that command does, it writes the table instead.

## `ReleasedCodes` / `ReleasedVersions` / `ReleasedCodeDebt`

```csharp
public static class ReleasedCodes { /* forwards onto ReleasedHistory.AllShipped/.AllEntityClasses */ }
public static class ReleasedVersions { public static IReadOnlyDictionary<string, string> HighestPublished { get; } }
public static class ReleasedCodeDebt { public static IEnumerable<string> KnownUnmigrated { get; } }

public sealed record ReleasedModHistory(
    IReadOnlyList<ReleasedCodes.Shipped> Shipped,
    IReadOnlyList<ReleasedCodes.ShippedEntityClass> EntityClasses,
    IReadOnlyDictionary<string, string> Versions,
    IReadOnlyList<string> Debt);
```

Unchanged call sites onto `ReleasedHistory`, which now holds the data: `ReleasedCodes` is every
block code that has ever shipped (the migration contract - a released code must resolve forever, or
carry a documented `IBlockCodeMigration`); `ReleasedVersions.HighestPublished` is the highest version
published per modid; `ReleasedCodeDebt.KnownUnmigrated` is the recorded, dated exception list so the
coverage guard still fails on anything new.

## Project configuration recap

`ExpandedLib.Testing.csproj`: `net10.0` by default (multi-targets `net8.0`/`net7.0` with
`-p:Legacy=true`), C# 14, nullable enabled. References `NSubstitute` 5.3.0, `xunit.extensibility.core`
2.9.2 (`StaticStateCollection` reads `[Collection]`/`[CollectionDefinition]`, so this is not a new
runtime dependency for any consumer - every one is already an xUnit project) and the game DLLs
(`VintagestoryAPI`, `VSSurvivalMod`, `VSEssentials`, `0Harmony`, `Newtonsoft.Json`, all
`Private=false`), and project-references `ExpandedLib`. A **consuming** test project additionally
needs `Microsoft.NET.Test.Sdk` 17.12.0, `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2, plus a
reference to the mod under test.

## Related pages

- [Testing Harness](Testing-Harness) - setup, module initializer, worked examples.
- [Block Networks](Block-Networks) - the real types these doubles stand in for.
