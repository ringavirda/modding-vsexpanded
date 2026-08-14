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

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Cross-checks the cast-iron metal descriptor (a JSON asset loaded at AssetsFinalize) against the
/// cast-iron item defs (C#, injected as synthetic assets). The goldens pin each side's shape in
/// isolation, so only this file catches a code that exists on one side and not the other, which at
/// runtime makes <c>world.GetItem(MetalRegistry.MoltenItemOf("castiron"))</c> return null.
/// </summary>
[Collection("MetalRegistry")] // the registry is a process-wide static; serialize the mutating classes
public class CastIronCatalogueTests {
  private const string Domain = "iiex";

  private static readonly string MetalAsset =
    DefinitionGoldens.SolutionRelative(
      "assets/iiex/config/metals/castiron.json"
    );

  public CastIronCatalogueTests() => MetalRegistry.Clear();

  private static MetalDef ShippedDef() {
    MetalDef? def = JsonConvert.DeserializeObject<MetalDef>(
      File.ReadAllText(MetalAsset)
    );
    Assert.NotNull(def);
    return def!;
  }

  private static void RegisterShipped() => MetalRegistry.Register(ShippedDef());

  /// <summary>
  /// Registers every metal the hearth block can be made of, not only cast iron. The rest of this file
  /// loads <c>castiron.json</c> alone; with pig iron unregistered <c>SolidDropOf</c> falls through to
  /// the <c>ingot-X</c> to <c>metalbit-X</c> convention and answers <c>game:metalbit-pigiron</c>, which
  /// is not a real item.
  /// </summary>
  private static void RegisterHearthMetals() {
    foreach (string metal in new[] { "castiron", "pigiron" }) {
      MetalDef? def = JsonConvert.DeserializeObject<MetalDef>(
        File.ReadAllText(
          DefinitionGoldens.SolutionRelative(
            $"assets/iiex/config/metals/{metal}.json"
          )
        )
      );
      Assert.NotNull(def);
      MetalRegistry.Register(def!);
    }
  }

  // The cast-iron item family the emitter generates from the shipped metal descriptor - the same set
  // the runtime injects, built off the same JSON.
  private static IEnumerable<ExItemDef> EmittedDefs() =>
    MetalFamilyEmitter.Emit([ShippedDef()]);

  // The built itemtype JSON for one of the generated cast-iron defs, by item code.
  private static JObject ItemJson(string code) =>
    EmittedDefs().Single(d => d.Code == code).ToJson();

  #region Metal descriptor <-> item catalogue
  [Fact]
  public void The_shipped_metal_json_parses_into_a_registrable_def() {
    // MetalCatalogueLoader.Populate drops a def missing either required field.
    MetalDef def = ShippedDef();
    Assert.Equal("castiron", def.Code);
    Assert.Equal("iiex:ingot-castiron", def.MoltenItem);
    Assert.Equal("iiex", def.CastDomain);
  }

  [Fact]
  public void Cast_iron_flows_lower_than_the_default_but_sets_at_vanillas_line() {
    MetalDef def = ShippedDef();

    // Near-eutectic: fluid at low superheat. 0.75 x 1200 = 900 °C, vs the global 0.8 default.
    Assert.Equal(0.75f, def.LiquidThreshold);

    // HardenedThreshold is left null, so the global 0.3 applies. Vanilla's
    // BlockEntityToolMold.IsHardened hardcodes 0.3 and gates collecting a casting while the pedestal's
    // pour gate reads the registry value; any other value here opens a band where the mold can neither
    // be topped up nor emptied. Changing it requires changing the retrieval gate too.
    Assert.Null(def.HardenedThreshold);
  }

  [Theory]
  // The carrier every furnace/canal/mold resolves, and the bit MoltenChisel recovers.
  [InlineData("ingot-castiron")]
  [InlineData("metalbit-castiron")]
  [InlineData("metalplate-castiron")]
  public void Every_cast_iron_item_the_registry_names_actually_exists(
    string code
  ) {
    Assert.Contains(EmittedDefs(), d => d.Code == code);
  }

  [Fact]
  public void MoltenItemOf_castiron_resolves_to_a_defined_item() {
    RegisterShipped();

    AssetLocation molten = MetalRegistry.MoltenItemOf("castiron");
    Assert.Equal("iiex:ingot-castiron", molten.ToString());

    // Without the shipped def the convention builds game:ingot-castiron, which does not exist.
    Assert.Contains(EmittedDefs(), d => d.Code == molten.Path);
  }

  [Fact]
  public void SolidDropOf_castiron_yields_cast_iron_bits() {
    RegisterShipped();

    // Until 2026-08-15 this was vanilla `game:metalbit-iron`, "shared across the iron alloys" - and
    // twenty of those smelt to a plain iron ingot, so a metal poured out of a cupola became forgeable
    // iron with no puddling anywhere in the route. `iiex:metalbit-castiron` was already generated and
    // already had its lang; nothing dropped it. See docs/design/materials.md.
    AssetLocation bit = MetalRegistry.SolidDropOf(
      MetalRegistry.MoltenItemOf("castiron")
    );
    Assert.Equal("iiex:metalbit-castiron", bit.ToString());
  }

