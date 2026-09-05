using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Networks;
using ExpandedLib.Processes;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Forming;
using IronIndustryExpanded.BlockStructures.Forming.BlockEntities;
using IronIndustryExpanded.BlockStructures.Forming.Blocks;
using IronIndustryExpanded.Items;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The two fastener benches: one machine, two dies, and the end of the two routes the mill and the shear
/// leave hanging. Until these existed, nail plate and rivet rod were products nothing consumed.
/// <para>
/// The die is the subject of most of what follows. It names the job and the machine, so it is what
/// makes a bench a nail cutter rather than a riveter - and it is why the block reads its own key off a
/// variant instead of carrying one in code.
/// </para>
/// </summary>
public class FastenerBenchTests {
  #region Harness

  private const string NailPlate = "iiex:nailplate";
  private const string RivetRod = "iiex:rivetrod";
  private const string Nails = "game:metalnailsandstrips-iron";
  private const string Rivet = "iiex:rivet";

  private static (
    TestWorld World,
    BlockEntityFastenerBench Bench,
    BlockPos Pos
  ) Bench(string type = BlockFastenerBench.NailCutter) {
    var world = new TestWorld();
    world.RegisterNetwork("mpenergy", n => new MpEnergyNetwork(n));
    foreach (string code in new[] { NailPlate, RivetRod, Nails, Rivet })
      world.RegisterItem(code);

    Block block = TestBlocks.Configure(
      new BlockFastenerBench(),
      $"iiex:forming-{type}-ns",
      1,
      ("type", type),
      ("orientation", "ns")
    );
    var pos = new BlockPos(0, 1, 0);
    var bench = new BlockEntityFastenerBench();
    world.Place(pos, block, bench);
    world.Initialize(bench);
    return (world, bench, pos);
  }

  /// <summary>
  /// A die carrying one job, built the way a mod's own die is - through <see cref="ItemDie.Job"/>, so the
  /// attribute under test is the one the shipped definitions emit and not a hand-written stand-in.
  /// </summary>
  private static ItemStack Die(
    TestWorld world,
    string machine,
    string input,
    string output,
    int count,
    double minTorque = 0,
    double seconds = 1
  ) {
    Item item = world.RegisterItem($"iiex:die-{machine}-{input.GetHashCode()}");
    item.Attributes = new JsonObject(
      JToken.FromObject(
        ItemDie.Job(
          machine,
          input,
          output,
          count: count,
          minTorque: minTorque,
          seconds: seconds
        )
      )
    );
    return new ItemStack(item);
  }

  private static ItemStack NailDie(TestWorld world, double minTorque = 0) =>
    Die(
      world,
      BenchDieItemDefinitions.NailMachine,
      NailPlate,
      Nails,
      BenchDieItemDefinitions.NailsPerPlate,
      minTorque
    );

  private static ItemStack RivetDie(TestWorld world) =>
    Die(
      world,
      BenchDieItemDefinitions.RivetMachine,
      RivetRod,
      Rivet,
      FastenerItemDefinitions.RivetsPerRod
    );

  private static ItemStack Stack(TestWorld world, string code) =>
    new(world.GetItem(new AssetLocation(code))!);

  private static void Turning(
    TestWorld world,
    BlockPos pos,
    float speed = 1f,
    float supplyPower = 100f
  ) =>
    ((MpEnergyNetwork)world.NetworkAt(pos)!).RestoreState(
      new MpEnergyNetworkState {
        Speed = speed,
        Inertia = 10f,
        SupplyPower = supplyPower,
      }
    );

  private static void Stopped(TestWorld world, BlockPos pos) =>
    Turning(world, pos, speed: 0f, supplyPower: 0f);

  // Drives the stroke in the 250 ms steps the production clock uses, so a test walks the same path the
  // machine does rather than jumping the whole duration in one call.
  private static void Advance(BlockEntityFastenerBench bench, float seconds) {
    for (float t = 0; t < seconds; t += 0.25f)
      bench.AdvanceStroke(0.25f);
  }

  #endregion

  #region The mass ledger

  /// <summary>
  /// The nail route conserves iron exactly: a plate is worth four bundles because vanilla's bundle is 25 u
  /// and a plate is 100. Asserted as arithmetic rather than as the literal 4, which would still pass if the
  /// plate's mass moved and would say nothing about whether metal was minted.
  /// </summary>
  [Fact]
  public void A_nail_plate_is_worth_exactly_its_own_metal_in_bundles() {
    const int VanillaBundleUnits = 25;
    int plate = RolledItemDefinitions.UnitsOf("nailplate");

    Assert.Equal(
      plate,
      BenchDieItemDefinitions.NailsPerPlate * VanillaBundleUnits
    );
  }

