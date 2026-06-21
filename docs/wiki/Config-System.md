# Config System

`Registries/Config/` is a generic, versioned, source-generated config system for gameplay
tunables. You write a plain POCO, tag it, and a generator emits a static accessor with typed
getters, `Load`/`Save`/`Edit`, range validation, version-reset migrations, legacy-file renaming
and optional live editing through `/exmod config`.

## Declaring a config

Write a POCO implementing `IExVersionedConfig` and tag it `[ExConfigRegister]`:

```csharp
[ExConfigRegister(
    "ppex_values.json",                 // file name under ModConfig/
    "ppex",                             // owning mod id (logging + version tracking)
    LegacyFileNames = ["ppex.json"],    // former names - auto-renamed on load
    Manageable = true                   // expose to /exmod config
)]
public class PpexConfig : IExVersionedConfig
{
    public string? ConfigVersion { get; set; }      // managed for you; stamps the writing mod version

    public static readonly ExConfigMigration[] Migrations =
    [
        new() { ToVersion = "0.6.0", ResetFields = [nameof(PumpWaterPerSecond)] },
    ];

    [ExConfigRange(1, 1_000_000)]
    public float LitresPerPipe { get; set; } = 30f;

    [ExConfigRange(0, 1)]
    public float BoilerWaterIntakeFillFraction { get; set; } = 0.5f;

    public float PumpWaterPerSecond { get; set; } = 16.67f;
    public string RecipeLevel { get; set; } = "normal";
}
```

The marker interface is tiny - it just lets the store track and migrate the file:

```csharp
public interface IExVersionedConfig
{
    string? ConfigVersion { get; set; }   // null on first run; set to the mod version that last wrote the file
}
```

## Using the generated accessor

The generator emits `PpexValues` (the name is the type name with `Config` -> `Values`; override
with `AccessorName`). You get:

```csharp
public static partial class PpexValues
{
    public const string ConfigFileName = "ppex_values.json";

    public static void Load(ICoreAPI api);            // load + migrate + sanitize + write-back (and register if Manageable)
    public static void Save();                        // persist live config
    public static void Edit(Action<PpexConfig> mutate);   // mutate + save

    public static float  LitresPerPipe { get; }       // one read-only getter per config property
    public static float  PumpWaterPerSecond { get; }
    public static string RecipeLevel { get; }
    // ...
}
```

```csharp
public override void Start(ICoreAPI api) => PpexValues.Load(api);   // call once at startup

// Read anywhere:
float litres = PpexValues.LitresPerPipe;

// Change + persist (typically server-side admin):
PpexValues.Edit(c => c.RecipeLevel = "cheap");
```

`Load` is safe on both sides; each reads its local copy. It applies migrations, clamps
out-of-range values to defaults, stamps the running mod version and writes the file back.

## Range validation

```csharp
[ExConfigRange(0, 1)]      // bounded
public float Fraction { get; set; } = 0.5f;

[ExConfigRange(1)]         // floor only; max = +infinity
public float LitresPerPipe { get; set; } = 30f;
```

`ExConfigRange(double min[, double max])` is enforced both on live edits (rejected if out of
bounds) and on load (file values out of bounds reset to the coded default). Numeric properties
**without** the attribute default to a non-negative, finite range `[0, +inf)`.

## Version-reset migrations

When you change a default and want existing players to pick it up, declare a migration. On load,
if the file's stamped version is below a migration's `ToVersion` and you're now at or past it, the
named fields reset to their coded defaults - everything else the player tuned is preserved.

```csharp
public sealed class ExConfigMigration
{
    public required string ToVersion { get; init; }   // reset fires when first loading at/above this version
    public string? FromVersion { get; init; }         // optional lower bound; null = any older version
    public string[]? ResetFields { get; init; }       // fields to reset; null/empty = reset all
}
```

```csharp
public static readonly ExConfigMigration[] Migrations =
[
    new() { ToVersion = "0.6.0", ResetFields = [nameof(PpexConfig.PumpWaterPerSecond)] },
];
```

The generator forwards a static `Migrations` member on your config type into the store
automatically.

## Legacy file names

`LegacyFileNames` preserves player configs across a rename. On load, if the current file is absent
but a legacy name exists, the first match is renamed to the current name - players keep their
settings.

## Live editing: `Manageable`

Set `Manageable = true` and the generated `Load` registers the store with `ExConfigProfiles`,
exposing it to the generic command:

```
/exmod config                       # list manageable mods
/exmod config ppex                  # list ppex's editable values
/exmod config ppex LitresPerPipe    # show current value
/exmod config ppex LitresPerPipe 40 # set it (immediate, no reload), validated + persisted
```

Behind the command is a non-generic view over the store:

```csharp
public interface IExConfigAccess
{
    string ModId { get; }
    string FileName { get; }
    IReadOnlyList<string> ValueNames { get; }
    bool TryGet(string name, out string canonicalName, out string value);
    ExConfigEditResult Set(string name, string raw);
}

public enum ExConfigEditStatus { Ok, UnknownValue, ParseFailed, OutOfRange }

public sealed class ExConfigEditResult
{
    public required ExConfigEditStatus Status { get; init; }
    public string Name { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string? Expected { get; init; }    // e.g. "number", "true/false"
    public string? Range { get; init; }       // e.g. "0..1"
}

public static class ExConfigProfiles
{
    public static void Register(IExConfigAccess config);
    public static bool TryGet(string code, out IExConfigAccess config);
    public static IReadOnlyCollection<string> Codes { get; }
}
```

Only simple-typed values (numbers, bools, strings) are surfaced for editing.

## The underlying store (if you skip the generator)

The generated accessor wraps `ExConfigRegister<TConfig>`; you can use it directly if you prefer:

```csharp
public sealed class ExConfigRegister<TConfig> : IExConfigAccess
    where TConfig : class, IExVersionedConfig, new()
{
    public TConfig Config { get; }
    public IReadOnlyList<string> LegacyFileNames { get; init; }

    public ExConfigRegister(string fileName, string modId, params ExConfigMigration[] migrations);

    public void Load(ICoreAPI api);
    public void Save();
    public void Edit(Action<TConfig> mutate);
}
```

But the generator path is recommended - adding a property is then a one-line change with the
getter, validation and command wiring all emitted for you.

## Related pages

- [Source Generators](Source-Generators) - exactly what `[ExConfigRegister]` emits.
- [Commands](Commands) - the `/exmod config` sub-command.
- [Recipe Costs](Recipe-Costs) - a `RecipeLevel` string in config drives the recipe profile.
