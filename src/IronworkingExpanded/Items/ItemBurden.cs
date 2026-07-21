using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace IronworkingExpanded.Items;

/// <summary>
/// Prepared furnace charge (the historical "burden"). Two distinct items share this class, one per
/// furnace family so they read differently in the world and can never merge into one pile:
/// <c>iwex:burden</c> is the blast-furnace ORE burden (iron ore + flux + coke) and
/// <c>iwex:remeltburden</c> is the cupola REMELT burden (scrap metal + flux + coke). Each stack carries
/// its <see cref="BurdenMix"/> as attributes; the furnace core reads the same mix (its coke fraction) to
/// drive the shared heat balance, so it never needs to know which family it is burning - only whether it
/// accepts that family. Replaces smex's count-only <c>blastmix</c>.
/// </summary>
[ItemRegister]
public partial class ItemBurden : Item, IExItemDefProvider
{
  /// <summary>The code-first itemtype definitions: the ore burden and the remelt burden, one item class
  /// for both (family is by identity, <see cref="Burden.FamilyOf"/>). Migrated from itemtypes/burden.json.</summary>
  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      Def(domain, "burden", "game:block/coal/orecoalmix"),
      // The remelt burden looks metallic (scrap-based), not coaly, so the two piles read apart at a glance.
      Def(domain, "remeltburden", "game:block/metal/tarnished/iron"),
    ];

  // The surface both burdens share; only the code and the texture differ. Kept as one factory so a
  // future field (a new transform, a behaviour) can never drift between the two families.
  private static ExItemDef Def(string domain, string code, string texture) =>
    ExItemDef
      .Create(domain, code)
      .Class<ItemBurden>()
      .MaxStackSize(128)
      .MaterialDensity(300)
      .Shape("game:item/resource/crushed/normal")
      .Texture("quartz", texture)
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
      );

  public override void GetHeldItemInfo(
    ItemSlot inSlot,
    StringBuilder dsc,
    IWorldAccessor world,
    bool withDebugInfo
  )
  {
    base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

    // Which furnace this charge is for, read the moment the stack is in hand - so the player learns
    // the burden is cupola-only (or blast-furnace-only) before walking to the wrong furnace with it.
    string family = Burden.FamilyOf(inSlot.Itemstack);
    dsc.AppendLine(Lang.Get("iwex:burden-for-" + family));

    BurdenMix mix = Burden.Read(inSlot.Itemstack);
    if (!mix.HasContent)
      return;

    // The primary channel is iron ore for ore burden but scrap metal for remelt burden; label it per
    // family so the composition line does not call scrap "iron ore".
    dsc.AppendLine(
      Lang.Get(
        family == Burden.FamilyRemelt
          ? "iwex:remelt-composition"
          : "iwex:burden-composition",
        (int)Math.Round(mix.IronFrac * 100),
        (int)Math.Round(mix.FluxFrac * 100),
        (int)Math.Round(mix.FuelFrac * 100)
      )
    );
    dsc.AppendLine(Lang.Get(Burden.ProfileLangKey(mix)));
  }
}
