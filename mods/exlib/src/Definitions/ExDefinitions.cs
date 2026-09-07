using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using ExpandedLib.Registries;
using Vintagestory.API.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// Process-wide registry of code-first block definitions. A mod authors a block in C# with
/// <see cref="ExBlockDef"/> and registers it here (from its <c>ModSystem.Start</c>); the shared
/// <see cref="ExDefinitionModSystem"/> serializes each and injects it as a synthetic <c>blocktypes/</c>
/// asset on the server, before the object loader runs. Keyed by asset location so a re-register (or a
/// deliberate override) replaces rather than duplicates.
/// </summary>
public static class ExDefinitions {
  private static readonly ExKeyedRegistry<ExBlockDef> _blocks = new(d =>
    d.Location.ToString()
  );

  private static readonly ExKeyedRegistry<ExItemDef> _items = new(d =>
    d.Location.ToString()
  );

  private static readonly ExKeyedRegistry<ExRecipeDef> _recipes = new(d =>
    d.Location.ToString()
  );

  // Location -> the assembly whose provider last registered it, so a re-registration from a different
  // assembly (two mods claiming the same asset path) can be told apart from a mod simply reloading its
  // own defs, which happens every world start.
  private static readonly Dictionary<string, Assembly> _blockProviders = new(
    StringComparer.Ordinal
  );
  private static readonly Dictionary<string, Assembly> _itemProviders = new(
    StringComparer.Ordinal
  );
  private static readonly Dictionary<string, Assembly> _recipeProviders = new(
    StringComparer.Ordinal
  );

  // Types, never instances (see the module system's static-state rule): a contributor is
  // instantiated fresh by RunContributors, not held across worlds.
  private static readonly List<Type> _contributors = [];

  /// <summary>Log sink for exlib's own definition diagnostics (the re-registration notification); set
  /// once by <see cref="Registries.ExModuleModSystem.StartPre"/> (0.03). Null before startup and in
  /// tests that never wire it, in which case diagnostics are silently skipped.
  /// </summary>
  public static ILogger? Logger { get; set; }

  /// <summary>Registers (or replaces) a code-first block definition.</summary>
  public static void RegisterBlock(ExBlockDef def) =>
    RegisterBlock(def, Assembly.GetCallingAssembly());

  internal static void RegisterBlock(ExBlockDef def, Assembly providerAssembly) {
    TrackProvider(_blockProviders, def.Location.ToString(), providerAssembly);
    _blocks.Register(def);
  }

  /// <summary>Registers (or replaces) a code-first item definition.</summary>
  public static void RegisterItem(ExItemDef def) =>
    RegisterItem(def, Assembly.GetCallingAssembly());

  internal static void RegisterItem(ExItemDef def, Assembly providerAssembly) {
    TrackProvider(_itemProviders, def.Location.ToString(), providerAssembly);
    _items.Register(def);
  }

  /// <summary>Registers (or replaces) a code-first recipe file.</summary>
  public static void RegisterRecipe(ExRecipeDef def) =>
    RegisterRecipe(def, Assembly.GetCallingAssembly());

  internal static void RegisterRecipe(
    ExRecipeDef def,
    Assembly providerAssembly
  ) {
    TrackProvider(_recipeProviders, def.Location.ToString(), providerAssembly);
    _recipes.Register(def);
  }

  // Records which assembly registered `location` this time, and logs a Notification when that differs
  // from the assembly that registered it last - two mods (or a mod and exlib itself) claiming the same
  // asset path, rather than one mod's own reload.
  private static void TrackProvider(
    Dictionary<string, Assembly> providers,
    string location,
    Assembly providerAssembly
  ) {
    if (
      providers.TryGetValue(location, out Assembly? prior)
      && prior != providerAssembly
    )
      Logger?.Notification(
        "[exlib] {0} re-registered by {1} (was {2})",
        location,
        providerAssembly.GetName().Name,
        prior.GetName().Name
      );
    providers[location] = providerAssembly;
  }

