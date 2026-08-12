using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Networks;
using ExpandedLib.Testing.Doubles;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ExpandedLib.Testing;

/// <summary>
/// A headless, in-process stand-in for a Vintage Story server world, large enough to drive the
/// block-network simulation in tests. It owns an in-memory block/block-entity store, a live
/// <see cref="BlockNetworkModSystem"/>, and NSubstitute fakes for the accessor, world and server API
/// wired to that store. Typical use: <see cref="Place"/> blocks, <see cref="AddNode"/> them to a
/// network, then <see cref="Tick"/> one server second at a time and assert on <see cref="NetworkAt"/>.
/// </summary>
public sealed class TestWorld {
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
  /// A server-side core API wired to this world (mod loader resolves <see cref="Networks"/>, event API
  /// captures block-entity tick listeners). Assign it to a block entity's <c>Api</c>, or use
  /// <see cref="Attach"/>, so it can resolve networks and register production ticks headlessly.
  /// </summary>
  public ICoreServerAPI Api { get; }

  private readonly Dictionary<long, TickListener> _tickListeners = new();

  // Sim-time (ms) accrued toward each listener's next fire, for interval-aware advancing
  // (AdvanceBlockEntityTime). Remainders carry across calls so two sub-interval advances still cross
  // the boundary. FireBlockEntityTicks ignores this and fires every listener regardless.
  private readonly Dictionary<long, int> _tickAccumMs = new();
  private long _nextListenerId;

  /// <summary>A captured block-entity tick listener: its callback and the interval (ms) it asked for.
  /// Keyed by a unique id so <c>UnregisterGameTickListener</c> can drop a torn-down block entity's
  /// listener.</summary>
  private readonly record struct TickListener(
    System.Action<float> Callback,
    int IntervalMs
  );

  /// <summary>Item stacks spawned by the simulation (e.g. a bursting pipe dropping its materials).</summary>
  public List<ItemStack> Drops { get; } = new();

  public TestWorld() {
    Air = TestBlocks.Configure(new Block(), "game:air", 0);
    _blocksById[0] = Air;

    Calendar = Substitute.For<IGameCalendar>();
    PushCalendar();

    Accessor = BuildAccessor();
    World = BuildWorld();
    Api = BuildApi();
    World.Api.Returns(Api);

    // StartServerSide is not called (it would register a real tick listener), so the server world it
    // normally captures is primed directly.
    ReflectionHelpers.SetProperty(
      Networks,
      nameof(Networks.ServerWorld),
      World
    );
  }

  /// <summary>Links <paramref name="be"/> to this world's API so it can resolve networks and ticks.</summary>
  public TestWorld Attach(BlockEntity be) {
    be.Api = Api;
    return this;
  }

  /// <summary>
  /// Runs <paramref name="be"/> through its real <see cref="BlockEntity.Initialize"/> against this
  /// world's API, so a network node registers itself and schedules its ticks exactly as the placement
  /// pipeline would. The block entity must already be <see cref="Place"/>d.
  /// </summary>
  public TestWorld Initialize(BlockEntity be) {
    be.Api = Api;
    be.Initialize(Api);
    return this;
  }

  #region Setup

  /// <summary>Registers a typed-network factory, exactly as a mod would in <c>ModSystem.Start</c>.</summary>
  public TestWorld RegisterNetwork(
    string networkType,
    System.Func<BlockNetworkModSystem, BlockNetwork> factory
  ) {
    Networks.RegisterNetworkType(networkType, () => factory(Networks));
    return this;
  }

  /// <summary>
  /// Places <paramref name="block"/> (and optional <paramref name="be"/>) at <paramref name="pos"/>,
  /// registering the block in the id/code lookup so <c>ExchangeBlock</c>/<c>GetBlock</c> resolve it.
  /// The block entity is positioned and linked but not <c>Initialize</c>d; see
  /// <see cref="Initialize"/> for the placement-pipeline path.
  /// </summary>
  public TestWorld Place(BlockPos pos, Block block, BlockEntity? be = null) {
    Register(block);
    _blocks[pos] = block;
    if (be != null) {
      be.Pos = pos.Copy();
      be.Block = block;
      _blockEntities[pos] = be;
    }
    return this;
  }

