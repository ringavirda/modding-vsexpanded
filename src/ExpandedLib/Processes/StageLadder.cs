using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Processes;

/// <summary>
/// One stock family's ladder of <see cref="ProcessStage"/>s - every state that family can be worked into,
/// across every machine family that works it. Parsed from a collectible's <c>stageladder</c> attribute and
/// merged into <see cref="StageLadderRegistry"/>, so a mod extends a process by declaring stages rather
/// than by patching ours.
/// <para>
/// The ladder is a graph, not a line: a stage several families accept is a fork, and the branches are
/// walked by filtering on the fitted family. See docs/design/mechanics/process-extension.md.
/// </para>
/// </summary>
/// <param name="Schema">Schema version of the declaration, so a parser can read every shipped form.</param>
/// <param name="Family">The stock family this ladder belongs to. The registry merges on it.</param>
/// <param name="Shape">Shape file carrying the family's stage elements, or null when each stage names its own.</param>
/// <param name="Stages">The declared stages, in declaration order.</param>
public sealed record StageLadder(
  int Schema,
  string Family,
  string? Shape,
  ProcessStage[] Stages
) {
  /// <summary>The attribute key a collectible declares its ladder under.</summary>
  public const string AttributeKey = "stageladder";

  /// <summary>The schema this parser writes and reads up to. Raise it only alongside the fallback that
  /// reads the form it replaces (<see cref="SpecSchema"/>).</summary>
  public const int CurrentSchema = SpecSchema.First;

  // Thicknesses are authored decimals that arrive as floats, so two declarations of "2.75" must compare
  // equal. Matches WorkPiece's evenness tolerance.
  private const float ThicknessEpsilon = 1e-4f;

  /// <summary>The branch <paramref name="family"/> walks: the stages it accepts, thickest first, which is
  /// the order the piece is worked through them.</summary>
  public IEnumerable<ProcessStage> AcceptedBy(string? family) =>
    Stages
      .Where(s => s.IsAcceptedBy(family))
      .OrderByDescending(s => s.Thickness);

  /// <summary>The stage <paramref name="family"/> sits on at <paramref name="thickness"/>, or null when
  /// that gauge is not one of its states.</summary>
  public ProcessStage? StageAt(float thickness, string? family) =>
    Stages.FirstOrDefault(s =>
      s.IsAcceptedBy(family) && SameThickness(s.Thickness, thickness)
    );

  /// <summary>Whether two declared gauges are the same rung.</summary>
  public static bool SameThickness(float a, float b) =>
    MathF.Abs(a - b) < ThicknessEpsilon;

  /// <summary>
  /// Parses and validates a <c>stageladder</c> attribute. Returns false with a human-readable
  /// <paramref name="error"/> on any malformed field, so a bad ladder fails at load rather than as a rung
  /// the walk silently steps over.
  /// </summary>
  public static bool TryParse(
    JsonObject? node,
    out StageLadder? ladder,
    out string? error
  ) {
    ladder = null;
    error = null;

    if (node is not { Exists: true }) {
      error = $"missing '{AttributeKey}' attribute";
      return false;
    }

    if (!SpecSchema.TryRead(node, CurrentSchema, out int schema, out error))
      return false;

    string family = node["family"].AsString("");
    if (string.IsNullOrWhiteSpace(family)) {
      error = "missing 'family' (the key the registry merges on)";
      return false;
    }

    JsonObject[] stageNodes = node["stages"].AsArray() ?? [];
    if (stageNodes.Length == 0) {
      error = "missing 'stages' (a ladder with no rungs works nothing)";
      return false;
    }

    var stages = new List<ProcessStage>();
    // (thickness, family) is the address of a stage, so a repeat is an ambiguity the walk could not
    // resolve. Two families at one thickness is the fork and stays legal.
    var seen = new List<(float Thickness, string Family)>();
    foreach (JsonObject stageNode in stageNodes) {
      float thickness = stageNode["thickness"].AsFloat(0f);
      if (thickness <= 0f) {
        error = $"stage 'thickness' must be > 0 (was {thickness})";
        return false;
      }

      string[] acceptedBy =
      [
        .. (stageNode["acceptedBy"].AsArray<string>([]) ?? []).Where(f =>
          !string.IsNullOrWhiteSpace(f)
        )!,
      ];
      if (acceptedBy.Length == 0) {
        error =
          $"stage at {thickness} names no 'acceptedBy' family, so nothing can reach it";
        return false;
      }

      foreach (string accepting in acceptedBy) {
        if (
          seen.Any(s =>
            SameThickness(s.Thickness, thickness)
            && string.Equals(
              s.Family,
              accepting,
              StringComparison.OrdinalIgnoreCase
            )
          )
        ) {
          error =
            $"two stages at {thickness} are both accepted by '{accepting}'";
          return false;
        }
        seen.Add((thickness, accepting));
      }

      stages.Add(
        new ProcessStage(
          thickness,
          Blank(stageNode["element"].AsString("")),
          acceptedBy,
          Blank(stageNode["code"].AsString("")),
          stageNode["generate"].AsBool(true),
          [
            .. (stageNode["formerCodes"].AsArray<string>([]) ?? []).Where(c =>
              !string.IsNullOrWhiteSpace(c)
            )!,
          ]
        )
      );
    }

    ladder = new StageLadder(
      schema,
      family,
      Blank(node["shape"].AsString("")),
      [.. stages]
    );
    return true;
  }

  // An absent optional string and one authored empty mean the same thing here: not declared.
  private static string? Blank(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value;
}
