# Repository restructure - the plan of record

**Status** ruled 2026-09-06 and revised the same evening after the owner's second and third
rounds of rulings (repo names `extools` and `exmod-starter`; the module system reframed as exlib's
extension mechanism, E6); not started. The owner executes this from a session rooted **outside** this folder, at
the parent workspace, because it moves the folder itself. The revision replaced E4 (the split is
unconditional), L6 (tools by wrapper and pin, not submodule), S1 (the starter is generated) and
the order of work (manifest and tools before the starter, module fixes first), and added E5, E6,
B1, R1, R2 and P8. Where a ruling reverses an earlier one, the reversal is noted in place.

**Read this first, and read it whole.** A session started outside `~/src/modding-vsexpanded` loads
none of this project's assistant memory - that store is keyed by working directory - so this document
is the only carrier of the decisions below. Nothing here is inferable from the code.

**Goal:** the Expanded family becomes several repositories under one parent, so exlib can be a
framework other people build on, the mods can be contributed to without cloning the framework, and
the plans stop living in a public repo.

---

## The rulings

Each was decided with the owner on 2026-09-06.

### Packaging and publishing

**P1. Four package ids, versioned in lockstep.** `ExpandedLib`, `ExpandedLib.Industry` and
`ExpandedLib.Testing` read their version from the core project's `modinfo.json` and publish from
the exlib repo; `ExpandedLib.Verify` publishes from the tools repo once E3 has moved it. All four
IDs were unclaimed on nuget.org on 2026-09-06.

**P2. Packages target the current game version only.** 1.20 and 1.21 concern about ten players and
already receive mod zips from the GitHub releases page. The legacy lanes stay in the repos and run
locally in source mode (L7); they are not part of what ships on NuGet.

**P3. The test harness ships.** `ExpandedLib.Testing` is published like the rest. Its API does
still move between releases; consumers pin.

**P4. Publishing goes through NuGet trusted publishing, never a stored key.** exlib's own
`release.yml` requests an OIDC token (`permissions: id-token: write`), exchanges it through
`NuGet/login@v1` for a key valid one hour, and pushes. It is **off** until the repository variable
`NUGET_USER` holds the nuget.org profile name. The policy is created at nuget.org under Trusted
Publishing for the exlib repository (owner `ringavirda`, workflow file `release.yml`, no
environment, scope "Push new packages and package versions", glob `ExpandedLib*`); a policy is
per repository and per workflow file, so the tools repo gets its own for `ExpandedLib.Verify`.
The split itself is what decouples the package release from the mod release: exlib tags its own
versions and never waits for iiex.

**P5. Three metadata surfaces, no overlap.** NuGet reads `PackageTags`, `PackageProjectUrl`,
`PackageIcon` and the symbol package; ModDB reads `modinfo.json` plus its own web form and the
`modicon.png` inside the zip; GitHub reads its own repository settings. Package tags, project URL,
icon and `.snupkg` are still missing and are NuGet-only work. `RepositoryUrl` moves to the exlib
repo's URL.

**P6. iiex publishes to ModDB as 0.7.0.** Its `modinfo.json` is right and its changelog is behind:
the `[Unreleased]` section becomes `## [0.7.0]` when the release is cut. `exmod release` fails on the
mismatch until then, deliberately.

**P7. The obsolete table assumes exlib's next release is 0.8.0.** `Supported-API.md` lists members
deprecated "since 0.8.0, removed in 0.9.0"; exlib is at 0.7.3. If the next exlib release is numbered
anything other than 0.8.0, that table has to move with it.

**P8. Prereleases are how the family consumes unreleased exlib.** A tag such as `v0.8.0-preview.1`
in the exlib repo publishes a GitHub prerelease with the mod zip and pushes prerelease packages;
nothing prerelease goes to ModDB. exmods pins one exlib version in `Directory.Packages.props`, and
a guard keeps each mod's `modinfo.json` exlib dependency floor equal to that pin.

