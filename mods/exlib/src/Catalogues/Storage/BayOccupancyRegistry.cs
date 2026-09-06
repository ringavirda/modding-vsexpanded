using System;
using System.Collections.Generic;
using ExpandedLib.Registries;

namespace ExpandedLib.Catalogues;

/// <summary>
/// The merged catalogue of what every item occupies, keyed by store. Contributed to rather than owned, as
/// <c>ProcessJobRegistry</c> is: a mod makes its own stock rackable by shipping a file, never by patching
/// ours. A second rule for one item is reported and the first stands, because taking the last writer
/// would make a rack's capacity depend on mod load order.
/// <para>
/// It is also the store's whitelist. An item no rule names is not stored at all - a rack takes what its
/// catalogue lists, which is what keeps it from becoming a chest that has to render arbitrary items.
/// </para>
/// </summary>
public sealed class BayOccupancyRegistry {
  /// <summary>The process-wide catalogue, repopulated at <c>AssetsFinalize</c>.</summary>
  public static BayOccupancyRegistry Shared { get; } = new();

  /// <summary>Code contributions to <see cref="Shared"/>, invoked by <c>BayOccupancyLoader</c> after
  /// its JSON read on every <c>Load(ICoreAPI)</c>, so a rule registered from C# survives the clear
  /// that precedes it.</summary>
  public static CatalogueContributors Contributors { get; } = new();

  private readonly Dictionary<string, List<BayOccupancy>> _byStore = new(
    StringComparer.OrdinalIgnoreCase
  );

  /// <summary>
  /// Merges <paramref name="set"/> into the store it names. Returns one human-readable message per rule
  /// whose item another rule already sizes differently.
  /// </summary>
  public IReadOnlyList<string> Contribute(BayOccupancySet set) {
    if (!_byStore.TryGetValue(set.Store, out List<BayOccupancy>? rules))
      _byStore[set.Store] = rules = [];

    var conflicts = new List<string>();
    foreach (BayOccupancy rule in set.Rules) {
      BayOccupancy? held = rules.Find(r =>
        string.Equals(r.Item, rule.Item, StringComparison.OrdinalIgnoreCase)
      );
      if (held != null) {
        if (held.Cells != rule.Cells)
          conflicts.Add(
            $"{set.Store}: '{rule.Item}' already occupies {held.Cells} cell(s), so {rule.Cells} is "
              + "ignored; the first declaration stands"
          );
        continue;
      }
      rules.Add(rule);
    }
    return conflicts;
  }

  /// <summary>Every rule <paramref name="store"/> carries, in declaration order.</summary>
  public IReadOnlyList<BayOccupancy> Rules(string? store) =>
    store != null && _byStore.TryGetValue(store, out List<BayOccupancy>? rules)
      ? rules
      : [];

  /// <summary>
  /// How many cells a stack of <paramref name="code"/> occupies in <paramref name="store"/>, or null when
  /// the store's catalogue does not list it - which is the store refusing it. The most specific rule
  /// wins, so an exact code beats a family wildcard however they were declared.
  /// </summary>
  public int? CellsFor(string? store, string? code) {
    if (code == null)
      return null;

    BayOccupancy? best = null;
    foreach (BayOccupancy rule in Rules(store))
      if (
        rule.Matches(code) && (best == null || rule.Precision > best.Precision)
      )
        best = rule;
    return best?.Cells;
  }

  /// <summary>Empties the catalogue. Asset reload repopulates it; a test uses it to stand up its
  /// own.</summary>
  public void Clear() => _byStore.Clear();
}
