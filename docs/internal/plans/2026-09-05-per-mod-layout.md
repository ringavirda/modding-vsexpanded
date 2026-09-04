# Per-mod repository layout

**Status** proposed 2026-09-05, awaiting the owner's go. Sequencing only; nothing here changes a
mechanic. Runs at a clean point: after the walkthrough blockers land and before Phase 2 of
[2026-09-04-roadmap.md](2026-09-04-roadmap.md), so the machining-line plan's paths are rewritten once.

**Goal** group everything a mod owns under one folder, the way `~/src/ari-home` groups an app or a
package: sources, tests, assets, docs and wiki together, with the cross-mod trees at the root.

## Target layout

```
mods/
  exlib/   src/ (ExpandedLib)  generators/  testing/ (the shipped harness)  tests/  assets/  docs/  wiki/  README.md  CHANGELOG.md
  iiex/    src/  tests/  assets/  docs/ (handbook, screenshots, moddb)  wiki/  README.md  CHANGELOG.md
  siex/    src/  tests/  assets/  docs/  wiki/  README.md  CHANGELOG.md
workbench/ editable shapes and textures (today assets/editable), layouts.md, machines.txt
infra/     CakeBuild (today dist/CakeBuild), scripts/tools (convert-shape, rolled-stock generator,
           coverage gate, released-codes, patch-api)
scripts/   exmod.ps1, exmod.sh only
docs/      design/ (cross-mod), internal/ (plans, worklog, research, vanilla), setups/
.github/   unchanged (GitHub requires the root)
dist/      gitignored release output only
```

`.game`, `.gamedata`, `.dotnet`, `.compat` stay at the root, gitignored, as today.

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
