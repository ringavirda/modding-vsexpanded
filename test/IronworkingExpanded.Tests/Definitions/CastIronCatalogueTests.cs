using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Metals;
using ExpandedLib.Testing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Cross-checks the cast-iron METAL descriptor (a JSON asset, loaded at AssetsFinalize) against the
/// cast-iron ITEM defs (C#, injected as synthetic assets). Nothing else compares the two: the goldens pin
/// each side's shape in isolation, so a typo in one code would leave both green while every
/// <c>world.GetItem(MetalRegistry.MoltenItemOf("castiron"))</c> silently returned null at runtime - the
/// failure mode the cupola and the molten canals both sit downstream of.
/// </summary>
[Collection("MetalRegistry")] // the registry is a process-wide static; serialize the mutating classes
public class CastIronCatalogueTests
{
  private const string Domain = "iwex";

  private static readonly string MetalAsset =
    DefinitionGoldens.SolutionRelative(
      "assets/iwex/config/metals/castiron.json"
    );

  public CastIronCatalogueTests() => MetalRegistry.Clear();

  private static MetalDef ShippedDef()
  {
    MetalDef? def = JsonConvert.DeserializeObject<MetalDef>(
      File.ReadAllText(MetalAsset)
    );
    Assert.NotNull(def);
    return def!;
  }

  private static void RegisterShipped() => MetalRegistry.Register(ShippedDef());

  // The cast-iron item family the emitter generates from the shipped metal descriptor - the same set the
  // runtime injects, built off the same JSON. This is what replaced the hand-authored item defs.
  private static IEnumerable<ExItemDef> EmittedDefs() =>
    MetalFamilyEmitter.Emit([ShippedDef()]);

  // The built itemtype JSON for one of the generated cast-iron defs, by item code.
  private static JObject ItemJson(string code) =>
    EmittedDefs().Single(d => d.Code == code).ToJson();

  #region Metal descriptor <-> item catalogue
  [Fact]
  public void The_shipped_metal_json_parses_into_a_registrable_def()
  {
    // MetalCatalogueLoader.Populate drops a def missing either required field, silently costing the
    // metal its identity.
    MetalDef def = ShippedDef();
    Assert.Equal("castiron", def.Code);
    Assert.Equal("iwex:ingot-castiron", def.MoltenItem);
    Assert.Equal("iwex", def.CastDomain);
  }

  [Fact]
  public void Cast_iron_flows_lower_than_the_default_but_sets_at_vanillas_line()
  {
    MetalDef def = ShippedDef();

    // Near-eutectic: fluid at low superheat. 0.75 x 1200 = 900 °C, vs the global 0.8 default.
    Assert.Equal(0.75f, def.LiquidThreshold);

    // HardenedThreshold is deliberately left null (-> the global 0.3). Vanilla's
    // BlockEntityToolMold.IsHardened HARDCODES 0.3 and is what gates collecting a casting, while the
    // pedestal's pour gate uses this registry value. Overriding it here would open a band where the
    // player can neither top the mold up nor take the plate out. Do not "tune" this without changing
    // the retrieval gate too.
    Assert.Null(def.HardenedThreshold);
  }

  [Theory]
  // The carrier every furnace/canal/mold resolves, and the bit MoltenChisel recovers.
  [InlineData("ingot-castiron")]
  [InlineData("metalbit-castiron")]
  [InlineData("metalplate-castiron")]
  public void Every_cast_iron_item_the_registry_names_actually_exists(
    string code
  )
  {
    Assert.Contains(EmittedDefs(), d => d.Code == code);
  }

  [Fact]
  public void MoltenItemOf_castiron_resolves_to_a_defined_item()
  {
    RegisterShipped();

    AssetLocation molten = MetalRegistry.MoltenItemOf("castiron");
    Assert.Equal("iwex:ingot-castiron", molten.ToString());

    // Without the shipped def the convention would build game:ingot-castiron, which does not exist.
    Assert.Contains(EmittedDefs(), d => d.Code == molten.Path);
  }

  [Fact]
  public void SolidDropOf_castiron_yields_the_shared_vanilla_scrap()
  {
    RegisterShipped();

    // Per the metals rule, a mod alloy does NOT ship its own scrap bit: breaking or chiselling cast
    // iron drops vanilla game:metalbit-iron, a drop shared across the iron alloys rather than a
    // per-metal metalbit-castiron copy. (The metalbit-castiron RESOURCE item still exists - it is just
    // no longer what a broken casting sheds.)
    AssetLocation bit = MetalRegistry.SolidDropOf(
      MetalRegistry.MoltenItemOf("castiron")
    );
    Assert.Equal("game:metalbit-iron", bit.ToString());
  }

  [Fact]
  public void The_solidified_cast_iron_block_names_the_registered_metal()
  {
    RegisterShipped();

    // BlockSolidifiedIron.GetDrops feeds this attribute straight into MoltenItemOf; a token the
    // registry does not know would silently fall back to a nonexistent game:ingot-<token>.
    JObject block = BlockStructures
      .Products.Blocks.BlockSolidifiedIron.Definitions(Domain)
      .Single(d => d.Code == "solidifiedcastiron")
      .ToJson();
    string metal = (string)block["attributes"]!["metal"]!;

    Assert.Equal("castiron", metal);
    // A dead cupola's residue breaks into the shared vanilla scrap, not a cast-iron-specific bit.
    Assert.Equal(
      "game:metalbit-iron",
      MetalRegistry.SolidDropOf(MetalRegistry.MoltenItemOf(metal)).ToString()
    );
  }

