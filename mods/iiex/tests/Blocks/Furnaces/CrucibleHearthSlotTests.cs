using System.Linq;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.Items;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The four melting holes: seating a pot, charging it, and pulling it back out. The holes are sub-block -
/// one cell holds all four - so the hearth decides which hole a gesture lands on, and these cases pin that
/// rule and the mass that rides through it.
/// </summary>
public class CrucibleHearthSlotTests {
  #region Harness

  private static readonly BlockPos At = new(0, 16, 0);

  private static (BlockEntityCrucibleHearth Hearth, TestWorld World) Stood() {
    var world = new TestWorld();
    world.World.Side.Returns(EnumAppSide.Server);
    world.Register(
      TestBlocks.Configure(
        new BlockSteelCrucible(),
        "iiex:steelcrucible-burned",
        910,
        ("type", "burned")
      )
    );
    world.Register(
      TestBlocks.Configure(
        new BlockSteelCruciblePour(),
        "iiex:steelcrucible-smelted",
        911,
        ("type", "smelted")
      )
    );
    world.RegisterItem(BlisterBreaking.ChunkCode);
    world.RegisterItem(BlisterBreaking.BitCode);
    world.RegisterItem("iiex:ingot-cruciblesteel");

    var hearth = new BlockEntityCrucibleHearth { Pos = At.Copy() };
    world.Place(
      At,
      TestBlocks.Configure(
        new Block(),
        "iiex:furnace-cruciblehearth-tier1-n",
        905
      ),
      hearth
    );
    world.Attach(hearth);
    return (hearth, world);
  }

  private static ItemStack Pot(TestWorld world, int firings = 0) {
    var stack = new ItemStack(
      world.World.GetBlock(new AssetLocation("iiex:steelcrucible-burned"))
    );
    if (firings > 0)
      CrucibleFiring.Set(stack, firings);
    return stack;
  }

  private static ItemStack Chunks(TestWorld world, int count) =>
    new(
      world.World.GetItem(new AssetLocation(BlisterBreaking.ChunkCode)),
      count
    );

  private static ItemStack Bits(TestWorld world, int count) =>
    new(world.World.GetItem(new AssetLocation(BlisterBreaking.BitCode)), count);

  #endregion

  #region Seating

  /// <summary>
  /// A pot goes in the first free hole, and four is the whole bank. The fifth is refused rather than
  /// swallowed: the hearth is the only thing that knows the holes are full, so a silent take here would
  /// destroy the pot.
  /// </summary>
  [Fact]
  public void Four_pots_seat_and_the_fifth_is_refused() {
    (BlockEntityCrucibleHearth hearth, TestWorld world) = Stood();

    for (int i = 0; i < CrucibleHearthLayout.Holes; i++) {
      Assert.True(hearth.Seat(Pot(world)));
      Assert.Equal(i + 1, hearth.Holes.Count(h => h.Occupied));
    }

    Assert.False(hearth.Seat(Pot(world)));
    Assert.Equal(
      CrucibleHearthLayout.Holes,
      hearth.Holes.Count(h => h.Occupied)
    );
  }

  /// <summary>
  /// Only a burned pot. A raw one has not been fired and a full one is not a container the hearth can
  /// stand up; both are refused rather than seated as an empty pot, which would destroy the difference.
  /// </summary>
  [Fact]
  public void Only_a_fired_and_empty_pot_is_seated() {
    (BlockEntityCrucibleHearth hearth, TestWorld world) = Stood();
    world.Register(
      TestBlocks.Configure(
        new BlockSteelCrucible(),
        "iiex:steelcrucible-raw",
        909,
        ("type", "raw")
      )
    );

    Assert.False(
      hearth.Seat(
        new ItemStack(
          world.World.GetBlock(new AssetLocation("iiex:steelcrucible-raw"))
        )
      )
    );
    Assert.False(hearth.Seat(null));
    Assert.All(hearth.Holes, h => Assert.False(h.Occupied));
  }

  /// <summary>
  /// The pot's age rides through the furnace. A pot is a consumable with three heats in it, so a hearth
  /// that handed back a fresh one would make it immortal - which is the whole ceiling the pot enforces.
  /// </summary>
  [Fact]
  public void A_pots_age_survives_being_seated_and_pulled() {
    (BlockEntityCrucibleHearth hearth, TestWorld world) = Stood();

    Assert.True(hearth.Seat(Pot(world, firings: 2)));
    ItemStack? back = hearth.Pull();

    Assert.NotNull(back);
    Assert.Equal(2, CrucibleFiring.Of(back));
  }

