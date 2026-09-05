using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Products.BlockEntities;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The crucible floor, for tests. A shaft furnace pools into <c>iiex:hearthmetal-*</c> blocks it places
/// itself, so a rig that wants a pool has to have the block, its entity class and the entity's two molten
/// cells resolvable. The headless world builds block entities from a registered factory rather than from
/// a definition, so the cells are configured here exactly as the definition configures them - one place,
/// rather than once per suite.
/// </summary>
public static class HearthRig {
  /// <summary>A hearth block entity carrying the iron and slag cells its definition declares.</summary>
  public static BlockEntity NewHearth() {
    var be = new BlockEntityHearthMetal();
    foreach (
      string key in new[]
      {
        BlockEntityHearthMetal.IronCellKey,
        BlockEntityHearthMetal.SlagCellKey,
      }
    ) {
      var cell = new BEBehaviorMoltenCell(be);
      be.Behaviors.Add(cell);
      cell.ConfigureFromFiller(
        null,
        null,
        new JsonObject(
          JToken.Parse($$"""{ "key": "{{key}}", "solidifies": true }""")
        )
      );
    }
    return be;
  }

  /// <summary>Registers the hearth block for <paramref name="code"/> and the factory that gives it its
  /// cells, so a melt can place it and find somewhere to put metal.</summary>
  public static void Register(TestWorld world, string code, int blockId) {
    world.RegisterBlockEntityFactory("iiex.BlockEntityHearthMetal", NewHearth);
    Block block = TestBlocks.Configure(new Block(), code, blockId);
    block.EntityClass = "iiex.BlockEntityHearthMetal";
    world.Register(block);
  }

  /// <summary>Units held in the named cell across <paramref name="cells"/> - the whole crucible floor.</summary>
  public static int Pooled(
    TestWorld world,
    IReadOnlyList<BlockPos> cells,
    string cellKey
  ) {
    int total = 0;
    foreach (BlockPos pos in cells)
      total +=
        world.Accessor.GetBlockEntity(pos).MoltenCell(cellKey)?.CellAmount ?? 0;
    return total;
  }
}
