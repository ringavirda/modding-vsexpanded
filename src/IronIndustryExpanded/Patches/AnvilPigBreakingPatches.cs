using HarmonyLib;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.Patches;

/// <summary>
/// Turns the helve hammer into a pig-breaker. A pig placed on the anvil becomes a marked work item
/// (<see cref="ItemPig"/>); vanilla <see cref="BlockEntityAnvil.OnHelveHammerHit"/> hammers its metal
/// voxels away toward the small <c>smithing/pig</c> recipe, and this patch pays the voxels shed each hit
/// out as pig chunks and bits, with mass conserved by <see cref="PigBreaking"/>. It fires only on the
/// marked work item, which is the cold-workable <see cref="ItemPigWorkItem"/> rather than a reused
/// <c>game:workitem-iron</c>, so ordinary iron smithing and other mods' iron-work-item patches are
/// unaffected.
/// </summary>
[HarmonyPatch(typeof(BlockEntityAnvil))]
public static class AnvilPigBreakingPatches {
  private static string RemainderKey => PigBreaking.MarkerKey + "rem";

  [HarmonyPrefix]
  [HarmonyPatch("OnHelveHammerHit")]
  public static void Prefix(BlockEntityAnvil __instance, ref int __state) {
    __state = -1; // sentinel: not a pig / client / nothing to do
    if (__instance.Api.Side != EnumAppSide.Server)
      return;
    if (!IsPigWork(__instance.WorkItemStack))
      return;
    __state = MetalVoxelCount(__instance.Voxels);
  }

  [HarmonyPostfix]
  [HarmonyPatch("OnHelveHammerHit")]
  public static void Postfix(BlockEntityAnvil __instance, int __state) {
    if (__state < 0)
      return;
    ItemStack? workItem = __instance.WorkItemStack;
    if (workItem == null)
      return; // the final hit produced the recipe's pigchunk; nothing left to shed

    int removed = __state - MetalVoxelCount(__instance.Voxels);
    if (removed <= 0)
      return;

    float remainder = workItem.Attributes.GetFloat(RemainderKey);
    (int chunks, int bits) = PigBreaking.Emit(removed, ref remainder);
    workItem.Attributes.SetFloat(RemainderKey, remainder);

    Spawn(__instance, workItem, "iiex:pigchunk", chunks);
    Spawn(__instance, workItem, "iiex:pigbit", bits);
  }

  private static bool IsPigWork(ItemStack? workItem) =>
    workItem != null && workItem.Attributes.GetBool(PigBreaking.MarkerKey);

  // Count filled metal voxels (EnumVoxelMaterial.Metal == 1); slag (2) and empty (0) don't count.
  private static int MetalVoxelCount(byte[,,] voxels) {
    int count = 0;
    for (int x = 0; x < 16; x++)
      for (int y = 0; y < 6; y++)
        for (int z = 0; z < 16; z++)
          if (voxels[x, y, z] == 1)
            count++;
    return count;
  }

  private static void Spawn(
    BlockEntityAnvil anvil,
    ItemStack workItem,
    string code,
    int count
  ) {
    if (count <= 0)
      return;
    Item? item = anvil.Api.World.GetItem(new AssetLocation(code));
    if (item == null)
      return;

    var stack = new ItemStack(item, count);
    // Fresh chunks inherit the pig's temperature, so they glow and cool like the metal they came off.
    item.SetTemperature(
      anvil.Api.World,
      stack,
      workItem.Collectible.GetTemperature(anvil.Api.World, workItem)
    );
    anvil.Api.World.SpawnItemEntity(
      stack,
      anvil.Pos.ToVec3d().Add(0.5, 1.0, 0.5)
    );
  }
}
