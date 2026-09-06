using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ExpandedLib.Testing;

/// <summary>
/// Reflection scan for a law that must hold across every concrete subclass of a base type in the
/// loaded assembly closure - iiex's own furnace branches, a downstream mod's, or one this harness has
/// never heard of. xUnit discovers tests per assembly, so a <c>[Fact]</c> declared in one mod's suite
/// never runs against another mod's leaf; this is the <c>FurnaceBranchGuards</c> idiom lifted so any
/// suite that hosts a leaf can call it without restating the scan.
/// </summary>
public static class RegistryLawScanner {
  /// <summary>
  /// Every concrete (non-abstract) type assignable to <typeparamref name="TBase"/>, across every
  /// assembly reachable from what this process has already loaded. Walked transitively rather than
  /// read off <see cref="AppDomain.GetAssemblies"/> alone, because references load lazily and a scan
  /// taken before anything has touched a mod's types would cover nothing and still pass - call this
  /// only after the subject type is known to be loaded.
  /// </summary>
  public static IReadOnlyList<Type> ConcreteSubclasses<TBase>() {
    var seen = new Dictionary<string, Assembly>(StringComparer.Ordinal);
    var pending = new Queue<Assembly>(AppDomain.CurrentDomain.GetAssemblies());

    while (pending.Count > 0) {
      Assembly asm = pending.Dequeue();
      if (
        asm.IsDynamic || !seen.TryAdd(asm.GetName().Name ?? asm.FullName!, asm)
      )
        continue;

      foreach (AssemblyName reference in asm.GetReferencedAssemblies()) {
        try {
          pending.Enqueue(Assembly.Load(reference));
        } catch {
          // A reference this host cannot resolve cannot contain a subclass this host could load either.
        }
      }
    }

    return seen
      .Values.SelectMany(asm => {
        try {
          return asm.GetTypes();
        } catch (ReflectionTypeLoadException ex) {
          // A half-loadable assembly still reports the types that did load.
          return ex.Types.Where(t => t is not null).Select(t => t!);
        }
      })
      .Where(t => !t.IsAbstract && typeof(TBase).IsAssignableFrom(t))
      .Distinct()
      .ToList();
  }

  /// <summary>
  /// Runs <paramref name="law"/> against every concrete subclass of <typeparamref name="TBase"/>,
  /// collecting every failure into one message instead of stopping at the first, so a law broken by
  /// three leaves names all three in one run rather than one retry at a time.
  /// </summary>
  /// <exception cref="InvalidOperationException"><paramref name="law"/> threw for at least one leaf.
  /// </exception>
  public static void ForEach<TBase>(Action<Type> law) {
    IReadOnlyList<Type> leaves = ConcreteSubclasses<TBase>();
    var failures = new List<string>();

    foreach (Type leaf in leaves) {
      try {
        law(leaf);
      } catch (Exception ex) {
        failures.Add($"{leaf.FullName}: {ex.Message}");
      }
    }

    if (failures.Count > 0)
      throw new InvalidOperationException(
        $"{failures.Count} of {leaves.Count} type(s) failed the law:\n  "
          + string.Join("\n  ", failures)
      );
  }
}
