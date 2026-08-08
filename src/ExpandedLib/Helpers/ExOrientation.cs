using System;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Helpers;

/// <summary>
/// Single source of truth for the mod family's horizontal rotation math. Every oriented block
/// places its fillers, connectors, particle boxes and sub-cells relative to a "north" (0°) layout
/// and rotates by the structure's angle, so a block only has to pick the right <em>angle</em>. All
/// methods share one convention: north 0°, west 90°, south 180°, east 270°, with
/// <c>(x,z) → 90:(z,-x) · 180:(-x,-z) · 270:(-z,x)</c>.
/// </summary>
public static class ExOrientation
{
  /// <summary>
  /// Maps a horizontal "side" variant to its rotation angle (north 0, west 90, south 180,
  /// east 270). Accepts both the full names used by <c>side</c> variants and the
  /// single-letter codes used by <c>orientation</c> variants ("n"/"w"/"s"/"e").
  /// </summary>
  public static int AngleFromSide(string? side) =>
    side switch
    {
      "east" or "e" => 270,
      "south" or "s" => 180,
      "west" or "w" => 90,
      _ => 0, // "north"/"n" or default
    };

  /// <summary>Rotates a structure-local offset by <paramref name="angle"/> (Y is untouched).</summary>
  public static Vec3i RotateOffset(Vec3i off, int angle) =>
    RotateOffset(off.X, off.Y, off.Z, angle);

  /// <summary>Rotates a structure-local offset by <paramref name="angle"/> (Y is untouched).</summary>
  public static Vec3i RotateOffset(int x, int y, int z, int angle)
  {
    // Normalise first so callers can pass any multiple/offset (e.g. Angle + 180 → 450)
    // without it slipping past the 90/180/270 cases into the unrotated default.
    angle = ((angle % 360) + 360) % 360;
    var (dx, dz) = angle switch
    {
      90 => (z, -x),
      180 => (-x, -z),
      270 => (-z, x),
      _ => (x, z), // 0° or any unhandled value
    };
    return new Vec3i(dx, y, dz);
  }

  /// <summary>
  /// Converts a structure-local offset into a world position for the given rotation:
  /// <c>origin + RotateOffset(local, angle)</c>. This is the body shared by every machine's
  /// <c>GetGlobalPos</c>.
  /// </summary>
  public static BlockPos GlobalPos(
    BlockPos origin,
    int localX,
    int localY,
    int localZ,
    int angle
  )
  {
    Vec3i r = RotateOffset(localX, localY, localZ, angle);
    return origin.AddCopy(r.X, r.Y, r.Z);
  }

  /// <summary>
  /// Reads a structure-local <c>{ x, y, z }</c> offset from an already-resolved JSON node (e.g. a
  /// block's generated offset accessor), falling back to <paramref name="fallback"/> when absent.
  /// </summary>
  public static Vec3i ReadOffset(JsonObject? node, Vec3i fallback)
  {
    if (node == null || !node.Exists)
      return fallback;
    return new Vec3i(
      node["x"].AsInt(fallback.X),
      node["y"].AsInt(fallback.Y),
      node["z"].AsInt(fallback.Z)
    );
  }

  /// <summary>
  /// The fractional (double) counterpart of <see cref="ReadOffset"/>, for continuous points
  /// (particle anchors). Falls back to <paramref name="fallback"/> when the node is absent.
  /// </summary>
  public static Vec3d ReadOffsetD(JsonObject? node, Vec3d fallback)
  {
    if (node == null || !node.Exists)
      return fallback;
    return new Vec3d(
      node["x"].AsDouble(fallback.X),
      node["y"].AsDouble(fallback.Y),
      node["z"].AsDouble(fallback.Z)
    );
  }

  /// <summary>
  /// Resolves an already-resolved structure-local offset node to a world cell:
  /// <c>origin + RotateOffset(ReadOffset(node), angle)</c>. The canonical way a machine turns a
  /// JSON offset (passed via its generated accessor) into a world position for its placed rotation.
  /// </summary>
  public static BlockPos WorldPosFromAttr(
    BlockPos origin,
    JsonObject? node,
    Vec3i fallback,
    int angle
  )
  {
    Vec3i off = ReadOffset(node, fallback);
    Vec3i r = RotateOffset(off, angle);
    return origin.AddCopy(r.X, r.Y, r.Z);
  }

