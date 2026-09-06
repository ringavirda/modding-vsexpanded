using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries;

/// <summary>
/// Discovers every module in the process - an assembly carrying <c>[assembly: ExModule]</c> - and
/// orders each host's modules by <c>Requires</c>.
/// </summary>
/// <remarks>
/// Discovery reads the assemblies the runtime has already loaded rather than any mod folder, so it
/// works the same for a folder mod and a zipped one, and needs no path handling: the game loads
/// every assembly in a mod folder while looking for its mod systems. See the wiki's Modules page.
/// </remarks>
public static class ExModules {
  // Discovered once per process; the set of loaded assemblies only grows, and one appearing
  // mid-lifecycle would be a mod folder the game has already finished inspecting.
  private static IReadOnlyList<ExModuleInfo>? _all;

  // Error lines that belong to one module specifically (a bad entry point, or a duplicate id) and
  // so must be repeated in every host set that ends up containing that module.
  private static Dictionary<string, List<string>> _perModuleErrors = [];

  // One host's ordered set, cached because every phase asks again and the answer cannot change once
  // discovery has run.
  private static readonly Dictionary<string, ExModuleSet> _byHost = new(
    StringComparer.OrdinalIgnoreCase
  );

  // Interim: entry-point instances, one list per host, flattened from For(host) in module then
  // entry-point order and cached exactly as the pre-module-system code cached its instances.
  // ExModuleHost replaces this in Task 2.
  private static readonly Dictionary<string, IReadOnlyList<IExModule>> _instancesByHost = new(
    StringComparer.OrdinalIgnoreCase
  );

  /// <summary>Every module of every host, discovered once from the assemblies the runtime has
  /// loaded, sorted by assembly full name.</summary>
  public static IReadOnlyList<ExModuleInfo> All {
    get {
      if (_all == null)
        Discover();
      return _all!;
    }
  }

  /// <summary>
  /// <paramref name="host"/>'s modules, in dependency order (see <see cref="Order"/>), cached per
  /// host. Empty (with no errors) for a host with no modules, which is the normal case.
  /// </summary>
  public static ExModuleSet For(string host) {
    if (_byHost.TryGetValue(host, out ExModuleSet? cached))
      return cached;

    ExModuleSet ordered = Order(
      All.Where(m => string.Equals(m.Host, host, StringComparison.OrdinalIgnoreCase))
    );

    List<string> errors = [.. ordered.Errors];
    foreach (ExModuleInfo module in ordered.Modules)
      if (_perModuleErrors.TryGetValue(module.Id, out List<string>? lines))
        errors.AddRange(lines);
    if (errors.Count > ordered.Errors.Count)
      ordered = ordered with { Errors = errors };

    _byHost[host] = ordered;
    return ordered;
  }

  /// <summary>Whether a module of the given id was discovered, on any host.</summary>
  public static bool IsLoaded(string moduleId) =>
    All.Any(m => string.Equals(m.Id, moduleId, StringComparison.OrdinalIgnoreCase));

  /// <summary>The world-config key a module sets to true while it is loaded, for a JSON patch
  /// condition to gate on.</summary>
  public static string FlagKey(string moduleId) => "exlib:module:" + moduleId;

  /// <summary>
  /// Orders <paramref name="modules"/> by <c>Requires</c> (Kahn's algorithm; ties broken by id) and
  /// reports what could not be placed. A module naming a requirement not present in
  /// <paramref name="modules"/> is excluded on its own; every module still part of a cycle after
  /// that is excluded together, one error naming them all. Pure - takes no dependency on discovery,
  /// for tests to build hand-made sets against.
  /// </summary>
  internal static ExModuleSet Order(IEnumerable<ExModuleInfo> modules) {
    var input = modules.ToList();
    var byId = new Dictionary<string, ExModuleInfo>(StringComparer.Ordinal);
    foreach (ExModuleInfo module in input)
      byId[module.Id] = module;

    var errors = new List<string>();
    var survivors = new List<ExModuleInfo>();
    foreach (ExModuleInfo module in input) {
      List<string> missing = [.. module.Requires.Where(r => !byId.ContainsKey(r))];
      if (missing.Count > 0) {
        foreach (string other in missing)
          errors.Add(
            $"module {module.Id} requires {other}, which is not loaded; {module.Id} is not driven"
          );
        continue;
      }
      survivors.Add(module);
    }

    var survivorIds = new HashSet<string>(survivors.Select(m => m.Id), StringComparer.Ordinal);
    var indegree = new Dictionary<string, int>(StringComparer.Ordinal);
    var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    foreach (ExModuleInfo module in survivors) {
      indegree[module.Id] = module.Requires.Count(survivorIds.Contains);
      foreach (string required in module.Requires) {
        if (!survivorIds.Contains(required))
          continue;
        if (!dependents.TryGetValue(required, out List<string>? deps))
          dependents[required] = deps = [];
        deps.Add(module.Id);
      }
    }

    var ready = new SortedSet<string>(StringComparer.Ordinal);
    foreach (ExModuleInfo module in survivors)
      if (indegree[module.Id] == 0)
        ready.Add(module.Id);

    var orderedIds = new List<string>();
    while (ready.Count > 0) {
      string id = ready.Min!;
      ready.Remove(id);
      orderedIds.Add(id);
      if (!dependents.TryGetValue(id, out List<string>? deps))
        continue;
      foreach (string dependent in deps) {
        indegree[dependent]--;
        if (indegree[dependent] == 0)
          ready.Add(dependent);
      }
    }

    if (orderedIds.Count < survivors.Count) {
      List<string> cycle = [
        .. survivors.Select(m => m.Id).Except(orderedIds).OrderBy(id => id, StringComparer.Ordinal),
      ];
      errors.Add(
        $"modules {string.Join(", ", cycle)} form a requires cycle; none of them are driven"
      );
    }

    return new ExModuleSet([.. orderedIds.Select(id => byId[id])], errors);
  }

