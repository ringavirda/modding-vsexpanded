# Low Pressure Expanded (`lpex`)

A [Vintage Story](https://www.vintagestory.at/) mod adding the low-pressure steam tier
and the pipe fittings that make a network usable. It sits on
[Ironworking Expanded](../IronworkingExpanded/README.md) (which owns the base pipe block
and the `pipe`/`molten` networks) and is a hard dependency of
[Steelmaking Expanded](../SteelmakingExpanded/README.md) and
[High Pressure Expanded](../HighPressureExpanded/README.md).

## What it adds

- **Cast pipe tier** - the mid tier of the three-tier pipe system (iwex bolted, lpex
  cast, hpex rolled). Tier = mod, one material each, and the tier sets the burst
  pressure; the weakest segment caps a whole run.
- **Fittings** - hand valves (sever the line), directional pressure valves (overflow
  above a configurable gate), brick passthroughs/outlets (build structure walls across
  a pipe run; cap an outlet with a vanilla chimney to vent gas), fluid intakes
  (draw fresh water from a pond), and a steam condenser (the only place steam turns
  back into water).
- **Cornish boiler** - the compact entry boiler (32 L/s steam, 5 atm), raised through
  right-click construction stages over a fire-brick firebox, burning coal piles, and
  exploding if left over-pressured.
- **Watt engine** - the low-pressure beam engine (2-4 atm) plus the boiler/engine bases
  the high-pressure leaves in `hpex` inherit. Each engine drives one attached
  sub-machine:
  - **MP Generator** - constant-power axle drive for vanilla machines,
  - **Fluid Pump** - moves water into a pressurised output line (boiler feed),
  - **Air Blower** (from Steelmaking Expanded) - makes Blast for the furnace.

In-game **handbook articles** (`Steam Power: …`) cover build costs, operating steps and
failure modes; all gameplay numbers live in the `lpex` section of
`ModConfig/ex_values.json` (see `LpexConfig.cs`).

## Code layout

- `BlockNetworkPipe/` - the cast segment tier plus the valve/intake/passthrough/outlet/
  condenser fittings and their block entities. The base `BlockPipe`/`BlockEntityPipe` and
  the `PipeNetwork` registration live in `iwex`.
- `BlockStructures/` - boiler, engine and manual-pump mega-block machines (multiblock
  structure + right-click construction + animation), including the abstract bases the
  `hpex` leaves derive from.
- `Patches/` - Harmony patches into vanilla (chimney look-at info).
- `BlockMigrations/` - save migrations for renamed block codes.
- `../../assets/lpex/` - shapes, lang, handbook pages (blocktypes/items/recipes are
  authored in C# as code-first definitions).

Depends on [Expanded Library](../ExpandedLib/README.md) (`exlib`) for the block-network,
multiblock-structure, config, command and recipe-cost frameworks, and on
[Ironworking Expanded](../IronworkingExpanded/README.md) (`iwex`) for the pipe base block
and the `pipe` network type.

## Building

Requires only the .NET SDK - provision the game binaries into the repo first (see the
[root README](../../README.md#building)), then build:

```sh
scripts/provision-game.sh -Version 1.22.0   # or scripts/provision-game.ps1 on Windows
dotnet build src/LowPressureExpanded/LowPressureExpanded.csproj
```
