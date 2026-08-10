# The vanilla source — what is vendored and how to use it

The public Vintage Story source sits at `.compat/vintagestory/`, gitignored like the rest of
`.compat/`. It is a read-only reference: nothing in it is ever edited, and nothing in this repo
compiles against it. What the mods build against is still the provisioned game in `.game/<slug>/`.

Read this tree before reasoning about what the game does. It answers, exactly and at a line number,
questions that were previously settled by decompiling a DLL or by guessing from behaviour.

| | |
|---|---|
| [api-map.md](api-map.md) | where every type lives, per repo, with the members a mod actually overrides |
| [practices.md](practices.md) | patterns vanilla follows, and the traps its source reveals — ranked by relevance to us |
| [moddb-api.md](moddb-api.md) | the ModDB site source and its public REST API, with a curl cookbook |

## What is checked out

| Repo | Revision | Contents |
|---|---|---|
| `vsapi` | `324ccf9e` — v1.22.5 | The modding API. 634 `.cs`. `Common/Collectible/Block/` holds `Block`, `BlockEntity`, `BlockBehavior`; `Common/Entity/` holds **entities, not block entities** |
| `vssurvivalmod` | `dfaeb44` — v1.22.5 | The survival mod. 885 `.cs`. Every vanilla block, the mechanical-power system, recipes, the handbook |
| `vsessentialsmod` | `0cd7da3` — v1.22.5 | 310 `.cs`. Renderers, particles, animation, sound, lighting, entity AI |
| `vscreativemod` | `1c64971` — v1.22.0-rc.1 | 31 `.cs`. Schematics and worldedit |
| `vsmodexamples` | `2b07243` | The official example mods, one technique each |
| `vsmoddb` | `f73e881` | mods.vintagestory.at, in PHP. The authority on the public API |
| `VSdotnetModTemplates` | `934a4c9` | The canonical project layout and `modinfo.json` schema |
| `Tavis.JsonPatch` | `963f03e` | The JSON-patch library behind asset patching |
| `modpeek` | `5b94254` | Reads `modinfo.json` out of an uploaded zip/dll — what ModDB runs on upload |
| `Cairo`, `nanosvg` | `b5a93a4`, `754dbb0` | Native wrappers. Nothing a mod touches |

The checkouts track the current release, so they can be **newer than the version being built for**.
This repo targets 1.20/1.21/1.22 (`src/Directory.Build.props`), and an API present in the vendored
1.22.5 source may not exist on the legacy targets — check before using it.

## Two more references inside `vsapi`

Both are generated and shipped in the repo, and neither is obvious from the directory names:

- `vsapi/docs/api/` — the full generated API documentation, one HTML file per type. Faster than
  reading source when the question is only "what does this method take".
- `vsapi/docs/json-docs/` — the JSON **asset** schema documentation: what every field in a
  blocktype, itemtype, shape or recipe file means. This is the reference for asset authoring, and
  it does not exist anywhere else.

## How these pages were built

By reading the source, not by summarising documentation. Every claim in
[practices.md](practices.md) and [api-map.md](api-map.md) carries a `repo/path.cs:line` citation
into `.compat/vintagestory/`, so a claim that looks wrong can be checked in one step — and should
be, since line numbers drift as the checkouts are updated.
