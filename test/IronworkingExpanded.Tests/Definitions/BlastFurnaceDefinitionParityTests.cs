using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.BlastFurnace.Blocks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Parity oracle for the blast-furnace fittings authored code-first: the bell + reinforced hoppers, the molten
/// tap, the tuyere (a BlockPipe node whose orientation table is now derived), and the solidified slag block.
/// Goldens are the deleted JSON, minified; comparison via the shared <see cref="DefinitionParity"/>. Two source
/// files used the case-insensitive key "entityclass"; the builder emits "entityClass" (the game binds blocktype
/// keys case-insensitively), so those goldens use the builder's spelling.
/// </summary>
public class BlastFurnaceDefinitionParityTests
{
  private const string HopperBell =
    """{"code":"hopperbell","class":"iwex.BlockHopperBell","entityClass":"iwex.BlockEntityHopperBell","behaviors":[{"name":"Lockable"}],"creativeinventory":{"general":["*"],"iwex":["*"]},"blockmaterial":"Metal","maxstacksize":1,"lightAbsorption":0,"shape":{"base":"iwex:blastfurnace/hopper-bell"},"sidesolid":{"all":false},"sideopaque":{"all":false},"resistance":1.75,"sounds":{"place":"game:block/anvil","break":"game:block/anvil","hit":"game:block/anvil","walk":"game:walk/stone"}}""";

  private const string Slag =
    """{"code":"slag","class":"iwex.BlockSlag","entityClass":"iwex.BlockEntitySlag","blockmaterial":"Stone","creativeinventory":{"general":["*"],"iwex":["*"]},"shape":{"base":"game:block/basic/cube"},"textures":{"all":{"base":"game:block/stone/gravel/phyllite"}},"resistance":3.0,"maxstacksize":64,"requiredMiningTier":2,"mineTool":"pickaxe","sounds":{"place":"game:block/stone","break":"game:block/stone","hit":"game:block/stone","walk":"game:walk/stone"},"combustibleProps":{"meltingPoint":720,"meltingDuration":30,"smeltedRatio":1,"smeltedStack":{"type":"item","code":"iwex:slag"}}}""";

