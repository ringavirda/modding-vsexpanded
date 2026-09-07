using ExpandedLib.Helpers;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkPipe.BlockEntities;
using IronIndustryExpanded.BlockNetworkPipe.Blocks;
using IronIndustryExpanded.BlockStructures.Engine;
using IronIndustryExpanded.BlockStructures.Engine.BlockEntities;
using IronIndustryExpanded.BlockStructures.Engine.Blocks;
using IronIndustryExpanded.Tests;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// A constructed steam engine driving a sub-machine, standing on a real <see cref="Scene"/>: the
/// engine, its sealed steam inlet (one or two pipe cells and a cap, in whichever material the band
/// needs), and the machinery that reaches its sub-machine cell. Public because the hpex plants
/// (<c>MPGeneratorPlant</c>, <c>AirBlowerPlant</c>) build on the same base rather than repeating its
/// eight construction steps.
/// </summary>
public abstract class EnginePlant : MachineRig {
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

  /// <summary>The constructed engine block entity.</summary>
  public BlockEntityEngine Engine { get; }

  protected readonly Scene Scene;
  private readonly BlockPos _inlet;

  /// <summary>
  /// Builds <paramref name="engine"/> at <paramref name="pos"/> and its sealed steam inlet: one pipe
  /// cell on <paramref name="engineBlock"/>'s <see cref="BlockEngine.SteamInletFace"/>, in
  /// <paramref name="material"/>, capped on the far side. The sub-machine itself is the subclass's
  /// concern; this covers only what every engine plant repeats.
  /// </summary>
  protected EnginePlant(
    Scene scene,
    BlockPos pos,
    Block engineBlock,
    BlockEntity engine,
    int inletPipeId,
    int capId,
    string material = "iron"
  )
    : base(scene.World) {
    Scene = scene;
    Engine = (BlockEntityEngine)engine;

    scene.Machine(pos, engineBlock, engine);
    RccFake.Complete(engine); // re-apply: Initialize clears _rcc from the absent behavior

    BlockFacing inletFace = ((BlockEngine)engineBlock).SteamInletFace;
    _inlet = pos.AddCopy(inletFace);
    Pipe(scene, _inlet, Axis(inletFace), inletPipeId, material);
    scene.Block(_inlet.AddCopy(inletFace), IiexScenes.Cap(capId));
  }

  /// <summary>Tops the engine's steam inlet back up to <paramref name="atm"/>.</summary>
  public void Steam(float atm) =>
    Scene
      .NetworkAt<PipeNetwork>(_inlet)!
      .TryProduceGas(
        atm * 30f,
        150f,
        "Steam",
        Scene.World.Accessor,
        maxOutputPressure: atm
      );

  /// <summary>
  /// Holds the inlet at <paramref name="atm"/> for <paramref name="seconds"/> one-second ticks, a
  /// stand-in for a boiler continuously feeding the line. It re-charges before each tick because a
  /// sealed pipe's charge would otherwise deplete as the running engine consumes it.
  /// </summary>
  public void RunWithSteam(float atm, int seconds) =>
    RunWhile(() => Steam(atm), seconds);

  public float InletVolume =>
    Scene.NetworkAt<PipeNetwork>(_inlet)!.State?.Volume ?? 0f;
}

/// <summary>
/// The starter steam-power setup's water half: a constructed Watt engine driving a fluid-pump
/// sub-machine. The pump's source line, below, holds a fluid intake over a pond; its output line, to
/// the left, is a sealed water main. With steam at the engine's inlet the engine engages, the pump
/// draws from the pond and lifts water into the main.
/// </summary>
internal sealed class WaterPumpPlant : EnginePlant {
  public readonly BlockEntityEngineFluidPump Pump;
  public readonly BlockEntityFluidIntake Intake;

  private readonly BlockPos _pond;
  private readonly BlockPos _output;

  public WaterPumpPlant(Scene scene, BlockPos pos)
    : base(
      scene,
      pos,
      TestBlocks.Configure(
        new BlockEngineWatt(),
        "iiex:enginewatt-n",
        30,
        ("side", "north")
      ),
      new BlockEntityEngineWatt { Pos = pos.Copy() },
      31,
      32
    ) {
    var engineBlock = (BlockEngineWatt)Engine.Block;

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
    intakeBlock.ApplyOrientationForTest("u");
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
    Pipe(scene, _output, Axis(leftFace), 35);
    scene.Block(_output.AddCopy(leftFace), IiexScenes.Cap(36));
  }

  /// <summary>Pre-fills the pond, the source line, with standing water for the pump to lift.</summary>
  public WaterPumpPlant FillPond(float litres) {
    Scene
      .NetworkAt<PipeNetwork>(_pond)!
      .TryProduceLiquid(litres, 12f, 1f, Scene.World.Accessor);
    return this;
  }

  public float OutputVolume =>
    Scene.NetworkAt<PipeNetwork>(_output)!.State?.Volume ?? 0f;
  public float PondVolume =>
    Scene.NetworkAt<PipeNetwork>(_pond)!.State?.Volume ?? 0f;
}
