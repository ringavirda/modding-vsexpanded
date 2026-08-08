using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace ExpandedLib.Testing;

/// <summary>
/// Stands up a mega-block's multiblock footprint in a headless world: every layout cell gets a block
/// whose code satisfies it, so the anchor's own monitor tick observes <c>InCompleteBlockCount == 0</c>
/// and sets <see cref="BlockEntityMultiblockStructure.StructureComplete"/> itself. Nothing is forced, so
/// a wrong layout, rotation or anchor shows up as a structure that never completes.
/// The layout comes from the anchor's <see cref="ExBlockDef"/>, whose attributes are attached to the
/// placed block because a <see cref="TestBlocks.Configure"/> block carries none. Call order:
/// <see cref="Occupy"/> the cells the test cares about, then <see cref="Raise"/>,
/// <see cref="TestWorld.Initialize"/> and <see cref="AwaitCompletion"/> - or <see cref="Complete"/>.
/// </summary>
public sealed class StructureRig {
  private static readonly AssetLocation AirCode = new("game:air");

  /// <summary>Block ids for the rig's stand-in blocks, kept clear of the low ids fixtures hand out.</summary>
  private const int FirstStandInId = 30000;

  private readonly TestWorld _world;
  private readonly BlockEntityMultiblockStructure _anchor;
  private readonly Dictionary<string, Block> _standIns = new();
  private int _nextId = FirstStandInId;

  /// <summary>The rotation the structure was raised at, in degrees (0 = north).</summary>
  public int Angle { get; }

  /// <summary>
  /// The world the structure stands in, exposed for callers that did not build it themselves and need
  /// to register a block type or a block-entity factory, or advance the clock.
  /// </summary>
  public TestWorld World => _world;

  /// <summary>
  /// Every cell of the rotated layout: world position and the (possibly wildcard) code it wants -
  /// vanilla's own rotated offset table. Codes for parts marked oriented turn with the structure, so a
  /// cell authored <c>iwex:hopper-tall-north</c> wants a <c>-west</c> hopper at 90 deg; all others keep
  /// their authored, possibly domainless, form (<see cref="MultiblockFacings"/>).
  /// </summary>
  public IReadOnlyList<(BlockPos Pos, string Wanted)> Cells { get; }

  private StructureRig(
    TestWorld world,
    BlockEntityMultiblockStructure anchor,
    int angle,
    IReadOnlyList<(BlockPos, string)> cells
  ) {
    _world = world;
    _anchor = anchor;
    Angle = angle;
    Cells = cells;
  }

  /// <summary>
  /// Prepares a rig around an already-placed <paramref name="anchor"/>: attaches
  /// <paramref name="def"/>'s attributes to the anchor's block so the production code can load the
  /// layout, then resolves the layout rotated by <paramref name="angle"/> into world cells.
  /// </summary>
  /// <param name="angle">The angle the machine derives from its block variant - north 0, west 90,
  /// south 180, east 270, plus any per-machine offset (the Bessemer control and the cowper stove face
  /// <c>angle + 180</c>). A wrong angle puts the cells where the machine does not look.</param>
  public static StructureRig Around(
    TestWorld world,
    BlockEntityMultiblockStructure anchor,
    ExBlockDef def,
    int angle = 0
  ) {
    if (anchor.Block == null)
      throw new InvalidOperationException(
        "The anchor must be placed (Block assigned) before a StructureRig is built around it."
      );

    JObject json = def.ToJson();
    if (json["attributes"] is not JObject attributes)
      throw new InvalidOperationException(
        $"Block definition '{def.Code}' has no attributes, so it carries no multiblockStructure."
      );
    if (attributes["multiblockStructure"] is not JObject layout)
      throw new InvalidOperationException(
        $"Block definition '{def.Code}' is not a multiblock: no 'multiblockStructure' attribute."
      );

    // The shipped attributes are what the machine's own UpdateStructureRotation reads; without them
    // _structure stays null and the monitor tick returns early.
    anchor.Block.Attributes = new JsonObject(attributes);

    // Authored glyph per block number, read straight off the JSON so a domainless or wildcard code
    // keeps its authored form instead of being re-domained by an AssetLocation round trip. Demand()
    // applies the rotation rewrite.
    var codeByNumber = ((JObject)layout["blockNumbers"]!)
      .Properties()
      .ToDictionary(p => (int)p.Value!, p => p.Name);

    // Vanilla MultiblockStructure, deserialized and rotated the way the production block entity does
    // at placement.
    MultiblockStructure structure = new JsonObject(
      layout
    ).AsObject<MultiblockStructure>()!;
    structure.InitForUse(angle);

    // The oriented-part table the layout ships. Without it the rig fills and counts by the authored
    // code while the machine checks the rotated one.
    MultiblockFacings facings = MultiblockFacings.FromAttributes(
      new JsonObject(attributes)
    );

    var cells = new List<(BlockPos, string)>();
    foreach (BlockOffsetAndNumber offset in structure.TransformedOffsets!)
      cells.Add(
        (
          anchor.Pos.AddCopy(offset.X, offset.Y, offset.Z),
          Demand(facings, codeByNumber[offset.W], angle)
        )
      );

    return new StructureRig(world, anchor, angle, cells);
  }

