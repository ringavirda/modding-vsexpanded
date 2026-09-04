using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Processes;

/// <summary>
/// Turns a plain object into the <see cref="JsonObject"/> the spec parsers read, so the C# registration
/// route can build the declaration a file would have held and run it through the same parser rather than
/// re-stating its rules.
/// </summary>
internal static class ExJson {
  /// <summary>The object as a parser-ready node.</summary>
  internal static JsonObject Of(object value) => new(JToken.FromObject(value));
}
