using System;
using System.Text;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace IronworkingExpanded.Items;

/// <summary>
/// Prepared blast-furnace charge (the historical "burden"): iron ore, flux and coke mixed in set
/// proportions by the ore mixer. Each stack carries its <see cref="BurdenMix"/> as attributes, so
/// the held-item tooltip shows the composition and named grade, and the blast furnace can later
/// read the same mix to drive composition-aware smelting. Replaces smex's count-only <c>blastmix</c>.
/// </summary>
[ItemRegister]
public partial class ItemBurden : Item
{
  public override void GetHeldItemInfo(
    ItemSlot inSlot,
    StringBuilder dsc,
    IWorldAccessor world,
    bool withDebugInfo
  )
  {
    base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

    BurdenMix mix = Burden.Read(inSlot.Itemstack);
    if (!mix.HasContent)
      return;

    dsc.AppendLine(
      Lang.Get(
        "iwex:burden-composition",
        (int)Math.Round(mix.IronFrac * 100),
        (int)Math.Round(mix.FluxFrac * 100),
        (int)Math.Round(mix.CokeFrac * 100)
      )
    );
    dsc.AppendLine(Lang.Get(Burden.ProfileLangKey(mix)));
  }
}
