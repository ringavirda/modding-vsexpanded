using System.Text;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Process;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SteelmakingExpanded.BlockStructures.Converter.BlockEntities;

// HUD side of the control: the look-at readouts on the control and the vessel, and the
// status line the ticks write into.
public partial class BlockEntityConverterControl
{
  #region HUD

  /// <summary>
  /// Brief setup guidance on the control. The live operational readout (charge,
  /// process, power, status) is shown on the converter vessel itself - see
  /// <see cref="AppendStructureState"/>, which the converter forwards to.
  /// </summary>
  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    if (!StructureComplete)
    {
      dsc.AppendLine(Lang.Get("smex:bessemer-info-incomplete"));
      return;
    }
    if (!IsConverterConstructed())
    {
      dsc.AppendLine(Lang.Get("smex:bessemer-info-notbuilt"));
      return;
    }
    // The vessel shows the full readout; the control just surfaces power state (wired to the
    // transmission directly under it).
    dsc.AppendLine(
      Lang.Get(
        "smex:bessemer-info-power",
        HasPower()
          ? Lang.Get("smex:bessemer-power-on")
          : Lang.Get("smex:bessemer-power-off")
      )
    );
  }

  // The furnace's lang keys for the shared exlib heat-balance ledger, and the measurement formatter it
  // uses (ExMeasure lives downstream of exlib, so it is handed in). The converter's keys ignore the coke
  // fuel-% and preheat args the furnace fills - its blow has neither.
  private static readonly HeatBalanceLedgerKeys HeatLedgerKeys = new(
    Temp: "smex:bessemer-info-temp",
    HeatOk: "smex:bessemer-info-heatok",
    HeatStall: "smex:bessemer-info-heatstall",
    HeatIn: "smex:bessemer-info-heatin",
    HeatLoss: "smex:bessemer-info-heatloss",
    BlastNone: "smex:bessemer-info-blastnone",
    BlastHot: "smex:bessemer-info-blastcold",
    BlastCold: "smex:bessemer-info-blastcold"
  );

  private static readonly System.Func<float, string> FormatTemp = t =>
    ExMeasure.Temperature(t);

  /// <summary>
  /// Builds the full operational readout for the converter. Invoked by the
  /// converter vessel's <c>GetBlockInfo</c> so the player reads the state off the
  /// big block they are naturally looking at, rather than the small control.
  /// </summary>
  public void AppendStructureState(IPlayer forPlayer, StringBuilder dsc)
  {
    if (!StructureComplete)
    {
      dsc.AppendLine(Lang.Get("smex:bessemer-info-incomplete"));
      return;
    }
    if (!IsConverterConstructed())
    {
      dsc.AppendLine(Lang.Get("smex:bessemer-info-notbuilt"));
      return;
    }
    if (!IsGasIntakeAligned())
    {
      dsc.AppendLine(Lang.Get("smex:bessemer-info-intake-misaligned"));
      return;
    }
    if (!IsTransmissionAligned())
    {
      dsc.AppendLine(Lang.Get("smex:bessemer-info-transmission-misaligned"));
      return;
    }

    // The steel/metal pool: amount, metal name (Pig Iron / Bessemer Steel / Iron) and temperature.
    if (_charge != null && _charge.Units > 0)
    {
      dsc.AppendLine(
        Lang.Get(
          "smex:bessemer-info-charge",
          _charge.Units,
          CapacityUnits,
          MoltenMetal.DisplayName(_charge.MetalCode.ToString()),
          ExMeasure.Temperature(_charge.Temperature(Api.World))
        )
      );
    }

    // Carbon % - the headline the player times the stop against - shown while the bath can still blow.
    if (IsBlowable())
      dsc.AppendLine(
        Lang.Get("smex:bessemer-info-carbon", CarbonPercentText())
      );

    // Cold-scrap load (the temperature gate) and the accumulated slag pool, when present.
    if (_scrapUnits > 0)
      dsc.AppendLine(Lang.Get("smex:bessemer-info-scrap", _scrapUnits));
    if (_moltenSlag >= 1f)
      dsc.AppendLine(Lang.Get("smex:bessemer-info-slag", (int)_moltenSlag));

    // The shared T / threshold / heat-in / heat-loss / blast ledger, while there is a bath to refine.
    if (_charge != null && _charge.Units > 0 && IsBlowable())
      HeatBalanceHud.AppendLedger(
        dsc,
        _lastHeatBalance,
        _charge.Temperature(Api.World),
        RefineTemperature,
        HeatLedgerKeys,
        FormatTemp
      );

    dsc.AppendLine(Lang.Get("smex:bessemer-info-status", _status));
  }

  private void SetStatus(string status)
  {
    if (_status == status)
      return;
    _status = status;
    if (Api?.Side == EnumAppSide.Server)
      MarkDirty();
  }

  #endregion
}
