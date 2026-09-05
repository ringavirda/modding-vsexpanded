using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace IronIndustryExpanded.BlockStructures.Engine.BlockEntities;

/// <summary>
/// The Watt engine: the cheap, iron-buildable, low-pressure tier. Runs on the 2-4 atm
/// band a Cornish boiler supplies, draws a fixed 30 L/s of steam while running, and has
/// no control rods. Behavior lives in <see cref="BlockEntityEngine"/>.
/// </summary>
[BlockEntityRegister]
public class BlockEntityEngineWatt : BlockEntityEngine {
  protected override float MaxPowerValue => IiexValues.WattEngineMaxPower;
  protected override float EngagePressure =>
    IiexValues.WattEngineEngagePressure;
  protected override float BreakPressure => IiexValues.WattEngineBreakPressure;
  protected override float RunSteamRate => IiexValues.WattEngineSteamRate;
  protected override float RunPower => IiexValues.WattEngineMaxPower;
  protected override float RunWaterOutput => IiexValues.WattEngineWaterRate;

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
        "iiex:engine-info-band",
        ExMeasure.PressureRange(EngagePressure, BreakPressure)
      )
    );
  }
}
