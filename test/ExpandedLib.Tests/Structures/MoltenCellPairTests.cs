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
/// <b>Two molten cells on one block entity.</b> The blast furnace's crucible has to hold iron <em>under</em>
/// slag in a single hearth block (<c>docs/design/layered-charge.md</c> § <i>Layered, like the charge pile</i>),
/// and <see cref="BEBehaviorMoltenCell.PushMetalRaw"/> refuses a second metal outright - one cell is one
/// metal, deliberately. So the layered crucible needs two behaviour instances sharing a block entity.
/// <para>
/// <b>That did not work, and the failure was silent.</b> Both instances wrote the same fixed tree keys
/// (<c>mc_amount</c>, <c>mc_type</c>, …) onto the shared block-entity tree, so the second simply overwrote
/// the first: a hearth would reload having lost one of its two metals, with no error. Each instance now
/// carries a <c>key</c> prefix.
/// </para>
/// <para>
/// exlib knows nothing about iron or slag - it is metal-agnostic. The pair below is named for the case
/// that motivated it; what is actually being asserted is "two instances, two independent states".
/// </para>
/// </summary>
public class MoltenCellPairTests
{
  private const string Iron = "game:ingot-iron";
  private const string Slag = "game:ingot-slag";

  #region Fixture

  private static TestWorld NewWorld()
  {
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
  ) HearthWithTwoCells(TestWorld world, string lowerProps, string upperProps)
  {
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
    lower.ConfigureFromFiller(null, null, new JsonObject(JToken.Parse(lowerProps)));

    var upper = new BEBehaviorMoltenCell(be);
    be.Behaviors.Add(upper);
    upper.ConfigureFromFiller(null, null, new JsonObject(JToken.Parse(upperProps)));

    return (be, lower, upper);
  }

  private const string LowerProps = "{ \"key\": \"iron_\", \"capacity\": 6400 }";
  private const string UpperProps = "{ \"key\": \"slag_\", \"capacity\": 6400 }";

  #endregion

  #region Two cells, one tree

  /// <summary>
  /// <b>The blocker this task exists to remove.</b> Both cells serialise into the block entity's one
  /// tree; with a single fixed key set the second write lands on top of the first, and a reloaded hearth
  /// comes back holding <em>one</em> metal in both cells. Nothing throws - the data is simply gone.
  /// </summary>
  [Fact]
  public void Two_cells_on_one_entity_keep_separate_state_across_a_save()
  {
    var world = NewWorld();
    var (_, lower, upper) = HearthWithTwoCells(world, LowerProps, UpperProps);
    lower.PushMetalRaw(600, Iron, 1400f, world.World);
    upper.PushMetalRaw(150, Slag, 1300f, world.World);

    // One tree, exactly as the block entity hands the same instance to every behaviour.
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
  /// Temperature and the solidified flag are per-cell too. Slag is tapped from a hotter part of the
  /// same hearth and freezes at a different point, so a shared temperature would make one of the two
  /// wrong on every read - and would do it after a reload only, which is the worst place to find it.
  /// </summary>
  [Fact]
  public void Each_cell_keeps_its_own_temperature_across_a_save()
  {
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
  /// <b><c>GetBehavior&lt;T&gt;()</c> cannot address the second cell - it returns the first match and
  /// there is no overload that says which.</b> So every consumer of a two-cell host must select by key,
  /// and this pins that <c>GetBehavior</c> is not that selector: a caller reaching for the slag cell the
  /// obvious way silently operates on the iron one, which is a data-loss bug wearing a correct-looking
  /// call site.
  /// </summary>
  [Fact]
  public void GetBehavior_returns_the_first_cell_and_cannot_reach_the_second()
  {
    var world = NewWorld();
    var (be, lower, upper) = HearthWithTwoCells(world, LowerProps, UpperProps);

    Assert.Same(lower, be.GetBehavior<BEBehaviorMoltenCell>());
    Assert.NotSame(upper, be.GetBehavior<BEBehaviorMoltenCell>());
  }

  /// <summary>The supported way to reach a named cell on a multi-cell host.</summary>
  [Fact]
  public void A_cell_can_be_selected_by_its_key()
  {
    var world = NewWorld();
    var (be, lower, upper) = HearthWithTwoCells(world, LowerProps, UpperProps);

    Assert.Same(lower, be.MoltenCell("iron_"));
    Assert.Same(upper, be.MoltenCell("slag_"));
    Assert.Null(be.MoltenCell("nosuchcell_"));
  }

  #endregion

  #region The default is load-bearing

  /// <summary>
  /// <b>The default prefix must stay <c>mc_</c> forever.</b> Every cell shipped before this change -
  /// the sand casting bed's runner, mold and basin cells, and the standalone casting cell - declares no
  /// <c>key</c> at all, so their saved trees are written with the bare <c>mc_</c> names. Changing the
  /// default (to <c>""</c>, or to the behaviour's name, or to anything "tidier") silently empties every
  /// casting bed in every existing world: <c>FromTreeAttributes</c> would look for keys that are not
  /// there and read zeros.
  /// </summary>
  [Fact]
  public void A_cell_with_no_key_uses_the_original_unprefixed_names()
  {
    var world = NewWorld();
    var (_, plain, _) = HearthWithTwoCells(world, "{}", "{ \"key\": \"other_\" }");
    plain.PushMetalRaw(40, Iron, 1300f, world.World);

    var tree = new TreeAttribute();
    plain.ToTreeAttributes(tree);

    // The literal pre-change key set, spelled out rather than derived - a test that asked the behaviour
    // for its own prefix would agree with any value it happened to have.
    Assert.Equal(40, tree.GetInt("mc_amount"));
    Assert.Equal(Iron, tree.GetString("mc_type"));
    Assert.True(tree.HasAttribute("mc_temp"));
    Assert.True(tree.HasAttribute("mc_solid"));
  }

  /// <summary>
  /// The other half: a keyed cell must not write the bare names, or it would still collide with an
  /// unkeyed one on the same entity.
  /// </summary>
  [Fact]
  public void A_keyed_cell_writes_none_of_the_unprefixed_names()
  {
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
