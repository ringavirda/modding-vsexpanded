# Package tidy and build plumbing - what ships in the package, what stays in the repo

> **For agentic workers:** execute task by task with a fresh implementer (`builder`) per task, in
> order; the gate in each task is the check. No review pass per task. Commits are the driver's
> (the main session), one per green task, short messages. Do not run `exmod format`; match the
> surrounding style by hand. Record each task in Progress at the bottom when its gate is green.

**Status** written 2026-09-06, not started. Step 2 of
[2026-09-06-repo-restructure.md](2026-09-06-repo-restructure.md): rulings B1, R1 (prepared in the
sample), E5, and the tidy named in its order of work. Runs after the module-system plan.

**Goal:** everything a consumer of the `ExpandedLib` package needs to build a mod ships inside the
package - the source generators, the game-path resolution, the provisioning hook, the asset globs,
the version stamping - so that a third party, the generated starter and the family repo in package
mode share one copy of the MSBuild plumbing. The repo keeps only what is repo-specific: the target
framework manifest, test-runner settings, pack metadata. exlib's own suite stops reading the
family's trees, the harness carries the checks each mod runs over itself, and the framework no
longer names its consumers' test assemblies.

**Why now:** the split (restructure step 5) makes the family a consumer. Anything the family gets
today by sharing a directory with exlib has to arrive through the package afterwards, and anything
exlib's suite checks in the family's trees stops being visible to it.

**Architecture:** `build/ExpandedLib.props` and `build/ExpandedLib.targets` under `mods/exlib/`
are packed into the `ExpandedLib` nupkg and NuGet imports them into every consuming project;
`mods/Directory.Build.props` imports the same two files from source, so source mode and package
mode run identical plumbing. Package versions live once in `Directory.Packages.props` (central
package management). The sample switches between a project reference and a package reference on
`$(ExlibRoot)`, which the monorepo sets and a standalone clone does not. Repo-wide guards become
harness checks parameterised by a mod's own tree.

**Tech stack:** MSBuild (SDK-style projects, .NET 10 SDK), NuGet pack conventions
(`build/<PackageId>.props|targets`, `analyzers/dotnet/cs`), xUnit through `ExpandedLib.Testing`.

## Traps that apply to every task

- MSBuild evaluates all properties first (props, then the project body, then targets), then all
  items. A property that depends on `$(TargetFramework)` or `$(AssetDomain)` (set in the project
  body) must be defined in a `.targets` file or an item group; defined in a `.props` file it is
  silently empty. `mods/Directory.Build.props` documents this in place.
- A package's `build/<PackageId>.props` is imported before the project body and
  `build/<PackageId>.targets` after it. `Directory.Build.props` is imported before both.
- A global property (`-p:Name=value`, including `-p:Name=`) cannot be overridden by any project
  property; that is what makes `-p:ExlibRoot=` force package mode.
- Restore needs `TargetFramework` before any package is present, so the target framework list can
  never come from the package. It stays in the consumer.
- `dotnet msbuild <proj> -getProperty:Name -getItem:Type` prints evaluated values without building;
  use it to prove a property lands, rather than reasoning about import order.
- Builds are serial (obj/ races); `bash scripts/exmod.sh test latest` takes about two minutes and
  runs five lanes; `test all` adds the legacy lanes and takes longer.
- `command grep` / `grep -F` for literal searches in the Bash tool.
- `PublicSurfaceTests` and `HarnessSurfaceTests` fail on a public type missing from the wiki's
  Supported-API and Testing-API-Reference pages; each task edits them for what it adds.
- Never `git stash`, `git reset` or `git checkout --` anything.

## Design

### The manifest, `exmod.json`

Introduced here so the harness can read a repo's layout; restructure step 3 makes exmod read the
rest of it. One file at the repo root:

