using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace LowPressureExpanded.BlockStructures.Engine.BlockEntities;

/// <summary>
/// The Watt engine: the cheap, iron-buildable, low-pressure tier. Runs on the 2-4 atm
/// band a Cornish boiler supplies, draws a fixed 30 L/s of steam while running, and has
/// no control rods. Behavior lives in <see cref="BlockEntityEngine"/>.
/// </summary>
[BlockEntityRegister]
public class BlockEntityEngineWatt : BlockEntityEngine {
  protected override float MaxPowerValue => LpexValues.WattEngineMaxPower;
  protected override float EngagePressure =>
    LpexValues.WattEngineEngagePressure;
  protected override float BreakPressure => LpexValues.WattEngineBreakPressure;
  protected override float RunSteamRate => LpexValues.WattEngineSteamRate;
  protected override float RunPower => LpexValues.WattEngineMaxPower;
  protected override float RunWaterOutput => LpexValues.WattEngineWaterRate;

  public override void GetBlockInfo(
    IPlayer forPlayer,
    System.Text.StringBuilder dsc
  ) {
    base.GetBlockInfo(forPlayer, dsc);
    if (!IsConstructed || IsBroken)
      return;

    // ExMeasure renders the fixed operating band in the player's measurement preference.
    dsc.AppendLine(
      Lang.Get(
        "lpex:engine-info-band",
        ExMeasure.PressureRange(EngagePressure, BreakPressure)
      )
    );
  }
}