  /// <summary>The rivet route conserves it too - a rod upsets into bundles worth the rod, nothing added for
  /// the head and nothing lost to it.</summary>
  [Fact]
  public void A_rivet_rod_is_worth_exactly_its_own_metal_in_bundles() {
    Assert.Equal(
      RolledItemDefinitions.UnitsOf("rivetrod"),
      FastenerItemDefinitions.RivetsPerRod * FastenerItemDefinitions.RivetUnits,
      3
    );
  }

  /// <summary>
  /// The trade the whole fastener design turns on: a rivet route yields strictly more fasteners per unit of
  /// iron than a nail route, and pays for it with a second machine and a longer schedule at the mill. If
  /// this ever reads equal, the riveter has no reason to exist.
  /// </summary>
  [Fact]
  public void The_rivet_route_out_yields_the_nail_route_per_unit_of_iron() {
    const float VanillaBundleUnits = 25f;
    float nailsPer100 = 100f / VanillaBundleUnits;
    float rivetsPer100 = 100f / FastenerItemDefinitions.RivetUnits;

    Assert.True(
      rivetsPer100 > nailsPer100,
      $"rivets {rivetsPer100}/100 u do not beat nails {nailsPer100}/100 u"
    );
  }

  #endregion

  #region The die names the machine

  /// <summary>
  /// The generalisation, stated directly: which bench this is comes off the block's <c>type</c> variant.
  /// One hardcoded key is all it would take to make the class the nail cutter's alone.
  /// </summary>
  [Theory]
  [InlineData(BlockFastenerBench.NailCutter)]
  [InlineData(BlockFastenerBench.Riveter)]
  public void The_bench_reads_its_machine_key_off_its_own_variant(string type) {
    var (_, bench, _) = Bench(type);

    Assert.Equal(type, bench.MachineKey);
  }

  /// <summary>
  /// A nail die in a riveter is refused. Without the machine check the die's job would match on the input
  /// alone and one bench would silently do the other's work - which would make the second machine
  /// decorative.
  /// </summary>
  [Fact]
  public void A_die_for_another_machine_gives_this_one_no_job() {
    var (world, bench, pos) = Bench(BlockFastenerBench.Riveter);
    Turning(world, pos);
    Assert.True(bench.TryFitDie(NailDie(world), out _));

    Assert.Null(bench.JobFor(Stack(world, NailPlate)));
    Assert.Equal(
      BenchVerdict.NoJob,
      bench.TryPress(Stack(world, NailPlate)).Verdict
    );
  }

  /// <summary>The control: the same die in the bench it was cut for does have the job.</summary>
  [Fact]
  public void A_die_for_this_machine_gives_it_the_job() {
    var (world, bench, pos) = Bench();
    Turning(world, pos);
    Assert.True(bench.TryFitDie(NailDie(world), out _));

    ProcessJob? job = bench.JobFor(Stack(world, NailPlate));

    Assert.NotNull(job);
    Assert.Equal(Nails, job!.Output);
    Assert.Equal(BenchDieItemDefinitions.NailsPerPlate, job.Count);
  }

  #endregion

  #region What the press refuses

  [Fact]
  public void A_bare_bench_has_no_work_at_all() {
    var (world, bench, pos) = Bench();
    Turning(world, pos);

    Assert.False(bench.HasDie);
    Assert.Equal(
      BenchVerdict.NoDie,
      bench.TryPress(Stack(world, NailPlate)).Verdict
    );
  }

  [Fact]
  public void A_blank_the_fitted_die_does_not_take_is_refused() {
    var (world, bench, pos) = Bench();
    Turning(world, pos);
    bench.TryFitDie(NailDie(world), out _);

    Assert.Equal(
      BenchVerdict.NoJob,
      bench.TryPress(Stack(world, RivetRod)).Verdict
    );
  }

  /// <summary>
  /// A stopped run reports as stopped rather than as short of drive: no amount of stored torque starts a
  /// stroke, and the two have different fixes.
  /// </summary>
  [Fact]
  public void A_bench_on_a_stopped_run_refuses_the_stroke() {
    var (world, bench, pos) = Bench();
    Stopped(world, pos);
    bench.TryFitDie(NailDie(world), out _);

    Assert.Equal(
      BenchVerdict.NotTurning,
      bench.TryPress(Stack(world, NailPlate)).Verdict
    );
  }

  [Fact]
  public void A_run_too_weak_for_the_job_refuses_the_stroke() {
    var (world, bench, pos) = Bench();
    // A turning run putting out far less torque than the die asks: P/omega = 1 against a gate of 5.
    Turning(world, pos, speed: 1f, supplyPower: 1f);
    bench.TryFitDie(NailDie(world, minTorque: 5), out _);

    Assert.Equal(
      BenchVerdict.NotEnoughDrive,
      bench.TryPress(Stack(world, NailPlate)).Verdict
    );
  }

