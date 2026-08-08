using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExpandedLib.Helpers;

/// <summary>
/// One <b>declared</b> set of orientation tokens, plus the rotation rule that set implies.
/// <para>
/// The whole point is that rotation needs nothing from a block except <b>the list of tokens it
/// declares</b> - which the block already writes in its <c>VariantGroup</c>. A scheme is that list,
/// named, so blocks reference it instead of retyping twelve bend tokens each and so the rule has
/// something to check against.
/// </para>
/// <para>
/// This replaces <see cref="ExOrientation.RotateOrientationToken"/>'s parser, which tried to derive
/// the grammar from the string alone. That cannot work: the spellings are not canonical (the bend
/// writes <c>en</c> not <c>ne</c>, <c>ws</c> not <c>sw</c>; the tee writes <c>uns</c> in one family and
/// <c>dnu</c> in another), and <c>ns</c> is a legal member of both the undirected and the directed
/// grammar, so no parser can tell a pipe from a valve. A declared list can.
/// </para>
/// </summary>
public sealed class ExOrientationScheme
{
  private readonly HashSet<string> _tokens;

  // One token per face set, for the fallback half of the rule. Directed schemes legitimately have
  // two tokens per set (`ns` and `sn`); the first wins here and is never consulted for them, because
  // the ordered step matches first. See Rotate.
  private readonly Dictionary<string, string> _bySet;

