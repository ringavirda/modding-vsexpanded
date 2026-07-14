using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using PipesAndPowerExpanded.BlockNetworkPipe.Blocks;
using Xunit;

namespace PipesAndPowerExpanded.Tests;

/// <summary>
/// Parity oracle for ppex's code-first pipe blocktypes: each authored <see cref="ExBlockDef"/> must
/// reproduce the hand-written <c>blocktypes/pipes/*.json</c> it replaced (semantically), so the injected
/// block expands to the identical variant list and no saved world sees a changed block. Goldens are
/// kept inline (the source JSON was deleted at migration). Numbers are compared type-agnostically
/// (0 == 0.0), matching how the game reads the JSON.
/// </summary>
public class PipeDefinitionParityTests
{
  // Verbatim copy of the former assets/ppex/blocktypes/pipes/straight.json.
  private const string StraightGolden =
    """
    {
      "code": "pipe",
      "class": "ppex.BlockPipe",
      "entityClass": "ppex.BlockEntityPipe",
      "blockmaterial": "Metal",
      "sounds": {
        "place": "game:block/anvil",
        "break": "game:block/anvil",
        "hit": "game:block/anvil",
        "walk": "game:walk/stone"
      },
      "maxstacksize": 16,
      "creativeinventory": {
        "general": ["*-straight-ns-*"],
        "ppex": ["*-straight-ns-*"]
      },
      "attributes": {
        "handbook": {
          "groupBy": ["pipe-straight-*", "pipe-bend-*", "pipe-tjunction-*", "pipe-xjunction-*"]
        }
      },
      "behaviors": [{ "name": "Lockable" }],
      "variantgroups": [
        { "code": "type", "states": ["straight"] },
        { "code": "orientation", "states": ["ns", "we", "ud"] },
        { "code": "material", "states": ["iron", "steel"] }
      ],
      "shapebytype": {
        "*-straight-ns-*": { "base": "ppex:pipes/straight" },
        "*-straight-we-*": { "base": "ppex:pipes/straight", "rotateY": 90 },
        "*-straight-ud-*": { "base": "ppex:pipes/straight", "rotateX": 90 }
      },
      "texturesByType": {
        "*-iron": { "iron4": { "base": "game:block/metal/sheet-plain/iron4" } },
        "*-steel": { "iron4": { "base": "game:block/metal/sheet-plain/steel4" } }
      },
      "collisionboxes": [
        { "x1": 0.3125, "y1": 0.3125, "z1": 0, "x2": 0.6875, "y2": 0.6875, "z2": 1 }
      ],
      "selectionboxes": [
        { "x1": 0.3125, "y1": 0.3125, "z1": 0, "x2": 0.6875, "y2": 0.6875, "z2": 1 }
      ],
      "renderpass": "OpaqueNoCull",
      "faceCullMode": "NeverCull",
      "lightAbsorption": 0,
      "sidesolid": { "all": false },
      "sideopaque": { "all": false }
    }
    """;

