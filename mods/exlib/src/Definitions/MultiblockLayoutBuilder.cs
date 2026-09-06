using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Structures;
using ExpandedLib.Helpers;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Definitions;

/// <summary>
/// Authors a <c>multiblockStructure</c> from ASCII grids: <see cref="Legend"/> maps characters to block
/// codes, drawn as one <see cref="Layer"/> per Y level (a top-down floor plan), one <see cref="Slice"/>
/// per X level or one <see cref="Face"/> per Z level (elevations; grid rules in
/// <see cref="ExpandedLib.Structures.CellGrid"/>) - a layout may mix them. Block numbers and offsets are
/// generated and validated through <see cref="MultiblockBuilder"/>: every cell resolves to a legend
/// entry, no cell is drawn twice. The game reads the offsets as an unordered set and the <c>w</c> numbers
/// as a private index, so the generated ordering carries no meaning. See docs/design/mechanics/multiblock.md.
/// </summary>
public sealed class MultiblockLayoutBuilder {
  private int _originA;
  private int _originB;
  private readonly List<(char Symbol, string Code)> _legend = new();
  private readonly List<(int Y, string Grid)> _layers = new();
  private readonly List<(int X, string Grid)> _slices = new();
  private readonly List<(int Z, string Grid)> _faces = new();
  private readonly Dictionary<string, IReadOnlyList<int>> _facingSegment =
    new();
  private readonly Dictionary<char, List<CellRole>> _roleOf = new();
  private readonly Dictionary<char, List<string>> _connectorOf = new();
  private char? _core;
  private JObject? _roles;
  private JObject? _connectors;

  /// <summary>Sets where the top-left of every grid sits: <paramref name="xLeft"/>/<paramref name="zTop"/>
  /// for a horizontal <see cref="Layer"/>, reinterpreted as <c>(zLeft, yTop)</c> for a fixed-X
  /// <see cref="Slice"/> and <c>(xLeft, yTop)</c> for a fixed-Z <see cref="Face"/>, since each grid kind
  /// draws a different pair of axes. Defaults to <c>(0, 0)</c>.</summary>
  public MultiblockLayoutBuilder Origin(int xLeft, int zTop) {
    _originA = xLeft;
    _originB = zTop;
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
  /// Marks what a glyph's cells are for - a tuyere, a flue - so a machine can ask the layout for them
  /// instead of carrying a hand-written offset list. Optional; a layout
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

  /// <summary>
  /// Demands that a glyph's cells hold a block exposing a network connector on each of
  /// <paramref name="outward"/> - the layout's way of saying "this tuyere opens into the hearth" without
  /// pinning the node's orientation variant, which the node's own neighbour scan is free to overwrite.
  /// Faces are authored in the structure's north-default frame and turn with it. Optional; a layout that
  /// never calls this emits nothing.
  /// </summary>
  /// <remarks>Satisfied by a superset: a passthrough wearing <c>ns</c> answers a demand for north, so a
  /// legitimate re-pick by the network does not break a standing structure. Two glyphs may share one
  /// code and demand different faces - they share a block number, which is what makes two connector
  /// directions on one code representable.</remarks>
  public MultiblockLayoutBuilder Connector(
    char symbol,
    params BlockFacing[] outward
  ) {
    if (!_connectorOf.TryGetValue(symbol, out List<string>? faces))
      _connectorOf[symbol] = faces = [];

    foreach (BlockFacing face in outward) {
      string letter = ExOrientation.TokenOf(face, asLetter: true);
      if (!faces.Contains(letter))
        faces.Add(letter);
    }
    return this;
  }

  /// <summary>
  /// Marks <paramref name="symbol"/> as the anchor - the block the player places, which must land on
  /// the layout's own <c>(0,0,0)</c>. Optional; a layout that never calls this has its <c>Origin</c>
  /// unchecked, same as before this method existed. <see cref="Build"/> then throws when the declared
  /// <see cref="Origin"/> does not put the marked glyph there, which otherwise builds the whole
  /// structure offset from the block the player placed with no error anywhere.
  /// </summary>
  public MultiblockLayoutBuilder Core(char symbol) {
    _core = symbol;
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
      RefuseNetworkToken(symbol, code);
      IReadOnlyList<int> segments = FindOrientationSegments(code);
      if (segments.Count > 0)
        // Keyed by the full domained form. `AssetLocation.ToShortString()` elides the `game` domain, so
        // a key written as the author typed it would never match a vanilla block's code at runtime.
        _facingSegment[new AssetLocation(code).ToString()] = segments;
    }
    return this;
  }

