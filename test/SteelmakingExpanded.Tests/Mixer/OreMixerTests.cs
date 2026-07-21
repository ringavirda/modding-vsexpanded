using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockStructures.OreProcessing.BlockEntities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The ore mixer's gameplay state machine: charging the raw iron/flux/coke parts from held stacks,
/// the powered mixing of a charge into graded burden, the "fully mixed" gate on draining, draining the
/// finished burden into the container directly below, and reloading off-spec burden to retune. The MP
/// power and construction gates live in the server tick / block and are exercised in-game; these drive
/// the public charge/mix/drain steps directly.
/// </summary>
public class OreMixerTests
{
  private sealed record Inputs(
    Item Iron,
    Item Metal,
    Item Lime,
    Item Coke,
    Item Charcoal,
    Item Burden,
    Item Remelt
  );

  private static (TestWorld world, BlockEntityOreMixer be, Inputs items) NewMixer()
  {
    var world = new TestWorld();
    var burden = world.RegisterItem("iwex:burden");
    var remelt = world.RegisterItem("iwex:remeltburden");
    var iron = world.RegisterItem("game:crushed-iron"); // path starts with "crushed-iron"
    var metal = world.RegisterItem("game:metalbit-iron"); // scrap: the remelt-family primary charge
    var lime = world.RegisterItem("game:lime");
    var coke = world.RegisterItem("game:coke"); // lump coke (crushed coke retired)
    var charcoal = world.RegisterItem("game:charcoal");

    var block = TestBlocks.Configure(new Block(), "iwex:mixer-north", 80);
    var pos = new BlockPos(0, 5, 0);
    var be = new BlockEntityOreMixer { Pos = pos, Block = block };
    world.Place(pos, block, be);
    world.Initialize(be); // server: resolves the burden items, registers the (unused here) tick

    return (world, be, new Inputs(iron, metal, lime, coke, charcoal, burden, remelt));
  }

  private static DummySlot Stack(Item item, int n) => new(new ItemStack(item, n));

  #region Charging

  [Fact]
  public void Held_materials_charge_their_matching_raw_part()
  {
    var (_, be, items) = NewMixer();

    Assert.True(be.TryAddInput(Stack(items.Iron, 12)));
    be.TryAddInput(Stack(items.Lime, 1));
    be.TryAddInput(Stack(items.Coke, 3)); // 3 lump coke -> 6 fuel value (2f each)

    Assert.Equal(19, be.TotalRaw);
    Assert.Equal(new BurdenMix(12f, 1f, 6f), be.Mix);
  }

  [Fact]
  public void Charging_consumes_the_held_stack()
  {
    var (_, be, items) = NewMixer();
    var slot = Stack(items.Iron, 12);

    Assert.True(be.TryAddInput(slot));
    Assert.True(slot.Empty);
  }

  [Fact]
  public void A_plain_right_click_adds_a_single_unit()
  {
    var (_, be, items) = NewMixer();
    var slot = Stack(items.Iron, 12);

    Assert.True(be.TryAddInput(slot, wholeStack: false));
    Assert.Equal(1, be.TotalRaw);
    Assert.Equal(11, slot.StackSize); // the rest stays in hand
  }

  [Fact]
  public void A_sneak_right_click_adds_the_whole_stack()
  {
    var (_, be, items) = NewMixer();
    var slot = Stack(items.Iron, 12);

    Assert.True(be.TryAddInput(slot, wholeStack: true));
    Assert.Equal(12, be.TotalRaw);
    Assert.True(slot.Empty);
  }

  [Fact]
  public void A_non_input_item_is_rejected()
  {
    var (world, be, _) = NewMixer();
    var stick = world.RegisterItem("game:stick");
    var slot = Stack(stick, 5);

    Assert.False(be.TryAddInput(slot));
    Assert.Equal(0, be.TotalRaw);
    Assert.Equal(5, slot.StackSize);
  }

