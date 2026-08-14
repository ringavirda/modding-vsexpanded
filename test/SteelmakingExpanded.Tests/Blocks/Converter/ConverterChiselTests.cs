using ExpandedLib.Metals;
using ExpandedLib.Testing;
using IronIndustryExpanded;
using IronIndustryExpanded.BlockNetworkMolten;
using IronIndustryExpanded.BlockNetworkMolten.BlockEntities;
using SteelmakingExpanded.BlockStructures.Converter.BlockEntities;
using SteelmakingExpanded.BlockStructures.Converter.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The Bessemer converter's relief mechanics: the charge cools slower inside the vessel (a configurable
/// coefficient on the molten cooldown speed, giving more time to pour a finished heat), and a small
/// fully-hardened residue can be chiselled out instead of breaking the converter.
/// </summary>
public class ConverterChiselTests {
  private const string Iron = "game:ingot-iron";
  private const string Steel = "game:ingot-steel";

  // Iron melts at 1500: hardened below 0.3x = 450, liquid above 0.8x = 1200. Capacity 4800, so the
  // 20% chisel ceiling is 960 units.
  private const float IronMelt = 1500f;

  // Resolved the way the control resolves it, for the pig to Bessemer-steel retype test.
  private static string Pig => MetalRegistry.MoltenItemOf("pigiron").ToString();

  private static readonly (int x, int y, int z) InputTapLocal = (1, 1, 2);

  private static TestWorld NewWorld() {
    var world = new TestWorld();
    world.RegisterItem(Iron, IronMelt);
    world.RegisterItem(Steel, IronMelt);
    world.RegisterItem("game:ingot-pigiron", 1150f);
    world.RegisterItem("iiex:ingot-pigiron", 1150f);
    world.RegisterItem("game:ingot-bessemersteel", IronMelt);
    world.RegisterItem("smex:ingot-bessemersteel", IronMelt);
    world.RegisterItem("game:metalbit-iron");
    world.RegisterItem("game:metalbit-steel");
    return world;
  }

  private static BlockEntityConverterControl Control(TestWorld world) {
    var be = new BlockEntityConverterControl {
      Pos = new BlockPos(0, 8, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "smex:converterbessemercontrol-n",
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
    (stack.Attributes["temperature"] as ITreeAttribute)?.GetFloat(
      "cooldownSpeed"
    ) ?? 0f;

  // Primes the control's charge directly (bypasses the peripheral-gated tick).
  private static void PrimeCharge(
    BlockEntityConverterControl be,
    TestWorld world,
    float temp,
    int units,
    bool solidified
  ) {
    ReflectionHelpers.SetField(
      be,
      "_charge",
      MoltenCharge.Of(Metal(world, Iron, temp), units)
    );
    ReflectionHelpers.SetField(be, "_solidified", solidified);
  }

  private static MoltenCharge? Charge(BlockEntityConverterControl be) =>
    ReflectionHelpers.GetField(be, "_charge") as MoltenCharge;

  private static int ChargeUnits(BlockEntityConverterControl be) =>
    Charge(be)?.Units ?? 0;

  private static float ExpectedSlowedCooldown =>
    IiexValues.MoltenCooldownSpeed * SmexValues.BessemerCooldownCoefficient;

  #region Cooldown coefficient

  [Fact]
  public void Filling_creates_the_charge_with_the_slowed_converter_cooldown() {
    var world = NewWorld();
    var be = Control(world);
    var input = PlaceInputCell(world, be);
    input.PushMetal(50, Metal(world, Iron, 1400f), world.World);

    ReflectionHelpers.Invoke(be, "TickFilling", 1f);

    var content = Charge(be)!.Stack;
    Assert.Equal(ExpectedSlowedCooldown, CooldownSpeedOf(content), 3);
  }

  [Fact]
  public void Refined_steel_carries_the_slowed_converter_cooldown() {
    var world = NewWorld();
    var be = Control(world);
    // A pig charge at the carbon target, retyped to Bessemer steel the way the blow does.
    ReflectionHelpers.SetField(
      be,
      "_charge",
      MoltenCharge.Of(Metal(world, Pig, 1700f), 50)
    );
    ReflectionHelpers.SetField(be, "_pigCharged", 50);

    ReflectionHelpers.Invoke(be, "RetypeToSteel");

    var content = Charge(be)!.Stack;
    Assert.Contains("bessemersteel", content.Collectible.Code.ToString());
    Assert.Equal(ExpectedSlowedCooldown, CooldownSpeedOf(content), 3);
  }

  [Fact]
  public void The_default_coefficient_halves_the_molten_cooldown_speed() {
    // Default is 0.5 x the molten-system rate: the charge cools twice as slowly inside the vessel.
    Assert.Equal(0.5f, SmexValues.BessemerCooldownCoefficient, 3);
    Assert.Equal(
      IiexValues.MoltenCooldownSpeed * 0.5f,
      ExpectedSlowedCooldown,
      3
    );
  }

  // The tick re-stamps the live rate onto the charge rather than baking it in at pour time, so a
  // coefficient change reaches metal already in the vessel.
  [Fact]
  public void Changing_the_cooldown_coefficient_reaches_the_charge_already_in_the_vessel() {
    var world = NewWorld();
    var be = Control(world);
    var content = Metal(world, Iron, 1400f);
    ReflectionHelpers.SetField(be, "_charge", MoltenCharge.Of(content, 50));

    float original = SmexValues.BessemerCooldownCoefficient;
    try {
      // Cooling sped up mid-session.
      SmexValues.Edit(c => c.BessemerCooldownCoefficient = 10f);
      ReflectionHelpers.Invoke(be, "SyncContentCooldown");

      Assert.Equal(
        IiexValues.MoltenCooldownSpeed * 10f,
        CooldownSpeedOf(content),
        3
      );
    } finally {
      SmexValues.Edit(c => c.BessemerCooldownCoefficient = original);
    }
  }

  #endregion

  #region Chisel-out gating

  [Fact]
  public void A_small_hardened_residue_can_be_chiselled_out() {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 300f, 100, solidified: true); // hardened (300<450), 100 < 960

    Assert.True(be.CanChiselOut());
  }

  [Fact]
  public void A_solidified_but_still_hot_residue_cannot_be_chiselled() {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 800f, 100, solidified: true); // 450 < 800 < 1500 -> cooling, not hardened

    Assert.False(be.ChargeIsHardened);
    Assert.False(be.CanChiselOut());
  }

  [Fact]
  public void A_large_hardened_charge_cannot_be_chiselled_only_broken() {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 300f, 1000, solidified: true); // hardened but 1000 >= 960 (20% of 4800)

    Assert.True(be.ChargeIsHardened);
    Assert.False(be.CanChiselOut());
  }

