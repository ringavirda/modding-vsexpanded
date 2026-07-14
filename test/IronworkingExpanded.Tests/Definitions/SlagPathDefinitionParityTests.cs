using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronworkingExpanded.SlagPath;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Parity oracle for the slag-path decorative family authored code-first on the dedicated
/// <see cref="SlagPathDefinitions"/> provider (all three use vanilla classes, so there is no mod block class to
/// host the def). These are the densest <c>*ByType</c> blocktypes in the whole migration: per-variant resistance,
/// break/hit sounds, behaviours, liquid barriers, side-solid flags and collision boxes, plus the 11-pebble atlas
/// alternates. Goldens are the deleted JSON, minified.
/// </summary>
public class SlagPathDefinitionParityTests
{
  private const string PathGolden =
    """{"code":"slagpath","behaviors":[{"name":"Lockable"}],"behaviorsByType":{"*-snow":[{"name":"BreakSnowFirst"}]},"variantgroups":[{"code":"cover","states":["free","snow"]}],"attributes":{"reinforcable":true,"mapColorCode":"road","liquidBarrierOnSides":[1.0,1.0,1.0,1.0,1.0,1.0],"inContainerTexture":{"base":"game:block/stone/gravel/phyllite"}},"shape":{"base":"game:block/basic/cube-lowered-{cover}"},"textures":{"all":{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble1"],"alternates":[{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble2"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble3"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble4"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble5"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble6"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble7"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble8"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble9"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble10"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble11"]}]}},"resistanceByType":{"*-snow":0.2,"*-free":2.4},"walkspeedmultiplier":1.3,"creativeinventory":{"general":["*-free"],"decorative":["*-free"],"iwex":["*-free"]},"blockmaterial":"Gravel","replaceable":900,"lightAbsorption":99,"faceCullMode":"FlushExceptTop","sideopaque":{"all":false,"down":true},"sidesolid":{"all":true,"up":false},"selectionbox":{"x1":0,"y1":0,"z1":0,"x2":1,"y2":0.9375,"z2":1},"collisionbox":{"x1":0,"y1":0,"z1":0,"x2":1,"y2":0.9375,"z2":1},"heldTpIdleAnimation":"holdbothhandslarge","heldRightReadyAnimation":"heldblockready","heldTpUseAnimation":"twohandplaceblock","tpHandTransform":{"translation":{"x":-1.23,"y":-0.91,"z":-0.8},"rotation":{"x":-2,"y":25,"z":-78},"scale":0.4},"drops":[{"type":"block","code":"slagpath-free"}],"sounds":{"place":"game:block/gravel","breakByType":{"*-snow":"game:block/snow","*-free":"game:block/gravel"},"hitByType":{"*-snow":"game:block/snow","*-free":"game:block/gravel"},"walk":"game:walk/gravel"}}""";

  private const string SlabGolden =
    """{"code":"slagpathslab","behaviors":[{"name":"Lockable"}],"behaviorsByType":{"*-snow":[{"name":"BreakSnowFirst"}]},"variantgroups":[{"code":"cover","states":["free","snow"]}],"attributes":{"reinforcable":true,"mapColorCode":"road","liquidBarrierOnSides":[0.5,0.5,0.5,0.5],"inContainerTexture":{"base":"game:block/stone/gravel/phyllite"}},"shape":{"base":"iwex:legacy/basic/slab/stonepath-slab-{cover}"},"textures":{"normal1":{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble1"],"alternates":[{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble2"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble3"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble4"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble5"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble6"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble7"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble8"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble9"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble10"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble11"]}]}},"resistanceByType":{"*-snow":0.2,"*-free":2.4},"walkspeedmultiplier":1.3,"creativeinventory":{"general":["*-free"],"decorative":["*-free"],"iwex":["*-free"]},"blockmaterial":"Gravel","replaceable":900,"lightAbsorption":99,"faceCullMode":"FlushExceptTop","sideopaque":{"all":false,"down":true},"sidesolid":{"all":false,"down":true},"selectionbox":{"x1":0,"y1":0,"z1":0,"x2":1,"y2":0.4375,"z2":1},"collisionbox":{"x1":0,"y1":0,"z1":0,"x2":1,"y2":0.4375,"z2":1},"heldTpIdleAnimation":"holdbothhandslarge","heldRightReadyAnimation":"heldblockready","heldTpUseAnimation":"twohandplaceblock","tpHandTransform":{"translation":{"x":-1.49,"y":-0.22,"z":-0.7},"rotation":{"x":6,"y":16,"z":98},"origin":{"x":0.5,"y":0.25,"z":0.5},"scale":0.4},"drops":[{"type":"block","code":"slagpathslab-free"}],"sounds":{"place":"game:block/gravel","breakByType":{"*-snow":"game:block/snow","*-free":"game:block/gravel"},"hitByType":{"*-snow":"game:block/snow","*-free":"game:block/gravel"},"walk":"game:walk/gravel"}}""";

