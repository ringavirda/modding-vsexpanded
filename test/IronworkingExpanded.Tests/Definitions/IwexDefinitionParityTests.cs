using System.Linq;
using ExpandedLib.Definitions;
using IronworkingExpanded.Definitions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Parity oracle for iwex's code-first block definitions: each authored <see cref="ExBlockDef"/> must
/// reproduce the hand-written blocktype JSON it replaced byte-for-byte (semantically), so the injected
/// block is indistinguishable from the file - same code, class, textures, drops, everything - and no
/// saved world sees a changed block. The golden is kept inline (the source JSON was deleted at
/// migration), so this is the durable record of what the block must remain.
/// </summary>
public class IwexDefinitionParityTests
{
  // Verbatim copy of the former assets/iwex/blocktypes/blastfurnace/solidifiediron.json.
  private const string SolidifiedIronGolden =
    """
    {
      "code": "solidifiediron",
      "class": "iwex.BlockSolidifiedIron",
      "entityClass": "iwex.BlockEntitySolidifiedIron",
      "blockmaterial": "Metal",
      "creativeinventory": { "general": ["*"], "iwex": ["*"] },
      "shape": { "base": "game:block/basic/cube" },
      "textures": { "all": { "base": "game:block/metal/sheet-plain/iron5" } },
      "resistance": 45.0,
      "maxstacksize": 8,
      "requiredMiningTier": 5,
      "mineTool": "pickaxe",
      "sounds": {
        "place": "game:block/anvil",
        "break": "game:block/anvil",
        "hit": "game:block/anvil",
        "walk": "game:walk/stone"
      }
    }
    """;

  [Fact]
  public void SolidifiedIron_def_reproduces_the_migrated_json_exactly()
  {
    JObject expected = JObject.Parse(SolidifiedIronGolden);
    JObject actual = IwexDefinitions.SolidifiedIron().ToJson();

    Assert.True(
      JToken.DeepEquals(expected, actual),
      "solidifiediron code-first def diverged from the migrated JSON:\n" + actual
    );
  }

  [Fact]
  public void SolidifiedIron_targets_the_iwex_blocktypes_asset_location()
  {
    var loc = IwexDefinitions.SolidifiedIron().Location;
    Assert.Equal("iwex", loc.Domain);
    Assert.Equal("blocktypes/solidifiediron.json", loc.Path);
  }

  [Fact]
  public void RegisterAll_registers_the_solidifiediron_def_for_injection()
  {
    ExDefinitions.Clear();
    IwexDefinitions.RegisterAll();

    ExBlockDef def = Assert.Single(ExDefinitions.Blocks);
    Assert.Equal("solidifiediron", def.Code);
    Assert.Equal("iwex", def.Domain);
  }
}
