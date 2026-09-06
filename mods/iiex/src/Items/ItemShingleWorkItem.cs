using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.Items;

/// <summary>
/// The anvil work item a pile of puddled balls stands as while the helve shingles it
/// (<c>iiex:shingleworkitem-iron</c>). A vanilla <see cref="ItemWorkItem"/>, so it renders as iron
/// voxels from the ingot-pile atlas, with one change scoped to this item:
/// <see cref="GetHelveWorkableMode"/> returns <see cref="EnumHelveWorkableMode.FullyWorkable"/>, so
/// the helve beats the pile down to the bar's shape. The base <c>CanWork</c> is kept: shingling is hot
/// work, and a pile that cools below half its melting point goes back in the fire. See
/// <see cref="Shingling"/> for why the pile is not vanilla's iron work item.
/// </summary>
[ItemRegister]
public class ItemShingleWorkItem : ItemWorkItem, IAnvilWorkable {
  // The base helve-works only the plate and blister-steel recipes, and the shingling recipe is named
  // neither. IAnvilWorkable is re-implemented so the anvil picks this up in place of the base method.
  public new EnumHelveWorkableMode GetHelveWorkableMode(
    ItemStack stack,
    BlockEntityAnvil beAnvil
  ) => EnumHelveWorkableMode.FullyWorkable;
}
