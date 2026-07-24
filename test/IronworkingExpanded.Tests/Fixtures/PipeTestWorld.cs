using ExpandedLib.Blocks.Networks;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockNetworkPipe.Blocks;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Builders shared by the pipe suite: real <see cref="BlockPipe"/> instances (the burst path
/// requires the exact type), pipe runs registered with the graph, and loose <see cref="PipeNetwork"/>
/// instances for pool/merge math that does not depend on a burst ceiling.
/// <para>
/// Lives in the iwex test project because iwex owns the pipe base block; the lpex, smex and hpex
/// suites reach it through the test-project reference chain that mirrors the mod chain.
/// </para>
/// </summary>
public static class PipeTestWorld
{
  /// <summary>Litres a single pipe holds at 1 atm - the value the production code reads from config.</summary>
  public const float LitresPerPipe = 30f;

  /// <summary>
  /// Cast (lpex) tier burst pressure, restated here because iwex cannot reference lpex. It must equal
  /// <c>LpexValues.CastPipeBurstPressure</c>; <c>LowPressureExpanded.Tests.PipeBurstParityTests</c>
  /// pins that, because a stale copy here silently retunes every burst test in three suites - which is
  /// exactly what happened when the tiers were rebalanced and this file kept the old 5/8 pair.
  /// </summary>
  public const float CastTierBurst = 5f;

  /// <summary>
  /// Rolled (hpex) tier burst pressure - same restatement, same reason. Guarded by
  /// <c>HighPressureExpanded.Tests.PipeBurstParityTests</c>.
  /// </summary>
  public const float RolledTierBurst = 12f;

  // The burst rating is per-tier now (registered per domain by each mod's ModSystem, which the headless
  // tests don't run). Seed all three shipped tiers so a pipe's BurstPressure resolves the same way it
  // does in-game. The iwex value is read straight off the live config so it cannot drift; the two above
  // iwex are the guarded constants above.
  static PipeTestWorld()
  {
    BlockPipe.RegisterBurst("iwex", () => IwexValues.BoltedPipeBurstPressure);
    BlockPipe.RegisterBurst("lpex", () => CastTierBurst);
    BlockPipe.RegisterBurst("hpex", () => RolledTierBurst);

    // The JOINT registry, seeded the same way and for the same reason. This is a separate axis from
    // pressure: bolted and cast are square and flanged so they mate with each other, rolled is
    // octagonal and welded so it mates only with itself. Unregistered, every tier falls back to
    // "flanged" and a headless test would happily couple an HP main to a bolted one - which is exactly
    // the thing the rule exists to prevent, so it has to be seeded here or the rule is untested.
    BlockPipe.RegisterJoint("iwex", BlockPipe.FlangedJoint);
    BlockPipe.RegisterJoint("lpex", BlockPipe.FlangedJoint);
    BlockPipe.RegisterJoint("hpex", BlockPipe.WeldedJoint);
  }

  /// <summary>
  /// The mod domain that owns a pipe tier. The old iron/steel <em>material</em> axis is gone - one
  /// material per mod - so these names now select a tier: <c>"iron"</c> bolted (iwex), <c>"steel"</c>
  /// cast (lpex), <c>"hadfield"</c> rolled (hpex). Which tier a fixture picks is a real choice, not
  /// decoration: it sets the burst ceiling, and a line charged past it bursts.
  /// </summary>
  public static string DomainOf(string material) =>
    material switch
    {
      "steel" => "lpex",
      "hadfield" or "rolled" => "hpex",
      _ => "iwex",
    };

  /// <summary>
  /// A real <see cref="BlockPipe"/> primed with tier/orientation. The <paramref name="material"/> names
  /// a tier (see <see cref="DomainOf"/>) since the per-material variant was dropped in favour of one
  /// material per mod. <c>OnLoaded</c>
  /// (which would parse these from variants) is skipped, so the protected <c>Type</c>/<c>Orientation</c>
  /// are set by reflection. One instance is shared across every cell of a straight run, as the engine does.
  /// </summary>
  public static BlockPipe MakePipe(
    string material = "iron",
    int id = 1,
    string orientation = "ns"
  )
  {
    string domain = DomainOf(material);
    var pipe = TestBlocks.Configure(
      new BlockPipe(),
      $"{domain}:pipe-straight-{orientation}",
      id,
      ("type", "straight"),
      ("orientation", orientation)
    );
    ReflectionHelpers.SetProperty(pipe, "Type", "straight");
    ReflectionHelpers.SetProperty(pipe, "Orientation", orientation);
    return pipe;
  }

  /// <summary>
  /// A real <see cref="BlockTuyere"/> - the furnace's blast intake, which is a pipe node wearing the
  /// <c>iwex:tuyere-*</c> code its furnace layout asks for at that cell. A generic
  /// <see cref="MakePipe"/> behaves identically as a network node but does <b>not</b> satisfy the
  /// layout, so a furnace rig standing up its real structure needs this one.
  /// </summary>
  public static BlockTuyere MakeTuyere(int id, string orientation = "n")
  {
    var tuyere = TestBlocks.Configure(
      new BlockTuyere(),
      $"iwex:tuyere-{orientation}",
      id,
      ("type", "tuyere"),
      ("orientation", orientation)
    );
    ReflectionHelpers.SetProperty(tuyere, "Type", "tuyere");
    ReflectionHelpers.SetProperty(tuyere, "Orientation", orientation);
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
  )
  {
    var world = new TestWorld();
    world.RegisterNetwork("pipe", sys => new PipeNetwork(sys));

    var pipe = MakePipe(material);
    for (int z = 0; z < length; z++)
      world.Place(new BlockPos(0, 0, z), pipe);

    if (capEnds)
    {
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
  )
  {
    var net = new PipeNetwork(system);
    for (int i = 0; i < nodeCount; i++)
      net.Nodes.Add(new BlockPos(0, 0, baseZ + i));
    return net;
  }
}