  /// <summary>
  /// Returns copies of <paramref name="boxes"/> rotated around the block centre by
  /// <paramref name="angle"/>° (Y axis). JSON boxes are authored north-facing and don't auto-rotate
  /// with the "side" variant, so port blocks rotate them to match. Unchanged for angle 0.
  /// </summary>
  public static Cuboidf[] RotateBoxes(Cuboidf[] boxes, int angle)
  {
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
  /// Rotates a horizontal block face by <paramref name="angle"/> (same convention as
  /// <see cref="RotateOffset(Vec3i, int)"/>). Vertical faces (up/down) are returned
  /// unchanged. Used to map north-orientation connector faces onto the placed orientation.
  /// </summary>
  public static BlockFacing RotateFacing(BlockFacing baseFace, int angle)
  {
    if (baseFace.IsVertical)
      return baseFace;
    Vec3i n = baseFace.Normali;
    Vec3i r = RotateOffset(new Vec3i(n.X, 0, n.Z), angle);
    return BlockFacing.FromNormal(r) ?? baseFace;
  }

  /// <summary>
  /// The inverse of <see cref="AngleFromSide"/>: the horizontal side word for a rotation angle
  /// (0 north, 90 west, 180 south, 270 east). <paramref name="asLetter"/> returns the single-letter
  /// form <c>orientation</c> variants use instead of the full <c>side</c> word.
  /// </summary>
  public static string SideFromAngle(int angle, bool asLetter = false)
  {
    angle = ((angle % 360) + 360) % 360;
    return angle switch
    {
      90 => asLetter ? "w" : "west",
      180 => asLetter ? "s" : "south",
      270 => asLetter ? "e" : "east",
      _ => asLetter ? "n" : "north",
    };
  }

  /// <summary>
  /// Rotates a horizontal side word by <paramref name="angle"/>, preserving the input's form (a full
  /// word stays a word, a letter stays a letter). Vertical words (<c>up</c>/<c>down</c>/<c>u</c>/<c>d</c>)
  /// and anything unrecognised come back unchanged, because a Y rotation does not move them.
  /// <para>
  /// This is the string counterpart of <see cref="RotateFacing"/> and shares its convention: a part
  /// authored facing <c>north</c> reads as the side whose <see cref="AngleFromSide"/> equals the
  /// structure's angle. It is what lets a multiblock layout demand a <em>correctly oriented</em> slab
  /// or door rather than any rotation of one.
  /// </para>
  /// </summary>
  public static string RotateSideWord(string side, int angle)
  {
    if (!IsHorizontalSideWord(side))
      return side;
    bool asLetter = side.Length == 1;
    return SideFromAngle(AngleFromSide(side) + angle, asLetter);
  }

  /// <summary>True for the four horizontal side words (full or single-letter). Vertical and unknown
  /// words are false - they are invariant under the Y rotations a structure can take.</summary>
  public static bool IsHorizontalSideWord(string? side) =>
    side is "north" or "south" or "east" or "west" or "n" or "s" or "e" or "w";

  /// <summary>
  /// The <see cref="BlockFacing"/> a <c>side</c>/<c>orientation</c> token names, in <b>either</b>
  /// spelling - <c>north</c> or <c>n</c>. Null when the token names no facing.
  /// <para>
  /// <b>Use this, never <c>BlockFacing.FromCode(Variant["side"])</c>.</b> Vanilla's
  /// <c>FromCode</c> understands only the full words and returns <b>null</b> for a letter - and the
  /// failures that produces are silent, not loud. A tap whose facing came back null simply drains
  /// nothing; a casting cell falls back to north and feeds from the wrong wall. Both look like a
  /// mechanic that "does not work" rather than like a bad code, which is the worst kind of bug to own.
  /// </para>
  /// <para>
  /// This is not only about the letter-spelled <c>side</c> variants: <c>orientation</c> groups have
  /// always been letters, so any code reading a facing off one was already exposed.
  /// </para>
  /// </summary>
  public static BlockFacing? FacingFromSide(string? side)
  {
    if (string.IsNullOrEmpty(side))
      return null;
    // Vertical and any other non-horizontal token: hand it to vanilla unchanged (up/down/north/...).
    if (!IsHorizontalSideWord(side))
      return BlockFacing.FromCode(side);
    // Horizontal, either spelling: go through the angle, which accepts both, and hand vanilla the word.
    return BlockFacing.FromCode(SideFromAngle(AngleFromSide(side), asLetter: false));
  }

  /// <summary>
  /// The token a <see cref="BlockFacing"/> wears in a block code, in whichever form that block's variant
  /// group uses: the full word for a <c>side</c> group (<c>north</c>), the single letter for a network
  /// node's <c>orientation</c> group (<c>n</c>).
  /// <para>
  /// This is the whole reason the generated code table can take a <see cref="BlockFacing"/> instead of a
  /// bare string. The two spellings are the N4 split - <c>side</c> when the player decides the facing,
  /// <c>orientation</c> when the network does - and a layout author should not have to remember which
  /// spelling a given part uses, only which way it points.
  /// </para>
  /// </summary>
  public static string TokenOf(BlockFacing facing, bool asLetter) =>
    SideFromAngle(AngleFromSide(facing.Code), asLetter);

  #region Multi-direction orientation tokens (network nodes)

  // Network nodes do not spell their orientation as a facing. A pipe, a passthrough, an axle, a
  // junction and a valve all carry an `orientation` variant holding the set of faces they connect -
  // `ns`, `we`, `ud`, `nswe`, `nsud`, `weud`, and the valves' reversed `sn` / `ew` / `du`. None of
  // those is a side word, so a layout pinning one was invisible to the oriented-parts check and
  // silently would not rotate: wrong two times out of four.
  //
  // The grammar is deliberately exact rather than "a run of nsewud letters". Direction letters
  // spell ordinary words - `sun`, `wend`, `used` - and a loose test would orientation-check a
  // material segment, rotate it, and produce a code matching no block. A token is therefore either
  // one direction, or whole axis pairs concatenated with each axis used at most once.

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
  /// True for a whole code segment that names an orientation: a single side (<c>n</c>, <c>north</c>,
  /// <c>u</c>, …) or a concatenation of complete axis pairs (<c>ns</c>, <c>weud</c>, <c>nswe</c>).
  /// <para>
  /// Being an orientation is not the same as being <em>rotatable</em>: <c>ud</c> and <c>up</c> are
  /// orientations that a Y rotation leaves alone. Use <see cref="RotatesUnderY"/> to ask that.
  /// </para>
  /// </summary>
  public static bool IsOrientationToken(string? token)
  {
    if (string.IsNullOrEmpty(token))
      return false;
    if (IsHorizontalSideWord(token) || token is "up" or "down" or "u" or "d")
      return true;

    // Whole axis pairs, each axis at most once. Length must therefore be even and <= 6.
    if (token.Length % 2 != 0 || token.Length > 6)
      return false;
    var axesSeen = new List<char>(3);
    for (int i = 0; i < token.Length; i += 2)
    {
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
  /// Rotates an orientation token by <paramref name="angle"/> - the generalisation of
  /// <see cref="RotateSideWord"/> to the multi-direction tokens network nodes use. Each direction
  /// letter is rotated independently, then the result is put back into canonical axis order
  /// (<c>ns</c> before <c>we</c> before <c>ud</c>) so it matches the variant the block actually
  /// declares. Anything that is not an orientation token comes back unchanged.
  /// <para>
  /// <b>Canonicalising loses the valves' direction.</b> <c>BlockValve</c> and
  /// <c>BlockPressureValve</c> declare both <c>ns</c> and <c>sn</c>, where the order encodes
  /// input → output; every other node treats a pair as an undirected axis and declares only the
  /// canonical spelling. The two grammars are indistinguishable from the string alone - <c>ns</c> is
  /// a legal member of both - so this canonicalises, which is right for pipes, passthroughs, axles,
  /// junctions, flywheels and mills, and wrong for a valve. <b>A layout that must pin a directed
  /// valve has to use <c>LegendAnyFacing</c></b> and check the direction itself.
  /// </para>
  /// </summary>
  public static string RotateOrientationToken(string token, int angle)
  {
    if (!IsOrientationToken(token))
      return token;
    if (token.Length > 1 && !IsHorizontalSideWord(token) && token is not ("up" or "down"))
    {
      var axes = new List<char>(3);
      foreach (char c in token)
      {
        char rotated = RotateLetter(c, angle);
        char axis = AxisOf(rotated);
        if (!axes.Contains(axis))
          axes.Add(axis);
      }
      var sb = new System.Text.StringBuilder(token.Length);
      foreach (char axis in "nwu") // canonical axis order: NS, WE, UD
        if (axes.Contains(axis))
          sb.Append(axis == 'n' ? "ns" : axis == 'w' ? "we" : "ud");
      return sb.ToString();
    }
    return RotateSideWord(token, angle);
  }

  /// <summary>
  /// Whether <paramref name="token"/> actually moves under a Y rotation. This is what decides
  /// whether a layout emits an orientation check for a segment at all: <c>ud</c>, <c>up</c> and
  /// <c>down</c> are orientations that no structure angle changes, so pinning them costs a table
  /// entry and buys nothing - they are already checked by the code matching literally.
  /// </summary>
  public static bool RotatesUnderY(string token) =>
    IsOrientationToken(token) && RotateOrientationToken(token, 90) != token;

  /// <summary>The axis a direction letter belongs to, named by its first member: n, w or u.</summary>
  private static char AxisOf(char direction) =>
    direction switch
    {
      'n' or 's' => 'n',
      'w' or 'e' => 'w',
      _ => 'u',
    };

  private static char RotateLetter(char direction, int angle) =>
    direction is 'u' or 'd' ? direction : RotateSideWord(direction.ToString(), angle)[0];

  #endregion

  /// <summary>
  /// Rotates a block-relative float coordinate around a cell centre by <paramref name="angle"/> -
  /// the continuous-coordinate counterpart of <see cref="RotateOffset(Vec3i, int)"/> (particle/
  /// render boxes). The caller supplies the correct angle source.
  /// </summary>
  public static void RotateAroundCenter(
    ref float x,
    ref float z,
    int angle,
    float center = 0.5f
  )
  {
    angle = ((angle % 360) + 360) % 360;
    float dx = x - center;
    float dz = z - center;
    var (ndx, ndz) = angle switch
    {
      90 => (dz, -dx),
      180 => (-dx, -dz),
      270 => (-dz, dx),
      _ => (dx, dz),
    };
    x = center + ndx;
    z = center + ndz;
  }
}
