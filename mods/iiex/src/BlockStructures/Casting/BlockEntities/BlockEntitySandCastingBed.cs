using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpandedLib;
using ExpandedLib.Blocks;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Helpers;
using ExpandedLib.Industry.Molten;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using ExpandedLib.Structures;
using IronIndustryExpanded.BlockNetworkMolten;
using IronIndustryExpanded.BlockStructures.Casting.Blocks;
using IronIndustryExpanded.Items;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockStructures.Casting.BlockEntities;

/// <summary>
/// Sand casting bed principal: a 3x1x4 megablock that casts molten metal delivered by the molten canal
/// network. It hosts its own pour-basin <see cref="BEBehaviorMoltenCell"/> and drives an isolated internal
/// flow over the footprint's filler-hosted cells (basin, four central runners, eight side molds), so the
/// external canal may carry a different metal. A pull-port drains the adjacent external cell each tick;
/// metal spreads by level-equalisation and cools where none arrives, and a hardened cell is right-clicked
/// to collect its castings, partial fills and runner residue coming back as chunks and bits. A fully carved
/// bed holds 20 impressions, and one mold shape casts both products - pigs from iron, bricks from slag.
/// See docs/design/machines/casting-bed.md.
/// </summary>
[BlockEntityRegister]
public class BlockEntitySandCastingBed : ExBlockEntity {
  // Units drained from an adjacent external molten cell per server tick (1 s).
  private const int PullRatePerTick = 25;

  private long _serverTick;
  private long _clientTick;

  // Renders the built brick+sand construction elements (the RCC behaviour suppresses the default mesh).
  private ConstructedAnimator? _animator;

  // What is carved into each of the twelve slots, indexed as SandBedLayout.Slots. Sand covers both
  // uncarved and shaken-out.
  private readonly BedSlotState[] _slots = new BedSlotState[
    SandBedLayout.Slots.Length
  ];

  private BEBehaviorMoltenCell? Basin => GetBehavior<BEBehaviorMoltenCell>();

  #region Lifecycle

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    // Re-size each carved cavity from the persisted slot states; without this a reloaded bed falls back to
    // the declared capacity mid-cast.
    foreach (BedSlot slot in SandBedLayout.Slots)
      if (StateOf(slot) != BedSlotState.Sand)
        ApplyCapacity(slot);

    // Constructed on both sides even though it only builds meshes on the client: TryCarveAt reads
    // _animator.IsConstructed on the server, so a client-only animator would leave the bed uncarvable. The
    // bed is static, so the animator only re-tesselates the built elements.
    _animator = new ConstructedAnimator(
      this,
      () => "sandcastingbed-" + (Block.Variant["side"] ?? "north")
    );
    // The repose hook runs after every rebuild, construction stages included, so the carved surface is
    // re-applied where the next stage cannot clobber it (see RebuildSurface).
    _animator.Initialize(RebuildSurface);

