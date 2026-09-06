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
    "exlib": "0.7.3"
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
  <ProjectReference Include="..\..\exlib\src\ExpandedLib.csproj">
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

exlib is **attribute-driven**: you tag classes, and a `ModSystem` deriving `ExModSystem` registers
them all by reflection with no calls of its own to write. No manual
`api.RegisterBlockClass(...)` lists to maintain.

```csharp
using ExpandedLib.Registries;
using Vintagestory.API.Common;

[BlockRegister]                       // registers as "yourmod.BlockMachine"
public class BlockMachine : Block { }

[BlockEntityRegister]                 // registers as "yourmod.BlockEntityMachine" (+ short aliases)
public class BlockEntityMachine : BlockEntity { }

public class YourModSystem : ExModSystem { }
```

That single, empty class also registers any `[CommandRegister]`/`[SubCommandRegister]` class on
each side and any `[PreferenceRegister]` class on the client - see **[Commands](Commands)** for
adding one. If you need something to run in a particular order relative to registration (or aren't
deriving `ModSystem` at all), **[Registries](Registries)** documents the explicit `RegisterAll`
calls this class makes for you and the one ordering rule they carry.

See **[Source Generators](Source-Generators)** for the two generators exlib ships - typed config
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

## 4. Your first block

`samples/HelloExpanded` in this repo is everything above, buildable and bootable: a `Code` mod
depending on `exlib`, one block, one config value, one command, and two tests. Read it file by file
rather than typing the snippets by hand - every one below (bar one labelled alternative) is copied
verbatim from it, so it compiles.

A code-first block is a class that implements `IExBlockDefProvider` and carries `[BlockRegister]`.
There is no `blocktypes/hello.json` anywhere in the mod's `assets/` folder; the JSON the object loader
reads is built by `ExBlockDef` and injected in memory at load:

```csharp
[BlockRegister]
public class BlockHello : Block, IExBlockDefProvider {
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "hello")
        .Class<BlockHello>()
        .EntityClass<BlockEntityHello>()
        .Material(EnumBlockMaterial.Stone)
        .Shape("game:block/basic/cube")
        .TextureAll("survival:block/stone/rock/granite*")
        .MetalSounds()
        .Resistance(3.0f)
        .MiningTier(1)
        .MineTool(EnumTool.Pickaxe)
        .CreativeCommon("*")
        .SideVariant()
        .Behavior<BlockBehaviorExOrientable>(),
    ];
}
```

`ExModSystem` registers it - and every other `[BlockRegister]`/`[BlockEntityRegister]`/
`IExBlockDefProvider` in the assembly, and loads `HelloValues` - with nothing to write:

```csharp
public class HelloExpandedModSystem : ExModSystem { }
```

The explicit form behind it, for a mod system that needs a different order:

```csharp
public class HelloExpandedModSystem : ModSystem {
  public override void Start(ICoreAPI api) {
    HelloValues.Load(api);
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);
  }
}
```

Build the sample and boot it (`exmod smoke -Mods samples/HelloExpanded/bin/Debug/Mods/mod`, alongside
exlib) and the log carries this, in order: the definition is injected, then the content checks run
against it (see [Migrations & Healing](Migrations-and-Healing) for what the checks line means and
[Checks](Checks) for the full list) and every one is clean:

```
[exlib] Injected 2 code-first block definition(s).
[exlib] check DefinitionCatalogue (helloexpanded): 0 error(s)
[exlib] check MultiblockCodes (helloexpanded): 0 error(s)
[exlib] check RecipeCodes (helloexpanded): 0 error(s)
[exlib] check LangCoverage (helloexpanded): 0 error(s)
[exlib] check NetworkNodeContract (helloexpanded): 0 error(s)
[exlib] check PinnedNetworkNodes (helloexpanded): 0 error(s)
[exlib] check CodePrefixCollision (helloexpanded): 0 error(s)
```

The count is 2, not 1: exlib injects one of its own (the structure-filler block every megablock
reuses) alongside this mod's `hello`. A domain with no definitions of its own still gets a full row
of `0 error(s)` - the checks run per domain regardless, so a clean run reads the same whether there
was anything to check or not.

## 5. State, clicks and info in three lines

The block entity's whole job is a counter that survives a save/reload, ticking on the shared
production lifecycle from [Production Machines](Production-Machines):

