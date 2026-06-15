using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PipesAndPowerExpanded.Helpers;

/// <summary>
/// The unit system used when formatting measurements for the look-at HUD / block info / handbook.
/// The simulation itself always runs in metric; this only changes how values are displayed.
/// </summary>
public enum MeasurementSystem
{
  /// <summary>Litres, atmospheres, degrees Celsius.</summary>
  Metric,

  /// <summary>Imperial gallons, pounds per square inch, degrees Fahrenheit.</summary>
  Imperial,
}

/// <summary>
/// Shared display-unit helper for the steam/pipe machinery (used by ppex and, through its dependency
/// on ppex, smex). All gameplay values are stored and calculated in metric (litres, atm, °C); these
/// methods convert a metric value into a player-facing string in the currently active
/// <see cref="System"/>, so block-info / handbook code can stay unit-agnostic.
/// <para>
/// <see cref="System"/> is a per-player, client-side preference: it is driven by
/// <see cref="Preferences.MeasurePreference"/> and persisted through the library's generic
/// preferences store, applied for the local player on join and changed via <c>/exmod measure</c>.
/// </para>
/// </summary>
public static class ExMeasure
{
  /// <summary>The display system currently active on this client (the local player's choice).
  /// Block-info formatters read this; it is set from the per-player preference on join.</summary>
  public static MeasurementSystem System { get; set; } =
    MeasurementSystem.Metric;

  // Conversion factors from metric.
  private const float LitresToImperialGallons = 0.219969248f; // 1 L = 0.21997 imp gal
  private const float AtmToPsi = 14.6959488f; // 1 atm = 14.696 psi

  private static bool Imperial => System == MeasurementSystem.Imperial;

  /// <summary>Parses a system name ("metric" / "imperial", case-insensitive); returns
  /// <see cref="MeasurementSystem.Metric"/> for anything unrecognised.</summary>
  public static MeasurementSystem Parse(string? name) =>
    string.Equals(name, "imperial", StringComparison.OrdinalIgnoreCase)
      ? MeasurementSystem.Imperial
      : MeasurementSystem.Metric;

  #region Volume

  /// <summary>A volume given in litres, e.g. <c>"800 L"</c> or <c>"176 gal"</c>.</summary>
  public static string Volume(float litres, string format = "F0")
  {
    var (num, unit) = Vol(litres, format);
    return num + " " + unit;
  }

  /// <summary>A volume range from a shared pool, e.g. <c>"400 / 800 L"</c> - the unit is
  /// printed once after the pair.</summary>
  public static string VolumeRange(
    float litres,
    float maxLitres,
    string format = "F0"
  )
  {
    var (a, _) = Vol(litres, format);
    var (b, unit) = Vol(maxLitres, format);
    return a + " / " + b + " " + unit;
  }

  /// <summary>A flow rate given in litres per second, e.g. <c>"48 L/s"</c> or <c>"11 gal/s"</c>.</summary>
  public static string FlowRate(float litresPerSecond, string format = "F1")
  {
    var (num, unit) = Flow(litresPerSecond, format);
    return num + " " + unit;
  }

  #endregion

  #region Pressure

  /// <summary>A pressure given in atmospheres, e.g. <c>"5.00 atm"</c> or <c>"73.48 psi"</c>.</summary>
  public static string Pressure(float atm, string format = "F2")
  {
    var (num, unit) = Pres(atm, format);
    return num + " " + unit;
  }

  /// <summary>A pressure range (current / limit), e.g. <c>"3.0 / 8.0 atm"</c> - the unit is
  /// printed once after the pair.</summary>
  public static string PressureRange(
    float atm,
    float maxAtm,
    string format = "F1"
  )
  {
    var (a, _) = Pres(atm, format);
    var (b, unit) = Pres(maxAtm, format);
    return a + " / " + b + " " + unit;
  }

  #endregion

  #region Temperature