    if (api.Side == EnumAppSide.Server)
      _serverTick = RegisterGameTickListener(OnServerTick, 1000);
    else {
      BuildSurfaces((ICoreClientAPI)api);
      _clientTick = RegisterGameTickListener(_ => UpdateSurfaces(), 1000);
    }
  }

  public override void OnBlockRemoved() {
    _animator?.Dispose();
    DisposeSurfaces();
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded() {
    _animator?.Dispose();
    DisposeSurfaces();
    base.OnBlockUnloaded();
  }

  #endregion

  #region Carved surface (which shape elements each slot draws)

  /// <summary>What is carved into <paramref name="slot"/> right now; plain sand for an unknown slot.</summary>
  public BedSlotState StateOf(BedSlot slot) {
    int i = SandBedLayout.IndexOf(slot);
    return i < 0 ? BedSlotState.Sand : _slots[i];
  }

  /// <summary>Which slot a world cell of this bed is, or null when it is not a carvable cell.</summary>
  public BedSlot? SlotAt(BlockPos cellPos) =>
    Block is BlockSandCastingBed bed
    && bed.SlotCells(Pos).TryGetValue(cellPos, out BedSlot slot)
      ? slot
      : null;

  /// <summary>
  /// Whether metal may pass through the cell at <paramref name="cellPos"/>. Sand, whether uncarved or
  /// broken out, is not a channel, and a cell that is not a slot at all (the brick shoulders) never carries
  /// metal.
  /// </summary>
  public bool IsCarved(BlockPos cellPos) =>
    SlotAt(cellPos) is { } slot && StateOf(slot) != BedSlotState.Sand;

  /// <summary>
  /// Carves <paramref name="slot"/> to <paramref name="state"/>, re-rendering when it changed. Refuses a
  /// state the slot cannot hold (a runner in a mold flank).
  /// </summary>
  /// <returns>True when the state changed. False covers both "already that state" and "not allowed";
  /// <see cref="BedSlot.Accepts"/> distinguishes them.</returns>
  public bool Carve(BedSlot slot, BedSlotState state) {
    int i = SandBedLayout.IndexOf(slot);
    if (i < 0 || !slot.Accepts(state) || _slots[i] == state)
      return false;

    _slots[i] = state;
    ApplyCapacity(slot);
    MarkDirty(true);
    if (Api?.Side == EnumAppSide.Client)
      RebuildSurface();
    return true;
  }

  /// <summary>
  /// Sizes a slot's molten cell to the cavity now cut into it: a middle row takes three impressions where
  /// an end row takes two. Called on load as well as on carve, since otherwise a reloaded bed falls back to
  /// the declared capacity and re-sizes a mid-cast mold.
  /// </summary>
  private void ApplyCapacity(BedSlot slot) {
    if (Api == null || Block is not BlockSandCastingBed bed)
      return;

    foreach ((BlockPos cellPos, BedSlot at) in bed.SlotCells(Pos)) {
      if (!at.Equals(slot))
        continue;

      BEBehaviorMoltenCell? cell = Api
        .World.BlockAccessor.GetBlockEntity(cellPos)
        ?.GetBehavior<BEBehaviorMoltenCell>();
      if (cell == null)
        return;

      int capacity = SandBedLayout.CapacityOf(slot, StateOf(slot));
      if (capacity > 0)
        cell.SetCapacity(capacity);
      else
        cell.ClearCapacity();
      return;
    }
  }

  /// <summary>
  /// Re-renders the bed for the current slot states, client side only. Composes over the construction
  /// behaviour's element list rather than replacing it, because that behaviour rebuilds the mesh from its
  /// own list on every stage; it runs from the animator's repose hook, which fires after every rebuild.
  /// </summary>
  private void RebuildSurface() {
    if (_animator is not { } animator || Api?.Side != EnumAppSide.Client)
      return;
    animator.Rebuild(
      SandBedLayout.Compose(
        animator.Rcc?.shape?.SelectiveElements,
        _slots,
        animator.IsConstructed
      )
    );
  }

  #endregion

  #region Cell gathering (basin + footprint fillers)

  // Every molten cell of this bed by world position: the principal's basin plus each footprint filler that
  // hosts one (the plain brick edge fillers host none). Rebuilt each tick so a reloaded filler is picked up.
  private Dictionary<BlockPos, IMoltenCell> Cells() {
    var cells = new Dictionary<BlockPos, IMoltenCell>();
    if (Basin is { } basin)
      cells[Pos] = basin;

    if (Block is BlockSandCastingBed bed)
      foreach (BlockPos cellPos in bed.FootprintPositions(Pos))
        if (
          Api
            .World.BlockAccessor.GetBlockEntity(cellPos)
            ?.GetBehavior<BEBehaviorMoltenCell>() is { } cell
        )
          cells[cellPos] = cell;

    return cells;
  }

  // Manhattan distance (on the flat bed) from the basin, so flow is processed basin-outward.
  private int DistFromBasin(BlockPos p) =>
    Math.Abs(p.X - Pos.X) + Math.Abs(p.Z - Pos.Z);

  #endregion

  #region Server tick: pull, flow, cool

  private void OnServerTick(float dt) {
    IWorldAccessor world = Api.World;
    PullFromNeighbours(world);

    var cells = Cells();
    if (cells.Count == 0)
      return;
    foreach (IMoltenCell c in cells.Values)
      c.EnsureMetalStack(world);

    // Drive each internal edge once, from the cell nearer the basin to the farther one, so a charge
    // propagates outward a wavefront at a time. An edge exists only where both ends are carved. The gate
    // is on edges rather than on the cell list because a stranded charge must still cool.
    foreach (BlockPos p in cells.Keys.OrderBy(DistFromBasin).ToList()) {
      if (!IsCarved(p))
        continue;
      IMoltenCell a = cells[p];
      foreach (BlockFacing face in BlockFacing.HORIZONTALS) {
        BlockPos np = p.AddCopy(face);
        if (
          cells.TryGetValue(np, out IMoltenCell? b)
          && DistFromBasin(np) > DistFromBasin(p)
          && IsCarved(np)
        )
          FlowEdge(a, b, world);
      }
    }

    foreach (IMoltenCell c in cells.Values)
      c.UpdateThermal(world);
  }

  // Intake: drains any adjacent external molten cell (a canal end) into the basin. The bed's own footprint
  // fillers host their cell on a behaviour, so the `GetBlockEntity is IMoltenCell` test matches only canal
  // nodes and the internal flow stays isolated.
  private void PullFromNeighbours(IWorldAccessor world) {
    if (Basin is not { } basin)
      return;

    foreach (
      var (_, src) in world.BlockAccessor.Neighbours<IMoltenCell>(
        Pos,
        BlockFacing.HORIZONTALS
      )
    ) {
      if (src.Solidified || src.Sealed || src.CellAmount <= 0)
        continue;

      int want = Math.Min(PullRatePerTick, src.CellAmount);
      int accepted = basin.PushMetalRaw(
        want,
        src.CellMetalType,
        src.CellTemperature,
        world
      );
      if (accepted > 0)
        src.DrainMetal(accepted);
    }
  }

  // Moves metal toward equal fill across one internal edge (mirrors MoltenNetwork.FlowEdge). A drain
  // fitting (a mold) never gives metal back; it holds its charge until it hardens.
  public static void FlowEdge(
    IMoltenCell x,
    IMoltenCell y,
    IWorldAccessor world
  ) {
    if (x.Solidified || x.Sealed || y.Solidified || y.Sealed)
      return;

    int diff = Math.Abs(x.CellAmount - y.CellAmount);
    if (diff == 0)
      return;

    IMoltenCell giver = x.CellAmount > y.CellAmount ? x : y;
    IMoltenCell receiver = giver == x ? y : x;
    if (giver.AcceptsSubMinimumFlow || giver.CellAmount <= 0)
      return; // molds don't drain back out
    if (
      receiver.CellAmount > 0
      && receiver.CellMetalType != giver.CellMetalType
    )
      return;

    int transfer = Math.Min(diff, ExlibValues.MoltenFlowRate);
    if (
      transfer < ExlibValues.MoltenMinFlowAmount
      && !receiver.AcceptsSubMinimumFlow
    )
      return;

    int accepted = receiver.PushMetalRaw(
      transfer,
      giver.CellMetalType,
      giver.CellTemperature,
      world
    );
    if (accepted > 0)
      giver.DrainMetal(accepted);
  }

  #endregion

  #region Harvest (right-click a hardened mold/runner)

  /// <summary>
  /// Routes a right-click on one of the bed's cells: a cell holding metal is a harvest, an empty one a
  /// carve. Returns true when the click was consumed, false to fall through to the construction
  /// interaction, which is what an unbuilt bed needs.
  /// </summary>
  public bool OnCellInteract(BlockPos cellPos, IPlayer byPlayer) =>
    TryHarvest(cellPos, byPlayer) || TryCarveAt(cellPos, byPlayer);

  /// <summary>
  /// Cuts the shape the cell can hold: a runner channel down the spine, a row of impressions in the flanks.
  /// Only on a finished bed and only into plain sand. There is no choice of mold - one mold casts both pigs
  /// and slag bricks - so the carve follows entirely from which cell was clicked.
  /// </summary>
  public bool TryCarveAt(BlockPos cellPos, IPlayer byPlayer) {
    if (_animator?.IsConstructed != true || SlotAt(cellPos) is not { } slot)
      return false;

    BedSlotState wanted = slot.IsMold ? BedSlotState.Mold : BedSlotState.Runner;

    if (Api.Side == EnumAppSide.Client)
      return StateOf(slot) == BedSlotState.Sand;

    if (StateOf(slot) != BedSlotState.Sand) {
      (byPlayer as IServerPlayer)?.SendIngameError(
        "iiex-castingbed-alreadycarved"
      );
      return true;
    }

    if (!Carve(slot, wanted))
      return false;
    ExSounds.Play(Api, cellPos, ExSounds.StoneCrush, 0.5f);
    return true;
  }

  /// <summary>
  /// Collects the hardened casting from the cell at <paramref name="cellPos"/>: full 150-unit pigs first,
  /// then chunks and bits from the remainder, conserving the mass. Returns true when the click was consumed
  /// (a cell holding metal), false to fall through to construction. The server hands over the items; the
  /// client only reports that it handled the click.
  /// </summary>
  public bool TryHarvest(BlockPos cellPos, IPlayer byPlayer) {
    if (
      Api
        .World.BlockAccessor.GetBlockEntity(cellPos)
        ?.GetBehavior<BEBehaviorMoltenCell>()
        is not { } cell
      || cell.CellAmount <= 0
    )
      return false;

    if (Api.Side == EnumAppSide.Client)
      return true;

    if (!cell.IsHardened) {
      (byPlayer as IServerPlayer)?.SendIngameError("iiex-castingbed-toohot");
      return true;
    }

    BedSlotState mold = SlotAt(cellPos) is { } s
      ? StateOf(s)
      : BedSlotState.Sand;
    foreach (
      ItemStack stack in BuildHarvest(
        cell.CellAmount,
        mold,
        cell.CellMetalType,
        cell.CellTemperature
      )
    )
      if (byPlayer.InventoryManager?.TryGiveItemstack(stack) != true)
        Api.World.SpawnItemEntity(stack, cellPos.ToVec3d().Add(0.5, 0.6, 0.5));

    cell.ClearContents();
    // Shaking the casting out breaks the impression open: the slot drops to plain sand and stops carrying
    // metal until it is carved again.
    if (SlotAt(cellPos) is { } slot)
      Carve(slot, BedSlotState.Sand);
    ExSounds.Play(Api, cellPos, ExSounds.StoneCrush, 0.6f);
    return true;
  }

  /// <summary>
  /// Greedy denomination of hardened units into pigs (150), chunks (25) and bits (5), conserving the mass:
  /// <c>pigs*150 + chunks*25 + bits*5</c> never exceeds <paramref name="units"/>, and the only loss is a
  /// sub-bit remainder (&lt; 5 u).
  /// </summary>
  public static (int Pigs, int Chunks, int Bits) Denominate(int units) {
    int pigs = units / ItemPig.PigUnits;
    int rem = units % ItemPig.PigUnits;
    int chunks = rem / ItemPig.ChunkUnits;
    rem %= ItemPig.ChunkUnits;
    int bits = rem / ItemPig.BitUnits;
    return (pigs, chunks, bits);
  }

  /// <summary>
  /// Whether a cell in <paramref name="state"/> yields a casting rather than recovered scrap. Only a carved
  /// mold does; a runner is a conduit, not a cavity, so whatever cooled in it comes back as recovered bits
  /// regardless of the metal.
  /// </summary>
  public static bool YieldsCasting(BedSlotState state) =>
    state == BedSlotState.Mold;

  // What a cell yields on shake-out: a mold gives the casting its metal makes - pigs from iron, bricks from
  // slag - and a runner's charge comes back as recovered bits. Nothing is destroyed, so a misrouted pour
  // costs a remelt rather than the metal.
  private List<ItemStack> BuildHarvest(
    int units,
    BedSlotState mold,
    string metalCode,
    float temperature
  ) {
    var stacks = new List<ItemStack>();
    void Add(string code, int count) {
      if (count <= 0)
        return;
      Item? item = Api.World.GetItem(new AssetLocation(code));
      if (item != null)
        stacks.Add(new ItemStack(item, count));
    }

    if (!YieldsCasting(mold)) {
      if (
        MoltenChisel.BuildRecovery(
          Api.World,
          new AssetLocation(metalCode),
          temperature,
          units,
          slagFallback: true
        ) is { } recovered
      )
        stacks.Add(recovered);
      return stacks;
    }

    if (metalCode == SlagItemDefinitions.MoltenCode) {
      // A brick occupies exactly one pig's impression, so the same cavity denominates either metal with no
      // stranded remainder.
      Add("iiex:slagbrick", units / SlagItemDefinitions.SlagBrickUnits);
      return stacks;
    }

    (int pigs, int chunks, int bits) = Denominate(units);
    Add("iiex:pig", pigs);
    Add("iiex:pigchunk", chunks);
    Add("iiex:pigbit", bits);
    return stacks;
  }

  #endregion

  #region Client surfaces (one molten renderer per cell + the basin's pool and tap)

  // A rendered molten surface: its renderer and a lookup for the cell whose fill/temperature it shows.
  private sealed record Surface(
    MoltenRenderer Renderer,
    Func<IMoltenCell?> Cell
  );

  private readonly List<Surface> _surfaces = [];

  private void BuildSurfaces(ICoreClientAPI capi) {
    // The surface cuboids are cut in the shape's own frame, so they turn with the drawn mesh - the same Y
    // rotation the tesselator bakes into it - and not with the footprint, which carries a further 180.
    float rotY = (Block.Shape?.rotateY ?? 0f) * GameMath.DEG2RAD;

    // The principal's two surfaces: the shallow pour pool, whose height tracks the basin fill, and the
    // spout on the tower, which lights up while the bed holds metal.
    AddSurface(
      capi,
      Pos,
      new Cuboidf(0, 12, 12, 16, 14, 14),
      rotY,
      () => Basin
    );
    AddSurface(
      capi,
      Pos,
      new Cuboidf(6, 14, 10, 10, 16, 16),
      rotY,
      () => Basin
    );

    if (Block is not BlockSandCastingBed bed)
      return;

    // One surface per footprint molten cell. Runners draw a central channel, molds a broad pool; the brick
    // edge fillers host no cell and add none. Which of the two a cell takes comes from its slot rather
    // than from its position, whose axes swap on a bed laid east or west.
    IReadOnlyDictionary<BlockPos, BedSlot> slots = bed.SlotCells(Pos);
    foreach (BlockPos cellPos in bed.FootprintPositions(Pos)) {
      if (
        capi
          .World.BlockAccessor.GetBlockEntity(cellPos)
          ?.GetBehavior<BEBehaviorMoltenCell>()
        is null
      )
        continue;
      bool mold = slots.TryGetValue(cellPos, out BedSlot slot) && slot.IsMold;
      Cuboidf box = mold
        ? new Cuboidf(2, 1, 3, 14, 4, 13)
        : new Cuboidf(5, 1, 0, 11, 3, 16);
      BlockPos captured = cellPos.Copy();
      AddSurface(
        capi,
        captured,
        box,
        rotY,
        () =>
          capi
            .World.BlockAccessor.GetBlockEntity(captured)
            ?.GetBehavior<BEBehaviorMoltenCell>()
      );
    }

    UpdateSurfaces();
  }

  private void AddSurface(
    ICoreClientAPI capi,
    BlockPos pos,
    Cuboidf box,
    float rotY,
    Func<IMoltenCell?> cell
  ) {
    var renderer = new MoltenRenderer(
      pos,
      capi,
      [box],
      rotY,
      box.Y1 / 16f,
      box.Y2 - box.Y1
    );
    capi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque);
    _surfaces.Add(new Surface(renderer, cell));
  }

  private void UpdateSurfaces() {
    foreach (Surface s in _surfaces) {
      IMoltenCell? cell = s.Cell();
      if (
        cell == null
        || cell.CellAmount <= 0
        || cell.CellMetalType.Length == 0
      ) {
        s.Renderer.FillRatio = 0f;
        s.Renderer.MetalStack = null;
        continue;
      }

      s.Renderer.FillRatio = GameMath.Clamp(
        cell.CellAmount / (float)Math.Max(1, cell.MaxUnitCapacity),
        0f,
        1f
      );
      s.Renderer.Temperature = cell.CellTemperature;
      Item? item = Api.World.GetItem(new AssetLocation(cell.CellMetalType));
      s.Renderer.MetalStack = item != null ? new ItemStack(item) : null;
    }
  }

  private void DisposeSurfaces() {
    foreach (Surface s in _surfaces)
      s.Renderer.Dispose();
    _surfaces.Clear();
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);

    if (Basin is not { } basin)
      return;

    if (basin.CellAmount > 0)
      dsc.AppendLine(
        Lang.Get(
          "iiex:castingbed-basin",
          basin.CellAmount,
          ExMeasure.Temperature(basin.CellTemperature)
        )
      );
    else
      dsc.AppendLine(Lang.Get("iiex:castingbed-empty"));

    int ready = 0;
    int cooling = 0;
    if (Block is BlockSandCastingBed bed) {
      IReadOnlyDictionary<BlockPos, BedSlot> slots = bed.SlotCells(Pos);
      foreach (BlockPos cellPos in bed.FootprintPositions(Pos)) {
        if (
          !slots.TryGetValue(cellPos, out BedSlot slot)
          || !slot.IsMold // count molds only (the two flanks)
          || Api
            .World.BlockAccessor.GetBlockEntity(cellPos)
            ?.GetBehavior<BEBehaviorMoltenCell>()
            is not { CellAmount: > 0 } cell
        )
          continue;
        if (cell.IsHardened)
          ready += cell.CellAmount / ItemPig.PigUnits;
        else
          cooling++;
      }
    }

    if (ready > 0)
      dsc.AppendLine(Lang.Get("iiex:castingbed-pigs-ready", ready));
    if (cooling > 0)
      dsc.AppendLine(Lang.Get("iiex:castingbed-cooling", cooling));
  }

  #endregion

  #region Serialization

  // The carved surface, one byte per slot, positional against SandBedLayout.Slots. That order is part of
  // the save format.
  private const string SlotsKey = "bed_slots";

  protected override void DeclareState(ExBlockState state) =>
    state.Tree(
      SlotsKey,
      tree => {
        var packed = new byte[_slots.Length];
        for (int i = 0; i < _slots.Length; i++)
          packed[i] = (byte)_slots[i];
        tree.SetBytes(SlotsKey, packed);
      },
      (tree, world) => {
        byte[]? packed = tree.GetBytes(SlotsKey);
        for (int i = 0; i < _slots.Length; i++) {
          // A missing or truncated array reads as uncarved sand, as does a state the slot cannot hold, so
          // an unknown persisted value renders a flat slot rather than nothing.
          BedSlotState slotState =
            packed != null && i < packed.Length
              ? (BedSlotState)packed[i]
              : BedSlotState.Sand;
          _slots[i] = SandBedLayout.Slots[i].Accepts(slotState)
            ? slotState
            : BedSlotState.Sand;
        }
      }
    );

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor world
  ) {
    base.FromTreeAttributes(tree, world);
    if (Api?.Side == EnumAppSide.Client)
      RebuildSurface();
  }

  #endregion
}
