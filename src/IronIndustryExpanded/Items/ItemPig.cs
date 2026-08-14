using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.Items;

/// <summary>
/// The pig-iron family. A cast <c>iiex:pig</c> is brittle scrap: not smithable into tools, but
/// shatterable under a helve hammer. <see cref="IAnvilWorkable"/> lets it be set on an anvil as a work
/// item of <see cref="PigBreaking.PigVoxels"/> voxels tagged with <see cref="PigBreaking.MarkerKey"/>,
/// which keeps <see cref="IronIndustryExpanded.Patches.AnvilPigBreakingPatches"/> off ordinary iron
/// smithing; the helve sheds voxels down to the <c>smithing/pig</c> shape and the patch pays them out as
/// chunks and bits. The plain <see cref="Item"/>s <c>iiex:pigchunk</c> (25 u) and <c>iiex:pigbit</c>
/// (5 u) are defined here too; break arithmetic is in <see cref="PigBreaking"/>.
/// </summary>
[ItemRegister]
public partial class ItemPig : Item, IAnvilWorkable, IExItemDefProvider {
  /// <summary>Material units a bit, a chunk and a full pig each represent (5 u, 25 u, 375 u). Casting in
  /// the bed and helve-breaking both conserve them, and each item records its value as
  /// <c>materialUnits</c>. 375 u is 5 x 3 x 10 = 150 vx³ at the mod's density rule of 2.5 u per vx³, and
  /// the cupola's charge band derives from it. See docs/design/items/pig.md.</summary>
  public const int PigUnits = 375;
  public const int ChunkUnits = 25;
  public const int BitUnits = 5;

  private const string TarnishedIron = "game:block/metal/tarnished/iron";

  #region Definitions

  // One provider for the family: the pig carries the IAnvilWorkable class, the chunk and bit are plain
  // items of the same tarnished iron.
  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Pig(domain), PigChunk(domain), PigBit(domain)];

  private static ExItemDef Pig(string domain) =>
    ExItemDef
      .Create(domain, "pig")
      .Class<ItemPig>()
      .Shape("iiex:pig")
      // pig.json binds its single surface to the "#iron" texture code.
      .Texture("iron", TarnishedIron)
      .MaxStackSize(16)
      .MaterialDensity(7200)
      .CombustibleProps(new { meltingPoint = 1150 })
      .Attribute("materialUnits", PigUnits)
      // Brittle scrap: shatterable at any temperature, never anvil-formed into tools.
      .Attribute("workableTemperature", 0)
      .CreativeCommon("*");

  private static ExItemDef PigChunk(string domain) =>
    ExItemDef
      .Create(domain, "pigchunk")
      .Shape("game:item/ore/ungraded/coke")
      .TextureAll(TarnishedIron)
      .MaxStackSize(64)
      .MaterialDensity(7200)
      .CombustibleProps(new { meltingPoint = 1150 })
      .Attribute("materialUnits", ChunkUnits)
      .CreativeCommon("*");

  private static ExItemDef PigBit(string domain) =>
    ExItemDef
      .Create(domain, "pigbit")
      .Shape("game:item/nugget")
      // The nugget shape's texture code is "#ore" (item/nugget.json), not "all".
      .Texture("ore", TarnishedIron)
      .MaxStackSize(128)
      .MaterialDensity(7200)
      .CombustibleProps(new { meltingPoint = 1150 })
      .Attribute("materialUnits", BitUnits)
      .CreativeCommon("*");

  #endregion

  #region IAnvilWorkable

  public int GetRequiredAnvilTier(ItemStack stack) => 0;

  public List<SmithingRecipe> GetMatchingRecipes(ItemStack stack) =>
    api.GetSmithingRecipes()
      ?.Where(r => r.Ingredient?.SatisfiesAsIngredient(stack, true) == true)
      .ToList()
    ?? [];

  // Pig iron shatters cold, so it is workable regardless of temperature.
  public bool CanWork(ItemStack stack) => true;

  public ItemStack? TryPlaceOn(ItemStack stack, BlockEntityAnvil beAnvil) {
    // One pig at a time: never merge into or top up another work item.
    if (beAnvil.WorkItemStack != null)
      return null;

    Item? workItem = api.World.GetItem(
      new AssetLocation(PigBreaking.WorkItemCode)
    );
    if (workItem == null)
      return null;

    var stackToPlace = new ItemStack(workItem, 1);
    stackToPlace.Collectible.SetTemperature(
      api.World,
      stackToPlace,
      stack.Collectible.GetTemperature(api.World, stack)
    );
    // Tagged so the helve patch shatters this work item, and only it, into chunks and bits.
    stackToPlace.Attributes.SetBool(PigBreaking.MarkerKey, value: true);

    CreatePigVoxels(ref beAnvil.Voxels);
    return stackToPlace;
  }

  public ItemStack GetBaseMaterial(ItemStack stack) => stack;

  // FullyWorkable makes the helve hammer away every metal voxel outside the recipe shape.
  public EnumHelveWorkableMode GetHelveWorkableMode(
    ItemStack stack,
    BlockEntityAnvil beAnvil
  ) => EnumHelveWorkableMode.FullyWorkable;

  public int VoxelCountForHandbook(ItemStack stack) => PigBreaking.PigVoxels;

  // A solid 5x3x10 = 150-voxel block, the pig's 375 u at 2.5 u/vx³, positioned so it fully covers the
  // smithing/pig recipe shape (x 4..8, y 0, z 6..7). The helve sheds the other 140 voxels, which the patch
  // turns into chunks and bits. The anvil grid is [16, 6, 16], so a 3-tall fill and z 6..15 stay in bounds.
  private static void CreatePigVoxels(ref byte[,,] voxels) {
    voxels = new byte[16, 6, 16];
    for (int x = 0; x < 5; x++)
      for (int y = 0; y < 3; y++)
        for (int z = 0; z < 10; z++)
          voxels[4 + x, y, 6 + z] = 1; // EnumVoxelMaterial.Metal
  }

  #endregion
}
