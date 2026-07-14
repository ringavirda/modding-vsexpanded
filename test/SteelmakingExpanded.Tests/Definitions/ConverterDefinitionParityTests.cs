using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using SteelmakingExpanded.BlockStructures.Converter.Blocks;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Parity oracle for smex's first code-first MACHINE-with-a-structure-map: the converter control's authored
/// <see cref="ExBlockDef"/> must reproduce the hand-written blocktypes/converter/control.json byte-for-byte
/// (semantically), so the injected block expands identically and no saved world sees a changed block. The
/// headline is the <c>multiblockStructure</c> table (blockNumbers + ~33 offsets) now authored via the typed
/// builder - this golden is the deleted file, verbatim, and the durable record of what it must remain.
/// </summary>
public class ConverterDefinitionParityTests
{
  // Verbatim copy of the former assets/smex/blocktypes/converter/control.json.
  private const string ControlGolden =
    """
    {
      "code": "convertercontrol",
      "class": "smex.BlockConverterControl",
      "entityClass": "smex.BlockEntityConverterControl",
      "entityBehaviors": [{ "name": "Animatable" }],
      "blockmaterial": "Metal",
      "sounds": {
        "place": "game:block/anvil",
        "break": "game:block/anvil",
        "hit": "game:block/anvil",
        "walk": "game:walk/stone"
      },
      "maxstacksize": 1,
      "creativeinventory": { "general": ["*-north"], "smex": ["*-north"] },
      "attributes": {
        "multiblockStructure": {
          "blockNumbers": {
            "smex:convertercontrol*": 1,
            "smex:convertertransmission*": 2,
            "smex:converterbessemer*": 3,
            "smex:converter-intake*": 4,
            "iwex:moltencanal-tap*": 5,
            "iwex:moltencanal-start*": 6,
            "iwex:moltencanal-straight*": 7,
            "exlib:structurefiller": 8
          },
          "offsets": [
            { "x": 0, "y": 0, "z": 0, "w": 1 },
            { "x": 0, "y": -1, "z": 0, "w": 2 },
            { "x": -1, "y": -1, "z": 1, "w": 8 },
            { "x": 0, "y": -1, "z": 1, "w": 8 },
            { "x": 1, "y": -1, "z": 1, "w": 8 },
            { "x": -1, "y": 0, "z": 1, "w": 8 },
            { "x": 0, "y": 0, "z": 1, "w": 8 },
            { "x": 1, "y": 0, "z": 1, "w": 8 },
            { "x": -1, "y": 1, "z": 1, "w": 8 },
            { "x": 0, "y": 1, "z": 1, "w": 8 },
            { "x": 1, "y": 1, "z": 1, "w": 8 },
            { "x": -1, "y": -1, "z": 2, "w": 8 },
            { "x": 0, "y": -1, "z": 2, "w": 8 },
            { "x": 1, "y": -1, "z": 2, "w": 8 },
            { "x": -1, "y": 0, "z": 2, "w": 8 },
            { "x": 0, "y": 0, "z": 2, "w": 3 },
            { "x": 1, "y": 0, "z": 2, "w": 8 },
            { "x": -1, "y": 1, "z": 2, "w": 8 },
            { "x": 0, "y": 1, "z": 2, "w": 8 },
            { "x": -1, "y": -1, "z": 3, "w": 8 },
            { "x": 0, "y": -1, "z": 3, "w": 8 },
            { "x": 1, "y": -1, "z": 3, "w": 8 },
            { "x": -1, "y": 0, "z": 3, "w": 8 },
            { "x": 0, "y": 0, "z": 3, "w": 8 },
            { "x": 1, "y": 0, "z": 3, "w": 8 },
            { "x": -1, "y": 1, "z": 3, "w": 8 },
            { "x": 0, "y": 1, "z": 3, "w": 8 },
            { "x": 1, "y": 1, "z": 3, "w": 8 },
            { "x": 0, "y": 0, "z": 4, "w": 4 },
            { "x": 1, "y": 1, "z": 2, "w": 5 },
            { "x": 2, "y": 1, "z": 2, "w": 7 },
            { "x": 1, "y": -2, "z": 2, "w": 6 },
            { "x": 2, "y": -2, "z": 2, "w": 7 }
          ]
        }
      },
      "behaviors": [
        { "name": "MultiblockStructure" },
        { "name": "HorizontalOrientable" }
      ],
      "variantgroups": [
        { "code": "side", "loadFromProperties": "abstract/horizontalorientation" }
      ],
      "shapebytype": {
        "*-north": { "base": "smex:converter/control", "rotateY": 0 },
        "*-east": { "base": "smex:converter/control", "rotateY": 270 },
        "*-south": { "base": "smex:converter/control", "rotateY": 180 },
        "*-west": { "base": "smex:converter/control", "rotateY": 90 }
      },
      "sidesolid": { "all": false },
      "sideopaque": { "all": false }
    }
    """;

  [Fact]
  public void Control_def_reproduces_the_migrated_json_exactly()
  {
    JObject expected = JObject.Parse(ControlGolden);
    JObject actual = BlockConverterControl.Definitions("smex").Single().ToJson();

    Assert.True(
      DefinitionParity.Equal(expected, actual, out string normalized),
      "converter control code-first def diverged from the migrated JSON:\n" + normalized
    );
  }

  [Fact]
  public void Control_targets_the_smex_converter_control_asset_location()
  {
    var loc = BlockConverterControl.Definitions("smex").Single().Location;
    Assert.Equal("smex", loc.Domain);
    Assert.Equal("blocktypes/converter/control.json", loc.Path);
  }