  /// <summary>
  /// What a cell authored as <paramref name="authored"/> requires once the structure is turned to
  /// <paramref name="angle"/> - the rig's half of <c>BlockEntityMultiblockStructure.WantedCodeAt</c>. An
  /// identity rotation returns the authored string unchanged, because an <see cref="AssetLocation"/>
  /// round trip re-domains a domainless glyph such as <c>@(air|coalpile|furnace-chargepile)</c>.
  /// </summary>
  private static string Demand(
    MultiblockFacings facings,
    string authored,
    int angle
  ) {
    if (facings.IsEmpty)
      return authored;

    var code = new AssetLocation(authored);
    AssetLocation rotated = facings.Rotate(code, angle);
    return rotated.Equals(code) ? authored : rotated.ToString();
  }

  /// <summary>
  /// The world position of a structure-local offset at this rig's rotation - the same mapping the
  /// machine's own <c>GetGlobalPos</c> performs, so a cell can be addressed by authored coordinates.
  /// </summary>
  public BlockPos Cell(int localX, int localY, int localZ) =>
    ExOrientation.GlobalPos(_anchor.Pos, localX, localY, localZ, Angle);

  /// <summary>
  /// Places a real, functional block (and its entity) at <paramref name="pos"/> before the rig fills
  /// the footprint. <see cref="Raise"/> never replaces it: if its code does not satisfy what the layout
  /// wants there, the structure does not complete.
  /// </summary>
  public StructureRig Occupy(BlockPos pos, Block block, BlockEntity? be = null) {
    _world.Place(pos, block, be);
    if (be != null)
      _world.Attach(be);
    return this;
  }

  /// <summary>
  /// Fills every empty footprint cell with a stand-in block matching what that cell wants. Cells
  /// satisfied by air (an open shaft, an <c>@(air|coalpile)</c> fuel slot) stay empty; filling them
  /// would pass the code check while plugging a cell the machine expects open. An occupied cell is
  /// never replaced, so a conflict surfaces through <see cref="Missing"/> instead. Idempotent.
  /// </summary>
  public StructureRig Raise() {
    foreach (var (pos, wanted) in Cells) {
      Block have = _world.GetBlock(pos);
      if (have.Id != 0)
        continue; // occupied - the anchor itself, or a block the test placed deliberately

      AssetLocation concrete = Concretize(new AssetLocation(wanted));
      // Compared on path alone: a shaft legend is domain-wildcarded (`*:@(air|coalpile|...)`), so its
      // first branch concretises to `*:air`, which is not equal to `game:air`.
      if (concrete.Path == AirCode.Path)
        continue; // an air-satisfied slot: leaving the cell empty is the fill

      _world.Place(pos, StandIn(concrete));
    }
    return this;
  }

  /// <summary>
  /// How many footprint cells are still unsatisfied, counted against the same rotated demand the
  /// machine's own completion check uses (see <see cref="Cells"/>), so this and <c>StructureComplete</c>
  /// cannot disagree. Non-zero after <see cref="Raise"/> means layout and world disagree.
  /// </summary>
  public int Missing {
    get {
      int missing = 0;
      foreach (var (pos, wanted) in Cells)
        if (
          !WildcardUtil.Match(
            new AssetLocation(wanted),
            _world.GetBlock(pos).Code
          )
        )
          missing++;
      return missing;
    }
  }

  /// <summary>
  /// Runs the machine's own completion monitor until it observes the finished footprint, and returns
  /// whether it did: the tick is the production one, so false means the machine cannot see the structure
  /// the rig built. The block entity must already be <see cref="TestWorld.Initialize">initialized</see>;
  /// the monitor runs on a 3 s interval, so one interval is advanced per attempt.
  /// </summary>
  public bool AwaitCompletion(int maxMonitorTicks = 2) {
    for (int i = 0; i < maxMonitorTicks && !_anchor.StructureComplete; i++)
      _world.AdvanceBlockEntityTime(3000);
    return _anchor.StructureComplete;
  }

