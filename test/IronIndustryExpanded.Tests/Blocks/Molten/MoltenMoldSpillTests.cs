using ExpandedLib.Metals;
using ExpandedLib.Testing;
using IronIndustryExpanded;
using IronIndustryExpanded.BlockNetworkMolten.Blocks;
using IronIndustryExpanded.BlockStructures.Casting.Blocks;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The in-hand mold safety helper <see cref="MoltenMoldSpill"/>. Covers which molds it handles
/// (<see cref="MoltenMoldSpill.IsHandledMold"/>: iiex cast molds always, vanilla clay molds only when
/// <c>EnhanceVanillaMolds</c> is set) and the spill rule: a mold holding still-liquid metal empties
/// when it leaves the active hand, a hardened cast stays put.
/// </summary>
public class MoltenMoldSpillTests {
  private const string Iron = "game:ingot-iron";

  private static TestWorld NewWorld() {
    var world = new TestWorld();
    world.RegisterItem(Iron, 1500f);
    return world;
  }

  private static ItemStack CastMold(
    TestWorld world,
    float? temp,
    int units,
    int id
  ) {
    var block = TestBlocks.Configure(
      new BlockCastMold(),
      "iiex:casting-mold-ingot",
      id,
      ("tooltype", "plate")
    );
    world.Register(block);
    var stack = new ItemStack(block);
    if (temp is { } t)
      MoltenContents.Write(
        stack,
        MoltenContents.MoldUnitsKey,
        MoltenMetal.CreateStack(world.World, Iron, t)!,
        units
      );
    return stack;
  }

  #region IsHandledMold

  [Fact]
  public void Our_cast_mold_is_always_handled() {
    var mold = TestBlocks.Configure(
      new BlockCastMold(),
      "iiex:casting-mold-ingot",
      3110,
      ("tooltype", "plate")
    );
    Assert.True(MoltenMoldSpill.IsHandledMold(mold));
  }

  [Fact]
  public void A_vanilla_clay_mold_is_not_handled_by_default() {
    var clay = TestBlocks.Configure(
      new BlockToolMold(),
      "game:toolmold-ingot",
      3111,
      ("tooltype", "ingot")
    );
    Assert.False(MoltenMoldSpill.IsHandledMold(clay));
  }

  [Fact]
  public void A_vanilla_clay_mold_is_handled_when_enhanced() {
    var clay = TestBlocks.Configure(
      new BlockToolMold(),
      "game:toolmold-ingot",
      3112,
      ("tooltype", "ingot")
    );
    try {
      IiexValues.Edit(c => c.EnhanceVanillaMolds = true);
      Assert.True(MoltenMoldSpill.IsHandledMold(clay));
    } finally {
      IiexValues.Edit(c => c.EnhanceVanillaMolds = false);
    }
  }

  [Fact]
  public void Null_and_plain_blocks_are_not_handled() {
    Assert.False(MoltenMoldSpill.IsHandledMold(null));
    Assert.False(MoltenMoldSpill.IsHandledMold(new Block()));
  }

  #endregion

  #region SpillIfMolten

  [Fact]
  public void A_liquid_cast_mold_spills_out_of_hand() {
    var world = NewWorld();
    var stack = CastMold(world, 1550f, 20, 3120); // above the liquid line for iron

    Assert.True(
      MoltenMoldSpill.SpillIfMolten(new DummySlot(stack), world.World, null)
    );

    var (contents, fill) = MoltenContents.Read(
      stack,
      MoltenContents.MoldUnitsKey,
      world.World
    );
    Assert.Null(contents); // emptied
    Assert.Equal(0, fill);
  }

  [Fact]
  public void A_hardened_cast_stays_put() {
    var world = NewWorld();
    var stack = CastMold(world, 300f, 20, 3121); // well below the hardened line

    Assert.False(
      MoltenMoldSpill.SpillIfMolten(new DummySlot(stack), world.World, null)
    );

    var (contents, fill) = MoltenContents.Read(
      stack,
      MoltenContents.MoldUnitsKey,
      world.World
    );
    Assert.NotNull(contents);
    Assert.Equal(20, fill);
  }

  [Fact]
  public void An_empty_mold_never_spills() {
    var world = NewWorld();
    Assert.False(
      MoltenMoldSpill.SpillIfMolten(
        new DummySlot(CastMold(world, null, 0, 3122)),
        world.World,
        null
      )
    );
  }

  [Fact]
  public void A_non_handled_block_never_spills_even_carrying_liquid() {
    var world = NewWorld();
    var plain = TestBlocks.Configure(new Block(), "game:stone", 3123);
    world.Register(plain);
    var stack = new ItemStack(plain);
    MoltenContents.Write(
      stack,
      MoltenContents.MoldUnitsKey,
      MoltenMetal.CreateStack(world.World, Iron, 1550f)!,
      20
    );

    Assert.False(
      MoltenMoldSpill.SpillIfMolten(new DummySlot(stack), world.World, null)
    );
  }

  #endregion
}
