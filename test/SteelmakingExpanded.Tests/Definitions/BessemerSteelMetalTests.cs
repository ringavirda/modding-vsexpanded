using System.IO;
using ExpandedLib.Metals;
using ExpandedLib.Testing;
using Newtonsoft.Json;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Pins the shipped Bessemer-steel metal descriptor (<c>assets/smex/config/metals/bessemersteel.json</c>).
/// smex owns this material - materials.md keeps Bessemer steel distinct from vanilla <c>game:steel</c> - so
/// the converter's output identity and the family emitter both key off exactly these fields. A typo in the
/// shipped JSON binds silently to null and would surface only in-game; this catches it headless.
/// </summary>
public class BessemerSteelMetalTests
{
  private static MetalDef ShippedDef()
  {
    MetalDef? def = JsonConvert.DeserializeObject<MetalDef>(
      File.ReadAllText(
        DefinitionGoldens.SolutionRelative(
          "assets/smex/config/metals/bessemersteel.json"
        )
      )
    );
    Assert.NotNull(def);
    return def!;
  }

  [Fact]
  public void Bessemer_steel_is_its_own_smex_owned_alloy()
  {
    MetalDef def = ShippedDef();

    Assert.Equal("bessemersteel", def.Code);
    Assert.Equal("smex:ingot-bessemersteel", def.MoltenItem);
    Assert.True(def.IsAlloy);
    Assert.Equal("smex", def.CastDomain);
    // Recovers as the shared vanilla steel scrap, not a per-metal metalbit-bessemersteel.
    Assert.Equal("game:metalbit-steel", def.SolidDrop);
  }

  [Fact]
  public void Bessemer_steel_generates_a_full_good_tooled_family()
  {
    MetalDef def = ShippedDef();

    Assert.True(def.GenerateItemFamily);
    Assert.Equal(new[] { "ingot", "plate", "rod", "nails" }, def.ItemForms);
    Assert.Equal(7820, def.Density);
    Assert.Equal(1500, def.MeltingPoint);
    Assert.NotNull(def.Tools);
    Assert.Equal("good", def.Tools!.Preset); // good ~ workable steel-tier tools
  }
}
