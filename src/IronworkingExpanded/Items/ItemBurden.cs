using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Definitions;
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
public partial class ItemBurden : Item, IExItemDefProvider
{
  /// <summary>The code-first itemtype definition (migrated from itemtypes/burden.json).</summary>
  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      ExItemDef
        .Create(domain, "burden")
        .Class<ItemBurden>()
        .MaxStackSize(128)
        .MaterialDensity(300)
        .Shape("game:item/resource/crushed/normal")
        .Texture("quartz", "game:block/coal/orecoalmix")
        .HeldTpIdleAnimation("holdbothhands")
        .HeldRightReadyAnimation("holdbothhands")
        .CombustibleProps(new { burnTemperature = 600, burnDuration = 1500 })
        .CreativeTab("general", "*")
        .CreativeTab("items", "*")
        .CreativeTab(domain, "*")
        .GuiTransform(
          new
          {
            translation = new { x = 0, y = 0, z = -25 },
            rotation = new { x = 171, y = 57, z = -20 },
            origin = new { x = 0.46, y = 0.11, z = 0.39 },
            scale = 2.64,
          }
        )
        .TpHandTransform(
          new
          {
            translation = new { x = -1.67, y = -1.1, z = -0.7 },
            rotation = new { x = 19, y = 26, z = -83 },
            scale = 0.48,
          }
        )
        .GroundTransform(
          new
          {
            translation = new { x = 0, y = 0.46, z = 0 },
            rotation = new { x = 0.1, y = 8, z = 0 },
            scale = 4.5,
          }
        ),
    ];

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
        (int)Math.Round(mix.FuelFrac * 100)
      )
    );
    dsc.AppendLine(Lang.Get(Burden.ProfileLangKey(mix)));
  }
}
