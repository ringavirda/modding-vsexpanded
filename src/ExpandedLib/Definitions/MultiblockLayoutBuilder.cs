using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// Authors a <c>multiblockStructure</c> from ASCII layer diagrams: <see cref="Legend"/> maps characters to
/// block codes, one <see cref="Layer"/> per Y level draws the build as a top-down grid (grid rules in
/// <see cref="StructureLayout"/>). Block numbers and offsets are generated and validated through
/// <see cref="MultiblockBuilder"/>: every cell resolves to a legend entry, no cell is drawn twice. The game
/// reads the offsets as an unordered set and the <c>w</c> numbers as a private index, so the generated
/// ordering carries no meaning. See docs/design/mechanics/multiblock.md.
/// </summary>
public sealed class MultiblockLayoutBuilder {
  private int _xLeft;
  private int _zTop;
  private readonly List<(char Symbol, string Code)> _legend = new();
  private readonly List<(int Y, string Grid)> _layers = new();
  private readonly Dictionary<string, IReadOnlyList<int>> _facingSegment =
    new();
  private readonly Dictionary<char, List<CellRole>> _roleOf = new();
  private JObject? _roles;

  /// <summary>Sets where the top-left of every layer grid sits: <paramref name="xLeft"/> is the X of the first
  /// column, <paramref name="zTop"/> the Z of the first row. Defaults to <c>(0, 0)</c>.</summary>
  public MultiblockLayoutBuilder Origin(int xLeft, int zTop) {
    _xLeft = xLeft;
    _zTop = zTop;
    return this;
  }

  /// <summary>Maps a grid character to a block code, wildcard or selector (e.g.
  /// <c>Legend('#', "exlib:structurefiller")</c>). <c>'.'</c> is reserved for empty cells. A code carrying a
  /// whole horizontal side segment (<c>north</c>…<c>west</c>, or their single letters) is oriented: the
  /// layout is authored in the structure's north-default frame and the required facing rotates with it.
  /// <c>up</c>/<c>down</c> and codes with no side segment are unaffected; <see cref="LegendAnyFacing"/> opts
  /// out.</summary>
  public MultiblockLayoutBuilder Legend(char symbol, string code) =>
    AddLegend(symbol, code, oriented: true);

  /// <summary>
  /// Like <see cref="Legend"/> but never rotates the code's side segment: the cell accepts the literal code
  /// at every structure angle, for a part whose facing is world-absolute or a wildcard that should stay lax.
  /// </summary>
  public MultiblockLayoutBuilder LegendAnyFacing(char symbol, string code) =>
    AddLegend(symbol, code, oriented: false);

  /// <summary>
  /// Marks what a glyph's cells are for (<see cref="CellRole.Tuyere"/>, <see cref="CellRole.Flue"/>) so a
  /// machine can ask the layout for them instead of carrying a hand-written offset list. Optional; a layout
  /// that never calls this emits nothing. Roles attach to the glyph rather than the code, so one code can
  /// serve several purposes in a layout; a glyph may carry several roles and each is kept, and restating one
  /// is a no-op. Callable before or after the glyph's <see cref="Legend"/>; both are validated at build time.
  /// </summary>
  public MultiblockLayoutBuilder Role(char symbol, CellRole role) {
    if (!_roleOf.TryGetValue(symbol, out List<CellRole>? roles))
      _roleOf[symbol] = roles = [];
    if (!roles.Contains(role))
      roles.Add(role);
    return this;
  }

  private MultiblockLayoutBuilder AddLegend(
    char symbol,
    string code,
    bool oriented
  ) {
    if (symbol is '.' or ' ')
      throw new ArgumentException(
        "'.' and space are reserved for empty cells and cannot be legend symbols."
      );
    // Two codes on one glyph cannot both be honoured: the cell gets one block number, so one of the codes
    // would stop being required with no error anywhere.
    if (_legend.Any(e => e.Symbol == symbol))
      throw new ArgumentException(
        $"Multiblock layout declares symbol '{symbol}' twice; a glyph maps to one code."
      );
    _legend.Add((symbol, code));
    if (oriented) {
      IReadOnlyList<int> segments = FindOrientationSegments(code);
      if (segments.Count > 0)
        // Keyed by the full domained form. `AssetLocation.ToShortString()` elides the `game` domain, so
        // a key written as the author typed it would never match a vanilla block's code at runtime.
        _facingSegment[new AssetLocation(code).ToString()] = segments;
    }
    return this;
  }

