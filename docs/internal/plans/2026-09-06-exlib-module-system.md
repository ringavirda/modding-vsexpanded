# exlib module system - identity, host, phases, contributors

> **For agentic workers:** execute task by task with a fresh implementer (`builder`) per task, in
> order; the gate in each task is the check. No review pass per task; one `architect` review after
> Task 4, before Task 5. Never commit - the owner commits. Do not run `exmod format` (the format
> gate needs a clean tree); match the surrounding style by hand. Record each task in Progress at
> the bottom when its gate is green.

**Status** written 2026-09-06, not started. Step 1 of
[2026-09-06-repo-restructure.md](2026-09-06-repo-restructure.md), ruling E6.

**Goal:** the module system becomes exlib's extension mechanism. A module is an assembly that
extends the framework and is driven through a host mod's lifecycle: identified by its own id,
ordered by its declared dependencies, registered exactly like a main assembly, and able to
contribute asset-dependent code-first definitions in a phase the definition system owns.
`ExpandedLib.Industry` is the first module; a sample module shipped as its own mod proves the
third-party form.

**Why now:** publishing to NuGet freezes `IExModule`. The current shape shares module instances
across the two sides of an in-process single-player game and across worlds, documents a phase
contract that only holds at exlib's own execute order, drives blocks but not commands, preferences,
config or Harmony, discovers nothing without an entry-point type, uses the asset domain as the mod
identity, and skips silently.

**Architecture:** `[assembly: ExModule]` is the identity; `ExModules` discovers every module in the
process once and orders each host's set by `Requires`; `ExModuleHost` is the per-driver-instance
runtime that owns entry-point instances and runs, per module assembly, the same registries
`ExModSystem` runs for a main assembly; exlib's own driver `ExModuleModSystem` (execute order 0.03)
hosts framework modules and boots exlib's config and loggers first; `ExModSystem` hosts a mod's own
modules through the same class. `IExDefinitionContributor` is discovered by the registry scan and run
by `ExDefinitionModSystem` immediately before injection, so it is correct at any host order.

**Tech stack:** C# 14, net10.0 primary with the net8.0/net7.0 legacy lanes unchanged; xUnit and
NSubstitute through `ExpandedLib.Testing` (`TestWorld`, `RecordingLogger`, `ReflectionHelpers`).

## Traps that apply to every task

- Builds are serial: two builds race on `obj/` (CS2012). `bash scripts/exmod.sh test latest` runs
  the three current-version lanes in about 30 s; `test all` adds the legacy lanes.
- `grep` in the Bash tool is a ugrep wrapper: use `command grep` or `grep -F` for literals.
- A generic test helper constrained on `BlockEntity` makes vstest skip the whole assembly
  (memory `xunit-generic-helper-breaks-discovery`).
- `EntityRegistry.RegisterAll` records an assembly against the mod id that registered it. A test
  that registers the test assembly under a throwaway id resets `_domainByAssembly` on dispose, the
  way `ExModSystemTests.Dispose` does, or every later `KeyFor`/`DomainOf` in the run is wrong.
- Harmony tests join `[Collection(ExHarmonyCollection.Name)]`; patches are process-wide.
- Static state in exlib is shared by both sides of the in-process single-player game and survives
  rejoining a world. Nothing in this plan may cache an instance statically; types and metadata only.
- `RegistrationKeyTests` relies on the test assembly declaring no `[assembly: ExDomain]`. It stays
  that way; the test assembly becomes a module of a fake host without one (allowed, see Task 1).
- `PublicSurfaceTests` fails on a public type missing from `wiki/Supported-API.md`, and
  `WikiParityTests` on a page naming a symbol that no longer exists. Each task edits those pages for
  what it adds or removes; the prose pages are Task 5.
- Never `git stash`, `git reset` or `git checkout --` anything. The tree is the owner's.

## Design

### Public surface after this plan

