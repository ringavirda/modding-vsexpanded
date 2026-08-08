using ExpandedLib.Metals;
using ExpandedLib.Process;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.Items;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The blast furnace's firing/melting behaviour is gated on a full multiblock with live tuyeres, gas
/// outlets and hearth piles, which the headless harness does not raise. What stands on its own is
/// pinned here: the idle default, the shutdown reset, molten stack construction, and the save round
/// trip.
/// </summary>
public class BlastFurnaceTests {
  private static TestWorld NewWorld() {
    var world = new TestWorld();
    world.RegisterItem("game:ingot-iron", 1500f);
    world.RegisterItem("iwex:slag");
    // The furnace resolves the "slag" short token through MetalRegistry, which the game populates from
    // assets/iwex/config/metals/slag.json at AssetsFinalize. The headless harness loads no assets, so
    // the same mapping is registered here. Iron and steel need none: they follow the game:ingot
    // convention.
    MetalRegistry.Register(
      new MetalDef { Code = "slag", MoltenItem = "iwex:slag" }
    );
    return world;
  }

  private static BlockEntityBlastFurnaceHot Furnace(TestWorld world) {
    var be = new BlockEntityBlastFurnaceHot {
      Pos = new BlockPos(0, 16, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "smex:blastfurnacecore-n",
        1,
        ("side", "north")
      ),
    };
    world.Attach(be);
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
    ReflectionHelpers.Invoke(be, "CacheAttributes");
    return be;
  }

  #region State machine

  [Fact]
  public void Defaults_to_idle() {
    Assert.Equal(FurnaceState.Idle, Furnace(NewWorld()).State);
  }

  // The shaft branch derives its state from the charge (`DerivesState`), so `TransitionToMelting` is
  // unreachable from a blast furnace. `FireboxTickTests` covers the branch that owns a stored state
  // machine, and `BlastFurnaceScenarioTests` the charge-driven melt.

  /// <summary>
  /// <c>Shutdown()</c> carries out what going out entails - reset to ambient, pools cleared, residue
  /// laid down - without deciding the state, which the derived branch reads from the charge.
  /// </summary>
  [Fact]
  public void Shutdown_resets_the_heat_and_the_pools_without_deciding_the_state() {
    var be = Furnace(NewWorld());
    ReflectionHelpers.SetField(be, "_internalTemp", 1500f);
    ReflectionHelpers.SetField(be, "_moltenIron", 50f);

    ReflectionHelpers.Invoke(be, "Shutdown");

    Assert.Equal(
      20f,
      (float)ReflectionHelpers.GetField(be, "_internalTemp")!,
      1
    );
    Assert.Equal(0f, (float)ReflectionHelpers.GetField(be, "_moltenIron")!, 3);
  }

  #endregion

  #region Molten stack construction

  [Fact]
  public void CreateMoltenStack_builds_iron_at_the_network_code() {
    var world = NewWorld();
    var be = Furnace(world);

    var stack = (ItemStack?)
      ReflectionHelpers.Invoke(be, "CreateMoltenStack", "iron", 12, 1400f);

    Assert.NotNull(stack);
    Assert.Equal("game:ingot-iron", stack!.Collectible.Code.ToString());
    Assert.Equal(12, stack.StackSize);
  }

  [Fact]
  public void CreateMoltenStack_maps_slag_to_its_own_domain() {
    var world = NewWorld();
    var be = Furnace(world);

    var stack = (ItemStack?)
      ReflectionHelpers.Invoke(be, "CreateMoltenStack", "slag", 8, 1300f);

    Assert.NotNull(stack);
    Assert.Equal("iwex:slag", stack!.Collectible.Code.ToString());
  }

  [Fact]
  public void CreateMoltenStack_is_null_for_an_unresolved_metal() {
    var world = NewWorld(); // gold not registered
    var be = Furnace(world);

    Assert.Null(
      (ItemStack?)
        ReflectionHelpers.Invoke(be, "CreateMoltenStack", "gold", 5, 1400f)
    );
  }