  private const string Tap =
    """{"code":"blastfurnacetap","class":"iwex.BlockBlastFurnaceTap","entityClass":"iwex.BlockEntityBlastFurnaceTap","entityBehaviors":[{"name":"Animatable"}],"blockmaterial":"Ceramic","maxstacksize":1,"behaviors":[{"name":"HorizontalOrientable"}],"variantgroups":[{"code":"side","loadFromProperties":"abstract/horizontalorientation"}],"shapebytype":{"*-north":{"base":"iwex:blastfurnace/tap","rotateY":0},"*-east":{"base":"iwex:blastfurnace/tap","rotateY":270},"*-south":{"base":"iwex:blastfurnace/tap","rotateY":180},"*-west":{"base":"iwex:blastfurnace/tap","rotateY":90}},"creativeinventory":{"general":["*-south"],"iwex":["*-south"]},"replaceable":400,"resistance":3.5,"lightAbsorption":3,"sounds":{"walk":"walk/stone","place":"block/ceramicplace","byTool":{"Pickaxe":{"hit":"block/rock-hit-pickaxe","break":"block/rock-break-pickaxe"}}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string Tuyere =
    """{"code":"blastfurnace","class":"iwex.BlockTuyere","entityClass":"iwex.BlockEntityTuyere","blockmaterial":"Ceramic","maxstacksize":1,"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["tuyere"]},{"code":"orientation","states":["s","n","w","e"]}],"shapebytype":{"*-tuyere-s":{"base":"iwex:blastfurnace/tuyere","rotateY":0},"*-tuyere-e":{"base":"iwex:blastfurnace/tuyere","rotateY":90},"*-tuyere-n":{"base":"iwex:blastfurnace/tuyere","rotateY":180},"*-tuyere-w":{"base":"iwex:blastfurnace/tuyere","rotateY":-90}},"creativeinventory":{"general":["*-tuyere-s"],"iwex":["*-tuyere-s"]},"replaceable":400,"resistance":3.5,"lightAbsorption":3,"sounds":{"walk":"walk/stone","place":"block/ceramicplace","byTool":{"Pickaxe":{"hit":"block/rock-hit-pickaxe","break":"block/rock-break-pickaxe"}}},"sidesolid":{"all":true},"sideopaque":{"all":false}}""";

  private const string HopperReinforced =
    """{"code":"hopperreinforced","class":"iwex.BlockHopperReinforced","entityClass":"iwex.BlockEntityHopperReinforced","behaviors":[{"name":"Lockable"},{"name":"Container"}],"attributes":{"inventoryClassName":"hopperreinforced","pullFaces":[],"acceptFromFaces":["up"],"pushFaces":["down"],"quantitySlots":8,"openSound":{"path":"block/hopperopen","pitch":{"avg":1.0,"var":0.25}},"tumbleSound":{"path":"block/hoppertumble","pitch":{"avg":1.0,"var":0.25},"range":8,"volume":{"avg":0.5,"var":0.0}}},"creativeinventory":{"general":["*"],"iwex":["*"]},"blockmaterial":"Metal","maxstacksize":1,"lightAbsorption":0,"shape":{"base":"iwex:blastfurnace/hopper-reinforced"},"sidesolid":{"all":false},"sideopaque":{"all":false},"resistance":1.75,"sounds":{"place":"game:block/anvil","break":"game:block/anvil","hit":"game:block/anvil","walk":"game:walk/stone"}}""";

  // The blast-furnace fittings live on five separate classes; resolve the one def targeting this asset path.
  private static ExBlockDef Def(string path) =>
    BlockHopperBell
      .Definitions("iwex")
      .Concat(BlockSlag.Definitions("iwex"))
      .Concat(BlockBlastFurnaceTap.Definitions("iwex"))
      .Concat(BlockTuyere.Definitions("iwex"))
      .Concat(BlockHopperReinforced.Definitions("iwex"))
      .Single(d => d.Location.Path == path);

  [Theory]
  [InlineData("blocktypes/blastfurnace/hopperbell.json", HopperBell)]
  [InlineData("blocktypes/blastfurnace/slag.json", Slag)]
  [InlineData("blocktypes/blastfurnace/tap.json", Tap)]
  [InlineData("blocktypes/blastfurnace/tuyere.json", Tuyere)]
  [InlineData("blocktypes/blastfurnace/hopperreinforced.json", HopperReinforced)]
  public void Def_reproduces_the_migrated_json(string path, string golden)
  {
    ExBlockDef def = Def(path);
    Assert.True(
      DefinitionParity.Equal(JObject.Parse(golden), def.ToJson(), out string normalized),
      $"blast-furnace def at {path} diverged from the migrated JSON:\n{normalized}"
    );
  }

  [Fact]
  public void Tuyere_derives_its_orientation_table_from_the_def()
  {
    // The BlockPipe base derives AllowedOrientations from the tuyere's own def; the variant group order
    // [s,n,w,e] carries through and its first state "s" is the fallback (no hand-written tables).
    var orientations = ExDefinitions.OrientationMap(BlockTuyere.Definitions("iwex"));
    Assert.Equal(["s", "n", "w", "e"], orientations["tuyere"]);
  }

  // Minified copy of the former blastfurnace/door.json - the furnace anchor with the ~147-cell multiblock
  // (authored as nine ASCII cross-sections; compared as an unordered cell set), the vanilla Door
  // width/height/sound *ByType maps, and the gui/tp/ground hold-transform *ByType maps. The source used the
  // camelCase key "renderPass"; the builder (shared with the pipes) emits "renderpass" (VS binds blocktype keys
  // case-insensitively), so the golden uses the builder's spelling.
  private const string Door =
    """{"code":"blastfurnacedoor","class":"iwex.BlockBlastFurnaceDoor","entityClass":"iwex.BlockEntityBlastFurnace","behaviors":[{"name":"MultiblockStructure"},{"name":"Door"},{"name":"BlockEntityInteract"},{"name":"Lockable"}],"entityBehaviors":[{"name":"Door"}],"attributes":{"widthByType":{"*":1},"heightByType":{"*":2},"openSoundByType":{"*":"game:sounds/block/cokeovendoor-open"},"closeSoundByType":{"*":"game:sounds/block/metaldoor"},"easingSpeedByType":{"*":2},"multiblockStructure":{"blockNumbers":{"game:refractorybricks-good-tier3":1,"game:multiblock-monolithic-0-p1-0":2,"iwex:blastfurnacedoor*":3,"iwex:blastfurnacetap*":4,"iwex:blastfurnace-tuyere*":5,"ppex:pipe-outlet*":7,"iwex:hopperreinforced*":8,"iwex:hopperbell*":9,"@(air|coalpile)":10,"game:air":11},"offsets":[{"x":1,"y":-3,"z":0,"w":1},{"x":2,"y":-3,"z":1,"w":1},{"x":1,"y":-3,"z":1,"w":1},{"x":0,"y":-3,"z":1,"w":1},{"x":-1,"y":-3,"z":1,"w":1},{"x":-2,"y":-3,"z":1,"w":1},{"x":-3,"y":-3,"z":1,"w":1},{"x":2,"y":-3,"z":2,"w":1},{"x":1,"y":-3,"z":2,"w":1},{"x":0,"y":-3,"z":2,"w":1},{"x":-1,"y":-3,"z":2,"w":1},{"x":-2,"y":-3,"z":2,"w":1},{"x":-3,"y":-3,"z":2,"w":1},{"x":2,"y":-3,"z":3,"w":1},{"x":1,"y":-3,"z":3,"w":1},{"x":0,"y":-3,"z":3,"w":1},{"x":-1,"y":-3,"z":3,"w":1},{"x":-2,"y":-3,"z":3,"w":1},{"x":-3,"y":-3,"z":3,"w":1},{"x":1,"y":-3,"z":4,"w":1},{"x":1,"y":-2,"z":0,"w":1},{"x":2,"y":-2,"z":1,"w":1},{"x":1,"y":-2,"z":1,"w":1},{"x":0,"y":-2,"z":1,"w":5},{"x":-1,"y":-2,"z":1,"w":1},{"x":-3,"y":-2,"z":1,"w":1},{"x":-1,"y":-2,"z":2,"w":1},{"x":0,"y":-2,"z":2,"w":10},{"x":1,"y":-2,"z":2,"w":10},{"x":2,"y":-2,"z":2,"w":4},{"x":2,"y":-2,"z":3,"w":1},{"x":1,"y":-2,"z":3,"w":1},{"x":0,"y":-2,"z":3,"w":5},{"x":-1,"y":-2,"z":3,"w":1},{"x":-3,"y":-2,"z":3,"w":1},{"x":1,"y":-2,"z":4,"w":1},{"x":1,"y":-1,"z":0,"w":1},{"x":0,"y":-1,"z":0,"w":1},{"x":-1,"y":-1,"z":0,"w":1},{"x":2,"y":-1,"z":1,"w":1},{"x":1,"y":-1,"z":1,"w":10},{"x":0,"y":-1,"z":1,"w":10},{"x":-1,"y":-1,"z":1,"w":10},{"x":-2,"y":-1,"z":1,"w":1},{"x":-3,"y":-1,"z":1,"w":1},{"x":2,"y":-1,"z":2,"w":1},{"x":1,"y":-1,"z":2,"w":10},{"x":0,"y":-1,"z":2,"w":10},{"x":-1,"y":-1,"z":2,"w":10},{"x":-2,"y":-1,"z":2,"w":4},{"x":2,"y":-1,"z":3,"w":1},{"x":1,"y":-1,"z":3,"w":10},{"x":0,"y":-1,"z":3,"w":10},{"x":-1,"y":-1,"z":3,"w":10},{"x":-2,"y":-1,"z":3,"w":1},{"x":-3,"y":-1,"z":3,"w":1},{"x":1,"y":-1,"z":4,"w":1},{"x":0,"y":-1,"z":4,"w":1},{"x":-1,"y":-1,"z":4,"w":1},{"x":1,"y":0,"z":0,"w":1},{"x":0,"y":0,"z":0,"w":3},{"x":-1,"y":0,"z":0,"w":1},{"x":2,"y":0,"z":1,"w":1},{"x":1,"y":0,"z":1,"w":10},{"x":0,"y":0,"z":1,"w":10},{"x":-1,"y":0,"z":1,"w":10},{"x":-2,"y":0,"z":1,"w":1},{"x":-3,"y":0,"z":1,"w":1},{"x":2,"y":0,"z":2,"w":1},{"x":1,"y":0,"z":2,"w":10},{"x":0,"y":0,"z":2,"w":10},{"x":-1,"y":0,"z":2,"w":10},{"x":-2,"y":0,"z":2,"w":1},{"x":2,"y":0,"z":3,"w":1},{"x":1,"y":0,"z":3,"w":10},{"x":0,"y":0,"z":3,"w":10},{"x":-1,"y":0,"z":3,"w":10},{"x":-2,"y":0,"z":3,"w":1},{"x":-3,"y":0,"z":3,"w":1},{"x":1,"y":0,"z":4,"w":1},{"x":0,"y":0,"z":4,"w":1},{"x":-1,"y":0,"z":4,"w":1},{"x":1,"y":1,"z":0,"w":1},{"x":0,"y":1,"z":0,"w":2},{"x":-1,"y":1,"z":0,"w":1},{"x":2,"y":1,"z":1,"w":1},{"x":1,"y":1,"z":1,"w":10},{"x":0,"y":1,"z":1,"w":10},{"x":-1,"y":1,"z":1,"w":10},{"x":-2,"y":1,"z":1,"w":1},{"x":2,"y":1,"z":2,"w":1},{"x":1,"y":1,"z":2,"w":10},{"x":0,"y":1,"z":2,"w":10},{"x":-1,"y":1,"z":2,"w":10},{"x":-2,"y":1,"z":2,"w":1},{"x":2,"y":1,"z":3,"w":1},{"x":1,"y":1,"z":3,"w":10},{"x":0,"y":1,"z":3,"w":10},{"x":-1,"y":1,"z":3,"w":10},{"x":-2,"y":1,"z":3,"w":1},{"x":1,"y":1,"z":4,"w":1},{"x":0,"y":1,"z":4,"w":1},{"x":-1,"y":1,"z":4,"w":1},{"x":1,"y":2,"z":0,"w":1},{"x":0,"y":2,"z":0,"w":1},{"x":-1,"y":2,"z":0,"w":1},{"x":2,"y":2,"z":1,"w":1},{"x":1,"y":2,"z":1,"w":10},{"x":0,"y":2,"z":1,"w":10},{"x":-1,"y":2,"z":1,"w":10},{"x":-2,"y":2,"z":1,"w":1},{"x":2,"y":2,"z":2,"w":1},{"x":1,"y":2,"z":2,"w":10},{"x":0,"y":2,"z":2,"w":10},{"x":-1,"y":2,"z":2,"w":10},{"x":-2,"y":2,"z":2,"w":1},{"x":2,"y":2,"z":3,"w":1},{"x":1,"y":2,"z":3,"w":10},{"x":0,"y":2,"z":3,"w":10},{"x":-1,"y":2,"z":3,"w":10},{"x":-2,"y":2,"z":3,"w":1},{"x":1,"y":2,"z":4,"w":1},{"x":0,"y":2,"z":4,"w":1},{"x":-1,"y":2,"z":4,"w":1},{"x":-1,"y":3,"z":1,"w":1},{"x":0,"y":3,"z":1,"w":7},{"x":1,"y":3,"z":1,"w":1},{"x":-1,"y":3,"z":2,"w":1},{"x":0,"y":3,"z":2,"w":11},{"x":1,"y":3,"z":2,"w":1},{"x":-1,"y":3,"z":3,"w":1},{"x":0,"y":3,"z":3,"w":7},{"x":1,"y":3,"z":3,"w":1},{"x":1,"y":4,"z":1,"w":1},{"x":0,"y":4,"z":1,"w":1},{"x":-1,"y":4,"z":1,"w":1},{"x":1,"y":4,"z":2,"w":1},{"x":0,"y":4,"z":2,"w":9},{"x":-1,"y":4,"z":2,"w":1},{"x":1,"y":4,"z":3,"w":1},{"x":0,"y":4,"z":3,"w":1},{"x":-1,"y":4,"z":3,"w":1},{"x":0,"y":5,"z":1,"w":1},{"x":1,"y":5,"z":2,"w":1},{"x":0,"y":5,"z":2,"w":8},{"x":-1,"y":5,"z":2,"w":1},{"x":0,"y":5,"z":3,"w":1}]}},"creativeinventory":{"general":["*"],"iwex":["*"]},"shape":{"base":"iwex:blastfurnace/door"},"renderpass":"OpaqueNoCull","faceCullMode":"NeverCull","blockmaterial":"Ceramic","maxstacksize":1,"sideAo":{"all":false},"heldTpIdleAnimation":"holdunderarm","replaceable":500,"resistance":3.5,"lightAbsorption":0,"sidesolid":{"all":false},"sideopaque":{"all":false},"guiTransformByType":{"*":{"origin":{"x":0.49,"y":1,"z":0.8},"scale":0.73}},"tpHandTransformByType":{"*":{"translation":{"x":-0.74,"y":-1.22,"z":-1.3},"rotation":{"x":8,"y":11,"z":59},"origin":{"x":0.5,"y":1,"z":1},"scale":0.71}},"groundTransformByType":{"*":{"translation":{"x":0,"y":0,"z":0},"rotation":{"x":-90,"y":0,"z":0},"origin":{"x":0.5,"y":1,"z":0.85},"scale":3}},"selectionbox":{"x1":0,"y1":0,"z1":0.6875,"x2":1,"y2":1,"z2":0.9375},"collisionbox":{"x1":0,"y1":0,"z1":0.6875,"x2":1,"y2":1,"z2":0.9375},"sounds":{"place":"game:block/metaldoor-place","break":"game:block/metaldoor-place","hit":"game:block/metaldoor-place","walk":"game:walk/stone*"},"materialDensity":2000}""";

  [Fact]
  public void Door_def_reproduces_the_migrated_json()
  {
    ExBlockDef def = BlockBlastFurnaceDoor.Definitions("iwex").Single();
    Assert.True(
      DefinitionParity.Equal(JObject.Parse(Door), def.ToJson(), out string normalized),
      $"blast-furnace door def diverged from the migrated JSON:\n{normalized}"
    );
  }

  [Fact]
  public void Door_multiblock_covers_all_147_cells()
  {
    JObject def = BlockBlastFurnaceDoor.Definitions("iwex").Single().ToJson();
    var offsets = (JArray)def["attributes"]!["multiblockStructure"]!["offsets"]!;
    Assert.Equal(147, offsets.Count);
  }
}