  /// <summary>Every registered block definition.</summary>
  public static IReadOnlyCollection<ExBlockDef> Blocks => _blocks.Values;

  /// <summary>Every registered item definition.</summary>
  public static IReadOnlyCollection<ExItemDef> Items => _items.Values;

  /// <summary>Every registered recipe file.</summary>
  public static IReadOnlyCollection<ExRecipeDef> Recipes => _recipes.Values;

  /// <summary>Every discovered <see cref="IExDefinitionContributor"/> type, in discovery order.</summary>
  public static IReadOnlyList<Type> Contributors => _contributors.AsReadOnly();

  /// <summary>Drops every registered definition (used by tests to isolate the static registry).</summary>
  public static void Clear() {
    _blocks.Clear();
    _items.Clear();
    _recipes.Clear();
    _blockProviders.Clear();
    _itemProviders.Clear();
    _recipeProviders.Clear();
    _contributors.Clear();
  }

  /// <summary>
  /// Builds a <c>type -&gt; orientation states</c> map from a class's code-first defs - the single
  /// source a block derives its runtime <c>AllowedOrientations</c> from, so the orientation list lives
  /// only in the variant groups. A def with no <c>type</c> states (e.g. a worldproperty-oriented block)
  /// has no pair to contribute and is skipped. Every type state a def declares is mapped, not just a
  /// lone one: a def may carry several (<c>iiex:flywheel</c> declares <c>type(normal|large)</c> with
  /// <c>orientation(ns|we)</c>), and they share that def's single orientation group by construction.
  /// </summary>
  public static Dictionary<string, string[]> OrientationMap(
    IEnumerable<ExBlockDef> defs
  ) {
    var map = new Dictionary<string, string[]>();
    foreach (ExBlockDef d in defs) {
      string[] orientations = d.VariantStates("orientation");
      foreach (string type in d.VariantStates("type"))
        map[type] = orientations;
    }
    return map;
  }

  /// <summary>
  /// Scans <paramref name="asm"/> for <see cref="IExBlockDefProvider"/> classes and registers each
  /// one's co-located definition (built for <paramref name="domain"/>, the mod id). Called from
  /// <see cref="Registries.EntityRegistry.RegisterAll"/> so a mod's block defs are discovered
  /// alongside its class registration. Returns how many were registered.
  /// </summary>
  public static int DiscoverAndRegister(string domain, Assembly asm) =>
    Discover<ExBlockDef>(
      domain,
      asm,
      typeof(IExBlockDefProvider),
      def => RegisterBlock(def, asm)
    );

  /// <summary>
  /// Item-side sibling of <see cref="DiscoverAndRegister"/>: scans <paramref name="asm"/> for
  /// <see cref="IExItemDefProvider"/> classes and registers each one's co-located definition(s).
  /// Returns how many were registered.
  /// </summary>
  public static int DiscoverAndRegisterItems(string domain, Assembly asm) =>
    Discover<ExItemDef>(
      domain,
      asm,
      typeof(IExItemDefProvider),
      def => RegisterItem(def, asm)
    );

  /// <summary>
  /// Recipe-side sibling of <see cref="DiscoverAndRegister"/>: scans <paramref name="asm"/> for
  /// <see cref="IExRecipeDefProvider"/> classes and registers each one's co-located recipe file(s).
  /// Returns how many were registered.
  /// </summary>
  public static int DiscoverAndRegisterRecipes(string domain, Assembly asm) =>
    Discover<ExRecipeDef>(
      domain,
      asm,
      typeof(IExRecipeDefProvider),
      def => RegisterRecipe(def, asm)
    );

