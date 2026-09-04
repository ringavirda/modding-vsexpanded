using System.IO;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Boiler;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Ties <c>BlockEntityBoiler.FuelTextureCode</c> to the shipped shape it names: the boiler's blocktype
/// declares no textures at all (<c>BlockBoiler.BoilerShell</c> has no <c>.Texture</c> call), so the
/// fuel-texture indexer's only anchor is that the coal-layer courses are authored against this one
/// texture code in the shape file itself. Nothing else asserts that pairing - a later task that
/// re-tesselates the fuel-layer submesh could re-key the art with no test failing, and the indexer would
/// then fall straight through to <c>UnknownTexturePosition</c>: pink, on every boiler, for every fuel.
/// See <c>BlockEntityBoiler.Client.cs</c> and the CB5 report.
/// </summary>
public class BoilerFuelTextureGuards {
  private const string ShapePath = "assets/iiex/shapes/boiler/cornish.json";

  private static JObject Shape() =>
    JObject.Parse(
      File.ReadAllText(
        DefinitionGoldens.SolutionRelative(ShapePath)
      )
    );

  [Fact]
  public void The_fuel_texture_code_is_a_key_the_shipped_shape_declares() {
    JToken? entry = Shape()["textures"]?[BlockEntityBoiler.FuelTextureCode];

    Assert.True(
      entry != null,
      $"BlockEntityBoiler.FuelTextureCode names \"{BlockEntityBoiler.FuelTextureCode}\", which is not "
        + $"a key in {ShapePath}'s own textures map. The coal-layer elements were re-keyed without "
        + "updating the resolver (or the resolver's constant drifted from the art), so a charged bed "
        + "now falls through to the block's own empty texture set and draws pink."
    );
  }
}
