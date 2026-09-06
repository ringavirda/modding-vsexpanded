using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// What a click carries: the held stack and tool, the sneak state, the clicked face and the side. The
/// player is a <see cref="TestPlayer"/>; its shift-key modifier (distinct from the crouch state
/// <see cref="TestPlayer.Sneaking"/> tracks - see <c>ExInteraction.Sneaking</c>) is set directly on
/// <see cref="TestPlayer.Entity"/>'s controls, which is what an interact handler actually reads.
/// </summary>
public class ExInteractionTests {
  private static IPlayer PlayerHolding(ItemStack? held, bool sneaking = false) {
    TestPlayer player = new TestWorld().Player();
    player.Hold(held);
    player.Entity.Controls.ShiftKey = sneaking;
    return player.Player;
  }

  private static BlockSelection Selection(BlockFacing? face = null) =>
    new() { Position = new BlockPos(1, 2, 3, 0), Face = face ?? BlockFacing.NORTH };

  private static Item ToolItem(EnumTool tool, string code = "test:tool") {
    var item = new Item { Code = new AssetLocation(code), Tool = tool };
    return item;
  }

  [Fact]
  public void Held_reads_the_active_hotbar_stack() {
    var stack = new ItemStack(new Item { Code = new AssetLocation("game:pick-iron") });
    IPlayer player = PlayerHolding(stack);

    Interaction interaction = ExInteraction.Of(
      Substitute.For<IWorldAccessor>(),
      player,
      Selection()
    );

    Assert.Same(stack, interaction.Held);
    Assert.Same(stack.Collectible, interaction.HeldCollectible);
    Assert.False(interaction.HandEmpty);
  }

  [Fact]
  public void HeldIs_tool_matches_the_declared_tool_type() {
    var stack = new ItemStack(ToolItem(EnumTool.Wrench));
    IPlayer player = PlayerHolding(stack);
    Interaction interaction = ExInteraction.Of(
      Substitute.For<IWorldAccessor>(),
      player,
      Selection()
    );

    Assert.True(interaction.HeldIs(EnumTool.Wrench));
    Assert.False(interaction.HeldIs(EnumTool.Chisel));
  }

  [Fact]
  public void HeldIs_code_matches_an_exact_code() {
    var stack = new ItemStack(new Item { Code = new AssetLocation("iiex:wrench-basic") });
    IPlayer player = PlayerHolding(stack);
    Interaction interaction = ExInteraction.Of(
      Substitute.For<IWorldAccessor>(),
      player,
      Selection()
    );

    Assert.True(interaction.HeldIs(new AssetLocation("iiex:wrench-basic")));
    Assert.False(interaction.HeldIs(new AssetLocation("iiex:wrench-fancy")));
  }

  [Fact]
  public void HeldIs_code_matches_a_wildcard() {
    var stack = new ItemStack(new Item { Code = new AssetLocation("iiex:wrench-fancy") });
    IPlayer player = PlayerHolding(stack);
    Interaction interaction = ExInteraction.Of(
      Substitute.For<IWorldAccessor>(),
      player,
      Selection()
    );

    Assert.True(interaction.HeldIs(new AssetLocation("iiex:wrench-*")));
    Assert.False(interaction.HeldIs(new AssetLocation("iiex:hammer-*")));
  }

  [Fact]
  public void HandEmpty_is_true_with_nothing_held() {
    IPlayer player = PlayerHolding(null);
    Interaction interaction = ExInteraction.Of(
      Substitute.For<IWorldAccessor>(),
      player,
      Selection()
    );

    Assert.True(interaction.HandEmpty);
    Assert.Null(interaction.Held);
    Assert.Null(interaction.HeldCollectible);
    Assert.False(interaction.HeldIs(EnumTool.Wrench));
  }

  [Fact]
  public void Sneaking_reads_the_shift_control() {
    IPlayer player = PlayerHolding(null, sneaking: true);
    Interaction interaction = ExInteraction.Of(
      Substitute.For<IWorldAccessor>(),
      player,
      Selection()
    );

    Assert.True(interaction.Sneaking);
  }

  [Fact]
  public void Face_reads_the_selections_face() {
    IPlayer player = PlayerHolding(null);
    Interaction interaction = ExInteraction.Of(
      Substitute.For<IWorldAccessor>(),
      player,
      Selection(BlockFacing.UP)
    );

    Assert.Equal(BlockFacing.UP, interaction.Face);
  }

  [Fact]
  public void Side_reads_the_world() {
    var world = Substitute.For<IWorldAccessor>();
    world.Side.Returns(EnumAppSide.Server);
    IPlayer player = PlayerHolding(null);

    Interaction interaction = ExInteraction.Of(world, player, Selection());

    Assert.True(interaction.IsServer);
    Assert.False(interaction.IsClient);
  }

  [Fact]
  public void A_null_player_reads_as_empty_handed_and_not_sneaking_without_throwing() {
    Interaction interaction = ExInteraction.Of(
      Substitute.For<IWorldAccessor>(),
      null!,
      Selection()
    );

    Assert.Null(interaction.Held);
    Assert.True(interaction.HandEmpty);
    Assert.False(interaction.Sneaking);
  }

  [Fact]
  public void A_null_selection_reads_no_face_without_throwing() {
    IPlayer player = PlayerHolding(null);
    Interaction interaction = ExInteraction.Of(
      Substitute.For<IWorldAccessor>(),
      player,
      null!
    );

    Assert.Null(interaction.Face);
  }
}
