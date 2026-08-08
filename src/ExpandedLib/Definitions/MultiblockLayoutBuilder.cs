using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// Authors a <c>multiblockStructure</c> from ASCII layer diagrams instead of a coordinate array: a
/// <see cref="Legend"/> maps characters to block codes, and one <see cref="Layer"/> per Y level draws the
/// build as a top-down grid (see <see cref="StructureLayout"/> for the grid rules). The block numbers and the
/// offsets table are generated from the drawing, then validated through <see cref="MultiblockBuilder"/> (every
/// cell resolves to a legend entry; no cell is drawn twice). Because the game treats the offsets as an
/// unordered set of required blocks and the <c>w</c> numbers as a private index, the generated table is
/// behaviour-identical to any hand-written ordering of the same cells.
/// </summary>
public sealed class MultiblockLayoutBuilder
{
  private int _xLeft;
  private int _zTop;
  private readonly List<(char Symbol, string Code)> _legend = new();
  private readonly List<(int Y, string Grid)> _layers = new();
  private readonly Dictionary<string, IReadOnlyList<int>> _facingSegment = new();
  private readonly Dictionary<char, List<CellRole>> _roleOf = new();
  private JObject? _roles;

  /// <summary>Sets where the top-left of every layer grid sits: <paramref name="xLeft"/> is the X of the first
  /// column, <paramref name="zTop"/> the Z of the first row. Defaults to <c>(0, 0)</c>.</summary>
  public MultiblockLayoutBuilder Origin(int xLeft, int zTop)
  {
    _xLeft = xLeft;
    _zTop = zTop;
    return this;
  }

  /// <summary>Maps a grid character to a block code (a wildcard/selector, e.g.
  /// <c>Legend('#', "exlib:structurefiller")</c>). <c>'.'</c> is reserved for empty cells.
  /// <para>
  /// A code containing a whole <b>horizontal side segment</b> (<c>north</c>/<c>south</c>/<c>east</c>/
  /// <c>west</c>, or their single letters) is treated as <b>oriented</b>: the layout is authored in the
  /// structure's north-default frame, and the required facing is rotated with the structure so a slab or
  /// door must be placed the right way round, not merely present. Vertical words (<c>up</c>/<c>down</c>)
  /// and codes with no side segment are unaffected. Use <see cref="LegendAnyFacing"/> to opt out.
  /// </para></summary>
  public MultiblockLayoutBuilder Legend(char symbol, string code) =>
    AddLegend(symbol, code, oriented: true);

  /// <summary>
  /// Like <see cref="Legend"/> but <b>never</b> rotates the code's side segment - the cell accepts the
  /// literal code at every structure angle. For the rare part whose facing is world-absolute rather than
  /// structure-relative (a chimney that must always vent north, say), or a wildcard that should stay lax.
  /// </summary>
  public MultiblockLayoutBuilder LegendAnyFacing(char symbol, string code) =>
    AddLegend(symbol, code, oriented: false);

  /// <summary>
  /// Marks what a glyph's cells are <b>for</b> - <see cref="CellRole.Tuyere"/>, <see cref="CellRole.Flue"/> -
  /// so a machine can ask the layout for them instead of keeping a hand-written offset list beside the
  /// drawing. Optional metadata: a layout that calls this never behaves differently, and a layout that does
  /// not call it emits nothing at all.
  /// <para>
  /// The role attaches to the <b>glyph</b>, so the author uses a distinct glyph per role. That is forced,
  /// not stylistic: one code serves several purposes in a single layout (<c>game:air</c> is the vent shaft,
  /// the flue and the tap alcove), so a role keyed by code could not tell them apart. Give the flue its own
  /// glyph pointing at the same code - <b>several glyphs may share one code</b> - exactly as the layouts
  /// already write <c>T</c> and <c>Y</c> for the two tuyere facings. Several glyphs may equally share one
  /// role.
  /// </para>
  /// <para>
  /// A glyph may carry <b>several</b> roles, because one cell can genuinely be two things at once: the
  /// shaft furnaces' crucible floor is where the burden rests <em>and</em> where the pool freezes, so it is
  /// <see cref="CellRole.Chargeable"/> and <see cref="CellRole.Pool"/> together. Each role is kept - there is
  /// no last-writer-wins here, which is the drift this feature exists to remove - and restating a role the
  /// glyph already has is idempotent. Splitting the two apart would need the cell to hold two glyphs, which
  /// the drawing cannot express.
  /// </para>
  /// <para>
  /// Order-independent: <see cref="Role"/> may be called before or after the glyph's <see cref="Legend"/>,
  /// and both are validated at build time.
  /// </para>
  /// </summary>
  public MultiblockLayoutBuilder Role(char symbol, CellRole role)
  {
    if (!_roleOf.TryGetValue(symbol, out List<CellRole>? roles))
      _roleOf[symbol] = roles = [];
    if (!roles.Contains(role))
      roles.Add(role);
    return this;
  }

