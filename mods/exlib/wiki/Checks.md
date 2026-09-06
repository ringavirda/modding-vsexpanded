# Content Checks

`ExpandedLib.Checks` is the shared library behind the content guards that used to live only in
`exlib.testing`: dangling recipe/multiblock codes, missing lang coverage, pinned network nodes, a
prefix collision that widens a wildcard onto a foreign block, a network node or membership missing
part of its contract. The rule for each lives in one class, parameterised only through
`ICheckSource` - never a file path, an assembly or a test framework - so the same rule runs against
the live game, against a repository tree, or against anything else you can describe in terms of
codes, recipes, lang and definitions.

`LangCoverageCheck` in this library only guards the `en` locale - an unresolved `en` key is the one
that renders raw on screen, since every other translation falls back to it. Parity across a mod's
other shipped locales (a missing Ukrainian key, say) is a repository-time concern instead: see
`ExpandedLib.Testing.LangCoverage` and each mod's own `LangParityTests`.

There are three rungs, in increasing order of control.

## Rung 1: nothing to do

`ExpandedLibModSystem.AssetsFinalize` runs every check against the live game state and logs the
results, after the metal/fluid/process catalogues finish loading. Each check logs one summary line
naming itself, its domain and how many errors it found, followed by one line per error. A modder who
never opens xUnit still sees "your recipe names a code that does not exist" in the server log the
first time the world loads with the mistake in it.

Set `RunChecksOnLoad` to `false` in `exlib`'s config (`ex_values.json`) to skip this pass - the one
reason to is the one-time scan costing something noticeable on a very large modpack's world load.

## Rung 2: `/exmod verify`

Run the same checks on demand:

```
/exmod verify           # exlib itself plus every mod that depends on it
/exmod verify iiex      # one domain only, named explicitly - any loaded mod, dependent or not
```

With no argument, only exlib and its dependents are checked - never a bystander mod with no exlib
dependency, and never vanilla's own `game`/`survival`/`creative`, which never declares one and whose
own incomplete locales are not this library's to police. Naming a domain explicitly checks it
regardless of whether exlib depends on it, so long as some loaded mod answers to that id.

Prints how many checks ran and how many errors they found, then the first ten error lines; the full
list always goes to the server log via the same `ExlibChecks.Log` call `AssetsFinalize` uses, so a
long list is never truncated where it matters.

