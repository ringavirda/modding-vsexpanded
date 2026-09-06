# Registries

exlib registration is **attribute-driven**: tag a class, and a single `RegisterAll` call in your
`ModSystem` finds and registers every tagged class by reflection. No hand-maintained lists. There
are four registry families - entities, commands, preferences, and [config](Config-System) - plus
[recipe profiles](Recipe-Costs). This page covers entities, commands and preferences.

The reflection scan tolerates partial load failures (`ReflectionScan.GetCandidateTypes`), so one
unloadable type can't break registration of the rest.

## ExModSystem: the zero-line rung

Derive `ExModSystem` instead of `ModSystem` and there is nothing left to write - it runs every
registry below for you, in the right phase and the right order:

```csharp
public class YourModSystem : ExModSystem { }
```

`Start` loads your `[ExConfigRegister]` config and runs `EntityRegistry.RegisterAll`;
`StartServerSide` runs `CommandRegistry.RegisterAll`; `StartClientSide` runs
`PreferenceRegistry.RegisterAll` then `CommandRegistry.RegisterAll` (preferences first, so a
command naming one finds it already registered - **command registration runs once per side**,
independently, not once for the whole mod). Override `OnStart`/`OnStartServerSide`/
`OnStartClientSide`/`OnAssetsFinalize` for anything of your own that needs to run after
registration; set `PatchHarmony => true` to fold `ExHarmony.PatchOnce`/`UnpatchAll` in too.

The rest of this page documents the calls `ExModSystem` makes for you, for a mod system that needs
a different order (see [Ordering rules](Lifecycle) - a preference read before a command that names
it, say) or that isn't a `ModSystem` at all.

## Entity registration

`Registries/Entities/` registers blocks, items, entities and behaviours. The base attribute:

```csharp
public abstract class RegisterAttribute(string? code = null) : Attribute
{
    public string? Code { get; }              // override the registry key; default {modid}.{ClassName}
    public bool PrefixModId { get; init; } = true;   // false -> register under a bare key (replace vanilla)
}
```

Six sealed attributes inherit it, each validating the target's base type:

| Attribute | Target base type |
| --- | --- |
| `[BlockRegister]` | `Block` |
| `[ItemRegister]` | `Item` |
| `[BlockEntityRegister]` | `BlockEntity` |
| `[BlockBehaviorRegister]` | `BlockBehavior` |
| `[BlockEntityBehaviorRegister]` | `BlockEntityBehavior` |
| `[CollectibleBehaviorRegister]` | `CollectibleBehavior` |

```csharp
[BlockRegister]                         // -> "yourmod.BlockPipe"
public class BlockPipe : BlockNetworkNode { }
```

| Variant | Registers as |
| --- | --- |
| `[BlockRegister("pipeStraight")]` | `"yourmod.pipeStraight"` |
| `[BlockRegister("MultiblockStructure", PrefixModId = false)]` | `"MultiblockStructure"` (replaces vanilla) |
| `[BlockEntityRegister]` on `BlockEntityPipe` | `"yourmod.BlockEntityPipe"` + aliases `"yourmod.Pipe"`, `"Pipe"`, `"pipe"` |

