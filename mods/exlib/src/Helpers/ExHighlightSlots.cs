using System.Collections.Generic;

namespace ExpandedLib.Helpers;

/// <summary>
/// Process-wide handout of distinct <c>IWorldAccessor.HighlightBlocks</c> slot ids, so two features
/// never collide by picking the same literal by hand. Ids start at <see cref="Base"/>, chosen well
/// above vanilla's own reserved slots (e.g. <c>MultiblockStructure.HighlightSlotId</c> = 23) and any
/// id a mod already picked by hand before this existed.
/// </summary>
public static class ExHighlightSlots {
  /// <summary>The first id <see cref="Reserve"/> hands out.</summary>
  public const int Base = 90000;

  private static readonly Dictionary<string, int> _slots = new();
  private static int _next = Base;

  /// <summary>
  /// The highlight slot id reserved for <paramref name="key"/>: the same id on every call for the same
  /// key, a freshly handed-out one the first time that key is seen. Call once per feature - a static
  /// field initializer, say - and keep the result rather than reserving on every highlight.
  /// </summary>
  public static int Reserve(string key) {
    if (_slots.TryGetValue(key, out int id))
      return id;
    id = _next++;
    _slots[key] = id;
    return id;
  }
}
