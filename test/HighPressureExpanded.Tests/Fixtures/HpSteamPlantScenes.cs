using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using HighPressureExpanded.BlockStructures.Engine.BlockEntities;
using HighPressureExpanded.BlockStructures.Engine.Blocks;
using IronIndustryExpanded.BlockStructures.Engine;
using IronIndustryExpanded.BlockStructures.Engine.BlockEntities;
using IronIndustryExpanded.BlockStructures.Engine.Blocks;
using IronIndustryExpanded.Tests;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using SmexAirBlowerBe = SteelmakingExpanded.BlockStructures.Engine.BlockEntities.BlockEntityEngineAirBlower;
using SmexAirBlowerBlock = SteelmakingExpanded.BlockStructures.Engine.Blocks.BlockEngineAirBlower;

namespace HighPressureExpanded.Tests;

/// <summary>
/// Whole-plant fixtures for the high-pressure Cornish engine driving each of its sub-machines.
/// Sub-machine wiring is identical whichever engine drives it, so these reuse iiex's
/// <see cref="EnginePlant"/> pipe/axis helper and <see cref="IiexScenes.Cap"/>; only the engine and its
/// pressure band differ.
/// </summary>
internal sealed class MPGeneratorPlant {
  public readonly BlockEntityEngineCornish Engine;
  public readonly BlockEntityEngineMPGenerator Generator;

  private readonly Scene _scene;
  private readonly BlockPos _inlet;

  /// <summary>
  /// Builds a constructed Cornish engine driving an MP-generator sub-machine. With steam in the
  /// engine's band it engages and delivers a mechanical-power budget
  /// (<see cref="BlockEntityEngine.MpPowerBudget"/>), the boiler to engine to MP-generator chain that
  /// powers the converter and helve hammers.
  /// </summary>
  public MPGeneratorPlant(Scene scene, BlockPos pos) {
    _scene = scene;

    var engineBlock = TestBlocks.Configure(
      new BlockEngineCornish(),
      "hpex:enginecornish-n",
      40,
      ("side", "north")
    );
    Engine = new BlockEntityEngineCornish {
      Pos = pos.Copy(),
      Block = engineBlock,
    };
    scene.Machine(pos, engineBlock, Engine);
    RccFake.Complete(Engine);

    BlockFacing inletFace = engineBlock.SteamInletFace;
    _inlet = pos.AddCopy(inletFace);
    // The Cornish band starts at 6 atm and a plated pipe bursts at 5, so the inlet runs in cast.
    EnginePlant.Pipe(
      scene,
      _inlet,
      EnginePlant.Axis(inletFace),
      41,
      material: "hadfield"
    );
    scene.Block(_inlet.AddCopy(inletFace), IiexScenes.Cap(42));

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

  public MPGeneratorPlant Steam(float atm) {
    _scene
      .NetworkAt<PipeNetwork>(_inlet)!
      .TryProduceGas(
        atm * 30f,
        150f,
        "Steam",
        _scene.World.Accessor,
        maxOutputPressure: atm
      );
    return this;
  }

  /// <summary>Holds the inlet at <paramref name="atm"/> for <paramref name="seconds"/> ticks (boiler stand-in).</summary>
  public MPGeneratorPlant RunWithSteam(float atm, int seconds) {
    for (int i = 0; i < seconds; i++) {
      Steam(atm);
      _scene.Step(1);
    }
    return this;
  }

  public float MpPowerBudget => Engine.MpPowerBudget;
  public float InletVolume =>
    _scene.NetworkAt<PipeNetwork>(_inlet)!.State?.Volume ?? 0f;
}

/// <summary>
/// A Cornish engine driving the smex air-blower sub-machine. With steam in the engine's band the blower
/// pressurises Air on its left network; above <c>SmexValues.BlastPressureThreshold</c> that air counts
/// as Blast, the chain that feeds the cowper stoves, blast-furnace tuyeres and Bessemer converter.
/// <para>
/// Lives in the hpex suite because it is the only fixture needing hpex and smex in one assembly and the
/// reference can only run this way: smex must not see hpex. The blower couples to the engine through
/// iiex's <see cref="BlockEntityEngine"/> contract alone, so there is no hpex-to-smex code edge.
/// </para>
/// </summary>
internal sealed class AirBlowerPlant {
  public readonly BlockEntityEngineCornish Engine;
  public readonly SmexAirBlowerBe Blower;

  private readonly Scene _scene;
  private readonly BlockPos _inlet;
  private readonly BlockPos _blast;

  public AirBlowerPlant(Scene scene, BlockPos pos) {
    _scene = scene;

    var engineBlock = TestBlocks.Configure(
      new BlockEngineCornish(),
      "hpex:enginecornish-n",
      45,
      ("side", "north")
    );
    Engine = new BlockEntityEngineCornish {
      Pos = pos.Copy(),
      Block = engineBlock,
    };
    scene.Machine(pos, engineBlock, Engine);
    RccFake.Complete(Engine);

    // The Cornish band starts at 6 atm and a plated pipe bursts at 5, so the inlet runs in cast.
    BlockFacing inletFace = engineBlock.SteamInletFace;
    _inlet = pos.AddCopy(inletFace);
    EnginePlant.Pipe(
      scene,
      _inlet,
      EnginePlant.Axis(inletFace),
      46,
      material: "hadfield"
    );
    scene.Block(_inlet.AddCopy(inletFace), IiexScenes.Cap(47));

    BlockPos subPos = engineBlock.SubmachinePos(pos);
    var blowerBlock = TestBlocks.Configure(
      new SmexAirBlowerBlock(),
      "smex:engineairblower-e",
      48,
      ("side", "east")
    );
    Blower = new SmexAirBlowerBe { Pos = subPos.Copy(), Block = blowerBlock };
    scene.Machine(subPos, blowerBlock, Blower);

    // Blast line: a sealed cast pipe on the blower's left face, along that axis, so the pressurised
    // air it pushes is held instead of leaking.
    BlockFacing leftFace = ExOrientation.RotateFacing(
      BlockFacing.WEST,
      ExOrientation.AngleFromSide("east")
    );
    _blast = subPos.AddCopy(leftFace);
    EnginePlant.Pipe(
      scene,
      _blast,
      EnginePlant.Axis(leftFace),
      49,
      material: "hadfield"
    );
    scene.Block(_blast.AddCopy(leftFace), IiexScenes.Cap(50));
  }

  public AirBlowerPlant Steam(float atm) {
    _scene
      .NetworkAt<PipeNetwork>(_inlet)!
      .TryProduceGas(
        atm * 30f,
        150f,
        "Steam",
        _scene.World.Accessor,
        maxOutputPressure: atm
      );
    return this;
  }

  /// <summary>Holds the inlet at <paramref name="atm"/> for <paramref name="seconds"/> ticks (boiler stand-in).</summary>
  public AirBlowerPlant RunWithSteam(float atm, int seconds) {
    for (int i = 0; i < seconds; i++) {
      Steam(atm);
      _scene.Step(1);
    }
    return this;
  }

  public PipeNetwork? BlastNet => _scene.NetworkAt<PipeNetwork>(_blast);
  public string BlastMedium => BlastNet?.State?.MediumType ?? "";
  public float BlastPressure => BlastNet?.State?.Pressure ?? 0f;
}
