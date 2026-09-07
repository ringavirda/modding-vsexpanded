using System.IO;
using ExpandedLib.Industry.Metals;
using ExpandedLib.Testing;
using Newtonsoft.Json;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Pins the shipped Bessemer-steel metal descriptor (<c>assets/siex/config/metals/bessemersteel.json</c>).
/// siex owns this material, kept distinct from vanilla <c>game:steel</c>, and both the converter's output
/// identity and the item-family emitter key off these fields. A typo in the shipped JSON binds to null
/// without error and would otherwise surface only in game. See docs/design/materials.md.
/// </summary>
public class BessemerSteelMetalTests {
  private static MetalDef ShippedDef() {
    MetalDef? def = JsonConvert.DeserializeObject<MetalDef>(
      File.ReadAllText(
        Path.Combine(
          RepoPaths.Assets("siex"),
          "config",
          "metals",
          "bessemersteel.json"
        )
      )
    );
    Assert.NotNull(def);
    return def!;
  }

  [Fact]
  public void Bessemer_steel_is_its_own_siex_owned_alloy() {
    MetalDef def = ShippedDef();

    Assert.Equal("bessemersteel", def.Code);
    Assert.Equal("siex:ingot-bessemersteel", def.MoltenItem);
    Assert.True(def.IsAlloy);
    Assert.Equal("siex", def.CastDomain);
    // Recovers as the shared vanilla steel scrap, not a per-metal metalbit-bessemersteel.
    Assert.Equal("game:metalbit-steel", def.SolidDrop);
  }

  [Fact]
  public void Bessemer_steel_generates_a_full_good_tooled_family() {
    MetalDef def = ShippedDef();

    Assert.True(def.GenerateItemFamily);
    Assert.Equal(new[] { "ingot", "plate", "rod", "nails" }, def.ItemForms);
    Assert.Equal(7820, def.Density);
    Assert.Equal(1500, def.MeltingPoint);
    Assert.NotNull(def.Tools);
    Assert.Equal("good", def.Tools!.Preset); // good ~ workable steel-tier tools
  }
}