This is exactly the command `exmod smoke` (see [Testing Harness](Testing-Harness#the-smoke-lane))
runs against a freshly-booted dedicated server before stopping it, so the smoke lane's pass/fail
includes whatever `/exmod verify` finds.

## Without the game: `exlib-verify`

A JSON-only modder has no code to build and no reason to install xUnit, but still wants "does my
mod even load" before ever launching the game. `exlib-verify` (`infra/tools/ExlibVerify`, packed as
the `ExpandedLib.Verify` .NET tool) answers that from a mod folder or zip alone, against a
provisioned game install and any number of other mods:

```
exlib-verify <modpath> [--game <install>] [--mods <dir>...] [--json] [--strict]
```

`<modpath>` is a folder or a zip carrying `modinfo.json`. `--game` defaults to the same
`VINTAGE_STORY`-or-`.game/<slug>` resolution the test harness uses; `--mods` loads any number of
other mods (folder or zip) as additional asset domains, so a compatibility patch against a mod that
isn't the one under test can actually be checked. `--json` prints a stable
`{level, check, file, line, message}` array for CI; `--strict` also fails the run (exit 1) on an
informational finding, not only an error.

**Errors** (exit 1):

- a JSON file under the mod's own `assets/` that does not parse, with line and column
- a patch whose `file` target exists in no loaded domain, once its `dependsOn` mods are satisfied
  (a target in a mod that isn't loaded and isn't required is informational instead - see below)
- a patch operation that does not apply against the real target document - `add`/`replace`/
  `remove`/`addmerge`/`addeach`/`move`/`copy`, run through the game's own `Tavis.JsonPatch` engine
  exactly the way `ModJsonPatchLoader` drives it, against the real (and, by the time a check runs,
  already-patched) target JSON
- a `title`/`text` key a `config/handbook/*.json` page names that has no matching key in that
  key's own domain's `lang/en.json`
- a recipe ingredient or output code - `{ "type": "item"|"block", "code": ... }`, wherever it
  appears in a recipe's own JSON shape - that resolves to no block or item code declared anywhere
  across the mod, the game, and any `--mods`

**Informational** (exit 0 unless `--strict`):

- a patch's `dependsOn` naming a mod id this run has no `--mods` for - the patch is not evaluated,
  since it may be entirely correct once that mod is actually loaded alongside it
- a patch `condition.when` - there is no live world config outside a running game to evaluate it
  against, so the patch is named but never evaluated
- a `variantgroups` entry this tool cannot expand headlessly (`loadFromProperties`, which needs the
  loader's own `ICoreServerAPI`-bound world-property resolution) - a reference under that type's
  base code is assumed to resolve rather than risking a false error
- a code whose domain isn't loaded at all (no `--mods` for it) - this run has no way to say whether
  it resolves
- a locale other than `en` missing some of `en`'s keys, one line per locale naming the count - the
  same gap `ExpandedLib.Testing.LangCoverage` tracks for a checked-in mod, since a missing non-`en`
  key falls back to English in game rather than showing raw

A hybrid code+JSON mod (most third-party mods on the Mod DB) will still show real findings this way:
anything it registers from C# is invisible to a JSON-only scan, so a reference to it reads as
unresolved. That is a limitation of what a JSON-only pass can know, not a defect in the check - see
`infra/tools/ExlibVerify.Tests`'s own run over `.compat/_im` and `.compat/industrialstory` for what
this looks like against two real mods.

## Rung 3: `ExlibChecks.All` from your own code

```csharp
using ExpandedLib.Checks;

IReadOnlyList<CheckResult> results = ExlibChecks.All(api);
foreach (CheckResult result in results.Where(r => r.Errors.Count > 0))
    DoSomethingWith(result);
```

`All(ICoreAPI)` is the in-game path, over a fresh `AssetCheckSource`. `All(ICheckSource)` runs the
same checks against any source you build yourself - useful for a build-time script, a CI job, or a
tool that reads from somewhere other than a running game.

## Writing a custom `ICheckSource`

Implement the five members - `Domains`, `BlockCodes`, `ItemCodes`, `Recipes(domain)`,
`Lang(domain)`, `BlockDefinitions(domain)` - over whatever you're validating, and every check runs
unmodified. `AssetCheckSource` and the harness's `RepoCheckSource` are the two shipped
implementations; reading either is the fastest way to see what each member is expected to answer.

## What moved from the harness, and what did not

Every one of the seven checks above started life as a validator in `exlib.testing`'s `Checks/`
folder, used from each mod's xUnit suite. `MultiblockCodesCheck`, `RecipeCodesCheck`,
`LangCoverageCheck`, `CodePrefixCollisionCheck`, `PinnedNetworkNodesCheck` and
`DefinitionCatalogueCheck` moved cleanly: everything they need is expressible over `ICheckSource`.

`NetworkNodeContractCheck` did not move in full. The harness's own `NetworkNodeContract` selects a
"network node" definition by C# class (`BlockNetworkNode`, `BEBehaviorNetworkMember` and their
subclasses), which needs an assembly to reflect over - something no `ICheckSource` can supply, in
game or in a repository tree read generically. The library version selects the same definitions by
the contract they declare in JSON instead (a behaviour named "ExOrientable" in `network` mode, and
the framework's own `BEBehaviorNetworkMember` key for a membership), which is everything every check
in this codebase
has needed so far but is a narrower rule than the harness's reflective one - see the class's own
remarks for exactly where the two can disagree. The harness's `NetworkNodeContract` stays as it was,
unchanged, for that reason; the wrappers over the other six now delegate into this library so the
rule is written once. See [Testing-Harness](Testing-Harness) for the harness side of this split.
