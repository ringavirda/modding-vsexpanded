# Changelog - Steelmaking Expanded (`smex`)

All notable changes to this mod are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/), and the project follows
[Semantic Versioning](https://semver.org/). For changes before this file existed,
see the git history.

## [0.9.2] - 2026-06-21

### Added

- **Bell hopper drops by default.** A freshly built blast furnace now feeds itself
  without the player first discovering the Ctrl + right-click toggle.
- **Chisel residue out of the Bessemer converter** when only a little hardened metal
  remains, instead of having to break the whole vessel; the cooldown coefficient is
  configurable.
- **Chisel molten canal taps and pedestals**, matching the canals and barrels.
- **Cooldown coefficients** for taps, barrels and pedestals (how fast their metal
  cools), and the cooldown speed now **syncs to the client** so the glow matches the
  server.
- **`/exmod molds <plate|ingot|rod|all> <on|off>`** to enable/disable the mod's tool
  molds (hidden from creative + handbook and their recipes removed when off).
- **Recipe-cost switching** for the steam/steel chains via the shared `/exmod recipes`
  command.
- **Orphaned machine blocks now self-heal** - a furnace door/tap/tuyere left without
  its block entity (a load failure or desync) gets a fresh one recreated on load
  (via the exlib healer), so it is interactable and breakable again rather than an
  inert ghost.
- **Russian and Ukrainian** translations.

### Changed

- **Molten chiselling generalized** into one shared behaviour across canals, barrels
  and the converter (consistent tool, sound, wear and recovery).
- **Converter (and boilers) no longer drop their base block when broken** - they
  scatter their build materials instead of dropping the whole mega-block.
- **Raised mining-tier / break-tool requirements** for mega-blocks.
- **Converter status strings** are more descriptive.
- Structures **read live config changes** without a world reload.

### Fixed

- The **Bessemer converter dropped itself** when broken.
- Corrected the **converter chisel cell position**.
- Fixed **engine animation direction** when powering mechanical power.
- The **molten canal tap** allowed placing barrels on structure filler blocks.
- Fixed **broken cowper-stove behaviour**.
- Fixed the **bell-hopper client/server inventory handshake** desync (grid clicks
  silently desyncing).
- Fixed **recipe item variants** and the **vanilla crushed-ore ratio** (EM compat).
- Fixed the **converter transmission recipe** rod requirements.
- Assorted localization issues.
