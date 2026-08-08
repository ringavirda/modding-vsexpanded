using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpandedLib.Blocks.Machines;
using ExpandedLib.Helpers;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// Base block entity for the mod's multiblock machines (blast furnace, cowper stove,
/// bessemer control). Runs a slow monitor tick that detects when the structure is
/// completed or broken, and a production tick that fires only while complete.
/// Subclasses supply the orientation logic, production behavior, and status messages.
/// </summary>
public abstract class BlockEntityMultiblockStructure
  : BlockEntityProductionMachine
{
  protected MultiblockStructure? _structure;
  protected MultiblockStructure? _highlightedStructure;
  protected int _currentAngle = -1;

  /// <summary>
  /// The angle actually handed to <c>InitForUse</c> - <c>_currentAngle + initAngleOffset</c>. The offsets
  /// are rotated by this, so the oriented-part check must be too; the bessemer control's <c>+180</c> frame
  /// is exactly the case where using <see cref="_currentAngle"/> would face every part backwards.
  /// </summary>
  private int _structureInitAngle;
  private MultiblockFacings _facings = MultiblockFacings.None;
  private MultiblockCellRoles _roles = MultiblockCellRoles.None;
  private long _completionTickId;

  /// <summary>Whether every block of the multiblock structure is currently in place.</summary>
  public bool StructureComplete { get; protected set; }

  /// <summary>Interval (ms) of the structure-completion monitor tick.</summary>
  protected virtual int CompletionTickMs => 3000;

  /// <summary>The production tick runs only while the structure is complete.</summary>
  protected override bool CanRunProduction => StructureComplete;

  /// <summary>Register the production tick on load only if the structure is already complete;
  /// the monitor tick starts/stops it across completion transitions.</summary>
  protected override bool AutoStartProduction => StructureComplete;

  public override void Initialize(ICoreAPI api)
  {
    // Base registers the production tick (only when already complete, via AutoStartProduction).
    base.Initialize(api);
    // The monitor tick runs unconditionally to detect both completion and breakage.
    if (api.Side == EnumAppSide.Server)
      StartMonitorTick();
  }

  /// <summary>Starts both the completion monitor and the production tick.</summary>
  protected void StartStructureTick()
  {
    StartMonitorTick();
    StartProductionTick();
  }

  protected void StartMonitorTick()
  {
    if (_completionTickId == 0 && Api.Side == EnumAppSide.Server)
      _completionTickId = RegisterGameTickListener(
        OnMonitorStructureTick,
        CompletionTickMs
      );
  }

  /// <summary>Stops both ticks (used on block removal).</summary>
  protected void StopStructureTick()
  {
    StopProductionTick();
    if (_completionTickId != 0)
    {
      UnregisterGameTickListener(_completionTickId);
      _completionTickId = 0;
    }
  }

  private void OnMonitorStructureTick(float dt)
  {
    UpdateStructureRotation();
    if (_structure == null)
      return;

    bool nowComplete = IncompleteBlockCount() == 0;
    if (nowComplete == StructureComplete)
      return;

    StructureComplete = nowComplete;
    if (nowComplete)
    {
      OnStructureCompleted();
      StartProductionTick();
    }
    else
    {
      OnStructureLost();
      if (StopsProductionOnStructureLost)
        StopProductionTick();
    }
    MarkDirty(true);
  }

  /// <summary>Called when a previously complete structure becomes incomplete. Default: no-op.</summary>
  protected virtual void OnStructureLost() { }

  /// <summary>
  /// Whether losing the structure also <b>unregisters</b> the production tick. True by default, which is
  /// right for almost everything: a machine missing a part should stop, and dropping the listener is
  /// cheaper than gating a body that can never do anything.
  /// <para>
  /// <b>A machine that must keep running while broken has to opt out here, not in
  /// <see cref="CanRunProduction"/>.</b> The gate is only consulted by a listener that still exists, so a
  /// machine overriding only the gate goes quiet with no error and no test failure - it simply freezes,
  /// holding its state for ever, which is indistinguishable from stopping deliberately. The case that
  /// surfaced this is the breached blast furnace: iwex's shaft furnace keeps burning when its walls come out (a breach is
  /// the opposite of a choke - opened to the air, it draws harder), and neither deleting its extinguish
  /// call nor relaxing its own tick guard had any effect while the listener was gone.
  /// </para>
  /// </summary>
  protected virtual bool StopsProductionOnStructureLost => true;

  /// <summary>Recomputes the structure's rotation/angle from the block orientation.</summary>
  protected abstract void UpdateStructureRotation();

  /// <summary>
  /// Canonical body for <see cref="UpdateStructureRotation"/>: (re)loads the
  /// <c>multiblockStructure</c> JSON when missing or <paramref name="angle"/> changed, calls
  /// <c>InitForUse(angle + initAngleOffset)</c>, caches the angle, and clears any stale build
  /// projection. <paramref name="initAngleOffset"/> covers machines whose local frame faces
  /// opposite the stored angle (e.g. the bessemer control at <c>angle + 180</c>).
  /// </summary>
  protected void SetStructureAngle(int angle, int initAngleOffset = 0)
  {
    if (_structure != null && _currentAngle == angle)
      return;

    _structure = Block.Attributes?[
      "multiblockStructure"
    ]?.AsObject<MultiblockStructure>();
    _structure?.InitForUse(angle + initAngleOffset);
    // All three caches below are reads OF the structure that was just replaced: the number->code map comes
    // out of its BlockNumbers, and the accepting-cell and role-cell lists are world positions computed at
    // the old angle. Dropping them here is what stops a wrench turn (or a block exchange) answering out of
    // the previous facing - and keeps them from ever disagreeing about which layout they describe.
    _codeByNumber = null;
    _cellsAccepting = null;
    _cellsWithRole = null;
    _currentAngle = angle;
    _structureInitAngle = angle + initAngleOffset;
    _facings = MultiblockFacings.FromAttributes(Block.Attributes);
    _roles = MultiblockCellRoles.FromAttributes(Block.Attributes);

    if (Api is ICoreClientAPI capi && _highlightedStructure != null)
    {
      _highlightedStructure.ClearHighlights(Api.World, capi.World.Player);
      _highlightedStructure = null;
    }
  }

  /// <summary>Converts a structure-local offset into a world position for the current rotation.</summary>
  protected virtual BlockPos GetGlobalPos(int localX, int localY, int localZ) =>
    ExOrientation.GlobalPos(Pos, localX, localY, localZ, _currentAngle);

  /// <summary>
  /// Ensures <see cref="_structure"/> and <see cref="_currentAngle"/> are populated. The monitor tick
  /// primes them server-side; a client-side read that needs the layout - a functional component
  /// resolving the anchor it belongs to during <c>GetBlockInfo</c> - primes them lazily here, because
  /// the monitor tick never runs on the client. Idempotent: <see cref="SetStructureAngle"/> is guarded.
  /// </summary>
  protected void EnsureStructureLoaded()
  {
    if (_structure == null)
      UpdateStructureRotation();
  }

  /// <summary>
  /// Whether <paramref name="worldCell"/> is one of the cells this structure occupies for its placed
  /// rotation - the target lands on one of the anchor's transformed layout offsets. A functional
  /// component (tap, hopper, tuyere) uses this to confirm the anchor it scanned up actually owns it,
  /// which is what disambiguates two adjacent structures whose scan boxes overlap. Reads the same
  /// <see cref="MultiblockStructure.TransformedOffsets"/> the build-outline highlight walks, and loads
  /// the layout lazily so it answers on the client too.
  /// </summary>
  public bool OwnsCell(BlockPos worldCell)
  {
    EnsureStructureLoaded();
    var offsets = _structure?.TransformedOffsets;
    if (offsets == null)
      return false;
    foreach (var o in offsets)
      if (
        Pos.X + o.X == worldCell.X
        && Pos.Y + o.Y == worldCell.Y
        && Pos.Z + o.Z == worldCell.Z
      )
        return true;
    return false;
  }

  private static readonly BlockPos[] _noCells = [];

  private Dictionary<AssetLocation, BlockPos[]>? _cellsAccepting;

  /// <summary>
  /// The world cells of this structure's footprint whose layout slot would <b>accept</b>
  /// <paramref name="blockCode"/> - every cell that block may stand in without the completion check ever
  /// counting it missing. Rotation-correct for the placed facing, and cached.
  /// <para>
  /// The layout is asked about itself, which is the whole point: no legend string is restated here and no
  /// machine keeps a second, hand-written list of "the cells that are X" beside the drawing it was copied
  /// from. The test is the same <see cref="WildcardUtil.Match"/> against the same rotation-resolved wanted
  /// code <see cref="IncompleteBlockCount"/> uses, so "this cell accepts that block" and "placing that
  /// block here leaves the structure complete" are one statement rather than two that can drift apart.
  /// Cells whose legend carries a facing are resolved through <see cref="MultiblockFacings"/> first, so an
  /// oriented slot admits the variant the placed structure actually wants.
  /// </para>
  /// <para>
  /// <paramref name="blockCode"/> is the <b>concrete</b> code of the block that would be placed
  /// (<c>iwex:furnace-chargepile</c>), never a wildcard - vanilla's matcher takes the wildcard on the left, and
  /// this argument is on the right. A code no cell admits answers empty, and that is a real answer rather
  /// than a failure: a layout whose fuel legend is <c>game:@(air|coalpile)</c> genuinely has nowhere to
  /// put an <c>iwex:furnace-chargepile</c>.
  /// </para>
  /// <para>
  /// Cached per code, and dropped in <see cref="SetStructureAngle"/> because these are <b>world</b>
  /// cells: a wrench turn moves every one of them. That drop is the load-bearing half.
  /// </para>
  /// <para>
  /// The empty answer of a structure whose layout has not arrived yet is additionally never written to the
  /// cache, and that half is <b>knowingly belt-and-braces</b>: <see cref="EnsureStructureLoaded"/> re-enters
  /// the reload path on every call while the layout is missing, and that path drops the cache, so today
  /// nothing can memoise "nothing" even without this line (verified by mutation - removing it fails no
  /// test). It stays because the failure it guards against is silent and permanent - a client-side block
  /// entity answering "no cells" for the rest of its life - and it only stops being free if someone moves
  /// the invalidation.
  /// </para>
  /// </summary>
  public IReadOnlyList<BlockPos> CellsAccepting(AssetLocation blockCode)
  {
    EnsureStructureLoaded();
    if (_structure?.TransformedOffsets is not { } offsets)
      return _noCells;

    _cellsAccepting ??= [];
    if (_cellsAccepting.TryGetValue(blockCode, out BlockPos[]? cached))
      return cached;

    var cells = new List<BlockPos>();
    foreach (BlockOffsetAndNumber offset in offsets)
      if (
        WantedCodeAt(offset) is AssetLocation wanted
        && WildcardUtil.Match(wanted, blockCode)
      )
        cells.Add(Pos.AddCopy(offset.X, offset.Y, offset.Z));

    BlockPos[] result = [.. cells];
    _cellsAccepting[blockCode] = result;
    return result;
  }

  private Dictionary<CellRole, BlockPos[]>? _cellsWithRole;

  /// <summary>
  /// The world cells this structure's layout marks with <paramref name="role"/> - "where are my tuyeres?"
  /// asked of the drawing itself. Rotation-correct for the placed facing, and cached. Empty for a layout that
  /// declares no roles, which is every layout authored before roles existed.
  /// <para>
  /// A role says what a cell is <b>for</b>, independent of what fills it, and that is the difference from
  /// <see cref="CellsAccepting"/>: that one answers "which cells would take this block", which is genuinely
  /// the right question when the caller has a block in hand, but it couples the caller to a block code that
  /// a retype can move out from under it. Both read the same footprint; neither restates a legend.
  /// </para>
  /// <para>
  /// <b>How rotation is inherited rather than reimplemented.</b> The role table stores the offsets the
  /// author drew, in the north-default frame. Vanilla's <c>InitForUse</c> builds
  /// <see cref="MultiblockStructure.TransformedOffsets"/> by walking <see cref="MultiblockStructure.Offsets"/>
  /// in order and rotating each one, leaving the two lists index-aligned and <c>Offsets</c> itself untouched
  /// (verified against the decompiled API). So the authored offset is looked up in <c>Offsets</c> and the
  /// <b>same index</b> is read out of <c>TransformedOffsets</c>. There is no second copy of the rotation
  /// maths here to disagree with the completion walk - not even a choice between <c>_currentAngle</c> and
  /// <see cref="_structureInitAngle"/>, which is exactly the distinction the bessemer control's <c>+180</c>
  /// frame turns on.
  /// </para>
  /// <para>
  /// Cached per role and dropped in <see cref="SetStructureAngle"/>, because these are <b>world</b> cells:
  /// a wrench turn moves every one of them. That drop is the load-bearing half.
  /// </para>
  /// </summary>
  public IReadOnlyList<BlockPos> CellsWithRole(CellRole role)
  {
    EnsureStructureLoaded();
    if (_structure?.TransformedOffsets is not { } transformed)
      return _noCells;
    List<BlockOffsetAndNumber> authored = _structure.Offsets;

    _cellsWithRole ??= [];
    if (_cellsWithRole.TryGetValue(role, out BlockPos[]? cached))
      return cached;

    IReadOnlySet<(int X, int Y, int Z)> wanted = _roles.CellsOf(role);
    var cells = new List<BlockPos>();
    // The two lists are the same length by construction; bounding on both keeps a hand-edited or
    // partially-initialised structure from throwing rather than answering short.
    for (int i = 0; i < authored.Count && i < transformed.Count; i++)
      if (wanted.Contains((authored[i].X, authored[i].Y, authored[i].Z)))
        cells.Add(
          Pos.AddCopy(transformed[i].X, transformed[i].Y, transformed[i].Z)
        );

    BlockPos[] result = [.. cells];
    _cellsWithRole[role] = result;
    return result;
  }

  /// <summary>
  /// The <b>authored</b> (north-frame) offsets this structure's layout marks with
  /// <paramref name="role"/> - the same cells <see cref="CellsWithRole"/> answers, before any rotation and
  /// before the anchor's position is added. Empty for a layout that marks none.
  /// <para>
  /// <b>Why a second accessor rather than un-rotating the first.</b> A caller that wants a
  /// <em>structure-local</em> fact - "how tall is the shaft", "which local <c>(x, z)</c> columns are there" -
  /// wants an answer no facing can move, and the authored offsets are already in that frame. Going through
  /// <see cref="CellsWithRole"/> and back would rotate each cell forward and then inverse-rotate it, which
  /// is a no-op that costs a round trip and, worse, reads as though rotation mattered here. It does not: a
  /// local box is a compile-time fact of the block type, which is exactly what lets a consumer cache it.
  /// </para>
  /// <para>
  /// <b>Not cached and not defensively copied</b>, both deliberately: this is a dictionary lookup into the
  /// table <see cref="MultiblockCellRoles"/> already holds, and the set it returns is that table's own -
  /// read-only by its interface, replaced wholesale (never mutated) when the layout reloads. A caller
  /// wanting it per tick should cache the <em>derived</em> fact, not this.
  /// </para>
  /// <para>
  /// Loads the layout lazily exactly as <see cref="CellsWithRole"/> does, so it answers on the client and -
  /// unlike anything needing <c>Api</c> - before <c>Initialize</c>, which is when a block entity restoring
  /// a save first needs its own geometry. <c>Block</c> is assigned by vanilla's <c>CreateBehaviors</c>
  /// immediately before <c>FromTreeAttributes</c> on every load path, so the layout is readable there.
  /// </para>
  /// </summary>
  public IReadOnlySet<(int X, int Y, int Z)> LocalCellsWithRole(CellRole role)
  {
    EnsureStructureLoaded();
    return _roles.CellsOf(role);
  }

  /// <summary>
  /// Scans a bounded box around <paramref name="componentPos"/> for a <typeparamref name="T"/> anchor
  /// whose structure <see cref="OwnsCell">owns</see> that cell, and returns it - the reverse lookup a
  /// functional component uses to find the multiblock it is part of. The anchor pushes to its
  /// components by offset and nothing points back, so the component scans. The box reaches
  /// <paramref name="below"/> cells down / <paramref name="above"/> up and <paramref name="horizontal"/>
  /// out on each horizontal axis, sized by the caller to cover its tallest component. Returns null when
  /// no owning anchor is in range (a component placed before its anchor, or a broken structure) - the
  /// caller then shows only its own readout. The ownership gate makes the box slack harmless: a wider
  /// box only turns up more candidates to reject, never a wrong owner.
  /// </summary>
  public static T? FindAnchorOwning<T>(
    IWorldAccessor world,
    BlockPos componentPos,
    int horizontal,
    int below,
    int above
  )
    where T : BlockEntityMultiblockStructure
  {
    for (int dy = -below; dy <= above; dy++)
    for (int dx = -horizontal; dx <= horizontal; dx++)
    for (int dz = -horizontal; dz <= horizontal; dz++)
    {
      BlockPos at = new(
        componentPos.X + dx,
        componentPos.Y + dy,
        componentPos.Z + dz,
        componentPos.dimension
      );
      if (
        world.BlockAccessor.GetBlockEntity(at) is T anchor
        && anchor.OwnsCell(componentPos)
      )
        return anchor;
    }
    return null;
  }

  /// <summary>
  /// Player interaction entry point (the structure-projection toggle): re-checks completeness,
  /// fires the completed/lost callbacks, and client-side shows the build outline + missing count
  /// or clears it once complete. <see cref="FromTreeAttributes"/> also auto-clears the projection
  /// the moment the structure completes.
  /// </summary>
  public virtual void Interact(IPlayer byPlayer)
  {
    UpdateStructureRotation();
    if (_structure == null)
      return;

    // Tally missing blocks by wanted code while counting, to both draw the projection and
    // print an exact shopping list.
    var missingByCode = new Dictionary<AssetLocation, int>();
    int missingCount = IncompleteBlockCount(
      (haveBlock, wantBlockCode) =>
      {
        // Air-satisfied or auto-filled slots aren't player-gathered, so leave them out.
        if (IsAutoFilled(wantBlockCode))
          return;
        missingByCode.TryGetValue(wantBlockCode, out int count);
        missingByCode[wantBlockCode] = count + 1;
      }
    );
    bool wasComplete = StructureComplete;
    StructureComplete = missingCount == 0;

    if (Api.Side == EnumAppSide.Server)
    {
      if (StructureComplete && !wasComplete)
      {
        OnStructureCompleted();
        StartStructureTick();
        MarkDirty(true);
      }
      else if (!StructureComplete && wasComplete)
      {
        OnStructureLost();
        StopProductionTick();
        MarkDirty(true);
      }

      if (!StructureComplete && byPlayer is IServerPlayer serverPlayer)
        SendMissingBlocksReport(serverPlayer, missingByCode);
    }

    if (Api is ICoreClientAPI clientApi)
    {
      if (missingCount > 0)
      {
        _highlightedStructure = _structure;
        clientApi.TriggerIngameError(
          this,
          "incomplete",
          GetIncompleteMessage(missingCount)
        );
        HighlightIncompleteSafe(_highlightedStructure, byPlayer);
      }
      else
      {
        clientApi.TriggerIngameError(this, "complete", GetCompleteMessage());
        _highlightedStructure?.ClearHighlights(Api.World, byPlayer);
        _highlightedStructure = null;
      }
    }
  }

  /// <summary>
  /// Number of structure cells not yet satisfied, and the replacement for vanilla
  /// <see cref="MultiblockStructure.InCompleteBlockCount"/>. It walks the same
  /// <c>TransformedOffsets</c> vanilla does and matches the same way, with one addition: a cell whose
  /// legend carries a facing has that facing <b>rotated with the structure</b> first
  /// (<see cref="MultiblockFacings"/>), so a layout can demand a correctly-oriented slab or door.
  /// <para>
  /// <paramref name="onMissing"/> receives <c>(blockThere, wantedCode)</c> per unsatisfied cell - the
  /// wanted code already rotated, so a missing-blocks report names the variant the player must actually
  /// place. Returns 0 when the structure is not loaded, matching the old <c>?? 0</c> call sites.
  /// </para>
  /// </summary>
  protected int IncompleteBlockCount(Action<Block, AssetLocation>? onMissing = null)
  {
    if (_structure?.TransformedOffsets == null)
      return 0;

    int missing = 0;
    foreach (BlockOffsetAndNumber offset in _structure.TransformedOffsets)
    {
      if (WantedCodeAt(offset) is not AssetLocation wanted)
        continue;

      Block actual = Api.World.BlockAccessor.GetBlockRaw(
        Pos.X + offset.X,
        Pos.InternalY + offset.Y,
        Pos.Z + offset.Z
      );
      if (WildcardUtil.Match(wanted, actual.Code))
        continue;

      missing++;
      onMissing?.Invoke(actual, wanted);
    }
    return missing;
  }

  /// <summary>
  /// The (rotation-resolved) code a transformed offset requires, or null when its block number has no
  /// <c>blockNumbers</c> entry. Vanilla keeps the number→code map private, so it is rebuilt from the
  /// public <c>BlockNumbers</c> - cached, because both the completion walk and the highlight need it.
  /// </summary>
  private AssetLocation? WantedCodeAt(BlockOffsetAndNumber offset)
  {
    if (_structure == null)
      return null;

    _codeByNumber ??= BuildCodeByNumber(_structure);
    return _codeByNumber.TryGetValue(offset.W, out AssetLocation? wanted)
      ? _facings.Rotate(wanted, _structureInitAngle)
      : null;
  }

  private Dictionary<int, AssetLocation>? _codeByNumber;

  private static Dictionary<int, AssetLocation> BuildCodeByNumber(
    MultiblockStructure structure
  )
  {
    var map = new Dictionary<int, AssetLocation>();
    foreach (var kv in structure.BlockNumbers)
      map[kv.Value] = kv.Key;
    return map;
  }

  /// <summary>
  /// Crash-safe replacement for vanilla <see cref="MultiblockStructure.HighlightIncompleteParts"/>,
  /// which tints each empty slot with <c>SearchBlocks(wantedCode)[0]</c> and throws
  /// <see cref="System.IndexOutOfRangeException"/> when a (wildcard) code resolves to no block.
  /// This mirrors the vanilla logic but falls back to a neutral tint for unresolvable slots.
  /// </summary>
  private void HighlightIncompleteSafe(
    MultiblockStructure structure,
    IPlayer player
  )
  {
    var offsets = structure.TransformedOffsets;
    if (offsets == null)
      return;

    var positions = new List<BlockPos>();
    var colors = new List<int>();

    foreach (var offset in offsets)
    {
      // Same rotation-resolved code the completion walk uses, so the tint and the count can never
      // disagree about which cells are wrong - and an oriented slot resolves to the exact variant,
      // which is what makes SearchBlocks below pick the right-facing block to colour from.
      if (WantedCodeAt(offset) is not AssetLocation wanted)
        continue;

      Block actual = Api.World.BlockAccessor.GetBlockRaw(
        Pos.X + offset.X,
        Pos.InternalY + offset.Y,
        Pos.Z + offset.Z
      );
      if (WildcardUtil.Match(wanted, actual.Code))
        continue;

      positions.Add(new BlockPos(offset.X, offset.Y, offset.Z).Add(Pos));

      if (actual.Id != 0)
      {
        // A wrong solid block occupies the slot - vanilla tints these red.
        colors.Add(ColorUtil.ColorFromRgba(215, 94, 94, 0x60));
        continue;
      }

      // Empty slot: tint with the wanted block's color when it resolves, otherwise
      // fall back to a neutral blue instead of crashing on an empty SearchBlocks.
      Block[] matches = Api.World.SearchBlocks(wanted);
      if (matches.Length == 0)
      {
        colors.Add(ColorUtil.ColorFromRgba(94, 94, 215, 0x60));
        continue;
      }

      int color = matches[0].GetColor(Api as ICoreClientAPI, Pos) & 0xFFFFFF;
      color |= 0x60 << 24;
      colors.Add(color);
    }

    Api.World.HighlightBlocks(
      player,
      MultiblockStructure.HighlightSlotId,
      positions,
      colors
    );
  }

  private static readonly AssetLocation AirCode = new("game:air");

  /// <summary>
  /// True when a slot is satisfied without the player gathering a block - an air slot (open shaft,
  /// "@(air|coalpile)" fuel) or a structure-filler cell. Excluded from the materials report.
  /// </summary>
  private static bool IsAutoFilled(AssetLocation wantBlockCode) =>
    WildcardUtil.Match(wantBlockCode, AirCode)
    || WildcardUtil.Match(wantBlockCode, StructureFillers.FillerCode);

  /// <summary>
  /// Sends the player a chat breakdown of every block still missing from the
  /// structure and how many of each, resolving (possibly wildcard) codes to
  /// readable block names.
  /// </summary>
  private void SendMissingBlocksReport(
    IServerPlayer player,
    Dictionary<AssetLocation, int> missingByCode
  )
  {
    if (missingByCode.Count == 0)
      return;

    // exlib owns these strings: it is its own mod with its own asset domain, so the shared report does
    // not borrow a consumer's lang file. Going through the generated ExlibLang accessors means a
    // renamed or deleted key is a compile error rather than a key echoed at the player at runtime.
    var sb = new StringBuilder();
    sb.Append(Lang.Get(ExlibLang.StructureMissingHeader));

    foreach (
      var entry in missingByCode
        .OrderByDescending(e => e.Value)
        .ThenBy(e => ResolveBlockName(e.Key))
    )
    {
      sb.Append('\n');
      sb.Append(
        Lang.Get(
          ExlibLang.StructureMissingLine,
          entry.Value,
          ResolveBlockName(entry.Key)
        )
      );
    }

    player.SendMessage(
      GlobalConstants.GeneralChatGroup,
      sb.ToString(),
      EnumChatType.Notification
    );
  }

  /// <summary>
  /// Resolves a structure block code - which may be a wildcard such as
  /// "iwex:furnace-blastcore-*" - to a human-readable display name.
  /// </summary>
  private string ResolveBlockName(AssetLocation wantBlockCode)
  {
    Block? block = Api.World.GetBlock(wantBlockCode);
    if (block == null)
    {
      Block[] matches = Api.World.SearchBlocks(wantBlockCode);
      if (matches.Length > 0)
        block = matches[0];
    }

    return block != null
      ? new ItemStack(block).GetName()
      : wantBlockCode.ToShortString();
  }

  /// <summary>Called when the structure transitions to complete. Default: no-op.</summary>
  protected virtual void OnStructureCompleted() { }

  /// <summary>Returns the ingame-error message shown when the structure is missing <paramref name="missingCount"/> blocks.</summary>
  protected abstract string GetIncompleteMessage(int missingCount);

  /// <summary>Returns the ingame-error message shown when the structure is complete.</summary>
  protected abstract string GetCompleteMessage();

  public override void OnBlockRemoved()
  {
    base.OnBlockRemoved();
    StopStructureTick();
    if (Api is ICoreClientAPI capi)
      _highlightedStructure?.ClearHighlights(Api.World, capi.World.Player);
  }

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    tree.SetBool("structureComplete", StructureComplete);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    bool wasComplete = StructureComplete;
    StructureComplete = tree.GetBool("structureComplete");

    // Auto-hide the build projection the moment the structure finishes.
    if (
      !wasComplete
      && StructureComplete
      && Api is ICoreClientAPI capi
      && _highlightedStructure != null
    )
    {
      _highlightedStructure.ClearHighlights(Api.World, capi.World.Player);
      _highlightedStructure = null;
    }
  }
}
