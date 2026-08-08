# Changelog - Ironworking Expanded (`iwex`)

All notable changes to this mod are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/), and the project follows
[Semantic Versioning](https://semver.org/). For changes before this file existed,
see the git history.

## [Unreleased]

### Added

- **The iron-age furnace chain**: shaft (bloomery-to-blast) furnace, cupola, puddling
  furnace and heating furnace, all charged in layers from the top and blown from below.
  The burden descends against the rising hot gas (a counter-current model), so charge
  order, fuel carbon and blast supply all matter; a starved or chilled furnace stalls
  recoverably instead of vanishing its contents.
- **Burdenmaker** - a hopper-fed mixer that turns ore, flux and fuel into uniform
  burden charges, the furnace chain's standard feedstock.
- **Sand casting**: a moldable casting bed, reusable patterns that carry the mold
  shape, and cast-iron molds for stock, machine parts and gears.
- **Molten-metal network** - canals and cells that carry tapped iron per block, cool
  in place, glow while hot, and can be chiselled back out once hardened.
- **Mechanical-energy (MP) network** for cast-iron machines: flywheels store energy as
  spinning inertia (not a battery), transmissions gear it, and the rolling mill spends
  it forming stock through roll-set tooling.
- **Plated pipe tier** - the iron-age pipe family (plain and plated segments, bends,
  passthroughs) that the blast main and later steam tiers build on.
- **Twin-tub blower** - the iron tier's only air source: a mechanically driven pair of
  bellows that cold-blasts a furnace from an axle.
- **Firebox** - the shared solid-fuel firing block the furnace chain drafts through.
