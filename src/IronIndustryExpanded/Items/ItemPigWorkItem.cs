using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.Items;

/// <summary>
/// The anvil work item a pig becomes while it is being helve-broken (<c>iiex:pigworkitem-iron</c>). A
/// vanilla <see cref="ItemWorkItem"/>, so it renders as iron voxels from the ingot-pile atlas, with two
/// changes scoped to this item so ordinary iron smithing is untouched: <c>workableTemperature: 0</c> in the
/// def, which the base <c>CanWork</c> honours, makes it workable cold; and
/// <see cref="GetHelveWorkableMode"/> returns <see cref="EnumHelveWorkableMode.FullyWorkable"/> so the helve
/// crumbles every voxel outside the pig-breaking recipe shape, which
/// <see cref="IronIndustryExpanded.Patches.AnvilPigBreakingPatches"/> pays out as chunks and bits. Being a
/// separate item from <c>game:workitem-iron</c> also keeps other mods' iron-work-item patches off the pig.
/// </summary>
[ItemRegister]
public class ItemPigWorkItem : ItemWorkItem, IAnvilWorkable, IExItemDefProvider {
  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      ExItemDef
        .Create(domain, "pigworkitem")
        .Class<ItemPigWorkItem>()
        // The metal variant drives the vanilla voxel render, via the ingot-pile "iron" texture.
        .VariantGroup("metal", "iron")
        .Raw("texture", new { @base = "game:block/metal/ingot/iron" })
        .MaxStackSize(1)
        .StorageFlags(5)
        .MaterialDensity(7000)
        // Workable at any temperature; the base CanWork reads this attribute. No creative entry: a
        // transient anvil work item, never held or crafted directly.
        .Attribute("workableTemperature", 0),
    ];

  // The base helve-works only the plate and blister-steel recipes; the pig has to crumble fully down to the
  // recipe shape. IAnvilWorkable is re-implemented so the anvil picks this up in place of the base method.
  public new EnumHelveWorkableMode GetHelveWorkableMode(
    ItemStack stack,
    BlockEntityAnvil beAnvil
  ) => EnumHelveWorkableMode.FullyWorkable;
}