  #endregion

  #region Charging

  /// <summary>
  /// A pot takes exactly its charge and no more, and the arithmetic is whole: four crushed chunks and two
  /// vanilla bits is 110 u exactly, which is what one ingot's crush plus a tenth comes to.
  /// </summary>
  [Fact]
  public void A_pot_takes_its_charge_exactly() {
    (BlockEntityCrucibleHearth hearth, TestWorld world) = Stood();
    hearth.Seat(Pot(world));

    Assert.Equal(4, hearth.Charge(Chunks(world, 4)));
    Assert.Equal(2, hearth.Charge(Bits(world, 8)));

    Assert.Equal(IiexValues.CruciblePotChargeUnits, hearth.Holes[0].Charge);
    Assert.True(hearth.Holes[0].Charged);
  }

  /// <summary>
  /// A charge runs over into the next pot rather than stopping at the first. One gesture fills the bank,
  /// which is what makes four holes worth having.
  /// </summary>
  [Fact]
  public void A_charge_spills_into_the_next_pot() {
    (BlockEntityCrucibleHearth hearth, TestWorld world) = Stood();
    hearth.Seat(Pot(world));
    hearth.Seat(Pot(world));

    // Nine chunks is 225 u: 110 into the first pot, 110 into the second, and one chunk left in hand
    // because 5 u of the second pot's charge has to come from bits.
    int taken = hearth.Charge(Chunks(world, 9));

    Assert.Equal(8, taken);
    Assert.Equal(100, hearth.Holes[0].Charge + 0);
    Assert.Equal(100, hearth.Holes[1].Charge);
  }

  /// <summary>
  /// Nothing goes into an empty hole. The block reports it as its own refusal rather than letting the
  /// gesture fall through to the fuel bed, which would tell the player blister steel is not fuel - true,
  /// and not the reason.
  /// </summary>
  [Fact]
  public void Blister_steel_needs_a_pot_to_go_into() {
    (BlockEntityCrucibleHearth hearth, TestWorld world) = Stood();

    Assert.Equal(0, hearth.Charge(Chunks(world, 4)));
    Assert.True(BlockEntityCrucibleHearth.IsBlister(Chunks(world, 1)));
    Assert.False(BlockEntityCrucibleHearth.IsBlister(Pot(world)));
  }

  #endregion

  #region Pulling

  /// <summary>
  /// A finished pot comes out as vanilla's own pourable container, carrying the metal and the units the
  /// molten path reads. Anything else and the pot would be unpourable - the canal accepts a
  /// <see cref="BlockSmeltedContainer"/> and asks it what it holds.
  /// </summary>
  [Fact]
  public void A_finished_pot_pours_what_the_heat_made() {
    (BlockEntityCrucibleHearth hearth, TestWorld world) = Stood();
    hearth.Seat(Pot(world, firings: 1));
    hearth.Charge(Chunks(world, 4));
    hearth.Charge(Bits(world, 2));
    Melt(hearth);

    ItemStack? pulled = hearth.Pull();

    Assert.NotNull(pulled);
    Assert.IsAssignableFrom<BlockSmeltedContainer>(pulled!.Block);
    (string metal, int units) = Contents(world, pulled);

    Assert.Equal("iiex:ingot-cruciblesteel", metal);
    Assert.Equal(IiexValues.CruciblePotYieldUnits, units);
    // The heat the pot just gave counts against its three.
    Assert.Equal(2, CrucibleFiring.Of(pulled));
  }

  /// <summary>
  /// A finished pot comes out before an unfinished one. An empty-handed player is reaching for the heat,
  /// not dismantling the charge standing behind it.
  /// </summary>
  [Fact]
  public void A_finished_pot_is_pulled_before_an_unfinished_one() {
    (BlockEntityCrucibleHearth hearth, TestWorld world) = Stood();
    hearth.Seat(Pot(world));
    hearth.Seat(Pot(world));
    hearth.Charge(Chunks(world, 4));
    hearth.Charge(Bits(world, 2));
    // Only the first pot is charged, so only it melts.
    Melt(hearth);

    ItemStack? pulled = hearth.Pull();

    Assert.NotNull(pulled);
    Assert.IsType<BlockSteelCruciblePour>(pulled!.Block);
    Assert.False(hearth.Holes[0].Occupied);
    Assert.True(hearth.Holes[1].Occupied);
  }

