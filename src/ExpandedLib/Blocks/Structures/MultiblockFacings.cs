using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Helpers;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// Rotates the facing of a multiblock layout's oriented parts, so a structure can require a slab, stairs or
/// door to be placed the right way round rather than merely be present. Vanilla's <c>MultiblockStructure</c>
/// rotates a structure's offsets through <c>InitForUse(angle)</c> but not its codes, so a cell wanting
/// <c>brickslabs-fire-south-free</c> would demand a south-facing slab at every structure angle. The layout
/// builder records which dash-segment of each oriented code is the facing word
/// (<c>attributes.multiblockFacings</c>), and this class swaps that segment for the structure-rotated one at
/// check time. Wildcards, vertical <c>up</c>/<c>down</c> parts and codes with no facing pass through untouched.
/// </summary>
public sealed class MultiblockFacings {
  /// <summary>A layout with no oriented parts. <see cref="Rotate"/> is the identity.</summary>
  public static readonly MultiblockFacings None = new(
    new Dictionary<string, int[]>()
  );

  private readonly Dictionary<string, int[]> _segmentOf;

  private MultiblockFacings(Dictionary<string, int[]> segmentOf) =>
    _segmentOf = segmentOf;

  /// <summary>True when no part of the layout is orientation-checked.</summary>
  public bool IsEmpty => _segmentOf.Count == 0;

  /// <summary>
  /// Reads the <c>multiblockFacings</c> attribute a code-first layout emits. Returns <see cref="None"/>
  /// for a block that declares none.
  /// </summary>
  public static MultiblockFacings FromAttributes(JsonObject? attributes) {
    JsonObject? node = attributes?["multiblockFacings"];
    if (node?.Exists != true)
      return None;

    // JsonObject has no key enumeration, so read the underlying token directly.
    if (node.Token is not JObject obj)
      return None;

    var map = new Dictionary<string, int[]>();
    foreach (var kv in obj) {
      // An array of segment indices; a bare integer is accepted too, since a hand-written attribute may
      // carry one and dropping it would silently disable the code's orientation check.
      if (kv.Value is JArray array) {
        int[] segments = array
          .Where(t => t.Type == JTokenType.Integer)
          .Select(t => (int)t)
          .ToArray();
        if (segments.Length > 0)
          map[kv.Key] = segments;
      } else if (kv.Value?.Type == JTokenType.Integer)
        map[kv.Key] = [(int)kv.Value];
    }
    return map.Count == 0 ? None : new MultiblockFacings(map);
  }

  /// <summary>
  /// The code a cell requires once the structure is turned to <paramref name="angle"/>: the authored code
  /// with its facing segment rotated. A code this layout did not mark as oriented is returned unchanged, so
  /// callers can pass everything through without branching.
  /// </summary>
  public AssetLocation Rotate(AssetLocation code, int angle) {
    if (_segmentOf.Count == 0)
      return code;

    // ToString() (not ToShortString()) - the table is keyed by the full domained form, because the
    // short form drops `game:` and would never match a vanilla block's code.
    if (!_segmentOf.TryGetValue(code.ToString(), out int[]? segments))
      return code;

    string? rotated = RotateSegments(code.Path, segments, angle);
    return rotated == null ? code : new AssetLocation(code.Domain, rotated);
  }

  /// <summary>
  /// Swaps every listed dash-segment of <paramref name="path"/> for its <paramref name="angle"/>-rotated
  /// orientation - a side word, or a network node's multi-direction token. Returns null when any index is out
  /// of range or names a segment that is not an orientation; all-or-nothing, because a partially rotated code
  /// matches no block and the cell could never be satisfied, so the caller keeps the authored code instead.
  /// </summary>
  internal static string? RotateSegments(string path, int[] segments, int angle) {
    string[] parts = path.Split('-');
    foreach (int segment in segments)
      if (
        segment < 0
        || segment >= parts.Length
        || !ExOrientation.IsOrientationToken(parts[segment])
      )
        return null;

    foreach (int segment in segments)
      parts[segment] = ExOrientation.RotateOrientationToken(
        parts[segment],
        angle
      );
    return string.Join('-', parts);
  }
}
