using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.Items;

/// <summary>
/// Code-first itemtypes for the plain cast-iron parts that come straight out of a sand casting cell,
/// finished on-cast with no machining: the heavy cast plate, a thick structural blank distinct from the
/// thin <c>metalplate-castiron</c> the mold pedestal casts; the cast barrel blank (<c>cast-barrel</c>), a
/// cored unlined vessel that becomes a <c>molten-barrel-cast</c> once lined with fire clay; the cast wheel
/// section, a rim arc, four to a flywheel's rim and eight to the large wheel's; and the cast shell segment,
/// the generic enclosure body. The machined blanks (cylinder, gear blanks, axle) arrive with the boring
/// machine. These parts are iwex's and lpex consumes them; <c>castframe</c> is lpex's and has no row here.
/// See <c>docs/design/items/cast-parts.md</c>.
/// </summary>
public class CastPartItemDefinitions : IExItemDefProvider {
  /// <summary>Units of metal a heavy cast plate is worth (= its cavity volume), for remelt/scrap maths.</summary>
  public const int HeavyPlateUnits = 160;

  /// <summary>Units of cast iron a cast-barrel blank is worth (= its casting capacity), for remelt/scrap.</summary>
  public const int CastBarrelUnits = 200;

  /// <summary>
  /// Units of cast iron in one wheel section: the wide tier's 600 u quantum, and one billet of cast stock,
  /// so a flywheel is four whole billets and a large one eight. Declared rather than derived from the art
  /// (216 vx³, which the density rule prices at 540) so it matches the fabricated rim pinned at 600 u in
  /// <c>docs/design/processes/bending.md</c> and neither route is cheaper in metal.
  /// </summary>
  public const int CastWheelSectionUnits = 600;

  /// <summary>
  /// Units of cast iron in one shell segment: the same 600 u quantum as
  /// <see cref="CastWheelSectionUnits"/>. Drawn at 216 vx³, which the density rule (2.5 u/vx³) prices at
  /// 540, but 600 matches the fabricated shell pinned in <c>docs/design/processes/bending.md</c>.
  /// </summary>
  public const int CastShellUnits = 600;

  private const string CastIron = "iwex:block/metal/castiron";

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      HeavyPlate(domain),
      CastBarrel(domain),
      CastWheelSection(domain),
      CastShell(domain),
    ];

  /// <summary>
  /// The shell segment: three bent panels cast as one piece, the enclosure body a skinned machine is built
  /// around. It names no machine, so the ladle, a tank, a crusher casing and an engine housing all spend
  /// the same part.
  /// </summary>
  private static ExItemDef CastShell(string domain) =>
    ExItemDef
      .Create(domain, "castshell")
      .Shape("iwex:item/castshell")
      .MaxStackSize(16)
      .MaterialDensity(7200)
      .CombustibleProps(new { meltingPoint = 1150 })
      .Attribute("materialUnits", CastShellUnits)
      .CreativeCommon("*");

  private static ExItemDef HeavyPlate(string domain) =>
    ExItemDef
      .Create(domain, "castplate-heavy")
      // Its own authored shape, carrying its own cast-iron texture: a thick cast slab, visibly heavier
      // than the thin `metalplate-castiron` the mold pedestal casts.
      .Shape("iwex:item/heavyplate")
      .MaxStackSize(16)
      .MaterialDensity(7200)
      .CombustibleProps(new { meltingPoint = 1150 })
      .Attribute("materialUnits", HeavyPlateUnits)
      .GuiTransform(new { scale = 1.3 })
      .CreativeCommon("*");

  private static ExItemDef CastBarrel(string domain) =>
    ExItemDef
      .Create(domain, "cast-barrel")
      // Self-contained shape (its own cast-iron texture); the blank is lined with fire clay to become the
      // molten-barrel-cast block (see MoltenRecipeDefinitions).
      .Shape("iwex:item/cast-barrel")
      .MaxStackSize(8)
      .MaterialDensity(7200)
      .CombustibleProps(new { meltingPoint = 1150 })
      .Attribute("materialUnits", CastBarrelUnits)
      .CreativeCommon("*");

  /// <summary>
  /// The wheel section: one arc of a rim. Four make a flywheel - see
  /// <see cref="Recipes.Grid.EnergyRecipeDefinitions"/>.
  /// </summary>
  private static ExItemDef CastWheelSection(string domain) =>
    ExItemDef
      .Create(domain, "castwheelsection")
      .Shape("iwex:item/castwheelsection")
      .MaxStackSize(16)
      .MaterialDensity(7200)
      .CombustibleProps(new { meltingPoint = 1150 })
      .Attribute("materialUnits", CastWheelSectionUnits)
      .CreativeCommon("*");
}
