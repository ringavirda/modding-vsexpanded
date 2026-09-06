using System.Collections.Generic;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Structures;

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
    var map = new Dictionary<CellRole, HashSet<(int X, int Y, int Z)>>();
    foreach (var (cell, key) in LayoutAttribute.CellsByKey(attributes, "multiblockRoles")) {
      // A blank key cannot be a role - Of() would throw - so it is skipped rather than let through to
      // poison the map with a role no caller could ever ask for by the same key.
      if (string.IsNullOrWhiteSpace(key))
        continue;
      // The primary constructor, not Of(): this is reading a role someone else already declared back
      // out of JSON, not declaring one, and Of() would reset whatever arity that declaration gave it.
      var role = new CellRole(key);
      if (!map.TryGetValue(role, out HashSet<(int X, int Y, int Z)>? offsets))
        map[role] = offsets = [];
      offsets.Add(cell);
    }
    return map.Count == 0 ? None : new MultiblockCellRoles(map);
  }
}
