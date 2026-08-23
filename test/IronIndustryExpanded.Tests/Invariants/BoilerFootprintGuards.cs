using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Boiler.Blocks;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Ties the Cornish boiler's drawn mesh to the volume it reserves, at every orientation it can be laid
/// at. The rule is <see cref="MegablockFrames"/>'s; what this fixes is which shape, which footprint and
/// which two angles - all four read off the shipped definition, so a shape path, a footprint or a spin
/// that moves is measured where it moved to.
/// </summary>
public class BoilerFootprintGuards {
  [Theory]
  [InlineData("north")]
  [InlineData("east")]
  [InlineData("south")]
  [InlineData("west")]
  public void The_drawn_mesh_lands_inside_the_reserved_footprint(string side) {
    ExBlockDef def = BlockBoilerCornish.Definitions("iiex").Single();
    JObject json = def.ToJson();

    var block = TestBlocks.Configure(
      new BlockBoilerCornish(),
      $"iiex:boilercornish-{side[0]}",
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

    Assert.True(misfit == null, $"A '{side}' Cornish boiler {misfit}");
  }
}
