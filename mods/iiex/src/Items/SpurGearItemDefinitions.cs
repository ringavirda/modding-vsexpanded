using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.Items;

/// <summary>
/// The iron tier's spur gear, the plain toothed wheel the mpenergy machines are assembled from (the rolling
/// mill stand, the transmission shafts). Pairs with <see cref="BevelGearItemDefinitions"/>: a bevel turns a
/// run through a corner, a spur passes it straight on. Defined in iiex so the constructions that need it do
/// not reach for <c>iiex:gear-iron</c>, from a mod iiex takes no dependency on.
/// <para>
/// Not gated on cast iron: it can be made from any iron, with the cupola route the cheaper one rather than
/// the only one (see <c>Recipes.Grid.EnergyRecipeDefinitions</c>), so a fresh world never depends on a
/// furnace it has not built yet.
/// </para>
/// </summary>
public class SpurGearItemDefinitions : IExItemDefProvider {
  /// <summary>Material units a spur gear is worth for remelt and scrap. Above the bevel's 40, a spur being
  /// the larger casting of the two.</summary>
  public const int GearUnits = 60;

  /// <summary>The gear's code, so the RCC constructions that require it never spell it by hand.</summary>
  public const string Code = "spurgear";

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      ExItemDef
        .Create(domain, Code)
        // Placeholder art: `workbench/shapes/item-gear-spur.json` is drawn but not yet exported to
        // the runtime tree, so this borrows vanilla's gear shape under a cast-iron texture. Once exported,
        // the Shape call becomes `iiex:item/gear-spur` and nothing else changes.
        .Shape("game:item/gear-rusty")
        .Texture("rusty-iron", "iiex:block/metal/castiron")
        .MaxStackSize(32)
        .MaterialDensity(7200)
        .Attribute("materialUnits", GearUnits)
        .CreativeCommon("*"),
    ];
}
