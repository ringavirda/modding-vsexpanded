using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.Items;

/// <summary>
/// Code-first itemtypes for the plain cast-iron parts that come straight out of a sand casting cell,
/// finished on-cast (no machining). Currently:
/// <list type="bullet">
/// <item>the <b>heavy cast plate</b> - a thick structural blank distinct from the thin
/// <c>metalplate-castiron</c> the mold pedestal casts - used as puddling-hearth plating, furnace
/// door/frame stock, and boring-machine blank stock;</item>
/// <item>the <b>cast barrel blank</b> (<c>cast-barrel</c>) - a cored, <i>unlined</i> vessel that becomes a
/// <c>molten-barrel-cast</c> once lined with fire clay (the vessel's version of "finish the casting").</item>
/// <item>the <b>cast wheel section</b> - a rim arc; four make a flywheel's rim, eight the large wheel's.</item>
/// </list>
/// The machined blanks (cylinder, gear blanks, axle) are added alongside the boring machine.
/// <para>
/// <b>Ownership.</b>
/// <c>castwheelsection</c> is <b>iwex's</b>, because iwex's flywheel consumes it. <c>castshell</c> is
/// <b>iwex's too</b>: the <b>ladle</b> is built from it, and the ladle is iwex's as the static canal
/// merger that gates every alloy in the mod.
/// <para>
/// The shape is <b>iwex owns it, lpex consumes it</b> (water tank, ore crusher, engines), which is the
/// normal dependency direction: no cross-mod pattern indirection is needed at all, because the pattern
/// and the part live in the same mod.
/// </para>
/// <para>
/// <c>castframe</c> is lpex's and its existence is still open.
/// </para>
/// </summary>
public class CastPartItemDefinitions : IExItemDefProvider
{
  /// <summary>Units of metal a heavy cast plate is worth (= its cavity volume), for remelt/scrap maths.</summary>
  public const int HeavyPlateUnits = 160;

  /// <summary>Units of cast iron a cast-barrel blank is worth (= its casting capacity), for remelt/scrap.</summary>
  public const int CastBarrelUnits = 200;

  /// <summary>
  /// Units of cast iron in one wheel section - the wide tier's 600 u quantum, the same number lpex's cast
  /// shell carries.
  /// <para>
  /// <b>600 is a declared mass, not a measured one</b>, on the rule
  /// <see cref="Items.CastStockItemDefinitions"/> states: the density rule sizes art <em>plausibly</em>, the
  /// mass is chosen so the economy divides. The part is drawn at 216 vx³, which the rule would price at
  /// 540 - close enough that the art needs no redraw, and 540 is what the "Rule says" column of
  /// <c>docs/design/items/cast-parts.md</c> records: a derivation, not an adopted mass.
  /// </para>
  /// <para>
  /// <b>Why 600 and not 540.</b> Every cast structural part has a rolled/fabricated
  /// equivalent reached by a dual RCC path, and the two are <b>alternatives, not tiers</b>. The
  /// fabricated halves are already pinned at 600 u apiece in <c>docs/design/processes/bending.md</c>
  /// (a <c>heavyplate</c> 12 × 2 × 10 bent into a rim; a <c>boilerplate</c> 15 × 1 × 16 bent into a shell).
  /// At 540 the cast route would be quietly 10% cheaper in metal and the choice would stop being a choice.
  /// At 600 the two routes cost the same iron and differ only in the <em>plant</em> they demand - cupola and
  /// pattern versus mill, roller and rivets - which is the mod's core trade stated in one number.
  /// </para>
  /// <para>
  /// It also lands on the cast-stock ladder's quantum, so one wheel section is exactly one billet of
  /// metal - which is what makes remelt-and-recast arithmetic legible to a player. A flywheel is four of
  /// them, a large one eight: whole billets both ways, with no remainder to explain.
  /// </para>
  /// </summary>
  public const int CastWheelSectionUnits = 600;

  /// <summary>
  /// Units of cast iron in one shell segment - the wide tier's 600 u quantum, the same number
  /// <see cref="CastWheelSectionUnits"/> carries, and for the same reason.
  /// <para>
  /// <b>Why 600 and not the rule's 540.</b> The shell is drawn at 216 vx³, which the density rule (2.5
  /// u/vx³) would price at 540. But every cast structural part has a rolled/fabricated
  /// equivalent and the two are <b>alternatives, not tiers</b> - and the fabricated shell is already
  /// pinned at 600 u in <c>docs/design/processes/bending.md</c>. At 540 the cast route would be quietly 10%
  /// cheaper in metal and the choice would stop being a choice; at 600 the two cost the same iron and differ
  /// only in the <em>plant</em> they demand.
  /// </para>
  /// </summary>
  public const int CastShellUnits = 600;

  private const string CastIron = "iwex:block/metal/castiron";

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [HeavyPlate(domain), CastBarrel(domain), CastWheelSection(domain), CastShell(domain)];

  /// <summary>
  /// The shell segment: three bent panels cast as one piece, the enclosure body a skinned machine is built
  /// around. Deliberately <b>generic</b> - it names no machine, so the ladle, a tank, a crusher casing and an
  /// engine housing all spend the same part rather than each inventing its own.
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
      // Its own authored shape - a thick cast slab, visibly heavier than the thin `metalplate-castiron` the
      // mold pedestal casts. The shape carries its own cast-iron texture.
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
  /// The wheel section: one arc of a rim. <b>Four make a flywheel</b> - see
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
