using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Structures;

/// <summary>
/// The never-throwing reads the three multiblock attribute schemas share: a per-cell coordinate that
/// tolerates a missing or malformed token, and the "key maps to an array of cells" shape
/// <see cref="MultiblockCellRoles"/> and <see cref="MultiblockConnectors"/> both parse. Every entry
/// point here is read from a live block entity (<c>SetStructureAngle</c>, the server monitor tick, a
/// client <c>GetBlockInfo</c>), so nothing below throws on hand-edited or corrupted JSON.
/// </summary>
internal static class LayoutAttribute {
  /// <summary>
  /// One coordinate of a role or connector cell, or null when it is absent, is not a whole number, or
  /// does not fit an <c>int</c>. Pattern-matched rather than cast so no conversion here can throw.
  /// Newtonsoft stores a parsed integer as <c>long</c> and a programmatically built one as <c>int</c>;
  /// anything else, such as a <c>BigInteger</c>, falls through to null.
  /// </summary>
  public static int? Coord(JToken? token) =>
    token is not JValue { Type: JTokenType.Integer } value
      ? null
      : value.Value switch {
        int i => i,
        long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
        _ => null,
      };

  /// <summary>
  /// Every (cell, key) pair under <c>attrs[key]</c>, in file order: the attribute is an object whose
  /// values are arrays of <c>{x,y,z}</c> cells, one array per key. Empty for a missing attribute, a
  /// node that is not an object, a value that is not an array, or a cell missing a coordinate - the
  /// caller decides whether a key it does not recognise (a blank role, a non-facing letter) still
  /// counts, so no key filtering happens here.
  /// </summary>
  public static IEnumerable<(
    (int X, int Y, int Z) Cell,
    string Key
  )> CellsByKey(JsonObject? attrs, string key) {
    JsonObject? node = attrs?[key];
    if (node?.Exists != true || node.Token is not JObject obj)
      yield break;

    foreach (var kv in obj) {
      if (kv.Value is not JArray cells)
        continue;
      foreach (JToken cell in cells)
        if (
          cell is JObject o
          && Coord(o["x"]) is int x
          && Coord(o["y"]) is int y
          && Coord(o["z"]) is int z
        )
          yield return ((x, y, z), kv.Key);
    }
  }
}
