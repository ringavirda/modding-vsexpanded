# Iron Industry Expanded (`iiex`)

A [Vintage Story](https://www.vintagestory.at/) mod covering the whole iron tier: cold-blast
ironmaking, the molten-metal and pipe networks it runs on, and the low-pressure steam plant
that mechanises it. It sits directly on [Expanded Library](../ExpandedLib/README.md) and is a
hard dependency of [Steelmaking Expanded](../SteelmakingExpanded/README.md) and
[High Pressure Expanded](../HighPressureExpanded/README.md).

Formed by merging Ironworking Expanded (`iiex`) and Low Pressure Expanded (`iiex`), which were
never separable in play: the iron line needs blast, and blast needs the pipes and engines. It
carries forward the released `ppex` version line.

## What it adds

- **The pipe system and both of its lower tiers.** `iiex` owns `BlockPipe`/`BlockEntityPipe`
  and registers the unified `pipe` network (one medium per network: a gas - air, blast,
  exhaust, steam - or water), the `molten` network and the `mpenergy` network. Its two tiers
  are **plated** (hammered from plate; the bootstrap rung a player plumbs the works with
  before steam exists) and **cast**; `hpex` adds the rolled tier. The weakest segment in a run
  sets its burst pressure and its throughput.
- **Fittings** - hand valves (sever the line), directional pressure valves (overflow above a
  configurable gate), brick passthroughs/outlets (build structure walls across a pipe run; cap
  an outlet with a vanilla chimney to vent gas), fluid intakes (draw fresh water from a pond),
  and a steam condenser (the only place steam turns back into water).
- **Cold blast furnace** - the iron-age furnace: charge preparation, tuyeres, a mechanical
  (waterwheel-driven) twin-tub blower, and molten pig tapped into canals. Plus the cupola,
  puddling and heating furnaces.
- **Molten-metal canals, taps, barrels and pedestals** - the per-cell molten flow system, plus
  sand casting.
- **Ore processing** - the burdenmaker, which blends crushed iron ore with lime flux into the
  graded burden the furnaces are charged with. Fuel is charged separately, as its own bands.
- **Cornish boiler** - the compact entry boiler (32 L/s steam, 5 atm), raised through
  right-click construction stages over a fire-brick firebox, burning coal piles, and exploding
  if left over-pressured.
- **Watt engine** - the low-pressure beam engine (2-4 atm) plus the boiler/engine bases the
  high-pressure leaves in `hpex` inherit. Each engine drives one attached sub-machine:
  - **MP Generator** - constant-power axle drive for vanilla machines,
  - **Fluid Pump** - moves water into a pressurised output line (boiler feed),
  - **Air Blower** (from Steelmaking Expanded) - makes Blast for the furnace.
- **Mechanical-energy (MP) network** for cast-iron machines: flywheels store energy as
  spinning inertia (not a battery), transmissions gear it, and the rolling mill spends it
  forming stock through roll-set tooling.
- **Design table** - the diagram-crafting station.

In-game **handbook articles** cover build costs, operating steps and failure modes; all
gameplay numbers live in the `iiex` section of `ModConfig/ex_values.json` (see `IiexConfig.cs`).

## Code layout

- `BlockNetworkPipe/` - the two segment tiers, the shared segment definition factory, the
  generic chimney-vent strategy, and the valve/intake/passthrough/outlet/condenser fittings
  with their block entities. The base `BlockPipe`/`BlockEntityPipe` live in `exlib`.
- `BlockNetworkMolten/` - the molten-metal network, canals, taps, barrels, pedestals.
- `BlockNetworkEnergy/` - shafting, flywheels and the gear transmissions.
- `BlockStructures/` - the furnace, ore-processing, casting, forming, boiler, engine and
  manual-pump mega-block machines (multiblock structure + right-click construction +
  animation), including the abstract bases the `hpex` leaves derive from.
- `Recipes/` - code-first grid and smithing recipes.
- `Items/` - the mod's items.
- `Patches/` - Harmony patches into vanilla (anvil pig breaking, filled-mold rack spill,
  tool-mold heat gate, chimney look-at info).
- `BlockMigrations/` - save migrations for renamed block codes.
- `Compat/` - other mods' iron-ore registrations for the furnace hoppers.
- `../../assets/iiex/` - shapes, textures, lang, handbook pages (blocktypes/items/recipes are
  authored in C# as code-first definitions). This mod is also the one that ships the shared
  `assets/game/` vanilla lang override.

Depends on [Expanded Library](../ExpandedLib/README.md) (`exlib`) for the block-network,
multiblock-structure, config, command and recipe-cost frameworks.

## Building

Requires only the .NET SDK - provision the game binaries into the repo first (see the
[root README](../../README.md#building)), then build:

```sh
scripts/provision-game.sh -Version 1.22.0   # or scripts/provision-game.ps1 on Windows
dotnet build src/IronIndustryExpanded/IronIndustryExpanded.csproj
```
