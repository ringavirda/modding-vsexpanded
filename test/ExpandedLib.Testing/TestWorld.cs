using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Networks;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ExpandedLib.Testing;

/// <summary>
/// A headless, in-process stand-in for a Vintage Story server world, just large enough to drive
/// the block-network simulation in tests. It owns an in-memory block/block-entity store, a live
/// <see cref="BlockNetworkModSystem"/>, and an <see cref="IServerWorldAccessor"/> wired to the
/// store (via NSubstitute - the real interfaces carry ~80 members each, almost none of which the
/// simulation touches).
///
/// Typical use: <see cref="Place"/> blocks, <see cref="AddNode"/> them to a network, then
/// <see cref="Tick"/> to advance one server second at a time and assert on <see cref="NetworkAt"/>.
/// </summary>
public sealed class TestWorld
{
  private readonly Dictionary<BlockPos, Block> _blocks = new();
  private readonly Dictionary<BlockPos, BlockEntity> _blockEntities = new();
  private readonly Dictionary<int, Block> _blocksById = new();
  private readonly Dictionary<string, Block> _blocksByCode = new();
  private readonly Dictionary<string, Item> _itemsByCode = new();
  private readonly Dictionary<int, Item> _itemsById = new();
  private readonly Dictionary<string, Func<BlockEntity>> _beFactories = new();
  private int _nextItemId = 1;

  private double _totalDays;

  /// <summary>The block returned for any cell that has not been placed (id 0, code "game:air").</summary>
  public Block Air { get; }

  /// <summary>The network graph manager under test. Factories are registered via <see cref="RegisterNetwork"/>.</summary>
  public BlockNetworkModSystem Networks { get; } = new();

  /// <summary>The fake block accessor handed to every production network call.</summary>
  public IBlockAccessor Accessor { get; }

  /// <summary>The fake server world (calendar, item-drop spawning) exposed as <see cref="BlockNetworkModSystem.ServerWorld"/>.</summary>
  public IServerWorldAccessor World { get; }

  /// <summary>The calendar; <see cref="AdvanceDays"/> moves <c>TotalDays</c> for evaporation tests.</summary>
  public IGameCalendar Calendar { get; }

  /// <summary>
  /// A server-side core API wired to this world (mod loader resolves <see cref="Networks"/>, event
  /// API captures block-entity tick listeners). Assign it to a block entity's <c>Api</c> (or via
  /// <see cref="Attach"/>) so it can resolve networks and register production ticks headlessly.
  /// </summary>
  public ICoreServerAPI Api { get; }

  private readonly Dictionary<long, TickListener> _tickListeners = new();

  // Sim-time (ms) accrued toward each listener's next fire, for interval-aware advancing
  // (AdvanceBlockEntityTime). Remainders carry across calls so two sub-interval advances still cross
  // the boundary. FireBlockEntityTicks ignores this and fires every listener regardless.
  private readonly Dictionary<long, int> _tickAccumMs = new();
  private long _nextListenerId;

  /// <summary>A captured block-entity tick listener: its callback and the interval (ms) it asked for.
  /// The interval feeds interval-aware ticking (<see cref="AdvanceBlockEntityTime"/>);
  /// <see cref="FireBlockEntityTicks"/> fires every live listener regardless. Keyed by a real unique id
  /// so <c>UnregisterGameTickListener</c> can drop a torn-down block entity's listener.</summary>
  private readonly record struct TickListener(
    System.Action<float> Callback,
    int IntervalMs
  );

  /// <summary>Item stacks spawned by the simulation (e.g. a bursting pipe dropping its materials).</summary>
  public List<ItemStack> Drops { get; } = new();

  public TestWorld()
  {
    Air = TestBlocks.Configure(new Block(), "game:air", 0);
    _blocksById[0] = Air;

    Calendar = Substitute.For<IGameCalendar>();
    PushCalendar();

    Accessor = BuildAccessor();
    World = BuildWorld();
    Api = BuildApi();
    World.Api.Returns(Api);

    // The manager normally captures the server world in StartServerSide, which we deliberately
    // do not call (it would also register a real tick listener). Prime it directly.
    ReflectionHelpers.SetProperty(
      Networks,
      nameof(Networks.ServerWorld),
      World
    );
  }

  /// <summary>Links <paramref name="be"/> to this world's API so it can resolve networks and ticks.</summary>
  public TestWorld Attach(BlockEntity be)
  {
    be.Api = Api;
    return this;
  }

