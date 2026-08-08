using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpandedLib;
using ExpandedLib.Blocks.Construction;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkMolten;
using IronworkingExpanded.BlockStructures.Casting.Blocks;
using IronworkingExpanded.Items;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockStructures.Casting.BlockEntities;

/// <summary>
/// The sand casting bed principal: a 3×1×4 mega-block that receives molten pig iron poured into the
/// molten canal network and casts it into solid pigs. It hosts its own pour-basin
/// <see cref="BEBehaviorMoltenCell"/> and drives an ISOLATED internal flow across the footprint's
/// filler-hosted cells (basin → the four central runners → the eight side molds) itself, so the outside
/// canal line can carry a different metal. Metal enters through a pull-port that drains the adjacent
/// external molten cell each tick; it propagates by level-equalisation, cools where no fresh metal
/// arrives, and - once a mold hardens - the player right-clicks it to collect its castings (partial fills
/// and runner residue come out as chunks/bits, conserving the mass).
/// <para>
/// A fully carved bed holds <b>20</b> impressions: the two end rows take two a side, the two middle rows
/// three. One mold shape casts both products - iron makes pigs, slag makes bricks - so nothing about the
/// carve has to anticipate what will eventually be poured into it.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntitySandCastingBed : BlockEntity
{
  // A mold cell casts its row's impressions at 150u each; a runner is a thin pass-through whose residue
  // is bits.
  private const int PullRatePerTick = 25;

  private long _serverTick;
  private long _clientTick;

  // Renders the built brick+sand construction elements (the RCC behaviour suppresses the default mesh).
  private ConstructedAnimator? _animator;

  // What the player has carved into each of the twelve slots, indexed as SandBedLayout.Slots. All sand until
  // the bed is built and carved, which is also exactly how a shaken-out slot reads.
  private readonly BedSlotState[] _slots = new BedSlotState[SandBedLayout.Slots.Length];

  private BEBehaviorMoltenCell? Basin => GetBehavior<BEBehaviorMoltenCell>();

  #region Lifecycle

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    // Re-size each carved cavity from the persisted slot states, so a reloaded bed keeps the mold the player
    // actually cut rather than reverting to the declared capacity mid-cast.
    foreach (BedSlot slot in SandBedLayout.Slots)
      if (StateOf(slot) != BedSlotState.Sand)
        ApplyCapacity(slot);

    // The animator (and IsConstructed) is resolved on BOTH sides; it only builds/poses on the client - the
    // same arrangement every other constructed machine in the suite uses.
    //
    // Caution: do not move this into the client-only `else` below. With `_animator` null on the server,
    // `TryCarveAt`'s `_animator?.IsConstructed != true` guard always fails there, the bed can never be
    // carved, the blast furnace has nowhere to pour, and the iron tier has no product at all. The bed is
    // static (no pose), so the animator only re-tesselates the built elements.
    _animator = new ConstructedAnimator(
      this,
      () => "sandcastingbed-" + (Block.Variant["side"] ?? "north")
    );
    // The repose hook runs after every rebuild, construction stages included - which is the only place
    // the carved surface can be re-applied without the next stage clobbering it (see RebuildSurface).
    _animator.Initialize(RebuildSurface);

    if (api.Side == EnumAppSide.Server)
      _serverTick = RegisterGameTickListener(OnServerTick, 1000);
    else
    {
      BuildSurfaces((ICoreClientAPI)api);
      _clientTick = RegisterGameTickListener(_ => UpdateSurfaces(), 1000);
    }
  }

  public override void OnBlockRemoved()
  {
    _animator?.Dispose();
    DisposeSurfaces();
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded()
  {
    _animator?.Dispose();
    DisposeSurfaces();
    base.OnBlockUnloaded();
  }

  #endregion

  #region Carved surface (which shape elements each slot draws)

  /// <summary>What is carved into <paramref name="slot"/> right now; plain sand for an unknown slot.</summary>
  public BedSlotState StateOf(BedSlot slot)
  {
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
  /// Whether metal may pass through the cell at <paramref name="cellPos"/>. Uncarved and broken-out sand is
  /// not a channel: this is what makes carving the actual gameplay, since the player decides where the heat
  /// can run by where they cut. A cell that is not a slot at all (the brick shoulders) never carries metal.
  /// </summary>
  public bool IsCarved(BlockPos cellPos) =>
    SlotAt(cellPos) is { } slot && StateOf(slot) != BedSlotState.Sand;

  /// <summary>
  /// Carves <paramref name="slot"/> to <paramref name="state"/>, re-rendering if it actually changed.
  /// Refuses a state the slot cannot hold (a runner in a mold flank), and returns whether anything moved -
  /// so a caller can tell "already like that" from "not allowed" by asking <see cref="BedSlot.Accepts"/>.
  /// </summary>
  public bool Carve(BedSlot slot, BedSlotState state)
  {
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
  /// Sizes a slot's molten cell to the cavity that is now cut into it - a middle row takes three impressions
  /// where an end row takes two, so where the player carved decides how much a pour sinks. Re-applied on load
  /// as well as on carve, or a reloaded bed would fall back to the declared capacity and quietly re-size a
  /// mid-cast mold.
  /// </summary>
  private void ApplyCapacity(BedSlot slot)
  {
    if (Api == null || Block is not BlockSandCastingBed bed)
      return;

    foreach ((BlockPos cellPos, BedSlot at) in bed.SlotCells(Pos))
    {
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
  /// Re-renders the bed for the current slot states. Composed over the construction behaviour's own element
  /// list rather than replacing it: that behaviour rebuilds the mesh from its list on every stage, so a bed
  /// that pushed its own list would be undone by the next one. Running through the repose hook - which the
  /// animator invokes after every rebuild, its own included - is what makes the carving survive.
  /// </summary>
  private void RebuildSurface()
  {
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

  // Every molten cell of this bed, keyed by world position: the principal's own basin plus each
  // footprint filler that hosts a molten cell (the plain brick edge fillers resolve to none). Rebuilt
  // each tick - cheap for a fixed 10-cell structure and robust to a filler reloading.
  private Dictionary<BlockPos, IMoltenCell> Cells()
  {
    var cells = new Dictionary<BlockPos, IMoltenCell>();
    if (Basin is { } basin)
      cells[Pos] = basin;

    if (Block is BlockSandCastingBed bed)
      foreach (BlockPos cellPos in bed.FootprintPositions(Pos))
        if (
          Api.World.BlockAccessor.GetBlockEntity(cellPos)
            ?.GetBehavior<BEBehaviorMoltenCell>()
          is { } cell
        )
          cells[cellPos] = cell;

    return cells;
  }

  // Manhattan distance (on the flat bed) from the basin, so flow is processed basin-outward.
  private int DistFromBasin(BlockPos p) => Math.Abs(p.X - Pos.X) + Math.Abs(p.Z - Pos.Z);

  #endregion

  #region Server tick: pull, flow, cool

  private void OnServerTick(float dt)
  {
    IWorldAccessor world = Api.World;
    PullFromNeighbours(world);

    var cells = Cells();
    if (cells.Count == 0)
      return;
    foreach (IMoltenCell c in cells.Values)
      c.EnsureMetalStack(world);

    // Drive each internal edge once, from the cell nearer the basin to the farther one, so a freshly
    // pulled charge propagates outward toward the molds a wavefront at a time. An edge only exists where
    // BOTH ends are carved: uncarved and broken-out sand is not a channel, so the heat runs exactly where
    // the player cut it and stops at a mold they broke open. (Cells still tick thermally either way, which
    // is why this gates the edges rather than the cell list - a stranded charge must still cool.)
    foreach (BlockPos p in cells.Keys.OrderBy(DistFromBasin).ToList())
    {
      if (!IsCarved(p))
        continue;
      IMoltenCell a = cells[p];
      foreach (BlockFacing face in BlockFacing.HORIZONTALS)
      {
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

  // The intake: drain any adjacent EXTERNAL molten cell (a canal end delivering pig iron) into the
  // basin. The bed's own footprint fillers host the cell on a BEHAVIOUR, so `GetBlockEntity as
  // IMoltenCell` matches only real canal nodes - never this bed's cells - keeping the networks isolated.
  private void PullFromNeighbours(IWorldAccessor world)
  {
    if (Basin is not { } basin)
      return;

    foreach (BlockFacing face in BlockFacing.HORIZONTALS)
    {
      if (
        world.BlockAccessor.GetBlockEntity(Pos.AddCopy(face)) is not IMoltenCell src
        || src.Solidified
        || src.Sealed
        || src.CellAmount <= 0
      )
        continue;

      int want = Math.Min(PullRatePerTick, src.CellAmount);
      int accepted = basin.PushMetalRaw(want, src.CellMetalType, src.CellTemperature, world);
      if (accepted > 0)
        src.DrainMetal(accepted);
    }
  }

  // Moves metal toward equal fill across one internal edge (mirrors MoltenNetwork.FlowEdge). A drain
  // fitting (a mold) never gives back - it hoards its charge until it hardens into a pig.
  public static void FlowEdge(IMoltenCell x, IMoltenCell y, IWorldAccessor world)
  {
    if (x.Solidified || x.Sealed || y.Solidified || y.Sealed)
      return;

    int diff = Math.Abs(x.CellAmount - y.CellAmount);
    if (diff == 0)
      return;

    IMoltenCell giver = x.CellAmount > y.CellAmount ? x : y;
    IMoltenCell receiver = giver == x ? y : x;
    if (giver.AcceptsSubMinimumFlow || giver.CellAmount <= 0)
      return; // molds don't drain back out
    if (receiver.CellAmount > 0 && receiver.CellMetalType != giver.CellMetalType)
      return;

    int transfer = Math.Min(diff, ExlibValues.MoltenFlowRate);
    if (transfer < ExlibValues.MoltenMinFlowAmount && !receiver.AcceptsSubMinimumFlow)
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
  /// Routes a right-click on one of the bed's cells. A cell holding metal is a harvest; an empty one is a
  /// carve. Returns true when the click was consumed, false to fall through to construction (which is what
  /// an unbuilt bed wants).
  /// </summary>
  public bool OnCellInteract(BlockPos cellPos, IPlayer byPlayer) =>
    TryHarvest(cellPos, byPlayer) || TryCarveAt(cellPos, byPlayer);

  /// <summary>
  /// Cuts the shape the cell can hold into it: a runner channel down the spine, a row of impressions in the
  /// flanks. Only on a finished bed, and only into plain sand.
  /// <para>
  /// <b>There is nothing to choose.</b> One mold serves both castings - iron makes pigs in it, slag makes
  /// bricks - so the carve is decided entirely by which cell was clicked. That is the whole reason the two
  /// mold types were merged: the old sneak-modified carve let a mis-held key cut a whole row for the wrong
  /// metal, and the player found out four rows later at the pour.
  /// </para>
  /// </summary>
  public bool TryCarveAt(BlockPos cellPos, IPlayer byPlayer)
  {
    if (_animator?.IsConstructed != true || SlotAt(cellPos) is not { } slot)
      return false;

    BedSlotState wanted = slot.IsMold ? BedSlotState.Mold : BedSlotState.Runner;

    if (Api.Side == EnumAppSide.Client)
      return StateOf(slot) == BedSlotState.Sand;

    if (StateOf(slot) != BedSlotState.Sand)
    {
      (byPlayer as IServerPlayer)?.SendIngameError("iwex-castingbed-alreadycarved");
      return true;
    }

    if (!Carve(slot, wanted))
      return false;
    ExSounds.Play(Api, cellPos, ExSounds.StoneCrush, 0.5f);
    return true;
  }

  /// <summary>
  /// Collects the hardened casting from the cell at <paramref name="cellPos"/>: full 150-unit pigs
  /// first, then chunks and bits from the remainder, conserving the mass. Returns true if the click was
  /// consumed (a cell with metal), false to fall through to construction. Server hands the items over;
  /// the client just reports it handled the click.
  /// </summary>
  public bool TryHarvest(BlockPos cellPos, IPlayer byPlayer)
  {
    if (
      Api.World.BlockAccessor.GetBlockEntity(cellPos)?.GetBehavior<BEBehaviorMoltenCell>()
      is not { } cell
      || cell.CellAmount <= 0
    )
      return false;

    if (Api.Side == EnumAppSide.Client)
      return true;

    if (!cell.IsHardened)
    {
      (byPlayer as IServerPlayer)?.SendIngameError("iwex-castingbed-toohot");
      return true;
    }

    BedSlotState mold = SlotAt(cellPos) is { } s ? StateOf(s) : BedSlotState.Sand;
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
    // Shaking the casting out destroys the impression - the mold is broken open to free it, which is the
    // defining property of sand casting rather than a detail. The slot drops to plain sand and stops being
    // a channel until it is carved again.
    if (SlotAt(cellPos) is { } slot)
      Carve(slot, BedSlotState.Sand);
    ExSounds.Play(Api, cellPos, ExSounds.StoneCrush, 0.6f);
    return true;
  }

  /// <summary>
  /// Greedy denomination of hardened units into pigs (150), chunks (25) and bits (5), conserving the
  /// mass: <c>pigs*150 + chunks*25 + bits*5</c> never exceeds <paramref name="units"/>, and the only
  /// loss is a sub-bit remainder (&lt; 5 u). Pure, so the mass-conservation invariant is unit-tested.
  /// </summary>
  public static (int Pigs, int Chunks, int Bits) Denominate(int units)
  {
    int pigs = units / ItemPig.PigUnits;
    int rem = units % ItemPig.PigUnits;
    int chunks = rem / ItemPig.ChunkUnits;
    rem %= ItemPig.ChunkUnits;
    int bits = rem / ItemPig.BitUnits;
    return (pigs, chunks, bits);
  }

  /// <summary>
  /// Whether a cell in <paramref name="state"/> yields a <b>casting</b> rather than recovered scrap. Only a
  /// carved mold does: a runner is a conduit, not a cavity, so whatever cooled in it is a stranded charge and
  /// comes back as bits however good the metal was.
  /// <para>
  /// There is deliberately no metal-versus-mold pairing left. One mold casts pigs from iron and bricks from
  /// slag, so the only way to get scrap out of a mold is to pour something that is neither - which the
  /// recovery path already handles without a rule.
  /// </para>
  /// </summary>
  public static bool YieldsCasting(BedSlotState state) => state == BedSlotState.Mold;

  // What a cell yields on shake-out. A mold gives the casting its metal makes - pigs from iron, bricks from
  // slag; a runner's stranded charge comes back as recovered bits of whatever it actually was. Nothing is
  // ever destroyed, so a misrouted pour costs a remelt rather than the metal, and no refusal or error message
  // is needed to protect the player from it.
  private List<ItemStack> BuildHarvest(
    int units,
    BedSlotState mold,
    string metalCode,
    float temperature
  )
  {
    var stacks = new List<ItemStack>();
    void Add(string code, int count)
    {
      if (count <= 0)
        return;
      Item? item = Api.World.GetItem(new AssetLocation(code));
      if (item != null)
        stacks.Add(new ItemStack(item, count));
    }

    if (!YieldsCasting(mold))
    {
      if (
        MoltenChisel.BuildRecovery(
          Api.World,
          new AssetLocation(metalCode),
          temperature,
          units,
          slagFallback: true
        )
        is { } recovered
      )
        stacks.Add(recovered);
      return stacks;
    }

    if (metalCode == SlagItemDefinitions.MoltenCode)
    {
      // A brick occupies exactly one pig's impression, so the same cavity denominates either way and no
      // remainder is stranded by pouring the "wrong" one.
      Add("iwex:slagbrick", units / SlagItemDefinitions.SlagBrickUnits);
      return stacks;
    }

    (int pigs, int chunks, int bits) = Denominate(units);
    Add("iwex:pig", pigs);
    Add("iwex:pigchunk", chunks);
    Add("iwex:pigbit", bits);
    return stacks;
  }

  #endregion

  #region Client surfaces (one molten renderer per cell + the basin's pool and tap)

  // A rendered molten surface: its renderer and a lookup for the cell whose fill/temperature it shows.
  private sealed record Surface(MoltenRenderer Renderer, Func<IMoltenCell?> Cell);

  private readonly List<Surface> _surfaces = [];

  private void BuildSurfaces(ICoreClientAPI capi)
  {
    float rotY = ((Block as BlockSandCastingBed)?.StructureAngle ?? 0) * GameMath.DEG2RAD;

    // The principal's two surfaces: the shallow pour pool (its height tracks the basin fill) and the
    // spout/tap up on the tower, which simply lights up while the bed holds metal.
    AddSurface(capi, Pos, new Cuboidf(0, 12, 12, 16, 14, 14), rotY, () => Basin);
    AddSurface(capi, Pos, new Cuboidf(6, 14, 10, 10, 16, 16), rotY, () => Basin);

    if (Block is not BlockSandCastingBed bed)
      return;

    // One surface per footprint molten cell. Runners read as a central channel, molds as a broad pool;
    // the brick edge fillers host no cell, so they add nothing.
    foreach (BlockPos cellPos in bed.FootprintPositions(Pos))
    {
      if (capi.World.BlockAccessor.GetBlockEntity(cellPos)?.GetBehavior<BEBehaviorMoltenCell>() is null)
        continue;
      bool runner = cellPos.X == Pos.X;
      Cuboidf box = runner
        ? new Cuboidf(5, 1, 0, 11, 3, 16)
        : new Cuboidf(2, 1, 3, 14, 4, 13);
      BlockPos captured = cellPos.Copy();
      AddSurface(
        capi,
        captured,
        box,
        rotY,
        () =>
          capi.World.BlockAccessor.GetBlockEntity(captured)?.GetBehavior<BEBehaviorMoltenCell>()
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
  )
  {
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

  private void UpdateSurfaces()
  {
    foreach (Surface s in _surfaces)
    {
      IMoltenCell? cell = s.Cell();
      if (cell == null || cell.CellAmount <= 0 || cell.CellMetalType.Length == 0)
      {
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

  private void DisposeSurfaces()
  {
    foreach (Surface s in _surfaces)
      s.Renderer.Dispose();
    _surfaces.Clear();
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    base.GetBlockInfo(forPlayer, dsc);

    if (Basin is not { } basin)
      return;

    if (basin.CellAmount > 0)
      dsc.AppendLine(
        Lang.Get(
          "iwex:castingbed-basin",
          basin.CellAmount,
          ExMeasure.Temperature(basin.CellTemperature)
        )
      );
    else
      dsc.AppendLine(Lang.Get("iwex:castingbed-empty"));

    int ready = 0;
    int cooling = 0;
    if (Block is BlockSandCastingBed bed)
      foreach (BlockPos cellPos in bed.FootprintPositions(Pos))
      {
        if (
          cellPos.X == Pos.X
          || Api.World.BlockAccessor.GetBlockEntity(cellPos)?.GetBehavior<BEBehaviorMoltenCell>()
            is not { CellAmount: > 0 } cell
        )
          continue; // count molds only (side columns)
        if (cell.IsHardened)
          ready += cell.CellAmount / ItemPig.PigUnits;
        else
          cooling++;
      }

    if (ready > 0)
      dsc.AppendLine(Lang.Get("iwex:castingbed-pigs-ready", ready));
    if (cooling > 0)
      dsc.AppendLine(Lang.Get("iwex:castingbed-cooling", cooling));
  }

  #endregion

  #region Serialization

  // The carved surface, one byte per slot, positional against SandBedLayout.Slots - which is why that order
  // is part of the save format and pinned by a test. Written as one array rather than ten keys so a layout
  // change is a single, visible break rather than ten silent ones.
  private const string SlotsKey = "bed_slots";

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    var packed = new byte[_slots.Length];
    for (int i = 0; i < _slots.Length; i++)
      packed[i] = (byte)_slots[i];
    tree.SetBytes(SlotsKey, packed);
  }

  public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
  {
    base.FromTreeAttributes(tree, world);

    byte[]? packed = tree.GetBytes(SlotsKey);
    for (int i = 0; i < _slots.Length; i++)
    {
      // A bed saved before carving existed (or a truncated array) reads as uncarved sand, and a value from
      // a future layout falls back the same way rather than rendering nothing.
      BedSlotState state =
        packed != null && i < packed.Length ? (BedSlotState)packed[i] : BedSlotState.Sand;
      _slots[i] = SandBedLayout.Slots[i].Accepts(state) ? state : BedSlotState.Sand;
    }

    if (Api?.Side == EnumAppSide.Client)
      RebuildSurface();
  }

  #endregion
}
