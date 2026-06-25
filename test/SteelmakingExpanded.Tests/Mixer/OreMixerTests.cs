using ExpandedLib.Blocks.Structures;
using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockStructures.OreBunker.BlockEntities;
using IronworkingExpanded.BlockStructures.OreMixer.BlockEntities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
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
    Item Lime,
    Item Coke,
    Item Charcoal,
    Item Burden
  );

  private static (TestWorld world, BlockEntityOreMixer be, Inputs items) NewMixer()
  {
    var world = new TestWorld();
    var burden = world.RegisterItem("iwex:burden");
    var iron = world.RegisterItem("game:crushed-iron"); // path starts with "crushed-iron"
    var lime = world.RegisterItem("game:lime");
    var coke = world.RegisterItem("game:crushed-coke");
    var charcoal = world.RegisterItem("game:charcoal");

    var block = TestBlocks.Configure(new Block(), "iwex:mixer-north", 80);
    var pos = new BlockPos(0, 5, 0);
    var be = new BlockEntityOreMixer { Pos = pos, Block = block };
    world.Place(pos, block, be);
    world.Initialize(be); // server: resolves the burden item, registers the (unused here) tick

    return (world, be, new Inputs(iron, lime, coke, charcoal, burden));
  }

  private static DummySlot Stack(Item item, int n) => new(new ItemStack(item, n));

  #region Charging

  [Fact]
  public void Held_materials_charge_their_matching_raw_part()
  {
    var (_, be, items) = NewMixer();

    Assert.True(be.TryAddInput(Stack(items.Iron, 12)));
    be.TryAddInput(Stack(items.Lime, 1));
    be.TryAddInput(Stack(items.Coke, 3));

    Assert.Equal(16, be.TotalRaw);
    Assert.Equal(new BurdenMix(12f, 1f, 3f), be.Mix);
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
    Assert.Equal(16, be.ReadyBurden); // count = total raw
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

    Assert.Equal(16, bunker.TotalContents);
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

    Assert.Equal(16, bunker.TotalContents);
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
    Assert.True(be.TryAddInput(Stack(items.Coke, 8)));
    Assert.Equal(new BurdenMix(12f, 1f, 11f), be.Mix); // retuned

    be.AdvanceMixing(1000f, IwexValues.MixerMaxSpeed);
    Assert.True(be.HasReadyBurden);
    Assert.Equal(24, be.ReadyBurden);
  }

  #endregion

  #region Acceptance

  [Fact]
  public void AcceptsAsInput_recognizes_materials_and_burden_but_not_junk()
  {
    var (world, _, items) = NewMixer();
    var stick = world.RegisterItem("game:stick");

    Assert.True(BlockEntityOreMixer.AcceptsAsInput(new ItemStack(items.Iron)));
    Assert.True(BlockEntityOreMixer.AcceptsAsInput(new ItemStack(items.Lime)));
    Assert.True(BlockEntityOreMixer.AcceptsAsInput(new ItemStack(items.Coke)));
    Assert.True(BlockEntityOreMixer.AcceptsAsInput(new ItemStack(items.Charcoal)));
    Assert.True(BlockEntityOreMixer.AcceptsAsInput(new ItemStack(items.Burden)));
    Assert.False(BlockEntityOreMixer.AcceptsAsInput(new ItemStack(stick)));
    Assert.False(BlockEntityOreMixer.AcceptsAsInput(null));
  }

  #endregion

  #region Charcoal fuel

  [Fact]
  public void Two_charcoal_count_as_one_coke_of_fuel()
  {
    var (_, be, items) = NewMixer();

    be.TryAddInput(Stack(items.Charcoal, 6)); // 6 charcoal -> 3 fuel value

    Assert.Equal(3f, be.Mix.Fuel, 3);
  }

  [Fact]
  public void Charcoal_and_coke_burden_grade_the_same_for_equal_fuel_value()
  {
    // 12 iron + 1 flux + 6 charcoal (=3 fuel) matches 12/1/3 coke - same proportions, same grade.
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
}
