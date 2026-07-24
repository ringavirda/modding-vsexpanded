# High Pressure Expanded (`hpex`)

A [Vintage Story](https://www.vintagestory.at/) mod adding the high-pressure end of the
steam chain. It is the top of the *Expanded* mod family:
`exlib -> iwex -> lpex -> smex -> hpex`. Most players stay low-tech, so high pressure is
deliberately opt-in - a player who never installs `hpex` still has a complete
`iwex + lpex + smex` experience.

## What it adds

- **Lancashire boiler** - the heavy twin-flue boiler (48 L/s steam, chokes at 12 atm,
  1200 L vessel). Raised through right-click construction stages over a fire-brick
  firebox exactly like the Cornish boiler, and just as explosive when left
  over-pressured; it takes an iron-tier pickaxe.
- **Cornish engine** - the efficient high-pressure beam engine (6-8 atm). Its steam
  control rods are wrench-adjustable through Low / Normal / High, which raises both the
  steam it draws (8 / 16 / 32 L/s) and the pressure band it needs to engage. Drives any
  of the sub-machines from `lpex`/`smex` (MP generator, fluid pump, air blower).

There is **no separate HP network**: the pipe network is one live pressure pool, and "high
pressure" is simply steam carried above the low tier's band. The Lancashire's 12 atm
ceiling sits above the Cornish engine's 8 atm break point, so a pressure valve between
them is mandatory.

Gameplay numbers live in the `hpex` section of `ModConfig/ex_values.json` (see
`HpexConfig.cs`); build costs in the `hpex` section of `ModConfig/ex_recipes.json`,
switched with `/exmod recipes hpex <level>`.

## Code layout

Both machines are thin **leaves**: they inherit essentially everything from `lpex`'s
`BlockBoiler`/`BlockEntityBoiler` and `BlockEngine`/`BlockEntityEngine` and override only
their per-variant stat table, their construction stages and (for the engine) the throttle
interaction.

- `BlockStructures/Boiler/` - the Lancashire boiler block + block entity.
- `BlockStructures/Engine/` - the Cornish engine block + block entity.
- `Recipes/` - the two grid "frame" recipes, code-first.
- `BlockMigrations/` - the save migration that moves placed HP machines off their old
  `lpex:`/`ppex:` codes.
- `../../assets/hpex/` - shapes, lang, handbook page.

This mod deliberately registers **no network type** (the `pipe` network is owned by
`iwex`) and applies **no Harmony patches**.

## Building

Requires only the .NET SDK - provision the game binaries into the repo first (see the
[root README](../../README.md#building)), then build:

```sh
scripts/provision-game.sh -Version 1.22.0   # or scripts/provision-game.ps1 on Windows
dotnet build src/HighPressureExpanded/HighPressureExpanded.csproj
```
