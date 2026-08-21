using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The clay plug: a tap's closed state, and the one thing the blow-in ritual costs. A newly built tap is
/// plugged; an empty hand breaks the plug out and destroys it, fire clay stops the hole again, and an
/// opened tap never closes itself. There is no animator any more - the plug is an element of the shape,
/// drawn or pruned.
/// <para>
/// The interaction is driven through <c>BlockFurnaceTap.OnBlockInteractStart</c> rather than through
/// <c>SetPlugged</c>, because the gesture and its cost are what these cases are about; the flag on its own
/// would agree with any of them.
/// </para>
/// </summary>
[Collection(FurnaceConfigCollection.Name)]
public class FurnaceTapPlugTests {
  #region Harness

  private const string FireClay = "game:clay-fire";

  private static TestWorld NewWorld() {
    var world = new TestWorld();
    // The plug is save data, so both verbs are server work; on a client-side world every click below is a
    // no-op and every case here would read as "nothing happened".
    world.World.Side.Returns(EnumAppSide.Server);
    world.RegisterItem(FireClay);
    return world;
  }

  private static BlockEntityFurnaceTap Tap(
    TestWorld world,
    string side = "north"
  ) {
    var be = new BlockEntityFurnaceTap {
      Pos = new BlockPos(0, 12, 0),
      Block = TestBlocks.Configure(
        new BlockFurnaceTap(),
        $"iiex:furnace-{BlockFurnaceTap.IronType}-{side}",
        1,
        ("type", BlockFurnaceTap.IronType),
        ("side", side)
      ),
    };
    world.Place(be.Pos, be.Block, be);
    world.Attach(be);
    return be;
  }

  /// <summary>
  /// A player holding <paramref name="held"/>, or nothing. Both the hotbar slot the interaction takes clay
  /// out of and the right-hand slot the interaction help reads are stood up, since the two are read by
  /// different code paths and a substitute returns null for whichever is left out.
  /// </summary>
  private static IPlayer Player(ItemStack? held = null) {
    var slot = new DummySlot(held);
    var player = Substitute.For<IPlayer>();
    var entity = Substitute.For<EntityPlayer>();
    entity.RightHandItemSlot.Returns(slot);
    player.Entity.Returns(entity);
    player.InventoryManager.ActiveHotbarSlot.Returns(slot);
    player.WorldData.CurrentGameMode.Returns(EnumGameMode.Survival);
    return player;
  }

  private static ItemStack Clay(TestWorld world, int count) =>
    new(world.World.GetItem(new AssetLocation(FireClay))!, count);

  /// <summary>The production gesture: right-click the tap with whatever the player is holding.</summary>
  private static bool Click(
    TestWorld world,
    BlockEntityFurnaceTap tap,
    IPlayer player
  ) =>
    ((BlockFurnaceTap)tap.Block).OnBlockInteractStart(
      world.World,
      player,
      new BlockSelection { Position = tap.Pos.Copy() }
    );

  private static ItemStack IronStack(TestWorld world) =>
    new(world.World.GetItem(new AssetLocation("game:ingot-iron"))!, 20);

  #endregion

  #region The plug is the closed state

  [Fact]
  public void A_newly_built_tap_is_plugged_and_pours_nothing() {
    // The default is what makes blowing in cost a plug: a tap that arrived open would let the player skip
    // the whole ritual by never touching it.
    var world = NewWorld();
    world.RegisterItem("game:ingot-iron", 1500f);
    var tap = Tap(world);

    Assert.True(tap.IsPlugged);
    Assert.False(tap.IsPouring);
    Assert.Equal(0, tap.TryPourMetal(IronStack(world), 1400f));
  }

  [Fact]
  public void An_empty_hand_breaks_the_plug_out_and_destroys_it() {
    // Ruled: breaking gives nothing back. ironmaking.md states the cost of a blow-in as one whole clay
    // plug, which a refund would halve - and a tap plug is knocked through, not chiselled out whole.
    var world = NewWorld();
    var tap = Tap(world);
    IPlayer player = Player();

    Assert.True(Click(world, tap, player));

    Assert.False(tap.IsPlugged);
    Assert.Equal(0, IiexValues.TapUnplugClayRefund);
    Assert.True(player.InventoryManager.ActiveHotbarSlot.Empty);
  }

  [Fact]
  public void Fire_clay_stops_an_open_tap_again_and_is_consumed() {
    var world = NewWorld();
    var tap = Tap(world);
    tap.SetPlugged(false);

    ItemStack clay = Clay(world, IiexValues.TapPlugClayCost + 3);
    IPlayer player = Player(clay);

    Assert.True(Click(world, tap, player));

    Assert.True(tap.IsPlugged);
    Assert.Equal(3, player.InventoryManager.ActiveHotbarSlot.StackSize);
  }

  [Fact]
  public void Too_little_clay_leaves_the_tap_open_and_takes_nothing() {
    // The half-applied failure this guards against is a tap that closes on a payment it could not take.
    var world = NewWorld();
    var tap = Tap(world);
    tap.SetPlugged(false);

    int short_ = IiexValues.TapPlugClayCost - 1;
    Assert.True(
      short_ > 0,
      "the cost must exceed one for this case to mean anything"
    );
    IPlayer player = Player(Clay(world, short_));

    Assert.False(Click(world, tap, player));

    Assert.False(tap.IsPlugged);
    Assert.Equal(short_, player.InventoryManager.ActiveHotbarSlot.StackSize);
  }

