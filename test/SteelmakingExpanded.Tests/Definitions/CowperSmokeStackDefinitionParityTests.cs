using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using SteelmakingExpanded.BlockStructures.CowperStove.Blocks;
using SteelmakingExpanded.BlockStructures.SmokeStack.Blocks;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Parity oracle for the two tall smex structure anchors authored code-first: the cowper-stove intake and the
/// smoke-stack intake. Each headlines a <c>multiblockStructure</c> table (59 and 72 cells) now authored via the
/// ASCII layer DSL - one top-down cross-section per Y level - which <see cref="DefinitionParity"/> compares as
/// an unordered cell set against the deleted JSON, verbatim (minified). The smoke-stack additionally derives its
/// orientation table from its own def (it is a <c>BlockPipePassthrough</c>), so no hand-written table survives.
/// </summary>
public class CowperSmokeStackDefinitionParityTests
{
  // Verbatim (minified) copy of the former assets/smex/blocktypes/cowperstove/intake.json.
  private const string CowperGolden =
    """{"code":"cowperstove","class":"smex.BlockCowperStoveIntake","entityClass":"smex.BlockEntityCowperStove","blockmaterial":"Ceramic","sounds":{"walk":"game:walk/stone","place":"game:block/ceramicplace","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":1,"attributes":{"handbook":{"groupBy":["cowperstove-intake-*"]},"multiblockStructure":{"blockNumbers":{"game:refractorybricks-good-tier*":1,"smex:cowperstove-intake*":2,"ppex:pipe-outlet*":3,"ppex:pipe-passthrough-*":4,"smex:cowperstoveheatsink*":5,"game:cokeovendoor*":6,"game:air":7,"@(air|coalpile)":8},"offsets":[{"x":1,"y":-1,"z":0,"w":1},{"x":0,"y":-1,"z":0,"w":8},{"x":-1,"y":-1,"z":0,"w":1},{"x":1,"y":-1,"z":1,"w":1},{"x":0,"y":-1,"z":1,"w":1},{"x":-1,"y":-1,"z":1,"w":1},{"x":1,"y":-1,"z":2,"w":1},{"x":0,"y":-1,"z":2,"w":1},{"x":-1,"y":-1,"z":2,"w":1},{"x":1,"y":0,"z":0,"w":1},{"x":0,"y":0,"z":0,"w":2},{"x":-1,"y":0,"z":0,"w":1},{"x":1,"y":0,"z":1,"w":1},{"x":0,"y":0,"z":1,"w":5},{"x":-1,"y":0,"z":1,"w":6},{"x":1,"y":0,"z":2,"w":1},{"x":0,"y":0,"z":2,"w":3},{"x":-1,"y":0,"z":2,"w":1},{"x":1,"y":1,"z":0,"w":1},{"x":0,"y":1,"z":0,"w":3},{"x":-1,"y":1,"z":0,"w":1},{"x":1,"y":1,"z":1,"w":1},{"x":0,"y":1,"z":1,"w":5},{"x":-1,"y":1,"z":1,"w":1},{"x":1,"y":1,"z":2,"w":1},{"x":0,"y":1,"z":2,"w":4},{"x":-1,"y":1,"z":2,"w":1},{"x":1,"y":2,"z":0,"w":1},{"x":0,"y":2,"z":0,"w":1},{"x":-1,"y":2,"z":0,"w":1},{"x":1,"y":2,"z":1,"w":1},{"x":0,"y":2,"z":1,"w":5},{"x":-1,"y":2,"z":1,"w":1},{"x":1,"y":2,"z":2,"w":1},{"x":0,"y":2,"z":2,"w":1},{"x":-1,"y":2,"z":2,"w":1},{"x":1,"y":3,"z":0,"w":1},{"x":0,"y":3,"z":0,"w":1},{"x":-1,"y":3,"z":0,"w":1},{"x":1,"y":3,"z":1,"w":1},{"x":0,"y":3,"z":1,"w":5},{"x":-1,"y":3,"z":1,"w":1},{"x":1,"y":3,"z":2,"w":1},{"x":0,"y":3,"z":2,"w":1},{"x":-1,"y":3,"z":2,"w":1},{"x":1,"y":4,"z":0,"w":1},{"x":0,"y":4,"z":0,"w":1},{"x":-1,"y":4,"z":0,"w":1},{"x":1,"y":4,"z":1,"w":1},{"x":0,"y":4,"z":1,"w":7},{"x":-1,"y":4,"z":1,"w":1},{"x":1,"y":4,"z":2,"w":1},{"x":0,"y":4,"z":2,"w":1},{"x":-1,"y":4,"z":2,"w":1},{"x":0,"y":5,"z":0,"w":1},{"x":1,"y":5,"z":1,"w":1},{"x":0,"y":5,"z":1,"w":1},{"x":-1,"y":5,"z":1,"w":1},{"x":0,"y":5,"z":2,"w":1}]}},"creativeinventory":{"general":["*-intake-*-south"],"smex":["*-intake-*-south"]},"behaviors":[{"name":"MultiblockStructure"},{"name":"Lockable"},{"name":"HorizontalOrientable"}],"variantgroups":[{"code":"type","states":["intake"]},{"code":"refractory","states":["tier1","tier2","tier3"]},{"code":"side","loadFromProperties":"abstract/horizontalorientation"}],"shapebytype":{"*-intake-*-north":{"base":"smex:cowperstove/intake","rotateY":0},"*-intake-*-west":{"base":"smex:cowperstove/intake","rotateY":90},"*-intake-*-south":{"base":"smex:cowperstove/intake","rotateY":180},"*-intake-*-east":{"base":"smex:cowperstove/intake","rotateY":270}},"textures":{"front1":{"base":"game:block/clay/refractory/{refractory}/front1"}},"sidesolid":{"all":true},"sideopaque":{"all":false}}""";

