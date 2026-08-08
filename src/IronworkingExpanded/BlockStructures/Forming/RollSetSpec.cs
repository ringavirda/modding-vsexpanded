using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace IronworkingExpanded.BlockStructures.Forming;

/// <summary>
/// The spec a roll set carries, parsed from the roll-set item's <c>rollset</c> attribute; the tooling owns
/// the data and the machine only reads it, as the casting patterns do (<see cref="Casting.MoldSpec"/>).
/// <see cref="Gaps"/> is the sequence cut along the barrel, walked widest first one segment at a time and
/// never re-gapped, so the spacing enforces <c>δ_max = μ²R</c>. See docs/design/machines/rolling-mill.md.
/// </summary>
/// <param name="Family">The roll family: <c>flat</c>, <c>grooved</c>, <c>slitting</c>. Flavour and handbook grouping.</param>
/// <param name="Accepts">Stock forms this set will bite (e.g. <c>bloom</c>, <c>billet</c>, <c>slab</c>). Empty accepts nothing.</param>
/// <param name="Gaps">The gap sequence cut along the barrel, in the order the stock walks them. Strictly descending.</param>
/// <param name="Outputs">Gap thickness to what the stock reads as when pulled at that gap.</param>
/// <param name="BarrelWidth">Usable width of the roll barrel. Stock wider than this cannot be taken in one bite and needs side-by-side passes.</param>
/// <param name="MinTorque">Drive torque the stand needs before this set will turn at all. Tiers gate on torque, not roll material.</param>
public sealed record RollSetSpec(
  string Family,
  string[] Accepts,
  float[] Gaps,
  IReadOnlyDictionary<float, string> Outputs,
  float BarrelWidth,
  float MinTorque
) {
  /// <summary>Working passes a single gap costs for stock of <paramref name="width"/>: a floor of two (a
  /// pass, then the piece turned over and passed again), multiplied by the strip count when the stock
  /// overhangs the barrel and has to be rolled side by side.</summary>
  public int PassesAt(float width) {
    if (BarrelWidth <= 0f || width <= BarrelWidth)
      return 2;
    return 2 * (int)MathF.Ceiling(width / BarrelWidth);
  }

  /// <summary>Whether stock of <paramref name="width"/> overhangs the barrel and so has to be taken in
  /// side-by-side strips rather than one bite.</summary>
  public bool OverhangsBarrel(float width) =>
    BarrelWidth > 0f && width > BarrelWidth;

  /// <summary>The attribute key a roll set carries its spec under.</summary>
  public const string AttributeKey = "rollset";

  /// <summary>A single-gap set: one wide groove filling the barrel (slab and bloom work), so reducing that
  /// stock takes a train of stands rather than a walk along one barrel.</summary>
  public bool IsWide => Gaps.Length == 1;

  /// <summary>Whether this set will bite <paramref name="form"/> at all.</summary>
  public bool AcceptsForm(string? form) =>
    form != null && Accepts.Contains(form);

  /// <summary>The next gap for stock at <paramref name="thickness"/>: the first gap strictly thinner than
  /// it, or null once the stock has passed the last gap or is thinner than the whole schedule. Walking the
  /// barrel in order is what prevents a segment being skipped.</summary>
  public float? NextGap(float thickness) {
    foreach (float gap in Gaps)
      if (gap < thickness)
        return gap;
    return null;
  }

  /// <summary>The draft (reduction) the next pass takes, or 0 when the stock is finished.</summary>
  public float NextDraft(float thickness) =>
    NextGap(thickness) is { } gap ? thickness - gap : 0f;

  /// <summary>What stock pulled at <paramref name="thickness"/> reads as, or null when that is not a named
  /// stopping point (mid-schedule stock stays stock).</summary>
  public string? OutputAt(float thickness) {
    foreach ((float gap, string code) in Outputs)
      if (gap == thickness)
        return code;
    return null;
  }

  /// <summary>Parses and validates a roll set's <c>rollset</c> attribute. Returns false with a
  /// human-readable <paramref name="error"/> on any malformed field, so a bad set fails at load rather than
  /// at the mill.</summary>
  public static bool TryParse(
    JsonObject? node,
    out RollSetSpec? spec,
    out string? error
  ) {
    spec = null;
    error = null;

    if (node is not { Exists: true }) {
      error = $"missing '{AttributeKey}' attribute";
      return false;
    }

    string family = node["family"].AsString("");
    if (string.IsNullOrWhiteSpace(family)) {
      error = "missing 'family'";
      return false;
    }

    string[] accepts =
    [
      .. (node["accepts"].AsArray<string>([]) ?? []).Where(a =>
        !string.IsNullOrWhiteSpace(a)
      )!,
    ];
    if (accepts.Length == 0) {
      error = "missing 'accepts' forms (a set that bites nothing is useless)";
      return false;
    }

    float[] gaps = node["gaps"].AsArray<float>([]) ?? [];
    if (gaps.Length == 0) {
      error = "missing 'gaps' sequence";
      return false;
    }
    // Strictly descending: the barrel is walked widest-first, so an out-of-order gap would let the stock
    // skip a reduction or take a negative one.
    for (int i = 0; i < gaps.Length; i++) {
      if (gaps[i] <= 0f) {
        error = $"gap {i} must be > 0 (was {gaps[i]})";
        return false;
      }
      if (i > 0 && gaps[i] >= gaps[i - 1]) {
        error =
          $"gaps must strictly descend along the barrel (gap {i} = {gaps[i]} is not below {gaps[i - 1]})";
        return false;
      }
    }

    // Outputs are an array of {gap, code} rather than a gap-keyed object: a float is a poor JSON key
    // (0.5 and "0.50" never compare equal), and the array keeps the stopping points in barrel order.
    var outputs = new Dictionary<float, string>();
    JsonObject[]? outputNodes = node["outputs"].AsArray();
    if (outputNodes is not { Length: > 0 }) {
      error =
        "missing 'outputs' (a set with no named stopping point makes nothing)";
      return false;
    }
    foreach (JsonObject outputNode in outputNodes) {
      float gap = outputNode["gap"].AsFloat(-1f);
      string code = outputNode["code"].AsString("");
      if (!gaps.Contains(gap)) {
        error = $"output gap {gap} is not one of the barrel's gaps";
        return false;
      }
      if (string.IsNullOrWhiteSpace(code)) {
        error = $"output at gap {gap} has no 'code'";
        return false;
      }
      outputs[gap] = code;
    }

    float barrelWidth = node["barrelWidth"].AsFloat(0f);
    if (barrelWidth <= 0f) {
      error =
        "missing 'barrelWidth' (the mill cannot tell whether stock overhangs the rolls without it)";
      return false;
    }

    spec = new RollSetSpec(
      family,
      accepts,
      gaps,
      outputs,
      barrelWidth,
      node["minTorque"].AsFloat(0f)
    );
    return true;
  }
}