  /// <summary>
  /// Runs <paramref name="be"/> through its real <see cref="BlockEntity.Initialize"/> against this
  /// world's API (so a network node registers itself, captures the manager and schedules its ticks),
  /// exactly as the placement pipeline would. The block entity must already be <see cref="Place"/>d.
  /// </summary>
  public TestWorld Initialize(BlockEntity be)
  {
    be.Api = Api;
    be.Initialize(Api);
    return this;
  }

  #region Setup

  /// <summary>Registers a typed-network factory, exactly as a mod would in <c>ModSystem.Start</c>.</summary>
  public TestWorld RegisterNetwork(
    string networkType,
    System.Func<BlockNetworkModSystem, BlockNetwork> factory
  )
  {
    Networks.RegisterNetworkType(networkType, () => factory(Networks));
    return this;
  }

  /// <summary>
  /// Places <paramref name="block"/> (and optional <paramref name="be"/>) at <paramref name="pos"/>,
  /// registering the block in the id/code lookup so <c>ExchangeBlock</c>/<c>GetBlock</c> resolve it.
  /// The block entity is positioned and linked but not <c>Initialize</c>d - the network suite drives
  /// the graph directly rather than through the placement pipeline.
  /// </summary>
  public TestWorld Place(BlockPos pos, Block block, BlockEntity? be = null)
  {
    Register(block);
    _blocks[pos] = block;
    if (be != null)
    {
      be.Pos = pos.Copy();
      be.Block = block;
      _blockEntities[pos] = be;
    }
    return this;
  }

  /// <summary>
  /// Registers a factory that <c>BlockAccessor.SpawnBlockEntity(classname, pos)</c> uses to create a
  /// block entity for <paramref name="classname"/> - the headless stand-in for the engine's class
  /// registry. The spawned entity is positioned, linked to the block at that cell, stored, and
  /// <c>Initialize</c>d against this world's API, mirroring the real spawn path closely enough to test
  /// block-entity recreation (e.g. the orphaned-BE healer).
  /// </summary>
  public TestWorld RegisterBlockEntityFactory(
    string classname,
    Func<BlockEntity> factory
  )
  {
    _beFactories[classname] = factory;
    return this;
  }

  /// <summary>Registers a block in the id/code lookup without placing it (for orientation-variant swaps).</summary>
  public TestWorld Register(Block block)
  {
    _blocksById[block.BlockId] = block;
    if (block.Code != null)
      _blocksByCode[block.Code.ToString()] = block;
    return this;
  }

  /// <summary>
  /// Registers a resolvable <see cref="Item"/> under <paramref name="code"/> so
  /// <c>World.GetItem(code)</c> returns it - the molten-metal API resolves its temperature carrier
  /// this way. <paramref name="meltingPoint"/> (°C, 0 = none) is exposed through the item's
  /// <see cref="CombustibleProperties"/> so that melt-point classification (liquid/cooling/hardened)
  /// works headlessly. Returns the created item.
  /// </summary>
  public Item RegisterItem(string code, float meltingPoint = 0f)
  {
    // A unique non-zero id so ItemStack.ResolveBlockOrItem (which re-resolves a cloned/loaded stack
    // by id) finds the item instead of nulling out its Collectible.
    var item = new Item
    {
      Code = new AssetLocation(code),
      ItemId = _nextItemId++,
    };
    if (meltingPoint > 0f)
      item.CombustibleProps = new CombustibleProperties
      {
        MeltingPoint = (int)meltingPoint,
      };
    _itemsByCode[code] = item;
    _itemsById[item.ItemId] = item;
    return item;
  }

  public Item? GetItem(AssetLocation? code) =>
    code != null && _itemsByCode.TryGetValue(code.ToString(), out var i)
      ? i
      : null;

  public Item? GetItem(int id) =>
    _itemsById.TryGetValue(id, out var i) ? i : null;

  #endregion

  #region Store access

  public Block GetBlock(BlockPos pos) =>
    _blocks.TryGetValue(pos, out var b) ? b : Air;

  public BlockEntity? GetBlockEntity(BlockPos pos) =>
    _blockEntities.TryGetValue(pos, out var be) ? be : null;

  #endregion

  #region Graph passthrough