  private const string StairsGolden =
    """{"code":"slagpathstairs","class":"BlockStairs","behaviors":[{"name":"WrenchOrientable","properties":{"baseCode":"slagpathstairs-up-*-{cover}"}}],"behaviorsByType":{"*-snow":[{"name":"BreakSnowFirst"}]},"variantgroups":[{"code":"updown","states":["up"]},{"loadFromProperties":"game:abstract/horizontalorientation"},{"code":"cover","states":["free","snow"]}],"attributes":{"reinforcable":true,"mapColorCode":"road","noDownVariant":true,"liquidBarrierOnSidesByType":{"*-up-north-*":[1.0,0.5,0.5,0.5],"*-up-south-*":[0.5,0.5,1.0,0.5],"*-up-west-*":[0.5,0.5,0.5,1.0],"*-up-east-*":[0.5,1.0,0.5,0.5]},"inContainerTexture":{"base":"game:block/stone/gravel/phyllite"}},"shapebytype":{"*-up-north-free":{"base":"iwex:legacy/basic/stairs/stonepath-stairs-free","rotateY":0},"*-up-west-free":{"base":"iwex:legacy/basic/stairs/stonepath-stairs-free","rotateY":90},"*-up-south-free":{"base":"iwex:legacy/basic/stairs/stonepath-stairs-free","rotateY":180},"*-up-east-free":{"base":"iwex:legacy/basic/stairs/stonepath-stairs-free","rotateY":270},"*-up-north-snow":{"base":"iwex:legacy/basic/stairs/stonepath-stairs-snow","rotateY":0},"*-up-west-snow":{"base":"iwex:legacy/basic/stairs/stonepath-stairs-snow","rotateY":90},"*-up-south-snow":{"base":"iwex:legacy/basic/stairs/stonepath-stairs-snow","rotateY":180},"*-up-east-snow":{"base":"iwex:legacy/basic/stairs/stonepath-stairs-snow","rotateY":270}},"textures":{"normal1":{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble1"],"alternates":[{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble2"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble3"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble4"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble5"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble6"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble7"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble8"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble9"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble10"]},{"base":"game:block/stone/gravel/phyllite","overlays":["game:block/overlay/pebble11"]}]}},"resistanceByType":{"*-snow":0.2,"*-free":2.4},"walkspeedmultiplier":1.2,"creativeinventory":{"general":["*-up-north-free"],"decorative":["*-up-north-free"],"iwex":["*-up-north-free"]},"blockmaterial":"Gravel","replaceable":900,"lightAbsorption":99,"faceCullMode":"NeverCull","emitSideAo":{"all":true},"sideopaque":{"all":false,"down":true},"sidesolidByType":{"*-up-north-*":{"all":false,"down":true,"north":true},"*-up-west-*":{"all":false,"down":true,"west":true},"*-up-south-*":{"all":false,"down":true,"south":true},"*-up-east-*":{"all":false,"down":true,"east":true}},"collisionSelectionBoxesByType":{"*-up-*":[{"x1":0,"y1":0,"z1":0,"x2":1,"y2":0.4375,"z2":1},{"x1":0,"y1":0.4375,"z1":0.5,"x2":1,"y2":0.9375,"z2":1,"rotateYByType":{"*-north-*":180,"*-east-*":90,"*-south-*":0,"*-west-*":270}}]},"heldTpIdleAnimation":"holdbothhandslarge","heldRightReadyAnimation":"heldblockready","heldTpUseAnimation":"twohandplaceblock","tpHandTransform":{"translation":{"x":-1.23,"y":-0.91,"z":-0.8},"rotation":{"x":-2,"y":25,"z":-78},"scale":0.4},"drops":[{"type":"block","code":"slagpathstairs-up-north-free"}],"sounds":{"place":"game:block/gravel","breakByType":{"*-snow":"game:block/snow","*-free":"game:block/gravel"},"hitByType":{"*-snow":"game:block/snow","*-free":"game:block/gravel"},"walk":"game:walk/gravel"}}""";

  private static ExBlockDef Def(string path) =>
    SlagPathDefinitions.Definitions("iwex").Single(d => d.Location.Path == path);

  [Theory]
  [InlineData("blocktypes/slagpath.json", PathGolden)]
  [InlineData("blocktypes/slagpathslab.json", SlabGolden)]
  [InlineData("blocktypes/slagpathstairs.json", StairsGolden)]
  public void Def_reproduces_the_migrated_json(string path, string golden)
  {
    ExBlockDef def = Def(path);
    Assert.True(
      DefinitionParity.Equal(JObject.Parse(golden), def.ToJson(), out string normalized),
      $"slag-path def at {path} diverged from the migrated JSON:\n{normalized}"
    );
  }

  [Fact]
  public void Provider_yields_the_three_slag_defs_under_iwex()
  {
    var defs = SlagPathDefinitions.Definitions("iwex").ToList();
    Assert.Equal(3, defs.Count);
    Assert.All(defs, d => Assert.Equal("iwex", d.Location.Domain));
  }

  [Fact]
  public void All_three_slag_blocks_are_discovered_and_registered_under_iwex()
  {
    ExDefinitions.Clear();
    ExDefinitions.DiscoverAndRegister("iwex", typeof(SlagPathDefinitions).Assembly);
    Assert.Equal(
      3,
      ExDefinitions.Blocks.Count(d =>
        d.Domain == "iwex" && d.Code.StartsWith("slagpath")
      )
    );
  }
}
