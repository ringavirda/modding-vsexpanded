# Notes for translators

Thank you for translating **ExpandedLib** (the shared library for Pipes & Power Expanded and
Steelmaking Expanded)!

## The basics

- Add one file per language next to `en.json`, named by its language code: `ru.json`, `uk.json`,
  `de.json`, … (the same codes Vintage Story uses).
- **Translate values, never keys.** Every key must stay byte-for-byte identical to `en.json`.
  Keys starting with `game:` are normal - leave the key as-is, translate the value.
- Keep every format placeholder intact: `{0}`, `{1}`, `{2}`. For example
  `"command-pref-set": "{0} set to {1}."` - keep both `{0}` and `{1}`.
- The command descriptions mention literal command names like `.exmod` and `.help exmod` and
  sub-command names like `network hi`. **Do not translate command names** - they are typed by the
  player exactly as written.

## Measurements / handbook (ppex & smex)

ExpandedLib itself has no units or handbook articles. The companion mods **Pipes & Power Expanded**
and **Steelmaking Expanded** do, and they convert measurements and rewrite handbook text at runtime.
If you translate those mods, read the `TRANSLATORS.md` shipped in their `lang/` folders first - it
explains the `unit-*` keys, the metric=>imperial auto-conversion, and the `<hk>` hotkey tag.
