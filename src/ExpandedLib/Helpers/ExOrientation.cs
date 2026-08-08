using System;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Helpers;

/// <summary>
/// Horizontal rotation math shared by the mod family's oriented blocks. Fillers, connectors,
/// particle boxes and sub-cells are authored against a north (0°) layout and rotated by the
/// structure's angle. Every method uses the same convention: north 0°, west 90°, south 180°,
/// east 270°, with <c>(x,z) → 90:(z,-x) · 180:(-x,-z) · 270:(-z,x)</c>.
/// </summary>
public static class ExOrientation {
  /// <summary>
  /// The rotation angle a horizontal side variant names (north 0, west 90, south 180, east 270).
  /// Accepts the full <c>side</c> words and the single-letter <c>orientation</c> codes ("n"/"w"/"s"/"e").
  /// </summary>
  public static int AngleFromSide(string? side) =>
    side switch {
      "east" or "e" => 270,
      "south" or "s" => 180,
      "west" or "w" => 90,
      _ => 0, // "north"/"n" or default
    };

  /// <summary>Rotates a structure-local offset by <paramref name="angle"/> (Y is untouched).</summary>
  public static Vec3i RotateOffset(Vec3i off, int angle) =>
    RotateOffset(off.X, off.Y, off.Z, angle);

  /// <summary>Rotates a structure-local offset by <paramref name="angle"/> (Y is untouched).</summary>
  public static Vec3i RotateOffset(int x, int y, int z, int angle) {
    // Normalise first so callers can pass any multiple or offset (e.g. Angle + 180 -> 450)
    // without it slipping past the 90/180/270 cases into the unrotated default.
    angle = ((angle % 360) + 360) % 360;
    var (dx, dz) = angle switch {
      90 => (z, -x),
      180 => (-x, -z),
      270 => (-z, x),
      _ => (x, z), // 0° or any unhandled value
    };
    return new Vec3i(dx, y, dz);
  }

  /// <summary>
  /// Converts a structure-local offset into a world position: <c>origin + RotateOffset(local, angle)</c>.
  /// </summary>
  public static BlockPos GlobalPos(
    BlockPos origin,
    int localX,
    int localY,
    int localZ,
    int angle
  ) {
    Vec3i r = RotateOffset(localX, localY, localZ, angle);
    return origin.AddCopy(r.X, r.Y, r.Z);
  }

  /// <summary>
  /// Reads a structure-local <c>{ x, y, z }</c> offset from an already-resolved JSON node, falling back
  /// to <paramref name="fallback"/> when the node is absent.
  /// </summary>
  public static Vec3i ReadOffset(JsonObject? node, Vec3i fallback) {
    if (node == null || !node.Exists)
      return fallback;
    return new Vec3i(
      node["x"].AsInt(fallback.X),
      node["y"].AsInt(fallback.Y),
      node["z"].AsInt(fallback.Z)
    );
  }

  /// <summary>
  /// The double counterpart of <see cref="ReadOffset"/>, for continuous points such as particle anchors.
  /// </summary>
  public static Vec3d ReadOffsetD(JsonObject? node, Vec3d fallback) {
    if (node == null || !node.Exists)
      return fallback;
    return new Vec3d(
      node["x"].AsDouble(fallback.X),
      node["y"].AsDouble(fallback.Y),
      node["z"].AsDouble(fallback.Z)
    );
  }

  /// <summary>
  /// Resolves a structure-local offset node to a world cell for the placed rotation:
  /// <c>origin + RotateOffset(ReadOffset(node), angle)</c>.
  /// </summary>
  public static BlockPos WorldPosFromAttr(
    BlockPos origin,
    JsonObject? node,
    Vec3i fallback,
    int angle
  ) {
    Vec3i off = ReadOffset(node, fallback);
    Vec3i r = RotateOffset(off, angle);
    return origin.AddCopy(r.X, r.Y, r.Z);
  }

