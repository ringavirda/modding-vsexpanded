using System.Collections.Generic;
using System.Linq;

namespace ExpandedLib.Testing;

/// <summary>One mod's contribution to the release history: what it has ever shipped, its highest
/// published version, and the codes from that shipping it has not yet migrated.</summary>
public sealed record ReleasedModHistory(
  IReadOnlyList<ReleasedCodes.Shipped> Shipped,
  IReadOnlyList<ReleasedCodes.ShippedEntityClass> EntityClasses,
  IReadOnlyDictionary<string, string> Versions,
  IReadOnlyList<string> Debt
);

/// <summary>
/// Registry of released-code history, one entry per mod. Each mod's own test <c>ModuleInit</c>
/// registers its shipped codes, entity classes, published versions and migration debt exactly once;
/// <see cref="ReleasedCodes"/>, <see cref="ReleasedVersions"/> and <see cref="ReleasedCodeDebt"/> read
/// it back through their old entry points, so the harness itself carries no mod-specific history.
/// </summary>
public static class ReleasedHistory {
  private static readonly Dictionary<string, ReleasedModHistory> ByMod = new();

  /// <summary>Registers <paramref name="mod"/>'s shipped history. Call once, from that mod's test
  /// <c>ModuleInit</c>; a second call for the same mod replaces the first.</summary>
  public static void Register(
    string mod,
    IReadOnlyList<ReleasedCodes.Shipped> shipped,
    IReadOnlyList<ReleasedCodes.ShippedEntityClass> entityClasses,
    IReadOnlyDictionary<string, string> versions,
    IReadOnlyList<string> debt
  ) =>
    ByMod[mod] = new ReleasedModHistory(shipped, entityClasses, versions, debt);

  /// <summary><paramref name="mod"/>'s registered history, or null if it has never shipped.</summary>
  public static ReleasedModHistory? For(string mod) =>
    ByMod.TryGetValue(mod, out ReleasedModHistory? history) ? history : null;

  /// <summary>Every shipped blocktype across every registered mod.</summary>
  public static IEnumerable<ReleasedCodes.Shipped> AllShipped =>
    ByMod.Values.SelectMany(h => h.Shipped);

  /// <summary>Every shipped block-entity class across every registered mod.</summary>
  public static IEnumerable<ReleasedCodes.ShippedEntityClass> AllEntityClasses =>
    ByMod.Values.SelectMany(h => h.EntityClasses);

  /// <summary>The highest published version per modid, across every registered mod.</summary>
  public static IReadOnlyDictionary<string, string> AllVersions =>
    ByMod
      .Values.SelectMany(h => h.Versions)
      .ToDictionary(kv => kv.Key, kv => kv.Value);

  /// <summary>Every recorded migration-debt code across every registered mod.</summary>
  public static IEnumerable<string> AllDebt =>
    ByMod.Values.SelectMany(h => h.Debt);
}