  // Verbatim copy of the former assets/ppex/blocktypes/pipes/bend.json.
  private const string BendGolden =
    """
    {
      "code": "pipe", "class": "ppex.BlockPipe", "entityClass": "ppex.BlockEntityPipe",
      "blockmaterial": "Metal",
      "sounds": { "place": "game:block/anvil", "break": "game:block/anvil", "hit": "game:block/anvil", "walk": "game:walk/stone" },
      "maxstacksize": 8,
      "creativeinventory": { "general": ["*-bend-nw-*"], "ppex": ["*-bend-nw-*"] },
      "attributes": { "handbook": { "groupBy": ["pipe-straight-*", "pipe-bend-*", "pipe-tjunction-*", "pipe-xjunction-*"] } },
      "behaviors": [{ "name": "Lockable" }],
      "variantgroups": [
        { "code": "type", "states": ["bend"] },
        { "code": "orientation", "states": ["nw", "se", "en", "ws", "un", "us", "uw", "ue", "dn", "ds", "dw", "de"] },
        { "code": "material", "states": ["iron", "steel"] }
      ],
      "shapebytype": {
        "*-bend-nw-*": { "base": "ppex:pipes/bend" },
        "*-bend-en-*": { "base": "ppex:pipes/bend", "rotateY": 270 },
        "*-bend-se-*": { "base": "ppex:pipes/bend", "rotateY": 180 },
        "*-bend-ws-*": { "base": "ppex:pipes/bend", "rotateY": 90 },
        "*-bend-dn-*": { "base": "ppex:pipes/bend", "rotateZ": 90 },
        "*-bend-de-*": { "base": "ppex:pipes/bend", "rotateZ": 90, "rotateY": 270 },
        "*-bend-ds-*": { "base": "ppex:pipes/bend", "rotateZ": 90, "rotateY": 180 },
        "*-bend-dw-*": { "base": "ppex:pipes/bend", "rotateZ": 90, "rotateY": 90 },
        "*-bend-un-*": { "base": "ppex:pipes/bend", "rotateZ": 270 },
        "*-bend-ue-*": { "base": "ppex:pipes/bend", "rotateZ": 270, "rotateY": 270 },
        "*-bend-us-*": { "base": "ppex:pipes/bend", "rotateZ": 270, "rotateY": 180 },
        "*-bend-uw-*": { "base": "ppex:pipes/bend", "rotateZ": 270, "rotateY": 90 }
      },
      "texturesByType": {
        "*-iron": { "iron4": { "base": "game:block/metal/sheet-plain/iron4" } },
        "*-steel": { "iron4": { "base": "game:block/metal/sheet-plain/steel4" } }
      },
      "collisionboxes": [
        { "x1": 0.3125, "y1": 0.3125, "z1": 0.0, "x2": 0.6875, "y2": 0.6875, "z2": 0.6875 },
        { "x1": 0.0, "y1": 0.3125, "z1": 0.3125, "x2": 0.6875, "y2": 0.6875, "z2": 0.6875 }
      ],
      "selectionboxes": [
        { "x1": 0.3125, "y1": 0.3125, "z1": 0.0, "x2": 0.6875, "y2": 0.6875, "z2": 0.6875 },
        { "x1": 0.0, "y1": 0.3125, "z1": 0.3125, "x2": 0.6875, "y2": 0.6875, "z2": 0.6875 }
      ],
      "renderpass": "OpaqueNoCull", "faceCullMode": "NeverCull", "lightAbsorption": 0,
      "sidesolid": { "all": false }, "sideopaque": { "all": false }
    }
    """;

