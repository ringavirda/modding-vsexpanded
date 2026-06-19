using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Networks;
using NSubstitute;
using Vintagestory.API.Common;
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

  private readonly List<System.Action<float>> _beTickCallbacks = new();

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

  /// <summary>Registers a block in the id/code lookup without placing it (for orientation-variant swaps).</summary>
  public TestWorld Register(Block block)
  {
    _blocksById[block.BlockId] = block;
    if (block.Code != null)
      _blocksByCode[block.Code.ToString()] = block;
    return this;
  }

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
      foreach (var cb in _beTickCallbacks.ToList())
        cb(dt);
  }

  /// <summary>Moves the calendar forward without ticking, for calendar-driven effects (evaporation).</summary>
  public void AdvanceDays(double days)
  {
    _totalDays += days;
    PushCalendar();
  }

  private void PushCalendar() => Calendar.TotalDays.Returns(_totalDays);

  #endregion

  #region Fake wiring

  private IBlockAccessor BuildAccessor()
  {
    var a = Substitute.For<IBlockAccessor>();

    a.GetBlock(Arg.Any<BlockPos>()).Returns(ci => GetBlock(ci.Arg<BlockPos>()));
    a.GetBlockEntity(Arg.Any<BlockPos>())
      .Returns(ci => GetBlockEntity(ci.Arg<BlockPos>()));

    a.When(x => x.SetBlock(Arg.Any<int>(), Arg.Any<BlockPos>()))
      .Do(ci => DoSetBlock(ci.ArgAt<int>(0), ci.ArgAt<BlockPos>(1)));
    a.When(x => x.ExchangeBlock(Arg.Any<int>(), Arg.Any<BlockPos>()))
      .Do(ci => DoExchangeBlock(ci.ArgAt<int>(0), ci.ArgAt<BlockPos>(1)));
    a.When(x => x.MarkBlockDirty(Arg.Any<BlockPos>())).Do(_ => { });
    a.When(x =>
        x.BreakBlock(Arg.Any<BlockPos>(), Arg.Any<IPlayer>(), Arg.Any<float>())
      )
      .Do(ci => DoBreak(ci.ArgAt<BlockPos>(0)));

    return a;
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

    // Capture the position-scoped server tick listeners block entities register, so the test can
    // pump them via FireBlockEntityTicks. (BlockEntity.RegisterGameTickListener forwards to this.)
    events
      .RegisterGameTickListener(
        Arg.Any<System.Action<float>>(),
        Arg.Any<BlockPos>(),
        Arg.Any<System.Action<System.Exception>>(),
        Arg.Any<int>(),
        Arg.Any<int>()
      )
      .Returns(ci =>
      {
        _beTickCallbacks.Add(ci.Arg<System.Action<float>>());
        return (long)_beTickCallbacks.Count;
      });

    return api;
  }

  private Block? GetByCode(AssetLocation? code) =>
    code != null && _blocksByCode.TryGetValue(code.ToString(), out var b)
      ? b
      : null;

  private void DoSetBlock(int id, BlockPos pos)
  {
    if (id == 0)
    {
      _blocks.Remove(pos);
      _blockEntities.Remove(pos);
      return;
    }
    if (_blocksById.TryGetValue(id, out var b))
      _blocks[pos] = b;
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
    _blocks.Remove(pos);
    _blockEntities.Remove(pos);
  }

  #endregion
}
