using System.Collections.Generic;
using ExpandedLib.Helpers;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Structures;

/// <summary>
/// Reads the <c>multiblockConnectors</c> attribute a code-first layout emits: for each authored
/// (north-frame) offset, the outward faces that cell's occupant must expose a network connector on.
/// A sibling attribute of <c>multiblockStructure</c> for the same reason
/// <see cref="MultiblockCellRoles"/> is one - vanilla deserialises that object and it must keep exactly
/// its schema. A layout that marks no connector gets <see cref="None"/>.
/// <para>
/// This states what the layout could otherwise only say by pinning the node's orientation variant in
/// its legend, which a network node's own neighbour scan is free to overwrite. The faces are authored,
/// so <see cref="BlockEntityMultiblockStructure"/> turns them by the structure's own init angle.
/// </para>
/// </summary>
public sealed class MultiblockConnectors {
  /// <summary>A layout that marks no connector. Every lookup answers empty.</summary>
  public static readonly MultiblockConnectors None = new(
    new Dictionary<(int X, int Y, int Z), List<string>>()
  );

  private static readonly string[] NoFaces = [];

  private readonly Dictionary<(int X, int Y, int Z), List<string>> _facesAt;

  private MultiblockConnectors(
    Dictionary<(int X, int Y, int Z), List<string>> facesAt
  ) => _facesAt = facesAt;

  /// <summary>True when the layout marks no cell with any connector demand.</summary>
  public bool IsEmpty => _facesAt.Count == 0;

  /// <summary>
  /// The authored outward face letters <paramref name="authoredOffset"/> demands, empty when that cell
  /// demands none. Authored, not rotated: the caller holds the structure's angle.
  /// </summary>
  public IReadOnlyList<string> OutwardFacesAt(
    (int X, int Y, int Z) authoredOffset
  ) => _facesAt.TryGetValue(authoredOffset, out var faces) ? faces : NoFaces;

  /// <summary>
  /// Reads the <c>multiblockConnectors</c> attribute, keyed face letter to the cells demanding it and
  /// inverted here to cell to faces. Returns <see cref="None"/> for a block that declares none. Never
  /// throws, for the reason <see cref="MultiblockCellRoles.FromAttributes"/> does not: it is re-read
  /// from <see cref="BlockEntityMultiblockStructure.SetStructureAngle"/>, on the server monitor tick and
  /// on a client <c>GetBlockInfo</c>, where an exception repeats on a live block entity mid-session.
  /// </summary>
  public static MultiblockConnectors FromAttributes(JsonObject? attributes) {
    var map = new Dictionary<(int X, int Y, int Z), List<string>>();
    foreach (var (cell, key) in LayoutAttribute.CellsByKey(attributes, "multiblockConnectors")) {
      // A key that is not a horizontal side letter would be a face no rotation can resolve, so the cell
      // would demand something unanswerable rather than nothing. Skipped, as a malformed role is.
      if (ExOrientation.FacingFromSide(key) == null)
        continue;
      if (!map.TryGetValue(cell, out List<string>? faces))
        map[cell] = faces = [];
      if (!faces.Contains(key))
        faces.Add(key);
    }
    return map.Count == 0 ? None : new MultiblockConnectors(map);
  }
}