  [Fact]
  public void Charging_is_capped_at_the_batch_size()
  {
    var (_, be, items) = NewMixer();
    Assert.True(be.TryAddInput(Stack(items.Iron, 1000)));
    Assert.Equal(IwexValues.MixerMaxRaw, be.TotalRaw);
  }

  [Fact]
  public void Adding_material_resets_the_mixing_progress()
  {
    var (_, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Iron, 10));
    be.AdvanceMixing(0.05f, IwexValues.MixerMinSpeed); // a small slice: partway, not finished
    Assert.True(be.MixProgress > 0f);

    be.TryAddInput(Stack(items.Iron, 2));
    Assert.Equal(0f, be.MixProgress, 3);
  }

  #endregion

  #region Mixing

  [Fact]
  public void A_charge_mixes_into_graded_burden_once_fully_mixed()
  {
    var (_, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Iron, 12));
    be.TryAddInput(Stack(items.Lime, 1));
    be.TryAddInput(Stack(items.Coke, 3));

    Assert.False(be.HasReadyBurden);
    be.AdvanceMixing(1000f, IwexValues.MixerMaxSpeed); // well past the required time

    Assert.True(be.HasReadyBurden);
    Assert.Equal(19, be.ReadyBurden); // count = total raw (3 coke -> 6 fuel value)
    Assert.Equal(0, be.TotalRaw); // raw consumed
  }

  [Fact]
  public void More_material_cannot_be_added_once_a_batch_is_ready()
  {
    var (_, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Iron, 8));
    be.AdvanceMixing(1000f, IwexValues.MixerMaxSpeed);

    Assert.False(be.TryAddInput(Stack(items.Iron, 4)));
  }

  #endregion

  #region Mix-time scaling (axle speed × amount)

  [Fact]
  public void A_full_batch_takes_the_slow_time_at_min_speed()
  {
    var (_, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Iron, IwexValues.MixerMaxRaw)); // full

    Assert.Equal(
      IwexValues.MixerFullMixSecondsSlow, // ~30 s
      be.MixSecondsRequired(IwexValues.MixerMinSpeed),
      1
    );
  }

  [Fact]
  public void A_full_batch_takes_the_fast_time_at_max_speed()
  {
    var (_, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Iron, IwexValues.MixerMaxRaw)); // full

    Assert.Equal(
      IwexValues.MixerFullMixSecondsFast, // ~10 s
      be.MixSecondsRequired(IwexValues.MixerMaxSpeed),
      1
    );
  }

  [Fact]
  public void More_material_takes_proportionally_longer()
  {
    var (_, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Iron, 100));
    float t100 = be.MixSecondsRequired(IwexValues.MixerMinSpeed);
    be.TryAddInput(Stack(items.Iron, 100)); // now 200
    float t200 = be.MixSecondsRequired(IwexValues.MixerMinSpeed);

    Assert.Equal(2f * t100, t200, 2);
  }

  [Fact]
  public void A_faster_axle_mixes_quicker()
  {
    var (_, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Iron, 200));

    Assert.True(
      be.MixSecondsRequired(IwexValues.MixerMaxSpeed)
        < be.MixSecondsRequired(IwexValues.MixerMinSpeed)
    );
  }

  [Fact]
  public void Speed_below_min_is_clamped_to_the_slow_time()
  {
    var (_, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Iron, 200));

    Assert.Equal(
      be.MixSecondsRequired(IwexValues.MixerMinSpeed),
      be.MixSecondsRequired(0.01f),
      3
    );
  }

  #endregion

  #region Draining

  [Fact]
  public void Draining_is_refused_until_a_batch_is_ready()
  {
    var (_, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Iron, 8)); // charged but not mixed
    Assert.False(be.ToggleDrain());
  }

  [Fact]
  public void A_ready_batch_drains_into_the_container_below_as_graded_burden()
  {
    var (world, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Iron, 12));
    be.TryAddInput(Stack(items.Lime, 1));
    be.TryAddInput(Stack(items.Coke, 3));
    be.AdvanceMixing(1000f, IwexValues.MixerMaxSpeed); // 16 burden ready

    // A bunker sitting directly below receives the burden.
    var bunkerBlock = TestBlocks.Configure(
      new Block(),
      "iwex:bunker-red-north",
      81
    );
    var bunker = new BlockEntityOreBunker
    {
      Pos = be.Pos.DownCopy(),
      Block = bunkerBlock,
    };
    world.Place(be.Pos.DownCopy(), bunkerBlock, bunker);
    world.Initialize(bunker);

    Assert.True(be.ToggleDrain());
    be.DrainStep(100f); // one big slice empties the buffer

    Assert.Equal(19, bunker.TotalContents); // 12 iron + 1 flux + 6 fuel (3 lump coke)
    Assert.False(be.HasReadyBurden);
  }

  [Fact]
  public void A_ready_batch_drains_through_a_filler_into_the_megablock_container()
  {
    // The bunker is a mega-block: only its principal cell carries the container, the rest are invisible
    // structure fillers. A mixer sitting over a filler cell must still reach the principal's inventory.
    var (world, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Iron, 12));
    be.TryAddInput(Stack(items.Lime, 1));
    be.TryAddInput(Stack(items.Coke, 3));
    be.AdvanceMixing(1000f, IwexValues.MixerMaxSpeed); // 16 burden ready

    // The bunker principal lives two cells over; the cell directly below the mixer is its filler.
    var bunkerBlock = TestBlocks.Configure(new Block(), "iwex:bunker-red-north", 81);
    BlockPos principalPos = be.Pos.DownCopy().AddCopy(2, 0, 0);
    var bunker = new BlockEntityOreBunker { Pos = principalPos, Block = bunkerBlock };
    world.Place(principalPos, bunkerBlock, bunker);
    world.Initialize(bunker);

    var fillerBlock = TestBlocks.Configure(
      new BlockStructureFiller(),
      "exlib:structurefiller",
      82
    );
    var filler = new BlockEntityStructureFiller { Principal = principalPos };
    world.Place(be.Pos.DownCopy(), fillerBlock, filler);

    Assert.True(be.ToggleDrain());
    be.DrainStep(100f);

    Assert.Equal(19, bunker.TotalContents); // 12 iron + 1 flux + 6 fuel (3 lump coke)
    Assert.False(be.HasReadyBurden);
  }

  [Fact]
  public void Draining_with_no_container_below_keeps_the_burden()
  {
    var (_, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Iron, 8));
    be.AdvanceMixing(1000f, IwexValues.MixerMaxSpeed);

    Assert.True(be.ToggleDrain());
    be.DrainStep(100f); // nothing below to accept it

    Assert.True(be.HasReadyBurden);
    Assert.Equal(8, be.ReadyBurden);
  }

  #endregion

  #region Reloading

  [Fact]
  public void Off_spec_burden_reloads_back_into_the_raw_charge()
  {
    var (_, be, items) = NewMixer();
    var stack = new ItemStack(items.Burden, 16);
    Burden.Write(stack, new BurdenMix(12f, 1f, 3f));
    var slot = new DummySlot(stack);

    Assert.True(be.TryReloadBurden(slot));
    Assert.True(slot.Empty);
    Assert.Equal(16, be.TotalRaw);
    Assert.Equal(new BurdenMix(12f, 1f, 3f), be.Mix); // proportions reconstructed
  }

  [Fact]
  public void Reloaded_burden_can_be_retuned_with_more_material_then_remixed()
  {
    var (_, be, items) = NewMixer();

    // A 'standard' off-spec burden reloaded back to raw...
    var stack = new ItemStack(items.Burden, 16);
    Burden.Write(stack, new BurdenMix(12f, 1f, 3f));
    Assert.True(be.TryReloadBurden(new DummySlot(stack)));
    Assert.Equal(new BurdenMix(12f, 1f, 3f), be.Mix);

    // ...then topped up with coke to shift the proportions, and mixed into a fresh batch.
    Assert.True(be.TryAddInput(Stack(items.Coke, 8))); // 8 lump coke -> +16 fuel value
    Assert.Equal(new BurdenMix(12f, 1f, 19f), be.Mix); // retuned

    be.AdvanceMixing(1000f, IwexValues.MixerMaxSpeed);
    Assert.True(be.HasReadyBurden);
    Assert.Equal(32, be.ReadyBurden);
  }

  #endregion

  #region Acceptance

  [Fact]
  public void AcceptsAsInput_recognizes_materials_and_burden_but_not_junk()
  {
    var (world, _, items) = NewMixer();
    var stick = world.RegisterItem("game:stick");

    Assert.True(BlockEntityOreMixer.AcceptsAsInput(new ItemStack(items.Iron)));
    Assert.True(BlockEntityOreMixer.AcceptsAsInput(new ItemStack(items.Metal)));
    Assert.True(BlockEntityOreMixer.AcceptsAsInput(new ItemStack(items.Lime)));
    Assert.True(BlockEntityOreMixer.AcceptsAsInput(new ItemStack(items.Coke)));
    Assert.True(BlockEntityOreMixer.AcceptsAsInput(new ItemStack(items.Charcoal)));
    Assert.True(BlockEntityOreMixer.AcceptsAsInput(new ItemStack(items.Burden)));
    Assert.True(BlockEntityOreMixer.AcceptsAsInput(new ItemStack(items.Remelt)));
    Assert.False(BlockEntityOreMixer.AcceptsAsInput(new ItemStack(stick)));
    Assert.False(BlockEntityOreMixer.AcceptsAsInput(null));
  }

  #endregion

  #region Charcoal fuel

  [Fact]
  public void Two_charcoal_count_as_one_fuel_unit()
  {
    var (_, be, items) = NewMixer();

    be.TryAddInput(Stack(items.Charcoal, 6)); // 6 charcoal -> 3 fuel value (0.5 each)

    Assert.Equal(3f, be.Mix.Fuel, 3);
  }

  [Fact]
  public void Charcoal_charges_the_fuel_part_by_value_not_item_count()
  {
    // 12 iron + 1 flux + 6 charcoal (= 3 fuel value at 0.5 each) -> the fuel part is 3, and the batch
    // volume counts that value, not the six raw charcoal items.
    var (_, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Iron, 12));
    be.TryAddInput(Stack(items.Lime, 1));
    be.TryAddInput(Stack(items.Charcoal, 6));

    Assert.Equal(new BurdenMix(12f, 1f, 3f), be.Mix);
    Assert.Equal(16, be.TotalRaw); // volume counts fuel value, not raw charcoal items
  }

  [Fact]
  public void Charcoal_fills_the_batch_by_fuel_value_not_item_count()
  {
    var (_, be, items) = NewMixer();

    // Charcoal is worth half, so it takes 2x the cap in items to fill from charcoal alone.
    Assert.True(be.TryAddInput(Stack(items.Charcoal, 5000)));
    Assert.Equal(IwexValues.MixerMaxRaw, be.TotalRaw);
    Assert.Equal(IwexValues.MixerMaxRaw, be.Mix.Fuel, 1);
  }

  #endregion

  #region Burden family lock

  [Fact]
  public void Iron_ore_added_first_locks_the_ore_family_and_refuses_scrap()
  {
    var (_, be, items) = NewMixer();

    Assert.True(be.TryAddInput(Stack(items.Iron, 4)));
    Assert.Equal(Burden.FamilyOre, be.Family);

    // The other primary (scrap metal) is now refused, and nothing is consumed.
    var scrap = Stack(items.Metal, 4);
    Assert.False(be.TryAddInput(scrap));
    Assert.Equal(4, scrap.StackSize);
    Assert.Equal(4, be.TotalRaw); // still just the iron
  }

  [Fact]
  public void Scrap_added_first_locks_the_remelt_family_and_refuses_iron_ore()
  {
    var (_, be, items) = NewMixer();

    Assert.True(be.TryAddInput(Stack(items.Metal, 4)));
    Assert.Equal(Burden.FamilyRemelt, be.Family);

    var ore = Stack(items.Iron, 4);
    Assert.False(be.TryAddInput(ore));
    Assert.Equal(4, ore.StackSize);
    Assert.Equal(4, be.TotalRaw);
  }

  [Fact]
  public void Flux_and_fuel_do_not_lock_the_family()
  {
    var (_, be, items) = NewMixer();

    // Flux and fuel are taken by both burdens, so they must not commit the batch to a family...
    Assert.True(be.TryAddInput(Stack(items.Lime, 2)));
    Assert.True(be.TryAddInput(Stack(items.Coke, 1)));
    Assert.Equal("", be.Family);

    // ...and either primary can still lock it afterwards (here: scrap -> remelt, then ore refused).
    Assert.True(be.TryAddInput(Stack(items.Metal, 4)));
    Assert.Equal(Burden.FamilyRemelt, be.Family);
    Assert.False(be.TryAddInput(Stack(items.Iron, 4)));
  }

  [Fact]
  public void A_scrap_batch_drains_as_remelt_burden()
  {
    var (world, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Metal, 12));
    be.TryAddInput(Stack(items.Lime, 1));
    be.TryAddInput(Stack(items.Coke, 1));
    be.AdvanceMixing(1000f, IwexValues.MixerMaxSpeed);

    // A plain container below (the ore bunker only accepts ore burden, so remelt needs a chest).
    var chest = ChestBelow(world, be);

    Assert.True(be.ToggleDrain());
    be.DrainStep(100f);

    ItemSlot filled = chest.Inventory.First(s => !s.Empty);
    Assert.True(Burden.IsRemelt(filled.Itemstack)); // the remelt item, not ore burden
    Assert.False(be.HasReadyBurden);
  }

  [Fact]
  public void Draining_the_batch_unlocks_the_family_for_the_next_one()
  {
    var (world, be, items) = NewMixer();
    be.TryAddInput(Stack(items.Metal, 8));
    be.AdvanceMixing(1000f, IwexValues.MixerMaxSpeed);

    var chest = ChestBelow(world, be);
    be.ToggleDrain();
    be.DrainStep(100f);

    Assert.False(be.HasReadyBurden);
    Assert.Equal("", be.Family); // fully drained -> unlocked

    // A fresh ore batch is now allowed.
    Assert.True(be.TryAddInput(Stack(items.Iron, 4)));
    Assert.Equal(Burden.FamilyOre, be.Family);
  }

  [Fact]
  public void Reloading_a_remelt_burden_locks_the_remelt_family()
  {
    var (_, be, items) = NewMixer();
    var stack = new ItemStack(items.Remelt, 16);
    Burden.Write(stack, new BurdenMix(12f, 1f, 3f));

    Assert.True(be.TryReloadBurden(new DummySlot(stack)));
    Assert.Equal(Burden.FamilyRemelt, be.Family);
    Assert.False(be.TryAddInput(Stack(items.Iron, 4))); // ore now refused
  }

  private static BeChest ChestBelow(TestWorld world, BlockEntityOreMixer be)
  {
    var chest = new BeChest { Pos = be.Pos.DownCopy() };
    var block = TestBlocks.Configure(new Block(), "game:chest-north", 83);
    world.Place(be.Pos.DownCopy(), block, chest);
    world.Initialize(chest); // wires the inventory's Api (LateInitialize), like the bunker test
    return chest;
  }

  // A minimal generic container to drain burden into. The mixer's deposit path treats any
  // BlockEntityContainer with an inventory as a valid drop target (a crate/chest in game).
  private sealed class BeChest : BlockEntityContainer
  {
    private readonly InventoryGeneric _inv = new(9, "testchest", "test", null, null);

    public override InventoryBase Inventory => _inv;
    public override string InventoryClassName => "testchest";

    public override void Initialize(ICoreAPI api)
    {
      base.Initialize(api);
      _inv.LateInitialize("testchest-" + Pos, api);
    }
  }

  #endregion
}
