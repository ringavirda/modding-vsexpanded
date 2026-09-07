# Testing Harness

`ExpandedLib.Testing` (`exlib.testing`) is a headless xUnit harness that loads the **real** Vintage
Story assemblies and lets you unit- and integration-test network and block-entity logic with plain
`dotnet test` - no game launch, no rendering, no world save. It fakes the server world with
NSubstitute, runs the real `BlockNetworkModSystem`, and ticks block entities and networks in
process.

This page gets a test project running; the **[Testing API Reference](Testing-API-Reference)** lists
every public type and signature.

## What it gives you

- `TestWorld` - an in-memory block/BE store with a live network manager and faked
  `IServerWorldAccessor` / `IBlockAccessor` / `ICoreServerAPI`.
- `Scene` + `SceneDiagram` - a fluent builder and an ASCII-layout parser, so multi-network setups
  read like diagrams.
- `VsAssemblyResolver` - resolves the game DLLs at runtime from your install or the in-repo
  `.game/<slug>` folder.
- `TestLang` - a minimal `Lang` so production code can call `Lang.Get()`.
- Test doubles (`StubNetwork`, `TestNetworkBlock`, `CapturingNode`, `SeverableNode`) for exercising
  the graph without real gameplay state.
- Supported doubles (`TestPlayer`, `TestInventory`, `TestModLoader`, `WorldConfigBag`,
  `ModConfigFiles`, `RecordingLogger`) already wired into every `TestWorld` - see [Doubles](#doubles).
- `ReflectionHelpers` / `TestBlocks` - prime private fields and configure bare blocks without the
  asset pipeline.

## 1. Ten minutes to a green test

Three ways to get from nothing to a green test, cheapest first.

### The template

```
dotnet new install ./templates/exlib-tests    # or, once published: dotnet new install ExpandedLib.Templates.Tests
dotnet new exlib-tests -n Demo.Tests -o Demo.Tests --ModName Demo
dotnet test Demo.Tests
```

`--ModName` names your mod project (a sibling folder, `../Demo/Demo.csproj`); `--GamePath` overrides
the Vintage Story install baked in (defaults to the `VINTAGE_STORY` environment variable). The
generated project's two content checks (a golden fact, a shipped-JSON fact) pass vacuously until
your mod has definitions or an `assets/` tree - see `templates/exlib-tests/README.md`.

### By hand

Reference the harness, xUnit, the test SDK and NSubstitute, plus the game API DLLs (with
`<Private>false</Private>` so you don't copy them). Inside this monorepo, mirror
`mods/exlib/tests/ExpandedLib.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>   <!-- 1.22; -p:Legacy=true adds net8.0/net7.0 -->
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="VintagestoryAPI"><HintPath>$(GamePath)/VintagestoryAPI.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="VSSurvivalMod"><HintPath>$(GamePath)/Mods/VSSurvivalMod.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="VSEssentials"><HintPath>$(GamePath)/Mods/VSEssentials.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\exlib\src\ExpandedLib.csproj" />
    <ProjectReference Include="..\src\YourMod.csproj" />
    <ProjectReference Include="..\..\exlib\testing\ExpandedLib.Testing.csproj" />
  </ItemGroup>
</Project>
```

#### Provisioning the game install: why `IPlayer` can be mocked at all

`exmod provision game` (both `-Kind server` and `-Kind client`) runs `Publicize-GameApi`
on the provisioned `VintagestoryAPI.dll` after every fetch, every re-check of an existing install and
every version bump - see the function in `scripts/exmod/provision.ps1`. It flips the accessibility bits on
`IPlayer.IsInInteractionRangeOf(BlockPos, float)`, which the game ships as `internal abstract`: an
interface member no external assembly is allowed to implement, so `Substitute.For<IPlayer>()` (and
`IServerPlayer`, which inherits the same member) cannot construct a proxy at all without this patch.
The edit is applied byte-for-byte **in place** rather than by regenerating the assembly - a
regenerated DLL loses its `CodeView` debug-directory entry, which crashes the game's own logger on
startup - so it is safe to leave in `.game/<slug>`, the copy this repo builds against and launches
from; the mods you ship never see it, since the install a player runs is never patched. Idempotent
and a no-op once upstream makes the member public.

### Consuming outside this repo

The harness is a developer library, not a game mod - it never ships inside a `Mods/mod` folder.
Three ways to use it from a separate mod repo, in the order the template tries them:

**A - NuGet.** `ExpandedLib` and `ExpandedLib.Testing` are `dotnet pack`-able (see the two `.csproj`
files' pack metadata) and are what `templates/exlib-tests`' generated project references by default.
They are not pushed to NuGet.org from this repo yet (`.github/workflows/release.yml`'s push step is
present and commented) - until then, `dotnet pack` them yourself into a local feed, or use option B
or C.

**B - Reference from source (recommended if you might tweak it).** Add `ExpandedLib.Testing` (and
`ExpandedLib`) as a git submodule or sibling checkout and `ProjectReference` the `.csproj`s, exactly
as above. You track upstream changes and can debug into the harness.

**C - The release bundle.** Each [GitHub release](https://github.com/ringavirda/modding-vsexpanded/releases)
ships `exlib-testing_<version>.zip` (versioned in lockstep with exlib) containing
`ExpandedLib.Testing.dll` + `exlib.dll` (built for the current game version, 1.22 / net10.0). Drop
both into your repo and reference them with copy-local off, supplying the rest yourself:

```xml
<ItemGroup>
  <!-- the bundle (exlib's assembly name is "exlib") -->
  <Reference Include="exlib"><HintPath>libs/exlib.dll</HintPath><Private>false</Private></Reference>
  <Reference Include="ExpandedLib.Testing"><HintPath>libs/ExpandedLib.Testing.dll</HintPath><Private>false</Private></Reference>
  <!-- game assemblies from your own install -->
  <Reference Include="VintagestoryAPI"><HintPath>$(GamePath)/VintagestoryAPI.dll</HintPath><Private>false</Private></Reference>
  <Reference Include="VSSurvivalMod"><HintPath>$(GamePath)/Mods/VSSurvivalMod.dll</HintPath><Private>false</Private></Reference>
  <Reference Include="VSEssentials"><HintPath>$(GamePath)/Mods/VSEssentials.dll</HintPath><Private>false</Private></Reference>
</ItemGroup>

<ItemGroup>
  <!-- the harness's own dependency, plus the test stack, from NuGet -->
  <PackageReference Include="NSubstitute" Version="5.3.0" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
  <PackageReference Include="xunit" Version="2.9.2" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
</ItemGroup>
```

The bundle deliberately omits the game assemblies (proprietary - you provide them) and `NSubstitute`
(pull it from NuGet so its own transitive deps resolve). The module initializer below is required
either way.

> Only the current game version is bundled. To target 1.20 / 1.21 test runs, use option **B** -
> the harness multi-targets `net8.0`/`net7.0` from source under `-p:Legacy=true`.

### The required module initializer

The game assemblies must be resolvable **before any test type is instantiated**, and `Lang.Get`
must work. Do both from a `[ModuleInitializer]` - it fires before the runner discovers test types:

```csharp
using System.Runtime.CompilerServices;
using ExpandedLib.Testing;

internal static class ModuleInit
{
    [ModuleInitializer]
    internal static void Init()
    {
        VsAssemblyResolver.Register();   // resolve VintagestoryAPI/VSSurvivalMod/... from the install or .game/<slug>
        TestLang.Init();                 // echo-the-key Lang so Lang.Get(...) is safe
    }
}
```

Every test project in this repo (`ExpandedLib.Tests`, `IronIndustryExpanded.Tests`,
`SteelIndustryExpanded.Tests`, `HelloExpanded.Tests`) has exactly this - a module initializer only
runs for the assembly that declares it. `VsAssemblyResolver.Register`
is idempotent and resolves the install via the `[AssemblyMetadata("GameInstallEnv")]` environment
variable (e.g. `VINTAGE_STORY`) or, failing that, by walking up to `.game/<slug>`.

### A smoke test

The smallest thing `TestWorld` can prove - place a block, read it back - with no network involved:

```csharp
[Fact]
public void A_placed_block_reads_back_at_its_position()
{
    var world = new TestWorld();
    var pos = new BlockPos(0, 0, 0);

    world.Place(pos, TestBlocks.Configure(new Block(), "test:stone", 1));

    Assert.Equal("test:stone", world.GetBlock(pos).Code.ToString());
}
```

### A first test

```csharp
using ExpandedLib.Testing;
using Vintagestory.API.MathTools;
using Xunit;

public class NetworkGraphTests
{
    [Fact]
    public void Three_adjacent_nodes_merge_into_one_network()
    {
        var world = new TestWorld();
        world.RegisterNetwork("test", sys => new StubNetwork(sys));

        var block = TestNetworkBlock.Create("test", "ns", id: 1);
        var positions = new[] { new BlockPos(0, 0, 0), new BlockPos(0, 0, 1), new BlockPos(0, 0, 2) };
        foreach (var pos in positions) world.Place(pos, block);
        foreach (var pos in positions) world.AddNode(pos, "test");

        var net = world.NetworkAt(positions[0]);
        Assert.NotNull(net);
        Assert.Equal(3, net!.Nodes.Count);
        Assert.Same(net, world.NetworkAt(positions[2]));
    }
}
```

## 2. Stand something up

### Doubles

Six supported doubles wire the game's own contracts into `TestWorld` so a modder's first inventory,
config or logging test needs no NSubstitute knowledge. Every one below is a real object, not a bare
`Substitute.For<T>()`: read state off it directly rather than reaching for `Received()`.

**`TestPlayer`** - a player with a real hotbar:

```csharp
TestPlayer player = world.Player();
player.Hold(new ItemStack(pickaxe));
Assert.True(player.Sneaking is false); // player.Sneaking = true toggles Entity.Controls.Sneak
```

**`TestInventory`** - a real multi-slot inventory, for a test that needs more than one slot:

```csharp
InventoryGeneric inv = TestInventory.Of(world, slots: 4);
inv[0].Itemstack = new ItemStack(ironBar);
```

**`TestModLoader`** (`world.Mods`) - `IsModEnabled` and the aliases some mods probe by reflection:

```csharp
world.Mods.Add("iiex", "1.0.0");
Assert.True(world.Api.ModLoader.IsModEnabled("iiex"));
```

**`WorldConfigBag`** (`world.Config`) - the real `ITreeAttribute` behind `World.Config`:

```csharp
world.Api.World.Config.SetString("difficulty", "hard");
Assert.Equal("hard", world.Config.Tree.GetString("difficulty"));
```

**`ModConfigFiles`** (`world.ConfigFiles`) - backs `Api.LoadModConfig`/`StoreModConfig` with real
files under a temp directory deleted when the `TestWorld` is disposed:

```csharp
world.Api.StoreModConfig(new MyConfig { Volume = 800 }, "mymod.json");
MyConfig? loaded = world.Api.LoadModConfig<MyConfig>("mymod.json"); // round-trips through real JSON
```

**`RecordingLogger`** (`world.Log`) - every entry `Api.Logger`/`World.Logger` received, formatted:

```csharp
Assert.Contains(world.Log.Errors, m => m.Contains("names no network type"));
```

### Integration tests with `Scene` and `SceneDiagram`

For your own real network types, build the world as a diagram, step it, then read state back:

```csharp
var scene = new Scene();
scene.Network("pipe", sys => new PipeNetwork(sys));

new SceneDiagram()
    .On('#', p => scene.Block(p, Rock))
    .On('=', p => scene.Node(p, Pipe, new BlockEntityPipe(), "pipe"))
    .Layer("#===#");        // rock caps + three pipe cells along +X

scene.Build();              // add all queued nodes to the graph
scene.Step(10);             // fire BE ticks then network ticks, 10 times

var net = scene.NetworkAt<PipeNetwork>(new BlockPos(1, 0, 0));
Assert.Equal(3, net!.Nodes.Count);
```

`SceneDiagram` maps characters to placement lambdas: columns advance +X, rows advance +Z (a space is
a gap, unlike a code-first layout's grids, where a space is a spacer between cells), and
`Stack(baseY, layers...)` stacks layers bottom-to-top in +Y. `SceneDiagram` is a thin forwarder over
`SceneGrid`, the scene diagram's derivation of `ExpandedLib.Structures.CellGrid` - the same grid core
the multiblock and filler layout DSLs draw over.

To pin a multiblock's own layout against a test rather than re-typing its cells by hand, read the
authored def's emitted table with `LayoutTable`:

```csharp
var layout = LayoutTable.From(BlockBlastFurnaceCoreHot.Definitions("siex").Single());
var rotated = LayoutTable.Rotated(def, angle: 90);   // as vanilla MultiblockStructure would place it
```

### Standing up a mega-block with `StructureRig`

A `BlockEntityMultiblockStructure` only runs its production tick while `StructureComplete` is true, and
that flag is set by the machine's own monitor tick when vanilla's `InCompleteBlockCount` reaches zero.
The tempting shortcut is to force it:

```csharp
ReflectionHelpers.SetProperty(be, "StructureComplete", true);   // don't
```

That asserts the conclusion. The test then passes even when the layout is wrong, the rotation is a
half-turn out, or the anchor never loaded its attributes - the machine "works" in the test and does
nothing in game. `StructureRig` builds the footprint instead, and lets the machine complete itself:

```csharp
var rig = StructureRig.Around(world, furnace, BlockBlastFurnaceCoreHot.Definitions("siex").Single(), angle: 0);

rig.Occupy(rig.Cell(0, 1, -1), tuyereBlock, new BlockEntityTuyere());  // cells the test cares about
rig.Complete();   // fill the rest, Initialize, and wait for the machine's own monitor tick
```

- The layout comes from the anchor's **code-first `ExBlockDef`**, so re-authoring a footprint moves its
  tests with it. `Around` also attaches the def's `attributes` to the placed block, which is what lets
  the production `UpdateStructureRotation` find the layout (a `TestBlocks.Configure` block has none).
- `angle` must be the angle the machine derives from its own variant (north 0, west 90, south 180,
  east 270, plus any per-machine offset - the Bessemer control and cowper stove use `angle + 180`).
  A wrong angle is not tolerated: the cells land where the machine isn't looking and `Complete` throws
  with a per-cell breakdown of what each unsatisfied cell wants and what it holds.
- `Raise()` fills only **empty** cells. A cell you placed yourself is never replaced, even if it does
  not satisfy the layout - otherwise a fixture could put its tuyere one cell out and still complete,
  orphaning the block it goes on to assert against. Air-satisfied cells (`@(air|coalpile)` shafts) are
  left empty on purpose.
- `AwaitCompletion()` / `Missing` let a test assert the *transitions* - that an unbuilt footprint never
  completes, or that breaking one cell takes a running machine back out of production.

Place the real functional blocks (tuyeres, taps, outlets) **before** raising, and give them the code
the layout asks for. A generic pipe behaves identically to a tuyere as a network node but does not
satisfy an `iiex:tuyere*` cell.

### Driving one machine with `MachineRig`

Fixtures that stand up a machine on a `TestWorld` (or a `Scene`, through `scene.World`) tend to write
the same three stepping loops by hand. `MachineRig(world)` is the base class for that: subclass it,
call one of the three, and stop repeating the loop.

```csharp
internal sealed class WaterPumpPlant : EnginePlant {
  // ... construction ...
}

var plant = new WaterPumpPlant(scene, pos);
plant.RunWithSteam(3f, seconds: 10);   // holds the inlet fed while the engine runs
```

- `RunUntil(Func<bool> until, ceilingSeconds, stepSeconds = 1f)` steps until `until()` holds, returning
  the seconds elapsed; throws `TimeoutException` naming the ceiling if it never holds.
- `RunLive(seconds, observer = null, stepSeconds = 1f)` steps for the given duration, calling
  `observer` (with the seconds just elapsed) after every step.
- `RunWhile(beforeEachStep, seconds, stepSeconds = 1f)` runs an action before every step - the "hold a
  source at a level and step" loop (recharge a pipe, crank a pump) so the machine sees a fed line
  rather than one that drains on the first tick.

Each step fires block-entity ticks then the network tick, the same order `Scene.Step` uses. A rig
that needs a different order - feeding a fuel source directly, invoking a production tick by
reflection to fast-forward past a multi-minute heat-up - is doing more than this base covers and
steps by hand instead; `RunUntil`/`RunLive`/`Tick` variants that re-feed a furnace's tuyeres each
step are like that, and stay as fixture-local methods.

### Testing Harmony patches

`HarmonyFixture` applies a mod's Harmony patches once and reverts them on dispose. Harmony patches
are process-wide (keyed by owner id and target method, not by test instance), so every class that
touches one joins a collection with `DisableParallelization = true`:

```csharp
[CollectionDefinition(Name, DisableParallelization = true)]
public class MyPatchesCollection { public const string Name = "MyPatches"; }

[Collection(MyPatchesCollection.Name)]
public class MyPatchesTests {
  [Fact]
  public void Prefix_runs() {
    using var fixture = new HarmonyFixture("mymod.patchtest", typeof(MyPatchesTests).Assembly);

    // exercise the patched method here

    Assert.True(fixture.IsPatched(typeof(SomeType).GetMethod(nameof(SomeType.SomeMethod))!));
  }
}
```

A category (`[HarmonyPatchCategory("...")]`) applies through the third constructor argument instead
of every uncategorised class in the assembly:

```csharp
using var fixture = new HarmonyFixture("mymod.patchtest", typeof(MyPatchesTests).Assembly, "myCategory");
```

For the explicit form - no fixture, reverting by hand - call `ExHarmony.PatchOnce`/`UnpatchAll`
directly, the same entry points the fixture is built on.

A vanilla type whose static constructor touches client or world state runs that constructor the
first time any test references the type, however indirectly - `BlockEntityAnvil`'s builds particle
objects in its type initialiser. Patch a server-safe vanilla method (`CollectibleObject
.GetHeldItemName` is the one this harness tests against) or a type the test assembly itself declares.

### Testing packets

A `ModSystem` that registers a channel through `Api.Network.RegisterChannel`/`ClientApi.Network
.RegisterChannel` in `StartServerSide`/`StartClientSide` needs no test-only wiring: call it against
`world.Api`/`world.ClientApi` and read the pair back off `world.Channels`:

```csharp
var system = new MyPacketModSystem();
system.StartServerSide(world.Api);
system.StartClientSide(world.ClientApi);

var channels = world.Channels("mymod-channel"); // same pair both sides just registered against
world.Api.Event.PlayerJoin += Raise.Event<PlayerDelegate>(channels.Sender);

Assert.Contains(channels.SentToClients, p => ((MyPacket)p).Value == 42);
```

Building the pair directly - `TestChannels.Create(world, name)` - is the same object `world.Channels`
memoises; reach for it when there is no `ModSystem` in the loop, just a packet type to round-trip:

```csharp
var channels = TestChannels.Create(world, "mymod-channel");
channels.Server.RegisterMessageType<MyPacket>();
channels.Client.RegisterMessageType<MyPacket>();

MyPacket? received = null;
channels.Client.SetMessageHandler<MyPacket>(p => received = p);
channels.Server.SendPacket(new MyPacket { Value = 42 }, channels.Sender);

Assert.Equal(42, received!.Value);
```

Every send round-trips through `SerializerUtil` into a fresh instance and delivers synchronously to
the registered handler on the other side - no scheduler, no wire. `SentToServer`/`SentToClients`
record every packet a channel carried, deserialised, oldest first. Sending a type neither side
registered throws `InvalidOperationException` naming it - the same mistake a real mismatched
`RegisterMessageType` order would silently corrupt in the real game.

### Priming private state

Prefer a named seam when one exists: `DriveProductionTick`/`DriveIdleTick` on
`BlockEntityProductionMachine`/`BEBehaviorProductionMachine`, `DriveMonitorTick`/
`ApplyStructureRotation` on `BlockEntityMultiblockStructure`, and `SetNetworkTypeForTest`/
`ApplyOrientationForTest` on `BlockNetworkNode` all run the same code path the real listener or loader
would, and a rename breaks them at compile time rather than at run time.

`ReflectionHelpers` reaches non-public fields/properties/methods (walking the base hierarchy) when
you need to set up or assert internal state that has no seam yet:

```csharp
var boiler = new BlockEntityBoilerCornish();
ReflectionHelpers.SetField(boiler, "_waterVolume", 300f);
var water = (float)ReflectionHelpers.GetField(boiler, "_waterVolume")!;
Assert.Equal(300f, water, 3);
```

## 3. Prove the content

### Pinning a block entity's save shape

`ExpandedLib.Testing.TreeKeys` golden-checks the keys a block entity writes, the same way
`DefinitionGoldens` (see [Code-First Definitions](Code-First-Definitions)) golden-checks a def's JSON:

```csharp
TreeKeys.AssertGolden(new BlockEntityFurnaceTap(), "iiex");
```

`TreeKeys.Of` reads back a fresh instance's `ToTreeAttributes`, sorted and type-tagged
(`"temp:float"`), nested trees flattened to `"parent/child"`. `AssertGolden` compares that against
`mods/<domain>/tests/goldens/<domain>/treekeys/<ClassName>.txt`, reblessed under
`EXLIB_WRITE_GOLDENS` like any other golden. It is the oracle for converting a hand-written
`ToTreeAttributes`/`FromTreeAttributes` pair to `[Persist]`/`Persisted` - see
[Block Entities](Block-Entities) § Converting a hand-written pair.

#### Content validators (`Checks/`)

Most types under `Checks/` are thin wrappers over [`ExpandedLib.Checks`](Checks) - the same rule
the game runs at load and `/exmod verify` runs on demand, callable directly against a mod's own
assembly and asset tree without booting anything. `DefinitionGoldens` and `TreeKeys` (above) are the
two that compare against a committed golden rather than a live rule; `DefinitionParity` is the
semantic JSON comparison both `DefinitionGoldens` and a migration's own before/after test build on;
`LayoutTable` reads a code-first multiblock layout's emitted table back into a per-cell block code.
The full list, one row per type, is the [Testing API Reference](Testing-API-Reference)'s "Checks"
table - this page doesn't repeat it.

### Repo-wide guards, per mod

`ShippedJson`, `LoopingAnimations`, `LangKeys` and `LangParity` are the four checks a mod's own
suite runs over its own tree: every shipped JSON parses and carries no control character, and (under
`patches/`) declares its side; no looping shape animation unwinds a whole turn across its wrap; every
literal `Lang.Get("domain:key")` in the mod's source resolves in its lang tree's English file; every
locale in that lang tree carries the same key set and placeholders as English. Each returns
`IReadOnlyList<string>` findings (empty means clean) and pairs with a premise method
(`PatchFiles`/`ShapeFiles`/`Literals`/`LocaleFiles`) a mod's suite asserts is non-empty wherever its
tree is known to carry that kind of file - otherwise a renamed folder or a dead regex would make the
rule above it pass trivially:

```csharp
[Fact]
public void Iiexs_shipped_json_carries_no_defect() {
  var offenders = ShippedJson.Check(RepoPaths.Assets("iiex"));
  Assert.True(offenders.Count == 0, string.Join("\n", offenders));
}
```

A mod whose tree carries another domain's overlay (iiex's `game` lang overlay) runs the same check
over both trees. `RepoPaths` and `RepoManifest` resolve which tree is whose from `exmod.json` (see
[Testing API Reference](Testing-API-Reference)): a mod, a sample and a plain `mods/<id>` fallback all
resolve the same way, so these four checks read the same whether `exmod.json` exists or not.

`dotnet new exlib-tests` scaffolds two of the four against `YourModProject`'s own tree -
`Invariants/ShippedAssetJsonTests.cs` and `Localization/LangParityTests.cs` - trivially true (no
files found, no failure) until the scaffolded mod ships assets and a translated locale to check.

### Reflection scans: `RegistryLawScanner` and `ResourceInvariant`

A law that must hold for every concrete subclass of some base type - wherever it is declared, not
just the leaves the current test assembly compiles against - is `RegistryLawScanner`:

```csharp
RegistryLawScanner.ForEach<BlockEntityFurnaceCore>(leaf => {
  // assert something about `leaf`, the concrete Type
});
```

`ConcreteSubclasses<TBase>()` walks the loaded assembly closure (references load lazily, so call
this only after the subject type is known to be loaded) and returns every non-abstract type
assignable to `TBase`. `ForEach` runs a law against each and aggregates every failure into one
message, so three broken leaves are reported together rather than one retry at a time.

`ResourceInvariant<TState>` checks an invariant across randomised operation sequences - useful where
a resource (a pipe network, a canal run) can be split, merged, drained or refilled in any order:

```csharp
new ResourceInvariant<Pool>(
  fresh: () => new Pool(),
  moves: [p => p.Split(), p => p.Merge(), p => p.Drain(10)],
  assert: p => Assert.True(p.Total >= 0)
).Run(sequences: 5, movesPerSequence: 50, seed: 1);
```

A failure names the sequence, the move index, and the full move list applied so far, so it
reproduces with the same seed.

### `StaticStateCollection`: catching an unsynchronised xUnit collection

xUnit runs test classes in parallel by default; two classes that mutate the same process-wide
static must join one `[Collection("Name")]`, backed by a `[CollectionDefinition("Name", ...)]`. A
collection name with no definition still "works" - xUnit synthesises one - but it synthesises a
*different* one per typo, so two classes meaning to serialize against each other race instead. Call
the guard once per test assembly:

```csharp
[Fact]
public void Every_collection_name_has_a_definition() =>
  StaticStateCollection.EveryCollectionNameHasADefinition(Assembly.GetExecutingAssembly());
```

### Repo paths and released-code history for a new mod

`RepoPaths.Assets(domain)` falls back to `mods/<domain>/assets/<domain>` for a domain it does not
already know, so a new mod's own domain resolves before anyone edits the harness for it.
`RepoPaths.Register(domain, modFolder)` is there for the case that does need an edit - a domain whose
zip ships from a *different* mod's folder, the way `game`'s shared vanilla-lang overlay ships from
iiex's.

Released-code history (`ReleasedCodes`, `ReleasedVersions`, `ReleasedCodeDebt`) is registered per
mod, from that mod's own test `ModuleInit`, through `ReleasedHistory.Register`:

```csharp
ReleasedHistory.Register(
  mod: "iiex",
  shipped: PpexShippedRows,
  entityClasses: PpexEntityClasses,
  versions: new Dictionary<string, string> { ["ppex"] = "0.6.8" },
  debt: PpexKnownUnmigrated
);
```

`ReleasedCodes.Ppex`/`.Smex`/`.Exlib`, `ReleasedVersions.HighestPublished` and
`ReleasedCodeDebt.KnownUnmigrated` are unchanged as call sites - they now read `ReleasedHistory`
instead of holding the rows themselves, so the harness carries no mod's shipping history.

### Regenerating a block-code table: `exmod codes`

`{Mod}Blocks.g.cs` (`ExlibBlocks`, `IiexBlocks`, `SiexBlocks`) is generated from the mod's own
code-first block definitions by the standalone `infra/tools/BlockCodeEmitter` console tool, not by
running the tests:

```
exmod codes iiex
```

builds the mod, runs the emitter, and rebuilds so a table change that no longer compiles is caught
immediately. The mod's own `*BlocksCodeTests` fixture (`BlockCodeEmitter.CheckOrWrite`) stays
read-only - it fails and names `exmod codes <mod>` when the table has drifted, rather than writing
it. There is no environment-variable switch for this anymore.

### Real assets: `TestWorld.LoadAssets`

`Register`/`RegisterItem` build a stand-in `Block`/`Item` by hand; `LoadAssets` instead drives the
game's own asset manager and object loader against a mod's real assets and compiled classes, so the
`Block`/`Item` it registers is the one the object loader itself resolved - real class, real
variants:

```csharp
using var world = new TestWorld();
world.LoadAssets(Path.Combine(RepoPaths.Root, "samples", "HelloExpanded"));
Block hello = world.World.GetBlock(new AssetLocation("helloexpanded:hello-n"))!;
```

`modPath` is a mod's folder (`modinfo.json`, `assets/<modid>/`, a compiled dll under `bin/`); every
`ModSystem` the mod's assembly declares is `Start`ed against an isolated API before the object
loader runs, so both JSON and code-first mods resolve. Base `game` domain assets load too, but not
vanilla survival/creative content - those blocks need classes only `VSSurvivalMod`'s own
`ModSystem`s register. See `docs/internal/research/2026-09-06-asset-loading-spike.md` for the
wall-by-wall trace this was built from.

## 4. Boot it

### The smoke lane

`exmod smoke` is the zero-effort rung below everything else on this page: one command boots the
**real** dedicated server with your mod and fails if anything goes wrong - no test project, no xUnit,
no fixtures.

```
exmod smoke                              # every built mod under mods/*/src/bin/Debug/Mods/mod
exmod smoke -Mods path/to/one/mod        # a single built mod folder instead
exmod smoke -Mods path/to/several-mods   # a folder holding several built mod folders
```

It provisions a dedicated-server install if it doesn't have one yet (`exmod provision game -Kind
server`), assembles a scratch mods folder keyed by each mod's own `modid`, boots
`VintagestoryServer.dll --dataPath <scratch> --addModPath <scratch>` on port 42499 (unusual on
purpose, so it never collides with a game you're actually playing on the same machine), waits for
"Dedicated Server now running", sends `/exmod verify` then `/stop` on its stdin, and reports what it
found: every `[exlib]` notification line, every `[Error]`/`[Fatal]` log line, and the verify summary.
It exits non-zero - printing the offending lines - on a boot timeout (`-Timeout`, default 180s), any
`[Error]`/`[Fatal]` line, or a verify summary with errors in it; `-KeepData` keeps the scratch
dataPath and mods folder afterwards instead of deleting them, for chasing a failure.

A mod whose `modinfo.json` doesn't even parse is still copied in (under its folder name) rather than
failed locally, so the real mod loader is what reports it - the point of this lane is what the game
itself catches, not what the script can catch first.

CI runs it in its own job (see `.github/workflows/tests.yml`'s `smoke` job, or
[`templates/ci/smoke.yml`](../../../templates/ci/smoke.yml) for a mod outside this repo), separate
from the test job so a game-loading failure and a test failure are reported distinctly.

#### CI templates

`templates/ci/tests.yml` (provision + `dotnet test`) and `templates/ci/smoke.yml` (this section's
lane) are the copy-into-your-own-repo forms of `.github/workflows/tests.yml`'s two jobs - each
carries the two edits a third-party repo needs to make, marked `CHANGE` inline.

## 5. Where things are

One namespace, `ExpandedLib.Testing`; the folders sort files by what you're doing, not by type.

| Folder | A test author finds... |
|---|---|
| `World/` | `TestWorld`, `TestBlocks`, `TestLang`, `VsAssemblyResolver` |
| `Scenes/` | `Scene`, `SceneDiagram`, `SceneGrid` |
| `Rigs/` | `StructureRig`, `MachineRig`, `RegistryLawScanner`, `ResourceInvariant<TState>`, `StaticStateCollection`, `HarmonyFixture` |
| `Doubles/` | stand-ins: `StubNetwork`, `TestNetworkBlock`, `CapturingNode`, `SeverableNode`, `OrientableNode`, `RccFake`, `TestMemberBlockEntity`, `MechPower`; supported doubles: `TestPlayer`, `TestInventory`, `TestModLoader`, `WorldConfigBag`, `ModConfigFiles`, `RecordingLogger`, `TestChannels` |
| `Checks/` | the content validators: definition parity, goldens, lang coverage, wiki parity, `LayoutTable`, ... - most now wrap [`ExpandedLib.Checks`](Checks), the same rule the game runs at load and `/exmod verify`; see the [Testing API Reference](Testing-API-Reference)'s full "Checks" table |
| `Repo/` | `RepoPaths`, `ReleasedHistory`, `ReleasedCodes`, `ReleasedVersions`, `ReleasedCodeDebt`, `BlockCodeEmitter` |
| (root) | `ReflectionHelpers` |

For the complete public surface, folder by folder, see the [Testing API Reference](Testing-API-Reference)'s
own "Where things are" table - this one stays a quick map, that one is the one kept in lockstep with
the assembly by `HarnessSurfaceTests`.

## 6. Examples

Six files worth reading end to end before writing a new one, one per shape of test:
`Networks/NetworkGraphTests.cs` (pure graph maths over POCOs), `Structures/StructureRigTests.cs`
(a mega-block driven through `StructureRig`), `Machines/ProductionMachineTests.cs` (a machine driven
through `MachineRig`), `Config/ConfigMigrationTests.cs` (the load-migrate-stamp-save cycle over a fake
`ICoreAPI`), `Definitions/ExlibDefinitionGoldenTests.cs` (a golden-file comparison), and
`Invariants/ShippedAssetJsonTests.cs` (a repository-wide JSON guard).

Beside them, from the Task T10 pass over exlib's own untested public surface:
`Registries/ExmodCommandTests.cs` (a hand-rolled fluent `IChatCommand` fake, since NSubstitute returns
a fresh substitute per call rather than the same one down a chain), `Registries/PreferencesTests.cs`,
`Registries/Recipes/RecipeProfilesTests.cs`, `Config/ConfigAttributesTests.cs`,
`Blocks/ConstructionTests.cs`, `Migrations/BlockRemovalTests.cs`, `Migrations/ItemCodeMigrationTests.cs`,
`Migrations/ChunkSweeperTests.cs`, `Helpers/LegacyTests.cs` (legacy-lane-only; empty on 1.22) and
`Helpers/Measure/HandbookUnitPatchTests.cs`.

## 7. Limits

- **Side.** The harness fakes the **server**; tests exercise server-side simulation. Client-only
  render paths, GUI and real chunk loading aren't covered - `IServerPlayer` works (see
  [§2 Doubles](#2-stand-something-up) above), because the game's own object graph makes it a
  server-side type in every way that matters to test code.
- **The publicizer.** `IPlayer`/`IServerPlayer` can be substituted at all only because provisioning
  patches the game's `VintagestoryAPI.dll` in place - see "Provisioning the game install" under
  [§1](#1-ten-minutes-to-a-green-test). It never touches the copy a player runs.
- **Legacy targets.** The harness multi-targets `net8.0`/`net7.0` under `-p:Legacy=true` and
  branches on `#if GAME_GE_1_22` for the tick-listener signature change, so the same tests run on
  1.20/1.21 too.
- **Burst/realism.** Some behaviours (e.g. pipe burst) need a real concrete block, not a stub -
  the doubles are for graph topology, your own network/node types bring the gameplay semantics.

## Related pages

- [Testing API Reference](Testing-API-Reference) - every public type and signature.
- [Block Networks](Block-Networks) - the system under test.