  /// <summary>
  /// Refuses a legend code carrying a multi-letter direction token (<c>ns</c>, <c>nw</c>, <c>uns</c>,
  /// <c>nswe</c>). Only a network node spells one - no player-oriented block does - and a network node
  /// picks its own orientation from its neighbours, so pinning the variant states a fact the node is free
  /// to contradict: the structure can be left uncompletable, or a complete one broken when the player
  /// plumbs something nearby. Mark the cell with <see cref="Connector"/> instead, which says what the
  /// layout actually wants.
  /// </summary>
  /// <remarks>Matched against the tokens the declared schemes spell rather than against a letter set, so
  /// an ordinary segment that happens to be made of direction letters is not caught. Single-letter cases
  /// are indistinguishable here - <c>furnace-tuyere-n</c> reads exactly like <c>hopper-tall-e</c> - and
  /// are left to the per-mod <c>PinnedNetworkNodes</c> check, which can see the defs.
  /// <see cref="LegendAnyFacing"/> is the documented opt-out.</remarks>
  private static void RefuseNetworkToken(char symbol, string code) {
    int colon = code.IndexOf(':');
    string path = colon >= 0 ? code[(colon + 1)..] : code;

    foreach (string part in path.Split('-')) {
      if (
        part.Length < 2
        || !ExOrientations.All.Any(s => s.Tokens.Contains(part))
      )
        continue;

      throw new InvalidOperationException(
        $"Multiblock layout pins symbol '{symbol}' to '{code}', whose '{part}' segment is a network "
          + "node's own orientation token. A network node takes its orientation from its neighbours, so "
          + "the pin can be contradicted at any time; mark the cell with Connector instead, or use "
          + "LegendAnyFacing if the code really is meant literally."
      );
    }
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
    var segmented = new ExOrientation.SegmentedCode(code);
    var found = new List<int>();
    for (int i = 0; i < segmented.Count; i++)
      if (ExOrientation.RotatesUnderY(segmented[i]))
        found.Add(i);
    return found;
  }

  /// <summary>Adds one horizontal Y-level grid (a floor plan; rows run +Z, columns +X). Layers may be
  /// declared in any Y order.</summary>
  public MultiblockLayoutBuilder Layer(int y, string grid) {
    _layers.Add((y, grid));
    return this;
  }

  /// <summary>Adds one vertical X-level grid (a front elevation; rows run down in -Y from the top, columns
  /// run +Z). Suits a structure that stacks in Y, such as a beam column. Slices may be declared in any X
  /// order.</summary>
  public MultiblockLayoutBuilder Slice(int x, string grid) {
    _slices.Add((x, grid));
    return this;
  }

