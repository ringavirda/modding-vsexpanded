using ExpandedLib.Testing;
using SteelmakingExpanded.BlockNetworkMolten;
using SteelmakingExpanded.BlockNetworkMolten.BlockEntities;
using SteelmakingExpanded.BlockStructures.Converter.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The player-relief mechanics added after the first Bessemer playtest: the charge cools slower
/// inside the vessel (a configurable coefficient on the molten cooldown speed, so a finished heat
/// gives more time to pour), and a small fully-hardened residue that solidified mid-pour can be
/// chiselled out instead of breaking the whole - expensive - converter.
/// </summary>
public class ConverterChiselTests
{
  private const string Iron = "game:ingot-iron";
  private const string Steel = "game:ingot-steel";

  // Iron melts at 1500: hardened below 0.3x = 450, liquid above 0.8x = 1200. Capacity 1200, so the
  // 20% chisel ceiling is 240 units.
  private const float IronMelt = 1500f;

  private static readonly (int x, int y, int z) InputTapLocal = (1, 1, 2);

  private static TestWorld NewWorld()
  {
    var world = new TestWorld();
    world.RegisterItem(Iron, IronMelt);
    world.RegisterItem(Steel, IronMelt);
    world.RegisterItem("game:metalbit-iron");
    return world;
  }

  private static BlockEntityConverterControl Control(TestWorld world)
  {
    var be = new BlockEntityConverterControl
    {
      Pos = new BlockPos(0, 8, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "smex:converterbessemercontrol-north",
        1,
        ("side", "north")
      ),
    };
    world.Attach(be);
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
    return be;
  }

  private static ItemStack Metal(TestWorld world, string code, float temp) =>
    MoltenMetal.CreateStack(world.World, code, temp)!;

  // The VS time-based cooldown speed lives on the stack's temperature tree.
  private static float CooldownSpeedOf(ItemStack stack) =>
    (stack.Attributes["temperature"] as ITreeAttribute)?.GetFloat("cooldownSpeed")
    ?? 0f;

  // Primes the control's charge directly (bypasses the peripheral-gated tick).
  private static void PrimeCharge(
    BlockEntityConverterControl be,
    TestWorld world,
    float temp,
    int units,
    bool solidified
  )
  {
    ReflectionHelpers.SetField(be, "_content", Metal(world, Iron, temp));
    ReflectionHelpers.SetField(be, "_contentUnits", units);
    ReflectionHelpers.SetField(be, "_solidified", solidified);
  }

  private static float ExpectedSlowedCooldown =>
    SmexValues.MoltenCooldownSpeed * SmexValues.BessemerCooldownCoefficient;

  #region Cooldown coefficient

  [Fact]
  public void Filling_creates_the_charge_with_the_slowed_converter_cooldown()
  {
    var world = NewWorld();
    var be = Control(world);
    var input = PlaceInputCell(world, be);
    input.PushMetal(50, Metal(world, Iron, 1400f), world.World);

    ReflectionHelpers.Invoke(be, "TickFilling", 1f);

    var content = (ItemStack)ReflectionHelpers.GetField(be, "_content")!;
    Assert.Equal(ExpectedSlowedCooldown, CooldownSpeedOf(content), 3);
  }

  [Fact]
  public void Refined_steel_carries_the_slowed_converter_cooldown()
  {
    var world = NewWorld();
    var be = Control(world);
    ReflectionHelpers.SetField(be, "_content", Metal(world, Iron, 1700f));
    ReflectionHelpers.SetField(be, "_contentUnits", 50);

    ReflectionHelpers.Invoke(be, "CompleteRefining");

    var content = (ItemStack)ReflectionHelpers.GetField(be, "_content")!;
    Assert.Equal(Steel, content.Collectible.Code.ToString());
    Assert.Equal(ExpectedSlowedCooldown, CooldownSpeedOf(content), 3);
  }

  [Fact]
  public void The_default_coefficient_halves_the_molten_cooldown_speed()
  {
    // 0.5 x the molten-system rate, the spec'd default (cools twice as slowly inside the vessel).
    Assert.Equal(0.5f, SmexValues.BessemerCooldownCoefficient, 3);
    Assert.Equal(
      SmexValues.MoltenCooldownSpeed * 0.5f,
      ExpectedSlowedCooldown,
      3
    );
  }

  #endregion

  #region Chisel-out gating

  [Fact]
  public void A_small_hardened_residue_can_be_chiselled_out()
  {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 300f, 100, solidified: true); // hardened (300<450), 100 < 240

    Assert.True(be.CanChiselOut());
  }

  [Fact]
  public void A_solidified_but_still_hot_residue_cannot_be_chiselled()
  {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 800f, 100, solidified: true); // 450 < 800 < 1500 -> cooling, not hardened

    Assert.False(be.ChargeIsHardened);
    Assert.False(be.CanChiselOut());
  }

  [Fact]
  public void A_large_hardened_charge_cannot_be_chiselled_only_broken()
  {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 300f, 300, solidified: true); // hardened but 300 >= 240 (20% of 1200)

    Assert.True(be.ChargeIsHardened);
    Assert.False(be.CanChiselOut());
  }

  [Fact]
  public void A_still_liquid_charge_cannot_be_chiselled()
  {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 300f, 100, solidified: false);

    Assert.False(be.HasSolidifiedCharge);
    Assert.False(be.CanChiselOut());
  }

  #endregion

  #region Chisel-out recovery

  [Fact]
  public void Chiselling_recovers_the_metal_and_clears_the_charge()
  {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 300f, 100, solidified: true);

    ItemStack? drop = be.ChiselOutContent();

    Assert.NotNull(drop);
    Assert.Equal("game:metalbit-iron", drop!.Collectible.Code.ToString());
    Assert.Equal(20, drop.StackSize); // 5 units per bit
    Assert.Null(ReflectionHelpers.GetField(be, "_content"));
    Assert.Equal(0, (int)ReflectionHelpers.GetField(be, "_contentUnits")!);
    Assert.False((bool)ReflectionHelpers.GetField(be, "_solidified")!);
  }

  [Fact]
  public void Chiselling_a_non_chiselable_charge_returns_nothing_and_keeps_it()
  {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 800f, 100, solidified: true); // too hot to chisel

    Assert.Null(be.ChiselOutContent());
    Assert.Equal(100, (int)ReflectionHelpers.GetField(be, "_contentUnits")!);
  }

  #endregion

  /// <summary>Places a molten-canal cell at the control's resolved input-tap offset.</summary>
  private static BlockEntityMoltenCanal PlaceInputCell(
    TestWorld world,
    BlockEntityConverterControl control
  )
  {
    var pos = (BlockPos)
      ReflectionHelpers.Invoke(
        control,
        "GetGlobalPos",
        InputTapLocal.x,
        InputTapLocal.y,
        InputTapLocal.z
      )!;
    var cell = new BlockEntityMoltenCanal
    {
      Block = TestBlocks.Configure(
        new Block(),
        "smex:moltencanal-straight-ns",
        9,
        ("type", "straight"),
        ("orientation", "ns")
      ),
    };
    world.Place(pos, cell.Block, cell);
    world.Attach(cell);
    return cell;
  }
}
