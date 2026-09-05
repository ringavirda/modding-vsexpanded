using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The firebox bed's config surface: what a bed with no behaviour properties reports, what one
/// initialised with a boiler-shaped property set reports instead, and that a fuel the bed itself takes
/// may still be one a reverberatory furnace refuses. The geometry and the bed element/prefix are what a
/// megablock boiler overrides through its own <c>EntityBehavior&lt;BEBehaviorFirebox&gt;</c> properties;
/// the shipped furnaces never set any of them and must keep reading the same defaults.
/// See docs/design/machines/firebox.md.
/// </summary>
public class FireboxConfigTests {
  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>A bed nobody initialised - a bare firebox block entity, exactly how every shipped furnace
  /// stands one up - reports the config-driven shipped defaults.</summary>
  [Fact]
  public void DefaultsMatchConfig() {
    var bed = new BEBehaviorFirebox(
      new BlockEntityFirebox { Pos = Anchor.Copy() }
    );

    Assert.Equal(6, bed.LayersPerCell);
    Assert.Equal(2, bed.UnitsPerLayer);
    Assert.Equal(12, bed.CellCapacity);
    Assert.Equal("Coke", bed.BedElement);
    Assert.Equal("CokeL", bed.LayerPrefix);
  }

  /// <summary>The property set a megablock boiler's own bed declares - a shorter, wider bed under its
  /// own element name - overrides every default and reshapes the elements it draws.</summary>
  [Fact]
  public void PropertiesOverrideDefaults() {
    var world = new TestWorld();
    var bed = new BEBehaviorFirebox(
      new BlockEntityFirebox { Pos = Anchor.Copy() }
    );

    bed.Initialize(
      world.Api,
      new JsonObject(
        new JObject {
          ["layers"] = 4,
          ["unitsPerLayer"] = 4,
          ["bedElement"] = "CoalLayers",
          ["layerPrefix"] = "L",
        }
      )
    );

    Assert.Equal(4, bed.LayersPerCell);
    Assert.Equal(4, bed.UnitsPerLayer);
    Assert.Equal(16, bed.CellCapacity);
    Assert.Equal(
      new[] { "CoalLayers/L1", "CoalLayers/L2" },
      bed.ElementsFor(2)
    );
  }

  /// <summary>
  /// Lignite clears the bed's own admission gate - it burns far hotter than a boiler needs - while a
  /// reverberatory hearth still refuses it for want of a metallurgical heat. The two tests disagree on
  /// purpose: what a bed takes and what a given machine accepts are different rules at different layers.
  /// </summary>
  [Fact]
  public void LigniteIsFuelAtTheBedButNotAtAFurnace() {
    var world = new TestWorld();
    var lignite = new ItemStack(world.RegisterItem("game:ore-lignite"), 4);
    var furnace = new BlockEntityPuddlingFurnace();

    Assert.True(BEBehaviorFirebox.IsFuel(lignite));
    Assert.False(furnace.AcceptsFireboxFuel(lignite));
  }
}
