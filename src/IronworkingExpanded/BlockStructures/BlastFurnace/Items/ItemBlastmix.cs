using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.BlastFurnace.Items;

/// <summary>Blast mix item (crushed iron ore + coke + flux); piles into a coal pile that fuels the blast furnace.</summary>
[ItemRegister]
public partial class ItemBlastmix : ItemPileable, IExItemDefProvider
{
  protected override AssetLocation PileBlockCode => new("coalpile");

  /// <summary>The code-first itemtype definition (migrated from itemtypes/blastmix.json).</summary>
  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      ExItemDef
        .Create(domain, "blastmix")
        .Class<ItemBlastmix>()
        .MaxStackSize(128)
        .MaterialDensity(300)
        .Shape("game:item/resource/crushed/normal")
        .Texture("quartz", "game:block/coal/orecoalmix")
        .HeldTpIdleAnimation("holdbothhands")
        .HeldRightReadyAnimation("holdbothhands")
        .Attribute("placeSound", "game:sound/block/sand")
        .CombustibleProps(new { burnTemperature = 600, burnDuration = 1500 })
        .CreativeTab("general", "*")
        .CreativeTab("items", "*")
        .CreativeTab(domain, "*")
        .GuiTransform(
          new
          {
            rotation = new { x = 155, y = 12, z = 0 },
            origin = new { x = 0.48, y = 0.09, z = 0.49 },
            scale = 2.54,
          }
        )
        .TpHandTransform(
          new
          {
            translation = new { x = -1.87, y = -1.25, z = -0.8 },
            rotation = new { x = 70, y = 11, z = -65 },
            scale = 0.41,
          }
        )
        .GroundTransform(
          new
          {
            translation = new { x = 0, y = 0.46, z = 0 },
            rotation = new { x = 0.2, y = 8, z = -0.1 },
            scale = 4.5,
          }
        ),
    ];
}
