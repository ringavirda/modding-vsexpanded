using ExpandedLib.Testing;
using LowPressureExpanded.BlockStructures.Boiler.BlockEntities;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// The boiler's manual bucket fill/drain entry guards. Only a liquid container holding water (or an
/// empty one, when draining) may interact, so any other held item is a no-op that leaves the water
/// level unchanged. Only the reject branches are covered: the transfer path runs through vanilla
/// <c>BlockLiquidContainerBase</c> litre metering, which needs water-tight-container props the
/// headless harness cannot supply.
/// </summary>
public class BoilerManualWaterTests {
  private static BlockEntityBoilerCornish Boiler(float water) {
    var be = new BlockEntityBoilerCornish();
    ReflectionHelpers.SetField(be, "_waterVolume", water);
    return be;
  }

  private static ItemSlot HeldItem(string code) =>
    new DummySlot(new ItemStack(new Item { Code = new AssetLocation(code) }));

  private static float WaterOf(BlockEntityBoilerCornish be) =>
    (float)ReflectionHelpers.GetField(be, "_waterVolume")!;

  [Fact]
  public void Filling_rejects_a_held_item_that_is_not_a_liquid_container() {
    var be = Boiler(water: 100f);
    Assert.False(
      be.TryManualFill(Substitute.For<IPlayer>(), HeldItem("game:rock-granite"))
    );
    Assert.Equal(100f, WaterOf(be), 3);
  }

  [Fact]
  public void Filling_rejects_an_empty_hand() {
    var be = Boiler(water: 100f);
    Assert.False(be.TryManualFill(Substitute.For<IPlayer>(), new DummySlot()));
    Assert.Equal(100f, WaterOf(be), 3);
  }

  [Fact]
  public void Draining_rejects_a_held_item_that_is_not_a_liquid_container() {
    var be = Boiler(water: 400f);
    Assert.False(
      be.TryManualDrain(
        Substitute.For<IPlayer>(),
        HeldItem("game:rock-granite")
      )
    );
    Assert.Equal(400f, WaterOf(be), 3);
  }
}