```csharp
namespace ExpandedLib.Registries;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ExModuleAttribute(string id) : Attribute {
  public string Id { get; } = id;            // unique across every loaded module, lower-case
  public string Host { get; set; } = "exlib"; // the mod id whose lifecycle drives this module
  public string[] Requires { get; set; } = []; // module ids this one runs after, same host
  public bool PatchHarmony { get; set; }      // apply this assembly's uncategorised patches at Start
}

public interface IExModule {                  // an entry point; every method has an empty default
  void StartPre(ICoreAPI api) { }
  void Start(ICoreAPI api) { }
  void StartServerSide(ICoreServerAPI api) { }
  void StartClientSide(ICoreClientAPI api) { }
  void AssetsLoaded(ICoreAPI api) { }
  void AssetsFinalize(ICoreAPI api) { }
  void Dispose() { }
}

public sealed class ExModuleInfo {            // one discovered module, built once per process
  public required string Id { get; init; }
  public required string Host { get; init; }
  public required IReadOnlyList<string> Requires { get; init; }
  public required Assembly Assembly { get; init; }
  public required IReadOnlyList<Type> EntryPoints { get; init; } // concrete IExModule types, name order
  public bool PatchHarmony { get; init; }
  public string HarmonyId => Host + "." + Id;
}

public sealed record ExModuleSet(IReadOnlyList<ExModuleInfo> Modules, IReadOnlyList<string> Errors);

public static class ExModules {
  public static IReadOnlyList<ExModuleInfo> All { get; }   // every module of every host
  public static ExModuleSet For(string host);              // one host's modules, dependency order
  public static bool IsLoaded(string moduleId);
  public static string FlagKey(string moduleId);           // "exlib:module:<id>", set in world config
  internal static ExModuleSet Order(IEnumerable<ExModuleInfo> modules); // pure, for tests
  internal static void Reset();
}

public sealed class ExModuleHost {            // one driver instance's modules; owns the instances
  public ExModuleHost(Mod mod);
  internal ExModuleHost(Mod mod, ExModuleSet set);
  public IReadOnlyList<ExModuleInfo> Modules { get; }
  public void StartPre(ICoreAPI api);
  public void Start(ICoreAPI api);
  public void StartServerSide(ICoreServerAPI api);
  public void StartClientSide(ICoreClientAPI api);
  public void AssetsLoaded(ICoreAPI api);
  public void AssetsFinalize(ICoreAPI api);
  public void Dispose();
}

namespace ExpandedLib.Definitions;

public interface IExDefinitionContributor {   // asset-dependent code-first definitions
  void Contribute(ICoreAPI api);              // register through ExDefinitions.Register*
}
```

`IExModule.Order` is removed: `Requires` orders modules, ids break ties. `ExModules.For` no longer
returns instances. `ExHarmony` gains string-id overloads of `PatchOnce` and `UnpatchAll`.

### Ordering at world load

| Execute order | System | What happens |
|---|---|---|
| 0.0 | `ExModsModSystem` | mod flags, as today |
| 0.03 | `ExModuleModSystem` | `StartPre`: wire `ExDefinitions.Logger` and `EntityRegistry.Logger`, `ExlibValues.Load`, set `exlib:module:<id>` flags, then every framework module's `StartPre`. `Start`: per module assembly `ExConfig.LoadAll`, `EntityRegistry.RegisterAll`, Harmony when opted in, then the entry points' `Start`. Side hooks: commands (and preferences on the client) per module assembly, then the entry points. `AssetsLoaded`, `AssetsFinalize`, `Dispose` likewise. |
| 0.04 | `ExDefinitionModSystem` | `ExDefinitions.RunContributors(api)`, then the injection as today |
| 0.05 | the game's patch loader | unchanged |
| 0.1 | `ExpandedLibModSystem`, every mod's `ExModSystem` | exlib registers its own classes; a mod's `ExModSystem` registers its main assembly, then hosts its own modules through `ExModuleHost` in the same order of operations, then its `On*` hook |

A contributor discovered in any `Start` (main assembly or module, any order) runs at 0.04, after
every `Start` and after the game's assets are indexed. That is the one legal place to emit a
definition that depends on loaded assets.

### What does not change

`[assembly: ExDomain]` still names the domain an assembly's classes are keyed under; Industry keeps
`exlib`, so `exlib.BlockPipe` in shipped blocktypes and in saves is unchanged. The wiki's two-line
module example still works once the assembly attribute is added. `ExModSystem`'s `On*` hooks, the
side-hook order (preferences before commands) and the catalogue loads in `ExpandedLibModSystem`
are untouched.

---

### Task 1: identity, discovery and ordering

**Files:**
- Create: `mods/exlib/src/Registries/ExModuleAttribute.cs`, `mods/exlib/src/Registries/ExModuleInfo.cs`
  (holds `ExModuleInfo` and `ExModuleSet`)
