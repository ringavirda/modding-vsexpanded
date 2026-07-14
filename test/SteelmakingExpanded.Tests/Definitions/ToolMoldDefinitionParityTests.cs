using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using SteelmakingExpanded.Molds;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Parity oracle for the two casting-mold blocktypes now authored code-first on the dedicated
/// <see cref="ToolMoldDefinitions"/> provider (both molds use VANILLA classes, so there is no mod block class to
/// host the def). Goldens are the deleted JSON, minified. These are the densest <c>*ByType</c> blocks migrated so
/// far: fired carries a per-tooltype fill/drop <c>attributesByType</c> map; raw carries a per-colour beehive-kiln
/// firing <c>attributesByType</c> map plus a <c>combustiblePropsByType</c> smelting map — plus block-root
/// gui/tp/ground hold transforms whose shapes differ per mold.
/// </summary>
public class ToolMoldDefinitionParityTests
{
  // Minified copy of the former assets/smex/blocktypes/molds/toolmoldfired.json, with two deliberate deltas:
  // (1) the source's camelCase "maxStackSize" is written as the builder's "maxstacksize" (VS binds blocktype
  // keys case-insensitively); (2) the "smex" creative tab is ADDED to creativeinventory (the source listed only
  // general + construction) so the molds show in the mod's own tab - an intentional behaviour change, not parity.
  private const string FiredGolden =
    """{"code":"toolmold","class":"BlockToolMold","entityClass":"ToolMold","behaviors":[{"name":"Lockable"},{"name":"UnstableFalling"}],"entityBehaviors":[{"name":"TemperatureSensitive"}],"variantgroups":[{"code":"color","states":["blue","fire","black","brown","cream","earthyorange","gray","orange","red","tan"]},{"code":"materialtype","states":["fired"]},{"code":"tooltype","states":["plate","doubleingot","quadrod"]}],"shapebytype":{"*-plate":{"base":"iwex:molten/molds/plate","rotateY":90},"*-doubleingot":{"base":"iwex:molten/molds/doubleingot","rotateY":90},"*-quadrod":{"base":"iwex:molten/molds/quadrod","rotateY":90}},"textures":{"floor":{"base":"game:block/clay/hardened/{color}"},"other":{"base":"game:block/clay/hardened/{color}"}},"heldTpIdleAnimation":"holdbothhandslarge","attributes":{"reinforcable":true,"shatteredShape":{"base":"game:block/clay/mold/shattered-ingot"},"onTongTransform":{"translation":{"x":-1.7,"y":-1.3,"z":-0.57},"rotation":{"x":98,"y":16,"z":3},"scale":0.55},"handbook":{"groupBy":["toolmold-*-{materialtype}-{tooltype}"]},"onMetalTongTransform":{"translation":{"x":-2.1,"y":-0.19,"z":-1},"rotation":{"x":180,"y":7,"z":4},"origin":{"x":0.5,"y":0,"z":0.5},"scale":0.55}},"attributesByType":{"toolmold-*-fired-plate":{"requiredUnits":200,"fillHeight":1,"moldrackable":true,"onmoldrackTransform":{"rotation":{"z":90}},"fillQuadsByLevel":[{"x1":2,"z1":2,"x2":14,"z2":14}],"drop":{"type":"item","code":"game:metalplate-{metal}"}},"toolmold-*-fired-doubleingot":{"requiredUnits":200,"fillHeight":1,"moldrackable":true,"onmoldrackTransform":{"rotation":{"z":90}},"fillQuadsByLevel":[{"x1":2,"z1":2,"x2":14,"z2":14}],"drop":{"type":"item","code":"game:ingot-{metal}","quantity":2}},"toolmold-*-fired-quadrod":{"requiredUnits":200,"fillHeight":1,"moldrackable":true,"onmoldrackTransform":{"rotation":{"z":90}},"fillQuadsByLevel":[{"x1":2,"z1":2,"x2":14,"z2":14}],"drop":{"type":"item","code":"game:rod-{metal}","quantity":4}}},"blockmaterial":"Ceramic","creativeinventory":{"general":["*"],"construction":["*"],"smex":["*"]},"replaceable":700,"resistance":1.5,"maxstacksize":8,"lightAbsorption":0,"sounds":{"walk":"game:walk/stone"},"collisionbox":{"x1":0.0625,"y1":0,"z1":0.0625,"x2":0.9375,"y2":0.125,"z2":0.9375},"selectionbox":{"x1":0.0625,"y1":0,"z1":0.0625,"x2":0.9375,"y2":0.125,"z2":0.9375},"sideopaque":{"all":false},"sidesolid":{"all":false},"guiTransform":{"translation":{"x":0,"y":3,"z":0},"origin":{"x":0.5,"y":0.0625,"z":0.5},"scale":1.33},"tpHandTransform":{"translation":{"x":-1.6,"y":-1,"z":-0.5},"rotation":{"x":102,"y":-16,"z":-77},"scale":0.55},"groundTransform":{"translation":{"x":0,"y":0,"z":0},"rotation":{"x":0,"y":-45,"z":0},"origin":{"x":0.5,"y":0,"z":0.5},"scale":2.2}}""";