  /// <summary>
  /// The indices of every dash-separated segment of <paramref name="code"/>'s path naming an orientation a Y
  /// rotation moves - a side word (<c>brickslabs-fire-south-free</c>) or a node's direction token
  /// (<c>pipe-straight-fire-ns</c>). All are reported: one code can carry two groups
  /// (<c>brickstairs-fire-up-south-free</c>). <c>up</c>, <c>down</c> and <c>ud</c> are excluded, since no
  /// structure angle moves them; empty means the cell is not orientation-checked. Matching is whole-segment,
  /// via <see cref="ExOrientation.IsOrientationToken"/>.
  /// </summary>
  internal static IReadOnlyList<int> FindOrientationSegments(string code) {
    int colon = code.IndexOf(':');
    string path = colon >= 0 ? code[(colon + 1)..] : code;
    string[] parts = path.Split('-');
    var found = new List<int>();
    for (int i = 0; i < parts.Length; i++)
      if (ExOrientation.RotatesUnderY(parts[i]))
        found.Add(i);
    return found;
  }

  /// <summary>Adds one Y-level grid (see <see cref="StructureLayout"/> for the drawing rules). Layers may be
  /// declared in any Y order.</summary>
  public MultiblockLayoutBuilder Layer(int y, string grid) {
    _layers.Add((y, grid));
    return this;
  }

  /// <summary>
  /// The oriented-legend table for <c>attributes.multiblockFacings</c>: block code -> the dash-segment
  /// indices of every orientation in it that a Y rotation moves. Null when the layout has no oriented parts,
  /// and nothing is emitted. Keyed by code rather than block number, so it reads against <c>blockNumbers</c>
  /// in a golden and survives renumbering; the value is an array because one code can carry several
  /// orientation groups.
  /// </summary>
  internal JObject? BuildFacings() {
    if (_facingSegment.Count == 0)
      return null;
    var o = new JObject();
    foreach ((string code, IReadOnlyList<int> segments) in _facingSegment)
      o[code] = new JArray(segments);
    return o;
  }

  /// <summary>
  /// The cell-role table for <c>attributes.multiblockRoles</c>: role name -> the authored offsets of its
  /// cells. Null when the layout marks nothing, and consumers then see
  /// <see cref="MultiblockCellRoles.None"/>. Computed by <see cref="Build"/>, which must run first.
  /// </summary>
  internal JObject? BuildRoles() => _roles;

