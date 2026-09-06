# Research snapshot - asset-loading spike

**Written** 2026-09-06 by a research/build agent, against branch `ironmaking-expanded`. **Covers**
Task T13 (`docs/internal/plans/2026-09-05-exlib-testing-plan.md`): can a test load a mod's real JSON
assets through the game's own asset manager, patch loader and object loader, inside `TestWorld`,
without a running server?

**Verdict: yes.** The spike reached a real, class-resolved, variant-resolved `Block` for both a
code-first mod (HelloExpanded) and vanilla's own base blocktypes, entirely in-process. Kept as
`TestWorld.LoadAssets(modPath, gamePath?)` in `mods/exlib/testing/World/AssetLoading.cs`, one test in
`mods/exlib/tests/Harness/AssetLoadingTests.cs`, wiki section "Real assets" in
`Testing-Harness.md`. Effort spent: about 90 minutes wall time (spike + harness + multiversion fix).

## The scratch project

A throwaway console app under the scratchpad, referencing `VintagestoryAPI.dll`,
`VintagestoryLib.dll`, `VSEssentials.dll`, `VSSurvivalMod.dll`, `0Harmony.dll`,
`Newtonsoft.Json.dll` and `protobuf-net.dll` from `.game/1.22-server` (all `Private=true` so the
build output is self-contained), plus the NSubstitute NuGet package. A `dump`/`findimpl`/`findtag`
command mode used `System.Reflection` (constructors/methods/fields/properties, and
interface-implementation search catching `ReflectionTypeLoadException`) to read the installed
assemblies' shapes without a decompiler, since `VintagestoryLib`/`VSEssentials` are not vendored as
source (only the public `vsapi`/`vssurvivalmod`/`vsessentialsmod` repos are, per
`docs/internal/vanilla/`).

## The working call sequence

1. `new Vintagestory.Common.AssetManager(gamePath + "/assets", EnumAppSide.Server)`, then
   `InitAndLoadBaseAssets(logger)` - loads only `assets/game/` (the `GameOrigin` the constructor
   wires internally); `assets/survival` and `assets/creative` are NOT scanned by this call despite
   being folders under the same root. Confirmed by dumping `AssetManager.Origins` after the call:
   one `GameOrigin` pointed at `assets/game/`.
2. To add a mod's own domain: `new Vintagestory.Common.PathOrigin(domain, fullPath)`, then for each
   `AssetCategory` of interest, `origin.GetAssets(category, shouldLoad: true)` and
   `mgr.Add(asset.Location, asset)` for each. **`AssetManager.AddPathOrigin`/`AddModOrigin` alone do
   NOT populate the live asset dictionary** - they only append to the `Origins`/`CustomModOrigins`
   list, which the object loader's `GetMany` never re-scans (it reads the dictionary directly, per
   the `vs-object-loader-injection` memory's route-A finding). Assets must be added directly.
3. `Vintagestory.API.Config.GamePaths.AssetsPath`'s backing field (reflection: it has no public
   setter) must be primed to the game's `assets/` path, and `Lang.Load(logger, mgr, "en")` called,
   before the object loader runs - it calls `Lang.Get("It remembers...")`/`Lang.Get("...all that
   came before")` and both are static state read off `GamePaths`/`Lang`'s own dictionaries, not off
   the passed `IAssetManager` for the bootstrap file.
4. Build one substituted `ICoreServerAPI` (NSubstitute) wired to: the real `AssetManager` as
   `Assets`; a real `Vintagestory.Common.ClassRegistry` reached two ways - directly as
   `ClassRegistry` (wrapped in a `ClassRegistryAPI(world, registry)`, needed for `CreateBlock`), and
   through `.When(x => x.RegisterBlockClass(...)).Do(...)` forwarding into the same registry,
   because **`RegisterBlockClass`/`RegisterItemClass`/`RegisterBlockEntityClass`/etc. are top-level
   `ICoreAPI` members ServerMain backs with its own registry in production, not delegated through
   `api.ClassRegistry`** - a class registered only via `EntityRegistry.RegisterAll` (which calls
   `api.RegisterBlockClass`) is invisible to `api.ClassRegistry.CreateBlock` unless this forwarding
   is wired by hand; a real `Server.Logger` (the loader logs some errors through `api.Server.Logger`
   rather than `api.Logger` - left as an auto-substitute, those errors vanish into a Castle
   dynamic-proxy no-op instead of printing, which cost real debugging time before it was noticed);
   and, 1.22 only, real `ConcurrentTagRegistry`/`ConcurrentTagRegistryFast` instances for
   `CollectibleTagRegistry`/`EntityTagRegistry`.
5. **Wall found and worked around: `PreloadTags()` calls `ITagRegistry<T>.TryRegister(ReadOnlySpan<string>
   tags)` on those two registries. Castle's dynamic proxy (what NSubstitute is built on) cannot
   intercept a method with a `ReadOnlySpan<T>` (ref struct) parameter - throws
   `System.InvalidProgramException: Common Language Runtime detected an invalid program.`** A
   substitute cannot stand in for these two members at all; they must be real objects. This is a
   genuine engine/tooling wall, not a scope choice, and does not exist before 1.22 - the two
   properties are not declared on `ICoreAPI` at all in 1.20/1.21 (`GAME_GE_1_22` guards them).
