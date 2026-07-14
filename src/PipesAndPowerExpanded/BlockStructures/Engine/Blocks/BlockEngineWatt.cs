using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using PipesAndPowerExpanded.BlockStructures.Engine.BlockEntities;

namespace PipesAndPowerExpanded.BlockStructures.Engine.Blocks;

/// <summary>
/// The Watt engine mega-block (iron, low-pressure tier). No control rods. Repairs accept
/// iron or steel. All behavior lives in <see cref="BlockEngine"/>.
/// </summary>
[BlockRegister]
public partial class BlockEngineWatt
  : BlockEngine,
    IFillerHost,
    IEngineGeometry,
    IExBlockDefProvider
{
  /// <summary>The Watt engine blocktype, authored in C# (migrated from engine/watt.json) off the shared
  /// <see cref="BlockEngine.EngineShell"/>, adding only its six-stage construction table.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Watt(domain)];

  private static ExBlockDef Watt(string domain) =>
    EngineShell(
        ExBlockDef
          .Create(domain, "enginewatt", "engine/watt")
          .Class<BlockEngineWatt>()
          .EntityClass<BlockEntityEngineWatt>(),
        "ppex:engine/watt"
      )
      // The beam column reserved beside the engine, drawn as a front elevation of the x=0 plane (rows are
      // Y from 3 down to 0, columns are Z 0..2). The bottom row's gaps are the engine cell itself (z=0) and
      // the sub-machine cell (z=2). Authored per-engine because each engine's column differs.
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
            s.Require(
                "metalplate-*",
                2,
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
              .Require("game:burnedbrick-fire", 36)
              .AddElements("Root/BeamSupport")
          )
          .Stage(s =>
            s.Require(
                "rod-*",
                8,
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
              .AddElements("Root/Beam")
          )
          .Stage(s =>
            s.Require(
                "metalplate-*",
                2,
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
                2,
                "ppex:rcc-ingredient-nailsandstrips",
                storeWildCard: "metal",
                allowedVariants: ["iron", "steel"]
              )
              .AddElements("Root/Piston")
          )
          .Stage(s =>
            s.Require(
                "rod-*",
                4,
                "ppex:rcc-ingredient-rod",
                storeWildCard: "metal",
                allowedVariants: ["iron", "steel"]
              )
              .Require(
                "metalnailsandstrips-*",
                2,
                "ppex:rcc-ingredient-nailsandstrips",
                storeWildCard: "metal",
                allowedVariants: ["iron", "steel"]
              )
              .AddElements("Root/ControlPiston")
          )
          .Stage(s =>
            s.Require(
                "rod-*",
                4,
                "ppex:rcc-ingredient-rod",
                storeWildCard: "metal",
                allowedVariants: ["iron", "steel"]
              )
              .AddElements("Root/Rod")
          )
      );

  protected override RepairItem[] RepairItems =>
    [
      new(["metalplate-iron", "metalplate-steel"], 4, "iron/steel plate"),
      new(["rod-iron", "rod-steel"], 2, "iron/steel rod"),
    ];
}
