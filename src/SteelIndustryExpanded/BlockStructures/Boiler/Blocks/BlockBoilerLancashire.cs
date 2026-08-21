using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded;
using IronIndustryExpanded.BlockStructures.Boiler;
using SteelIndustryExpanded.BlockStructures.Boiler.BlockEntities;
using Vintagestory.API.MathTools;

namespace SteelIndustryExpanded.BlockStructures.Boiler.Blocks;

/// <summary>
/// The Lancashire boiler mega-block (steel, high-pressure tier). All behavior lives in iiex's
/// <see cref="BlockBoiler"/>; siex only supplies this leaf's definition.
/// </summary>
[BlockRegister]
public partial class BlockBoilerLancashire
  : BlockBoiler,
    IFillerHost,
    IBoilerGeometry,
    IExBlockDefProvider {
  /// <summary>The Lancashire boiler blocktype: geometry offsets as attributes, filler footprint and
  /// structure map as ASCII layer diagrams, and the build sequence as a typed stage table.</summary>
  /// <summary>
  /// The fastener a pressure vessel is built with, and it is the iron tier's: a rivet is a rivet whatever
  /// the shell is made of, and siex mints none of its own. See docs/design/items/fasteners.md.
  /// </summary>
  private const string RivetCode =
    "iiex:" + IronIndustryExpanded.Items.FastenerItemDefinitions.RivetCode;

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Lancashire(domain)];

  private static ExBlockDef Lancashire(string domain) =>
    BoilerShell(
        ExBlockDef
          .Create(domain, "boilerlancashire", "boiler/lancashire")
          .Class<BlockBoilerLancashire>()
          .EntityClass<BlockEntityBoilerLancashire>(),
        "siex:boiler/lancashire"
      )
      .MiningTier(4)
      .Attributes(
        new {
          steamConnectorOffset = new {
            x = 0,
            y = 1,
            z = 4,
          },
          lidOffset = new {
            x = 0,
            y = 1,
            z = 1,
          },
          fuelOffset = new {
            x = 0,
            y = 0,
            z = -1,
          },
          explosionCenterOffset = new {
            x = 0,
            y = 1,
            z = 3,
          },
          lightSampleOffset = new {
            x = 0,
            y = 1,
            z = 3,
          },
          exhaustOutletOffset = new {
            x = 0,
            y = 1,
            z = 6,
          },
          waterRendererBox = new {
            x1 = -14,
            y1 = 2,
            z1 = 2,
            x2 = 30,
            y2 = 30,
            z2 = 94,
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
              # + #
              # + #
              """
            )
        )
      )
      // The firebox/flue masonry the boiler is walled into. The pipe fittings it seats on are iiex's
      // cast tier, so those legends keep the iiex domain.
      .MultiblockLayout(s =>
        s.Origin(-1, -2)
          .Legend('#', ExCodes.Filler)
          .Legend('L', SiexBlocks.BoilerLancashire.Any)
          .Legend('p', IiexCodes.PipePassthroughFire)
          .Legend('B', IiexCodes.PipePassthroughBendFireUp)
          .Legend('b', VanillaCodes.FireBricks)
          .Legend('a', VanillaCodes.Air)
          .Legend('c', VanillaCodes.CoalBed)
          .Legend('d', VanillaCodes.Sealing(BlockFacing.NORTH))
          .Legend('o', IiexCodes.PipeOutletFireUp)
          .Layer(
            1,
            """
            . b .
            . b .
            # # #
            # # #
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
            b b b
            b b b
            """
          )
      )
      .Construction(c =>
        c.Stage(s => s.AddElements("Root/Base"))
          .Stage(s =>
            s.Require("metalplate-steel", 10)
              .RequireRivets(domain, RivetCode, 16)
              .Require("game:burnedbrick-fire", 12)
              .AddElements("Root/BaseExtension")
          )
          .Stage(s =>
            s.Require("metalplate-steel", 8)
              .Require("rod-steel", 4)
              .RequireRivets(domain, RivetCode, 16)
              .AddElements("Root/Flues")
          )
          .Stage(s =>
            s.Require("metalplate-steel", 16)
              .RequireRivets(domain, RivetCode, 16)
              .Require("rod-steel", 6)
              .Require("game:burnedbrick-fire", 48)
              .AddElements("Root/Casing")
          )
      );
}
