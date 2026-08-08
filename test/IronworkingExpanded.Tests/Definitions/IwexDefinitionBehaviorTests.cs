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
  public void Core_multiblock_covers_all_160_cells()
  {
    // A whole-footprint count, which is what makes it useful: it catches a cell added or lost
    // anywhere in the nine layers, including in a layer nobody was editing.
    //
    // Was 157 until the furnace redraw. Both taps moved down onto the hearth course, the
    // charge column gained cells, and layer 0 gave up its (-3,0,0) corner as the slag runout - see
    // BlockBlastFurnaceCoreCold. A count is a summary, so it moves whenever the drawing does; that
    // is the point of pinning it rather than a reason to stop.
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
  public void Burdenmaker_footprint_is_eight_fillers_and_hosts_no_ports()
  {
    // "No cell carries a behaviours array" is the design's headline stated as an assertion, not a
    // formality: the burdenmaker has nothing to turn, so it has no MP port, and that is what lets it be
    // built before any power exists - which is in turn what keeps *nothing before cast iron requiring
    // power*. The ore mixer it replaced hosted three west MP ports, so a copy-paste from the wrong template
    // is the exact mistake this catches. (That mixer is gone; this assertion is what makes sure
    // its shape did not come along.)
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
  public void Burdenmaker_construction_never_names_an_lpex_code()
  {
    // The ore mixer's stage 3 asked for `lpex:gear-iron` from a mod iwex declares no dependency on, which
    // made the only source of burden unreachable for an iwex-only player. That is this machine's whole
    // reason for having no mechanism, so the refusal is asserted rather than left to review.
    string json = BlockBurdenmaker
      .Definitions("iwex")
      .First()
      .ToJson()
      .ToString();
    Assert.DoesNotContain("lpex:", json);
  }

  #endregion

  [Fact]
  public void Parity_catches_a_wrong_filler_port_face()
  {
    // Guards that the oracle actually compares per-cell behaviours (they used to be dropped): flip one port's
    // face on a clone of the def's own output and parity must fail - without the CanonicalCells behaviours fix
    // this would pass, hiding a broken port.
    //
    // Re-pointed from the ore mixer to the twin-tub blower when the mixer was deleted.
    // This is a harness self-test, not a test of whichever def it happens to read - it must survive every
    // content change, so it needs any def that still hosts a per-cell port. It asserts its own premise below
    // for that reason: the day the blower stops hosting one, this fails loudly instead of passing vacuously
    // on a def with no behaviours to mutate.
    JObject original = BlockTwinTubMPBlower.Definitions("iwex").Single().ToJson();
    var mutated = (JObject)original.DeepClone();
    JToken? cell = ((JArray)mutated["attributes"]!["fillerOffsets"]!).FirstOrDefault(c =>
      c["behaviors"] is JArray
    );

    Assert.NotNull(cell); // the premise: there is a per-cell behaviour to corrupt
    cell["behaviors"]![0]!["face"] = "east";

    Assert.False(DefinitionParity.Equal(original, mutated));
  }
}