6. Run `Vintagestory.ServerMods.NoObf.ModRegistryObjectTypeLoader.AssetsLoaded(coreApi)` by
   reflection (public class, but a version-fragile one to hard-reference, so found by name). Threw
   `System.ArgumentNullException` at `Lang.HasTranslation` until step 3 was done, and produced
   `Loaded 0 unique block(s)` (no exception, no registration) until `protobuf-net.dll` was also
   referenced: `LoadWorldProperties()` calls `api.Assets.GetMany<StandardWorldProperty>(...)`, and
   without `protobuf-net` loadable, every world-property asset fails to deserialize; the RESULT of
   that failure (not fully understood - the per-file failures are individually caught and logged,
   `worldProperties`/`worldPropertiesVariants` end up as valid, if empty or partial, dictionaries
   either way) still left every OTHER block's variant list empty too, symptom-identical to the
   walls above. Referencing `protobuf-net.dll` (matching exlib's own `src/ExpandedLib.csproj`, which
   already references it for the same reason) fixed it outright; not chased further given the fix
   was one reference away and the spike was time-boxed.
7. Registering the ENTIRE `assets/survival` domain (all of vanilla's real blocktypes) reaches a real
   but out-of-scope wall: many vanilla blocks (`BlockCoalPile`, `PumpkinVine`, `BlockCrop`, ...) name
   classes only `VSSurvivalMod`'s own `ModSystem`s register via `RegisterBlockClass` at its own
   `Start`, which this pipeline does not run - `type.CreateBlock(api)` throws
   `"Don't know how to instantiate crop behavior of class 'Pumpkin'"`, uncaught (only
   `RegisterBlock`, not `CreateBlock`, is try/caught in `LoadBlocks`), aborting the whole
   `LoadFromVariants` batch mid-iteration. Worked around by not mirroring survival/creative
   blocktypes/itemtypes/entities at all - only `worldproperties`/`patches`/`lang`/`config`, which
   carry no class dependency. A mod that references a vanilla code by name (a shape, a texture, an
   ingredient) still resolves fine; only vanilla's OWN types are excluded from the object loader's
   input.
8. Once blocks build, `api.RegisterBlock(block)`/`api.RegisterItem(item)` are the harvest point -
   NSubstitute records every call, so `api.ReceivedCalls().Where(c => c.GetMethodInfo().Name ==
   "RegisterBlock").Select(c => (Block)c.GetArguments()[0])` yields the real, resolved instances (in
   production these calls feed `ServerMain`'s own block-ID table; here they are the harness's only
   way to observe what the loader built, since `blockTypes`/`blockVariants` are nulled by
   `FreeRam()` at the end of `AssetsLoaded`).

End state, verified against HelloExpanded's `helloexpanded:hello` (a code-first def with a `side`
variantgroup, `n`/`e`/`s`/`w`): four real `Block` instances, `GetType().FullName ==
"HelloExpanded.BlockHello"`, `EntityClass == "helloexpanded.BlockEntityHello"`,
`Variant["side"]` correct per instance, `BlockMaterial == Stone` - everything the JSON declared,
resolved exactly as a real server would.

## Multiversion fix

The first cut of the harness code did not compile under `-p:Legacy=true` (`net7.0`/`net8.0`): the
tag-registry types (`Vintagestory.Common.Datastructures.ConcurrentTagRegistry`/`Fast`, and
`ICoreAPI.CollectibleTagRegistry`/`EntityTagRegistry` themselves) do not exist before 1.22, confirmed
by compiling a copy of the spike against `.game/1.20`. Fixed with the existing `#if GAME_GE_1_22`
capability-threshold convention (`mods/exlib/testing/World/TestWorld.cs` already used it for a
tick-listener signature change). The `AssetLoadingTests.cs` test itself is also `#if GAME_GE_1_22`-
gated: HelloExpanded only builds for the current game version, so a real-asset load of it can only
run there, though `TestWorld.LoadAssets` itself compiles and would run against a same-TFM-built mod
on any version. `bash scripts/exmod.sh test all` (11 lanes, 1.20/1.21/1.22) is green with this in
place.

## Design landed

`TestWorld.LoadAssets(string modPath, string? gamePath = null)`: reads `modinfo.json` for the
mod's `modid`, mirrors base `game` domain assets plus the mod's own domain into a fresh
`AssetManager`, `Start`s every `ModSystem` the mod's compiled assembly declares against an isolated
substituted `ICoreServerAPI` (so both JSON-only and code-first mods resolve, and exlib's own
`ExDefinitionModSystem` also runs unconditionally, matching production), runs the object loader, and
`Register`s every resulting `Block`/`Item` into the calling `TestWorld`. Isolated rather than
reusing `TestWorld.Api`, because other `TestWorld` members (`RegisterBlockEntityBehaviorFactory`)
stub `Api.ClassRegistry` with NSubstitute return values that a real `ClassRegistry` cannot honour.
`TestWorld.Register(Item)` was added alongside the existing `Register(Block)` as the harvest
counterpart; `VsAssemblyResolver.InstallPath` was exposed (was private) so `LoadAssets` shares the
harness's existing `.game/<slug>` resolution instead of re-deriving it;
`ReflectionHelpers.SetStaticField` was added alongside the existing instance-field/property helpers.

## Not covered

- The JSON patch loader (`ModJsonPatchLoader`) is not exercised - HelloExpanded ships no patches.
  Nothing in the pipeline should stop it running (it needs the same `Assets`/`Logger`, executes
  before the object loader by `ExecuteOrder`), but it was not proven live.
- `StructureRig` standing up a JSON-defined multiblock from a `LoadAssets`-loaded block was not
  attempted (time-boxed); nothing found suggests it would not work, since the registered `Block` is
  real and `StructureRig` only needs a `Block` and a `TestWorld`.
- The root cause of the `protobuf-net`-shaped symptom (item 6 above) - why a downstream, unrelated
  block's variant count follows whether `worldProperties`/`worldPropertiesVariants` fully populated
  - was not traced into `GatherVariants`/`CollectFromWorldProperties`; the empirical fix (reference
  `protobuf-net.dll`) is verified but not explained by source.
