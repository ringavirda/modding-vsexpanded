# Getting Started

This page gets a third-party mod consuming `exlib`: declaring the runtime dependency, wiring a
project reference so you can call its APIs, and registering your first block.

## 1. Depend on exlib at runtime

`exlib` is a separate `Code` mod. In your mod's `modinfo.json`, add it under `dependencies`
with the minimum version you build against:

```json
{
  "type": "Code",
  "modid": "yourmod",
  "name": "Your Mod",
  "version": "1.0.0",
  "dependencies": {
    "game": "1.22.0",
    "exlib": "0.7.2"
  }
}
```

> ⚠ **A dependency is a minimum, not a pin.** The game accepts any installed `exlib` at or above the
> number you write, so a floor left at an old release lets a player satisfy it with an `exlib` that
> predates the method you are calling - and the failure arrives at world load as a missing member,
> not as a dependency error. Declare the version you actually compiled against, and raise it whenever
> you start calling something newer.

Declare the `game` floor the same way: the oldest version you support. This repo builds the whole
family against 1.20, 1.21 and 1.22 from one source tree and rewrites each `modinfo.json`'s game
version per target as it packs, so the shipped 1.20 zip declares `1.20.0` while the source declares
the current floor. If you target a single version, just name it.

At load time the game ensures `exlib` is present and loaded before your mod, so its
`ModSystem`s (the block-network manager, migration sweeper, healer, `/exmod` root) are already
up when your `Start`/`StartServerSide`/`StartClientSide` run.

> **Game versions.** `exlib` targets 1.22 but the family also builds and runs on 1.21 and 1.20
> via the `Legacy/` shim. If you only target 1.22 you can ignore the shim entirely; the public
> APIs on this wiki are the same across versions unless a page says otherwise.

## 2. Reference exlib at compile time

The project is called `ExpandedLib` and that is its root namespace, but the assembly it builds is
**`exlib.dll`** - the assembly name matches the mod id. That is the file you reference, and there is
no `ExpandedLib.dll` anywhere outside `obj/`.

Inside this monorepo it is a plain `ProjectReference` with copy-local turned **off**:

```xml
<ItemGroup>
  <ProjectReference Include="..\ExpandedLib\ExpandedLib.csproj">
    <Private>false</Private>
  </ProjectReference>
</ItemGroup>
```

> ⚠ **`<Private>false</Private>` is not optional, and omitting it fails silently.** Without it the SDK
> copies `exlib.dll` into your mod's output, and Vintage Story refuses to load a mod folder carrying a
> second assembly with `ModSystem`s in it - *"Found multiple .dll files with ModSystems and/or ModInfo
> attributes"*. Your mod is then simply absent from the loaded-mod list. The player installs `exlib`
> as its own mod, so you never bundle a copy.
>
> It also **does not propagate transitively.** If you reference another mod that itself references
> `exlib`, the SDK synthesises a transitive reference with the default copy-local *on*. Declare every
> such project explicitly, each with its own `<Private>false</Private>`; the explicit item's metadata
> beats the synthesised one.

Outside this repo you reference the same assembly as a file. `exlib` ships as `exlib_<version>.zip`,
which the game loads without unpacking, so take `exlib.dll` out of that zip (or out of the
[exlib-testing bundle](Testing-Harness), which carries it alongside the harness) and keep it beside
your project:

```xml
<PropertyGroup>
  <!-- The Vintage Story install. Set VINTAGE_STORY, or pass -p:GamePath=... on the command line. -->
  <GamePath Condition="'$(GamePath)' == ''">$(VINTAGE_STORY)</GamePath>
</PropertyGroup>

<Target Name="CheckGamePath" BeforeTargets="BeforeBuild" Condition="'$(GamePath)' == ''">
  <Error Text="Set the VINTAGE_STORY environment variable to your game install." />
</Target>

<ItemGroup>
  <Reference Include="exlib">
    <HintPath>libs/exlib.dll</HintPath>
    <Private>false</Private>
  </Reference>
  <Reference Include="VintagestoryAPI">
    <HintPath>$(GamePath)/VintagestoryAPI.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

`$(GamePath)` is this wiki's convention for the install directory and the
[Testing Harness](Testing-Harness) page assumes it too, but nothing defines it for you - the
definition above is the whole of it. Stop at the environment variable and a hard error rather than
guessing per-OS install locations; this repo defines none, and a wrong guess fails later and less
clearly than the `<Error>` does.

## 3. Register your content

exlib is **attribute-driven**: you tag classes, and a one-line call in `Start` registers them
all by reflection. No manual `api.RegisterBlockClass(...)` lists to maintain.

```csharp
using ExpandedLib.Registries.Commands;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;

[BlockRegister]                       // registers as "yourmod.BlockMachine"
public class BlockMachine : Block { }

[BlockEntityRegister]                 // registers as "yourmod.BlockEntityMachine" (+ short aliases)
public class BlockEntityMachine : BlockEntity { }

public class YourModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        // Scans THIS assembly for every [BlockRegister]/[ItemRegister]/[BlockEntityRegister]/
        // [*BehaviorRegister] class and registers each with the matching game registry. The same
        // scan picks up any IExBlockDefProvider / IExItemDefProvider / IExRecipeDefProvider for
        // code-first definitions.
        EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        // Scans for [CommandRegister]/[SubCommandRegister] classes (optional). Register commands
        // from the side hooks rather than Start - see Registries for why the client side matters.
        CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
    }
}
```

See **[Registries](Registries)** for every attribute and the full registration story, and
**[Source Generators](Source-Generators)** for the two generators exlib ships - typed config
accessors and typed lang keys. Read a block's JSON `attributes` with
`Attributes["..."].AsFloat()` as usual, or skip the JSON entirely and declare the block code-first
through `IExBlockDefProvider`.

### If other mods will name your types

Add an assembly-level domain marker so a cross-assembly lookup can resolve your classes:

```csharp
[assembly: ExDomain("yourmod")]
```

It must equal your mod id. `EntityRegistry.RegisterAll` does not need it - that path keys off the mod
id directly - so a mod nobody else extends works without it. It matters when *another* mod names one
of your types, for example through `ExBlockDef.Class<T>()`: the key is resolved from the type's own
assembly, and without the marker that lookup produces a key nobody registered. The block half of that
failure is not logged, which is why the attribute is worth declaring up front.

## 4. Pick the system you need

| You want to... | Read |
| --- | --- |
| Build pipes / wires / canals (anything that connects into a network) | [Block Networks](Block-Networks) |
| Build a multi-cell machine (furnace, boiler) with completion + build outline | [Multiblock Structures](Multiblock-Structures) |
| Run periodic server-side work on a block entity | [Production Machines](Production-Machines) |
| Use the vanilla right-click-construction flow with salvage drops | [Construction (RCC)](Construction) |
| Ship gameplay tunables players can edit live | [Config System](Config-System) |
| Add a `/exmod` (server) or `.exmod` (client) sub-command | [Commands](Commands) |
| Offer cheap/normal recipe-cost levels | [Recipe Costs](Recipe-Costs) |
| Rotation math, particles, sounds, inventory counting, content gating | [Helpers & Renderers](Helpers-and-Renderers) |
| Rename/remove blocks in old saves without orphaning them | [Migrations & Healing](Migrations-and-Healing) |
| Unit/integration-test all of the above headlessly | [Testing Harness](Testing-Harness) |