```json
{
  "tools": "0.0.0",
  "series": ["1.22", "1.21", "1.20"],
  "mods": {
    "exlib": { "path": "mods/exlib" },
    "iiex":  { "path": "mods/iiex" },
    "siex":  { "path": "mods/siex" }
  },
  "samples": {
    "helloexpanded": { "path": "samples/HelloExpanded", "tests": "samples/HelloExpanded.Tests" },
    "hellomodule":   { "path": "samples/HelloModule",   "tests": "samples/HelloModule.Tests" }
  },
  "tests": ["infra/tools/ExlibVerify.Tests"]
}
```

Conventions fill in the rest: a mod's project is the one `.csproj` under `<path>/src/` (or
`<path>/` when there is none under `src/`), its tests are `<path>/tests/`, its assets `<path>/assets/`;
a sample's project sits at its path and its tests where `tests` says. `tools` is the pinned extools
tag (step 4), `series` the game series this repo builds for, current first. `tests` lists test
projects that belong to neither a mod nor a sample.

### What ships in `build/`

`build/ExpandedLib.props` (imported before the consumer's project body):

- Defaults, each guarded with `Condition="'$(X)' == ''"` so a repo's own props win:
  `CurrentGameTfm` net10.0, `CurrentGameVer` 1.22.0, `AllGameTfms` net7.0;net8.0;net10.0.
- The per-TFM rows (`GameVer`, `GameSlug`, `GameInstallEnv`) keyed on `_GameTfm`, which falls back
  to `CurrentGameTfm` when `TargetFramework` is not yet known - exactly today's manifest region
  minus the three defaults above. A single-target consumer therefore resolves the current row.

`build/ExpandedLib.targets` (imported after the body, sees `TargetFramework` and `AssetDomain`):

- `GamePath` resolution (env var named by `GameInstallEnv`, else `.game/<slug>` under the repo
  root), `GameVersion` stamping, the `GAME_GE_*` constants, `GameInstallEnv`/`GameSlug` assembly
  metadata, the legacy `NoWarn`s.
- The auto-provision target and the game-path check, invoking the wrapper found by walking up
  from the project to the first directory holding `scripts/exmod.sh` (property `ExmodWrapper`,
  overridable), or failing with the same message as today when there is none.
- Assembly identity and XML docs for a project with its own `modinfo.json`.
- The asset globs: `ModAssetsRoot` defaults to `$(MSBuildProjectDirectory)/../assets` when that
  directory exists and to `$(MSBuildProjectDirectory)/assets` otherwise; the `Content` glob with
  `Link` and the `AdditionalFiles` en.json feed for the lang generator, both guarded on
  `AssetDomain`. The sample's flat layout and the family's per-mod layout both resolve.
- `LegacyUsings.cs` (shipped in `build/`) linked into the compile on the legacy TFMs.
- The repo root for `.game/`: the first directory above the project holding `exmod.json`, a
  `.git` directory, or a `.sln`, in that order (property `ExmodRepoRoot`, overridable), so a
  standalone clone provisions into its own `.game/` and the workspace can point every repo at one.

Stays in the repo (`mods/Directory.Build.props`): the version manifest (the three defaults and the
`Legacy` switch), the imports of the two build files from source, the test-only settings
(runsettings, `xunit.runner.json`, the Legacy `Using` for `.Tests`/`.Testing`).
`mods/Directory.Build.targets` becomes an import of `build/ExpandedLib.targets`.

### Dual-mode references (R1), in the sample

```xml
<!-- HelloExpanded.csproj -->
<ItemGroup Condition="'$(ExlibRoot)' != ''">
  <ProjectReference Include="$(ExlibRoot)src/ExpandedLib.csproj" Private="false" />
  <ProjectReference Include="$(ExlibRoot)generators/ExpandedLib.Generators.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
<ItemGroup Condition="'$(ExlibRoot)' == ''">
  <PackageReference Include="ExpandedLib" ExcludeAssets="runtime" />
</ItemGroup>
```

`mods/Directory.Build.props` sets `ExlibRoot` to `$(MSBuildThisFileDirectory)exlib/` when it is
empty. The package carries the generators as analyzers and its `build/` files, so the package
branch needs nothing else. `HelloExpanded.Tests` references `ExpandedLib.Testing` the same two
ways (a test project keeps runtime assets). Versions come from `Directory.Packages.props`.

### Repo-wide guards become per-mod checks (E5)

The four exlib tests that walk every mod's tree become cores in `ExpandedLib.Testing.Checks`, each
a static class taking the roots it scans and returning findings, with the test-facing shape the
template already uses (a `[Fact]` that loops and asserts an empty offender list, so an empty corpus
passes rather than failing as an empty theory):

| Today | Core | Scans |
|---|---|---|
| `ShippedAssetJsonTests` | `ShippedJson` | one asset tree: every `.json` parses, no control characters, every patch entry declares its side, and the patch corpus is not empty when the tree has a `patches/` folder |
| `LoopingAnimationTests` | `LoopingAnimations` | one asset tree's `shapes/`: the wrap rule and the `rotShortestDistance` rule |
| `LangKeyResolutionTests` | `LangKeys` | one mod's source roots against one lang tree: every literal `Lang.Get("domain:key")` resolves |
| `LangParityTests` | `LangParity` | one lang tree: every locale carries the English key set and the same placeholders |

Thin tests call them per mod: exlib's suite over exlib's own tree and source, iiex's and siex's
suites over theirs (iiex's tree carries the `game` overlay domain and is checked with it), the
sample's suite over the sample's, and the template ships the same four files against
`YourModProject`. `RepoPaths` reads the layout from `exmod.json` when the file exists and keeps
today's `mods/<id>` convention when it does not; `AllAssetTrees()` and the harness cores that use
it (`DefinitionGoldens`, `HandbookSync`, `CodeLiterals`) are unchanged in behaviour.

### The consumer seams (retiring `InternalsVisibleTo`)

exlib's `InternalsVisibleTo.cs` names `IronIndustryExpanded.Tests` and `SteelIndustryExpanded.Tests`.
The list of internals those suites reach is derived, not guessed: remove the two attributes, build
the two test projects, collect the CS0122/CS0117 errors. Each member on the list becomes either a
documented harness hook in `ExpandedLib.Testing` (the preferred answer: a test-only switch such as
turning `ValidatePickRange` off belongs beside `TestPlayer`) or a public member hidden with
`[EditorBrowsable(Never)]` when it is an engine-facing seam a mod may legitimately reach. The
`ExpandedLib.Tests` attribute stays. The Industry assembly's own `InternalsVisibleTo` names only
`ExpandedLib.Tests` and stays.

---

### Task 1: central versions, shared pack metadata, solution filter

**Files:**
- Create: `Directory.Packages.props` (repo root), `mods/exlib/Directory.Build.props`,
  `exlib.slnf`, `exmod.json` (the manifest above, `tools` at `0.0.0` until step 4)
- Modify: every csproj with a `PackageReference` (the five test projects, the harness, the
  generators, `infra/tools/ExlibVerify*`, `samples/*`, `templates/exlib-tests/YourMod.Tests.csproj`):
  the `Version` attributes move to `Directory.Packages.props`; `mods/exlib/src/ExpandedLib.csproj`,
  `mods/exlib/industry/ExpandedLib.Industry.csproj`, `mods/exlib/testing/ExpandedLib.Testing.csproj`:
  the pack metadata that is identical across them moves up; `infra/tools/ExlibVerify/ExlibVerify.csproj`
  keeps its own (it leaves for the tools repo in step 3); `mods/Directory.Build.props` (one line:
  `ManagePackageVersionsCentrally` stays in the packages file, nothing to add here unless the SDK
  asks for it)

**Produces:** `$(ExlibVersion)` is not a property; the version is a `PackageVersion` row for
`ExpandedLib`, `ExpandedLib.Industry` and `ExpandedLib.Testing` in `Directory.Packages.props`, read
from `mods/exlib/src/modinfo.json` the same way the csprojs read it today, so a release still bumps
one number. `mods/exlib/Directory.Build.props` imports the file above it
(`$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))`) and
then sets, for the three packable projects: `Authors`, `RepositoryUrl`, `PackageProjectUrl`
(the wiki), `PackageLicenseExpression`, `PackageReadmeFile`, `PackageIcon` (`modicon.png` from
`src/`, packed to the package root), `PackageTags` (`vintagestory mod framework exlib`),
`IncludeSymbols` with `SymbolPackageFormat` `snupkg`, `PublishRepositoryUrl`, and the modinfo
version read. Each csproj keeps `PackageId`, `Description`, `IsPackable` and its own `None`
readme item. `exlib.slnf` lists the exlib projects, the generators, the harness, exlib's tests and
both samples with their tests, nothing from `mods/iiex`, `mods/siex` or `infra/`.

**Gate:** `bash scripts/exmod.sh build latest` warning-free; `bash scripts/exmod.sh test latest`
green; `bash scripts/exmod.sh nuget` produces four `.nupkg` and three `.snupkg`, and
`unzip -l dist/nuget/ExpandedLib.<ver>.nupkg` shows `README.md`, `modicon.png` and the `.xml` doc
beside the dll and nothing from `assets/`; `dotnet build exlib.slnf -clp:ErrorsOnly` succeeds.

### Task 2: the build plumbing ships in the package

**Files:**
- Create: `mods/exlib/build/ExpandedLib.props`, `mods/exlib/build/ExpandedLib.targets`,
  `mods/exlib/build/LegacyUsings.cs` (moved from `mods/LegacyUsings.cs`)
- Modify: `mods/Directory.Build.props` (shrinks to the manifest, the two imports, the test-only
  settings), `mods/Directory.Build.targets` (an import of the targets file), `mods/exlib/src/ExpandedLib.csproj`
  (packs `build/**` under `build/`; the `ExcludeAssetsFromPack` target keeps working), every mod
  csproj and both sample csprojs (nothing to change unless a HintPath or property moved - verify),
  `.github/workflows/tests.yml`, `tests-legacy.yml`, `release.yml` (only if a path changed),
  `docs/design/conventions.md` (the layout table gains `build/`)

**Consumes:** the metadata layout from Task 1.
**Produces:** the properties `ExmodRepoRoot`, `ExmodWrapper`, `ModAssetsRoot` (all overridable),
and the guarantee that a project importing only the two build files (no `mods/Directory.Build.props`)
resolves `GamePath`, provisions, stamps its version from `modinfo.json`, globs its assets and feeds
the lang generator.

Spec: move, do not rewrite. Every property, item and target in today's `mods/Directory.Build.props`
and `.targets` lands in one of the four places named in the Design section, with its comment; the
one new behaviour is the root and wrapper discovery. The `_AutoProvisionGame` target calls
`$(ExmodWrapper)`; when no wrapper is found, the `_CheckGamePath` error names the env var to set,
as today. Prove the plumbing with a project that does not import `mods/Directory.Build.props`:
a throwaway copy of `samples/HelloExpanded` under `/tmp` (not in the repo) whose csproj imports
`mods/exlib/build/ExpandedLib.props` at the top and `.targets` at the bottom by absolute path, then
`dotnet msbuild <copy>.csproj -getProperty:GamePath -getProperty:DefineConstants -getItem:Content
-getItem:AdditionalFiles` shows the game path under the repo's `.game/1.22`, `GAME_GE_1_22`, the
asset files linked under `assets/`, and the en.json feed. Delete the copy afterwards.

**Gate:** `build latest` warning-free; `bash scripts/exmod.sh build all` warning-free apart from the
pre-existing net7 MSB3277; `test latest` green; `test all` green; `exmod pack` produces the three
mod zips with `assets/<domain>/` inside and the legacy zips stamped with their game version;
`unzip -l dist/nuget/ExpandedLib.<ver>.nupkg` shows `build/ExpandedLib.props`,
`build/ExpandedLib.targets` and `build/LegacyUsings.cs`; the throwaway-project check above.

### Task 3: generators inside the package, the sample in dual mode

**Files:**
- Modify: `mods/exlib/src/ExpandedLib.csproj` (packs `ExpandedLib.Generators.dll` under
  `analyzers/dotnet/cs/` through a `None` item with `Pack="true"` that depends on the generator
  project having built - the existing analyzer `ProjectReference` orders it), `mods/exlib/build/ExpandedLib.targets`
  (nothing, the en.json feed is already there), `samples/HelloExpanded/HelloExpanded.csproj` and
  `samples/HelloExpanded.Tests/HelloExpanded.Tests.csproj` (the two conditioned item groups from the
  Design section), `samples/HelloModule/HelloModule.csproj` and `samples/HelloModule.Tests/HelloModule.Tests.csproj`
  (the same), `mods/Directory.Build.props` (`ExlibRoot` default), `templates/exlib-tests/YourMod.Tests.csproj`
  (package references only; the commented project-reference alternative goes),
  `mods/exlib/wiki/Getting-Started.md` ("Reference exlib at compile time" becomes the package
  reference; the `libs/exlib.dll` HintPath form goes), `mods/exlib/README.md` (Packages section:
  the package carries the generators and the build plumbing)

**Consumes:** Task 2's build files; Task 1's central versions.
**Produces:** a consumer that references only the `ExpandedLib` package gets the config and lang
generators, the plumbing, and IntelliSense docs; `ExlibRoot` is the one switch.

Spec: in package mode nothing references `ExpandedLib.Generators.csproj`; the analyzer arrives from
the package. `ExcludeAssets="runtime"` on the mod project's package reference keeps `exlib.dll` out
of the mod's output (the player installs exlib); the test project's reference is plain, since the
harness loads the dll at test time. The local-feed check is the gate: pack to `dist/nuget`, write a
throwaway `nuget.config` under `/tmp` naming `dist/nuget` and nuget.org as sources, and restore,
build and test `samples/HelloExpanded.Tests` with `-p:ExlibRoot=` and
`--configfile /tmp/<file>` so the sample resolves the freshly packed packages. The sample's own
`ModuleInit`, block and config code compile unchanged in both modes, which proves the generator
ran from the package.

**Gate:** `build latest` warning-free; `test latest` green (source mode); the package-mode
restore, build and test of `HelloExpanded.Tests` and `HelloModule.Tests` green against
`dist/nuget`; `git status` shows no leftover `nuget.config` or `/tmp` artefacts in the repo.

### Task 4: repo-wide guards become per-mod checks

**Files:**
- Create: `mods/exlib/testing/Checks/ShippedJson.cs`, `mods/exlib/testing/Checks/LoopingAnimations.cs`,
  `mods/exlib/testing/Checks/LangKeys.cs`, `mods/exlib/testing/Checks/LangParity.cs`,
  `mods/exlib/testing/Repo/RepoManifest.cs` (reads `exmod.json`: mods, samples, tests; absent file
  means the default layout), `mods/iiex/tests/Invariants/ShippedAssetJsonTests.cs`,
  `mods/iiex/tests/Invariants/LoopingAnimationTests.cs`, `mods/iiex/tests/Localization/LangKeyResolutionTests.cs`,
  `mods/iiex/tests/Localization/LangParityTests.cs`, the same four under `mods/siex/tests/`,
  `samples/HelloExpanded.Tests/Invariants/ShippedAssetJsonTests.cs` and `.../Localization/LangParityTests.cs`,
  `templates/exlib-tests/Localization/LangParityTests.cs`
- Modify: `mods/exlib/tests/Invariants/ShippedAssetJsonTests.cs`, `LoopingAnimationTests.cs`,
  `mods/exlib/tests/Localization/LangKeyResolutionTests.cs`, `LangParityTests.cs` (thin, exlib's
  own tree and source only), `mods/exlib/testing/Repo/RepoPaths.cs` (`Mod`, `Assets`, `Src`,
  `AllAssetTrees` through `RepoManifest`; `DomainToMod` seeded from the manifest's mods plus the
  `game` overlay rule, which the manifest states as `"overlays": { "game": "iiex" }` on the mod
  that ships it), `templates/exlib-tests/Invariants/ShippedAssetJsonTests.cs` (calls the core),
  `mods/exlib/wiki/Testing-API-Reference.md` and `Testing-Harness.md` (the four checks and
  `RepoManifest`), `docs/internal/testing.md`

**Consumes:** the manifest from Task 1.
**Produces:** `ShippedJson.Check(string assetTree)`, `LoopingAnimations.Check(string assetTree)`,
`LangKeys.Check(IEnumerable<string> sourceRoots, string langTree)`, `LangParity.Check(string
langTree)`, each returning `IReadOnlyList<string>` findings (empty means clean) and each with the
"corpus not empty" premise exposed as a second method the thin test asserts where the tree is
known to carry that kind of file; `RepoPaths.Src(string modId)`; `RepoManifest.Mods`, `.Samples`,
`.Tests`.

Spec: the cores keep every rule and every message of the tests they replace (the patch-side rule
including its server-only-category clause; the wrap rule with its threshold; the prefix-literal
exemption in the lang scan; the placeholder comparison). The exlib thin tests pass over exlib's
tree exactly as the old ones passed over everything, and the iiex and siex thin tests pass over
theirs - the corpus is the same files, split by owner. Count the theory cases before and after:
the sum over the three suites equals the old exlib count, and the log of the gate shows it.

**Gate:** `test latest` green with the counts reconciled in the task's Progress line; a deliberate
syntax error in a throwaway file under `mods/iiex/assets/iiex/patches/` fails `IronIndustryExpanded.Tests`
and not `ExpandedLib.Tests` (remove the file afterwards); `HarnessSurfaceTests` green.

### Task 5: the consumer seams

**Files:**
- Modify: `mods/exlib/src/InternalsVisibleTo.cs` (the two consumer entries go), the exlib types
  whose internal members the list names, `mods/exlib/testing/**` (the hooks), the iiex and siex tests
  that reached the internals (they call the hooks), `mods/exlib/wiki/Testing-API-Reference.md` and
  `Supported-API.md` (rows for what became public), `mods/exlib/CHANGELOG.md`

Spec: derive the list first (Design section) and put it in this task's Progress line before
changing anything. For each member: a test-only need becomes a harness hook next to the double
that needs it (`TestPlayer`, `MachineRig`, `StructureRig`, the network rigs); an engine-facing seam
becomes public with `[EditorBrowsable(EditorBrowsableState.Never)]` and a doc line saying what
reaches it. No member becomes public merely because a test used it.

**Gate:** `build latest` warning-free with the two attributes gone; `test latest` green;
`PublicSurfaceTests` green.

### Task 6: the prose

**Files:**
- Modify: `mods/exlib/README.md` (Building: package mode and source mode), `mods/exlib/wiki/Getting-Started.md`,
  `mods/exlib/wiki/Testing-Harness.md`, `docs/design/conventions.md` (`build/`, the manifest),
  `docs/internal/testing.md` (per-mod guards, the manifest), `CONTRIBUTING.md` (the manifest, the
  solution filter), `mods/exlib/CHANGELOG.md`, `docs/internal/README.md` (this plan's row),
  `docs/internal/worklog/2026-09.md` (one entry)

**Gate:** `test latest` green (the wiki parity, public-surface and harness-surface guards); the
link check in `exmod check` clean.

---

## Progress

(nothing yet)
