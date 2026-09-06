using System;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace ExpandedLib.Testing;

/// <summary>
/// A player with a real hotbar: the active slot holds whatever the test puts there via
/// <see cref="Hold"/>. Built the way <c>ExOrientableRig</c> and <c>ExInteractionTests</c> already
/// stood one up by hand - a substituted <see cref="IPlayer"/>/<see cref="EntityPlayer"/> with a real
/// <see cref="DummySlot"/> behind <c>InventoryManager.ActiveHotbarSlot</c> - so every interaction
/// handler that reads "what is the player holding" or "is the player sneaking" sees the same shape
/// it does in game.
/// </summary>
public sealed class TestPlayer {
  private readonly DummySlot _activeSlot;

  private TestPlayer(
    IPlayer player,
    IServerPlayer? serverPlayer,
    EntityPlayer entity,
    DummySlot activeSlot
  ) {
    Player = player;
    ServerPlayer = serverPlayer;
    Entity = entity;
    _activeSlot = activeSlot;
  }

  /// <summary>The substituted player, valid on every lane.</summary>
  public IPlayer Player { get; }

  /// <summary>The same object as <see cref="Player"/>, viewed as <see cref="IServerPlayer"/>, or
  /// <c>null</c> when this lane's game assembly cannot proxy it (see the constructor's catch).</summary>
  public IServerPlayer? ServerPlayer { get; }

  /// <summary>The player's active hotbar slot - a real <see cref="ItemSlot"/>, not a fake.</summary>
  public ItemSlot ActiveSlot => _activeSlot;

  /// <summary>The substituted entity behind <see cref="Player"/>; its <c>Controls.Sneak</c> is real
  /// (Castle proxies a class by running its base constructor, so non-overridden members like
  /// <c>Controls</c> come from the genuine <see cref="EntityPlayer"/> field initialisers).</summary>
  public EntityPlayer Entity { get; }

  /// <summary>Whether the player is sneaking; backed by <see cref="Entity"/>'s own controls.</summary>
  public bool Sneaking {
    get => Entity.Controls.Sneak;
    set => Entity.Controls.Sneak = value;
  }

  /// <summary>Puts <paramref name="stack"/> in the active hotbar slot, or empties it for <c>null</c>.</summary>
  public void Hold(ItemStack? stack) => _activeSlot.Itemstack = stack;

  /// <summary>
  /// Builds a player standing in <paramref name="world"/>: a substituted <see cref="IServerPlayer"/>
  /// where the game assembly allows it (every lane today, once <c>provision game</c> has run - see
  /// <c>Publicize-GameApi</c> in <c>scripts/exmod.ps1</c>), falling back to a plain
  /// <see cref="IPlayer"/> substitute otherwise, since every lane supports that one.
  /// </summary>
  public static TestPlayer Create(
    TestWorld world,
    string uid = "test",
    string name = "Tester"
  ) {
    EntityPlayer entity = Substitute.For<EntityPlayer>();
    entity.World = world.World;

    IPlayer player;
    IServerPlayer? serverPlayer;
    try {
      serverPlayer = Substitute.For<IServerPlayer>();
      player = serverPlayer;
    } catch (TypeLoadException) {
      serverPlayer = null;
      player = Substitute.For<IPlayer>();
    }

    player.Entity.Returns(entity);
    player.PlayerUID.Returns(uid);
    player.PlayerName.Returns(name);

    var activeSlot = new DummySlot();
    var inventoryManager = Substitute.For<IPlayerInventoryManager>();
    inventoryManager.ActiveHotbarSlot.Returns(activeSlot);
    player.InventoryManager.Returns(inventoryManager);

    return new TestPlayer(player, serverPlayer, entity, activeSlot);
  }
}
