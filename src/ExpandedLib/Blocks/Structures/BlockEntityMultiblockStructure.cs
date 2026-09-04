using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpandedLib.Blocks.Machines;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
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
/// bessemer control). Runs a slow monitor tick that detects when the structure is completed or broken,
/// publishes the completed pattern as readiness, and starts and stops whatever production process the
/// machine carries across those transitions. Subclasses supply the orientation logic and the status
/// messages.
/// <para>
/// This is form alone: a multiblock that also produces derives from
/// <see cref="BlockEntityMultiblockMachine"/>, which is the only place the process is taken on.
/// </para>
/// </summary>
public abstract class BlockEntityMultiblockStructure
  : BlockEntity,
    IProductionReadiness {
  protected MultiblockStructure? _structure;
  protected MultiblockStructure? _highlightedStructure;
  protected int _currentAngle = -1;

  /// <summary>
  /// The angle handed to <c>InitForUse</c> (<c>_currentAngle + initAngleOffset</c>). Layout offsets are
  /// rotated by it, so oriented-part checks use this rather than <see cref="_currentAngle"/>, which
  /// differs for a machine with a frame offset such as the bessemer control's <c>+180</c>.
  /// </summary>
  private int _structureInitAngle;
  private MultiblockFacings _facings = MultiblockFacings.None;
  private MultiblockCellRoles _roles = MultiblockCellRoles.None;
  private MultiblockConnectors _connectors = MultiblockConnectors.None;
  private long _completionTickId;

  /// <summary>Whether every block of the multiblock structure is currently in place.</summary>
  public bool StructureComplete { get; protected set; }

  /// <summary>Interval (ms) of the structure-completion monitor tick.</summary>
  protected virtual int CompletionTickMs => 3000;

  /// <summary>Whether the machine may run production this tick: a multiblock is ready once its pattern
  /// is whole. A machine that must also run while broken widens this and opts out of
  /// <see cref="StopsProductionOnStructureLost"/>.</summary>
  protected virtual bool CanRunProduction => StructureComplete;

  /// <summary>This form's readiness answer, for a hosted process and anything else that asks. Not
  /// overridable: a subclass states its gate in <see cref="CanRunProduction"/>, so the two cannot drift
  /// apart.</summary>
  public bool IsReadyToProduce => CanRunProduction;

  public override void Initialize(ICoreAPI api) {
    // A process the machine carries registers its own tick from inside base.Initialize, which fans out
    // over the behaviours, and only when the structure is already complete.
    base.Initialize(api);
    if (api.Side == EnumAppSide.Server) {
      // Prime the angle before anything ticks. _currentAngle starts at -1, which ExOrientation
      // normalises to 359 and then resolves through the unrotated default - so a structure that
      // loads already complete would run its first production ticks reading every structure-local
      // offset in the block's north frame, finding its peripherals at mirrored positions.
      UpdateStructureRotation();
      // The monitor tick runs unconditionally to detect both completion and breakage.
      StartMonitorTick();
    }
  }

  /// <summary>Starts both the completion monitor and the production tick.</summary>
  protected void StartStructureTick() {
    StartMonitorTick();
    ProductionProcess.Start(this);
  }

  protected void StartMonitorTick() {
    if (_completionTickId == 0 && Api.Side == EnumAppSide.Server)
      _completionTickId = RegisterGameTickListener(
        OnMonitorStructureTick,
        CompletionTickMs
      );
  }

  /// <summary>Stops both ticks (used on block removal).</summary>
  protected void StopStructureTick() {
    ProductionProcess.Stop(this);
    if (_completionTickId != 0) {
      UnregisterGameTickListener(_completionTickId);
      _completionTickId = 0;
    }
  }

  private void OnMonitorStructureTick(float dt) {
    UpdateStructureRotation();
    if (_structure == null)
      return;

    bool nowComplete = IncompleteBlockCount() == 0;
    if (nowComplete == StructureComplete)
      return;

    StructureComplete = nowComplete;
    if (nowComplete) {
      OnStructureCompleted();
      ProductionProcess.Start(this);
    } else {
      OnStructureLost();
      // Asked of every publisher on the machine, not of this class alone: a behaviour that must keep
      // its process ticking through the breach answers for the machine too.
      if (ProductionReadiness.StopsProductionWhenNotReady(this))
        ProductionProcess.Stop(this);
    }
    MarkDirty(true);
  }

  /// <summary>Called when a previously complete structure becomes incomplete. Default: no-op.</summary>
  protected virtual void OnStructureLost() { }

  /// <summary>
  /// One unsatisfied footprint cell: what stands there, the rotation-resolved code the layout wants, the
  /// world position, and - when the code matched but the cell's connector faces the wrong way - the
  /// outward face it must open to. <see cref="OutwardFace"/> is null for an ordinary code mismatch, and
  /// the two cases read differently to a player: one wants a block placed, the other wants the block
  /// already there turned.
  /// </summary>
  public readonly record struct MissingCell(
    Block Actual,
    AssetLocation Wanted,
    BlockPos At,
    string? OutwardFace
  );

  /// <summary>
  /// Whether losing the structure also unregisters the production tick. True by default. A machine that
  /// must keep running while broken overrides this, not <see cref="CanRunProduction"/>: that gate is only
  /// consulted by a listener that still exists, so overriding it alone leaves the machine frozen with its
  /// state held rather than stopped. A breached shaft furnace keeps burning by opting out here.
  /// </summary>
  protected virtual bool StopsProductionOnStructureLost => true;

  /// <summary>Losing the pattern is this form's way of losing readiness, so the published answer is the
  /// one above. Not overridable, so a subclass has a single place to state the rule.</summary>
  public bool StopsProductionWhenNotReady => StopsProductionOnStructureLost;

  /// <summary>Recomputes the structure's rotation/angle from the block orientation.</summary>
  protected abstract void UpdateStructureRotation();

  /// <summary>
  /// Canonical body for <see cref="UpdateStructureRotation"/>: reloads the <c>multiblockStructure</c>
  /// JSON when missing or <paramref name="angle"/> changed, calls <c>InitForUse</c> with
  /// <c>angle + initAngleOffset</c>, caches the angle and clears any stale build projection. The offset
  /// covers a machine whose local frame faces opposite the stored angle (bessemer control: <c>+180</c>).
  /// </summary>
  protected void SetStructureAngle(int angle, int initAngleOffset = 0) {
    if (_structure != null && _currentAngle == angle)
      return;

    _structure = Block.Attributes?[
      "multiblockStructure"
    ]?.AsObject<MultiblockStructure>();
    _structure?.InitForUse(angle + initAngleOffset);
    // These caches derive from the structure just replaced: the number->code map, and the accepting-cell
    // and role-cell lists as world positions at the old angle. Dropping them keeps a wrench turn or a
    // block exchange from answering out of the previous facing.
    _codeByNumber = null;
    _cellsAccepting = null;
    _cellsWithRole = null;
    _currentAngle = angle;
    _structureInitAngle = angle + initAngleOffset;
    _facings = MultiblockFacings.FromAttributes(Block.Attributes);
    _roles = MultiblockCellRoles.FromAttributes(Block.Attributes);
    _connectors = MultiblockConnectors.FromAttributes(Block.Attributes);

    if (Api is ICoreClientAPI capi && _highlightedStructure != null) {
      _highlightedStructure.ClearHighlights(Api.World, capi.World.Player);
      _highlightedStructure = null;
    }
  }

  /// <summary>Converts a structure-local offset into a world position for the current rotation.</summary>
  protected virtual BlockPos GetGlobalPos(int localX, int localY, int localZ) =>
    ExOrientation.GlobalPos(Pos, localX, localY, localZ, _currentAngle);

  /// <summary>
  /// Ensures <see cref="_structure"/> and <see cref="_currentAngle"/> are populated. The monitor tick
  /// primes them server-side; a client-side read that needs the layout primes them lazily here, since
  /// the monitor tick never runs on the client. Idempotent: <see cref="SetStructureAngle"/> is guarded.
  /// </summary>
  protected void EnsureStructureLoaded() {
    if (_structure == null)
      UpdateStructureRotation();
  }

  /// <summary>
  /// Whether <paramref name="worldCell"/> is one of the cells this structure occupies at its placed
  /// rotation. A functional component (tap, hopper, tuyere) uses this to confirm the anchor it scanned up
  /// actually owns it, which disambiguates two adjacent structures whose scan boxes overlap. Reads the
  /// same <see cref="MultiblockStructure.TransformedOffsets"/> the build outline walks, and loads the
  /// layout lazily so it answers on the client too.
  /// </summary>
  public bool OwnsCell(BlockPos worldCell) {
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
  /// The world cells of this footprint whose layout slot would accept <paramref name="blockCode"/> at the
  /// placed facing, resolving a legend's facing through <see cref="MultiblockFacings"/> first. Matches by
  /// the same <see cref="WildcardUtil.Match"/> against the same wanted code as
  /// <see cref="IncompleteBlockCount"/>, so acceptance and completion cannot drift apart. Cached per code
  /// and dropped in <see cref="SetStructureAngle"/>, since a wrench turn moves these cells; the empty
  /// answer of a layout that has not loaded is never cached.
  /// </summary>
  /// <param name="blockCode">Concrete code of the block that would be placed, never a wildcard: vanilla's
  /// matcher takes the wildcard on the left. A code no cell admits answers empty.</param>
  public IReadOnlyList<BlockPos> CellsAccepting(AssetLocation blockCode) {
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
  /// The world cells this structure's layout marks with <paramref name="role"/>, rotation-correct for the
  /// placed facing; empty when the layout declares no roles. A role states what a cell is for rather than
  /// what may fill it. Cached per role and dropped in <see cref="SetStructureAngle"/>, since a wrench turn
  /// moves these cells.
  /// <para>
  /// Rotation is inherited, not recomputed: <c>InitForUse</c> rotates
  /// <see cref="MultiblockStructure.Offsets"/> into <see cref="MultiblockStructure.TransformedOffsets"/>
  /// in order, so an authored north-frame role offset is read back at the same index.
  /// </para>
  /// </summary>
  public IReadOnlyList<BlockPos> CellsWithRole(CellRole role) {
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
  /// The outward faces this layout demands a network connector on at <paramref name="worldCell"/>,
  /// rotation-correct for the placed facing; empty when that cell carries no connector mark or is not one
  /// of this structure's cells. What <c>IncompleteBlockCount</c> tests each connector cell against, so a
  /// report or a hint asks the same question completion does.
  /// </summary>
  public IReadOnlyList<BlockFacing> ConnectorFacesAt(BlockPos worldCell) {
    EnsureStructureLoaded();
    if (_connectors.IsEmpty || _structure?.TransformedOffsets is not { } turned)
      return _noFaces;

    List<BlockOffsetAndNumber> authored = _structure.Offsets;
    for (int i = 0; i < turned.Count && i < authored.Count; i++) {
      if (
        Pos.X + turned[i].X != worldCell.X
        || Pos.InternalY + turned[i].Y != worldCell.Y
        || Pos.Z + turned[i].Z != worldCell.Z
      )
        continue;

      return
      [
        .. _connectors
          .OutwardFacesAt((authored[i].X, authored[i].Y, authored[i].Z))
          .Select(ExOrientation.FacingFromSide)
          .Where(f => f != null)
          .Select(f => ExOrientation.RotateFacing(f!, _structureInitAngle)),
      ];
    }
    return _noFaces;
  }

  private static readonly BlockFacing[] _noFaces = [];

  /// <summary>
  /// The authored (north-frame) offsets this layout marks with <paramref name="role"/> - what
  /// <see cref="CellsWithRole"/> answers before rotation and before the anchor position is added; empty
  /// when the layout marks none. Suits a structure-local fact such as shaft height, which no facing moves.
  /// <para>
  /// Neither cached nor copied: the returned set is the <see cref="MultiblockCellRoles"/> table's own,
  /// read-only by its interface and replaced wholesale when the layout reloads, so a per-tick caller
  /// should cache the derived fact. Loads the layout lazily, so it answers on the client and from
  /// <c>FromTreeAttributes</c> - vanilla assigns <c>Block</c> before it on every load path.
  /// </para>
  /// </summary>
  public IReadOnlySet<(int X, int Y, int Z)> LocalCellsWithRole(CellRole role) {
    EnsureStructureLoaded();
    return _roles.CellsOf(role);
  }

  /// <summary>
  /// Scans a box around <paramref name="componentPos"/> for a <typeparamref name="T"/> anchor whose
  /// structure <see cref="OwnsCell">owns</see> that cell - the reverse lookup a functional component uses
  /// to find its multiblock, since the anchor pushes to its components by offset and nothing points back.
  /// The caller sizes the box to cover its tallest component; a slack box is harmless, as the ownership
  /// gate only turns up more candidates to reject. Returns null when no owning anchor is in range.
  /// </summary>
  /// <param name="horizontal">Box reach out from the component on each horizontal axis, in cells.</param>
  /// <param name="below">Box reach below the component, in cells.</param>
  /// <param name="above">Box reach above the component, in cells.</param>
  public static T? FindAnchorOwning<T>(
    IWorldAccessor world,
    BlockPos componentPos,
    int horizontal,
    int below,
    int above
  )
    where T : BlockEntityMultiblockStructure {
    for (int dy = -below; dy <= above; dy++)
      for (int dx = -horizontal; dx <= horizontal; dx++)
        for (int dz = -horizontal; dz <= horizontal; dz++) {
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
  /// Player interaction entry point (the projection toggle): re-checks completeness, fires the
  /// completed/lost callbacks, and client-side shows the build outline and missing count or clears it
  /// once complete. <see cref="FromTreeAttributes"/> also auto-clears the projection on completion.
  /// </summary>
  public virtual void Interact(IPlayer byPlayer) {
    UpdateStructureRotation();
    if (_structure == null)
      return;

    // Tally missing blocks by wanted code while counting, so one walk feeds both the projection and the
    // missing-materials report. A cell whose block is right but whose connector faces the wrong way is
    // tallied apart: telling the player to fetch another tuyere when one is already standing there is
    // worse than saying nothing.
    var missingByCode = new Dictionary<AssetLocation, int>();
    var misfacing = new List<MissingCell>();
    int missingCount = IncompleteBlockCount(cell => {
      if (cell.OutwardFace != null) {
        misfacing.Add(cell);
        return;
      }
      // Air-satisfied or auto-filled slots aren't player-gathered, so leave them out.
      if (IsAutoFilled(cell.Wanted))
        return;
      missingByCode.TryGetValue(cell.Wanted, out int count);
      missingByCode[cell.Wanted] = count + 1;
    });
    bool wasComplete = StructureComplete;
    StructureComplete = missingCount == 0;

    if (Api.Side == EnumAppSide.Server) {
      if (StructureComplete && !wasComplete) {
        OnStructureCompleted();
        StartStructureTick();
        MarkDirty(true);
      } else if (!StructureComplete && wasComplete) {
        OnStructureLost();
        // The same readiness question the monitor tick asks on this transition. A machine that keeps
        // running while broken must not be stopped by whichever of the two noticed first.
        if (ProductionReadiness.StopsProductionWhenNotReady(this))
          ProductionProcess.Stop(this);
        MarkDirty(true);
      }

      if (!StructureComplete && byPlayer is IServerPlayer serverPlayer)
        SendMissingBlocksReport(serverPlayer, missingByCode, misfacing);
    }

    if (Api is ICoreClientAPI clientApi) {
      if (missingCount > 0) {
        _highlightedStructure = _structure;
        clientApi.TriggerIngameError(
          this,
          "incomplete",
          GetIncompleteMessage(missingCount)
        );
        HighlightIncompleteSafe(_highlightedStructure, byPlayer);
      } else {
        clientApi.TriggerIngameError(this, "complete", GetCompleteMessage());
        _highlightedStructure?.ClearHighlights(Api.World, byPlayer);
        _highlightedStructure = null;
      }
    }
  }

  /// <summary>
  /// Number of structure cells not yet satisfied, replacing vanilla
  /// <see cref="MultiblockStructure.InCompleteBlockCount"/>. Walks the same <c>TransformedOffsets</c> and
  /// matches the same way, with one addition: a cell whose legend carries a facing has that facing
  /// rotated with the structure first (<see cref="MultiblockFacings"/>), so a layout can demand a
  /// correctly-oriented slab or door.
  /// </summary>
  /// <param name="onMissing">Called per unsatisfied cell, the wanted code already rotated, so a report
  /// names the variant the player must place - and, for a cell whose code matches but whose connector
  /// faces the wrong way, the outward face it wants.</param>
  /// <returns>The count of unsatisfied cells, or 0 when the structure is not loaded.</returns>
  protected int IncompleteBlockCount(Action<MissingCell>? onMissing = null) {
    if (_structure?.TransformedOffsets is not { } transformed)
      return 0;

    List<BlockOffsetAndNumber> authored = _structure.Offsets;
    int missing = 0;

    // Indexed rather than walked, because the connector demand is authored in the north frame and is
    // read back at the same index InitForUse rotated it from - the alignment CellsWithRole relies on.
    for (int i = 0; i < transformed.Count; i++) {
      BlockOffsetAndNumber offset = transformed[i];
      if (WantedCodeAt(offset) is not AssetLocation wanted)
        continue;

      var at = new BlockPos(
        Pos.X + offset.X,
        Pos.InternalY + offset.Y,
        Pos.Z + offset.Z,
        Pos.dimension
      );
      Block actual = Api.World.BlockAccessor.GetBlockRaw(at.X, at.Y, at.Z);

      if (!WildcardUtil.Match(wanted, actual.Code)) {
        missing++;
        onMissing?.Invoke(new MissingCell(actual, wanted, at, null));
        continue;
      }

      if (
        i < authored.Count
        && UnopenedFace(authored[i], at, actual) is string face
      ) {
        missing++;
        onMissing?.Invoke(new MissingCell(actual, wanted, at, face));
      }
    }
    return missing;
  }

  /// <summary>
  /// The first outward face a connector cell demands that its occupant does not answer, or null when
  /// the cell demands none or answers them all. The demand is authored in the north frame, so it turns
  /// by the structure's own init angle - the same angle the wanted code was rotated by.
  /// </summary>
  /// <remarks>Satisfied by a superset, so a passthrough wearing <c>ns</c> answers a demand for north
  /// and a legitimate re-pick by the network does not break a standing structure. An occupant that is
  /// no <see cref="INetworkConnector"/> at all answers nothing, so a plain brick dropped into a
  /// connector cell cannot satisfy the mark.</remarks>
  private string? UnopenedFace(
    BlockOffsetAndNumber authored,
    BlockPos at,
    Block actual
  ) {
    if (_connectors.IsEmpty)
      return null;

    foreach (
      string letter in _connectors.OutwardFacesAt(
        (authored.X, authored.Y, authored.Z)
      )
    ) {
      if (ExOrientation.FacingFromSide(letter) is not BlockFacing authoredFace)
        continue;

      BlockFacing face = ExOrientation.RotateFacing(
        authoredFace,
        _structureInitAngle
      );
      if (
        actual is INetworkMember member
        && member.HasConnectorAt(Api.World.BlockAccessor, at, face)
      )
        continue;

      return ExOrientation.TokenOf(face, asLetter: true);
    }
    return null;
  }

  /// <summary>
  /// The rotation-resolved code a transformed offset requires, or null when its block number has no
  /// <c>blockNumbers</c> entry. Vanilla keeps the number-to-code map private, so it is rebuilt from the
  /// public <c>BlockNumbers</c> and cached: both the completion walk and the highlight need it.
  /// </summary>
  private AssetLocation? WantedCodeAt(BlockOffsetAndNumber offset) {
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
  ) {
    var map = new Dictionary<int, AssetLocation>();
    foreach (var kv in structure.BlockNumbers)
      map[kv.Value] = kv.Key;
    return map;
  }

  /// <summary>
  /// Crash-safe replacement for vanilla <see cref="MultiblockStructure.HighlightIncompleteParts"/>, which
  /// tints each empty slot with <c>SearchBlocks(wantedCode)[0]</c> and throws
  /// <see cref="System.IndexOutOfRangeException"/> when a wildcard code resolves to no block. Mirrors the
  /// vanilla logic but falls back to a neutral tint for unresolvable slots.
  /// </summary>
  private void HighlightIncompleteSafe(
    MultiblockStructure structure,
    IPlayer player
  ) {
    var offsets = structure.TransformedOffsets;
    if (offsets == null)
      return;

    var positions = new List<BlockPos>();
    var colors = new List<int>();

    foreach (var offset in offsets) {
      // Same rotation-resolved code the completion walk uses, so the tint and the count cannot disagree
      // about which cells are wrong. An oriented slot resolves to the exact variant, so SearchBlocks
      // below colours from the right-facing block.
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

      if (actual.Id != 0) {
        // A wrong solid block occupies the slot - vanilla tints these red.
        colors.Add(ColorUtil.ColorFromRgba(215, 94, 94, 0x60));
        continue;
      }

      // Empty slot: tint with the wanted block's color when it resolves, otherwise
      // fall back to a neutral blue instead of crashing on an empty SearchBlocks.
      Block[] matches = Api.World.SearchBlocks(wanted);
      if (matches.Length == 0) {
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
  /// Sends the player a chat breakdown of every block still missing and how many of each, resolving
  /// (possibly wildcard) codes to readable block names.
  /// </summary>
  private void SendMissingBlocksReport(
    IServerPlayer player,
    Dictionary<AssetLocation, int> missingByCode,
    IReadOnlyList<MissingCell> misfacing
  ) {
    if (missingByCode.Count == 0 && misfacing.Count == 0)
      return;

    // The strings come from exlib's own asset domain, so the shared report does not borrow a consumer
    // mod's lang file. The generated ExlibLang accessors make a renamed or deleted key a compile error.
    var sb = new StringBuilder();
    sb.Append(
      Lang.Get(
        missingByCode.Count > 0
          ? ExlibLang.StructureMissingHeader
          : ExlibLang.StructureMisfacingHeader
      )
    );

    foreach (
      var entry in missingByCode
        .OrderByDescending(e => e.Value)
        .ThenBy(e => ResolveBlockName(e.Key))
    ) {
      sb.Append('\n');
      sb.Append(
        Lang.Get(
          ExlibLang.StructureMissingLine,
          entry.Value,
          ResolveBlockName(entry.Key)
        )
      );
    }

    // One line per misfaced cell, naming the position, because two tuyeres in one wall are the same
    // block and the same face and the player needs to know which one to turn.
    foreach (MissingCell cell in misfacing) {
      sb.Append('\n');
      sb.Append(
        Lang.Get(
          ExlibLang.StructureMisfacingLine,
          ResolveBlockName(cell.Wanted),
          $"{cell.At.X}, {cell.At.Y}, {cell.At.Z}",
          FaceName(cell.OutwardFace)
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
  /// The player-facing name of an outward face letter. Switched onto literal keys rather than composed
  /// from the letter, so a deleted or renamed one is a compile error - a composed key names a family and
  /// no guard in the repo checks that it resolves.
  /// </summary>
  private static string FaceName(string? letter) =>
    Lang.Get(
      letter switch {
        "n" => ExlibLang.FacingN,
        "e" => ExlibLang.FacingE,
        "s" => ExlibLang.FacingS,
        "w" => ExlibLang.FacingW,
        "u" => ExlibLang.FacingU,
        _ => ExlibLang.FacingD,
      }
    );

  /// <summary>
  /// Resolves a structure block code, which may be a wildcard such as "iiex:furnace-blastcore-*", to a
  /// human-readable display name.
  /// </summary>
  private string ResolveBlockName(AssetLocation wantBlockCode) {
    Block? block = Api.World.GetBlock(wantBlockCode);
    if (block == null) {
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

  public override void OnBlockRemoved() {
    base.OnBlockRemoved();
    StopStructureTick();
    if (Api is ICoreClientAPI capi)
      _highlightedStructure?.ClearHighlights(Api.World, capi.World.Player);
  }

  /// <summary>
  /// Chunk unload. The structure stays placed, so only this instance's listeners and its client-side
  /// build outline go: the outline lives in a per-player highlight slot owned by the world, not by this
  /// block entity, so an unload that skips it paints a projection over ground that no longer resolves.
  /// Vanilla's <c>BEBeeHiveKiln</c> clears its highlight on both paths for the same reason.
  /// </summary>
  public override void OnBlockUnloaded() {
    base.OnBlockUnloaded();
    StopStructureTick();
    if (Api is ICoreClientAPI capi)
      _highlightedStructure?.ClearHighlights(Api.World, capi.World.Player);
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetBool("structureComplete", StructureComplete);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    bool wasComplete = StructureComplete;
    StructureComplete = tree.GetBool("structureComplete");

    // Auto-hide the build projection the moment the structure finishes.
    if (
      !wasComplete
      && StructureComplete
      && Api is ICoreClientAPI capi
      && _highlightedStructure != null
    ) {
      _highlightedStructure.ClearHighlights(Api.World, capi.World.Player);
      _highlightedStructure = null;
    }
  }
}
