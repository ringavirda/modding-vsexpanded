using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using PipesAndPowerExpanded.BlockStructures.Engine.Blocks;
using PipesAndPowerExpanded.BlockStructures.ManualPump.Blocks;
using Xunit;

namespace PipesAndPowerExpanded.Tests;

/// <summary>
/// Parity oracle for the engine mega-blocks (Watt/Cornish) and the manual fluid pump, authored code-first.
/// The engines exercise the shared <c>EngineShell</c> surface + a multi-ingredient staged construction table
/// (6 stages for Watt, 7 for Cornish) + the geometry/filler attributes; the pump is a single-cell filler
/// connector. Comparison via the shared <see cref="DefinitionParity"/> (numeric-agnostic, fillerOffsets as an
/// unordered set, schema arrays order-sensitive). Goldens are the deleted JSON, minified.
/// </summary>
public class EngineMachineDefinitionParityTests
{
  // Verbatim (minified) copy of the former assets/ppex/blocktypes/engine/watt.json.
  private const string WattGolden =
    """{"code":"enginewatt","class":"ppex.BlockEngineWatt","entityClass":"ppex.BlockEntityEngineWatt","blockmaterial":"Metal","sounds":{"place":"game:block/anvil","break":"game:block/anvil","hit":"game:block/anvil","walk":"game:walk/stone"},"requiredMiningTier":3,"resistance":45.0,"maxstacksize":1,"behaviors":[{"name":"HorizontalOrientable"},{"name":"BlockEntityInteract"}],"entityBehaviors":[{"name":"Animatable"},{"name":"ExRightClickConstructable","properties":{"stages":[{"addElements":["Root/Cylinder"]},{"requireStacks":[{"type":"item","code":"metalplate-*","name":"ppex:rcc-ingredient-metalplate","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":2},{"type":"item","code":"rod-*","name":"ppex:rcc-ingredient-rod","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":4},{"type":"item","code":"metalnailsandstrips-*","name":"ppex:rcc-ingredient-nailsandstrips","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":4},{"type":"item","code":"game:burnedbrick-fire","quantity":36}],"addElements":["Root/BeamSupport"]},{"requireStacks":[{"type":"item","code":"rod-*","name":"ppex:rcc-ingredient-rod","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":8},{"type":"item","code":"metalnailsandstrips-*","name":"ppex:rcc-ingredient-nailsandstrips","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":4}],"addElements":["Root/Beam"]},{"requireStacks":[{"type":"item","code":"metalplate-*","name":"ppex:rcc-ingredient-metalplate","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":2},{"type":"item","code":"rod-*","name":"ppex:rcc-ingredient-rod","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":4},{"type":"item","code":"metalnailsandstrips-*","name":"ppex:rcc-ingredient-nailsandstrips","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":2}],"addElements":["Root/Piston"]},{"requireStacks":[{"type":"item","code":"rod-*","name":"ppex:rcc-ingredient-rod","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":4},{"type":"item","code":"metalnailsandstrips-*","name":"ppex:rcc-ingredient-nailsandstrips","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":2}],"addElements":["Root/ControlPiston"]},{"requireStacks":[{"type":"item","code":"rod-*","name":"ppex:rcc-ingredient-rod","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":4}],"addElements":["Root/Rod"]}]}}],"attributes":{"submachineOffset":{"x":0,"y":0,"z":2},"gearHousingOffset":{"x":0,"y":3,"z":1},"fillerOffsets":[{"x":0,"y":1,"z":0},{"x":0,"y":2,"z":0},{"x":0,"y":3,"z":0},{"x":0,"y":0,"z":1},{"x":0,"y":1,"z":1},{"x":0,"y":2,"z":1},{"x":0,"y":3,"z":1},{"x":0,"y":1,"z":2},{"x":0,"y":2,"z":2},{"x":0,"y":3,"z":2}]},"variantgroups":[{"code":"side","loadFromProperties":"abstract/horizontalorientation"}],"creativeinventory":{"general":["*-north"],"ppex":["*-north"]},"shape":{"base":"ppex:engine/watt","rotateYByType":{"*-north":180,"*-east":90,"*-south":0,"*-west":270},"selectiveElements":["Root/Cylinder/*"]},"selectionbox":{"x1":0,"y1":0,"z1":0,"x2":1,"y2":1,"z2":1},"collisionbox":{"x1":0,"y1":0,"z1":0,"x2":1,"y2":1,"z2":1},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  [Fact]
  public void Watt_def_reproduces_the_migrated_json()
  {
    JObject expected = JObject.Parse(WattGolden);
    JObject actual = BlockEngineWatt.Definitions("ppex").Single().ToJson();

    Assert.True(
      DefinitionParity.Equal(expected, actual, out string normalized),
      "Watt engine def diverged from the migrated JSON. Normalized actual:\n" + normalized
    );
  }

  [Fact]
  public void Watt_targets_its_asset_location()
  {
    var loc = BlockEngineWatt.Definitions("ppex").Single().Location;
    Assert.Equal("ppex", loc.Domain);
    Assert.Equal("blocktypes/engine/watt.json", loc.Path);
  }

  // Verbatim (minified) copy of the former assets/ppex/blocktypes/engine/cornish.json.
  private const string CornishGolden =
    """{"code":"enginecornish","class":"ppex.BlockEngineCornish","entityClass":"ppex.BlockEntityEngineCornish","blockmaterial":"Metal","sounds":{"place":"game:block/anvil","break":"game:block/anvil","hit":"game:block/anvil","walk":"game:walk/stone"},"requiredMiningTier":3,"resistance":45.0,"maxstacksize":1,"behaviors":[{"name":"HorizontalOrientable"},{"name":"BlockEntityInteract"}],"entityBehaviors":[{"name":"Animatable"},{"name":"ExRightClickConstructable","properties":{"stages":[{"addElements":["Root/Cylinder"]},{"requireStacks":[{"type":"item","code":"metalplate-*","name":"ppex:rcc-ingredient-metalplate","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":12},{"type":"item","code":"rod-*","name":"ppex:rcc-ingredient-rod","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":12},{"type":"item","code":"game:burnedbrick-fire","quantity":36}],"addElements":["Root/BeamSupport"]},{"requireStacks":[{"type":"item","code":"metalplate-*","name":"ppex:rcc-ingredient-metalplate","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":16},{"type":"item","code":"rod-*","name":"ppex:rcc-ingredient-rod","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":8}],"addElements":["Root/Beam"]},{"requireStacks":[{"type":"item","code":"metalplate-*","name":"ppex:rcc-ingredient-metalplate","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":8},{"type":"item","code":"rod-*","name":"ppex:rcc-ingredient-rod","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":6}],"addElements":["Root/Piston"]},{"requireStacks":[{"type":"item","code":"metalplate-*","name":"ppex:rcc-ingredient-metalplate","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":6},{"type":"item","code":"metalnailsandstrips-*","name":"ppex:rcc-ingredient-nailsandstrips","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":6}],"addElements":["Root/ControlPiston"]},{"requireStacks":[{"type":"item","code":"metalplate-*","name":"ppex:rcc-ingredient-metalplate","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":4},{"type":"item","code":"rod-*","name":"ppex:rcc-ingredient-rod","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":8}],"addElements":["Root/ControlPistonSteam"]},{"requireStacks":[{"type":"item","code":"rod-*","name":"ppex:rcc-ingredient-rod","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":8},{"type":"item","code":"metalnailsandstrips-*","name":"ppex:rcc-ingredient-nailsandstrips","allowedVariants":["iron","steel"],"storeWildCard":"metal","quantity":8}],"addElements":["Root/Rod"]}]}}],"attributes":{"submachineOffset":{"x":0,"y":0,"z":2},"gearHousingOffset":{"x":0,"y":3,"z":1},"fillerOffsets":[{"x":0,"y":1,"z":0},{"x":0,"y":2,"z":0},{"x":0,"y":3,"z":0},{"x":0,"y":0,"z":1},{"x":0,"y":1,"z":1},{"x":0,"y":2,"z":1},{"x":0,"y":3,"z":1},{"x":0,"y":1,"z":2},{"x":0,"y":2,"z":2},{"x":0,"y":3,"z":2}]},"variantgroups":[{"code":"side","loadFromProperties":"abstract/horizontalorientation"}],"creativeinventory":{"general":["*-north"],"ppex":["*-north"]},"shape":{"base":"ppex:engine/cornish","rotateYByType":{"*-north":180,"*-east":90,"*-south":0,"*-west":270},"selectiveElements":["Root/Cylinder/*"]},"selectionbox":{"x1":0,"y1":0,"z1":0,"x2":1,"y2":1,"z2":1},"collisionbox":{"x1":0,"y1":0,"z1":0,"x2":1,"y2":1,"z2":1},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  [Fact]
  public void Cornish_def_reproduces_the_migrated_json()
  {
    JObject expected = JObject.Parse(CornishGolden);
    JObject actual = BlockEngineCornish.Definitions("ppex").Single().ToJson();

    Assert.True(
      DefinitionParity.Equal(expected, actual, out string normalized),
      "Cornish engine def diverged from the migrated JSON. Normalized actual:\n" + normalized
    );
  }

  [Fact]
  public void Cornish_targets_its_asset_location()
  {
    var loc = BlockEngineCornish.Definitions("ppex").Single().Location;
    Assert.Equal("blocktypes/engine/cornish.json", loc.Path);
  }

  // Verbatim (minified) copy of the former assets/ppex/blocktypes/manualfluidpump.json.
  private const string PumpGolden =
    """{"code":"manualfluidpump","class":"ppex.BlockManualFluidPump","entityClass":"ppex.BlockEntityManualFluidPump","behaviors":[{"name":"HorizontalOrientable"}],"entityBehaviors":[{"name":"Animatable"}],"blockmaterial":"Metal","variantgroups":[{"code":"side","loadFromProperties":"abstract/horizontalorientation"}],"creativeinventory":{"general":["*-north"],"ppex":["*-north"]},"attributes":{"fillerOffsets":[{"x":0,"y":1,"z":0}]},"shapebytype":{"*-north":{"base":"ppex:manualfluidpump","rotateY":0},"*-east":{"base":"ppex:manualfluidpump","rotateY":270},"*-south":{"base":"ppex:manualfluidpump","rotateY":180},"*-west":{"base":"ppex:manualfluidpump","rotateY":90}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  [Fact]
  public void ManualFluidPump_def_reproduces_the_migrated_json()
  {
    JObject expected = JObject.Parse(PumpGolden);
    JObject actual = BlockManualFluidPump.Definitions("ppex").Single().ToJson();

    Assert.True(
      DefinitionParity.Equal(expected, actual, out string normalized),
      "Manual fluid pump def diverged from the migrated JSON. Normalized actual:\n" + normalized
    );
  }

  [Fact]
  public void ManualFluidPump_targets_its_asset_location()
  {
    var loc = BlockManualFluidPump.Definitions("ppex").Single().Location;
    Assert.Equal("blocktypes/manualfluidpump.json", loc.Path);
  }
}
