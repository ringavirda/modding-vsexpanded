using System.Text.Json;

namespace ExpandedLib.Helpers;

/// <summary>
/// Helpers for reading values a block entity persisted into its attribute tree.
/// </summary>
public static class ExTree {
  /// <summary>
  /// Deserializes a JSON string a block entity wrote into its tree, returning <paramref name="fallback"/>
  /// instead of throwing when the stored text is missing or malformed. A throw inside
  /// <c>FromTreeAttributes</c> makes the engine discard the whole block entity on load, losing its
  /// runtime state, and the other tree accessors already default silently.
  /// </summary>
  public static T SafeDeserialize<T>(string? json, T fallback) {
    if (string.IsNullOrEmpty(json))
      return fallback;

    try {
      return JsonSerializer.Deserialize<T>(json) ?? fallback;
    } catch (JsonException) {
      return fallback;
    }
  }
}