  [Fact]
  public void A_still_liquid_charge_cannot_be_chiselled() {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 300f, 100, solidified: false);

    Assert.False(be.HasSolidifiedCharge);
    Assert.False(be.CanChiselOut());
  }

  #endregion

  #region Self-drop suppression

  // The control spawns the vessel, so breaking it must yield only construction materials. "drops": []
  // in the JSON is not always honoured for a variant block, so the block overrides GetDrops to return
  // an empty list. Here the registry is simulated handing the block its own code as a fallback drop.
  [Fact]
  public void Bessemer_vessel_never_drops_itself_even_if_registered_with_a_self_drop() {
    var block = TestBlocks.Configure(
      new BlockConverterBessemer(),
      "smex:converterbessemer-n",
      1,
      ("side", "north")
    );
    block.Drops = [new BlockDropItemStack(new ItemStack(block))];

    ItemStack[] drops = block.GetDrops(null!, new BlockPos(0, 8, 0), null);

    Assert.Empty(drops);
  }

  #endregion

  #region Solidified status feedback

  // The block-info status names the step for clearing a frozen charge: break a large residue, wait for
  // a small but still hot one to harden, chisel a small hardened one.
  private static string SolidifiedStatus(BlockEntityConverterControl be) =>
    (string)ReflectionHelpers.Invoke(be, "SolidifiedStatus")!;

  [Fact]
  public void Status_tells_the_player_to_break_a_large_solidified_charge() {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 300f, 1000, solidified: true); // hardened but 1000 >= 960

    Assert.Equal("smex:bessemer-status-solidified", SolidifiedStatus(be));
  }

  [Fact]
  public void Status_tells_the_player_to_wait_for_a_small_hot_residue_to_harden() {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 800f, 100, solidified: true); // small (100 < 240) but not yet hardened

    Assert.Equal("smex:bessemer-status-coolingtochisel", SolidifiedStatus(be));
  }

  [Fact]
  public void Status_tells_the_player_to_chisel_a_small_hardened_residue() {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 300f, 100, solidified: true); // small and hardened

    Assert.Equal("smex:bessemer-status-chiselout", SolidifiedStatus(be));
  }

  #endregion

  #region Chisel-out recovery

  [Fact]
  public void Chiselling_recovers_the_metal_and_clears_the_charge() {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 300f, 100, solidified: true);

    ItemStack? drop = be.ChiselOutContent();

    Assert.NotNull(drop);
    Assert.Equal("game:metalbit-iron", drop!.Collectible.Code.ToString());
    Assert.Equal(20, drop.StackSize); // 5 units per bit
    Assert.Null(ReflectionHelpers.GetField(be, "_charge"));
    Assert.False((bool)ReflectionHelpers.GetField(be, "_solidified")!);
  }

  [Fact]
  public void Chiselling_a_non_chiselable_charge_returns_nothing_and_keeps_it() {
    var world = NewWorld();
    var be = Control(world);
    PrimeCharge(be, world, 800f, 100, solidified: true); // too hot to chisel

    Assert.Null(be.ChiselOutContent());
    Assert.Equal(100, ChargeUnits(be));
  }

  #endregion

  /// <summary>Places a molten-canal cell at the control's resolved input-tap offset.</summary>
  private static BlockEntityMoltenCanal PlaceInputCell(
    TestWorld world,
    BlockEntityConverterControl control
  ) {
    var pos = (BlockPos)
      ReflectionHelpers.Invoke(
        control,
        "GetGlobalPos",
        InputTapLocal.x,
        InputTapLocal.y,
        InputTapLocal.z
      )!;
    var cell = new BlockEntityMoltenCanal {
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
