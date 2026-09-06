using System.Collections.Generic;

namespace ExpandedLib.Catalogues;

/// <summary>
/// One material-role assignment: the data a machine reads to classify a flux, fuel, ore, scrap or
/// charge item. Deserialized from every domain's <c>config/materialroles.json</c> and registered into
/// <see cref="MaterialRoleRegistry"/>, so a content mod gives an item a role by shipping or patching
/// one JSON entry rather than by recompiling.
/// <para>
/// A def matches an item by <see cref="Code"/> (exact, domain-normalised like the family layer's
/// <c>MetalRegistry</c>) or by <see cref="PathPrefix"/> (a domain-blind
/// <c>Code.Path.StartsWith</c> test). At least one of the two must be set.
/// </para>
/// </summary>
public class MaterialRoleDef {
  /// <summary>The role this def grants, one of the <c>Roles</c> constants in the family layer's
  /// <c>Materials</c> namespace.</summary>
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
/// entries, like <see cref="LiquidCatalogue"/>.</summary>
public class MaterialRoleCatalogue {
  /// <summary>The role assignments this file contributes.</summary>
  public List<MaterialRoleDef>? Materials { get; set; }
}
