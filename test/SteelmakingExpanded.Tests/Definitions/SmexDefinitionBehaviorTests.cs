using System.Linq;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using SteelmakingExpanded.BlockStructures.CowperStove.Blocks;
using SteelmakingExpanded.BlockStructures.SmokeStack.Blocks;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Behavioural checks the golden parity oracle (<see cref="SmexDefinitionGoldenTests"/>) does not
/// cover: the two tall anchors' multiblock cell counts, which guard the ASCII layer DSL, and the smoke
/// stack deriving its orientation table from its own def.
/// </summary>
public class SmexDefinitionBehaviorTests {
  [Fact]
  public void Cowper_multiblock_covers_all_59_cells() {
    JObject def = BlockCowperStoveIntake.Definitions("smex").Single().ToJson();
    var offsets = (JArray)
      def["attributes"]!["multiblockStructure"]!["offsets"]!;
    Assert.Equal(59, offsets.Count);
  }

  [Fact]
  public void SmokeStack_multiblock_covers_all_72_cells() {
    JObject def = BlockSmokeStackIntake.Definitions("smex").Single().ToJson();
    var offsets = (JArray)
      def["attributes"]!["multiblockStructure"]!["offsets"]!;
    Assert.Equal(72, offsets.Count);
  }

  [Fact]
  public void SmokeStack_derives_its_orientation_table_from_the_def() {
    // The BlockPipe base derives AllowedOrientations from the intake's own def: the orientation variant
    // group [n,s,w,e] carries through, and its first state "n" is the fallback.
    var orientations = ExDefinitions.OrientationMap(
      BlockSmokeStackIntake.Definitions("smex")
    );
    Assert.Equal(["n", "s", "w", "e"], orientations["intake"]);
  }
}
