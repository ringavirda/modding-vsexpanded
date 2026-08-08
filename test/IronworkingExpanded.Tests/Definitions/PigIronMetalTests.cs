using System.IO;
using ExpandedLib.Metals;
using ExpandedLib.Testing;
using Newtonsoft.Json;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Pins the shipped pig-iron metal descriptor (<c>assets/iwex/config/metals/pigiron.json</c>): the blast
/// furnace's cast target and the converter's feedstock. Pig iron is a feedstock - solid ingot form only,
/// no tools - and recovers as the shared vanilla iron scrap rather than a per-metal bit. A typo in the
/// shipped JSON binds silently to null and would surface only in-game; this catches it headless.
/// </summary>
public class PigIronMetalTests
{
  private static MetalDef ShippedDef()
  {
    MetalDef? def = JsonConvert.DeserializeObject<MetalDef>(
      File.ReadAllText(
        DefinitionGoldens.SolutionRelative(
          "assets/iwex/config/metals/pigiron.json"
        )
      )
    );
    Assert.NotNull(def);
    return def!;
  }

  [Fact]
  public void Pig_iron_is_an_iwex_feedstock_dropping_shared_vanilla_scrap()
  {
    MetalDef def = ShippedDef();

    Assert.Equal("pigiron", def.Code);
    Assert.Equal("iwex:ingot-pigiron", def.MoltenItem);
    Assert.Equal("iwex", def.CastDomain);
    Assert.Equal("game:metalbit-iron", def.SolidDrop);
  }

  [Fact]
  public void Pig_iron_generates_only_the_ingot_form_and_no_tools()
  {
    MetalDef def = ShippedDef();

    Assert.True(def.GenerateItemFamily);
    // Feedstock: just the ingot the blast furnace casts into - no plate/rod/nails, and Tools null so the
    // emitter makes no pig-iron tools (materials.md: pig iron is never worked, only converted).
    Assert.Equal(new[] { "ingot" }, def.ItemForms);
    Assert.Null(def.Tools);
  }
}