  /// <summary>
  /// Scans <paramref name="asm"/> for concrete <see cref="IExDefinitionContributor"/> types with a
  /// parameterless constructor and records each once (types, never instances). Called from
  /// <see cref="Registries.EntityRegistry.RegisterAll"/> alongside the three <c>DiscoverAndRegister*</c>
  /// passes, so a contributor needs no separate registration call. One without a parameterless
  /// constructor is logged through <see cref="Logger"/> as a warning naming the type and skipped.
  /// </summary>
  /// <remarks>Plumbing with one caller (<see cref="Registries.EntityRegistry.RegisterAll"/>);
  /// public because the harness needs it to seed a test's own contributors.</remarks>
  [EditorBrowsable(EditorBrowsableState.Never)]
  public static void DiscoverContributors(Assembly asm) {
    foreach (Type type in ReflectionScan.GetCandidateTypes(asm)) {
      if (!typeof(IExDefinitionContributor).IsAssignableFrom(type))
        continue;
      if (type.GetConstructor(Type.EmptyTypes) == null) {
        Logger?.Warning(
          "[exlib] {0} implements IExDefinitionContributor but has no parameterless constructor; skipped.",
          type.FullName
        );
        continue;
      }
      if (!_contributors.Contains(type))
        _contributors.Add(type);
    }
  }

  /// <summary>
  /// Instantiates and runs every discovered <see cref="IExDefinitionContributor"/>, in discovery
  /// order, each isolated (a throw is logged through <paramref name="api"/>'s logger naming the type;
  /// the rest still run). Called by <see cref="ExDefinitionModSystem.AssetsLoaded"/> right before
  /// injection, after every mod's and module's <c>Start</c> has registered its contributors, so a
  /// contribution depending on loaded assets is always in time. Server-only - the caller's own
  /// side check gates this, since the definition system does not run client-side.
  /// </summary>
  /// <remarks>Plumbing with one caller (<see cref="ExDefinitionModSystem.AssetsLoaded"/>); public
  /// because the harness needs it to replay contributors against real loaded assets.</remarks>
  [EditorBrowsable(EditorBrowsableState.Never)]
  public static void RunContributors(ICoreAPI api) {
    int ran = 0;
    foreach (Type type in _contributors) {
      try {
        ((IExDefinitionContributor)Activator.CreateInstance(type)!).Contribute(
          api
        );
        ran++;
      } catch (Exception e) {
        api.Logger.Error(
          "[exlib] definition contributor {0} threw; skipped.",
          type.FullName
        );
        api.Logger.Error(e);
      }
    }
    if (ran > 0)
      api.Logger.Notification(
        "[exlib] Ran {0} definition contributor(s).",
        ran
      );
  }

  // Discovers every concrete implementor of `providerInterface` in the assembly and registers each def its
  // static `Definitions(string)` factory returns; shared by the three public passes above, which differ only
  // by provider interface, def type and target registry. The provider interface is passed as a Type, not a
  // type argument, because it carries a `static abstract` member, which bars it from being a generic type
  // argument (CS8920), and only a reflective assignability check needs it.
  private static int Discover<TDef>(
    string domain,
    Assembly asm,
    Type providerInterface,
    Action<TDef> register
  )
    where TDef : IExDef {
    int count = 0;
    foreach (Type type in ReflectionScan.GetCandidateTypes(asm))
      foreach (TDef def in DefinitionsOf<TDef>(type, domain, providerInterface)) {
        register(def);
        count++;
      }
    return count;
  }

  /// <summary>
  /// The code-first block defs a <paramref name="type"/> declares itself, or empty when it declares none.
  /// Used both by discovery and by a block deriving runtime tables from its own def
  /// (<c>ExDefinitions.OrientationMap(DefinitionsOf(GetType(), domain))</c>).
  /// </summary>
  public static IEnumerable<ExBlockDef> DefinitionsOf(
    Type type,
    string domain
  ) => DefinitionsOf<ExBlockDef>(type, domain, typeof(IExBlockDefProvider));

