using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Catalogues;

/// <summary>
/// One stock family's route of <see cref="ProcessStage"/>s - every state that family can be worked into,
/// across every machine family that works it. Parsed from a collectible's <c>processroute</c> attribute and
/// merged into <see cref="ProcessRouteRegistry"/>, so a mod extends a process by declaring stages rather
/// than by patching ours.
/// <para>
/// The route is a graph, not a line: a stage several families accept is a fork, and the branches are
/// walked by filtering on the fitted family. See docs/design/mechanics/process-extension.md.
/// </para>
/// </summary>
/// <param name="Schema">Schema version of the declaration, so a parser can read every shipped form.</param>
/// <param name="Family">The stock family this route belongs to. The registry merges on it.</param>
/// <param name="Shape">Shape file carrying the family's stage elements, or null when each stage names its own.</param>
/// <param name="Stages">The declared stages, in declaration order.</param>
public sealed record ProcessRoute(
  int Schema,
  string Family,
  string? Shape,
  ProcessStage[] Stages
) {
  /// <summary>The attribute key a collectible declares its route under.</summary>
  public const string AttributeKey = "processroute";

  /// <summary>The schema this parser writes and reads up to. Raise it only alongside the fallback that
  /// reads the form it replaces (<see cref="SpecSchema"/>).</summary>
  public const int CurrentSchema = SpecSchema.First;

  // Thicknesses are authored decimals that arrive as floats, so two declarations of "2.75" must compare
  // equal. Matches WorkPiece's evenness tolerance.
  private const float ThicknessEpsilon = 1e-4f;

  /// <summary>The branch <paramref name="family"/> walks: the gauges it can be set to, thickest first,
  /// which is the order the piece is worked through them. Half-steps are excluded - they are states the
  /// piece passes through mid-round, not settings, so a machine that offered them would be offering twice
  /// the gaps it has.</summary>
  public IEnumerable<ProcessStage> RungsFor(string? family) =>
    Stages
      .Where(s => s.IsAcceptedBy(family) && s.IsRung)
      .OrderByDescending(s => s.Thickness);

  /// <summary>The stage <paramref name="family"/> sits on at <paramref name="thickness"/>, or null when
  /// that gauge is not one of its states. Half-steps included: this is what the renderer asks, and the
  /// state between two rungs is exactly the one it has no other way to draw.</summary>
  public ProcessStage? StageAt(float thickness, string? family) =>
    Stages.FirstOrDefault(s =>
      s.IsAcceptedBy(family) && SameThickness(s.Thickness, thickness)
    );

  /// <summary>Whether two declared gauges are the same rung.</summary>
  public static bool SameThickness(float a, float b) =>
    MathF.Abs(a - b) < ThicknessEpsilon;

  /// <summary>
  /// Parses and validates a <c>processroute</c> attribute. Returns false with a human-readable
  /// <paramref name="error"/> on any malformed field, so a bad route fails at load rather than as a rung
  /// the walk silently steps over.
  /// </summary>
  public static bool TryParse(
    JsonObject? node,
    out ProcessRoute? route,
    out string? error
  ) {
    route = null;
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
      error = "missing 'stages' (a route with no rungs works nothing)";
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

      bool halfStep = stageNode["halfStep"].AsBool(false);
      string? code = Blank(stageNode["code"].AsString(""));
      // A half-step is where a piece is mid-round, so it is not somewhere work can be left. Rejected at
      // parse rather than ignored at claim time: a product declared at a gauge the player cannot stop on
      // would read as reachable everywhere it is listed and be obtainable nowhere.
      if (halfStep && code != null) {
        error =
          $"stage at {thickness} is a half-step and names code '{code}'; a half-step is a state passed "
          + "through, never a stopping point";
        return false;
      }

      stages.Add(
        new ProcessStage(
          thickness,
          Blank(stageNode["element"].AsString("")),
          acceptedBy,
          code,
          stageNode["generate"].AsBool(true),
          [
            .. (stageNode["formerCodes"].AsArray<string>([]) ?? []).Where(c =>
              !string.IsNullOrWhiteSpace(c)
            )!,
          ],
          halfStep
        )
      );
    }

    route = new ProcessRoute(
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
