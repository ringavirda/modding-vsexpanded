using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace IronworkingExpanded.Items;

/// <summary>
/// Prepared furnace charge (the historical "burden"): <b>crushed iron ore blended with lime flux, and
/// nothing else</b>. One item, made only by the burdenmaker, carrying its <see cref="BurdenMix"/> as stack
/// attributes so the ratio is decided once and travels with the stack through splitting, a hopper tank, a
/// charge column and a burn-out.
/// <para>
/// <b>Coke is not in it.</b> Fuel is charged as its own bands at the furnace
/// (<c>docs/design/layered-charge.md</c>), which is why the only quality this item carries is its flux
/// fraction.
/// </para>
/// </summary>
[ItemRegister]
public partial class ItemBurden : Item, IExItemDefProvider
{
  /// <summary>
  /// The code-first itemtype definition. Migrated from itemtypes/burden.json.
  /// <para>
  /// <b><c>iwex:remeltburden</c> is retired, not renamed - there is deliberately no migration for
  /// it.</b> Its producer (the ore mixer) and its consumer (the cupola, which charges metal directly)
  /// are both gone, so there is nothing to remap it to. A world that somehow held one loses it; no iwex
  /// build has ever shipped, so no world does.
  /// </para>
  /// </summary>
  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Def(domain, "burden", "game:block/coal/orecoalmix")];

  // Kept as a factory even with one caller: it is the shape of the item, and inlining it would make the
  // next burden-like item a copy-paste instead of a second call.
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

    // No "which furnace is this for" line: there is one burden and one furnace that eats it, so the
    // line could only ever say the same thing.
    BurdenMix mix = Burden.Read(inSlot.Itemstack);
    if (!mix.HasContent)
      return;

    // Two numbers, not three. Carbon is charged as its own fuel bands at the furnace, so printing a
    // fuel percentage here would be a second, disagreeing answer to "how much carbon is at the
    // raceway".
    dsc.AppendLine(
      Lang.Get(
        "iwex:burden-composition",
        (int)Math.Round(mix.IronFrac * 100),
        (int)Math.Round(mix.FluxFrac * 100)
      )
    );
    dsc.AppendLine(Lang.Get(Burden.ProfileLangKey(mix)));
  }
}
