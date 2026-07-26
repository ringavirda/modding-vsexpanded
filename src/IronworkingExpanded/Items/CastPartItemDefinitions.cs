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
/// <c>moltenbarrel-cast</c> once lined with fire clay (the vessel's version of "finish the casting").</item>
/// </list>
/// The machine-part blanks are added alongside their stations.
/// </summary>
public class CastPartItemDefinitions : IExItemDefProvider
{
  /// <summary>Units of metal a heavy cast plate is worth (= its cavity volume), for remelt/scrap maths.</summary>
  public const int HeavyPlateUnits = 160;

  /// <summary>Units of cast iron a cast-barrel blank is worth (= its casting capacity), for remelt/scrap.</summary>
  public const int CastBarrelUnits = 200;

  private const string CastIron = "iwex:block/metal/castiron";

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [HeavyPlate(domain), CastBarrel(domain)];

  private static ExItemDef HeavyPlate(string domain) =>
    ExItemDef
      .Create(domain, "castplate-heavy")
      // Placeholder art: the vanilla plate shape at cast-iron texture, scaled up. A dedicated heavy-plate
      // shape can replace this without touching anything else.
      .Shape("game:item/plate")
      .TextureAll(CastIron)
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
      // moltenbarrel-cast block (see MoltenRecipeDefinitions).
      .Shape("iwex:item/cast-barrel")
      .MaxStackSize(8)
      .MaterialDensity(7200)
      .CombustibleProps(new { meltingPoint = 1150 })
      .Attribute("materialUnits", CastBarrelUnits)
      .CreativeCommon("*");
}
