using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using SteelIndustryExpanded.BlockStructures.Boiler.Blocks;
using Vintagestory.API.Datastructures;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Ties the Lancashire boiler's drawn mesh to the volume it reserves, at every orientation it can be
/// laid at - the siex half of the Cornish's own guard, and the reason this vessel's shape spin is 0
/// while the Cornish's is 180. Its art is drawn along local -z and its footprint is authored along +z,
/// so <c>StructureAngle</c>'s own half turn is what brings the two together; adding the same offset to
/// the spin would move its mesh six cells off its fillers. That is an easy thing to "tidy up" by making
/// the two leaves look alike, which is why it is asserted rather than argued.
/// </summary>
public class BoilerFootprintGuards {
  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_drawn_mesh_lands_inside_the_reserved_footprint(string side) {
    ExBlockDef def = BlockBoilerLancashire.Definitions("siex").Single();
    JObject json = def.ToJson();

    var block = TestBlocks.Configure(
      new BlockBoilerLancashire(),
      $"siex:boilerlancashire-{side[0]}",
      1,
      ("side", side)
    );
    block.Attributes = new JsonObject((JObject)json["attributes"]!);

    JToken shape = json["shape"]!;
    string? misfit = MegablockFrames.Misfit(
      MegablockFrames.ShapeFile((string)shape["base"]!),
      (int)shape["rotateYByType"]![$"*-{side[0]}"]!,
      block,
      block.StructureAngle
    );

    Assert.True(misfit == null, $"A '{side}' Lancashire boiler {misfit}");
  }
}
