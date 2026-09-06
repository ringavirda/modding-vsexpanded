using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Industry.Metals;
using ExpandedLib.Industry.Molten;
using ExpandedLib.Testing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The metal-family emitter turns an opted-in <see cref="MetalDef"/> into its resource item family
/// (ingot / plate / bits / rod / nails) and, where the metal declares one, its tool family. Covers which
/// codes come out per metal, that an un-opted-in metal is untouched, and that generated items are homed
/// and wired correctly: owning domain, shared-scrap shatter, own-ingot smelt-back. Byte-level parity of
/// the generated defs is the golden harness's job (<see cref="DefinitionGoldens"/>).
/// </summary>
public class MetalFamilyEmitterTests {
  // The shipped metal descriptor, read from the same JSON the runtime and the golden harness feed the
  // emitter.
  private static MetalDef Shipped(string mod, string metal) =>
    JsonConvert.DeserializeObject<MetalDef>(
      File.ReadAllText(
        Path.Combine(RepoPaths.Assets(mod), "config", "metals", $"{metal}.json")
      )
    )!;

  private static List<ExItemDef> Emit(params MetalDef[] metals) =>
    MetalFamilyEmitter.Emit(metals).ToList();

  private static string[] Codes(IEnumerable<ExItemDef> defs) =>
    defs.Select(d => d.Code).OrderBy(c => c).ToArray();

  // A generated def is a tool iff it carries the top-level "tool" key; resource forms never do.
  private static bool IsTool(ExItemDef d) => d.ToJson()["tool"] != null;

  private static IEnumerable<ExItemDef> Resources(
    IEnumerable<ExItemDef> defs
  ) => defs.Where(d => !IsTool(d));

  private static JObject ToolJson(IEnumerable<ExItemDef> defs, string code) =>
    defs.Single(d => d.Code == code).ToJson();

  #region Which codes per shipped metal

  [Fact]
  public void Cast_iron_emits_its_five_shipped_forms_in_iwex() {
    List<ExItemDef> defs = Emit(Shipped("iiex", "castiron"));

    // The three codes the cupola and solidified block reference, plus the two build stocks the
    // iron-substitution recipes need, all in iiex - the domain its molten item names. The tool family
    // is emitted too and asserted separately.
    Assert.Equal(
      new[]
      {
        "ingot-castiron",
        "metalbit-castiron",
        "metalnailsandstrips-castiron",
        "metalplate-castiron",
        "rod-castiron",
      },
      Codes(Resources(defs))
    );
    Assert.All(defs, d => Assert.Equal("iiex", d.Domain));
  }

  [Fact]
  public void Pig_iron_emits_its_ingot_and_the_bits_it_pays_out_in() {
    List<ExItemDef> defs = Emit(Shipped("iiex", "pigiron"));

    // A feedstock: the blast furnace's cast target, plus the bits a frozen heat sheds - and nothing
    // else (no plate/rod/nails, no tools). The bits form is what stops pig iron paying out in vanilla
    // iron, which smelts to a plain ingot and skips puddling.
    Assert.Equal(
      new[] { "ingot-pigiron", "metalbit-pigiron" },
      defs.Select(d => d.Code).Order()
    );
    Assert.All(defs, d => Assert.Equal("iiex", d.Domain));
  }

  [Fact]
  public void Bessemer_steel_emits_its_four_forms_in_siex() {
    List<ExItemDef> defs = Emit(Shipped("siex", "bessemersteel"));

    Assert.Equal(
      new[]
      {
        "ingot-bessemersteel",
        "metalnailsandstrips-bessemersteel",
        "metalplate-bessemersteel",
        "rod-bessemersteel",
      },
      Codes(Resources(defs))
    );
    // Owned by siex (its molten item is siex:ingot-bessemersteel), not the folder or iiex.
    Assert.All(defs, d => Assert.Equal("siex", d.Domain));
  }

  #endregion

  #region Opt-in gate

  [Fact]
  public void A_metal_that_has_not_opted_in_emits_nothing() {
    // Every vanilla / EM metal: it already owns game:ingot-iron, so the emitter must never touch it.
    MetalDef vanilla = new() { Code = "iron", MoltenItem = "game:ingot-iron" };
    Assert.Empty(Emit(vanilla));
  }

  [Fact]
  public void An_opted_in_metal_missing_its_molten_item_emits_nothing() {
    // Without a molten item there is no owning domain to home the family - skip rather than guess.
    MetalDef broken = new() { Code = "mystery", GenerateItemFamily = true };
    Assert.Empty(Emit(broken));
  }

  #endregion

  #region Forms selection