- Modify: `mods/exlib/src/Registries/ExModules.cs` (rewrite), `mods/exlib/src/Registries/IExModule.cs`
  (full phase set, `Order` removed, doc rewritten), `mods/exlib/industry/AssemblyInfo.cs`
  (`[assembly: ExModule("industry")]`), `mods/exlib/wiki/Supported-API.md` (rows for
  `ExModuleAttribute`, `ExModuleInfo`, `ExModuleSet`; `IExModule` and `ExModules` reworded),
  `mods/exlib/wiki/Registries.md` (the sentence "lowest `Order` first" goes)
- Test: `mods/exlib/tests/Registries/ExModulesTests.cs` (rewrite), `mods/exlib/tests/ModuleInit.cs`
  (assembly attribute, see below)

**Produces:** the types in the design block above except `ExModuleHost`; `ExModules.Order` for
Task 2's tests.

Spec:

- `ExModules.All` walks `AppDomain.CurrentDomain.GetAssemblies()` once, sorted by assembly full
  name, and builds an `ExModuleInfo` per assembly carrying `ExModuleAttribute`. Entry points are
  `ReflectionScan.GetCandidateTypes(asm)` filtered to `IExModule` implementors, sorted by full name.
  An implementor without a parameterless constructor is left out and produces an error line
  `module <id>: entry point <type> has no parameterless constructor; skipped` in every set that
  contains the module. Two assemblies declaring one id: the first by assembly name stands, the
  second produces `module <id> is declared by both <asmA> and <asmB>; <asmB> ignored`.
- An assembly without `[assembly: ExDomain]` is still a module; its classes key under the host's
  mod id through `EntityRegistry.RegisterAll`'s existing fallback. That is right for a mod's own
  second assembly and the wiki says a framework module declares its own domain.
- `For(host)` filters `All` by `Host` (ordinal, case-insensitive), runs `Order`, caches the result
  per host. `Order` is Kahn's algorithm with the ready set kept sorted by id; a module naming a
  `Requires` id that is not in the input is excluded with `module <id> requires <other>, which is
  not loaded; <id> is not driven`; a cycle excludes every module in it with one error naming them
  all, sorted by id.
- `Reset()` clears both caches (internal, tests).
- `IExModule` doc: an entry point of a module; the host runs it through the phases of its own
  `ModSystem` in module order; `AssetsLoaded` is where assets are readable and catalogue reads
  belong, **not** where definitions are contributed - that is `IExDefinitionContributor` (Task 3
  adds the cref; this task writes the sentence).

Tests (`ExModulesTests`, the test assembly declares `[assembly: ExModule("exlibtests", Host =
"exlibtest.host")]` in `ModuleInit.cs` and keeps no `ExDomain`):

```csharp
[Fact] public void Finds_industry_as_a_framework_module()
  // touch typeof(IndustryModule); ExModules.Reset(); For("exlib").Modules has Id "industry",
  // Host "exlib", EntryPoints containing typeof(IndustryModule), Errors empty
[Fact] public void Finds_this_assembly_as_a_module_of_its_declared_host()
  // For("exlibtest.host").Modules has Id "exlibtests"; For("exlib") does not
[Fact] public void Finds_nothing_for_a_mod_with_no_modules()
  // For("a-mod-with-no-modules") is (empty, empty)
[Fact] public void IsLoaded_answers_for_any_host()
  // IsLoaded("industry") and IsLoaded("exlibtests") true; IsLoaded("nothing") false
[Fact] public void Orders_by_requires_then_by_id()
  // Order([c requires b, a, b]) gives a, b, c
[Fact] public void A_missing_requirement_excludes_the_module_and_names_both()
  // Order([x requires y]) gives no modules and one error containing "x" and "y"
[Fact] public void A_cycle_excludes_every_member_and_names_them()
  // Order([a requires b, b requires a]) gives no modules and one error containing "a" and "b"
[Fact] public void An_entry_point_without_a_parameterless_constructor_is_reported()
  // a private sealed class NoCtorModule(int x) : IExModule in this file; For("exlibtest.host")
  // .Errors contains "NoCtorModule" and .Modules[0].EntryPoints does not contain it
```

Hand-built infos for the `Order` tests: `new ExModuleInfo { Id = "a", Host = "h", Requires = [],
Assembly = typeof(ExModulesTests).Assembly, EntryPoints = [] }`.

**Gate:** `bash scripts/exmod.sh build latest` warning-free; `bash scripts/exmod.sh test latest`
green (the new tests included); `ExModuleModSystem` and `ExModSystem` still compile against the
changed `For` by the end of this task, so adapt their calls minimally (the real rewrite is Task 2).