  // Verbatim copy of the former assets/ppex/blocktypes/pipes/tjunction.json.
  private const string TJunctionGolden =
    """
    {
      "code": "pipe", "class": "ppex.BlockPipe", "entityClass": "ppex.BlockEntityPipe",
      "blockmaterial": "Metal",
      "sounds": { "place": "game:block/anvil", "break": "game:block/anvil", "hit": "game:block/anvil", "walk": "game:walk/stone" },
      "maxstacksize": 8,
      "creativeinventory": { "general": ["*-tjunction-uns-*"], "ppex": ["*-tjunction-uns-*"] },
      "attributes": { "handbook": { "groupBy": ["pipe-straight-*", "pipe-bend-*", "pipe-tjunction-*", "pipe-xjunction-*"] } },
      "behaviors": [{ "name": "Lockable" }],
      "variantgroups": [
        { "code": "type", "states": ["tjunction"] },
        { "code": "orientation", "states": ["uns", "uwe", "dns", "dwe", "nes", "esw", "swn", "wne", "dnu", "deu", "dsu", "dwu"] },
        { "code": "material", "states": ["iron", "steel"] }
      ],
      "shapebytype": {
        "*-tjunction-wne-*": { "base": "ppex:pipes/tjunction" },
        "*-tjunction-nes-*": { "base": "ppex:pipes/tjunction", "rotateY": 270 },
        "*-tjunction-esw-*": { "base": "ppex:pipes/tjunction", "rotateY": 180 },
        "*-tjunction-swn-*": { "base": "ppex:pipes/tjunction", "rotateY": 90 },
        "*-tjunction-uwe-*": { "base": "ppex:pipes/tjunction", "rotateX": 90 },
        "*-tjunction-uns-*": { "base": "ppex:pipes/tjunction", "rotateX": 90, "rotateZ": 90 },
        "*-tjunction-dwe-*": { "base": "ppex:pipes/tjunction", "rotateX": 270 },
        "*-tjunction-dns-*": { "base": "ppex:pipes/tjunction", "rotateZ": 90, "rotateX": 270 },
        "*-tjunction-dnu-*": { "base": "ppex:pipes/tjunction", "rotateZ": 90 },
        "*-tjunction-deu-*": { "base": "ppex:pipes/tjunction", "rotateZ": 90, "rotateY": 270 },
        "*-tjunction-dsu-*": { "base": "ppex:pipes/tjunction", "rotateZ": 90, "rotateY": 180 },
        "*-tjunction-dwu-*": { "base": "ppex:pipes/tjunction", "rotateZ": 90, "rotateY": 90 }
      },
      "texturesByType": {
        "*-iron": { "iron4": { "base": "game:block/metal/sheet-plain/iron4" } },
        "*-steel": { "iron4": { "base": "game:block/metal/sheet-plain/steel4" } }
      },
      "collisionboxes": [
        { "x1": 0, "y1": 0.3125, "z1": 0.3125, "x2": 1, "y2": 0.6875, "z2": 0.6875 },
        { "x1": 0.3125, "y1": 0.3125, "z1": 0, "x2": 0.6875, "y2": 0.6875, "z2": 0.3125 }
      ],
      "selectionboxes": [
        { "x1": 0, "y1": 0.3125, "z1": 0.3125, "x2": 1, "y2": 0.6875, "z2": 0.6875 },
        { "x1": 0.3125, "y1": 0.3125, "z1": 0, "x2": 0.6875, "y2": 0.6875, "z2": 0.3125 }
      ],
      "renderpass": "OpaqueNoCull", "faceCullMode": "NeverCull", "lightAbsorption": 0,
      "sidesolid": { "all": false }, "sideopaque": { "all": false }
    }
    """;

  // Verbatim copy of the former assets/ppex/blocktypes/pipes/xjunction.json.
  private const string XJunctionGolden =
    """
    {
      "code": "pipe", "class": "ppex.BlockPipe", "entityClass": "ppex.BlockEntityPipe",
      "blockmaterial": "Metal",
      "sounds": { "place": "game:block/anvil", "break": "game:block/anvil", "hit": "game:block/anvil", "walk": "game:walk/stone" },
      "maxstacksize": 8,
      "creativeinventory": { "general": ["*-xjunction-nswe-*"], "ppex": ["*-xjunction-nswe-*"] },
      "attributes": { "handbook": { "groupBy": ["pipe-straight-*", "pipe-bend-*", "pipe-tjunction-*", "pipe-xjunction-*"] } },
      "behaviors": [{ "name": "Lockable" }],
      "variantgroups": [
        { "code": "type", "states": ["xjunction"] },
        { "code": "orientation", "states": ["nswe", "nsud", "weud"] },
        { "code": "material", "states": ["iron", "steel"] }
      ],
      "shapebytype": {
        "*-xjunction-nswe-*": { "base": "ppex:pipes/xjunction" },
        "*-xjunction-nsud-*": { "base": "ppex:pipes/xjunction", "rotateZ": 90 },
        "*-xjunction-weud-*": { "base": "ppex:pipes/xjunction", "rotateZ": 90, "rotateY": 90 }
      },
      "texturesByType": {
        "*-iron": { "iron4": { "base": "game:block/metal/sheet-plain/iron4" } },
        "*-steel": { "iron4": { "base": "game:block/metal/sheet-plain/steel4" } }
      },
      "collisionboxes": [
        { "x1": 0, "y1": 0.3125, "z1": 0.3125, "x2": 1, "y2": 0.6875, "z2": 0.6875 },
        { "x1": 0.3125, "y1": 0.3125, "z1": 0, "x2": 0.6875, "y2": 0.6875, "z2": 1 }
      ],
      "selectionboxes": [
        { "x1": 0, "y1": 0.3125, "z1": 0.3125, "x2": 1, "y2": 0.6875, "z2": 0.6875 },
        { "x1": 0.3125, "y1": 0.3125, "z1": 0, "x2": 0.6875, "y2": 0.6875, "z2": 1 }
      ],
      "renderpass": "OpaqueNoCull", "faceCullMode": "NeverCull", "lightAbsorption": 0,
      "sidesolid": { "all": false }, "sideopaque": { "all": false }
    }
    """;

