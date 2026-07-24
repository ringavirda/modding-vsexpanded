# Ironworking Expanded (`iwex`)

A [Vintage Story](https://www.vintagestory.at/) mod adding cold-blast ironmaking - the
foundational tier of the *Expanded* mod family. It sits directly on
[Expanded Library](../ExpandedLib/README.md) and is a hard dependency of
[Low Pressure Expanded](../LowPressureExpanded/README.md) and
[Steelmaking Expanded](../SteelmakingExpanded/README.md).

## What it adds

- **The base pipe tier + both networks.** `iwex` owns `BlockPipe`/`BlockEntityPipe` and
  registers the unified `pipe` network (one medium per network: a gas - air, blast,
  exhaust, steam - or water) and the `molten` network. Its own tier is the plain
  **bolted** segment (straight, bend, T/X junction); `lpex` adds the cast tier and the
  fittings, `hpex` the rolled tier. The weakest segment in a run sets its burst pressure.
- **Cold blast furnace** - the iron-age furnace: charge preparation, tuyeres, a
  mechanical (waterwheel-driven) twin-tub blower, and molten pig tapped into canals.
- **Molten-metal canals, taps, barrels and pedestals** - the per-cell molten flow system,
  plus sand casting.
- **Ore processing** - the ore bunker and ore mixer that prepare the graded burden the
  furnaces are charged with.
- **Design table** - the diagram-crafting station.

Gameplay numbers live in the `iwex` section of `ModConfig/ex_values.json` (see
`IwexConfig.cs`).

## Code layout

- `BlockNetworkPipe/` - the base pipe block/BE, the shared segment definition factory and
  the generic chimney-vent strategy.
- `BlockNetworkMolten/` - the molten-metal network, canals, taps, barrels, pedestals.
- `BlockStructures/` - the furnace, ore-processing and casting mega-block machines
  (multiblock structure + right-click construction + animation).
- `Recipes/` - code-first grid and smithing recipes.
- `Items/` - the mod's items.
- `Patches/` - Harmony patches into vanilla (coal-pile blast mix, anvil work).
- `BlockMigrations/` - save migrations for renamed block codes.
- `Compat/` - other mods' iron-ore registrations for the furnace hoppers.
- `../../assets/iwex/` - shapes, textures, lang, handbook pages. This mod is also the one
  that ships the shared `assets/game/` vanilla lang override.

## Building

Requires only the .NET SDK - provision the game binaries into the repo first (see the
[root README](../../README.md#building)), then build:

```sh
scripts/provision-game.sh -Version 1.22.0   # or scripts/provision-game.ps1 on Windows
dotnet build src/IronworkingExpanded/IronworkingExpanded.csproj
```
