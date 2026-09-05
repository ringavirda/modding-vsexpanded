using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Boiler;
using IronIndustryExpanded.BlockStructures.Boiler.BlockEntities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The boiler block entity's persistence and hatch state. The operating-state fields (water, steam,
/// phase, both hatches, the lit bit, the fuel clock, timers) must survive a save/reload round trip, and
/// each hatch toggles on its own. The pressure/temperature formulas are pinned separately in
/// <see cref="BoilerMathTests"/>.
/// </summary>
public class BoilerBeTests {
  private static BlockEntityBoilerCornish Boiler(TestWorld? world = null) {
    var be = new BlockEntityBoilerCornish {
      Pos = new BlockPos(0, 0, 0),
      // Base BlockEntity.ToTreeAttributes reads Block.Code, so a placed block is required.
      Block = TestBlocks.Configure(
        new Vintagestory.API.Common.Block(),
        "iiex:boiler-cornish-n",
        1
      ),
    };
    world?.Attach(be);
    return be;
  }

  [Fact]
  public void Operating_state_round_trips_through_the_tree() {
    var src = Boiler();
    ReflectionHelpers.SetField(src, "_waterVolume", 600f);
    ReflectionHelpers.SetField(src, "_steamVolume", 400f);
    ReflectionHelpers.SetField(
      src,
      "_state",
      BlockEntityBoiler.BoilerState.Boiling
    );
    ReflectionHelpers.SetField(src, "_heatingSeconds", 90f);
    ReflectionHelpers.SetField(src, "_shutdownSeconds", 5f);
    // The hatch setters are private; set their backing fields directly.
    ReflectionHelpers.SetField(src, "<MainHatchOpen>k__BackingField", true);
    ReflectionHelpers.SetField(src, "<ManHatchOpen>k__BackingField", true);
    ReflectionHelpers.SetField(src, "_lit", true);
    ReflectionHelpers.SetField(src, "_fuelSeconds", 12f);

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);

    var world = new TestWorld();
    var dst = Boiler(world);
    dst.FromTreeAttributes(tree, world.World);

    Assert.Equal(
      600f,
      (float)ReflectionHelpers.GetField(dst, "_waterVolume")!,
      3
    );
    Assert.Equal(
      400f,
      (float)ReflectionHelpers.GetField(dst, "_steamVolume")!,
      3
    );
    Assert.Equal(
      BlockEntityBoiler.BoilerState.Boiling,
      ReflectionHelpers.GetField(dst, "_state")
    );
    Assert.Equal(
      90f,
      (float)ReflectionHelpers.GetField(dst, "_heatingSeconds")!,
      3
    );
    Assert.True(dst.MainHatchOpen);
    Assert.True(dst.ManHatchOpen);
    Assert.True((bool)ReflectionHelpers.GetField(dst, "_lit")!);
    Assert.Equal(
      12f,
      (float)ReflectionHelpers.GetField(dst, "_fuelSeconds")!,
      3
    );
    // Derived pressure reconstructs from the restored volumes: 400 / (1600 - 600) = 0.4.
    Assert.Equal(0.4f, dst.InternalPressure, 3);
  }

  [Fact]
  public void Both_hatches_default_closed() {
    var be = Boiler();
    Assert.False(be.MainHatchOpen);
    Assert.False(be.ManHatchOpen);
  }

  [Fact]
  public void Each_hatch_toggles_without_moving_the_other() {
    var world = new TestWorld();
    var be = Boiler(world);

    be.ToggleMainHatch();
    Assert.True(be.MainHatchOpen);
    Assert.False(be.ManHatchOpen);

    be.ToggleManHatch();
    Assert.True(be.MainHatchOpen);
    Assert.True(be.ManHatchOpen);

    be.ToggleMainHatch();
    Assert.False(be.MainHatchOpen);
    Assert.True(be.ManHatchOpen);
  }

  [Fact]
  public void A_fresh_boiler_serializes_an_idle_closed_state() {
    var be = Boiler();
    var tree = new TreeAttribute();
    be.ToTreeAttributes(tree);

    Assert.Equal(
      (int)BlockEntityBoiler.BoilerState.Idle,
      tree.GetInt("boilerState")
    );
    Assert.False(tree.GetBool("mainHatchOpen"));
    Assert.False(tree.GetBool("manHatchOpen"));
    Assert.False(tree.GetBool("lit"));
    Assert.Equal(0f, tree.GetFloat("waterVolume"), 3);
  }
}
