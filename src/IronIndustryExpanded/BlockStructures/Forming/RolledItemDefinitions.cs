using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.BlockStructures.Forming;

/// <summary>
/// The rolled-product catalogue: what the shear claims off a mill schedule. Each is a finished part rather
/// than stock - it carries no <c>WorkPiece</c>, does not re-enter the mill, and stacks.
/// See docs/design/items/rolled-parts.md.
/// <para>
/// Two products of the family are deliberately absent. The rod the mill re-rolls is vanilla's
/// <c>game:rod-{metal}</c> and the plate is <c>game:metalplate-{metal}</c>: both are exactly the section
/// the design asks for, and between them 30-odd call sites already name them, so minting ours would split
/// every consumer in two. <c>heavyplate</c> waits on M.8, which merges it with <c>castplate-heavy</c> onto
/// one metal axis - shipping a rolled one now is the duplication that stage exists to remove.
/// </para>
/// </summary>
public class RolledItemDefinitions : IExItemDefProvider {
  // Masses are the density rule (1 vx^3 = 2.5 u) applied to the drawn section, and every one is settled in
  // rolled-parts.md's catalogue. Declared here rather than derived from the art because the art rounds: the
  // rod's crop length is 10.125 and it is drawn at 10.
  //
  // 1x1x10 = 10 vx^3. Four off one rolled rod, which is 100 u - so the fork conserves exactly.
  private const int RivetRodUnits = 25;

  // 4x1x10 = 40 vx^3. The whole rolled rod taken flat, no crop, so it is the rod's mass unchanged.
  private const int NailPlateUnits = 100;

  // 4.5x2x18 = 162 vx^3, which is a shingled bar's whole volume: the flat 2.0 stage IS the beam, claimed
  // at the mill with no shear cut. Rounded to 400 exactly as the bar it comes from is (405 by the rule).
  private const int BeamUnits = 400;

  // 8x2x5 and 8x1x10 = 80 vx^3 each: the same metal at two gauges, one for machining and one for curling.
  private const int BlankUnits = 200;
  private const int SkelpUnits = 200;

  // 15x1x16 = 240 vx^3. The wide tier's 600 u quantum, which is what lets both slabs divide into it.
  private const int BoilerPlateUnits = 600;

  // 12x2x10 = 240 vx^3, the same 600 u at a heavier gauge: two off a shingled slab's last gap, and each
  // goes back through the rolls to become one boiler plate.
  private const int HeavyPlateUnits = 600;

  private const string RolledIron = "game:block/metal/sheet-plain/iron5";

  // The declared mass of every product, by code. Public so a ledger reads the number the item ships with
  // rather than a literal of its own - a mass that moves must move every assertion about it with it.
  private static readonly System.Collections.Generic.Dictionary<
    string,
    int
  > Units = new() {
    ["rivetrod"] = RivetRodUnits,
    ["nailplate"] = NailPlateUnits,
    ["beam"] = BeamUnits,
    ["blank"] = BlankUnits,
    ["skelp"] = SkelpUnits,
    ["heavyplate"] = HeavyPlateUnits,
    ["boilerplate"] = BoilerPlateUnits,
  };

  /// <summary>The metal a rolled product carries, or 0 for a code this mod rolls nothing for.</summary>
  public static int UnitsOf(string code) =>
    Units.TryGetValue(code, out int units) ? units : 0;

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      RivetRod(domain),
      NailPlate(domain),
      Beam(domain),
      Blank(domain),
      Skelp(domain),
      HeavyPlate(domain),
      BoilerPlate(domain),
    ];

  /// <summary>
  /// The rivet rod: the quarter-section blank the rivet machine cuts and upsets. Four come off a rolled rod
  /// taken down the grooved branch to 1.0, which is the round half of the rod fork.
  /// <para>
  /// Named for what it feeds, and it has to be: it is a quarter of vanilla's rod by section and by mass
  /// (1 × 1 × 10 at 25 u against 2 × 2 × 10 at 100 u), so a bare "rod" puts two items a player cannot tell
  /// apart beside each other in the handbook. Owner ruling, 2026-08-15.
  /// </para>
  /// </summary>
  private static ExItemDef RivetRod(string domain) =>
    Product(domain, "rivetrod", "iiex:item/rolled-rivetrod", RivetRodUnits);

  /// <summary>
  /// The strip the nail machine cuts into nails and strips. It is the flat half of the rod fork and the one
  /// whole-piece conversion in the design: a rolled rod taken flat to 1.0 is nail plate, with no crop.
  /// </summary>
  private static ExItemDef NailPlate(string domain) =>
    Product(domain, "nailplate", "iiex:item/rolled-nailplate", NailPlateUnits);

  /// <summary>
  /// The structural section fabricated frames are built from, with plate and rivets. Its shape file is a
  /// family file - it also carries the mill's flattened stages and another product - so only the
  /// <c>Beam</c> element is rendered.
  /// </summary>
  private static ExItemDef Beam(string domain) =>
    Product(domain, "beam", "iiex:item/rolled-beam", BeamUnits)
      .ShapeSelectiveElements("Beam");

  /// <summary>The wide 2.0 piece the boring machine machines into cranks and gear blanks.</summary>
  private static ExItemDef Blank(string domain) =>
    Product(domain, "blank", Placeholder, BlankUnits);

  /// <summary>The wide 1.0 piece the bending roller curls into pipe.</summary>
  private static ExItemDef Skelp(string domain) =>
    Product(domain, "skelp", Placeholder, SkelpUnits);

  /// <summary>
  /// The heavy plate: the wide tier's structural section, two off a shingled slab. M.8 is meant to merge
  /// this with the cast <c>castplate-heavy</c> onto one metal axis, so shipping the rolled one is the
  /// duplication that stage exists to remove - it is here because its route now exists and the slab dead-ends
  /// without it. Its shape file is the family the wide route draws, so only the finished element renders.
  /// </summary>
  private static ExItemDef HeavyPlate(string domain) =>
    Product(domain, "heavyplate", "iiex:forming/heavyplate", HeavyPlateUnits)
      .ShapeSelectiveElements("HeavyPlate1");

  /// <summary>The wide plate a boiler shell is built from, or the hammer stamps into three plates.</summary>
  private static ExItemDef BoilerPlate(string domain) =>
    Product(domain, "boilerplate", Placeholder, BoilerPlateUnits);

  // Art is drawn for three of the six; the rest take the shipped placeholder the roll sets already use,
  // so a product exists and is craftable before its shape is exported.
  private const string Placeholder = "game:item/ingot";

  // Every rolled product is the same kind of thing: wrought iron at a finished section, stackable, priced
  // in metal units for remelt and scrap.
  private static ExItemDef Product(
    string domain,
    string code,
    string shape,
    int units
  ) =>
    ExItemDef
      .Create(domain, code)
      .Shape(shape)
      .TextureAll(RolledIron)
      .MaxStackSize(16)
      .MaterialDensity(7850)
      .Attribute("materialUnits", units)
      .CreativeCommon("*");
}