### Repository layout

**L1. Sibling repositories under one parent workspace.**

```
~/src/modding-vsex/            the workspace: a private git repo with a Pi remote
  exlib/                       framework repo (GitHub ringavirda/exlib): the exlib mod as N packages
  exmods/                      the family repo (GitHub keeps modding-vsexpanded): iiex, siex,
                               workbench, family design docs, the 0.9-support branch, the old tags
  extools/                     the tools repo (GitHub ringavirda/extools): the exmod CLI, generic tools
  starter/                     generated (GitHub ringavirda/exmod-starter)
  wiki/                        the web app; renders content pulled from exlib and exmods
  docs/                        plans, worklog, research, vanilla notes (today docs/internal)
  .game/  .dotnet/  .compat/   shared game installs, toolchain and vendored sources
  Directory.Build.props        the workspace switch (L2)
```

The family folder's local name is the owner's choice and nothing below depends on it. Every
new GitHub repository is created private (owner ruling 2026-09-06) and made public by the owner
when ready; the trusted-publishing policy and the wrapper clone work either way. The exlib
repo is the mod: one modinfo, one zip, N assemblies,
one folder per shipped package so that a fifth module is a fifth folder:

```
exlib/
  src/ExpandedLib/             core: the csproj, modinfo.json, modicon.png   (today mods/exlib/src)
  src/ExpandedLib.Industry/                                                  (today mods/exlib/industry)
  src/ExpandedLib.Testing/                                                   (today mods/exlib/testing)
  src/ExpandedLib.Generators/                                                (today mods/exlib/generators)
  tests/ExpandedLib.Tests/     splits per module when a module needs it      (today mods/exlib/tests)
  build/                       the MSBuild plumbing that ships inside the ExpandedLib package (B1)
  assets/exlib/  wiki/  docs/  samples/HelloExpanded/  samples/HelloExpanded.Tests/  templates/
  Directory.Build.props  Directory.Build.targets  Directory.Packages.props  exmod.json
  scripts/exmod.sh  scripts/exmod.ps1                                        (the wrapper, L6)
  .github/workflows/           tests, tests-legacy, release (zip + NuGet), wiki-sync
```

exmods keeps the per-mod layout of `2026-09-05-per-mod-layout.md` minus `mods/exlib`, `samples`
and `templates`; `infra/tools` keeps the family tools (shape converter, rolled-stock generator,
released-codes derivation). Keeping today's folder names inside exlib is the fallback if the
rename to `src/<Package>` proves noisy; the history follows either way (see Traps).

**L2. The parent is a private git repo with a Pi remote, the children are gitignored, and the
parent's `Directory.Build.props` is the workspace switch.** It sets `ExlibRoot` (and `ExmodRoot`);
each child repo's root `Directory.Build.props` imports the file above it when one exists, through
`GetPathOfFileAbove`. A standalone clone has nothing above it and builds in package mode (R1). The
parent also holds the plans, worklog and research, which today sit in a public repository despite
being candid working notes. Submodule pins would be paid for on every commit touching two repos
and collected rarely; tags in each repo plus a dated line in the worklog answer the same question.

**L3. Design documents go where the code that cites them goes.** exlib code carries 37 XML-comment
citations of seven pages under `docs/design/mechanics/` (process-extension,
framework-composition, pipe-network, orientation-schemes, multiblock, mp-energy, molten-network);
those pages move to the exlib repo's `docs/`. Every other design page is family content and stays
in exmods. `docs/design/conventions.md` splits the same way: the "How exlib is laid out" and
framework rules go with exlib, the family rules stay, and `CONTRIBUTING.md` exists in both repos.

**L4. Wiki content stays beside the code it documents.** Three tests read the exlib wiki
(`PublicSurfaceTests`, `HarnessSurfaceTests`, `WikiParityTests`) and fail when a page documents a
symbol that no longer exists; they move with it. The web app is a separate repo that renders from
the source repos at build or deploy time.

