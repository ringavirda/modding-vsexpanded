# Config System

`Registries/Config/` is a generic, versioned, source-generated config system for gameplay
tunables. You write a plain POCO, tag it, and a generator emits a static accessor with typed
getters, `Load`/`Save`/`Edit`, range validation, version-reset migrations, legacy-file folding
and optional live editing through `/exmod config`.

## Where the values actually live

Config files are **shared and mod-sectioned**. A file under `ModConfig` is one JSON document whose
top-level keys are mod ids, each holding that mod's whole config object:

```json
{
  "exlib": { "ConfigVersion": "0.7.2", "LitresPerPipe": 30.0 },
  "iiex":  { "ConfigVersion": "0.6.9", "PumpWaterPerSecond": 16.67 },
  "yourmod": { "ConfigVersion": "1.0.0", "YourValue": 5.0 }
}
```

Each store reads and writes only its own section, so several mods share one file without seeing each
other's keys, and each carries its own independent `ConfigVersion` and migration history. The shipped
mods use two documents: `ex_values.json` for gameplay tunables and `ex_recipes.json` for recipe-cost
levels.

Three consequences worth knowing before you pick a file name:

- **Your section key is your mod id.** Naming a file another mod already uses is legal and simply adds
  a section to it.
- **Two configs owned by the same mod id cannot share a file.** The second one to bind overwrites the
  first's section. Give them different file names.
- **`Save()` rewrites the whole document**, from the in-memory copy that every mod's section binds
  into. That is safe by design - mod load is single-threaded, and between load and flush the
  in-memory document is the authority - but it is why a whole-file parse failure is expensive: the
  unreadable file is set aside as `<name>.corrupt` and *every* mod in it starts from coded defaults,
  where a single unreadable section costs only its own mod.

## Declaring a config

Write a POCO implementing `IExVersionedConfig` and tag it `[ExConfigRegister]`:

```csharp
[ExConfigRegister(
    "ex_values.json",                   // the shared document under ModConfig/
    "iiex",                             // owning mod id - and this config's section key
    LegacyFileNames = new string[] { "iwex_values.json", "lpex_values.json" },
    Manageable = true                   // expose to /exmod config
)]
public class IiexConfig : IExVersionedConfig
{
    public string? ConfigVersion { get; set; }      // managed for you; stamps the writing mod version

    public static readonly ExConfigMigration[] Migrations =
    [
        new() { ToVersion = "0.6.0", ResetFields = [nameof(PumpWaterPerSecond)] },
    ];

    [ExConfigRange(0, 1)]
    public float BoilerWaterIntakeFillFraction { get; set; } = 0.5f;

    public float PumpWaterPerSecond { get; set; } = 16.67f;
    public string RecipeLevel { get; set; } = "normal";
}
```

The marker interface is tiny - it just lets the store track and migrate the mod's section:

```csharp
public interface IExVersionedConfig
{
    string? ConfigVersion { get; set; }   // null on first run; set to the mod version that last wrote the section
}
```

## Using the generated accessor

The generator emits `IiexValues` (the name is the type name with a trailing `Config` replaced by
`Values`; override with `AccessorName`). You get:

```csharp
public static partial class IiexValues
{
    public const string ConfigFileName = "ex_values.json";

    public static void Load(ICoreAPI api);            // load + migrate + sanitize (server also writes back)
    public static void Save();                        // persist live config
    public static void Edit(Action<IiexConfig> mutate);   // mutate + save

    public static float  BoilerWaterIntakeFillFraction { get; }   // one read-only getter per config property
    public static float  PumpWaterPerSecond { get; }
    public static string RecipeLevel { get; }
    // ...
}
```

```csharp
public override void Start(ICoreAPI api) => IiexValues.Load(api);   // call once at startup

// Read anywhere:
float fraction = IiexValues.BoilerWaterIntakeFillFraction;

// Change + persist (typically server-side admin):
IiexValues.Edit(c => c.RecipeLevel = "cheap");
```

`Load` runs on both sides and each reads its own copy: it folds any legacy file in, applies
migrations, resets invalid values and stamps the running mod version.

> ⚠ **Only the server writes the file back.** In singleplayer both sides load the same store in one
> process against one file and would race over it, so the client migrates, sanitizes and stamps
> **in memory only**. The server's copy is the authority. `Save()` before `Load()` is likewise a
> silent no-op, because the store has no API handle yet - an `Edit()` that early mutates memory and
> persists nothing.

