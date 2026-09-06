# extools - the CLI in its own repository, consumed by wrapper and pin

> **For agentic workers:** execute task by task, in order; the gate in each task is the check.
> Task 1 is the driver's own work (history surgery and remotes); Tasks 2 and 3 take a fresh
> `builder` each. No review pass per task. Record each task in Progress at the bottom when its
> gate is green.

**Status** written 2026-09-06, not started. Step 4 of
[2026-09-06-repo-restructure.md](2026-09-06-repo-restructure.md): ruling L6. Runs after the exmod
manifest plan, which removes the last family knowledge from the scripts.

**Goal:** `exmod`, the packaging build, the verify tool and the generic helper scripts live in
`extools` (GitHub `ringavirda/extools`, Pi remote `git/extools.git`), tagged, MIT. A consuming repo
checks in two wrapper files and a pinned version; the wrapper finds the tools through an
environment variable, the workspace sibling, or a clone of the pinned tag, in that order.

**Why now:** the starter (step 7) and the family repo after the split (step 5) both consume the
CLI; a second copy is how a script starts lying.

**Architecture:** `scripts/exmod.sh` and `scripts/exmod.ps1` in a consuming repo become launchers
that resolve the tools directory and invoke `<tools>/exmod.ps1 -RepoRoot <repo>`; the dispatcher
and its stages move unchanged (step 3 already made them read the repo from `exmod.json`). The
history of the moved files travels with them through `git filter-repo` on a fresh clone.

**Tech stack:** git filter-repo, PowerShell 7, bash, GitHub Actions.

## Traps that apply to every task

- `git filter-repo` refuses to run in a clone that has a remote or uncommitted state; run it on a
  fresh `git clone --no-local` of this repo under `/tmp`, never in the working checkout.
- `gh` is installed and authenticated. Every repository this restructure creates is created
  **private** (owner ruling 2026-09-06); the owner flips it public when ready. While extools is
  private, a wrapper clone of the pinned tag needs the owner's git credentials, which the
  workspace machine and the owner's CI have; the wrapper's `EXTOOLS_URL` override points a
  machine without them at the Pi remote.
- The wrapper must keep working on Windows PowerShell 7 and on POSIX shells; `exmod.sh` keeps its
  pwsh bootstrap into `.dotnet/tools`.
- The wrapper runs before any manifest is read, so it parses the pin out of `exmod.json` with a
  regex, not a JSON library, the same way the csprojs read modinfo.
- `command grep` / `grep -F` for literals in the Bash tool.
- Never `git stash`, `git reset` or `git checkout --` in the working checkout.

## Design

### The tools repository

```
extools/
  exmod.ps1              the dispatcher (today scripts/exmod.ps1); $RepoRoot from -RepoRoot
  exmod/                 provision.ps1 src.ps1 run.ps1 dist.ps1 windows.ps1
  wrappers/exmod.sh      what a consuming repo checks in as scripts/exmod.sh
  wrappers/exmod.ps1     what a consuming repo checks in as scripts/exmod.ps1
  pack/                  CakeBuild (today infra/CakeBuild), manifest-driven
  verify/ExlibVerify/    the exlib-verify .NET tool (today infra/tools/ExlibVerify)
  verify/ExlibVerify.Tests/
  tools/patch-api.cs     the publicizer provision runs
  tools/coverage_gate.py the coverage ratchet check runs
  tools/gen-released-codes.py
  templates/ci/          tests.yml smoke.yml for third-party repos
  exmod.json             { "series": ["1.22"], "tests": ["verify/ExlibVerify.Tests"] }
  scripts/exmod.sh scripts/exmod.ps1   copies of the wrappers, so extools drives itself
  .github/workflows/ci.yml   pwsh parse of every script, then `exmod test latest`
  README.md  LICENSE  CHANGELOG.md  .gitattributes  .editorconfig
```

`infra/tools/convert-shape.py` and `generate-rolled-stock.py` are family tools and stay.
`templates/exlib-tests` is the harness template and stays with exlib.

### The wrapper contract

Both wrappers resolve the tools directory in this order and stop at the first that holds
`exmod.ps1`:

1. `$EXTOOLS_HOME`;
2. `<repo>/../extools` (the workspace sibling);
3. `<repo>/.extools/`, cloned on first use with
   `git clone --depth 1 --branch v<pin> <EXTOOLS_URL or https://github.com/ringavirda/extools.git> <repo>/.extools`,
   where `<pin>` is the `"tools"` value in `<repo>/exmod.json`; when the directory exists but
   its checked-out tag is not `v<pin>`, the wrapper fetches the tag and checks it out.

