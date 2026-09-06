namespace ExpandedLib.Industry.Materials;

/// <summary>
/// The canonical material-role tokens the machines classify by. Constants rather than an enum so the
/// JSON <see cref="ExpandedLib.Catalogues.MaterialRoleDef.Role"/> strings and the code call sites share
/// one spelling, and a mod can add a role by shipping the string alone.
/// </summary>
public static class Roles {
  /// <summary>Flux: lime and the like, for the mixer and hopper flux slot.</summary>
  public const string Flux = "flux";

  /// <summary>Fuel: a carbon reductant (coke, charcoal), carrying a per-item carbon
  /// <see cref="ExpandedLib.Catalogues.MaterialRoleDef.Value"/>.</summary>
  public const string Fuel = "fuel";

  /// <summary>Crushed iron ore: the ore-family primary charge (the <c>crushed-iron</c> prefix plus mod
  /// ores).</summary>
  public const string IronOre = "ironore";

  /// <summary>Scrap metal: vanilla iron bits, the remelt-family primary charge.</summary>
  public const string Scrap = "scrap";

  /// <summary>Prepared furnace charge: blast mix, the count-only shaft charge.</summary>
  public const string Charge = "charge";
}
