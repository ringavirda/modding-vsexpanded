using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockNetworkMolten.Blocks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Parity oracle for the canal-family endpoints authored code-first off the same shared surface as the
/// canals: the pour start (fire-brick + cobblestone skin), the tap, the mold pedestal (fire-brick +
/// cobblestone), and the portable molten barrel. Goldens are the deleted JSON, minified; comparison via the
/// shared <see cref="DefinitionParity"/> (numeric-type-agnostic, order-sensitive on the schema arrays).
/// </summary>
public class MoltenEndpointDefinitionParityTests
{
  #region Start (pour anchor)

  private const string StartBrick =
    """{"code":"moltencanal","class":"iwex.BlockMoltenCanalStart","entityClass":"iwex.BlockEntityMoltenCanalStart","blockmaterial":"Ceramic","sounds":{"walk":"game:walk/stone","place":"game:block/ceramicplace","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":1,"creativeinventory":{"general":["*-start-*-s"],"iwex":["*-start-*-s"]},"attributes":{"fillHeight":1,"fillStart":14,"fillQuadsByLevel":[{"x1":5,"z1":5,"x2":11,"z2":11},{"x1":7,"z1":11,"x2":9,"z2":16}],"handbook":{"groupBy":["moltencanal-start-*"]}},"textures":{"granite1":{"base":"game:block/clay/brick/four/running/cream1","overlays":["game:block/clay/brick/four/running/{brick}1"]}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["start"]},{"code":"brick","states":["fire","black","brown","cream","gray","orange","red","tan"]},{"code":"orientation","states":["n","w","s","e"]}],"shapebytype":{"*-start-*-s":{"base":"iwex:molten/canal/start"},"*-start-*-w":{"base":"iwex:molten/canal/start","rotateY":270},"*-start-*-n":{"base":"iwex:molten/canal/start","rotateY":180},"*-start-*-e":{"base":"iwex:molten/canal/start","rotateY":90}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string StartCobble =
    """{"code":"moltencanal","class":"iwex.BlockMoltenCanalStart","entityClass":"iwex.BlockEntityMoltenCanalStart","blockmaterial":"Stone","sounds":{"walk":"game:walk/stone","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":1,"creativeinventory":{"general":["*-start-*-s"],"iwex":["*-start-*-s"]},"attributes":{"fillHeight":1,"fillStart":14,"fillQuadsByLevel":[{"x1":5,"z1":5,"x2":11,"z2":11},{"x1":7,"z1":11,"x2":9,"z2":16}],"handbook":{"groupBy":["moltencanal-start-*"]}},"textures":{"granite1":{"base":"game:block/stone/cobblestone/{rock}1"}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["start"]},{"code":"rock","loadFromProperties":"block/rockwithdeposit"},{"code":"orientation","states":["n","w","s","e"]}],"skipVariants":["*-halite-*","*-scoria-*","*-tuff-*","*-travertine-*"],"shapebytype":{"*-start-*-s":{"base":"iwex:molten/canal/start"},"*-start-*-w":{"base":"iwex:molten/canal/start","rotateY":270},"*-start-*-n":{"base":"iwex:molten/canal/start","rotateY":180},"*-start-*-e":{"base":"iwex:molten/canal/start","rotateY":90}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  [Theory]
  [InlineData("blocktypes/molten/canalbrick/start.json", StartBrick)]
  [InlineData("blocktypes/molten/canalcobblestone/start.json", StartCobble)]
  public void Start_def_reproduces_the_migrated_json(string path, string golden)
  {
    ExBlockDef def = BlockMoltenCanalStart
      .Definitions("iwex")
      .Single(d => d.Location.Path == path);

    Assert.True(
      DefinitionParity.Equal(JObject.Parse(golden), def.ToJson(), out string normalized),
      $"start def at {path} diverged from the migrated JSON:\n{normalized}"
    );
  }

  #endregion

  #region Tap

  private const string Tap =
    """{"code":"moltencanal","class":"iwex.BlockMoltenCanalTap","entityClass":"iwex.BlockEntityMoltenCanalTap","blockmaterial":"Stone","sounds":{"walk":"game:walk/stone","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":1,"creativeinventory":{"general":["*-tap-s"],"iwex":["*-tap-s"]},"attributes":{"fillHeight":1,"fillStart":14,"fillQuadsByLevel":[{"x1":7,"z1":0,"x2":9,"z2":5}],"handbook":{"groupBy":["moltencanal-tap-*"]}},"textures":{"burned":{"base":"game:block/clay/vessel/sides/burned"},"steel3":{"base":"game:block/metal/riveted/steel3"},"iron3":{"base":"game:block/metal/sheet-plain/iron3"},"steel32":{"base":"game:block/metal/sheet-plain/steel3"}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["tap"]},{"code":"orientation","states":["n","w","s","e"]}],"shapebytype":{"*-tap-n":{"base":"iwex:molten/canal/tap"},"*-tap-w":{"base":"iwex:molten/canal/tap","rotateY":90},"*-tap-s":{"base":"iwex:molten/canal/tap","rotateY":180},"*-tap-e":{"base":"iwex:molten/canal/tap","rotateY":270}},"collisionboxes":[{"x1":0.0625,"y1":0.0625,"z1":0,"x2":0.9375,"y2":0.9375,"z2":0.9375}],"selectionboxes":[{"x1":0.0625,"y1":0.0625,"z1":0,"x2":0.9375,"y2":0.9375,"z2":0.9375}],"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  [Fact]
  public void Tap_def_reproduces_the_migrated_json()
  {
    ExBlockDef def = BlockMoltenCanalTap
      .Definitions("iwex")
      .Single(d => d.Location.Path == "blocktypes/molten/tap.json");

    Assert.True(
      DefinitionParity.Equal(JObject.Parse(Tap), def.ToJson(), out string normalized),
      $"tap def diverged from the migrated JSON:\n{normalized}"
    );
  }

  #endregion

  #region Mold pedestal

  private const string PedestalBrick =
    """{"code":"moltencanal","class":"iwex.BlockMoltenCanalMoldPedestal","entityClass":"iwex.BlockEntityMoltenCanalMoldPedestal","blockmaterial":"Ceramic","sounds":{"walk":"game:walk/stone","place":"game:block/ceramicplace","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":1,"creativeinventory":{"general":["*-moldpedestal-*-s"],"iwex":["*-moldpedestal-*-s"]},"attributes":{"fillStart":14,"fillHeight":1,"fillQuadsByLevel":[{"x1":7,"z1":0,"x2":9,"z2":5}],"moldFillStart":12,"moldFillHeight":1,"moldFillQuadsByLevel":[{"x1":2,"z1":2,"x2":14,"z2":14}],"handbook":{"groupBy":["moltencanal-moldpedestal-*"]}},"textures":{"granite1":{"base":"game:block/clay/brick/four/running/cream1","overlays":["game:block/clay/brick/four/running/{brick}1"]}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["moldpedestal"]},{"code":"brick","states":["fire","black","brown","cream","gray","orange","red","tan"]},{"code":"orientation","states":["n","w","s","e"]}],"shapebytype":{"*-moldpedestal-*-n":{"base":"iwex:molten/canal/moldpedestal"},"*-moldpedestal-*-w":{"base":"iwex:molten/canal/moldpedestal","rotateY":90},"*-moldpedestal-*-s":{"base":"iwex:molten/canal/moldpedestal","rotateY":180},"*-moldpedestal-*-e":{"base":"iwex:molten/canal/moldpedestal","rotateY":270}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string PedestalCobble =
    """{"code":"moltencanal","class":"iwex.BlockMoltenCanalMoldPedestal","entityClass":"iwex.BlockEntityMoltenCanalMoldPedestal","blockmaterial":"Stone","sounds":{"walk":"game:walk/stone","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":1,"creativeinventory":{"general":["*-moldpedestal-*-s"],"iwex":["*-moldpedestal-*-s"]},"attributes":{"fillStart":14,"fillHeight":1,"fillQuadsByLevel":[{"x1":7,"z1":0,"x2":9,"z2":5}],"moldFillStart":12,"moldFillHeight":1,"moldFillQuadsByLevel":[{"x1":2,"z1":2,"x2":14,"z2":14}],"handbook":{"groupBy":["moltencanal-moldpedestal-*"]}},"textures":{"granite1":{"base":"game:block/stone/cobblestone/{rock}1"}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["moldpedestal"]},{"code":"rock","loadFromProperties":"block/rockwithdeposit"},{"code":"orientation","states":["n","w","s","e"]}],"skipVariants":["*-halite-*","*-scoria-*","*-tuff-*","*-travertine-*"],"shapebytype":{"*-moldpedestal-*-n":{"base":"iwex:molten/canal/moldpedestal"},"*-moldpedestal-*-w":{"base":"iwex:molten/canal/moldpedestal","rotateY":90},"*-moldpedestal-*-s":{"base":"iwex:molten/canal/moldpedestal","rotateY":180},"*-moldpedestal-*-e":{"base":"iwex:molten/canal/moldpedestal","rotateY":270}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  [Theory]
  [InlineData("blocktypes/molten/canalbrick/moldpedestal.json", PedestalBrick)]
  [InlineData("blocktypes/molten/canalcobblestone/moldpedestal.json", PedestalCobble)]
  public void MoldPedestal_def_reproduces_the_migrated_json(string path, string golden)
  {
    ExBlockDef def = BlockMoltenCanalMoldPedestal
      .Definitions("iwex")
      .Single(d => d.Location.Path == path);

    Assert.True(
      DefinitionParity.Equal(JObject.Parse(golden), def.ToJson(), out string normalized),
      $"mold pedestal def at {path} diverged from the migrated JSON:\n{normalized}"
    );
  }

  #endregion

  #region Barrel

  // The source barrel.json used the camelCase key "maxStackSize"; the builder (and every other migrated
  // block) emits "maxstacksize". The game binds blocktype keys case-insensitively, so the two are the same
  // block - the golden uses the builder's spelling so DeepEquals (case-sensitive) matches.
  private const string Barrel =
    """{"code":"moltenbarrel","class":"iwex.BlockMoltenBarrel","entityClass":"iwex.BlockEntityMoltenBarrel","maxstacksize":1,"storageFlags":2,"blockmaterial":"Metal","sounds":{"place":"game:block/anvil","break":"game:block/anvil","hit":"game:block/anvil","walk":"game:walk/stone"},"creativeinventory":{"general":["*"],"iwex":["*"]},"heldTpIdleAnimation":"holdbothhandslarge","attributes":{"maxUnits":800,"fillHeight":8,"fillStart":2,"fillQuadsByLevel":[{"x1":4,"z1":4,"x2":12,"z2":12}]},"behaviors":[{"name":"Lockable"},{"name":"UnstableFalling"}],"shape":{"base":"iwex:molten/barrel"},"sidesolid":{"all":false},"sideopaque":{"all":false},"tpHandTransform":{"translation":{"x":-0.8,"y":-1,"z":-0.55},"rotation":{"x":20,"y":14,"z":-90},"scale":0.75}}""";

  [Fact]
  public void Barrel_def_reproduces_the_migrated_json()
  {
    ExBlockDef def = BlockMoltenBarrel
      .Definitions("iwex")
      .Single(d => d.Location.Path == "blocktypes/molten/barrel.json");

    Assert.True(
      DefinitionParity.Equal(JObject.Parse(Barrel), def.ToJson(), out string normalized),
      $"barrel def diverged from the migrated JSON:\n{normalized}"
    );
  }

  #endregion

  #region Coverage

  [Fact]
  public void Definitions_cover_every_migrated_endpoint_blocktype()
  {
    Assert.Equal(2, BlockMoltenCanalStart.Definitions("iwex").Count());
    Assert.Single(BlockMoltenCanalTap.Definitions("iwex"));
    Assert.Equal(2, BlockMoltenCanalMoldPedestal.Definitions("iwex").Count());
    Assert.Single(BlockMoltenBarrel.Definitions("iwex"));
  }

  #endregion
}