  public void AddNode(BlockPos pos, string networkType) =>
    Networks.AddNode(Accessor, pos, networkType);

  public void RemoveNode(BlockPos pos) => Networks.RemoveNode(Accessor, pos);

  public BlockNetwork? NetworkAt(BlockPos pos) => Networks.GetNetworkAt(pos);

  #endregion

  #region Neighbours

  /// <summary>
  /// Fires <see cref="Block.OnNeighbourBlockChange"/> on each of the six blocks adjacent to
  /// <paramref name="changedPos"/>, exactly as the engine does right after a block is placed, broken or
  /// exchanged at that cell (each neighbour is told its own position and the position that changed).
  /// Empty cells resolve to <see cref="Air"/>, whose base implementation is a no-op, so only real
  /// neighbours react. This is <b>opt-in</b>: the low-level <see cref="Place"/>/<c>SetBlock</c>/
  /// <c>ExchangeBlock</c>/<c>BreakBlock</c> helpers deliberately do NOT auto-fire it, because doing so
  /// would make an isolated network node self-break and reorientations recurse across the graph suite.
  /// Call it when a test needs to exercise neighbour-driven reactions - a network node re-checking its
  /// support and self-breaking, a canal updating its end connectors, an intake re-syncing orientation.
  /// </summary>
  public TestWorld NotifyNeighbours(BlockPos changedPos)
  {
    foreach (BlockFacing face in BlockFacing.ALLFACES)
    {
      BlockPos nPos = changedPos.AddCopy(face);
      GetBlock(nPos).OnNeighbourBlockChange(World, nPos, changedPos);
    }
    return this;
  }

  #endregion

  #region Time

  /// <summary>
  /// Advances the simulation by <paramref name="seconds"/> server ticks (the network manager runs
  /// one tick per second). Mirrors <c>BlockNetworkModSystem.OnServerTick</c> by dispatching
  /// <see cref="BlockNetwork.OnTick"/> for every live network, with <c>dt = 1</c>.
  /// </summary>
  public void Tick(int seconds = 1)
  {
    for (int i = 0; i < seconds; i++)
      foreach (var net in Networks.AllNetworks.ToList())
        net.OnTick(Accessor, 1f, Networks);
  }

  /// <summary>Fires every block-entity server tick listener registered through <see cref="Api"/>
  /// (i.e. via <c>BlockEntity.RegisterGameTickListener</c>), <paramref name="times"/> times.</summary>
  public void FireBlockEntityTicks(float dt = 1f, int times = 1)
  {
    for (int i = 0; i < times; i++)
      foreach (var listener in _tickListeners.Values.ToList())
        listener.Callback(dt);
  }

  /// <summary>
  /// Advances block-entity sim time by <paramref name="totalMs"/> ms, firing each registered listener
  /// once per whole interval that elapses - honouring the interval each block entity asked for at
  /// <c>RegisterGameTickListener</c>. A 1000 ms listener fires twice over 2500 ms; a 250 ms listener
  /// fires ten times; each callback receives <c>dt = interval / 1000</c> s. Remainders carry across
  /// calls, so two 600 ms advances still cross a 1000 ms boundary once. Unlike
  /// <see cref="FireBlockEntityTicks"/> (which fires every listener a fixed number of times regardless
  /// of interval), this lets a scene with block entities on different intervals be advanced faithfully,
  /// and interval-gated behaviour be asserted. A listener that unregisters itself mid-advance stops
  /// receiving further fires this call.
  /// </summary>
  public void AdvanceBlockEntityTime(int totalMs)
  {
    // Snapshot: a listener may unregister (or a block entity may register a new one) while firing.
    foreach (long id in _tickListeners.Keys.ToList())
    {
      if (!_tickListeners.TryGetValue(id, out TickListener listener))
        continue; // already removed by an earlier callback this pass
      int interval = System.Math.Max(1, listener.IntervalMs);
      int accum = _tickAccumMs.TryGetValue(id, out int a) ? a : 0;
      accum += totalMs;
      float dt = interval / 1000f;
      while (accum >= interval && _tickListeners.ContainsKey(id))
      {
        accum -= interval;
        listener.Callback(dt);
      }
      // The listener may have been torn down by its own callback; only keep live remainders.
      if (_tickListeners.ContainsKey(id))
        _tickAccumMs[id] = accum;
    }
  }

