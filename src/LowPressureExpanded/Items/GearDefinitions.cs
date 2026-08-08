using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace LowPressureExpanded.Items;

/// <summary>
/// Itemtype definitions for lpex's craftable gears. Both use the vanilla <c>Item</c> class, so there is no
/// mod class to hang the definitions on and a stand-alone provider carries them instead. See
/// <see cref="IExItemDefProvider"/> for how a never-instantiated provider class is discovered.
/// </summary>
public class GearDefinitions : IExItemDefProvider {
  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Gear(domain), LargeGear(domain)];

  // The surface both gears share: an iron/steel metal variant (whose ingot texture tints the model), the
  // steel-density material, and the general/items/lpex creative tabs.
  private static ExItemDef Common(ExItemDef def) =>
    def.VariantGroup("metal", "iron", "steel")
      .MaterialDensity(7870)
      .Texture("rusty-iron", "game:block/metal/ingot/{metal}")
      .CreativeTab("general", "*")
      .CreativeTab("items", "*")
      .CreativeTab("lpex", "*");

  private static ExItemDef Gear(string domain) =>
    Common(ExItemDef.Create(domain, "gear"))
      .Shape("game:item/gear-rusty")
      .MaxStackSize(64)
      .GuiTransform(
        new {
          translation = new {
            x = 0,
            y = 0,
            z = 0,
          },
          rotation = new {
            x = 145,
            y = -12,
            z = 0,
          },
          origin = new {
            x = 0.32,
            y = 0.0625,
            z = 0.5,
          },
          scale = 2.5,
        }
      )
      .TpHandTransform(
        new {
          translation = new {
            x = -0.45,
            y = -0.35,
            z = -0.55,
          },
          rotation = new {
            x = 0,
            y = -89,
            z = -83,
          },
          origin = new {
            x = 0.5,
            y = 0.0625,
            z = 0.5,
          },
          scale = 0.97,
        }
      )
      .GroundTransform(
        new {
          translation = new {
            x = 0,
            y = 0,
            z = 0,
          },
          rotation = new {
            x = 0,
            y = 0,
            z = 0,
          },
          origin = new {
            x = 0.39,
            y = 0,
            z = 0.47,
          },
          scale = 3.76,
        }
      );

  private static ExItemDef LargeGear(string domain) =>
    Common(ExItemDef.Create(domain, "largegear"))
      .Shape("game:block/machine/jonas/steamengine/gear24")
      .Texture("gold", "game:block/metal/ingot/{metal}")
      .MaxStackSize(16)
      .GuiTransform(
        new {
          translation = new {
            x = 0,
            y = 0,
            z = 0,
          },
          rotation = new {
            x = 145,
            y = 13,
            z = 0,
          },
          origin = new {
            x = 0.47,
            y = 0.2,
            z = 0.5,
          },
          scale = 2.5,
        }
      )
      .TpHandTransform(
        new {
          translation = new {
            x = -0.4,
            y = 0.2,
            z = -0.39,
          },
          rotation = new {
            x = 4,
            y = 103,
            z = 73,
          },
          origin = new {
            x = 0.5,
            y = 0.0625,
            z = 0.5,
          },
          scale = 1,
        }
      )
      .GroundTransform(
        new {
          translation = new {
            x = 0,
            y = 0,
            z = 0,
          },
          rotation = new {
            x = 0,
            y = 0,
            z = 0,
          },
          origin = new {
            x = 0.39,
            y = 0,
            z = 0.47,
          },
          scale = 5,
        }
      );
}
