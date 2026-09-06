using System;
using System.Collections.Generic;

namespace ExpandedLib.Registries;

/// <summary>
/// A process-wide, case-insensitive registry of items keyed by a string code derived from each item.
/// Register or replace by derived code, look up by code, and enumerate the registered codes and
/// values. Backs the mod-keyed catalogues (<see cref="Config.ExConfigProfiles"/>,
/// <see cref="ExRecipeProfiles"/>). Domain behaviour such as the recipe apply pipeline stays
/// on the owning class; this holds the keyed storage only.
/// </summary>
/// <typeparam name="T">The registered item type.</typeparam>
public sealed class ExKeyedRegistry<T> {
  private readonly Dictionary<string, T> _items = new(
    StringComparer.OrdinalIgnoreCase
  );
  private readonly Func<T, string> _key;

  /// <param name="keySelector">Derives the registry code from an item (e.g. <c>c =&gt; c.ModId</c>).</param>
  public ExKeyedRegistry(Func<T, string> keySelector) => _key = keySelector;

  /// <summary>Registers (or replaces) an item under its derived code.</summary>
  public void Register(T item) => _items[_key(item)] = item;

  /// <summary>Drops every registered item. Asset-backed catalogues repopulate on each
  /// <c>AssetsFinalize</c> and clear first, so entries do not accumulate across world reloads within
  /// one process.</summary>
  public void Clear() => _items.Clear();

  /// <summary>Looks up an item by code (case-insensitive); <c>false</c> when none is registered.</summary>
  public bool TryGet(string code, out T item) =>
    _items.TryGetValue(code, out item!);

  /// <summary>The registered codes.</summary>
  public IReadOnlyCollection<string> Codes => _items.Keys;

  /// <summary>The registered items.</summary>
  public IReadOnlyCollection<T> Values => _items.Values;
}
