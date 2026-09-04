using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.Items;

/// <summary>
/// The puddled ball: wrought iron drawn white-hot off the hearth, one per rabbling stroke. It is not a
/// stock form and never enters the mill - it goes straight under the helve, where balls are piled and
/// shingled into a bar. The cooling is the carry cost, which is why it takes vanilla's own
/// <c>temperature</c> attribute and cools in the hand like any hot piece.
/// </summary>
public class WroughtBallItemDefinitions : IExItemDefProvider {
  /// <summary>The item code the helve and the hearth both name.</summary>
  public const string Code = "puddled-ironball";

  /// <summary>
  /// Metal in one ball. Divides the heat exactly: nine pigs at <see cref="ItemPig.PigUnits"/> make
  /// <c>floor(9 x PigUnits / BallUnits)</c> balls with the remainder raked out as tap cinder, and two
  /// balls make a shingled bar. Changing it moves both, which
  /// <c>PuddlingYieldTests</c> asserts rather than assumes.
  /// </summary>
  public const int BallUnits = 200;

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      ExItemDef
        .Create(domain, Code)
        // The class is what lets a ball go on an anvil and pile onto another one.
        .Class<ItemPuddledBall>()
        .Shape("iiex:item/puddled-ironball")
        // Bright ingot iron, not the tarnished sheet the older stock wears: a ball comes off the hearth
        // freshly worked. The shape names the same texture, so the two cannot disagree.
        .TextureAll("game:block/metal/ingot/iron")
        .MaxStackSize(16)
        .MaterialDensity(7800)
        .Attribute("materialUnits", BallUnits)
        // The ball comes off the hearth at welding heat and cools in the hand; `temperature` is vanilla's
        // own attribute, so the engine does the cooling.
        .Raw(
          "combustibleProps",
          new
          {
            meltingPoint = 1500,
            meltingDuration = 30,
            smeltedRatio = 1,
          }
        )
        .Raw("temperatureDamage", 4f)
        .CreativeCommon("*"),
      // The pile of balls on the anvil. Its own work item rather than vanilla's iron one so the helve has
      // exactly one matching recipe and needs no dialog - see Shingling.WorkItemCode.
      ExItemDef
        .Create(domain, "shingleworkitem")
        // Its own registered work-item class: vanilla's answers NotWorkable to the helve for a recipe
        // named neither plate nor blistersteel.
        .Class<ItemShingleWorkItem>()
        // The metal variant drives the vanilla voxel render, via the ingot-pile "iron" texture, and the
        // base class reads it on load.
        .VariantGroup("metal", "iron")
        .Shape("game:item/workitem")
        .TextureAll("game:block/metal/tarnished/iron")
        .MaxStackSize(1)
        .MaterialDensity(7800)
        .Attribute("materialUnits", BallUnits * Shingling.BallsPerBar)
        .Raw(
          "combustibleProps",
          new
          {
            meltingPoint = 1500,
            meltingDuration = 30,
            smeltedRatio = 1,
          }
        )
        .Raw("temperatureDamage", 4f),
    ];
}
