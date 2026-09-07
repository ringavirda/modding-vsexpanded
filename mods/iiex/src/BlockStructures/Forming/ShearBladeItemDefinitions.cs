using System.Collections.Generic;
using ExpandedLib.Catalogues;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.BlockStructures.Forming;

/// <summary>
/// The shear's blade sets: the consumable tooling fitted to the crop station, whose hardness gates which
/// crops it will take. Forging and tempering are vanilla mechanics, so the tier ladder is the metal the
/// blades were forged from and no hardness system of our own is owed.
/// See docs/design/mechanics/machining-line.md and docs/design/machines/shear.md.
/// </summary>
public class ShearBladeItemDefinitions : IExItemDefProvider {
  /// <summary>
  /// Metal to the tier its blades cut at. Iron is the entry rung a player reaches with the shear itself;
  /// steel is the one a crop can be gated behind, which is what
  /// <see cref="ProcessJob.MinTier"/> is for.
  /// </summary>
  private static readonly Dictionary<string, int> Tiers = new() {
    ["iron"] = 1,
    ["steel"] = 2,
  };

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      MachineTool
        .Itemtype(
          domain,
          "shearblade",
          Tiers,
          "iiex:item/shearblade",
          variantGroup: "metal"
        )
        .TextureAll("game:block/metal/plate/iron"),
    ];
}
