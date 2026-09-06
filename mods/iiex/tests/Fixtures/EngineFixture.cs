using ExpandedLib.Industry.Pipes;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkPipe;
using IronIndustryExpanded.BlockNetworkPipe.BlockEntities;
using IronIndustryExpanded.BlockStructures.Engine.BlockEntities;
using IronIndustryExpanded.BlockStructures.Engine.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// A constructed Watt engine in a shared <see cref="Scene"/>, wired with a sealed steam inlet pipe and
/// a fluid-pump sub-machine at its sub-machine cell, which is the minimum to drive the power tick: the
/// engine engages only when a sub-machine demands power. Geometry comes from the real
/// <see cref="BlockEngineWatt"/>, whose offsets fall back to coded defaults with no JSON. North-facing,
/// so the steam inlet is south and the sub-machine sits two cells north.
/// </summary>
internal sealed class EngineFixture {
  public readonly BlockEntityEngineWatt Engine;
  public readonly BlockEngineWatt Block;
  public readonly BlockEntityEngineFluidPump Pump;

  private readonly Scene _scene;
  private readonly BlockPos _inletPipe;

  public EngineFixture(Scene scene, BlockPos pos) {
    _scene = scene;

    Block = TestBlocks.Configure(
      new BlockEngineWatt(),
      "iiex:enginewatt-n",
      20,
      ("side", "north")
    );
    Engine = new BlockEntityEngineWatt { Pos = pos.Copy(), Block = Block };
    scene.Machine(pos, Block, Engine); // Initialize registers the production tick
    RccFake.Complete(Engine); // re-apply: Initialize clears _rcc from the absent behaviors

    // Sealed single-cell steam inlet on the south face: north end abuts the engine's connector,
    // south end capped, so a produced charge holds its pressure instead of leaking.
    _inletPipe = pos.AddCopy(0, 0, 1);
    // Cast (iiex) tier: the engine runs on 3-4 atm steam, over the plated tier's burst rating.
    var nsPipe = PipeTestWorld.MakePipe(
      material: "steel",
      orientation: "ns",
      id: 21
    );
    scene.Node(
      _inletPipe,
      nsPipe,
      new BlockEntityPipe { Pos = _inletPipe.Copy(), Block = nsPipe },
      "pipe"
    );
    scene.Block(pos.AddCopy(0, 0, 2), IiexScenes.Cap(98));

    // Fluid-pump sub-machine at the engine's sub-machine cell: the source of the power demand.
    BlockPos subPos = Block.SubmachinePos(pos);
    var pumpBlock = TestBlocks.Configure(
      new BlockEngineFluidPump(),
      "iiex:enginefluidpump-e",
      22,
      ("side", "east")
    );
    Pump = new BlockEntityEngineFluidPump {
      Pos = subPos.Copy(),
      Block = pumpBlock,
    };
    scene.Machine(subPos, pumpBlock, Pump);
  }

  /// <summary>
  /// Charges the inlet steam network to <paramref name="atm"/> (a single 30 L pipe). Runs several
  /// passes because one <c>TryProduceGas</c> moves at most the run's weakest segment's
  /// litres-per-second, so a single push stops at the throughput limit short of the target.
  /// </summary>
  public EngineFixture SetInletPressure(float atm) {
    var net = _scene.NetworkAt<PipeNetwork>(_inletPipe)!;
    float target = atm * 30f;
    for (int i = 0; i < 512 && (net.State?.Volume ?? 0f) < target; i++) {
      float before = net.State?.Volume ?? 0f;
      net.TryProduceGas(
        target,
        150f,
        "Steam",
        _scene.World.Accessor,
        maxOutputPressure: atm
      );
      if ((net.State?.Volume ?? 0f) - before <= 0.0001f)
        break; // the run refuses more - its own ceiling, not the rate
    }
    return this;
  }

  public float InletVolume =>
    _scene.NetworkAt<PipeNetwork>(_inletPipe)!.State?.Volume ?? 0f;
}
