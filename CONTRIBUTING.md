# Code style

How the C# in `mods/*/src/` and `mods/*/tests/` is written and formatted. Domain rules - units, invariants,
network semantics - live in [conventions.md](docs/design/conventions.md); this file is about the code itself.

The mechanical parts are enforced, not trusted to review: formatting by `exmod format`, comment
style by `CommentStyleGuards` in `ExpandedLib.Tests`. If a rule below is not enforced, it is a
convention that a reviewer has to catch.

## Running things

Every repo task goes through one entry point. `scripts/exmod.ps1` holds the implementation and runs
on all three platforms under PowerShell 7; `scripts/exmod.sh` is a launcher for POSIX shells that
finds `pwsh` (installing it into `.dotnet/tools` if the machine has none) and forwards to it. There
is deliberately no second implementation: the previous `.ps1`/`.sh` pairs had already drifted, with
the same VS Code task provisioning a different game build on Windows than on Linux.

`exmod help <command>` prints one command in detail, flags included - the list below is a map, not
a manual.

```
first run
  setup           provision .NET and the game, then restore the solution
  provision       one half of that on its own: provision dotnet | provision game

source
  build           compile the mods for one game series or all of them
  test            run the test suites, one lane per game series
  format          rewrite with CSharpier, then dotnet format
  verify          check the shipped assets the way the game loads them, with no game running
  codes           regenerate a mod's block-code table
  check           format, build, verify and test in one pass - the gate
  clean           delete build output

run
  client          the game client, with the built mods
  server          a dedicated server, with the built mods
  smoke           boot a server, verify it, stop it
  stage           copy built mods into a Mods folder
  logs            the newest client or server log

package
  pack            build every mod for every game series and zip them into dist/Releases
  bundle          the developer bundle: test harness, generators, XML docs
  nuget           the NuGet packages
  release         check that a version is ready to tag (read-only; it never writes git)

machine
  fix-registry    repoint Windows' Vintage Story file association (Windows only)
```

### The manifest, central versions, the solution filter

