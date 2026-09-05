using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.Items;

/// <summary>
/// The puddler's two long bars. Rabbling and drawing out are separate verbs done with separate tools, the
/// way the work was actually done: the rabble gathers the stiffening metal into a ball, and the paddle
/// carries the white-hot ball out through the door. The paddle's drawn geometry says so - it is a shaft
/// with a ball on its head.
/// </summary>
/// <remarks>
/// Neither is a vanilla tool: they have no tool mode, no durability and no mining behaviour, being long
/// iron bars a smith made rather than tool heads. What they are is a gate - the hearth checks which one
/// is held and refuses the other verb - so the process reads as two motions instead of one repeated
/// right-click.
/// </remarks>
public class PuddlingToolItemDefinitions : IExItemDefProvider {
  /// <summary>The bar the bath is gathered with, one ball per stroke.</summary>
  public const string RabbleCode = "tool-rabble";

  /// <summary>The bar a formed ball is drawn out on.</summary>
  public const string PaddleCode = "tool-paddle";

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      Tool(domain, RabbleCode, "iiex:item/tool-rabble"),
      Tool(domain, PaddleCode, "iiex:item/tool-paddle"),
    ];

  private static ExItemDef Tool(string domain, string code, string shape) =>
    ExItemDef
      .Create(domain, code)
      .Shape(shape)
      .TextureAll("game:block/metal/sheet-plain/iron5")
      .MaxStackSize(1)
      .MaterialDensity(7800)
      .Attribute("materialUnits", 200)
      .CreativeCommon("*");
}
