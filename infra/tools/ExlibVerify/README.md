# exlib-verify

Checks a Vintage Story mod's shipped assets the way the game would load them, with no game running
and no test project.

```sh
dotnet tool install --global ExpandedLib.Verify
exlib-verify path/to/mod --game path/to/vintagestory
```

`path/to/mod` is a mod folder or a mod zip. `--game` is any Vintage Story install; the freely
downloadable dedicated-server archive is enough, so no game licence is needed. Add `--mods <dir>`
once per other mod that would be loaded alongside yours, so a patch aimed across mods resolves
against its real target.

What it checks:

- **Patches** are replayed exactly as the game's own patch loader would: the same `dependsOn` and
  `condition` filtering, the same file resolution including the `*` wildcard, applied against the
  real parsed target. A patch whose target does not exist is an error - unless that target's mod
  ships an assembly, in which case it may inject the file at load and the finding is informational,
  because this tool reads only what is on disk.
- **Recipe codes** resolve to a block or item that exists once every patch has been applied.
- **Handbook and lang coverage**: a key the assets reference and no locale defines.

Exit codes: `0` clean, `1` findings, `2` a usage or load failure. `--strict` makes informational
findings count; `--json` prints the findings as JSON for a CI step to read.

Part of [ExpandedLib](https://github.com/ringavirda/modding-vsexpanded). MIT licensed.