  /// <summary>
  /// Places a network node at <paramref name="pos"/>: a <see cref="TestNetworkBlock"/> of
  /// <paramref name="networkType"/> whose connectors are <paramref name="orientation"/>, a block
  /// entity carrying one membership for that network, and the real <see cref="Initialize"/> the
  /// placement pipeline runs - which is what registers the cell. <see cref="RegisterNetwork"/> must
  /// have run for <paramref name="networkType"/> first, or the factory lookup throws.
  /// </summary>
  public TestWorld PlaceNode(
    BlockPos pos,
    string networkType,
    string orientation,
    int id = 1
  ) {
    TestMemberBlockEntity be = TestMemberBlockEntity.Carrying(networkType);
    Place(pos, TestNetworkBlock.Create(networkType, orientation, id), be);
    return Initialize(be);
  }

  /// <summary>
  /// Places a cell whose membership is all that puts it on the graph: a plain <see cref="Block"/> -
  /// deliberately neither a node block nor an <see cref="INetworkConnector"/> - under a block entity
  /// carrying one membership that states its own <paramref name="connectors"/>. Registered the same
  /// way <see cref="PlaceNode"/> is.
  /// </summary>
  public TestWorld PlaceMemberBlock(
    BlockPos pos,
    string networkType,
    string connectors,
    int id = 899
  ) {
    TestMemberBlockEntity be = TestMemberBlockEntity.Declaring(
      networkType,
      connectors
    );
    Place(pos, TestBlocks.Configure(new Block(), $"test:member-{id}", id), be);
    return Initialize(be);
  }

  /// <summary>
  /// The one shared <see cref="BlockStructureFiller"/> this world places footprint cells from, created
  /// on first use. Production has exactly one instance - always north, no variants - standing in every
  /// cell of every mega-block, so a cell's own answers have to come from its block entity rather than
  /// from its block; the fixtures share one instance for the same reason.
  /// </summary>
  public BlockStructureFiller Filler =>
    _filler ??= TestBlocks.Configure(
      new BlockStructureFiller(),
      "exlib:structurefiller",
      897
    );

  private BlockStructureFiller? _filler;

  /// <summary>
  /// The registered class code of the network-membership behaviour, as a <c>fillerOffsets</c> cell
  /// names it. Written out rather than derived, because it is the string content authors write;
  /// <c>EntityRegistry.KeyFor</c> is asserted against it so the two cannot drift.
  /// </summary>
  public const string NetworkMemberClass = "exlib.BEBehaviorNetworkMember";

  /// <summary>
  /// Places a mega-block footprint cell at <paramref name="pos"/>: the shared <see cref="Filler"/>
  /// block over a filler block entity linked to <paramref name="principal"/> (the cell below by
  /// default) and hosting <paramref name="hosted"/>, then runs the real <see cref="Initialize"/> the
  /// placement and load paths both go through, which is what creates the hosted behaviours.
  /// </summary>
  public TestWorld PlaceFiller(
    BlockPos pos,
    FillerBehavior[]? hosted = null,
    BlockPos? principal = null
  ) {
    var be = new BlockEntityStructureFiller {
      Principal = (principal ?? pos.AddCopy(0, -1, 0)).Copy(),
      HostedBehaviors = hosted,
    };
    Place(pos, Filler, be);
    return Initialize(be);
  }

  /// <summary>
  /// Places a footprint cell that is a graph node in its own right: a <see cref="PlaceFiller"/> cell
  /// hosting one network membership on <paramref name="networkType"/>, declared exactly as a
  /// <c>fillerOffsets</c> cell declares it - a class code, a connector face already rotated into the
  /// placed orientation, and a properties blob. <paramref name="orientation"/> is one side letter, or
  /// two naming an opposite pair for a cell a run passes straight through.
  /// </summary>
  public TestWorld PlaceFillerNode(
    BlockPos pos,
    string networkType,
    string orientation,
    BlockPos? principal = null
  ) {
    RegisterBlockEntityBehaviorFactory(
      NetworkMemberClass,
      be => new BEBehaviorNetworkMember(be)
    );
    (BlockFacing face, bool passThrough) = ReadOrientation(orientation);
    var props = new JObject { ["networkType"] = networkType };
    if (passThrough)
      props["passThrough"] = true;
    return PlaceFiller(
      pos,
      [new FillerBehavior(NetworkMemberClass, face, new JsonObject(props))],
      principal
    );
  }