  /// <summary>
  /// The metal is carried by the block code, so this asserts the variant states rather than
  /// <c>attributes.metal</c>. <c>BlockHearthMetal.GetDrops</c> feeds <c>Variant["metal"]</c> straight
  /// into <c>MoltenItemOf</c>, and a token the registry does not know resolves to a nonexistent
  /// <c>game:ingot-&lt;token&gt;</c> without an error, because a null item drops nothing. Every state
  /// is checked so a third metal added without a def fails here.
  /// </summary>
  [Fact]
  public void Every_hearth_metal_variant_names_a_registered_metal() {
    RegisterHearthMetals();

    JObject block = BlockStructures
      .Products.Blocks.BlockHearthMetal.Definitions(Domain)
      .Single()
      .ToJson();
    string[] metals =
    [
      .. block["variantgroups"]!.Single(g => (string)g["code"]! == "metal")[
        "states"
      ]!.Select(s => (string)s!),
    ];

    Assert.Equal(["pigiron", "castiron"], metals);
    foreach (string metal in metals)
      // A dead furnace's metal breaks into that metal's OWN bits. Both used to break into the shared
      // vanilla bit, which made a frozen hearth a source of forgeable iron.
      Assert.Equal(
        $"iiex:metalbit-{metal}",
        MetalRegistry.SolidDropOf(MetalRegistry.MoltenItemOf(metal)).ToString()
      );
  }

  /// <summary>
  /// The block must carry no <c>metal</c> attribute. A block holding both the variant and an attribute
  /// gives two answers to which metal it is, and the drop path uses whichever it was written against.
  /// </summary>
  [Fact]
  public void The_hearth_metal_block_carries_no_metal_attribute() {
    JObject block = BlockStructures
      .Products.Blocks.BlockHearthMetal.Definitions(Domain)
      .Single()
      .ToJson();

    Assert.Null(block["attributes"]?["metal"]);
  }

  [Fact]
  public void Cast_iron_declares_an_opted_in_generated_family() {
    // The emitter reads these generation fields off the shipped JSON; a change here drops cast iron
    // out of family generation or mis-stats its brittle tools.
    MetalDef def = ShippedDef();

    Assert.True(def.GenerateItemFamily);
    Assert.Equal(7200, def.Density);
    Assert.Equal(1200, def.MeltingPoint);
    Assert.Equal("iiex:block/metal/castiron", def.TexturePath);
    Assert.NotNull(def.Tools);
    Assert.Equal("brittle", def.Tools!.Preset); // brittle ~ gold-tier durability

    Assert.NotNull(def.ItemForms);
    // ingot/plate/bits are the codes the cupola and the hearth metal block reference; rod/nails are
    // what the iron-substitution recipes need.
    Assert.Equal(
      new[] { "ingot", "plate", "bits", "rod", "nails" },
      def.ItemForms
    );

    // The metal pays out in its own bits, which is what keeps cast iron out of the vanilla forging
    // tree while leaving it recoverable in the cupola.
    Assert.Equal("iiex:metalbit-castiron", def.SolidDrop);
  }
  #endregion

  #region Casting
  [Fact]
  public void The_plate_mold_drop_template_rehomes_into_iwex_for_cast_iron() {
    RegisterShipped();

    // smex's plate mold ships drop = game:metalplate-{metal}. Vanilla would resolve that to
    // game:metalplate-castiron (nonexistent); the cast domain redirects it to the iiex item.
    AssetLocation cast = MetalRegistry.CastProductOf(
      new AssetLocation("game:metalplate-{metal}"),
      MetalRegistry.MoltenItemOf("castiron")
    );

    Assert.Equal("iiex:metalplate-castiron", cast.ToString());
    Assert.Contains(EmittedDefs(), d => d.Code == cast.Path);
  }

  [Fact]
  public void Iron_still_casts_into_the_vanilla_plate() {
    RegisterShipped();

    // Registering cast iron must not perturb any other metal.
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
  public void Cast_iron_is_not_forgeable() {
    // Cast iron is castable and brittle, never beaten (docs/design/materials.md). The rule is carried
    // by the absence of these attributes: copying more of vanilla's ingot surface in makes it
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
  public void Every_cast_iron_item_melts_at_the_cupola_temperature(string code) {
    // 1200 °C is read off the carrier item by MoltenMetal.MeltingPointOf and is what the 0.75 liquid
    // threshold applies to (900 °C). A mismatch across the three items melts a chiselled bit at a
    // different point than the ingot.
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
  ) {
    // Vanilla class keys bound by string, so a typo surfaces only at world load.
    Assert.Equal(expected, (string?)ItemJson(code)["class"]);
  }

  [Theory]
  [InlineData("ingot-castiron")]
  [InlineData("metalbit-castiron")]
  [InlineData("metalplate-castiron")]
  public void Every_cast_iron_item_smelts_back_into_a_cast_iron_ingot(
    string code
  ) {
    // Closes the recovery loop: bits and plates must return cast iron, not vanilla iron.
    Assert.Equal(
      "iiex:ingot-castiron",
      (string?)ItemJson(code)["combustibleProps"]!["smeltedStack"]!["code"]
    );
  }
  #endregion
}
