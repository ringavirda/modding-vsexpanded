using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Catalogues;

namespace IronIndustryExpanded.BlockStructures.Forming;

/// <summary>
/// What a fitted roll set can do to one stock family: the branch of that family's
/// <see cref="ProcessRoute"/> the set's roller family accepts, walked thickest first. The two halves are
/// declared apart on purpose - the set carries the tooling's own limits, the route carries the states -
/// so the mill reads a schedule off the pair and names no product itself.
/// <para>
/// A fork is why the branch and not the route is the unit: the same stock at the same gauge is a rod on
/// grooved rolls and a beam on flat ones, which a schedule keyed on gap alone could not express. See
/// docs/design/mechanics/process-extension.md.
/// </para>
/// </summary>
public sealed class MillSchedule {
  private MillSchedule(
    RollSetSpec set,
    ProcessRoute route,
    IReadOnlyList<ProcessStage> stages
  ) {
    Set = set;
    Route = route;
    Stages = stages;
    Gaps = [.. stages.Select(s => s.Thickness)];
  }

  /// <summary>The fitted set.</summary>
  public RollSetSpec Set { get; }

  /// <summary>The stock family's whole route, both branches of a fork included.</summary>
  public ProcessRoute Route { get; }

  /// <summary>The stages this set works, thickest first - the order the piece walks them.</summary>
  public IReadOnlyList<ProcessStage> Stages { get; }

  /// <summary>The gauges of <see cref="Stages"/>, in the same order. What the deck's gap zones index.</summary>
  public float[] Gaps { get; }

  /// <summary>A single-rung branch: one gap filling the whole barrel, so there is no sequence to walk along
  /// it and reducing that stock takes a train of stands instead.</summary>
  public bool IsWide => Stages.Count == 1;

  /// <summary>
  /// The schedule a set fitted to a mill has for stock of <paramref name="form"/>, or null when there is
  /// none - no set fitted, tooling that will not bite that stock, a stock family with no route, or a
  /// route with no stage this set's family accepts.
  /// </summary>
  public static MillSchedule? For(
    RollSetSpec? set,
    string? form,
    ProcessRouteRegistry? registry = null
  ) {
    if (set == null || !set.AcceptsForm(form))
      return null;

    ProcessRoute? route = (registry ?? ProcessRouteRegistry.Shared).Route(form);
    if (route == null)
      return null;

    ProcessStage[] stages = [.. route.RungsFor(set.Family)];
    return stages.Length == 0 ? null : new MillSchedule(set, route, stages);
  }

  /// <summary>The next gap for stock at <paramref name="thickness"/>: the first rung strictly thinner than
  /// it, or null once the stock has passed the last one or is thinner than the whole branch. Walking the
  /// branch in order is what prevents a rung being skipped.</summary>
  public float? NextGap(float thickness) {
    foreach (float gap in Gaps)
      if (gap < thickness)
        return gap;
    return null;
  }

  /// <summary>The draft (reduction) the next pass takes, or 0 when the stock is finished.</summary>
  public float NextDraft(float thickness) =>
    NextGap(thickness) is { } gap ? thickness - gap : 0f;

  /// <summary>What stock pulled at <paramref name="thickness"/> reads as, or null when that stage names no
  /// code and the piece stays stock. Matched with the route's own tolerance, because the gauge a piece
  /// carries is arrived at by arithmetic rather than by re-reading the literal.</summary>
  public string? OutputAt(float thickness) =>
    Route.StageAt(thickness, Set.Family)?.Code;
}