  // Minified copy of the former assets/smex/blocktypes/molds/toolmoldraw.json (same two deltas as fired:
  // maxStackSize->maxstacksize, and the added "smex" creative tab).
  private const string RawGolden =
    """{"code":"toolmold","class":"Block","behaviors":[{"name":"GroundStorable","properties":{"layout":"SingleCenter"}},{"name":"Unplaceable"},{"name":"RightClickPickup"}],"variantgroups":[{"code":"color","states":["blue","red","fire"]},{"code":"materialtype","states":["raw"]},{"code":"tooltype","states":["plate","doubleingot","quadrod"]}],"shapebytype":{"*-plate":{"base":"iwex:molten/molds/plate","rotateY":90},"*-doubleingot":{"base":"iwex:molten/molds/doubleingot","rotateY":90},"*-quadrod":{"base":"iwex:molten/molds/quadrod","rotateY":90}},"textures":{"all":{"base":"game:block/clay/{color}clay"}},"attributes":{"reinforcable":true,"onTongTransform":{"translation":{"x":-0.9,"y":-1.5,"z":-0.6},"rotation":{"x":117,"y":0,"z":0},"scale":0.74},"shelvable":false,"handbook":{"groupBy":["toolmold-*-{materialtype}-{tooltype}"]},"onMetalTongTransform":{"translation":{"x":-2.1,"y":-0.19,"z":-1},"rotation":{"x":180,"y":7,"z":4},"origin":{"x":0.5,"y":0,"z":0.5},"scale":0.55}},"attributesByType":{"toolmold-red-raw-*":{"beehivekiln":{"0":{"type":"block","code":"smex:toolmold-tan-fired-{tooltype}"},"1":{"type":"block","code":"smex:toolmold-orange-fired-{tooltype}"},"2":{"type":"block","code":"smex:toolmold-red-fired-{tooltype}"},"3":{"type":"block","code":"smex:toolmold-brown-fired-{tooltype}"}}},"toolmold-blue-raw-*":{"beehivekiln":{"0":{"type":"block","code":"smex:toolmold-cream-fired-{tooltype}"},"1":{"type":"block","code":"smex:toolmold-gray-fired-{tooltype}"},"2":{"type":"block","code":"smex:toolmold-black-fired-{tooltype}"},"3":{"type":"block","code":"smex:toolmold-black-fired-{tooltype}"}}},"toolmold-fire-raw-*":{"beehivekiln":{"0":{"type":"block","code":"smex:toolmold-fire-fired-{tooltype}"},"1":{"type":"block","code":"smex:toolmold-fire-fired-{tooltype}"},"2":{"type":"block","code":"smex:toolmold-fire-fired-{tooltype}"},"3":{"type":"block","code":"smex:toolmold-fire-fired-{tooltype}"}}}},"combustiblePropsByType":{"toolmold-fire-raw-*":{"meltingPoint":650,"meltingDuration":45,"smeltedRatio":1,"smeltingType":"fire","smeltedStack":{"type":"block","code":"smex:toolmold-fire-fired-{tooltype}"},"requiresContainer":false},"toolmold-blue-raw-*":{"meltingPoint":650,"meltingDuration":45,"smeltedRatio":1,"smeltingType":"fire","smeltedStack":{"type":"block","code":"smex:toolmold-blue-fired-{tooltype}"},"requiresContainer":false},"toolmold-red-raw-*":{"meltingPoint":650,"meltingDuration":45,"smeltedRatio":1,"smeltingType":"fire","smeltedStack":{"type":"block","code":"smex:toolmold-earthyorange-fired-{tooltype}"},"requiresContainer":false}},"blockmaterial":"Ceramic","creativeinventory":{"general":["*"],"construction":["*"],"smex":["*"]},"replaceable":700,"resistance":1.5,"maxstacksize":8,"lightAbsorption":0,"sounds":{"walk":"game:walk/stone"},"collisionbox":{"x1":0.0625,"y1":0,"z1":0.0625,"x2":0.9375,"y2":0.125,"z2":0.9375},"selectionbox":{"x1":0.0625,"y1":0,"z1":0.0625,"x2":0.9375,"y2":0.125,"z2":0.9375},"sideopaque":{"all":false},"sidesolid":{"all":false},"guiTransform":{"translation":{"x":0,"y":3,"z":0},"origin":{"x":0.5,"y":0.0625,"z":0.5},"scale":1.33},"tpHandTransform":{"translation":{"x":-1,"y":-0.6,"z":-1.05},"rotation":{"x":-87,"y":9,"z":4},"origin":{"x":0.5,"y":0.125,"z":0.5},"scale":0.5},"groundTransform":{"translation":{"x":0,"y":0,"z":0},"rotation":{"x":0,"y":-45,"z":0},"origin":{"x":0.5,"y":0,"z":0.5},"scale":2.2}}""";

