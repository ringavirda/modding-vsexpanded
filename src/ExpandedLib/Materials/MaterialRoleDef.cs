using System.Collections.Generic;

namespace ExpandedLib.Materials;

/// <summary>
/// One material-role assignment: the data a machine reads to classify a flux, fuel, ore, scrap or
/// charge item. Deserialized from every domain's <c>config/materialroles.json</c> and registered into
/// <see cref="MaterialRoleRegistry"/>, so a content mod gives an item a role by shipping or patching
/// one JSON entry rather than by recompiling.
/// <para>
/// A def matches an item by <see cref="Code"/> (exact, domain-normalised like
/// <see cref="Metals.MetalRegistry"/>) or by <see cref="PathPrefix"/> (a domain-blind
/// <c>Code.Path.StartsWith</c> test). At least one of the two must be set.
/// </para>
/// </summary>
public class MaterialRoleDef {
  /// <summary>The role this def grants, one of the <see cref="Roles"/> constants.</summary>
  public string Role { get; set; } = "";

  /// <summary>Exact item code to match ("game:lime"), domain-normalised. Null matches by
  /// <see cref="PathPrefix"/> only.</summary>
  public string? Code { get; set; }

  /// <summary>Matches any item whose <c>Code.Path</c> starts with this, domain-blind. Null matches by
  /// <see cref="Code"/> only.</summary>
  public string? PathPrefix { get; set; }

  /// <summary>Per-role scalar: for the fuel role, the carbon value of one item (coke 2, charcoal 0.5).
  /// Null makes <see cref="MaterialRoleRegistry.ValueOf"/> return the caller's fallback.</summary>
  public float? Value { get; set; }

  /// <summary>Mod id this assignment waits on, or null to apply always. A def naming a mod that is not
  /// loaded is skipped silently - an absent mod is the ordinary case, not a defect - which is what lets
  /// compatibility with another mod's ore, fuel or scrap ship as data rather than as a code contributor.
  /// See docs/design/mechanics/material-roles.md.</summary>
  public string? RequiresMod { get; set; }
}

/// <summary>The <c>config/materialroles.json</c> file shape: one <c>materials</c> array of role
/// entries, like <see cref="Fluids.LiquidCatalogue"/>.</summary>
public class MaterialRoleCatalogue {
  /// <summary>The role assignments this file contributes.</summary>
  public List<MaterialRoleDef>? Materials { get; set; }
}

/// <summary>
/// The canonical material-role tokens the machines classify by. Constants rather than an enum so the
/// JSON <see cref="MaterialRoleDef.Role"/> strings and the code call sites share one spelling, and a
/// mod can add a role by shipping the string alone.
/// </summary>
public static class Roles {
  /// <summary>Flux: lime and the like, for the mixer and hopper flux slot.</summary>
  public const string Flux = "flux";

  /// <summary>Fuel: a carbon reductant (coke, charcoal), carrying a per-item carbon
  /// <see cref="MaterialRoleDef.Value"/>.</summary>
  public const string Fuel = "fuel";

  /// <summary>Crushed iron ore: the ore-family primary charge (the <c>crushed-iron</c> prefix plus mod
  /// ores).</summary>
  public const string IronOre = "ironore";

  /// <summary>Scrap metal: vanilla iron bits, the remelt-family primary charge.</summary>
  public const string Scrap = "scrap";

  /// <summary>Prepared furnace charge: blast mix, the count-only shaft charge.</summary>
  public const string Charge = "charge";
}