  // Verbatim (minified) copies of the deleted special pipe blocktype files.
  private const string OutletGolden =
    """{"code":"pipe","class":"ppex.BlockPipeOutlet","entityClass":"ppex.BlockEntityPipeOutlet","blockmaterial":"Ceramic","sounds":{"walk":"game:walk/stone","place":"game:block/ceramicplace","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":1,"creativeinventory":{"general":["*-outlet-*-n"],"ppex":["*-outlet-*-n"]},"attributes":{"handbook":{"groupBy":["pipe-outlet-*"]}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["outlet"]},{"code":"brick","states":["fire","black","brown","cream","gray","orange","red","tan"]},{"code":"orientation","states":["s","n","w","e","u","d"]}],"shapebytype":{"*-outlet-*-s":{"base":"ppex:pipes/outlet"},"*-outlet-*-n":{"base":"ppex:pipes/outlet","rotateY":180},"*-outlet-*-e":{"base":"ppex:pipes/outlet","rotateY":90},"*-outlet-*-w":{"base":"ppex:pipes/outlet","rotateY":-90},"*-outlet-*-u":{"base":"ppex:pipes/outlet","rotateX":-90},"*-outlet-*-d":{"base":"ppex:pipes/outlet","rotateX":90}},"texturesByType":{"*":{"front1":{"base":"game:block/clay/brick/four/running/cream1","overlays":["game:block/clay/brick/four/running/{brick}1"]}}},"sidesolid":{"all":true},"sideopaque":{"all":false}}""";

  private const string PassthroughGolden =
    """{"code":"pipe","class":"ppex.BlockPipePassthrough","entityClass":"ppex.BlockEntityPipePassthrough","blockmaterial":"Ceramic","sounds":{"walk":"game:walk/stone","place":"game:block/ceramicplace","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":1,"creativeinventory":{"general":["*-passthrough-*-ns"],"ppex":["*-passthrough-*-ns"]},"attributes":{"handbook":{"groupBy":["pipe-passthrough-*"]}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["passthrough"]},{"code":"brick","states":["fire","black","brown","cream","gray","orange","red","tan"]},{"code":"orientation","states":["ns","we","ud"]}],"shapebytype":{"*-passthrough-*-ns":{"base":"ppex:pipes/passthrough","rotateY":0},"*-passthrough-*-we":{"base":"ppex:pipes/passthrough","rotateY":90},"*-passthrough-*-ud":{"base":"ppex:pipes/passthrough","rotateX":90}},"texturesByType":{"*":{"front1":{"base":"game:block/clay/brick/four/running/cream1","overlays":["game:block/clay/brick/four/running/{brick}1"]}}},"renderpass":"OpaqueNoCull","faceCullMode":"NeverCull","lightAbsorption":0,"sidesolid":{"all":true},"sideopaque":{"all":false}}""";

