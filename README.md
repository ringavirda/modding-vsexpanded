# Fallenstar's Expanded mods

A monorepo of five [Vintage Story](https://www.vintagestory.at/) mods that together
add an industrial-era production chain - pipe networks, steam power, bulk iron and
steel making:

| Mod                                                             | modid   | What it is                                                                                                   |
| --------------------------------------------------------------- | ------- | ------------------------------------------------------------------------------------------------------------ |
| [Expanded Library](mods/exlib/README.md)                   | `exlib` | Shared framework: block networks, multiblock structures, registries (entities/commands/config), save migrations, common helpers. |
| [Iron Industry Expanded](mods/iiex/README.md)    | `iiex`  | The whole iron tier: cold-blast ironmaking, the `pipe`/`molten`/`mpenergy` networks, the plated and cast pipe tiers with their fittings, the Cornish boiler and the Watt engine. |
| [Steel Industry Expanded](mods/siex/README.md)  | `siex`  | The whole steel tier: hot blast furnace, cowper stoves, molten-metal casting, Bessemer converter, air blower, plus the high-pressure Lancashire boiler and Cornish engine and the rolled pipe tier. |

## Repository layout

| Path                            | Purpose                                                                |
| ------------------------------- | ---------------------------------------------------------------------- |
| `mods/exlib/src/`               | The `exlib` framework mod (C# + minimal assets).                       |
| `mods/exlib/generators/`        | Roslyn source generators (config value accessors, typed lang keys).    |
| `mods/iiex/src/`                | The `iiex` mod: networks, pipes, ironmaking and low-pressure steam.    |
| `mods/siex/src/`                | The `siex` mod: the steel chain and the high-pressure steam leaves.    |
| `mods/Directory.Build.props`    | Shared MSBuild config + the supported-game-version manifest.           |
| `mods/<mod>/assets/<domain>/`   | Each mod's own asset tree, the packaged layout verbatim.               |
| `mods/<mod>/tests/`             | Headless xUnit test projects (per-mod unit tests + cross-mod integration). |
| `scripts/`                      | Game/.NET provisioning, mod staging, test runners.                     |
| `docs/`                         | Cross-mod design docs, internal plans, setups.                        |
| `infra/CakeBuild/`              | Cake build project that publishes per-game-version release zips into `dist/Releases/`. |
| `VintageStory.sln`              | Solution tying the projects together.                                  |

The dependency chain is a straight line, `exlib -> iiex -> siex`. Every reference is
`Private=false`, so players install each mod separately; the network manager identity
lives in `exlib` only.

## Code conventions

Code is organized by **feature**, and within each feature by Vintage Story's
`Block` / `BlockEntity` split:

- **`Block*`** classes = the block definition (placement, orientation, interaction
  routing, drops).
- **`BlockEntity*`** classes = the per-tile state and logic (ticking, inventory,
  networks, rendering).
- **`Patches/`** in each mod = Harmony patches into vanilla classes. Vanilla behavior
  is extended via prefix/postfix patches, never by re-registering vanilla class names,
  so other mods touching the same blocks can coexist.
- **`BlockMigrations/`** in each mod = `IBlockCodeMigration` implementations that
  rewrite old block codes when variants change between versions (the framework in
  `exlib` discovers them by reflection and applies them as chunks load).
- Registration is attribute-driven: decorate a class with the kind-specific
  `[BlockRegister]` / `[ItemRegister]` / `[BlockEntityRegister]` /
  `[BlockBehaviorRegister]` (etc.) attribute and `EntityRegistry.RegisterAll` picks it
  up. Chat commands use `[CommandRegister]` / `[SubCommandRegister]` the same way.
- Gameplay tunables live in a per-mod `*Values` accessor (`IiexValues`, `IiexValues`,
  `SiexValues`, `SiexValues` - source-generated from the `[ExConfigRegister]` config
  classes), persisted as one section per mod in the shared `ModConfig/ex_values.json` and
  editable live with `/exmod config`. Recipe and construction costs live in the same
  per-mod-section way in `ModConfig/ex_recipes.json` (per-level `normal` / `cheap`
  numbers), switched with `/exmod recipes` and applied on the next world reload. Both
  files auto-fold each mod's pre-merge standalone `*_values.json` / `*_recipes.json`.

## Network system (`mods/exlib/src/Networks/` + `Blocks/Networks/`)

Both the pipe and molten systems are instances of one generic block-network framework -
and both concrete networks (`PipeNetwork`, `MoltenNetwork`) now live in `exlib` alongside
the framework, so every mod shares one implementation. A network is a connected graph
of same-type nodes; the library owns the **graph-level** work (membership, merge on join,
fracture on break, per-tick dispatch) *and* the concrete simulations, while each mod
registers the type and supplies only its content-specific pieces through small seams.

The split follows the repo-wide layout rule (see
[conventions.md](docs/design/conventions.md#networks)): `Networks/` is the **graph model**, which you
can reason about with no world loaded; `Blocks/Networks/` is where it meets the engine (`Block`,
`BlockEntity`, `ModSystem`). Consumers typically import both.

- `INetworkNode` - the block-entity-facing contract: connector faces, network type,
  open/leaking faces (`OnLeak`), state pushes.
- `BlockNetworkNode` - the `Block` base for self-orienting nodes (placement
  orientation, wrench rotation, variant-aware display names).
- `BlockEntityNetworkNode` - the `BlockEntity` base that registers/unregisters with
  the manager and persists state.
- `BlockNetwork` - the abstract live-network instance; `PipeNetwork` and `MoltenNetwork`
  are its concrete subclasses, in `exlib`.
- `BlockNetworkModSystem` - the graph manager; a mod registers a factory via
  `RegisterNetworkType("pipe", () => new PipeNetwork(mgr, new LpexChimneyVent()))`
  during `ModSystem.Start`.
- Content seams so the exlib networks never name a mod's block type: `IMoltenCell`
  (canal cells), `IBurstablePipe` (pipe burst rating), `IPipeVentStrategy` (chimney
  draw). Network tunables live in exlib's own `ExlibValues` config.

## Building

### Prerequisites

The repo bootstraps almost everything itself. You only need, up front:

- **Git** and **PowerShell 7+** (`pwsh`) on Windows, or **bash** on Linux/macOS.
- A **.NET 10 SDK** on PATH (see `global.json`) is recommended for normal `dotnet build`.
  If it (or any required runtime) is missing, the test runner downloads a self-contained
  .NET into `.dotnet/` and uses it — so a clone with no .NET at all can still run the tests.

Everything else is fetched on demand into gitignored folders:

- **Game binaries** → `.game/<version>` (`scripts/provision-game.*`).
- **.NET runtimes** the game versions need (net10/net8/net7, incl. the Windows Desktop
  runtime) → `.dotnet/` (`scripts/provision-dotnet.*`). Each Vintage Story version is pinned
  to one .NET major and won't roll forward, and the legacy test hosts need those runtimes —
  so a fork that only has .NET 10 still gets 8/7 provisioned automatically.

### Build

```sh
# Build/test: the dedicated-server archive (a plain zip/tarball - no installer) is enough.
# -Version takes a full patch (1.22.3) or a major.minor series (1.22 -> latest patch).
pwsh scripts/provision-game.ps1 -Version 1.22     # Windows
scripts/provision-game.sh       -Version 1.22     # Linux/macOS

dotnet build VintageStory.sln                                    # builds every mod + the tests
dotnet build mods/siex/src/HighPressureExpanded.csproj # or one mod + its dependencies
dotnet run --project infra/CakeBuild   # full Cake build: per-game-version release zips in dist/Releases/
```

### Testing

`dotnet test` runs the latest (1.22) suite. To run a version separately (the VS Code
"Test: …" tasks do the same), or all of them in parallel:

```sh
pwsh scripts/run-tests.ps1 -Version latest      # or 1.21 / 1.20 / all
scripts/run-tests.sh latest                     # Linux/macOS
pwsh scripts/run-tests.ps1 -Coverage            # latest + coverage gate (needs Python)
```

Each run builds the test projects for that version (auto-provisioning its game binaries
and, if missing, its .NET runtime) and executes the projects in parallel.

To actually **launch** the game (the GUI client) add `-Kind client`. On Windows the
client only ships as an Inno Setup installer, so this runs a silent install into
`.game/<version>`; on Linux/macOS it's a plain client tarball. Because every VS
installer shares one uninstall id, the Windows client install snapshots and restores
the existing "Vintage Story" Add/Remove-Programs entry so it never clobbers a
machine-wide install. If an earlier run already broke that entry, repoint it with:

```powershell
scripts/fix-vs-registry.ps1 -InstallDir "D:\Path\To\Vintagestory"
```

If you already have a game install you'd rather use, set the `VINTAGE_STORY`
environment variable to it and the build/launch will use that instead.

If you use VS Code, the included launch config provisions the client, builds, stages
the mods, and runs the game automatically - with all saves and configs written under a
single shared `.gamedata/` in the repo (both `.game/` and `.gamedata/` are gitignored).
The primary launch config tracks the latest patch of the current series; the legacy
(1.21 / 1.20) configs pin the series `.0` floor.
