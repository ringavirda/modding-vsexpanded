# Changelog - High Pressure Expanded (`hpex`)

## 0.1.0

Initial release. The high-pressure steam tier, split out of Low Pressure Expanded so the
low-tech chain (`iwex + lpex + smex`) stays complete without it.

- **Lancashire boiler** and **Cornish engine** moved here from `lpex` (codes
  `lpex:boilerlancashire-*` / `lpex:enginecornish-*` -> `hpex:*`). Placed machines in
  existing worlds are migrated automatically, including worlds that predate the
  `ppex -> lpex` rename.
- Their tunables moved into the mod's own `hpex` config section. The Lancashire's stat
  table, which carried the un-prefixed `Boiler*` names while it was `lpex`'s default
  boiler variant, is now `LancashireBoiler*` to match `CornishBoiler*`.
- The Cornish engine's grid recipe previously asked for `lpex:pipe-straight-*-steel`, a
  code the pipe-tier refactor removed along with the iron/steel material axis; it now
  takes the plain bolted `iwex:pipe-straight-*` segment like every other machine recipe.
- New handbook page **Steam Power: High Pressure**, carved out of the `lpex` boilers and
  engines pages.
