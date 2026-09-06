using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks;
using ExpandedLib.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.BlockStructures.OreProcessing.Blocks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Behavioural (non-parity) checks on iiex's code-first defs - beyond the byte-for-byte golden oracle
/// (<see cref="IiexDefinitionGoldenTests"/>): runtime orientation derivation, the big multiblock cell counts
/// (a guard on the ASCII layer DSL), and that the parity oracle actually compares per-cell filler behaviours.
/// </summary>
public class IiexDefinitionBehaviorTests {
  private static readonly System.Reflection.Assembly Mod =
    typeof(IiexConfig).Assembly;

  [Fact]
  public void Tuyere_derives_its_orientation_table_from_the_def() {
    // The BlockPipe base derives AllowedOrientations from the tuyere's own def; the variant group order
    // [s,n,w,e] carries through and its first state "s" is the fallback (no hand-written tables).
    var orientations = ExDefinitions.OrientationMap(
      BlockTuyere.Definitions("iiex")
    );
    Assert.Equal(["s", "n", "w", "e"], orientations["tuyere"]);
  }

  [Fact]
  public void Core_multiblock_covers_all_160_cells() {
    // A whole-footprint count: it catches a cell added or lost anywhere in the nine layers. The number
    // moves whenever the layout in BlockBlastFurnaceCoreCold is redrawn.
    JObject def = BlockBlastFurnaceCoreCold
      .Definitions("iiex")
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
    JObject def = BlockBurdenmaker.Definitions("iiex").First().ToJson();
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
  public void No_definition_names_a_code_from_a_mod_iiex_does_not_depend_on() {
    // iiex is the bottom content mod: its modinfo declares only exlib, so any smex:/hpex: code it
    // names is a block an iiex-only player can never obtain. On a construction stage that silently
    // walls off the machine - which is how this started, as a burdenmaker stage reaching up into the
    // steam mod for a part. Nothing rejects the code at load; the stage just never completes.
    string[] upstream = ["smex:", "hpex:", "iwex:", "lpex:", "ppex:"];
    var offenders = new List<string>();
    foreach (var def in DefinitionGoldens.Collect("iiex", Mod)) {
      string json = def.ToJson().ToString();
      foreach (string prefix in upstream)
        if (json.Contains(prefix))
          offenders.Add($"{def.Location}: names {prefix}");
    }

    Assert.True(offenders.Count == 0, string.Join("\n  ", offenders));
  }

  #endregion

  [Fact]
  public void Parity_catches_a_wrong_filler_port_face() {
    // Guards that the parity oracle compares per-cell behaviours: flipping one port's face on a clone of
    // the def's own output must fail parity. This is a harness self-test, so it can read any def that
    // hosts a per-cell port; it asserts that premise below rather than passing vacuously on a def with
    // no behaviours to mutate.
    JObject original = BlockTwinTubMPBlower
      .Definitions("iiex")
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

  /// <summary>
  /// The outlet is the fitting whose orientation set is widest (six faces), so it is the one where a
  /// hand-kept duplicate list would most likely fall out of step with the def.
  /// </summary>
  [Fact]
  public void Outlet_AllowedOrientations_is_derived_from_the_defs_variant_groups() {
    var outletOrientations = ExDefinitions.OrientationMap(
      BlockNetworkPipe.Blocks.BlockPipeOutlet.Definitions("iiex")
    );
    Assert.Equal(["s", "n", "w", "e", "u", "d"], outletOrientations["outlet"]);
  }

  /// <summary>
  /// A node's drop is its own first-listed state, which is deliberately not the scheme's first token.
  /// Declaring <c>ExOrientable</c> on every node must not change that: the behaviour's
  /// <c>CanonicalStack</c> answers <see cref="ExOrientations.Face"/>'s <c>n</c>, and pointing either
  /// drop site at it would silently move four shipped blocks onto a different item.
  /// </summary>
  [Fact]
  public void A_tuyere_carrying_the_behaviour_still_drops_and_picks_its_own_first_state() {
    var world = new TestWorld();
    BlockTuyere placed = Tuyere(world, "n", 1, withBehaviour: true);
    Tuyere(world, "s", 2, withBehaviour: true);
    var pos = new Vintagestory.API.MathTools.BlockPos(0, 0, 0);

    Assert.Equal(
      "iiex:furnace-tuyere-s",
      placed.GetDrops(world.World, pos, null)[0].Collectible.Code.ToString()
    );
    Assert.Equal(
      "iiex:furnace-tuyere-s",
      placed.OnPickBlock(world.World, pos).Collectible.Code.ToString()
    );
    // The premise, asserted rather than assumed: the behaviour is attached and does answer differently.
    Assert.Equal("n", ExOrientations.Face.Tokens[0]);
    Assert.Equal(
      "iiex:furnace-tuyere-n",
      placed
        .GetBehavior<BlockBehaviorExOrientable>()!
        .CanonicalStack(world.World)
        .Collectible.Code.ToString()
    );
  }

  /// <summary>One registered tuyere variant, optionally carrying the network-mode behaviour its def
  /// declares - in both behaviour arrays, since <c>GetBehavior</c> reads only one of them.</summary>
  private static BlockTuyere Tuyere(
    TestWorld world,
    string token,
    int id,
    bool withBehaviour
  ) {
    BlockTuyere block = TestBlocks.Configure(
      new BlockTuyere(),
      $"iiex:furnace-tuyere-{token}",
      id,
      ("type", "tuyere"),
      ("orientation", token)
    );
    ReflectionHelpers.SetProperty(
      block,
      nameof(BlockNetworkNode.Type),
      "tuyere"
    );

    if (withBehaviour) {
      var behaviour = new BlockBehaviorExOrientable(block);
      behaviour.Initialize(
        new Vintagestory.API.Datastructures.JsonObject(
          JToken.Parse("""{"mode":"network","scheme":"Face"}""")
        )
      );
      block.BlockBehaviors = [behaviour];
      block.CollectibleBehaviors = [behaviour];
    }

    world.Register(block);
    return block;
  }
}