  #endregion

  #region Serialization

  [Fact]
  public void Furnace_state_round_trips_through_the_tree() {
    var world = NewWorld();
    var src = Furnace(world);
    // State goes in through the reader: there is no setter
    // (FurnaceBranchGuards.NoFurnaceExposesASettableState), and what this pins is that a furnace loaded
    // as Melting writes Melting back out.
    var seed = new TreeAttribute();
    seed.SetInt("bfState", (int)FurnaceState.Melting);
    src.FromTreeAttributes(seed, world.World);
    Assert.Equal(FurnaceState.Melting, src.State);

    ReflectionHelpers.SetProperty(src, nameof(src.IsChoked), true);
    ReflectionHelpers.SetField(src, "_internalTemp", 1456f);
    ReflectionHelpers.SetField(src, "_moltenIron", 80f);
    ReflectionHelpers.SetField(src, "_moltenSlag", 40f);
    ReflectionHelpers.SetField(src, "_cachedMixCount", 220);
    ReflectionHelpers.SetField(src, "_cachedIsFull", true);

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);

    var dst = Furnace(world);
    dst.FromTreeAttributes(tree, world.World);

    Assert.Equal(FurnaceState.Melting, dst.State);
    Assert.True(dst.IsChoked);
    Assert.Equal(
      1456f,
      (float)ReflectionHelpers.GetField(dst, "_internalTemp")!,
      1
    );
    Assert.Equal(
      80f,
      (float)ReflectionHelpers.GetField(dst, "_moltenIron")!,
      1
    );
    Assert.Equal(
      40f,
      (float)ReflectionHelpers.GetField(dst, "_moltenSlag")!,
      1
    );
    Assert.Equal(220, (int)ReflectionHelpers.GetField(dst, "_cachedMixCount")!);
  }

  [Fact]
  public void The_air_starved_flag_round_trips_so_the_client_hud_can_read_it() {
    // GetBlockInfo runs client-side and never reads the tuyere network, so the air-starved stall line
    // has to ride the save tree - the same reason the rejected-charge and mix-count state do.
    var world = NewWorld();
    var src = Furnace(world);
    ReflectionHelpers.SetField(src, "_airStarved", true);

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);
    var dst = Furnace(world);
    dst.FromTreeAttributes(tree, world.World);

    Assert.True((bool)ReflectionHelpers.GetField(dst, "_airStarved")!);
  }

  [Fact]
  public void The_heat_balance_round_trips_so_the_client_hud_can_read_it() {
    // GetBlockInfo runs client-side and the client never walks the charge or reads the pipes, so the
    // balance has to ride the save tree for the heat readout to show anything in game.
    var world = NewWorld();
    var src = Furnace(world);
    var balance = new HeatBalance(
      TIn: 2175.5f,
      TLoss: 430f,
      TProcess: 1745.5f,
      FuelFrac: 0.2f,
      FuelFactor: 1f,
      AirFactor: 1f,
      BlastSupplied: true,
      BlastTemp: 950f,
      PreheatGain: 325.5f,
      ChargeLoss: 310f,
      AmbientLoss: 0f
    );
    ReflectionHelpers.SetField(src, "_lastHeatBalance", balance);
    ReflectionHelpers.SetField(src, "_chargeMix", new BurdenMix(75f, 5f, 20f));

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);
    var dst = Furnace(world);
    dst.FromTreeAttributes(tree, world.World);

    Assert.Equal(
      balance,
      (HeatBalance)ReflectionHelpers.GetField(dst, "_lastHeatBalance")!
    );
    Assert.Equal(
      "iwex:burden-profile-standard",
      Burden.ProfileLangKey(
        (BurdenMix)ReflectionHelpers.GetField(dst, "_chargeMix")!
      )
    );
  }

  #endregion
}
