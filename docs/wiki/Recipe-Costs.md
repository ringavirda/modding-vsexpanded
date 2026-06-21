# Recipe Costs

`Registries/Recipes/` lets a mod ship **named cost levels** for its recipes (e.g. `normal` and
`cheap`) that players switch live with `/exmod recipes <mod> <level>`. It adjusts both grid
crafting recipes and right-click-construction (RCC) stage costs from a per-mod catalogue.

## The shape of it

You register a `RecipeProfile` that hands the shared framework lambdas to read your live
catalogue, your shipped defaults, and your active level. The framework runs one pipeline: repair
the catalogue, discover the `normal` baseline from live recipes, fill derived levels by scaling,
persist, then apply the active level to live recipes.

```csharp
public sealed class RecipeProfile
{
    public required string Code { get; init; }   // command code, e.g. "ppex" in `/exmod recipes ppex cheap`

    public required Func<IDictionary<string, RecipeCostEntry>> Catalogue { get; init; }       // live, persisted
    public required Func<IReadOnlyDictionary<string, RecipeCostEntry>> Defaults { get; init; } // fresh shipped copy
    public required Func<string> GetLevel { get; init; }       // read active level
    public required Action<string> SetLevel { get; init; }     // set + persist active level
    public required Action SaveCatalogue { get; init; }        // persist the catalogue file after fill

    public IReadOnlyList<string> Levels { get; init; } = ["normal", "cheap"];
    public IReadOnlyDictionary<string, double> DerivedLevels { get; init; }
        = new Dictionary<string, double> { ["cheap"] = 0.5 };
}
```

`Levels` is the selectable list in display order (first = authored baseline). `DerivedLevels` maps
a level name to a scale factor applied to `normal` (each ingredient floored at 1) - so `cheap` at
`0.5` is half cost, auto-filled, no hand authoring.

## Catalogue entries

```csharp
public class RecipeCostEntry
{
    public string Type { get; set; } = "grid";   // "grid" (match by output code) or "rcc" (match by block code)
    public string Match { get; set; } = "";       // wildcard code, e.g. "ppex:enginewatt-*"
    public Dictionary<string, RecipeProfileCost> Profiles { get; set; } = new();   // level name -> cost
}

public class RecipeProfileCost
{
    public Dictionary<string, int>? Ingredients { get; set; }                  // grid: ingredient code -> qty
    public int? Quantity { get; set; }                                         // grid: output count (null keeps authored)
    public Dictionary<string, Dictionary<string, int>>? Stages { get; set; }   // rcc: stage index -> {ingredient -> qty}
    public bool HasContent { get; }
}
```

`Match` is wildcard-aware, so one entry can cover a whole variant family.

## Registering

```csharp
ExRecipeProfiles.Register(new RecipeProfile
{
    Code = Mod.Info.ModID,
    Catalogue = () => PpexRecipeValues.Recipes,
    Defaults  = PpexRecipeConfig.DefaultCatalogue,
    GetLevel  = () => PpexValues.RecipeLevel,
    SetLevel  = level => PpexValues.Edit(c => c.RecipeLevel = level),
    SaveCatalogue = PpexRecipeValues.Save,
});
```

Call this once from `Start` after your config/catalogue is loaded. exlib applies all registered
profiles from its own `StartServerSide`/`StartClientSide`. The level itself is just a string in
your [config](Config-System) (`RecipeLevel` above), so it persists and is editable through
`/exmod config` too.

```csharp
public static class ExRecipeProfiles
{
    public static void Register(RecipeProfile profile);
    public static bool TryGet(string code, out RecipeProfile profile);
    public static IReadOnlyCollection<string> Codes { get; }
    public static void ApplyAll(ICoreAPI api);
    public static void Apply(ICoreAPI api, RecipeProfile profile);
}
```

## The adjuster (used internally)

`ExRecipeProfiles.Apply` drives the lower-level `ExRecipeCosts` helpers, which you can also call
directly if you build a custom pipeline:

```csharp
public static class ExRecipeCosts
{
    public const string ProfileNormal = "normal";

    public static bool EnsureNormalExtracted(ICoreAPI api, IDictionary<string, RecipeCostEntry> catalogue);
    public static bool EnsureScaledLevel(IDictionary<string, RecipeCostEntry> catalogue, string profile, double factor);
    public static bool Reconcile(IDictionary<string, RecipeCostEntry> live, IReadOnlyDictionary<string, RecipeCostEntry> defaults);
    public static void Apply(ICoreAPI api, IDictionary<string, RecipeCostEntry> catalogue, string profile);
}
```

- `EnsureNormalExtracted` auto-fills the `normal` profile from the live recipes for entries that
  lack it (returns `true` if anything changed -> persist).
- `EnsureScaledLevel` fills a profile by scaling `normal` by `factor` (floored at 1), skipping
  entries with explicit pinned costs.
- `Reconcile` repairs a hand-edited catalogue: restores deleted entries, fixes structural fields,
  restores pinned defaults, clamps quantities to >=1.
- `Apply` writes a named profile into the live grid ingredient/output counts and RCC stage costs.

## Related pages

- [Config System](Config-System) - store the active level and edit it with `/exmod config`.
- [Construction (RCC)](Construction) - `rcc`-type entries adjust construction stage costs.
- [Commands](Commands) - `/exmod recipes`.
