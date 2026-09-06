using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries;

/// <summary>
/// Finds and drives the <see cref="IExModule"/>s of one mod - the entry points of the companion
/// assemblies it ships beside the one declaring its <see cref="ModSystem"/>.
/// </summary>
/// <remarks>
/// Discovery reads the assemblies the runtime has already loaded rather than the mod folder, so it
/// works the same for a folder mod and a zipped one, and needs no path handling: the game loads
/// every assembly in a mod folder while looking for its mod systems, which is how it comes to
/// complain about two. Ownership comes from <c>[assembly: ExDomain]</c> - see
/// <see cref="IExModule"/>.
/// </remarks>
public static class ExModules {
  // One mod's modules, in the order they are driven, cached because every phase asks again and the
  // answer cannot change: the set of loaded assemblies only grows, and one appearing mid-lifecycle
  // would be a mod folder the game has already finished inspecting.
  private static readonly Dictionary<string, IReadOnlyList<IExModule>> _byModId = new(
    StringComparer.OrdinalIgnoreCase
  );

  /// <summary>
  /// Every companion module belonging to <paramref name="modId"/>, ordered by
  /// <see cref="IExModule.Order"/>. Empty when the mod ships one assembly, which is the normal case.
  /// </summary>
  /// <param name="modId">The mod id its companion assemblies name in <c>[assembly: ExDomain]</c>.</param>
  public static IReadOnlyList<IExModule> For(string modId) {
    if (_byModId.TryGetValue(modId, out IReadOnlyList<IExModule>? cached))
      return cached;

    var found = new List<IExModule>();
    foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies()) {
      // An assembly with no ExDomain belongs to no mod as far as this is concerned: the game's own,
      // the runtime's, and any library a mod happens to ship.
      if (
        asm.GetCustomAttribute<ExDomainAttribute>() is not { } domain
        || !string.Equals(domain.Domain, modId, StringComparison.OrdinalIgnoreCase)
      )
        continue;

      foreach (Type type in ReflectionScan.GetCandidateTypes(asm)) {
        if (!typeof(IExModule).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
          continue;
        if (type.GetConstructor(Type.EmptyTypes) == null)
          continue;
        found.Add((IExModule)Activator.CreateInstance(type)!);
      }
    }

    IReadOnlyList<IExModule> ordered = [.. found.OrderBy(m => m.Order)];
    _byModId[modId] = ordered;
    return ordered;
  }

  /// <summary>
  /// Runs <paramref name="phase"/> on every module of <paramref name="mod"/>, in order. A module
  /// that throws is logged and skipped rather than taking the rest of the mod down with it: a
  /// companion assembly is a part of the mod, not the whole of it.
  /// </summary>
  public static void Drive(Mod mod, Action<IExModule> phase) {
    foreach (IExModule module in For(mod.Info.ModID)) {
      try {
        phase(module);
      } catch (Exception e) {
        mod.Logger.Error(
          "Companion module {0} threw; the rest of the mod continues without what it does.",
          module.GetType().FullName
        );
        mod.Logger.Error(e);
      }
    }
  }

  /// <summary>
  /// Registers every companion assembly's <c>[BlockRegister]</c>-style classes under
  /// <paramref name="mod"/>, then runs each module's own <see cref="IExModule.Start"/>. Called from
  /// the mod's <c>Start</c>; the registration happens first so a module's own code can rely on it.
  /// </summary>
  public static void Start(Mod mod, ICoreAPI api) {
    // By assembly, not by module: two modules in one companion assembly would otherwise register
    // its classes twice, and the game's own registries reject a duplicate key.
    var registered = new HashSet<Assembly>();
    foreach (IExModule module in For(mod.Info.ModID))
      if (registered.Add(module.GetType().Assembly))
        EntityRegistry.RegisterAll(api, mod, module.GetType().Assembly);
    Drive(mod, m => m.Start(api));
  }

  /// <summary>Clears the discovery cache. For tests, which build one AppDomain's worth of mods more
  /// than once and would otherwise see the previous run's modules.</summary>
  internal static void Reset() => _byModId.Clear();
}
