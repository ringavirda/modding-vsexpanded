using System.Text.Json;

namespace ExpandedLib.Helpers;

/// <summary>
/// Helpers for reading values a block entity persisted into its attribute tree.
/// </summary>
public static class ExTree
{
  /// <summary>
  /// Deserializes a JSON string a block entity wrote into its tree, returning <paramref name="fallback"/>
  /// instead of throwing when the stored text is missing or malformed. Every other field in a
  /// <c>FromTreeAttributes</c> is read with the non-throwing <c>GetString</c>/<c>GetFloat</c> accessors
  /// that default silently; a raw <see cref="JsonSerializer.Deserialize{T}(string, JsonSerializerOptions)"/>
  /// is the one exception - and a throw there makes the engine discard the whole block entity on load,
  /// losing all its runtime state. Routing those reads through here makes the outlier field degrade like
  /// the rest.
  /// </summary>
  public static T SafeDeserialize<T>(string? json, T fallback)
  {
    if (string.IsNullOrEmpty(json))
      return fallback;

    try
    {
      return JsonSerializer.Deserialize<T>(json) ?? fallback;
    }
    catch (JsonException)
    {
      return fallback;
    }
  }
}
