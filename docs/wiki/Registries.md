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
public partial class BlockPipe : BlockNetworkNode { }

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

> Marking a registered block `partial` lets the [attribute generator](Source-Generators) surface
> its JSON `attributes` as typed members - see that page.

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

Wire it up in `StartClientSide`, after loading the store and before commands register:

```csharp
public override void StartClientSide(ICoreClientAPI api)
{
    ExPreferences.LoadConfig(api);
    PreferenceRegistry.RegisterAll(api, Mod, GetType().Assembly);
}
```

The shared store persists to `ModConfig/exmod_preferences.json` keyed by player UID, applies each
preference on join and when changed:

```csharp
public static class ExPreferences
{
    public const string ConfigFileName = "exmod_preferences.json";

    public static void Register(IExPreference preference);
    public static IExPreference? Find(string key);
    public static void LoadConfig(ICoreAPI api);
    public static string GetForPlayer(string playerUid, string key);
    public static void SetForPlayer(string playerUid, string key, string value);   // store + apply + persist
    public static void ApplyForPlayer(string playerUid);                           // apply all, on join
    public static IEnumerable<IExPreference> All { get; }
}
```

The `Key` doubles as the lang-key stem: `"measure"` drives `command-measure-desc`,
`pref-measure-label`, `pref-measure-metric`, etc.

## Registration cheat-sheet

```csharp
public override void Start(ICoreAPI api)
{
    EntityRegistry.RegisterAll(api, Mod, GetType().Assembly);    // blocks/items/entities/behaviours
    CommandRegistry.RegisterAll(api, Mod, GetType().Assembly);   // [CommandRegister]/[SubCommandRegister]
    YourValues.Load(api);                                        // generated config accessor (Config System)
}

public override void StartClientSide(ICoreClientAPI api)
{
    ExPreferences.LoadConfig(api);
    PreferenceRegistry.RegisterAll(api, Mod, GetType().Assembly);
}
```

## Related pages

- [Config System](Config-System) - `[ExConfigRegister]` and the generated value accessor.
- [Source Generators](Source-Generators) - what `partial` blocks and config classes generate.
- [Commands](Commands) - the shared `/exmod` root.