**L5. Shared game installs, toolchain and vendored sources, not four copies.** `VINTAGE_STORY`,
`VINTAGE_STORY_121` and `VINTAGE_STORY_120` are already honoured everywhere, so one `.game`, one
`.dotnet` and one `.compat` under the parent serve every repo; `.compat` is gitignored today and both
framework and family work read it.

**L6. The tools repo is consumed through a checked-in wrapper and a version pin, not a submodule.**
Reverses the earlier ruling in this document. A submodule would cost one commit in the tools repo
plus a pin bump in every consuming repo for each exmod edit, `submodules: true` in every CI
checkout, and stale content on branch switches; the wrapper answers the clone failure the same way
without that tax. Each consuming repo checks in `scripts/exmod.sh`, `scripts/exmod.ps1` and a
pinned tools version (a line in `exmod.json`). The wrapper resolves the tools in this order:
`EXTOOLS_HOME`; the workspace sibling `../extools`; a local clone of the pinned tag into
`.extools/` (gitignored), made on first use. Third-party repos and the starter get the identical wrapper.

**L7. Every repo must run standalone, in package mode.** A contributor clones exmods alone:
`dotnet restore` pulls `ExpandedLib`, `ExpandedLib.Industry` and `ExpandedLib.Testing` from NuGet,
`exmod provision game` fetches the server archive (no licence needed), `exmod provision mods`
fetches the exlib mod zip (R2), and build, test and smoke work. Legacy lanes exist only in source
mode: exlib's CI keeps them (they prove its shims), exmods' CI runs the current lane only, and the
family's legacy builds happen in the workspace.

### exlib itself

**E1. The framework and the family layer are separate assemblies and packages.** Landed 2026-09-06;
see [2026-09-06-exlib-industry-split.md](2026-09-06-exlib-industry-split.md).

**E2. Only one dll per mod folder may contain mod systems.** Any number within one assembly is fine.
A second dll that declares one makes the game refuse the whole mod. exlib's answer is `IExModule` +
`ExModules`; see the wiki's Registries page, "Shipping more than one assembly". The loader lives in
`VintagestoryLib`, shipped as a dll only, so this is not discoverable from the vendored sources.

**E3. `exlib-verify` and the block-code emitter become generic and move to the tools repo.**
`exlib-verify` currently takes a `ProjectReference` on exlib for the check shapes and must grow its
own finding model (it already has `Finding.cs`) and drop that dependency. `BlockCodeEmitter`
references exlib, the harness, iiex and siex by project; it becomes a tool that loads a built mod
dll and its asset tree by path, so `exmod codes <mod>` works in any repo.

**E4. exlib is split from the family, unconditionally, as step 5.** Reverses the earlier
"until the tax is worth paying". The owner's reasons: exlib is already modular (core, industry,
testing) and may gain more modules; a physically separate repo, with the family consuming
published packages, is what stops the family from changing the public surface casually once it is
public; and the framework gets its own GitHub presence - issues, wiki, releases - separate from
mod content. The daily proving loop survives through the workspace: in source mode the family
references exlib by project and `exmod test all` still proves both in one command; exmods' CI
runs in package mode and is the physical stop.

**E5. Family invariants leave exlib's suite.** Eight exlib files read iiex or siex trees today:
`ShippedAssetJsonTests`, `LoopingAnimationTests`, `LangKeyResolutionTests`, `LangParityTests` in
the tests, and `CodeLiterals`, `DefinitionGoldens`, `HandbookSync` plus `RepoPaths` in the harness.
Each becomes a harness check that a mod's own suite runs over its own tree, the way the template's
`ShippedAssetJsonTests` already does, so exlib's suite covers exlib and the sample only.
`ReleasedCodes.cs` is the family's release history and leaves the harness; the harness keeps the
mechanism (`ReleasedVersions`, the referenced-codes guard) and reads a per-repo manifest.
`RepoPaths` already roots on the first `.sln`, `.slnx` or `.git` above the test binary; what it
hardcodes is the `mods/<mod>/assets/<domain>` table, which becomes the layout named in the repo's
`exmod.json`. The consumer `InternalsVisibleTo` entries in exlib
(`IronIndustryExpanded.Tests`, `SteelIndustryExpanded.Tests`) are retired by promoting the seams
they drive into documented harness hooks.