  /// <summary>
  /// Copies of <paramref name="boxes"/> rotated around the block centre by <paramref name="angle"/>°
  /// (Y axis); the input array itself for angle 0. JSON boxes are authored north-facing and do not
  /// auto-rotate with the <c>side</c> variant, so port blocks rotate them to match.
  /// </summary>
  public static Cuboidf[] RotateBoxes(Cuboidf[] boxes, int angle) {
    angle = ((angle % 360) + 360) % 360;
    if (angle == 0 || boxes.Length == 0)
      return boxes;
    var origin = new Vec3d(0.5, 0.5, 0.5);
    var rotated = new Cuboidf[boxes.Length];
    for (int i = 0; i < boxes.Length; i++)
      rotated[i] = boxes[i].RotatedCopy(0, angle, 0, origin);
    return rotated;
  }

  /// <summary>
  /// Rotates a horizontal block face by <paramref name="angle"/>, same convention as
  /// <see cref="RotateOffset(Vec3i, int)"/>. Vertical faces come back unchanged.
  /// </summary>
  public static BlockFacing RotateFacing(BlockFacing baseFace, int angle) {
    if (baseFace.IsVertical)
      return baseFace;
    Vec3i n = baseFace.Normali;
    Vec3i r = RotateOffset(new Vec3i(n.X, 0, n.Z), angle);
    return BlockFacing.FromNormal(r) ?? baseFace;
  }

  /// <summary>
  /// Inverse of <see cref="AngleFromSide"/>: the side word for a rotation angle (0 north, 90 west,
  /// 180 south, 270 east). <paramref name="asLetter"/> selects the single-letter <c>orientation</c>
  /// form over the full <c>side</c> word.
  /// </summary>
  public static string SideFromAngle(int angle, bool asLetter = false) {
    angle = ((angle % 360) + 360) % 360;
    return angle switch {
      90 => asLetter ? "w" : "west",
      180 => asLetter ? "s" : "south",
      270 => asLetter ? "e" : "east",
      _ => asLetter ? "n" : "north",
    };
  }

  /// <summary>
  /// String counterpart of <see cref="RotateFacing"/>: rotates a horizontal side word by
  /// <paramref name="angle"/>, preserving the input's form (a word stays a word, a letter stays a
  /// letter). Vertical and unrecognised words come back unchanged, being invariant under a Y rotation.
  /// </summary>
  public static string RotateSideWord(string side, int angle) {
    if (!IsHorizontalSideWord(side))
      return side;
    bool asLetter = side.Length == 1;
    return SideFromAngle(AngleFromSide(side) + angle, asLetter);
  }

  /// <summary>True for the four horizontal side words, full or single-letter. Vertical and unknown
  /// words are false: a Y rotation does not move them.</summary>
  public static bool IsHorizontalSideWord(string? side) =>
    side is "north" or "south" or "east" or "west" or "n" or "s" or "e" or "w";

  /// <summary>
  /// The <see cref="BlockFacing"/> a <c>side</c> or <c>orientation</c> token names, in either spelling
  /// (<c>north</c> or <c>n</c>); null when the token names no facing. Use this rather than
  /// <c>BlockFacing.FromCode</c>, which understands only the full words and returns null for the single
  /// letters <c>orientation</c> groups use.
  /// </summary>
  public static BlockFacing? FacingFromSide(string? side) {
    if (string.IsNullOrEmpty(side))
      return null;
    // Vertical and any other non-horizontal token: hand it to vanilla unchanged (up/down/...).
    if (!IsHorizontalSideWord(side))
      return BlockFacing.FromCode(side);
    // Horizontal, either spelling: go through the angle, which accepts both, and hand vanilla the word.
    return BlockFacing.FromCode(
      SideFromAngle(AngleFromSide(side), asLetter: false)
    );
  }

  /// <summary>
  /// The token a <see cref="BlockFacing"/> wears in a block code: the full word for a <c>side</c> group
  /// (<c>north</c>), the single letter for a network node's <c>orientation</c> group (<c>n</c>).
  /// </summary>
  public static string TokenOf(BlockFacing facing, bool asLetter) =>
    SideFromAngle(AngleFromSide(facing.Code), asLetter);

  #region Multi-direction orientation tokens (network nodes)

  // Network nodes spell their orientation as the set of faces they connect, not as a side word: pipes,
  // passthroughs, axles, junctions and valves carry an `orientation` variant such as `ns`, `we`, `ud`,
  // `nswe`, `nsud`, `weud`, plus the valves' reversed `sn` / `ew` / `du`.
  //
  // The grammar is exact - one direction, or whole axis pairs with each axis used at most once - rather
  // than "a run of nsewud letters", because direction letters also spell ordinary words (`sun`, `wend`,
  // `used`) and a loose test would rotate a material segment into a code matching no block.