  /// <summary>A temperature given in degrees Celsius, e.g. <c>"100 °C"</c> or <c>"212 °F"</c>.</summary>
  public static string Temperature(float celsius, string format = "F0")
  {
    var (num, unit) = Temp(celsius, format);
    return num + " " + unit;
  }

  #endregion

  #region Handbook prose conversion

  // Plain metric unit mentions in prose: a single value, a "a-b" range or an "a / b / c" list,
  // followed by a metric unit. The unit alternation lists "L/s" before "L" so the longer match
  // wins; the trailing negative look-ahead stops "L"/"atm" from matching inside a word. The whole
  // number (incl. decimals) is captured, so "2.5 atm" converts cleanly without partial matches.
  private static readonly Regex MetricLiteralRegex = new(
    @"((?:\d+(?:\.\d+)?\s*[-/]\s*)*\d+(?:\.\d+)?)\s*(L/s|L|atm|°C)(?![A-Za-z])",
    RegexOptions.Compiled
  );

  /// <summary>
  /// Converts plain metric unit mentions in free prose (e.g. handbook text) to the active
  /// <see cref="System"/>: "30 L" → "6.6 gal", "2-4 atm" → "29.39-58.78 psi", "160-220 °C" →
  /// "320-428 °F", "8 / 16 / 32 L/s" → "1.76 / 3.52 / 7.04 gal/s". Returns the text unchanged in
  /// metric mode (the prose is authored in metric) and for any text without a recognised unit.
  /// </summary>
  public static string ConvertMetricText(string text)
  {
    if (!Imperial || string.IsNullOrEmpty(text))
      return text;

    return MetricLiteralRegex.Replace(
      text,
      m =>
      {
        string numbers = m.Groups[1].Value;
        string unit = m.Groups[2].Value;
        // Convert each number in the (possibly multi-value) expression, keeping the
        // original "-" / "/" separators and spacing between them.
        string converted = Regex.Replace(
          numbers,
          @"\d+(?:\.\d+)?",
          nm =>
            ConvertNumber(
              unit,
              float.Parse(nm.Value, CultureInfo.InvariantCulture)
            )
        );
        return converted + " " + ImperialUnit(unit);
      }
    );
  }

  private static string ConvertNumber(string metricUnit, float value) =>
    metricUnit switch
    {
      "L/s" => Num(value * LitresToImperialGallons, "0.##"),
      "L" => Num(value * LitresToImperialGallons, "0.##"),
      "atm" => Num(value * AtmToPsi, "0.##"),
      "°C" => Num(value * 9f / 5f + 32f, "0"),
      _ => Num(value, "0.##"),
    };

  private static string ImperialUnit(string metricUnit) =>
    metricUnit switch
    {
      "L/s" => "gal/s",
      "L" => "gal",
      "atm" => "psi",
      "°C" => "°F",
      _ => metricUnit,
    };

  #endregion

  #region Conversions

  private static (string num, string unit) Vol(float litres, string format) =>
    Imperial
      ? (Num(litres * LitresToImperialGallons, format), "gal")
      : (Num(litres, format), "L");

  private static (string num, string unit) Flow(
    float litresPerSecond,
    string format
  ) =>
    Imperial
      ? (Num(litresPerSecond * LitresToImperialGallons, format), "gal/s")
      : (Num(litresPerSecond, format), "L/s");

  private static (string num, string unit) Pres(float atm, string format) =>
    Imperial ? (Num(atm * AtmToPsi, format), "psi") : (Num(atm, format), "atm");

  private static (string num, string unit) Temp(float celsius, string format) =>
    Imperial
      ? (Num(celsius * 9f / 5f + 32f, format), "°F")
      : (Num(celsius, format), "°C");

  // Formats with the invariant culture so the decimal separator stays a dot regardless of
  // the player's locale (matching how the rest of the HUD numbers read).
  private static string Num(float value, string format) =>
    value.ToString(format, CultureInfo.InvariantCulture);

  #endregion
}
