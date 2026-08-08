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
/// Stands up a mega-block's <b>real</b> multiblock footprint in a headless world, so a machine becomes
/// complete the way the game makes it complete: every cell of the authored layout is occupied by a
/// block whose code satisfies that cell, and the block entity's own monitor tick then observes
/// <c>InCompleteBlockCount == 0</c> and flips <see cref="BlockEntityMultiblockStructure.StructureComplete"/>
/// itself.
/// <para>
/// This exists because the alternative - <c>ReflectionHelpers.SetProperty(be, "StructureComplete", true)</c> -
/// asserts the conclusion instead of the premise. A rig that forces the flag passes even when the
/// layout is wrong, the rotation is a half-turn out, or the anchor can no longer see its own cells; the
/// machine "works" in the test and does nothing in game. Building the footprint makes completion an
/// <em>outcome</em>, so those failures show up as a structure that never completes.
/// </para>
/// The layout comes from the anchor's own code-first <see cref="ExBlockDef"/> - the shipped source of
/// truth - not from a copy in the test, so re-authoring a footprint moves its scenarios with it. The
/// def's <c>attributes</c> are attached to the placed block, which is also what lets the production
/// <c>UpdateStructureRotation</c> load the layout at all (a hand-configured
/// <see cref="TestBlocks.Configure"/> block carries no attributes).
/// <para>
/// Typical use - place the cells the test actually cares about first, then let the rig fill the rest:
/// <code>
///   var rig = StructureRig.Around(world, furnace, BlockBlastFurnaceCoreHot.Definitions("smex").Single(), angle: 0);
///   rig.Occupy(rig.Cell(0, 1, -1), tuyereBlock, tuyereBe);   // a real, functional cell
///   rig.Raise();                                             // stand-ins for every remaining cell
///   world.Initialize(furnace);                               // registers the monitor tick
///   rig.AwaitCompletion();                                   // the machine completes itself
/// </code>
/// </para>
/// </summary>
public sealed class StructureRig
{
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
  /// The world the structure stands in. Exposed because <c>FurnaceLayoutRig.Stand</c> builds the world
  /// itself, so a caller that needs to register a block type, a block-entity factory or advance the clock
  /// has no other handle on it - and reflecting into the private field for that is the kind of thing a test
  /// harness should not make its callers do.
  /// </summary>
  public TestWorld World => _world;

  /// <summary>
  /// Every cell of the rotated layout: its world position and the (possibly wildcard) block code that
  /// cell wants. This is vanilla's own rotated offset table, so it is the same set the machine checks.
  /// <para>
  /// <b>The code is the rotated demand, not the authored glyph.</b> A layout may mark a part oriented
  /// (<see cref="MultiblockFacings"/>), in which case the production completion check turns that part's
  /// facing with the structure - a cell authored <c>iwex:hopper-tall-north</c> wants a <c>-west</c> hopper
  /// on a structure at 90 deg. A rig that filled and counted by the authored code instead would place a
  /// north one, satisfy its own check, and report <c>0 of 157 cells unsatisfied</c> on a machine that never
  /// completes - which is exactly the wall the first rotated furnace scenario hit. Codes the layout did not
  /// mark oriented are carried through in their <b>authored</b> form, so a domainless or wildcard glyph
  /// keeps it rather than being re-domained by an <see cref="AssetLocation"/> round trip.
  /// </para>
  /// </summary>
  public IReadOnlyList<(BlockPos Pos, string Wanted)> Cells { get; }

  private StructureRig(
    TestWorld world,
    BlockEntityMultiblockStructure anchor,
    int angle,
    IReadOnlyList<(BlockPos, string)> cells
  )
  {
    _world = world;
    _anchor = anchor;
    Angle = angle;
    Cells = cells;
  }

