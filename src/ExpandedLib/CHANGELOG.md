# Changelog - Expanded Library (`exlib`)

All notable changes to this mod are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/), and the project follows
[Semantic Versioning](https://semver.org/). For changes before this file existed,
see the git history.

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
