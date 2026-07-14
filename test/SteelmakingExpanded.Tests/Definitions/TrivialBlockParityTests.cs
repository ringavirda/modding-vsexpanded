using System;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using SteelmakingExpanded.BlockStructures.Converter.Blocks;
using SteelmakingExpanded.BlockStructures.CowperStove.Blocks;
using SteelmakingExpanded.BlockStructures.Engine.Blocks;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Parity oracle for the flat smex blocktypes now authored code-first (converter transmission + intake,
/// engine air-blower, cowperstove heat-sink). No attributes, so each is a straight builder transcription; the
/// shape rotations come from <c>ShapeByTypeSpunPerOrientation</c> (transmission at +180). Goldens are the
/// deleted JSON, minified.
/// </summary>
public class TrivialBlockParityTests
{
  private const string Transmission =
    """{"code":"convertertransmission","class":"smex.BlockConverterTransmission","entityClass":"smex.BlockEntityConverterTransmission","entityBehaviors":[{"name":"smex.BEBehaviorMPConverterTransmission"}],"blockmaterial":"Metal","sounds":{"place":"game:block/anvil","break":"game:block/anvil","hit":"game:block/anvil","walk":"game:walk/stone"},"maxstacksize":1,"creativeinventory":{"general":["*-north"],"smex":["*-north"]},"behaviors":[{"name":"HorizontalOrientable"}],"variantgroups":[{"code":"side","loadFromProperties":"abstract/horizontalorientation"}],"shapebytype":{"*-north":{"base":"smex:converter/transmission","rotateY":180},"*-east":{"base":"smex:converter/transmission","rotateY":90},"*-south":{"base":"smex:converter/transmission","rotateY":0},"*-west":{"base":"smex:converter/transmission","rotateY":270}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string Intake =
    """{"code":"converter","class":"smex.BlockConverterIntake","behaviors":[{"name":"HorizontalOrientable"}],"blockmaterial":"Metal","sounds":{"place":"game:block/anvil","break":"game:block/anvil","hit":"game:block/anvil","walk":"game:walk/stone"},"maxstacksize":1,"creativeinventory":{"general":["*-north"],"smex":["*-north"]},"variantgroups":[{"code":"type","states":["intake"]},{"code":"side","loadFromProperties":"abstract/horizontalorientation"}],"shapebytype":{"*-north":{"base":"smex:converter/intake","rotateY":0},"*-east":{"base":"smex:converter/intake","rotateY":270},"*-south":{"base":"smex:converter/intake","rotateY":180},"*-west":{"base":"smex:converter/intake","rotateY":90}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string AirBlower =
    """{"code":"engineairblower","class":"smex.BlockEngineAirBlower","entityClass":"smex.BlockEntityEngineAirBlower","behaviors":[{"name":"HorizontalOrientable"}],"entityBehaviors":[{"name":"Animatable"}],"blockmaterial":"Metal","variantgroups":[{"code":"side","loadFromProperties":"abstract/horizontalorientation"}],"creativeinventory":{"general":["*-north"],"smex":["*-north"]},"shapebytype":{"*-north":{"base":"smex:engine/airblower","rotateY":0},"*-east":{"base":"smex:engine/airblower","rotateY":270},"*-south":{"base":"smex:engine/airblower","rotateY":180},"*-west":{"base":"smex:engine/airblower","rotateY":90}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  private const string HeatSink =
    """{"code":"cowperstoveheatsink","class":"smex.BlockHeatSink","entityClass":"smex.BlockEntityHeatSink","shape":{"base":"smex:cowperstove/heatsink"},"blockmaterial":"Metal","sounds":{"place":"game:block/anvil","break":"game:block/anvil","hit":"game:block/anvil","walk":"game:walk/stone"},"maxstacksize":4,"lightAbsorption":99,"creativeinventory":{"general":["*-north"],"smex":["*-north"]},"behaviors":[{"name":"HorizontalOrientable"}],"variantgroups":[{"code":"side","loadFromProperties":"abstract/horizontalorientation"}],"shapebytype":{"*-north":{"base":"smex:cowperstove/heatsink","rotateY":0},"*-east":{"base":"smex:cowperstove/heatsink","rotateY":270},"*-south":{"base":"smex:cowperstove/heatsink","rotateY":180},"*-west":{"base":"smex:cowperstove/heatsink","rotateY":90}},"sidesolid":{"all":false},"sideopaque":{"all":false}}""";

  [Theory]
  [InlineData("transmission")]
  [InlineData("intake")]
  [InlineData("airblower")]
  [InlineData("heatsink")]
  public void Trivial_def_reproduces_the_migrated_json(string name)
  {
    (string golden, JObject actual) = name switch
    {
      "transmission" => (Transmission, BlockConverterTransmission.Definitions("smex").Single().ToJson()),
      "intake" => (Intake, BlockConverterIntake.Definitions("smex").Single().ToJson()),
      "airblower" => (AirBlower, BlockEngineAirBlower.Definitions("smex").Single().ToJson()),
      "heatsink" => (HeatSink, BlockHeatSink.Definitions("smex").Single().ToJson()),
      _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    Assert.True(
      DefinitionParity.Equal(JObject.Parse(golden), actual, out string normalized),
      $"{name} def diverged from the migrated JSON:\n{normalized}"
    );
  }
}
