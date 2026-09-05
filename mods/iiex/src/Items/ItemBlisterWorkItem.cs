using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.Items;

/// <summary>
/// The anvil work item a cold blister-steel ingot becomes while it is being crushed
/// (<c>iiex:blisterworkitem-blistersteel</c>). A vanilla <see cref="ItemWorkItem"/>, so it renders as
/// blister voxels from the ingot atlas, with two changes scoped to this item: <c>workableTemperature: 0</c>
/// in the def, which the base <c>CanWork</c> honours, makes it workable cold; and
/// <see cref="GetHelveWorkableMode"/> returns <see cref="EnumHelveWorkableMode.FullyWorkable"/> so the
/// helve crushes every voxel outside the recipe shape.
/// </summary>
/// <remarks>
/// Its own item rather than <c>game:workitem-blistersteel</c>, which is what makes the fork exclusive: the
/// vanilla work item is the one vanilla's shear-steel recipe is worked on, and it is not cold-workable.
/// The <c>metal</c> variant has to spell <c>blistersteel</c> - the base class reads it to name the ingot a
/// cancelled work item returns to, and to refuse a second ingot being piled onto the first.
/// </remarks>
[ItemRegister]
public class ItemBlisterWorkItem
  : ItemWorkItem,
    IAnvilWorkable,
    IExItemDefProvider {
  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      ExItemDef
        .Create(domain, "blisterworkitem")
        .Class<ItemBlisterWorkItem>()
        // The metal variant drives the vanilla voxel render, via the ingot-pile "blistersteel" texture.
        .VariantGroup("metal", "blistersteel")
        .Raw("texture", new { @base = "game:block/metal/ingot/blistersteel" })
        .MaxStackSize(1)
        .StorageFlags(5)
        .MaterialDensity(7720)
        // Workable at any temperature; the base CanWork reads this attribute. No creative entry: a
        // transient anvil work item, never held or crafted directly.
        .Attribute("workableTemperature", 0),
    ];

  // The base helve-works only the plate and blister-steel recipes, and the crushing recipe is deliberately
  // named neither. IAnvilWorkable is re-implemented so the anvil picks this up in place of the base method.
  public new EnumHelveWorkableMode GetHelveWorkableMode(
    ItemStack stack,
    BlockEntityAnvil beAnvil
  ) => EnumHelveWorkableMode.FullyWorkable;
}
