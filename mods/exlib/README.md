# Expanded Library (`exlib`)

Shared framework mod for the *Expanded* family
([Iron Industry Expanded](../iiex/README.md),
[Steel Industry Expanded](../siex/README.md)). It ships no gameplay content
of its own - install it because another mod depends on it.

## What it provides

- **Block networks** (`Networks/`) - a generic connected-graph framework: the model
  (`BlockNetwork` + subclasses, the `I*Node`/`I*Connector` contracts) and the
  engine-facing shell (`BlockNetworkNode`, node block entities, the
  `BlockNetworkModSystem` manager) share the one folder and namespace. `iiex`
  registers both the "pipe" and the "molten" network on it.
- **Multiblock structures** (`Structures/`) - completion monitoring,
  build-outline projection (ctrl+shift+rmb), crash-safe incomplete-part highlighting,
  and the shared invisible `structurefiller` block that gives mega-block machines
  per-cell collision.
- **Production machines** (`Machines/`) - `BlockEntityProductionMachine` base
  (the tick lifecycle + operational gate) and `MachinePorts` helpers, shared by
  engines, furnaces, converters and sub-machines.
- **Block-entity healing** (`Migrations/`) - recreates a block entity that was
  lost while its block survived (a load failure or desync), automatically on chunk
  load and via `/exmod heal`.
- **Registries** (`Registries/`) - attribute-driven registration:
  - `Entities/` - `[BlockRegister]` / `[ItemRegister]` / `[BlockEntityRegister]` /
    `[BlockBehaviorRegister]` / `[BlockEntityBehaviorRegister]` /
    `[CollectibleBehaviorRegister]` for blocks, items, entities and behaviors.
  - `Commands/` - `[CommandRegister]` / `[SubCommandRegister]` building the shared
    `/exmod` (server) and `.exmod` (client) command root.
  - `Recipes/` - per-mod recipe-cost profiles switchable with `/exmod recipes`.
  - `Preferences/` - per-player display-preference store.
- **Config** (`Config/`) - generic versioned config store (`ExConfigRegister`) with
  source-generated value accessors, range-gated values, migrations and live
  `/exmod config` editing.
- **Block migrations** (`Migrations/`) - rewrites renamed/re-variantted block
  codes (and matching item stacks) in old saves as chunks load.
- **Data catalogues** (`Catalogues/`) - shared process, material-role, liquid and
  storage-occupancy catalogues, their loaders and code contributors, plus the load
  report every loader hands back.
- **Shared helpers** (`Helpers/`) - `ExOrientation` (rotation math),
  `ExParticles` / `ExSounds` (effect catalogues), `ExCreativeTabs`,
  `ExInventory` / `ExItems`, `ExBlockNames` (variant display names), `ExContentGate`
  (hide-from-creative/handbook + recipe removal), the shared `SurfaceRenderer`
  (`Rendering/`) and the unit-display system (`Measure/`).
- **Legacy support** (`Legacy/`) - shims/polyfills that let the family build and run
  against Vintage Story 1.21 and 1.20 alongside 1.22.
- **Modules** (`Registries/ExModuleAttribute.cs`, `ExModules`, `ExModuleHost`) - an assembly that
  extends the framework or a mod built on it without carrying a `ModSystem` of its own, driven
  through a host mod's lifecycle instead: `ExpandedLib.Industry` is the first, shipped inside this
  mod's own folder; a third party's own mod can be one too, depending on exlib. See the wiki's
  [Modules](../wiki/Modules.md) page.

## Start from the sample

`samples/HelloExpanded` is a third-party mod written against this library end to end: a block, a
saved counter, a config value and a command, plus two headless tests, all in the shapes the wiki's
[Getting Started](../wiki/Getting-Started.md) walk teaches. It builds and boots like any other mod
here (`dotnet build VintageStory.sln`, `exmod smoke`) - read it alongside the wiki rather than typing
its snippets by hand.

## What is supported

`ExpandedLib.*` outside `ExpandedLib.Industry` is the supported contract: every public type
there is listed on the wiki's [Supported API](../wiki/Supported-API.md) page, and a public type
missing from that list has been hidden from IntelliSense with `[EditorBrowsable(Never)]` because
the engine has to see it, not because a mod is meant to call it. `ExpandedLib.Industry` is also
public, but it is the family's own content layer and changes without notice.

## Packages

The mod ships as one download - one modinfo, one folder, `exlib.dll` and `exlib.industry.dll` - and
the module system means that is not a hard limit of two: any assembly, inside this folder or
shipped as its own mod, can declare `[assembly: ExModule]` and join exlib's lifecycle without a
`ModSystem` of its own. It ships as four NuGet packages a mod project references at compile time:

| Package | What it is |
| --- | --- |
| `ExpandedLib` | the framework: `exlib.dll`, the config/lang source generators, and the build/ plumbing (`GamePath` resolution, provisioning, asset globs) - a consumer needs no props of its own beyond a `TargetFramework` |
| `ExpandedLib.Industry` | this family's content layer: `exlib.industry.dll`, beside it in the same mod folder |
| `ExpandedLib.Testing` | the headless xUnit harness, for a test project rather than a mod |
| `ExpandedLib.Verify` | a .NET tool, `exlib-verify`, that checks a JSON-only mod's assets with no game running |

They are built for the current Vintage Story version only. The mod zips on the GitHub releases
page cover the older versions; the packages do not, because a mod targeting an older version
builds against a different .NET and a different game API.

None of them carries the game's own assemblies: a consuming project references
`VintagestoryAPI.dll` and friends from its own install, the same way any Vintage Story mod does.
At runtime the player installs this mod, and the game loads it like any other dependency.

## Building

```sh
dotnet build mods/exlib/src/ExpandedLib.csproj        # the framework
dotnet build mods/exlib/industry/ExpandedLib.Industry.csproj   # the family layer
```
