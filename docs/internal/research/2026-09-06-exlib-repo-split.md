# Research snapshot - should exlib live in its own repository

**Written** 2026-09-06 against branch `ironmaking-expanded` at commit `bbf22691`.
**Covers** the case for and against extracting `mods/exlib` into a standalone repository now that it is
positioned as a framework other modders can build on, the measurements behind that case, and what has
to change before a split would be cheap.
**Purpose** answering the owner's question directly, and leaving the reasoning where a later revisit
can check whether the conditions have moved.

Not a ruling. The recommendation is this note's own judgment; the decision is the owner's.

---

## Recommendation

Keep exlib in this repository for now. Split it when a third party is building against it, not before.

The thing that makes exlib good is that two large mods and a sample use it, and that one command
proves all of them at once across three game versions. A split trades that for presentation. The
presentation problem has a cheaper answer (a published mirror), and the presentation problem is not
yet real: nobody outside this repo consumes exlib.

## Measurements

| | lines of C# | tracked files |
|---|---|---|
| `mods/exlib/src` | 29,734 | 539 (whole mod, including wiki and assets) |
| of which `src/Industry` | 7,126 | |
| `mods/exlib/testing` (harness) | 7,895 | |
| `mods/exlib/tests` | 28,163 | |
| `mods/iiex/src` | 47,027 | 887 |
| `mods/siex/src` | 7,011 | 212 |

Both consumers reference exlib by `ProjectReference`, and siex references iiex the same way.
`mods/exlib/src/InternalsVisibleTo.cs` names three assemblies: `ExpandedLib.Tests`,
`IronIndustryExpanded.Tests` and `SteelIndustryExpanded.Tests`.

## What a split would buy

- A repository a stranger can read in one sitting: framework code, a wiki, a README and a release
  list, with no ironmaking content, no vendored third-party mods under `.compat/`, and no design
  documents about blast furnaces.
- Issues, releases and stars that belong to the framework rather than to a mod family.
- A hard boundary. Today a consumer can reach past the published surface by being in the same
  solution; across repositories only what ships works, so the boundary is enforced by the build
  rather than by the four guard tests that enforce it now.
- Release cadence that is not entangled: exlib can ship a patch without a mod release, and its tags
  stop sharing a namespace with `iiex` and `siex` tags.

## What it would cost

- The proving loop. `exmod test all` builds and runs eleven lanes across 1.20, 1.21 and 1.22, and
  `exmod smoke` boots a real server with every mod loaded. Split the repository and a change to
  exlib becomes: pack a preview package, bump the consumer repo, run its suites, find the break, go
  back. That is the loop that has caught most of the real defects here.
- `src/Industry` is a quarter of exlib and is family domain code: pipes, molten metal, mechanical
  power, metals, heat. Splitting forces a decision on it. Either the framework repository keeps
  domain code no other modder wants, or `Industry` becomes a third package and both consumers grow a
  second dependency to version.
- Shared infrastructure duplicates or drifts: provisioning, the vendored game sources under
  `.compat/`, the test harness, `exlib-verify`, the `dotnet new` template, the CI workflows, the
  coverage floors, and now this CLI. Two copies of a script is how a script starts lying.
- `InternalsVisibleTo` names the consumers' test assemblies. It keeps working across a split (the
  attribute travels in the assembly and nothing here is strong-named), but a framework that names
  its users' test projects is a framework that has not finished separating.

## The cheaper middle, if the standalone presence is wanted now

`git subtree split --prefix=mods/exlib` produces a commit history containing only exlib. Pushed to a
second repository on release, that gives a standalone read-only home with its own README, LICENSE,
wiki and release assets, while development stays here. Issues filed there get fixed here and arrive
with the next sync. This is a few lines in the release path, and it is reversible; a real split is
not.

## Revisit when any of these is true

- A mod outside this family ships against exlib and files its first issue.
- `src/Industry` has moved out of exlib, or has been decided to be a separate package.
- Nothing in exlib needs the consumers' suites to prove it, which today is not the case: the family
  tests are what cover the framework's block, network and structure bases.
- The monorepo's own gate stops being able to run in one command - if `test all` splits into
  per-mod gates for time reasons, the argument for keeping them together weakens.

## What to do meanwhile, so a split stays cheap

Mostly done already: the published surface is declared in `Supported-API.md` and guarded, the
packages carry their own metadata and now a license, the wiki is exlib's own, and the harness ships
as a package and a template. What is left:

- Retire `InternalsVisibleTo` for the two consumer test assemblies by promoting the seams they drive
  into the harness as documented test hooks.
- Decide where `src/Industry` belongs. It is the only part of exlib that would not make sense in a
  framework repository.
