using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Processes;
using IronIndustryExpanded.BlockStructures.Forming;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The shipped tooling and the shipped routes, checked together as the route a player actually walks. A
/// schedule is a branch of a stock family's route walked thickest first, and it is only usable if every
/// consecutive step is a legal bite - a draft within <c>δ_max = μ²R</c>. One illegal rung anywhere makes the
/// whole route unenterable.
/// <para>
/// The golden pins these numbers, but a golden only says the data changed; regenerating it accepts a
/// broken route without complaint. That is how the grooved set shipped with a first gap of 1.0 against
/// 3.0 stock - a 2.0 draft against a 1.0 limit, so the rod route could never be entered at all.
/// </para>
/// </summary>
public class ShippedRollSetTests {
  /// <summary>Every shipped set, parsed from the emitted definition exactly as the game reads it.</summary>
  private static IEnumerable<(string Type, RollSetSpec Spec)> ShippedSets() {
    ExItemDef def = RollSetItemDefinitions.Definitions("iiex").Single();
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

  /// <summary>The shipped routes in a registry of their own, so this suite measures the emitted data and
  /// not whatever else a test has since contributed to the shared one.</summary>
  private static ProcessRouteRegistry ShippedRoutes() {
    var registry = new ProcessRouteRegistry();
    foreach (ProcessRoute route in ProcessRouteSeeds.Shipped())
      Assert.Empty(registry.Contribute(route));
    return registry;
  }

  /// <summary>Every route the game can actually run: a shipped set, a stock form that really exists, and a
  /// branch of that form's route the set works.</summary>
  private static IEnumerable<(
    string Type,
    StockForm Form,
    MillSchedule Schedule
  )> UsableRoutes() {
    ProcessRouteRegistry routes = ShippedRoutes();
    foreach ((string type, RollSetSpec spec) in ShippedSets())
      foreach (StockForm form in StockForm.All.Values)
        if (MillSchedule.For(spec, form.Name, routes) is { } schedule)
          yield return (type, form, schedule);
  }

  [Fact]
  public void Every_shipped_set_and_route_parses_and_the_corpus_is_not_empty() {
    // Without this the checks below would pass by scanning nothing.
    Assert.NotEmpty(ShippedSets());
    Assert.NotEmpty(ProcessRouteSeeds.Shipped());
    Assert.NotEmpty(UsableRoutes());
  }

  [Fact]
  public void Every_usable_route_can_be_walked_from_fresh_stock_to_its_last_gap() {
    var broken = new List<string>();

    foreach (
      (string type, StockForm form, MillSchedule schedule) in UsableRoutes()
    ) {
      // The real walk, round by round, not gap by gap: a gap is two rounds and the rolls are only ever
      // asked for one round's draft. Checking the whole gap was the old one-bite model, and against a
      // delta_max calibrated below a gap it would condemn every route a player can actually walk.
      WorkPiece piece = WorkPiece.Fresh(form);
      bool walkable = true;
      foreach (float gap in schedule.Gaps) {
        for (int round = 0; round < WorkPiece.FeedsPerSide && walkable; round++) {
          float draft = piece.Thickness - piece.RoundTarget(gap);
          if (
            !RollingPass.CanBite(
              draft,
              IiexValues.RollingRollRadius,
              // Hot: the cold case is a separate, deliberate refusal.
              IiexValues.RollingTempC,
              IiexValues.RollingTempC
            )
          ) {
            broken.Add(
              $"{type} on {form.Name}: round {round + 1} of the {gap} gap is a draft of {draft} from "
                + $"{piece.Thickness}, over delta_max "
                + $"{RollingPass.MaxDraft(IiexValues.RollingRollRadius, RollingPass.HotFriction)}"
            );
            walkable = false;
            break;
          }

          // One side: the shipped barrels are never outgrown by the width this walk reaches.
          piece = piece.ForSides(1).Feed(0, gap);
        }
        if (!walkable)
          break;
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

    // None, since the slitting set was retired 2026-08-12. It accepted only "plate", which is not a
    // StockForm, and no route declared a slitting rung, so it was tooling with no route at all. A set
    // that reaches nothing must not ship again.
    Assert.Empty(dead);
  }

  [Fact]
  public void A_skipped_gap_is_refused_on_every_shipped_route() {
    // The other half of the calibration, and the reason it was chosen over an ordering rule: delta_max sits
    // below one gap's draft, so clicking past the next rung skids rather than buying a shortcut. Without
    // this the twelve-feed schedule is an upper bound rather than a cost.
    foreach (
      (string type, StockForm form, MillSchedule schedule) in UsableRoutes()
    ) {
      if (schedule.Gaps.Length < 2)
        continue; // a single-rung branch has nothing to skip

      WorkPiece fresh = WorkPiece.Fresh(form);
      float skipped = schedule.Gaps[1];

      Assert.False(
        RollingPass.CanBite(
          fresh.Thickness - fresh.RoundTarget(skipped),
          IiexValues.RollingRollRadius,
          IiexValues.RollingTempC,
          IiexValues.RollingTempC
        ),
        $"{type} on {form.Name}: fresh stock can skip straight to the {skipped} gap"
      );
    }
  }

  [Fact]
  public void A_stopping_point_is_the_route_s_to_name_and_no_set_names_one() {
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
                .Route.StageAt(gap, schedule.Set.Family)!
                .IsStoppingPoint,
            $"{type} on {form.Name}: the product at {gap} came from somewhere other than the route"
          )
      );
  }
}
