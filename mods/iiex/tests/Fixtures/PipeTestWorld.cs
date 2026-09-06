using ExpandedLib.Networks;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Builders shared by the pipe suite: real <see cref="BlockPipe"/> instances (the burst path requires
/// the exact type), pipe runs registered with the graph, and loose <see cref="PipeNetwork"/> instances
/// for pool and merge math that does not depend on a burst ceiling. Lives in the iiex test project
/// because iiex owns the pipe base block; the iiex, smex and hpex suites reach it through the
/// test-project reference chain that mirrors the mod chain.
/// </summary>
public static class PipeTestWorld {
  /// <summary>Litres a single pipe holds at 1 atm, matching the value production reads from config.</summary>
  public const float LitresPerPipe = 30f;

  /// <summary>
  /// Cast (iiex) tier burst pressure (atm), restated here because iiex cannot reference iiex. Must
  /// equal <c>IiexValues.CastPipeBurstPressure</c>, which
  /// <c>IronIndustryExpanded.Tests.PipeBurstParityTests</c> pins: a stale copy here retunes every burst
  /// test in three suites.
  /// </summary>
  public const float CastTierBurst = 5f;

  /// <summary>
  /// Rolled (hpex) tier burst pressure (atm), restated for the same reason and guarded by
  /// <c>HighPressureExpanded.Tests.PipeBurstParityTests</c>.
  /// </summary>
  public const float RolledTierBurst = 12f;

  /// <summary>
  /// Cast (iiex) tier throughput (L/s), restated for the same reason as <see cref="CastTierBurst"/> and
  /// guarded by the same parity test. Must equal <c>IiexValues.CastPipeThroughput</c>.
  /// </summary>
  public const float CastTierThroughput = 120f;

  /// <summary>
  /// Rolled (hpex) tier throughput (L/s), same restatement and guard. Must equal
  /// <c>HpexValues.RolledPipeThroughput</c>.
  /// </summary>
  public const float RolledTierThroughput = 250f;

  // Burst ratings are per-tier, registered by each mod's ModSystem, which the headless tests do not
  // run. All three shipped tiers are seeded here so a pipe's BurstPressure resolves as it does in game.
  // The iiex value is read off the live config; the other two are the guarded constants above.
  static PipeTestWorld() {
    BlockPipe.RegisterBurst(
      BlockPipe.PlatedTier,
      () => IiexValues.PlatedPipeBurstPressure
    );
    BlockPipe.RegisterBurst(BlockPipe.CastTier, () => CastTierBurst);
    BlockPipe.RegisterBurst(BlockPipe.RolledTier, () => RolledTierBurst);

    // Throughput is a third axis, independent of the other two: burst is how hard a run can be
    // pressurised, LitresPerPipe is how much it holds, this is how much it passes per second.
    // Unregistered, every tier falls back to one default and no tier can refuse a line.
    BlockPipe.RegisterThroughput(
      BlockPipe.PlatedTier,
      () => IiexValues.PlatedPipeThroughput
    );
    BlockPipe.RegisterThroughput(BlockPipe.CastTier, () => CastTierThroughput);
    BlockPipe.RegisterThroughput(
      BlockPipe.RolledTier,
      () => RolledTierThroughput
    );

    // Joints are a further axis, separate from pressure: plated and cast are square and flanged so
    // they mate with each other, rolled is octagonal and welded so it mates only with itself.
    // Unregistered, every tier falls back to "flanged" and an HP main couples to a plated one.
    BlockPipe.RegisterJoint(BlockPipe.PlatedTier, BlockPipe.FlangedJoint);
    BlockPipe.RegisterJoint(BlockPipe.CastTier, BlockPipe.FlangedJoint);
    BlockPipe.RegisterJoint(BlockPipe.RolledTier, BlockPipe.WeldedJoint);
  }

  /// <summary>
  /// Fills <paramref name="net"/> with gas until it stops accepting, as blowers do over a few seconds
  /// in game. Use this rather than a single large <c>TryProduceGas</c> call: under the throughput gate
  /// one call moves at most the weakest segment's litres per second, so a single call fills the run to
  /// its throughput rather than to its pressure ceiling.
  /// </summary>
  public static void Saturate(
    PipeNetwork net,
    float temperature,
    string gasType,
    IBlockAccessor accessor,
    float maxOutputPressure = 1f,
    bool bypassLeakCap = false
  ) {
    // Bounded so a run that refuses everything, or one that accepts a trickle for ever while leaking,
    // still terminates. 512 passes is more than any shipped run needs to reach its ceiling.
    for (int i = 0; i < 512; i++) {
      float before = net.State?.Volume ?? 0f;
      net.TryProduceGas(
        float.MaxValue,
        temperature,
        gasType,
        accessor,
        maxOutputPressure,
        bypassLeakCap
      );
      if ((net.State?.Volume ?? 0f) - before <= 0.0001f)
        return;
    }
  }

  /// <summary>
  /// The pipe tier a material name selects: <c>"iron"</c> plated, <c>"steel"</c> cast,
  /// <c>"hadfield"</c> rolled. The tier sets the burst ceiling, so a line charged past it bursts, and
  /// it is what the three registries above are keyed on.
  /// </summary>
  public static string TierOf(string material) =>
    material switch {
      "steel" => BlockPipe.CastTier,
      "hadfield" or "rolled" => BlockPipe.RolledTier,
      _ => BlockPipe.PlatedTier,
    };

  /// <summary>
  /// The mod domain that ships a tier. Distinct from <see cref="TierOf"/>: a code still carries a
  /// domain, but nothing about a pipe's behaviour is resolved from it any more.
  /// </summary>
  public static string DomainOf(string material) =>
    material switch {
      "steel" => "iiex",
      "hadfield" or "rolled" => "hpex",
      _ => "iiex",
    };

  /// <summary>
  /// A real <see cref="BlockPipe"/> primed with tier and orientation; <paramref name="material"/> names
  /// a tier (see <see cref="TierOf"/>). <c>OnLoaded</c>, which would parse these from the variants, is
  /// skipped, so the protected <c>Type</c> and <c>Orientation</c> are set by reflection. The
  /// <c>tier</c> variant needs no such help - <see cref="BlockPipe.Tier"/> reads it live, which is why
  /// a fixture that sets only the code would silently get the default rating. One instance is shared
  /// across every cell of a straight run, as the engine does.
  /// </summary>
  public static BlockPipe MakePipe(
    string material = "iron",
    int id = 1,
    string orientation = "ns"
  ) {
    string tier = TierOf(material);
    var pipe = TestBlocks.Configure(
      new BlockPipe(),
      $"{DomainOf(material)}:pipe-{tier}-straight-{orientation}",
      id,
      ("tier", tier),
      ("type", "straight"),
      ("orientation", orientation)
    );
    pipe.SetNetworkTypeForTest("straight");
    pipe.ApplyOrientationForTest(orientation);
    return pipe;
  }

  /// <summary>
  /// A real <see cref="BlockTuyere"/>, the furnace's blast intake: a pipe node carrying the
  /// <c>iiex:furnace-tuyere-*</c> code the furnace layout asks for at that cell. <see cref="MakePipe"/>
  /// behaves identically as a network node but does not satisfy the layout, so a furnace rig standing
  /// up its real structure needs this one.
  /// </summary>
  public static BlockTuyere MakeTuyere(int id, string orientation = "n") {
    var tuyere = TestBlocks.Configure(
      new BlockTuyere(),
      $"iiex:furnace-tuyere-{orientation}",
      id,
      ("type", "tuyere"),
      ("orientation", orientation)
    );
    tuyere.SetNetworkTypeForTest("tuyere");
    tuyere.ApplyOrientationForTest(orientation);
    return tuyere;
  }

  /// <summary>
  /// Builds a straight pipe run of <paramref name="length"/> cells along +Z and registers it as
  /// one network. With <paramref name="capEnds"/> the two open ends are butted against a solid
  /// (non-air) block, so the run is sealed and a gas can build pressure instead of leaking.
  /// </summary>
  public static (TestWorld world, PipeNetwork net) Run(
    int length,
    string material = "iron",
    bool capEnds = false
  ) {
    var world = new TestWorld();
    world.RegisterNetwork("pipe", sys => new PipeNetwork(sys));

    var pipe = MakePipe(material);
    for (int z = 0; z < length; z++)
      world.Place(new BlockPos(0, 0, z), pipe);

    if (capEnds) {
      var rock = TestBlocks.Configure(new Block(), "game:rock", 99);
      world.Place(new BlockPos(0, 0, -1), rock);
      world.Place(new BlockPos(0, 0, length), rock);
    }

    for (int z = 0; z < length; z++)
      world.AddNode(new BlockPos(0, 0, z), "pipe");

    var net = (PipeNetwork)world.NetworkAt(new BlockPos(0, 0, 0))!;
    return (world, net);
  }

  /// <summary>
  /// Like <see cref="Run"/>, but every cell also carries a real <see cref="BlockEntityPipe"/>. Required
  /// for anything that depends on the tick's node classification: <see cref="Run"/> places blocks with
  /// no block entities, and <c>PipeNetwork.ClassifyOpenings</c> counts a consumer per node whose block
  /// entity is an <c>IPipeNode</c>, so on a bare run that count is 0 where in game it equals the
  /// segment count.
  /// </summary>
  public static (TestWorld world, PipeNetwork net) LiveRun(
    int length,
    string material = "iron",
    bool capEnds = false
  ) {
    var world = new TestWorld();
    world.RegisterNetwork("pipe", sys => new PipeNetwork(sys));

    var pipe = MakePipe(material);
    for (int z = 0; z < length; z++) {
      var pos = new BlockPos(0, 0, z);
      var be = new BlockEntityPipe { Pos = pos.Copy(), Block = pipe };
      world.Place(pos, pipe, be);
      world.Attach(be);
    }

    if (capEnds) {
      var rock = TestBlocks.Configure(new Block(), "game:rock", 99);
      world.Place(new BlockPos(0, 0, -1), rock);
      world.Place(new BlockPos(0, 0, length), rock);
    }

    for (int z = 0; z < length; z++)
      world.AddNode(new BlockPos(0, 0, z), "pipe");

    var net = (PipeNetwork)world.NetworkAt(new BlockPos(0, 0, 0))!;
    return (world, net);
  }

  /// <summary>
  /// A bare <see cref="PipeNetwork"/> with <paramref name="nodeCount"/> phantom nodes (no blocks),
  /// so <c>MaxVolume = nodeCount * LitresPerPipe</c> but there is no burst ceiling. For pool and
  /// merge math that does not involve over-pressure.
  /// </summary>
  public static PipeNetwork LooseNet(
    BlockNetworkModSystem system,
    int nodeCount,
    int baseZ = 0
  ) {
    var net = new PipeNetwork(system);
    for (int i = 0; i < nodeCount; i++)
      net.Nodes.Add(new BlockPos(0, 0, baseZ + i));
    return net;
  }
}
