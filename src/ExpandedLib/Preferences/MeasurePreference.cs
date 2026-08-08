using System.Collections.Generic;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Preferences;

namespace ExpandedLib.Preferences;

/// <summary>
/// Metric/imperial display-unit preference. <see cref="Apply"/> sets the active
/// <see cref="ExMeasure.System"/> that every block-info and handbook formatter reads; the simulation
/// always runs in metric, so this changes display only. Registered and persisted through the
/// library's preferences store; the <c>.exmod measure</c> sub-command is built by
/// <see cref="ExpandedLib.Commands.MeasureSubCommand"/>.
/// <para>
/// Lang keys (exlib domain): <c>command-measure-desc</c>, <c>pref-measure-label</c>,
/// <c>pref-measure-metric</c>, <c>pref-measure-imperial</c>.
/// </para>
/// </summary>
[PreferenceRegister]
public sealed class MeasurePreference : IExPreference {
  public string Key => "measure";

  public IReadOnlyList<string> Options { get; } = ["metric", "imperial"];

  public string Default => "metric";

  public void Apply(string value) => ExMeasure.System = ExMeasure.Parse(value);
}
