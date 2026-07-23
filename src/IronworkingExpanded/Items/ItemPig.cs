using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronworkingExpanded.Items;

/// <summary>
/// The pig-iron family and the pig's helve-breaking behaviour. A cast <c>iwex:pig</c> (150 units) is
/// deliberately not smithable into tools, but - being brittle - it CAN be shattered under a helve hammer:
/// implementing <see cref="IAnvilWorkable"/> lets the player set a pig on an anvil, where it becomes a work
/// item of <see cref="PigBreaking.PigVoxels"/> voxels tagged with <see cref="PigBreaking.MarkerKey"/>. The
/// helve then hammers it down to the tiny <c>smithing/pig</c> recipe shape (one leftover pigchunk), and
/// <see cref="IronworkingExpanded.Patches.AnvilPigBreakingPatches"/> pays the shed voxels out as chunks and
/// bits - conserving the mass. The work item reuses the vanilla iron one so it renders without any custom
/// registration; the marker keeps the patch off ordinary iron smithing.
/// <para>
/// The pig's broken products live here too - <c>iwex:pigchunk</c> (25 u) and <c>iwex:pigbit</c> (5 u) - as
/// they are nothing but smaller denominations of a pig, so the whole family and its unit hierarchy sit in
/// one file. Only the pig needs a custom class; the chunk and bit are plain <see cref="Item"/>s. All three
/// are non-forgeable brittle scrap that feeds the cupola, never the smithy; the mass-conserving arithmetic
/// of breaking one down is in <see cref="PigBreaking"/>.
/// </para>
/// </summary>
[ItemRegister]
public partial class ItemPig : Item, IAnvilWorkable, IExItemDefProvider
{
  /// <summary>Units a full pig, a chunk, and a bit each represent - the mass the bed's casting and the
  /// helve-breaking both conserve (5 u bit -&gt; 25 u chunk -&gt; 150 u pig), recorded on each item as
  /// <c>materialUnits</c> and read as constants by the machines and the breaking maths.</summary>
  public const int PigUnits = 150;
  public const int ChunkUnits = 25;
  public const int BitUnits = 5;

  private const string TarnishedIron = "game:block/metal/tarnished/iron";

  #region Definitions

  // The pig carries the helve-breaking (IAnvilWorkable) class; its chunk and bit are plain items, nothing
  // but smaller denominations of the same tarnished iron. One provider for the whole family.
  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Pig(domain), PigChunk(domain), PigBit(domain)];

  private static ExItemDef Pig(string domain) =>
    ExItemDef
      .Create(domain, "pig")
      .Class<ItemPig>()
      .Shape("iwex:pig")
      // pig.json binds its single surface to the "#iron" texture code.
      .Texture("iron", TarnishedIron)
      .MaxStackSize(16)
      .MaterialDensity(7200)
      .CombustibleProps(new { meltingPoint = 1150 })
      .Attribute("materialUnits", PigUnits)
      // Brittle scrap: workable (shatterable) at any temperature, never anvil-formed into tools.
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
      ?.Where(r => r.Ingredient?.SatisfiesAsIngredient(stack, checkStackSize: true) == true)
      .ToList() ?? [];

  // Pig iron is brittle - it shatters cold, so it is workable regardless of temperature.
  public bool CanWork(ItemStack stack) => true;

  public ItemStack? TryPlaceOn(ItemStack stack, BlockEntityAnvil beAnvil)
  {
    // One pig at a time: never merge into or top up another work item.
    if (beAnvil.WorkItemStack != null)
      return null;

    Item? workItem = api.World.GetItem(new AssetLocation(PigBreaking.WorkItemCode));
    if (workItem == null)
      return null;

    var stackToPlace = new ItemStack(workItem, 1);
    stackToPlace.Collectible.SetTemperature(
      api.World,
      stackToPlace,
      stack.Collectible.GetTemperature(api.World, stack)
    );
    // Tag it so the helve patch shatters THIS work item (and only it) into chunks/bits.
    stackToPlace.Attributes.SetBool(PigBreaking.MarkerKey, value: true);

    CreatePigVoxels(ref beAnvil.Voxels);
    return stackToPlace;
  }

  public ItemStack GetBaseMaterial(ItemStack stack) => stack;

  // FullyWorkable: the helve hammers away every metal voxel not in the recipe shape - i.e. it crumbles.
  public EnumHelveWorkableMode GetHelveWorkableMode(
    ItemStack stack,
    BlockEntityAnvil beAnvil
  ) => EnumHelveWorkableMode.FullyWorkable;

  public int VoxelCountForHandbook(ItemStack stack) => PigBreaking.PigVoxels;

  // A solid 6x2x5 = 60-voxel block, positioned so it fully covers the small smithing/pig recipe shape
  // (x 4..8, y 0, z 6..7); the helve sheds the other 50 voxels, which the patch turns into chunks/bits.
  private static void CreatePigVoxels(ref byte[,,] voxels)
  {
    voxels = new byte[16, 6, 16];
    for (int x = 0; x < 6; x++)
      for (int y = 0; y < 2; y++)
        for (int z = 0; z < 5; z++)
          voxels[4 + x, y, 6 + z] = 1; // EnumVoxelMaterial.Metal
  }

  #endregion
}
