using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The discovery scan that lets a block class carry its own code-first definition
/// (<see cref="IExBlockDefProvider"/>) instead of a central list. <c>DiscoverAndRegister</c> must find
/// every implementor in an assembly, invoke its static <c>Define(domain)</c>, and register the result.
/// <see cref="ExDefinitions"/> is a process-wide static, so the class is serialized and cleared first.
/// </summary>
[Collection("ExDefinitions")]
public class ExDefinitionDiscoveryTests
{
  public ExDefinitionDiscoveryTests() => ExDefinitions.Clear();

  // A block that authors its own def(s) - exactly the shape a real migrated block uses.
  [BlockRegister]
  private sealed class DiscoverableBlock : Block, IExBlockDefProvider
  {
    public static IEnumerable<ExBlockDef> Definitions(string domain) =>
      [ExBlockDef.Create(domain, "discoverable").Class<DiscoverableBlock>()];
  }

  // A plain block with no def - must be ignored by the scan.
  [BlockRegister]
  private sealed class PlainBlock : Block { }

  [Fact]
  public void DiscoverAndRegister_finds_a_providers_def_and_binds_the_domain()
  {
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

  [Fact]
  public void DiscoverAndRegister_ignores_classes_without_a_definition()
  {
    ExDefinitions.DiscoverAndRegister("test", typeof(PlainBlock).Assembly);
    Assert.DoesNotContain(ExDefinitions.Blocks, d => d.Code == "plain");
  }

  [Fact]
  public void DefinitionsOf_returns_a_types_own_declared_defs()
  {
    ExBlockDef def = Assert.Single(
      ExDefinitions.DefinitionsOf(typeof(DiscoverableBlock), "test")
    );
    Assert.Equal("discoverable", def.Code);
  }

  [Fact]
  public void DefinitionsOf_is_empty_for_a_class_that_declares_no_defs()
  {
    Assert.Empty(ExDefinitions.DefinitionsOf(typeof(PlainBlock), "test"));
  }

  [Fact]
  public void OrientationMap_derives_type_to_orientation_from_variant_groups()
  {
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
  public void OrientationMap_skips_defs_without_a_single_type_state()
  {
    // A worldproperty-oriented block (no explicit type states) contributes nothing.
    var defs = new[]
    {
      ExBlockDef
        .Create("d", "x")
        .VariantGroupFromProperties("side", "abstract/horizontalorientation"),
    };
    Assert.Empty(ExDefinitions.OrientationMap(defs));
  }
}