One nuance of the emitted surface: a getter is generated for every public, readable, non-static
property except `ConfigVersion`, **including get-only ones**. Validation and `/exmod config` both
require a setter, so a computed get-only property is readable through the accessor yet invisible to
both.

## Range validation

```csharp
[ExConfigRange(0, 1)]      // bounded
public float Fraction { get; set; } = 0.5f;

[ExConfigRange(1)]         // floor only; max = +infinity
public float Capacity { get; set; } = 30f;
```

`ExConfigRange(double min[, double max])` is enforced both on live edits (rejected if out of
bounds) and on load. Numeric properties **without** the attribute default to a non-negative, finite
range `[0, +inf)`.

> ⚠ **On load an invalid value is reset, not clamped.** A file carrying `2000000` under
> `[ExConfigRange(1, 1_000_000)]` comes back as the *coded default*, not as `1000000`. NaN and
> infinity are treated the same way, and so is a reference-typed value nulled out in the file whose
> coded default is non-null - a nulled string or collection would otherwise NRE its reader. Every
> reset is named in a warning log line.

## Version-reset migrations

When you change a default and want existing players to pick it up, declare a migration. On load,
if the section's stamped version is below a migration's `ToVersion` and you're now at or past it, the
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
    new() { ToVersion = "0.6.0", ResetFields = [nameof(IiexConfig.PumpWaterPerSecond)] },
];
```

The generator forwards a static `Migrations` member on your config type into the store
automatically - it must be static, named exactly `Migrations`, and be a field or a property.

## Legacy file names

`LegacyFileNames` carries player configs across a rename **or** across the move from a per-mod file
into the shared document. On load, if this mod's **section** is absent and one of the legacy files
still exists under `ModConfig`, that file's contents become the section and the old file is renamed
to `<name>.migrated` rather than deleted, so the carry-over stays reversible. First existing name
wins, and the fold never re-runs once the section exists.

That is how the shipped mods moved: `ppex.json` became `lpex_values.json` became the `iiex` section
of `ex_values.json`, and a player upgrading across either step keeps their settings.

## Live editing: `Manageable`

Set `Manageable = true` and the generated `Load` registers the store with `ExConfigProfiles`,
exposing it to the generic command:

```
/exmod config                       # list manageable mods
/exmod config iiex                  # list iiex's editable values
/exmod config iiex PumpWaterPerSecond    # show current value
/exmod config iiex PumpWaterPerSecond 20 # set it (immediate, no reload), validated + persisted
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

Only simple-typed values are surfaced for editing: `string`, `bool`, `int`, `long`, `float` and
`double`, and only when the property has a setter.

## The underlying store (if you skip the generator)

The generated accessor wraps `ExConfigRegister<TConfig>`; you can use it directly if you prefer:

```csharp
public sealed class ExConfigRegister<TConfig> : IExConfigAccess
    where TConfig : class, IExVersionedConfig, new()
{
    public TConfig Config { get; private set; }      // never null; holds coded defaults before Load
    public string ModId { get; }
    public string FileName { get; }
    public IReadOnlyList<string> LegacyFileNames { get; init; }

    public ExConfigRegister(string fileName, string modId, params ExConfigMigration[] migrations);

    public void Load(ICoreAPI api);
    public void Save();

    // the IExConfigAccess view the /exmod config command drives
    public IReadOnlyList<string> ValueNames { get; }
    public bool TryGet(string name, out string canonicalName, out string value);
    public ExConfigEditResult Set(string name, string raw);
}
```

Three things the generator does for you that you then own:

- **There is no `Edit` on the store.** Mutate `Config` and call `Save()` yourself.
- **`Manageable` is an attribute flag only the generator honours.** A hand-rolled store must call
  `ExConfigProfiles.Register` itself or it never appears in `/exmod config`.
- **`LegacyFileNames` is init-only and not a constructor parameter**, so set it through an object
  initializer.

The generator path is recommended - adding a property is then a one-line change with the getter,
validation and command wiring all emitted for you.

## Related pages

- [Source Generators](Source-Generators) - exactly what `[ExConfigRegister]` emits.
- [Commands](Commands) - the `/exmod config` sub-command.
- [Recipe Costs](Recipe-Costs) - a `RecipeLevel` string in config drives the recipe profile.
