using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Two molten cells on one block entity. A cell holds one metal, and
/// <see cref="BEBehaviorMoltenCell.PushMetalRaw"/> refuses a second, so a crucible holding iron under
/// slag in a single hearth block needs two behaviour instances. Both serialise into the same
/// block-entity tree, so each carries a <c>key</c> prefix to keep its tree keys apart. See
/// docs/design/layered-charge.md, "Layered, like the charge pile". exlib is metal-agnostic; the pair
/// below is named for that case but asserts only two instances with independent state.
/// </summary>
public class MoltenCellPairTests {
  private const string Iron = "game:ingot-iron";
  private const string Slag = "game:ingot-slag";

  #region Fixture

  private static TestWorld NewWorld() {
    var world = new TestWorld();
    world.RegisterItem(Iron, 1500f);
    world.RegisterItem(Slag, 1200f);
    world.RegisterItem("game:metalbit-iron");
    return world;
  }

  /// <summary>A placed, API-linked block entity hosting two configured molten cells.</summary>
  private static (
    BlockEntity Be,
    BEBehaviorMoltenCell Lower,
    BEBehaviorMoltenCell Upper
  ) HearthWithTwoCells(TestWorld world, string lowerProps, string upperProps) {
    var block = TestBlocks.Configure(
      new BlockStructureFiller(),
      "exlib:structurefiller",
      70
    );
    var be = new BlockEntityStructureFiller();
    world.Place(new BlockPos(0, 0, 0), block, be);
    world.Attach(be);

    var lower = new BEBehaviorMoltenCell(be);
    be.Behaviors.Add(lower);
    lower.ConfigureFromFiller(
      null,
      null,
      new JsonObject(JToken.Parse(lowerProps))
    );

    var upper = new BEBehaviorMoltenCell(be);
    be.Behaviors.Add(upper);
    upper.ConfigureFromFiller(
      null,
      null,
      new JsonObject(JToken.Parse(upperProps))
    );

    return (be, lower, upper);
  }

  private const string LowerProps =
    "{ \"key\": \"iron_\", \"capacity\": 6400 }";
  private const string UpperProps =
    "{ \"key\": \"slag_\", \"capacity\": 6400 }";

  #endregion

  #region Two cells, one tree

  /// <summary>
  /// Both cells serialise into the block entity's one tree, so with a single fixed key set the second
  /// write would land on top of the first and a reloaded hearth would hold one metal in both cells.
  /// </summary>
  [Fact]
  public void Two_cells_on_one_entity_keep_separate_state_across_a_save() {
    var world = NewWorld();
    var (_, lower, upper) = HearthWithTwoCells(world, LowerProps, UpperProps);
    lower.PushMetalRaw(600, Iron, 1400f, world.World);
    upper.PushMetalRaw(150, Slag, 1300f, world.World);

    // One tree, as the block entity hands the same instance to every behaviour.
    var tree = new TreeAttribute();
    lower.ToTreeAttributes(tree);
    upper.ToTreeAttributes(tree);

    var (_, restoredLower, restoredUpper) = HearthWithTwoCells(
      NewWorld(),
      LowerProps,
      UpperProps
    );
    restoredLower.FromTreeAttributes(tree, world.World);
    restoredUpper.FromTreeAttributes(tree, world.World);

    Assert.Equal(600, restoredLower.CellAmount);
    Assert.Equal(Iron, restoredLower.CellMetalType);
    Assert.Equal(150, restoredUpper.CellAmount);
    Assert.Equal(Slag, restoredUpper.CellMetalType);
  }