**E6. The module system is exlib's extension mechanism, and it is finished before the first
publish.** Reframed by the owner: a module is an assembly that extends the framework and is driven
by exlib's lifecycle, the way `ExpandedLib.Industry` extends it with pipes, molten metal,
mechanical power, metals and heat. Industry is the first module; other modders have asked for
electric and heating layers, which are the second and third. "Companion assembly" is one shipping
form of a module, not the concept. `IExModule` and `ExModules` keep their names; the wiki's
"Shipping more than one assembly" becomes a Modules page with Industry as the worked example.

What a module is:

- **Identity is the module's own.** `[assembly: ExModule("<id>")]` names it; `[assembly: ExDomain]`
  still names the domain its classes are keyed under (Industry keeps `exlib`, so `exlib.BlockPipe`
  in shipped blocktypes and saves is unchanged; a third-party module declares its own). The
  attribute carries `Requires` for module-to-module dependencies, resolved in dependency order; a
  missing requirement logs an error naming both modules and the module is not driven.
- **The host drives it.** A module names the mod whose lifecycle drives it, default `exlib`. A
  framework module (Industry, electric, heating) is hosted by exlib and driven by exlib's own
  driver at exlib's phases; a mod's private second assembly names that mod as its host and is
  driven by the mod's `ExModSystem`. One mechanism, two hosts; the "domain equals mod id" lookup
  of today is replaced by the explicit host.
- **Two shipping forms.** A first-party module ships inside the exlib zip. A third-party module
  ships as its own Vintage Story mod that depends on exlib, so that several mods can share it;
  embedding one shared module dll inside two mods loads two assemblies of one name, which is not
  supported and is said so on the wiki. Whether a Code mod needs a `ModSystem` to load at all is
  verified in a smoke experiment; if it does, the module template carries a trivial one.
- **Definitions contribute in a phase the definition system owns.** `IExDefinitionContributor`
  is discovered by the same assembly scan as the register attributes and driven by
  `ExDefinitionModSystem` immediately before it injects at 0.04, so it is correct at any host
  order and available to a main assembly as well as a module. Industry's metal-family emitter
  moves there from `AssetsLoaded`.
- **A sample module proves the third-party form in the gate**: a minimal framework module under
  `samples/`, shipped as its own mod folder in the smoke, that `HelloExpanded` depends on.
  `exmod new <id> --module` scaffolds one (S2). The `ExpandedLib` prefix is reserved on nuget.org
  by the owner so that third-party module packages publish under their own ids.

What is fixed in the existing code, because publishing freezes it:

1. `ExModules` caches module instances in a static dictionary, so the client and the integrated
   server share one instance in single player and instances survive rejoining a world. Cache
   discovered types per host; each driver instance creates and owns its modules.
2. The `AssetsLoaded` doc says a module registers code-first definitions there; that holds only at
   exlib's own driver order 0.03 and fails at the default 0.1, after injection at 0.04. Replaced by
   the contributor phase above; the doc says so.
3. The interface lacks `StartServerSide`, `StartClientSide` and `Dispose`, and the driver
   registers only the block family of attributes: commands, sub-commands, preferences, config
   accessors and Harmony patches in a module are never scanned. Mirror the full phase set and run
   per module assembly every registry `ExModSystem` runs for a main assembly. `ExHarmony.PatchOnce`
   guards on the mod id, so a second assembly's patches under one mod are skipped; the guard
   becomes per assembly.
4. Discovery keys on the module type, so an assembly with the attributes and block classes but no
   module class registers nothing. The assembly attribute is the mark; the module type is
   optional.