  /// <summary>Moves the calendar forward without ticking, for calendar-driven effects (evaporation).</summary>
  public void AdvanceDays(double days)
  {
    _totalDays += days;
    PushCalendar();
  }

  private void PushCalendar() => Calendar.TotalDays.Returns(_totalDays);

  #endregion

  #region Lifecycle

  /// <summary>
  /// Models a save → chunk-unload → reload of the block entity at <paramref name="pos"/>: serialises its
  /// real <c>ToTreeAttributes</c> bytes, tears the live instance down (unregistering its tick listeners,
  /// exactly as the engine does on unload) while leaving the block placed, then rebuilds a <b>fresh</b>
  /// instance of the same class and drives <c>FromTreeAttributes</c> → <c>Initialize</c> - the sequence a
  /// loaded-from-disk block entity actually goes through, and where reload bugs (stale-pool bursts,
  /// dropped mid-cycle state, phantom graph nodes) surface. Returns the new instance; the old one is
  /// discarded. Tick after this to exercise the first post-reload tick.
  /// </summary>
  public BlockEntity? Reload(BlockPos pos)
  {
    BlockEntity? old = GetBlockEntity(pos);
    if (old == null)
      return null;

    Block block = GetBlock(pos);

    var tree = new TreeAttribute();
    old.ToTreeAttributes(tree);

    // Unload the live instance (unregisters its listeners; a network node keeps its graph node, as
    // the base OnBlockUnloaded does not RemoveNode), then drop it - the block stays placed.
    old.OnBlockUnloaded();
    _blockEntities.Remove(pos);

    BlockEntity fresh = NewBlockEntityLike(old, block);
    fresh.Pos = pos.Copy();
    fresh.Block = block;
    fresh.Api = Api;
    _blockEntities[pos] = fresh;
    fresh.FromTreeAttributes(tree, World);
    fresh.Initialize(Api);
    return fresh;
  }

  /// <summary>
  /// Models a chunk unload of the block entity at <paramref name="pos"/>: runs its real
  /// <c>OnBlockUnloaded</c> (which unregisters its tick listeners - now honoured by the fake event API,
  /// so it truly stops ticking) and drops the instance while leaving the block placed. Use to assert a
  /// machine's teardown, or that a "stopped" listener no longer fires.
  /// </summary>
  public void Unload(BlockPos pos)
  {
    GetBlockEntity(pos)?.OnBlockUnloaded();
    _blockEntities.Remove(pos);
  }

  /// <summary>Creates a fresh block entity of the same class the engine would instantiate on load:
  /// a registered factory for the block's entity class if one exists (see
  /// <see cref="RegisterBlockEntityFactory"/>), otherwise the type's parameterless constructor.</summary>
  private BlockEntity NewBlockEntityLike(BlockEntity old, Block block)
  {
    string? classname = block?.EntityClass ?? old.Block?.EntityClass;
    if (classname != null && _beFactories.TryGetValue(classname, out var factory))
      return factory();
    return (BlockEntity)Activator.CreateInstance(old.GetType())!;
  }

  #endregion

  #region Fake wiring

  private IBlockAccessor BuildAccessor()
  {
    var a = Substitute.For<IBlockAccessor>();

    a.GetBlock(Arg.Any<BlockPos>()).Returns(ci => GetBlock(ci.Arg<BlockPos>()));
    // The fluid/solid-layer overload (BlockLayersAccess) reads the same store - tests that need a
    // distinct fluid layer place a block whose LiquidCode is set.
    a.GetBlock(Arg.Any<BlockPos>(), Arg.Any<int>())
      .Returns(ci => GetBlock(ci.Arg<BlockPos>()));
    a.GetBlockEntity(Arg.Any<BlockPos>())
      .Returns(ci => GetBlockEntity(ci.Arg<BlockPos>()));

    a.When(x => x.SetBlock(Arg.Any<int>(), Arg.Any<BlockPos>()))
      .Do(ci => DoSetBlock(ci.ArgAt<int>(0), ci.ArgAt<BlockPos>(1)));
    a.When(x => x.ExchangeBlock(Arg.Any<int>(), Arg.Any<BlockPos>()))
      .Do(ci => DoExchangeBlock(ci.ArgAt<int>(0), ci.ArgAt<BlockPos>(1)));
    a.When(x => x.MarkBlockDirty(Arg.Any<BlockPos>())).Do(_ => { });
    a.When(x =>
        x.SpawnBlockEntity(
          Arg.Any<string>(),
          Arg.Any<BlockPos>(),
          Arg.Any<ItemStack>()
        )
      )
      .Do(ci =>
        DoSpawnBlockEntity(ci.ArgAt<string>(0), ci.ArgAt<BlockPos>(1))
      );
    a.When(x =>
        x.BreakBlock(Arg.Any<BlockPos>(), Arg.Any<IPlayer>(), Arg.Any<float>())
      )
      .Do(ci => DoBreak(ci.ArgAt<BlockPos>(0)));

    // WalkBlocks over an inclusive box, reading the store cell by cell (empties read as Air). Used by
    // region scans such as the blast furnace's hearth-pile walk.
    a.When(x =>
        x.WalkBlocks(
          Arg.Any<BlockPos>(),
          Arg.Any<BlockPos>(),
          Arg.Any<System.Action<Block, int, int, int>>(),
          Arg.Any<bool>()
        )
      )
      .Do(ci =>
        DoWalkBlocks(
          ci.ArgAt<BlockPos>(0),
          ci.ArgAt<BlockPos>(1),
          ci.ArgAt<System.Action<Block, int, int, int>>(2)
        )
      );

    return a;
  }