  /// <summary>Adds one vertical Z-level grid (a front elevation looking along -Z; rows run down in -Y from
  /// the top, columns run +X). Suits a thin-in-Z, north-facing structure whose face lies in the X-Y
  /// plane. Faces may be declared in any Z order.</summary>
  public MultiblockLayoutBuilder Face(int z, string grid) {
    _faces.Add((z, grid));
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

  /// <summary>
  /// The connector table for <c>attributes.multiblockConnectors</c>: outward face letter -> the authored
  /// offsets of the cells demanding it. Null when the layout marks nothing, and consumers then see
  /// <see cref="MultiblockConnectors.None"/>. Computed by <see cref="Build"/>, which must run first.
  /// </summary>
  internal JObject? BuildConnectors() => _connectors;

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
    if (_core is char core && !wOf.ContainsKey(core))
      throw new InvalidOperationException(
        $"Multiblock layout marks '{core}' as the anchor but has no Legend entry for it."
      );

    var roleCells = new Dictionary<CellRole, JArray>();
    var connectorCells = new Dictionary<string, JArray>();
    var drawn = new HashSet<char>();
    (int X, int Y, int Z)? anchor = DrawnCells(out List<LayoutCell> cells);
    foreach (LayoutCell cell in cells) {
      if (!wOf.TryGetValue(cell.Symbol, out int cellW))
        throw new InvalidOperationException(
          $"Multiblock layout uses symbol '{cell.Symbol}' at ({cell.X},{cell.Y},{cell.Z}) with no Legend entry."
        );
      mb.At(cell.X, cell.Y, cell.Z, cellW);
      drawn.Add(cell.Symbol);

      if (_core == cell.Symbol)
        anchor ??= (cell.X, cell.Y, cell.Z);

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

      if (_connectorOf.TryGetValue(cell.Symbol, out List<string>? faces))
        foreach (string face in faces)
          FaceArray(connectorCells, face)
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

    // Same silent-empty-set failure a role glyph has, and the worse half of it: an undrawn connector
    // glyph reads as a structure with no facing demand at all, which completes with the node backwards.
    foreach ((char symbol, List<string> faces) in _connectorOf)
      if (!drawn.Contains(symbol))
        throw new InvalidOperationException(
          $"Multiblock layout demands symbol '{symbol}' open to {string.Join(", ", faces)} but never draws "
            + "it in any Layer, so the demand would resolve to nothing at runtime."
        );

    if (_core is char coreSymbol) {
      if (anchor is null)
        throw new InvalidOperationException(
          $"Multiblock layout marks '{coreSymbol}' as the anchor but never draws it in any Layer."
        );
      if (anchor.Value != (0, 0, 0))
        throw new InvalidOperationException(
          $"Multiblock layout declares Origin({_originA},{_originB}) but anchor '{coreSymbol}' lands at "
            + $"({anchor.Value.X},{anchor.Value.Y},{anchor.Value.Z}), not (0,0,0) - Origin should be "
            + $"({_originA - anchor.Value.X},{_originB - anchor.Value.Z})."
        );
    }

    ValidateRoleArity(roleCells);

    _roles = EmitRoles(roleCells);
    _connectors = EmitConnectors(connectorCells);
    return mb.Build();
  }

  /// <summary>
  /// Draws every declared <see cref="Layer"/>, <see cref="Slice"/> and <see cref="Face"/> grid (a layout
  /// may mix them) and returns their cells in that order through <paramref name="cells"/>. The anchor is
  /// wherever <see cref="Core"/>'s glyph landed in whichever grid drew it first; null when
  /// <see cref="Core"/> was never called or its glyph was never drawn.
  /// </summary>
  private (int X, int Y, int Z)? DrawnCells(out List<LayoutCell> cells) {
    var options = new GridOptions(Anchor: _core);
    cells = new List<LayoutCell>();
    (int X, int Y, int Z)? anchor = null;

    if (_layers.Count > 0) {
      var grid = new CellGrid(GridPlane.Horizontal, _originA, _originB, options);
      foreach ((int y, string g) in _layers)
        grid.Add(y, g);
      cells.AddRange(grid.Cells);
      anchor ??= grid.AnchorCell;
    }
    if (_slices.Count > 0) {
      var grid = new CellGrid(GridPlane.SliceX, _originA, _originB, options);
      foreach ((int x, string g) in _slices)
        grid.Add(x, g);
      cells.AddRange(grid.Cells);
      anchor ??= grid.AnchorCell;
    }
    if (_faces.Count > 0) {
      var grid = new CellGrid(GridPlane.FaceZ, _originA, _originB, options);
      foreach ((int z, string g) in _faces)
        grid.Add(z, g);
      cells.AddRange(grid.Cells);
      anchor ??= grid.AnchorCell;
    }
    return anchor;
  }

  /// <summary>
  /// Enforces <see cref="CellRole.IsSingle"/>: a role declared single-cell is drawn exactly once, so a
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
  /// The role check that needs no drawing: every role glyph has a <see cref="Legend"/> entry, since a
  /// role on an undefined glyph would answer empty for ever with no error anywhere. A layout's own
  /// mutually-exclusive-role rules, if it has any, are the declaring mod's to enforce - exlib knows no
  /// role's meaning, so it cannot know which pairs contradict each other.
  /// </summary>
  private void ValidateRoles(Dictionary<char, int> wOf) {
    foreach ((char symbol, List<CellRole> roles) in _roleOf)
      if (!wOf.ContainsKey(symbol))
        throw new InvalidOperationException(
          $"Multiblock layout gives symbol '{symbol}' the role {string.Join(", ", roles)} but has no Legend "
            + "entry for it."
        );

    foreach ((char symbol, List<string> faces) in _connectorOf)
      if (!wOf.ContainsKey(symbol))
        throw new InvalidOperationException(
          $"Multiblock layout demands symbol '{symbol}' open to {string.Join(", ", faces)} but has no Legend "
            + "entry for it."
        );
  }

  private static JArray FaceArray(
    Dictionary<string, JArray> connectorCells,
    string face
  ) {
    if (!connectorCells.TryGetValue(face, out JArray? array))
      connectorCells[face] = array = new JArray();
    return array;
  }

  /// <summary>
  /// Serialises the collected connector cells: faces in <c>n e s w u d</c> order, each face's cells in
  /// drawing order, so an unrelated edit elsewhere in the layout leaves the table unchanged.
  /// </summary>
  private static JObject? EmitConnectors(
    Dictionary<string, JArray> connectorCells
  ) {
    if (connectorCells.Count == 0)
      return null;
    var o = new JObject();
    foreach (string face in FaceOrder)
      if (connectorCells.TryGetValue(face, out JArray? cells))
        o[face] = cells;
    return o;
  }

  private static readonly string[] FaceOrder = ["n", "e", "s", "w", "u", "d"];

  private static JArray RoleArray(
    Dictionary<CellRole, JArray> roleCells,
    CellRole role
  ) {
    if (!roleCells.TryGetValue(role, out JArray? array))
      roleCells[role] = array = new JArray();
    return array;
  }

  /// <summary>
  /// Serialises the collected role cells: roles sorted by key (ordinal), each role's cells in drawing
  /// order, so an unrelated edit elsewhere in the layout leaves the table unchanged. `multiblockRoles` is
  /// read back as a map, so this order carries no meaning beyond being stable. Keys are the role's own
  /// key, read back exactly.
  /// </summary>
  private static JObject? EmitRoles(Dictionary<CellRole, JArray> roleCells) {
    if (roleCells.Count == 0)
      return null;
    var o = new JObject();
    foreach (
      CellRole role in roleCells.Keys.OrderBy(
        r => r.Key,
        StringComparer.Ordinal
      )
    )
      o[role.ToString()] = roleCells[role];
    return o;
  }
}