  /// <summary>
  /// An empty bank gives nothing, which is what lets the firebox's own fuel gesture through underneath it.
  /// </summary>
  [Fact]
  public void An_empty_bank_pulls_nothing() {
    (BlockEntityCrucibleHearth hearth, _) = Stood();

    Assert.Null(hearth.Pull());
  }

  #endregion

  #region Across a save

  /// <summary>
  /// Every hole's state survives a round trip. A heat runs for the better part of a fuel charge, so a
  /// reload mid-melt that reset the pots would lose the charge and the pot with it.
  /// </summary>
  [Fact]
  public void The_holes_survive_a_round_trip() {
    (BlockEntityCrucibleHearth hearth, TestWorld world) = Stood();
    hearth.Seat(Pot(world, firings: 2));
    hearth.Charge(Chunks(world, 2));
    hearth.Holes[0].SoakTick(IiexValues.CruciblePreheatSec);

    var tree = new Vintagestory.API.Datastructures.TreeAttribute();
    hearth.ToTreeAttributes(tree);

    var elsewhere = At.AddCopy(4, 0, 0);
    var loaded = new BlockEntityCrucibleHearth { Pos = elsewhere };
    world.Place(
      elsewhere,
      TestBlocks.Configure(
        new Block(),
        "iiex:furnace-cruciblehearth-tier1-n",
        905
      ),
      loaded
    );
    world.Attach(loaded);
    loaded.FromTreeAttributes(tree, world.World);

    Assert.True(loaded.Holes[0].Occupied);
    Assert.Equal(2, loaded.Holes[0].Firings);
    Assert.Equal(50, loaded.Holes[0].Charge);
    Assert.True(loaded.Holes[0].Preheated);
  }

  /// <summary>
  /// A hearth broken mid-heat gives back the pots and their charge. The puddling hearth's own mistake was
  /// swallowing what it held; four pots and 440 u of blister steel is a great deal more to swallow.
  /// </summary>
  [Fact]
  public void Breaking_the_hearth_gives_back_the_pots_and_their_charge() {
    (BlockEntityCrucibleHearth hearth, TestWorld world) = Stood();
    hearth.Seat(Pot(world, firings: 1));
    hearth.Charge(Chunks(world, 4));

    var drops = hearth.HoleDrops().ToList();

    Assert.Contains(drops, s => s.Block is BlockSteelCrucible);
    Assert.Equal(
      100,
      drops
        .Where(s => s.Collectible.Code?.ToString() == BlisterBreaking.ChunkCode)
        .Sum(s => s.StackSize * BlisterBreaking.ChunkUnits)
    );
    Assert.All(hearth.Holes, h => Assert.False(h.Occupied));
  }

  #endregion

  /// <summary>
  /// What a pourable pot says it holds, read through vanilla's own <c>GetContents</c> where the version
  /// exposes it - so the two attributes are checked against the code that consumes them rather than
  /// against the code that wrote them. It is not public before 1.22, where the raw keys are read instead.
  /// </summary>
  private static (string Metal, int Units) Contents(
    TestWorld world,
    ItemStack pot
  ) {
    System.Reflection.MethodInfo? read = pot
      .Block.GetType()
      .GetMethod("GetContents", [typeof(IWorldAccessor), typeof(ItemStack)]);
    if (read != null) {
      var kv = (System.Collections.Generic.KeyValuePair<ItemStack, int>)
        read.Invoke(pot.Block, [world.World, pot])!;
      return (kv.Key.Collectible.Code.ToString(), kv.Value);
    }

    ItemStack? output = pot.Attributes.GetItemstack(CruciblePot.ContentsKey);
    output?.ResolveBlockOrItem(world.World);
    return (
      output!.Collectible.Code.ToString(),
      pot.Attributes.GetInt(CruciblePot.UnitsKey)
    );
  }

  private static void Melt(BlockEntityCrucibleHearth hearth) {
    foreach (CrucibleHole hole in hearth.Holes)
      hole.SoakTick(IiexValues.CruciblePreheatSec);
    hearth.MeltStep(IiexValues.CrucibleMeltSec);
  }
}