  /// <summary>
  /// Temperature and the solidified flag are per-cell as well. The two cells sit at different
  /// temperatures and freeze at different points, so a shared value would be wrong for one of them,
  /// and only after a reload.
  /// </summary>
  [Fact]
  public void Each_cell_keeps_its_own_temperature_across_a_save() {
    var world = NewWorld();
    var (_, lower, upper) = HearthWithTwoCells(world, LowerProps, UpperProps);
    lower.PushMetalRaw(600, Iron, 1400f, world.World);
    upper.PushMetalRaw(150, Slag, 1250f, world.World);

    var tree = new TreeAttribute();
    lower.ToTreeAttributes(tree);
    upper.ToTreeAttributes(tree);

    var (_, restoredLower, restoredUpper) = HearthWithTwoCells(
      NewWorld(),
      LowerProps,
      UpperProps
    );
    restoredLower.FromTreeAttributes(tree, world.World);
    restoredUpper.FromTreeAttributes(tree, world.World);

    Assert.Equal(1400f, restoredLower.CellTemperature, 1);
    Assert.Equal(1250f, restoredUpper.CellTemperature, 1);
  }

  #endregion

  #region Addressing the second instance

  /// <summary>
  /// <c>GetBehavior&lt;T&gt;()</c> returns the first match and has no overload naming which, so it
  /// cannot address the second cell: a caller reaching for the upper cell this way operates on the
  /// lower one. A consumer of a two-cell host selects by key instead.
  /// </summary>
  [Fact]
  public void GetBehavior_returns_the_first_cell_and_cannot_reach_the_second() {
    var world = NewWorld();
    var (be, lower, upper) = HearthWithTwoCells(world, LowerProps, UpperProps);

    Assert.Same(lower, be.GetBehavior<BEBehaviorMoltenCell>());
    Assert.NotSame(upper, be.GetBehavior<BEBehaviorMoltenCell>());
  }

  /// <summary>The supported way to reach a named cell on a multi-cell host.</summary>
  [Fact]
  public void A_cell_can_be_selected_by_its_key() {
    var world = NewWorld();
    var (be, lower, upper) = HearthWithTwoCells(world, LowerProps, UpperProps);

    Assert.Same(lower, be.MoltenCell("iron_"));
    Assert.Same(upper, be.MoltenCell("slag_"));
    Assert.Null(be.MoltenCell("nosuchcell_"));
  }

  #endregion

  #region The default is load-bearing

  /// <summary>
  /// The default prefix is <c>mc_</c> and must stay so. Cells that declare no <c>key</c> - the sand
  /// casting bed's runner, mold and basin cells, and the standalone casting cell - have their saved
  /// trees written with the bare <c>mc_</c> names, so changing the default empties them in existing
  /// worlds: <c>FromTreeAttributes</c> would look for keys that are not there and read zeros.
  /// </summary>
  [Fact]
  public void A_cell_with_no_key_uses_the_original_unprefixed_names() {
    var world = NewWorld();
    var (_, plain, _) = HearthWithTwoCells(
      world,
      "{}",
      "{ \"key\": \"other_\" }"
    );
    plain.PushMetalRaw(40, Iron, 1300f, world.World);

    var tree = new TreeAttribute();
    plain.ToTreeAttributes(tree);

    // Key names spelled out rather than derived: asking the behaviour for its own prefix would agree
    // with whatever value it carried.
    Assert.Equal(40, tree.GetInt("mc_amount"));
    Assert.Equal(Iron, tree.GetString("mc_type"));
    Assert.True(tree.HasAttribute("mc_temp"));
    Assert.True(tree.HasAttribute("mc_solid"));
  }

  /// <summary>
  /// A keyed cell must not write the bare names, or it would still collide with an unkeyed cell on
  /// the same entity.
  /// </summary>
  [Fact]
  public void A_keyed_cell_writes_none_of_the_unprefixed_names() {
    var world = NewWorld();
    var (_, keyed, _) = HearthWithTwoCells(world, LowerProps, UpperProps);
    keyed.PushMetalRaw(40, Iron, 1300f, world.World);

    var tree = new TreeAttribute();
    keyed.ToTreeAttributes(tree);

    Assert.False(tree.HasAttribute("mc_amount"));
    Assert.False(tree.HasAttribute("mc_type"));
    Assert.True(tree.HasAttribute("iron_amount"));
    Assert.Equal(Iron, tree.GetString("iron_type"));
  }

  #endregion
}
