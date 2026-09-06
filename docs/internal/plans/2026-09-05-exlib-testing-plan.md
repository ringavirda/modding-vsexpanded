# exlib testing plan - the harness as a generic kit for Vintage Story mods

> **For agentic workers:** execute task by task with a fresh implementer per task; the gate in each
> task is the check. No review pass per task.

**Status** complete 2026-09-06 (every task landed, uncommitted; written 2026-09-05). Implements
[2026-09-05-exlib-testing-assessment.md](2026-09-05-exlib-testing-assessment.md) items 1 to 13 under
the default rulings TR1 (the server smoke lane before in-process asset loading), TR2 (ship the
publicizer with provisioning), TR3 (NuGet for the harness with the framework boundary), TR4 (lifted
patterns live in the harness). Facts: the current-version gate runs 4,744 tests in 26 seconds; the
Linux dedicated server boots headless with exlib and iiex in 91 seconds and stops on `/stop`.

**Goal:** a modder with no test at all can add a passing test in ten minutes, and a modder with only
JSON gets a real check without writing code.

**Architecture:** the harness stays content-free (`ExpandedLib.Testing`): rigs, doubles, validators
as wrappers over `ExpandedLib.Checks`, a template, a CI template. Real-engine checks run in a
dedicated-server smoke lane driven by `scripts/exmod.ps1`. In-process asset loading is a time-boxed
spike at the end.

**Tech stack:** xUnit, NSubstitute, `ExpandedLib.Testing`, pwsh 7 (`scripts/exmod.ps1`), GitHub
Actions, the dedicated server from `cdn.vintagestory.at`.

**Spec:** the assessment above; [../testing.md](../testing.md) for the homing and folder rules (to be
rewritten in Task T5).

## Progress