```csharp
[BlockEntityRegister]
public class BlockEntityHello : BlockEntityProductionMachine {
  [Persist]
  private int _ticks;

  protected override int ProductionTickMs => HelloValues.TickIntervalMs;
  protected override bool CanRunProduction => true;
  protected override void OnProductionTick(float dt) => _ticks++;

  public void ResetTicks() => _ticks = 0;

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.Lang("helloexpanded:ticks", _ticks);
    int neighbours = Api.World.BlockAccessor
      .Neighbours<BlockEntityHello>(Pos)
      .Count();
    dsc.Lang("helloexpanded:neighbours", neighbours);
  }
}
```

`[Persist]` is the whole save/load story: no `ToTreeAttributes`/`FromTreeAttributes` override, no key
to spell twice. `ExBlockAccess.Neighbours<T>` replaces the six-line
`GetBlockEntity(pos.AddCopy(facing)) is T be` loop a hand-written block info would otherwise carry,
and `ExInfo.Lang` is `dsc.AppendLine(Lang.Get(key, args))` written once as an extension method rather
than at every call site.

The block answers a sneak-click by resetting that counter, through `ExInteraction` rather than a
hand-rolled `IPlayer`/`BlockSelection` guard:

```csharp
public override bool OnBlockInteractStart(
  IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel
) {
  if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityHello be)
    return base.OnBlockInteractStart(world, byPlayer, blockSel);

  Interaction interaction = ExInteraction.Of(world, byPlayer, blockSel);
  if (!interaction.Sneaking)
    return base.OnBlockInteractStart(world, byPlayer, blockSel);

  if (interaction.IsClient)
    return true;

  be.ResetTicks();
  return true;
}
```

See [Helpers & Renderers](Helpers-and-Renderers) "Declared state" and "Block-entity lookups and side
checks" for the full surface of both.

## 6. A config value and a command

One tunable, generated into a typed `HelloValues` accessor:

```csharp
[ExConfigRegister("helloexpanded.json", "helloexpanded", Manageable = true)]
public class HelloConfig : IExVersionedConfig {
  public string? ConfigVersion { get; set; }

  [ExConfigRange(100, 10000)]
  public int TickIntervalMs { get; set; } = 1000;
}
```

`Manageable = true` is what puts it on the generic switch: `/exmod config helloexpanded
tickintervalms 500` reads or writes it live, validated against the `[ExConfigRange]` bound, with no
code of this mod's own involved. `HelloValues.TickIntervalMs` (read live in
`BlockEntityHello.ProductionTickMs` above) is generated from the property name.

A one-line command, attached to the shared `.exmod`/`/exmod` root rather than declaring its own:

```csharp
[SubCommandRegister(Side = EnumAppSide.Server)]
public sealed class HelloSubCommand : IExSubCommand {
  public string ParentName => "exmod";

  public void Register(ICoreAPI api, Mod mod, IChatCommand parent) {
    parent
      .BeginSubCommand("hello")
      .WithDescription(Lang.Get("helloexpanded:command-hello-desc"))
      .HandleWith(args =>
        TextCommandResult.Success(
          Lang.Get("helloexpanded:command-hello-result", HelloValues.TickIntervalMs)
        )
      )
      .EndSubCommand();
  }
}
```

`/exmod hello` now prints the tick interval. See [Config System](Config-System) and
[Commands](Commands) for everything else either surface offers.

## 7. Test it

`samples/HelloExpanded.Tests` drives the same block entity headlessly through
[Testing Harness](Testing-Harness)'s `TestWorld`, with no game launch:

```csharp
[Fact]
public void Counts_a_tick_and_round_trips_through_the_tree() {
  var world = new TestWorld();
  var pos = new BlockPos(0, 0, 0);
  Block block = TestBlocks.Configure(new BlockHello(), "helloexpanded:hello", 1);
  var be = new BlockEntityHello();
  world.Place(pos, block, be);
  world.Initialize(be);

  world.FireBlockEntityTicks(times: 3);

  var tree = new TreeAttribute();
  be.ToTreeAttributes(tree);
  Assert.Equal(3, tree.GetInt("ticks"));
}
```

A second test stands up a `TestPlayer`, sets `player.Entity.Controls.ShiftKey = true` and calls
`OnBlockInteractStart` directly, asserting the counter is back at zero - the same handler a real
sneak-click runs, exercised with no client and no server socket. Run both with
`dotnet test samples/HelloExpanded.Tests/HelloExpanded.Tests.csproj`, or
`exmod test latest -Filter HelloExpanded`.

## 8. Boot it

`exmod smoke -Mods samples/HelloExpanded/bin/Debug/Mods/mod` (the default smoke lane already includes
it) launches the real dedicated server against the built mod, runs the content checks and `/exmod
verify`, and fails on a boot timeout or an `[Error]`/`[Fatal]` log line - the same lane this repo's CI
runs on every mod, now covering the one you just read.

## 9. Pick the system you need

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
