using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The discovery scan that lets a block class carry its own code-first definition
/// (<see cref="IExBlockDefProvider"/>) instead of a central list. <c>DiscoverAndRegister</c> finds every
/// implementor in an assembly, invokes its static <c>Define(domain)</c> and registers the result.
/// <see cref="ExDefinitions"/> is a process-wide static, so the class is serialized and cleared first.
/// </summary>
[Collection("ExDefinitions")]
public class ExDefinitionDiscoveryTests {
  public ExDefinitionDiscoveryTests() => ExDefinitions.Clear();

  // A block that authors its own defs, in the shape a migrated block uses.
  [BlockRegister]
  private sealed class DiscoverableBlock : Block, IExBlockDefProvider {
    public static IEnumerable<ExBlockDef> Definitions(string domain) =>
      [ExBlockDef.Create(domain, "discoverable").Class<DiscoverableBlock>()];
  }

  // A plain block with no def; the scan must ignore it.
  [BlockRegister]
  private sealed class PlainBlock : Block { }

  // An item that authors its own def, in the shape a migrated item (ItemBurden) uses.
  [ItemRegister]
  private sealed class DiscoverableItem : Item, IExItemDefProvider {
    public static IEnumerable<ExItemDef> Definitions(string domain) =>
      [ExItemDef.Create(domain, "discoverableitem").Class<DiscoverableItem>()];
  }

  // A plain item with no def; the item scan must ignore it.
  [ItemRegister]
  private sealed class PlainItem : Item { }

  // A stand-alone recipe provider, in the shape a migrated recipe file (PipeRecipeDefinitions) uses.
  private sealed class DiscoverableRecipes : IExRecipeDefProvider {
    public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
      [ExRecipeDef.Create(domain, "grid", "discoverablerecipes")];
  }

  [Fact]
  public void DiscoverAndRegister_finds_a_providers_def_and_binds_the_domain() {
    int count = ExDefinitions.DiscoverAndRegister(
      "test",
      typeof(DiscoverableBlock).Assembly
    );

    Assert.True(count >= 1);
    ExBlockDef def = Assert.Single(
      ExDefinitions.Blocks,
      d => d.Code == "discoverable"
    );
    Assert.Equal("test", def.Domain);
    // Define ran with the supplied domain, so the type-safe Class<T>() bound to that domain's key.
    Assert.Equal("test.DiscoverableBlock", (string?)def.ToJson()["class"]);
  }

  // A provider that emits somewhere other than the mod that ships it - the shape an absorbing assembly
  // uses to keep emitting an absorbed mod's codes while the relocation lands.
  [BlockRegister]
  [ExDefDomain("borrowed")]
  private sealed class ForeignDomainBlock : Block, IExBlockDefProvider {
    public static IEnumerable<ExBlockDef> Definitions(string domain) =>
      [ExBlockDef.Create(domain, "foreigndomain")];
  }

  [Fact]
  public void A_provider_may_declare_a_domain_other_than_the_registering_mod_s() {
    ExDefinitions.DiscoverAndRegister(
      "test",
      typeof(ForeignDomainBlock).Assembly
    );

    ExBlockDef def = Assert.Single(
      ExDefinitions.Blocks,
      d => d.Code == "foreigndomain"
    );
    Assert.Equal("borrowed", def.Domain);
  }

  [Fact]
  public void A_provider_without_the_attribute_still_gets_the_registering_domain() {
    ExDefinitions.DiscoverAndRegister(
      "test",
      typeof(DiscoverableBlock).Assembly
    );

    Assert.Equal(
      "test",
      Assert.Single(ExDefinitions.Blocks, d => d.Code == "discoverable").Domain
    );
  }

  [Fact]
  public void DiscoverAndRegister_ignores_classes_without_a_definition() {
    ExDefinitions.DiscoverAndRegister("test", typeof(PlainBlock).Assembly);
    Assert.DoesNotContain(ExDefinitions.Blocks, d => d.Code == "plain");
  }

  [Fact]
  public void DefinitionsOf_returns_a_types_own_declared_defs() {
    ExBlockDef def = Assert.Single(
      ExDefinitions.DefinitionsOf(typeof(DiscoverableBlock), "test")
    );
    Assert.Equal("discoverable", def.Code);
  }

  [Fact]
  public void DefinitionsOf_is_empty_for_a_class_that_declares_no_defs() {
    Assert.Empty(ExDefinitions.DefinitionsOf(typeof(PlainBlock), "test"));
  }

