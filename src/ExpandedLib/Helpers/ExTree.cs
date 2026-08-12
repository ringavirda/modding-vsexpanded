using System.Text.Json;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Helpers;

/// <summary>
/// Helpers for reading values a block entity persisted into its attribute tree.
/// </summary>
public static class ExTree {
  /// <summary>
  /// Writes a string array as the tree's own <see cref="StringArrayAttribute"/> rather than as JSON text
  /// under a string key. <c>ToTreeAttributes</c> runs on every save and every <c>MarkDirty</c>, so for
  /// something every pipe in the world carries, a serialise-and-parse round trip is real cost on a hot
  /// path, and the text is a larger sync payload than the array it encodes.
  /// </summary>
  public static void SetStrings(
    this ITreeAttribute tree,
    string key,
    string[] values
  ) => tree[key] = new StringArrayAttribute(values);

  /// <summary>
  /// Reads back a <see cref="SetStrings"/> array, or <paramref name="fallback"/> when the key holds
  /// nothing of that shape. The default fallback is null rather than an empty array so that "never
  /// written" stays distinguishable from "written empty" - which is what lets a caller try this first
  /// and an older encoding of the same key second.
  /// </summary>
  public static string[]? GetStrings(
    this ITreeAttribute tree,
    string key,
    string[]? fallback = null
  ) => (tree[key] as StringArrayAttribute)?.value ?? fallback;

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
