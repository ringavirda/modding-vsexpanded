using System;
using System.Linq;
using ExpandedLib.Helpers;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ExpandedLib.Metals;

/// <summary>What a chisel and hammer click resolved to on an <see cref="IChiselableMolten"/> holder.</summary>
public enum ChiselOutcome {
  /// <summary>Not a chisel-out here: no chisel and hammer, or nothing solidified. The click falls
  /// through.</summary>
  NotChiseling,

  /// <summary>The solidified content is not ready (too hot or too full). The click is claimed, nothing
  /// is recovered.</summary>
  Blocked,

  /// <summary>The hardened content was chipped out and recovered.</summary>
  Chiseled,
}

/// <summary>
/// The chip-solidified-metal-out interaction shared by every <see cref="IChiselableMolten"/> holder
/// (canal cells, the molten barrel, the bessemer vessel): tool gating, not-ready feedback, the
/// recovered drop, tool wear and sound. A holder supplies its content-specific state through the
/// interface and the clear-and-recover step in <see cref="IChiselableMolten.ChiselOut"/>; the drop
/// itself is built by <see cref="BuildRecovery"/>.
/// </summary>
public static class MoltenChisel {
  /// <summary>True when <paramref name="stack"/> is a tool of kind <paramref name="tool"/>.</summary>
  public static bool IsTool(ItemStack? stack, EnumTool tool) =>
    stack?.Collectible?.Tool == tool;

  /// <summary>True when the player holds a chisel in the active hand and a hammer in the off-hand.</summary>
  public static bool HasChiselAndHammer(IPlayer byPlayer) =>
    IsTool(
      byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack,
      EnumTool.Chisel
    ) && IsTool(byPlayer.Entity?.LeftHandItemSlot?.Itemstack, EnumTool.Hammer);

  /// <summary>
  /// The metal-bit recovery stack for <paramref name="units"/> of <paramref name="metalCode"/> at
  /// <paramref name="temperature"/> °C, at <paramref name="unitsPerBit"/> units per bit and mapped to
  /// the solid drop code by <see cref="MetalRegistry.SolidDropOf"/>. When the solid item does not
  /// resolve, falls back to the metal's recovery item through
  /// <see cref="MetalRegistry.FallbackOf"/> if <paramref name="slagFallback"/> is set, else returns
  /// <c>null</c>. Used by every chisel and break drop path.
  /// </summary>
  public static ItemStack? BuildRecovery(
    IWorldAccessor world,
    AssetLocation metalCode,
    float temperature,
    int units,
    int unitsPerBit = 5,
    bool slagFallback = false
  ) {
    int count = Math.Max(1, units / unitsPerBit);
    AssetLocation loc = MetalRegistry.SolidDropOf(metalCode);
    Item? item = world.GetItem(loc);
    if (item == null) {
      if (!slagFallback)
        return null;
      Item? slag = world.GetItem(MetalRegistry.FallbackOf(metalCode));
      return slag != null ? new ItemStack(slag, count) : null;
    }
    var drop = new ItemStack(item, count);
    MoltenMetal.SetTemperature(world, drop, temperature);
    return drop;
  }

  /// <summary>
  /// Runs the chisel-out interaction against <paramref name="target"/>. On
  /// <see cref="ChiselOutcome.Chiseled"/> the hardened content has been chipped out and given to the
  /// player, or spawned at <paramref name="yOffset"/> when the inventory is full, the chisel damaged
  /// (unless <paramref name="damageChisel"/> is false or the player is in creative) and
  /// <paramref name="sound"/> played. On <see cref="ChiselOutcome.Blocked"/> the block error, if any,
  /// is sent. All world mutation is server-side; the outcome is still returned on the client so the
  /// caller can claim the click for prediction.
  /// </summary>
  public static ChiselOutcome TryChisel(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos pos,
    IChiselableMolten target,
    AssetLocation sound,
    bool damageChisel = true,
    double yOffset = 0.6
  ) {
    if (!HasChiselAndHammer(byPlayer) || !target.HasChiselableContent)
      return ChiselOutcome.NotChiseling;

    if (!target.CanChiselOut) {
      if (world.Side == EnumAppSide.Server && target.ChiselBlockedError != null)
        (byPlayer as IServerPlayer)?.SendIngameError(target.ChiselBlockedError);
      return ChiselOutcome.Blocked;
    }

    if (world.Side == EnumAppSide.Server) {
      ItemStack? recovered = target.ChiselOut();
      if (
        recovered != null
        && !byPlayer.InventoryManager.TryGiveItemstack(recovered)
      )
        world.SpawnItemEntity(recovered, pos.ToVec3d().Add(0.5, yOffset, 0.5));

      ItemSlot? slot = byPlayer.InventoryManager.ActiveHotbarSlot;
      if (
        damageChisel
        && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative
      )
        slot?.Itemstack?.Collectible.DamageItem(
          world,
          byPlayer.Entity,
          slot,
          2
        );

      ExSounds.Play(world.Api, pos, sound, 0.8f);
    }
    return ChiselOutcome.Chiseled;
  }

  // The chisel items advertised in the interaction help, resolved once per process.
  private static ItemStack[]? _chiselStacks;

  /// <summary>The chisel-out interaction hint, advertising every chisel item.</summary>
  public static WorldInteraction ChiselHelp(
    IWorldAccessor world,
    string langCode
  ) =>
    new() {
      ActionLangCode = langCode,
      MouseButton = EnumMouseButton.Right,
      Itemstacks = _chiselStacks ??=
        [
          .. world
            .SearchItems(new AssetLocation("chisel-*"))
            .Select(i => new ItemStack(i)),
        ],
    };
}
