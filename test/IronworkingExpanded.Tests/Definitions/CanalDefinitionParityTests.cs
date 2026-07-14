using System;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockNetworkMolten.Blocks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Parity oracle for the molten-canal family - one class (<see cref="BlockMoltenCanal"/>) backs eight
/// blocktypes: the four canal shapes (straight/bend/tjunction/xjunction) in a fire-brick and a cobblestone
/// skin, authored code-first from a shared data-driven builder. Goldens are the deleted JSON, minified;
/// comparison via the shared <see cref="DefinitionParity"/>.
/// </summary>
public class CanalDefinitionParityTests
{
  private const string BrickStraight =
    """{"code":"moltencanal","class":"iwex.BlockMoltenCanal","entityClass":"iwex.BlockEntityMoltenCanal","blockmaterial":"Ceramic","sounds":{"walk":"game:walk/stone","place":"game:block/ceramicplace","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":8,"creativeinventory":{"general":["*-straight-*-ns"],"iwex":["*-straight-*-ns"]},"attributes":{"fillHeight":1,"fillStart":14,"fillQuadsByLevel":[{"x1":7,"z1":0,"x2":9,"z2":16}],"handbook":{"groupBy":["moltencanal-straight-*"]}},"textures":{"granite1":{"base":"game:block/clay/brick/four/running/cream1","overlays":["game:block/clay/brick/four/running/{brick}1"]}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["straight"]},{"code":"brick","states":["fire","black","brown","cream","gray","orange","red","tan"]},{"code":"orientation","states":["ns","we"]}],"shapebytype":{"*-straight-*-ns":{"base":"iwex:molten/canal/straight"},"*-straight-*-we":{"base":"iwex:molten/canal/straight","rotateY":90}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string BrickBend =
    """{"code":"moltencanal","class":"iwex.BlockMoltenCanal","entityClass":"iwex.BlockEntityMoltenCanal","blockmaterial":"Ceramic","sounds":{"walk":"game:walk/stone","place":"game:block/ceramicplace","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":4,"creativeinventory":{"general":["*-bend-*-nw"],"iwex":["*-bend-*-nw"]},"attributes":{"fillHeight":1,"fillStart":14,"fillQuadsByLevel":[{"x1":7,"z1":0,"x2":9,"z2":9},{"x1":0,"z1":7,"x2":7,"z2":9}],"handbook":{"groupBy":["moltencanal-bend-*"]}},"textures":{"granite1":{"base":"game:block/clay/brick/four/running/cream1","overlays":["game:block/clay/brick/four/running/{brick}1"]}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["bend"]},{"code":"brick","states":["fire","black","brown","cream","gray","orange","red","tan"]},{"code":"orientation","states":["nw","se","en","ws"]}],"shapebytype":{"*-bend-*-nw":{"base":"iwex:molten/canal/bend"},"*-bend-*-en":{"base":"iwex:molten/canal/bend","rotateY":270},"*-bend-*-se":{"base":"iwex:molten/canal/bend","rotateY":180},"*-bend-*-ws":{"base":"iwex:molten/canal/bend","rotateY":90}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string BrickTjunction =
    """{"code":"moltencanal","class":"iwex.BlockMoltenCanal","entityClass":"iwex.BlockEntityMoltenCanal","blockmaterial":"Ceramic","sounds":{"walk":"game:walk/stone","place":"game:block/ceramicplace","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":4,"creativeinventory":{"general":["*-tjunction-*-esw"],"iwex":["*-tjunction-*-esw"]},"attributes":{"fillHeight":1,"fillStart":14,"fillQuadsByLevel":[{"x1":0,"z1":7,"x2":16,"z2":9},{"x1":7,"z1":0,"x2":9,"z2":7}],"handbook":{"groupBy":["moltencanal-tjunction-*"]}},"textures":{"granite1":{"base":"game:block/clay/brick/four/running/cream1","overlays":["game:block/clay/brick/four/running/{brick}1"]}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["tjunction"]},{"code":"brick","states":["fire","black","brown","cream","gray","orange","red","tan"]},{"code":"orientation","states":["nes","esw","swn","wne"]}],"shapebytype":{"*-tjunction-*-wne":{"base":"iwex:molten/canal/tjunction"},"*-tjunction-*-nes":{"base":"iwex:molten/canal/tjunction","rotateY":270},"*-tjunction-*-esw":{"base":"iwex:molten/canal/tjunction","rotateY":180},"*-tjunction-*-swn":{"base":"iwex:molten/canal/tjunction","rotateY":90}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string BrickXjunction =
    """{"code":"moltencanal","class":"iwex.BlockMoltenCanal","entityClass":"iwex.BlockEntityMoltenCanal","blockmaterial":"Ceramic","sounds":{"walk":"game:walk/stone","place":"game:block/ceramicplace","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":4,"creativeinventory":{"general":["*-xjunction-*-nswe"],"iwex":["*-xjunction-*-nswe"]},"attributes":{"fillHeight":1,"fillStart":14,"fillQuadsByLevel":[{"x1":0,"z1":7,"x2":16,"z2":9},{"x1":7,"z1":0,"x2":9,"z2":7},{"x1":7,"z1":9,"x2":9,"z2":16}],"handbook":{"groupBy":["moltencanal-xjunction-*"]}},"textures":{"granite1":{"base":"game:block/clay/brick/four/running/cream1","overlays":["game:block/clay/brick/four/running/{brick}1"]}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["xjunction"]},{"code":"brick","states":["fire","black","brown","cream","gray","orange","red","tan"]},{"code":"orientation","states":["nswe"]}],"shapebytype":{"*-xjunction-*-nswe":{"base":"iwex:molten/canal/xjunction"}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string CobbleStraight =
    """{"code":"moltencanal","class":"iwex.BlockMoltenCanal","entityClass":"iwex.BlockEntityMoltenCanal","blockmaterial":"Stone","sounds":{"walk":"game:walk/stone","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":8,"creativeinventory":{"general":["*-straight-*-ns"],"iwex":["*-straight-*-ns"]},"attributes":{"fillHeight":1,"fillStart":14,"fillQuadsByLevel":[{"x1":7,"z1":0,"x2":9,"z2":16}],"handbook":{"groupBy":["moltencanal-straight-*"]}},"textures":{"granite1":{"base":"game:block/stone/cobblestone/{rock}1"}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["straight"]},{"code":"rock","loadFromProperties":"block/rockwithdeposit"},{"code":"orientation","states":["ns","we"]}],"skipVariants":["*-halite-*","*-scoria-*","*-tuff-*","*-travertine-*"],"shapebytype":{"*-straight-*-ns":{"base":"iwex:molten/canal/straight"},"*-straight-*-we":{"base":"iwex:molten/canal/straight","rotateY":90}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string CobbleBend =
    """{"code":"moltencanal","class":"iwex.BlockMoltenCanal","entityClass":"iwex.BlockEntityMoltenCanal","blockmaterial":"Stone","sounds":{"walk":"game:walk/stone","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":4,"creativeinventory":{"general":["*-bend-*-nw"],"iwex":["*-bend-*-nw"]},"attributes":{"fillHeight":1,"fillStart":14,"fillQuadsByLevel":[{"x1":7,"z1":0,"x2":9,"z2":9},{"x1":0,"z1":7,"x2":7,"z2":9}],"handbook":{"groupBy":["moltencanal-bend-*"]}},"textures":{"granite1":{"base":"game:block/stone/cobblestone/{rock}1"}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["bend"]},{"code":"rock","loadFromProperties":"block/rockwithdeposit"},{"code":"orientation","states":["nw","se","en","ws"]}],"skipVariants":["*-halite-*","*-scoria-*","*-tuff-*","*-travertine-*"],"shapebytype":{"*-bend-*-nw":{"base":"iwex:molten/canal/bend"},"*-bend-*-en":{"base":"iwex:molten/canal/bend","rotateY":270},"*-bend-*-se":{"base":"iwex:molten/canal/bend","rotateY":180},"*-bend-*-ws":{"base":"iwex:molten/canal/bend","rotateY":90}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string CobbleTjunction =
    """{"code":"moltencanal","class":"iwex.BlockMoltenCanal","entityClass":"iwex.BlockEntityMoltenCanal","blockmaterial":"Stone","sounds":{"walk":"game:walk/stone","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":4,"creativeinventory":{"general":["*-tjunction-*-esw"],"iwex":["*-tjunction-*-esw"]},"attributes":{"fillHeight":1,"fillStart":14,"fillQuadsByLevel":[{"x1":0,"z1":7,"x2":16,"z2":9},{"x1":7,"z1":0,"x2":9,"z2":7}],"handbook":{"groupBy":["moltencanal-tjunction-*"]}},"textures":{"granite1":{"base":"game:block/stone/cobblestone/{rock}1"}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["tjunction"]},{"code":"rock","loadFromProperties":"block/rockwithdeposit"},{"code":"orientation","states":["nes","esw","swn","wne"]}],"skipVariants":["*-halite-*","*-scoria-*","*-tuff-*","*-travertine-*"],"shapebytype":{"*-tjunction-*-wne":{"base":"iwex:molten/canal/tjunction"},"*-tjunction-*-nes":{"base":"iwex:molten/canal/tjunction","rotateY":270},"*-tjunction-*-esw":{"base":"iwex:molten/canal/tjunction","rotateY":180},"*-tjunction-*-swn":{"base":"iwex:molten/canal/tjunction","rotateY":90}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string CobbleXjunction =
    """{"code":"moltencanal","class":"iwex.BlockMoltenCanal","entityClass":"iwex.BlockEntityMoltenCanal","blockmaterial":"Stone","sounds":{"walk":"game:walk/stone","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":4,"creativeinventory":{"general":["*-xjunction-*-nswe"],"iwex":["*-xjunction-*-nswe"]},"attributes":{"fillHeight":1,"fillStart":14,"fillQuadsByLevel":[{"x1":0,"z1":7,"x2":16,"z2":9},{"x1":7,"z1":0,"x2":9,"z2":7},{"x1":7,"z1":9,"x2":9,"z2":16}],"handbook":{"groupBy":["moltencanal-xjunction-*"]}},"textures":{"granite1":{"base":"game:block/stone/cobblestone/{rock}1"}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["xjunction"]},{"code":"rock","loadFromProperties":"block/rockwithdeposit"},{"code":"orientation","states":["nswe"]}],"skipVariants":["*-halite-*","*-scoria-*","*-tuff-*","*-travertine-*"],"shapebytype":{"*-xjunction-*-nswe":{"base":"iwex:molten/canal/xjunction"}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  [Theory]
  [InlineData("canalbrick", "straight", BrickStraight)]
  [InlineData("canalbrick", "bend", BrickBend)]
  [InlineData("canalbrick", "tjunction", BrickTjunction)]
  [InlineData("canalbrick", "xjunction", BrickXjunction)]
  [InlineData("canalcobblestone", "straight", CobbleStraight)]
  [InlineData("canalcobblestone", "bend", CobbleBend)]
  [InlineData("canalcobblestone", "tjunction", CobbleTjunction)]
  [InlineData("canalcobblestone", "xjunction", CobbleXjunction)]
  public void Canal_def_reproduces_the_migrated_json(
    string skin,
    string type,
    string golden
  )
  {
    string path = $"blocktypes/molten/{skin}/{type}.json";
    ExBlockDef def = BlockMoltenCanal
      .Definitions("iwex")
      .Single(d => d.Location.Path == path);

    Assert.True(
      DefinitionParity.Equal(JObject.Parse(golden), def.ToJson(), out string normalized),
      $"{skin}/{type} canal def diverged from the migrated JSON:\n{normalized}"
    );
  }

  [Fact]
  public void Definitions_covers_all_eight_canal_blocktypes()
  {
    Assert.Equal(8, BlockMoltenCanal.Definitions("iwex").Count());
  }
}
