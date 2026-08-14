using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.BlockStructures.Forming;

/// <summary>
/// The iiex roll sets: the swappable tooling that decides what a rolling mill makes. Each variant carries its
/// own <see cref="RollSetSpec"/> in a <c>rollset</c> attribute, so the mill reads what to do off the fitted set
/// and never names a product in code.
/// <para>
/// A set declares only what the tooling itself decides - its roller family, the stock it will bite, its
/// barrel width and the torque it needs. Which states the metal passes through is the stock's stage ladder
/// (<see cref="StockItemDefinitions"/>), so adding a rolled product is a rung there and touches nothing
/// here. Rolls are chilled cast iron; harder material tiers gate on <see cref="RollSetSpec.MinTorque"/>
/// rather than on the roll's own material. See docs/design/items/roll-sets.md.
/// </para>
/// </summary>
public class RollSetItemDefinitions : IExItemDefProvider {
  // One roll set's spec, as the nested attribute a variant carries.
  //
  // Declared in double, not float: a float widened on the way into JSON leaks its binary error (0.4f becomes
  // 0.4000000059604645) and the emitted def then drifts from its golden. The spec reads the values back as
  // floats; only the authoring side needs the exact literal.
  private static object Set(
    string family,
    string[] accepts,
    double barrelWidth,
    double minTorque
  ) =>
    new {
      rollset = new {
        schema = RollSetSpec.CurrentSchema,
        family,
        accepts,
        barrelWidth,
        minTorque,
      },
    };

  /// <summary>
  /// Each entry maps a set's <c>type</c> variant to its spec. <c>family</c> selects the set's branch of
  /// whichever stock it is fed, and <c>accepts</c> is the tooling's own geometry - a narrow barrel refuses
  /// a slab whatever states the slab has, which is the one fact the ladder cannot carry.
  /// </summary>
  private static readonly Dictionary<string, object> Sets = new() {
    // Flat: a shingled bar enters 3 thick and 3 wide, so the first two gaps fit inside the 4-wide barrel at
    // two feeds each. The piece spreads as it flattens (StockForm.ShingledBar) and outgrows the barrel by
    // the 1.5 gap, after which every gap costs four feeds because it must be taken a side at a time.
    // Schedule cost runs 2, 2, 4, 4 - twelve feeds for two plates, which is the number the wide hall is
    // measured against.
    ["flat"] = Set(
      "flat",
      ["shingledbar", "billet"],
      barrelWidth: 4.0,
      minTorque: 0.2
    ),
    // Flat-wide: the same branch on a barrel wide enough that the work never overhangs, so every gap stays
    // at two passes however far the piece spreads. Slab starts wider than a narrow barrel and runs only here.
    ["flatwide"] = Set(
      "flat",
      ["shingledslab", "shingledbar"],
      // Wider than any form's MaxWidth (the slab caps at 14), so the work never overhangs however far it
      // spreads.
      barrelWidth: 16.0,
      minTorque: 0.5
    ),
    // Grooved: the rod route. A real groove constrains the spread rather than letting it run sideways, which
    // is modelled here as a barrel the work never outgrows. It walks the same four rungs as flat, so every
    // draft in the game is uniform and a gap costs the same two rounds whichever family takes it.
    ["grooved"] = Set(
      "grooved",
      ["shingledbar", "billet"],
      barrelWidth: 16.0,
      minTorque: 0.3
    ),
  };

  /// <summary>The roll-set <c>type</c> variants in emit order. The recipes and the handbook both derive from
  /// this list.</summary>
  public static readonly string[] SetTypes = [.. Sets.Keys];

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [RollSet(domain)];

  private static ExItemDef RollSet(string domain) {
    var byType = new Dictionary<string, object>();
    foreach ((string type, object spec) in Sets)
      byType["*-" + type] = spec;

    return ExItemDef
      .Create(domain, "rollset")
      // Placeholder art until the authored roll shapes are exported.
      .Shape("game:item/ingot")
      .TextureAll("iiex:block/metal/castiron")
      .VariantGroup("type", SetTypes)
      .MaxStackSize(1)
      .Raw("attributesByType", byType)
      .CreativeCommon("*");
  }
}