A class named `BlockEntityXxx` automatically also registers the short-name aliases
`{modid}.{Xxx}`, `{Xxx}`, `{xxx}` (when you don't set an explicit `Code`), so your JSON can use the
short `entityClass`.

Register them all from `Start`:

```csharp
public static class EntityRegistry
{
    public static void RegisterAll(ICoreAPI api, Mod mod, Assembly? asm = null);   // default asm = caller's
}
```

```csharp
public override void Start(ICoreAPI api)
    => EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);
```

The same scan also picks up code-first definition providers in the assembly - any
`IExBlockDefProvider`, `IExItemDefProvider` or `IExRecipeDefProvider` - so blocks, items and recipes
declared in C# register through this one call too.

## Command registration

`Registries/Commands/` discovers two kinds of class. A **top-level command** implements
`IExCommand`; a **sub-command** that hangs off an existing command implements `IExSubCommand`.
The `Side` on each attribute gates registration to `Universal` / `Server` / `Client`.

```csharp
public interface IExCommand
{
    void Register(ICoreAPI api, Mod mod);                        // build via api.ChatCommands.Create(...)
}

public interface IExSubCommand
{
    string ParentName { get; }                                  // existing command to attach to, e.g. "exmod"
    void Register(ICoreAPI api, Mod mod, IChatCommand parent);  // build via parent.BeginSubCommand(...)
}
```

```csharp
[SubCommandRegister(Side = EnumAppSide.Server)]
public sealed class StatusSubCommand : IExSubCommand
{
    public string ParentName => "exmod";

    public void Register(ICoreAPI api, Mod mod, IChatCommand parent)
    {
        parent.BeginSubCommand("status")
              .WithDescription(Lang.Get(mod.Info.ModID + ":command-status-desc"))
              .HandleWith(args => /* ... */)
              .EndSubCommand();
    }
}
```

Register them from `Start` (or the side-specific start methods):

```csharp
public static class CommandRegistry
{
    public static void RegisterAll(ICoreAPI api, Mod mod, Assembly? asm = null);
}
```

The registry resolves (or creates) each sub-command's parent via `api.ChatCommands.GetOrCreate`,
so multiple mods can safely add sub-commands to the shared `exmod` root. See **[Commands](Commands)**
for the `/exmod` / `.exmod` root exlib provides and its built-in sub-commands.

## Preferences

`Registries/Preferences/` is a per-player, **client-side** display-preference store (e.g. a
metric/imperial unit toggle). Implement `IExPreference`, tag it `[PreferenceRegister]`:

```csharp
public interface IExPreference
{
    string Key { get; }                       // lower-case, no spaces: config key + sub-command name + lang stem
    IReadOnlyList<string> Options { get; }     // allowed values, lower-case
    string Default { get; }                    // must be in Options
    void Apply(string value);                  // push the stored value into live client state
}
```

```csharp
[PreferenceRegister]
public sealed class MeasurePreference : IExPreference
{
    public string Key => "measure";
    public IReadOnlyList<string> Options { get; } = ["metric", "imperial"];
    public string Default => "metric";
    public void Apply(string value) => ExMeasure.System = ExMeasure.Parse(value);
}
```

Wire it up in `StartClientSide`, **before your own `CommandRegistry.RegisterAll`**:

```csharp
public override void StartClientSide(ICoreClientAPI api)
{
    PreferenceRegistry.RegisterAll(api, Mod, GetType().Assembly);
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
}
```

That is the only ordering constraint, and it exists because a preference sub-command resolves its
definition **once, at registration time**, into a local it then holds. A command registered before
the preference it names finds nothing: written defensively you get a sub-command whose name, options
and description come from a throwaway fallback the store does not hold; written as
`ExPreferences.Find(key)!` you get an NRE at world load.

> ⚠ **You do not load or apply the store yourself.** exlib calls `ExPreferences.LoadConfig` in its own
> `StartClientSide`, and hooks `LevelFinalize` to apply every registered preference for the local
> player - which is exactly why registering yours later in the same phase still works, and why
> `ExPreferences.ApplyForPlayer` is not yours to call: `api.World.Player` is not ready during
> `StartClientSide`. Calling `LoadConfig` again re-reads the file and re-points the store's
> process-global API handle at your own; harmless at that phase, but it claims an ownership you do
> not have.

The shared store persists to `ModConfig/exmod_preferences.json` keyed by player UID:

```csharp
public static class ExPreferences
{
    public const string ConfigFileName = "exmod_preferences.json";

    public static void Register(IExPreference preference);
    public static IExPreference? Find(string key);
    public static void LoadConfig(ICoreAPI api);                                   // exlib calls this
    public static string GetForPlayer(string playerUid, string key);
    public static void SetForPlayer(string playerUid, string key, string value);   // store + apply + persist
    public static void ApplyForPlayer(string playerUid);                           // exlib calls this
    public static IEnumerable<IExPreference> All { get; }
}
```

The on-disk shape is an internal map of player UID to that player's chosen values. The store is
**process-global static state** shared by every Expanded mod, which is what lets one file and one
`LevelFinalize` hook serve all of them.

Two lookups fail quietly rather than throwing, both on an unregistered key: `GetForPlayer` yields
`string.Empty`, and `SetForPlayer` persists the value but applies nothing. Register the preference
before you read or write it.

The `Key` doubles as the lang-key stem: `"measure"` drives `command-measure-desc`,
`pref-measure-label`, `pref-measure-metric`, etc.

## Shipping more than one assembly

One dll may declare as many mod systems as it likes. The limit is on files: **a mod folder may hold
several dlls, but only one of them may contain mod systems at all.** When a second one does, the
game refuses to load the mod, with

```
Found multiple .dll files with ModSystems and/or ModInfo attributes
```

and no indication of which files collided. exlib itself hits this - it ships `exlib.dll` and
`exlib.industry.dll` in one folder - so the framework has an answer for it.

An assembly past the first declares an `IExModule` instead of a `ModSystem`, and marks itself with
the mod it belongs to:

```csharp
[assembly: ExDomain("mymod")]

public sealed class MyCompanionModule : IExModule {
  public void AssetsFinalize(ICoreAPI api) => MyCatalogue.Load(api).Log(api.Logger);
}
```

That is the whole of it. `ExModSystem` finds the modules of its own mod and runs them through
`StartPre`, `Start`, `AssetsLoaded` and `AssetsFinalize`, lowest `Order` first, and it registers the
companion assembly's `[BlockRegister]`/`[BlockEntityRegister]`/etc classes before calling its
`Start` - so a block class in a second assembly needs no more code than one in the first. The keys
it registers under come from the mod, not the assembly, so moving a class between your own
assemblies does not change the name a shipped blocktype or a player's save already holds.

Two details worth knowing. Discovery reads the assemblies the runtime has loaded rather than the mod
folder, so a zipped mod behaves the same as an unzipped one. And a module that throws is logged and
skipped rather than failing the phase: a companion assembly is a part of the mod, not the whole of
it, and the rest should still load.


## Registration cheat-sheet

```csharp
public override void Start(ICoreAPI api)
{
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);    // blocks/items/entities/behaviours + def providers
    YourValues.Load(api);                                        // generated config accessor (Config System)
}

public override void StartClientSide(ICoreClientAPI api)
{
    PreferenceRegistry.RegisterAll(api, Mod, GetType().Assembly);   // before the commands that name them
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);      // [CommandRegister]/[SubCommandRegister]
}

public override void StartServerSide(ICoreServerAPI api)
{
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);
}
```

`CommandRegistry.RegisterAll` is safe to call from `Start` - each command declares its side, so it
registers once either way - but every shipped mod calls it from the two side hooks instead. That is
what keeps a client sub-command from being built before the preference it names exists.

## Other mods

Three rungs, shortest first.

A one-line check:

```csharp
if (ExMods.IsLoaded(api, "toolsmith")) { /* ... */ }
if (ExMods.AtLeast(api, "toolsmith", "1.9.0")) { /* ... */ }
```

A JSON patch condition, no C# at all - exlib sets `ExMods.FlagKey(modId)` (`"exlib:mod:<modid>"`) to
`true` in world config for every enabled mod, before the patch loader runs:

```json
{ "op": "add", "path": "/...", "condition": { "when": "exlib:mod:toolsmith", "isValue": "true" } }
```

A run-now callback, for code that only makes sense once:

```csharp
ExMods.WhenLoaded(api, "toolsmith", () => RegisterToolsmithCompat(api));
```

```csharp
public static class ExMods
{
    public static bool IsLoaded(ICoreAPI api, string modId);
    public static string? Version(ICoreAPI api, string modId);
    public static bool AtLeast(ICoreAPI api, string modId, string minimumVersion);
    public static bool WhenLoaded(ICoreAPI api, string modId, Action action);
    public static string FlagKey(string modId);
}
```

`IsLoaded` never throws: a null or blank id is just not loaded. `AtLeast` compares the way the game
itself does, so a pre-release such as `"1.9.0-rc.1"` sorts below its own release `"1.9.0"`.

## Harmony

The bootstrap in three lines, once per mod system:

```csharp
public override void Start(ICoreAPI api)
{
    _harmony = ExHarmony.PatchOnce(Mod, GetType().Assembly);
}

public override void Dispose()
{
    ExHarmony.UnpatchAll(Mod);
}
```

`PatchOnce` applies every uncategorised `[HarmonyPatch]` class in the assembly, guarded so it is a
no-op on a second call however many dependent mods share the process. A class also carrying
`[HarmonyPatchCategory("...")]` is left alone until you opt it in, gated on another mod being loaded:

```csharp
ExHarmony.PatchCategoryWhenLoaded(api, _harmony, GetType().Assembly, "compat-toolsmith", "toolsmith");
```

```csharp
public static class ExHarmony
{
    public static Harmony PatchOnce(Mod mod, Assembly assembly);
    public static bool PatchCategoryWhenLoaded(ICoreAPI api, Harmony harmony, Assembly assembly, string category, string requiredModId);
    public static void UnpatchAll(Mod mod);
}
```

## Related pages

- [Config System](Config-System) - `[ExConfigRegister]` and the generated value accessor.
- [Source Generators](Source-Generators) - what config classes and lang files generate.
- [Commands](Commands) - the shared `/exmod` root.
