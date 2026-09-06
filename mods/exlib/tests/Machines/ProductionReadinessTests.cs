using System.Linq;
using ExpandedLib.Blocks;
using ExpandedLib.Machines;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
#if GAME_GE_1_22
using Vintagestory.GameContent;
#endif

namespace ExpandedLib.Tests;

/// <summary>A readiness answer hung on a machine as a behaviour, standing in for an axis the machine's
/// own class does not carry - a construction stage, a resolved peer.</summary>
internal sealed class TestReadinessPublisher(BlockEntity blockentity)
  : BlockEntityBehavior(blockentity),
    IProductionReadiness {
  public bool Ready = true;
  public bool Stops = true;

  public bool IsReadyToProduce => Ready;

  public bool StopsProductionWhenNotReady => Stops;
}

/// <summary>A block entity that publishes no readiness at all, as a plain block does.</summary>
internal sealed class TestPlainBlockEntity : BlockEntity { }

/// <summary>A machine that hosts a real <see cref="ExRightClickConstructable"/>, as the shipped
/// mega-blocks (boiler, engine, transmission) declare it from their JSON definition, so the
/// behaviour's own readiness answer is exercised through the publisher path rather than an
/// ad hoc forwarder. The `_rcc` field name matches what <see cref="RccFake"/> primes.</summary>
internal sealed class TestConstructedMachine : BlockEntityProductionMachine {
  // Set only through reflection (RccFake.Complete), never by compiled code - the same pattern
  // PersistAttributeTests' [Persist] fixtures use for a field PersistScan reaches by name.
#pragma warning disable CS0169 // field is set only via reflection, never by name
  private ExRightClickConstructable? _rcc;
#pragma warning restore CS0169
  public int ProductionTicks;
  public int IdleTicks;

  // Construction now publishes its own gate; nothing else on this machine does.
  protected override bool CanRunProduction => true;

  protected override void OnProductionTick(float dt) => ProductionTicks++;

  protected override void OnIdleProductionTick(float dt) => IdleTicks++;
}

/// <summary>A multiblock whose response to losing its pattern is set per test.</summary>
internal sealed class TestBreachMegablock : BlockEntityMultiblockMachine {
  public bool KeepsTicking;
  public int ProductionTicks;
  public int IdleTicks;

  protected override bool StopsProductionOnStructureLost => !KeepsTicking;

  protected override void UpdateStructureRotation() => SetStructureAngle(0);

  protected override void OnProductionTick(float dt) => ProductionTicks++;

  protected override void OnIdleProductionTick(float dt) => IdleTicks++;

  protected override string GetIncompleteMessage(int missingCount) =>
    $"missing {missingCount}";

  protected override string GetCompleteMessage() => "complete";
}

/// <summary>
/// The readiness contract: whatever knows publishes, the process reads. Every publisher on a machine
/// must agree before production runs, and each says separately whether losing readiness closes the gate
/// or takes the tick away with it. See docs/design/mechanics/framework-composition.md.
/// </summary>
public class ProductionReadinessTests {
  private static (TestWorld world, TestProductionMachine machine) NewMachine() {
    var world = new TestWorld();
    var machine = new TestProductionMachine { Pos = new BlockPos(0, 0, 0) };
    world.Attach(machine);
    return (world, machine);
  }

  private static ExBlockDef Def() =>
    ExBlockDef
      .Create("exlib", "testmega")
      .Multiblock(m =>
        m.Number("exlib:testmega*", 1)
          .Number("exlib:testbrick*", 2)
          .At(0, 0, 0, 1)
          .At(1, 0, 0, 2)
          .At(-1, 0, 0, 2)
          .At(0, 0, 1, 2)
          .At(0, 0, -1, 2)
      );

  private static (TestWorld world, TestBreachMegablock machine) Breached(
    bool keepsTicking
  ) {
    var world = new TestWorld();
    var machine = new TestBreachMegablock { KeepsTicking = keepsTicking };
    world.Place(
      new BlockPos(0, 10, 0),
      TestBlocks.Configure(new Block(), "exlib:testmega-n", 1),
      machine
    );
    world.Attach(machine);
    StructureRig.Around(world, machine, Def()).Complete();

    // The premise for every assertion below: a complete machine that has not run a tick yet, so any
    // count afterwards was produced by the breach.
    Assert.True(machine.StructureComplete);
    Assert.Equal(0, machine.ProductionTicks);
    Assert.Equal(0, machine.IdleTicks);

    world.Accessor.SetBlock(0, new BlockPos(0, 10, -1));
    return (world, machine);
  }

  #region Who publishes

  [Fact]
  public void A_machine_publishes_its_own_gate_as_readiness() {
    var (_, machine) = NewMachine();

    // The premise: the machine is a publisher at all, so the answer below is its own rather than the
    // empty-set default.
    Assert.Single(ProductionReadiness.PublishersOn(machine));
    Assert.True(ProductionReadiness.IsReady(machine));

    machine.Operational = false;
    Assert.False(ProductionReadiness.IsReady(machine));
  }

  [Fact]
  public void Readiness_answers_before_the_machine_is_initialized() {
    var machine = new TestProductionMachine { Operational = false };

    // The load path reads readiness from inside Initialize, so a publisher that needed an api, a
    // neighbour or a network would answer wrong exactly when the machine decides whether to tick.
    Assert.Null(machine.Api);
    Assert.False(ProductionReadiness.IsReady(machine));
  }