  /// <summary>
  /// Prepares a rig around an already-placed <paramref name="anchor"/>: attaches
  /// <paramref name="def"/>'s attributes to the anchor's block (so the production code can load the
  /// layout), then resolves the layout rotated by <paramref name="angle"/> into world cells.
  /// <para>
  /// <paramref name="angle"/> must be the angle the machine itself derives from its block variant -
  /// north 0, west 90, south 180, east 270, plus whatever offset that machine applies (the Bessemer
  /// control and the cowper stove face <c>angle + 180</c>). Passing the wrong one is not silently
  /// tolerated: the cells land somewhere the machine will not look, and it never completes.
  /// </para>
  /// </summary>
  public static StructureRig Around(
    TestWorld world,
    BlockEntityMultiblockStructure anchor,
    ExBlockDef def,
    int angle = 0
  )
  {
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

    // Give the placed block the shipped attributes. This is what the machine's own
    // UpdateStructureRotation reads - without it _structure stays null and the monitor tick returns
    // early, which is exactly the state the SetProperty shortcut used to paper over.
    anchor.Block.Attributes = new JsonObject(attributes);

    // Authored glyph per block number, read straight off the JSON so a domainless or wildcard code -
    // notably a shaft's "*:@(air|coalpile|furnace-chargepile)" - keeps its authored form instead of being
    // re-domained by an AssetLocation round trip. Demand() then applies the one rewrite that is real.
    var codeByNumber = ((JObject)layout["blockNumbers"]!)
      .Properties()
      .ToDictionary(p => (int)p.Value!, p => p.Name);

    // The rotation comes from vanilla MultiblockStructure, deserialized and rotated exactly the way the
    // production block entity does at placement.
    MultiblockStructure structure = new JsonObject(layout)
      .AsObject<MultiblockStructure>()!;
    structure.InitForUse(angle);

    // The oriented-part table the layout ships, read the same way the production block entity reads it.
    // Without this the rig fills and counts by the authored code while the machine checks the rotated one.
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
  /// What a cell authored as <paramref name="authored"/> actually requires once the structure is turned to
  /// <paramref name="angle"/> - the rig's half of <c>BlockEntityMultiblockStructure.WantedCodeAt</c>, which
  /// is where the production check applies the same rotation.
  /// <para>
  /// The authored string is handed straight back whenever the rotation is the identity (no oriented parts,
  /// or a code this layout did not mark), rather than the round-tripped one: an
  /// <see cref="AssetLocation"/> round trip re-domains a domainless glyph - notably a shaft's
  /// <c>@(air|coalpile|furnace-chargepile)</c> - and the authored form is what the rest of the rig reads.
  /// </para>
  /// </summary>
  private static string Demand(
    MultiblockFacings facings,
    string authored,
    int angle
  )
  {
    if (facings.IsEmpty)
      return authored;

    var code = new AssetLocation(authored);
    AssetLocation rotated = facings.Rotate(code, angle);
    return rotated.Equals(code) ? authored : rotated.ToString();
  }

  /// <summary>
  /// The world position of a structure-local offset at this rig's rotation - the same mapping the
  /// machine's own <c>GetGlobalPos</c> performs, so a test can address "the west tuyere cell" by its
  /// authored coordinates rather than by hand-rotating them.
  /// </summary>
  public BlockPos Cell(int localX, int localY, int localZ) =>
    ExOrientation.GlobalPos(_anchor.Pos, localX, localY, localZ, Angle);

  /// <summary>
  /// Places a real, functional block (and its entity) at <paramref name="pos"/> before the rig fills
  /// the footprint - a tuyere, a tap, a gas outlet. <see cref="Raise"/> never replaces it: if its code
  /// does not satisfy what the layout wants there, the structure simply does not complete, which is the
  /// honest outcome (a fixture that puts its tuyere one cell out should fail, not be quietly corrected).
  /// </summary>
  public StructureRig Occupy(BlockPos pos, Block block, BlockEntity? be = null)
  {
    _world.Place(pos, block, be);
    if (be != null)
      _world.Attach(be);
    return this;
  }

  /// <summary>
  /// Fills every <b>empty</b> cell of the footprint with a stand-in block whose code matches what that
  /// cell wants, so the machine can see a finished structure. Cells satisfied by air (an open shaft, an
  /// <c>@(air|coalpile)</c> fuel slot) stay empty - filling those would satisfy the code check while
  /// plugging a cell the machine expects open. Idempotent.
  /// <para>
  /// A cell that already holds a block is <b>never</b> replaced, even when that block does not satisfy
  /// the layout. Overwriting would let a fixture place its tuyere one cell out - or at the wrong
  /// rotation - and still get a complete structure, silently orphaning the functional block the test
  /// went on to assert against. The conflict surfaces through <see cref="Missing"/> instead.
  /// </para>
  /// </summary>
  public StructureRig Raise()
  {
    foreach (var (pos, wanted) in Cells)
    {
      Block have = _world.GetBlock(pos);
      if (have.Id != 0)
        continue; // occupied - the anchor itself, or a block the test placed deliberately

      AssetLocation concrete = Concretize(new AssetLocation(wanted));
      // Compared on path alone. A shaft legend is domain-wildcarded (`*:@(air|coalpile|furnace-chargepile)`,
      // because vanilla's matcher cannot cross domains inside an alternation), so its first branch
      // concretises to `*:air`, which is not equal to `game:air` - and a rig that compared the whole code
      // would plug every shaft cell with a stand-in while still reporting the structure satisfied.
      if (concrete.Path == AirCode.Path)
        continue; // an air-satisfied slot: leaving the cell empty *is* the fill

      _world.Place(pos, StandIn(concrete));
    }
    return this;
  }

  /// <summary>
  /// How many cells of the footprint are still unsatisfied, counted against the same rotated demand the
  /// machine's own completion check uses (see <see cref="Cells"/>) - so this number and
  /// <c>StructureComplete</c> cannot disagree. Non-zero after <see cref="Raise"/> means the layout and
  /// the world genuinely disagree.
  /// </summary>
  public int Missing
  {
    get
    {
      int missing = 0;
      foreach (var (pos, wanted) in Cells)
        if (!WildcardUtil.Match(new AssetLocation(wanted), _world.GetBlock(pos).Code))
          missing++;
      return missing;
    }
  }

  /// <summary>
  /// Runs the machine's own structure-completion monitor until it observes the finished footprint, and
  /// returns whether it did. Nothing is forced: the tick is the production one registered by
  /// <c>Initialize</c>, so a false here means the machine cannot see the structure the rig built -
  /// wrong angle, wrong layout, or an anchor that never loaded its attributes.
  /// <para>
  /// The block entity must already be <see cref="TestWorld.Initialize">initialized</see>; the monitor
  /// runs on a 3 s interval, so one interval is advanced per attempt.
  /// </para>
  /// </summary>
  public bool AwaitCompletion(int maxMonitorTicks = 2)
  {
    for (int i = 0; i < maxMonitorTicks && !_anchor.StructureComplete; i++)
      _world.AdvanceBlockEntityTime(3000);
    return _anchor.StructureComplete;
  }

  /// <summary>
  /// <see cref="Raise"/> + <see cref="TestWorld.Initialize"/> + <see cref="AwaitCompletion"/>, throwing
  /// with the missing-cell breakdown if the machine does not complete - the one-liner for a scenario
  /// whose subject is what the machine does <em>after</em> it is built, not the building.
  /// </summary>
  public StructureRig Complete()
  {
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
  /// A per-cell breakdown of what each unsatisfied cell wants and what it actually holds - the message
  /// <see cref="Complete"/> throws with, exposed so a fixture that raises a structure and then lets a
  /// scene build over it can say *which* cell got trodden on.
  /// </summary>
  public string MissingReport => UnsatisfiedReport();

  /// <summary>A per-cell breakdown of what each unsatisfied cell wants and what it actually holds.</summary>
  private string UnsatisfiedReport()
  {
    var lines = new List<string>();
    foreach (var (pos, wanted) in Cells)
    {
      Block have = _world.GetBlock(pos);
      if (!WildcardUtil.Match(new AssetLocation(wanted), have.Code))
        lines.Add($"\n  {pos}: wants '{wanted}', has '{have.Code}'");
    }
    return string.Concat(lines);
  }

  /// <summary>One stand-in block per distinct code, so a 100-cell footprint registers a handful of blocks.</summary>
  private Block StandIn(AssetLocation code)
  {
    if (_standIns.TryGetValue(code.ToString(), out Block? cached))
      return cached;

    var block = TestBlocks.Configure(new Block(), code.ToString(), _nextId++);
    _standIns[code.ToString()] = block;
    return block;
  }

  /// <summary>
  /// Turns a wanted code into a concrete one that satisfies it: an alternation <c>@(air|coalpile)</c>
  /// collapses to its first branch, and <c>*</c> becomes a literal segment. The result only has to
  /// match the wildcard - a footprint cell is checked by code, so a stand-in needs no behaviour.
  /// </summary>
  private static AssetLocation Concretize(AssetLocation wanted) =>
    new(wanted.Domain, FirstAlternative(wanted.Path).Replace("*", "x"));

  /// <summary>
  /// Collapses every alternation group down to its first branch, <b>counting bracket depth</b>. Vanilla
  /// treats the inside of <c>@( )</c> as a regex, so a branch may carry its own parenthesised
  /// alternation - the smoke stack's chimney course is
  /// <c>@(claybricks-good-fire|refractorybricks-good-.*|brickcourse-.*-(black|…|tan))</c>. A regex that
  /// scans to the first <c>)</c> stops inside that nested group and leaves a stray bracket on the end of
  /// the code, which then matches nothing: the stand-ins get placed, and the structure silently never
  /// completes. Depth-counting is the whole fix; branches are also resolved recursively, so a nested
  /// group in the chosen branch collapses too.
  /// </summary>
  private static string FirstAlternative(string path)
  {
    var sb = new StringBuilder();
    int i = 0;
    while (i < path.Length)
    {
      bool tagged = path[i] == '@' && i + 1 < path.Length && path[i + 1] == '(';
      if (!tagged && path[i] != '(')
      {
        sb.Append(path[i++]);
        continue;
      }

      int open = tagged ? i + 1 : i;
      int close = MatchingParen(path, open);
      if (close < 0)
      {
        sb.Append(path[i++]); // unbalanced - leave it alone rather than mangle it further
        continue;
      }

      sb.Append(
        FirstAlternative(FirstBranch(path.Substring(open + 1, close - open - 1)))
      );
      i = close + 1;
    }
    return sb.ToString();
  }

  /// <summary>Index of the <c>)</c> closing the <c>(</c> at <paramref name="open"/>, or -1 if unbalanced.</summary>
  private static int MatchingParen(string s, int open)
  {
    int depth = 0;
    for (int i = open; i < s.Length; i++)
    {
      if (s[i] == '(')
        depth++;
      else if (s[i] == ')' && --depth == 0)
        return i;
    }
    return -1;
  }

  /// <summary>The part of an alternation body before its first <b>top-level</b> <c>|</c>.</summary>
  private static string FirstBranch(string inner)
  {
    int depth = 0;
    for (int i = 0; i < inner.Length; i++)
    {
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
