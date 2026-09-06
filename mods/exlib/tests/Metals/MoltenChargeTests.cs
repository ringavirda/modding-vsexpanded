using ExpandedLib.Industry.Molten;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The shared molten-charge domain type used by the converter, barrel, tap and pedestal: a
/// temperature-tracked stack plus a unit count, with temperature, state classification, retype,
/// recovery and tree round-trip. Iron melts at 1500 C, so it is liquid above 0.8x = 1200, hardened
/// below 0.3x = 450, and below its melting point under 1500.
/// </summary>
public class MoltenChargeTests {
  private const string Iron = "game:ingot-iron";
  private const string Steel = "game:ingot-steel";
  private const float IronMelt = 1500f;

  private static TestWorld NewWorld() {
    var world = new TestWorld();
    world.RegisterItem(Iron, IronMelt);
    world.RegisterItem(Steel, IronMelt);
    world.RegisterItem("game:metalbit-iron");
    world.RegisterItem("game:metalbit-steel");
    return world;
  }

  private static float CooldownSpeedOf(ItemStack stack) =>
    (stack.Attributes["temperature"] as ITreeAttribute)?.GetFloat(
      "cooldownSpeed"
    ) ?? 0f;

  #region Create / identity

  [Fact]
  public void Create_builds_a_charge_with_the_metal_code_units_and_temperature() {
    var w = NewWorld();
    MoltenCharge? charge = MoltenCharge.Create(w.World, Iron, 1400f, 40);

    Assert.NotNull(charge);
    Assert.Equal(Iron, charge!.MetalCode.ToString());
    Assert.Equal(40, charge.Units);
    Assert.Equal(1400f, charge.Temperature(w.World), 1);
  }

  [Fact]
  public void Create_returns_null_when_the_item_does_not_resolve() {
    var w = NewWorld();
    Assert.Null(MoltenCharge.Create(w.World, "game:doesnotexist", 1400f, 10));
  }

  #endregion

  #region Temperature + state classification

  [Fact]
  public void SetTemperature_round_trips() {
    var w = NewWorld();
    var charge = MoltenCharge.Create(w.World, Iron, 1400f, 40)!;

    charge.SetTemperature(w.World, 800f);

    Assert.Equal(800f, charge.Temperature(w.World), 1);
  }

  // Iron: liquid above 0.8x1500 = 1200, hardened below 0.3x1500 = 450, below melt under 1500. The
  // 1200-1500 band still flows (IsLiquid) while already below the melting point (IsBelowMeltingPoint).
  [Theory]
  [InlineData(1600f, true, false, false)] // above melt: liquid, not hardened, not below melt
  [InlineData(1300f, true, false, true)] // flowing but below the melting point
  [InlineData(1100f, false, false, true)] // cooling: no longer flows, not yet hardened
  [InlineData(400f, false, true, true)] // cold: hardened + below melt
  public void State_classifies_against_the_melting_point(
    float temp,
    bool liquid,
    bool hardened,
    bool belowMelt
  ) {
    var w = NewWorld();
    var charge = MoltenCharge.Create(w.World, Iron, temp, 40)!;

    Assert.Equal(liquid, charge.IsLiquid(w.World));
    Assert.Equal(hardened, charge.IsHardened(w.World));
    Assert.Equal(belowMelt, charge.IsBelowMeltingPoint(w.World));
  }

  [Fact]
  public void SyncCooldown_writes_the_rate_onto_the_stack() {
    var w = NewWorld();
    var charge = MoltenCharge.Create(w.World, Iron, 1400f, 40)!;

    charge.SyncCooldown(w.World, 7f);

    Assert.Equal(7f, CooldownSpeedOf(charge.Stack), 2);
  }

  #endregion

  #region Retype (refine) + recovery

  [Fact]
  public void RetypeTo_swaps_the_metal_keeping_units_and_temperature() {
    var w = NewWorld();
    var charge = MoltenCharge.Create(w.World, Iron, 1600f, 40)!;

    bool ok = charge.RetypeTo(w.World, Steel);

    Assert.True(ok);
    Assert.Equal(Steel, charge.MetalCode.ToString());
    Assert.Equal(40, charge.Units); // units preserved
    Assert.Equal(1600f, charge.Temperature(w.World), 1); // heat carried over
  }

  [Fact]
  public void RetypeTo_leaves_the_charge_unchanged_when_the_target_does_not_resolve() {
    var w = NewWorld();
    var charge = MoltenCharge.Create(w.World, Iron, 1600f, 40)!;

    bool ok = charge.RetypeTo(w.World, "game:doesnotexist");

    Assert.False(ok);
    Assert.Equal(Iron, charge.MetalCode.ToString());
  }

  [Fact]
  public void BuildRecovery_yields_metal_bits_at_five_units_each() {
    var w = NewWorld();
    var charge = MoltenCharge.Create(w.World, Iron, 900f, 50)!;

    ItemStack? recovered = charge.BuildRecovery(w.World, charge.Units);

    Assert.NotNull(recovered);
    Assert.Equal("game:metalbit-iron", recovered!.Collectible.Code.ToString());
    Assert.Equal(10, recovered.StackSize); // 50 / 5
  }

  #endregion

  #region Serialization

  [Fact]
  public void ToTree_then_FromTree_round_trips_the_stack_and_units() {
    var w = NewWorld();
    var charge = MoltenCharge.Create(w.World, Iron, 1400f, 35)!;

    var tree = new TreeAttribute();
    charge.ToTree(tree, "content", "contentUnits");
    MoltenCharge? loaded = MoltenCharge.FromTree(
      tree,
      "content",
      "contentUnits",
      w.World
    );

    Assert.NotNull(loaded);
    Assert.Equal(Iron, loaded!.MetalCode.ToString());
    Assert.Equal(35, loaded.Units);
  }

  [Fact]
  public void FromTree_of_an_empty_tree_is_null() {
    var w = NewWorld();
    Assert.Null(
      MoltenCharge.FromTree(
        new TreeAttribute(),
        "content",
        "contentUnits",
        w.World
      )
    );
  }

  #endregion
}