  [Fact]
  public void Cast_iron_declares_an_opted_in_generated_family()
  {
    // Stage-2's emitter reads these generation fields off the shipped JSON; a regression here silently
    // drops cast iron out of family generation or mis-stats its brittle tools.
    MetalDef def = ShippedDef();

    Assert.True(def.GenerateItemFamily);
    Assert.Equal(7200, def.Density);
    Assert.Equal(1200, def.MeltingPoint);
    Assert.Equal("iwex:block/metal/castiron", def.TexturePath);
    Assert.NotNull(def.Tools);
    Assert.Equal("brittle", def.Tools!.Preset); // brittle ~ gold-tier durability

    Assert.NotNull(def.ItemForms);
    // Keeps the legacy ingot/plate/bits codes the cupola + solidified block reference, and adds the
    // rod/nails the iron-substitution recipes need.
    Assert.Equal(
      new[] { "ingot", "plate", "bits", "rod", "nails" },
      def.ItemForms
    );

    // The scrap drop is the shared vanilla bit, not a per-metal copy.
    Assert.Equal("game:metalbit-iron", def.SolidDrop);
  }
  #endregion

  #region Casting
  [Fact]
  public void The_plate_mold_drop_template_rehomes_into_iwex_for_cast_iron()
  {
    RegisterShipped();

    // smex's plate mold ships drop = game:metalplate-{metal}. Vanilla would resolve that to
    // game:metalplate-castiron (nonexistent); the cast domain redirects it to the iwex item.
    AssetLocation cast = MetalRegistry.CastProductOf(
      new AssetLocation("game:metalplate-{metal}"),
      MetalRegistry.MoltenItemOf("castiron")
    );

    Assert.Equal("iwex:metalplate-castiron", cast.ToString());
    Assert.Contains(EmittedDefs(), d => d.Code == cast.Path);
  }

  [Fact]
  public void Iron_still_casts_into_the_vanilla_plate()
  {
    RegisterShipped();

    // The regression that matters most: registering cast iron must not perturb any other metal.
    Assert.Equal(
      "game:metalplate-iron",
      MetalRegistry
        .CastProductOf(
          new AssetLocation("game:metalplate-{metal}"),
          new AssetLocation("game:ingot-iron")
        )
        .ToString()
    );
  }
  #endregion

  #region Material rules
  [Fact]
  public void Cast_iron_is_not_forgeable()
  {
    // materials.md: castable and brittle, never beaten. What enforces it is the ABSENCE of these
    // attributes - if a future edit copies more of vanilla's ingot surface in, it silently becomes
    // anvil-workable.
    JObject attributes = (JObject)(
      ItemJson("ingot-castiron")["attributes"] ?? new JObject()
    );

    Assert.DoesNotContain(
      "workableTemperature",
      attributes.Properties().Select(p => p.Name)
    );
    Assert.DoesNotContain(
      "requiresAnvilTier",
      attributes.Properties().Select(p => p.Name)
    );
    Assert.DoesNotContain(
      "carburizableProps",
      attributes.Properties().Select(p => p.Name)
    );
  }

  [Theory]
  [InlineData("ingot-castiron")]
  [InlineData("metalbit-castiron")]
  [InlineData("metalplate-castiron")]
  public void Every_cast_iron_item_melts_at_the_cupola_temperature(string code)
  {
    // 1200 °C is load-bearing in both directions: MoltenMetal.MeltingPointOf reads it off the carrier
    // item, and the 0.75 liquid threshold is applied to it (-> flows down to 900 °C). A mismatch
    // between the three items would make a chiselled bit melt at a different point than the ingot.
    Assert.Equal(
      1200,
      (int)ItemJson(code)["combustibleProps"]!["meltingPoint"]!
    );
  }

  [Theory]
  [InlineData("ingot-castiron", "ItemIngot")]
  [InlineData("metalbit-castiron", "ItemNugget")]
  [InlineData("metalplate-castiron", "ItemMetalPlate")]
  public void Every_cast_iron_item_binds_its_vanilla_class(
    string code,
    string expected
  )
  {
    // These are vanilla class keys bound by STRING (no rename safety), so a typo would surface only at
    // world load. Pin them.
    Assert.Equal(expected, (string?)ItemJson(code)["class"]);
  }

  [Theory]
  [InlineData("ingot-castiron")]
  [InlineData("metalbit-castiron")]
  [InlineData("metalplate-castiron")]
  public void Every_cast_iron_item_smelts_back_into_a_cast_iron_ingot(
    string code
  )
  {
    // Closes the recovery loop: bits and plates must return cast iron, not vanilla iron.
    Assert.Equal(
      "iwex:ingot-castiron",
      (string?)ItemJson(code)["combustibleProps"]!["smeltedStack"]!["code"]
    );
  }
  #endregion
}