  #endregion

  #region The stroke

  [Fact]
  public void A_nail_plate_becomes_its_bundles_and_nothing_comes_back() {
    var (world, bench, pos) = Bench();
    Turning(world, pos);
    bench.TryFitDie(NailDie(world), out _);
    world.Drops.Clear();

    Assert.True(bench.TryPress(Stack(world, NailPlate)).Accepted);
    Assert.True(bench.IsStroking);
    Advance(bench, 2f);

    Assert.False(bench.IsStroking);
    // One drop, and it is the whole yield: a whole-item job converts, so there is no remainder to come
    // back the way a crop's does.
    ItemStack drop = Assert.Single(world.Drops);
    Assert.Equal(Nails, drop.Collectible.Code.ToString());
    Assert.Equal(BenchDieItemDefinitions.NailsPerPlate, drop.StackSize);
  }

  [Fact]
  public void A_rivet_rod_becomes_its_bundles() {
    var (world, bench, pos) = Bench(BlockFastenerBench.Riveter);
    Turning(world, pos);
    bench.TryFitDie(RivetDie(world), out _);
    world.Drops.Clear();

    Assert.True(bench.TryPress(Stack(world, RivetRod)).Accepted);
    Advance(bench, 2f);

    ItemStack drop = Assert.Single(world.Drops);
    Assert.Equal(Rivet, drop.Collectible.Code.ToString());
    Assert.Equal(FastenerItemDefinitions.RivetsPerRod, drop.StackSize);
  }

  /// <summary>A stroke is drawn from a turning run, so a run that stops holds the press where it is rather
  /// than finishing the job.</summary>
  [Fact]
  public void A_stalled_run_holds_the_press_without_losing_the_blank() {
    var (world, bench, pos) = Bench();
    Turning(world, pos);
    bench.TryFitDie(NailDie(world), out _);
    bench.TryPress(Stack(world, NailPlate));
    world.Drops.Clear();

    Stopped(world, pos);
    Advance(bench, 10f);

    Assert.True(bench.IsStroking);
    Assert.Empty(world.Drops);
  }

  /// <summary>The wrench recovery: a blank under a dead press comes back unchanged.</summary>
  [Fact]
  public void A_stuck_blank_can_be_taken_back_out() {
    var (world, bench, pos) = Bench();
    Turning(world, pos);
    bench.TryFitDie(NailDie(world), out _);
    bench.TryPress(Stack(world, NailPlate));

    ItemStack? freed = bench.ReleaseStuckBlank();

    Assert.NotNull(freed);
    Assert.Equal(NailPlate, freed!.Collectible.Code.ToString());
    Assert.False(bench.IsStroking);
  }

  /// <summary>The tooling cannot change with a blank under it, or a swap would eat the blank.</summary>
  [Fact]
  public void A_die_cannot_be_swapped_mid_stroke() {
    var (world, bench, pos) = Bench();
    Turning(world, pos);
    bench.TryFitDie(NailDie(world), out _);
    bench.TryPress(Stack(world, NailPlate));

    Assert.False(bench.TryFitDie(RivetDie(world), out ItemStack? previous));
    Assert.Null(previous);
    Assert.True(bench.HasDie);
  }

  #endregion

  #region Break safety

  [Fact]
  public void Breaking_the_bench_returns_the_die_and_any_blank() {
    var (world, bench, pos) = Bench();
    Turning(world, pos);
    bench.TryFitDie(NailDie(world), out _);
    bench.TryPress(Stack(world, NailPlate));
    world.Drops.Clear();

    bench.OnBlockBroken();

    Assert.Equal(2, world.Drops.Count);
    Assert.False(bench.HasDie);
  }

  #endregion

  #region The shipped tooling

  /// <summary>
  /// The dies this mod ships name the machines this mod builds. A die whose <c>machine</c> matched nothing
  /// would be tooling no bench accepts, and nothing else would notice.
  /// </summary>
  [Fact]
  public void Every_shipped_die_names_a_bench_that_exists() {
    string[] benches =
    [
      BlockFastenerBench.NailCutter,
      BlockFastenerBench.Riveter,
    ];

    ExItemDef def = BenchDieItemDefinitions.Definitions("iiex").Single();
    JObject byType = (JObject)def.ToJson()["attributesByType"]!;

    Assert.NotEmpty(byType.Properties());
    foreach (JProperty variant in byType.Properties()) {
      Assert.True(
        ItemDie.TryParse(
          new JsonObject(variant.Value)[ItemDie.AttributeKey],
          out ProcessJobSet? set,
          out string? error
        ),
        $"{variant.Name}: {error}"
      );
      Assert.Contains(set!.Machine, benches);
    }
  }

  #endregion
}
