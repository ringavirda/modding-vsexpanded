# exlib industry split - three packages, one mod

> **For agentic workers:** execute task by task with a fresh implementer per task; the gate in each
> task is the check. No review pass per task.

**Status** I1 and I2 complete 2026-09-06 (uncommitted). I3 folded into I2. See Progress below: the runtime design changed once the engine's actual rule was found.

**Goal:** `ExpandedLib` ships as a generic framework with no ironmaking in it. The family layer
becomes `ExpandedLib.Industry`, its own assembly and its own NuGet package, still inside the exlib
mod. Someone who takes the framework to build a farming mod never sees a pipe.

**Why now:** the owner is building a starter repository other modders clone, and publishing exlib to
NuGet through trusted publishing. Both make the package's contents public API in a way they were not
before, and splitting a package after it ships is a breaking change for everyone who took the first
version. See [../research/2026-09-06-exlib-repo-split.md](../research/2026-09-06-exlib-repo-split.md).

**Architecture:** three assemblies inside one mod folder. `exlib.dll` (framework), the new
`exlib.industry.dll` (pipes, molten, mechanical power, metals, heat), and the developer-only
`ExpandedLib.Testing.dll`. The runtime story does not change: one mod, one modinfo, one ModDB entry.
What changes is that the dependency arrow between framework and domain becomes one the compiler
enforces.

**The engine's rule, found the hard way (see Progress):** a mod folder may hold more than one dll,
but only **one of them may contain mod systems at all**. A second one that does makes the game
refuse the whole mod with `Found multiple .dll files with ModSystems and/or ModInfo attributes`,
naming neither dll. One assembly may declare as many mod systems as it likes - exlib's own declares
several - so the limit is per file, not per mod. The domain layer therefore declares no mod system:
it implements exlib's new `IExModule` and exlib drives it.

**Tech stack:** as the framework plan. C# 14, net10.0 primary with net8.0/net7.0 legacy lanes.

## What is in the way

`mods/exlib/src/Industry` is 7,126 lines under `ExpandedLib.Industry.*`, already one namespace tree.
Core reaches into it from eight files, and only three of those are real code:

| Site | Use | Inversion |
|---|---|---|
| `ExpandedLibModSystem.cs:64` | `MetalCatalogueLoader.Load(api)` at AssetsFinalize | Industry's own ModSystem loads its own catalogue |
| `Definitions/ExDefinitionModSystem.cs:39` | `MetalFamilyEmitter.Emit(...)` at AssetsLoaded 0.04 | same, at the same ExecuteOrder |
| `Networks/BlockNetworkNode.cs:691` | `ExBlockNames.Decorate(this, ...)` | `ExBlockNames` is generic and moves to core |

The other five are `<see cref="ExpandedLib.Industry...">` in XML comments
(`ExlibConfig.cs`, `Catalogues/Fluids/LiquidCatalogueLoader.cs`, `Catalogues/Fluids/IMediumTaxonomy.cs`,
`Catalogues/Fluids/ExLiquids.cs`, `Catalogues/Materials/MaterialRoleDef.cs`). A cref across an
unreferenced assembly is CS1574, and this tree builds warning-free, so they have to become plain
prose or `<c>` text.

`MaterialRoleLoader`'s "must load after the metal and liquid registries" is a content ordering, not a
compile dependency - no core type calls `MetalRegistry`. ExecuteOrder between the two mod systems
preserves it.

---

### Task I1: break the three core-to-Industry call sites

**Files:**
- Modify: `mods/exlib/src/Helpers/ExBlockNames.cs` (moved from `src/Industry/Helpers/`)
- Modify: `mods/exlib/src/Networks/BlockNetworkNode.cs`
- Modify: `mods/exlib/src/ExpandedLibModSystem.cs`, `mods/exlib/src/Definitions/ExDefinitionModSystem.cs`
- Create: `mods/exlib/src/Industry/IndustryModSystem.cs`
- Modify: the five files carrying an `ExpandedLib.Industry` cref
- Test: `mods/exlib/tests/Invariants/IndustryBoundaryTests.cs`

`ExBlockNames` reads the `material`, `rock` and `brick` variants through vanilla's own lang keys -
that is framework behaviour any mod with a material variant group wants. Only the `refractory`
clause is family-specific. Move the type to `ExpandedLib.Helpers`, keep `Decorate` behaving exactly
as it does today for the three generic groups, and replace the hardcoded refractory clause with a
registry a mod adds to:

```csharp
ExBlockNames.AddVariantQualifier("refractory", "exlib:refractory-");
```

Industry registers that one line at StartPre. Same output, and a third-party mod can now decorate on
its own variant group without touching exlib.

`IndustryModSystem` takes over both metal call sites: `AssetsLoaded` at ExecuteOrder 0.04 for the
family emitter (it must inject before the object loader), `AssetsFinalize` for the catalogue load.
Both use core public API only.

**Gate:** `exmod build all` warning-free; `exmod test all`; a new assertion in
`IndustryBoundaryTests` that no type outside `ExpandedLib.Industry.*` names an Industry type - this
is what proves the task done before the assemblies are actually separate.

### Task I2: the project and the move

