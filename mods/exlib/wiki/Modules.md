# Modules

## What a module is

A Vintage Story mod folder may hold as many dlls as it likes, but only one of them may contain a
`ModSystem` - the engine refuses the whole mod when a second one does, with no indication of which
files collided. exlib itself ships two assemblies in one folder, `exlib.dll` and
`exlib.industry.dll`, so the framework needed an answer for its own content layer before it could
ask anyone else to use one.

A **module** is that answer generalised into exlib's extension mechanism: an assembly that extends
the framework or a mod built on it, identified by its own id, driven through a host mod's lifecycle
instead of carrying a `ModSystem` of its own. `ExpandedLib.Industry` is the first module - pipes,
molten metal, mechanical power, metals and heat, hosted by exlib. Other modders have asked for
electric and heating layers of their own; those are modules too, and a third party's own mod can be
one without shipping inside exlib's folder at all.

## The two assembly attributes

`[assembly: ExModule("<id>")]` declares the assembly as a module and is the whole of what discovery
needs:

```csharp
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ExModuleAttribute(string id) : Attribute {
  public string Id { get; }
  public string Host { get; set; } = "exlib";
  public string Mod { get; set; } = id;
  public string[] Requires { get; set; } = [];
  public bool PatchHarmony { get; set; }
}
```

- `Id` is the module's id, unique across every module loaded in the process, lower-case.
- `Host` names the mod whose lifecycle drives this module. It defaults to `"exlib"`, the framework
  itself; a mod hosting its own modules sets its own id instead.
- `Mod` is the Vintage Story mod id that ships this assembly, defaulting to `Id`. A module shipped
  inside another mod's folder (Industry, inside exlib's) sets this to that mod's id, so `ExModules`
  can tell "not enabled this world" from "not part of the process at all".
- `Requires` names module ids this one runs after, within the same host.
- `PatchHarmony`, when true, has the host patch this assembly's uncategorised `[HarmonyPatch]`
  classes at `Start` and unpatch them at `Dispose`.

`[assembly: ExDomain]` is unchanged by any of this - it still names the asset domain an assembly's
registered classes and code-first definitions are keyed under. A module carrying no `ExDomain` is
still discovered and driven; its classes key under its host's mod id instead, through
`EntityRegistry`'s existing fallback. That is right for a mod's own private second assembly, and
wrong for a framework module meant to be extended by other mods, which should declare its own
domain the way Industry keeps `exlib`.

## The two shipping forms

A module ships one of two ways:

- **Inside the host's mod folder**, beside the host's own dll - Industry's shape, `exlib.industry.dll`
  next to `exlib.dll` in the exlib mod folder. This is how a framework grows a second assembly
  without a second `ModSystem`.
- **As its own Vintage Story mod**, depending on the host mod so the game loads it first. This is the
  third-party shape: several mods can each depend on the same module mod, which a module dll
  embedded and duplicated inside two different mod folders cannot do - two assemblies of the same
  name loaded twice is not supported.

Whichever form it takes, a module dll shipped as its own mod still has to satisfy the engine's own
Code-mod loader, which does not know what a module is. `samples/HelloModule` ships with no
`ModSystem` at all to settle this: the loader refused it outright, `declared as code mod, but there
are no .dll files that contain at least one ModSystem or has a ModInfo attribute`, so the sample
carries an empty placeholder,

```csharp
public class HelloModuleModSystem : ModSystem { }
```

purely to satisfy that check. Nothing in it does any work - `ExModuleModSystem` is what actually
drives `HelloModule` through the phases below. A module shipped as its own mod needs the same empty
`ModSystem` for the same reason.

## Enabled-mod filtering

Discovery finds every module in the process, but drives only the ones whose shipping mod
(`ExModuleAttribute.Mod`) is enabled on the world being asked about - a module assembly still
present from an earlier world, or one belonging to a mod the player disabled for this one, is left
out. A module skipped this way is logged once per host lookup, at Notification level:

```
[exlib] module <id> skipped: mod <mod> is not enabled
```

`ExModuleModSystem` also logs, once per host instance at `StartPre`, the modules that host actually
drives:

```
[exlib] modules hosted by exlib: industry
```

or `none` when there aren't any.

## Phases

