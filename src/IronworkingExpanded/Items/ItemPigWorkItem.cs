using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronworkingExpanded.Items;

/// <summary>
/// The anvil work item a pig becomes while it is being helve-broken (<c>iwex:pigworkitem-iron</c>). It is a
/// vanilla <see cref="ItemWorkItem"/> - so it renders and behaves like any metal work item (iron voxels via
/// the ingot-pile atlas) - with two pig-specific tweaks, both ISOLATED to this item so ordinary iron
/// smithing is untouched:
/// <list type="bullet">
/// <item>the def carries <c>workableTemperature: 0</c>, which the base <see cref="ItemWorkItem"/>'s
/// <c>CanWork</c> honours (the same trick vanilla uses for soft lead), so it is workable COLD - pig iron is
/// brittle and shatters cold, never needing the forge;</item>
/// <item><see cref="GetHelveWorkableMode"/> returns <see cref="EnumHelveWorkableMode.FullyWorkable"/> (the
/// base only helve-works the plate / blister-steel recipes), so the helve crumbles every voxel outside the
/// tiny pig-breaking recipe shape - which <see cref="IronworkingExpanded.Patches.AnvilPigBreakingPatches"/>
/// pays out as chunks and bits.</item>
/// </list>
/// Being its own item (not the reused <c>game:workitem-iron</c>) also keeps other mods' iron-work-item
/// patches (e.g. SmithingPlus's bit recovery) off the pig.
/// </summary>
[ItemRegister]
public class ItemPigWorkItem : ItemWorkItem, IAnvilWorkable, IExItemDefProvider
{
  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      ExItemDef
        .Create(domain, "pigworkitem")
        .Class<ItemPigWorkItem>()
        // Iron metal drives the vanilla voxel render (the ingot-pile "iron" texture).
        .VariantGroup("metal", "iron")
        .Raw("texture", new { @base = "game:block/metal/ingot/iron" })
        .MaxStackSize(1)
        .StorageFlags(5)
        .MaterialDensity(7000)
        // Brittle: workable at any temperature (base CanWork reads this, as vanilla lead does). No creative
        // entry - this is a transient anvil work item, never held or crafted directly.
        .Attribute("workableTemperature", 0),
    ];

  // The base only helve-works the plate / blister-steel recipes; the pig needs the helve to crumble it
  // fully down to the recipe shape (re-implementing IAnvilWorkable so this overrides the base for the anvil).
  public new EnumHelveWorkableMode GetHelveWorkableMode(
    ItemStack stack,
    BlockEntityAnvil beAnvil
  ) => EnumHelveWorkableMode.FullyWorkable;
}
