# Changelog - Expanded Library (`exlib`)

All notable changes to this mod are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/), and the project follows
[Semantic Versioning](https://semver.org/). For changes before this file existed,
see the git history.

## [0.7.3] - 2026-08-13

Six public subsystems landed between 0.7.0 and 0.7.2 without a changelog entry; they are
recorded here together with 0.7.3's own packaging work.

### Added

- **Code-first definitions** (`ExBlockDef` / `ExItemDef` / `ExRecipeDef`). Blocks, items
  and recipes are authored in C# and injected as synthetic assets at `ExecuteOrder 0.04` -
  above the base index, below the JSON patch loader (0.05) and the object loader (0.2) - so
  vanilla variant expansion, the atlas, block-id assignment and other mods' JSON patches all
  still apply. Fluent builders with derived codes, an ASCII multiblock layout DSL validated
  at load, and type-safe class binding.
- **Process extension contract** (`StageLadderRegistry`, `ProcessJobRegistry`, `SpecSchema`,
  `ProcessExtensions`, `ItemDie`). Merged catalogues read from `config/stageladders/*.json`
  and `config/processjobs/*.json`, load-order-independent, with a versioned spec format:
  an absent `schema` reads as 1, an older one falls back, and a **newer one is refused** with
  an error naming both versions.
- **Metal, material-role, liquid and heat catalogues** (`MetalRegistry`, `MaterialRoleRegistry`,
  `ExLiquids`, `HeatBalance`), each backed by a JSON catalogue a dependent mod contributes to.
- **XML documentation now ships** (`exlib.xml`, beside the dll in every zip), so a consumer
  gets IntelliSense over the public surface instead of bare signatures.
- **Source generators are distributed** in the `exlib-testing` bundle under `analyzers/`.
  The config-accessor recipe in the wiki previously could not compile outside this repository.
- **`[assembly: ExDomain]`** declares the domain an assembly's registered classes are keyed
  under, so `Class<T>()` / `Behavior<T>()` resolve correctly across assemblies.
- **`[ExDefDomain]`** lets one assembly emit definitions into more than one domain.
- **`BlockPipe.Tier`** - a pipe's family, read from its `tier` variant, with
  `PlatedTier` / `CastTier` / `RolledTier` naming the three this project ships. A pipe that
  declares no tier takes the default rating, throughput and joint, which is what every
  fitting does.

### Changed

- ⛔ **Breaking: a pipe's tier is a variant, not its domain.** `RegisterBurst`,
  `RegisterThroughput` and `RegisterJoint` are keyed on the tier name rather than the mod id,
  and `BlockPipe.Segments` takes the tier as a second argument
  (`Segments(domain, tier)`; pass `null` for an untiered family). A mod may now ship several
  tiers under one domain, which one domain per tier made impossible. Callers registering with
  `Mod.Info.ModID` must pass their tier name instead - the registration is otherwise silently
  unused and every segment falls back to the defaults.
- ⛔ **Breaking: `BlockPipePassthrough.Passthroughs` takes the tier too**
  (`Passthroughs(domain, tier)`, `null` for an untiered family). Two tiers shipping a passthrough
  apiece carried one code between them, so under a single domain the later registration replaced
  the earlier with no error; the tier is what keeps both. The sheet texture is now selected by tier
  rather than by domain.
- **Assembly identity is real.** Every release previously shipped `AssemblyVersion` and
  `FileVersion` `1.0.0.0`; both are now read from the mod's own `modinfo.json`.
  `AssemblyVersion` stays `major.minor.0.0` so a patch does not break a dependent's binding.
- **Type-safe class binding works across assemblies.** `EntityRegistry.KeyFor` resolves the
  domain from the *type's* assembly rather than the caller's. Naming a class from a dependency
  previously produced a key nobody had registered - which compiles, fails at world load, and on
  the block half is not logged.

### Fixed

- **An unregistered network type no longer takes a world down.** `AddNode` runs inside chunk
  load; a mistyped `networkType` threw out of it. It now logs an error naming the block, the
  position, the requested type and the registered types, and adds no node.
- **The test harness runs outside this repository.** Its path helpers probed upward for a file
  literally named `VintageStory.sln` and threw everywhere else, which disabled the goldens, the
  block-code table and the handbook sync for any outside consumer. Any `.sln`/`.slnx`/`.git`
  now marks a root, overridable with `EXLIB_REPO_ROOT`.
- Nineteen XML doc references that pointed at nothing, surfaced by enabling the doc file.

## [0.7.0] - 2026-06-21

### Added

- **Orphaned block-entity healer.** A server-side system that recreates a block
  entity when a block is left in the world without one - e.g. a block entity
  discarded on chunk load (a load exception) or lost to a server desync, which
  otherwise leaves an inert, often unbreakable block. It runs automatically as
  chunks load (and once over already-loaded chunks at startup), scoped to block
  entities registered through the mod's attribute system so vanilla/other-mod
  entities are never touched.
- **`/exmod heal` command.** Sweeps the loaded chunks and recreates orphaned block
  entities on demand, for an operator who does not want to wait for the automatic
  on-load pass. Server-side, gated behind the `/exmod` root's `controlserver`.
- **Config framework.** A generic, versioned per-mod config store with
  source-generated value accessors, version-reset migrations, and legacy file-name
  renaming. Values can be marked manageable and edited live via
  `/exmod config <mod> [value] [new]` - applied immediately, no world reload.
- **Min/max range gates** on config values: out-of-range edits are rejected with a
  clear message.
- **Recipe-cost profiles.** A per-mod catalogue framework that rebalances grid and
  right-click-construction ingredient quantities, switchable with
  `/exmod recipes <mod> <level>`.
- **Content-gating helper** (`ExContentGate`) for hiding a block/item from creative
  and the handbook and removing its recipes - the framework behind smex's mold
  toggle.
- **Command framework.** Attribute-driven `[CommandRegister]` / `[SubCommandRegister]`
  registration under a shared `/exmod` (server) and `.exmod` (client) root, so
  dependent mods hang their own sub-commands off one root.
- **Production-machine base** (`BlockEntityProductionMachine`) and machine-port
  helpers, shared by engines, furnaces, converters and sub-machines.
- **Legacy support framework.** Shims and polyfills that let the family build and run
  against Vintage Story 1.21 and 1.20 alongside 1.22.
- **Russian and Ukrainian** translations.

### Changed

- **Internal reorganization** into `Blocks/{Networks,Structures,Machines,Migrations,Construction,Healing}`,
  `Registries/{Entities,Commands,Config,Preferences,Recipes}`, `Helpers`,
  `Renderers` and `Legacy`.
- **Registration attributes split.** The single `[EntityRegister]` became
  kind-specific `[BlockRegister]`, `[ItemRegister]`, `[BlockEntityRegister]`,
  `[BlockBehaviorRegister]`, `[BlockEntityBehaviorRegister]` and
  `[CollectibleBehaviorRegister]`, each validating that the class derives from the
  expected base type.
- **Right-click-construction salvage:** the ratio of materials dropped when a
  partially-built or finished structure is broken is now configurable.
- **Multiblock structures read live config changes** without a world reload.

### Fixed

- Right-click-constructable blocks ignored their last construction stage when
  computing dropped materials.
- Non-pipe network blocks could incorrectly burst.
- Block display-name ordering and assorted localization issues.
- `/exmod config` value display formatting.

## [0.6.0] - 2026-06-16

### Added

- **Command framework.** A shared `/exmod` (server) and `.exmod` (client) command
  root, with a server-side version and privilege handling, so dependent mods hang
  their sub-commands off one root.
- **Measurement helpers** (metric/imperial) and a **per-player preference registry**,
  with a handbook patch that converts displayed measurements to the player's units.
- **Network-highlight** subcommand and a **base surface renderer**.
- **Source generators** that bake block/item JSON attributes into generated class
  members.
- **Config migrations** from older versions.

### Changed

- The **structure filler** mirrors the principal block's block-info.

## [0.5.1] - 2026-06-14

### Changed

- The **migration system** now also covers items held in inventories, not just placed
  blocks.

### Fixed

- **Right-click-constructable** wildcard handling and the names shown for missing
  materials.

## [0.5.0] - 2026-06-13

The first standalone release of the shared library, extracted from Steelmaking
Expanded (internal `0.1.0` groundwork promoted to `0.5.0`).

### Added

- **Block-network framework** (nodes, connectors, graph) backing gas pipes and molten
  canals.
- **Multiblock structure framework** with right-click construction.
- **Attribute-driven registration** for blocks, items and behaviors.
- **World-migration system** for updating old blocks.
- Shared **particle, sound and orientation** catalogues and helpers.
