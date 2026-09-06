using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace IronIndustryExpanded.Items;

/// <summary>
/// Prepared furnace charge (the historical "burden"): crushed iron ore blended with lime flux, and no
/// fuel. Made only by the burdenmaker, and carries its <see cref="BurdenMix"/> as stack attributes so
/// the ratio survives splitting, a hopper tank, a charge column and a burn-out. Fuel is charged as its
/// own bands at the furnace, so the only quality this item carries is its flux fraction. See
/// docs/design/layered-charge.md.
/// </summary>
[ItemRegister]
public partial class ItemBurden : Item, IExItemDefProvider {
  /// <summary>
  /// The code-first itemtype definition. <c>iiex:remeltburden</c> has no migration entry: neither its
  /// producer nor its consumer exists, so there is nothing to remap it to.
  /// </summary>
  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Def(domain, "burden", "game:block/coal/orecoalmix")];

  // Parameterised so a second burden-like item is another call rather than a copied block.
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
        new {
          translation = new {
            x = 0,
            y = 0,
            z = -25,
          },
          rotation = new {
            x = 171,
            y = 57,
            z = -20,
          },
          origin = new {
            x = 0.46,
            y = 0.11,
            z = 0.39,
          },
          scale = 2.64,
        }
      )
      .TpHandTransform(
        new {
          translation = new {
            x = -1.67,
            y = -1.1,
            z = -0.7,
          },
          rotation = new {
            x = 19,
            y = 26,
            z = -83,
          },
          scale = 0.48,
        }
      )
      .GroundTransform(
        new {
          translation = new {
            x = 0,
            y = 0.46,
            z = 0,
          },
          rotation = new {
            x = 0.1,
            y = 8,
            z = 0,
          },
          scale = 4.5,
        }
      );

  public override void GetHeldItemInfo(
    ItemSlot inSlot,
    StringBuilder dsc,
    IWorldAccessor world,
    bool withDebugInfo
  ) {
    base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

    BurdenMix mix = Burden.Read(inSlot.Itemstack);
    if (!mix.HasContent)
      return;

    // Iron and flux only. Carbon is charged as separate fuel bands at the furnace, so a fuel
    // percentage here would contradict the carbon actually at the raceway.
    dsc.AppendLine(
      Lang.Get(
        "iiex:burden-composition",
        (int)Math.Round(mix.IronFrac * 100),
        (int)Math.Round(mix.FluxFrac * 100)
      )
    );
    dsc.AppendLine(Lang.Get(Burden.ProfileLangKey(mix)));
  }
}