  [Fact]
  public void A_null_item_forms_list_emits_the_default_build_set() {
    MetalDef m = new() {
      Code = "foo",
      MoltenItem = "mymod:ingot-foo",
      GenerateItemFamily = true,
    };

    // Default = the forms a buildable metal needs (ingot + the three stocks), bits opt-in only.
    Assert.Equal(
      new[]
      {
        "ingot-foo",
        "metalnailsandstrips-foo",
        "metalplate-foo",
        "rod-foo",
      },
      Codes(Emit(m))
    );
  }

  [Fact]
  public void An_unknown_form_token_is_skipped() {
    MetalDef m = new() {
      Code = "foo",
      MoltenItem = "mymod:ingot-foo",
      GenerateItemFamily = true,
      ItemForms = ["ingot", "bogus"],
    };

    ExItemDef only = Assert.Single(Emit(m));
    Assert.Equal("ingot-foo", only.Code);
  }

  [Fact]
  public void Known_forms_are_the_five_resource_tokens() {
    Assert.Equal(
      new[] { "bits", "ingot", "nails", "plate", "rod" },
      MetalFamilyEmitter.KnownForms.OrderBy(f => f).ToArray()
    );
  }

  [Fact]
  public void Known_tools_are_the_eight_tool_tokens() {
    Assert.Equal(
      new[]
      {
        "axe",
        "chisel",
        "hammer",
        "knife",
        "pickaxe",
        "saw",
        "scythe",
        "shovel",
      },
      MetalFamilyEmitter.KnownTools.OrderBy(t => t).ToArray()
    );
  }

  #endregion

  #region Tool families (preset-driven stats)

  // The eight tool codes a full tool-making metal emits, for a given metal code.
  private static string[] ToolCodes(string metal) =>
    new[]
    {
      "axe",
      "chisel",
      "hammer",
      "knife",
      "pickaxe",
      "saw",
      "scythe",
      "shovel",
    }
      .Select(t => t + "-" + metal)
      .OrderBy(c => c)
      .ToArray();

  [Fact]
  public void Cast_iron_emits_the_full_brittle_tool_set() {
    // A Tools { preset: brittle } spec yields the eight default tool items, each in iiex beside the
    // resource forms.
    List<ExItemDef> defs = Emit(Shipped("iiex", "castiron"));

    Assert.Equal(ToolCodes("castiron"), Codes(defs.Where(IsTool)));
    Assert.All(defs.Where(IsTool), d => Assert.Equal("iiex", d.Domain));
  }

  [Fact]
  public void Cast_iron_tools_are_brittle_gold_tier_durability() {
    // Cast iron makes tools, but brittle ones: gold-tier durability of 150 against an iron pick's 1000.
    List<ExItemDef> defs = Emit(Shipped("iiex", "castiron"));

    foreach (string code in ToolCodes("castiron"))
      Assert.Equal(150, (int)ToolJson(defs, code)["durability"]!);
  }

  [Fact]
  public void Cast_iron_tools_still_mine_at_a_decent_hard_rate() {
    // The brittle preset keeps a usable mining tier and speed: iron tier (4), 6.0 on stone/ore/metal.
    JObject pick = ToolJson(
      Emit(Shipped("iiex", "castiron")),
      "pickaxe-castiron"
    );

    Assert.Equal(4, (int)pick["tooltier"]!);
    Assert.Equal(6.0, (double)pick["miningspeed"]!["metal"]!);
  }

  [Fact]
  public void Pig_iron_makes_no_tools() {
    // A feedstock (Tools null): the blast furnace's cast target only, never a tool.
    // See docs/design/materials.md.
    Assert.DoesNotContain(Emit(Shipped("iiex", "pigiron")), IsTool);
  }

  [Fact]
  public void Bessemer_steel_tools_carry_good_steel_tier_stats() {
    // The "good" preset: steel-grade durability, attack power and tool tier.
    JObject pick = ToolJson(
      Emit(Shipped("siex", "bessemersteel")),
      "pickaxe-bessemersteel"
    );

    Assert.Equal(2600, (int)pick["durability"]!);
    Assert.Equal(5, (int)pick["tooltier"]!);
    Assert.Equal(2.5, (double)pick["attackpower"]!);
  }

  [Fact]
  public void Cast_iron_tools_bind_the_vanilla_tool_classes() {
    // The vanilla class is what makes a generated item behave as its tool (axe felling, scythe
    // harvest, chisel microblocks); a class typo would surface only at world load.
    List<ExItemDef> defs = Emit(Shipped("iiex", "castiron"));

    Assert.Equal("ItemAxe", (string?)ToolJson(defs, "axe-castiron")["class"]);
    Assert.Equal(
      "ItemScythe",
      (string?)ToolJson(defs, "scythe-castiron")["class"]
    );
    // A pickaxe binds no bespoke class in vanilla - the base Item drives it via the tool + stat fields.
    Assert.Null(ToolJson(defs, "pickaxe-castiron")["class"]);
    Assert.Equal(
      "pickaxe",
      (string?)ToolJson(defs, "pickaxe-castiron")["tool"]
    );
  }