  /// <summary>
  /// <see cref="Raise"/> + <see cref="TestWorld.Initialize"/> + <see cref="AwaitCompletion"/>, throwing
  /// with the missing-cell breakdown if the machine does not complete.
  /// </summary>
  public StructureRig Complete() {
    Raise();
    _world.Initialize(_anchor);
    if (!AwaitCompletion())
      throw new InvalidOperationException(
        $"{_anchor.GetType().Name} did not complete at angle {Angle}: {Missing} of "
          + $"{Cells.Count} cells unsatisfied.{UnsatisfiedReport()}"
      );
    return this;
  }

  /// <summary>
  /// A per-cell breakdown of what each unsatisfied cell wants and what it holds - the message
  /// <see cref="Complete"/> throws with, exposed for fixtures that build a scene over a raised structure.
  /// </summary>
  public string MissingReport => UnsatisfiedReport();

  /// <summary>A per-cell breakdown of what each unsatisfied cell wants and what it actually holds.</summary>
  private string UnsatisfiedReport() {
    var lines = new List<string>();
    foreach (var (pos, wanted) in Cells) {
      Block have = _world.GetBlock(pos);
      if (!WildcardUtil.Match(new AssetLocation(wanted), have.Code))
        lines.Add($"\n  {pos}: wants '{wanted}', has '{have.Code}'");
    }
    return string.Concat(lines);
  }

  /// <summary>One stand-in block per distinct code, so a 100-cell footprint registers a handful of blocks.</summary>
  private Block StandIn(AssetLocation code) {
    if (_standIns.TryGetValue(code.ToString(), out Block? cached))
      return cached;

    var block = TestBlocks.Configure(new Block(), code.ToString(), _nextId++);
    _standIns[code.ToString()] = block;
    return block;
  }

  /// <summary>
  /// Turns a wanted code into a concrete one that satisfies it: an alternation <c>@(air|coalpile)</c>
  /// collapses to its first branch, and <c>*</c> becomes a literal segment. The result only has to
  /// match the wildcard, since a footprint cell is checked by code alone.
  /// </summary>
  private static AssetLocation Concretize(AssetLocation wanted) =>
    new(wanted.Domain, FirstAlternative(wanted.Path).Replace("*", "x"));

  /// <summary>
  /// Collapses every alternation group down to its first branch, counting bracket depth. Vanilla treats
  /// the inside of <c>@( )</c> as a regex, so a branch may carry its own parenthesised alternation
  /// (<c>brickcourse-.*-(black|tan)</c>); scanning to the first <c>)</c> would stop inside it and leave
  /// a stray bracket that matches nothing. Branches are resolved recursively.
  /// </summary>
  private static string FirstAlternative(string path) {
    var sb = new StringBuilder();
    int i = 0;
    while (i < path.Length) {
      bool tagged = path[i] == '@' && i + 1 < path.Length && path[i + 1] == '(';
      if (!tagged && path[i] != '(') {
        sb.Append(path[i++]);
        continue;
      }

      int open = tagged ? i + 1 : i;
      int close = MatchingParen(path, open);
      if (close < 0) {
        sb.Append(path[i++]); // unbalanced - leave it alone rather than mangle it further
        continue;
      }

      sb.Append(
        FirstAlternative(
          FirstBranch(path.Substring(open + 1, close - open - 1))
        )
      );
      i = close + 1;
    }
    return sb.ToString();
  }

  /// <summary>Index of the <c>)</c> closing the <c>(</c> at <paramref name="open"/>, or -1 if unbalanced.</summary>
  private static int MatchingParen(string s, int open) {
    int depth = 0;
    for (int i = open; i < s.Length; i++) {
      if (s[i] == '(')
        depth++;
      else if (s[i] == ')' && --depth == 0)
        return i;
    }
    return -1;
  }

  /// <summary>The part of an alternation body before its first top-level <c>|</c>.</summary>
  private static string FirstBranch(string inner) {
    int depth = 0;
    for (int i = 0; i < inner.Length; i++) {
      if (inner[i] == '(')
        depth++;
      else if (inner[i] == ')')
        depth--;
      else if (inner[i] == '|' && depth == 0)
        return inner[..i];
    }
    return inner;
  }
}