  [Fact]
  public void DiscoverAndRegisterItems_finds_a_providers_def_and_binds_the_domain() {
    int count = ExDefinitions.DiscoverAndRegisterItems(
      "test",
      typeof(DiscoverableItem).Assembly
    );

    Assert.True(count >= 1);
    ExItemDef def = Assert.Single(
      ExDefinitions.Items,
      d => d.Code == "discoverableitem"
    );
    Assert.Equal("test", def.Domain);
    // Define ran with the supplied domain, so the type-safe Class<T>() bound to that domain's key.
    Assert.Equal("test.DiscoverableItem", (string?)def.ToJson()["class"]);
    // Item discovery does not spill into the block registry.
    Assert.DoesNotContain(
      ExDefinitions.Blocks,
      d => d.Code == "discoverableitem"
    );
  }

  [Fact]
  public void ItemDefinitionsOf_returns_a_types_own_declared_defs() {
    ExItemDef def = Assert.Single(
      ExDefinitions.ItemDefinitionsOf(typeof(DiscoverableItem), "test")
    );
    Assert.Equal("discoverableitem", def.Code);
  }

  [Fact]
  public void ItemDefinitionsOf_is_empty_for_a_class_that_declares_no_defs() {
    Assert.Empty(ExDefinitions.ItemDefinitionsOf(typeof(PlainItem), "test"));
  }

  [Fact]
  public void DiscoverAndRegisterRecipes_finds_a_providers_file_and_binds_the_domain() {
    int count = ExDefinitions.DiscoverAndRegisterRecipes(
      "test",
      typeof(DiscoverableRecipes).Assembly
    );

    Assert.True(count >= 1);
    ExRecipeDef def = Assert.Single(
      ExDefinitions.Recipes,
      d => d.Code == "discoverablerecipes"
    );
    Assert.Equal("test", def.Domain);
    Assert.Equal("test", def.Location.Domain);
    // Recipe discovery does not spill into the block or item registries.
    Assert.DoesNotContain(
      ExDefinitions.Blocks,
      d => d.Code == "discoverablerecipes"
    );
    Assert.DoesNotContain(
      ExDefinitions.Items,
      d => d.Code == "discoverablerecipes"
    );
  }

  [Fact]
  public void RecipeDefinitionsOf_returns_a_types_own_declared_defs() {
    ExRecipeDef def = Assert.Single(
      ExDefinitions.RecipeDefinitionsOf(typeof(DiscoverableRecipes), "test")
    );
    Assert.Equal("discoverablerecipes", def.Code);
  }

  [Fact]
  public void RecipeDefinitionsOf_is_empty_for_a_class_that_declares_no_defs() {
    Assert.Empty(ExDefinitions.RecipeDefinitionsOf(typeof(PlainItem), "test"));
  }

  [Fact]
  public void OrientationMap_derives_type_to_orientation_from_variant_groups() {
    var defs = new[]
    {
      ExBlockDef
        .Create("d", "x")
        .VariantGroup("type", "straight")
        .VariantGroup("orientation", "ns", "we"),
      ExBlockDef
        .Create("d", "pipe", "y")
        .VariantGroup("type", "bend")
        .VariantGroup("orientation", "nw"),
    };

    var map = ExDefinitions.OrientationMap(defs);
    Assert.Equal(["ns", "we"], map["straight"]);
    Assert.Equal(["nw"], map["bend"]);
  }

  [Fact]
  public void OrientationMap_skips_a_def_with_no_type_states_at_all() {
    // A worldproperty-oriented block declares no explicit type states, so it contributes no
    // type-to-orientation pair.
    var defs = new[]
    {
      ExBlockDef
        .Create("d", "x")
        .VariantGroupFromProperties("side", "abstract/horizontalorientation"),
    };
    Assert.Empty(ExDefinitions.OrientationMap(defs));
  }

  [Fact]
  public void OrientationMap_maps_every_type_state_a_def_declares() {
    // A def may declare several type states in one group, and every one of them maps to that def's
    // whole orientation group. Dropping the multi-state case leaves AllowedOrientations empty, and a
    // block with no valid orientations cannot be placed at all.
    var defs = new[]
    {
      ExBlockDef
        .Create("d", "flywheel")
        .VariantGroup("type", "normal", "large")
        .VariantGroup("orientation", "ns", "we"),
    };

    var map = ExDefinitions.OrientationMap(defs);

    Assert.Equal(["ns", "we"], map["normal"]);
    Assert.Equal(["ns", "we"], map["large"]);
  }
}
