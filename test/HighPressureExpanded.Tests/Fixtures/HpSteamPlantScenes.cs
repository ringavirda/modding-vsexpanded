using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using HighPressureExpanded.BlockStructures.Engine.BlockEntities;
using HighPressureExpanded.BlockStructures.Engine.Blocks;
using LowPressureExpanded.BlockStructures.Engine;
using LowPressureExpanded.BlockStructures.Engine.BlockEntities;
using LowPressureExpanded.BlockStructures.Engine.Blocks;
using LowPressureExpanded.Tests;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using SmexAirBlowerBe = SteelmakingExpanded.BlockStructures.Engine.BlockEntities.BlockEntityEngineAirBlower;
using SmexAirBlowerBlock = SteelmakingExpanded.BlockStructures.Engine.Blocks.BlockEngineAirBlower;

namespace HighPressureExpanded.Tests;

/// <summary>
/// Whole-plant fixtures for the high-pressure Cornish engine driving each of its sub-machines. They
/// reuse lpex's <see cref="EnginePlant"/> pipe/axis helper and <see cref="LpexScenes.Cap"/> - the
/// sub-machine wiring is identical whichever engine drives it; only the engine and its pressure band
/// differ.
/// </summary>
internal sealed class MPGeneratorPlant
{
  public readonly BlockEntityEngineCornish Engine;
  public readonly BlockEntityEngineMPGenerator Generator;

  private readonly Scene _scene;
  private readonly BlockPos _inlet;

  /// <summary>
  /// Models the improved steel setup's MP half: a constructed Cornish engine driving an MP-generator
  /// sub-machine. With steam in its band the engine engages and delivers a mechanical-power budget
  /// (<see cref="BlockEntityEngine.MpPowerBudget"/>) - the boiler→engine→MP-generator chain that powers
  /// the converter / helve hammers.
  /// </summary>
  public MPGeneratorPlant(Scene scene, BlockPos pos)
  {
    _scene = scene;

    var engineBlock = TestBlocks.Configure(
      new BlockEngineCornish(),
      "hpex:enginecornish-north",
      40,
      ("side", "north")
    );
    Engine = new BlockEntityEngineCornish
    {
      Pos = pos.Copy(),
      Block = engineBlock,
    };
    scene.Machine(pos, engineBlock, Engine);
    RccFake.Complete(Engine);

    BlockFacing inletFace = engineBlock.SteamInletFace;
    _inlet = pos.AddCopy(inletFace);
    // The Cornish engine's band is high (≥6 atm) - a bolted pipe bursts at 5, so feed it through cast.
    EnginePlant.Pipe(
      scene,
      _inlet,
      EnginePlant.Axis(inletFace),
      41,
      material: "hadfield"
    );
    scene.Block(_inlet.AddCopy(inletFace), LpexScenes.Cap(42));

    BlockPos subPos = engineBlock.SubmachinePos(pos);
    var genBlock = TestBlocks.Configure(
      new BlockEngineMPGenerator(),
      "lpex:enginempgenerator-east",
      43,
      ("side", "east")
    );
    Generator = new BlockEntityEngineMPGenerator
    {
      Pos = subPos.Copy(),
      Block = genBlock,
    };
    scene.Machine(subPos, genBlock, Generator);
  }

  public MPGeneratorPlant Steam(float atm)
  {
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
  public MPGeneratorPlant RunWithSteam(float atm, int seconds)
  {
    for (int i = 0; i < seconds; i++)
    {
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
/// Models the hot-blast supply's air half: a Cornish engine driving the smex air-blower sub-machine.
/// With steam in the engine's band the blower pressurises Air on its left network; once that air
/// crosses the blast threshold (<c>SmexValues.BlastPressureThreshold</c>) it counts as Blast - the
/// boiler→engine→blower→blast chain that feeds the cowper stoves, blast-furnace tuyeres and the
/// Bessemer converter.
/// <para>
/// This is the one fixture that needs hpex and smex in the same assembly. It lives here rather than in
/// the smex suite because the reference can only run this way: smex must never see hpex, while hpex
/// already depends on smex in the shipped chain. The blower itself bolts onto the engine purely
/// through lpex's <see cref="BlockEntityEngine"/> base contract - there is no hpex↔smex code edge.
/// </para>
/// </summary>
internal sealed class AirBlowerPlant
{
  public readonly BlockEntityEngineCornish Engine;
  public readonly SmexAirBlowerBe Blower;

  private readonly Scene _scene;
  private readonly BlockPos _inlet;
  private readonly BlockPos _blast;

  public AirBlowerPlant(Scene scene, BlockPos pos)
  {
    _scene = scene;

    var engineBlock = TestBlocks.Configure(
      new BlockEngineCornish(),
      "hpex:enginecornish-north",
      45,
      ("side", "north")
    );
    Engine = new BlockEntityEngineCornish
    {
      Pos = pos.Copy(),
      Block = engineBlock,
    };
    scene.Machine(pos, engineBlock, Engine);
    RccFake.Complete(Engine);

    // The Cornish band is high (≥6 atm) - feed the inlet through cast (bolted bursts at 5).
    BlockFacing inletFace = engineBlock.SteamInletFace;
    _inlet = pos.AddCopy(inletFace);
    EnginePlant.Pipe(
      scene,
      _inlet,
      EnginePlant.Axis(inletFace),
      46,
      material: "hadfield"
    );
    scene.Block(_inlet.AddCopy(inletFace), LpexScenes.Cap(47));

    BlockPos subPos = engineBlock.SubmachinePos(pos);
    var blowerBlock = TestBlocks.Configure(
      new SmexAirBlowerBlock(),
      "smex:engineairblower-east",
      48,
      ("side", "east")
    );
    Blower = new SmexAirBlowerBe { Pos = subPos.Copy(), Block = blowerBlock };
    scene.Machine(subPos, blowerBlock, Blower);

    // Blast line: a sealed cast pipe on the blower's left face, oriented along that axis, so the
    // pressurised air it pushes is held instead of leaking.
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
    scene.Block(_blast.AddCopy(leftFace), LpexScenes.Cap(50));
  }

  public AirBlowerPlant Steam(float atm)
  {
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
  public AirBlowerPlant RunWithSteam(float atm, int seconds)
  {
    for (int i = 0; i < seconds; i++)
    {
      Steam(atm);
      _scene.Step(1);
    }
    return this;
  }

  public PipeNetwork? BlastNet => _scene.NetworkAt<PipeNetwork>(_blast);
  public string BlastMedium => BlastNet?.State?.MediumType ?? "";
  public float BlastPressure => BlastNet?.State?.Pressure ?? 0f;
}