  private void DoWalkBlocks(
    BlockPos min,
    BlockPos max,
    System.Action<Block, int, int, int> onBlock
  )
  {
    int x0 = System.Math.Min(min.X, max.X),
      x1 = System.Math.Max(min.X, max.X);
    int y0 = System.Math.Min(min.Y, max.Y),
      y1 = System.Math.Max(min.Y, max.Y);
    int z0 = System.Math.Min(min.Z, max.Z),
      z1 = System.Math.Max(min.Z, max.Z);
    for (int x = x0; x <= x1; x++)
    for (int y = y0; y <= y1; y++)
    for (int z = z0; z <= z1; z++)
      onBlock(GetBlock(new BlockPos(x, y, z, min.dimension)), x, y, z);
  }

  private IServerWorldAccessor BuildWorld()
  {
    var w = Substitute.For<IServerWorldAccessor>();
    w.BlockAccessor.Returns(Accessor);
    w.Calendar.Returns(Calendar);
    // Particle/sound helpers (e.g. a bursting pipe's vapour plume) read world.Rand.
    w.Rand.Returns(new Random(1));
    w.GetBlock(Arg.Any<AssetLocation>())
      .Returns(ci => GetByCode(ci.Arg<AssetLocation>()));
    w.GetBlock(Arg.Any<int>())
      .Returns(ci =>
        _blocksById.TryGetValue(ci.Arg<int>(), out var b) ? b : Air
      );
    w.GetItem(Arg.Any<AssetLocation>())
      .Returns(ci => GetItem(ci.Arg<AssetLocation>()));
    w.GetItem(Arg.Any<int>()).Returns(ci => GetItem(ci.Arg<int>()));
    w.When(x =>
        x.SpawnItemEntity(
          Arg.Any<ItemStack>(),
          Arg.Any<Vec3d>(),
          Arg.Any<Vec3d>()
        )
      )
      .Do(ci => Drops.Add(ci.Arg<ItemStack>()));
    return w;
  }

