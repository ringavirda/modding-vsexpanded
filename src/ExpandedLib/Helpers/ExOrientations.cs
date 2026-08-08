using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExpandedLib.Helpers;

/// <summary>
/// One declared set of orientation tokens, plus the rotation rule that set implies. Rotation needs
/// nothing from a block but the list of tokens it declares in its <c>VariantGroup</c>; a scheme is that
/// list, named, so blocks reference it and the rule has something to check against.
/// <para>
/// The scheme cannot be derived from a token: the spellings are not canonical (the bend writes
/// <c>en</c> not <c>ne</c>, <c>ws</c> not <c>sw</c>; the tee writes <c>uns</c> in one family and
/// <c>dnu</c> in another), and <c>ns</c> is legal in both the undirected and the directed grammar.
/// </para>
/// </summary>
public sealed class ExOrientationScheme {
  private readonly HashSet<string> _tokens;

  // One token per face set, for the fallback half of the rule. Directed schemes have two tokens per
  // set (`ns` and `sn`); the first wins here and is never consulted for them, because the ordered step
  // matches first. See Rotate.
  private readonly Dictionary<string, string> _bySet;

  internal ExOrientationScheme(string name, params string[] tokens) {
    Name = name;
    Tokens = tokens;
    _tokens = [.. tokens];
    _bySet = [];
    foreach (string t in tokens)
      _bySet.TryAdd(SetKey(t), t);
  }

  /// <summary>The scheme's name, for diagnostics and for the build-time ambiguity error.</summary>
  public string Name { get; }

  /// <summary>Every token this scheme declares, in declaration order - the variant states a block
  /// using this scheme writes.</summary>
  public IReadOnlyList<string> Tokens { get; }

  /// <summary>Whether <paramref name="token"/> is one this scheme declares.</summary>
  public bool Contains(string? token) =>
    token != null && _tokens.Contains(token);

  /// <summary>
  /// Rotates <paramref name="token"/> by <paramref name="angle"/> degrees about Y. Each direction
  /// letter is rotated preserving order; if that spelling is declared it is used, otherwise the unique
  /// declared token with the same face set is. The ordered step preserves direction where a scheme
  /// declares both spellings (a valve's <c>we</c> → <c>sn</c>, not <c>ns</c>); the set fallback resolves
  /// the non-canonical spellings (an axis's <c>we</c> → <c>sn</c> → <c>ns</c>). Neither step alone
  /// suffices. A token this scheme does not declare comes back unchanged, so a variant segment that
  /// happens to spell <c>sun</c> or <c>used</c> is not rotated into a code matching no block.
  /// </summary>
  public string Rotate(string? token, int angle) {
    if (token == null || !_tokens.Contains(token))
      return token ?? "";

    string ordered = RotateLetters(token, angle);
    if (_tokens.Contains(ordered))
      return ordered;
    return _bySet.TryGetValue(SetKey(ordered), out string? bySet)
      ? bySet
      : token;
  }

  /// <summary>
  /// Whether <paramref name="token"/> moves under a Y rotation. Decides whether a layout emits an
  /// orientation check at all: no structure angle changes <c>ud</c> or <c>u</c>, so the code already
  /// matches them literally.
  /// </summary>
  public bool RotatesUnderY(string? token) =>
    Contains(token) && Rotate(token, 90) != token;

  /// <summary>Rotates every direction letter, preserving the input's order. Vertical letters are
  /// untouched, since a Y rotation does not move them.</summary>
  private static string RotateLetters(string token, int angle) {
    var sb = new StringBuilder(token.Length);
    foreach (char c in token)
      sb.Append(
        c is 'u' or 'd'
          ? c
          : ExOrientation.SideFromAngle(
            ExOrientation.AngleFromSide(c.ToString()) + angle,
            asLetter: true
          )[0]
      );
    return sb.ToString();
  }

  /// <summary>The face set of a token, order-independent - the key the fallback half matches on.</summary>
  private static string SetKey(string token) =>
    string.Concat(token.Distinct().OrderBy(c => c));
}

