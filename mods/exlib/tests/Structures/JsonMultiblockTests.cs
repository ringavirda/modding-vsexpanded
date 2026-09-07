using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// A modder with no C# gets a megablock from a blocktype JSON alone: <see cref="BlockFilledMegastructure"/>
/// registered as <c>ExFilledMegastructure</c>, <see cref="BlockEntityMultiblock"/> as <c>ExMultiblock</c>,
/// and an <c>attributes.multiblockLayout</c> ASCII grid that <see cref="JsonMultiblockLayout"/> resolves
/// into the same <c>multiblockStructure</c>/<c>fillerOffsets</c> a code-first definition would emit.
/// </summary>
public class JsonMultiblockTests {
  private static TestWorld NewWorld() {
    var world = new TestWorld();
    world.Api.Logger.Returns(Substitute.For<ILogger>());
    return world;
  }

  [Fact]
  public void A_json_only_layout_gets_fillers_and_completion_with_no_C_sharp() {
    TestWorld world = NewWorld();

    // A 2x1x1 layout: the placed block at (0,0,0) plus one brick cell at (1,0,0), declared with
    // nothing but the block's own JSON attributes.
    var anchor = TestBlocks.Configure(
      new BlockFilledMegastructure(),
      "exlib:testmega-n",
      1
    );
    anchor.Attributes = new JsonObject(
      JToken.Parse(
        """
        {
          "multiblockLayout": {
            "legend": { "C": "exlib:testmega-n", "B": "exlib:testbrick*" },
            "layers": [["CB"]]
          }
        }
        """
      )
    );
    anchor.OnLoaded(world.Api);

    var be = new BlockEntityMultiblock();
    var pos = new BlockPos(0, 10, 0);
    world.Place(pos, anchor, be);
    world.Initialize(be);

    world.AdvanceBlockEntityTime(3000);
    Assert.False(be.StructureComplete);

    var brick = TestBlocks.Configure(new Block(), "exlib:testbrick", 2);
    world.Place(pos.AddCopy(1, 0, 0), brick);

    world.AdvanceBlockEntityTime(3000);
    Assert.True(be.StructureComplete);
  }

  [Fact]
  public void GetIncompleteMessage_falls_back_to_the_exlib_key_when_the_domain_declares_none() {
    // TestLang echoes every key back as true, so the fallback branch is exercised by restubbing the
    // one domain key this test cares about to "not declared".
    string domainKey = "exlib:multiblock-testmega-n-incomplete";
    TestLang.Service.HasTranslation(domainKey, Arg.Any<bool>()).Returns(false);
    TestLang
      .Service.HasTranslation(domainKey, Arg.Any<bool>(), Arg.Any<bool>())
      .Returns(false);

    var be = new BlockEntityMultiblock {
      Block = TestBlocks.Configure(new Block(), "exlib:testmega-n", 1),
    };

    string message = be.IncompleteMessageForTest(2);
    Assert.Equal("exlib:multiblock-incomplete", message);
  }

  [Fact]
  public void A_malformed_layout_logs_one_error_and_never_completes() {
    TestWorld world = NewWorld();
    var logger = Substitute.For<ILogger>();
    world.Api.Logger.Returns(logger);

    var anchor = TestBlocks.Configure(
      new BlockFilledMegastructure(),
      "exlib:badmega-n",
      3
    );
    // 'Q' is drawn but never given a Legend entry: MultiblockLayoutBuilder.Build() throws.
    anchor.Attributes = new JsonObject(
      JToken.Parse(
        """{ "multiblockLayout": { "legend": { "C": "exlib:badmega-n" }, "layers": [["Q"]] } }"""
      )
    );

    anchor.OnLoaded(world.Api);
    logger.Received(1).Error(Arg.Any<string>(), Arg.Any<object[]>());
    Assert.False(anchor.Attributes["multiblockStructure"].Exists);

    var be = new BlockEntityMultiblock();
    var pos = new BlockPos(2, 10, 0);
    world.Place(pos, anchor, be);
    world.Initialize(be);

    world.AdvanceBlockEntityTime(3000);
    Assert.False(be.StructureComplete);

    // Loading it again (a second block entity of the same singleton) does not re-log the failure.
    anchor.OnLoaded(world.Api);
    logger.Received(1).Error(Arg.Any<string>(), Arg.Any<object[]>());
  }

  [Fact]
  public void FillerOffsets_derived_from_the_layout_matches_the_C_sharp_derivation() {
    TestWorld world = NewWorld();

    var block = TestBlocks.Configure(
      new BlockFilledMegastructure(),
      "exlib:ringmega-n",
      4
    );
    block.Attributes = new JsonObject(
      JToken.Parse(
        """
        {
          "multiblockLayout": {
            "origin": [-1, -1],
            "legend": { "C": "exlib:ringmega-n", "B": "exlib:testbrick*" },
            "layers": [["BBB", "BCB", "BBB"]],
            "core": "C"
          }
        }
        """
      )
    );
    block.OnLoaded(world.Api);

    JObject structure = new MultiblockLayoutBuilder()
      .Origin(-1, -1)
      .Legend('C', "exlib:ringmega-n")
      .Legend('B', "exlib:testbrick*")
      .Core('C')
      .Layer(0, "BBB\nBCB\nBBB")
      .Build();

    var expectedCells = new List<FillerCellSpec>();
    foreach (JToken offset in (JArray)structure["offsets"]!) {
      int x = (int)offset["x"]!;
      int y = (int)offset["y"]!;
      int z = (int)offset["z"]!;
      if (x == 0 && y == 0 && z == 0)
        continue;
      expectedCells.Add(new FillerCellSpec(x, y, z));
    }
    JArray expected = ExBlockDef.SerializeFillerCells(expectedCells);

    Assert.True(
      JToken.DeepEquals(expected, block.Attributes["fillerOffsets"].Token)
    );
  }

  [Fact]
  public void The_two_JSON_only_classes_register_under_their_bare_names() {
    Assert.Equal(
      "ExFilledMegastructure",
      EntityRegistry.KeyFor("exlib", typeof(BlockFilledMegastructure))
    );
    Assert.Equal(
      "ExMultiblock",
      EntityRegistry.KeyFor("exlib", typeof(BlockEntityMultiblock))
    );
  }
}
