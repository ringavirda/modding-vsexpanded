using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Products.BlockEntities;
using IronworkingExpanded.Items;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The blast furnace's melting math, driven directly: the per-cycle conversion of hearth blast mix
/// into molten iron + slag (capacity-clamped) and the hearth blast-mix accounting. These are the
/// numbers the firing/melting tick relies on but the gated <c>OnProductionTick</c> made unreachable.
/// </summary>
public class BlastFurnaceLifecycleTests
{
  private static TestWorld NewWorld()
  {
    var world = new TestWorld();
    world.RegisterItem("game:ingot-iron", 1500f);
    world.RegisterItem("iwex:slag");
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

  /// <summary>A hearth coal pile holding <paramref name="units"/> of legacy blast mix, lit.</summary>
  private static BlockEntityCoalPile BlastmixPile(
    TestWorld world,
    BlockPos pos,
    int units
  ) => ChargePile(world, pos, "blastmix", units, null);

  /// <summary>A hearth coal pile holding prepared burden of a known composition, lit.</summary>
  private static BlockEntityCoalPile BurdenPile(
    TestWorld world,
    BlockPos pos,
    int units,
    BurdenMix mix
  ) => ChargePile(world, pos, "burden", units, mix);

  private static BlockEntityCoalPile ChargePile(
    TestWorld world,
    BlockPos pos,
    string itemPath,
    int units,
    BurdenMix? mix
  )
  {
    var pile = new BlockEntityCoalPile { Pos = pos.Copy() };
    // Pass a real Api so slot.MarkDirty() (DidModifyItemSlot) doesn't NRE when ConsumeForMelting
    // takes charge out of the slot.
    var inv = new InventoryGeneric(1, "coalpile", "test", world.Api, null);
    var charge = new Item
    {
      Code = new AssetLocation("iwex", itemPath),
      ItemId = 4242,
    };
    var stack = new ItemStack(charge, units);
    inv[0].Itemstack = stack;
    if (mix != null)
      Burden.Write(stack, mix.Value);
    ReflectionHelpers.SetField(pile, "inventory", inv);
    ReflectionHelpers.SetField(pile, "burning", true);
    world.Place(
      pos,
      TestBlocks.Configure(new Block(), "game:coalpile", 50 + pos.Y),
      pile
    );
    world.Attach(pile);
    return pile;
  }

  private static List<(BlockPos, BlockEntityCoalPile)> Piles(
    params (BlockPos, BlockEntityCoalPile)[] p
  ) => new(p);

  private static int Mix(BlockEntityCoalPile pile) =>
    pile.inventory[0].StackSize;

  private static float Iron(BlockEntityBlastFurnaceHot be) =>
    (float)ReflectionHelpers.GetField(be, "_moltenIron")!;

  private static float Slag(BlockEntityBlastFurnaceHot be) =>
    (float)ReflectionHelpers.GetField(be, "_moltenSlag")!;

  #region ConsumeForMelting

  [Fact]
  public void A_melt_cycle_burns_blast_mix_into_molten_iron_and_slag()
  {
    var world = NewWorld();
    var be = Furnace(world);
    var pile = BlastmixPile(world, new BlockPos(0, 13, 0), 100);

    ReflectionHelpers.Invoke(
      be,
      "ConsumeForMelting",
      Piles((pile.Pos, pile)),
      16,
      60f,
      10f
    );

    Assert.Equal(84, Mix(pile)); // 16 blast mix consumed
    Assert.Equal(60f, Iron(be), 3); // one cycle of iron
    Assert.Equal(10f, Slag(be), 3); // one cycle of slag
  }

  [Fact]
  public void Molten_output_is_clamped_at_the_furnace_capacity()
  {
    var world = NewWorld();
    var be = Furnace(world);
    var pile = BlastmixPile(world, new BlockPos(0, 13, 0), 100);
    // Already near the iron ceiling (2400 default).
    ReflectionHelpers.SetField(
      be,
      "_moltenIron",
      IwexValues.BfMaxMoltenIron - 10f
    );

    ReflectionHelpers.Invoke(
      be,
      "ConsumeForMelting",
      Piles((pile.Pos, pile)),
      16,
      60f,
      10f
    );

    Assert.Equal(IwexValues.BfMaxMoltenIron, Iron(be), 1); // capped, not 2450
  }

  [Fact]
  public void Melting_draws_from_the_upper_piles_first()
  {
    var world = NewWorld();
    var be = Furnace(world);
    var low = BlastmixPile(world, new BlockPos(0, 12, 0), 50);
    var high = BlastmixPile(world, new BlockPos(0, 14, 0), 50);

    ReflectionHelpers.Invoke(
      be,
      "ConsumeForMelting",
      Piles((low.Pos, low), (high.Pos, high)),
      16,
      60f,
      10f
    );

    Assert.Equal(34, Mix(high)); // the higher pile is drained first
    Assert.Equal(50, Mix(low)); // the lower pile is untouched
  }

  #endregion

  #region Blast-mix accounting

  /// <summary>Runs the charge scan, handing back its out-parameters (isFull + mix; the rejected count
  /// and family the family gate reads are exercised in the gate tests below).</summary>
  private static int CountCharge(
    BlockEntityBlastFurnaceHot be,
    List<(BlockPos, BlockEntityCoalPile)> piles,
    out bool isFull,
    out BurdenMix mix
  )
  {
    object?[] args = { piles, false, default(BurdenMix), 0, null };
    int count = (int)ReflectionHelpers.Invoke(be, "GetBlastMixCount", args)!;
    isFull = (bool)args[1]!;
    mix = (BurdenMix)args[2]!;
    return count;
  }

  [Fact]
  public void Mix_count_totals_the_hearth_and_reports_full_at_the_fire_threshold()
  {
    var world = NewWorld();
    var be = Furnace(world);
    var pile = BlastmixPile(
      world,
      new BlockPos(0, 13, 0),
      IwexValues.BlastMixRequiredToFire
    );

    int count = CountCharge(
      be,
      Piles((pile.Pos, pile)),
      out bool isFull,
      out _
    );

    Assert.Equal(IwexValues.BlastMixRequiredToFire, count);
    Assert.True(isFull, "a hearth at the threshold should read as full");
  }

  [Fact]
  public void A_thin_charge_does_not_read_as_full()
  {
    var world = NewWorld();
    var be = Furnace(world);
    var pile = BlastmixPile(world, new BlockPos(0, 13, 0), 10);

    int count = CountCharge(
      be,
      Piles((pile.Pos, pile)),
      out bool isFull,
      out _
    );

    Assert.Equal(10, count);
    Assert.False(isFull);
  }

  [Fact]
  public void Stamped_burden_reports_its_own_coke_fraction()
  {
    var world = NewWorld();
    var be = Furnace(world);
    var pile = BurdenPile(
      world,
      new BlockPos(0, 13, 0),
      100,
      new BurdenMix(65f, 5f, 30f)
    );

    CountCharge(be, Piles((pile.Pos, pile)), out _, out BurdenMix mix);

    Assert.Equal(0.30f, mix.FuelFrac, 3);
  }

  [Fact]
  public void Legacy_blast_mix_reads_as_a_standard_grade_burden()
  {
    // Charge stamped before burden compositions existed has to keep burning the way it used to, or
    // every existing world's furnace would collapse to the no-coke end of the heat balance.
    var world = NewWorld();
    var be = Furnace(world);
    var pile = BlastmixPile(world, new BlockPos(0, 13, 0), 100);

    CountCharge(be, Piles((pile.Pos, pile)), out _, out BurdenMix mix);

    Assert.Equal(IwexValues.BfDefaultFuelFrac, mix.FuelFrac, 3);
    Assert.Equal(
      "iwex:burden-profile-standard",
      Burden.ProfileLangKey(mix) // and it must not read as a flux shortfall
    );
  }

  [Fact]
  public void A_mixed_column_reads_the_volume_weighted_average_coke_ratio()
  {
    var world = NewWorld();
    var be = Furnace(world);
    var rich = BurdenPile(
      world,
      new BlockPos(0, 13, 0),
      300,
      new BurdenMix(60f, 5f, 35f)
    );
    var lean = BurdenPile(
      world,
      new BlockPos(0, 12, 0),
      100,
      new BurdenMix(90f, 5f, 5f)
    );

    CountCharge(
      be,
      Piles((rich.Pos, rich), (lean.Pos, lean)),
      out _,
      out BurdenMix mix
    );

    // 300 units at 35% + 100 at 5% = 27.5%, not the 20% a naive per-pile mean would give.
    Assert.Equal(0.275f, mix.FuelFrac, 3);
  }

  #endregion

  #region Extinguish residue

  /// <summary>World cells of the bottommost shaft layer, where the molten pool freezes.</summary>
  private static BlockPos[] BottomLayer(BlockEntityBlastFurnaceHot be) =>
    ((Vec3i[])ReflectionHelpers.GetProperty(be, "SolidifyCells")!)
      .Select(c =>
        (BlockPos)ReflectionHelpers.Invoke(be, "GetGlobalPos", c.X, c.Y, c.Z)!
      )
      .ToArray();

  [Fact]
  public void Extinguishing_a_melt_solidifies_the_iron_across_the_bottom_layer()
  {
    var world = NewWorld();
    // Give the block an entity class + factory so SetBlock spawns a real BlockEntitySolidifiedIron,
    // the way the engine would - otherwise the nugget count has nothing to be stamped onto and the
    // even-split half of the behaviour goes unasserted.
    Block iron = TestBlocks.Configure(new Block(), "iwex:solidifiediron", 700);
    iron.EntityClass = "solidifiediron";
    world.RegisterBlockEntityFactory(
      "solidifiediron",
      () => new BlockEntitySolidifiedIron()
    );
    world.Register(iron);

    var be = Furnace(world); // no charge piles -> the burnout walk is a no-op
    ReflectionHelpers.SetProperty(be, nameof(be.State), FurnaceState.Melting);
    ReflectionHelpers.SetField(be, "_moltenIron", 50f);

    ReflectionHelpers.Invoke(be, "Extinguish");

    Assert.Equal(FurnaceState.Idle, be.State);
    Assert.Equal(0f, Iron(be), 3); // the molten pool is gone

    // The pool freezes onto the hearth floor - every free cell of it, not two fixed cells - and the
    // nuggets are split evenly, so the same wreck is left every time.
    BlockPos[] floor = BottomLayer(be);
    Assert.NotEmpty(floor);
    Assert.All(
      floor,
      p => Assert.Equal(iron.BlockId, world.GetBlock(p).BlockId)
    );

    int expectedTotal = (int)(50f / IwexValues.BfUnitsPerSolidNugget);
    Assert.Equal(
      expectedTotal,
      floor.Sum(p =>
        ((BlockEntitySolidifiedIron)world.GetBlockEntity(p)!).MetalCount
      )
    );
  }

  [Fact]
  public void Extinguishing_burns_the_burden_out_by_height_instead_of_slagging_it()
  {
    var world = NewWorld();
    // Well clear of the coal piles' ids, which are derived from their Y.
    Block slag = TestBlocks.Configure(new Block(), "iwex:slag", 701);
    world.Register(slag);
    var be = Furnace(world);

    // Two piles at opposite ends of the shaft, both carrying the same standard-grade burden.
    var mix = new BurdenMix(0.75f, 0.05f, 0.20f);
    BlockPos bottom = (BlockPos)
      ReflectionHelpers.Invoke(be, "GetGlobalPos", 0, 1, 0)!;
    BlockPos top = (BlockPos)
      ReflectionHelpers.Invoke(be, "GetGlobalPos", 0, 5, 0)!;
    var lowPile = BurdenPile(world, bottom, 100, mix);
    var highPile = BurdenPile(world, top, 100, mix);

    ReflectionHelpers.SetProperty(be, nameof(be.State), FurnaceState.Melting);
    ReflectionHelpers.Invoke(be, "Extinguish");

    // (a) The burden is still burden. Nothing on this path makes slag - a furnace going out is a
    // setback, not the loss of the whole charge.
    Assert.NotEqual(slag.BlockId, world.GetBlock(bottom).BlockId);
    Assert.NotEqual(slag.BlockId, world.GetBlock(top).BlockId);

    BurdenMix low = Burden.Read(lowPile.inventory[0].Itemstack);
    BurdenMix high = Burden.Read(highPile.inventory[0].Itemstack);

    // (b) Coke burns out by height: the bottom pile sat on the tuyeres, the top one never saw blast.
    Assert.Equal(0.20f * IwexValues.BfBurnoutFuelRetainedBottom, low.Fuel, 4);
    Assert.Equal(0.20f * IwexValues.BfBurnoutFuelRetainedTop, high.Fuel, 4);
    Assert.True(high.Fuel > low.Fuel);

    // (c) Iron and flux are preserved verbatim, so the salvage can be re-coked and charged again.
    foreach (BurdenMix m in new[] { low, high })
    {
      Assert.Equal(0.75f, m.Iron, 4);
      Assert.Equal(0.05f, m.Flux, 4);
    }

    // (d) A stripped burden grades as burned out, so the tooltip says "re-coke it" rather than
    // passing the salvage off as a deliberately low-coke grade.
    Assert.Equal("iwex:burden-profile-burnedout", Burden.ProfileLangKey(low));
  }

  [Fact]
  public void A_second_extinguish_burns_the_already_spent_burden_out_no_further()
  {
    // Dirty-precondition pass: re-lighting and losing a furnace on salvaged burden must not keep
    // eating iron and flux, and must not underflow the fuel it already stripped.
    var world = NewWorld();
    var be = Furnace(world);
    BlockPos bottom = (BlockPos)
      ReflectionHelpers.Invoke(be, "GetGlobalPos", 0, 1, 0)!;
    var pile = BurdenPile(
      world,
      bottom,
      100,
      new BurdenMix(0.75f, 0.05f, 0.20f)
    );

    ReflectionHelpers.SetProperty(be, nameof(be.State), FurnaceState.Melting);
    ReflectionHelpers.Invoke(be, "Extinguish");
    BurdenMix afterFirst = Burden.Read(pile.inventory[0].Itemstack);

    ReflectionHelpers.SetProperty(be, nameof(be.State), FurnaceState.Melting);
    ReflectionHelpers.Invoke(be, "Extinguish");
    BurdenMix afterSecond = Burden.Read(pile.inventory[0].Itemstack);

    Assert.Equal(afterFirst.Iron, afterSecond.Iron, 4);
    Assert.Equal(afterFirst.Flux, afterSecond.Flux, 4);
    Assert.True(afterSecond.Fuel >= 0f);
  }

  #endregion

  #region Burden family gate

  // The blast furnace burns ore burden only. Remelt burden (the cupola's charge) counts toward the burn
  // - so a mis-loaded shaft still lights and burns out - but is tallied as rejected, which blocks the
  // conversion and never lets the wrong family become molten iron. These pin the charge-scan half; the
  // tick-level "never converts / stays Firing" half is in BlastFurnaceScenarioTests.

  /// <summary>A hearth pile of the cupola's remelt burden - the wrong family for a blast furnace.</summary>
  private static BlockEntityCoalPile RemeltBurdenPile(
    TestWorld world,
    BlockPos pos,
    int units,
    BurdenMix mix
  ) => ChargePile(world, pos, "remeltburden", units, mix);

  /// <summary>Runs the charge scan, surfacing the rejected-family count and token too.</summary>
  private static int CountChargeFull(
    BlockEntityBlastFurnaceHot be,
    List<(BlockPos, BlockEntityCoalPile)> piles,
    out int rejectedCount,
    out string? rejectedFamily
  )
  {
    object?[] args = { piles, false, default(BurdenMix), 0, null };
    int count = (int)ReflectionHelpers.Invoke(be, "GetBlastMixCount", args)!;
    rejectedCount = (int)args[3]!;
    rejectedFamily = (string?)args[4];
    return count;
  }

  [Fact]
  public void Remelt_burden_is_counted_but_rejected_as_the_wrong_family()
  {
    var world = NewWorld();
    var be = Furnace(world);
    var pile = RemeltBurdenPile(
      world,
      new BlockPos(0, 13, 0),
      100,
      new BurdenMix(60f, 5f, 35f)
    );

    int total = CountChargeFull(
      be,
      Piles((pile.Pos, pile)),
      out int rejected,
      out string? family
    );

    Assert.Equal(100, total); // counted family-blind, so the shaft still lights and burns
    Assert.Equal(100, rejected); // ...but every unit is the wrong family
    Assert.Equal(Burden.FamilyRemelt, family);
  }

  [Fact]
  public void Ore_burden_is_accepted_with_no_rejected_charge()
  {
    var world = NewWorld();
    var be = Furnace(world);
    var pile = BurdenPile(
      world,
      new BlockPos(0, 13, 0),
      100,
      new BurdenMix(75f, 5f, 20f)
    );

    CountChargeFull(
      be,
      Piles((pile.Pos, pile)),
      out int rejected,
      out string? family
    );

    Assert.Equal(0, rejected);
    Assert.Null(family);
  }

  [Fact]
  public void A_mixed_shaft_rejects_only_the_wrong_family_pile()
  {
    var world = NewWorld();
    var be = Furnace(world);
    var ore = BurdenPile(
      world,
      new BlockPos(0, 13, 0),
      200,
      new BurdenMix(75f, 5f, 20f)
    );
    var wrong = RemeltBurdenPile(
      world,
      new BlockPos(0, 12, 0),
      50,
      new BurdenMix(60f, 5f, 35f)
    );

    int total = CountChargeFull(
      be,
      Piles((ore.Pos, ore), (wrong.Pos, wrong)),
      out int rejected,
      out string? family
    );

    Assert.Equal(250, total); // both piles burn
    Assert.Equal(50, rejected); // only the remelt pile is rejected
    Assert.Equal(Burden.FamilyRemelt, family);
  }

  [Fact]
  public void Melting_does_not_draw_from_a_wrong_family_pile()
  {
    // Defensive: even if the melt cycle is reached, a rejected pile is never eaten (the tick's
    // conversion block stops the cycle running while any rejected pile is present).
    var world = NewWorld();
    var be = Furnace(world);
    var pile = RemeltBurdenPile(
      world,
      new BlockPos(0, 13, 0),
      100,
      new BurdenMix(60f, 5f, 35f)
    );

    ReflectionHelpers.Invoke(
      be,
      "ConsumeForMelting",
      Piles((pile.Pos, pile)),
      16,
      60f,
      10f
    );

    Assert.Equal(100, Mix(pile)); // the wrong-family charge is untouched
  }

  [Fact]
  public void A_wrong_family_charge_burns_out_on_extinguish_not_destroyed()
  {
    var world = NewWorld();
    Block slag = TestBlocks.Configure(new Block(), "iwex:slag", 701);
    world.Register(slag);
    var be = Furnace(world);
    BlockPos bottom = (BlockPos)
      ReflectionHelpers.Invoke(be, "GetGlobalPos", 0, 1, 0)!;
    var pile = RemeltBurdenPile(
      world,
      bottom,
      100,
      new BurdenMix(0.60f, 0.05f, 0.35f)
    );

    ReflectionHelpers.SetProperty(be, nameof(be.State), FurnaceState.Melting);
    ReflectionHelpers.Invoke(be, "Extinguish");

    // Still remelt burden salvage - not slag, not destroyed: metal + flux kept, coke burned out.
    Assert.NotEqual(slag.BlockId, world.GetBlock(bottom).BlockId);
    Assert.True(Burden.IsRemelt(pile.inventory[0].Itemstack));
    BurdenMix left = Burden.Read(pile.inventory[0].Itemstack);
    Assert.Equal(0.60f, left.Iron, 4);
    Assert.Equal(0.05f, left.Flux, 4);
    Assert.Equal(0.35f * IwexValues.BfBurnoutFuelRetainedBottom, left.Fuel, 4);
  }

  [Fact]
  public void Extinguishing_does_not_freeze_iron_over_a_wrong_family_pile()
  {
    // A furnace holding molten iron from an earlier clean melt, which then took a wrong pile into a
    // bottom cell, must not overwrite that pile with solid iron - it is the player's salvage.
    var world = NewWorld();
    Block iron = TestBlocks.Configure(new Block(), "iwex:solidifiediron", 700);
    iron.EntityClass = "solidifiediron";
    world.RegisterBlockEntityFactory(
      "solidifiediron",
      () => new BlockEntitySolidifiedIron()
    );
    world.Register(iron);

    var be = Furnace(world);
    BlockPos bottom = (BlockPos)
      ReflectionHelpers.Invoke(be, "GetGlobalPos", 0, 1, 0)!;
    var wrong = RemeltBurdenPile(
      world,
      bottom,
      100,
      new BurdenMix(0.60f, 0.05f, 0.35f)
    );
    ReflectionHelpers.SetProperty(be, nameof(be.State), FurnaceState.Melting);
    ReflectionHelpers.SetField(be, "_moltenIron", 50f);

    ReflectionHelpers.Invoke(be, "Extinguish");

    Assert.NotEqual(iron.BlockId, world.GetBlock(bottom).BlockId); // pile not overwritten
    Assert.True(Burden.IsRemelt(wrong.inventory[0].Itemstack)); // salvage survives
  }

  [Fact]
  public void The_rejected_charge_state_round_trips_through_the_tree()
  {
    // GetBlockInfo runs client-side and never walks the charge, so the mismatch line the HUD prints
    // has to ride the save tree - the same reason _cachedMixCount does.
    var world = NewWorld();
    var src = Furnace(world);
    ReflectionHelpers.SetField(src, "_cachedRejectedCount", 48);
    ReflectionHelpers.SetField(src, "_cachedRejectedFamily", Burden.FamilyRemelt);

    var tree = new Vintagestory.API.Datastructures.TreeAttribute();
    src.ToTreeAttributes(tree);
    var dst = Furnace(world);
    dst.FromTreeAttributes(tree, world.World);

    Assert.Equal(
      48,
      (int)ReflectionHelpers.GetField(dst, "_cachedRejectedCount")!
    );
    Assert.Equal(
      Burden.FamilyRemelt,
      (string?)ReflectionHelpers.GetField(dst, "_cachedRejectedFamily")
    );
  }

  #endregion
}