`exmod.json` at the repo root is every command's only source of family knowledge: which mods and
samples this repo builds, where their projects and tests sit, which solution and game series to
use. `exmod` (`scripts/exmod.ps1`) and `RepoPaths` (C#, through `RepoManifest`) both read it and
fall back to the `mods/<id>` convention where a field is silent.

- `tools` - the pinned CLI/tool version.
- `solution` - the `.sln` at the repo root; defaults to the single one there.
- `series` - the game series this repo builds for, current first; defaults to the current series
  alone.
- `mods` - id -> `{ path, overlays }`, in build order (a mod is also a dependency of every later
  one in the list). A mod's project is the single `.csproj` under `<path>/src` when that folder
  holds one, else under `<path>` itself; its test project, when there is one, is the single
  `.csproj` under `<path>/tests`.
- `samples` - id -> `{ path, tests }`. A sample's project sits at its own path; `tests` names the
  folder holding its test project.
- `tests` - extra test projects (folders holding one `.csproj` each) run alongside the mods' and
  samples' own.
- `packages` - the packable project folders `nuget` packs.
- `depends` - runtime dependency mods this repo does not build itself: id -> `{ github }` or
  `{ url }` naming where release zips are published; an id with no entry resolves through the
  ModDB API instead.

`-RepoRoot <path>` points a command at a checkout other than the one holding the script; without
it, `exmod` finds the nearest `exmod.json` above the current directory.

`exmod provision mods` resolves every dependency named in `depends` or in a mod's or sample's own
`modinfo.json` (skipping `game` and anything the repo builds itself): a workspace sibling first - a
directory beside the repo root whose own `exmod.json` builds that id, used from its build output
directly - else a cached or freshly downloaded release, unpacked under `.exmod/mods/<id>/`
(gitignored).

Every `PackageReference` version in the repo (exlib's own test projects, the harness, the generators,
`infra/tools/ExlibVerify*`, both samples) comes from `Directory.Packages.props` at the root -
central package management, one number per package, no `Version` attribute at the reference site.
`exlib.slnf` is the solution filter scoped to exlib itself: its projects, the generators, the
harness, exlib's tests and both samples with their tests, none of `mods/iiex`, `mods/siex` or
`infra/` - `dotnet build exlib.slnf` is the fast loop for exlib work alone.

A project referencing `ExpandedLib` builds in one of two modes, switched on `$(ExlibRoot)`: source
mode (inside this repo, or a workspace checkout with `ExlibRoot` set) references the checked-out
projects directly; package mode (`-p:ExlibRoot=`, the default outside the repo) restores the
`ExpandedLib` NuGet package, which carries the generators as analyzers and the `build/` plumbing
alongside the dll.

### The API patch

`provision game` runs `infra/tools/patch-api.cs` over the provisioned
`.game/<slug>/VintagestoryAPI.dll` and makes `IPlayer.IsInInteractionRangeOf(BlockPos, float)` public.

Vintage Story 1.22.6 ships that member as `internal abstract`. An interface member is a vtable slot
every implementer must fill, and an `internal` one cannot be filled from another assembly - the
compiler demands it (`CS0535`) and forbids it (`CS0122`) in the same build, and the CLR enforces the
same rule on override. So `IPlayer` cannot be implemented or mocked at all, and every test that
substitutes one fails with `TypeLoadException` on the .NET 10 lane. It was 170 tests.

The patch flips one accessibility bit and changes no IL. `VintagestoryAPI.dll` is not strong-named,
so this is safe. Only the provisioned copy is touched, and `.game/` is a regenerable build artifact -
the mods still compile and run against whatever API the player has installed. It is idempotent and a
no-op once the member is public, so it can stay in place and will disappear on its own when upstream
changes it (the member is marked `[Obsolete("This signature will change in 1.23.")]`, though that
covers the signature, not the accessibility).

## Formatting

Braces go on the same line, indentation is two spaces, lines wrap at 80 columns.

```csharp
public class ChargeColumn {
  public void Push(BurdenMix mix, int units) {
    if (units <= 0) {
      return;
    } else {
      _courses.Add(new Course(mix, units));
    }
  }
}
```

This needs two tools, because neither does the whole job:

| Tool | Wraps long lines | Brace placement |
| --- | --- | --- |
| CSharpier | yes, to `printWidth` in `.csharpierrc` | Allman only, not configurable |
| `dotnet format` | no, Roslyn cannot reflow | reads `.editorconfig` |

So `scripts/exmod.ps1 format` runs CSharpier first and `dotnet format` second. **The order
is load-bearing** - CSharpier emits Allman braces and `dotnet format` moves them up. The pair is
idempotent. Running CSharpier on its own afterwards puts the braces back, so always use the script.

`csharpier check` reports the finished tree as unformatted, so it cannot be the CI gate. Use
`scripts/exmod.ps1 format -Check`, which formats and then fails if git sees a change.

`max_line_length` in `.editorconfig` is an editor guide only; Roslyn ignores it. Keep it equal to
`printWidth` in `.csharpierrc` or the two tools will disagree about where a line ends.

## Comments

Write what a caller needs. Delete what a historian wants.

The repo has been swept once for the opposite habit: comments that carried decision history, bug
post-mortems and paragraphs defending a design against alternatives. That belongs in `docs/design/`
and in git, not beside the code.

### Keep

- What the type or member is and does, in one or two sentences.
- Units, ranges, defaults, invariants - "seconds", "litres", "0 means unlimited".
- Constraints on the caller: ordering, client-versus-server side, "must be called after X",
  "has no setter because the tick recomputes it".
- Mechanism the code cannot show for itself: why a value is cached or serialized, why a clamp
  exists, why a default is what it is.
- Pointers into `docs/design/`. When a rationale is long, cite the doc instead of restating it.
- `<param>`, `<returns>`, `<exception>` where the signature does not already answer it.
- In `**/Migrations/**` and `ReleasedCodes*`, factual version history - that is what those files
  are for. The guard test exempts them.

### Delete

- Decision history: what it used to be, what was tried, what shipped broken, how many call sites
  were fixed, dates, task IDs.
- Essays defending the design against alternatives.
- Rhetoric and dramatic framing.
- Prose duplicated from `docs/design/` - replace it with a one-line pointer.
- Restatement of the code: `// increment the counter`.
- Meta-commentary about the repo, the test suite or the process.

### Voice

Plain declarative, present tense, third person.

- No first or second person. Not "we cache this", but "cached because".
- No `<b>`, `<em>` or `<i>` for stress. Prefer none at all.
- No em-dash. Use `-` or restructure the sentence.
- No emoji, star or warning markers, no ALL-CAPS stress.
- Drop "Note that", "Importantly", "It is worth noting", "Remember", "Crucially".
- One `<summary>`. A second `<para>` only when it states a genuinely separate constraint.

### Size

| Kind | Target |
| --- | --- |
| Class or interface doc | 6 lines |
| Method or property doc | 4 lines |
| Each <param> / <returns> | 1-2 lines |
| Inline comment | 1-3 lines |

These are targets. `CommentStyleGuards` fails a doc block only above 16 lines and above three
<para> blocks - a backstop against the essay coming back, not the standard to write to. Go longer
than the target only where a real constraint cannot be stated shorter.

### Examples

A class doc that argued its own case, cut to what a reader needs:

```csharp
// before - 30 lines on why typos are dangerous, which catalogue can never be generated,
// and a FIX: note from a scratchpad
/// <summary>
/// Vanilla block codes used by the suite's multiblock layouts, named once here so that a typo in a
/// <see cref="MultiblockLayoutBuilder.Legend"/> is a compile error rather than a blockNumbers entry
/// that matches nothing. <see cref="ExCodes"/> holds exlib's own blocks, IiexCodes and IiexCodes
/// each mod's; vanilla items used by recipes live in <see cref="ExIngredients"/>.
/// </summary>
```

A property doc that kept the rule and dropped the post-mortem:

```csharp
// before - 20 lines including "the green-suite lie this repo has already been bitten by"
// and a count of the fixture call sites that were rewritten
/// <summary>
/// Current operating state. Has no setter: on the shaft branch the value is recomputed from the
/// charge every tick (<see cref="DerivesState"/>), so an assignment would be discarded. It is
/// stored rather than computed because the firebox branch owns a real state machine and the value
/// must survive a save. The production tick is the only writer.
/// </summary>
```

A comment that was already right and was left alone:

```csharp
// Timers accumulate elapsed seconds (dt) so durations are independent of the
// production-tick interval. Thresholds below are in seconds.
```

## Structure

- File-scoped namespaces; `using` directives outside the namespace, `System` first.
- One public type per file, named after the file.
- `#region` blocks group members by role in long types. Keep the names meaningful.
- Tests live with the top mod they touch; there is no shared cross-mod test project.
- Definitions are authored in C# and checked against goldens rather than hand-written JSON.
