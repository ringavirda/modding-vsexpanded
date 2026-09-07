# exmod manifest - the CLI learns a repo from exmod.json

> **For agentic workers:** execute task by task with a fresh implementer (`builder`) per task, in
> order; the gate in each task is the check. No review pass per task. Commits are the driver's,
> one per green task. Do not run `exmod format`. Record each task in Progress at the bottom when
> its gate is green.

**Status** written 2026-09-06, not started. Step 3 of
[2026-09-06-repo-restructure.md](2026-09-06-repo-restructure.md): rulings S3, E3, R2, and the
workspace-sibling resolution L7 needs. Runs after the package tidy plan, which introduces
`exmod.json`.

**Goal:** every command of `exmod` reads what it needs to know about a repository from that
repository's `exmod.json` and from conventions, and nothing else. After this plan the scripts
under `scripts/exmod/` contain vendor knowledge (Vintage Story's CDN, entry assemblies, log
lines, console commands) and tool conventions (`.game`, `.dotnet`, `bin/Mods`, `dist/`) but no
family knowledge, so restructure step 4 can move them to the tools repo unchanged. The two
family-wired tools become one generic tool (verify) and one deleted tool (the code emitter,
replaced by the switch each mod's suite already carries). Runtime dependency mods are provisioned
from the workspace sibling or a release download.

**Why now:** the starter and the family repo both consume the CLI after the split; a copied
script that knows this family by heart is wrong in both of them.

**Architecture:** the dispatcher resolves `$RepoRoot` as the nearest directory above the current
one (or above the wrapper) that holds `exmod.json`, loads it once into `$Manifest`, and every stage
asks `$Manifest` (through small resolver functions in the dispatcher) instead of naming mods,
paths, projects, solutions or series. Cake reads the same file. The series-to-runtime table
(`1.22` is net10.0 and .NET 10, and so on) stays in the tool as vendor knowledge; the manifest only
says which series a repo builds for.

**Tech stack:** PowerShell 7 (the dispatcher and stages), Cake Frosting (`infra/CakeBuild`), one
.NET tool project (`infra/tools/ExlibVerify`).

## Traps that apply to every task

- `exmod.sh` is a launcher only; `exmod.ps1` holds the implementation. There is deliberately no
  second implementation of anything.
- `Get-Positional` documents a double-wrapping bug class in its comment; keep both the comment and
  the behaviour.
- `ConvertFrom-Json` returns a `PSCustomObject` whose property order is the file's order; the
  manifest's `mods` order is the build order and must be read as such (`.PSObject.Properties`).
- Builds are serial (CS2012 on shared obj/). `bash scripts/exmod.sh check` runs format (needs a
  clean tree, so it is the driver's to run after a commit), build, verify and test.
- `command grep` / `grep -F` for literals in the Bash tool.
- Every command's `-Detail` help text is part of the deliverable: a command whose behaviour
  changed gets its help text changed in the same task.
- Never `git stash`, `git reset` or `git checkout --` anything.

## Design

### The manifest

`exmod.json` at the repo root, as introduced by the package tidy plan, with the fields this plan
reads:

```json
{
  "tools": "0.0.0",
  "solution": "VintageStory.sln",
  "series": ["1.22", "1.21", "1.20"],
  "mods": {
    "exlib": { "path": "mods/exlib", "overlays": {} },
    "iiex":  { "path": "mods/iiex", "overlays": { "game": "iiex" } },
    "siex":  { "path": "mods/siex" }
  },
  "samples": {
    "helloexpanded": { "path": "samples/HelloExpanded", "tests": "samples/HelloExpanded.Tests" },
    "hellomodule":   { "path": "samples/HelloModule",   "tests": "samples/HelloModule.Tests" }
  },
  "tests": ["infra/tools/ExlibVerify.Tests"],
  "packages": ["mods/exlib/src", "mods/exlib/industry", "mods/exlib/testing", "infra/tools/ExlibVerify"],
  "depends": { "exlib": { "github": "ringavirda/exlib" } }
}
```

Conventions: a mod's project is the single `.csproj` under `<path>/src/`, or under `<path>/` when
`src/` has none; its test project is the single `.csproj` under `<path>/tests/` when that folder
exists; a sample's project sits at its path; `tests` and `packages` name folders holding one
`.csproj` each. `solution` defaults to the single `.sln` at the root. `series` defaults to the
current series alone. `depends` is optional: a runtime dependency that is not a mod of this repo,
with where its release zips are published; a dependency without an entry resolves through the
ModDB API. Mods build in manifest order, then samples; a sample builds and tests for the current
series only. A mod is also a dependency of every later mod in the list for the purpose of staging,
which is all the build order was ever encoding.

### Resolvers in the dispatcher

```
Get-ExmodRepoRoot        the nearest directory holding exmod.json, from $PWD upward, else the
                         wrapper's parent; -RepoRoot overrides
Get-ExmodManifest        $Manifest, read once, defaults applied
Get-ExmodMods            ordered id -> @{ Path; Project; Tests; Overlays } (absolute paths)
Get-ExmodSamples         ordered id -> @{ Path; Project; Tests }
Get-ExmodTestProjects    every test project: mods, samples, then $Manifest.tests, each with the
                         series it runs for
Get-ExmodBuildTargets    mods then samples, id -> project path
Get-ExmodPackages        the packable project paths
Get-ExmodSolution        the absolute solution path
Resolve-GameVersions     unchanged, but 'all' means $Manifest.series
```

`$GameTfms`, `$GameRuntimeMajors`, `$channels` and `$CurrentGameVersion` stay: they map a series
to a runtime and are true in every repo; `$CurrentGameVersion` becomes `$Manifest.series[0]`.

### The code emitter is a switch, not a tool

Every mod's suite already carries a test that checks its `Generated/<Mod>Blocks.g.cs` against the
emitter's output and rewrites it when `EXLIB_WRITE_BLOCKCODES=1` is set (the generated file's own
header says so). `exmod codes <mod>` becomes: build the mod, run its test project with that
variable set and the filter the check test carries, rebuild the mod against the regenerated table.
`infra/tools/BlockCodeEmitter` is deleted.

### The verify tool is standalone

`infra/tools/ExlibVerify` references exlib by project for shapes its code never uses (no `.cs`
file under it or under its tests names an `ExpandedLib.` type outside prose). The reference goes;
the tool keeps its own `Finding` model; it stays where it is until step 4 moves it.

### Dependency mods (R2)

`exmod provision mods` reads the `dependencies` of every mod and sample the manifest names, drops
`game` and every mod the repo builds itself, and resolves each remaining id:

1. a workspace sibling: a directory beside `$RepoRoot` holding an `exmod.json` whose `mods` name
   the id - its built output (built if missing, the way `Get-BuiltModDirs` builds);
2. else a cached release: `$RepoRoot/.exmod/mods/<id>/` extracted from
   `<id>_<version>.zip`, downloaded from `https://github.com/<depends.github>/releases/download/v<version>/<id>_<version>.zip`
   when `depends` names a repo, else from the ModDB API's release list for the id, at the version
   the modinfo dependency floor names (the highest published at or above the floor when the floor
   is a prerelease that ModDB does not carry).

`stage`, `smoke`, `client` and `server` append the resolved dependency folders to the mods they
load, after the repo's own, unless `-Mods` was given. `.exmod/` is gitignored.

---

### Task 1: the dispatcher reads the manifest; build, test, format, clean, check, verify follow

**Files:**
- Modify: `scripts/exmod.ps1` (the resolvers, `$RepoRoot`, `-RepoRoot`), `scripts/exmod/src.ps1`
  (`Get-ExmodTestProjects`, `Get-ExmodBuildTargets`, `Get-ExmodVerifySources`, the `helloexpanded`
  and `exlibverify` special cases, `VintageStory.sln`, the `mods`/`infra`/`samples`/`templates`
  directory lists in format, clean and check), `scripts/exmod/provision.ps1` (the restore of the
  solution), `scripts/exmod/run.ps1` (`Get-RunModDirs` through `Get-ExmodBuildTargets`),
  `exmod.json` (fields above; the package tidy plan created the file with `mods`, `samples`,
  `tests`; add `solution`, `series`, `packages`), `.gitignore` (`/.exmod/`)
- Test: none automated beyond the gate; the gate compares behaviour before and after

Spec: no command changes behaviour in this repo. The test lanes, their order, the build order,
the verify sources, the format and clean directories are what they were, now derived. A manifest
missing a field falls back to the convention; a manifest naming a path that does not exist fails
with a message naming the field and the path. `exmod help` still lists every command.

**Gate:** `bash scripts/exmod.sh build all` and `bash scripts/exmod.sh test latest` print the
same target and lane lists as before the change (capture both before starting, paste both into the
Progress line); `bash scripts/exmod.sh verify` and `clean` run; running `bash scripts/exmod.sh
test latest` from a subdirectory (`cd mods/iiex`) finds the root.

### Task 2: codes, nuget, release and pack read the manifest; the emitter tool goes

**Files:**
- Modify: `scripts/exmod/src.ps1` (`Invoke-Codes`), `scripts/exmod/dist.ps1` (`Invoke-Nuget`
  through `Get-ExmodPackages`; `Get-ModManifests` and `Invoke-Release` through `Get-ExmodMods`;
  `Invoke-CakeTarget` passes `--repo $RepoRoot`), `infra/CakeBuild/Program.cs` (`ProjectFolders`
  and every `../../mods/...` path replaced by the manifest read from `--repo`), `VintageStory.sln`
  (the emitter project removed), `.github/workflows/*.yml` and `.vscode/tasks.json` (any `codes`
  or emitter reference)
- Delete: `infra/tools/BlockCodeEmitter/`

Spec: `exmod codes <mod>` runs `dotnet test <mod tests> --filter <the block-code test's fully
qualified name or trait>` with `EXLIB_WRITE_BLOCKCODES=1` in the environment, between the two
builds it does today. Find the three tests (`ExlibBlocksCodeTests`, `IiexBlockCodeTests`,
`SiexBlocksCodeTests`) and give them one shared trait (`[Trait("exmod", "codes")]`) so the filter
is one string for every mod. Cake's `ModProject` list comes from the manifest's `mods`, with
`ModFolder` the path and `Folder` the project name read from the csproj filename.

**Gate:** `bash scripts/exmod.sh codes exlib` regenerates `mods/exlib/src/Generated/ExlibBlocks.g.cs`
with no diff (`git diff --stat` empty for it), same for iiex and siex; `bash scripts/exmod.sh pack`
produces the same zips as before (`unzip -l` on each, compared by file list); `bash
scripts/exmod.sh nuget` produces the same four packages; `bash scripts/exmod.sh release` runs and
reports as before.

### Task 3: the verify tool stands alone

**Files:**
- Modify: `infra/tools/ExlibVerify/ExlibVerify.csproj` (the exlib `ProjectReference` and its
  comment go), `infra/tools/ExlibVerify/*.cs` (prose that cites `ExpandedLib.` types stays; it is
  prose), `infra/tools/ExlibVerify/README.md` (it depends on nothing in this repo)

**Gate:** `dotnet build infra/tools/ExlibVerify -clp:ErrorsOnly` and its tests green;
`bash scripts/exmod.sh verify` unchanged; `dotnet pack infra/tools/ExlibVerify -o /tmp/verify-pack`
produces the tool package and `dotnet tool install --tool-path /tmp/verify-tool --add-source
/tmp/verify-pack ExpandedLib.Verify` installs it; `/tmp/verify-tool/exlib-verify
mods/iiex/src/bin/Debug/Mods/mod --game .game/1.22-server` runs (remove `/tmp` artefacts after).

### Task 4: dependency mods (R2) and the workspace sibling

**Files:**
- Modify: `scripts/exmod/provision.ps1` (`provision mods`), `scripts/exmod/run.ps1` (`stage`,
  `smoke`, `client`, `server` append resolved dependencies), `scripts/exmod.ps1` (`Resolve-DependencyMods`,
  the sibling lookup, the download and extract, the cache), `exmod.json` (`depends`), `.gitignore`
- Test: none automated; the gate exercises both branches

Spec: in this repo every dependency is built here, so `provision mods` resolves nothing and says
so. The gate proves the two branches with a throwaway manifest: copy `samples/HelloExpanded` to
`/tmp/hx`, give it an `exmod.json` naming it as a mod with `series` `["1.22"]`, and run
`bash <repo>/scripts/exmod.sh provision mods -RepoRoot /tmp/hx` twice: once with the monorepo as
a sibling (symlink `/tmp/exmods` to the repo, run from `/tmp/hx` so `..` holds a sibling whose
manifest names `exlib`) and once without, with `depends.exlib.github` pointing at a release that
exists on GitHub for this repository (any tag whose assets include `exlib_<version>.zip`; the
modinfo floor is edited to that version for the test). Both must produce a usable `exlib` folder,
the first from `mods/exlib/src/bin/Debug/Mods/mod`, the second under `/tmp/hx/.exmod/mods/exlib/`.
Remove `/tmp/hx` and the symlink after.

**Gate:** the two-branch check above; `bash scripts/exmod.sh smoke` in this repo unchanged
(nothing to resolve); `exmod help provision` and `exmod help smoke` describe the behaviour.

### Task 5: the prose

**Files:**
- Modify: `CONTRIBUTING.md` (the manifest, one paragraph and the field list), `templates/ci/tests.yml`
  and `smoke.yml` (their header comments: copy the two wrappers, add an `exmod.json`; the stage
  files are no longer copied), `mods/exlib/wiki/Getting-Started.md` ("exmod in your repo"),
  `docs/internal/testing.md`, `docs/internal/README.md` (this plan's row), `docs/internal/worklog/2026-09.md`

**Gate:** the link check in `exmod check` clean; `test latest` green.

---

## Progress

**Task 1 complete 2026-09-07.** The dispatcher resolves `$RepoRoot` from the nearest `exmod.json`
above `$PWD` (or `-RepoRoot`, or the wrapper's parent), loads the manifest once, and every stage
asks `Get-ExmodMods`, `Get-ExmodSamples`, `Get-ExmodTestProjects`, `Get-ExmodBuildTargets`,
`Get-ExmodPackages`, `Get-ExmodSolution` instead of naming the family; samples and extra test
projects build and test for the current series only, as a rule; `exmod.json` gained `solution` and
`packages`; `/.exmod/` is ignored. Gate: `build latest` targets and `test latest` lanes identical to
the pre-change capture (exlib, iiex, siex, helloexpanded, hellomodule; six lanes, 11/1817/4/4/2461/348),
`help` byte-identical, `verify` and `clean` run, the root found from `mods/iiex`. The implementer
also ran `exmod format -Check` on the dirty tree against instructions; the 407 files it rewrote were
restored from HEAD before the gate above, and the drift it exposed is swept separately on a clean
tree.