### Task 2: the host, the two drivers, registries per module assembly

**Files:**
- Create: `mods/exlib/src/Registries/ExModuleHost.cs`
- Modify: `mods/exlib/src/Registries/ExModSystem.cs`, `mods/exlib/src/Registries/ExModuleModSystem.cs`,
  `mods/exlib/src/ExpandedLibModSystem.cs` (the three boot lines leave `Start`),
  `mods/exlib/src/Registries/ExHarmony.cs`, `mods/exlib/src/Registries/Entities/EntityRegistry.cs`
  (`RegisterAll` keys under the assembly's declared domain), `mods/exlib/wiki/Supported-API.md`
  (`ExModuleHost` row; `ExHarmony` row mentions the id overloads)
- Test: `mods/exlib/tests/Registries/ExModuleHostTests.cs` (new),
  `mods/exlib/tests/Registries/ExModSystemTests.cs`, `mods/exlib/tests/Registries/ExHarmonyTests.cs`,
  `mods/exlib/tests/Registries/RegistrationKeyTests.cs`, `mods/exlib/tests/Registries/ExModuleModSystemTests.cs` (new)

**Consumes:** `ExModules.For`, `ExModuleSet`, `ExModuleInfo` from Task 1.
**Produces:** `ExModuleHost` as in the design block; `ExHarmony.PatchOnce(string harmonyId,
Assembly assembly)` and `ExHarmony.UnpatchAll(string harmonyId)`.

Spec:

- `ExModuleHost(Mod mod)` calls `ExModules.For(mod.Info.ModID)`. The constructor instantiates each
  module's entry points with `Activator.CreateInstance`; a constructor that throws is logged through
  `mod.Logger.Error` with the type name and that entry point is left out. Instances live in the
  host and nowhere else.
- `StartPre` first logs every line of `set.Errors` at Error level, once per host instance. Every
  phase runs the modules in set order and each module's entry points in their order, each call
  isolated the way `ExModules.Drive` isolates today (log, continue).
- `Start`, per module before its entry points: `ExConfig.LoadAll(api, asm)`,
  `EntityRegistry.RegisterAll(api, mod, asm)`, and when `PatchHarmony`,
  `ExHarmony.PatchOnce(info.HarmonyId, asm)`. `StartServerSide`: `CommandRegistry.RegisterAll(api,
  mod, asm)`. `StartClientSide`: `PreferenceRegistry.RegisterAll(api, mod, asm)` then
  `CommandRegistry.RegisterAll(api, mod, asm)`. `Dispose`: entry points' `Dispose`, then
  `ExHarmony.UnpatchAll(info.HarmonyId)` for every `PatchHarmony` module.
- `EntityRegistry.RegisterAll`: `string domain = asm.GetCustomAttribute<ExDomainAttribute>()?.Domain
  ?? mod.Info.ModID;` is what `_domainByAssembly` records and what `KeyFor` and the three
  `ExDefinitions.DiscoverAndRegister*` calls receive; `modId` stays for the log lines and for
  `RegisterBlockEntity` unless that method uses it for the key (check; it must not). Today a
  framework module hosted by exlib would key under `exlib` whatever its own domain says.
- `ExHarmony`: `PatchOnce(Mod, Assembly)` delegates to `PatchOnce(mod.Info.ModID, assembly)`; the
  guard becomes a static `HashSet<string>` of `"<id>::<assembly full name>"`, replacing
  `Harmony.HasAnyPatches(id)`, which also suppressed the uncategorised patches whenever a category
  had been applied first under the same id. `UnpatchAll(string)` removes the id's entries from both
  sets; `UnpatchAll(Mod)` delegates.
- `ExModSystem`: a private `ExModuleHost? _modules` created lazily (`_modules ??= new
  ExModuleHost(Mod)`) so a phase called without `StartPre` (tests do this) still works; every phase
  keeps its order of operations (config, entities, Harmony, then modules, then the hook; on the
  client preferences, commands, modules, hook). `Dispose` disposes the host, unpatches when
  `PatchHarmony`, nulls the field.
- `ExModuleModSystem` at 0.03: `StartPre` wires `ExDefinitions.Logger` and `EntityRegistry.Logger`,
  calls `ExlibValues.Load(api)`, sets `ExModules.FlagKey(m.Id)` to true in `api.World.Config` for
  every module in `ExModules.All` (skip when `World.Config` is null, and repeat the flag pass in
  `Start`, the idiom `ExModsModSystem.SetFlags` documents), then drives the host. The three lines
  leave `ExpandedLibModSystem.Start`; its `RegisterAll` of exlib's own assembly and the filler code
  stay. `ExModuleModSystem` logs one Notification per host at `StartPre`:
  `[exlib] modules hosted by <host>: a, b` or `none`.

Tests. In `ExModuleHostTests`, a `private sealed class RecordingModule : IExModule` in the test
assembly (which is a module of `exlibtest.host` since Task 1) appends each phase name to a static
`List<string> Phases` and adds itself to a static `List<RecordingModule> Created`; both cleared in
the test constructor. `FakeMod(id, logger)` as in the existing `ExModulesTests`.

```csharp
[Fact] public void Two_hosts_get_distinct_entry_point_instances()
  // new ExModuleHost(FakeMod("exlibtest.host")) twice; Created.Count == 2; NotSame(Created[0], Created[1])
[Fact] public void Runs_every_phase_in_order()
  // StartPre, Start, StartServerSide(world.Api), StartClientSide(world.ClientApi), AssetsLoaded,
  // AssetsFinalize, Dispose; Phases equals that sequence
[Fact] public void Start_registers_the_module_assemblys_classes_before_its_entry_points()
  // world.Api.Received(1).RegisterBlockClass(Arg.Is<string>(k => k.EndsWith("TestBlock")), ...)
  // where TestBlock is a [BlockRegister] class in this file; RecordingModule.Start observed
  // EntityRegistry.KeyFor("exlibtest.host", typeof(TestBlock)) already resolving
[Fact] public void A_throwing_entry_point_is_logged_and_the_rest_continue()
  // a ThrowingModule whose Start throws; Phases still records RecordingModule's Start;
  // logger.Errors contains "ThrowingModule"
[Fact] public void A_registration_only_module_still_registers_its_classes()
  // internal ctor with an ExModuleSet holding one info for this assembly with EntryPoints = [];
  // Start; Received RegisterBlockClass for TestBlock
[Fact] public void Resolution_errors_are_logged_at_StartPre()
  // internal ctor with ExModuleSet([], ["boom"]); StartPre; logger.Errors contains "boom"
[Fact] public void PatchHarmony_patches_under_the_module_id_and_unpatches_on_Dispose()
  // [Collection(ExHarmonyCollection.Name)]; info with PatchHarmony = true for this assembly and a
  // private HarmonyTarget/HarmonyTargetPatch pair as in ExModSystemTests; after Start
  // Harmony.GetPatchInfo(target).Owners contains "exlibtest.host.exlibtests"; after Dispose none
```

`ExModSystemTests` gains `Hosting_a_module_runs_it_through_the_mods_phases`: `NewSystem(FakeMod(
"exlibtest.host"))`, `Start` then `Dispose`; `RecordingModule.Phases` contains `"Start"` and
`"Dispose"` (and the existing `_domainByAssembly` cleanup covers the new registration).
`RegistrationKeyTests` gains `RegisterAll_keys_under_the_assemblys_declared_domain_not_the_mod`:
`EntityRegistry.RegisterAll(world.Api, FakeMod("notexlib"), typeof(IndustryModule).Assembly)` then
`world.Api.Received().RegisterBlockClass("exlib.BlockPipe", typeof(BlockPipe))`. `ExHarmonyTests`
gains `PatchOnce_is_once_per_assembly_and_id` (two calls, one prefix) and
`An_earlier_category_patch_does_not_suppress_the_uncategorised_ones`. `ExModuleModSystemTests`:
`StartPre_wires_loggers_and_sets_module_flags` (`ExDefinitions.Logger` and `EntityRegistry.Logger`
are `world.Api.Logger` afterwards; `world.Api.World.Config.GetBool(ExModules.FlagKey("industry"))`
is true - check how `TestWorld` exposes `World.Config` before writing this assertion) and
`Phases_drive_the_framework_modules` (`IndustryModule` observed: assert through a `RecordingLogger`
line or by the `ExBlockNames` qualifier `refractory` being registered after `StartPre`).

**Gate:** `build latest` warning-free; `test latest` green; `bash scripts/exmod.sh smoke` boots
with exlib, iiex, siex and the sample loaded and verifies clean, and the server log shows
`modules hosted by exlib: industry`.

### Task 3: definition contributors

**Files:**
- Create: `mods/exlib/src/Definitions/IExDefinitionContributor.cs`
- Modify: `mods/exlib/src/Definitions/ExDefinitions.cs` (`DiscoverContributors`, `Contributors`,
  `RunContributors`, `Clear`), `mods/exlib/src/Definitions/ExDefinitionModSystem.cs`,
  `mods/exlib/src/Registries/Entities/EntityRegistry.cs` (`RegisterAll` calls `DiscoverContributors`),
  `mods/exlib/industry/IndustryModule.cs`, `mods/exlib/src/Registries/IExModule.cs` (the cref),
  `mods/exlib/wiki/Supported-API.md` (`IExDefinitionContributor` under `ExpandedLib.Definitions`),
  `mods/exlib/wiki/Lifecycle.md` (the 0.04 row and the "def provider" ordering bullet)
- Test: `mods/exlib/tests/Definitions/DefinitionContributorTests.cs` (new)

**Consumes:** nothing new from Tasks 1-2 beyond the module being driven at 0.03.
**Produces:** `IExDefinitionContributor`; `ExDefinitions.DiscoverContributors(Assembly asm)`,
`ExDefinitions.Contributors` (`IReadOnlyList<Type>`), `ExDefinitions.RunContributors(ICoreAPI api)`.

Spec:

- `DiscoverContributors(asm)` records every concrete `IExDefinitionContributor` with a parameterless
  constructor in a static `List<Type>` (types, never instances), once per type; one without a
  constructor is logged through `ExDefinitions.Logger` as a warning naming the type and skipped.
  `Clear()` also clears the list. `EntityRegistry.RegisterAll` calls it after the three
  `DiscoverAndRegister*` calls.
- `RunContributors(api)` instantiates and runs each recorded type in list order, isolating each
  (`api.Logger.Error` with the type name; the rest still run), and logs one Notification with the
  count when it is not zero.
- `ExDefinitionModSystem.AssetsLoaded` calls `ExDefinitions.RunContributors(api)` right after its
  server check and before the process-route emission, so contributed definitions are audited and
  injected with the rest. `mods/exlib/testing/World/AssetLoading.cs` constructs the definition
  system for `TestWorld.LoadAssets`; contributors run there too, against real loaded assets, and the
  existing asset-loading tests must stay green.
- `IndustryModule` implements `IExModule, IExDefinitionContributor`: `StartPre` and `AssetsFinalize`
  unchanged, `AssetsLoaded` removed, `Contribute(api)` holds the metal-family emission without the
  side check (the definition system is server-only).
- `IExModule.AssetsLoaded`'s doc points at `IExDefinitionContributor` for definitions.
  `Lifecycle.md`'s 0.04 row says contributors run first; the ordering bullet "A def provider must
  be discovered in `Start`" gains the sentence that a contributor discovered in any `Start` runs at
  0.04 and is the place for asset-dependent definitions.

Tests (`DefinitionContributorTests`, `IDisposable` calling `ExDefinitions.Clear()` and the
`_domainByAssembly` cleanup):

```csharp
[Fact] public void RegisterAll_discovers_contributors_in_the_scanned_assembly()
  // a private sealed class TestContributor : IExDefinitionContributor registering
  // ExItemDef.Create(domain, "contributed") in Contribute; after
  // EntityRegistry.RegisterAll(world.Api, FakeMod("exlibtest.contrib"), typeof(...).Assembly)
  // ExDefinitions.Contributors contains typeof(TestContributor)
[Fact] public void The_definition_system_runs_contributors_before_injecting()
  // RegisterAll as above; new ExDefinitionModSystem().AssetsLoaded(world.Api);
  // ExDefinitions.Items contains a def with Code "contributed" and
  // world.Api.Assets.Received().Add(Arg.Is<AssetLocation>(l => l.Path.Contains("contributed")), Arg.Any<IAsset>())
  // (confirm world.Api.Side is Server in TestWorld; the system returns early otherwise)
[Fact] public void A_throwing_contributor_is_logged_and_the_rest_still_run()
  // a ThrowingContributor plus TestContributor; RunContributors(api) with a RecordingLogger on the
  // api; logger.Errors contains "ThrowingContributor"; Items still contains "contributed"
[Fact] public void A_contributor_without_a_parameterless_constructor_is_skipped_with_a_warning()
```

**Gate:** `build latest` warning-free; `test latest` green (`DefinitionGoldens` still replays the
metal family through `MetalFamilyEmitter` directly and is unaffected); `smoke` boots and the log
shows the injected metal-family item count unchanged from Task 2's run.

### Task 4: the sample module, shipped as its own mod

**Files:**
- Create: `samples/HelloModule/HelloModule.csproj`, `samples/HelloModule/modinfo.json`,
  `samples/HelloModule/src/AssemblyInfo.cs`, `samples/HelloModule/src/HelloModule.cs`,
  `samples/HelloModule/src/GreetingDef.cs`, `samples/HelloModule/src/Greetings.cs`,
  `samples/HelloModule/src/GreetingItems.cs`, `samples/HelloModule/src/BlockBehaviorGreeter.cs`,
  `samples/HelloModule/src/HelloModuleConfig.cs`, `samples/HelloModule/src/GreetSubCommand.cs`,
  `samples/HelloModule/assets/hellomodule/config/greetings/default.json`,
  `samples/HelloModule/assets/hellomodule/lang/en.json`,
  `samples/HelloModule.Tests/HelloModule.Tests.csproj`, `samples/HelloModule.Tests/ModuleInit.cs`,
  `samples/HelloModule.Tests/HelloModuleTests.cs`
- Modify: `samples/HelloExpanded/HelloExpanded.csproj` (project reference, `Private=false`),
  `samples/HelloExpanded/modinfo.json` (`"hellomodule": "1.0.0"` dependency),
  `samples/HelloExpanded/src/BlockHello.cs` (`.Behavior<BlockBehaviorGreeter>()`),
  `scripts/exmod/src.ps1` (`Get-ExmodTestProjects`: `hellomodule = 'HelloModule.Tests'` and its
  path under `$paths`), `VintageStory.sln` (`dotnet sln add` both projects beside the HelloExpanded
  entries)

**Consumes:** everything above.

Spec. The module gives any mod's block a greeting: a catalogue of greetings under
`config/greetings/` (any domain), one generated item per greeting, a block behaviour that sends a
greeting on interact, a config value, a server sub-command. Mod id, module id and domain are all
`hellomodule`; the csproj mirrors `HelloExpanded.csproj` (same props import, single TFM,
`AssemblyName` `hellomodule`, `RootNamespace` `HelloModule`, `AssetDomain` `hellomodule`, exlib and
the generators referenced the same way). No icon.

```csharp
// AssemblyInfo.cs
[assembly: ExDomain("hellomodule")]
[assembly: ExModule("hellomodule")]

// GreetingDef.cs - the JSON shape of one entry under config/greetings/, read with
// AssetCatalogueLoader.GetMany<GreetingDef>(api, "config/greetings/") the way Industry reads metals
public sealed class GreetingDef { public string Code { get; set; } = ""; public string Text { get; set; } = ""; }

// Greetings.cs - the loaded catalogue; populated on both sides at AssetsFinalize, identical on each
public static class Greetings { public static IReadOnlyList<GreetingDef> All { get; }  public static void Load(ICoreAPI api); }

// GreetingItems.cs - pure: one ExItemDef per greeting, code "greeting-<code>", vanilla shape and
// texture the way BlockHello uses vanilla art
public static class GreetingItems { public static IEnumerable<ExItemDef> Emit(string domain, IEnumerable<GreetingDef> greetings); }

// HelloModule.cs - the entry point
public sealed class HelloModule : IExModule, IExDefinitionContributor {
  public void Contribute(ICoreAPI api) {
    foreach (ExItemDef def in GreetingItems.Emit("hellomodule", AssetCatalogueLoader.GetMany<GreetingDef>(api, "config/greetings/")))
      ExDefinitions.RegisterItem(def);
  }
  public void AssetsFinalize(ICoreAPI api) => Greetings.Load(api);
}

// BlockBehaviorGreeter.cs - [BlockBehaviorRegister]; on a server-side interact sends
// HelloModuleValues.GreetingsPerClick greetings from Greetings.All to the player through
// IServerPlayer.SendMessage(GlobalConstants.GeneralChatGroup, ..., EnumChatType.Notification);
// handling stays PassThrough so the block's own handler runs

// HelloModuleConfig.cs - [ExConfigRegister("hellomodule.json", "hellomodule", Manageable = true)]
// with one [ExConfigRange(1, 10)] int GreetingsPerClick = 1, generated into HelloModuleValues

// GreetSubCommand.cs - [SubCommandRegister(Side = EnumAppSide.Server)] "/exmod greet" printing
// Greetings.All.Count through a hellomodule lang key
```

`default.json` holds two greetings; `en.json` names both items and the command. `BlockHello`'s
definition gains `.Behavior<BlockBehaviorGreeter>()`, which resolves to
`hellomodule.BlockBehaviorGreeter` through the module's `ExDomain` - the cross-assembly key path
this sample exists to prove.

The experiment this task settles: the module ships with **no `ModSystem`**. Build, then
`bash scripts/exmod.sh smoke` and read the server log. If the game loads `hellomodule` and the
0.03 driver logs `modules hosted by exlib: hellomodule, industry`, the finding is "a Code mod
needs no ModSystem", recorded in Progress below and repeated on the wiki in Task 5. If the game
refuses or skips the mod, add `public class HelloModuleModSystem : ModSystem { }` to the sample,
record that finding instead, and Task 5 says the template carries one.

Tests (`HelloModuleTests`; `ModuleInit` mirrors `HelloExpanded.Tests` and additionally touches
`typeof(HelloModule.HelloModule)` so the assembly is loaded before discovery runs):

```csharp
[Fact] public void Is_discovered_as_a_framework_module()
  // ExModules.For("exlib").Modules has Id "hellomodule" with EntryPoints containing typeof(HelloModule)
[Fact] public void Emits_one_item_per_greeting()
  // GreetingItems.Emit("hellomodule", [a, b]) yields codes "greeting-a", "greeting-b"
[Fact] public void Registers_its_behaviour_under_its_own_domain()
  // new ExModuleHost(FakeMod("exlib")).Start(world.Api);
  // world.Api.Received().RegisterBlockBehaviorClass("hellomodule.BlockBehaviorGreeter", typeof(BlockBehaviorGreeter))
```

**Gate:** `build latest` warning-free (HelloExpanded builds HelloModule through the project
reference); `test latest` now runs four lanes, all green; `smoke` boots with exlib, iiex, siex,
hellomodule and helloexpanded, verifies clean, logs the module list, and the injected-item count
includes the two greeting items; `exmod verify` reports no dangling code for `greeting-*`.

### Review checkpoint

One `architect` review of Tasks 1-4 together, scoped to the contract: the public surface in the
design block and its XML docs, static state (nothing may hold an instance), the ordering claims
against `mods/exlib/wiki/Lifecycle.md` and the vendored loader facts, and whether the sample proves
the third-party form or merely compiles. Findings go back to a `builder`; no second review unless a
finding is Critical.

### Task 5: the prose

**Files:**
- Create: `mods/exlib/wiki/Modules.md`
- Modify: `mods/exlib/wiki/Registries.md` ("Shipping more than one assembly" becomes a pointer to
  Modules), `mods/exlib/wiki/Lifecycle.md` (the 0.03 row), `mods/exlib/wiki/Code-First-Definitions.md`
  (contributors), `mods/exlib/wiki/Getting-Started.md` (the sample module, one paragraph),
  `mods/exlib/wiki/Home.md` (one bullet), `mods/exlib/wiki/_Sidebar.md` (Modules under Registration),
  `mods/exlib/README.md` (a Modules paragraph under "What it provides"), `mods/exlib/CHANGELOG.md`
  (`[Unreleased]`), `docs/design/conventions.md` ("How exlib is laid out": `samples/` holds two,
  the mod and the module), `docs/internal/README.md` (this plan's row: complete),
  `docs/internal/plans/2026-09-06-repo-restructure.md` (E6 status line: landed),
  `docs/internal/worklog/2026-09.md` (one entry for the plan)

`Modules.md` covers, in this order: what a module is and why it exists (the one-ModSystem-dll rule
and the extension story, Industry first); the two attributes; the two shipping forms and the
duplicate-assembly rule, with the Task 4 finding about a `ModSystem`; the phase table (host phase,
what the host does for the module, the entry-point hook); `IExDefinitionContributor`; `Requires`
and the error you get; `ExModules.IsLoaded` and the `exlib:module:<id>` world-config flag for JSON
patch conditions; `PatchHarmony`; Industry as the worked example (its `AssemblyInfo.cs` and
`IndustryModule.cs` quoted); the sample module; and a closing note for third parties that a module
package publishes under its own id, not `ExpandedLib.*`. Plain ASCII, neutral voice, no decision
history.

**Gate:** `test latest` green (`WikiParityTests`, `PublicSurfaceTests`, `HarnessSurfaceTests` are
the check); the wiki link check that `exmod check` runs is clean.

---

## Progress

- Task 1 (identity, discovery and ordering) done 2026-09-06. Gate green.