A module is driven through the same phases as a `ModSystem`, in the host's own execute order, and
each module's entry points run in the host's dependency order (see [Requires](#requires-and-ordering)
below). Before a module's entry points run at all, the host performs the registration a
`ModSystem` would do for its own assembly, against the module's assembly instead:

| Host phase | What the host does for the module first | `IExModule` hook |
| --- | --- | --- |
| `StartPre` | Nothing yet - too early for registration. | `StartPre(ICoreAPI api)` |
| `Start` | `ExConfig.LoadAll`, `EntityRegistry.RegisterAll`, and - if `PatchHarmony` is set - `ExHarmony.PatchOnce` under the module's Harmony id. | `Start(ICoreAPI api)` |
| `AssetsLoaded` | Nothing extra. | `AssetsLoaded(ICoreAPI api)` |
| `AssetsFinalize` | Nothing extra. | `AssetsFinalize(ICoreAPI api)` |
| `StartServerSide` | `CommandRegistry.RegisterAll`. | `StartServerSide(ICoreServerAPI api)` |
| `StartClientSide` | `PreferenceRegistry.RegisterAll`, then `CommandRegistry.RegisterAll` (preferences first, the same rule as any `ExModSystem`). | `StartClientSide(ICoreClientAPI api)` |
| `Dispose` | Nothing before; `ExHarmony.UnpatchAll` after, if `PatchHarmony` was set. | `Dispose()` |

Every `IExModule` method has an empty default, so a module overrides only what it needs. The
engine's own call order across phases is `StartPre`, `Start`, `AssetsLoaded`, `AssetsFinalize`, then
`StartServerSide`/`StartClientSide` for whichever side is running, then `Dispose` - the same order a
`ModSystem`'s own hooks run in, and the order `IExModule`'s phases run in too. `AssetsLoaded` is
where assets are readable and a catalogue read belongs; it is **not** where a definition should be
contributed, because a module's own `Start` may have already run before another module's, or after
- see the next section.

A module that throws from any phase is logged (naming the module type) and the rest of the host's
modules keep running; a module's own failure never takes down the phase for the others.

## `IExDefinitionContributor`

```csharp
public interface IExDefinitionContributor {
  void Contribute(ICoreAPI api);
}
```

Implemented by a module (or a main assembly) entry point that emits code-first definitions
depending on assets that are only readable once every mod's `Start` has run - a metal catalogue, a
config-driven item family. `EntityRegistry.RegisterAll` discovers implementors alongside a mod's
registered classes, the same call that discovers a module's own classes; `ExDefinitions.RunContributors`
instantiates and runs each one at `AssetsLoaded` 0.04, right before injection, regardless of which
host or which module order discovered it - the one legal place to emit a definition that depends on
loaded assets, because it is guaranteed to run after every `Start`, however the hosts and modules
involved are ordered against each other. Runs on the server only: the definition system does not
exist client-side.

## `Requires` and ordering

Modules of one host are ordered by `Requires`: a module runs after every module it names, ids broken
by alphabetical order. Three things can go wrong, each excluding only what it has to and logging one
line naming the problem:

- A module names a `Requires` id that is not among the host's other modules:

  ```
  module <id> requires <other>, which is not loaded; <id> is not driven
  ```

- A set of modules requires each other in a cycle:

  ```
  modules <a>, <b> form a requires cycle; none of them are driven
  ```

- Two assemblies declare the same module id; the first (by assembly name) stands and the rest are
  excluded:

  ```
  module <id> is declared by both <asmA> and <asmB>; <asmB> ignored
  ```

`ExModules.IsLoaded(api, moduleId)` answers whether an enabled module of that id was discovered, on
any host - the check a mod makes before relying on one it does not itself require:

```csharp
if (ExModules.IsLoaded(api, "industry"))
  RegisterIndustryCompat(api);
```

Every loaded module also gets a world-config flag, the same idiom as `ExMods.FlagKey` for mods, for
a JSON patch condition to gate on with no C# at all:

```json
{ "op": "add", "path": "/...", "condition": { "when": "exlib:module:industry", "isValue": "true" } }
```

`ExModules.FlagKey(moduleId)` builds the key (`"exlib:module:<id>"`); `ExModuleModSystem` sets it
`true` for every enabled module, on both sides, before the JSON patch loader runs.

## `PatchHarmony`

A module that sets `PatchHarmony = true` gets its uncategorised `[HarmonyPatch]` classes patched at
`Start` and unpatched at `Dispose`, the same convenience `ExModSystem` offers a main assembly -
patched and unpatched under `ExModuleInfo.HarmonyId`, which is `Host` and `Id` joined with a dot
(`"<host>.<id>"`), distinct from the host's own Harmony id and from every other module's.

## Industry as the worked example

Industry ships inside the exlib mod folder, keeps the `exlib` domain so a shipped blocktype and a
player's save keep the keys they already have, and is hosted by exlib itself:

```csharp
// AssemblyInfo.cs
[assembly: ExDomain("exlib")]
[assembly: ExModule("industry", Mod = "exlib")]
```

Its entry point registers a variant qualifier before anything else runs, contributes the metal
resource item family once assets are readable everywhere, and loads the metal catalogue once the
JSON patch pipeline has merged every mod's:

```csharp
public sealed class IndustryModule : IExModule, IExDefinitionContributor {
  public void StartPre(ICoreAPI api) =>
    ExBlockNames.AddVariantQualifier("refractory", "exlib:refractory-");

  public void Contribute(ICoreAPI api) {
    foreach (
      ExItemDef def in MetalFamilyEmitter.Emit(
        AssetCatalogueLoader.GetMany<MetalDef>(api, "config/metals/")
      )
    )
      ExDefinitions.RegisterItem(def);
  }

  public void AssetsFinalize(ICoreAPI api) => MetalCatalogueLoader.Load(api).Log(api.Logger);
}
```

`Mod = "exlib"` is what tells `ExModules.For` that Industry ships as part of the exlib mod, not a
mod of its own called `industry` - the id nothing installs and nothing could ever enable.

## The sample module

`samples/HelloModule` proves the third-party shape end to end: its own mod folder, its own domain,
depending on exlib, giving any mod's block a greeting. Mod id, module id and domain are all
`hellomodule`.

- `src/AssemblyInfo.cs` - `[assembly: ExDomain("hellomodule")]` and `[assembly: ExModule("hellomodule")]`,
  the two lines that make the assembly a module of its own mod's identity.
- `src/HelloModuleModSystem.cs` - the empty placeholder the loader requires; see above.
- `src/GreetingDef.cs` - the JSON shape of one entry under `config/greetings/`: a `Code` and a
  `Text`. Read with `AssetCatalogueLoader.GetMany<GreetingDef>(api, "config/greetings/")`, the way
  Industry reads its metals. `GetMany` deserializes one file to one object, so each file under
  `config/greetings/` holds exactly one `GreetingDef`, not an array of them - which is why the
  sample ships two files, `default.json` and `welcome.json`, one greeting each.
- `src/Greetings.cs` - the loaded catalogue, populated on both sides identically at
  `AssetsFinalize`, once the patch pipeline has merged every domain's greetings.
- `src/GreetingItems.cs` - pure, no asset reads: one `ExItemDef` per greeting, coded
  `greeting-<code>`, vanilla shape and texture.
- `src/HelloModule.cs` - the entry point: `Contribute` emits the greeting items through
  `ExDefinitions.RegisterItem`, `AssetsFinalize` loads `Greetings`.
- `src/BlockBehaviorGreeter.cs` - `[BlockBehaviorRegister]`; on a server-side interact sends a
  player some greetings from `Greetings.All`, handling left `PassThrough` so the block's own
  handler still runs. Any mod's block picks this up with `.Behavior<BlockBehaviorGreeter>()`, which
  resolves to `hellomodule.BlockBehaviorGreeter` through this assembly's own `ExDomain` rather than
  the calling block's - the cross-assembly key path this sample exists to prove.
- `src/HelloModuleConfig.cs` - one `[ExConfigRegister]` tunable, how many greetings per click.
- `src/GreetSubCommand.cs` - `/exmod greet`, printing how many greetings loaded.

## For third parties

A module package publishes under its own id, not `ExpandedLib.*` - that prefix names exlib's own
framework and content-layer packages. `hellomodule`, `electric`, `heating`: whatever the module's
own id is, the package that ships it is named for that, the same as any other Vintage Story mod
that happens to extend a framework instead of shipping content directly.
