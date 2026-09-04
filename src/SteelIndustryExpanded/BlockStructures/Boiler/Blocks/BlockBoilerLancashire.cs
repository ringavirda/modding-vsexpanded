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
  /// <summary>The Lancashire boiler blocktype: geometry offsets as attributes, the filler footprint as
  /// ASCII layer diagrams, and the build sequence as a typed stage table.</summary>
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
        "siex:boiler/lancashire",
        // Zero, not BodySpinOffset, and the two leaves differ on purpose: this art is drawn along local
        // -z while the footprint below is authored along +z, so StructureAngle's own half turn is what
        // brings them together and the mesh must take none. Adding the offset here would move the mesh
        // six cells off its fillers. BoilerFootprintGuards asserts it at all four orientations.
        0
      )
      .MiningTier(4)
      // The masonry setting is what the pre-construction mesh shows.
      .ShapeSelectiveElements("Root/Base/*")
      .Attributes(
        new {
          steamConnectorOffset = new {
            x = 0,
            y = 1,
            z = 4,
          },
          manHatchOffset = new {
            x = 0,
            y = 1,
            z = 1,
          },
          fuelOffset = new {
            x = 0,
            y = 0,
            z = -1,
          },
          // Distinct cells on purpose: the blast is centred on the barrel's middle, the mesh is lit from
          // a body cell clear of both the fire at z -1 and the steam port at z 4. Sharing one cell makes
          // a later move of any of the three silently move the others.
          explosionCenterOffset = new {
            x = 0,
            y = 1,
            z = 3,
          },
          lightSampleOffset = new {
            x = 0,
            y = 1,
            z = 2,
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
            .Port('S', BlockFacing.UP, "pipe")
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
              # S #
              # + #
              """
            )
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
