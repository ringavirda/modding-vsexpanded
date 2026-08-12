using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.BlockStructures.Forming;

/// <summary>
/// The iwex roll sets: the swappable tooling that decides what a rolling mill makes. Each variant carries its
/// own <see cref="RollSetSpec"/> in a <c>rollset</c> attribute, so the mill reads what to do off the fitted set
/// and never names a product in code. Adding a rolled product means an entry here plus its output item.
/// Rolls are chilled cast iron; harder material tiers gate on <see cref="RollSetSpec.MinTorque"/> rather than
/// on the roll's own material. See docs/design/items/roll-sets.md.
/// </summary>
public class RollSetItemDefinitions : IExItemDefProvider {
  // One roll set's spec, as the nested attribute a variant carries. Gaps are in the same block-space units as
  // the mill's roll radius and must descend, since the barrel is walked widest-first.
  //
  // Declared in double, not float: a float widened on the way into JSON leaks its binary error (0.4f becomes
  // 0.4000000059604645) and the emitted def then drifts from its golden. The spec reads the values back as
  // floats; only the authoring side needs the exact literal.
  private static object Set(
    string family,
    string[] accepts,
    double[] gaps,
    object[] outputs,
    double barrelWidth,
    double minTorque
  ) =>
    new {
      rollset = new {
        family,
        accepts,
        gaps,
        outputs,
        barrelWidth,
        minTorque,
      },
    };

  private static object Out(double gap, string code) => new { gap, code };

  /// <summary>
  /// Each entry maps a set's <c>type</c> variant to its spec: a sequence of gaps cut along one barrel, walked
  /// widest-first.
  /// <para>
  /// Every set declares <c>outputs: []</c>. Per the crop table in <c>docs/design/items/rolled-parts.md</c>
  /// every shipped stage is a shear crop, so the mill ejects the piece it drew through and the shear claims
  /// the items. The four codes that used to sit here named no item that existed and contradicted two settled
  /// rulings, so they are gone rather than re-pointed.
  /// </para>
  /// <para>
  /// The one whole-piece conversion - a <c>rolledrod</c> taken flat to 1.0 into a <c>nailplate</c> - cannot be
  /// written yet: <see cref="RollSetSpec.Outputs"/> is keyed on gap alone, and flat 1.0 also yields plate from
  /// a bloom. Same gap, different product, decided by the form that entered; the key must become (form, gap).
  /// </para>
  /// </summary>
  private static readonly Dictionary<string, object> Sets = new() {
    // Flat: a shingled bloom enters 3 thick and 3 wide, so the early gaps fit inside the 6-wide barrel at two
    // passes each. The piece spreads as it flattens (StockForm.Bloom) and overhangs the barrel by the 1.0 gap,
    // after which every gap costs four passes because it must be taken in side-by-side strips. Schedule cost
    // runs 2, 2, 4, 4.
    ["flat"] = Set(
      "flat",
      ["bloom", "billet"],
      [2.0, 1.5, 1.0, 0.5],
      [],
      barrelWidth: 6.0,
      minTorque: 0.2
    ),
    // Flat-wide: the same schedule on a barrel wide enough that the work never overhangs, so every gap stays
    // at two passes however far the piece spreads. Slab starts wider than a narrow barrel and runs only here.
    ["flatwide"] = Set(
      "flat",
      ["slab", "bloom"],
      [2.0, 1.5, 1.0, 0.5],
      [],
      // Wider than any form's MaxWidth (slab caps at 14), so the work never overhangs however far it spreads.
      barrelWidth: 16.0,
      minTorque: 0.5
    ),
    // Grooved: the rod route. A real groove constrains the spread rather than letting it run sideways, which
    // is modelled here as a barrel the work never outgrows. The groove bottoms out at 1.0 - it cannot close
    // further - and the ladder walks down in 0.5 steps from an entry the fresh 3.0 bloom can bite: the old
    // first gap of 1.0 was a 2.0 draft against delta_max 1.0, so the rod route had no legal entry at all.
    ["grooved"] = Set(
      "grooved",
      ["bloom", "billet"],
      [2.5, 2.0, 1.5, 1.0],
      // No outputs: both grooved stages are shear crops (2.0 -> 4x rolledrod, 1.0 -> 4x rod), and the mill
      // ejects the one piece it drew through. See the note on Outputs below.
      [],
      barrelWidth: 16.0,
      minTorque: 0.3
    ),
    // Slitting: plate slit into nail rod. Plate arrives already flat and to width, so it never overhangs.
    ["slitting"] = Set(
      "slitting",
      ["plate"],
      [0.5],
      [],
      barrelWidth: 16.0,
      minTorque: 0.4
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
      .TextureAll("iwex:block/metal/castiron")
      .VariantGroup("type", SetTypes)
      .MaxStackSize(1)
      .Raw("attributesByType", byType)
      .CreativeCommon("*");
  }
}
