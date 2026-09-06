using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries;

/// <summary>
/// Discovers every module in the process - an assembly carrying <c>[assembly: ExModule]</c> - and
/// orders each host's modules by <c>Requires</c>, keeping only the ones whose shipping mod is
/// enabled on the world being asked about.
/// </summary>
/// <remarks>
/// Discovery reads the assemblies the runtime has already loaded rather than any mod folder, so it
/// works the same for a folder mod and a zipped one, and needs no path handling: the game loads
/// every assembly in a mod folder while looking for its mod systems. See the wiki's Modules page.
/// </remarks>
public static class ExModules {
  // Memoised while AppDomain.CurrentDomain.GetAssemblies().Length is unchanged since the last scan;
  // rescanned whenever it differs, so an assembly loaded mid-process (a test loading one after the
  // first call, or the game's own mod folders finishing later than exlib's) is found on the very
  // next call rather than never. Never cached across a count change - a shrink is as much a change
  // as a growth, though the runtime does not actually unload assemblies.
  private static IReadOnlyList<ExModuleInfo>? _all;
  private static int _lastAssemblyCount = -1;

  // Entry-point ctor-validation errors, one list per ExModuleInfo instance rather than per id: two
  // assemblies may legally declare the same id until Order's dedup runs, and each keeps its own
  // entry-point errors independent of that.
  private static Dictionary<ExModuleInfo, List<string>> _entryPointErrors = new();

  /// <summary>Every module of every host, discovered once from the assemblies the runtime has
  /// loaded, sorted by assembly full name. Not filtered by whether its mod is enabled; see
  /// <see cref="Enabled"/> for that.</summary>
  public static IReadOnlyList<ExModuleInfo> All => Discover();

  /// <summary>
  /// <paramref name="host"/>'s modules whose shipping mod (<see cref="ExModuleInfo.Mod"/>) is
  /// enabled on <paramref name="api"/>'s world, in dependency order (see <see cref="Order"/>). A
  /// module whose mod is not enabled is left out and logged once at Notification level through
  /// <paramref name="api"/>'s logger, naming both. Empty (with no errors) for a host with no enabled
  /// modules, which is the normal case.
  /// </summary>
  public static ExModuleSet For(ICoreAPI api, string host) {
    var candidates = new List<ExModuleInfo>();
    foreach (ExModuleInfo module in All) {
      if (!string.Equals(module.Host, host, StringComparison.OrdinalIgnoreCase))
        continue;
      if (!api.ModLoader.IsModEnabled(module.Mod)) {
        api.Logger.Notification(
          "[exlib] module {0} skipped: mod {1} is not enabled",
          module.Id,
          module.Mod
        );
        continue;
      }
      candidates.Add(module);
    }

    ExModuleSet ordered = Order(candidates);

    List<string> errors = [.. ordered.Errors];
    foreach (ExModuleInfo module in ordered.Modules)
      if (_entryPointErrors.TryGetValue(module, out List<string>? lines))
        errors.AddRange(lines);
    return errors.Count > ordered.Errors.Count ? ordered with { Errors = errors } : ordered;
  }

  /// <summary>Every module of every host whose shipping mod is enabled on <paramref name="api"/>'s
  /// world, unordered. Used to set <see cref="FlagKey"/> for every module a world actually runs,
  /// regardless of which host drives it.</summary>
  public static IReadOnlyList<ExModuleInfo> Enabled(ICoreAPI api) =>
    [.. All.Where(m => api.ModLoader.IsModEnabled(m.Mod))];

  /// <summary>Whether an enabled module of the given id was discovered, on any host.</summary>
  public static bool IsLoaded(ICoreAPI api, string moduleId) =>
    Enabled(api).Any(m => string.Equals(m.Id, moduleId, StringComparison.OrdinalIgnoreCase));

  /// <summary>The world-config key a module sets to true while it is loaded, for a JSON patch
  /// condition to gate on.</summary>
  public static string FlagKey(string moduleId) => "exlib:module:" + moduleId;

  /// <summary>
  /// Orders <paramref name="modules"/> by <c>Requires</c> (Kahn's algorithm; ties broken by id,
  /// <see cref="StringComparer.OrdinalIgnoreCase"/> throughout) and reports what could not be
  /// placed. Two modules sharing one id (any case) are a duplicate: the first (in
  /// <paramref name="modules"/> order) stands, the rest are excluded, one error naming the first
  /// and each excluded one's assembly. A module naming a <c>Requires</c> id not present among the
  /// survivors is excluded on its own; every module still part of a cycle after that is excluded
  /// together, one error naming them all. Pure - takes no dependency on discovery, for tests to
  /// build hand-made sets against.
  /// </summary>
  internal static ExModuleSet Order(IEnumerable<ExModuleInfo> modules) {
    var errors = new List<string>();
    var byId = new Dictionary<string, ExModuleInfo>(StringComparer.OrdinalIgnoreCase);
    var input = new List<ExModuleInfo>();
    foreach (ExModuleInfo module in modules) {
      if (byId.TryGetValue(module.Id, out ExModuleInfo? existing)) {
        errors.Add(
          $"module {existing.Id} is declared by both {existing.Assembly.GetName().Name} and "
            + $"{module.Assembly.GetName().Name}; {module.Assembly.GetName().Name} ignored"
        );
        continue;
      }
      byId[module.Id] = module;
      input.Add(module);
    }

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

    var survivorIds = new HashSet<string>(
      survivors.Select(m => m.Id),
      StringComparer.OrdinalIgnoreCase
    );
    var indegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
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

    var ready = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
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
        .. survivors
          .Select(m => m.Id)
          .Except(orderedIds, StringComparer.OrdinalIgnoreCase)
          .OrderBy(id => id, StringComparer.OrdinalIgnoreCase),
      ];
      errors.Add(
        $"modules {string.Join(", ", cycle)} form a requires cycle; none of them are driven"
      );
    }

    return new ExModuleSet([.. orderedIds.Select(id => byId[id])], errors);
  }

  private static IReadOnlyList<ExModuleInfo> Discover() {
    int count = AppDomain.CurrentDomain.GetAssemblies().Length;
    if (_all != null && count == _lastAssemblyCount)
      return _all;

    var found = new List<ExModuleInfo>();
    var entryPointErrors = new Dictionary<ExModuleInfo, List<string>>();

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
        Mod = attr.Mod,
        Requires = attr.Requires,
        Assembly = asm,
        EntryPoints = entryPoints,
        PatchHarmony = attr.PatchHarmony,
      };

      found.Add(info);
      if (ctorErrors.Count > 0)
        entryPointErrors[info] = ctorErrors;
    }

    _all = found;
    _entryPointErrors = entryPointErrors;
    _lastAssemblyCount = count;
    return _all;
  }
}
