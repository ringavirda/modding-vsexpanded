using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using IronworkingExpanded.BlockStructures.Forming;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The shipped roll sets, checked as tooling a player actually uses rather than as data. A set is a ladder
/// of gaps walked widest-first, and a ladder is only usable if every consecutive step is a legal bite - a
/// draft within <c>δ_max = μ²R</c>. One illegal rung anywhere makes the whole route unenterable.
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

  /// <summary>Every (set, stock form) pair the game can actually produce: a declared <c>accepts</c> entry
  /// naming a form that really exists.</summary>
  private static IEnumerable<(
    string Type,
    RollSetSpec Spec,
    StockForm Form
  )> UsablePairs() {
    foreach ((string type, RollSetSpec spec) in ShippedSets())
      foreach (StockForm form in StockForm.All.Values)
        if (spec.AcceptsForm(form.Name))
          yield return (type, spec, form);
  }

  [Fact]
  public void Every_shipped_set_parses_and_the_corpus_is_not_empty() {
    // Without this the two theories below would pass by scanning nothing.
    Assert.NotEmpty(ShippedSets());
    Assert.NotEmpty(UsablePairs());
  }

  [Fact]
  public void Every_usable_set_can_be_walked_from_fresh_stock_to_its_last_gap() {
    var broken = new List<string>();

    foreach ((string type, RollSetSpec spec, StockForm form) in UsablePairs()) {
      float thickness = form.BaseThickness;
      foreach (float gap in spec.Gaps) {
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
      "These shipped ladders cannot be walked, so the route has no legal entry:\n    "
        + string.Join("\n    ", broken)
    );
  }

  [Fact]
  public void Gaps_descend_so_the_barrel_is_walked_widest_first() {
    foreach ((string type, RollSetSpec spec) in ShippedSets()) {
      Assert.NotEmpty(spec.Gaps);
      for (int i = 1; i < spec.Gaps.Length; i++)
        Assert.True(
          spec.Gaps[i] < spec.Gaps[i - 1],
          $"{type}: gap {i} ({spec.Gaps[i]}) does not descend from {spec.Gaps[i - 1]}"
        );
    }
  }

  [Fact]
  public void The_sets_that_no_real_stock_form_can_enter_are_exactly_the_known_ones() {
    // A set whose every `accepts` entry names a form that does not exist can never take anything, and it
    // is invisible to the walkability check above precisely because that check finds no pair for it.
    // Pinned rather than merely tolerated, so a second one cannot appear unnoticed.
    string[] dead =
    [
      .. ShippedSets()
        .Where(s => !StockForm.All.Values.Any(f => s.Spec.AcceptsForm(f.Name)))
        .Select(s => s.Type)
        .OrderBy(t => t),
    ];

    // "slitting" accepts only "plate", which is not a StockForm. It needs the plate form to exist before
    // it is tooling rather than decoration - tracked with the rolled-product catalogue.
    Assert.Equal(["slitting"], dead);
  }
}
