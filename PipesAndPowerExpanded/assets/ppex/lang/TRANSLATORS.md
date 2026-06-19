# Notes for translators

Thank you for translating **Pipes & Power Expanded** / **Steelmaking Expanded** / **ExpandedLib**!
Please read this before editing the lang files - these mods do a few non-obvious things at runtime.

## The basics

- Add one file per language next to `en.json`, named by its language code: `ru.json`, `uk.json`,
  `de.json`, … (the same codes Vintage Story uses).
- **Translate values, never keys.** Every key must stay byte-for-byte identical to `en.json`.
  Keys ending in `*` (e.g. `block-pipe-straight*`) and keys starting with `game:` are normal -
  leave the key as-is, translate the value.
- Keep every format placeholder intact and in a sensible order: `{0}`, `{1}`, `{0:F0}`, `{0:F1}`.
  The number/format part after the colon (`:F0`) must not change.
- In handbook articles keep the markup: `<strong>`, `<i>`, `<br>`, `<br />`, and
  `<a href="handbooksearch://...">`. Translate the visible link text, but see the caveat below.

## `<hk>` is a hotkey tag - do not translate its contents

`<hk>rightmouse</hk>`, `<hk>ctrl</hk>`, `<hk>shift</hk>` are rendered by the game as the player's
actual key bindings. The text inside `<hk>…</hk>` is a **hotkey code**, not a word - leave it exactly
as in `en.json`. Putting anything that is not a real hotkey code inside `<hk>` makes the game draw a
literal `?`. Command strings like `.exmod measure` are written with `<strong>…</strong>`, never `<hk>`.

## Measurements - IMPORTANT

The simulation always runs in **metric**. Each player chooses how units are _displayed_ with
`.exmod measure metric|imperial`; this changes the display only, never the simulation.

The unit symbols live in the `unit-*` keys (in the **ppex** domain), and you may localize them:

| key                       | en      | what it is             |
| ------------------------- | ------- | ---------------------- |
| `unit-litres`             | `L`     | volume (metric)        |
| `unit-gallons`            | `gal`   | volume (imperial)      |
| `unit-litres-per-second`  | `L/s`   | flow (metric)          |
| `unit-gallons-per-second` | `gal/s` | flow (imperial)        |
| `unit-atm`                | `atm`   | pressure (metric)      |
| `unit-psi`                | `psi`   | pressure (imperial)    |
| `unit-celsius`            | `°C`    | temperature (metric)   |
| `unit-fahrenheit`         | `°F`    | temperature (imperial) |

- **Block-info / HUD / tooltip strings** receive their value already formatted (number + the
  localized unit) through a `{0}` placeholder, so you only translate the surrounding label - e.g.
  `"boiler-info-water": "Water: {0}"` => the `{0}` becomes `800 L` / `800 л` automatically.

- **Handbook prose converts itself.** When a player is in imperial mode, the mod rescans the article
  text and rewrites every metric value to imperial _on the fly_ (and re-renders when you switch units
  with `.exmod measure`). It looks for `<number> <metric-symbol>` where the symbol is **exactly** your
  `unit-litres`, `unit-litres-per-second`, `unit-atm` or `unit-celsius` value, then swaps in the
  imperial symbol.

  => **So in handbook text, always write metric values with the same symbol string you put in the
  `unit-*` keys.** If `unit-litres` is `л`, write `30 л` (not `30 L`, not `30 литров`). A value whose
  symbol does not match the `unit-*` key is simply left in metric for imperial players - it is not an
  error, just a missed conversion.
  - Ranges and lists are supported and must keep their separators, with the unit once at the end,
    exactly like English: `2-4 атм`, `160-220 °C`, `8 / 16 / 32 л/с`.
  - **Steelmaking Expanded handbook prose uses ppex's `unit-*` symbols too** (the conversion is
    shared). Keep the metric symbols in `smex` articles identical to your ppex `unit-*` values
    (`°C`, `atm`, `L`, `L/s` => your translations of them).

## handbooksearch links

`<a href="handbooksearch://blast furnace">…</a>` runs an in-game handbook _search_ for that text.
The targets in `en.json` are English search terms; in a translated game they may not jump to the
intended page. Translating the visible link text is safe. Adjusting the search target to match your
translated page/block titles is optional polish - leave it in English if unsure.