5. `ExModSystem` loads config before starting its modules; `ExModuleModSystem` at 0.03 starts
   Industry before `ExpandedLibModSystem` at 0.1 loads `ExlibValues` and wires the loggers. A boot
   system ordered before the driver does exlib's own config load and logger wiring.
6. Lookup compares the asset domain to the mod id; replaced by the host.
7. A module type without a parameterless constructor is skipped silently. Log it.

**B1. The build plumbing ships inside the `ExpandedLib` package.** `build/ExpandedLib.props` and
`.targets` carry what today lives in `mods/Directory.Build.props` and `.targets` and is not a
target framework: `$(GamePath)` resolution from the env var or `.game/<slug>`, the `GAME_GE_*`
constants, the auto-provision target (invoking the wrapper), the asset globs, the modinfo version
reading and the generator's `AdditionalFiles` glob. The target framework list stays in each
consumer, because restore needs it before any package is present. In source mode the workspace
props import the same files from `$(ExlibRoot)build/`. Every consumer, the starter and any third
party get the same plumbing with nothing copied.

**R1. Dual-mode references.** With `ExlibRoot` set, iiex and siex reference exlib, industry, the
harness and the generators by `ProjectReference` (analyzer reference for the generators); with it
unset they use `PackageReference` with `ExcludeAssets="runtime"` for the mod projects (the player
installs exlib; nothing is copied beside the mod) and plain references for the test projects, with
every version in `Directory.Packages.props`. Both modes must build the same dll.

**R2. exmod provisions dependency mods.** `exmod stage`, `smoke`, `client` and `server` need the
exlib mod at runtime once it is not built in the same repo. exmod reads each mod's `modinfo.json`
dependencies, resolves each one from the workspace sibling's build output when present, else
downloads the release zip (GitHub release asset, ModDB as fallback) into a cache. No second list
of dependencies exists. The same resolution serves a module shipped as its own mod (E6).

### The starter and the toolchain

**S1. The starter is generated, never hand-edited.** `exmod starter` (or the exlib release job)
copies `samples/HelloExpanded`, its tests, `templates/` and the wrapper into the `exmod-starter`
repo,
flips the project references to package references pinned to the released version, and commits.
The tested source of truth stays in the exlib repo's gate, so the starter cannot go stale. The
minimal sample is the starter; a larger showcase is a later sample.

**S2. `exmod new <modid>` scaffolds an empty mod into a monorepo** - csproj, modinfo, asset
skeleton and a test project wired to the harness; `--module` scaffolds a framework module instead
(E6), from the sample module.

**S3. exmod is manifest-driven before it is extracted, and extracted before the starter exists.**
It knows this family by heart in four places: `Get-ExmodBuildTargets`, `Get-ExmodTestProjects`,
`Invoke-Codes` and `$GameTfms`, plus one tool path in the dist stage. A small `exmod.json` at each
repo root names its mods, test projects, samples, supported series and the pinned tools version;
build order comes from the solution. Generated before the extraction, the starter would carry a
copied family script that drifts.

---

## Order of work

Each step is what makes the next one cheap. Steps 1 and 3 touch disjoint files and may interleave;
everything else is in order.

0. **Commit what is pending.** Only this document and its index row are uncommitted; the CLI, the
   licence, trusted publishing, the Industry split and the module capability are in `a130265c`.
1. **The module system (E6).** Its own task plan, `2026-09-06-exlib-module-system.md`. Gate:
   exlib suite, both sample suites, `exmod smoke` with exlib, the sample module and the sample.
2. **Package tidy and build plumbing (B1, E5, R1 prepared).** Shared pack metadata; generators
   packed as analyzers; `build/` props and targets; `Directory.Packages.props`; the family
   invariants relocated into harness checks; `ReleasedCodes` and `RepoPaths` unbound; consumer
   `InternalsVisibleTo` retired; a solution filter. Gate: `exmod test all`; `exmod pack`; the
   sample restored in package mode from a local feed (`dist/nuget`) and green.
