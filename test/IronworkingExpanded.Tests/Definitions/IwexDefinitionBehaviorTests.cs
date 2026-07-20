using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using IronworkingExpanded.BlockStructures.OreProcessing.Blocks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Behavioural (non-parity) checks on iwex's code-first defs - beyond the byte-for-byte golden oracle
/// (<see cref="IwexDefinitionGoldenTests"/>): runtime orientation derivation, the big multiblock cell counts
/// (a guard on the ASCII layer DSL), and that the parity oracle actually compares per-cell filler behaviours.
/// </summary>
public class IwexDefinitionBehaviorTests
{
  [Fact]
  public void Tuyere_derives_its_orientation_table_from_the_def()
  {
    // The BlockPipe base derives AllowedOrientations from the tuyere's own def; the variant group order
    // [s,n,w,e] carries through and its first state "s" is the fallback (no hand-written tables).
    var orientations = ExDefinitions.OrientationMap(
      BlockTuyere.Definitions("iwex")
    );
    Assert.Equal(["s", "n", "w", "e"], orientations["tuyere"]);
  }

  [Fact]
  public void Core_multiblock_covers_all_157_cells()
  {
    // The door -> core move swaps two cells (the bottom-centre brick becomes the core, the door's
    // own two cells revert to brick) and adds none, so this count is the primary guard that the
    // re-originned layout is a pure translation.
    JObject def = BlockBlastFurnaceCoreCold
      .Definitions("iwex")
      .Single()
      .ToJson();
    var offsets = (JArray)
      def["attributes"]!["multiblockStructure"]!["offsets"]!;
    Assert.Equal(157, offsets.Count);
  }

  [Fact]
  public void Mixer_footprint_hosts_three_west_mp_ports()
  {
    var cells = (JArray)
      BlockOreMixer.Definitions("iwex").Single().ToJson()["attributes"]![
        "fillerOffsets"
      ]!;
    var ports = cells
      .Where(c => c["behaviors"] is JArray)
      .SelectMany(c => (JArray)c["behaviors"]!)
      .ToList();
    Assert.Equal(3, ports.Count);
    Assert.All(
      ports,
      b =>
      {
        Assert.Equal("exlib.BEBehaviorMPFillerPort", (string?)b["code"]);
        Assert.Equal("west", (string?)b["face"]);
      }
    );
  }

  [Fact]
  public void Parity_catches_a_wrong_filler_port_face()
  {
    // Guards that the oracle actually compares per-cell behaviours (they used to be dropped): flip one port's
    // face on a clone of the def's own output and parity must FAIL - without the CanonicalCells behaviours fix
    // this would pass, hiding a broken port.
    JObject original = BlockOreMixer.Definitions("iwex").Single().ToJson();
    var mutated = (JObject)original.DeepClone();
    var cell = ((JArray)mutated["attributes"]!["fillerOffsets"]!).First(c =>
      c["behaviors"] is JArray
    );
    cell["behaviors"]![0]!["face"] = "east";

    Assert.False(DefinitionParity.Equal(original, mutated));
  }
}
