using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.Patches;

/// <summary>
/// The fork itself: which of the two routes a blister-steel ingot takes when it is set on an anvil. A hot
/// ingot is vanilla's, and becomes shear steel. A cold one is refused by vanilla, and this patch catches
/// the refusal and puts a crushable work item on the anvil instead.
/// </summary>
/// <remarks>
/// Nothing here reads a temperature. The fork is vanilla's own <c>CanWork</c>, asked once by letting
/// <c>TryPlaceOn</c> run first, which is what makes the two routes exclusive rather than merely different.
/// The recipe list is forked on the same answer, because leaving both recipes on offer would let a player
/// select the shear-steel one and helve a cold ingot down it - the mod would then have deleted vanilla's
/// requirement that blister steel be worked hot. See <see cref="BlisterBreaking"/>.
/// </remarks>
[HarmonyPatch(typeof(ItemIngot))]
public static class BlisterIngotPatches {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(ItemIngot.TryPlaceOn))]
  public static void TryPlaceOnPostfix(
    ItemStack stack,
    BlockEntityAnvil beAnvil,
    ref ItemStack? __result
  ) {
    if (
      beAnvil?.Api == null
      || !BlisterBreaking.Crushes(
        stack,
        __result != null,
        beAnvil.WorkItemStack != null
      )
    )
      return;

    IWorldAccessor world = beAnvil.Api.World;
    Item? workItem = world.GetItem(
      new AssetLocation(BlisterBreaking.WorkItemCode)
    );
    if (workItem == null)
      return;

    var placed = new ItemStack(workItem, 1);
    placed.Collectible.SetTemperature(
      world,
      placed,
      stack.Collectible.GetTemperature(world, stack)
    );
    // Tagged so the helve patch crushes this work item, and only it, into chunks.
    placed.Attributes.SetBool(BlisterBreaking.MarkerKey, value: true);

    BlisterBreaking.CreateVoxels(ref beAnvil.Voxels);
    __result = placed;
  }

  [HarmonyPostfix]
  [HarmonyPatch(nameof(ItemIngot.GetMatchingRecipes))]
  public static void GetMatchingRecipesPostfix(
    ItemIngot __instance,
    ItemStack stack,
    ref List<SmithingRecipe> __result
  ) {
    if (__result == null || !BlisterBreaking.IsBlisterIngot(stack))
      return;

    bool workable = __instance.CanWork(stack);
    var kept = __result
      .Where(r => BlisterBreaking.Offers(IsCrushRecipe(r), workable))
      .ToList();

    // A route with nothing on it is left alone rather than emptied: another mod removing either recipe
    // should cost the player a choice, not the anvil.
    if (kept.Count > 0)
      __result = kept;
  }

  private static bool IsCrushRecipe(SmithingRecipe recipe) =>
    recipe?.Name?.Path == BlisterBreaking.RecipeName;
}

/// <summary>
/// Turns the helve hammer into an ingot crusher. Vanilla <see cref="BlockEntityAnvil.OnHelveHammerHit"/>
/// hammers the marked work item's voxels away toward the small <c>smithing/blister</c> recipe, and this
/// patch pays the voxels shed each hit out as chunks, with mass conserved by
/// <see cref="BlisterBreaking"/>. It fires only on the marked work item, so ordinary steel smithing and
/// the pig chain are unaffected.
/// </summary>
[HarmonyPatch(typeof(BlockEntityAnvil))]
public static class AnvilBlisterBreakingPatches {
  private static string RemainderKey => BlisterBreaking.MarkerKey + "rem";

  [HarmonyPrefix]
  [HarmonyPatch("OnHelveHammerHit")]
  public static void Prefix(BlockEntityAnvil __instance, ref CrushHit __state) {
    __state = default; // Work == null: not a crush / client / nothing to do
    if (__instance.Api.Side != EnumAppSide.Server)
      return;
    if (
      __instance.WorkItemStack?.Attributes.GetBool(BlisterBreaking.MarkerKey)
      != true
    )
      return;

    __state = new CrushHit(
      MetalVoxelCount(__instance.Voxels),
      RecipeVoxelCount(__instance.SelectedRecipe),
      __instance.WorkItemStack
    );
  }

  /// <summary>
  /// Pays out the voxels this hit shed.
  /// </summary>
  /// <remarks>
  /// The hit that finishes the shape has to be counted differently. Vanilla completes the recipe from
  /// inside the hit, which blanks the anvil and drops the work item, so counting what is left on the anvil
  /// would read the whole remaining block as shed. What actually left is one voxel; the rest walked off as
  /// the recipe's own output. Skipping that hit instead, as the pig chain does, would strand the run's last
  /// chunk on a work item that no longer exists.
  /// </remarks>
  [HarmonyPostfix]
  [HarmonyPatch("OnHelveHammerHit")]
  public static void Postfix(BlockEntityAnvil __instance, CrushHit __state) {
    if (__state.Work == null)
      return;

    bool finished = __instance.WorkItemStack == null;
    int removed = BlisterBreaking.Shed(
      __state.Before,
      MetalVoxelCount(__instance.Voxels),
      __state.ShapeVoxels,
      finished
    );
    if (removed <= 0)
      return;

    float remainder = __state.Work.Attributes.GetFloat(RemainderKey);
    int chunks = BlisterBreaking.Emit(removed, ref remainder);
    if (!finished)
      __state.Work.Attributes.SetFloat(RemainderKey, remainder);

    Spawn(__instance, __state.Work, BlisterBreaking.ChunkCode, chunks);
  }

  /// <summary>What one helve hit started from: the metal on the anvil, the shape the recipe keeps, and the
  /// work item the running remainder rides on. A null <see cref="Work"/> means this hit is not ours.</summary>
  public readonly record struct CrushHit(
    int Before,
    int ShapeVoxels,
    ItemStack? Work
  );

  // Count filled metal voxels (EnumVoxelMaterial.Metal == 1); slag (2) and empty (0) don't count.
  private static int MetalVoxelCount(byte[,,] voxels) {
    int count = 0;
    for (int x = 0; x < 16; x++)
      for (int y = 0; y < 6; y++)
        for (int z = 0; z < 16; z++)
          if (voxels[x, y, z] == (byte)EnumVoxelMaterial.Metal)
            count++;
    return count;
  }

  private static int RecipeVoxelCount(SmithingRecipe? recipe) {
    if (recipe?.Voxels == null)
      return 0;

    int count = 0;
    for (int x = 0; x < recipe.Voxels.GetLength(0); x++)
      for (int y = 0; y < recipe.Voxels.GetLength(1); y++)
        for (int z = 0; z < recipe.Voxels.GetLength(2); z++)
          if (recipe.Voxels[x, y, z])
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
    // Fresh chunks inherit the ingot's temperature, so they glow and cool like the metal they came off.
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
