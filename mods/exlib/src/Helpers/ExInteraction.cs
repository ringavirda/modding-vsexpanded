using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace ExpandedLib.Helpers;

/// <summary>
/// What a click carried - the held stack, the tool, the sneak state, the clicked face and the side.
/// Answers questions about the click; it never decides which side runs a mutation, so a caller still
/// writes its own <c>IsServer</c> guard around whatever the click triggers.
/// </summary>
public readonly struct Interaction {
  private readonly IPlayer? _player;
  private readonly BlockSelection? _selection;
  private readonly IWorldAccessor _world;

  /// <summary>Wraps an interact handler's own arguments; none is validated here, so a null
  /// <paramref name="player"/> or <paramref name="selection"/> is read back as empty-handed,
  /// not-sneaking and faceless rather than throwing.</summary>
  public Interaction(
    IWorldAccessor world,
    IPlayer player,
    BlockSelection selection
  ) {
    _world = world;
    _player = player;
    _selection = selection;
  }

  /// <summary>The active hotbar stack, or <c>null</c> for an empty hand or no player.</summary>
  public ItemStack? Held => _player?.InventoryManager?.ActiveHotbarSlot?.Itemstack;

  /// <summary>The held stack's collectible, or <c>null</c> for an empty hand.</summary>
  public CollectibleObject? HeldCollectible => Held?.Collectible;

  /// <summary>Whether the held collectible declares <paramref name="tool"/> as its tool type.</summary>
  public bool HeldIs(EnumTool tool) => HeldCollectible?.Tool == tool;

  /// <summary>Whether the held collectible's code matches <paramref name="code"/> - an exact code or
  /// a wildcard with <c>*</c>.</summary>
  public bool HeldIs(AssetLocation code) {
    AssetLocation? held = HeldCollectible?.Code;
    return held != null && WildcardUtil.Match(code, held);
  }

  /// <summary>True when nothing is held.</summary>
  public bool HandEmpty => Held == null;

  /// <summary>The player's sneak control; <c>false</c> for no player.</summary>
  public bool Sneaking => _player?.Entity?.Controls?.ShiftKey ?? false;

  /// <summary>The clicked face, or <c>null</c> for no selection.</summary>
  public BlockFacing? Face => _selection?.Face;

  /// <summary>True on the server.</summary>
  public bool IsServer => _world.IsServer();

  /// <summary>True on the client.</summary>
  public bool IsClient => _world.IsClient();
}

/// <summary>Builds an <see cref="Interaction"/> from an interact handler's own arguments.</summary>
public static class ExInteraction {
  /// <summary>Wraps <paramref name="world"/>, <paramref name="player"/> and
  /// <paramref name="selection"/> as they arrived in the handler.</summary>
  public static Interaction Of(
    IWorldAccessor world,
    IPlayer player,
    BlockSelection selection
  ) => new(world, player, selection);
}
