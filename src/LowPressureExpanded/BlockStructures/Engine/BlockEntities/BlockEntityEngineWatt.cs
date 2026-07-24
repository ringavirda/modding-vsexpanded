using ExpandedLib.Registries.Entities;
using ExpandedLib.Helpers;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace LowPressureExpanded.BlockStructures.Engine.BlockEntities;

/// <summary>
/// The Watt engine - the cheap, iron-buildable, low-pressure tier. Runs on the
/// pressures a Cornish boiler supplies (2-4 atm band) but is thirsty: it draws a fixed
/// 30 L/s of steam while running and has no control rods. All behavior lives in
/// <see cref="BlockEntityEngine"/>.
/// </summary>
[BlockEntityRegister]
public class BlockEntityEngineWatt : BlockEntityEngine
{
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
  )
  {
    base.GetBlockInfo(forPlayer, dsc);
    if (!IsConstructed || IsBroken)
      return;

    // Show the fixed operating band through ExMeasure (like the Cornish engine), so it converts
    // with the player's measurement preference instead of reading a hardcoded "2-4 atm".
    dsc.AppendLine(
      Lang.Get(
        "lpex:engine-info-band",
        ExMeasure.PressureRange(EngagePressure, BreakPressure)
      )
    );
  }
}