  /// <summary>Reads a one- or two-letter orientation as the face a filler cell couples on plus whether
  /// the run passes through to its opposite; anything else is an authoring mistake in the fixture.</summary>
  private static (BlockFacing Face, bool PassThrough) ReadOrientation(
    string orientation
  ) {
    BlockFacing[] faces =
    [
      .. orientation
        .Select(c => BlockNetworkModSystem.SideToFace(c.ToString()))
        .OfType<BlockFacing>()
        .Distinct(),
    ];
    return faces switch {
      [BlockFacing one] => (one, false),
      [BlockFacing a, BlockFacing b] when a.Opposite == b => (a, true),
      _ => throw new ArgumentException(
        $"A filler cell couples on one face, or on two opposite ones; '{orientation}' names neither.",
        nameof(orientation)
      ),
    };
  }

  /// <summary>
  /// Registers a factory the fake class registry builds <paramref name="classname"/> from - the
  /// headless stand-in for the behaviour registry a structure filler creates its hosted behaviours
  /// through. Production fills that registry from <c>[BlockEntityBehaviorRegister]</c>.
  /// </summary>
  public TestWorld RegisterBlockEntityBehaviorFactory(
    string classname,
    System.Func<BlockEntity, BlockEntityBehavior> factory
  ) {
    Api.ClassRegistry.CreateBlockEntityBehavior(
        Arg.Any<BlockEntity>(),
        classname
      )
      .Returns(ci => factory(ci.Arg<BlockEntity>()));
    return this;
  }

  /// <summary>
  /// Registers a factory that <c>BlockAccessor.SpawnBlockEntity(classname, pos)</c> uses for
  /// <paramref name="classname"/> - the headless stand-in for the engine's class registry. The spawned
  /// entity is positioned, linked to the block at that cell, stored and <c>Initialize</c>d against this
  /// world's API.
  /// </summary>
  public TestWorld RegisterBlockEntityFactory(
    string classname,
    Func<BlockEntity> factory
  ) {
    _beFactories[classname] = factory;
    return this;
  }

  /// <summary>Registers a block in the id/code lookup without placing it (for orientation-variant swaps).</summary>
  public TestWorld Register(Block block) {
    _blocksById[block.BlockId] = block;
    if (block.Code != null)
      _blocksByCode[block.Code.ToString()] = block;
    return this;
  }

