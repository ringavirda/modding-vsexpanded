using System;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using LowPressureExpanded.BlockStructures.Engine;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace HighPressureExpanded.BlockStructures.Engine.BlockEntities;

/// <summary>
/// The Cornish engine, the steel high-pressure tier. Its steam control rods set how much steam it
/// admits and so its power: low is half, normal nominal, high double. A higher setting also raises
/// the operating band (<see cref="EngagePressure"/>, <see cref="BreakPressure"/>), so it needs a
/// hotter line to engage. Breaking is handled by lpex's <see cref="BlockEntityEngine"/>.
/// </summary>
[BlockEntityRegister]
public class BlockEntityEngineCornish : BlockEntityEngine {
  // Control-rod setting: 0 = low, 1 = normal, 2 = high.
  private int _throttle = 1;

  /// <summary>The current control-rod setting index, 0..2.</summary>
  public int ThrottleIndex => Math.Clamp(_throttle, 0, 2);

  private static readonly string[] ThrottleKeys = ["low", "normal", "high"];

  /// <summary>Lang key fragment for the current setting.</summary>
  public string ThrottleKey => ThrottleKeys[ThrottleIndex];

  protected override float MaxPowerValue => HpexValues.CornishEngineMaxPower;

  // The operating band moves with the steam admission: a higher setting needs a hotter line, a lower
  // one works off a softer line.
  protected override float EngagePressure =>
    ThrottleIndex switch {
      0 => HpexValues.CornishEngineEngagePressureLow,
      2 => HpexValues.CornishEngineEngagePressureHigh,
      _ => HpexValues.CornishEngineEngagePressureNormal,
    };

  protected override float BreakPressure =>
    ThrottleIndex switch {
      0 => HpexValues.CornishEngineBreakPressureLow,
      2 => HpexValues.CornishEngineBreakPressureHigh,
      _ => HpexValues.CornishEngineBreakPressureNormal,
    };

  // Cylinder steam particles scale with the setting: double on high, none on low.
  protected override int CylinderSteamPuffCount =>
    ThrottleIndex switch {
      0 => 0,
      2 => 4,
      _ => 2,
    };

  // On high the strokes are louder and the gear train is pitched lower; low and normal are unchanged.
  protected override float SoundVolumeFactor =>
    ThrottleIndex == 2 ? HpexValues.CornishEngineOverclockVolume : 1f;

  protected override float SoundPitchFactor =>
    ThrottleIndex == 2 ? HpexValues.CornishEngineOverclockPitch : 1f;

  protected override float RunSteamRate =>
    ThrottleIndex switch {
      0 => HpexValues.CornishEngineSteamLow,
      2 => HpexValues.CornishEngineSteamHigh,
      _ => HpexValues.CornishEngineSteamNormal,
    };

  protected override float RunPower =>
    ThrottleIndex switch {
      0 => HpexValues.CornishEnginePowerLow,
      2 => HpexValues.CornishEnginePowerHigh,
      _ => HpexValues.CornishEnginePowerNormal,
    };

  protected override float RunWaterOutput =>
    ThrottleIndex switch {
      0 => HpexValues.CornishEngineWaterLow,
      2 => HpexValues.CornishEngineWaterHigh,
      _ => HpexValues.CornishEngineWaterNormal,
    };

  /// <summary>
  /// Moves the control rods one step in <paramref name="direction"/> (positive is toward high),
  /// clamped to 0..2. Returns <c>true</c> when the setting changed. Server-side.
  /// </summary>
  public bool AdjustThrottle(int direction) {
    int next = Math.Clamp(ThrottleIndex + Math.Sign(direction), 0, 2);
    if (next == ThrottleIndex)
      return false;
    _throttle = next;
    MarkDirty(true);
    return true;
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetInt("throttle", _throttle);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    _throttle = tree.GetInt("throttle", 1);
  }

  public override void GetBlockInfo(
    IPlayer forPlayer,
    System.Text.StringBuilder dsc
  ) {
    base.GetBlockInfo(forPlayer, dsc);
    if (!IsConstructed || IsBroken)
      return;

    dsc.AppendLine(
      Lang.Get(
        "hpex:engine-info-throttle",
        Lang.Get("hpex:engine-throttle-" + ThrottleKey),
        ExMeasure.PressureRange(EngagePressure, BreakPressure)
      )
    );
  }
}