  internal ExOrientationScheme(string name, params string[] tokens)
  {
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
  public bool Contains(string? token) => token != null && _tokens.Contains(token);

  /// <summary>
  /// <b>The rule, and it is the same rule for every scheme:</b> rotate each direction letter
  /// <b>preserving order</b>; if the result is a declared token, use it; otherwise use the unique
  /// declared token with the same face <b>set</b>.
  /// <para>
  /// The ordered step is what preserves direction where a scheme declares both spellings (a valve's
  /// <c>we</c> → <c>sn</c>, not <c>ns</c>). The set fallback is what repairs the non-canonical
  /// spellings everywhere else (an axis's <c>we</c> → <c>sn</c> → <c>ns</c>; a tee's <c>uwe</c> →
  /// <c>usn</c> → <c>uns</c>). <b>Neither step alone works</b> - ordered-only breaks the axis and the
  /// tee, set-only cannot tell <c>ns</c> from <c>sn</c>.
  /// </para>
  /// <para>
  /// A token this scheme does not declare comes back unchanged: rotating something that is not an
  /// orientation must be a no-op, or a material segment that happens to spell <c>sun</c> or
  /// <c>used</c> gets "rotated" into a code matching no block.
  /// </para>
  /// </summary>
  public string Rotate(string? token, int angle)
  {
    if (token == null || !_tokens.Contains(token))
      return token ?? "";

    string ordered = RotateLetters(token, angle);
    if (_tokens.Contains(ordered))
      return ordered;
    return _bySet.TryGetValue(SetKey(ordered), out string? bySet) ? bySet : token;
  }

  /// <summary>
  /// Whether <paramref name="token"/> actually moves under a Y rotation. This is what decides whether a
  /// layout emits an orientation check at all: <c>ud</c> and <c>u</c> are orientations no structure
  /// angle changes, so pinning them costs a table entry and buys nothing - the code already matches
  /// them literally.
  /// </summary>
  public bool RotatesUnderY(string? token) =>
    Contains(token) && Rotate(token, 90) != token;

  /// <summary>Rotates every direction letter, <b>preserving the input's order</b>. Vertical letters are
  /// untouched - a Y rotation does not move them.</summary>
  private static string RotateLetters(string token, int angle)
  {
    var sb = new StringBuilder(token.Length);
    foreach (char c in token)
      sb.Append(
        c is 'u' or 'd' ? c : ExOrientation.SideFromAngle(
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
/// listing states inline.
/// <para>
/// Beyond de-duplicating twelve bend tokens per block, naming the scheme is what makes the
/// <see cref="Axis"/>/<see cref="DirectedAxis"/> distinction expressible at all. From the string alone
/// <c>ns</c> belongs to both, so a parser must guess; the block naming its scheme cannot.
/// </para>
/// <para>
/// <b>Who declares a scheme is not who may be pinned in a layout.</b> A network node picks its own
/// orientation from its neighbours (<c>BlockNetworkNode.RecalculateAndSyncOrientations</c>), so pinning
/// one in a multiblock layout is actively harmful - the structure could never be completed, or a
/// complete one could silently come apart when the player plumbs something unrelated nearby. Schemes
/// are shared vocabulary; ownership of the value is a separate question. See
/// <c>docs/design/mechanics/orientation-schemes.md</c>.
/// </para>
/// </summary>
public static class ExOrientations
{
  /// <summary>The four horizontal faces - the scheme a player-oriented block's <c>side</c> variant
  /// uses, and the one <c>BlockBehaviorExOrientable</c> defaults to.</summary>
  public static readonly ExOrientationScheme Face = new("Face", "n", "e", "s", "w");

  /// <summary>All six faces, for a block that may also point up or down (the pipe outlet, and any
  /// omni-orientable block).</summary>
  public static readonly ExOrientationScheme FaceAll =
    new("FaceAll", "n", "e", "s", "w", "u", "d");

  /// <summary>Undirected axes including vertical - pipe straight, pipe passthrough, cast-iron shaft,
  /// cast-iron bevel.</summary>
  public static readonly ExOrientationScheme Axis = new("Axis", "ns", "we", "ud");

  /// <summary>Undirected horizontal axes - flywheel, rolling mill, mill axle, canal straight.</summary>
  public static readonly ExOrientationScheme AxisFlat = new("AxisFlat", "ns", "we");

  /// <summary>Horizontal axes where order encodes input → output - the molten canal's ends.</summary>
  public static readonly ExOrientationScheme DirectedAxisFlat =
    new("DirectedAxisFlat", "ns", "we", "ew", "sn");

  /// <summary>Axes where order encodes input → output, including vertical - valve, pressure valve.</summary>
  public static readonly ExOrientationScheme DirectedAxis =
    new("DirectedAxis", "ns", "we", "ud", "sn", "ew", "du");

  /// <summary>Two <b>adjacent</b> faces - the pipe bend's twelve. Spelled as authored (<c>en</c>,
  /// not <c>ne</c>); the set fallback is what makes the spelling not matter.</summary>
  public static readonly ExOrientationScheme PipeBend =
    new(
      "PipeBend",
      "nw", "se", "en", "ws",
      "un", "us", "uw", "ue",
      "dn", "ds", "dw", "de"
    );

  /// <summary>The molten canal's four horizontal bends.</summary>
  public static readonly ExOrientationScheme CanalBend =
    new("CanalBend", "nw", "se", "en", "ws");

  /// <summary>Three faces - the pipe T-junction's twelve.</summary>
  public static readonly ExOrientationScheme PipeTee =
    new(
      "PipeTee",
      "uns", "uwe", "dns", "dwe",
      "nes", "esw", "swn", "wne",
      "dnu", "deu", "dsu", "dwu"
    );

  /// <summary>The molten canal's four horizontal tees.</summary>
  public static readonly ExOrientationScheme CanalTee =
    new("CanalTee", "nes", "esw", "swn", "wne");

  /// <summary>Four faces - the pipe X-junction's three planes.</summary>
  public static readonly ExOrientationScheme PipeCross =
    new("PipeCross", "nswe", "nsud", "weud");

  /// <summary>The molten canal's single horizontal cross.</summary>
  public static readonly ExOrientationScheme CanalCross = new("CanalCross", "nswe");

  /// <summary>Every scheme, for the drift tests and for <see cref="Resolve"/>.</summary>
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
  /// The scheme whose declared tokens are exactly <paramref name="states"/>, or null.
  /// <para>
  /// <b>Exact set equality, deliberately.</b> A block whose states are a subset of a scheme's is not
  /// using that scheme - it is using an undeclared one, and the set fallback would then map a rotation
  /// onto a token the block does not have. Better to fail to resolve and be told than to resolve
  /// wrongly.
  /// </para>
  /// </summary>
  public static ExOrientationScheme? Resolve(IEnumerable<string>? states)
  {
    if (states == null)
      return null;
    var set = new HashSet<string>(states);
    return All.FirstOrDefault(s =>
      s.Tokens.Count == set.Count && s.Tokens.All(set.Contains)
    );
  }
}