- **T1 and T3 landed 2026-09-06** (nine lanes green; exlib 2247, iiex 2453, siex 337 tests):
  `MachineRig` (`RunUntil`, `RunLive`, `RunWhile`; seven fixture loops converted, the three furnace
  rigs keep their richer self-typed drivers), `RegistryLawScanner`, `ResourceInvariant`,
  `StaticStateCollection` with the every-collection-has-a-definition guard in all three suites,
  `EnginePlant` as a base for the three plant fixtures; `RepoPaths.Register` with the
  `mods/<domain>` fallback, `ReleasedHistory` seeded from each mod's test `ModuleInit`, coverage
  floors in `infra/test/coverage-floors.json`, `exmod codes <mod>` through
  `infra/tools/BlockCodeEmitter` (byte-identical to the committed tables; `EXLIB_WRITE_BLOCKCODES`
  removed). The layering was then corrected: `ModinfoTests` moved to siex's suite, and the test
  project graph is exlib.tests -> harness; iiex.tests -> harness; siex.tests -> harness, iiex.tests,
  exlib.tests (the top of the chain replays every mod's release history). Counts after: exlib 2244,
  iiex 2453, siex 340.
- **T2 landed 2026-09-06** (nine lanes green; exlib 2252): `TestPlayer` (an `IServerPlayer`
  substitute with a real hotbar slot and a settable sneak, working on all three lanes because
  provisioning publicises the API dll), `TestInventory`, `TestModLoader` (with the three aliases
  mods probe by reflection), `WorldConfigBag`, `ModConfigFiles` (through an NSubstitute call
  handler, since open generic members cannot be matched with argument wildcards), `RecordingLogger`
  (a real `LoggerBase`); `TestWorld` exposes them as `Log`, `Config`, `Mods`, `ConfigFiles`,
  `Player()` and is now `IDisposable`. Thirteen hand-rolled player substitutions remain in the mods'
  fixtures.
- **T6 landed 2026-09-06**: `exmod smoke` boots the Linux dedicated server (`.game/1.22-server`;
  the owner's Windows client install at `.game/1.22` is left alone) on port 42499 with the built mods,
  waits for "now running", runs `/exmod verify`, stops, and fails on any `[Error]`/`[Fatal]` or a
  verify summary with errors; `provision game -Kind server` fetches the platform's server tarball and
  `-Dest` honours absolute paths; a `smoke` job in CI and `templates/ci/smoke.yml`. Exlib alone boots
  clean in about 19 seconds. The full tree failed the lane on its first run, for real reasons a fix
  task is now addressing: the lang check ran over vanilla's domain, siex compat patches target iiex
  JSON files that became code-first, and the multiblock check reports dangling codes for shipped
  furnaces that need a check-versus-content verdict.
- **Smoke findings fixed 2026-09-06** (full tree green in the lane; exlib 2259): `AssetCheckSource`
  scopes the in-game checks to exlib and the mods that depend on it; `LangCoverageCheck` guards `en`
  only (locale parity stays a repository guard); siex's compat patches now target the synthetic
  locations the code-first pipe definitions declare (27 patch errors to 0, a real content defect);
  the 21 dangling multiblock codes were a check defect (`MultiblockCodesCheck` only matched a trailing
  wildcard), fixed with segment-wise matching and tests.
- **T7 and T8 landed 2026-09-06** (ten lanes green; exlib 2265): `HarmonyFixture` on `ExHarmony`
  (`IsPatched`, `PatchedMethods`, category form) proven on a test type and on the server-safe
  `CollectibleObject.GetHeldItemName`; `TestChannels` (a server and client pair serialising through
  `SerializerUtil`, delivering synchronously) handed out by `TestWorld.Channels(name)` and by
  `Api.Network.RegisterChannel` on both API views, so `ExConfigSyncModSystem` is tested unchanged.
- **T11 and T12 landed 2026-09-06** (ten lanes green; exlib 2272): internal seams
  `DriveProductionTick`, `DriveIdleTick` (both production bases), `DriveMonitorTick`,
  `ApplyStructureRotation` (structures), `SetNetworkTypeForTest`, `ApplyOrientationForTest`
  (network nodes); siex's tests see exlib internals; reflection call sites fell from 499 to 420
  (the fifteen remaining `OnServerTick` reflections are mod-private tick callbacks, not base
  members, so they stay); the two oversized test files became fourteen files under 300 lines with
  fixture files beside them; siex's `HeatBalanceTests` joins the furnace-config collection.
- **T10 landed 2026-09-06** (ten lanes green; exlib 2332 on 1.22): ten new test classes over the
  command tree, preferences, recipe profiles, config attributes, right-click construction,
  migrations (removal, item codes, sweeper, discovery order), the legacy shims (legacy lanes only)
  and the handbook unit patch through `HarmonyFixture`; four documented dispatch seams on the
  remaining sub-commands; the untested contract surface fell from 64 to 42 types, with a soft report
  in `PublicSurfaceTests` capped at 60. Left as findings: `ExPreferences` is silent on an unknown
  key; the chunk-column sweep loop reads a `WorldManager` the fake world does not model.
- **T4 and T5 landed 2026-09-06** (ten lanes green; exlib 2394): the `dotnet new exlib-tests`
  template (verified end to end against the built harness), `templates/ci/tests.yml`,
  `.github/workflows/release.yml` (gate, Cake packages, `dotnet pack`, release assets, a commented
  NuGet push), pack metadata on both libraries with the version read from `modinfo.json` (the
  packages hold the dll, the XML docs and a README only; `PackageLicenseExpression` is unset because
  the repository has no LICENSE file - an owner decision), testing.md and both wiki testing pages
  rewritten in the three-rung order, and `HarnessSurfaceTests` guarding the API reference.
  Found: `dotnet new` corrupts an MSBuild `Condition` that names a template symbol; the template
  avoids the pattern.
- **T9 landed 2026-09-06** (eleven lanes green; the tool's eleven tests are a lane of their own):
  `exlib-verify <modpath> [--game] [--mods] [--json] [--strict]` under infra/tools/ExlibVerify,
  packable as a dotnet tool and packed by the release workflow; it parses every JSON file, resolves
  and applies patches through the game's own `Tavis.JsonPatch`, checks handbook lang keys and recipe
  codes with headless variant expansion for inline groups (a `loadFromProperties` group is an
  informational unresolved prefix, never an error), folds `survival` and `creative` into the `game`
  domain as the asset manager does, and scopes every check to a mod's real asset domains (its mod id
  can differ from its folder). Over the reference mods it found real undeclared cross-mod patch
  targets in industrialstory.
- **T13 landed 2026-09-06** (eleven lanes green; exlib 2403): the spike succeeded, so
  `TestWorld.LoadAssets(modPath, gamePath)` exists in the harness: the game's own `AssetManager`,
  `Lang`, `ClassRegistry`, the tag registries (1.22) and `ModRegistryObjectTypeLoader` run in
  process over a mod's asset folder and yield real class-resolved, variant-resolved blocks and items
  (the sample's block with its four facings is the test); the call sequence, the walls that were not
  walls, and one unexplained dependency on `protobuf-net` are recorded in
  `docs/internal/research/2026-09-06-asset-loading-spike.md`. The testing plan is complete.

- **Earlier, from the framework plan:** G2 laid out the harness (World, Scenes, Rigs, Doubles,
  Checks, Repo); H2 added `SceneGrid` and `LayoutTable`; D4 made the validators wrappers over
  `ExpandedLib.Checks`; V1 added `TreeKeys` and the tree-key goldens.

## The convenience rule (owner, 2026-09-05)

If using the kit takes work, nobody uses it. Every fixture has a zero-config entry (`TestWorld.Create()`
with nothing else; a rig that finds the block's own definition), a declarative form (the scene
diagram, attributes on the test class) and the explicit API for scale. The template project must run
green with one command after `dotnet new`; a first machine test is under ten lines. The harness tree
is reorganised first (framework plan Task G2) so every later task lands in a designed folder.

## Global constraints

Those of the framework plan, plus: `ExpandedLib.Testing` references no content mod; a fixture never
forces `StructureComplete`; tests drive the fake clock, never wall time; every process-global static
touched by a test is joined to a named collection.

---

### Task T1: lift the stranded patterns into the harness

**Files:**
- Create: `mods/exlib/testing/MachineRig.cs`, `RegistryLawScanner.cs`, `ResourceInvariant.cs`,
  `StaticStateCollection.cs`.
- Modify: `mods/iiex/tests/Fixtures/FurnaceLayoutRig.cs` and the boiler and engine rigs to derive from
  `MachineRig` where their `RunUntil`/`RunLive` bodies are the generic ones; `FurnaceBranchGuards`
  to use `RegistryLawScanner`; `PipeInvariantTests` to use `ResourceInvariant`.
- Test: `mods/exlib/tests/Harness/MachineRigTests.cs`, `RegistryLawScannerTests.cs`,
  `ResourceInvariantTests.cs`, `StaticStateCollectionTests.cs`.

**Interfaces:**
```csharp
namespace ExpandedLib.Testing;
/// Drives one machine on a TestWorld to a condition rather than to a fixed offset.
public abstract class MachineRig(TestWorld world) {
  public TestWorld World { get; } = world;
  /// Advances the world in steps of stepSeconds until the predicate holds; returns the seconds
  /// elapsed; throws TimeoutException naming the ceiling when it never holds.
  public float RunUntil(Func<bool> until, float ceilingSeconds, float stepSeconds = 1f);
  /// Advances the world for the given seconds, calling the observer after every step.
  public void RunLive(float seconds, Action<float>? observer = null, float stepSeconds = 1f);
  /// Runs the action before every step for the given seconds: the "hold a source at a level and
  /// step" loop thirteen fixtures write by hand today.
  public void RunWhile(Action beforeEachStep, float seconds, float stepSeconds = 1f);
}
/// Applies one law to every concrete subclass of TBase in the loaded assembly closure.
public static class RegistryLawScanner {
  public static IReadOnlyList<Type> ConcreteSubclasses<TBase>();
  public static void ForEach<TBase>(Action<Type> law);   // aggregates failures into one message
}
/// A randomised operation-sequence invariant over a resource with produce, consume and tick moves.
public sealed class ResourceInvariant<TState> {
  public ResourceInvariant(Func<TState> fresh, IReadOnlyList<Action<TState>> moves, Action<TState> assert);
  public void Run(int sequences = 5, int movesPerSequence = 50, int seed = 1);  // reports the failing sequence
}
/// The base for a [CollectionDefinition]: every test class touching a process-global static joins it.
public abstract class StaticStateCollection { }   // documented pattern plus a guard test helper
```
`StaticStateCollectionTests` includes the reusable guard `EveryCollectionNameHasADefinition(Assembly)`
that each suite calls.

- [ ] **Step 1: tests** for each type in `mods/exlib/tests/Harness/` (a rig over `TestProductionMachine`, a scanner over a test base with two leaves, an invariant that catches a planted bug, the collection guard failing for a bare name).
- [ ] **Step 2-4:** implement, adopt in iiex where mechanical, gate `bash scripts/exmod.sh test latest`.
- [ ] Also in iiex's fixtures (not the harness: it names `BlockEntityEngine`, `PipeNetwork`, `RccFake`):
  promote `EnginePlant` to a base `EnginePlant(Scene, BlockPos, Block engineBlock, BlockEntity engine)`
  with `Steam`, `RunWithSteam`, `InletVolume`, so `WaterPumpPlant` (iiex) and `MPGeneratorPlant`,
  `AirBlowerPlant` (siex) stop repeating its eight steps.

### Task T2: supported doubles

**Files:**
- Create: `mods/exlib/testing/Doubles/TestPlayer.cs`, `TestInventory.cs`, `TestModLoader.cs`,
  `WorldConfigBag.cs`, `ModConfigFiles.cs`, `RecordingLogger.cs`.
- Modify: `mods/exlib/testing/TestWorld.cs` (wire `Api.Logger` and `World.Logger` to a
  `RecordingLogger`, `World.Config` to a `WorldConfigBag`, `Api.ModLoader` to a `TestModLoader`,
  `Api.LoadModConfig/StoreModConfig` to `ModConfigFiles` under a temp dir; expose them as properties),
  `mods/exlib/tests/Fixtures/ExOrientableRig.cs` (use `TestPlayer`).
- Modify: `scripts/exmod.ps1` (`Publicize-GameApi` runs for every provisioned kind and the wiki
  documents it), `mods/exlib/wiki/Testing-Harness.md`.
- Test: `mods/exlib/tests/Harness/DoublesTests.cs`.

**Interfaces:**
```csharp
namespace ExpandedLib.Testing.Doubles;
/// A player with a real hotbar: the active slot holds whatever the test puts there.
public sealed class TestPlayer {
  public static TestPlayer Create(TestWorld world, string uid = "test");
  public IPlayer Player { get; }                 // substitute, publicised IPlayer
  public ItemSlot ActiveSlot { get; }            // real ItemSlot
  public void Hold(ItemStack? stack);
  public bool Sneaking { get; set; }
}
public sealed class TestInventory { public static InventoryGeneric Of(TestWorld world, int slots, string id = "test"); }
/// Answers IsModEnabled and the three aliases some mods probe by reflection.
public sealed class TestModLoader : IModLoader {
  public void Add(string modId, string version);
  public bool IsModLoaded(string modId); public bool HasMod(string modId); public bool HasModId(string modId);
}
public sealed class WorldConfigBag { public ITreeAttribute Tree { get; } }
public sealed class ModConfigFiles { public string Directory { get; } public void Write(string file, object value); public T? Read<T>(string file); }
public sealed class RecordingLogger : ILogger { public IReadOnlyList<(EnumLogType Type, string Message)> Entries { get; } public IEnumerable<string> Errors { get; } }
```

- [ ] **Step 1: tests**: hold a stack and read it back through `IPlayer.InventoryManager.ActiveHotbarSlot`; a config class saved and loaded through `Api.LoadModConfig`; `IsModEnabled` and each alias; a logged error retrievable.
- [ ] **Step 2-4:** implement, gate, wiki.

### Task T3: unbind the edges

**Files:**
- Modify: `mods/exlib/testing/RepoPaths.cs` (`DomainToMod` falls back to `domain == mod`; `Register(domain, modFolder)`),
  `ReleasedCodes.cs`, `ReleasedVersions.cs`, `ReleasedCodeDebt.cs` (become a `ReleasedHistory`
  registry: `ReleasedHistory.Register(string mod, IReadOnlyList<string> codes, IReadOnlyDictionary<string,string> versions, IReadOnlyList<string> debt)`;
  the family's data moves into each mod's test `ModuleInit`), `infra/tools/coverage_gate.py`
  (floors from `infra/test/coverage-floors.json`), `.github/workflows/tests.yml` (pass the file).
- Create: `infra/test/coverage-floors.json`, `infra/tools/BlockCodeEmitter/` (console project
  referencing the harness; `scripts/exmod.ps1 codes <mod>` builds the mod, runs it, writes
  `mods/<mod>/src/Generated/<Mod>Blocks.g.cs`, rebuilds), remove the env-var path from
  `mods/exlib/testing/BlockCodeEmitter.cs` (the class stays as the library the tool calls).
- Test: existing tests; `mods/exlib/tests/Harness/RepoPathsTests.cs`.

- [ ] **Step 1: tests**: an unknown domain resolves to `mods/<domain>`; a registered mapping wins; `ReleasedHistory` answers per mod.
- [ ] **Step 2-4:** implement, `exmod codes iiex` reproduces the committed table byte for byte, gate.

### Task T4: template, CI template, release workflow, packages

**Files:**
- Create: `templates/exlib-tests/.template.config/template.json`, `templates/exlib-tests/YourMod.Tests.csproj`,
  `ModuleInit.cs`, `Blocks/FirstBlockTests.cs` (one `TestWorld` test), `Definitions/GoldenTests.cs`,
  `Invariants/ShippedAssetJsonTests.cs`; `templates/ci/tests.yml` (the provisioning and test job
  for a third-party repo); `.github/workflows/release.yml` (on tag `v*`: `test latest`, Cake
  `Package` and `PackageTesting`, `dotnet pack` for `ExpandedLib.csproj` and
  `ExpandedLib.Testing.csproj`, upload zips and nupkgs as release assets; the NuGet push step is
  present and commented with the secret name it needs).
- Modify: `mods/exlib/src/ExpandedLib.csproj`, `mods/exlib/testing/ExpandedLib.Testing.csproj`
  (`PackageId`, `Version` from `modinfo.json`, `Authors`, `Description`, `PackageLicenseExpression`,
  `RepositoryUrl`, `IsPackable`), `mods/exlib/wiki/Getting-Started.md` and `Testing-Harness.md`
  ("dotnet new exlib-tests", NuGet reference once published).

- [ ] **Step 1:** `dotnet new install ./templates/exlib-tests && dotnet new exlib-tests -n Demo.Tests` produces a project that builds against the built harness and passes.
- [ ] **Step 2:** `dotnet pack` for both projects succeeds locally; the workflow lints (`actionlint` if present, else a dry read).
- [ ] **Step 3:** gate.

### Task T5: the testing docs

- [ ] Rewrite [../testing.md](../testing.md) for the three projects (`ExpandedLib.Tests`,
  `IronIndustryExpanded.Tests`, `SteelIndustryExpanded.Tests`) and the per-mod paths; keep the
  homing rule, folders, fixture suffixes, the two quiet traps, the collection rule.
- [ ] Rewrite `mods/exlib/wiki/Testing-Harness.md` and `Testing-API-Reference.md` for `MachineRig`,
  the doubles, `RegistryLawScanner`, `ResourceInvariant`, the collections, the template, the smoke
  lane, `ExlibChecks`; the six exemplary files become the "Examples" section:
  `NetworkGraphTests`, `StructureRigTests`, `ProductionMachineTests`, `ConfigMigrationTests`,
  `ExlibDefinitionGoldenTests`, `ShippedAssetJsonTests`.
- [ ] `bash scripts/exmod.sh test latest -Filter WikiParity` green.

### Task T6: the dedicated-server smoke lane

**Files:**
- Modify: `scripts/exmod.ps1`: `provision game` on Linux and macOS fetches the server tarball for
  `-Kind server` (the Windows client package lacks `libe_sqlite3.so` and `libSkiaSharp.so`), `-Dest`
  accepts an absolute path (today an absolute path is joined onto the repo root), and a new command
  `exmod smoke [-Version <x.y>] [-Mods <dir>[,<dir>]] [-Timeout 180]`: provisions if needed, copies the
  built mods (`mods/*/src/bin/Debug/Mods/mod`) or the given dirs into a scratch mods folder, boots
  `VintagestoryServer.dll --dataPath <scratch> --addModPath <mods>`, waits for
  `Dedicated Server now running` in `Logs/server-main.log`, sends `/exmod verify` then `/stop` on
  stdin, and exits non-zero when the log holds `[Error]` or `[Fatal]` or the verify summary reports
  errors; prints the notification lines that start with `[exlib]`.
- Modify: `.github/workflows/tests.yml`: job `smoke` after `test`, cached `.game` server install,
  runs `exmod smoke` for exlib+iiex+siex and for `samples/HelloExpanded`.
- Create: `templates/ci/smoke.yml`.
- Test: `scripts/exmod.ps1 smoke` locally: exit 0 with the current tree; exit non-zero when a mod
  folder carries a deliberately broken `modinfo.json` (manual check recorded in the task note).

- [ ] Implement, run locally, gate.

### Task T7: a Harmony fixture

**Files:**
- Create: `mods/exlib/testing/HarmonyFixture.cs`; test `mods/exlib/tests/Harness/HarmonyFixtureTests.cs`
  with a test patch on `Vintagestory.API.Common.CollectibleObject.GetHeldItemName`.

**Interfaces:**
```csharp
namespace ExpandedLib.Testing;
/// Applies a mod's Harmony patches once per test assembly and reverts them on dispose.
public sealed class HarmonyFixture : IDisposable {
  public HarmonyFixture(string modId, Assembly patches, string? category = null);
  public Harmony Harmony { get; }
  public bool IsPatched(MethodBase original);
  public void Dispose();   // UnpatchAll(modId)
}
```

- [ ] Tests: patched and reverted; a second fixture with the same id does not double-patch; the static-constructor hazard documented in the class comment with `BlockEntityAnvil` as the example.
- [ ] Implement, gate, wiki section "Testing Harmony patches".

### Task T8: a packet round-trip fixture

**Files:**
- Create: `mods/exlib/testing/Doubles/TestChannels.cs`; test `mods/exlib/tests/Harness/TestChannelsTests.cs`.

**Interfaces:**
```csharp
namespace ExpandedLib.Testing.Doubles;
/// A paired client and server channel that serialises through the game's own serialiser and
/// delivers synchronously to the registered handlers.
public sealed class TestChannels {
  public static TestChannels Create(TestWorld world, string channelName);
  public IServerNetworkChannel Server { get; }
  public IClientNetworkChannel Client { get; }
  public IReadOnlyList<object> SentToServer { get; }
  public IReadOnlyList<object> SentToClients { get; }
}
```

- [ ] Tests: a `[ProtoContract]` packet registered on both sides round-trips with the same field values; an unregistered type throws naming it.
- [ ] Implement, gate, wiki.

### Task T9: `exlib-verify`

**Files:**
- Create: `infra/tools/ExlibVerify/ExlibVerify.csproj` (packable as a dotnet tool `exlib-verify`),
  `Program.cs`, `Checks/*.cs`; wiki `Checks.md` section "Without the game".

**Behaviour** over a mod folder (`exlib-verify <modpath> [--game <install>] [--mods <dir>...]`):
every JSON parses; every patch `file` target exists in the mod, the game assets or a given mod;
every patch op applies to the target document (the game's own `JsonPatch` classes from
`VintagestoryAPI.dll`); every `dependsOn` mod id and `condition.when` key is reported (informational);
every lang key referenced by `config/handbook/*.json` exists in `lang/en.json`; recipe ingredient and
output codes resolve against block and item codes declared in JSON across the mod, the game and the
given mods, with variant expansion through the game's `RegistryObjectType` resolution when it can be
run headlessly and a prefix match with an "unresolved prefix" verdict otherwise. Non-zero exit on any
error; `--json` output for CI.

- [ ] Tests: `infra/tools/ExlibVerify.Tests/` with fixture mod folders: one clean, one with each error.
- [ ] Implement time-boxed; a check that cannot be made reliable is reported as informational, never as a false error.

### Task T10: the untested surface

**Files:** new test files under `mods/exlib/tests/`: `Commands/ExmodCommandTests.cs` (a substituted
`IChatCommandApi` whose `Create` returns a substituted `IChatCommand`; asserts the registered names
and that each sub-command handler runs), `Registries/PreferencesTests.cs`,
`Blocks/ConstructionTests.cs` (`ExRightClickConstructable` stages on `TestWorld`),
`Recipes/RecipeProfilesTests.cs`, `Config/ConfigAttributesTests.cs` (range enforcement from
`[ExConfigRange]`), `Migrations/BlockRemovalTests.cs`, `Migrations/ItemCodeMigrationTests.cs`,
`Migrations/ChunkSweeperTests.cs`, `Legacy/LegacyLinqTests.cs`, and `HandbookUnitPatchTests.cs`
through `HarmonyFixture` if the client type loads headlessly, else through `ExMeasure` alone.

- [ ] Each new class has at least one behaviour test per public member of the type it covers; gate; the untested-surface count (`PublicSurfaceTests` gains a soft report) falls below 60.

### Task T11: seams instead of reflection

**Files:**
- Modify: `mods/exlib/src/Blocks/Machines/BlockEntityProductionMachine.cs` (`internal void DriveProductionTick()`),
  `BEBehaviorProductionMachine.cs` (same), `mods/exlib/src/Blocks/Networks/BlockEntityNetworkNode.cs`
  (`internal void SetNetworkTypeForTest(string)`, `internal void ApplyOrientationForTest(string)`),
  `BlockNetworkNode.cs` (`internal` orientation setter), `mods/exlib/src/Blocks/Structures/BlockEntityMultiblockStructure.cs`
  (`internal void DriveMonitorTick()`), `mods/exlib/src/InternalsVisibleTo.cs` (add `SteelIndustryExpanded.Tests`).
- Modify: the call sites of the five most-reflected members in `mods/iiex/tests` and `mods/siex/tests`
  (`Orientation`, `Type`, `OnProductionTick`, `OnServerTick`, `UpdateStructureRotation`) to use the seams.

- [ ] `grep -rc "ReflectionHelpers\." mods/iiex/tests mods/siex/tests` falls by at least 150 sites; gate.

### Task T12: collections and file splits

- [ ] `[CollectionDefinition]` classes for `MetalRegistry`, `MaterialRoles`, `ExLiquids`, `ExDefinitions` in `mods/exlib/tests/Fixtures/`; siex's `HeatBalanceTests` joins `FurnaceConfigCollection`'s equivalent; the `EveryCollectionNameHasADefinition` guard from T1 runs in all three suites.
- [ ] Split `mods/exlib/tests/Structures/MultiblockCellRolesTests.cs` and `Networks/NetworkMembershipTests.cs` along their regions into files of under 300 lines; gate.

### Task T13: in-process asset loading (time-boxed spike)

- [ ] In `mods/exlib/testing/AssetLoading.cs`, attempt: construct the game's `AssetManager` over
  `.game/<slug>/assets` plus a mod folder, run the JSON patch loader, and resolve block types through
  `ModRegistryObjectTypeLoader` against `TestWorld`'s substituted server API. Record in
  `docs/internal/research/2026-09-XX-asset-loading-spike.md` what constructed, what needed a real
  server, and the exact exception at the first wall. Keep the working part only if
  `TestWorld.LoadAssets(modPath)` yields real `Block` instances with resolved variants; otherwise
  leave the note and no code.

## Order

T3, T1, T2, T12, T5 first (small, unblock the rest; T5 alongside code). Then T6, T7, T8. Then T10,
T11, T4. Then T9, T13. Gate after each: `bash scripts/exmod.sh test latest`; after T4 and T6 also
the smoke lane.
