# Source Generators

`ExpandedLib.Generators` is a Roslyn source-generator project (netstandard2.0) that removes two
kinds of boilerplate at compile time: typed accessors for config classes, and typed members for a
block's JSON attributes. Both run automatically on build - there is nothing to invoke, you just tag
classes and mark them `partial`.

## `ExConfigGenerator` - config accessors

**Triggers on:** a class tagged `[ExConfigRegister(fileName, modId)]` (see [Config System](Config-System)).

**Emits:** a static partial class (default name = your type name with `Config` -> `Values`, override
with `AccessorName`) containing:

- `public const string ConfigFileName` - the file name you passed.
- A private `ExConfigRegister<T>` backing store, with `LegacyFileNames` initialised and a static
  `Migrations` member forwarded if your config type declares one.
- `public static void Load(ICoreAPI api)` - calls the store's `Load`; if `Manageable = true`, also
  registers with `ExConfigProfiles`.
- `public static void Edit(Action<T> mutate)` and `public static void Save()`.
- One `public static` read-only getter per public, non-static, readable property (except
  `ConfigVersion`), forwarding to the live config.

So this:

```csharp
[ExConfigRegister("ppex_values.json", "ppex", LegacyFileNames = ["ppex.json"], Manageable = true)]
public class PpexConfig : IExVersionedConfig
{
    public string? ConfigVersion { get; set; }
    public static readonly ExConfigMigration[] Migrations = [ /* ... */ ];
    public float LitresPerPipe { get; set; } = 30f;
}
```

generates roughly:

```csharp
public static partial class PpexValues
{
    public const string ConfigFileName = "ppex_values.json";
    private static readonly ExConfigRegister<PpexConfig> _store =
        new(ConfigFileName, "ppex", PpexConfig.Migrations) { LegacyFileNames = ["ppex.json"] };
    private static PpexConfig _config => _store.Config;

    public static void Load(ICoreAPI api) { _store.Load(api); ExConfigProfiles.Register(_store); }
    public static void Edit(Action<PpexConfig> mutate) { mutate(_store.Config); _store.Save(); }
    public static void Save() => _store.Save();

    public static float LitresPerPipe => _config.LitresPerPipe;
}
```

## `ExAttributeGenerator` - block/item attribute members

**Triggers on:** a class tagged `[BlockRegister]` or `[ItemRegister]` and declared `partial`.

**Inputs:** the matching block-type / item-type JSON in `blocktypes/` or `itemtypes/`.

**Emits:** members on the partial class that source the JSON `attributes`, so you read
`BurstPressure` instead of `Attributes?["burstPressure"].AsFloat(0f)`:

- **`const` members** for values identical across every matching file (no `attributesByType`
  override): `const bool` / `const string` / `const int` / `const float`.
- **Instance properties** for values that vary, for objects/arrays, or for `attributesByType`
  entries:
  - `bool` -> `public bool Foo => Attributes?["foo"].AsBool(false) ?? false;`
  - `string` -> `public string? Foo => Attributes?["foo"]?.AsString();`
  - number -> `public float Foo => Attributes?["foo"].AsFloat(0f) ?? 0f;`
  - object/array -> `public JsonObject? Foo => Attributes?["foo"];` (caller does `.AsObject<T>()`)

JSON keys are PascalCased for the member name (`"burstPressure"` -> `BurstPressure`). Members
inherited unchanged from a `[BlockRegister]` ancestor are skipped; differing ones get the `new`
keyword to avoid CS0108 hiding warnings; keys that would collide with an existing member are
skipped with a comment.

```csharp
[BlockRegister]
public partial class BlockPipe : BlockNetworkNode { }   // <- partial + tagged
```

```csharp
// generated (given the matching blocktype JSON):
public partial class BlockPipe
{
    public const string Material = "iron";                         // identical across all variants
    public float BurstPressure => Attributes?["burstPressure"].AsFloat(0f) ?? 0f;   // varies by variant
    public JsonObject? CustomData => Attributes?["customData"];
}
```

This is also how `IFillerHost.FillerOffsets` gets implemented for free - the generator surfaces the
`fillerOffsets` attribute as a member, satisfying the interface (see
[Multiblock Structures](Multiblock-Structures)).

## Notes

- Generated code is re-emitted every build, so adding a config property or a JSON attribute is
  instant - no boilerplate to duplicate or keep in sync.
- The generator targets `netstandard2.0` (a Roslyn requirement); if you fork it, mind the usual
  netstandard2.0 source-generator constraints (no newer BCL APIs).

## Related pages

- [Config System](Config-System) - the runtime side of `[ExConfigRegister]`.
- [Registries](Registries) - `[BlockRegister]` / `[ItemRegister]` registration.
