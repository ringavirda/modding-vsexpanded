using ExpandedLib.Helpers;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Engine;
using IronIndustryExpanded.BlockStructures.Engine.BlockEntities;
using IronIndustryExpanded.BlockStructures.Engine.Blocks;
using IronIndustryExpanded.Tests;
using SiexAirBlowerBe = SteelIndustryExpanded.BlockStructures.Engine.BlockEntities.BlockEntityEngineAirBlower;
using SiexAirBlowerBlock = SteelIndustryExpanded.BlockStructures.Engine.Blocks.BlockEngineAirBlower;
using SteelIndustryExpanded.BlockStructures.Engine.BlockEntities;
using SteelIndustryExpanded.BlockStructures.Engine.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Whole-plant fixtures for the high-pressure Cornish engine driving each of its sub-machines.
/// Sub-machine wiring is identical whichever engine drives it, so these build on iiex's
/// <see cref="EnginePlant"/> base; only the engine and its pressure band differ.
/// </summary>
internal sealed class MPGeneratorPlant : EnginePlant {
  public readonly BlockEntityEngineMPGenerator Generator;

  /// <summary>
  /// Builds a constructed Cornish engine driving an MP-generator sub-machine. With steam in the
  /// engine's band it engages and delivers a mechanical-power budget
  /// (<see cref="BlockEntityEngine.MpPowerBudget"/>), the boiler to engine to MP-generator chain that
  /// powers the converter and helve hammers.
  /// </summary>
  public MPGeneratorPlant(Scene scene, BlockPos pos)
    // The Cornish band starts at 6 atm and a plated pipe bursts at 5, so the inlet runs in cast.
    : base(
      scene,
      pos,
      TestBlocks.Configure(
        new BlockEngineCornish(),
        "siex:enginecornish-n",
        40,
        ("side", "north")
      ),
      new BlockEntityEngineCornish { Pos = pos.Copy() },
      41,
      42,
      material: "hadfield"
    ) {
    var engineBlock = (BlockEngineCornish)Engine.Block;

    BlockPos subPos = engineBlock.SubmachinePos(pos);
    var genBlock = TestBlocks.Configure(
      new BlockEngineMPGenerator(),
      "iiex:enginempgenerator-e",
      43,
      ("side", "east")
    );
    Generator = new BlockEntityEngineMPGenerator {
      Pos = subPos.Copy(),
      Block = genBlock,
    };
    scene.Machine(subPos, genBlock, Generator);
  }

  public float MpPowerBudget => Engine.MpPowerBudget;
}

/// <summary>
/// A Cornish engine driving the air-blower sub-machine. With steam in the engine's band the blower
/// pressurises Air on its left network; above <c>SiexValues.BlastPressureThreshold</c> that air counts
/// as Blast, the chain that feeds the cowper stoves, blast-furnace tuyeres and Bessemer converter.
/// <para>
/// The blower couples to the engine through iiex's <see cref="BlockEntityEngine"/> contract alone,
/// so the two never name each other's types.
/// </para>
/// </summary>
internal sealed class AirBlowerPlant : EnginePlant {
  public readonly SiexAirBlowerBe Blower;

  private readonly BlockPos _blast;

  public AirBlowerPlant(Scene scene, BlockPos pos)
    // The Cornish band starts at 6 atm and a plated pipe bursts at 5, so the inlet runs in cast.
    : base(
      scene,
      pos,
      TestBlocks.Configure(
        new BlockEngineCornish(),
        "siex:enginecornish-n",
        45,
        ("side", "north")
      ),
      new BlockEntityEngineCornish { Pos = pos.Copy() },
      46,
      47,
      material: "hadfield"
    ) {
    var engineBlock = (BlockEngineCornish)Engine.Block;

    BlockPos subPos = engineBlock.SubmachinePos(pos);
    var blowerBlock = TestBlocks.Configure(
      new SiexAirBlowerBlock(),
      "siex:engineairblower-e",
      48,
      ("side", "east")
    );
    Blower = new SiexAirBlowerBe { Pos = subPos.Copy(), Block = blowerBlock };
    scene.Machine(subPos, blowerBlock, Blower);

    // Blast line: a sealed cast pipe on the blower's left face, along that axis, so the pressurised
    // air it pushes is held instead of leaking.
    BlockFacing leftFace = ExOrientation.RotateFacing(
      BlockFacing.WEST,
      ExOrientation.AngleFromSide("east")
    );
    _blast = subPos.AddCopy(leftFace);
    Pipe(scene, _blast, Axis(leftFace), 49, material: "hadfield");
    scene.Block(_blast.AddCopy(leftFace), IiexScenes.Cap(50));
  }

  public PipeNetwork? BlastNet => Scene.NetworkAt<PipeNetwork>(_blast);
  public string BlastMedium => BlastNet?.State?.MediumType ?? "";
  public float BlastPressure => BlastNet?.State?.Pressure ?? 0f;
}
