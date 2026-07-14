using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.BlastFurnace.Blocks;
using IronworkingExpanded.BlockStructures.OreBunker.Blocks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Parity oracle for iwex's code-first block definitions: each block's co-located
/// <see cref="IExBlockDefProvider.Define"/> must reproduce the hand-written blocktype JSON it replaced
/// byte-for-byte (semantically), so the injected block is indistinguishable from the file - same code,
/// class, textures, drops, everything - and no saved world sees a changed block. The golden is kept
/// inline (the source JSON was deleted at migration), so this is the durable record of what the block
/// must remain. Also pins that the shared discovery scan finds the def next to its class.
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
    JObject actual = BlockSolidifiedIron.Definitions("iwex").Single().ToJson();

    Assert.True(
      DefinitionParity.Equal(expected, actual, out string normalized),
      "solidifiediron code-first def diverged from the migrated JSON:\n" + normalized
    );
  }

  [Fact]
  public void SolidifiedIron_targets_the_iwex_blocktypes_asset_location()
  {
    var loc = BlockSolidifiedIron.Definitions("iwex").Single().Location;
    Assert.Equal("iwex", loc.Domain);
    Assert.Equal("blocktypes/solidifiediron.json", loc.Path);
  }

  [Fact]
  public void Discovery_finds_the_co_located_solidifiediron_def()
  {
    // The shared scan (also run by EntityRegistry.RegisterAll) must pick the def up straight off the
    // block class - no central list, no explicit registration call.
    ExDefinitions.Clear();
    ExDefinitions.DiscoverAndRegister(
      "iwex",
      typeof(BlockSolidifiedIron).Assembly
    );

    Assert.Contains(
      ExDefinitions.Blocks,
      d => d is { Code: "solidifiediron", Domain: "iwex" }
    );
  }

  // Verbatim copy of the former assets/iwex/blocktypes/ore/bunker.json - the first migrated MACHINE
  // (fillerOffsets computed from a footprint, RCC stages authored as a typed table).
  private const string BunkerGolden =
    """
    {
      "code": "bunker",
      "class": "iwex.BlockOreBunker",
      "entityClass": "iwex.BlockEntityOreBunker",
      "blockmaterial": "Ceramic",
      "requiredMiningTier": 0,
      "resistance": 3.5,
      "maxstacksize": 1,
      "drops": [],
      "attributes": {
        "fillerOffsets": [
          { "x": 1, "y": 0, "z": 0, "allowAttach": true },
          { "x": -1, "y": 0, "z": 0, "allowAttach": true },
          { "x": 0, "y": 0, "z": 1 },
          { "x": 1, "y": 0, "z": 1, "allowAttach": true },
          { "x": -1, "y": 0, "z": 1, "allowAttach": true },
          { "x": 0, "y": 0, "z": 2 },
          { "x": 1, "y": 0, "z": 2, "allowAttach": true },
          { "x": -1, "y": 0, "z": 2, "allowAttach": true },
          { "x": 0, "y": 0, "z": 3 },
          { "x": 1, "y": 0, "z": 3, "allowAttach": true },
          { "x": -1, "y": 0, "z": 3, "allowAttach": true },
          { "x": 0, "y": 0, "z": 4 },
          { "x": 1, "y": 0, "z": 4, "allowAttach": true },
          { "x": -1, "y": 0, "z": 4, "allowAttach": true },
          { "x": 0, "y": 0, "z": 5 },
          { "x": 1, "y": 0, "z": 5, "allowAttach": true },
          { "x": -1, "y": 0, "z": 5, "allowAttach": true }
        ]
      },
      "behaviors": [
        { "name": "HorizontalOrientable" },
        { "name": "BlockEntityInteract" }
      ],
      "entityBehaviors": [
        { "name": "Animatable" },
        {
          "name": "ExRightClickConstructable",
          "properties": {
            "stages": [
              { "addElements": ["Root/InputBase"] },
              {
                "requireStacks": [
                  { "type": "item", "code": "game:burnedbrick-{brick}", "name": "iwex:rcc-ingredient-brick", "quantity": 8 }
                ],
                "addElements": ["Root/Base"]
              },
              {
                "requireStacks": [
                  { "type": "item", "code": "game:burnedbrick-{brick}", "name": "iwex:rcc-ingredient-brick", "quantity": 24 }
                ],
                "addElements": ["Root/Walls"]
              }
            ]
          }
        }
      ],
      "variantgroups": [
        { "code": "brick", "states": ["black", "brown", "cream", "gray", "orange", "red", "tan"] },
        { "code": "side", "loadFromProperties": "abstract/horizontalorientation" }
      ],
      "creativeinventory": { "general": ["*-north"], "iwex": ["*-north"] },
      "shape": {
        "base": "iwex:ore/bunker",
        "rotateYByType": { "*-north": 180, "*-east": 90, "*-south": 0, "*-west": 270 },
        "selectiveElements": ["Root/InputBase/*"]
      },
      "textures": {
        "fire1": {
          "base": "game:block/clay/brick/four/running/cream1",
          "overlays": ["game:block/clay/brick/four/running/{brick}1"]
        }
      },
      "selectionbox": { "x1": 0, "y1": 0, "z1": 0, "x2": 1, "y2": 1, "z2": 1 },
      "collisionbox": { "x1": 0, "y1": 0, "z1": 0, "x2": 1, "y2": 1, "z2": 1 },
      "sidesolid": { "all": false },
      "sideopaque": { "all": false },
      "sounds": {
        "place": "game:block/ceramicplace",
        "break": "game:block/ceramic",
        "hit": "game:block/ceramic",
        "walk": "game:walk/stone"
      }
    }
    """;

  [Fact]
  public void Bunker_def_reproduces_the_migrated_json_exactly()
  {
    JObject expected = JObject.Parse(BunkerGolden);
    JObject actual = BlockOreBunker.Definitions("iwex").Single().ToJson();

    Assert.True(
      DefinitionParity.Equal(expected, actual, out string normalized),
      "bunker code-first def diverged from the migrated JSON:\n" + normalized
    );
  }

  [Fact]
  public void Bunker_targets_the_iwex_ore_bunker_asset_location()
  {
    var loc = BlockOreBunker.Definitions("iwex").Single().Location;
    Assert.Equal("iwex", loc.Domain);
    Assert.Equal("blocktypes/ore/bunker.json", loc.Path);
  }

  [Fact]
  public void Discovery_finds_the_co_located_bunker_def()
  {
    ExDefinitions.Clear();
    ExDefinitions.DiscoverAndRegister("iwex", typeof(BlockOreBunker).Assembly);
    Assert.Contains(
      ExDefinitions.Blocks,
      d => d is { Code: "bunker", Domain: "iwex" }
    );
  }
}
