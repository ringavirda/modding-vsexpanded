using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.Items;

/// <summary>
/// The cast-iron bevel gear: the part added to a cast-iron shaft to branch an mpenergy run around a
/// corner. Consumed when added to a shaft or bevel, recovered when the bevel is broken.
/// </summary>
public class BevelGearItemDefinitions : IExItemDefProvider {
  private const int GearUnits = 40; // units of cast iron a bevel gear is worth, for remelt/scrap

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      ExItemDef
        .Create(domain, "bevelgear")
        .Shape("iwex:item/gearbevel")
        .MaxStackSize(16)
        .MaterialDensity(7200)
        .CombustibleProps(new { meltingPoint = 1150 })
        .Attribute("materialUnits", GearUnits)
        .CreativeCommon("*"),
    ];
}
