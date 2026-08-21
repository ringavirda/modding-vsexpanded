using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Boiler.BlockEntities;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Boiler.Blocks;

/// <summary>
/// The Cornish boiler mega-block (iron, low-pressure entry tier). Behavior lives in
/// <see cref="BlockBoiler"/>.
/// </summary>
[BlockRegister]
public partial class BlockBoilerCornish
  : BlockBoiler,
    IFillerHost,
    IBoilerGeometry,
    IExBlockDefProvider {
  /// <summary>The Cornish boiler blocktype. Shorter body than the Lancashire.</summary>
  /// <summary>
  /// The fastener a pressure vessel is built with. A rivet makes a joint that is strong and tight; a nail
  /// is strong and not tight, so a boiler takes rivets and nothing else - the one place in the suite where
  /// the fastener is a gate rather than a substitution. See docs/design/items/fasteners.md.
  /// </summary>
  private const string RivetCode =
    "iiex:" + Items.FastenerItemDefinitions.RivetCode;

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Cornish(domain)];

  private static ExBlockDef Cornish(string domain) =>
    BoilerShell(
        ExBlockDef
          .Create(domain, "boilercornish", "boiler/cornish")
          .Class<BlockBoilerCornish>()
          .EntityClass<BlockEntityBoilerCornish>(),
        "iiex:boiler/cornish"
      )
      .MiningTier(3)
      .Attributes(
        new {
          steamConnectorOffset = new {
            x = 0,
            y = 1,
            z = 2,
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
            z = 2,
          },
          lightSampleOffset = new {
            x = 0,
            y = 1,
            z = 2,
          },
          exhaustOutletOffset = new {
            x = 0,
            y = 1,
            z = 4,
          },
          waterRendererBox = new {
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
          .Legend('#', ExCodes.Filler)
          .Legend('L', IiexBlocks.BoilerCornish.Any)
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
            s.RequireMetalPlate(domain, 6)
              .RequireRivets(domain, RivetCode, 8)
              .Require("game:burnedbrick-fire", 8)
              .AddElements("Root/BaseExtension")
          )
          .Stage(s =>
            s.RequireMetalPlate(domain, 8)
              .RequireMetalRod(domain, 4)
              .RequireRivets(domain, RivetCode, 8)
              .AddElements("Root/Flues")
          )
          .Stage(s =>
            s.RequireMetalPlate(domain, 8)
              .RequireRivets(domain, RivetCode, 16)
              .RequireMetalRod(domain, 4)
              .Require("game:burnedbrick-fire", 36)
              .AddElements("Root/Casing")
          )
      );
}
