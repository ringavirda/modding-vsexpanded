using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using HighPressureExpanded.BlockStructures.Boiler.BlockEntities;
using LowPressureExpanded;
using LowPressureExpanded.BlockStructures.Boiler;

using Vintagestory.API.MathTools;

namespace HighPressureExpanded.BlockStructures.Boiler.Blocks;

/// <summary>
/// The Lancashire boiler mega-block (steel, high-pressure tier). All behavior lives in lpex's
/// <see cref="BlockBoiler"/>; hpex only supplies this leaf's definition.
/// </summary>
[BlockRegister]
public partial class BlockBoilerLancashire
  : BlockBoiler,
    IFillerHost,
    IBoilerGeometry,
    IExBlockDefProvider
{
  /// <summary>The Lancashire boiler blocktype, authored in C# (migrated from boiler/lancashire.json). Its
  /// filler footprint and structure map are drawn as ASCII layer diagrams; the geometry offsets are plain
  /// attributes; the build sequence is a typed stage table.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Lancashire(domain)];

  private static ExBlockDef Lancashire(string domain) =>
    BoilerShell(
        ExBlockDef
          .Create(domain, "boilerlancashire", "boiler/lancashire")
          .Class<BlockBoilerLancashire>()
          .EntityClass<BlockEntityBoilerLancashire>(),
        "hpex:boiler/lancashire"
      )
      .MiningTier(4)
      .Attributes(
        new
        {
          steamConnectorOffset = new { x = 0, y = 1, z = 4 },
          lidOffset = new { x = 0, y = 1, z = 1 },
          fuelOffset = new { x = 0, y = 0, z = -1 },
          explosionCenterOffset = new { x = 0, y = 1, z = 3 },
          lightSampleOffset = new { x = 0, y = 1, z = 3 },
          exhaustOutletOffset = new { x = 0, y = 1, z = 6 },
          waterRendererBox = new
          {
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
      // The firebox/flue masonry the boiler is walled into. The pipe fittings it seats on are lpex's
      // cast tier (hpex depends on lpex), so those legends keep the lpex domain.
      .MultiblockLayout(s =>
        s.Origin(-1, -2)
          .Legend('#', ExCodes.Filler)
          .Legend('L', HpexBlocks.BoilerLancashire.Any)
          .Legend('p', LpexCodes.PipePassthroughFire)
          .Legend('B', LpexCodes.PipePassthroughBendFireUp)
          .Legend('b', VanillaCodes.FireBricks)
          .Legend('a', VanillaCodes.Air)
          .Legend('c', VanillaCodes.CoalBed)
          .Legend('d', VanillaCodes.Sealing(BlockFacing.NORTH))
          .Legend('o', LpexCodes.PipeOutletFireUp)
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
              .RequireMetalNails(domain, 8)
              .Require("game:burnedbrick-fire", 12)
              .AddElements("Root/BaseExtension")
          )
          .Stage(s =>
            s.Require("metalplate-steel", 8)
              .Require("rod-steel", 4)
              .RequireMetalNails(domain, 8)
              .AddElements("Root/Flues")
          )
          .Stage(s =>
            s.Require("metalplate-steel", 16)
              .RequireMetalNails(domain, 8)
              .Require("rod-steel", 6)
              .Require("game:burnedbrick-fire", 48)
              .AddElements("Root/Casing")
          )
      );
}
