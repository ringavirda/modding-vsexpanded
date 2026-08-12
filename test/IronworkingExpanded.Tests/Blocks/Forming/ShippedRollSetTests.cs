using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Processes;
using IronworkingExpanded.BlockStructures.Forming;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The shipped tooling and the shipped ladders, checked together as the route a player actually walks. A
/// schedule is a branch of a stock family's ladder walked thickest first, and it is only usable if every
/// consecutive step is a legal bite - a draft within <c>δ_max = μ²R</c>. One illegal rung anywhere makes the
/// whole route unenterable.
/// <para>
/// The golden pins these numbers, but a golden only says the data changed; regenerating it accepts a
/// broken ladder without complaint. That is how the grooved set shipped with a first gap of 1.0 against
/// 3.0 stock - a 2.0 draft against a 1.0 limit, so the rod route could never be entered at all.
/// </para>
/// </summary>
public class ShippedRollSetTests {
  /// <summary>Every shipped set, parsed from the emitted definition exactly as the game reads it.</summary>
  private static IEnumerable<(string Type, RollSetSpec Spec)> ShippedSets() {
    ExItemDef def = RollSetItemDefinitions.Definitions("iwex").Single();
    var byType = (JObject)def.ToJson()["attributesByType"]!;

    foreach (string type in RollSetItemDefinitions.SetTypes) {
      JToken node = byType[$"*-{type}"]!;
      Assert.True(
        RollSetSpec.TryParse(
          new JsonObject(node)[RollSetSpec.AttributeKey],
          out RollSetSpec? spec,
          out string? error
        ),
        $"shipped set \"{type}\" does not parse: {error}"
      );
      yield return (type, spec!);
    }
  }

  /// <summary>The shipped ladders in a registry of their own, so this suite measures the emitted data and
  /// not whatever else a test has since contributed to the shared one.</summary>
  private static StageLadderRegistry ShippedLadders() {
    var registry = new StageLadderRegistry();
    foreach (StageLadder ladder in StageLadderSeeds.Shipped())
      Assert.Empty(registry.Contribute(ladder));
    return registry;
  }

  /// <summary>Every route the game can actually run: a shipped set, a stock form that really exists, and a
  /// branch of that form's ladder the set works.</summary>
  private static IEnumerable<(
    string Type,
    StockForm Form,
    MillSchedule Schedule
  )> UsableRoutes() {
    StageLadderRegistry ladders = ShippedLadders();
    foreach ((string type, RollSetSpec spec) in ShippedSets())
      foreach (StockForm form in StockForm.All.Values)
        if (MillSchedule.For(spec, form.Name, ladders) is { } schedule)
          yield return (type, form, schedule);
  }

  [Fact]
  public void Every_shipped_set_and_ladder_parses_and_the_corpus_is_not_empty() {
    // Without this the checks below would pass by scanning nothing.
    Assert.NotEmpty(ShippedSets());
    Assert.NotEmpty(StageLadderSeeds.Shipped());
    Assert.NotEmpty(UsableRoutes());
  }

  [Fact]
  public void Every_usable_route_can_be_walked_from_fresh_stock_to_its_last_gap() {
    var broken = new List<string>();

    foreach (
      (string type, StockForm form, MillSchedule schedule) in UsableRoutes()
    ) {
      float thickness = form.BaseThickness;
      foreach (float gap in schedule.Gaps) {
        float draft = thickness - gap;
        if (
          !RollingPass.CanBite(
            draft,
            IwexValues.RollingRollRadius,
            // Hot: the cold case is a separate, deliberate refusal.
            IwexValues.RollingTempC,
            IwexValues.RollingTempC
          )
        ) {
          broken.Add(
            $"{type} on {form.Name}: {thickness} -> {gap} is a draft of {draft}, over "
              + $"delta_max {RollingPass.MaxDraft(IwexValues.RollingRollRadius, RollingPass.HotFriction)}"
          );
          break;
        }
        thickness = gap;
      }
    }

    Assert.True(
      broken.Count == 0,
      "These shipped routes cannot be walked, so the route has no legal entry:\n    "
        + string.Join("\n    ", broken)
    );
  }

  [Fact]
  public void Every_branch_descends_so_the_barrel_is_walked_widest_first() {
    foreach (
      (string type, StockForm form, MillSchedule schedule) in UsableRoutes()
    ) {
      Assert.NotEmpty(schedule.Gaps);
      for (int i = 1; i < schedule.Gaps.Length; i++)
        Assert.True(
          schedule.Gaps[i] < schedule.Gaps[i - 1],
          $"{type} on {form.Name}: gap {i} ({schedule.Gaps[i]}) does not descend from {schedule.Gaps[i - 1]}"
        );
    }
  }

  [Fact]
  public void The_sets_no_real_stock_can_enter_are_exactly_the_known_ones() {
    // A set is dead when no stock form both passes its `accepts` and declares a rung its family works. It
    // is invisible to the walkability check above precisely because that check finds no route for it.
    // Pinned rather than merely tolerated, so a second one cannot appear unnoticed.
    string[] live = [.. UsableRoutes().Select(r => r.Type).Distinct()];
    string[] dead =
    [
      .. ShippedSets()
        .Select(s => s.Type)
        .Where(t => !live.Contains(t))
        .OrderBy(t => t),
    ];

    // "slitting" accepts only "plate", which is not a StockForm, and no ladder declares a slitting rung.
    // It needs the plate form to exist before it is tooling rather than decoration - tracked with the
    // rolled-product catalogue.
    Assert.Equal(["slitting"], dead);
  }

  [Fact]
  public void A_stopping_point_is_the_ladder_s_to_name_and_no_set_names_one() {
    // The rule this whole layer exists for: a machine may not name a product. Every shipped set is
    // geometry and torque, and what the metal becomes is declared by the stock it is made of.
    foreach (
      (string type, StockForm form, MillSchedule schedule) in UsableRoutes()
    )
      Assert.All(
        schedule.Gaps,
        gap =>
          Assert.True(
            schedule.OutputAt(gap) == null
              || schedule
                .Ladder.StageAt(gap, schedule.Set.Family)!
                .IsStoppingPoint,
            $"{type} on {form.Name}: the product at {gap} came from somewhere other than the ladder"
          )
      );
  }
}