3. **exmod manifest and generic tools (S3, E3, R2).** Manifest, generic verify and code emitter,
   dependency-mod provisioning, workspace-sibling resolution. Gate: `exmod check` behaves as before.
4. **Tools repo extracted (L6).** `scripts/exmod/*` and the generic tools move to the tools repo
   (GitHub and Pi); this repo consumes them through the wrapper and the pin. Gate: `exmod check`
   through the wrapper; a fresh clone's first command works.
5. **The split (L1-L5, L7, R1).** The workspace is created and this checkout moves into it; exlib's
   history is filtered out with `git filter-repo` on a fresh clone (paths `mods/exlib/`, `samples/`,
   `templates/`, the shared props, `infra/test/`, the three workflows, the root dotfiles; renames
   to the layout in L1); exmods drops those paths; the workspace switch and dual-mode references
   land; sweeps for paths, wiki and README links, doc citations; CI per repo; `docs/internal` and
   `.compat` move to the parent; the memory stores are split and pruned; `~/.claude/CLAUDE.md`
   lists the new stores. Gate: source-mode `exmod check` in both repos; exlib smoke with exlib and
   the sample; exmods smoke with exlib staged from the sibling plus iiex and siex; the legacy lanes
   in source mode.
6. **First publish from exlib (P4, P8).** Policy for the new repo; tag `v0.8.0-preview.1`; NuGet
   and GitHub prerelease. Then exmods bumps the pin and the modinfo floors and goes green in
   package mode; a standalone clone of exmods builds, tests and smokes. This proves L7.
7. **Starter (S1, S2).** Gate: the generated repo clones, restores from NuGet, provisions, builds,
   tests and smokes with nothing edited by hand.
8. **The wiki web app**, independent of all of it.

## Traps

- **The assistant's memory store is keyed by the working directory.** Moving this checkout strands
  about 225 notes under `~/.claude/projects/-home-fallen-src-modding-vsexpanded/memory/`. They are
  split by subject, not copied whole: family content (furnaces, machines, shapes, forming line,
  gameplay systems, block and rendering gotchas) to the exmods store, framework notes (exlib,
  registries, config, harness traps, MSBuild) to the exlib store, plans and roadmap notes to the
  parent store, working-style feedback to every store it applies to. The owner asked for a
  **prune** at the same time: completed-work records that git history carries, stale-path notes
  the layout memory supersedes, rulings later rulings overturned. Absolute paths inside notes are
  not rewritten.
- **`git filter-repo` rewrites history: run it on a fresh clone, never in the working checkout.**
  It keeps the history of the chosen paths and applies the renames; the exmods repo keeps its full
  history untouched. Carry `.gitattributes` (the EOL rule that ended the CRLF flip),
  `.editorconfig`, `.csharpierrc`, `Directory.Build.rsp` (node reuse off) and `global.json` into
  every new repo, or the same problems return one at a time.
- **The boundary-straddling tests and docs are the underestimated part**, not the content: the
  eight files in E5, the 37 citations in L3, `ReleasedCodes.cs`, the harness root marker, and the
  wiki, README, template and CI paths that name `modding-vsexpanded` or `..\modding-vsexpanded\`.
  A missed path breaks loudly in the build; a missed citation breaks silently.
- **Each mode must be gated.** Source mode proves the daily loop; package mode proves standalone
  clones. A change green in one and unbuilt in the other has not landed.
- **Prerelease dependency floors in `modinfo.json`** (`"exlib": "0.8.0-preview.1"`) rely on the
  game's version comparison sorting a prerelease below its release; verify in the first smoke.
- **Four repos means four CI setups, four provisioning paths and four memory stores.** Four is
  defensible; resist the urge to add more.
- **`worktree-wf_d7e47fe4-157-4`** on both remotes is a leftover from a workflow worktree, not a
  maintenance branch; the owner decides whether it goes before the filter.