  /// <summary>The item-side sibling of <see cref="DefinitionsOf"/> (via <see cref="IExItemDefProvider"/>).</summary>
  public static IEnumerable<ExItemDef> ItemDefinitionsOf(
    Type type,
    string domain
  ) => DefinitionsOf<ExItemDef>(type, domain, typeof(IExItemDefProvider));

  /// <summary>The recipe-side sibling of <see cref="DefinitionsOf"/> (via <see cref="IExRecipeDefProvider"/>).</summary>
  public static IEnumerable<ExRecipeDef> RecipeDefinitionsOf(
    Type type,
    string domain
  ) => DefinitionsOf<ExRecipeDef>(type, domain, typeof(IExRecipeDefProvider));

  // The defs a type declares itself via its provider interface's static `Definitions(string)` factory, or
  // empty when it declares none. DeclaredOnly matters: several blocks subclass a def-providing base (the
  // special pipes extend BlockPipe), and without it a derived class would return the base's inherited defs.
  // All three provider interfaces name the factory "Definitions", so one generic lookup serves them all.
  // A provider carrying [ExDefDomain] is handed that domain rather than the registering mod's, which is
  // what lets one assembly emit into several domains.
  private static IEnumerable<TDef> DefinitionsOf<TDef>(
    Type type,
    string domain,
    Type providerInterface
  ) {
    if (!providerInterface.IsAssignableFrom(type))
      return [];

    MethodInfo? define = type.GetMethod(
      "Definitions",
      BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
      binder: null,
      types: [typeof(string)],
      modifiers: null
    );
    string effective =
      type.GetCustomAttribute<ExDefDomainAttribute>()?.Domain ?? domain;
    return define?.Invoke(null, [effective]) as IEnumerable<TDef> ?? [];
  }

  /// <summary>
  /// Serializes every registered block definition to the synthetic assets the loader consumes:
  /// one <c>{domain}:blocktypes/{code}.json</c> per def, its bytes the def's JSON. Side-effect-free -
  /// the caller performs the <c>AssetManager.Add</c>. The <paramref name="origin"/> is stamped as each
  /// asset's <see cref="IAsset.Origin"/>.
  /// </summary>
  public static IEnumerable<(
    AssetLocation location,
    IAsset asset
  )> BuildBlockAssets(IAssetOrigin origin) =>
    BuildAssets(_blocks.Values, origin);

  /// <summary>Item-side sibling of <see cref="BuildBlockAssets"/>: one
  /// <c>{domain}:itemtypes/{code}.json</c> synthetic asset per registered item def.</summary>
  public static IEnumerable<(
    AssetLocation location,
    IAsset asset
  )> BuildItemAssets(IAssetOrigin origin) => BuildAssets(_items.Values, origin);

  /// <summary>Recipe-side sibling of <see cref="BuildBlockAssets"/>: one
  /// <c>{domain}:recipes/{category}/{name}.json</c> synthetic asset per registered recipe file.</summary>
  public static IEnumerable<(
    AssetLocation location,
    IAsset asset
  )> BuildRecipeAssets(IAssetOrigin origin) =>
    BuildAssets(_recipes.Values, origin);

  // Serializes each def to a synthetic asset at its own Location; shared by the three public Build* passes,
  // which differ only by which registry feeds them (IExDef's covariant Location/ToJson lets blocks, items and
  // recipes flow through together).
  private static IEnumerable<(
    AssetLocation location,
    IAsset asset
  )> BuildAssets(IEnumerable<IExDef> defs, IAssetOrigin origin) {
    foreach (IExDef def in defs) {
      // Parameterless ToString() (indented JSON): the payload only needs to be valid JSON for the loader
      // to parse. The Formatting overload is not exposed at runtime by the game's bundled Newtonsoft build.
      byte[] bytes = Encoding.UTF8.GetBytes(def.ToJson().ToString());
      yield return (
        def.Location,
        ExSyntheticAsset.Create(def.Location, bytes, origin)
      );
    }
  }
}