/// <summary>
/// The named orientation schemes every mod declares against, so a block references a set instead of
/// listing states inline. Naming the scheme is what makes the <see cref="Axis"/> /
/// <see cref="DirectedAxis"/> distinction expressible, since <c>ns</c> belongs to both.
/// <para>
/// Declaring a scheme does not make a block pinnable in a multiblock layout. A network node picks its
/// own orientation from its neighbours (<c>BlockNetworkNode.RecalculateAndSyncOrientations</c>), so a
/// pinned one can leave the structure uncompletable, or break a complete one when the player plumbs
/// something nearby. See <c>docs/design/mechanics/orientation-schemes.md</c>.
/// </para>
/// </summary>
public static class ExOrientations {
  /// <summary>The four horizontal faces - the scheme a player-oriented block's <c>side</c> variant
  /// uses, and the one <c>BlockBehaviorExOrientable</c> defaults to.</summary>
  public static readonly ExOrientationScheme Face = new(
    "Face",
    "n",
    "e",
    "s",
    "w"
  );

  /// <summary>All six faces, for a block that may also point up or down (the pipe outlet, and any
  /// omni-orientable block).</summary>
  public static readonly ExOrientationScheme FaceAll = new(
    "FaceAll",
    "n",
    "e",
    "s",
    "w",
    "u",
    "d"
  );

  /// <summary>Undirected axes including vertical - pipe straight, pipe passthrough, cast-iron shaft,
  /// cast-iron bevel.</summary>
  public static readonly ExOrientationScheme Axis = new(
    "Axis",
    "ns",
    "we",
    "ud"
  );

  /// <summary>Undirected horizontal axes - flywheel, rolling mill, mill axle, canal straight.</summary>
  public static readonly ExOrientationScheme AxisFlat = new(
    "AxisFlat",
    "ns",
    "we"
  );

  /// <summary>Horizontal axes where order encodes input → output - the molten canal's ends.</summary>
  public static readonly ExOrientationScheme DirectedAxisFlat = new(
    "DirectedAxisFlat",
    "ns",
    "we",
    "ew",
    "sn"
  );

  /// <summary>Axes where order encodes input → output, including vertical - valve, pressure valve.</summary>
  public static readonly ExOrientationScheme DirectedAxis = new(
    "DirectedAxis",
    "ns",
    "we",
    "ud",
    "sn",
    "ew",
    "du"
  );

  /// <summary>Two adjacent faces - the pipe bend's twelve. Spelled as authored (<c>en</c>, not
  /// <c>ne</c>); the set fallback is what makes the spelling not matter.</summary>
  public static readonly ExOrientationScheme PipeBend = new(
    "PipeBend",
    "nw",
    "se",
    "en",
    "ws",
    "un",
    "us",
    "uw",
    "ue",
    "dn",
    "ds",
    "dw",
    "de"
  );

  /// <summary>The molten canal's four horizontal bends.</summary>
  public static readonly ExOrientationScheme CanalBend = new(
    "CanalBend",
    "nw",
    "se",
    "en",
    "ws"
  );

  /// <summary>Three faces - the pipe T-junction's twelve.</summary>
  public static readonly ExOrientationScheme PipeTee = new(
    "PipeTee",
    "uns",
    "uwe",
    "dns",
    "dwe",
    "nes",
    "esw",
    "swn",
    "wne",
    "dnu",
    "deu",
    "dsu",
    "dwu"
  );

  /// <summary>The molten canal's four horizontal tees.</summary>
  public static readonly ExOrientationScheme CanalTee = new(
    "CanalTee",
    "nes",
    "esw",
    "swn",
    "wne"
  );

  /// <summary>Four faces - the pipe X-junction's three planes.</summary>
  public static readonly ExOrientationScheme PipeCross = new(
    "PipeCross",
    "nswe",
    "nsud",
    "weud"
  );

  /// <summary>The molten canal's single horizontal cross.</summary>
  public static readonly ExOrientationScheme CanalCross = new(
    "CanalCross",
    "nswe"
  );

  /// <summary>Every declared scheme - the set <see cref="Resolve"/> searches.</summary>
  public static IReadOnlyList<ExOrientationScheme> All =>
    [
      Face,
      FaceAll,
      Axis,
      AxisFlat,
      DirectedAxisFlat,
      DirectedAxis,
      PipeBend,
      CanalBend,
      PipeTee,
      CanalTee,
      PipeCross,
      CanalCross,
    ];

  /// <summary>
  /// The scheme whose declared tokens are exactly <paramref name="states"/>, or null. Matching is exact
  /// set equality: a block whose states are a subset of a scheme's is using an undeclared scheme, and
  /// the set fallback would map a rotation onto a token that block does not have.
  /// </summary>
  public static ExOrientationScheme? Resolve(IEnumerable<string>? states) {
    if (states == null)
      return null;
    var set = new HashSet<string>(states);
    return All.FirstOrDefault(s =>
      s.Tokens.Count == set.Count && s.Tokens.All(set.Contains)
    );
  }
}
