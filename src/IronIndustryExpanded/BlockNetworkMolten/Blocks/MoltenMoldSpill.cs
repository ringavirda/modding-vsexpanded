using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using IronIndustryExpanded.BlockStructures.Casting.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockNetworkMolten.Blocks;

/// <summary>
/// A tool mold holding liquid metal may only be carried in the active hand. Anywhere else - another
/// hotbar slot, a backpack, a chest, a mold rack - the metal spills out and empties the mold. Hardened
/// and cooling castings are unaffected.
/// </summary>
public static class MoltenMoldSpill {
  /// <summary>Spill notification code, resolved client-side from "game:ingameerror-{code}".</summary>
  public const string ErrorCode = "iiex-moltenspill";

  /// <summary>
  /// Whether a block gets the enhanced mold handling (spill, burn, carry). <see cref="BlockCastMold"/>
  /// always does; vanilla clay <c>BlockToolMold</c> only when the server has opted in via
  /// <c>EnhanceVanillaMolds</c> (<c>/exmod molds vanilla on</c>).
  /// </summary>
  public static bool IsHandledMold(Block? block) =>
    block is BlockCastMold
    || (IiexValues.EnhanceVanillaMolds && block is BlockToolMold);

  /// <summary>
  /// If <paramref name="slot"/> holds a tool mold with molten metal, strips its
  /// contents and (optionally) notifies the player. Returns true if it spilled.
  /// </summary>
  public static bool SpillIfMolten(
    ItemSlot? slot,
    IWorldAccessor world,
    IServerPlayer? notify
  ) {
    if (slot?.Itemstack is not { } stack || !IsHandledMold(stack.Block))
      return false;

    var (contents, fill) = MoltenContents.Read(
      stack,
      MoltenContents.MoldUnitsKey,
      world
    );
    if (contents?.Collectible == null || fill <= 0)
      return false;

    if (!MoltenMetal.IsLiquid(world, contents))
      return false;

    stack.Attributes?.RemoveAttribute("blockEntityAttributes");
    slot.MarkDirty();
    notify?.SendIngameError(ErrorCode);
    return true;
  }

  /// <summary>Error code for refusing to hand over a liquid mold into occupied hands.</summary>
  public const string NeedEmptyHandsCode = "iiex-needemptyhands";

  /// <summary>
  /// True when <paramref name="contents"/>/<paramref name="units"/> describe metal
  /// that is still liquid - a mold holding it may only travel in an empty hand.
  /// </summary>
  public static bool IsLiquidContent(
    IWorldAccessor world,
    ItemStack? contents,
    int units
  ) =>
    contents?.Collectible != null
    && units > 0
    && MoltenMetal.IsLiquid(world, contents);

  /// <summary>
  /// Enforces the empty-hand pickup rule for a mold holding liquid metal: returns
  /// true (and notifies the player) when the pickup must be refused because the
  /// active hand is occupied. Call before detaching the mold from its holder.
  /// </summary>
  public static bool DenyLiquidPickup(
    IWorldAccessor world,
    IPlayer byPlayer,
    ItemStack? contents,
    int units
  ) {
    if (!IsLiquidContent(world, contents, units))
      return false;
    if (byPlayer.InventoryManager?.ActiveHotbarSlot?.Empty == true)
      return false;

    (byPlayer as IServerPlayer)?.SendIngameError(NeedEmptyHandsCode);
    return true;
  }

  /// <summary>
  /// Hands <paramref name="stack"/> to the player. A liquid mold goes into the active hand, which the
  /// caller has verified empty, so it cannot land in a backpack and spill; anything else is given to the
  /// inventory or dropped at <paramref name="dropPos"/>.
  /// </summary>
  public static void GiveMoldStack(
    IWorldAccessor world,
    IPlayer byPlayer,
    ItemStack stack,
    bool liquid,
    Vec3d dropPos
  ) {
    var active = byPlayer.InventoryManager?.ActiveHotbarSlot;
    if (liquid && active?.Empty == true) {
      active.Itemstack = stack;
      active.MarkDirty();
      return;
    }

    if (byPlayer.InventoryManager?.TryGiveItemstack(stack) != true)
      world.SpawnItemEntity(stack, dropPos);
  }
}
