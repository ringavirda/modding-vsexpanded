using ExpandedLib;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Helpers;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using IronIndustryExpanded;
using IronIndustryExpanded.BlockNetworkPipe;
using IronIndustryExpanded.BlockStructures.Engine;
using Vintagestory.API.MathTools;

namespace SteelIndustryExpanded.BlockStructures.Engine.BlockEntities;

/// <summary>
/// Cornish-engine sub-machine: an air compressor. While powered it injects air into its left
/// network at the engine's inlet steam pressure times engine efficiency; air at or above the blast
/// threshold of 2.5 atm counts as blast.
/// </summary>
[BlockEntityRegister]
public class BlockEntityEngineAirBlower : BlockEntityEngineSubmachine {
  // Cylinder cycle keyframes (60-frame loop): the piston tops out at frame 15 (intake) and
  // bottoms out at frame 45 (compression). See assets cornish/airblower.json.
  private const int PistonTopFrame = 15;
  private const int PistonBottomFrame = 45;

  /// <summary>World point at the open top of the cylinder. The piston travels on the block's centre
  /// line, so this point is rotation-independent.</summary>
  private Vec3d CylinderMouth => new(Pos.X + 0.5, Pos.Y + 0.9, Pos.Z + 0.5);

  /// <summary>
  /// Replaces the shared stroke sounds with the single-acting cylinder's own: a bellows wheeze as
  /// the piston tops out (intake) and an iron clang as it bottoms out (compression). The intake
  /// stroke also draws a wisp of ambient air into the cylinder mouth.
  /// </summary>
  protected override void OnCycleStroke(float last, float cur, int total) {
    if (PistonCycleSounds.CrossedFrame(last, cur, total, PistonTopFrame)) {
      ExSounds.PlayLocal(Api.World, Pos, ExSounds.Bellows, 0.6f, 16f);
      ExParticles.AirInhale(Api.World, CylinderMouth, 4);
    }
    if (PistonCycleSounds.CrossedFrame(last, cur, total, PistonBottomFrame))
      ExSounds.PlayLocal(Api.World, Pos, ExSounds.AnvilMergeHit, 0.2f, 16f);
  }

  protected override void DoWork(float power, float dt) {
    if (power <= 0f)
      return;
    PipeNetwork? leftNet = ConnectedNetwork(LeftFace);
    if (leftNet == null)
      return;

    float maxPressure =
      (Engine?.InletPressure ?? 0f) * IiexValues.SteamEngineEfficiency;
    float amount = SiexValues.AirBlowerOutputPerSecond * 3 * power * dt;

    leftNet.TryProduceGas(
      amount,
      ExlibValues.AmbientTemperature,
      "Air",
      Api.World.BlockAccessor,
      maxOutputPressure: maxPressure
    );
  }
}
