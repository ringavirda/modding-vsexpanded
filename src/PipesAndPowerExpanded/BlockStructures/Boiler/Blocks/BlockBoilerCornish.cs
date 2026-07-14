using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using PipesAndPowerExpanded.BlockStructures.Boiler.BlockEntities;

namespace PipesAndPowerExpanded.BlockStructures.Boiler.Blocks;

/// <summary>
/// The Cornish boiler mega-block (iron, low-pressure entry tier). All behavior lives
/// in <see cref="BlockBoiler"/>.
/// </summary>
[BlockRegister]
public partial class BlockBoilerCornish
  : BlockBoiler,
    IFillerHost,
    IBoilerGeometry,
    IExBlockDefProvider
{
  /// <summary>The Cornish boiler blocktype, authored in C# (migrated from boiler/cornish.json). Shorter body
  /// than the Lancashire; its filler footprint and structure map are drawn as ASCII layer diagrams.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Cornish(domain)];

  private static ExBlockDef Cornish(string domain) =>
    BoilerShell(
        ExBlockDef
          .Create(domain, "boilercornish", "boiler/cornish")
          .Class<BlockBoilerCornish>()
          .EntityClass<BlockEntityBoilerCornish>(),
        "ppex:boiler/cornish"
      )
      .MiningTier(3)
      .Attributes(
        new
        {
          steamConnectorOffset = new { x = 0, y = 1, z = 2 },
          lidOffset = new { x = 0, y = 1, z = 1 },
          fuelOffset = new { x = 0, y = 0, z = -1 },
          explosionCenterOffset = new { x = 0, y = 1, z = 2 },
          lightSampleOffset = new { x = 0, y = 1, z = 2 },
          exhaustOutletOffset = new { x = 0, y = 1, z = 4 },
          waterRendererBox = new
          {
            x1 = -14,
            y1 = 2,
            z1 = 2,
            x2 = 30,
            y2 = 30,
            z2 = 62,
          },
        }
      )
      .FillerOffsets(
        StructureFootprint.Layout(f =>
          f.Origin(-1, 0)
            .Layer(
              0,
              """
              + O +
              + + +
              + + +
              + + +
              """
            )
            .Layer(
              1,
              """
              # + #
              # + #
              # + #
              # + #
              """
            )
        )
      )
      .MultiblockLayout(s =>
        s.Origin(-1, -2)
          .Legend('#', "exlib:structurefiller")
          .Legend('L', "ppex:boilercornish*")
          .Legend('p', "ppex:pipe-passthrough-fire-*")
          .Legend('B', "ppex:pipe-passthroughbend-fire-u*")
          .Legend('b', "game:claybricks-good-fire")
          .Legend('a', "game:air*")
          .Legend('c', "@(air|coalpile)")
          .Legend('d', "game:cokeovendoor*")
          .Legend('o', "ppex:pipe-outlet-fire-u")
          .Layer(
            1,
            """
            . b .
            . b .
            # # #
            # # #
            # # #
            # # #
            . o .
            . b .
            """
          )
          .Layer(
            0,
            """
            b d b
            b c b
            # L #
            # # #
            # # #
            # # #
            b a b
            b b b
            """
          )
          .Layer(
            -1,
            """
            b p b
            b p b
            b B b
            b b b
            b b b
            b b b
            b b b
            b b b
            """
          )
      )
      .Construction(c =>
        c.Stage(s => s.AddElements("Root/Base"))
          .Stage(s =>
            s.Require(
                "metalplate-*",
                6,
                "ppex:rcc-ingredient-metalplate",
                storeWildCard: "metal",
                allowedVariants: ["iron", "steel"]
              )
              .Require(
                "metalnailsandstrips-*",
                4,
                "ppex:rcc-ingredient-nailsandstrips",
                storeWildCard: "metal",
                allowedVariants: ["iron", "steel"]
              )
              .Require("game:burnedbrick-fire", 8)
              .AddElements("Root/BaseExtension")
          )
          .Stage(s =>
            s.Require(
                "metalplate-*",
                8,
                "ppex:rcc-ingredient-metalplate",
                storeWildCard: "metal",
                allowedVariants: ["iron", "steel"]
              )
              .Require(
                "rod-*",
                4,
                "ppex:rcc-ingredient-rod",
                storeWildCard: "metal",
                allowedVariants: ["iron", "steel"]
              )
              .Require(
                "metalnailsandstrips-*",
                4,
                "ppex:rcc-ingredient-nailsandstrips",
                storeWildCard: "metal",
                allowedVariants: ["iron", "steel"]
              )
              .AddElements("Root/Flues")
          )
          .Stage(s =>
            s.Require(
                "metalplate-*",
                8,
                "ppex:rcc-ingredient-metalplate",
                storeWildCard: "metal",
                allowedVariants: ["iron", "steel"]
              )
              .Require(
                "metalnailsandstrips-*",
                8,
                "ppex:rcc-ingredient-nailsandstrips",
                storeWildCard: "metal",
                allowedVariants: ["iron", "steel"]
              )
              .Require(
                "rod-*",
                4,
                "ppex:rcc-ingredient-rod",
                storeWildCard: "metal",
                allowedVariants: ["iron", "steel"]
              )
              .Require("game:burnedbrick-fire", 36)
              .AddElements("Root/Casing")
          )
      );
}
