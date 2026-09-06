using System;
using System.Text;

namespace ExpandedLib.Helpers;

/// <summary>
/// The <c>GetBlockInfo</c> boilerplate: a line through <c>Lang.Get</c>, the same line guarded by a
/// condition, and a measured value formatted through <see cref="ExMeasure"/> so metric and imperial
/// readers both get their unit.
/// </summary>
public static class ExInfo {
  /// <summary>Appends <c>Lang.Get(key, args)</c> as one line.</summary>
  public static StringBuilder Lang(
    this StringBuilder dsc,
    string key,
    params object[] args
  ) => dsc.AppendLine(Vintagestory.API.Config.Lang.Get(key, args));

  /// <summary>Appends the line only when <paramref name="condition"/> holds; otherwise
  /// <paramref name="dsc"/> is returned unchanged.</summary>
  public static StringBuilder LangIf(
    this StringBuilder dsc,
    bool condition,
    string key,
    params object[] args
  ) => condition ? dsc.Lang(key, args) : dsc;

  /// <summary>
  /// Appends <paramref name="key"/>'s text with <paramref name="value"/> formatted through
  /// <see cref="ExMeasure"/> in <paramref name="unit"/> - one of <c>volume</c>, <c>pressure</c>,
  /// <c>temperature</c>, <c>power</c>, <c>speed</c>, <c>flowrate</c> or <c>energy</c>
  /// (case-insensitive) - so metric and imperial readers both get theirs. Throws
  /// <see cref="ArgumentException"/> naming an unrecognised unit.
  /// </summary>
  public static StringBuilder Measure(
    this StringBuilder dsc,
    string key,
    float value,
    string unit
  ) {
    string formatted = unit.ToLowerInvariant() switch {
      "volume" => ExMeasure.Volume(value),
      "pressure" => ExMeasure.Pressure(value),
      "temperature" => ExMeasure.Temperature(value),
      "power" => ExMeasure.Power(value),
      "speed" => ExMeasure.Speed(value),
      "flowrate" => ExMeasure.FlowRate(value),
      "energy" => ExMeasure.Energy(value),
      _ => throw new ArgumentException(
        $"'{unit}' is not a unit ExInfo.Measure knows.",
        nameof(unit)
      ),
    };
    return dsc.Lang(key, formatted);
  }
}
