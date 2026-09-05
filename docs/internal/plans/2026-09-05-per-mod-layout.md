# Per-mod repository layout

**Status** done 2026-09-05. Sequencing only; nothing here changed a mechanic. Ran after the
casting-bed fix landed and before Phase 2 of [2026-09-04-roadmap.md](2026-09-04-roadmap.md), so the
machining-line plan's paths were rewritten once.

**Goal** group everything a mod owns under one folder, the way `~/src/ari-home` groups an app or a
package: sources, tests, assets, docs and wiki together, with the cross-mod trees at the root.

## Target layout

```
mods/
  exlib/   src/ (ExpandedLib)  generators/  testing/ (the shipped harness)  tests/  assets/  docs/  wiki/  README.md  CHANGELOG.md
  iiex/    src/  tests/  assets/  docs/ (handbook, screenshots, moddb)  wiki/  README.md  CHANGELOG.md
  siex/    src/  tests/  assets/  docs/  wiki/  README.md  CHANGELOG.md
workbench/ editable shapes and textures (today assets/editable), layouts.md, machines.txt
infra/     CakeBuild (today infra/CakeBuild), scripts/tools (convert-shape, rolled-stock generator,
           coverage gate, released-codes, patch-api)
scripts/   exmod.ps1, exmod.sh only
docs/      design/ (cross-mod), internal/ (plans, worklog, research, vanilla), setups/
.github/   unchanged (GitHub requires the root)
dist/      gitignored release output only
```

`.game`, `.gamedata`, `.dotnet`, `.compat` stay at the root, gitignored, as today.

## Execution mapping (2026-09-05)

Every path below is repo-relative. A row moves with plain `mv`; nothing is renamed inside a tree,
so namespaces, assembly names, project names, asset domains and codes are untouched.

| Today | After | Note |
|---|---|---|
| `src/ExpandedLib/` | `mods/exlib/src/` | csproj sits directly in `src/` |
| `src/ExpandedLib.Generators/` | `mods/exlib/generators/` | |
| `mods/exlib/testing/` | `mods/exlib/testing/` | the shipped harness |
| `test/ExpandedLib.Tests/` | `mods/exlib/tests/` | goldens ride along |
| `src/IronIndustryExpanded/` | `mods/iiex/src/` | |
| `test/IronIndustryExpanded.Tests/` | `mods/iiex/tests/` | |
| `src/SteelIndustryExpanded/` | `mods/siex/src/` | |
| `test/SteelIndustryExpanded.Tests/` | `mods/siex/tests/` | |
| `assets/exlib/` | `mods/exlib/assets/exlib/` | the packaged layout verbatim: a mod's `assets/` folder is exactly what its zip carries |
| `assets/iiex/` | `mods/iiex/assets/iiex/` | |
| `assets/game/` | `mods/iiex/assets/game/` | the vanilla lang overlay ships in iiex; being inside its tree replaces the opt-in property |
| `assets/siex/` | `mods/siex/assets/siex/` | |
| `assets/editable/{shapes,textures}` | `workbench/{shapes,textures}` | |
| `docs/internal/workbench/{layouts.md,machines.txt}` | `workbench/` | |
| `docs/wiki/` | `mods/exlib/wiki/` | the GitHub wiki source, modders |
| (new) | `mods/iiex/wiki/Home.md`, `mods/siex/wiki/Home.md` | one-paragraph stubs; the player wikis publish later (follow-up 1) |
| `docs/exlib/`, `docs/iiex/`, `docs/siex/` | `mods/<mod>/docs/` | handbook, screenshots, moddb pages, logos |
| `src/Directory.Build.props`, `src/Directory.Build.targets`, `src/LegacyUsings.cs` | `mods/` | one props for mods and tests (below) |
| `test/Directory.Build.props` | folded into `mods/Directory.Build.props` | test-only settings conditioned on the project name ending in `.Tests` |
| `test/parallel.runsettings`, `test/xunit.runner.json` | `infra/test/` | |
| `test/README.md` | `docs/internal/testing.md` | links inside it follow |
| `dist/CakeBuild/` | `infra/CakeBuild/` | `dist/` keeps only the gitignored `Releases/` |
| `scripts/tools/` | `infra/tools/` | `scripts/` keeps `exmod.ps1` and `exmod.sh` |
| `VintageStory.sln` | unchanged path | project paths rewritten; solution folders `exlib`, `iiex`, `siex`, `infra` replace `src` and `test` |
| `docs/design/`, `docs/internal/`, `docs/setups/`, `.github/`, `.vscode/`, root files | unchanged | contents get path rewrites only |

### Build system after the move