  private static readonly string[] AxisPairs =
  [
    "ns",
    "sn",
    "we",
    "ew",
    "ud",
    "du",
  ];

  /// <summary>
  /// True for a whole code segment naming an orientation: a single side (<c>n</c>, <c>north</c>,
  /// <c>u</c>) or a concatenation of complete axis pairs (<c>ns</c>, <c>weud</c>, <c>nswe</c>). Naming
  /// an orientation is not the same as rotating under Y; <see cref="RotatesUnderY"/> answers that.
  /// </summary>
  public static bool IsOrientationToken(string? token) {
    if (string.IsNullOrEmpty(token))
      return false;
    if (IsHorizontalSideWord(token) || token is "up" or "down" or "u" or "d")
      return true;

    // Whole axis pairs, each axis at most once. Length must therefore be even and <= 6.
    if (token.Length % 2 != 0 || token.Length > 6)
      return false;
    var axesSeen = new List<char>(3);
    for (int i = 0; i < token.Length; i += 2) {
      string pair = token.Substring(i, 2);
      if (Array.IndexOf(AxisPairs, pair) < 0)
        return false;
      char axis = AxisOf(pair[0]);
      if (axesSeen.Contains(axis))
        return false;
      axesSeen.Add(axis);
    }
    return true;
  }

  /// <summary>
  /// <see cref="RotateSideWord"/> for multi-direction tokens: letters rotate independently and the
  /// result is emitted in canonical axis order (<c>ns</c>, <c>we</c>, <c>ud</c>); a non-orientation
  /// token comes back unchanged. Canonicalising discards direction, so a layout pinning a directed
  /// valve must use <c>LegendAnyFacing</c>. See docs/design/mechanics/orientation-schemes.md.
  /// </summary>
  public static string RotateOrientationToken(string token, int angle) {
    if (!IsOrientationToken(token))
      return token;
    if (
      token.Length > 1
      && !IsHorizontalSideWord(token)
      && token is not ("up" or "down")
    ) {
      var axes = new List<char>(3);
      foreach (char c in token) {
        char rotated = RotateLetter(c, angle);
        char axis = AxisOf(rotated);
        if (!axes.Contains(axis))
          axes.Add(axis);
      }
      var sb = new System.Text.StringBuilder(token.Length);
      foreach (char axis in "nwu") // canonical axis order: NS, WE, UD
        if (axes.Contains(axis))
          sb.Append(
            axis == 'n' ? "ns"
            : axis == 'w' ? "we"
            : "ud"
          );
      return sb.ToString();
    }
    return RotateSideWord(token, angle);
  }

  /// <summary>
  /// Whether <paramref name="token"/> moves under a Y rotation, so a layout knows whether to emit an
  /// orientation check for the segment. <c>ud</c>, <c>up</c> and <c>down</c> are false at every
  /// structure angle and are covered by the code matching literally.
  /// </summary>
  public static bool RotatesUnderY(string token) =>
    IsOrientationToken(token) && RotateOrientationToken(token, 90) != token;

  /// <summary>The axis a direction letter belongs to, named by its first member: n, w or u.</summary>
  private static char AxisOf(char direction) =>
    direction switch {
      'n' or 's' => 'n',
      'w' or 'e' => 'w',
      _ => 'u',
    };

  private static char RotateLetter(char direction, int angle) =>
    direction is 'u' or 'd'
      ? direction
      : RotateSideWord(direction.ToString(), angle)[0];

  #endregion

  /// <summary>
  /// Rotates a block-relative float coordinate around a cell centre by <paramref name="angle"/>: the
  /// continuous counterpart of <see cref="RotateOffset(Vec3i, int)"/>, for particle and render boxes.
  /// </summary>
  public static void RotateAroundCenter(
    ref float x,
    ref float z,
    int angle,
    float center = 0.5f
  ) {
    angle = ((angle % 360) + 360) % 360;
    float dx = x - center;
    float dz = z - center;
    var (ndx, ndz) = angle switch {
      90 => (dz, -dx),
      180 => (-dx, -dz),
      270 => (-dz, dx),
      _ => (dx, dz),
    };
    x = center + ndx;
    z = center + ndz;
  }
}
