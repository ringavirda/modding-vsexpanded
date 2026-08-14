using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.Tests;
using IronIndustryExpanded.BlockNetworkPipe.BlockEntities;
using IronIndustryExpanded.BlockNetworkPipe.Blocks;
using IronIndustryExpanded.BlockStructures.Engine.BlockEntities;
using IronIndustryExpanded.BlockStructures.Engine.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Whole-plant fixtures that stand a steam engine up with its real peripherals in a shared
/// <see cref="Scene"/>, so scenario tests can model the in-game setups end to end: steam in, power,
/// work out. Shared helpers for the engine and its sealed steam inlet; the plants below add the
/// sub-machine and its lines. Public because the hpex plants build on the same helpers.
/// </summary>
public static class EnginePlant {
  /// <summary>One oriented, network-joined pipe cell in the scene; steel carries high-pressure lines.</summary>
  public static void Pipe(
    Scene scene,
    BlockPos pos,
    string orientation,
    int id,
    string material = "iron"
  ) {
    var block = PipeTestWorld.MakePipe(
      material: material,
      orientation: orientation,
      id: id
    );
    scene.Node(
      pos,
      block,
      new BlockEntityPipe { Pos = pos.Copy(), Block = block },
      "pipe"
    );
  }

  /// <summary>The pipe axis ("ns"/"we") that lies along <paramref name="face"/>.</summary>
  public static string Axis(BlockFacing face) =>
    face == BlockFacing.NORTH || face == BlockFacing.SOUTH ? "ns"
    : face == BlockFacing.EAST || face == BlockFacing.WEST ? "we"
    : "ud";
}

/// <summary>
/// The starter steam-power setup's water half: a constructed Watt engine driving a fluid-pump
/// sub-machine. The pump's source line, below, holds a fluid intake over a pond; its output line, to
/// the left, is a sealed water main. With steam at the engine's inlet the engine engages, the pump
/// draws from the pond and lifts water into the main.
/// </summary>
internal sealed class WaterPumpPlant {
  public readonly BlockEntityEngineWatt Engine;
  public readonly BlockEntityEngineFluidPump Pump;
  public readonly BlockEntityFluidIntake Intake;

  private readonly Scene _scene;
  private readonly BlockPos _inlet;
  private readonly BlockPos _pond;
  private readonly BlockPos _output;

  public WaterPumpPlant(Scene scene, BlockPos pos) {
    _scene = scene;

    var engineBlock = TestBlocks.Configure(
      new BlockEngineWatt(),
      "iiex:enginewatt-n",
      30,
      ("side", "north")
    );
    Engine = new BlockEntityEngineWatt {
      Pos = pos.Copy(),
      Block = engineBlock,
    };
    scene.Machine(pos, engineBlock, Engine);
    RccFake.Complete(Engine); // re-apply: Initialize clears _rcc from the absent behavior

    // Sealed steam inlet on the engine's south face.
    BlockFacing inletFace = engineBlock.SteamInletFace;
    _inlet = pos.AddCopy(inletFace);
    EnginePlant.Pipe(scene, _inlet, EnginePlant.Axis(inletFace), 31);
    scene.Block(_inlet.AddCopy(inletFace), IiexScenes.Cap(32));

    // Fluid pump at the engine's sub-machine cell; the side rotates from north to east.
    BlockPos subPos = engineBlock.SubmachinePos(pos);
    var pumpBlock = TestBlocks.Configure(
      new BlockEngineFluidPump(),
      "iiex:enginefluidpump-e",
      33,
      ("side", "east")
    );
    Pump = new BlockEntityEngineFluidPump {
      Pos = subPos.Copy(),
      Block = pumpBlock,
    };
    scene.Machine(subPos, pumpBlock, Pump);

    // Source line: a fluid intake directly below the pump, oriented "u" so it presents a connector up
    // into the pump's down face. It is its own one-cell network, the pond the pump draws from.
    _pond = subPos.DownCopy();
    var intakeBlock = TestBlocks.Configure(
      new BlockFluidIntake(),
      "iiex:fluidintake",
      34,
      ("orientation", "u")
    );
    ReflectionHelpers.SetProperty(intakeBlock, "Orientation", "u");
    Intake = new BlockEntityFluidIntake {
      Pos = _pond.Copy(),
      Block = intakeBlock,
    };
    scene.Node(_pond, intakeBlock, Intake, "pipe");
    // The intake reads water below + resolves its own network when the pump asks it to refill.
    ReflectionHelpers.SetProperty(Intake, nameof(Intake.HasWater), true);
    ReflectionHelpers.SetProperty(
      Intake,
      nameof(Intake.NetworkSystem),
      scene.World.Networks
    );

    // Output line: a sealed water main on the pump's left face.
    BlockFacing leftFace = ExOrientation.RotateFacing(
      BlockFacing.WEST,
      ExOrientation.AngleFromSide("east")
    );
    _output = subPos.AddCopy(leftFace);
    EnginePlant.Pipe(scene, _output, EnginePlant.Axis(leftFace), 35);
    scene.Block(_output.AddCopy(leftFace), IiexScenes.Cap(36));
  }

  /// <summary>Tops the engine's steam inlet back up to <paramref name="atm"/>; a single 30 L pipe.</summary>
  public WaterPumpPlant Steam(float atm) {
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

  /// <summary>
  /// Holds the inlet at <paramref name="atm"/> for <paramref name="seconds"/> one-second ticks, a
  /// stand-in for a boiler continuously feeding the line. It re-charges before each tick because a
  /// sealed pipe's charge would otherwise deplete as the running engine consumes it.
  /// </summary>
  public WaterPumpPlant RunWithSteam(float atm, int seconds) {
    for (int i = 0; i < seconds; i++) {
      Steam(atm);
      _scene.Step(1);
    }
    return this;
  }

  /// <summary>Pre-fills the pond, the source line, with standing water for the pump to lift.</summary>
  public WaterPumpPlant FillPond(float litres) {
    _scene
      .NetworkAt<PipeNetwork>(_pond)!
      .TryProduceLiquid(litres, 12f, 1f, _scene.World.Accessor);
    return this;
  }

  public float OutputVolume =>
    _scene.NetworkAt<PipeNetwork>(_output)!.State?.Volume ?? 0f;
  public float PondVolume =>
    _scene.NetworkAt<PipeNetwork>(_pond)!.State?.Volume ?? 0f;
  public float InletVolume =>
    _scene.NetworkAt<PipeNetwork>(_inlet)!.State?.Volume ?? 0f;
}
