using HarmonyLib;
using IronworkingExpanded.BlockNetworkMolten.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace IronworkingExpanded.Patches;

/// <summary>
/// Harmony patch on the vanilla mold rack that spills a molten mold the moment
/// it is placed on the rack. The rack stores molds in an internal inventory (no
/// opened GUI), so the player-inventory scan can't see it - this catches the
/// racked mold right after the vanilla put logic runs.
/// <para>
/// Part of the mold-safety handling this foundational mod owns (alongside the in-hand spill/burn tick in
/// <see cref="IronworkingExpandedModSystem"/>), so a cast-iron mold is safe on a rack without the
/// steelmaking add-on installed. Applies to our cast molds always and to vanilla clay molds when
/// EnhanceVanillaMolds is on (see <see cref="BlockNetworkMolten.Blocks.MoltenMoldSpill"/>).
/// </para>
/// </summary>
[HarmonyPatch(
  typeof(BlockMoldRack),
  nameof(BlockMoldRack.OnBlockInteractStart)
)]
public static class MoldRackSpillPatch
{
  public static void Postfix(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  )
  {
    if (
      world.Side != EnumAppSide.Server
      || world.BlockAccessor.GetBlockEntity(blockSel.Position)
        is not BlockEntityMoldRack rack
      || rack.Inventory is not { } inv
    )
      return;

    var notify = byPlayer as IServerPlayer;
    bool spilled = false;
    foreach (var slot in inv)
      spilled |= MoltenMoldSpill.SpillIfMolten(
        slot,
        world,
        spilled ? null : notify
      );
    if (spilled)
      rack.MarkDirty(true);
  }
}