  [Fact]
  public void A_none_preset_makes_the_resource_forms_but_no_tools() {
    // "none" is the explicit way to say "buildable metal, but not tool-grade" - distinct from a null spec.
    MetalDef m = new() {
      Code = "foo",
      MoltenItem = "mymod:ingot-foo",
      GenerateItemFamily = true,
      ItemForms = ["ingot"],
      Tools = new MetalToolSpec { Preset = "none" },
    };

    List<ExItemDef> defs = Emit(m);
    Assert.DoesNotContain(defs, IsTool);
    Assert.Contains(defs, d => d.Code == "ingot-foo");
  }

  [Fact]
  public void Explicit_tool_types_limit_the_generated_set() {
    // A metal can opt into a subset of tool types; unlisted types are not emitted.
    MetalDef m = new() {
      Code = "foo",
      MoltenItem = "mymod:ingot-foo",
      GenerateItemFamily = true,
      ItemForms = [],
      Tools = new MetalToolSpec { Preset = "good", ToolTypes = ["pickaxe"] },
    };

    ExItemDef only = Assert.Single(Emit(m), IsTool);
    Assert.Equal("pickaxe-foo", only.Code);
  }

  [Fact]
  public void An_unknown_tool_type_is_skipped() {
    // Symmetry with resource forms: a type the emitter has no template for is dropped, not crashed on.
    MetalDef m = new() {
      Code = "foo",
      MoltenItem = "mymod:ingot-foo",
      GenerateItemFamily = true,
      ItemForms = [],
      Tools = new MetalToolSpec {
        Preset = "good",
        ToolTypes = ["pickaxe", "laser"],
      },
    };

    ExItemDef only = Assert.Single(Emit(m), IsTool);
    Assert.Equal("pickaxe-foo", only.Code);
  }

  [Fact]
  public void A_stat_override_beats_the_preset() {
    // Presets keep the JSON terse; an explicit number overrides only the stat it names.
    MetalDef m = new() {
      Code = "foo",
      MoltenItem = "mymod:ingot-foo",
      GenerateItemFamily = true,
      ItemForms = [],
      Tools = new MetalToolSpec {
        Preset = "brittle",
        Durability = 42,
        ToolTypes = ["pickaxe"],
      },
    };

    JObject pick = ToolJson(Emit(m), "pickaxe-foo");
    Assert.Equal(42, (int)pick["durability"]!);
    // Untouched stats still come from the brittle preset (tier 4), so the override is per-stat.
    Assert.Equal(4, (int)pick["tooltier"]!);
  }

  #endregion

  #region Wiring: scrap, smelt-back, domain

  [Fact]
  public void The_ingot_sheds_the_metals_own_scrap_on_shatter() {
    // A shattered mold yields exactly what MoltenChisel recovers - the metal's own bit since
    // 2026-08-15, where it used to be the shared vanilla one.
    ExItemDef ingot = Emit(Shipped("iiex", "castiron"))
      .Single(d => d.Code == "ingot-castiron");

    Assert.Equal(
      "iiex:metalbit-castiron",
      (string?)ingot.ToJson()["attributes"]!["shatteredStack"]!["code"]
    );
  }

  [Fact]
  public void The_ingot_shatter_falls_back_to_the_metals_own_bit_when_no_scrap_is_set() {
    // No SolidDrop -> the convention bit in the owning domain (a metal that ships its own scrap).
    MetalDef m = new() {
      Code = "foo",
      MoltenItem = "mymod:ingot-foo",
      GenerateItemFamily = true,
      ItemForms = ["ingot"],
    };
    ExItemDef ingot = Assert.Single(Emit(m));

    Assert.Equal(
      "mymod:metalbit-foo",
      (string?)ingot.ToJson()["attributes"]!["shatteredStack"]!["code"]
    );
  }

  [Fact]
  public void Every_non_ingot_form_smelts_back_to_the_metals_own_ingot() {
    // Plates, rods, nails and bits recover the alloy rather than vanilla iron. Tools carry no
    // combustibleProps, so this is scoped to the non-ingot resource forms.
    foreach (
      ExItemDef def in Resources(Emit(Shipped("iiex", "castiron")))
        .Where(d => d.Code != "ingot-castiron")
    )
      Assert.Equal(
        "iiex:ingot-castiron",
        (string?)def.ToJson()["combustibleProps"]!["smeltedStack"]!["code"]
      );
  }

  #endregion
}
