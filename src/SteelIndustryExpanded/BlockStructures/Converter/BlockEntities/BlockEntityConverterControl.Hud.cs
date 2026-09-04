using System.Text;
using ExpandedLib.Heat;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SteelIndustryExpanded.BlockStructures.Converter.BlockEntities;

// HUD side of the control: the look-at readouts on the control and the vessel, and the
// status line the ticks write into.
public partial class BlockEntityConverterControl {
  #region HUD

  /// <summary>
  /// Brief setup guidance on the control. The live operational readout (charge, process, power,
  /// status) is shown on the converter vessel itself - see <see cref="AppendStructureState"/>.
  /// </summary>
  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    if (!StructureComplete) {
      dsc.AppendLine(Lang.Get("siex:bessemer-info-incomplete"));
      return;
    }
    if (!IsConverterConstructed()) {
      dsc.AppendLine(Lang.Get("siex:bessemer-info-notbuilt"));
      return;
    }
    // The vessel shows the full readout; the control only surfaces power state, wired to the
    // transmission directly under it.
    dsc.AppendLine(
      Lang.Get(
        "siex:bessemer-info-power",
        HasPower()
          ? Lang.Get("siex:bessemer-power-on")
          : Lang.Get("siex:bessemer-power-off")
      )
    );
  }

  // Lang keys for the shared exlib heat-balance ledger, plus the measurement formatter it uses
  // (ExMeasure lives downstream of exlib, so it is passed in). The converter's keys ignore the coke
  // fuel-percentage and preheat arguments the furnace fills; its blow has neither.
  private static readonly HeatBalanceLedgerKeys HeatLedgerKeys = new(
    Temp: "siex:bessemer-info-temp",
    HeatOk: "siex:bessemer-info-heatok",
    HeatStall: "siex:bessemer-info-heatstall",
    HeatIn: "siex:bessemer-info-heatin",
    HeatLoss: "siex:bessemer-info-heatloss",
    BlastNone: "siex:bessemer-info-blastnone",
    BlastHot: "siex:bessemer-info-blastcold",
    BlastCold: "siex:bessemer-info-blastcold"
  );

  private static readonly System.Func<float, string> FormatTemp = t =>
    ExMeasure.Temperature(t);

  /// <summary>
  /// Builds the full operational readout for the converter. Invoked by the converter vessel's
  /// <c>GetBlockInfo</c> so the state reads off the large block rather than the small control.
  /// </summary>
  public void AppendStructureState(IPlayer forPlayer, StringBuilder dsc) {
    if (!StructureComplete) {
      dsc.AppendLine(Lang.Get("siex:bessemer-info-incomplete"));
      return;
    }
    if (!IsConverterConstructed()) {
      dsc.AppendLine(Lang.Get("siex:bessemer-info-notbuilt"));
      return;
    }
    if (!IsGasIntakeAligned()) {
      dsc.AppendLine(Lang.Get("siex:bessemer-info-intake-misaligned"));
      return;
    }
    if (!IsTransmissionAligned()) {
      dsc.AppendLine(Lang.Get("siex:bessemer-info-transmission-misaligned"));
      return;
    }

    // The steel/metal pool: amount, metal name (Pig Iron / Bessemer Steel / Iron) and temperature.
    if (_charge != null && _charge.Units > 0) {
      dsc.AppendLine(
        Lang.Get(
          "siex:bessemer-info-charge",
          _charge.Units,
          CapacityUnits,
          MoltenMetal.DisplayName(_charge.MetalCode.ToString()),
          ExMeasure.Temperature(_charge.Temperature(Api.World))
        )
      );
    }

    // Carbon percentage, the figure the stop is timed against; shown while the bath can still blow.
    if (IsBlowable())
      dsc.AppendLine(
        Lang.Get("siex:bessemer-info-carbon", CarbonPercentText())
      );

    // Cold-scrap load (the temperature gate) and the accumulated slag pool, when present.
    if (_scrapUnits > 0)
      dsc.AppendLine(Lang.Get("siex:bessemer-info-scrap", _scrapUnits));
    if (_moltenSlag >= 1f)
      dsc.AppendLine(Lang.Get("siex:bessemer-info-slag", (int)_moltenSlag));

    // The shared temperature/threshold/heat-in/heat-loss/blast ledger, while a bath is present.
    if (_charge != null && _charge.Units > 0 && IsBlowable())
      HeatBalanceHud.AppendLedger(
        dsc,
        _lastHeatBalance,
        _charge.Temperature(Api.World),
        RefineTemperature,
        HeatLedgerKeys,
        FormatTemp
      );

    dsc.AppendLine(Lang.Get("siex:bessemer-info-status", _status));
  }

  private void SetStatus(string status) {
    if (_status == status)
      return;
    _status = status;
    if (Api?.Side == EnumAppSide.Server)
      MarkDirty();
  }

  #endregion
}
