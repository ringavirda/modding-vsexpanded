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
  public void Pig_iron_is_an_iwex_feedstock_paying_out_in_its_own_bits() {
    MetalDef def = ShippedDef();

    Assert.Equal("pigiron", def.Code);
    Assert.Equal("iiex:ingot-pigiron", def.MoltenItem);
    Assert.Equal("iiex", def.CastDomain);
    // Not `game:metalbit-iron`, which it was until 2026-08-15. Twenty vanilla bits smelt to a plain iron
    // ingot, so pig iron - the cheapest metal in the game, straight off the blast furnace - was a route
    // to forgeable iron that skipped puddling entirely. It pays out in pig iron now, which remelts and
    // converts and does not forge.
    Assert.Equal("iiex:metalbit-pigiron", def.SolidDrop);
  }

  [Fact]
  public void Pig_iron_generates_the_ingot_and_its_bits_and_no_tools() {
    MetalDef def = ShippedDef();

    Assert.True(def.GenerateItemFamily);
    // The ingot the blast furnace casts into, plus the bits it pays out as; no plate/rod/nails, and
    // Tools null so the emitter makes no pig-iron tools. Pig iron is never worked, only converted.
    // See docs/design/materials.md.
    Assert.Equal(new[] { "ingot", "bits" }, def.ItemForms);
    Assert.Null(def.Tools);
  }
}