Then `exmod.sh` execs `pwsh -NoProfile -File <tools>/exmod.ps1 -RepoRoot <repo> "$@"` and
`exmod.ps1` does the same in PowerShell. `<repo>` is the wrapper's own parent directory.
`.extools/` is gitignored by the consuming repo. The pwsh bootstrap stays in `exmod.sh` exactly
as today.

### Shared installs in the workspace

`.game` and `.dotnet` are shared by symlink, not by code: `exmod setup` links `<repo>/.game` to
`../.game` and `<repo>/.dotnet` to `../.dotnet` when the parent holds them and the repo does not,
and creates them in the repo otherwise. Nothing else in the tool changes for the workspace.

---

### Task 1: the repository (driver)

1. Fresh clone: `git clone --no-local /home/fallen/src/modding-vsexpanded /tmp/extools-src`.
2. `git filter-repo` with `--path scripts/exmod.ps1 --path scripts/exmod.sh --path scripts/exmod/
   --path infra/CakeBuild/ --path infra/tools/ExlibVerify/ --path infra/tools/ExlibVerify.Tests/
   --path infra/tools/patch-api.cs --path infra/tools/coverage_gate.py
   --path infra/tools/gen-released-codes.py --path templates/ci/ --path LICENSE
   --path .gitattributes --path .editorconfig` and the renames
   `scripts/exmod.ps1:exmod.ps1`, `scripts/exmod/:exmod/`, `scripts/exmod.sh:wrappers/exmod.sh`,
   `infra/CakeBuild/:pack/`, `infra/tools/ExlibVerify/:verify/ExlibVerify/`,
   `infra/tools/ExlibVerify.Tests/:verify/ExlibVerify.Tests/`, `infra/tools/:tools/`,
   `templates/ci/:templates/ci/`.
3. Add `wrappers/exmod.ps1` (new), `scripts/` copies of both wrappers, `exmod.json`, README,
   CHANGELOG, the CI workflow, a `.gitignore` for `.game`, `.dotnet`, `bin`, `obj`.
4. Move the result to `/home/fallen/src/extools` for now (it moves under the workspace in step 5);
   `git init --bare git/extools.git` on the Pi; `gh repo create ringavirda/extools --private
   --source . --remote origin`; push both remotes; tag `v0.1.0`; push the tag.

**Gate:** in `/home/fallen/src/extools`, `bash scripts/exmod.sh help` lists every command;
`bash scripts/exmod.sh test latest` runs the verify tests green (provisioning the game into its
own `.game`); `git log --oneline -- exmod/src.ps1 | wc -l` is greater than one (history came along).

### Task 2: this repo consumes the tools

**Files:**
- Replace: `scripts/exmod.sh`, `scripts/exmod.ps1` with the wrappers from extools
- Delete: `scripts/exmod/`, `infra/CakeBuild/`, `infra/tools/ExlibVerify/`,
  `infra/tools/ExlibVerify.Tests/`, `infra/tools/patch-api.cs`, `infra/tools/coverage_gate.py`,
  `infra/tools/gen-released-codes.py`, `templates/ci/`
- Modify: `exmod.json` (`"tools": "0.1.0"`, the `tests` and `packages` entries that named the
  verify tool go), `.gitignore` (`/.extools/`), `VintageStory.sln` (the verify and Cake projects
  go), `.vscode/tasks.json` and `launch.json` (every path into the deleted folders), `.github/workflows/*.yml`
  (they call the wrapper as before; the release workflow's `dotnet pack` of the verify tool goes),
  `mods/exlib/wiki/Checks.md` (the verify tool's home), `CONTRIBUTING.md`

**Gate:** `bash scripts/exmod.sh help` through the wrapper; with `EXTOOLS_HOME` unset and no
sibling, `rm -rf .extools && bash scripts/exmod.sh help` clones the pinned tag and works; with
`EXTOOLS_HOME=/home/fallen/src/extools` the same; `bash scripts/exmod.sh check` green apart from the
format step, which the driver runs after committing; `bash scripts/exmod.sh pack` and `nuget`
produce the same artefacts as before (three zips; three packages, the verify tool now packs from
extools); a fresh `git clone` of this repo into `/tmp` runs `bash scripts/exmod.sh provision game`
and `test latest` green with nothing edited (remove the clone after).

### Task 3: the prose

**Files:**
- Modify: `CONTRIBUTING.md` (the wrapper, the pin, the three resolution rungs, how to work on the
  tools), `mods/exlib/wiki/Getting-Started.md` ("exmod in your repo": copy two files, add the
  manifest), `README.md` (Repository layout), `docs/design/conventions.md` (the layout table),
  `docs/internal/README.md` (this plan's row), `docs/internal/worklog/2026-09.md`; in extools:
  `README.md` (what it is, the wrapper contract, releasing a tag) and `templates/ci/*.yml` headers

**Gate:** link check clean; `test latest` green.

---

## Progress

(nothing yet)