  [Fact]
  public void A_creative_player_plugs_without_paying() {
    var world = NewWorld();
    var tap = Tap(world);
    tap.SetPlugged(false);

    IPlayer player = Player(Clay(world, IiexValues.TapPlugClayCost));
    player.WorldData.CurrentGameMode.Returns(EnumGameMode.Creative);

    Assert.True(Click(world, tap, player));

    Assert.True(tap.IsPlugged);
    Assert.Equal(
      IiexValues.TapPlugClayCost,
      player.InventoryManager.ActiveHotbarSlot.StackSize
    );
  }

  [Fact]
  public void A_held_item_that_is_not_fire_clay_does_not_work_the_plug() {
    // Both directions: a held stack cannot break a plug (that gesture is the empty hand's) and a held
    // stack that is not clay cannot make one. Otherwise the tap swallows every click at its cell.
    var world = NewWorld();
    world.RegisterItem("game:ingot-iron", 1500f);
    var tap = Tap(world);
    IPlayer holding = Player(IronStack(world));

    Assert.False(Click(world, tap, holding));
    Assert.True(tap.IsPlugged);

    tap.SetPlugged(false);
    Assert.False(Click(world, tap, holding));
    Assert.False(tap.IsPlugged);
  }

  #endregion

  #region Persistence

  [Fact]
  public void The_plug_state_round_trips_through_the_tree() {
    var world = NewWorld();
    var src = Tap(world);
    src.SetPlugged(false);

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);

    var dst = Tap(world);
    dst.FromTreeAttributes(tree, world.World);

    Assert.False(dst.IsPlugged);
  }

  [Fact]
  public void A_tap_saved_before_the_plug_existed_keeps_the_state_it_had() {
    // The old key was `isPouring`, and reading a missing `plugged` as "plugged" would stop every open tap
    // in an existing world the moment it loaded - a furnace mid-campaign would stop draining with no
    // event to explain it.
    var world = NewWorld();

    var open = new TreeAttribute();
    open.SetBool("isPouring", true);
    var restored = Tap(world);
    restored.FromTreeAttributes(open, world.World);
    Assert.False(restored.IsPlugged);

    var closed = new TreeAttribute();
    closed.SetBool("isPouring", false);
    var stopped = Tap(world);
    stopped.FromTreeAttributes(closed, world.World);
    Assert.True(stopped.IsPlugged);
  }

  #endregion

  #region An open tap stays open

  [Fact]
  public void An_open_tap_with_no_canal_below_still_opens_and_pours_nothing() {
    // This is what the blow-in ritual needs and what the old block refused: opening used to require a
    // canal start under the spout, so a tap could not be opened to be torched. It opens now; what it
    // cannot do is deliver.
    var world = NewWorld();
    world.RegisterItem("game:ingot-iron", 1500f);
    var tap = Tap(world);

    Assert.True(Click(world, tap, Player()));

    Assert.False(tap.IsPlugged);
    Assert.Equal(0, tap.TryPourMetal(IronStack(world), 1400f));
  }

  [Fact]
  public void An_open_tap_that_has_drained_everything_stays_open() {
    // The tap has no self-closing path at all - nothing in the block entity ever sets the plug back - and
    // that is the design ("an opened tap runs until the crucible empties and the player re-plugs it").
    // Stated as a case because a future auto-close would be silent: the furnace would simply stop
    // draining one day.
    var world = NewWorld();
    world.RegisterItem("game:ingot-iron", 1500f);
    var tap = Tap(world);
    tap.SetPlugged(false);

    for (int i = 0; i < 5; i++)
      Assert.Equal(0, tap.TryPourMetal(IronStack(world), 1400f));

    Assert.False(tap.IsPlugged);
  }

  #endregion

  #region The plug is drawn, not posed

  /// <summary>
  /// <c>OnTesselation</c> prunes the shape to <c>Base</c>/<c>TapCanal</c>(<c>/ClayPlug</c>), which is only
  /// correct while both shapes carry exactly those three top-level elements. Neither may grow an
  /// <c>animations</c> array either: the block no longer runs an animator, so a clip added to the art
  /// would never play and would look like a broken tap rather than a missing behaviour.
  /// </summary>
  [Theory]
  [InlineData(BlockFurnaceTap.IronType)]
  [InlineData(BlockFurnaceTap.SlagType)]
  public void Both_tap_shapes_carry_the_three_elements_the_prune_expects(
    string type
  ) {
    string path = DefinitionGoldens.SolutionRelative(
      $"assets/iiex/shapes/furnace/{type}.json"
    );
    string json = File.ReadAllText(path);

    Assert.Equal(
      new[] { "Base", "ClayPlug", "TapCanal" },
      JsonConvert
        .DeserializeObject<Shape>(json)!
        .Elements.Select(e => e.Name)
        .OrderBy(n => n, System.StringComparer.Ordinal)
        .ToArray()
    );
    Assert.DoesNotContain("\"animations\"", json);
  }

  /// <summary>
  /// The two types really do draw different shapes. They shared one until the drawn art landed, and a
  /// definition that kept the shared path would render an iron notch where a cinder notch belongs with
  /// nothing else disagreeing.
  /// </summary>
  [Fact]
  public void Each_tap_type_names_its_own_shape_at_every_facing() {
    var byType = new Dictionary<string, HashSet<string>>();
    foreach (var def in BlockFurnaceTap.Definitions("iiex")) {
      var paths = new HashSet<string>();
      foreach (var entry in def.ToJson()["shapebytype"]!.Children<JProperty>())
        paths.Add((string)entry.Value["base"]!);
      byType[def.ToJson()["variantgroups"]![0]["states"]![0]!.ToString()] =
        paths;
    }

    Assert.Equal(["iiex:furnace/irontap"], byType[BlockFurnaceTap.IronType]);
    Assert.Equal(["iiex:furnace/slagtap"], byType[BlockFurnaceTap.SlagType]);
  }

  #endregion
}
