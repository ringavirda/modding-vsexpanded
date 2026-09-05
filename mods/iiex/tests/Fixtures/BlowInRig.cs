using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The blow-in, for scenes that want a furnace burning rather than a furnace being lit. A shaft furnace
/// does not catch by itself: since U4.9 its state derives to Idle until a player has reached a flame
/// through an open tap-hole, so every scene that runs a campaign has to perform the ritual first.
/// <para>
/// Driven through the tap block's own interaction, not through <c>TryLightFromTap</c>: a scene that lit
/// its furnace by reaching past the gesture would go on passing after the gesture broke. Only the
/// re-plugging shortcuts, because paying for clay is <c>FurnaceTapPlugTests</c>'s subject and not every
/// scene's.
/// </para>
/// </summary>
public static class BlowInRig {
  private const string LitTorchCode = "game:torch-basic-lit-up";

  /// <summary>
  /// A lit torch: a block wearing vanilla's <c>CanIgnite</c> behaviour, which is what the tap tests for
  /// rather than a code. Registered in <paramref name="world"/> so the stack resolves.
  /// </summary>
  public static ItemStack LitTorch(TestWorld world) {
    Block torch = TestBlocks.Configure(new Block(), LitTorchCode, 940);
    torch.BlockBehaviors = [new BlockBehaviorCanIgnite(torch)];
    world.Register(torch);
    return new ItemStack(torch);
  }

  /// <summary>
  /// Lights <paramref name="furnace"/>. With a real tap in <paramref name="tap"/> this is the whole ritual
  /// - break the plug out, reach a lit torch in, stop it again - and the tap is left as it was found, so a
  /// scene that opens it afterwards to drain reads exactly as it did.
  /// <para>
  /// A scene that stands up no tap block gets the flame handed straight to the furnace instead. Its own
  /// layout still demands both notches, so this is a gap in the fixture rather than in the furnace: the
  /// gesture is covered by the cold-furnace scenes and by <c>FurnaceBlowInTests</c>, which is where it
  /// belongs.
  /// </para>
  /// </summary>
  public static void BlowIn(
    TestWorld world,
    BlockEntityFurnaceCore furnace,
    BlockEntityFurnaceTap? tap = null
  ) {
    if (tap?.Block is not BlockFurnaceTap block) {
      furnace.TryLightFromTap(furnace.Pos);
      return;
    }

    bool wasPlugged = tap.IsPlugged;
    if (wasPlugged)
      block.OnBlockInteractStart(
        world.World,
        Holding(null),
        new BlockSelection { Position = tap.Pos.Copy() }
      );

    Torch(world, tap);

    if (wasPlugged)
      tap.SetPlugged(true);
  }

  /// <summary>The flame alone: one right-click with a lit torch, whatever state the tap is in.</summary>
  public static void Torch(TestWorld world, BlockEntityFurnaceTap tap) =>
    ((BlockFurnaceTap)tap.Block).OnBlockInteractStart(
      world.World,
      Holding(LitTorch(world)),
      new BlockSelection { Position = tap.Pos.Copy() }
    );

  /// <summary>A player holding <paramref name="held"/>, or nothing. Both slots the tap reads are stood
  /// up: a substitute answers null for whichever is left out, which reads as an empty hand.</summary>
  private static IPlayer Holding(ItemStack? held) {
    var slot = new DummySlot(held);
    var player = Substitute.For<IPlayer>();
    var entity = Substitute.For<EntityPlayer>();
    entity.RightHandItemSlot.Returns(slot);
    player.Entity.Returns(entity);
    player.InventoryManager.ActiveHotbarSlot.Returns(slot);
    player.WorldData.CurrentGameMode.Returns(EnumGameMode.Survival);
    return player;
  }
}
