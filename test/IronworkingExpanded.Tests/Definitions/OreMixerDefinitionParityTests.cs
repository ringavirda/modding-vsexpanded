using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.OreMixer.Blocks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Parity oracle for the ore-mixer mega-block authored code-first (migrated from blocktypes/ore/mixer.json). The
/// headline is a <c>fillerOffsets</c> footprint whose three upper cells each host a mechanical-power port
/// (<c>exlib.BEBehaviorMPFillerPort</c> on the west face) — the first migrated block with per-cell filler
/// <c>behaviors</c>, so this also pins that the parity oracle now compares those behaviors (they were previously
/// dropped). Plus a 3-stage RightClickConstructable and the derived per-orientation shape rotation.
/// </summary>
public class OreMixerDefinitionParityTests
{
  private const string MixerGolden =
    """{"code":"mixer","class":"iwex.BlockOreMixer","entityClass":"iwex.BlockEntityOreMixer","blockmaterial":"Metal","requiredMiningTier":0,"resistance":4.5,"maxstacksize":1,"drops":[],"attributes":{"fillerOffsets":[{"x":-1,"y":0,"z":0},{"x":1,"y":0,"z":0},{"x":0,"y":1,"z":0,"behaviors":[{"code":"exlib.BEBehaviorMPFillerPort","face":"west"}],"allowAttach":true},{"x":-1,"y":1,"z":0,"behaviors":[{"code":"exlib.BEBehaviorMPFillerPort","face":"west"}],"allowAttach":true},{"x":1,"y":1,"z":0,"behaviors":[{"code":"exlib.BEBehaviorMPFillerPort","face":"west"}],"allowAttach":true}]},"behaviors":[{"name":"HorizontalOrientable"},{"name":"BlockEntityInteract"}],"entityBehaviors":[{"name":"Animatable"},{"name":"ExRightClickConstructable","properties":{"stages":[{"addElements":["Root/LowerCasing","Root/Support"]},{"requireStacks":[{"type":"item","code":"game:ingot-iron","name":"iwex:rcc-ingredient-ironcasing","quantity":6}],"addElements":["Root/UpperCasing"]},{"requireStacks":[{"type":"item","code":"ppex:gear-iron","name":"iwex:rcc-ingredient-rotorgears","quantity":2},{"type":"item","code":"game:ingot-iron","name":"iwex:rcc-ingredient-rotorshaft","quantity":2}],"addElements":["Root/Rotor"]}]}}],"variantgroups":[{"code":"side","loadFromProperties":"abstract/horizontalorientation"}],"creativeinventory":{"general":["*-north"],"iwex":["*-north"]},"shape":{"base":"iwex:ore/mixer","rotateYByType":{"*-north":0,"*-east":270,"*-south":180,"*-west":90},"selectiveElements":["Root/LowerCasing/*","Root/Support/*"]},"selectionbox":{"x1":0,"y1":0,"z1":0,"x2":1,"y2":1,"z2":1},"collisionbox":{"x1":0,"y1":0,"z1":0,"x2":1,"y2":1,"z2":1},"sidesolid":{"all":false},"sideopaque":{"all":false},"sounds":{"place":"game:block/anvil","break":"game:block/metal","hit":"game:block/metal","walk":"game:walk/stone"}}""";

  private static ExBlockDef Def() => BlockOreMixer.Definitions("iwex").Single();

  [Fact]
  public void Mixer_def_reproduces_the_migrated_json()
  {
    Assert.True(
      DefinitionParity.Equal(JObject.Parse(MixerGolden), Def().ToJson(), out string normalized),
      "ore-mixer code-first def diverged from the migrated JSON:\n" + normalized
    );
  }

  [Fact]
  public void Mixer_targets_the_iwex_ore_mixer_asset_location()
  {
    var loc = Def().Location;
    Assert.Equal("iwex", loc.Domain);
    Assert.Equal("blocktypes/ore/mixer.json", loc.Path);
  }

  [Fact]
  public void Mixer_footprint_hosts_three_west_mp_ports()
  {
    var cells = (JArray)Def().ToJson()["attributes"]!["fillerOffsets"]!;
    var ports = cells
      .Where(c => c["behaviors"] is JArray)
      .SelectMany(c => (JArray)c["behaviors"]!)
      .ToList();
    Assert.Equal(3, ports.Count);
    Assert.All(ports, b =>
    {
      Assert.Equal("exlib.BEBehaviorMPFillerPort", (string?)b["code"]);
      Assert.Equal("west", (string?)b["face"]);
    });
  }

  [Fact]
  public void Parity_now_catches_a_wrong_filler_port_face()
  {
    // Guards that the oracle actually compares per-cell behaviors (they used to be dropped): flip one port's
    // face and parity must FAIL. Without the CanonicalCells behaviors fix this would pass, hiding a broken port.
    JObject mutated = JObject.Parse(MixerGolden);
    var cell = ((JArray)mutated["attributes"]!["fillerOffsets"]!)
      .First(c => c["behaviors"] is JArray);
    cell["behaviors"]![0]!["face"] = "east";

    Assert.False(DefinitionParity.Equal(mutated, Def().ToJson()));
  }
}