- `mods/Directory.Build.props` is today's `src/Directory.Build.props` with the test-only settings
  from `test/Directory.Build.props` appended under `Condition="$(MSBuildProjectName.EndsWith('.Tests'))"`
  (the runsettings path, the linked `xunit.runner.json`, the legacy `Using`). The relative hops
  `..\.game\` and `..\scripts\` are unchanged because `mods/` sits where `src/` sat.
- `mods/Directory.Build.targets` packs the whole `$(MSBuildProjectDirectory)\..\assets\**\*` tree
  with `Link="assets\%(RecursiveDir)%(Filename)%(Extension)"`. The absorbed-domain and
  `ShipGameLangOverride` machinery goes: the `game` overlay is packed because it lives inside iiex's
  tree, and no absorbed tree exists. `$(AssetDomain)` stays: it names the primary domain whose
  `..\assets\$(AssetDomain)\lang\en.json` feeds `ExLangKeyGenerator`.
- The harness gains `RepoPaths` (in `mods/exlib/testing/`): `Root` (walk up from the test assembly
  to the directory holding `VintageStory.sln`), `Assets(domain)` = `mods/<mod>/assets/<domain>` with
  the map exlib, iiex, siex to themselves and `game` to iiex, `Mod(id)` = `mods/<id>`. Every literal
  `assets/<domain>/...`, `src/...`, `test/...` and `docs/<mod>/...` in code and tests goes through
  it. The goldens keep resolving from the test project directory.
- CakeBuild's `ModProject` carries the mod folder as well as the project name; its source, publish,
  asset, modicon and modinfo paths and the harness package path follow the table; `../Releases`
  becomes `../../dist/Releases`.
- `wiki-sync.yml` triggers on and copies `mods/exlib/wiki/**`. The wiki parity guard follows.

### Gate (in this order, all green before the worklog entry)

1. `dotnet build VintageStory.sln -clp:ErrorsOnly` and `dotnet build VintageStory.sln -p:Legacy=true -clp:ErrorsOnly`.
2. `dotnet test VintageStory.sln --nologo -v q`: the same three Passed! lines as before the move
   (exlib 2019, iiex 2393, siex 324, or higher if the bed fix added tests).
3. `git status --short | grep -v '^??' | grep -v '^ D'` lists only files edited in place.
4. `dotnet run --project infra/CakeBuild -- --target=Package` (or the target the script names)
   produces the three zips under `dist/Releases/<ver>/` with `assets/<domain>/...` inside.
5. `python3 infra/tools/convert-shape.py --help` and the other tools start; `scripts/exmod.sh`
   subcommands that name paths run.
6. A path sweep: `git grep -n --untracked -I -E '(^|[^A-Za-z0-9_/])(src|test|assets)/' -- . ':!docs/internal/research' ':!docs/internal/worklog'`
   finds only prose or the packaged-layout `assets/<domain>` spelling.

### Follow-ups, not part of the move

1. Player wikis for iiex and siex as a GitHub Pages site built from `mods/*/wiki` (repo settings are
   the owner's).
2. The research snapshots keep their 2026-09-04 citations; `docs/internal/research/README.md` gets
   the root mapping.

## What the move touches (sized 2026-09-04)

| Area | Pinned today | Change |
|---|---|---|
| solution, csproj | 9 project paths, 12 project references | rewrite |
| `src/Directory.Build.props`, `test/Directory.Build.props` | asset globs assume `../assets/<domain>`; game path `../.game`; test props import the src props | one props at `mods/` with the asset root relative to each project; the test-only settings conditioned on the `.Tests` suffix; `.game` path recomputed |
| tests | 60 literal `assets/<domain>/...` paths in 33 files, 25 `src/` paths, 5 `test/` paths | a `RepoPaths.Assets(domain)` helper in the harness; the literals follow it |
| code | 24 `assets/` literals in 17 files | the same helper or the injected asset domain |
| tooling | convert-shape, rolled-stock generator, coverage gate, released-codes, CakeBuild's project list (12 hits), `.vscode/tasks.json` stage maps (15 hits), `tests.yml`, `tests-legacy.yml`, `wiki-sync.yml` | path edits |
| docs | zero design-page links into the moved trees; three `src/`/`assets/` links in all of `docs/`; 178 XML-comment citations of design pages stay valid | nothing structural; a note in `docs/internal/research/README.md` mapping old roots to new |

Guards that fail loudly on a missed path: shipped-asset guards, goldens (resolved from the solution
root), handbook sync, lang coverage, released-codes manifest, `Every_shape_reference_resolves_to_a_shipped_file`.

## Rules

- Plain `mv`, never `git mv`; the owner stages, and git detects the renames.
- One move, one worklog entry, the nine-target gate green before it is called done.
- The 2026-09-04 research snapshots keep their old citations; the index gets the root mapping.

## Decisions the layout forces

1. **Wikis.** GitHub gives one wiki per repository and `wiki-sync.yml` pushes `docs/wiki` into it.
   Recommended: the GitHub wiki stays exlib's (modders, `WikiParity` keeps guarding it); the player
   wikis for iiex and siex publish as a GitHub Pages site built from `mods/*/wiki`, linked from the
   ModDB pages.
2. **The `game` overlay.** `assets/game/lang/{en,ru,uk}.json` patch vanilla strings and belong to no
   mod; they move to whichever mod's zip ships them (check the packager).

## Not part of the move

Turbo-style task graphs (MSBuild orders builds already), `.env` files, co-located test folders inside
`src/` (the parallel tests folder is the .NET norm and the harness ships as its own package).