  private static ExBlockDef Def(string path) =>
    ToolMoldDefinitions.Definitions("smex").Single(d => d.Location.Path == path);

  [Theory]
  [InlineData("blocktypes/molds/toolmoldfired.json", FiredGolden)]
  [InlineData("blocktypes/molds/toolmoldraw.json", RawGolden)]
  public void Def_reproduces_the_migrated_json(string path, string golden)
  {
    ExBlockDef def = Def(path);
    Assert.True(
      DefinitionParity.Equal(JObject.Parse(golden), def.ToJson(), out string normalized),
      $"tool-mold def at {path} diverged from the migrated JSON:\n{normalized}"
    );
  }

  [Fact]
  public void Provider_yields_exactly_the_two_mold_defs_under_smex()
  {
    var defs = ToolMoldDefinitions.Definitions("smex").ToList();
    Assert.Equal(2, defs.Count);
    Assert.All(defs, d => Assert.Equal("smex", d.Location.Domain));
    Assert.Contains(defs, d => d.Location.Path == "blocktypes/molds/toolmoldfired.json");
    Assert.Contains(defs, d => d.Location.Path == "blocktypes/molds/toolmoldraw.json");
  }

  [Fact]
  public void Both_molds_are_discovered_and_registered_under_smex()
  {
    ExDefinitions.Clear();
    ExDefinitions.DiscoverAndRegister("smex", typeof(ToolMoldDefinitions).Assembly);
    Assert.Equal(
      2,
      ExDefinitions.Blocks.Count(d =>
        d is { Code: "toolmold", Domain: "smex" }
      )
    );
  }
}
