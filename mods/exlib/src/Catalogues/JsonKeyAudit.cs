using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Catalogues;

/// <summary>
/// Compares a parsed <see cref="JsonObject"/>'s own keys against a schema's known set, for the
/// hand-rolled <c>JsonObject</c> parsers (route, job, bay) that have no deserializer to reject an
/// unknown key for them.
/// </summary>
internal static class JsonKeyAudit {
  /// <summary>Every key on <paramref name="node"/> that is not in <paramref name="known"/>. Empty when
  /// <paramref name="node"/> is absent or not an object (an array element, say), which is a shape error
  /// the parser itself already reports.</summary>
  public static IReadOnlyList<string> UnknownKeys(
    JsonObject? node,
    IReadOnlySet<string> known
  ) =>
    node?.Token is JObject obj
      ? [.. obj.Properties().Select(p => p.Name).Where(k => !known.Contains(k))]
      : [];
}
