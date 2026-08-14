using System.IO;
using ExpandedLib.Metals;
using ExpandedLib.Testing;
using Newtonsoft.Json;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Pins the shipped pig-iron metal descriptor (<c>assets/iiex/config/metals/pigiron.json</c>): the blast
/// furnace's cast target and the converter's feedstock. Pig iron ships in solid ingot form only, with no
/// tools, and recovers as the shared vanilla iron scrap rather than a per-metal bit. The shipped file is
/// read rather than a fixture, because a typo in it binds to null silently and surfaces only in game.
/// </summary>
public class PigIronMetalTests {
  private static MetalDef ShippedDef() {
    MetalDef? def = JsonConvert.DeserializeObject<MetalDef>(
      File.ReadAllText(
        DefinitionGoldens.SolutionRelative(
          "assets/iiex/config/metals/pigiron.json"
        )
      )
    );
    Assert.NotNull(def);
    return def!;
  }

  [Fact]
  public void Pig_iron_is_an_iwex_feedstock_dropping_shared_vanilla_scrap() {
    MetalDef def = ShippedDef();

    Assert.Equal("pigiron", def.Code);
    Assert.Equal("iiex:ingot-pigiron", def.MoltenItem);
    Assert.Equal("iiex", def.CastDomain);
    Assert.Equal("game:metalbit-iron", def.SolidDrop);
  }

  [Fact]
  public void Pig_iron_generates_only_the_ingot_form_and_no_tools() {
    MetalDef def = ShippedDef();

    Assert.True(def.GenerateItemFamily);
    // Only the ingot the blast furnace casts into: no plate/rod/nails, and Tools null so the emitter
    // makes no pig-iron tools. Pig iron is never worked, only converted. See docs/design/materials.md.
    Assert.Equal(new[] { "ingot" }, def.ItemForms);
    Assert.Null(def.Tools);
  }
}
