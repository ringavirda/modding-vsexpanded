using ExpandedLib.Blocks;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The right-click-construction wiring exlib owns on top of vanilla's own
/// <c>BEBehaviorRightClickConstructable</c>: the JSON-driven <see cref="ExRightClickConstructable.GatesProduction"/>
/// flag and the readiness it derives, the per-domain salvage fraction in <see cref="ExRccSettings"/>,
/// and <see cref="ConstructedAnimator.IsConstructed"/> following the resolved behavior.
/// </summary>
public class ConstructionTests {
  /// <summary>Minimal concrete block entity: only used to host a behavior under test.</summary>
  private sealed class StubBlockEntity : BlockEntity;

  #region GatesProduction / readiness

  private static ExRightClickConstructable Behavior(
    TestWorld world,
    bool? gatesProduction
  ) {
    var block = TestBlocks.Configure(new Block(), "stub:rccblock", 4999);
    var be = new StubBlockEntity {
      Block = block,
      Pos = new BlockPos(0, 0, 0, 0),
    };
    be.Initialize(world.Api);
    var behavior = new ExRightClickConstructable(be);

    var json = new JObject {
      // Two bare stages: CurrentCompletedStage starts at 0, and IsComplete compares that against
      // Stages.Length - 1, so a single stage would already read as complete before any interaction.
      ["stages"] = new JArray { new JObject(), new JObject() },
    };
    if (gatesProduction.HasValue)
      json["gatesProduction"] = gatesProduction.Value;

    behavior.Initialize(world.Api, new JsonObject(json));
    return behavior;
  }

  [Fact]
  public void GatesProduction_defaults_to_true_when_the_property_is_absent() {
    var behavior = Behavior(new TestWorld(), gatesProduction: null);

    Assert.True(behavior.GatesProduction);
  }

  [Fact]
  public void GatesProduction_reads_false_from_JSON() {
    var behavior = Behavior(new TestWorld(), gatesProduction: false);

    Assert.False(behavior.GatesProduction);
  }

  [Fact]
  public void An_unfinished_construction_is_not_ready_when_it_gates_production() {
    var behavior = Behavior(new TestWorld(), gatesProduction: true);

    Assert.False(behavior.IsComplete); // one stage, none completed yet
    Assert.False(behavior.IsReadyToProduce);
    Assert.True(behavior.StopsProductionWhenNotReady);
  }

  [Fact]
  public void An_unfinished_construction_is_always_ready_when_it_opts_out_of_gating() {
    var behavior = Behavior(new TestWorld(), gatesProduction: false);

    Assert.False(behavior.IsComplete);
    Assert.True(behavior.IsReadyToProduce);
    Assert.False(behavior.StopsProductionWhenNotReady);
  }

  #endregion

  #region ExRccSettings

  private static string FreshDomain() =>
    "rcctest-" + System.Guid.NewGuid().ToString("N")[..8];

  [Fact]
  public void An_unregistered_domain_leaves_the_ratio_unset() {
    Assert.Null(ExRccSettings.BrokenDropsRatio(FreshDomain()));
  }

  [Fact]
  public void A_registered_domain_reads_its_getter_live_on_every_call() {
    string domain = FreshDomain();
    float current = 1f;
    ExRccSettings.RegisterBrokenDropsRatio(domain, () => current);

    Assert.Equal(1f, ExRccSettings.BrokenDropsRatio(domain));
    current = 0.25f; // a live /exmod config change
    Assert.Equal(0.25f, ExRccSettings.BrokenDropsRatio(domain));
  }

  #endregion

  #region ConstructedAnimator.IsConstructed

  // One completed stage: on 1.22 vanilla's own RightClickConstruction/ConstructionStage; on
  // 1.20/1.21 exlib's own port, ExRightClickConstruction/ExConstructionStage (ExRightClickConstructable.cs
  // has neither type in scope on the other version). Both land on a field literally named "rcc".
  private static ExRightClickConstructable CompletedRcc(BlockEntity be) {
    var rcc = new ExRightClickConstructable(be);
#if GAME_GE_1_22
    var construction = new RightClickConstruction
    {
      Stages = [new ConstructionStage()],
      CurrentCompletedStage = 0,
    };
#else
    var construction = new ExRightClickConstruction {
      Stages = [new ExConstructionStage()],
      CurrentCompletedStage = 0,
    };
#endif
    ReflectionHelpers.SetField(rcc, "rcc", construction);
    return rcc;
  }

  [Fact]
  public void IsConstructed_is_false_before_a_behavior_is_resolved() {
    var animator = new ConstructedAnimator(new StubBlockEntity(), () => "key");

    Assert.False(animator.IsConstructed);
  }

  [Fact]
  public void IsConstructed_follows_the_resolved_behaviors_own_IsComplete() {
    var be = new StubBlockEntity {
      Block = TestBlocks.Configure(new Block(), "stub:rccblock2", 5000),
    };
    var animator = new ConstructedAnimator(be, () => "key");
    ReflectionHelpers.SetField(animator, "_rcc", CompletedRcc(be));

    Assert.True(animator.IsConstructed);
  }

  #endregion
}
