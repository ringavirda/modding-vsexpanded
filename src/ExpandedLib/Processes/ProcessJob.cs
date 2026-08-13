using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Processes;

/// <summary>
/// One terminal job: a piece goes in, one kind of thing comes out, and <see cref="Count"/> of them do. A
/// staged job crops and the input survives; a whole-item job converts and it does not.
/// See docs/design/mechanics/process-extension.md § What a count means.
/// </summary>
/// <param name="Input">Item code the machine takes.</param>
/// <param name="Output">Item code it yields.</param>
/// <param name="Count">How many of the output the input is worth. On a staged job that is the whole
/// piece's yield and one leaves per stroke; on a whole-item job they all leave at once. At least one.</param>
/// <param name="Stage">Gauge the input must be at, or null when the whole item is the input.</param>
/// <param name="Family">Branch the input must be on, for a staged job at a fork.</param>
/// <param name="MinTorque">Drive torque the machine needs for this job. 0 when it is not gated.</param>
/// <param name="MinTier">Temper floor of the tool the job needs. 0 when any will do.</param>
/// <param name="Seconds">How long one job takes. Never 0, or a job would complete every tick.</param>
public sealed record ProcessJob(
  string Input,
  string Output,
  int Count,
  float? Stage,
  string? Family,
  float MinTorque,
  int MinTier = 0,
  float Seconds = ProcessJob.DefaultSeconds
) {
  /// <summary>What a job takes when it declares no time of its own. A job of no duration would complete
  /// on the tick it started.</summary>
  public const float DefaultSeconds = 1f;

  /// <summary>Whether this job is the one for a piece of <paramref name="input"/> at
  /// <paramref name="stage"/> on <paramref name="family"/>. A job with no stage takes the whole item and
  /// ignores both.</summary>
  public bool Matches(string input, float? stage, string? family) {
    if (!string.Equals(Input, input, StringComparison.OrdinalIgnoreCase))
      return false;
    if (Stage == null)
      return true;
    return stage != null
      && StageLadder.SameThickness(Stage.Value, stage.Value)
      && string.Equals(Family, family, StringComparison.OrdinalIgnoreCase);
  }
}

/// <summary>
/// Every terminal job one machine can do, as a mod declares them. Merged into
/// <see cref="ProcessJobRegistry"/>, so a mod adds a crop by shipping a file rather than by patching ours.
/// </summary>
/// <param name="Schema">Schema version of the declaration.</param>
/// <param name="Machine">The machine these jobs belong to, e.g. <c>shear</c>.</param>
/// <param name="Jobs">The jobs, in declaration order.</param>
public sealed record ProcessJobSet(
  int Schema,
  string Machine,
  ProcessJob[] Jobs
) {
  /// <summary>The schema this parser writes and reads up to.</summary>
  public const int CurrentSchema = SpecSchema.First;

  /// <summary>
  /// Parses and validates one job-set declaration. Returns false with a human-readable
  /// <paramref name="error"/> on any malformed field, so a bad table fails at load rather than by the
  /// machine quietly refusing a piece.
  /// </summary>
  public static bool TryParse(
    JsonObject? node,
    out ProcessJobSet? set,
    out string? error
  ) {
    set = null;
    error = null;

    if (node is not { Exists: true }) {
      error = "missing job-set declaration";
      return false;
    }
    if (!SpecSchema.TryRead(node, CurrentSchema, out int schema, out error))
      return false;

    string machine = node["machine"].AsString("");
    if (string.IsNullOrWhiteSpace(machine)) {
      error = "missing 'machine' (the key the registry files these jobs under)";
      return false;
    }

    var jobs = new List<ProcessJob>();
    foreach (JsonObject jobNode in node["jobs"].AsArray() ?? []) {
      string input = jobNode["input"].AsString("");
      if (string.IsNullOrWhiteSpace(input)) {
        error = $"{machine}: a job names no 'input'";
        return false;
      }

      string output = jobNode["output"].AsString("");
      if (string.IsNullOrWhiteSpace(output)) {
        error = $"{machine}: the job on '{input}' names no 'output'";
        return false;
      }

      int count = jobNode["count"].AsInt(1);
      if (count < 1) {
        error =
          $"{machine}: the job on '{input}' yields a 'count' of {count}, which would destroy the piece";
        return false;
      }

      float stage = jobNode["stage"].AsFloat(-1f);
      float seconds = jobNode["seconds"].AsFloat(ProcessJob.DefaultSeconds);
      jobs.Add(
        new ProcessJob(
          input,
          output,
          count,
          stage > 0f ? stage : null,
          Blank(jobNode["family"].AsString("")),
          jobNode["minTorque"].AsFloat(0f),
          jobNode["minTier"].AsInt(0),
          // A job of no duration would complete on the tick it started, so an authored 0 reads as
          // "unstated" rather than as instant.
          seconds > 0f
            ? seconds
            : ProcessJob.DefaultSeconds
        )
      );
    }

    set = new ProcessJobSet(schema, machine, [.. jobs]);
    return true;
  }

  private static string? Blank(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value;
}
