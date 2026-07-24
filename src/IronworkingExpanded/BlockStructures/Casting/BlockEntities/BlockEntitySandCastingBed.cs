using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpandedLib;
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
/// filler-hosted cells (basin → central runners → the six side double-molds) itself, so the outside
/// canal line can carry a different metal. Metal enters through a pull-port that drains the adjacent
/// external molten cell each tick; it propagates by level-equalisation, cools where no fresh metal
/// arrives, and - once a mold hardens - the player right-clicks it to collect up to two 150-unit pigs
/// (partial fills and runner residue come out as chunks/bits, conserving the mass).
/// </summary>
[BlockEntityRegister]
public class BlockEntitySandCastingBed : BlockEntity
{
  // A mold cell casts two 150u pigs (300u); a runner is a thin pass-through whose residue is bits.
  private const int PullRatePerTick = 25;

  private long _serverTick;
  private long _clientTick;

  private BEBehaviorMoltenCell? Basin => GetBehavior<BEBehaviorMoltenCell>();

  #region Lifecycle

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
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
    DisposeSurfaces();
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded()
  {
    DisposeSurfaces();
    base.OnBlockUnloaded();
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
    // pulled charge propagates outward toward the molds a wavefront at a time.
    foreach (BlockPos p in cells.Keys.OrderBy(DistFromBasin).ToList())
    {
      IMoltenCell a = cells[p];
      foreach (BlockFacing face in BlockFacing.HORIZONTALS)
      {
        BlockPos np = p.AddCopy(face);
        if (cells.TryGetValue(np, out IMoltenCell? b) && DistFromBasin(np) > DistFromBasin(p))
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

    foreach (ItemStack stack in BuildHarvest(cell.CellAmount))
      if (byPlayer.InventoryManager?.TryGiveItemstack(stack) != true)
        Api.World.SpawnItemEntity(stack, cellPos.ToVec3d().Add(0.5, 0.6, 0.5));

    cell.ClearContents();
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

  private List<ItemStack> BuildHarvest(int units)
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
}