  private ICoreServerAPI BuildApi()
  {
    var api = Substitute.For<ICoreServerAPI>();
    // A block entity's Api field is typed ICoreAPI, so it reads the base-interface World/Event/
    // ModLoader members - which ICoreServerAPI re-declares with `new`. Configure both views.
    var coreApi = (ICoreAPI)api;

    api.Side.Returns(EnumAppSide.Server);
    api.World.Returns(World);
    coreApi.World.Returns(World);

    var modLoader = Substitute.For<IModLoader>();
    modLoader.GetModSystem<BlockNetworkModSystem>().Returns(Networks);
    api.ModLoader.Returns(modLoader);
    coreApi.ModLoader.Returns(modLoader);

    var events = Substitute.For<IServerEventAPI>();
    api.Event.Returns(events);
    coreApi.Event.Returns(events);

    // Capture the server tick listeners block entities register, so the test can pump them via
    // FireBlockEntityTicks. BlockEntity.RegisterGameTickListener forwards to a position-scoped
    // event-API overload that gained a BlockPos parameter in 1.22: 1.22 calls
    // (onGameTick, Pos, errorHandler, interval, delay); 1.20/1.21 call (onGameTick, errorHandler,
    // interval, delay) with no position. Mock whichever overload this game version forwards to.
#if GAME_GE_1_22
    events
      .RegisterGameTickListener(
        Arg.Any<System.Action<float>>(),
        Arg.Any<BlockPos>(),
        Arg.Any<System.Action<System.Exception>>(),
        Arg.Any<int>(),
        Arg.Any<int>()
      )
      .Returns(ci => AddTickListener(ci.Arg<System.Action<float>>(), ci.ArgAt<int>(3)));
#else
    events
      .RegisterGameTickListener(
        Arg.Any<System.Action<float>>(),
        Arg.Any<System.Action<System.Exception>>(),
        Arg.Any<int>(),
        Arg.Any<int>()
      )
      .Returns(ci => AddTickListener(ci.Arg<System.Action<float>>(), ci.ArgAt<int>(2)));
#endif

    // Honour UnregisterGameTickListener so a torn-down block entity (Reload/Unload/OnBlockRemoved)
    // actually stops ticking. The real event API removes it; leaving this a no-op would let a
    // discarded block entity keep ticking and mask double-tick bugs after a reload.
    events
      .When(x => x.UnregisterGameTickListener(Arg.Any<long>()))
      .Do(ci =>
      {
        long id = ci.Arg<long>();
        _tickListeners.Remove(id);
        _tickAccumMs.Remove(id);
      });

    return api;
  }

  private Block? GetByCode(AssetLocation? code) =>
    code != null && _blocksByCode.TryGetValue(code.ToString(), out var b)
      ? b
      : null;

  private long AddTickListener(System.Action<float> callback, int intervalMs)
  {
    long id = ++_nextListenerId;
    _tickListeners[id] = new TickListener(callback, intervalMs);
    _tickAccumMs[id] = 0;
    return id;
  }

  private void DoSetBlock(int id, BlockPos pos)
  {
    if (id == 0)
    {
      _blocks.Remove(pos);
      _blockEntities.Remove(pos);
      return;
    }
    if (!_blocksById.TryGetValue(id, out var b))
      return;
    _blocks[pos] = b;

    // Engine parity: placing a block that declares an entity class (re)creates its block entity, so a
    // caller that swaps a block and then reads its block entity - as the migrator's ReplaceBlock does
    // to hand over the old entity's saved tree - finds the new block's BE right after the swap. Only
    // acts when a factory is registered for the class (opt-in via RegisterBlockEntityFactory), so the
    // graph-only network tests that never register one are unaffected. A BE already matching the new
    // block is kept; a stale one (different block) is replaced.
    if (
      b.EntityClass is { } entityClass
      && (
        !_blockEntities.TryGetValue(pos, out var existing)
        || existing.Block != b
      )
    )
    {
      // A block-changing SetBlock replaces the old block entity; tear the stale one down first (as
      // the engine's chunk unload does - unregistering its tick listeners) so it cannot linger as a
      // zombie that keeps ticking, mirroring the faithful DoBreak teardown.
      existing?.OnBlockUnloaded();
      DoSpawnBlockEntity(entityClass, pos);
    }
  }

  private void DoSpawnBlockEntity(string classname, BlockPos pos)
  {
    if (!_beFactories.TryGetValue(classname, out var factory))
      return;
    var be = factory();
    be.Pos = pos.Copy();
    be.Block = GetBlock(pos);
    _blockEntities[pos] = be;
    be.Initialize(Api);
  }

  private void DoExchangeBlock(int id, BlockPos pos)
  {
    if (!_blocksById.TryGetValue(id, out var b))
      return;
    _blocks[pos] = b;
    if (_blockEntities.TryGetValue(pos, out var be))
      be.Block = b;
  }

  private void DoBreak(BlockPos pos)
  {
    // Route through the real break lifecycle so a block entity drops its contents and, crucially,
    // runs OnBlockRemoved - which unregisters its tick listeners and (for a network node) calls
    // RemoveNode. The old stub skipped this, so a "forgot to RemoveNode" regression left a phantom
    // graph node that no test could see.
    if (_blockEntities.TryGetValue(pos, out var be))
    {
      be.OnBlockBroken();
      be.OnBlockRemoved();
    }
    _blocks.Remove(pos);
    _blockEntities.Remove(pos);
  }

  #endregion
}