  internal JObject Build() {
    var mb = new MultiblockBuilder();
    // Numbers are keyed by code, not by glyph, and handed out in legend-declaration order. `blockNumbers` is
    // a JSON object keyed by code, so two glyphs on one code would emit two numbers under one entry and
    // vanilla's InCompleteBlockCount would index a BlockCodes[w] that is gone. Sharing the number is what
    // lets several glyphs carry one code with different roles.
    var wOfCode = new Dictionary<string, int>(StringComparer.Ordinal);
    var wOf = new Dictionary<char, int>();
    int w = 1;
    foreach ((char symbol, string code) in _legend) {
      if (!wOfCode.TryGetValue(code, out int cellW))
        wOfCode[code] = cellW = w++;
      wOf[symbol] = cellW;
      mb.Number(code, cellW);
    }

    ValidateRoles(wOf);

    var roleCells = new Dictionary<CellRole, JArray>();
    var drawn = new HashSet<char>();
    foreach (LayoutCell cell in StructureLayout.Parse(_xLeft, _zTop, _layers)) {
      if (!wOf.TryGetValue(cell.Symbol, out int cellW))
        throw new InvalidOperationException(
          $"Multiblock layout uses symbol '{cell.Symbol}' at ({cell.X},{cell.Y},{cell.Z}) with no Legend entry."
        );
      mb.At(cell.X, cell.Y, cell.Z, cellW);
      drawn.Add(cell.Symbol);

      if (_roleOf.TryGetValue(cell.Symbol, out List<CellRole>? roles))
        foreach (CellRole role in roles)
          RoleArray(roleCells, role)
            .Add(
              new JObject {
                ["x"] = cell.X,
                ["y"] = cell.Y,
                ["z"] = cell.Z,
              }
            );
    }

    // A role glyph the drawing never uses resolves to an empty set at runtime with no error anywhere.
    // Checked per glyph rather than per role: a role two glyphs share would still look drawn when one of
    // them had been dropped.
    foreach ((char symbol, List<CellRole> roles) in _roleOf)
      if (!drawn.Contains(symbol))
        throw new InvalidOperationException(
          $"Multiblock layout gives symbol '{symbol}' the role {string.Join(", ", roles)} but never draws it "
            + "in any Layer, so the role would resolve to nothing at runtime."
        );

    ValidateRoleArity(roleCells);

    _roles = EmitRoles(roleCells);
    return mb.Build();
  }

  /// <summary>
  /// Enforces <see cref="SingleCellAttribute"/>: a role declared single-cell is drawn exactly once, so a
  /// consumer can call <c>Single()</c> on the runtime accessor without risking an
  /// <see cref="InvalidOperationException"/>. Counted over the drawn cells, so drawing one glyph twice fails
  /// as well as giving two glyphs the same single-cell role.
  /// </summary>
  private static void ValidateRoleArity(Dictionary<CellRole, JArray> roleCells) {
    foreach ((CellRole role, JArray cells) in roleCells)
      if (CellRoles.IsSingleCell(role) && cells.Count != 1)
        throw new InvalidOperationException(
          $"Multiblock layout draws {cells.Count} cells with the role {role}, which is a single-cell role; "
            + "exactly one cell may carry it."
        );
  }

  /// <summary>
  /// The two role checks that need no drawing: every role glyph has a <see cref="Legend"/> entry, since a
  /// role on an undefined glyph would answer empty for ever with no error anywhere; and the layout is a
  /// shaft or a firebox rather than both, since <see cref="CellRole.Chargeable"/> with
  /// <see cref="CellRole.Firebox"/> claims a burden column and a plain fuel bed at once.
  /// </summary>
  private void ValidateRoles(Dictionary<char, int> wOf) {
    foreach ((char symbol, List<CellRole> roles) in _roleOf)
      if (!wOf.ContainsKey(symbol))
        throw new InvalidOperationException(
          $"Multiblock layout gives symbol '{symbol}' the role {string.Join(", ", roles)} but has no Legend "
            + "entry for it."
        );

    if (
      _roleOf.Values.Any(r => r.Contains(CellRole.Chargeable))
      && _roleOf.Values.Any(r => r.Contains(CellRole.Firebox))
    )
      throw new InvalidOperationException(
        "Multiblock layout marks cells both Chargeable and Firebox; a furnace holds a burden column or a "
          + "fuel bed, never both."
      );
  }

  private static JArray RoleArray(
    Dictionary<CellRole, JArray> roleCells,
    CellRole role
  ) {
    if (!roleCells.TryGetValue(role, out JArray? array))
      roleCells[role] = array = new JArray();
    return array;
  }

  /// <summary>
  /// Serialises the collected role cells: roles in enum-declaration order, each role's cells in drawing
  /// order, so an unrelated edit elsewhere in the layout leaves the table unchanged. Keys are the enum's own
  /// names, read back case-insensitively.
  /// </summary>
  private static JObject? EmitRoles(Dictionary<CellRole, JArray> roleCells) {
    if (roleCells.Count == 0)
      return null;
    var o = new JObject();
    foreach (CellRole role in roleCells.Keys.OrderBy(r => (int)r))
      o[role.ToString()] = roleCells[role];
    return o;
  }
}
