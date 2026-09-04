// Shims for API members introduced in 1.21, so missing on 1.20 only. Guarded by !GAME_GE_1_21 so
// they neither shadow nor duplicate the real members on 1.21+. See the GAME_GE_* convention in
// src/Directory.Build.props.
#if !GAME_GE_1_21
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace ExpandedLib.Legacy;

public static class LegacyApi120 {
  /// <summary>Shim for <c>BlockDropItemStack.ToRandomItemstackForPlayer</c> (1.21+, player-aware
  /// drop rate). Falls back to <c>GetNextItemStack</c>: 1.20 has no player luck modifier to
  /// honour.</summary>
  public static ItemStack? ToRandomItemstackForPlayer(
    this BlockDropItemStack drop,
    IPlayer byPlayer,
    IWorldAccessor world,
    float dropQuantityMultiplier = 1f
  ) => drop.GetNextItemStack(dropQuantityMultiplier);

  extension(BlockEntityToolMold mold) {
    /// <summary>Shim for <c>BlockEntityToolMold.MeshAngle</c> (1.21+); 1.20 tool molds do not rotate.</summary>
    public float MeshAngle => 0f;
  }
}
#endif