  private const string PassthroughBendGolden =
    """{"code":"pipe","class":"ppex.BlockPipePassthrough","entityClass":"ppex.BlockEntityPipePassthrough","blockmaterial":"Ceramic","sounds":{"walk":"game:walk/stone","place":"game:block/ceramicplace","byTool":{"Pickaxe":{"hit":"game:block/rock-hit-pickaxe","break":"game:block/rock-break-pickaxe"}}},"maxstacksize":1,"creativeinventory":{"general":["*-passthroughbend-*-nw"],"ppex":["*-passthroughbend-*-nw"]},"attributes":{"handbook":{"groupBy":["pipe-passthroughbend-*"]}},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["passthroughbend"]},{"code":"brick","states":["fire","black","brown","cream","gray","orange","red","tan"]},{"code":"orientation","states":["nw","se","en","ws","un","us","uw","ue","dn","ds","dw","de"]}],"shapebytype":{"*-passthroughbend-*-nw":{"base":"ppex:pipes/passthroughbend"},"*-passthroughbend-*-en":{"base":"ppex:pipes/passthroughbend","rotateY":270},"*-passthroughbend-*-se":{"base":"ppex:pipes/passthroughbend","rotateY":180},"*-passthroughbend-*-ws":{"base":"ppex:pipes/passthroughbend","rotateY":90},"*-passthroughbend-*-dn":{"base":"ppex:pipes/passthroughbend","rotateZ":90},"*-passthroughbend-*-de":{"base":"ppex:pipes/passthroughbend","rotateZ":90,"rotateY":270},"*-passthroughbend-*-ds":{"base":"ppex:pipes/passthroughbend","rotateZ":90,"rotateY":180},"*-passthroughbend-*-dw":{"base":"ppex:pipes/passthroughbend","rotateZ":90,"rotateY":90},"*-passthroughbend-*-un":{"base":"ppex:pipes/passthroughbend","rotateZ":270},"*-passthroughbend-*-ue":{"base":"ppex:pipes/passthroughbend","rotateZ":270,"rotateY":270},"*-passthroughbend-*-us":{"base":"ppex:pipes/passthroughbend","rotateZ":270,"rotateY":180},"*-passthroughbend-*-uw":{"base":"ppex:pipes/passthroughbend","rotateZ":270,"rotateY":90}},"texturesByType":{"*":{"front1":{"base":"game:block/clay/brick/four/running/cream1","overlays":["game:block/clay/brick/four/running/{brick}1"]}}},"renderpass":"OpaqueNoCull","faceCullMode":"NeverCull","lightAbsorption":0,"sidesolid":{"all":true},"sideopaque":{"all":false}}""";

