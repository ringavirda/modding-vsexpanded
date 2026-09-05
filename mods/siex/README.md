# Steel Industry Expanded (`siex`)

A [Vintage Story](https://www.vintagestory.at/) mod adding an industrial-era iron and
steel production chain on top of vanilla metalworking, together with the
high-pressure end of the steam chain that drives it. Requires
[Expanded Library](../exlib/README.md) (`exlib`) and
[Iron Industry Expanded](../iiex/README.md) (`iiex`).

## What it adds

- **Blast furnace** - a tall refractory multiblock charged through a hopper pair with
  alternating bands of burden (crushed ore fluxed with lime in the `iiex` burdenmaker)
  and coke. Fired and held above iron's melting point, it pools molten iron and slag.
- **Hot blast machinery** - cowper stoves that recycle furnace exhaust into scorching
  blast air, a smoke stack that vents the surplus, and a steam-driven air blower
  (a `iiex` engine sub-machine) that pressurises the line.
- **Bessemer converter** - stage II: a 3×3×3 vessel that takes mechanical power and a
  Blast line and blows molten iron into steel, poured back out through the same canals.
- **Slag chain** - solidified slag grinds into powdered slag, usable as mortar
  ingredient or phosphate fertilizer; scrap iron bits crush back into crushed iron.

Liquid metal is plumbed, not carried: it drains through taps into the molten canal
network. That network (canals, taps, mold pedestals, molten barrels) and the casting
chain (sand casting, iron molds) belong to `iiex` - siex consumes them, it does not
own them.

The in-game **handbook** ships five articles (overview, blast furnace, hot blast,
casting, Bessemer) with full build costs and operating procedures. Gameplay tunables
live in the `siex` section of `ModConfig/ex_values.json` (see `SmexConfig.cs`).

## Code layout

- `BlockStructures/` - the mega-block machines: blast furnace (with its hopper pair),
  cowper stove, smoke stack, Bessemer converter and the air-blower engine sub-machine.
- `Recipes/` - code-first grid and barrel recipes.
- `Generated/` - the emitted `SmexBlocks` block-code constants (drift-tested).
- `BlockMigrations/` - save migrations for renamed block codes and the removed
  ceramic molds.
- `../assets/siex/` - shapes, textures, patches, lang, config.

## Building

Requires only the .NET SDK - provision the game binaries into the repo first (see the
[root README](../../README.md#building)), then build:

```sh
scripts/provision-game.sh -Version 1.22.0   # or scripts/provision-game.ps1 on Windows
dotnet build mods/siex/src/SteelIndustryExpanded.csproj
```
