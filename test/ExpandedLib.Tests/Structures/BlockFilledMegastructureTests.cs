using ExpandedLib.Blocks.Structures;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The code-first generator inversion (master-plan §5.4): a mega-block's footprint no longer needs a
/// per-class member baked from JSON by the attribute source generator. The <see cref="BlockFilledMegastructure"/>
/// base implements <see cref="IFillerHost"/> once, reading the <c>fillerOffsets</c> attribute at runtime -
/// and that attribute is populated identically whether it came from a JSON file or an injected code-first def.
/// </summary>
public class BlockFilledMegastructureTests
{
  private sealed class FakeMega : BlockFilledMegastructure
  {
    public override int StructureAngle => 0;
  }

  private static FakeMega WithAttributes(string? json) =>
    new() { Attributes = json == null ? null! : new JsonObject(JToken.Parse(json)) };

  [Fact]
  public void FillerOffsets_reads_the_attribute_and_feeds_the_footprint_reader()
  {
    var block = WithAttributes(
      """{ "fillerOffsets": [{ "x": 1, "y": 0, "z": 2, "allowAttach": true }] }"""
    );

    var offsets = StructureFillers.ReadOffsets(block.FillerOffsets);
    var off = Assert.Single(offsets);
    Assert.Equal(new Vintagestory.API.MathTools.Vec3i(1, 0, 2), off.Offset);
    Assert.True(off.AllowAttach);
  }

  [Fact]
  public void FillerOffsets_of_a_block_without_the_attribute_reads_as_empty()
  {
    var block = WithAttributes("""{ "other": 1 }""");
    Assert.Empty(StructureFillers.ReadOffsets(block.FillerOffsets));
  }

  [Fact]
  public void FillerOffsets_is_null_safe_when_the_block_has_no_attributes()
  {
    var block = WithAttributes(null);
    Assert.Null(block.FillerOffsets);
    Assert.Empty(StructureFillers.ReadOffsets(block.FillerOffsets));
  }
}