**Files:**
- Create: `mods/exlib/industry/ExpandedLib.Industry.csproj`
- Move: `mods/exlib/src/Industry/**` to `mods/exlib/industry/**` (namespaces are unchanged)
- Modify: `mods/iiex/src/IronIndustryExpanded.csproj`, `mods/siex/src/SteelIndustryExpanded.csproj`,
  `mods/exlib/tests/ExpandedLib.Tests.csproj`
- Modify: `infra/CakeBuild/Program.cs`, `.github/workflows/release.yml`, `infra/test/coverage-floors.json`

The new csproj mirrors `ExpandedLib.csproj`: same TFM manifest, same legacy opt-in, `AssemblyName`
`exlib.industry`, `RootNamespace` `ExpandedLib.Industry`, pack metadata reading the version from
`mods/exlib/src/modinfo.json` like the other two, `PackageLicenseExpression` MIT. Its `OutputPath`
is exlib's mod output (`../src/bin/$(Configuration)/Mods/mod`, and the per-TFM legacy variant), so
the dll lands where the game finds it and `exmod stage` picks it up with no extra step. It
references `ExpandedLib.csproj` with `Private=false`, the same way the mods reference exlib.

`InternalsVisibleTo` for `ExpandedLib.Tests` moves with whatever internals the tests drive.

Cake publishes both projects into the exlib stage directory, and `exmod nuget` and release.yml pack
the third package.

**Gate:** `exmod build all`; `exmod test all`; `exmod pack` produces an exlib zip containing both
`exlib.dll` and `exlib.industry.dll`; `exmod smoke` boots with the mods loaded.

### Task I3: the contract and the prose

**Files:**
- Modify: `mods/exlib/wiki/Supported-API.md`, `mods/exlib/wiki/Home.md`
- Modify: `docs/design/conventions.md`, `docs/internal/testing.md`
- Modify: `mods/exlib/CHANGELOG.md`
- Modify: `mods/exlib/tests/Invariants/PublicSurfaceTests.cs` and `IndustryBoundaryTests.cs`

`Supported-API.md` gains an `ExpandedLib.Industry` section stating plainly that it is the family's
domain layer, supported but not general-purpose, and that a mod outside this family should not need
it. The surface guard splits per assembly. `conventions.md`'s layout rule gains the third project.

**Gate:** `exmod test all` (the wiki parity guard is a test); `exmod check` end to end.

---

## Progress

**I1 complete 2026-09-06.** `ExBlockNames` moved to `ExpandedLib.Helpers` and gained
`AddVariantQualifier`, so the refractory clause is registered by the domain layer rather than
hardcoded in the framework. The two metal call sites moved out of exlib's mod systems. Five XML
crefs rewritten, plus a sixth the brief's grep could not see (a namespace-relative
`<see cref="Industry.Materials.Roles"/>`, resolvable only while both namespaces shared one
assembly). `IndustryBoundaryTests` gained a reflection walk and a source scan.

**I2 complete 2026-09-06, with the design changed mid-task.** The project, the move, the references,
Cake, `exmod nuget`, `release.yml` and the solution all landed as planned. Then smoke failed: the
domain layer had been given its own `ModSystem`, and the game refuses a mod folder whose second dll
carries one. An experiment that excluded just that type confirmed the rule precisely - the "multiple
.dll files" error vanished and only "no such class registered" remained, which is a second dll
being loaded and used with nothing registering its classes.

The fix is a framework capability rather than a workaround, since any mod outgrowing one assembly
hits the same wall:

- `Registries/IExModule.cs` - the entry point of a companion assembly, with empty defaults for
  `StartPre`, `Start`, `AssetsLoaded` and `AssetsFinalize`, and an `Order`.
- `Registries/ExModules.cs` - discovery over loaded assemblies keyed by `[assembly: ExDomain]`
  (not a folder scan, so a zipped mod works the same), plus driving with per-module exception
  isolation, and `EntityRegistry.RegisterAll` run over each companion assembly at `Start` so a
  `[BlockRegister]` type in one needs no code.
- `Registries/ExModuleModSystem.cs` - exlib's own driver at ExecuteOrder 0.03, under
  `ExDefinitionModSystem`'s 0.04 and `ExpandedLibModSystem`'s 0.1, which is what makes a module's
  `AssetsLoaded` and `AssetsFinalize` land early enough to be useful.
- `ExModSystem` drives its own companions in the same four phases, so a third-party mod gets this
  without writing a driver.

`AssetCatalogueLoader` was made public in the same task: `ContributedCatalogueLoader<TSet, TRegistry>`
and `CatalogueLoadReport` were already supported API while the primitive under them was not.

**Gate:** `build all` clean apart from the pre-existing net7 MSB3277; 11 lanes green; `exmod pack`
produces an exlib zip carrying `exlib.dll`, `exlib.industry.dll`, both XML docs and the assets
exactly once; `exmod nuget` produces four packages; `exmod smoke` boots and verifies clean.

**Coverage:** `exlib.industry` measures 84.9% and takes a 75.0 floor, the same margin the other
three carry. exlib's own rose to 77.9% against its unchanged 68.0 floor - the domain code it lost
was the less-covered half - so nothing had to be relaxed. `ExpandedLib.Verify` gained the README
that `dotnet pack` was warning it lacked.
