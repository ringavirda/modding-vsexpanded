using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;
using ExpandedLib.Structures;
using IronIndustryExpanded.BlockStructures.Boiler.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces;
using Newtonsoft.Json.Linq;
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
        "iiex:boiler/cornish",
        // The barrel is drawn along local -z, the axis the footprint below is authored on, so the mesh
        // takes the same spin the footprint does.
        BodySpinOffset
      )
      .MiningTier(3)
      // The brick setting is the first thing raised, and all the pre-construction mesh shows.
      .ShapeSelectiveElements("MasonryBase/*")
      // Four courses of four units under the barrel, drawn as the art's own CoalLayers/L1..L4.
      .EntityBehavior<BEBehaviorFirebox>(
        new JObject {
          ["layers"] = 4,
          ["unitsPerLayer"] = 4,
          ["bedElement"] = "CoalLayers",
          ["layerPrefix"] = "L",
        }
      )
      .Attributes(
        new {
          feedwaterFace = "south",
          steamConnectorOffset = new {
            x = 0,
            y = 2,
            z = -3,
          },
          mainHatchOffset = new {
            x = 0,
            y = 1,
            z = 0,
          },
          manHatchOffset = new {
            x = 0,
            y = 2,
            z = -1,
          },
          fuelOffset = new {
            x = 0,
            y = 1,
            z = 0,
          },
          // The blast centre and the light sample are deliberately distinct cells, and distinct from
          // the steam port: pinning any two of them to one cell by coincidence is what made a later
          // geometry change move all three at once. See docs/design/machines/boiler-cornish.md.
          explosionCenterOffset = new {
            x = 0,
            y = 1,
            z = -2,
          },
          lightSampleOffset = new {
            x = 0,
            y = 1,
            z = -3,
          },
          exhaustOutletOffset = new {
            x = 1,
            y = 0,
            z = -5,
          },
          // The water surface is a flat quad, so only x and z are read (SurfaceRenderer.BuildQuad);
          // its height is driven in discrete steps by SurfaceLevel and the y pair records the barrel
          // interior the box was measured inside rather than feeding the mesh. The barrel spans
          // principal-relative voxels x -11..17, y 0..51, z -64..16, with the flues at x -1..17, y 9..39.
          waterRendererBox = new {
            x1 = -8,
            y1 = 4,
            z1 = -60,
            x2 = 14,
            y2 = 30,
            z2 = 12,
          },
        }
      )
      // 3 x 6 x 3 less the L3 corners: 40 fillers around the principal, which the DSL draws as its own
      // 'O' glyph and never fills. See workbench/machines.txt for the authored layout.
      .FillerOffsets(
        StructureFootprint.Layout(f =>
          f.Origin(-1, -5)
            .Slab('_', BlockFacing.DOWN)
            .Slab('M', BlockFacing.DOWN)
            .Solid('I')
            .Port('S', BlockFacing.UP, "pipe")
            .Port('E', BlockFacing.EAST, "pipe")
            .Layer(
              0,
              """
              # # E
              # # #
              # # #
              # # #
              # # #
              # O #
              """
            )
            .Layer(
              1,
              """
              # # #
              # # #
              # # #
              # # #
              # # #
              # I #
              """
            )
            .Layer(
              2,
              """
              . . .
              . _ .
              . S .
              . _ .
              . M .
              . _ .
              """
            )
        )
      )
      .Construction(c =>
        c.Stage(s => s.AddElements("MasonryBase"))
          .Stage(s =>
            s.RequireMetalPlate(domain, 6)
              .RequireRivets(domain, RivetCode, 8)
              .Require("game:burnedbrick-fire", 8)
              .AddElements("BoilerCasing", "CasingSegment5")
          )
          .Stage(s =>
            s.RequireMetalPlate(domain, 8)
              .RequireMetalRod(domain, 4)
              .RequireRivets(domain, RivetCode, 8)
              .AddElements("Flues", "CoalLayers")
          )
          .Stage(s =>
            s.RequireMetalPlate(domain, 8)
              .RequireRivets(domain, RivetCode, 16)
              .RequireMetalRod(domain, 4)
              .Require("game:burnedbrick-fire", 36)
              .AddElements("BoilerEnds", "MasonryTop")
          )
      );
}