  /// <summary>
  /// Registers a resolvable <see cref="Item"/> under <paramref name="code"/> so
  /// <c>World.GetItem(code)</c> returns it. <paramref name="meltingPoint"/> (°C, 0 = none) is exposed
  /// through the item's <see cref="CombustibleProperties"/> so melt-point classification
  /// (liquid/cooling/hardened) works headlessly. Returns the created item.
  /// </summary>
  public Item RegisterItem(string code, float meltingPoint = 0f) {
    // A unique non-zero id so ItemStack.ResolveBlockOrItem (which re-resolves a cloned/loaded stack
    // by id) finds the item instead of nulling out its Collectible.
    var item = new Item {
      Code = new AssetLocation(code),
      ItemId = _nextItemId++,
    };
    if (meltingPoint > 0f)
      item.CombustibleProps = new CombustibleProperties {
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

  /// <summary>What the store holds at <paramref name="pos"/>, whether or not its chunk is loaded.
  /// <see cref="Accessor"/> reads through <see cref="ReadBlock"/> instead, which answers
  /// <see cref="Air"/> for an unloaded chunk exactly as the engine does.</summary>
  public Block GetBlock(BlockPos pos) =>
    _blocks.TryGetValue(pos, out var b) ? b : Air;

  /// <summary>What the store holds at <paramref name="pos"/>, whether or not its chunk is loaded;
  /// the accessor reads through <see cref="ReadBlockEntity"/>.</summary>
  public BlockEntity? GetBlockEntity(BlockPos pos) =>
    _blockEntities.TryGetValue(pos, out var be) ? be : null;

  #endregion

  #region Chunk loading

  private readonly HashSet<Vec3i> _unloadedChunks = new();
  private readonly IWorldChunk _loadedChunk = Substitute.For<IWorldChunk>();

  /// <summary>The chunk every loaded position resolves to. One instance for the whole world, so it
  /// answers "did anything ask a chunk to be saved again" rather than "which chunk" - enough for a
  /// <c>MarkModified</c> assertion, not enough to tell two chunks apart.</summary>
  public IWorldChunk LoadedChunk => _loadedChunk;

  /// <summary>The chunk coordinate <paramref name="pos"/> falls in, dimension-aware through
  /// <c>InternalY</c> - a mini-dimension sits above the world in internal Y, so a chunk column there
  /// must not share a key with the one below it.</summary>
  private static Vec3i ChunkOf(BlockPos pos) =>
    new(
      pos.X / GlobalConstants.ChunkSize,
      pos.InternalY / GlobalConstants.ChunkSize,
      pos.Z / GlobalConstants.ChunkSize
    );

  /// <summary>Whether the chunk holding <paramref name="pos"/> is loaded. Every cell starts loaded;
  /// <see cref="UnloadChunkAt"/> takes one chunk away.</summary>
  public bool IsChunkLoaded(BlockPos pos) =>
    !_unloadedChunks.Contains(ChunkOf(pos));

  /// <summary>
  /// Hides every cell in the chunk holding <paramref name="pos"/> from <see cref="Accessor"/>: its
  /// blocks read as <see cref="Air"/>, its block entities as <c>null</c> and
  /// <c>GetChunkAtBlockPos</c> as <c>null</c>, which is what a real unload looks like to a walk. The
  /// store is untouched, so <see cref="LoadChunkAt"/> brings the chunk back exactly as it was.
  /// </summary>
  /// <remarks>Distinct from <see cref="Unload"/>, which models the other half - one block entity
  /// running its own <c>OnBlockUnloaded</c> and being dropped - and leaves the cell readable.</remarks>
  public TestWorld UnloadChunkAt(BlockPos pos) {
    _unloadedChunks.Add(ChunkOf(pos));
    return this;
  }

  /// <summary>Brings back the chunk holding <paramref name="pos"/>. Loading a chunk that was never
  /// unloaded does nothing, so a test can call it twice.</summary>
  public TestWorld LoadChunkAt(BlockPos pos) {
    _unloadedChunks.Remove(ChunkOf(pos));
    return this;
  }

  /// <summary>What <see cref="Accessor"/> sees at <paramref name="pos"/>: the placed block, or
  /// <see cref="Air"/> when its chunk is away. The engine never returns null for an unloaded cell,
  /// which is exactly why an absent cell and an unreadable one need telling apart.</summary>
  private Block ReadBlock(BlockPos pos) =>
    IsChunkLoaded(pos) ? GetBlock(pos) : Air;

  /// <summary>What <see cref="Accessor"/> sees at <paramref name="pos"/>: the live block entity, or
  /// <c>null</c> when its chunk is away.</summary>
  private BlockEntity? ReadBlockEntity(BlockPos pos) =>
    IsChunkLoaded(pos) ? GetBlockEntity(pos) : null;

  #endregion

  #region Graph passthrough

  public void AddNode(BlockPos pos, string networkType) =>
    Networks.AddNode(Accessor, pos, networkType);

  public void RemoveNode(BlockPos pos) => Networks.RemoveNode(Accessor, pos);

  public BlockNetwork? NetworkAt(BlockPos pos) => Networks.GetNetworkAt(pos);

  #endregion

  #region Neighbours

  /// <summary>
  /// Fires <see cref="Block.OnNeighbourBlockChange"/> on the six blocks adjacent to
  /// <paramref name="changedPos"/>, as the engine does after a place, break or exchange there; empty
  /// cells resolve to <see cref="Air"/> and no-op. Opt-in, because auto-firing from <see cref="Place"/>
  /// and the accessor makes an isolated network node self-break and reorientations recurse.
  /// </summary>
  public TestWorld NotifyNeighbours(BlockPos changedPos) {
    foreach (BlockFacing face in BlockFacing.ALLFACES) {
      BlockPos nPos = changedPos.AddCopy(face);
      GetBlock(nPos).OnNeighbourBlockChange(World, nPos, changedPos);
    }
    return this;
  }

  #endregion

  #region Time

  /// <summary>
  /// Advances the simulation by <paramref name="seconds"/> server ticks (the network manager runs one
  /// tick per second) through <see cref="BlockNetworkModSystem.ServerTick"/>, so a test drives the
  /// same per-tick graph work the server does - resuming discovery a chunk suspended, then
  /// <see cref="BlockNetwork.OnTick"/> for every live network with <c>dt = 1</c>.
  /// </summary>
  public void Tick(int seconds = 1) {
    for (int i = 0; i < seconds; i++)
      Networks.ServerTick(Accessor, 1f);
  }

  /// <summary>Fires every block-entity server tick listener registered through <see cref="Api"/>
  /// (i.e. via <c>BlockEntity.RegisterGameTickListener</c>), <paramref name="times"/> times.</summary>
  public void FireBlockEntityTicks(float dt = 1f, int times = 1) {
    for (int i = 0; i < times; i++)
      foreach (var listener in _tickListeners.Values.ToList())
        listener.Callback(dt);
  }

  /// <summary>
  /// Advances block-entity sim time by <paramref name="totalMs"/> ms, firing each listener once per
  /// whole interval that elapses at the interval it registered, with <c>dt = interval / 1000</c> s.
  /// Remainders carry across calls, so two 600 ms advances cross a 1000 ms boundary once. A listener
  /// that unregisters itself mid-advance receives no further fires this call.
  /// </summary>
  public void AdvanceBlockEntityTime(int totalMs) {
    // Snapshot: a listener may unregister (or a block entity may register a new one) while firing.
    foreach (long id in _tickListeners.Keys.ToList()) {
      if (!_tickListeners.TryGetValue(id, out TickListener listener))
        continue; // already removed by an earlier callback this pass
      int interval = System.Math.Max(1, listener.IntervalMs);
      int accum = _tickAccumMs.TryGetValue(id, out int a) ? a : 0;
      accum += totalMs;
      float dt = interval / 1000f;
      while (accum >= interval && _tickListeners.ContainsKey(id)) {
        accum -= interval;
        listener.Callback(dt);
      }
      // The listener may have been torn down by its own callback; only keep live remainders.
      if (_tickListeners.ContainsKey(id))
        _tickAccumMs[id] = accum;
    }
  }

  /// <summary>Moves the calendar forward without ticking, for calendar-driven effects (evaporation).</summary>
  public void AdvanceDays(double days) {
    _totalDays += days;
    PushCalendar();
  }

  /// <summary>Moves the calendar forward by game hours without ticking (for away-catch-up tests).</summary>
  public void AdvanceHours(double hours) => AdvanceDays(hours / 24.0);

  private void PushCalendar() {
    Calendar.TotalDays.Returns(_totalDays);
    Calendar.TotalHours.Returns(_totalDays * 24.0);
  }

  #endregion

  #region Lifecycle

  /// <summary>
  /// Models a save, chunk unload and reload of the block entity at <paramref name="pos"/>: serialises
  /// its real <c>ToTreeAttributes</c> bytes, tears the live instance down (unregistering its tick
  /// listeners) while leaving the block placed, then builds a fresh instance of the same class and
  /// drives <c>FromTreeAttributes</c> then <c>Initialize</c>. Returns the new instance.
  /// </summary>
  public BlockEntity? Reload(BlockPos pos) {
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
  /// Models the block-entity half of a chunk unload at <paramref name="pos"/>: runs its real
  /// <c>OnBlockUnloaded</c> (the fake event API honours the tick-listener unregister, so it stops
  /// ticking) and drops the instance while leaving the block placed and readable. The cell itself
  /// stays visible to the accessor; <see cref="UnloadChunkAt"/> is the half that takes it away.
  /// </summary>
  public void Unload(BlockPos pos) {
    GetBlockEntity(pos)?.OnBlockUnloaded();
    _blockEntities.Remove(pos);
  }

  /// <summary>Creates a fresh block entity of the same class the engine would instantiate on load:
  /// a registered factory for the block's entity class if one exists (see
  /// <see cref="RegisterBlockEntityFactory"/>), otherwise the type's parameterless constructor.</summary>
  private BlockEntity NewBlockEntityLike(BlockEntity old, Block block) {
    string? classname = block?.EntityClass ?? old.Block?.EntityClass;
    if (
      classname != null
      && _beFactories.TryGetValue(classname, out var factory)
    )
      return factory();
    return (BlockEntity)Activator.CreateInstance(old.GetType())!;
  }

  #endregion

  #region Fake wiring

  private IBlockAccessor BuildAccessor() {
    var a = Substitute.For<IBlockAccessor>();

    a.GetBlock(Arg.Any<BlockPos>())
      .Returns(ci => ReadBlock(ci.Arg<BlockPos>()));
    // The fluid/solid-layer overload (BlockLayersAccess) reads the same store - tests that need a
    // distinct fluid layer place a block whose LiquidCode is set.
    a.GetBlock(Arg.Any<BlockPos>(), Arg.Any<int>())
      .Returns(ci => ReadBlock(ci.Arg<BlockPos>()));
    // Null for a chunk this world has taken away, a live chunk otherwise. The one call that can tell
    // an absent cell from an unreadable one, since every block read answers air for both.
    a.GetChunkAtBlockPos(Arg.Any<BlockPos>())
      .Returns(ci => IsChunkLoaded(ci.Arg<BlockPos>()) ? _loadedChunk : null);
    // Coordinate overloads, including the unchecked GetBlockRaw vanilla's multiblock code reads
    // through. Left unwired these return null and NRE inside engine code. The int overload is obsolete
    // in favour of the BlockPos one but engine code still calls it, hence the suppression.
#pragma warning disable CS0618
    a.GetBlock(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>())
      .Returns(ci =>
        ReadBlock(
          new BlockPos(ci.ArgAt<int>(0), ci.ArgAt<int>(1), ci.ArgAt<int>(2))
        )
      );
#pragma warning restore CS0618
    a.GetBlockRaw(
        Arg.Any<int>(),
        Arg.Any<int>(),
        Arg.Any<int>(),
        Arg.Any<int>()
      )
      .Returns(ci =>
        ReadBlock(
          new BlockPos(ci.ArgAt<int>(0), ci.ArgAt<int>(1), ci.ArgAt<int>(2))
        )
      );
    a.GetBlockEntity(Arg.Any<BlockPos>())
      .Returns(ci => ReadBlockEntity(ci.Arg<BlockPos>()));
    // Resolve-by-code, the same store IServerWorldAccessor.GetBlock(AssetLocation) reads. Orientation
    // behaviours swap a block to its facing variant through the accessor rather than the world
    // (CodeWithVariant -> GetBlock -> ExchangeBlock), and read a null here as an undeclared variant.
    a.GetBlock(Arg.Any<AssetLocation>())
      .Returns(ci => GetByCode(ci.Arg<AssetLocation>()));

    a.When(x => x.SetBlock(Arg.Any<int>(), Arg.Any<BlockPos>()))
      .Do(ci => DoSetBlock(ci.ArgAt<int>(0), ci.ArgAt<BlockPos>(1)));
    // The placement overload. The stack it carries seeds block-entity attributes in the engine and the
    // store has no use for it, but unwired this overload makes a real TryPlaceBlock place nothing.
    a.When(x =>
        x.SetBlock(Arg.Any<int>(), Arg.Any<BlockPos>(), Arg.Any<ItemStack>())
      )
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
      .Do(ci => DoSpawnBlockEntity(ci.ArgAt<string>(0), ci.ArgAt<BlockPos>(1)));
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
  ) {
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

  private IServerWorldAccessor BuildWorld() {
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

  private ICoreServerAPI BuildApi() {
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

    // Capture the server tick listeners block entities register, so a test can pump them via
    // FireBlockEntityTicks. The event-API overload BlockEntity.RegisterGameTickListener forwards to
    // gained a BlockPos parameter in 1.22, so only the one this game version calls is mocked.
#if GAME_GE_1_22
    events
      .RegisterGameTickListener(
        Arg.Any<System.Action<float>>(),
        Arg.Any<BlockPos>(),
        Arg.Any<System.Action<System.Exception>>(),
        Arg.Any<int>(),
        Arg.Any<int>()
      )
      .Returns(ci =>
        AddTickListener(ci.Arg<System.Action<float>>(), ci.ArgAt<int>(3))
      );
#else
    events
      .RegisterGameTickListener(
        Arg.Any<System.Action<float>>(),
        Arg.Any<System.Action<System.Exception>>(),
        Arg.Any<int>(),
        Arg.Any<int>()
      )
      .Returns(ci =>
        AddTickListener(ci.Arg<System.Action<float>>(), ci.ArgAt<int>(2))
      );
#endif

    // Honour UnregisterGameTickListener so a torn-down block entity (Reload/Unload/OnBlockRemoved)
    // stops ticking; left a no-op, a discarded block entity would go on ticking.
    events
      .When(x => x.UnregisterGameTickListener(Arg.Any<long>()))
      .Do(ci => {
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

  private long AddTickListener(System.Action<float> callback, int intervalMs) {
    long id = ++_nextListenerId;
    _tickListeners[id] = new TickListener(callback, intervalMs);
    _tickAccumMs[id] = 0;
    return id;
  }

  private void DoSetBlock(int id, BlockPos pos) {
    if (id == 0) {
      _blocks.Remove(pos);
      _blockEntities.Remove(pos);
      return;
    }
    if (!_blocksById.TryGetValue(id, out var b))
      return;
    _blocks[pos] = b;

    // Engine parity: placing a block that declares an entity class (re)creates its block entity, so a
    // caller that swaps a block and then reads its block entity finds the new block's one. Acts only
    // when a factory is registered for the class, so graph-only tests are unaffected. A block entity
    // already matching the new block is kept; a stale one is replaced.
    if (
      b.EntityClass is { } entityClass
      && (
        !_blockEntities.TryGetValue(pos, out var existing)
        || existing.Block != b
      )
    ) {
      // A block-changing SetBlock replaces the old block entity; tear the stale one down first (as
      // the engine's chunk unload does, unregistering its tick listeners) so it cannot keep ticking.
      existing?.OnBlockUnloaded();
      DoSpawnBlockEntity(entityClass, pos);
    }
  }

  private void DoSpawnBlockEntity(string classname, BlockPos pos) {
    if (!_beFactories.TryGetValue(classname, out var factory))
      return;
    var be = factory();
    be.Pos = pos.Copy();
    be.Block = GetBlock(pos);
    _blockEntities[pos] = be;
    be.Initialize(Api);
  }

  private void DoExchangeBlock(int id, BlockPos pos) {
    if (!_blocksById.TryGetValue(id, out var b))
      return;
    _blocks[pos] = b;
    if (_blockEntities.TryGetValue(pos, out var be))
      be.Block = b;
  }

  private void DoBreak(BlockPos pos) {
    // Route through the real break lifecycle so a block entity drops its contents and runs
    // OnBlockRemoved, which unregisters its tick listeners and, for a network node, calls RemoveNode.
    if (_blockEntities.TryGetValue(pos, out var be)) {
      be.OnBlockBroken();
      be.OnBlockRemoved();
    }
    _blocks.Remove(pos);
    _blockEntities.Remove(pos);
  }

  #endregion
}
