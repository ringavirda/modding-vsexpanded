using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// Reads the <c>multiblockRoles</c> attribute a code-first layout emits: <see cref="CellRole"/> to the authored
/// (north-frame) offsets of the cells carrying that role. The offsets are the ones the author drew, not world
/// positions and not rotated ones; <see cref="BlockEntityMultiblockStructure.CellsWithRole"/> resolves an
/// authored offset against vanilla's own <c>Offsets</c>/<c>TransformedOffsets</c> pair, so a role cell is turned
/// by exactly the rotation the completion check applied and this type holds no rotation maths. It is a sibling
/// attribute of <c>multiblockStructure</c> rather than a member of it, because vanilla's own
/// <c>MultiblockStructure</c> deserialises that object and it must keep exactly that schema. A layout that
/// declares no roles gets <see cref="None"/>.
/// </summary>
public sealed class MultiblockCellRoles {
  /// <summary>A layout that declares no roles. Every lookup answers empty.</summary>
  public static readonly MultiblockCellRoles None = new(
    new Dictionary<CellRole, HashSet<(int X, int Y, int Z)>>()
  );

  private static readonly HashSet<(int X, int Y, int Z)> NoCells = new();

  private readonly Dictionary<
    CellRole,
    HashSet<(int X, int Y, int Z)>
  > _cellsOf;

  private MultiblockCellRoles(
    Dictionary<CellRole, HashSet<(int X, int Y, int Z)>> cellsOf
  ) => _cellsOf = cellsOf;

  /// <summary>True when the layout marks no cell with any role.</summary>
  public bool IsEmpty => _cellsOf.Count == 0;

  /// <summary>
  /// The authored offsets carrying <paramref name="role"/>, empty when the layout declares none. A set,
  /// because the caller tests membership per footprint cell and <c>MultiblockBuilder.At</c> already rejects
  /// two cells at one offset.
  /// </summary>
  public IReadOnlySet<(int X, int Y, int Z)> CellsOf(CellRole role) =>
    _cellsOf.TryGetValue(role, out var cells) ? cells : NoCells;

  /// <summary>
  /// Reads the <c>multiblockRoles</c> attribute. Returns <see cref="None"/> for a block that declares none.
  /// Never throws: it is re-read from <see cref="BlockEntityMultiblockStructure.SetStructureAngle"/>, on the
  /// server monitor tick and on a client <c>GetBlockInfo</c>, where an exception would repeat on a live block
  /// entity mid-session. Malformed entries are therefore skipped and every value is type-checked rather than
  /// cast, since <c>(int)JToken</c> throws on a string, an object, or a number too wide for an <c>int</c>.
  /// </summary>
  public static MultiblockCellRoles FromAttributes(JsonObject? attributes) {
    JsonObject? node = attributes?["multiblockRoles"];
    if (node?.Exists != true)
      return None;

    // JsonObject has no key enumeration, so read the underlying token directly.
    if (node.Token is not JObject obj)
      return None;

    var map = new Dictionary<CellRole, HashSet<(int X, int Y, int Z)>>();
    foreach (var kv in obj) {
      // IsDefined as well as TryParse: TryParse alone admits a numeric string ("99") and a comma-composed
      // one ("Flue, Damper"), either of which would put an undefined CellRole in the map, answering nothing
      // while making IsEmpty claim the layout has roles.
      if (
        !Enum.TryParse(kv.Key, ignoreCase: true, out CellRole role)
        || !Enum.IsDefined(role)
      )
        continue;
      if (kv.Value is not JArray cells)
        continue;

      var offsets = new HashSet<(int X, int Y, int Z)>();
      foreach (JToken cell in cells)
        if (
          cell is JObject o
          && Coord(o["x"]) is int x
          && Coord(o["y"]) is int y
          && Coord(o["z"]) is int z
        )
          offsets.Add((x, y, z));

      if (offsets.Count > 0)
        map[role] = offsets;
    }
    return map.Count == 0 ? None : new MultiblockCellRoles(map);
  }

  /// <summary>
  /// One coordinate of a role cell, or null when it is absent, is not a whole number, or does not fit an
  /// <c>int</c>. Pattern-matched rather than cast so no conversion here can throw (see
  /// <see cref="FromAttributes"/>). Newtonsoft stores a parsed integer as <c>long</c> and a programmatically
  /// built one as <c>int</c>; anything else, such as a <c>BigInteger</c>, falls through to null.
  /// </summary>
  private static int? Coord(JToken? token) =>
    token is not JValue { Type: JTokenType.Integer } value
      ? null
      : value.Value switch {
        int i => i,
        long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
        _ => null,
      };
}