  [Fact]
  public void A_block_entity_that_publishes_nothing_is_ready() {
    Assert.Empty(ProductionReadiness.PublishersOn(new TestPlainBlockEntity()));
    Assert.True(ProductionReadiness.IsReady(new TestPlainBlockEntity()));
    Assert.True(ProductionReadiness.IsReady(null));
  }

  #endregion

  #region The gate is every publisher at once

  [Fact]
  public void Every_publisher_must_agree_before_production_runs() {
    var (world, machine) = NewMachine();
    var second = new TestReadinessPublisher(machine) { Ready = false };
    machine.Behaviors.Add(second);
    machine.StartTicking();

    // The premise: the machine's own gate is open, so only the second publisher can close this one.
    Assert.True(machine.Operational);
    world.FireBlockEntityTicks();
    Assert.Equal(0, machine.ProductionTicks);
    Assert.Equal(1, machine.IdleTicks);

    second.Ready = true;
    world.FireBlockEntityTicks();
    Assert.Equal(1, machine.ProductionTicks);
  }

  #endregion

  #region Closing the gate versus taking the tick away

  [Fact]
  public void One_publisher_that_keeps_ticking_holds_the_tick_for_the_machine() {
    var (_, machine) = NewMachine();
    var second = new TestReadinessPublisher(machine);
    machine.Behaviors.Add(second);

    Assert.True(ProductionReadiness.StopsProductionWhenNotReady(machine));

    second.Stops = false;
    Assert.False(ProductionReadiness.StopsProductionWhenNotReady(machine));
  }

  [Theory]
  [InlineData(true)] // keeps its listener, so it idles through the breach
  [InlineData(false)] // gives its listener up, so nothing runs at all
  public void A_breach_takes_the_tick_only_from_a_machine_that_says_so(
    bool keepsTicking
  ) {
    var (world, machine) = Breached(keepsTicking);

    world.AdvanceBlockEntityTime(3000);
    Assert.False(machine.StructureComplete);

    machine.ProductionTicks = 0;
    machine.IdleTicks = 0;
    world.AdvanceBlockEntityTime(1000);

    Assert.Equal(0, machine.ProductionTicks);
    Assert.Equal(keepsTicking, machine.IdleTicks > 0);
  }

  [Fact]
  public void A_completeness_recheck_leaves_a_machine_that_keeps_running_ticking() {
    var (world, machine) = Breached(keepsTicking: true);

    // Interact recomputes completion itself, so it reaches the same transition the monitor tick does
    // and must answer the same question. The shipped route only ever hands it an already-incomplete
    // anchor, so this arm is reached from here rather than from play.
    machine.Interact(null!);
    Assert.False(machine.StructureComplete);

    machine.IdleTicks = 0;
    world.AdvanceBlockEntityTime(1000);

    Assert.True(machine.IdleTicks > 0);
  }

  #endregion

  #region Construction gates production

  /// <summary>
  /// Adds a real <see cref="ExRightClickConstructable"/> to <paramref name="machine"/>, primed with two
  /// stages and none completed, and registers it as a publisher. Mirrors the field <see cref="RccFake"/>
  /// primes on the real mega-blocks (see its own remarks on `rcc` vs the reimplementation's field).
  /// </summary>
  private static ExRightClickConstructable AddIncompleteRcc(
    TestConstructedMachine machine
  ) {
    var rcc = new ExRightClickConstructable(machine);
#if GAME_GE_1_22
    ReflectionHelpers.SetField(
      rcc,
      "rcc",
      new RightClickConstruction {
        Stages = [
          new Vintagestory.GameContent.ConstructionStage(),
          new Vintagestory.GameContent.ConstructionStage(),
        ],
        CurrentCompletedStage = 0,
      }
    );
#else
    ReflectionHelpers.SetField(
      rcc,
      "rcc",
      new ExRightClickConstruction {
        Stages = [new ExConstructionStage(), new ExConstructionStage()],
        CurrentCompletedStage = 0,
      }
    );
#endif
    machine.Behaviors.Add(rcc);
    return rcc;
  }

  [Fact]
  public void A_machine_hosting_an_incomplete_construction_is_not_ready_and_stops() {
    var machine = new TestConstructedMachine { Pos = new BlockPos(0, 0, 0), Block = new Block() };
    AddIncompleteRcc(machine);

    Assert.False(ProductionReadiness.IsReady(machine));
    Assert.True(ProductionReadiness.StopsProductionWhenNotReady(machine));
  }

  [Fact]
  public void A_machine_hosting_a_completed_construction_is_ready() {
    var machine = new TestConstructedMachine { Pos = new BlockPos(0, 0, 0), Block = new Block() };

    // RccFake primes the machine's own `_rcc` field; the behaviour it plants there is added to
    // Behaviors afterward so the readiness scan and the fake share the identical instance.
    RccFake.Complete(machine);
    var rcc = (ExRightClickConstructable)
      ReflectionHelpers.GetField(machine, "_rcc")!;
    machine.Behaviors.Add(rcc);

    Assert.True(ProductionReadiness.IsReady(machine));
    Assert.True(ProductionReadiness.StopsProductionWhenNotReady(machine));
  }

  [Fact]
  public void GatesProduction_false_neither_blocks_nor_stops() {
    var machine = new TestConstructedMachine { Pos = new BlockPos(0, 0, 0), Block = new Block() };
    ExRightClickConstructable rcc = AddIncompleteRcc(machine);
    ReflectionHelpers.SetProperty(rcc, nameof(rcc.GatesProduction), false);

    Assert.True(ProductionReadiness.IsReady(machine));
    Assert.False(ProductionReadiness.StopsProductionWhenNotReady(machine));
  }

  #endregion
}
