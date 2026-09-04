using System;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace IronIndustryExpanded.BlockStructures.Engine.BlockEntities;

/// <summary>
/// Engine sub-machine that generates mechanical power. Torque is injected into the vanilla MP
/// network by the <see cref="BEBehaviorEngineMPGenerator"/> behavior, which reads this BE's
/// <see cref="BlockEntityEngineSubmachine.Engine"/> power. The generator has no animation of its
/// own and instead drives the engine's <c>cyclemp</c> animation: every render frame it pushes its
/// axle angle to the engine (one revolution per cycle), keeping the two in step at any speed and
/// while the flywheel coasts.
/// </summary>
[BlockEntityRegister]
public class BlockEntityEngineMPGenerator
  : BlockEntityEngineSubmachine,
    IRenderer {
  private BEBehaviorEngineMPGenerator? _mp;
  private ICoreClientAPI? _capi;

  // Looping metal grind from the gear train while the axle turns. Client only.
  private ILoadedSound? _grindSound;

  // Update the engine's frame before the opaque pass so it renders in step with the axle.
  public double RenderOrder => 0.0;
  public int RenderRange => 64;

  // Full power while an engine is attached and the MP load is within what it can drive; drops to 0
  // once the load overstresses the engine. Judged by network resistance rather than live speed, so
  // a stalled engine recovers when load is shed.
  public override float PowerDemand {
    get {
      if (Engine is not { } engine)
        return 0f;
      float load = _mp?.Network?.NetworkResistance ?? 0f;
      return engine.IsMpOverstressed(load) ? 0f : 1f;
    }
  }

  // No pipe work - power leaves as MP torque via the behavior.
  protected override void DoWork(float power, float dt) { }

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    _mp = GetBehavior<BEBehaviorEngineMPGenerator>();
    if (api is ICoreClientAPI capi) {
      _capi = capi;
      capi.Event.RegisterRenderer(
        this,
        EnumRenderStage.Before,
        "iiex-engine-mpcycle"
      );
    }
  }

  /// <summary>
  /// Pushes the axle's current render angle to the master engine each frame so it can lock its cycle
  /// animation to the visible axle (see <see cref="BlockEntityEngine.DriveMpCycleFrame"/>).
  /// </summary>
  public void OnRenderFrame(float deltaTime, EnumRenderStage stage) {
    if (_mp == null)
      return;
    bool turning = _mp.Network != null && Math.Abs(_mp.Network.Speed) > 0.001f;
    UpdateGrindSound(turning);
    if (Engine is { } engine)
      // The engine cycle runs off the axle's signed render angle so its rod tracks the axle's
      // visible spin direction; AngleRad already carries that direction.
      engine.DriveMpCycleFrame(turning, _mp.AngleRad);
  }

  /// <summary>Runs a quiet looping metal grind while the axle turns; stops it when the axle stalls.</summary>
  private void UpdateGrindSound(bool turning) {
    if (_capi == null)
      return;
    if (turning) {
      _grindSound ??= ExSounds.CreateLoop(
        _capi,
        Pos,
        ExSounds.MetalGrinding,
        0.3f,
        16f,
        0.85f
      );
      if (_grindSound is { IsPlaying: false })
        _grindSound.Start();
    } else if (_grindSound is { IsPlaying: true })
      _grindSound.Stop();
  }

  /// <summary>
  /// Re-applies the axle orientation after the engine snapped this generator to its matching facing.
  /// The base re-resolves the engine; this re-seeds the mechanical axis.
  /// </summary>
  public override void OnExchanged(Block block) {
    base.OnExchanged(block);
    _mp?.OnOrientationChanged();
  }

  public void Dispose() {
    _grindSound?.Stop();
    _grindSound?.Dispose();
    _grindSound = null;
    _capi?.Event.UnregisterRenderer(this, EnumRenderStage.Before);
  }

  public override void OnBlockRemoved() {
    Dispose();
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded() {
    Dispose();
    base.OnBlockUnloaded();
  }
}