  [Fact]
  public void Discovery_finds_the_co_located_control_def()
  {
    ExDefinitions.Clear();
    ExDefinitions.DiscoverAndRegister(
      "smex",
      typeof(BlockConverterControl).Assembly
    );
    Assert.Contains(
      ExDefinitions.Blocks,
      d => d is { Code: "convertercontrol", Domain: "smex" }
    );
  }

  // Verbatim (minified) copy of the former assets/smex/blocktypes/converter/bessemer.json. The 3x3x3 filler
  // cube is authored via the layer DSL; the 7-stage RCC (one stage takes a {metal}-templated block pipe) via
  // the typed stage builder. Compared as an unordered filler set + numeric-agnostic values by DefinitionParity.
  private const string BessemerGolden =
    """{"code":"converterbessemer","class":"smex.BlockConverterBessemer","entityClass":"smex.BlockEntityConverterBessemer","blockmaterial":"Metal","sounds":{"place":"game:block/anvil","break":"game:block/anvil","hit":"game:block/anvil","walk":"game:walk/stone"},"requiredMiningTier":4,"resistance":45.0,"maxstacksize":1,"drops":[],"attributes":{"chiselOffset":{"x":0,"y":1,"z":0},"fillerOffsets":[{"x":-1,"y":-1,"z":-1},{"x":0,"y":-1,"z":-1},{"x":1,"y":-1,"z":-1},{"x":-1,"y":0,"z":-1},{"x":0,"y":0,"z":-1},{"x":1,"y":0,"z":-1},{"x":-1,"y":1,"z":-1},{"x":0,"y":1,"z":-1},{"x":1,"y":1,"z":-1},{"x":-1,"y":-1,"z":0},{"x":0,"y":-1,"z":0},{"x":1,"y":-1,"z":0},{"x":-1,"y":0,"z":0},{"x":1,"y":0,"z":0},{"x":1,"y":1,"z":0},{"x":0,"y":1,"z":0},{"x":-1,"y":-1,"z":1},{"x":0,"y":-1,"z":1},{"x":1,"y":-1,"z":1},{"x":-1,"y":0,"z":1},{"x":0,"y":0,"z":1},{"x":1,"y":0,"z":1},{"x":-1,"y":1,"z":1},{"x":0,"y":1,"z":1},{"x":1,"y":1,"z":1}]},"behaviors":[{"name":"HorizontalOrientable"},{"name":"BlockEntityInteract"}],"entityBehaviors":[{"name":"Animatable"},{"name":"ExRightClickConstructable","properties":{"stages":[{"addElements":["Root/GearShaft"]},{"requireStacks":[{"type":"item","code":"metalplate-*","name":"smex:rcc-ingredient-metalplate","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":24},{"type":"item","code":"metalnailsandstrips-*","name":"smex:rcc-ingredient-nailsandstrips","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":24},{"type":"item","code":"rod-*","name":"smex:rcc-ingredient-rod","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":12}],"addElements":["Root/BottomIron"]},{"requireStacks":[{"type":"item","code":"metalplate-*","name":"smex:rcc-ingredient-metalplate","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":4},{"type":"block","code":"ppex:pipe-straight-ns-{metal}","quantity":3},{"type":"item","code":"metalnailsandstrips-*","name":"smex:rcc-ingredient-nailsandstrips","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":6}],"addElements":["Root/GasIntake"]},{"requireStacks":[{"type":"item","code":"refractorybrick-fired-tier3","quantity":60},{"type":"item","code":"game:clay-fire","quantity":48}],"addElements":["Root/BottomRefractory"]},{"requireStacks":[{"type":"item","code":"refractorybrick-fired-tier3","quantity":24},{"type":"item","code":"game:clay-fire","quantity":24}],"addElements":["Root/UpRefractory"]},{"requireStacks":[{"type":"item","code":"metalplate-*","name":"smex:rcc-ingredient-metalplate","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":12},{"type":"item","code":"metalnailsandstrips-*","name":"smex:rcc-ingredient-nailsandstrips","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":12},{"type":"item","code":"rod-*","name":"smex:rcc-ingredient-rod","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":6}],"addElements":["Root/UpIron"]},{"requireStacks":[{"type":"item","code":"game:clay-fire","quantity":12}],"addElements":["Root/InputLining"]}]}}],"variantgroups":[{"code":"side","loadFromProperties":"abstract/horizontalorientation"}],"shape":{"base":"smex:converter/bessemer","rotateYByType":{"*-north":0,"*-east":270,"*-south":180,"*-west":90},"selectiveElements":["Root/GearShaft/*"]},"selectionbox":{"x1":-0.5,"y1":-0.5,"z1":-0.5,"x2":1.5,"y2":2,"z2":1.5},"collisionbox":{"x1":-0.5,"y1":-0.5,"z1":-0.5,"x2":1.5,"y2":2,"z2":1.5},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  [Fact]
  public void Bessemer_def_reproduces_the_migrated_json()
  {
    JObject expected = JObject.Parse(BessemerGolden);
    JObject actual = BlockConverterBessemer.Definitions("smex").Single().ToJson();

    Assert.True(
      DefinitionParity.Equal(expected, actual, out string normalized),
      "converter bessemer code-first def diverged from the migrated JSON:\n" + normalized
    );
  }

  [Fact]
  public void Bessemer_targets_the_smex_converter_bessemer_asset_location()
  {
    var loc = BlockConverterBessemer.Definitions("smex").Single().Location;
    Assert.Equal("blocktypes/converter/bessemer.json", loc.Path);
  }
}
