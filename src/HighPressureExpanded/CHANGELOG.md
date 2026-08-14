# Changelog - High Pressure Expanded (`hpex`)

## 0.1.0

Initial release. The high-pressure steam tier, split out of Low Pressure Expanded so the
low-tech chain (`iiex + iiex + smex`) stays complete without it.

- **Lancashire boiler** and **Cornish engine** moved here from `iiex` (codes
  `iiex:boilerlancashire-*` / `iiex:enginecornish-*` -> `hpex:*`). Placed machines in
  existing worlds are migrated automatically, including worlds that predate the
  `ppex -> iiex` rename.
- Their tunables moved into the mod's own `hpex` config section. The Lancashire's stat
  table, which carried the un-prefixed `Boiler*` names while it was `iiex`'s default
  boiler variant, is now `LancashireBoiler*` to match `CornishBoiler*`.
- The Cornish engine's grid recipe previously asked for `iiex:pipe-straight-*-steel`, a
  code the pipe-tier refactor removed along with the iron/steel material axis; it now
  takes the plain bolted `iiex:pipe-straight-*` segment like every other machine recipe.
- New handbook page **Steam Power: High Pressure**, carved out of the `iiex` boilers and
  engines pages.