  private const string ValveGolden =
    """{"code":"pipe","class":"ppex.BlockValve","entityClass":"ppex.BlockEntityValve","entityBehaviors":[{"name":"Animatable"}],"blockmaterial":"Metal","sounds":{"place":"game:block/anvil","break":"game:block/anvil","hit":"game:block/anvil","walk":"game:walk/stone"},"maxstacksize":1,"creativeinventory":{"general":["*-valve-sn-*"],"ppex":["*-valve-sn-*"]},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["valve"]},{"code":"orientation","states":["ns","we","ud","sn","ew","du"]},{"code":"material","states":["iron","steel"]}],"shapebytype":{"*-valve-ns-*":{"base":"ppex:pipes/valve"},"*-valve-we-*":{"base":"ppex:pipes/valve","rotateY":90},"*-valve-ud-*":{"base":"ppex:pipes/valve","rotateX":90},"*-valve-sn-*":{"base":"ppex:pipes/valve","rotateY":180},"*-valve-ew-*":{"base":"ppex:pipes/valve","rotateY":-90},"*-valve-du-*":{"base":"ppex:pipes/valve","rotateX":90,"rotateY":180}},"texturesByType":{"*-iron":{"iron4":{"base":"game:block/metal/sheet-plain/iron4"}},"*-steel":{"iron4":{"base":"game:block/metal/sheet-plain/steel4"}}},"collisionboxes":[{"x1":0.3125,"y1":0.3125,"z1":0,"x2":0.6875,"y2":0.6875,"z2":1}],"selectionboxes":[{"x1":0.3125,"y1":0.3125,"z1":0,"x2":0.6875,"y2":0.6875,"z2":1}],"renderpass":"OpaqueNoCull","faceCullMode":"NeverCull","lightAbsorption":0,"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string PressureValveGolden =
    """{"code":"pipe","class":"ppex.BlockPressureValve","entityClass":"ppex.BlockEntityPressureValve","blockmaterial":"Metal","sounds":{"place":"game:block/anvil","break":"game:block/anvil","hit":"game:block/anvil","walk":"game:walk/stone"},"maxstacksize":1,"creativeinventory":{"general":["*-pressurevalve-sn-*"],"ppex":["*-pressurevalve-sn-*"]},"behaviors":[{"name":"Lockable"}],"variantgroups":[{"code":"type","states":["pressurevalve"]},{"code":"orientation","states":["ns","we","ud","sn","ew","du"]},{"code":"material","states":["iron","steel"]}],"shapebytype":{"*-pressurevalve-ns-*":{"base":"ppex:pipes/pressurevalve"},"*-pressurevalve-we-*":{"base":"ppex:pipes/pressurevalve","rotateY":90},"*-pressurevalve-ud-*":{"base":"ppex:pipes/pressurevalve","rotateX":90},"*-pressurevalve-sn-*":{"base":"ppex:pipes/pressurevalve","rotateY":180},"*-pressurevalve-ew-*":{"base":"ppex:pipes/pressurevalve","rotateY":-90},"*-pressurevalve-du-*":{"base":"ppex:pipes/pressurevalve","rotateX":-90}},"texturesByType":{"*-iron":{"iron4":{"base":"game:block/metal/sheet-plain/iron4"}},"*-steel":{"iron4":{"base":"game:block/metal/sheet-plain/steel4"}}},"collisionboxes":[{"x1":0.3125,"y1":0.3125,"z1":0,"x2":0.6875,"y2":0.6875,"z2":1}],"selectionboxes":[{"x1":0.3125,"y1":0.3125,"z1":0,"x2":0.6875,"y2":0.6875,"z2":1}],"renderpass":"OpaqueNoCull","faceCullMode":"NeverCull","lightAbsorption":0,"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string FluidIntakeGolden =
    """{"code":"pipe","class":"ppex.BlockFluidIntake","entityClass":"ppex.BlockEntityFluidIntake","blockmaterial":"Metal","variantgroups":[{"code":"type","states":["fluidintake"]},{"code":"orientation","states":["n","s","w","e"]}],"creativeinventory":{"general":["*-fluidintake-s"],"ppex":["*-fluidintake-s"]},"shapebytype":{"*-n":{"base":"ppex:pipes/fluidintake","rotateY":180},"*-e":{"base":"ppex:pipes/fluidintake","rotateY":90},"*-s":{"base":"ppex:pipes/fluidintake","rotateY":0},"*-w":{"base":"ppex:pipes/fluidintake","rotateY":270}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string SteamCondenserGolden =
    """{"code":"steamcondenser","class":"ppex.BlockSteamCondenser","entityClass":"ppex.BlockEntitySteamCondenser","blockmaterial":"Metal","sounds":{"place":"game:block/anvil","break":"game:block/anvil","hit":"game:block/anvil","walk":"game:walk/stone"},"maxstacksize":8,"behaviors":[{"name":"HorizontalOrientable"}],"variantgroups":[{"code":"side","loadFromProperties":"abstract/horizontalorientation"}],"creativeinventory":{"general":["*-north"],"ppex":["*-north"]},"shapebytype":{"*-north":{"base":"ppex:pipes/steamcondenser","rotateY":0},"*-east":{"base":"ppex:pipes/steamcondenser","rotateY":270},"*-south":{"base":"ppex:pipes/steamcondenser","rotateY":180},"*-west":{"base":"ppex:pipes/steamcondenser","rotateY":90}},"collisionboxes":[{"x1":0,"y1":0.3125,"z1":0.3125,"x2":1,"y2":0.6875,"z2":0.6875},{"x1":0.3125,"y1":0.3125,"z1":0,"x2":0.6875,"y2":0.6875,"z2":0.3125}],"selectionboxes":[{"x1":0,"y1":0.3125,"z1":0.3125,"x2":1,"y2":0.6875,"z2":0.6875},{"x1":0.3125,"y1":0.3125,"z1":0,"x2":0.6875,"y2":0.6875,"z2":0.3125}],"renderpass":"OpaqueNoCull","faceCullMode":"NeverCull","lightAbsorption":0,"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  // Every code-first pipe blocktype, gathered from the classes that author them.
  private static IEnumerable<ExBlockDef> AllDefs() =>
    BlockPipe
      .Definitions("ppex")
      .Concat(BlockPipeOutlet.Definitions("ppex"))
      .Concat(BlockPipePassthrough.Definitions("ppex"))
      .Concat(BlockValve.Definitions("ppex"))
      .Concat(BlockPressureValve.Definitions("ppex"))
      .Concat(BlockFluidIntake.Definitions("ppex"))
      .Concat(BlockSteamCondenser.Definitions("ppex"));

  private static ExBlockDef Def(string type) =>
    AllDefs().Single(d => d.Location.Path.EndsWith("/" + type + ".json"));

  [Theory]
  [InlineData("straight", nameof(StraightGolden))]
  [InlineData("bend", nameof(BendGolden))]
  [InlineData("tjunction", nameof(TJunctionGolden))]
  [InlineData("xjunction", nameof(XJunctionGolden))]
  [InlineData("outlet", nameof(OutletGolden))]
  [InlineData("passthrough", nameof(PassthroughGolden))]
  [InlineData("passthroughbend", nameof(PassthroughBendGolden))]
  [InlineData("valve", nameof(ValveGolden))]
  [InlineData("pressurevalve", nameof(PressureValveGolden))]
  [InlineData("fluidintake", nameof(FluidIntakeGolden))]
  [InlineData("steamcondenser", nameof(SteamCondenserGolden))]
  public void Pipe_def_reproduces_the_migrated_json_exactly(
    string type,
    string goldenName
  )
  {
    JObject expected = JObject.Parse(Golden(goldenName));
    JObject actual = Def(type).ToJson();

    Assert.True(
      SemanticEquals(expected, actual),
      $"{type} pipe code-first def diverged from the migrated JSON:\n{actual}"
    );
  }

  [Theory]
  [InlineData("straight")]
  [InlineData("bend")]
  [InlineData("outlet")]
  [InlineData("valve")]
  [InlineData("fluidintake")]
  [InlineData("steamcondenser")]
  public void Each_pipe_type_targets_its_own_asset_path(string type)
  {
    ExBlockDef def = Def(type);
    Assert.Equal("ppex", def.Location.Domain);
    Assert.Equal($"blocktypes/pipes/{type}.json", def.Location.Path);
  }

  [Fact]
  public void AllowedOrientations_is_derived_from_the_defs_variant_groups()
  {
    // The dedup: the runtime table now comes from the same variant states the block is generated with.
    var outletOrientations = ExDefinitions.OrientationMap(
      BlockPipeOutlet.Definitions("ppex")
    );
    Assert.Equal(["s", "n", "w", "e", "u", "d"], outletOrientations["outlet"]);
  }

  private static string Golden(string name) =>
    name switch
    {
      nameof(StraightGolden) => StraightGolden,
      nameof(BendGolden) => BendGolden,
      nameof(TJunctionGolden) => TJunctionGolden,
      nameof(XJunctionGolden) => XJunctionGolden,
      nameof(OutletGolden) => OutletGolden,
      nameof(PassthroughGolden) => PassthroughGolden,
      nameof(PassthroughBendGolden) => PassthroughBendGolden,
      nameof(ValveGolden) => ValveGolden,
      nameof(PressureValveGolden) => PressureValveGolden,
      nameof(FluidIntakeGolden) => FluidIntakeGolden,
      nameof(SteamCondenserGolden) => SteamCondenserGolden,
      _ => throw new System.ArgumentOutOfRangeException(nameof(name), name),
    };

  // DeepEquals but numeric-type-agnostic (a hand-written "0" vs the builder's 0f), matching how the
  // game parses blocktype coordinates.
  private static bool SemanticEquals(JToken a, JToken b) =>
    JToken.DeepEquals(Normalize(a), Normalize(b));

  private static JToken Normalize(JToken token)
  {
    switch (token)
    {
      case JObject obj:
        var no = new JObject();
        foreach (JProperty p in obj.Properties())
          no[p.Name] = Normalize(p.Value);
        return no;
      case JArray arr:
        var na = new JArray();
        foreach (JToken item in arr)
          na.Add(Normalize(item));
        return na;
      case JValue { Type: JTokenType.Integer or JTokenType.Float } v:
        return new JValue(v.Value<double>());
      default:
        return token.DeepClone();
    }
  }
}
