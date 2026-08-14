using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Engine.BlockEntities;

namespace IronIndustryExpanded.BlockStructures.Engine.Blocks;

/// <summary>
/// The Watt engine mega-block (iron, low-pressure tier). No control rods. Repairs accept
/// iron or steel. All behavior lives in <see cref="BlockEngine"/>.
/// </summary>
[BlockRegister]
public partial class BlockEngineWatt
  : BlockEngine,
    IFillerHost,
    IEngineGeometry,
    IExBlockDefProvider {
  /// <summary>The Watt engine blocktype, built on the shared <see cref="BlockEngine.EngineShell"/> and
  /// adding its six-stage construction table.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Watt(domain)];

  private static ExBlockDef Watt(string domain) =>
    EngineShell(
        ExBlockDef
          .Create(domain, "enginewatt", "engine/watt")
          .Class<BlockEngineWatt>()
          .EntityClass<BlockEntityEngineWatt>(),
        "iiex:engine/watt"
      )
      // The beam column reserved beside the engine, drawn as a front elevation of the x=0 plane (rows are
      // Y from 3 down to 0, columns are Z 0..2). The bottom row's gaps are the engine cell (z=0) and the
      // sub-machine cell (z=2). Authored per engine because each engine's column differs.
      .FillerOffsets(
        StructureFootprint.Layout(f =>
          f.Origin(0, 3)
            .Slice(
              0,
              """
              # # #
              # # #
              # # #
              O # .
              """
            )
        )
      )
      .Construction(c =>
        c.Stage(s => s.AddElements("Root/Cylinder"))
          .Stage(s =>
            s.RequireMetalPlate(domain, 2)
              .RequireMetalRod(domain, 4)
              .RequireMetalNails(domain, 4)
              .Require("game:burnedbrick-fire", 36)
              .AddElements("Root/BeamSupport")
          )
          .Stage(s =>
            s.RequireMetalRod(domain, 8)
              .RequireMetalNails(domain, 4)
              .AddElements("Root/Beam")
          )
          .Stage(s =>
            s.RequireMetalPlate(domain, 2)
              .RequireMetalRod(domain, 4)
              .RequireMetalNails(domain, 2)
              .AddElements("Root/Piston")
          )
          .Stage(s =>
            s.RequireMetalRod(domain, 4)
              .RequireMetalNails(domain, 2)
              .AddElements("Root/ControlPiston")
          )
          .Stage(s => s.RequireMetalRod(domain, 4).AddElements("Root/Rod"))
      );

  protected override RepairItem[] RepairItems =>
    [
      new(["metalplate-iron", "metalplate-steel"], 4, "iron/steel plate"),
      new(["rod-iron", "rod-steel"], 2, "iron/steel rod"),
    ];
}