  private MultiblockLayoutBuilder AddLegend(char symbol, string code, bool oriented)
  {
    if (symbol is '.' or ' ')
      throw new ArgumentException(
        "'.' and space are reserved for empty cells and cannot be legend symbols."
      );
    // Two codes on one glyph cannot both be honoured - the cell gets one block number, so one of the codes
    // silently stops being required. Since roles make glyphs meaningful rather than merely convenient, this
    // has to fail loudly.
    if (_legend.Any(e => e.Symbol == symbol))
      throw new ArgumentException(
        $"Multiblock layout declares symbol '{symbol}' twice; a glyph maps to one code."
      );
    _legend.Add((symbol, code));
    if (oriented)
    {
      IReadOnlyList<int> segments = FindOrientationSegments(code);
      if (segments.Count > 0)
        // Keyed by the full domained form. `AssetLocation.ToShortString()` elides the `game` domain, so
        // a key written as the author typed it would never match a vanilla block's code at runtime.
        _facingSegment[new AssetLocation(code).ToString()] = segments;
    }
    return this;
  }

  /// <summary>
  /// The indices of <b>every</b> dash-separated segment of <paramref name="code"/>'s path that names
  /// an orientation a Y rotation actually moves - a horizontal side word
  /// (<c>brickslabs-fire-<b>south</b>-free</c>) or a network node's multi-direction token
  /// (<c>pipe-straight-fire-<b>ns</b></c>). Empty when the code has none, in which case the cell is
  /// not orientation-checked and behaves exactly as it always has.
  /// <para>
  /// <b>All of them, not the last one.</b> A code may carry more than one orientation group -
  /// vanilla's stairs spell both the half and the facing (<c>brickstairs-fire-up-south-free</c>) -
  /// and pinning only one leaves the other free to be placed wrong. Scanning every segment also
  /// removes the old "the last one wins" tie-break, which existed only because a single answer had to
  /// be chosen.
  /// </para>
  /// <para>
  /// Only segments that genuinely <b>rotate</b> are recorded. <c>up</c>, <c>down</c> and <c>ud</c>
  /// are orientations no structure angle changes, so an entry for them would buy nothing - they are
  /// already required exactly, by the code matching literally.
  /// </para>
  /// <para>
  /// Only <b>whole</b> segments count, so <c>westward</c> is not mistaken for a facing, and the
  /// token grammar is exact rather than "a run of nsewud letters" - see
  /// <see cref="ExOrientation.IsOrientationToken"/> for why a loose test would rotate a material name
  /// into a code matching no block.
  /// </para>
  /// </summary>
  internal static IReadOnlyList<int> FindOrientationSegments(string code)
  {
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
  public MultiblockLayoutBuilder Layer(int y, string grid)
  {
    _layers.Add((y, grid));
    return this;
  }

  /// <summary>
  /// The oriented-legend table for <c>attributes.multiblockFacings</c>: block code → the dash-segment
  /// indices of every orientation in it that a Y rotation moves. Empty when the layout has no oriented
  /// parts, in which case nothing is emitted and the completion check behaves exactly as it always has.
  /// <para>
  /// This is keyed by <b>code</b> rather than by block number so it reads against <c>blockNumbers</c>
  /// in a golden and survives the numbering being reassigned. The authored facings themselves are not
  /// stored: they are already in the code, which is the only copy that can never drift.
  /// </para>
  /// <para>
  /// An <b>array</b> rather than a single index, because one code can carry several orientation
  /// groups (stairs spell both a half and a facing). A one-element array is by far the common case and
  /// is what every layout shipped so far emits.
  /// </para>
  /// </summary>
  internal JObject? BuildFacings()
  {
    if (_facingSegment.Count == 0)
      return null;
    var o = new JObject();
    foreach ((string code, IReadOnlyList<int> segments) in _facingSegment)
      o[code] = new JArray(segments);
    return o;
  }

  /// <summary>
  /// The cell-role table for <c>attributes.multiblockRoles</c>: role name → the <b>authored</b> offsets of
  /// its cells. Null when the layout marks nothing, in which case nothing is emitted and every consumer sees
  /// <see cref="MultiblockCellRoles.None"/> - the additive guarantee. Computed by <see cref="Build"/>.
  /// </summary>
  internal JObject? BuildRoles() => _roles;

  internal JObject Build()
  {
    var mb = new MultiblockBuilder();
    // The number is keyed by CODE, not by glyph. `blockNumbers` is a JSON object keyed by code, so two
    // glyphs on one code would emit two numbers but only one entry - the first number would be overwritten
    // and every cell holding it would silently stop being required (vanilla's own InCompleteBlockCount
    // indexes BlockCodes[w] and would throw outright). Sharing the number is what makes "several glyphs, one
    // code, different roles" - the whole premise of Role() - representable. Numbers are still handed out in
    // legend-declaration order, so a layout with one glyph per code emits exactly what it always did.
    var wOfCode = new Dictionary<string, int>(StringComparer.Ordinal);
    var wOf = new Dictionary<char, int>();
    int w = 1;
    foreach ((char symbol, string code) in _legend)
    {
      if (!wOfCode.TryGetValue(code, out int cellW))
        wOfCode[code] = cellW = w++;
      wOf[symbol] = cellW;
      mb.Number(code, cellW);
    }

    ValidateRoles(wOf);

    var roleCells = new Dictionary<CellRole, JArray>();
    var drawn = new HashSet<char>();
    foreach (LayoutCell cell in StructureLayout.Parse(_xLeft, _zTop, _layers))
    {
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
              new JObject
              {
                ["x"] = cell.X,
                ["y"] = cell.Y,
                ["z"] = cell.Z,
              }
            );
    }

    // A role glyph the drawing never uses resolves to nothing at runtime with no error anywhere - the silent
    // empty set roles exist to remove. Checked per glyph rather than per role, because a role two glyphs
    // share (the two tuyere facings) would still look drawn when one of them had been dropped.
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
  /// Enforces <see cref="SingleCellAttribute"/>: a role declared single-cell is drawn exactly once.
  /// <para>
  /// A consumer of <c>MetalTap</c> or <c>SlagTap</c> wants a point, not a set - the <c>MetalTapCell</c> and
  /// <c>SlagTapCell</c> these roles replace are a single <c>Vec3i</c> - so it will write <c>Single()</c>.
  /// Making that legal is a build-time job: the runtime accessor hands back a list and the caller cannot
  /// re-derive the author's intent from it, so the alternative is an <see cref="InvalidOperationException"/>
  /// out of LINQ on a live block entity instead of out of the load. Counted over the <b>drawn cells</b>
  /// rather than over <c>_roleOf</c>, so drawing one glyph twice fails as well as giving two glyphs the same
  /// single-cell role.
  /// </para>
  /// </summary>
  private static void ValidateRoleArity(Dictionary<CellRole, JArray> roleCells)
  {
    foreach ((CellRole role, JArray cells) in roleCells)
      if (CellRoles.IsSingleCell(role) && cells.Count != 1)
        throw new InvalidOperationException(
          $"Multiblock layout draws {cells.Count} cells with the role {role}, which is a single-cell role; "
            + "exactly one cell may carry it."
        );
  }

  /// <summary>
  /// The two role checks that need no drawing: every role glyph has a <see cref="Legend"/> entry, and the
  /// layout is a shaft or a firebox rather than both.
  /// <para>
  /// A role on a glyph the legend does not define would answer empty for ever with no error anywhere - the
  /// glyph is simply not in the drawing's alphabet. <see cref="CellRole.Chargeable"/> together with
  /// <see cref="CellRole.Firebox"/> is the other one: a burden column and a plain fuel bed are the two halves
  /// of the shaft/firebox furnace split, and a layout claiming both would hand the migration two
  /// contradictory answers. Refusing it here makes the mismatch unrepresentable instead of something a test
  /// downstream has to notice - the guard three separate tasks in this plan have each hand-built.
  /// </para>
  /// </summary>
  private void ValidateRoles(Dictionary<char, int> wOf)
  {
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
  )
  {
    if (!roleCells.TryGetValue(role, out JArray? array))
      roleCells[role] = array = new JArray();
    return array;
  }

  /// <summary>
  /// Serialises the collected role cells. Roles are emitted in <b>enum-declaration order</b> and each role's
  /// cells in drawing order, so the table is stable against an unrelated edit elsewhere in the layout and a
  /// golden diff means the roles genuinely moved. Keys are the enum's own names, read back
  /// case-insensitively: a renamed role is then a compile error at the author and a visible golden diff at
  /// the reader, with no case-mapping layer in between to get wrong.
  /// </summary>
  private static JObject? EmitRoles(Dictionary<CellRole, JArray> roleCells)
  {
    if (roleCells.Count == 0)
      return null;
    var o = new JObject();
    foreach (CellRole role in roleCells.Keys.OrderBy(r => (int)r))
      o[role.ToString()] = roleCells[role];
    return o;
  }
}
