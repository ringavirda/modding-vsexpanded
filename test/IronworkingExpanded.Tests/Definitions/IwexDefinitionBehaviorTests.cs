using System.Collections.Generic;
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
public class IwexDefinitionBehaviorTests {
  [Fact]
  public void Tuyere_derives_its_orientation_table_from_the_def() {
    // The BlockPipe base derives AllowedOrientations from the tuyere's own def; the variant group order
    // [s,n,w,e] carries through and its first state "s" is the fallback (no hand-written tables).
    var orientations = ExDefinitions.OrientationMap(
      BlockTuyere.Definitions("iwex")
    );
    Assert.Equal(["s", "n", "w", "e"], orientations["tuyere"]);
  }

  [Fact]
  public void Core_multiblock_covers_all_160_cells() {
    // A whole-footprint count: it catches a cell added or lost anywhere in the nine layers. The number
    // moves whenever the layout in BlockBlastFurnaceCoreCold is redrawn.
    JObject def = BlockBlastFurnaceCoreCold
      .Definitions("iwex")
      .Single()
      .ToJson();
    var offsets = (JArray)
      def["attributes"]!["multiblockStructure"]!["offsets"]!;
    Assert.Equal(160, offsets.Count);
  }

  #region Burdenmaker

  [Fact]
  public void Burdenmaker_footprint_is_eight_fillers_and_hosts_no_ports() {
    // The burdenmaker has nothing to turn, so it hosts no MP port and can be built before any power
    // exists. No cell carries a behaviours array.
    JObject def = BlockBurdenmaker.Definitions("iwex").First().ToJson();
    var cells = (JArray)def["attributes"]!["fillerOffsets"]!;

    var offsets = cells
      .Select(c => ((int)c["x"]!, (int)c["y"]!, (int)c["z"]!))
      .ToHashSet();

    // 9 drawn cells minus the principal at the origin, which FillerLayoutBuilder skips.
    Assert.Equal(8, cells.Count);
    Assert.Equal(
      new HashSet<(int, int, int)>
      {
        (-1, 0, -1),
        (0, 0, -1),
        (1, 0, -1),
        (-1, 0, 0),
        (1, 0, 0), // the basin, minus the principal at (0,0,0)
        (-1, 1, -1),
        (0, 1, -1),
        (1, 1, -1), // the two hoppers and their gate
      },
      offsets
    );
    Assert.All(cells, c => Assert.Null(c["behaviors"]));
  }

  [Fact]
  public void Burdenmaker_construction_never_names_an_lpex_code() {
    // iwex declares no dependency on lpex, so a construction stage naming an lpex code would put the
    // only source of burden out of reach for an iwex-only player.
    string json = BlockBurdenmaker
      .Definitions("iwex")
      .First()
      .ToJson()
      .ToString();
    Assert.DoesNotContain("lpex:", json);
  }

  #endregion

  [Fact]
  public void Parity_catches_a_wrong_filler_port_face() {
    // Guards that the parity oracle compares per-cell behaviours: flipping one port's face on a clone of
    // the def's own output must fail parity. This is a harness self-test, so it can read any def that
    // hosts a per-cell port; it asserts that premise below rather than passing vacuously on a def with
    // no behaviours to mutate.
    JObject original = BlockTwinTubMPBlower
      .Definitions("iwex")
      .Single()
      .ToJson();
    var mutated = (JObject)original.DeepClone();
    JToken? cell = (
      (JArray)mutated["attributes"]!["fillerOffsets"]!
    ).FirstOrDefault(c => c["behaviors"] is JArray);

    Assert.NotNull(cell); // the premise: there is a per-cell behaviour to corrupt
    cell["behaviors"]![0]!["face"] = "east";

    Assert.False(DefinitionParity.Equal(original, mutated));
  }
}
