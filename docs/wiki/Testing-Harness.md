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
- `ReflectionHelpers` / `TestBlocks` - prime private fields and configure bare blocks without the
  asset pipeline.

## Setting up a test project

Reference the harness, xUnit, the test SDK and NSubstitute, plus the game API DLLs (with
`<Private>false</Private>` so you don't copy them). Inside this monorepo, mirror
`test/ExpandedLib.Tests/ExpandedLib.Tests.csproj`:

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
    <ProjectReference Include="..\..\src\YourMod\YourMod.csproj" />
    <ProjectReference Include="..\ExpandedLib.Testing\ExpandedLib.Testing.csproj" />
  </ItemGroup>
</Project>
```

## Consuming outside this repo

The harness is a developer library, not a NuGet package and not a game mod. Two ways to use it
from a separate mod repo:

**A - Reference from source (recommended if you might tweak it).** Add `ExpandedLib.Testing` (and
`ExpandedLib`) as a git submodule or sibling checkout and `ProjectReference` the `.csproj`s, exactly
as above. You track upstream changes and can debug into the harness.

**B - The release bundle.** Each [GitHub release](https://github.com/ringavirda/modding-vsexpanded/releases)
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

> Only the current game version is bundled. To target 1.20 / 1.21 test runs, use option **A** -
> the harness multi-targets `net8.0`/`net7.0` from source under `-p:Legacy=true`.

## The required module initializer

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

Every test project in this repo (`ExpandedLib.Tests`, `PipesAndPowerExpanded.Tests`,
`SteelmakingExpanded.Tests`, `Integration.Tests`) has exactly this. `VsAssemblyResolver.Register`
is idempotent and resolves the install via the `[AssemblyMetadata("GameInstallEnv")]` environment
variable (e.g. `VINTAGE_STORY`) or, failing that, by walking up to `.game/<slug>`.

## A first test

```csharp
using ExpandedLib.Testing;
using ExpandedLib.Testing.Doubles;
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

## Integration tests with `Scene` and `SceneDiagram`

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

`SceneDiagram` maps characters to placement lambdas: columns advance +X, rows advance +Z, and
`Stack(baseY, layers...)` stacks layers bottom-to-top in +Y.

## Priming private state

`ReflectionHelpers` reaches non-public fields/properties/methods (walking the base hierarchy) when
you need to set up or assert internal state:

```csharp
var boiler = new BlockEntityBoilerCornish();
ReflectionHelpers.SetField(boiler, "_waterVolume", 300f);
var water = (float)ReflectionHelpers.GetField(boiler, "_waterVolume")!;
Assert.Equal(300f, water, 3);
```

## Notes

- **Side.** The harness fakes the **server**; tests exercise server-side simulation. Client-only
  render paths aren't covered.
- **Legacy targets.** The harness multi-targets `net8.0`/`net7.0` under `-p:Legacy=true` and
  branches on `#if GAME_GE_1_22` for the tick-listener signature change, so the same tests run on
  1.20/1.21 too.
- **Burst/realism.** Some behaviours (e.g. pipe burst) need a real concrete block, not a stub -
  the doubles are for graph topology, your own network/node types bring the gameplay semantics.

## Related pages

- [Testing API Reference](Testing-API-Reference) - every public type and signature.
- [Block Networks](Block-Networks) - the system under test.
