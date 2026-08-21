using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.Items;

/// <summary>
/// A puddled ball on the anvil. Unlike every other <see cref="IAnvilWorkable"/> in this mod, it
/// accumulates: the first ball lays a layer of metal voxels and the second piles on top of it,
/// which is how a puddler's bar was actually made. The helve then beats the pile down into one shingled
/// bar, and because the pile and the target shape are the same voxel count, the iron is conserved exactly.
/// </summary>
/// <remarks>
/// Mid-build inputs are vanilla's own mechanism, not a patch: <c>BlockEntityAnvil.TryPut</c> calls
/// <see cref="TryPlaceOn"/> with the anvil in hand and keeps the existing work item when one stands, so a
/// workable that adds voxels rather than replacing them accumulates for free. What is this mod's is the
/// dedicated work item - see <see cref="Shingling.WorkItemCode"/> for why it is not vanilla's iron one.
/// </remarks>
[ItemRegister]
public class ItemPuddledBall : Item, IAnvilWorkable {
  /// <summary>Any anvil will do; shingling is beating a pile down, not working a hard metal.</summary>
  public int GetRequiredAnvilTier(ItemStack stack) => 0;

  public List<SmithingRecipe> GetMatchingRecipes(ItemStack stack) =>
    api.GetSmithingRecipes()
      ?.Where(r => r.Ingredient?.SatisfiesAsIngredient(stack, true) == true)
      .ToList()
    ?? [];

  /// <summary>
  /// Wrought iron shingles hot and only hot - the balls are welded together, not merely squashed - so the
  /// same halfway-to-melting rule vanilla's ingots use applies. A ball that cooled on the way from the
  /// hearth goes back into the fire.
  /// </summary>
  public bool CanWork(ItemStack stack) {
    float temperature = stack.Collectible.GetTemperature(api.World, stack);
    float meltingPoint = stack.Collectible.GetMeltingPoint(
      api.World,
      null,
      new DummySlot(stack)
    );
    return temperature >= meltingPoint / 2f;
  }

  public ItemStack? TryPlaceOn(ItemStack stack, BlockEntityAnvil beAnvil) {
    if (!CanWork(stack))
      return null;

    Item? workItem = api.World.GetItem(
      new AssetLocation(Shingling.WorkItemCode)
    );
    if (workItem == null)
      return null;

    // The pile is only ever this mod's own work item. Balls do not go onto somebody else's.
    if (
      beAnvil.WorkItemStack != null
      && beAnvil.WorkItemStack.Collectible?.Code?.ToString()
        != Shingling.WorkItemCode
    )
      return null;

    if (!PileLayer(ref beAnvil.Voxels, beAnvil.WorkItemStack == null))
      return null;

    var placed = new ItemStack(workItem, 1);
    placed.Collectible.SetTemperature(
      api.World,
      placed,
      stack.Collectible.GetTemperature(api.World, stack)
    );
    return placed;
  }

  public ItemStack GetBaseMaterial(ItemStack stack) => stack;

  /// <summary>
  /// FullyWorkable, so the helve beats the pile down rather than needing the shape hammered by hand.
  /// Nothing is shed by it: the pile is exactly the recipe's shape.
  /// </summary>
  public EnumHelveWorkableMode GetHelveWorkableMode(
    ItemStack stack,
    BlockEntityAnvil beAnvil
  ) => EnumHelveWorkableMode.FullyWorkable;

  public int VoxelCountForHandbook(ItemStack stack) => Shingling.BallVoxels;

  /// <summary>
  /// Lays one ball's worth of metal into the lowest empty layer of the pile. Returns false when the pile
  /// is already the full bar, which is what refuses a third ball rather than silently eating it.
  /// </summary>
  private static bool PileLayer(ref byte[,,] voxels, bool fresh) {
    if (fresh)
      voxels = new byte[16, 6, 16];

    for (int y = 0; y < Shingling.Layers; y++) {
      if (voxels[0, y, 0] != 0)
        continue;
      for (int x = 0; x < Shingling.Width; x++)
        for (int z = 0; z < Shingling.Depth; z++)
          voxels[x, y, z] = (byte)EnumVoxelMaterial.Metal;
      return true;
    }
    return false;
  }
}