  /// <summary>Clears every cache. For tests, which build one AppDomain's worth of modules more than
  /// once and would otherwise see a previous run's discovery.</summary>
  internal static void Reset() {
    _all = null;
    _perModuleErrors = [];
    _byHost.Clear();
    _instancesByHost.Clear();
  }

  private static void Discover() {
    var byId = new Dictionary<string, ExModuleInfo>(StringComparer.Ordinal);
    var perModuleErrors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

    IEnumerable<Assembly> assemblies = AppDomain
      .CurrentDomain.GetAssemblies()
      .OrderBy(a => a.FullName, StringComparer.Ordinal);

    foreach (Assembly asm in assemblies) {
      if (asm.GetCustomAttribute<ExModuleAttribute>() is not { } attr)
        continue;

      var entryPoints = new List<Type>();
      var ctorErrors = new List<string>();
      IEnumerable<Type> candidates = ReflectionScan
        .GetCandidateTypes(asm)
        .Where(t => typeof(IExModule).IsAssignableFrom(t))
        .OrderBy(t => t.FullName, StringComparer.Ordinal);
      foreach (Type type in candidates) {
        if (type.GetConstructor(Type.EmptyTypes) == null) {
          ctorErrors.Add(
            $"module {attr.Id}: entry point {type.FullName} has no parameterless constructor; skipped"
          );
          continue;
        }
        entryPoints.Add(type);
      }

      var info = new ExModuleInfo {
        Id = attr.Id,
        Host = attr.Host,
        Requires = attr.Requires,
        Assembly = asm,
        EntryPoints = entryPoints,
        PatchHarmony = attr.PatchHarmony,
      };

      if (byId.TryGetValue(attr.Id, out ExModuleInfo? existing)) {
        if (!perModuleErrors.TryGetValue(attr.Id, out List<string>? dupErrors))
          perModuleErrors[attr.Id] = dupErrors = [];
        dupErrors.Add(
          $"module {attr.Id} is declared by both {existing.Assembly.GetName().Name} and "
            + $"{asm.GetName().Name}; {asm.GetName().Name} ignored"
        );
        continue;
      }

      byId[attr.Id] = info;
      if (ctorErrors.Count > 0)
        perModuleErrors[attr.Id] = ctorErrors;
    }

    _all = [.. byId.Values];
    _perModuleErrors = perModuleErrors;
  }

  // Interim: everything below drives modules through instances the same way the pre-module-system
  // ExModules did, so ExModSystem and ExModuleModSystem keep working unchanged until ExModuleHost
  // replaces both the instances and these two methods in Task 2.

  internal static void Drive(Mod mod, Action<IExModule> phase) {
    foreach (IExModule module in Instances(mod.Info.ModID)) {
      try {
        phase(module);
      } catch (Exception e) {
        mod.Logger.Error(
          "Module {0} threw; the rest of the host continues without what it does.",
          module.GetType().FullName
        );
        mod.Logger.Error(e);
      }
    }
  }

  internal static void Start(Mod mod, ICoreAPI api) {
    var registered = new HashSet<Assembly>();
    foreach (ExModuleInfo info in For(mod.Info.ModID).Modules)
      if (registered.Add(info.Assembly))
        EntityRegistry.RegisterAll(api, mod, info.Assembly);
    Drive(mod, m => m.Start(api));
  }

  private static IReadOnlyList<IExModule> Instances(string host) {
    if (_instancesByHost.TryGetValue(host, out IReadOnlyList<IExModule>? cached))
      return cached;

    var instances = new List<IExModule>();
    foreach (ExModuleInfo info in For(host).Modules)
      foreach (Type type in info.EntryPoints)
        instances.Add((IExModule)Activator.CreateInstance(type)!);

    _instancesByHost[host] = instances;
    return instances;
  }
}
