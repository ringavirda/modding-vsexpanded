using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// Reads the <c>multiblockRoles</c> attribute a code-first layout emits: <see cref="CellRole"/> → the
/// <b>authored</b> (north-frame) offsets of the cells carrying that role.
/// <para>
/// The offsets are the ones the author drew, not world positions and not rotated ones. Rotation is not this
/// type's business: <see cref="BlockEntityMultiblockStructure.CellsWithRole"/> resolves an authored offset
/// against vanilla's own <c>Offsets</c>/<c>TransformedOffsets</c> pair, so a role cell is turned by exactly
/// the rotation the completion check already applied and there is no second copy of the rotation maths to get
/// wrong. See that method for why.
/// </para>
/// <para>
/// Mirrors <see cref="MultiblockFacings"/> in every respect that matters: a sibling attribute rather than a
/// member of <c>multiblockStructure</c> (that object is deserialised by vanilla's own
/// <c>MultiblockStructure</c> and must stay exactly its schema), and a layout that declares nothing gets
/// <see cref="None"/> and behaves precisely as it always did - which is why roles need no migration.
/// </para>
/// </summary>
public sealed class MultiblockCellRoles
{
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
  /// because the caller's job is a membership test per footprint cell and a layout may not draw two cells at
  /// one offset anyway (<c>MultiblockBuilder.At</c> rejects duplicates).
  /// </summary>
  public IReadOnlySet<(int X, int Y, int Z)> CellsOf(CellRole role) =>
    _cellsOf.TryGetValue(role, out var cells) ? cells : NoCells;

  /// <summary>
  /// Reads the <c>multiblockRoles</c> attribute. Returns <see cref="None"/> for a block that declares none -
  /// including every layout authored before this existed.
  /// <para>
  /// Unparseable entries are skipped rather than thrown on: the authoring side already fails the build for
  /// every way this can be got wrong (<c>MultiblockLayoutBuilder</c>), so anything malformed here came from a
  /// hand-edited JSON patch, and a mod that refuses to load is a worse answer than a role that finds nothing.
  /// </para>
  /// <para>
  /// That is a <b>total</b> promise, not a best-effort one, because of where this runs: it is re-read in
  /// <see cref="BlockEntityMultiblockStructure.SetStructureAngle"/>, i.e. off the server monitor tick and off
  /// a client <c>GetBlockInfo</c>. A throw there is not a refusal to load - it is a repeating exception on a
  /// live block entity mid-session, which is the worse failure the skip exists to avoid. So every value read
  /// below is <b>type-checked</b> rather than cast: <c>(int)JToken</c> throws on a string, on an object and on
  /// a number too wide for an <c>int</c>.
  /// </para>
  /// </summary>
  public static MultiblockCellRoles FromAttributes(JsonObject? attributes)
  {
    JsonObject? node = attributes?["multiblockRoles"];
    if (node?.Exists != true)
      return None;

    // JsonObject has no key enumeration, so read the underlying token directly - the same route
    // MultiblockFacings takes.
    if (node.Token is not JObject obj)
      return None;

    var map = new Dictionary<CellRole, HashSet<(int X, int Y, int Z)>>();
    foreach (var kv in obj)
    {
      // IsDefined as well as TryParse: TryParse alone admits a numeric string ("99") and a comma-composed
      // one ("Flue, Damper"), both of which land an undefined CellRole in the map. No caller could ever ask
      // for one, so it would sit there answering nothing while making IsEmpty claim the layout has roles.
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
  /// <c>int</c>. Pattern-matched rather than cast so there is no conversion left that can throw - see the
  /// remark on <see cref="FromAttributes"/> for why a throw here is the worse answer. Newtonsoft stores a
  /// parsed integer as <c>long</c> and a programmatically built one as <c>int</c>; anything else (a
  /// <c>BigInteger</c> from an absurd literal) falls through to the skip.
  /// </summary>
  private static int? Coord(JToken? token) =>
    token is not JValue { Type: JTokenType.Integer } value
      ? null
      : value.Value switch
      {
        int i => i,
        long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
        _ => null,
      };
}