  // Verbatim (minified) copy of the former assets/smex/blocktypes/smokestack/intake.json.
  private const string SmokeStackGolden =
    """{"code":"smokestack","class":"smex.BlockSmokeStackIntake","entityClass":"smex.BlockEntitySmokeStack","blockmaterial":"Ceramic","sounds":{"walk":"game:walk/stone","place":"game:block/ceramicplace","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":1,"attributes":{"handbook":{"groupBy":["smokestack-intake-*"]},"multiblockStructure":{"blockNumbers":{"smex:smokestack-intake*":1,"game:refractorybricks-good-tier*":2,"game:air":3,"@(claybricks-good-fire|refractorybricks-good-.*|brickcourse-.*-(black|brown|cream|gray|orange|red|tan))":4},"offsets":[{"x":0,"y":-1,"z":0,"w":2},{"x":-1,"y":-1,"z":0,"w":2},{"x":1,"y":-1,"z":0,"w":2},{"x":0,"y":-1,"z":1,"w":2},{"x":-1,"y":-1,"z":1,"w":2},{"x":1,"y":-1,"z":1,"w":2},{"x":0,"y":-1,"z":2,"w":2},{"x":-1,"y":-1,"z":2,"w":2},{"x":1,"y":-1,"z":2,"w":2},{"x":0,"y":0,"z":0,"w":1},{"x":-1,"y":0,"z":0,"w":2},{"x":1,"y":0,"z":0,"w":2},{"x":0,"y":0,"z":1,"w":3},{"x":-1,"y":0,"z":1,"w":2},{"x":1,"y":0,"z":1,"w":2},{"x":0,"y":0,"z":2,"w":2},{"x":-1,"y":0,"z":2,"w":2},{"x":1,"y":0,"z":2,"w":2},{"x":0,"y":1,"z":0,"w":2},{"x":-1,"y":1,"z":0,"w":2},{"x":1,"y":1,"z":0,"w":2},{"x":0,"y":1,"z":1,"w":3},{"x":-1,"y":1,"z":1,"w":2},{"x":1,"y":1,"z":1,"w":2},{"x":0,"y":1,"z":2,"w":2},{"x":-1,"y":1,"z":2,"w":2},{"x":1,"y":1,"z":2,"w":2},{"x":0,"y":2,"z":0,"w":4},{"x":1,"y":2,"z":1,"w":4},{"x":0,"y":2,"z":1,"w":3},{"x":-1,"y":2,"z":1,"w":4},{"x":0,"y":2,"z":2,"w":4},{"x":0,"y":3,"z":0,"w":4},{"x":1,"y":3,"z":1,"w":4},{"x":0,"y":3,"z":1,"w":3},{"x":-1,"y":3,"z":1,"w":4},{"x":0,"y":3,"z":2,"w":4},{"x":0,"y":4,"z":0,"w":4},{"x":1,"y":4,"z":1,"w":4},{"x":0,"y":4,"z":1,"w":3},{"x":-1,"y":4,"z":1,"w":4},{"x":0,"y":4,"z":2,"w":4},{"x":0,"y":5,"z":0,"w":4},{"x":1,"y":5,"z":1,"w":4},{"x":0,"y":5,"z":1,"w":3},{"x":-1,"y":5,"z":1,"w":4},{"x":0,"y":5,"z":2,"w":4},{"x":0,"y":6,"z":0,"w":4},{"x":1,"y":6,"z":1,"w":4},{"x":0,"y":6,"z":1,"w":3},{"x":-1,"y":6,"z":1,"w":4},{"x":0,"y":6,"z":2,"w":4},{"x":0,"y":7,"z":0,"w":4},{"x":1,"y":7,"z":1,"w":4},{"x":0,"y":7,"z":1,"w":3},{"x":-1,"y":7,"z":1,"w":4},{"x":0,"y":7,"z":2,"w":4},{"x":0,"y":8,"z":0,"w":4},{"x":1,"y":8,"z":1,"w":4},{"x":0,"y":8,"z":1,"w":3},{"x":-1,"y":8,"z":1,"w":4},{"x":0,"y":8,"z":2,"w":4},{"x":0,"y":9,"z":0,"w":4},{"x":1,"y":9,"z":1,"w":4},{"x":0,"y":9,"z":1,"w":3},{"x":-1,"y":9,"z":1,"w":4},{"x":0,"y":9,"z":2,"w":4},{"x":0,"y":10,"z":0,"w":4},{"x":1,"y":10,"z":1,"w":4},{"x":0,"y":10,"z":1,"w":3},{"x":-1,"y":10,"z":1,"w":4},{"x":0,"y":10,"z":2,"w":4}]}},"creativeinventory":{"general":["*-intake-*-n"],"smex":["*-intake-*-n"]},"behaviors":[{"name":"MultiblockStructure"},{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["intake"]},{"code":"refractory","states":["tier1","tier2","tier3"]},{"code":"orientation","states":["n","s","w","e"]}],"shapebytype":{"*-intake-*-s":{"base":"ppex:pipes/outlet"},"*-intake-*-e":{"base":"ppex:pipes/outlet","rotateY":90},"*-intake-*-n":{"base":"ppex:pipes/outlet","rotateY":180},"*-intake-*-w":{"base":"ppex:pipes/outlet","rotateY":270}},"textures":{"front1":{"base":"game:block/clay/refractory/{refractory}/front1"}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  #region Cowper-stove intake

  [Fact]
  public void Cowper_def_reproduces_the_migrated_json()
  {
    ExBlockDef def = BlockCowperStoveIntake.Definitions("smex").Single();
    Assert.True(
      DefinitionParity.Equal(JObject.Parse(CowperGolden), def.ToJson(), out string normalized),
      "cowper-stove intake code-first def diverged from the migrated JSON:\n" + normalized
    );
  }

  [Fact]
  public void Cowper_targets_the_smex_asset_location()
  {
    var loc = BlockCowperStoveIntake.Definitions("smex").Single().Location;
    Assert.Equal("smex", loc.Domain);
    Assert.Equal("blocktypes/cowperstove/intake.json", loc.Path);
  }

  [Fact]
  public void Cowper_multiblock_covers_all_59_cells()
  {
    JObject def = BlockCowperStoveIntake.Definitions("smex").Single().ToJson();
    var offsets = (JArray)def["attributes"]!["multiblockStructure"]!["offsets"]!;
    Assert.Equal(59, offsets.Count);
  }

  #endregion

  #region Smoke-stack intake

  [Fact]
  public void SmokeStack_def_reproduces_the_migrated_json()
  {
    ExBlockDef def = BlockSmokeStackIntake.Definitions("smex").Single();
    Assert.True(
      DefinitionParity.Equal(JObject.Parse(SmokeStackGolden), def.ToJson(), out string normalized),
      "smoke-stack intake code-first def diverged from the migrated JSON:\n" + normalized
    );
  }

  [Fact]
  public void SmokeStack_targets_the_smex_asset_location()
  {
    var loc = BlockSmokeStackIntake.Definitions("smex").Single().Location;
    Assert.Equal("smex", loc.Domain);
    Assert.Equal("blocktypes/smokestack/intake.json", loc.Path);
  }

  [Fact]
  public void SmokeStack_multiblock_covers_all_72_cells()
  {
    JObject def = BlockSmokeStackIntake.Definitions("smex").Single().ToJson();
    var offsets = (JArray)def["attributes"]!["multiblockStructure"]!["offsets"]!;
    Assert.Equal(72, offsets.Count);
  }

  [Fact]
  public void SmokeStack_derives_its_orientation_table_from_the_def()
  {
    // A BlockPipePassthrough: the BlockPipe base derives AllowedOrientations from the intake's own def; the
    // orientation variant group [n,s,w,e] carries through and its first state "n" is the fallback.
    var orientations = ExDefinitions.OrientationMap(BlockSmokeStackIntake.Definitions("smex"));
    Assert.Equal(["n", "s", "w", "e"], orientations["intake"]);
  }

  #endregion
}
