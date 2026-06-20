# Changelog - Pipes and Power Expanded (`ppex`)

All notable changes to this mod are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/), and the project follows
[Semantic Versioning](https://semver.org/). For changes before this file existed,
see the git history.

## [0.6.3] - 2026-06-21

### Added

- **Manual boiler draining with buckets** - take water back out of a boiler by hand.
- **Localizable measurement units.** `.exmod measure` reports your display units and
  `.exmod measure metric` / `imperial` switches them (L/atm/°C vs gal/psi/°F); a
  display-only change, the simulation stays metric.
- **Recipe-cost levels** for ppex's construction recipes, switchable via the shared
  `/exmod recipes` command.
- **Russian and Ukrainian** translations.

### Changed

- **Boilers no longer have an upper boil limit** - water is gated on the way in, so
  the old hard cap was removed - and the boiler **water-draw speed is gated to
  10 L/s**, so it no longer gulps its whole intake buffer in one tick.
- An **open boiler lid drops pressure to 0 atm while idle**.
- **Molten chiselling generalized** into the shared behaviour (consistent
  tool/sound/recovery handling).
- **Boilers no longer drop their base block when broken** - a broken boiler scatters
  its build materials (custom salvage ratio) instead of dropping the whole mega-block.
- **Raised break-tool requirements** for mega-blocks.
- Machines **read live config changes** without a world reload.

### Fixed

- The **Watt engine** now displays its togglable pressure band correctly.
- The **Cornish engine** now correctly costs bricks to construct.
- Assorted **valve** issues.
- Network blocks that are not pipes could incorrectly **burst**.
- Right-click-constructable blocks ignored their **last construction stage** when
  computing dropped materials.
- **Handbook**: command strings displayed incorrectly, and measurement units did not
  refresh mid-session after a `.exmod measure` change.
- Block display-name ordering and assorted localization issues.
