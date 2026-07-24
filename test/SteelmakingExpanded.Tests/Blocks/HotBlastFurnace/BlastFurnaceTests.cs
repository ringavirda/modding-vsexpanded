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
/// The blast furnace's firing/melting state machine is gated on a full multiblock with live tuyeres,
/// gas outlets and hearth piles - too much to fake wholesale - but the state persistence, the molten
/// stack construction, and the simple state transitions (transition to melting, extinguish-to-idle)
/// stand on their own. Those are pinned here.
/// </summary>
public class BlastFurnaceTests
{
  private static TestWorld NewWorld()
  {
    var world = new TestWorld();
    world.RegisterItem("game:ingot-iron", 1500f);
    world.RegisterItem("iwex:slag");
    // The furnace resolves the "slag" short token through MetalRegistry, which the game populates from
    // assets/iwex/config/metals/slag.json at AssetsFinalize. The headless harness runs no asset load,
    // so register the same mapping here (iron/steel need none - they follow the game:ingot convention).
    MetalRegistry.Register(
      new MetalDef { Code = "slag", MoltenItem = "iwex:slag" }
    );
    return world;
  }

  private static BlockEntityBlastFurnaceHot Furnace(TestWorld world)
  {
    var be = new BlockEntityBlastFurnaceHot
    {
      Pos = new BlockPos(0, 16, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "smex:blastfurnacecore-north",
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
  public void Defaults_to_idle()
  {
    Assert.Equal(FurnaceState.Idle, Furnace(NewWorld()).State);
  }

  [Fact]
  public void TransitionToMelting_moves_firing_into_melting()
  {
    var be = Furnace(NewWorld());
    ReflectionHelpers.SetProperty(be, nameof(be.State), FurnaceState.Firing);

    ReflectionHelpers.Invoke(be, "TransitionToMelting");

    Assert.Equal(FurnaceState.Melting, be.State);
    Assert.Equal(0f, (float)ReflectionHelpers.GetField(be, "_meltSeconds")!, 3);
  }

  [Fact]
  public void Extinguish_returns_to_idle_and_resets_heat()
  {
    var be = Furnace(NewWorld());
    ReflectionHelpers.SetProperty(be, nameof(be.State), FurnaceState.Melting);
    ReflectionHelpers.SetField(be, "_internalTemp", 1500f);
    // No molten iron, so the solidified-iron drop branch is skipped.

    ReflectionHelpers.Invoke(be, "Extinguish");

    Assert.Equal(FurnaceState.Idle, be.State);
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
  public void CreateMoltenStack_builds_iron_at_the_network_code()
  {
    var world = NewWorld();
    var be = Furnace(world);

    var stack = (ItemStack?)
      ReflectionHelpers.Invoke(be, "CreateMoltenStack", "iron", 12, 1400f);

    Assert.NotNull(stack);
    Assert.Equal("game:ingot-iron", stack!.Collectible.Code.ToString());
    Assert.Equal(12, stack.StackSize);
  }

  [Fact]
  public void CreateMoltenStack_maps_slag_to_its_own_domain()
  {
    var world = NewWorld();
    var be = Furnace(world);

    var stack = (ItemStack?)
      ReflectionHelpers.Invoke(be, "CreateMoltenStack", "slag", 8, 1300f);

    Assert.NotNull(stack);
    Assert.Equal("iwex:slag", stack!.Collectible.Code.ToString());
  }

  [Fact]
  public void CreateMoltenStack_is_null_for_an_unresolved_metal()
  {
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
  public void Furnace_state_round_trips_through_the_tree()
  {
    var world = NewWorld();
    var src = Furnace(world);
    ReflectionHelpers.SetProperty(src, nameof(src.State), FurnaceState.Melting);
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
  public void The_air_starved_flag_round_trips_so_the_client_hud_can_read_it()
  {
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
  public void The_heat_balance_round_trips_so_the_client_hud_can_read_it()
  {
    // GetBlockInfo runs client-side, and the client never walks the charge or reads the pipes. If
    // the balance did not ride the tree the whole heat readout would print zeroes in game while
    // every headless test still passed - so the round trip is pinned here.
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
    ReflectionHelpers.SetField(
      src,
      "_chargeMix",
      new BurdenMix(75f, 5f, 20f)
    );

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
