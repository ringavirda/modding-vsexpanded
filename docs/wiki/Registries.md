# Registries

exlib registration is **attribute-driven**: tag a class, and a single `RegisterAll` call in your
`ModSystem` finds and registers every tagged class by reflection. No hand-maintained lists. There
are four registry families - entities, commands, preferences, and [config](Config-System) - plus
[recipe profiles](Recipe-Costs). This page covers entities, commands and preferences.

The reflection scan tolerates partial load failures (`ReflectionScan.GetCandidateTypes`), so one
unloadable type can't break registration of the rest.

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

[BlockRegister("pipeStraight")]         // -> "yourmod.pipeStraight"
public class BlockPipeStraight : BlockPipe { }

[BlockRegister("MultiblockStructure", PrefixModId = false)]   // -> "MultiblockStructure" (replaces vanilla)
public class BlockMultiblock : Block { }

[BlockEntityRegister]                   // -> "yourmod.BlockEntityPipe" + aliases "yourmod.Pipe","Pipe","pipe"
public class BlockEntityPipe : BlockEntity { }
```

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

The on-disk shape is the public `ExPreferencesConfig` - a map of player UID to that player's chosen
values. The store is **process-global static state** shared by every Expanded mod, which is what lets
one file and one `LevelFinalize` hook serve all of them.

Two lookups fail quietly rather than throwing, both on an unregistered key: `GetForPlayer` yields
`string.Empty`, and `SetForPlayer` persists the value but applies nothing. Register the preference
before you read or write it.

The `Key` doubles as the lang-key stem: `"measure"` drives `command-measure-desc`,
`pref-measure-label`, `pref-measure-metric`, etc.

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

## Related pages

- [Config System](Config-System) - `[ExConfigRegister]` and the generated value accessor.
- [Source Generators](Source-Generators) - what config classes and lang files generate.
- [Commands](Commands) - the shared `/exmod` root.
