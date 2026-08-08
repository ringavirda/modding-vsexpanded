using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.BlockStructures.Forming;

/// <summary>
/// The iwex <b>roll sets</b> - the swappable tooling that decides what a rolling mill makes. Each carries its
/// own <see cref="RollSetSpec"/> in a per-variant <c>rollset</c> attribute, so the mill reads what to do off
/// the fitted set and never names a product in code: the same tooling-owns-the-data idiom as the casting
/// patterns, and adding a rolled product is an entry here plus its output item.
/// <para>
/// Rolls are <b>chilled cast iron</b> - a roll takes steady compression, not shock, so it is cast rather than
/// forged (the compression/tension rule). Harder material tiers gate on <see cref="RollSetSpec.MinTorque"/>,
/// not on the roll's own material.
/// </para>
/// </summary>
public class RollSetItemDefinitions : IExItemDefProvider
{
  // One roll set's spec, as the nested attribute a variant carries. Gaps are in the same block-space units
  // the mill's roll radius uses, and must descend - the barrel is walked widest-first.
  //
  // Declared in DOUBLE, not float: a float widened on the way into JSON leaks its binary error (0.4f becomes
  // 0.4000000059604645), which makes the emitted def unstable against its golden. The spec reads them back as
  // floats; only the authoring side needs the exact literal.
  private static object Set(
    string family,
    string[] accepts,
    double[] gaps,
    object[] outputs,
    double barrelWidth,
    double minTorque
  ) =>
    new
    {
      rollset = new
      {
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
  /// Each entry: the set's <c>type</c> variant → its spec.
  /// <para>
  /// The <b>narrow</b> sets carry a real sequence of gaps cut along one barrel, so the player walks a piece
  /// down it and pulls out at the thickness they want - plate at 1.0, sheet at 0.5. The <b>wide</b> set has a
  /// single gap because slab fills the whole barrel: reducing it means a train of stands, not a deeper bite.
  /// </para>
  /// </summary>
  private static readonly Dictionary<string, object> Sets = new()
  {
    // Flat: the workhorse, and the set that teaches the width lesson. A shingled bloom enters 3 thick and 3
    // wide, so the early gaps sit inside the 6-wide barrel at the two-pass floor - but the piece SPREADS as it
    // flattens (StockForm.Bloom), overhangs the barrel by the 1.0 gap, and from there every gap costs four
    // passes because it has to be taken in side-by-side strips. Schedule cost runs 2, 2, 4, 4.
    ["flat"] = Set(
      "flat",
      ["bloom", "billet"],
      [2.0, 1.5, 1.0, 0.5],
      [Out(1.0, "iwex:rolledplate-iron"), Out(0.5, "iwex:rolledsheet-iron")],
      barrelWidth: 6.0,
      minTorque: 0.2
    ),
    // Flat-wide: the same schedule with a barrel that swallows the work, so it stays at two passes a gap all
    // the way down however far the piece spreads. That is the entire argument for a plate mill's wide rolls,
    // and it is why a slab - which starts wider than a narrow barrel - can only be run here.
    ["flatwide"] = Set(
      "flat",
      ["slab", "bloom"],
      [2.0, 1.5, 1.0, 0.5],
      [Out(1.0, "iwex:rolledplate-iron"), Out(0.5, "iwex:rolledsheet-iron")],
      // Wider than any form's MaxWidth (slab caps at 14), so the work never overhangs however far it spreads.
      barrelWidth: 16.0,
      minTorque: 0.5
    ),
    // Grooved: Cort's 1783 grooved rolls, contemporaneous with his puddling furnace - this is where rod
    // comes from, and rolling it from wrought iron is the correct route
    // besides (a cast rod snaps). The groove constrains the spread rather than letting it run sideways,
    // which is modelled here simply by a barrel the work never outgrows.
    ["grooved"] = Set(
      "grooved",
      ["bloom", "billet"],
      [1.0, 0.5],
      [Out(1.0, "game:rod-iron"), Out(0.5, "iwex:wirerod-iron")],
      barrelWidth: 16.0,
      minTorque: 0.3
    ),
    // Slitting: the early nail mill - plate slit into nail rod, the feedstock every bolted joint in the tier
    // eats. Plate arrives already flat and to width, so it never overhangs.
    ["slitting"] = Set(
      "slitting",
      ["plate"],
      [0.5],
      [Out(0.5, "iwex:nailrod-iron")],
      barrelWidth: 16.0,
      minTorque: 0.4
    ),
  };

  /// <summary>The roll-set <c>type</c> variants, in emit order - the single source the recipes and the
  /// handbook both derive from.</summary>
  public static readonly string[] SetTypes = [.. Sets.Keys];

  public static IEnumerable<ExItemDef> Definitions(string domain) => [RollSet(domain)];

  private static ExItemDef RollSet(string domain)
  {
    var byType = new Dictionary<string, object>();
    foreach ((string type, object spec) in Sets)
      byType["*-" + type] = spec;

    return ExItemDef
      .Create(domain, "rollset")
      // Placeholder art until the authored roll shapes are exported: the item-rollers source holds all the
      // families in one file, so they ship together rather than one at a time.
      .Shape("game:item/ingot")
      .TextureAll("iwex:block/metal/castiron")
      .VariantGroup("type", SetTypes)
      .MaxStackSize(1)
      .Raw("attributesByType", byType)
      .CreativeCommon("*");
  }
}
